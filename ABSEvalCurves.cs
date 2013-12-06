//using System;
using System;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using System.Web;
using System.Linq;
//using CS;

namespace CreditPricing
{

    /// <summary>
    /// Summary description for ABSEvalCurves
    /// </summary>
    // **** DICTIONARY COLLECTION OF CABSEvalCurve objects *****

    public class ABSEvalCurves : Dictionary<string, ABSEvalCurve> // System.Collections.DictionaryBase
    {
        // class members
        public DateTime EvalDate { get; set; }



        // fill my collection class from
        public ABSEvalCurves(DateTime _evalDate)
            : base()
        {
            EvalDate = _evalDate;
            Load();
        }


        public double Interpolate(string _curve, DateTime _date)
        {
            return this.ContainsKey(_curve) ? this[_curve].InterpSpread(_date) : 1;
        }


        public List<ABSEvalCurve> GetData(DateTime _date)
        {
            EvalDate = _date;
            Load();
            return this.Values.ToList();
        }


        private void Load()
        {
            Clear();
            var db = new CreditPricingEntities();

            List<ABSEvalCurve> lst = (from curve in db.ABSEvalCurves
                                      where curve.EvalDate == EvalDate
                                      orderby curve.ABSEvalCurveName
                                      select curve).ToList();
            foreach (ABSEvalCurve c in lst)
            {
                Add(c.ABSEvalCurveName, c);
            }
        }
    }
}