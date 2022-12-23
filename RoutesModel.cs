using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ETAPredictor
{
    public class RoutesModel
    {
        
        private List<StopNode> _stops = new List<StopNode>();
        private List<RouteEdge> _routes = new List<RouteEdge>();

        private Dictionary<string, List<GPSData>> _gpsDataBySerial = new Dictionary<string, List<GPSData>>();
        private Dictionary<string, GPSData> _recentPositionsBySerial = new Dictionary<string, GPSData>();

        //with simulation mode we have fake current time
        private bool _simulationMode = false;
        private DateTime _simulationTime = DateTime.UtcNow;

        private int _garbageCollectionCounter = 0;
        private long _totalDataPoints = 0;

        public string SaveToJSON() {
            _stops.Select(s => s.Data).ToList();
            _gpsDataBySerial.Keys.ToList().SelectMany(key => _gpsDataBySerial[key]).OrderBy(item => item.Time).ToList();
            string serialized = JsonSerializer.Serialize(new RoutesModelFileFormat()
            {
                Stops = _stops.Select(s => s.Data).ToList(),
                Data = _gpsDataBySerial.Keys.ToList().SelectMany(key => _gpsDataBySerial[key]).OrderBy(item => item.Time).ToList()
            });
            return serialized;
        }

        public static RoutesModel LoadFromJSON(string Model, bool SimulationMode)
        {
            var fileFormat = JsonSerializer.Deserialize<RoutesModelFileFormat>(Model);

            RoutesModel newModel = new RoutesModel(fileFormat.Stops, SimulationMode);
            if (SimulationMode)
            {
                fileFormat.Data.ForEach(data => { 
                    newModel.CurrentTime = data.Time; 
                    newModel.IntegrateDataPointIntoModel(data); //note: garbage collection can happen here.... so if we are simulation mode we set the time, it's up to whoever loads from JSON to set the time appropriately if simulating to prevent garbage collection from eating data.
                }); 
            }
            else
            {
                fileFormat.Data.ForEach(data => newModel.IntegrateDataPointIntoModel(data)); //note: garbage collection can happen here.... so if we are simulation mode we set the time, it's up to whoever loads from JSON to set the time appropriately if simulating to prevent garbage collection from eating data.
            }

            return newModel;

        }

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

        private void EnsureGPSSerialList(string serial)
        {
            if (_gpsDataBySerial.ContainsKey(serial))
            {
                return;
            }
            else
            {
                _gpsDataBySerial.Add(serial,new List<GPSData>());
            }
        }

        public void IntegrateDataPointIntoModel(GPSData dataPoint)
        {
            _totalDataPoints++;
            _garbageCollectionCounter++;

            if(_totalDataPoints > 150000 && _garbageCollectionCounter > 5000)
            {
                CollectGarbage();
                _garbageCollectionCounter = 0;
            }

            EnsureGPSSerialList(dataPoint.Serial);
            //we assume these are fed to us in order
            _gpsDataBySerial[dataPoint.Serial].Add(dataPoint);

            //update recent positions
            _recentPositionsBySerial[dataPoint.Serial] = dataPoint;

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
                            _routes.Add(edge);
                        }

                        //update time on route
                        edge.IntegrateTimeOnRoute(dataPoint.Time - lastStop.Time);
                        

                        //Associate previous data points from this serial to this edge...
                        _gpsDataBySerial[dataPoint.Serial].Take(_gpsDataBySerial[dataPoint.Serial].Count - 1).OrderByDescending(d => d.Time).TakeWhile(d => d.IdentifiedAsStopName == null).ToList().ForEach(d =>
                        {
                            d.AssociatedRouteEdge = edge;
                            edge.AssociatedData.Add(d);
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

        private void CollectGarbage()
        {
            var expiration = CurrentTime.AddDays(-5);
            //we want to remove data beyond a certain length of time, just to conserve memory. The model will basically evolve as needed over time, will be saved and rehydrated as services restart and scale, etc...
            //so we shouldn't rely on a particular expiration windowWe probably should count backwards until we get to maybe 10K*N(serial) entries, but it's tricky because of different buckets per serial and having a consistent cutoff time is probably good. Keep in mind, 20 minutes of driving a single vehicle produces about 200 entries. 10K is like 50 hours of bus history
            //worst case the model rebuilds itself if it's loaded after a long time of dormancy
            _gpsDataBySerial.Keys.ToList().ForEach(key => {
                var list = _gpsDataBySerial[key];
                _totalDataPoints -= list.RemoveAll(item => item.Time < expiration);
            });

            //remove the data from _stops and _edges
            _stops.ForEach(stop => stop.AssociatedData.RemoveAll(item => item.Time < expiration));
            _routes.ForEach(route => route.AssociatedData.RemoveAll(route => route.Time < expiration)); 

        }

        private bool IsPreviousItemStop(string serial)
        {
            var items = _gpsDataBySerial[serial];
            if (items.Count <= 1)
            {
                return false;
            }
           
            return items[items.Count - 2].AssociatedStopNode != null;
        }

        private GPSData GetLastStopBeforeCurrent(string serial)
        {
            var items = _gpsDataBySerial[serial];
            for (int i = items.Count - 1; i >= 0; i--)
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
            var items = _gpsDataBySerial[serial];
            var lookingForExit = false;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (lookingForExit)
                {
                    if (items[i].AssociatedStopNode == null)
                    {
                        //return the previous ping
                        return items[i + 1];
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

        public ICollection<ETATableEntry> GetETATable()
        {
            PruneRecentPositions();
            ETATable table = new ETATable();
            _stops.Select(s => new ETATableEntry() { StopName = s.Data.IdentifiedAsStopName, ETA = GetETAForStop(s.Data) ?? TimeSpan.MaxValue }).ToList().ForEach(i => table.Add(i));
            return table;
        }

        public IDictionary<string, GPSData> GetRecentPositions()
        {
            PruneRecentPositions();
            return _recentPositionsBySerial;            
        }

        //Get the ETA to a stop, where stop represents the stop itself...
        private TimeSpan? GetETAForStop(GPSData stop)
        {
            //try once with GeoHelper.GetEstimatedPosition, if it doesn't work we'll use the last known good position
            //var returnValue = _recentPositionsBySerial.Keys.ToList().Select(s => GetETAForStopAndBus(stop, GeoHelper.GetEstimatedPosition(_recentPositionsBySerial[s], CurrentTime - _recentPositionsBySerial[s].Time))).OrderBy(x => x).FirstOrDefault();
            TimeSpan? returnValue = null;
            if(returnValue == null || returnValue == TimeSpan.MaxValue)
            {
                returnValue = _recentPositionsBySerial.Keys.ToList().Select(s => GetETAForStopAndBus(stop, _recentPositionsBySerial[s])).OrderBy(x => x).FirstOrDefault();
            }
            return returnValue;
        }

        private void PruneRecentPositions()
        {
            _recentPositionsBySerial.Keys.ToList().ForEach(x => { if (_recentPositionsBySerial[x].Time < CurrentTime.AddMinutes(-3)) { _recentPositionsBySerial.Remove(x); } });
        }

        private TimeSpan? GetETAForStopAndBus(GPSData stop, GPSData busPosition)
        {
            //if we are at the stop return minvalue
            if(GeoHelper.GetDistanceBetweenInMeters(stop, busPosition) < 60)
            {
                return TimeSpan.MinValue;
            }

            //find nearest locations in history, and not recent history (more than 5 minutes old)
            var examples = _gpsDataBySerial[busPosition.Serial].Where(d => d.Time < CurrentTime.AddMinutes(-5) &&  d.IdentifiedAsStopName == null && GeoHelper.GetDistanceBetweenInMeters(d, busPosition) < 20 && GeoHelper.GetAngularDistanceBetweenHeadings(d, busPosition) < 90);

            if (examples.Count() < 2)
            {
                examples = _gpsDataBySerial[busPosition.Serial].Where(d => d.Time < CurrentTime.AddMinutes(-5) && d.IdentifiedAsStopName == null && GeoHelper.GetDistanceBetweenInMeters(d, busPosition) < 50 && GeoHelper.GetAngularDistanceBetweenHeadings(d, busPosition) < 90);
            }

            if (examples.Count() < 2)
            {
                examples = _gpsDataBySerial[busPosition.Serial].Where(d => d.Time < CurrentTime.AddMinutes(-5) && d.IdentifiedAsStopName == null && GeoHelper.GetDistanceBetweenInMeters(d, busPosition) < 100 && GeoHelper.GetAngularDistanceBetweenHeadings(d, busPosition) < 90);
            }

            if(examples.Count() == 0)
            {
                //we've got to look at other vehicles to get an idea of what may be going on here...
                _gpsDataBySerial.Keys.SelectMany(k => _gpsDataBySerial[k]).Where(d => d.Time < CurrentTime.AddMinutes(-5) && d.IdentifiedAsStopName == null && GeoHelper.GetDistanceBetweenInMeters(d, busPosition) < 20 && GeoHelper.GetAngularDistanceBetweenHeadings(d, busPosition) < 90);

                if (examples.Count() < 2)
                {
                    examples = _gpsDataBySerial.Keys.SelectMany(k => _gpsDataBySerial[k]).Where(d => d.Time < CurrentTime.AddMinutes(-5) && d.IdentifiedAsStopName == null && GeoHelper.GetDistanceBetweenInMeters(d, busPosition) < 50 && GeoHelper.GetAngularDistanceBetweenHeadings(d, busPosition) < 90);
                }

                if (examples.Count() < 2)
                {
                    examples = _gpsDataBySerial.Keys.SelectMany(k => _gpsDataBySerial[k]).Where(d => d.Time < CurrentTime.AddMinutes(-5) && d.IdentifiedAsStopName == null && GeoHelper.GetDistanceBetweenInMeters(d, busPosition) < 100 && GeoHelper.GetAngularDistanceBetweenHeadings(d, busPosition) < 90);
                }
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
                    //what does it mean to be here?
                    //it means that we have examples of busses (this bus likely) going to different places from here... more than one edge.
                    //ambiguity, deal with it
                    List<double> AverageETAs = new List<double>();

                    //we take the longest of most recent times and use that
                    double avg = edges.Select(edge => examples.Where(i => i.AssociatedRouteEdge == edge).Select(i => new { item = i, time = HistoricalTimeToStop(i, stop) }).Where(ts => ts.time != TimeSpan.MaxValue).OrderByDescending(i => i.item.Time).First().time.Ticks).OrderByDescending(t => t).FirstOrDefault();
                    return new TimeSpan((long)avg);
                    //TODO: take into account recency


                }
                else
                {

                    var bestGuess = TimeSpan.MaxValue;
                    //but... let's take into account recent times, unless we don't have any 'cause this bus just got going, we only want the 4 most recent examples if we end up averaging anyway...
                    var recentItems = examples.OrderByDescending(e => e.Time).Where(i => HistoricalTimeToStop(i, stop) != TimeSpan.MaxValue).Take(4);

                    //if we have recent data that isn't too old, use that....
                    if (recentItems.Count() > 0 && recentItems.First().Time > this.CurrentTime.AddHours(-2)) {
                        var lastTimeItTook = recentItems.Select(i => HistoricalTimeToStop(i, stop)).FirstOrDefault();

                        if (lastTimeItTook != null)
                        {
                            bestGuess = lastTimeItTook;
                        }
                    }

                            
                    //average ETA to next stop
                    double avg = recentItems.Select(i => HistoricalTimeToStop(i, stop)).Average(ts => (double)ts.Ticks);
                    TimeSpan averageTime = new TimeSpan((long)avg);

                    if (bestGuess == TimeSpan.MaxValue)
                    {
                        //no? historical avg then
                        bestGuess = averageTime;
                    }


                    //so now we hopefully have a best guess, but how are the last X GPSData compared to *historical* (more than 15 mins ago) speed?
                    var curAvg = GetAverageSpeedOnEdge(busPosition);

                    var totalExamples = new List<GPSData>();
                    _gpsDataBySerial.Keys.ToList().ForEach(key => {
                        totalExamples.AddRange(_gpsDataBySerial[key].Where(d => d.Time < busPosition.Time.AddMinutes(-15) && d.IdentifiedAsStopName == null && GeoHelper.GetDistanceBetweenInMeters(d, busPosition) < 100 && GeoHelper.GetAngularDistanceBetweenHeadings(d, busPosition) < 90));
                    });
                    if(totalExamples.Count == 0)
                    {
                        //slightly expand the search
                        _gpsDataBySerial.Keys.ToList().ForEach(key => {
                            totalExamples.AddRange(_gpsDataBySerial[key].Where(d => d.Time < busPosition.Time.AddMinutes(-15) && d.IdentifiedAsStopName == null && GeoHelper.GetDistanceBetweenInMeters(d, busPosition) < 150 && GeoHelper.GetAngularDistanceBetweenHeadings(d, busPosition) < 135));
                        });
                    }
                    var samplesAverageItems = totalExamples.Select(ex => GetAverageSpeedOnEdge(ex)).Where(time => time != -1);

                    if (samplesAverageItems.Any())
                    {
                        var samplesAverage = samplesAverageItems.Average();

                        //are we fast or slow? 8MPH more than avg?
                        if (Math.Abs(curAvg - samplesAverage) > 5 )
                        {
                            var scale = curAvg / samplesAverage;
                            bestGuess = bestGuess / scale;
                        }
                    }

                    return bestGuess;

                }

            }
            else
            {
                return TimeSpan.MaxValue; //we have no idea
            }
        }

        private double GetAverageSpeedOnEdge(GPSData data)
        {
            if(data.MemoizedAverageSpeedOnEdgeToThisPoint != -1)
            {
                return data.MemoizedAverageSpeedOnEdgeToThisPoint;
            }

            var items = _gpsDataBySerial[data.Serial];
            //.OrderByDescending(busPosition => busPosition.Time).SkipWhile(item => item != data).TakeWhile(busPosition => busPosition.AssociatedStopNode != null);
            List<GPSData> dataPoints = new List<GPSData>();
            bool found = false;
            for(int i = items.Count - 1; i >= 0; i--)
            {
                if(items[i] == data)
                {
                    found = true;
                    if(i < 20)
                    {
                        //we have fewer than 20 pings, not enough to accurately gauge traffic on this edge
                        return -1;
                    }
                }
                if (found)
                {
                    if(items[i].AssociatedStopNode == null)
                    {
                        if (items[i].Speed > 3) //we want to avoid tracking stops at lights and low speed during acceleration, limit avg to above 3mph data points
                        {
                            dataPoints.Add(items[i]);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            
            if (dataPoints.Any())
            {
                data.MemoizedAverageSpeedOnEdgeToThisPoint = dataPoints.Average(i => i.Speed);
                return data.MemoizedAverageSpeedOnEdgeToThisPoint;
            }
            else
            {
                return -1;
            }
        }

        //Given a historical example, loop forward in time until you get to the stop that matches what you are looking for
        //then return the timespan between them, giving us a historical ETA
        //give up with Max Timespan after X attempts
        private TimeSpan HistoricalTimeToStop(GPSData example, GPSData stop)
        {
            if (example.MemoizedTimesToStops.ContainsKey(stop.IdentifiedAsStopName))
            {
                return example.MemoizedTimesToStops[stop.IdentifiedAsStopName];
            }
            var items = _gpsDataBySerial[example.Serial];
            var index = items.IndexOf(example);
            while (index < items.Count)
            {
                var item = items[index];
                //we compare the associated stop node's data node with stop
                //and not the item directly. We could also match on StopName
                if (item.AssociatedStopNode?.Data == stop) 
                {
                    var timeToGetToStop = items[index].Time - example.Time;
                    example.MemoizedTimesToStops[stop.IdentifiedAsStopName] = timeToGetToStop;
                    return timeToGetToStop;
                }
                index++;
            }

            return TimeSpan.MaxValue;
        }


    }
}
