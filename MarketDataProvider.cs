using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CreditPricing
{


    public class MarketDataProvider
    {
        public string Name { get; set; }
        public string InterfaceName { get; set; }
        public MarketDataProvider(string _name, string _interfaceName)
        {
            Name = _name;
            InterfaceName = _interfaceName;
        }

    }
}