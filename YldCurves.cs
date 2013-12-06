using System;
using System.Collections.Generic;
using System.Linq;

namespace CreditPricing
{
    /// <summary>
    /// A dictionary of (string,YldCurve)
    /// </summary>
    public class YldCurves : Dictionary<string, YldCurve> //System.Collections.IEnumerable
    {
        //local variable to hold collection
        //private ICollection mCol = new Collection();

        // loads yldcurve collection just default curve per CCY
        /// <summary>
        /// default constructor: create a dictionary of the default yield curves one for each CCY
        /// indexed by CCY  
        /// </summary>
        /// <param name="_evalDate">date to load date from</param>
        public YldCurves(DateTime _evalDate)
            : base()
        {
            var db = new CreditPricingEntities();
            var ccylst = (from c in db.locCcyDefs
                          orderby c.CCY
                          select c).ToList();

            // load prices once! and use to recalc all embedded curves
            RealTimePrices prices = new RealTimePrices(_evalDate);

            //foreach (dsCreditNet.locCcyDefRow row in dt.Rows)
            foreach (locCcyDef c in ccylst)
            {
                // call the yc constructor with curvename and prices in the parameter list
                //keyed by CCY
                Add(c.CCY, new YldCurve(c.DefaultYCurve, prices));
            }
        }
    }
}