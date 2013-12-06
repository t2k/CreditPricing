using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;


namespace CreditPricing
{
    /// <summary>
    ///  mxCDS a class to store Murex CDS transaction data
    /// </summary>
    // base class implements a Credit Default Swap 
    public class tkCDS
    {
        private DateTime m_CurveDate;
        private DateTime m_dtEval;
        //evaluation date
        private string m_NB;
        // murex number/tradeID
        private string m_CCY;
        // CCY code
        private string m_Counterparty;
        // mx trading counterparty name
        private string m_Portfolio;
        //mx portfolio
        private double m_NotionalAmt;
        //notional amount of trde
        private System.DateTime m_TradeDate;
        // date traded/transacted
        private System.DateTime m_SettleDate;
        // effective date (usually T+1)
        private System.DateTime m_MaturityDate;
        //  maturity date of risk exposure (usually on the 20th)
        private System.DateTime m_ExpiryDate;
        //  last payment date  could be after the maturity date
        private string m_Instrument;
        // mx instrument/issuer name
        private string m_MktIndex;
        //marker index (SWAPS?)
        private double m_DealSpread;
        // deal/contract rate
        private double m_EvalSpread;
        // evaluate/mkt level  (used for object oriented MTM processing)
        private string m_Trader;
        // trader name from mx
        private string m_PayRecvLeg;
        // pay or recv
        private double m_RecoveryPricing;
        //??
        private double m_RecoveryDefault;
        //??
        private bool m_IsFixedRR;
        // for bespoke simulation purposes
        private double m_sdRR;
        // standard deviation of recovery rates (used primarity for simulation)
        private double m_UpFront;
        // upfront payments 
        private mxCDSCFlows m_CashFlows;
        // cashflow collection
        private CDSCreditCurve m_CDSCurve;
        // new 8/25/2005  
        private string m_TICKER;
        // ticker (Markit®)
        private string m_SENIORITY;
        //SNRFOR or SUBLT2  Markit TIER for curve mapping
        private bool m_SimDefaulted;
        // flag if this CDS has defaulted during MC simulation
        private string m_MXGroup;
        // CDS or CRDI for Index


        //create a CDS object from a .NET mxCDS row object
        /// <summary>
        /// mxCDS constructor from the Entity Framework mxCDS entity
        /// </summary>
        /// <param name="row"></param>
        /// <param name="yc"></param>
        /// <param name="cdsCurves"></param>
        /// <param name="_map"></param>
        public tkCDS(mxCDS row, YldCurve yc, CDSCreditCurves cdsCurves, mpTKRmxMap _map)
        {
            // a line read in for the stream readline 
            m_CurveDate = yc.ValueDate;
            m_dtEval = yc.ValueDate.AddDays(1);
            // t+1 is default practice

            m_Instrument = row.INSTRUMENT;


            char pad = Convert.ToChar(",");

            try
            {
                string strkey = _map[m_Instrument];
                // ie: "CCR,USD,SNRFOR"
                string[] strSplit = strkey.Split(pad);
                // this splits the record into array elements...
                m_TICKER = strSplit[0];
                m_CCY = strSplit[1];
                m_SENIORITY = strSplit[2];
            }

            catch
            {
                m_TICKER = "???";
                m_CCY = "USD";
                m_SENIORITY = "SNRFOR";
            }


            // these are foremost important... tkr, ccy, snrsub
            m_SimDefaulted = false;
            m_NB = row.NB;
            m_MXGroup = row.GROUP;
            m_Counterparty = row.COUNTERPART;
            // .CounterParty
            m_Portfolio = row.PORTFOLIO;
            m_NotionalAmt = (double)row.BRW_NOM1;
            //NotionalAmount ' * 10 ^ 6
            //new dates get converted to DATE format during the load...
            m_SettleDate = (DateTime)row.BRW_SDTE;
            //.TradeDate
            m_MaturityDate = (DateTime)row.MATURITY;
            // .MaturityDate
            m_ExpiryDate = (DateTime)row.EXPIRY;
            m_MktIndex = row.MARKET_INDEX;
            // "LIBOR"
            m_DealSpread = (double)row.RATE_MARG0;
            // ..DealRate / 100 ' CType(mxFieldArray(16), Double)
            m_EvalSpread = (double)row.RATE_MARG0;
            //.DealRate / 100 'm_DealSpread + CDbl(0.01)

            m_Trader = row.TRADER;
            // mxFieldArray(18)

            m_PayRecvLeg = row.PayRecvLeg;
            //.PayRecv 'mxFieldArray(21)
            m_RecoveryPricing = (double)0.4;
            m_RecoveryDefault = (double)0.4;
            //row.Recovery
            m_UpFront = (double)row.STL_FLW;
            //.UpfrontPaymen

            m_sdRR = (double)0.05;
            m_IsFixedRR = false;
            // row.IsFixedRR

            if (m_PayRecvLeg == "Pay")
            {
                //(mxFieldArray(8), Double)
                m_NotionalAmt = -m_NotionalAmt;
            }

            // new FEB 08  use 0 for recovery in pricing defalults and reverse the sign of upfront
            if (m_MXGroup == "CRDI")
            {
                m_UpFront = -m_UpFront;
                m_RecoveryPricing = 0;
            }

            // assign a spread curve...
            GetCDSCurve(cdsCurves);
            // create the collection of cashflows 

            // for CMBX trades we store the WAL date in the 6M curvepoint
            if (m_MXGroup == "CRDI")
            {
                double dTmp = m_CDSCurve.CurvePoints[0].Market.BestBid / 100;
                m_MaturityDate = DateTime.FromOADate(dTmp);
                m_ExpiryDate = m_MaturityDate;
            }

            m_CashFlows = new mxCDSCFlows(this, yc);
        }


