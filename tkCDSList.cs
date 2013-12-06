using System;
using System.Data;
using System.Linq;
using System.Collections.Generic;

namespace CreditPricing
{

    /// <summary>
    /// Generic List of (mxCDS) custom objects
    /// used extensively thoughout the credit portfolio pricing model...
    /// </summary>
    public class tkCDSList : List<tkCDS>  // System.Collections.CollectionBase
    {
        // the collection can have a 'title... 
        // for report formatting etc...
        public string Title { get; set; }


        /// <summary>
        /// empty constructor Used by Overlap
        /// </summary>
        public tkCDSList()
            : base()
        {
        }



        // SUPER CONSTRUCTOR,  create a mxCDSCollection per date;
        // MUREX TRADES filtered by strMurexPortfolio parameter
        // creates: CYldCurve, TKRMAP, CDSCreditCurves(Markit STATIC), then loads the mxCDS portfolio
        // from .NET database and finally we create a MTM...
        /// <summary>
        ///  load mxCDSList by MUREXPortfolio and Date
        /// </summary>
        /// <param name="dateCreation">loaded on Date</param>
        /// <param name="strMurexPortfolio">Murex Portfolio Name</param>
        public tkCDSList(DateTime dateCreation, string strMurexPortfolio)
            : base()
        {

            //1)  load the murex trades for this date from the .NET datasource
            DateTime dtLoaded = dateCreation;

            var db = new CreditPricingEntities();
            var lst = (from cds in db.mxCDS
                       where cds.PORTFOLIO == strMurexPortfolio
                       select cds).ToList();


            // ticker map to mx Instrument class
            mpTKRmxMap map = new mpTKRmxMap();

            //dsCreditNetTableAdapters.mxCDSTableAdapter ta = new dsCreditNetTableAdapters.mxCDSTableAdapter();
            //dsCreditNet.mxCDSDataTable dt = ta.GetDataByMurexPORT(dtLoaded, strMurexPortfolio);
            //if (dt.Rows.Count == 0)
            //{
            //    dtLoaded = ta.MaxDate;
            //    dt = ta.GetDataByMurexPORT(dtLoaded, strMurexPortfolio);
            //}


            Title = string.Format("MUREX PORTFOLIO = [{0}] Trades via CREDITNET for [{1:d}]", strMurexPortfolio, dtLoaded);

            // create the TICKER MAP>
            // create a collection like "CCR,USD,SNRFOR","COUNTRYWIDE"
            // dsCreditNetTableAdapters.MUREXTKRMAPTableAdapter taMap = new dsCreditNetTableAdapters.MUREXTKRMAPTableAdapter();
            // dsCreditNet.MUREXTKRMAPDataTable dtMap = taMap.GetData;



            //CREATE and REPRICE, it is loaded and recalc with prices...
            YldCurve ycUSD = new YldCurve(dateCreation);
            //, "USD") ' create and recalc the yc all inside it's constructor

            CDSCreditCurves markitCDS = new CDSCreditCurves(dateCreation);
            //tkCDSCurves(dtEval)

            // the tkrMap is used to map the murex instrument to the Markit TICKER, CCY, TIER
            foreach (var mxRow in lst) //dsCreditNet.mxCDSRow mxRow in dt.Rows)
            {
                try
                {
                    if (mxRow.IE == "E")
                    {
                        // this mxCDS constructor is for MUREX, we need the tkrMAP to map INSTRUMENT to MARKIT TKR,CCY,TIER
                        Add(new tkCDS(mxRow, ycUSD, markitCDS, map));
                    }
                }
                catch
                {
                }
            }

            // MTM the portfolio
            this.cdsMTM(false);
        }


