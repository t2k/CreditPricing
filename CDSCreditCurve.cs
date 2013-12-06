using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;

/// <summary>
/// CDSCreditCurve: ValueDate, Recovery, BBCorpTicker, OrbitID, CorpTKR, CCY, Seniority
/// GROUP, Sector, SubGroup, RefOb, EQTicker, RefEnt and CurvePoints
/// </summary>
/// 
namespace CreditPricing
{

    public class CDSCreditCurve
    {
        public DateTime ValueDate { get; set; }
        public double Recovery { get; set; }
        public string BBCorpTKR { get; set; }
        public string OrbitID { get; set; }
        public string CorpTKR { get; set; }
        public string CCY { get; set; }
        public string Seniority { get; set; }
        public string GROUP { get; set; }
        public string Sector { get; set; }
        public string SubGROUP { get; set; }
        public string RefOb { get; set; }
        public string EQTicker { get; set; }
        public string RefEnt { get; set; }
        public CDSCreditCurvePoints CurvePoints { get; set; }

        /// <summary>
        /// Unique Curve Key
        /// </summary>
        /// <returns>string combined of Ticker CCY Seniority</returns>
        public string CurveKeyName
        {
             get { return string.Format("{0}{1}{2}", CorpTKR, CCY, Seniority); } // m_CORPTKR, m_CCY, m_SNRSUB); 
        }


        /// <summary>
        /// 
        /// </summary>
        public string CDS5YrTKR
        {
            get
            {
                foreach (CDSCreditCurvePoint cp in CurvePoints)
                {
                    if (cp.CDSTerm == 5)
                    {
                        return cp.Ticker;
                    }
                }
                return null;
            }
        }


        //constructor
        // create a CDS curve from a mpCDSCurveROW , dateNextIMM and  providers collection
        /// <summary>
        /// contructor: (mpCDSCurve, DateTime, List of MarketDataProvider
        /// </summary>
        /// <param name="row"></param>
        /// <param name="dateNextIMM"></param>
        /// <param name="cdsMARKETProviders"></param>
        public CDSCreditCurve(mpCDSCurve row, DateTime dateNextIMM, List<MarketDataProvider> cdsMARKETProviders)
        {
            {
                this.CorpTKR = row.TICKER;
                this.CCY = row.CCY;
                this.Seniority = row.Tier;
                this.GROUP = row.Region;
                this.Sector = row.Sector;
                this.SubGROUP = "N/A";
                this.RefEnt = row.ShortName;
                this.RefOb = row.RedCode;
                //row.ISIN + " CORP" '"N/A" 'row.REFERENCE_OBLIGATION
                this.EQTicker = "N/A";
                //row.EQUITY_TICKER
                this.ValueDate = row.EvalDate;

                // parse out parent ticker here
                string strTKR = row.TICKER;
                this.BBCorpTKR = strTKR.Contains("-") ? strTKR.Substring(0, strTKR.IndexOf("-")) : strTKR;
                this.OrbitID = "N/A";
                this.Recovery = row.Recovery.HasValue ? (double)row.Recovery: .4;

                // creating a CreditCurve object...  
                // add a new CreditCurvePoints

                try
                {
                    this.CurvePoints = new CDSCreditCurvePoints(row, dateNextIMM, cdsMARKETProviders);
                }
                catch 
                {
                    this.CurvePoints = null;
                }
            }
        }


