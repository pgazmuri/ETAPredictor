using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETAPredictor
{
    public class RoutesModelFileFormat
    {
        public List<GPSData> Stops { get; set; } = new List<GPSData>();
        public List<GPSData> Data { get; set; } = new List<GPSData>();

        public RoutesModelFileFormat() { }
    }
}
