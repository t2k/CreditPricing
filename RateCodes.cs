using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CreditPricing
{

    /// <summary>
    /// A dictionary of string keyed RateCode objects  
    /// each RateCode must have unique string key...
    /// </summary>
    public class RateCodes : Dictionary<string,RateCode> 
    {
        //local variable to hold collection
        //private Collection m_Col;
        public DateTime ValueDate { get; set; }
        // private System.DateTime m_ValueDate;
        /// <summary>
        /// base constructor
        /// </summary>
        public RateCodes()
            : base()
        {
            ValueDate = DateTime.Today;

        }

        /// <summary>
        /// Constructor: Pass in a date, load data from database...
        /// </summary>
        /// <param name="_dateEval"></param>
        public RateCodes(DateTime _dateEval) : base()
        {
            ValueDate = _dateEval;
            Calendars cals = new Calendars();

            var db = new CreditPricingEntities();
            List<RATECODE> qry = (from r in db.RATECODEs
                      select r).ToList();

            foreach (RATECODE row in qry)
            {
                {
                    // need to dynamically set 'R'elative dates
                    // each day these dates are dynamically calculated
                    //if (row.CF_Period_Type == "R")
                    if (row.CF_Period_Type == "R")
                    {
                        
                        Calendar cal = cals[row.CCY];
                        // relative (calc forward dates)
                        row.CF_StartDate = cal.Workdays(ValueDate, (int)row.nDay2Start);
                        row.CF_EndDate = cal.FarDate((DateTime)row.CF_StartDate, row.Period);
                        row.CF_SensiEndDate = row.CF_EndDate;

                    }

                    try
                    {
                        Add(row.RATECODE1, new RateCode(row));
                    }
                    catch 
                    {
                        // ignore...
                    }
                }
            }
        }


        /// <summary>
        /// Contruct RateCodes for a given Date and YieldCurve
        /// </summary>
        /// <param name="_dateEval">value date </param>
        /// <param name="_ycKey">uniqued yield curve key</param>
        public RateCodes(DateTime _dateEval,string _ycKey): base()
        {
            ValueDate = _dateEval;

            var db = new CreditPricingEntities();

            var qry1 = (from y in db.ycHeaders
                       where y.Name == _ycKey
                       select new { 
                        y.HolidayCenter
                      }).SingleOrDefault();
            
            Calendar cal = new Calendar(qry1.HolidayCenter.Split(',').Select(p => p.Trim()).ToList(), ValueDate);
            // I think we only need the ratecodes per CURVENAME
            
            var qry = (from y in db.ycItemHistories
                        where (y.Date==_dateEval && y.CurveName==_ycKey)
                        orderby y.SORTORDER
                        select new
                        {
                            y.BPShift,
                            y.RATECODE1
                        }).ToList();

            // remember  qry is an anonymous projection of RATECODE + BPShift from ycITEMHistories
            foreach (var row in qry)
            {
                {
                    // need to dynamically set 'R'elative dates
                    // each day these dates are dynamically calculated
                    if (row.RATECODE1.CF_Period_Type == "R")
                    {
                        // relative (calc forward dates)
                        row.RATECODE1.CF_StartDate = cal.Workdays(ValueDate, (int)row.RATECODE1.nDay2Start);
                        row.RATECODE1.CF_EndDate = cal.FarDate((DateTime)row.RATECODE1.CF_StartDate, row.RATECODE1.Period);
                        row.RATECODE1.CF_SensiEndDate = row.RATECODE1.CF_EndDate;
                    }

                    try
                    {
                        Add(row.RATECODE1.RATECODE1, new RateCode(row.RATECODE1));
                    }
                    catch
                    {
                        // ignore...
                    }
                }
            }
        }

        /// <summary>
        /// gets just thee ratecodes associated with teh _ycKey use passed in cal to compute periods
        /// </summary>
        /// <param name="_dateEval"></param>
        /// <param name="_ycKey"></param>
        /// <param name="cal"></param>
        public RateCodes(DateTime _dateEval, string _ycKey, Calendar cal) :base()
        {
            ValueDate = _dateEval;
            var db = new CreditPricingEntities();

            var qry = (from y in db.ycItemHistories
                       where (y.Date == _dateEval && y.CurveName == _ycKey)
                       orderby y.SORTORDER
                       select new
                       {
                           y.BPShift,
                           y.RATECODE1
                       }).ToList();

            // remember  qry is an anonymous projection of RATECODE + BPShift from ycITEMHistories
            foreach (var row in qry)
            {
                {
                    // need to dynamically set 'R'elative dates
                    // each day these dates are dynamically calculated
                    if (row.RATECODE1.CF_Period_Type == "R")
                    {
                        // relative (calc forward dates)
                        row.RATECODE1.CF_StartDate = cal.Workdays(ValueDate, (int)row.RATECODE1.nDay2Start);
                        row.RATECODE1.CF_EndDate = cal.FarDate((DateTime)row.RATECODE1.CF_StartDate, row.RATECODE1.Period);
                        row.RATECODE1.CF_SensiEndDate = row.RATECODE1.CF_EndDate;
                    }

                    try
                    {
                        Add(row.RATECODE1.RATECODE1, new RateCode(row.RATECODE1));
                    }
                    catch
                    {
                        // ignore...
                    }
                }
            }
        }
    }
}