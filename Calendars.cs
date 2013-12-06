using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CreditPricing
{
    /// <summary>
    /// Summary description for Calendars
    /// </summary>
    /// 

    public class Calendars : Dictionary<string, Calendar>
    {
        // read the locCCYDef table and contruct calendars for each!
        public Calendars()
            : base()
        {
            var db = new CreditPricingEntities();
            var qry = from c in db.locCcyDefs
                      select c.CCY;
            foreach (string ccy in qry) //.locCcyDefRow row in dt.Rows)
            {
                Add(ccy, new Calendar(ccy));
            }
        }
    }
}