        // SUPER CONSTRUCTOR,  create a mxCDSCollection per date;
        // ALL MUREX TRADES
        // creates: CYldCurve, TKRMAP, CDSCreditCurves(Markit STATIC), then loads the mxCDS portfolio
        // from .NET database and finally we create a MTM...
        public tkCDSList(DateTime dateCreation)
            : base()
        {

            //1)  load the murex trades for this date from the .NET datasource
            DateTime dtLoaded = dateCreation;
            var db = new CreditPricingEntities();

            var lst = (from m in db.mxCDS
                       where m.SAVEDATE == dateCreation
                       orderby m.INSTRUMENT
                       select m).ToList();


            //dsCreditNetTableAdapters.mxCDSTableAdapter ta = new dsCreditNetTableAdapters.mxCDSTableAdapter();
            //dsCreditNet.mxCDSDataTable dt = ta.GetDataByDate(dtLoaded);
            if (lst.Count == 0)
            {
                dtLoaded = (from m in db.mxCDS
                            select m.SAVEDATE).Max();
                lst = (from m in db.mxCDS
                       where m.SAVEDATE == dtLoaded
                       orderby m.INSTRUMENT
                       select m).ToList();
            }

            Title = "ALL MUREX TRADES via CREDITNET: " + dtLoaded.ToShortDateString();

            mpTKRmxMap map = new mpTKRmxMap();


            //CREATE and REPRICE, it is loaded and recalc with prices...
            YldCurve ycUSD = new YldCurve(dateCreation);

            CDSCreditCurves markitCDS = new CDSCreditCurves(dateCreation);
            //tkCDSCurves(dtEval)


            if (markitCDS != null)
            {
                foreach (var mxRow in lst)
                {
                    try
                    {
                        if (mxRow.IE == "E")
                        {
                            // this mxCDS constructor is for MUREX, we need the tkrMAP to map INSTRUMENT to MARKIT TKR,CCY,TIER
                            Add(new tkCDS(mxRow, ycUSD, markitCDS, map));
                        }
                    }
                    catch
                    {
                    }
                }
            }
            this.cdsMTM(false);
        }



        // 
        /// <summary>
        /// new Feb 08 create a collection of trades by passing in a List of (mxCDS) generics 
        /// </summary>
        /// <param name="dtMX"></param>
        /// <param name="yc"></param>
        /// <param name="cdscurves"></param>
        public tkCDSList(List<mxCDS> dtMX, YldCurve yc, CDSCreditCurves cdscurves)
            : base()
        {
            Title = "MUREX TRADES via CREDITNET: ";
            mpTKRmxMap map = new mpTKRmxMap();

            // the tkrMap is used to map the murex instrument to the Markit TICKER, CCY, TIER
            foreach (var mxRow in dtMX)
            {
                try
                {
                    if (mxRow.IE == "E")
                    {
                        Add(new tkCDS(mxRow, yc, cdscurves, map));
                    }
                }
                catch
                {
                }
            }
            this.cdsMTM(false);
        }

        // 
        // 
        /// <summary>
        /// new 3/08 used for BESPOKE or INDEX portfolios...
        /// EASIER, we do not need a TICKER MAP collection as is the case with MUREX portfolios... 
        /// </summary>
        /// <param name="strTKPortfolioName"></param>
        /// <param name="dateEval"></param>
        public tkCDSList(string strTKPortfolioName, DateTime dateEval)
            : base()
        {
            // dt As dsCreditNet.PortfolioTradesDataTable, ByVal cal As CCalendar, ByVal yc As CYldCurve, ByVal cdscurves As CDSCreditCurves)
            Title = strTKPortfolioName;

            var db = new CreditPricingEntities();
            var dt = (from p in db.PortfolioTrades
                      where p.PortfolioName == strTKPortfolioName
                      orderby p.TICKER
                      select p).ToList();

            //dsCreditNetTableAdapters.PortfolioTradesTableAdapter ta = new dsCreditNetTableAdapters.PortfolioTradesTableAdapter();
            //dsCreditNet.PortfolioTradesDataTable dt = ta.GetDataByPortfolio(strTKPortfolioName);
            Calendar cal = new Calendar("USD");
            YldCurve yc = new YldCurve(dateEval);
            CDSCreditCurves cdsCurves = new CDSCreditCurves(dateEval);

            foreach (var row in dt)
            {
                try
                {
                    Add(new tkCDS(row, cal, yc, cdsCurves));
                }
                catch
                {
                }
            }
            this.cdsMTM(false);
        }


