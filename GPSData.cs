using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPSTrackerTest
{
    public class GPSData
    {
        public double Lat;
        public double Long;
        public double Speed;
        public double Heading;
        public string Serial;
        public DateTime Time;

        internal StopNode AssociatedStopNode = null;
        internal RouteEdge AssociatedRouteEdge = null;
        public string IdentifiedAsStopName = null;

        public GPSData() { }

        public GPSData Clone()
        {
            return new GPSData()
            {
                Lat = Lat,
                Long = Long,
                Speed = Speed,
                Heading = Heading,
                Serial = Serial,
                Time = Time,
                IdentifiedAsStopName = IdentifiedAsStopName,
                AssociatedStopNode = AssociatedStopNode,
                AssociatedRouteEdge = AssociatedRouteEdge,
            };
        }


    }
}
