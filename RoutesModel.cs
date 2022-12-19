using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETAPredictor
{
    public class RoutesModel
    {
        
        private List<StopNode> _stops = new List<StopNode>();
        private List<RouteEdge> _routes = new List<RouteEdge>();

        private List<GPSData> _totalData = new List<GPSData>();
        private Dictionary<string, GPSData> _RecentPositions = new Dictionary<string, GPSData>();

        //with simulation mode we have fake current time
        private bool _simulationMode = false;
        private DateTime _simulationTime = DateTime.UtcNow;

        public DateTime CurrentTime
        {
            get
            {
                if (_simulationMode) { return _simulationTime; }
                return DateTime.UtcNow;
            }

            set
            {
                if (!_simulationMode)
                {
                    throw new InvalidOperationException("Cannot set current time without enabling simulation mode on Model construction.");
                }
                _simulationTime = value;
            }
        }

        public RoutesModel(List<GPSData> Stops, bool SimulationMode)
        {
            _simulationMode = SimulationMode;

            //populate stops
            Stops.ForEach(s => _stops.Add(new StopNode(s)));
        }

        public void IntegrateDataPointIntoModel(GPSData dataPoint)
        {
            //we assume these are fed to us in order
            _totalData.Add(dataPoint);

            //update recent positions
            _RecentPositions[dataPoint.Serial] = dataPoint;

            //First Step:
            //is this point a stop? Label it, add to stopNode object, associate the stopNode Object

            //first we get the nearest stop
            var NearestStop = _stops.OrderByDescending(s => GeoHelper.GetDistanceBetweenInMeters(dataPoint, s.Data)).First();

            //if we are within 60 meters, it's a stop
            if (GeoHelper.GetDistanceBetweenInMeters(dataPoint, NearestStop.Data) < 60)
            {
                NearestStop.AssociatedData.Add(dataPoint);
                dataPoint.AssociatedStopNode = NearestStop;
                dataPoint.IdentifiedAsStopName = NearestStop.Data.IdentifiedAsStopName;

                //Have we just arrived at a stop? Is the previous item not a stop?
                if (!IsPreviousItemStop(dataPoint.Serial))
                {
                    //can we find a previous stop for this serial?
                    var lastStop = GetLastStopBeforeCurrent(dataPoint.Serial);
                    if (lastStop != null)
                    {
                        //Yes! We may have a new edge to build into our model, let's check.
                        var edge = _routes.Where(r => r.ToNode == NearestStop && r.FromNode == lastStop.AssociatedStopNode).FirstOrDefault();
                        if (edge == null)
                        {
                            edge = new RouteEdge();
                            edge.FromNode = lastStop.AssociatedStopNode;
                            edge.ToNode = NearestStop;
                            edge.CurrentTimeOnRoute = dataPoint.Time - lastStop.Time;
                            //edge.AverageTimeOnRoute = dataPoint.Time - lastStop.Time;

                            
                            _routes.Add(edge);
                        }
                        else
                        {
                            //update time on route
                            edge.CurrentTimeOnRoute = dataPoint.Time - lastStop.Time;
                            //edge.AverageTimeOnRoute = dataPoint.Time - lastStop.Time;
                            //TODO: set average time
                        }

                        //Associate previous data points from this serial to this edge...
                        _totalData.Take(_totalData.Count - 1).Where(d => d.Serial == dataPoint.Serial).OrderByDescending(d => d.Time).TakeWhile(d => d.IdentifiedAsStopName == null).ToList().ForEach(d =>
                        {
                            d.AssociatedRouteEdge = edge;
                        });
                    }
                }
            }
            else //we are not at a stop...
            {
                //Are we just leaving a stop? Is the previous item a stop?
                if (IsPreviousItemStop(dataPoint.Serial))
                {
                    //Update stop timing
                    var exitingStop = GetLastStopBeforeCurrent(dataPoint.Serial);
                    var arrivingStop = GetArrivalAtStopBeforeCurrent(dataPoint.Serial);
                    //maybe we started tracking within the stop and never previously arrived while tracking...
                    if (arrivingStop != null)
                    {
                        exitingStop.AssociatedStopNode.CurrentTimeAtStop = exitingStop.Time - arrivingStop.Time;
                    }
                    //TODO: set average time
                }

            }

        }

        private bool IsPreviousItemStop(string serial)
        {
            var items = _totalData.Where(s => s.Serial == serial).OrderBy(s => s.Time).ToList();
            if (items.Count <= 1)
            {
                return false;
            }
           
            return items[items.Count - 2].AssociatedStopNode != null;
        }

        private GPSData GetLastStopBeforeCurrent(string serial)
        {
            var items = _totalData.Where(s => s.Serial == serial).OrderByDescending(s => s.Time).ToList();
            for (int i = 1; i < items.Count; i++)
            {
                if (items[i].AssociatedStopNode != null)
                {
                    return items[i];
                }
            }
            return null;
        }

        private GPSData GetArrivalAtStopBeforeCurrent(string serial)
        {
            var items = _totalData.Where(s => s.Serial == serial).OrderByDescending(s => s.Time).ToList();
            var lookingForExit = false;
            for (int i = 1; i < items.Count; i++)
            {
                if (lookingForExit)
                {
                    if (items[i].AssociatedStopNode == null)
                    {
                        //return the previous ping
                        return items[i - 1];
                    }
                }
                else
                {
                    if (items[i].AssociatedStopNode != null)
                    {
                        lookingForExit = true;
                    }
                }
            }
            return null;
        }

        public ETATable GetETATable()
        {
            PruneRecentPositions();
            ETATable table = new ETATable();
            _stops.Select(s => new ETATableEntry() { StopName = s.Data.IdentifiedAsStopName, ETA = GetETAForStop(s.Data) ?? TimeSpan.MaxValue }).ToList().ForEach(i => table.Add(i));
            return table;
        }

        //Get the ETA to a stop, where stop represents the stop itself...
        private TimeSpan? GetETAForStop(GPSData stop)
        {
            //try once with GeoHelper.GetEstimatedPosition, if it doesn't work we'll use the last known good position
            var returnValue =  _RecentPositions.Keys.ToList().Select(s => GetETAForStopAndBus(stop, GeoHelper.GetEstimatedPosition(_RecentPositions[s], CurrentTime - _RecentPositions[s].Time))).OrderBy(x => x).FirstOrDefault();
            if(returnValue == null || returnValue == TimeSpan.MaxValue)
            {
                returnValue = _RecentPositions.Keys.ToList().Select(s => GetETAForStopAndBus(stop, _RecentPositions[s])).OrderBy(x => x).FirstOrDefault();
            }
            return returnValue;
        }

        private void PruneRecentPositions()
        {
            _RecentPositions.Keys.ToList().ForEach(x => { if (_RecentPositions[x].Time < CurrentTime.AddMinutes(-3)) { _RecentPositions.Remove(x); } });
        }

        private TimeSpan? GetETAForStopAndBus(GPSData stop, GPSData busPosition)
        {
            //if we are at the stop return minvalue
            if(GeoHelper.GetDistanceBetweenInMeters(stop, busPosition) < 60)
            {
                return TimeSpan.MinValue;
            }

            //find nearest locations in history
            var examples = _totalData.Where(d => d.IdentifiedAsStopName == null && GeoHelper.GetDistanceBetweenInMeters(d, busPosition) < 20 && GeoHelper.GetAngularDistanceBetweenHeadings(d, busPosition) < 90);

            if (examples.Count() < 2)
            {
                examples = _totalData.Where(d => d.IdentifiedAsStopName == null && GeoHelper.GetDistanceBetweenInMeters(d, busPosition) < 50 && GeoHelper.GetAngularDistanceBetweenHeadings(d, busPosition) < 90);
            }

            if (examples.Count() < 2)
            {
                examples = _totalData.Where(d => d.IdentifiedAsStopName == null && GeoHelper.GetDistanceBetweenInMeters(d, busPosition) < 100 && GeoHelper.GetAngularDistanceBetweenHeadings(d, busPosition) < 90);
            }


            examples = examples.ToList();

            if (examples.Count() > 0)
            {
                //are multiple edges invoked here?
                var edges = examples.Select(i => i.AssociatedRouteEdge).Where(i => i!= null).Distinct().ToList();
                if (edges.Count() == 0)
                {
                    return TimeSpan.MaxValue; //we have no idea
                }
                if (edges.Count() > 1)
                {
                    //yes? ambiguity, deal with it
                    //pick the longer of the possibilities to err on side of pleasant surprise that your bus is early
                    List<double> AverageETAs = new List<double>();
                    //TODO: the expression below is wrong.... both edges could be pointing at different stops so this doesn't work...
                    double avg = edges.Select(edge => examples.Where(i => i.AssociatedRouteEdge == edge).Select(i => HistoricalTimeToStop(i, stop)).Where(ts => ts != TimeSpan.MaxValue).Average(ts => (double)ts.Ticks)).OrderByDescending(t => t).FirstOrDefault();
                    return new TimeSpan((long)avg);
                    //TODO: take into account recency
                }
                else
                {
                    //no? historical avg then
                    //average out their ETA to next stop
                    double avg = examples.Select(i => HistoricalTimeToStop(i, stop)).Where(ts => ts != TimeSpan.MaxValue).Average(ts => (double)ts.Ticks);
                    //but... let's take into account recent times, unless we don't have any 'cause this bus just got going
                    //TODO: take into account recency
                    return new TimeSpan((long)avg);
                }

            }
            else
            {
                return TimeSpan.MaxValue; //we have no idea
            }
        }

        //Given a historical example, loop forward in time until you get to the stop that matches what you are looking for
        //then return the timespan between them, giving us a historical ETA
        private TimeSpan HistoricalTimeToStop(GPSData example, GPSData stop)
        {
            var index = _totalData.IndexOf(example);
            while (index < _totalData.Count)
            {
                var item = _totalData[index];
                //we compare the associated stop node's data node with stop
                //and not the item directly. We could also match on StopName
                if (item.Serial == example.Serial && item.AssociatedStopNode?.Data == stop) 
                {
                    return _totalData[index].Time - example.Time;
                }
                index++;
            }

            return TimeSpan.MaxValue;
        }


    }
}
