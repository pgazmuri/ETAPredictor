using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETAPredictor
{
    /// <summary>
    /// Represents a GPS Location with values typically supplied by GPSTrackers like the SinoTrack ST-901
    /// Can also be used to represent the location of a stop on a route, indicated by setting the IdentifiedAsStopName property.
    /// </summary>
    public class GPSData
    {
        /// <summary>
        /// Latitude in degrees
        /// </summary>
        public double Lat { get; set; }

        /// <summary>
        /// Longitude in degrees
        /// </summary>
        public double Long { get; set; }

        /// <summary>
        /// Speed in MPH
        /// </summary>
        public double Speed { get; set; }

        /// <summary>
        /// Heading in degrees
        /// </summary>
        public double Heading { get; set; }

        /// <summary>
        /// Unique identifier for the device/vehicle that generated this PGS data point
        /// </summary>
        public string Serial { get; set; }
        public DateTime Time { get; set; }

        internal StopNode? AssociatedStopNode = null;
        internal RouteEdge? AssociatedRouteEdge = null;
        internal double MemoizedAverageSpeedOnEdgeToThisPoint = -1;

        /// <summary>
        /// If this data point represents the location of a stop on a route (e.g. a bus stop), this property should be set with a unique name.
        /// </summary>
        public string IdentifiedAsStopName { get; set; } = null;

        public GPSData() { }

        public Dictionary<string, TimeSpan> MemoizedTimesToStops = new Dictionary<string, TimeSpan>();

        /// <summary>
        /// Creates an identical copy of this object
        /// </summary>
        /// <returns></returns>
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
