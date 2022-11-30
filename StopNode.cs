using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPSTrackerTest
{
    public class StopNode
    {
        public GPSData Data;
        public List<GPSData> AssociatedData = new List<GPSData>();
        public List<RouteEdge> routes = new List<RouteEdge>();

        public TimeSpan AverageTimeAtStop = TimeSpan.FromMinutes(5);
        public TimeSpan CurrentTimeAtStop = TimeSpan.FromMinutes(5);

        public StopNode(GPSData data)
        {
            this.Data = data;
        }
    }
}
