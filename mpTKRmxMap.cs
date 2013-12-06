using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CreditPricing
{

    /// <summary>
    /// create a map (dictionary <string,string>) from the MUREXTKRMAP table, used to attach pricing to a murex CDS intrument
    /// </summary>
    public class mpTKRmxMap : Dictionary<string, string>
    {
        public mpTKRmxMap()
            : base()
        {
            var db = new CreditPricingEntities();
            var lst = (from m in db.MUREXTKRMAPs
                       select m).ToList();

            foreach (var m in lst)
            {
                // worth noting here: the key looks like "JPM,USD,SNRFOR" and the Value would be the MUREX instument for this JPM
                Add(string.Format("{0},{1},{2}", m.TKR, m.CCY, m.TIER), m.INSTRUMENT);
            }

        }
    }
}