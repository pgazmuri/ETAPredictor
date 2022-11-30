using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPSTrackerTest
{
    public class RouteEdge
    {
        public TimeSpan AverageTimeOnRoute = TimeSpan.FromMinutes(5);
        public TimeSpan CurrentTimeOnRoute = TimeSpan.FromMinutes(5);
        public StopNode FromNode { get; set; }
        public StopNode ToNode { get; set;} 

    }
}