        //NEW: Nov. 1, 2006: Implement s/l interpolation between end dates...
        /// <summary>
        /// 
        /// </summary>
        /// <param name="_dateInterp">The date to Interpolate to:</param>
        /// <returns>double bid side</returns>
        public double cdsBid(DateTime _dateInterp)
        {
            CDSCreditCurvePoint cp = null;
            CDSCreditCurvePoint cpPrev = null;
            double dbl1 = 0;
            DateTime dt1 = ValueDate; // m_ValueDate;
            double dbl2 = 0;
            DateTime dt2 = ValueDate; // m_ValueDate;

            // pass thru the collection of curvepoints just past the date we are looking for
            foreach (CDSCreditCurvePoint pt in this.CurvePoints)
            {
                if ((pt.ValueDate >= _dateInterp && pt.Market.BidStatus && pt.MarketStatus))
                {
                    cp = pt;
                    break;
                }
                else
                {
                    cpPrev = pt;
                }
            }

            //remember: each curve point has a market of multiple providers giving us bids and offers.
            try
            {
                if (cpPrev == null)
                {
                    // prior to first curvepoint  (interp from zero to first stub)
                    // leave dbl1 and dt1 as initialized ie 0 and valuedate
                    // pickup the far bid and far date
                    dbl2 = cp.Market.BestBid;
                    dt2 = cp.ValueDate;
                }
                else if (cpPrev.ValueDate < cp.ValueDate)
                {
                    // between points
                    dbl1 = cpPrev.Market.BestBid;
                    // a double
                    dt1 = cpPrev.ValueDate;
                    // pickup the far bid and far date
                    dbl2 = cp.Market.BestBid;
                    dt2 = cp.ValueDate;
                }
                else if ((cp.ValueDate == cpPrev.ValueDate))
                {
                    //after last point  use last then...
                    return Math.Max( cp.Market.BestBid,(double).001);
                }

                // return nearbid + ((dt-nearDate)/(farDate-nearDate) *(FarBid-nearBid))
                // note: must cast int to double as the C# compliler is not the same as the VB compiler
                return Math.Max(dbl1 + (((double)_dateInterp.Subtract(dt1).Days / (double)dt2.Subtract(dt1).Days) * (dbl2 - dbl1)), (double)0.001);
            }
            catch 
            {
                // this should not occur logically
                return cp.Market.BestBid;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_dateInterp">Date to Interpolate to:</param>
        /// <returns>Ask side of mkt</returns>
        public double cdsAsk(DateTime _dateInterp)
        {
            CDSCreditCurvePoint cp = null;
            CDSCreditCurvePoint cpPrev = null;
            double dbl1 = 0;
            DateTime dt1 = ValueDate; // m_ValueDate;
            double dbl2 = 0;
            DateTime dt2 = ValueDate; // m_ValueDate;

            foreach (CDSCreditCurvePoint pt in this.CurvePoints)
            {
                if ((pt.ValueDate >= _dateInterp && pt.Market.AskStatus && pt.MarketStatus))
                {
                    cp = pt;
                    break; // TODO: might not be correct. Was : Exit For
                }
                else
                {
                    cpPrev = pt;
                }
            }


            //remember: each curve point has a market of multiple providers giving us bids and offers.
            try
            {
                if (cpPrev == null)
                {
                    // prior to first curvepoint  (interp from zero to first stub)
                    // leave dbl1 and dt1 as initialized ie 0 and valuedate
                    // pickup the far bid and far date
                    dbl2 = cp.Market.BestAsk;
                    dt2 = cp.ValueDate;
                }
                else if (cpPrev.ValueDate < cp.ValueDate)
                {
                    // between points
                    dbl1 = cpPrev.Market.BestAsk;
                    // a double
                    dt1 = cpPrev.ValueDate;
                    // pickup the far bid and far date
                    dbl2 = cp.Market.BestAsk;
                    dt2 = cp.ValueDate;
                }
                else if ((cp.ValueDate == cpPrev.ValueDate))
                {
                    //after last point  use last then...
                    return  Math.Max(cp.Market.BestAsk, (double)0.001);
                }

                // return nearbid + ((dt-nearDate)/(farDate-nearDate) *(FarBid-nearBid))
                // do not return zero
                return Math.Max(dbl1 + (((double)_dateInterp.Subtract(dt1).Days / (double)dt2.Subtract(dt1).Days) * (dbl2 - dbl1)), (double)0.001);
            }
            catch 
            {
                // this should not occur logically
                return Math.Max(cp.Market.BestAsk, (double)0.001);
                //End If
            }
        }

        /// <summary>
        /// Return mid market cds interpolated to _date
        /// </summary>
        /// <param name="_dateInterp"></param>
        /// <returns></returns>
        public double cdsMid(DateTime _dateInterp)
        {
            CDSCreditCurvePoint cp = null;
            CDSCreditCurvePoint cpPrev = null;
            double dbl1 = 0;
            DateTime dt1 = ValueDate; // m_ValueDate;
            double dbl2 = 0;
            DateTime dt2 = ValueDate; // m_ValueDate;

            foreach (CDSCreditCurvePoint pt in CurvePoints)
            {
                if ((pt.ValueDate >= _dateInterp && pt.Market.BidStatus && pt.MarketStatus))
                {
                    cp=pt;
                    break; 
                }
                else
                {
                    cpPrev = cp;
                }
            }


            //remember: each curve point has a market of multiple providers giving us bids and offers.
            try
            {
                if (cpPrev == null)
                {
                    // prior to first curvepoint  (interp from zero to first stub)
                    // leave dbl1 and dt1 as initialized ie 0 and valuedate
                    // pickup the far bid and far date
                    dbl2 = cp.Market.BestMid;
                    dt2 = cp.ValueDate;
                }
                else if (cpPrev.ValueDate < cp.ValueDate)
                {
                    // between points
                    dbl1 = cpPrev.Market.BestMid;
                    // a double
                    dt1 = cpPrev.ValueDate;
                    // pickup the far bid and far date
                    dbl2 = cp.Market.BestMid;
                    dt2 = cp.ValueDate;
                }
                else if ((cp.ValueDate == cpPrev.ValueDate))
                {
                    //after last point  use last then...
                    return Math.Max(cp.Market.BestMid, (double)0.001);
                }

                // return nearbid + ((dt-nearDate)/(farDate-nearDate) *(FarBid-nearBid))

                return Math.Max(dbl1 + (((double)_dateInterp.Subtract(dt1).Days / (double)dt2.Subtract(dt1).Days) * (dbl2 - dbl1)), (double)0.001);
            }
            catch 
            {
                // this should not occur logically
                return Math.Max(cp.Market.BestMid,(double).001);
            }
        }

    }
}