        /// <summary>
        ///  Constructor: new 6/08 used for BESPOKE or INDEX portfolios...
        /// </summary>
        /// <param name="_TTNo">Tranche Trade No</param>
        /// <param name="dateEval">Eval Date</param>
        public tkCDSList(int _TTNo, DateTime dateEval)
            : base()
        {
            // dt As dsCreditNet.PortfolioTradesDataTable, ByVal cal As CCalendar, ByVal yc As CYldCurve, ByVal cdscurves As CDSCreditCurves)

            var db = new CreditPricingEntities();
            var tRow = (from t in db.TrancheTrades
                        where t.TTNo == _TTNo
                        select t).SingleOrDefault();


            var pt = (from p in db.PortfolioTrades
                      where p.PortfolioName == tRow.PortfolioName
                      orderby p.TICKER
                      select p).ToList();


            //dsCreditNetTableAdapters.TrancheTradeTableAdapter taTT = new dsCreditNetTableAdapters.TrancheTradeTableAdapter();
            //dsCreditNet.TrancheTradeRow tRow = taTT.GetDataByTTNo(_TTNo).Rows(0);

            //dsCreditNetTableAdapters.PortfolioTradesTableAdapter ta = new dsCreditNetTableAdapters.PortfolioTradesTableAdapter();
            //dsCreditNet.PortfolioTradesDataTable dt = ta.GetDataByPortfolio(tRow.Portfolio);
            Calendar cal = new Calendar(tRow.CCY, dateEval);
            YldCurve yc = new YldCurve(dateEval, tRow.CCY);
            CDSCreditCurves cdsCurves = new CDSCreditCurves(dateEval);

            foreach (var row in pt) //dsCreditNet.PortfolioTradesRow row in dt.Rows)
            {
                try
                {
                    Add(new tkCDS(row, cal, yc, cdsCurves));
                }
                catch
                {
                }
            }
            this.cdsMTM(false);
        }


        // not sure how to abstract this constructor...  the bln parameter is added just to create a different signature
        // contruct a new collection based upon a mx PORTFOLIO
        public tkCDSList(string strTICKER, tkCDSList existingPort, bool bln)
            : base()
        {
            //mxCDS obj = default(mxCDS);
            foreach (tkCDS obj in existingPort)
            {
                if (obj.Ticker == strTICKER)
                {
                    Add(obj);
                }
            }
        }


        /// <summary>
        /// return a list of the current trades in this collection/list
        /// </summary>
        public List<tkCDS> GetTrades
        {
            get { return this; }
        }




        // here's the workhorse in terms of setting up the data request structure
        public List<string> CurveList
        {
            get
            {
                List<string> list = new List<string>();
                foreach (tkCDS cds in this)
                {
                    try
                    {
                        list.Add(cds.CurveName); // col.Add(cds.CurveName, cds.CurveName);
                    }
                    catch
                    {
                    }
                }
                return list;
            }
        }

        public void BlipSpreads(double dblSpreadBlip)
        {
            foreach (tkCDS obj in this)
            {
                obj.EvalSpread += dblSpreadBlip;
            }
        }

        public void cdsMTM(bool blnUseMidRate)
        {
            foreach (tkCDS obj in this)
            {
                // the reprice subroutine takes care of the logic
                // it sets the evalspread property to the bid/ask or mid
                // and the evalspread property takes care of repricing the cashflows...
                //obj.Reprice(bArg) old way save for now
                obj.Reprice(blnUseMidRate);
            }
        }


        public double[,] CLPMatrix
        {
            get
            {
                int iMax = 0;
                foreach (tkCDS cds in this)
                {
                    if (cds.Cashflows.Count > iMax)
                    {
                        iMax = cds.Cashflows.Count;
                    }
                }

                // this is the return matrix
                double[,] matrix = new double[Count, iMax];
                int i = 0;

                foreach (tkCDS cds in this)
                {
                    for (int j = 0; j < cds.Cashflows.Count; j++)
                    {
                        matrix[i, j] = cds.Cashflows[j].rnpCLP;
                    }
                    i += 1;
                }
                return matrix;
            }
        }

        public double cdsPortfolioEL
        {
            get
            {
                double dTotalVolEL = 0;
                double dTotalVol = 0;
                try
                {
                    foreach (tkCDS obj in this)
                    {
                        dTotalVol = dTotalVol + obj.NotionalAmt;
                        dTotalVolEL = dTotalVolEL + (obj.NotionalAmt * obj.rnpCLP);
                    }
                    return dTotalVolEL / dTotalVol;
                }
                catch
                {
                    return 0;
                }
            }
        }


        public double cdsWADealSpreadL
        {
            get
            {
                double dTotalVolSprd = 0;
                double dTotalVol = 0;
                try
                {
                    foreach (tkCDS obj in this)
                    {
                        if (obj.NotionalAmt > 0)
                        {
                            dTotalVol = dTotalVol + obj.NotionalAmt;
                            dTotalVolSprd = dTotalVolSprd + (obj.NotionalAmt * obj.DealSpread);
                        }
                    }
                    return dTotalVolSprd / dTotalVol;
                }
                catch
                {
                    return 0;

                }
            }
        }