        // alt object constructor!  create a new object from a PortTradesRow object
        // NEW 9/13/2005  
        public tkCDS(PortfolioTrade row, Calendar cal, YldCurve yc, CDSCreditCurves cdsCurves)
        {

            try
            {
                m_CurveDate = yc.ValueDate;
                m_dtEval = yc.ValueDate.AddDays(1);
            }
            catch
            {
            }

            // add error checking try/Catch
            try
            {
                // these are foremost important... tkr, ccy, snrsub
                m_TICKER = row.TICKER;
                m_CCY = row.CCY;
                m_SENIORITY = row.SnrSub;
                m_SimDefaulted = false;
                m_NB = row.NB.ToString();
                m_Counterparty = row.CounterParty;
                m_Portfolio = row.PortfolioName;
                m_NotionalAmt = (double)row.NotionalAmount;
                // * 10 ^ 6
                //new dates get converted to DATE format during the load...
                m_SettleDate = (DateTime)row.TradeDate;
                m_MaturityDate = (DateTime)row.MaturityDate;
                m_ExpiryDate = (DateTime)row.ExpireDate;
                m_Instrument = "N/A";
                m_MktIndex = "LIBOR";
                m_DealSpread = (double)row.DealRate / 100;
                // CType(mxFieldArray(16), Double)
                m_EvalSpread = (double)row.DealRate / 100;
                //m_DealSpread + CDbl(0.01)
                m_Trader = row.Trader;
                // mxFieldArray(18)
                m_PayRecvLeg = row.PayRecv;
                //mxFieldArray(21)
                m_RecoveryPricing = (double)0.4;
                m_RecoveryDefault = (double)row.Recovery;
                m_UpFront = (double)row.UpfrontPayment;
                m_sdRR = (double)0.05;
                m_IsFixedRR = (bool)row.IsFixedRR;
            }
            catch
            {
                m_IsFixedRR = false;
            }

            // assign a spread curve...
            GetCDSCurve(cdsCurves);
            // create the collection of cashflows 
            m_CashFlows = new mxCDSCFlows(this, yc);
        }



