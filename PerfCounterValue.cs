using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Perfon.Interfaces.PerfCounterStorage;

namespace Perfon.Storage.MySql
{
    /// <summary>
    /// Data block of Perf Counter.
    /// Used for obtaining perf counter time series from storages
    /// </summary>
    public class PerfCounterValue : IPerfCounterValue
    {
        //public PerfCounterValue()
        //{

        //}

        public PerfCounterValue(DateTime timestamp, float value) //:this()
        {
            Timestamp = timestamp;
            Value = value;
        }

        public DateTime Timestamp { get; private set; }
        public float Value { get; private set; }
    }
}