        public double cdsWADealSpreadS
        {
            get
            {
                double dTotalVolSprd = 0;
                double dTotalVol = 0;

                foreach (tkCDS obj in this)
                {
                    if (obj.NotionalAmt < 0)
                    {
                        dTotalVol = dTotalVol + obj.NotionalAmt;
                        dTotalVolSprd = dTotalVolSprd + (obj.NotionalAmt * obj.DealSpread);
                    }
                }
                return dTotalVolSprd / dTotalVol;
            }
        }

        // return the WAS of the already MTM'd (given) spread
        public double cdsWAS_Given
        {
            get
            {
                double dTotalVolSprd = 0;
                double dTotalVol = 0;

                try
                {
                    foreach (tkCDS obj in this)
                    {
                        if (obj.NotionalAmt > 0)
                        {
                            dTotalVol += obj.NotionalAmt;
                            dTotalVolSprd += obj.NotionalAmt * obj.EvalSpread;
                        }
                    }
                    return dTotalVolSprd / dTotalVol;
                }
                catch
                {
                    return 0;

                }
            }

        }

        // this is the MTM of the CURVE SPREAD (note: value comes not from the deal but from the assigned curve)
        public double cdsWASpreadL
        {
            get
            {
                double dTotalVolSprd = 0;
                double dTotalVol = 0;

                try
                {
                    foreach (tkCDS obj in this)
                    {
                        if (obj.NotionalAmt > 0)
                        {
                            dTotalVol += obj.NotionalAmt;
                            //dTotalVolSprd += (obj.NotionalAmt * obj.CDSCurve.cdsAsk(obj.dtMaturity))
                            dTotalVolSprd += (obj.NotionalAmt * obj.EvalSpread);
                        }
                    }
                    return dTotalVolSprd / dTotalVol;
                }
                catch
                {
                    return 0;

                }
            }
        }



        public double cdsWASpreadS
        {
            get
            {
                double dTotalVolSprd = 0;
                double dTotalVol = 0;

                foreach (tkCDS obj in this)
                {
                    if (obj.NotionalAmt < 0)
                    {
                        dTotalVol = dTotalVol + obj.NotionalAmt;
                        try
                        {

                            dTotalVolSprd = dTotalVolSprd + (obj.NotionalAmt * obj.CDSCurve.cdsAsk(obj.dtMaturity));
                        }
                        catch
                        {
                            dTotalVolSprd = 0;

                        }
                    }
                }
                return dTotalVolSprd / dTotalVol;
            }
        }


        public double cdsTotalCarry
        {
            get
            {
                double dTotal = 0;
                foreach (var obj in this)
                {
                    dTotal += obj.cdsHPCarry;
                }
                return dTotal;
            }
        }


        public double cdsTotalUpFront
        {
            get
            {

                double dTotal = 0;
                foreach (tkCDS obj in this)
                {
                    dTotal += obj.cdsUpFrontAmt;
                }
                return dTotal;
            }
        }

        public double cdsTotalNotional
        {
            get
            {
                double dTotal = 0;
                foreach (tkCDS obj in this)
                {
                    dTotal += obj.NotionalAmt;
                }
                return dTotal;
            }
        }

        public double cdsTotalMtM
        {
            get
            {
                double dTotal = 0;
                foreach (tkCDS obj in this)
                {
                    dTotal = dTotal + obj.cdsPVCF;
                }
                return dTotal;
            }
        }

        public double cdsTotalCrBPV
        {
            get
            {

                double dTotal = 0;
                foreach (var obj in this)
                {
                    dTotal = dTotal + obj.cdsCrBPV;
                }
                return dTotal;
            }
        }


        public double cdsTotalPL
        {
            get
            {
                double dTotal = 0;
                foreach (var obj in this)
                {
                    dTotal = dTotal + obj.cdsHPTotalPL;
                }
                return dTotal;
            }
        }