        // IMPORTANT WORK HERE FOR MUREX CDS creation
        //this is the main contructor when loading MUREX trades from the .mxD download)
        // object constructor!  create a new object from a semi-colon delimited file (.mxD)
        // NEW 8/26/2005   pass in a CDSCreditCurve object, 
        // prior to Nov 23, 05 the dates were downloaded like YYYYMMDD
        // after Nov 23 2005 date look like mm/dd/yy
        //NEW 2/13/2007:  TradeDate is field#9 and SettleDate is a NEWLY download field#23 
        //NEW FEB 12 2007;  we downloaded an extra column #23 (EffectiveDate) the T+1 settledate
        // note: AFTER 2/12/07 we download the Settledate and TradeDate
        public tkCDS(string strMXRecord, YldCurve yc, CDSCreditCurves cdsCurves, mpTKRmxMap tkrMAP)
        {
            // a line read in for the stream readline 
            char pad = '\0';
            pad = Convert.ToChar(";");
            string[] mxFieldArray = strMXRecord.Split(pad);
            // this splits the record into array elements...

            // default the PV Evaluation date for CDS to T+1 
            try
            {
                m_CurveDate = yc.ValueDate;
                m_dtEval = yc.ValueDate.AddDays(1);
            }
            catch
            {
            }

            string strDate = null;
            // parse MX formatted date strings...

            // add error checking try/Catch
            try
            {
                m_MXGroup = mxFieldArray[4];
                m_SimDefaulted = false;
                m_NB = mxFieldArray[0];
                m_CCY = mxFieldArray[2];
                m_Counterparty = mxFieldArray[6];
                m_Portfolio = mxFieldArray[7];
                m_NotionalAmt = Double.Parse(mxFieldArray[8]);
                m_sdRR = 0.05;
                //new dates get converted to DATE format during the load...
                // prior to Nov 23, 05 the dates were downloaded like YYYYMMDD
                if (yc.ValueDate < new DateTime(2005, 11, 23))
                {
                    // don't ask!  it appears a new release of MX began formatting dates differently than before!?
                    strDate = mxFieldArray[9];
                    // old method was YYYY
                    m_SettleDate = new DateTime(int.Parse(strDate.Substring(0, 4)), int.Parse(strDate.Substring(5, 2)), int.Parse(strDate.Substring(strDate.Length - 1, 2)));
                    m_TradeDate = m_SettleDate;
                    m_SettleDate = m_TradeDate.AddDays(1);

                    strDate = mxFieldArray[10];
                    m_MaturityDate = new DateTime(int.Parse(strDate.Substring(0, 4)), int.Parse(strDate.Substring(5, 2)), int.Parse(strDate.Substring(strDate.Length - 1, 2))); //new DateTime((int)Strings.Left(strDate, 4), (int)Strings.Mid(strDate, 5, 2), (int)Strings.Right(strDate, 2)).Date;
                    //            
                    strDate = mxFieldArray[11];
                    m_ExpiryDate = new DateTime(int.Parse(strDate.Substring(0, 4)), int.Parse(strDate.Substring(5, 2)), int.Parse(strDate.Substring(strDate.Length - 1, 2))); // new DateTime((int)Strings.Left(strDate, 4), (int)Strings.Mid(strDate, 5, 2), (int)Strings.Right(strDate, 2)).Date;
                }
                else
                {
                    // after Nov 23 2005 date look like mm/dd/yy
                    // now after Jun 11 2007 dates look like mm/dd/yyyy
                    string[] strDateParts = null;

                    try
                    {

                        //NEW 2/13/2007:  TradeDate is field#9 and SettleDate is a NEWLY download field#23 
                        //NEW FEB 12 2007;  we downloaded an extra column #23 (EffectiveDate) the T+1 settledate
                        // note: AFTER 2/12/07 we download the Settledate and TradeDate
                        // this was off in the prior dated downloads... oh well...
                        // the accruals were off by one day typically
                        strDate = mxFieldArray[23].Trim();
                        strDateParts = strDate.Split('/');
                        m_SettleDate = new DateTime(int.Parse(strDateParts[2]), int.Parse(strDateParts[0]), int.Parse(strDateParts[1]));
                    }
                    catch
                    {
                        // otherwise just add one day to the trade date (TRN.DATE in MUREX)
                        strDate = mxFieldArray[9].Trim();
                        strDateParts = strDate.Split('/');
                        m_SettleDate = new DateTime(int.Parse(strDateParts[2]), int.Parse(strDateParts[0]), int.Parse(strDateParts[1]));
                        m_SettleDate = m_SettleDate.AddDays(1);
                    }

                    // old method was YYYY
                    //m_SettleDate = New DateTime(CInt(Left(strDate, 4)), CInt(Mid(strDate, 5, 2)), CInt(Right(strDate, 2))).Date
                    // new method = MM/DD/YY like 09/12/05  
                    strDate = mxFieldArray[9].Trim();
                    strDateParts = strDate.Split('/');

                    m_TradeDate = new DateTime(int.Parse(strDateParts[2]), int.Parse(strDateParts[0]), int.Parse(strDateParts[1]));
                    strDate = mxFieldArray[10].Trim();
                    strDateParts = strDate.Split('/');
                    m_MaturityDate = new DateTime(int.Parse(strDateParts[2]), int.Parse(strDateParts[0]), int.Parse(strDateParts[1]));

                    strDate = mxFieldArray[11].Trim();
                    strDateParts = strDate.Split('/');
                    m_ExpiryDate = new DateTime(int.Parse(strDateParts[2]), int.Parse(strDateParts[0]), int.Parse(strDateParts[1]));
                }

                m_Instrument = mxFieldArray[14];

                try
                {
                    m_TICKER = tkrMAP[m_Instrument];
                }
                catch
                {
                    m_TICKER = "???";
                }

                m_MktIndex = mxFieldArray[15];
                m_DealSpread = double.Parse(mxFieldArray[16]);
                m_EvalSpread = m_DealSpread;
                // not that important during constructor
                m_Trader = mxFieldArray[18];
                m_PayRecvLeg = mxFieldArray[21];
                m_RecoveryPricing = (double)0.4;
                //defaults to .4
                m_IsFixedRR = false;
                m_SENIORITY = "SNRFOR";

                if (m_PayRecvLeg == "Pay")
                {
                    m_NotionalAmt = -double.Parse(mxFieldArray[8]);
                }
                else
                {
                    m_NotionalAmt = double.Parse(mxFieldArray[8]); //,m_NotionalAmt) ;
                }


                // add this just in case,  the UPFRONT fee col was added after a week or so...
                try
                {
                    m_UpFront = double.Parse(mxFieldArray[22]);
                }
                catch
                {
                    m_UpFront = (double)0;
                }

                // new FEB 08  use 0 for recovery in pricing defalults and reverse the sign of upfront
                if (m_MXGroup == "CRDI")
                {
                    m_UpFront = -m_UpFront;
                    m_RecoveryPricing = 0;
                }
            }
            catch
            {
            }
            // do something about it bub...

            GetCDSCurve(cdsCurves);

            // here's a trick!  we store the INDEX WAL Maturity date in the _6M field which is always the first, index=0, curvepoint...
            // if its and Index we can use this for now...
            if (m_MXGroup == "CRDI")
            {
                double dTmp = m_CDSCurve.CurvePoints[0].Market.BestBid / 100;
                m_MaturityDate = DateTime.FromOADate(dTmp);
            }

            m_CashFlows = new mxCDSCFlows(this, yc);
        }

        public string mxGROUP
        {
            get { return m_MXGroup; }
        }

        public string RefEntity
        {
            get
            {
                try
                {
                    return m_CDSCurve.RefEnt;
                }
                catch
                {
                    return "000-CDS Curve Not Assigned";
                }
            }
        }

        public string CDSTicker
        {
            get
            {
                try
                {
                    return m_CDSCurve.CDS5YrTKR;
                }
                catch
                {
                    return "CDS Curve N/A";
                }
            }
        }





        public string Sector
        {
            get
            {

                try
                {
                    return m_CDSCurve.Sector;
                }
                catch
                {
                    return "CDS Curve N/A";
                }
            }
        }

        public string SENIORITY
        {
            get { return m_SENIORITY; }
        }


        public string Ticker
        {
            get { return m_TICKER; }
        }
        //return the # days in the holding period 

