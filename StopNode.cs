using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETAPredictor
{
    public class StopNode
    {
        /// <summary>
        /// The Canonical data points for this stop. This contains the stop name and location
        /// </summary>
        public GPSData Data;


        internal List<GPSData> AssociatedData = new List<GPSData>();
        internal List<RouteEdge> routes = new List<RouteEdge>();

        //public TimeSpan AverageTimeAtStop = TimeSpan.FromMinutes(5);
        public TimeSpan CurrentTimeAtStop = TimeSpan.FromMinutes(5);

        public StopNode(GPSData data)
        {
            this.Data = data;
        }
    }
}