        // optimized do it in ONE PASS  what is the total return of the CDS portfolio...
        public double cdsTotalReturn
        {

            get
            {

                double dTotalPL = 0;
                double dTotDayWtdNotional = 0;
                double dTotNotional = 0;
                double dNtlAmt = 0;
                // notional amount of each cds trade
                DateTime dtCDSSettle = default(System.DateTime);
                DateTime dtCDSEval = default(System.DateTime);
                // this will be the same for each trade in the portfolio, but stor for property get optimization
                DateTime dtFirst = default(System.DateTime);
                // first date in Total Return horizon
                DateTime dtLast = default(System.DateTime);
                // last date in Total Return horizon
                dtFirst = DateTime.Today;
                dtLast = DateTime.Today;

                foreach (var obj in this)
                {
                    {
                        dtCDSSettle = obj.dtSettle;
                        // each trades trade/date settle date
                        dtCDSEval = obj.EvalDate;

                        if (dtCDSSettle < dtFirst) dtFirst = dtCDSSettle;
                        // pick out ealiest date of T/R horizon
                        if (dtCDSEval > dtLast) dtLast = dtCDSEval;
                        // pick out latest date of T/R horizon

                        dNtlAmt = Math.Abs(obj.NotionalAmt);
                        dTotalPL = dTotalPL + obj.cdsHPTotalPL;
                        dTotDayWtdNotional = dTotDayWtdNotional + (dNtlAmt * (obj.EvalDate.Subtract(obj.dtSettle).Days));
                        dTotNotional = dTotNotional + dNtlAmt;
                    }
                }
                return (dTotalPL * 365 / (double)dtLast.Subtract(dtFirst).Days) / (dTotDayWtdNotional / dtLast.Subtract(dtFirst).Days);
            }
        }


        /// <summary>
        /// return the object from the list by passing an MUREX TRADE# and matching...
        /// </summary>
        /// <param name="mxNB"></param>
        /// <returns></returns>
        public tkCDS mxDeal(string mxNB)
        {
            {
                return this.Find(delegate(tkCDS m) { return m.NB == mxNB; }); // fastest way to find object using generics...

                // SNAAP! that's how you do it beeeyotch...
                // this was the old way!!!
                //bool found = false;

                //foreach (var obj in this)
                //{
                //    if (obj.NB == mxNB)
                //    {
                //        found = true;
                //        break; // TODO: might not be correct. Was : Exit For
                //    }
                //}
                //if (!found) obj = null;
                //return obj;
            }
        }


        /// <summary>
        /// not sure here?!?
        /// </summary>
        /// <param name="strPortfolio"></param>
        /// <param name="colExist"></param>
        public tkCDSList(string strPortfolio, tkCDSList colExist)
            : base()
        {
            //mxCDS obj = default(mxCDS);

            foreach (var obj in colExist)
            {
                if (obj.Portfolio == strPortfolio)
                {
                    Add(obj);
                }

            }
        }


        //public dsOUT.CDSListSummaryDataTable DisplaySummary
        public List<cdsListSummary> DisplaySummary
        {
            get
            {
                
                List<cdsListSummary> dt = new List<cdsListSummary>(); // dsOUT.CDSListSummaryDataTable dt = new dsOUT.CDSListSummaryDataTable();
                cdsListSummary row = new cdsListSummary(); // dsOUT.CDSListSummaryRow row = dt.NewCDSListSummaryRow();
                {
                    row.ListName = Title;
                    row.ListCount = Count;
                    row.TotalUpfront = cdsTotalUpFront;
                    row.TotalCarry = cdsTotalCarry;
                    row.TotalMTM = cdsTotalMtM;
                    row.TotalPL = cdsTotalPL;
                    row.TotalReturn = cdsTotalReturn;
                    row.TotalCrBPV = cdsTotalCrBPV;
                    row.WASLong = cdsWADealSpreadL * 100;
                    row.WASShort = cdsWADealSpreadS * 100;
                    row.WASMtMLong = cdsWASpreadL * 100;
                    row.WASMtMShort = cdsWASpreadS * 100;
                }
                dt.Add(row); //dt.AddCDSListSummaryRow(row);
                return dt;
            }
        }