        public int cdsHoldingPeriodDys
        {
            // m_dtEval, go private, why not it's a little quicker...  
            get { return Math.Max(1, m_dtEval.Subtract(dtSettle).Days); }
            // note holding period must be >0 days
        }


        // returns the previous settlement date...
        public DateTime cdsPrevSettleDate
        {
            get { return Cashflows.cdsPrevPmtDate; }
        }


        // returns the accrued interest used for trade settlement purposes...
        public int cdsAIDays
        {
            get { return Cashflows.cdsAIDays; }
        }

        // standard deviation of recovery rates (used during simulation) defaults to 5%
        public double sdRR
        {
            get { return m_sdRR; }
            set { m_sdRR = value; }
        }

        // return the upfront payment amount...
        public double cdsUpFrontAmt
        {
            get { return m_UpFront; }
            set { m_UpFront = value; }
        }


        public DateTime CurveDate
        {
            get { return m_CurveDate; }
        }

        // this is the EVALUATION date or MTM date, usually T+1 for CDS 
        // gets set during contructor...
        public System.DateTime EvalDate
        {
            get { return m_dtEval; }
            set { m_dtEval = value; }
        }



        // boolean value is generally passed in via the interface it a checkbox says USE MID
        // if true then use mid else cross the market spread...
        public void ReDiscount(double dSprdBP, YldCurve yc, Calendar cal, RealTimePrices pr)
        {
            //YldCurve yc2 = default(YldCurve);

            YldCurve yc2 = yc.Copy;
            // copy base curve to parallel shifted curve ie +40BP

            // the shifted curve  converting a double to a short so it could fail
            yc2.BPShift = dSprdBP;

            yc2.Recalc(pr);
            // recalc

            //mxCDSCF cf = default(mxCDSCF);
            foreach (mxCDSCF cf in Cashflows)
            {
                cf.DF = yc2.GetDF2(cf.CFEnd, this.m_dtEval);
            }
            // this will be passed in as BP ie 40 BP = .40%  EvalSpread takes .40
            EvalSpread = dSprdBP / 100;
        }


        public void Reprice(bool blnUseMid)
        {
            if (m_CDSCurve == null)
            {
                EvalSpread = (double)1;
                return;
            }


            if (!blnUseMid)
            {
                if (this.NotionalAmt > 0)
                {
                    //not using mid and notl <0  we are short and must hid the bid to get out at the 'market' price
                    EvalSpread = m_CDSCurve.cdsAsk(m_ExpiryDate);
                }
                else
                {
                    EvalSpread = m_CDSCurve.cdsBid(m_ExpiryDate);
                }
                //not using mid and notl >0  we are long and must take the offer to get out at the 'market' price
            }
            else
            {
                // take mid rates
                try
                {
                    EvalSpread = m_CDSCurve.cdsMid(m_ExpiryDate);
                }
                catch
                {
                    EvalSpread = 0.001;
                }
            }
        }


        // setting this property DOES A LOT OF WORK!!!
        // it updates the mtm spread field for all cashflows and
        // updates then updates/recalcs the survival probability
        public double EvalSpread
        {
            get { return m_EvalSpread; }

            set
            {
                // this is good to follow... when updating the eval/mtm spread on a CDS trade this will auto
                //update the CASHFLOWS with the new spreads and the survival probabilities... (they are affected by mtm spread)
                // spreads must be positive...

                m_EvalSpread = value;
                Cashflows.UpdateMktSpread(value);
                Cashflows.UpdateSurvivalProb(CleanSpread, m_dtEval);
            }
        }



        //CleanSpread is the (mtm spread) / (1-recoveryrate)
        // this gets updated any time the evalspread is changes...
        public double CleanSpread
        {
            get
            {
                //Return (m_EvalSpread / 100) * (1 - m_Recovery) ^ -1
                try
                {
                    return (m_EvalSpread / 100) / (1 - m_RecoveryPricing);
                }
                catch
                {
                    return (m_EvalSpread / 100) / (0.6);
                }
            }
        }

        public bool IsFixedRR
        {
            get { return m_IsFixedRR; }
            set { m_IsFixedRR = false; }
        }

        public double RecoveryOnDefault
        {
            get { return m_RecoveryDefault; }
            set { m_RecoveryDefault = value; }
        }

        // recovery rate 0 to 1
        public double Recovery
        {
            get { return m_RecoveryPricing; }
            set
            {
                m_RecoveryPricing = value;
                Cashflows.UpdateSurvivalProb(CleanSpread, this.m_dtEval);
            }
        }


        // trade id/Murex Number
        public string NB
        {
            get { return m_NB; }
            set { m_NB = value; }
        }


        //currency code
        public string CCY
        {
            get
            {
                if (string.IsNullOrEmpty(m_CCY))
                {
                    return "USD";
                }
                else
                {
                    return m_CCY;
                }
            }
            set { m_CCY = value; }
        }


        //counterparty
        public string Counterparty
        {
            get { return m_Counterparty; }
            set { m_Counterparty = value; }
        }


        // name of the portfolio the CDS belongs...
        public string Portfolio
        {
            get { return m_Portfolio; }
            set { m_Portfolio = value; }
        }


        // notional amount of CDS
        public double NotionalAmt
        {
            get { return m_NotionalAmt; }
            set { m_NotionalAmt = value; }
        }

        // return the TRADE DATE from the MUREX supplied STRING representation
        public DateTime dtTraded
        {
            get { return m_TradeDate; }
        }

