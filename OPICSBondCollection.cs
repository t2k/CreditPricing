using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CreditPricing
{

    /// <summary>
    /// Summary description for OPICSBondCollection
    /// </summary>
    // a stronlg typed collection class for storing OpicsBond objects safely
    public class OpicsBondCollection : Dictionary<string, OPICSBond> // CollectionBase
    {
        private DateTime m_RatesValueDate;
        // date the rates are loaded/saved from
        private DateTime m_SettleDate;
        // settlement date ie T+3...
        private string m_title;


        // new FEB 08, just pass in a date to directly query bonds and flows
        public OpicsBondCollection(DateTime p_ycEvalDate, DateTime p_EvalSpreadsDate)
            : base()
        {
            //Dim cals As New CCalendars  ' load the collection of calendars based upon the locCCYDef table
            //Dim curvesByCCY As New CYldCurves(p_ycEvalDate) ' returns a calculated collection of yc objects the list is keyed by CCY

            var db = new CreditPricingEntities();
            var bonds = (from b in db.OPICSLIVEDEALs
                         join s in db.Security_Masters on b.CUSIP equals s.CUSIP
                         select new
                         {
                             bond = b,
                             s.ABSEvalCurveKey
                         }).ToList();


            // set this property for the collection
            m_RatesValueDate = p_ycEvalDate;
            //curve.ValueDate  ' pick up the date the rates were saved  used on form for date changes..
            m_SettleDate = p_ycEvalDate;
            // T+0 for now

            foreach (var row in bonds)
            {
                try
                {
                    Add(row.bond.DEALID, new OPICSBond(row.bond.DEALID));
                }
                catch
                {
                }
            }
        }


        // new FEB 08, just pass in a date to directly query bonds and flows
        /// <summary>
        /// Construct a filtered bond collection
        /// </summary>
        /// <param name="p_ycEvalDate">yield curve value date</param>
        /// <param name="p_EvalSpreadsDate">MTM Spreads date</param>
        /// <param name="p_strClass">Class: RMBS, CMBS, CDO etc</param>
        /// <param name="p_strFirstLetter">First Letter (A, B, C, etc)</param>
        public OpicsBondCollection(DateTime p_ycEvalDate, DateTime p_EvalSpreadsDate, string p_strClass, string p_strFirstLetter)
            : base()
        {
            var db = new CreditPricingEntities();

            Calendars cals = new Calendars();
            YldCurves curvesByCCY = new YldCurves(p_ycEvalDate);

            var bonds = (from b in db.OPICSLIVEDEALs
                         join s in db.Security_Masters on b.CUSIP equals s.CUSIP
                         where b.SECURITY.StartsWith(p_strFirstLetter) && s.SecurityClass == p_strClass
                         select new
                         {
                             bond = b,
                             bondProp = s,
                         }).ToList();

            var allFLows = (from c in db.OPICSCashFlows
                            orderby c.DEALID, c.INTSTRTDTE
                            select c).ToList();

            m_RatesValueDate = p_ycEvalDate;
            m_SettleDate = p_ycEvalDate;

            ABSEvalCurves tkEvalSpreads = new ABSEvalCurves(p_EvalSpreadsDate);


            // loop thru each bond..
            foreach (var row in bonds) //dsCreditNet.OPICSBondRow row in bonds.Rows)
            {

                // get the flows just for this .DEALID
                var bondFlows = (from df in allFLows
                                 where df.DEALID == row.bond.DEALID
                                 orderby df.INTSTRTDTE
                                 select df).ToList();
                // pick our calendar by CCY
                Calendar theCAL = cals[row.bond.CCY];
                // pick our YC
                YldCurve theYC = curvesByCCY[row.bond.CCY];
                // get ABS Spread Curve
                ABSEvalCurve theABSCurve = tkEvalSpreads[row.bondProp.ABSEvalCurveKey];

                Add(row.bond.DEALID, new OPICSBond(row.bond, row.bondProp, bondFlows, theCAL, theYC, theABSCurve));
            }
        }



        public string Title
        {
            get { return m_title; }
            set { m_title = value; }
        }

        public DateTime SettleDate
        {
            get { return m_SettleDate; }
        }


        public DateTime EvalDate
        {
            get { return m_RatesValueDate; }
        }


        public OPICSBond ByDEALID(string _dealID)
        {

            {
                return this[_dealID];
                // fastest way of for generic list //return this.Find(delegate(OPICSBond b) { return b.DealID == _dealID; }); // != null; // fastest way to find object using generics...

                //bool found = false;
                //OPICSBond obj = null;
                //foreach (var obj in this.Values)
                //{
                //    if (obj.DealID == strID)
                //    {
                //        found = true;
                //        break; // TODO: might not be correct. Was : Exit For
                //    }
                //}
                //if (!found) obj = null;
                //return obj;
            }
        }






        //   create and return a fixed array for this OpicsBond (OPICS)collection...
        // used to blast these specific fields to EXCEL...
        //public dsOUT.BondPX_2WAYDataTable Display_PX2WAY()
        //{
        //    dsOUT.BondPX_2WAYDataTable tbl = new dsOUT.BondPX_2WAYDataTable();

        //    foreach (OPICSBond obj in List)
        //    {
        //        obj.RecalcDMFlows();
        //        dsOUT.BondPX_2WAYRow row = tbl.NewBondPX_2WAYRow;
        //        {
        //            row.DEALID = obj.DealID;
        //            row.CUSIP = obj.CUSIP;
        //            row.Security = obj.Issuer;
        //            row.AssetClass = obj.AssetClass;
        //            row.AssetSubClass = obj.AssetSubClass;
        //            row.CCY = obj.CCY;
        //            row.WALDate = obj.MaturityDate;
        //            row.LegalFinal = obj.LegalFinalMaturity;
        //            row.WALA = obj.WALA;
        //            row.Factor = obj.CurrentFactor;
        //            row.CouponType = obj.CouponType;
        //            if (obj.CouponType == "ASW")
        //            {
        //                row.FRNSpread = obj.AswSprd * 10000;
        //            }
        //            else
        //            {
        //                row.FRNSpread = obj.Spread * 10000;
        //            }
        //            row.CPV = obj.CPV;
        //            row.WAL = obj.WAL;
        //            row.WALCPV = obj.WALcpv;
        //            row.CreditBPV = obj.CrBPV;
        //            row.CreditBPVCPV = obj.CrBPV_cpv;
        //            row.MTMSpread = obj.DM_BP;
        //            row.MTMSpreadCPV = obj.DM_cpv;
        //            row.OrigPrice = obj.PurchPrice;
        //            row.FULLPrice = obj.DirtyPrice;
        //            row.FULLPriceCPV = obj.DirtyPrice_CPV;
        //            row.AmountOutstanding = obj.OutAmt;
        //            row.AmountMTM = obj.DirtyPrice / 100 * obj.OutAmt;
        //            row.AmountMTMCPV = obj.DirtyPrice_CPV / 100 * obj.OutAmt;
        //        }
        //        tbl.AddBondPX_2WAYRow(row);
        //    }
        //    return tbl;
        //}



        //Function InterpABSCurve(ByVal CurveName As String, ByVal EvalCurveDate As Date, ByVal InterpDate As Date) As Double
        //    Dim ta As New dsCreditNetTableAdapters.ABSEvalCurveTableAdapter
        //    Dim dt As dsCreditNet.ABSEvalCurveDataTable = ta.GetCurveByDate(EvalCurveDate, CurveName)

        //    If dt.Rows.Count = 0 Then
        //        Return 0
        //    Else
        //        Dim row As dsCreditNet.ABSEvalCurveRow = dt.Rows(0)
        //        Dim nearDate As Date
        //        Dim farDate As Date
        //        Dim nearSprd As Integer
        //        Dim farSprd As Integer


        //        If InterpDate < EvalCurveDate.AddYears(1) Then
        //            Return row._1

        //        ElseIf InterpDate < EvalCurveDate.AddYears(3) Then
        //            nearDate = EvalCurveDate.AddYears(1)
        //            farDate = EvalCurveDate.AddYears(3)
        //            nearSprd = row._1
        //            farSprd = row._3

        //        ElseIf InterpDate < EvalCurveDate.AddYears(5) Then
        //            nearDate = EvalCurveDate.AddYears(3)
        //            farDate = EvalCurveDate.AddYears(5)
        //            nearSprd = row._3
        //            farSprd = row._5

        //        ElseIf InterpDate < EvalCurveDate.AddYears(7) Then
        //            nearDate = EvalCurveDate.AddYears(5)
        //            farDate = EvalCurveDate.AddYears(7)
        //            nearSprd = row._5
        //            farSprd = row._7
        //        ElseIf InterpDate < EvalCurveDate.AddYears(10) Then
        //            nearDate = EvalCurveDate.AddYears(7)
        //            farDate = EvalCurveDate.AddYears(10)
        //            nearSprd = row._7
        //            farSprd = row._10

        //        ElseIf InterpDate >= EvalCurveDate.AddYears(10) Then
        //            Return row._10
        //        End If
        //        Return nearSprd + (farSprd - nearSprd) * ((InterpDate.ToOADate - nearDate.ToOADate) / (farDate.ToOADate - nearDate.ToOADate))
        //    End If
        //End Function



    }
}