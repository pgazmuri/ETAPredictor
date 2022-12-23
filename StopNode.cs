using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETAPredictor
{
    /// <summary>
    /// Represents a stop on a route, the model is seeded with stops and infers routes from GSPPositions of vehicles
    /// </summary>
    internal class StopNode
    {
        /// <summary>
        /// The Canonical data points for this stop. This contains the stop name and location
        /// </summary>
        internal GPSData Data;


        internal List<GPSData> AssociatedData = new List<GPSData>();
        internal List<RouteEdge> routes = new List<RouteEdge>();

        //public TimeSpan AverageTimeAtStop = TimeSpan.FromMinutes(5);
        internal TimeSpan CurrentTimeAtStop = TimeSpan.FromMinutes(5);

        internal StopNode(GPSData data)
        {
            this.Data = data;
        }

    }
}