        // return a DATE from the MUREX supplied STRING representation
        public DateTime dtSettle
        {
            get { return m_SettleDate; }
        }


        // return a DATE from the MUREX supplied STRING representation
        public DateTime dtMaturity
        {
            get { return m_MaturityDate; }
        }



        // the maturity date as passed in by MUREX... differs from the Expiry date if not a valid business date..
        //Property MaturityDate() As String
        //    Get
        //        Return m_MaturityDate
        //    End Get
        //    Set(ByVal Value As String)
        //        m_MaturityDate = Value
        //    End Set
        //End Property


        // the maturity settlement date...


        // DATE format
        public DateTime dtExpire
        {

            get { return m_ExpiryDate; }
        }

        // this is the CREDIT ISSUER/TICKER in MUREX
        public string Instrument
        {
            get { return m_Instrument; }
            set { m_Instrument = value; }
        }

        //Mkt INDEX ie SWAPS...
        public string MktIndex
        {
            get { return m_MktIndex; }
            set { m_MktIndex = value; }
        }


        // DEAL/CONTRACT SPREAD
        public double DealSpread
        {
            get { return m_DealSpread; }
            set { m_DealSpread = value; }
        }



        //TRADER ID
        public string Trader
        {
            get { return m_Trader; }
            set { m_Trader = value; }
        }


        // PAY/RECV INDICATOR
        public string PayRecvLeg
        {
            get { return m_PayRecvLeg; }
            set { m_PayRecvLeg = value; }
        }


        public DataSet GetDataSet
        {
            get
            {
                DataSet ds = new DataSet("CDSFlows");
                System.Data.DataTable tbl = ds.Tables.Add("CDSFlows");
                tbl.Columns.Add("CFStart", Type.GetType("System.DateTime"));
                tbl.Columns.Add("CFEnd", Type.GetType("System.DateTime"));
                tbl.Columns.Add("Days", Type.GetType("System.Int32"));
                tbl.Columns.Add("CCY", Type.GetType("System.String"));
                tbl.Columns.Add("Notional", Type.GetType("System.Double"));
                tbl.Columns.Add("OrigSprd", Type.GetType("System.Double"));
                tbl.Columns.Add("CurrSprd", Type.GetType("System.Double"));
                tbl.Columns.Add("CDS Premium", Type.GetType("System.Double"));
                tbl.Columns.Add("df", Type.GetType("System.Double"));
                tbl.Columns.Add("pSurv", Type.GetType("System.Double"));
                tbl.Columns.Add("pvCF", Type.GetType("System.Double"));

                foreach (mxCDSCF cf in m_CashFlows)
                {

                    DataRow row = tbl.NewRow();
                    row[0] = cf.CFStart;
                    row[1] = cf.CFEnd;
                    row[2] = cf.Dys;
                    row[3] = cf.CCY;
                    row[4] = cf.NAmt / Math.Pow(10, 6);
                    row[5] = cf.dSpread * 100;
                    row[6] = cf.eSpread * 100;
                    row[7] = cf.NetCDSCFPremium;
                    row[8] = cf.DF;
                    row[9] = cf.pSurv;
                    row[10] = cf.cdsPVCF;
                    tbl.Rows.Add(row);
                }
                return ds;
            }
        }

        //
        // cumulative Loss Probability in a Continuous compounding framework, using full spread curve
        public double CLP_CC_Curve(DateTime dt)
        {
            double dys = 360;

            {
                try
                {
                    return 1 - Math.Exp(-(Math.Max(m_CDSCurve.cdsMid(dt) / 100, 0.0001)) / (1 - m_RecoveryPricing) * ((double)dt.Subtract(EvalDate).Days) / dys);
                }
                catch
                {
                    return 1 - Math.Exp(-(0.0001) / (1 - m_RecoveryPricing) * ((double)dt.Subtract(EvalDate).Days) / dys);
                }
            }
            //Return 1 - Math.Exp(-(dt.ToOADate - EvalDate.ToOADate) / 360 * CleanSpread)
        }



        //was to be used for simulation but let's hold off for now...
        // cumulative Loss Probability in a Continuos compounding framework
        public double CLP_CC(DateTime dt)
        {
            double dys = 360;
            return 1 - Math.Exp(-(CleanSpread * ((double)dt.Subtract(EvalDate).Days) / dys));
            //Return 1 - Math.E ^ -(CleanSpread * (dt.ToOADate - EvalDate.ToOADate) / 360)
        }


        public double rnpCLP
        {
            get { return m_CashFlows.rnpCLP; }
        }


        public mxCDSCFlows Cashflows
        {
            get { return m_CashFlows; }

            set { m_CashFlows = value; }
        }


