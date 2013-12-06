using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CreditPricing
{
    /// <summary>
    /// Summary description for RealTimePrices
    /// </summary>
    public class RealTimePrices
    {
        //local variable(s) to hold property value(s)
        public DateTime ValueDate { get; set; }
        public DateTime TimeStamp { get; set; }
        public DateTimeOffset TimeStampUTC { get; set; }
        public Dictionary<string, Rate> Rates { get; set; }

        // sort of deprecated...
        public List<string> Undefined { get; set; }
        public string XLSWorkbookProvider { get; set; }

        /// <summary>
        /// Contruct from database by _dateEval
        /// </summary>
        /// <param name="_dateEval">pass a date to load realtimeprices</param>
        public RealTimePrices(DateTime _dateEval)
        {
            ValueDate = _dateEval;
            Rates = new Dictionary<string, Rate>();
            Undefined = new List<string>();
            LoadDB();
        }

        public RealTimePrices()
        {
            Rates = new Dictionary<string, Rate>();
            Undefined = new List<string>();
        }


        /// <summary>
        /// optimized constructor, only load subset of _ratecodes
        /// </summary>
        /// <param name="_date"></param>
        /// <param name="_ratecodes"></param>
        public RealTimePrices(DateTime _date, List<string> _ratecodes)
        {
            ValueDate = _date;
            // init the rates dictionary
            Rates = new Dictionary<string, Rate>();

            // LINQ to entities
            var db = new CreditPricingEntities();
            var rates = from r in db.RATEHISTs
                         where r.Date == _date && _ratecodes.Contains(r.RATECODE)
                         select r;

            foreach (var row in rates) // dsCreditNet.RATEHISTRow row in dt.Rows)
            {
                // special constructor-- create 
                Rate rate = new Rate(row);
                Rates.Add(rate.RATECODE, rate);
            }

            var daysaves = from t in db.RATETimeStamps
                           where t.SaveDate == ValueDate
                           select t;


            // linq qry for max timestamp by savedate...
            TimeStamp = daysaves.Max(t => t.TimeStamp);
            TimeStampUTC = (DateTimeOffset)daysaves.Max(t => t.TimeStampUTC);
        }


        public string UndefinedList
        {
            get
            {
                string strList = "";
                foreach (string s in Undefined)
                {
                    strList += s + Environment.NewLine;
                }
                return strList;
            }
        }

        public int UndefinedCount
        {
            get { return Undefined.Count(); }
        }


        /// <summary>
        /// lookup a ratecode and return the requested price
        /// </summary>
        /// <param name="_rateCode"></param>
        /// <param name="_priceType"></param>
        /// <returns></returns>
        public double Price(string _rateCode, tkPrice _priceType)
        {
            {
                // CRate p = new CRate();
                Rate p = Rates[_rateCode];
                if (p != null)
                {
                    switch (_priceType)
                    {
                        case tkPrice.tkBID:
                            return p.bid;
                        case tkPrice.tkASK:
                            return p.ask;
                        case tkPrice.tkMID:
                            return (p.ask + p.bid) / 2;
                        case tkPrice.tkCLS:
                            return p.cls;
                        default: return p.ask;
                    }
                }
                else
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// private method gets data from Entity Framework classes RATEHIST
        /// </summary>
        private void LoadDB()
        {
            // init the rates dictionary
            Rates = new Dictionary<string, Rate>();

            // LINQ to entities
            var db = new CreditPricingEntities();
            var rates = (from r in db.RATEHISTs
                         where r.Date == this.ValueDate
                         select r).ToList();

            foreach (RATEHIST row in rates) // dsCreditNet.RATEHISTRow row in dt.Rows)
            {
                // special constructor-- create 
                Rate rate = new Rate(row);
                Rates.Add(rate.RATECODE, rate);
            }



            // linq qry for max timestamp by savedate...
            var daysaves = from t in db.RATETimeStamps
                           where t.SaveDate == ValueDate
                           select t;

            TimeStamp = daysaves.Max(t => t.TimeStamp);
            TimeStampUTC = (DateTimeOffset)daysaves.Max(t => t.TimeStampUTC);
        }


    }
}