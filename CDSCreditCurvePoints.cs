using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;


namespace CreditPricing
{

    /// <summary>
    /// Summary description for CDSCreditCurvePoints
    /// </summary>
    // strongly typed class to store a collection of  CDSCreditCurvePoint objects...
    public class CDSCreditCurvePoints : List<CDSCreditCurvePoint> // System.Collections.CollectionBase
    {


        // NEW 9/26/06:  MARKIT PARTNERS INTERFACE...
        /// <summary>
        /// Markit Record constructor
        /// </summary>
        /// <param name="row"></param>
        /// <param name="dtIMM"></param>
        /// <param name="providers"></param>
        public CDSCreditCurvePoints(mpCDSCurve row, DateTime dtIMM, List<MarketDataProvider> providers)
            : base()
        {
            bool blnAddrec = true;

            // markit partners credit curve structure is fixed....
            int[] intCurveMonths = { 6, 12, 24, 36, 48, 60, 84, 120, 180, 240, 360};

            int i = 0;
            double dblCumSprd = 0;

            // this is tricky...  if all spreads have a zero then we assume it is a defaulted credit, these need to be removed from 
            // our portfolios er a credit event, but until then we will override with a 10000 bp spread
            // note: this is a little goofy in C#, must use reflection (system.relflection) to total class members beginning with 'C_
            // the fields that begin w/ C_ are the 6m thru 30y values I need to test...

            BindingFlags flags = BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic;

            foreach (FieldInfo f in row.GetType().GetFields(flags))
            {
                if (f.Name.StartsWith("_C_"))
                {
                    dblCumSprd += (double)f.GetValue(row);
                }
                //dblCumSprd += f.Name.StartsWith("_C_") ? (double)f.GetValue(row) : 0;
            }

            for (i = 0; i < intCurveMonths.Length; i++)
            {
                CDSCreditCurvePoint cp = new CDSCreditCurvePoint();
                cp.MarketStatus = true;
                cp.CDSTerm = intCurveMonths[i];
                cp.BLPCode = row.TICKER + cp.CDSTerm.ToString() + " Curncy";
                //force this to look like a bloomberg tkr
                cp.ValueDate = dtIMM;
                // this creates final date ie 1M, 12M, 36M etc
                cp.Ticker = row.TICKER;
                cp.Market = new CDSMarketProviders(cp.BLPCode, providers);

                if (dblCumSprd == 0)
                {
                    // if all spreads are given to be zero, assume defaults, 
                    //this occurs when a Credit has defaulted, sometimes it's posted with all zero's... we test for this
                    cp.Market.Update("MarkIt", "BID", 100);
                    cp.Market.Update("MarkIt", "ASK", 100);
                    blnAddrec = true;
                }
                else
                {
                    switch (i)
                    {
                        case 0:
                            cp.Market.Update("MarkIt", "BID", (double)row.C_6m * 100);
                            cp.Market.Update("MarkIt", "ASK", (double)row.C_6m * 100);
                            blnAddrec = ((double)row.C_6m != 0);
                            break;
                        case 1:
                            cp.Market.Update("MarkIt", "BID", (double)row.C_1Y * 100);
                            cp.Market.Update("MarkIt", "ASK", (double)row.C_1Y * 100);
                            blnAddrec = ((double)row.C_1Y != 0);
                            break;
                        case 2:
                            cp.Market.Update("MarkIt", "BID", (double)row.C_2Y * 100);
                            cp.Market.Update("MarkIt", "ASK", (double)row.C_2Y * 100);
                            blnAddrec = ((double)row.C_2Y != 0);
                            break;
                        case 3:
                            cp.Market.Update("MarkIt", "BID", (double)row.C_3Y * 100);
                            cp.Market.Update("MarkIt", "ASK", (double)row.C_3Y * 100);
                            blnAddrec = ((double)row.C_3Y != 0);
                            break;
                        case 4:
                            cp.Market.Update("MarkIt", "BID", (double)row.C_4Y * 100);
                            cp.Market.Update("MarkIt", "ASK", (double)row.C_4Y * 100);
                            blnAddrec = ((double)row.C_4Y != 0);
                            break;
                        case 5:
                            cp.Market.Update("MarkIt", "BID", (double)row.C_5Y * 100);
                            cp.Market.Update("MarkIt", "ASK", (double)row.C_5Y * 100);
                            blnAddrec = ((double)row.C_5Y != 0);
                            break;
                        case 6:
                            cp.Market.Update("MarkIt", "BID", (double)row.C_7Y * 100);
                            cp.Market.Update("MarkIt", "ASK", (double)row.C_7Y * 100);
                            blnAddrec = ((double)row.C_7Y != 0);
                            break;
                        case 7:
                            cp.Market.Update("MarkIt", "BID", (double)row.C_10Y * 100);
                            cp.Market.Update("MarkIt", "ASK", (double)row.C_10Y * 100);
                            blnAddrec = ((double)row.C_10Y != 0);
                            break;
                        case 8:
                            cp.Market.Update("MarkIt", "BID", (double)row.C_15Y * 100);
                            cp.Market.Update("MarkIt", "ASK", (double)row.C_15Y * 100);
                            blnAddrec = ((double)row.C_15Y != 0);
                            break;
                        case 9:
                            cp.Market.Update("MarkIt", "BID", (double)row.C_20Y * 100);
                            cp.Market.Update("MarkIt", "ASK", (double)row.C_20Y * 100);
                            blnAddrec = ((double)row.C_20Y != 0);
                            break;
                        case 10:
                            cp.Market.Update("MarkIt", "BID", (double)row.C_30Y * 100);
                            cp.Market.Update("MarkIt", "ASK", (double)row.C_30Y * 100);
                            blnAddrec = ((double)row.C_30Y != 0);
                            break;
                    }
                }
                if (blnAddrec)
                {
                   this.Add(cp);
                }
            }
        }



        // used to retrieve a point  not indexed ( this is OK here because the list is always very small!)
        // strTKR looks like "CAIG1U5 Curncy"
        public CDSCreditCurvePoint GetPointByID(string strID)
        {
           // CDSCreditCurvePoint cp = null;
            foreach (CDSCreditCurvePoint cp in this)
            {
                if (cp.BLPCode == strID) { return cp; }

            }
            // if not 'found' then return null
            return null;
        }
    }
}