        //USED TO QUICKLY VIEW DATA in a listbox
        //public string ListView1
        //{
        //    get
        //    {
        //        char pad = '\0';
        //        pad = Convert.ToChar(" ");
        //        //Return String.Format("{0}{1}{2}{3}{4} {5} MM to{6} {7} mx#{8}", CDSPrice.Ticker.PadRight(8, pad), Instrument.PadRight(13, pad), Counterparty.PadRight(13, pad), Left(PayRecvLeg, 3).PadRight(5, pad), Format(DealSpread, "0.0000"), Format(NotionalAmt, "##0,,").PadLeft(4, pad), dtMaturity.ToShortDateString.PadLeft(11, pad), Portfolio.PadRight(15, pad), NB)
        //        return string.Format("{0}{1}{2}{3}{4} {5} MM to{6} {7} mx#{8}", Ticker.PadRight(8, pad), Instrument.PadRight(13, pad), Counterparty.PadRight(13, pad), Strings.Left(PayRecvLeg, 3).PadRight(5, pad), Strings.Format(DealSpread, "0.0000"), Strings.Format(NotionalAmt, "##0,,").PadLeft(4, pad), dtMaturity.ToShortDateString.PadLeft(11, pad), Portfolio.PadRight(15, pad), NB
        //        );
        //    }
        //    //Me.CDSPrice.Ticker.PadRight(10, pad)
        //    //1& Instrument.PadRight(13, pad) 
        //    //2& Me.Counterparty.PadRight(12, pad) 
        //    //3& Left(PayRecvLeg, 3).PadRight(5, pad) 
        //    //4& Format(DealSpread, "0.0000") 
        //    //5& " on " & Format(NotionalAmt, "##0,,.0").PadLeft(6, pad) 
        //    //6& "MM  to " & dtMaturity.ToShortDateString.PadLeft(11, pad) 
        //    //7& " " & Portfolio.PadRight(16, pad) 
        //    //8& " mx#" & NB
        //}


        //calc the total return over the entire holding period...
        public double cdsTotalReturn
        {
            get
            {
                try
                {
                    return (cdsHPTotalPL * 365 / cdsHoldingPeriodDys) / Math.Abs(NotionalAmt);
                }
                catch
                {
                    return 0;
                }
            }
        }

        //holding period carry
        public double cdsHPCarry
        {
            //Return m_NotionalAmt * (m_dtEval.ToOADate - dtSettle.ToOADate) / 360 * DealSpread / 100
            get { return m_NotionalAmt * cdsHoldingPeriodDys / 360 * DealSpread / 100; }
        }


        // calculate the AI settlement interest for trade settlement
        public double cdsAI
        {
            // note the cdsAIDays looks to the CASHFLOWS collection for its value...
            get { return m_NotionalAmt * cdsAIDays / 360 * DealSpread / 100; }
        }

        // 
        public double cdsSettlementPL
        {
            get { return cdsPVCF + cdsAI; }
        }

        // return the cds total Holding period P&L
        public double cdsHPTotalPL
        {
            get { return cdsUpFrontAmt + cdsHPCarry + cdsPVCF; }
        }

        public double cdsPVCF
        {
            get { return this.Cashflows.TotalcdsPVCF; }
        }

        public double cdsCrBPV
        {
            get { return Cashflows.TotalcdspvBPV; }
            // not too elegant?
        }

        //public dsOUT.mxCDSDisplayDataTable DisplayMxCDS
        public List<mxCDSDisplay> DisplayMxCDS
        {
            get
            {

                List<mxCDSDisplay> dt = new List<mxCDSDisplay>();

                //dsOUT.mxCDSDisplayDataTable r = new dsOUT.mxCDSDisplayDataTable();
                mxCDSDisplay row = new mxCDSDisplay();
                //dsOUT.mxCDSDisplayRow row = r.NewmxCDSDisplayRow();
                {
                    row.TradeID = NB;
                    //String.Format("#{0} Markit {1} - {2}", NB, Ticker, Instrument) 'Me.NBcoll.Add(New View1("TKR/Issuer:", Ticker & " - " & Instrument & " - mx#" & NB))
                    row.Instrument = Instrument;
                    row.Markit = CDSCurve.CurveKeyName;  // .Name;
                    row.CounterParty = Counterparty;
                    //coll.Add(New View1("CtrParty:", Counterparty))
                    row.SettleDate = dtSettle;
                    //.Add(New View1("Trade Date:", Me.dtTraded.ToShortDateString))
                    row.MaturityDate = dtMaturity;
                    //coll.Add(New View1("Maturity Date:", dtMaturity.ToShortDateString & " - " & System.Math.Round(System.DateTime.FromOADate(dtMaturity.ToOADate - dtSettle.ToOADate).ToOADate / 365, 1) & " yrs(orig) " & System.Math.Round(System.DateTime.FromOADate(dtExpire.ToOADate - DateTime.Today.ToOADate).ToOADate / 365, 1) & " yrs(rem)"))
                    row.Notional = NotionalAmt;
                    //coll.Add(New View1("Principal:", Format(NotionalAmt, "n") & " - " & CCY))
                    row.CDSSpread = DealSpread * 100;
                    //coll.Add(New View1("DealSpread:", PayRecvLeg & " " & DealSpread))
                    row.mxPortfolio = Portfolio;
                    row.CDSMTMSpread = EvalSpread * 100;
                    //Try
                    //    .CDS_MTM_Spread = CDSCurve.cdsBid(dtExpire) ' ' coll.Add(New View1("Pricing:", String.Format("TICKER={0} Markit={1:n}", Ticker, CDSCurve.cdsBid(Me.dtExpire))))
                    //Catch ex As Exception
                    //    .CDS_MTM_Spread = 0 'coll.Add(New View1("Pricing:", String.Format("TICKER={0} w/ no Markit® Curve mapped", Ticker)))
                    //End Try
                    row.Upfront = cdsUpFrontAmt;
                    //coll.Add(New View1("Upfront:", Format(cdsUpFrontAmt, "n").PadLeft(15, pad)))
                    row.HPCarry = cdsHPCarry;
                    //.Add(New View1("Carry HP:", Format(cdsHPCarry, "n").PadLeft(15, pad) & "  (carry to " & m_dtEval.ToShortDateString & " = " & cdsHoldingPeriodDys.ToString & " Days)"))
                    row.MarketValue = cdsPVCF;
                    //coll.Add(New View1("Market Value:", Format(cdsPVCF, "n").PadLeft(15, pad)))
                    row.HP_PL = cdsUpFrontAmt + cdsHPCarry + cdsPVCF;
                    //, "n").PadLeft(15, pad)))
                    row.CreditBPV = cdsCrBPV;
                    //coll.Add(New View1("CREDIT BPV:", Format(cdsCrBPV, "n").PadLeft(15, pad)))
                    row.TotalReturn = cdsTotalReturn;
                    //coll.Add(New View1("Total Return:", Format(cdsTotalReturn, "Percent").PadLeft(15, pad)))
                    row.Recovery = Recovery;
                    //coll.Add(New View1("RNP CumLoss:", Format(rnpCLP, "Percent").PadLeft(15, pad)))
                    row.AISettlement = cdsAI;
                    //coll.Add(New View1("Accured Int:", Format(Me.cdsAI, "n").PadLeft(15, pad) & "  (" & Me.cdsAIDays.ToString & " days " & Me.cdsPrevSettleDate.ToShortDateString & " to " & m_dtEval.ToShortDateString & ")"))
                    //coll.Add(New View1("MV at Closeout:", Format(Me.cdsSettlementPL, "n").PadLeft(15, pad)))
                    row.CloseOutMV = cdsSettlementPL;
                }
                dt.Add(row); // r.AddmxCDSDisplayRow(row);
                return dt; //return r;
            }
        }


