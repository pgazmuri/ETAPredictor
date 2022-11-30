using Geodesy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPSTrackerTest
{
    public class GeoHelper
    {
        /// <summary>
        /// Returns an estimate of the position using dead reckoning based on a GPS data point and the time passed.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="since"></param>
        /// <returns></returns>
        public static GPSData GetEstimatedPosition(GPSData data, TimeSpan since)
        {
            if (data.Speed < 2 || since.Ticks == 0)
            {
                return data;
            }
            Geodesy.GeodeticCalculator calc = new Geodesy.GeodeticCalculator(Ellipsoid.WGS84);

            var miles = ((double)since.TotalSeconds / 3600D) * data.Speed;
            var meters = miles * 1609.43;

            var result = calc.CalculateEndingGlobalCoordinates(
                ConvertToGlobalCoords(data), new Geodesy.Angle(data.Heading),
                 meters);
            var clone = data.Clone();
            clone.Lat = result.Latitude.Degrees;
            clone.Long = result.Longitude.Degrees;
            return clone;
        }

        public static GlobalCoordinates ConvertToGlobalCoords(GPSData data)
        {
            return new Geodesy.GlobalCoordinates(
                            new Geodesy.Angle(data.Lat), new Geodesy.Angle(data.Long));
        }

        public static double GetDistanceBetweenInMeters(GPSData ptOne, GPSData ptTwo)
        {
            Geodesy.GeodeticCalculator calc = new Geodesy.GeodeticCalculator(Ellipsoid.WGS84);
            var result = calc.CalculateGeodeticCurve(ConvertToGlobalCoords(ptOne), ConvertToGlobalCoords(ptTwo));
            return result.EllipsoidalDistance;
        }

        public static double GetAngularDistanceBetweenHeadings(GPSData ptOne, GPSData ptTwo)
        {
            var heading = Math.Abs(ptOne.Heading - ptTwo.Heading);
            //the furthest we can go apart is 180 degrees. If the result is more than that, subtract 360 and take ABS
            if(heading > 180)
            {
                heading -= 360;
            }
            return Math.Abs(heading);
        }
    }
}
