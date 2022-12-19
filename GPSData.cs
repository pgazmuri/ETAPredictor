using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETAPredictor
{
    public class GPSData
    {
        /// <summary>
        /// Latitude in degrees
        /// </summary>
        public double Lat;

        /// <summary>
        /// Longitude in degrees
        /// </summary>
        public double Long;

        /// <summary>
        /// Speed in MPH
        /// </summary>
        public double Speed;

        /// <summary>
        /// Heading in degrees
        /// </summary>
        public double Heading;

        /// <summary>
        /// Unique identifier for the device/vehicle that generated this PGS data point
        /// </summary>
        public string Serial;
        public DateTime Time;

        internal StopNode AssociatedStopNode = null;
        internal RouteEdge AssociatedRouteEdge = null;

        /// <summary>
        /// If this data point represents the location of a stop on a route (e.g. a bus stop), this property should be set with a unique name.
        /// </summary>
        public string IdentifiedAsStopName = null;

        public GPSData() { }

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