        //public dsOUT.mxCDSCloseOutDataTable DisplayMxCDSCloseout(DateTime _dateCloseout)
        public List<mxCDSCloseOut> DisplayMxCDSCloseout(DateTime _dateCloseout)
        {
            {
                //dsOUT.mxCDSCloseOutDataTable r = new dsOUT.mxCDSCloseOutDataTable();
                List<mxCDSCloseOut> dt = new List<mxCDSCloseOut>();
                //dsOUT.mxCDSCloseOutRow row = r.NewmxCDSCloseOutRow();
                mxCDSCloseOut row = new mxCDSCloseOut();
                {
                    row.TradeID = NB;
                    //String.Format("#{0} Markit {1} - {2}", NB, Ticker, Instrument) 'Me.NBcoll.Add(New View1("TKR/Issuer:", Ticker & " - " & Instrument & " - mx#" & NB))
                    row.Instrument = Instrument;
                    row.Markit = CDSCurve.CurveKeyName;
                    row.CounterParty = Counterparty;
                    //coll.Add(New View1("CtrParty:", Counterparty))
                    row.OrigTradeDate = dtTraded;
                    row.OrigSettleDate = dtSettle;
                    //.Add(New View1("Trade Date:", Me.dtTraded.ToShortDateString))
                    row.MaturityDate = dtMaturity;
                    //coll.Add(New View1("Maturity Date:", dtMaturity.ToShortDateString & " - " & System.Math.Round(System.DateTime.FromOADate(dtMaturity.ToOADate - dtSettle.ToOADate).ToOADate / 365, 1) & " yrs(orig) " & System.Math.Round(System.DateTime.FromOADate(dtExpire.ToOADate - DateTime.Today.ToOADate).ToOADate / 365, 1) & " yrs(rem)"))
                    row.ValuationDate = _dateCloseout;
                    row.Notional = NotionalAmt;
                    //coll.Add(New View1("Principal:", Format(NotionalAmt, "n") & " - " & CCY))
                    row.CDSSpread = DealSpread * 100;
                    //coll.Add(New View1("DealSpread:", PayRecvLeg & " " & DealSpread))
                    row.mxPortfolio = Portfolio;
                    row.ValuationSpread = EvalSpread * 100;
                    //Try
                    //    .CDS_MTM_Spread = CDSCurve.cdsBid(dtExpire) ' ' coll.Add(New View1("Pricing:", String.Format("TICKER={0} Markit={1:n}", Ticker, CDSCurve.cdsBid(Me.dtExpire))))
                    //Catch ex As Exception
                    //    .CDS_MTM_Spread = 0 'coll.Add(New View1("Pricing:", String.Format("TICKER={0} w/ no Markit® Curve mapped", Ticker)))
                    //End Try
                    row.UpFront = cdsUpFrontAmt;
                    //coll.Add(New View1("Upfront:", Format(cdsUpFrontAmt, "n").PadLeft(15, pad)))
                    row.HPCarry = cdsHPCarry;
                    //.Add(New View1("Carry HP:", Format(cdsHPCarry, "n").PadLeft(15, pad) & "  (carry to " & m_dtEval.ToShortDateString & " = " & cdsHoldingPeriodDys.ToString & " Days)"))
                    row.MarketValue = cdsPVCF;
                    //coll.Add(New View1("Market Value:", Format(cdsPVCF, "n").PadLeft(15, pad)))
                    row.HP_PL = cdsUpFrontAmt + cdsHPCarry + cdsPVCF;
                    //, "n").PadLeft(15, pad)))
                    row.CreditBPV = cdsCrBPV;
                    //coll.Add(New View1("CREDIT BPV:", Format(cdsCrBPV, "n").PadLeft(15, pad)))
                    row.TotalReturn = cdsTotalReturn;
                    //coll.Add(New View1("Total Return:", Format(cdsTotalReturn, "Percent").PadLeft(15, pad)))
                    row.Recovery = Recovery;
                    //coll.Add(New View1("RNP CumLoss:", Format(rnpCLP, "Percent").PadLeft(15, pad)))
                    row.CloseoutAI = this.cdsAI;
                    // System.Math.Max(_dateCloseout.ToOADate - Me.cdsPrevSettleDate.ToOADate, 0) / 360 * Me.DealSpread / 100 ' cdsAI 'coll.Add(New View1("Accured Int:", Format(Me.cdsAI, "n").PadLeft(15, pad) & "  (" & Me.cdsAIDays.ToString & " days " & Me.cdsPrevSettleDate.ToShortDateString & " to " & m_dtEval.ToShortDateString & ")"))
                    //.cdsPVCF + .Closeout_AI ' cdsSettlementPL 'coll.Add(New View1("MV at Closeout:", Format(Me.cdsSettlementPL, "n").PadLeft(15, pad)))
                    row.CloseOutMV = this.cdsSettlementPL;
                }
                dt.Add(row); // r.AddmxCDSCloseOutRow(row);
                return dt; // return r;
            }
        }


