﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETAPredictor
{
    public class ETATable : List<ETATableEntry> { }
    public class ETATableEntry
    {
        /// <summary>
        /// Name of the stop
        /// </summary>
        public string StopName;
        
        /// <summary>
        /// Estimated Time of arrival at the stop
        /// </summary>
        public TimeSpan ETA;

        /// <summary>
        /// Unique identifier of GPS device/vehicle expected to arrive.
        /// </summary>
        public string Serial;

        public ETATableEntry() { }

    }
}