        //public dsOUT.mxCDSCalcDataTable mxCDSCalc
        public List<mxCDSCalc> mxCDSCalc
        {
            get
            {
                List<mxCDSCalc> dt = new List<mxCDSCalc>(); //dsOUT.mxCDSCalcDataTable dt = new dsOUT.mxCDSCalcDataTable();
                foreach (tkCDS cds in this)
                {
                    try
                    {
                        mxCDSCalc row = new mxCDSCalc(); // dsOUT.mxCDSCalcRow row = dt.NewmxCDSCalcRow();
                        {
                            row.EvalDate = cds.CurveDate;
                            row.NB = int.Parse(cds.NB);
                            row.Ticker = cds.Ticker;
                            row.Ccy = cds.CCY;
                            row.RefEnt = cds.RefEntity;
                            row.Tier = cds.SENIORITY;
                            row.Sector = cds.Sector;
                            row.Settle = cds.dtSettle;
                            row.Maturity = cds.dtMaturity;
                            row.Notional = cds.NotionalAmt;
                            row.DealSpread = cds.DealSpread * 100;
                            row.MtMSpread = cds.EvalSpread * 100;
                            row.SpreadChg= (cds.EvalSpread - cds.DealSpread) * 100;
                            row.CrBPV = cds.cdsCrBPV;
                            row.UpFront = cds.cdsUpFrontAmt;
                            row.Carry = cds.cdsHPCarry;
                            row.PVFlows = cds.cdsPVCF;
                            row.TotalPL = cds.cdsHPTotalPL;
                            row.TotalReturn = cds.cdsTotalReturn;
                            row.HPDays = cds.cdsHoldingPeriodDys;
                            row.Portfolio = cds.Portfolio;
                            row.Instrument = cds.Instrument;
                            row.CounterParty = cds.Counterparty;
                        }
                        dt.Add(row); // dt.AddmxCDSCalcRow(row);
                    }
                    catch
                    {
                    }
                }
                return dt;
            }
        }


        public DataSet GetDataSet
        {
            get
            {
                DataSet ds = new DataSet("CDSTrades");
                System.Data.DataTable tbl = ds.Tables.Add("CDSTrades");

                tbl.Columns.Add("NB", Type.GetType("System.Int32"));
                //0
                tbl.Columns.Add("TICKER", Type.GetType("System.String"));
                //0
                tbl.Columns.Add("RefEntity", Type.GetType("System.String"));
                //1
                tbl.Columns.Add("Sector", Type.GetType("System.String"));
                //2
                tbl.Columns.Add("SettleDate", Type.GetType("System.DateTime"));
                //3
                tbl.Columns.Add("Maturity", Type.GetType("System.DateTime"));
                //4
                tbl.Columns.Add("Notl", Type.GetType("System.Double"));
                //5
                tbl.Columns.Add("OrigSprd", Type.GetType("System.Double"));
                //6
                tbl.Columns.Add("CurrSprd", Type.GetType("System.Double"));
                //7
                tbl.Columns.Add("SprdChng", Type.GetType("System.Double"));
                //8
                tbl.Columns.Add("CrBPV", Type.GetType("System.Double"));
                //9
                tbl.Columns.Add("Upfront", Type.GetType("System.Double"));
                //10
                tbl.Columns.Add("Carry", Type.GetType("System.Double"));
                //12
                tbl.Columns.Add("PV_CFlows", Type.GetType("System.Double"));
                //13
                tbl.Columns.Add("Total_P&L", Type.GetType("System.Double"));
                //14
                tbl.Columns.Add("TR", Type.GetType("System.Double"));
                //15
                tbl.Columns.Add("HP_Days", Type.GetType("System.Int32"));
                //16
                //                tbl.Columns.Add("EL", Type.GetType("System.Double")) '17

                tbl.BeginLoadData();
                foreach (tkCDS cds in this)
                {
                    DataRow row = default(DataRow);
                    row = tbl.NewRow();
                    {
                        row[0] = cds.NB;
                        row[1] = cds.Ticker;
                        row[2] = cds.RefEntity;
                        row[3] = cds.Sector;
                        row[4] = cds.dtSettle;
                        row[5] = cds.dtMaturity;
                        row[6] = cds.NotionalAmt;
                        row[7] = cds.DealSpread * 100;
                        row[8] = cds.EvalSpread * 100;
                        row[9] = (cds.EvalSpread - cds.DealSpread) * 100;
                        row[10] = cds.cdsCrBPV;
                        row[11] = cds.cdsUpFrontAmt;
                        row[12] = cds.cdsHPCarry;
                        row[13] = cds.cdsPVCF;
                        row[14] = cds.cdsHPTotalPL;
                        row[15] = cds.cdsTotalReturn;
                        row[16] = cds.cdsHoldingPeriodDys;
                    }
                    //row(17) = .rnpCLP
                    tbl.Rows.Add(row);
                }
                tbl.EndLoadData();
                return ds;
            }
        }


        public tkCDSList OverLap(tkCDSList portCompare)
        {
            tkCDSList returnPort = new tkCDSList();
            returnPort.Title = "Overlap: " + portCompare.Title + " - " + this.Title;
            foreach (tkCDS cds in portCompare)
            {
                foreach (tkCDS obj in this)
                {
                    if (cds.Ticker == obj.Ticker)
                    {
                        returnPort.Add(cds);
                    }
                }
            }
            return returnPort;
        }


    }
}