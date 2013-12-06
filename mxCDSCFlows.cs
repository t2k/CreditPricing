using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CreditPricing
{

    /// <summary>
    /// generic List of mxCDSCF
    /// </summary>
    public class mxCDSCFlows : List<mxCDSCF> // System.Collections.CollectionBase
    {
        public int AIDays { get; set; }

        //private int m_AIDays;
        public DateTime Prev { get; set; }
        //private DateTime m_prev;


        //// used to create OPICS BOND cashflows records...
        //// here we pass in a 'filtered' array of typed rows, all rows per DEAL...
        //public mxCDSCFlows(dsCreditNet.OPICSBondCashFlowsRow[] rows, YldCurve yc, DateTime dtSettle) : base()
        //{
        //    foreach (dsCreditNet.OPICSBondCashFlowsRow row in rows)
        //    {
        //        Add(new mxCDSCF(row, yc, dtSettle));
        //    }
        //}



        /// <summary>
        /// construct mxCDSFlows 
        /// </summary>
        /// <param name="_opicsFlow">generic List of (OPICSCashFlow)</param>
        /// <param name="_yc">YldCurve</param>
        /// <param name="_dtSettle">DateTime</param>
        public mxCDSCFlows(List<OPICSCashFlow> _opicsFlow, YldCurve _yc, DateTime _dtSettle, double _FaceAmt)
            : base()
        {
            foreach (OPICSCashFlow row in _opicsFlow) //dsCreditNet.OPICSBondCashFlowsRow row in dtFlows.Rows)
            {
                Add(new mxCDSCF(row, _yc, _dtSettle, _FaceAmt));
            }
        }

        /// <summary>
        /// ASSET SWAP useage, creates 3M psuedo floating flows
        /// NEW: JUN 19 2006: ASSET SWAP cashflows:  create the floating 3MLIBOR legs heres..
        /// used to create OPICS BOND cashflows records...
        /// </summary>
        /// <param name="bond">OPICSBond</param>
        /// <param name="cal">Calendar</param>
        /// <param name="yc">YldCurve</param>
        /// <param name="dtOrigSettle">Original Settlement Date</param>
        public mxCDSCFlows(OPICSBond bond, Calendar cal, YldCurve yc, System.DateTime dtOrigSettle)
            : base()
        {
            DateTime dtRoll = bond.MaturityDate;
            DateTime dtCFStart; // = default(System.DateTime);
            DateTime dtCFEnd = dtRoll;

            int iCtr = 1;

            // each cashflows time period  CF Start and CF End...
            do
            {
                dtCFStart = cal.FwdDate(dtRoll, iCtr * -3);
                // this will generate a 3M roll date prior
                if ((dtCFStart < bond.EvalDate) & (bond.EvalDate < dtCFEnd))
                {
                    Prev = dtCFStart;
                    AIDays = bond.EvalDate.Subtract(Prev).Days;// (int)(bond.EvalDate.ToOADate - m_prev.ToOADate);
                }

                //If dtCFStart < dtOrigSettle Then
                //dtCFStart = dtOrigSettle ' this will dynamically shorten the front stub for eval purposes
                //End If

                Insert(0, new mxCDSCF(bond, dtCFStart, dtCFEnd, yc));
                dtCFEnd = dtCFStart;
                iCtr += 1;
            }
            //usually T+1 settle by convention for CDS
            // add the principal coupot
            // InsertFirst(New mxCDSCF(bond, dtOrigSettle, dtOrigSettle, yc))
            while (!(dtCFStart < dtOrigSettle));
        }




        // NEW: Mar 08  CPV cashflows:  create the floating legs heres..
        // used to create OPICS BOND cashflows records...
        public mxCDSCFlows(OPICSBond bond, Calendar cal, YldCurve yc)
            : base()
        {
            DateTime dtRoll = bond.MaturityDate;
            DateTime dtCFEnd = dtRoll;
            DateTime dtOrigSettle = bond.EvalDate;
            double[] dCPV = GetClassPayVector(bond.CPV, 480);
            // an array of doubles out to legal final?
            int l = bond.WALA;
            int iCpnPA = 0;

            int iRollDay = bond.LegalFinalMaturity.Day;
            // look up the deal by BR and Dealno and SettleDate to 'backdate the prinamt if needed

            // this should enable look back in time 
            double dblOrigFace = bond.OutstandingOn(bond.EvalDate);


            // for now this will do assume all RMBS is on a 1Month schedule
            // all others are quaterly or asset swapped quarterly
            if (bond.AssetClass.StartsWith("RMBS") || bond.AssetClass.StartsWith("CMBS"))
            {
                // monthly
                iCpnPA = 12;
            }
            else
            {
                // quarterly
                iCpnPA = 4;
            }

            // begin by creating the dynamically allocated cashflows find front stub
            DateTime dtFlowBegin = default(System.DateTime);
            DateTime dtFlowEnd = default(System.DateTime);

            // start from maturity date backwards to allign front stub with maturity date
            dtFlowBegin = bond.MaturityDate;
            int iCtr = 1;
            while (dtFlowBegin > bond.EvalDate)
            {
                //            
                dtFlowEnd = dtFlowBegin;
                // create monthly/qtrly cashflows by rolling backward from maturity date...
                // this will be holiday sensitive w/ modified following
                dtFlowBegin = bond.MaturityDate.AddMonths(-iCtr * (int)(12 / iCpnPA));
                iCtr += 1;
            }

            // ensure iRollDate day of month will match that of the maturity...
            DateTime iRollDate = dtFlowEnd;
            if (iRollDate.Day != iRollDay)
            {
                // picked up rollday from day of legal final
                iRollDate = new DateTime(iRollDate.Year, iRollDate.Month, iRollDay);
            }

            double dNextAmt = dblOrigFace;

            // reuse/reset index counter
            iCtr = 0;

            dtFlowBegin = bond.EvalDate;
            //eliminate the accrued interest prior to settlement
            while (dNextAmt > 0.01)
            {
                // empty flow object
                mxCDSCF newflow = new mxCDSCF();
                {
                    newflow.NAmt = dNextAmt;
                    newflow.Factor = newflow.NAmt / dblOrigFace;
                    // cannot state the factor yet, need to add to class
                    //If bondRow.FACE <> 0 Then .FACTOR = .PRINAMT / bondRow.FACE

                    newflow.CFStart = dtFlowBegin;
                    //.INTSTRTDTE = dtFlowBegin
                    if (iCtr == 0)
                    {
                        newflow.IsStub = true;
                        newflow.CFEnd = dtFlowEnd;
                        // .INTENDDTE = dtFlowEnd ' front stub end date found earlier...
                        // note both .cfstart and .cfend must be set prior.. 
                        // calculated rate from yc (only for front stub)
                        newflow.BaseRate = yc.FwdRate(newflow.CFStart, newflow.CFEnd);
                    }
                    else
                    {
                        newflow.IsStub = false;
                        // display FlatIndex from bond object... create during contructor...
                        // this will be displayed and it is used for market standard Flat Index DM pricing
                        newflow.BaseRate = bond.FlatIndex;
                        // .INTENDDTE = tkCalendar.FwdDate(iRollDate, CShort(iCtr * CInt(12 / iCpnPA)))
                        newflow.CFEnd = cal.FwdDate(iRollDate, (short)iCtr * (int)12 / iCpnPA);
                    }

                    newflow.CFPayDate = newflow.CFEnd;
                    // .IPAYDATE = .INTENDDTE


                    //If (dNextAmt < dblOrigFace / 100) Or (iCtr * CInt(12 / iCpnPA) > 359) Then ' 1% Cleanup Call or 30years
                    if ((dNextAmt < dblOrigFace / 100) | (newflow.CFEnd >= bond.LegalFinalMaturity))
                    {
                        // 1% Cleanup Call legal final 
                        // .PPAYAMT = dNextAmt
                        newflow.PrinPayment = dNextAmt;
                    }
                    else
                    {
                        newflow.PrinPayment = System.Math.Round(newflow.NAmt * ((dCPV[l] / 100) / iCpnPA), 2);
                        newflow.CPR = dCPV[l] / 100;
                    }
                    dNextAmt -= newflow.PrinPayment;
                    //.PPAYAMT



                    // roll date for next flow...
                    dtFlowBegin = newflow.CFEnd;
                    // .INTENDDTE

                    if (bond.AswSprd == 0)
                    {
                        newflow.dSpread = bond.Spread;
                        // SPREAD = bondRow.SPREAD / 100
                        newflow.MktIndex = "LIBOR";
                    }
                    else
                    {
                        newflow.dSpread = bond.AswSprd;
                        newflow.MktIndex = "LIBORASW";
                    }

                    newflow.Basis = "A360";
                    newflow.CCY = bond.CCY;
                    iCtr += 1;

                    // l is the index of the iCPV array a monthly (0-359) vector of CPRs
                    l += (int)12 / iCpnPA;
                    // l is the pointer into the CPV() array
                    if (l >= dCPV.GetLength(0))
                    {
                        l = dCPV.GetLength(0) - 1;
                    }
                }
                // dtFlows.AddOPICSDealCashFlowsRow(newFlow)
                Add(newflow);

            }
        }


        // creates a 3M roll schedule from maturity date backwards until settle
        // create a new (overridden) collection class of CDS cash flow objects
        // pass in a mxCDS object, a tkCalendar and a tkYldCurve object
        //Sub New(ByVal mx As mxCDS, ByVal cal As CCalendar, ByVal yc As CYldCurve)
        /// <summary>
        /// contruct mxCDS Flows from a mxCDS row and pass in a YldCurve object.
        /// </summary>
        /// <param name="mx"></param>
        /// <param name="yc"></param>
        public mxCDSCFlows(tkCDS mx, YldCurve yc)
            : base()
        {
            DateTime dtCFStart = default(DateTime);

            DateTime dtCFEnd = mx.dtMaturity;
            DateTime dtEval = mx.EvalDate;
            // this is usually T+1 but user can overide this via the user interface
            //iCtr = 1
            int iMonth = 0;
            if (mx.mxGROUP == "CRDI")
            {
                iMonth = -1;
            }
            else
            {
                iMonth = -3;
            }
            // this is key... passing in an object of type mxCDS we can loop backwards from Maturity date
            // to create the cashflow, so we work from the maturity/expiry date backwards until we create 

            // each cashflows time period  CF Start and CF End...
            double dblProb = 0;
            do
            {
                //dtCFStart =  cal.FwdDate(dtRoll, iCtr * -3)   ' this will generate a 3M roll date prior
                dtCFStart = dtCFEnd.AddMonths(iMonth);
                //cal.FwdDate(dtRoll, iCtr * -3)   ' this will generate a 3M roll date prior
                // survival prob = 1/(1+CleanSPread)^yrs  in Act/360 year format...
                // see definition of CleanSpread elsewhere in this file...
                try
                {
                    dblProb = Math.Pow((1 + mx.CleanSpread), -((double)dtCFEnd.Subtract(yc.ValueDate).Days / 360));
                }
                catch
                {
                    dblProb = Math.Pow((1 + mx.CleanSpread), -((double)dtCFEnd.Subtract(dtEval).Days / 360));
                }

                // this is a twist... dynamically truncate the first coupon because we only PV the remaining stub 
                // then calc and store the Accrued Interest for these cashflows...
                if (dtCFStart < dtEval)
                {
                    // this is key and a good speed up...  store the prev stub date...
                    //new add up to 1m longer front stub...
                    if (dtCFStart.AddMonths(-1) < mx.dtSettle)
                    {
                        Prev = mx.dtSettle;
                    }
                    else
                    {
                        Prev = dtCFStart;
                    }
                    AIDays = dtEval.Subtract(Prev).Days; //.ToOADate - m_prev.ToOADate);
                    // this will dynamically shorten the front stub for eval purposes
                    dtCFStart = dtEval;
                }
                Insert(0, new mxCDSCF(mx.NB, mx.CCY, dtCFStart, dtCFEnd, mx.NotionalAmt, mx.DealSpread, mx.EvalSpread, yc.GetDF2(dtCFEnd, dtEval), dblProb));
                dtCFEnd = dtCFStart;
            }
            while (!(dtCFStart <= dtEval));
        }





        // here: we return the Expected loss of the LAST CDS CF record, get the last item (a cdsCF) and return its EL property 
        // which, btw, is (1- survival prob) 
        public double rnpCLP
        {
            get
            {
                try
                {
                    return this[Count - 1].rnpCLP;  //[this.Count - 1].rnpCLP;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public int cdsAIDays
        {
            get { return AIDays; }
        }

        public DateTime cdsPrevPmtDate
        {
            get { return Prev; }
        }


        // take the pv of a 1BP value
        public double TotalcdspvBPV
        {
            get
            {
                double dTotal = 0;
                foreach (var cf in this)
                {
                    dTotal += cf.cdspvBPV;
                }
                return dTotal;
            }
        }



        public double TotalcdsPVCF
        {
            get
            {
                //mxCDSCF obj = default(mxCDSCF);
                double dTotal = 0;
                foreach (mxCDSCF cf in this)
                {
                    dTotal += cf.cdsPVCF;
                }
                return dTotal;
            }
        }


        public void UpdateMktSpread(double dblMktSpread)
        {
            foreach (mxCDSCF cf in this)
            {
                cf.eSpread = dblMktSpread;
            }
        }


        public void UpdateSurvivalProb(double clnSprd, System.DateTime dtEval)
        {
            double dblPrev = 0;
            //Dim dys As Integer
            foreach (mxCDSCF cf in this)
            {
                //dys = (cf.CFEnd.ToOADate - dtEval.ToOADate)
                //cf.pSurv = ((1 + clnSprd / dys) ^ -(dys * dys / 360)) daily ?
                // continuous...
                {
                    cf.pSurv = Math.Exp(-clnSprd * ((double)cf.CFEnd.Subtract(dtEval).Days / 365)); //.ToOADate - dtEval.ToOADate) / 365);

                    // new as of 5/9/2007 we are storing the MDP (Marginal Default Probability) for tranche pricing purposes 
                    // this as the CDS Cashflow level, it is just the CumLossProb less the previous periods CumLossProbability...
                    cf.MDP = cf.rnpCLP - dblPrev;
                    dblPrev = cf.rnpCLP;
                }
            }
        }

        //public dsOUT.BondCashFlowDataTable BondDMFlows
        public List<BondCashFlow> BondDMFlows
        {
            get
            {

                //dsOUT.BondCashFlowDataTable dt = new dsOUT.BondCashFlowDataTable();
                List<BondCashFlow> dt = new List<BondCashFlow>();

                foreach (mxCDSCF li in this)
                {
                    //dsOUT.BondCashFlowRow row = dt.NewBondCashFlowRow();
                    BondCashFlow row = new BondCashFlow();
                    {
                        row.CFStart = li.CFStart;
                        //
                        row.CFEnd = li.CFEnd;
                        //.CF_End
                        row.CCY = li.CCY;
                        //=
                        row.Index = li.MktIndex;
                        row.Basis = li.Basis;
                        row.Principal = li.NAmt;
                        row.Amortization = li.PrinPayment;
                        row.CPR = li.CPR;
                        row.Factor = li.Factor;
                        row.IndexRate = li.BaseRate;
                        row.ResetMargin = li.dSpread;
                        row.PVFlow = li.DMFlow;
                    }
                    dt.Add(row); // dt.AddBondCashFlowRow(row);
                }
                return dt;
            }
        }

        //public dsOUT.mxCDSCashFlowDisplayDataTable Display
        public List<mxCDSCashFlowDisplay> Display
        {
            get
            {
                //dsOUT.mxCDSCashFlowDisplayDataTable dt = new dsOUT.mxCDSCashFlowDisplayDataTable();

                List<mxCDSCashFlowDisplay> dt = new List<mxCDSCashFlowDisplay>();

                foreach (mxCDSCF li in this)
                {
                    //dsOUT.mxCDSCashFlowDisplayRow row = dt.NewmxCDSCashFlowDisplayRow();
                    mxCDSCashFlowDisplay row = new mxCDSCashFlowDisplay(); // dt.NewmxCDSCashFlowDisplayRow();
                    {
                        row.CFStart = li.CFStart;
                        //
                        row.CFEnd = li.CFEnd;
                        //.CF_End
                        row.Days = (short)li.Dys;
                        //.Days
                        row.CCY = li.CCY;
                        //=
                        row.Notional = li.NAmt;
                        row.CDSSpread = li.dSpread * 100;
                        row.MTMSpread = li.eSpread * 100;
                        row.Change = (li.eSpread - li.dSpread) * 100;
                        row.CDSPrem = li.NetCDSCFPremium;
                        row.Discount = li.DF;
                        row.SurvivalProb = li.pSurv;
                        row.RiskyFlow = li.cdsPVCF;
                    }
                    dt.Add(row); // dt.AddmxCDSCashFlowDisplayRow(row);
                }
                return dt;
            }
        }



        // CPV  class paydown vector (months)  convert a vector string into and array
        private double[] GetClassPayVector(string p_CPV, int iSize = 360)
        {
            double[] iCPV = new double[iSize];
            // array of integer

            // ie p_CPV looks like 20 for 12, 30 for 12 40 for 12, 40
            // parse
            double dCPR = 0;
            // cpr
            int iDur = 0;
            // duration



            try
            {
                if (!p_CPV.Contains(","))
                {
                    //constant CPV... create vector
                    if (!double.TryParse(p_CPV.Trim(), out dCPR))
                    {
                        // 25% default
                        dCPR = (double)0.25;
                    }

                    int j = 0;
                    while (j < iSize)
                    {
                        iCPV[j] = dCPR;
                        j += 1;
                    }
                }
                else

                // ok test for , splitter...
                {
                    string[] arVector = p_CPV.Split(',');
                    int l = 0;

                    // we are looking at the splits here...  '20 for 6'
                    for (int k = 0; k < arVector.Length; k++)
                    {
                        string strPart = arVector[k];
                        // strPart looks like   '20.5 for 12'
                        strPart = strPart.ToUpper();
                        if (strPart.Contains("FOR"))
                        {
                            strPart = strPart.Replace("FOR", "#");
                            string[] strSplit = strPart.Split('#');
                            if (double.TryParse(strSplit[0].ToString().Trim(), out dCPR) && int.TryParse(strSplit[1].ToString().Trim(), out iDur))
                            {
                                for (int j = 1; j <= iDur; j++)
                                {
                                    if (l < iSize)
                                    {
                                        iCPV[l] = dCPR;
                                    }
                                    l += 1;
                                }
                            }
                        }
                        else
                        {
                            if (!double.TryParse(strPart.Trim(), out dCPR))
                            {
                                dCPR = 0.2;
                            }

                            while (l < iSize)
                            {
                                iCPV[l] = dCPR;
                                l += 1;
                            }
                        }
                    }
                }

                return iCPV;
            }
            catch
            {
                // if any errors escape my simple logic, use zero 
                int j = 0;
                while (j < iSize)
                {
                    iCPV[j] = 0;
                    j += 1;
                }
                return iCPV;
            }
        }
    }
}