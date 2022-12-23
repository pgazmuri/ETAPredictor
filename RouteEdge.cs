using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETAPredictor
{
    /// <summary>
    /// Represents a path from one stop to another
    /// </summary>
    internal class RouteEdge
    {
        private int AveragedWithCount = 0;
        internal TimeSpan AverageTimeOnRoute = TimeSpan.FromMinutes(5);
        internal TimeSpan CurrentTimeOnRoute = TimeSpan.FromMinutes(5);
        internal StopNode FromNode { get; set; }
        internal StopNode ToNode { get; set;} 

        internal List<GPSData> AssociatedData = new List<GPSData>();

        internal void IntegrateTimeOnRoute(TimeSpan time)
        {
            AverageTimeOnRoute = ((AverageTimeOnRoute * AveragedWithCount) + time) / (++AveragedWithCount);
            CurrentTimeOnRoute = time;
        }
        
    }
}