        //returns a Collection object for grid binding
        // a simple 2d view used for grid binding...
        //Function View() As Collection
        //    Dim pad As Char
        //    pad = Convert.ToChar(" ")
        //    Dim coll As New Collection
        //    coll.Add(New View1("TKR/Issuer:", Ticker & " - " & Instrument & " - mx#" & NB))
        //    coll.Add(New View1("CtrParty:", Counterparty))
        //    coll.Add(New View1("Trade Date:", Me.dtTraded.ToShortDateString))
        //    coll.Add(New View1("Effective Date:", dtSettle.ToShortDateString))
        //    coll.Add(New View1("Maturity Date:", dtMaturity.ToShortDateString & " - " & System.Math.Round(System.DateTime.FromOADate(dtMaturity.ToOADate - dtSettle.ToOADate).ToOADate / 365, 1) & " yrs(orig) " & System.Math.Round(System.DateTime.FromOADate(dtExpire.ToOADate - DateTime.Today.ToOADate).ToOADate / 365, 1) & " yrs(rem)"))
        //    coll.Add(New View1("Principal:", Format(NotionalAmt, "n") & " - " & CCY))
        //    coll.Add(New View1("DealSpread:", PayRecvLeg & " " & DealSpread))
        //    coll.Add(New View1("Portfolio:", Portfolio & " - " & MktIndex))
        //    Try
        //        coll.Add(New View1("Pricing:", String.Format("TICKER={0} Markit={1:n}", Ticker, CDSCurve.cdsBid(Me.dtExpire))))
        //    Catch ex As Exception
        //        coll.Add(New View1("Pricing:", String.Format("TICKER={0} w/ no Markit® Curve mapped", Ticker)))
        //    End Try
        //    coll.Add(New View1("Upfront:", Format(cdsUpFrontAmt, "n").PadLeft(15, pad)))
        //    coll.Add(New View1("Carry HP:", Format(cdsHPCarry, "n").PadLeft(15, pad) & "  (carry to " & m_dtEval.ToShortDateString & " = " & cdsHoldingPeriodDys.ToString & " Days)"))
        //    coll.Add(New View1("Market Value:", Format(cdsPVCF, "n").PadLeft(15, pad)))
        //    coll.Add(New View1("Total HP P&L:", Format(cdsUpFrontAmt + cdsHPCarry + cdsPVCF, "n").PadLeft(15, pad)))
        //    coll.Add(New View1("CREDIT BPV:", Format(cdsCrBPV, "n").PadLeft(15, pad)))
        //    coll.Add(New View1("Total Return:", Format(cdsTotalReturn, "Percent").PadLeft(15, pad)))
        //    coll.Add(New View1("RNP CumLoss:", Format(rnpCLP, "Percent").PadLeft(15, pad)))
        //    coll.Add(New View1("Accured Int:", Format(Me.cdsAI, "n").PadLeft(15, pad) & "  (" & Me.cdsAIDays.ToString & " days " & Me.cdsPrevSettleDate.ToShortDateString & " to " & m_dtEval.ToShortDateString & ")"))
        //    coll.Add(New View1("MV at Closeout:", Format(Me.cdsSettlementPL, "n").PadLeft(15, pad)))
        //    Return coll
        //End Function


        public CDSCreditCurve CDSCurve
        {
            get { return m_CDSCurve; }

            set { m_CDSCurve = value; }
        }


        public string CurveName
        {
            get { return m_TICKER + m_CCY + m_SENIORITY; }
        }


        // mainly for Murex curves...
        public void GetCDSCurve(CDSCreditCurves curves)
        {
            try
            {
                curves.TryGetValue(CurveName, out m_CDSCurve);
                //Ticker & CCY & SENIORITY)
                //m_CDSCurve = curves[CurveName];
            }
            catch
            {
                //MsgBox(ex.Message & " " & CurveName, MsgBoxStyle.Exclamation, "mxCDS.GetCDSCurve")
                m_CDSCurve = null;
            }
        }

        // new 5/9/2007 to help during Monte Carlo Simulation
        public bool SimDefaulted
        {
            get { return m_SimDefaulted; }
            set { m_SimDefaulted = value; }
        }


    }
    // end of mxCDS class definition

}