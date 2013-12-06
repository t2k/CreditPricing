using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;


namespace CreditPricing
{
    /// <summary>
    /// Simulation Bin class
    /// </summary>
    public class SimBin
    {
        public string BinID { get; set; }
        public double BinPct { get; set; }
        public double BinThreshold { get; set; }
        public int BinCount { get; set; }
        public void Increment()
        {
            BinCount++; //= 1;
        }
    }
}