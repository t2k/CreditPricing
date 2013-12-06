using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;
using System.Web.Script.Services;
using CreditPricing; 

namespace CreditPricing
{
    public class ycPricingOptions
    {
        public string curveName { get; set; }
        public string CCY { get; set; }
        public List<string> HolidayCenter { get; set; }
        public DateTime curveDate { get; set; }
        public DateTime startDate { get; set; }
        public DateTime endDate { get; set; }
    }

    public class fxArbOptions
    {
        public int id { get; set; }
        public DateTime fixingDate { get; set; }
    }

    public class fxArbOptionsOddDate
    {
        public int id { get; set; }
        public DateTime fixingDate { get; set; }
        public DateTime oddDate { get; set; }
    }


    public class pmReqOptions
    {
        public string portfolio { get; set; }
    }


    public class CumLossInfo
    {
        public int SimID { get; set; }
        public string CreditUniverseName { get; set; }
        public string PortfolioName { get; set; }
        public string SimulationStatus { get; set; }
        public string ElapsedSec { get; set; }
    }


    public class futContractParam  // drag/drop trade passed in from client
    {
        public string contract { get; set; }
        public DateTime startDate { get; set; }
        public int years { get; set; }
        public string cycle { get; set; }
        public int nDys2Settle { get; set; }
        public string settlement { get; set; }
        public string depoBasis { get; set; }
        public string ccy { get; set; }
        public string basis { get; set; }
    }


    /// <summary>
    /// liquidity spread options
    /// </summary>
    public class lsOptions
    {
        public string ccy { get; set; }
        public DateTime fixingDate { get; set; }
        public string curveNameTK { get; set; }
        public double bidOffer { get; set; }
        public bool xCCY { get; set; }
        public DateTime? odddate { get; set; }
    }

    /// <summary>
    /// time series data
    /// </summary>
    public class tsData
    {
        public DateTime Date { get; set; }
        public string Period { get; set; }
        public double Price { get; set; }
    }

	/// <summary>
	/// time series
	/// </summary>
	public class tsData2
	{
		public string Period { get; set; }
		public DateTime Date { get; set; }
		public double Yield { get; set; }
		public double discFactor { get; set; }
	}


    /// <summary>
    /// historical spreads options, name of pillar and days
    /// </summary>
    public class hsOptions
    {
        public string Pillar { get; set; }
        public int Days{ get; set; }
    }


    /// <summary>
    /// liquidity spread report
    /// </summary>
    public class lsReport
    {
        public string Period { get; set; }
        public DateTime Date { get; set; }
        public double TK { get; set; }
        public double xCCYBSS { get; set; }
        public double BSS3x1 { get; set; }
        public double bidOffer { get; set; }
        public double LS { get; set; }
    }


    public class googDataTable
    {
        public googColInfo[] cols { get; set; }
        public googDataPointSet[] rows { get; set; }
        public Dictionary<string, string> p { get; set; }
    }

    public class googColInfo
    {
        public string id { get; set; }
        public string label { get; set; }
        public string type { get; set; }
    }

    public class googDataPointSet
    {
        public googDataPoint[] c { get; set; }
    }

    public class googDataPoint
    {
        public string v { get; set; } // value
        public string f { get; set; } // format
    }


}

/// <summary>
/// Summary description for CreditService
/// </summary>
[WebService(Namespace = "http://dnyias20/CreditPricing/")]
[WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
[ScriptService]
public class CreditPricingService : WebService {

    public CreditPricingService () {
        //Uncomment the following line if using designed components 
        //InitializeComponent(); 
    }



    /// <summary>
    /// returns list of yieldcurves and dates
    /// </summary>
    /// <returns></returns>
    [WebMethod]
    public dynamic getYieldCurves()
    {
        try
        {

            var data = new CreditPricingEntities();

            var qry = from u in data.ycHeaders
                      where u.curveType=="MM"
                      orderby u.Name

                      select new
                      {
                          u.Name,
                          u.CCY,
                          u.Description,
                          u.HolidayCenter
                      };

            var qry2 = (from r in data.RATETimeStamps
                        orderby r.TimeStamp descending
                        select r).Take(1).SingleOrDefault();


            DateTime evaldate = qry2.SaveDate;

            Calendar cal = new Calendar("USD", evaldate);
            DateTime spot = cal.WorkDate(evaldate, 2);
            DateTime far = cal.FarDate(spot, "3M");

            return new
            {
                status = "success",
                savedate = evaldate,
                spotdate = spot,
                fardate = far,
                timestamp = qry2.TimeStamp,
                timestampUTC = qry2.TimeStampUTC,
                results = qry.ToList(),
            };

        }
        catch (Exception e)
        {
            return new
            {
                status = "error",
                message = e.Message
            };
        }
    }




    /// <summary>
    /// 
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    [WebMethod]
    public dynamic getYldCurvePricing(ycPricingOptions options)
    {
        try
        {
            var data = new CreditPricingEntities();

            List<DateTime> spotdates = new List<DateTime>();
            Calendar cal = new Calendar(options.HolidayCenter, options.curveDate);
            //Calendar cal = new Calendar(options.CCY, options.curveDate);
            for (var i = 0; i < 120; i++)
            {
                spotdates.Add(cal.FwdDate(options.startDate, i+1));
            }

            YldCurve yc = new YldCurve(options.curveName, options.curveDate);

            return new
            {
                status = "success",
                curvename = options.curveName,
                timestamp =  yc.prices.TimeStamp,
                timestampUTC = yc.prices.TimeStampUTC,
                results = yc.DisplayZero(),
                fixingdate = options.curveDate,
                valuedate = options.startDate,
                maturitydate = options.endDate,
                rates = yc.GetPrices(),  // perfect returns an array of rates with bid/ask/cls
                bid = yc.FwdRate(options.startDate,options.endDate,tkPrice.tkBID),
                ask = yc.FwdRate(options.startDate, options.endDate, tkPrice.tkASK),
                spotdates = spotdates
            };
        }
        catch (Exception e)
        {
            return new
            {
                status = "error",
                message = e.Message
            };
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    [WebMethod]
    public  DateTime MaxDate()
    {
        var ctx = new CreditPricingEntities();
        return (from m in ctx.mpCDSCurves
                select m.EvalDate).Max();
    }


    [WebMethod]
    public dynamic GetCUList()
    {
        var data = new CreditPricingEntities();

        var qry = from u in data.CrUniverses
                  orderby u.CreditUniverseName
                  select new
                  {
                      CreditUniverse = u.CreditUniverseName,
                      u.BatchDate,
                      u.Batchable,
                      u.IsDefault
                  };

        return new
        {
            count = qry.Count(),
            rows = qry.ToList()
        };
    }



    [WebMethod]
    public dynamic GetTTList()
    {
        var data = new CreditPricingEntities();
        return (from t in data.TTLists
                orderby t.TTListName
                select new
                {
                    t.TTListName
                }).ToList();

        //return new { TTListNames = qry };
    }

    [WebMethod]
    public dynamic GetTTSelectList()
    {
        var data = new CreditPricingEntities();
        var qry = (from t in data.TrancheTrades
                orderby t.PortfolioName
                select new
                { 
                    t.TTNo,
                    t.PortfolioName,
                    t.Attachment,
                    Detachment = t.Attachment + t.Width
                }).ToList();

        var qry2 = (from r in qry
                   select new {
                       value=r.TTNo,
                       label = string.Format("{0} ({1:p}-{2:p})",r.PortfolioName,r.Attachment, r.Detachment)
                   }).ToList();
        return qry2;
    }


    /// <summary>
    /// generate cumulative loss data for a Batch of portfolios 'CreditUniverse'
    /// </summary>
    /// <param name="_CreditUniverseName"></param>
    /// <param name="_Date"></param>
    /// <returns></returns>
    [WebMethod]
    public dynamic PortfolioCumLossBATCH(string _CreditUniverseName, DateTime _Date)
    {
        DateTime timeStart = DateTime.Now;
        try
        {
            var ctx = new CreditPricingEntities();
            List<CrUniverseList> theList = (from l in ctx.CrUniverseLists
                                            where l.CreditUniverseName == _CreditUniverseName
                                            select l).ToList();

            List<CumLossInfo> cumLossList = new List<CumLossInfo>();  // this will be loaded and returned as the function result
            foreach (CrUniverseList listRow in theList)
            {
                tkCDSList thePort = new tkCDSList(listRow.PortfolioName, _Date);
                tkSimulation theSim = new tkSimulation(listRow.PortfolioName, (int)listRow.PortfolioNumDraws, (double)listRow.PortfolioCorrelation, "P", _Date);
                mcOutput theResult = theSim.PortfolioCumLoss(thePort, (int)listRow.PortfolioNumDraws, (double)listRow.PortfolioCorrelation); //            Dim theResult As tkSimulation.mcOutput = theSim.PortfolioCumLoss(thePort, listrow.PortfolioNumDraws, listrow.PortfolioCorrelation)
                tkSimResult simResult = new tkSimResult(theSim.SimID, theResult, theResult.mcMaxNumDefaults + 1, 0, 1);
                simResult.SaveData();
                simResult.SaveBins();
                
                // save the data
                CumLossInfo clInfo = new CumLossInfo();
                clInfo.SimID = theSim.SimID;
                clInfo.CreditUniverseName = listRow.CreditUniverseName;
                clInfo.PortfolioName = listRow.PortfolioName;
                clInfo.SimulationStatus = "OK";
                clInfo.ElapsedSec = string.Format("{0}", (DateTime.Now - timeStart));
                cumLossList.Add(clInfo);
            }

            return new
            {
                success = true,
                evalDate = _Date,
                lossInfo = cumLossList,
                count = cumLossList.Count()
            };

        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                errmsg=ex.Message
            };
        }
    }

    /// <summary>
    /// Jump to Default tranche pricing:
    /// </summary>
    /// <param name="_TTList"></param>
    /// <param name="_Date"></param>
    /// <param name="_DefaultCreditList"></param>
    /// <returns></returns>
    [WebMethod]
    public dynamic TTListValuationJTD(string _TTListName, DateTime _Date, string _DefaultCreditList)
    {

        try
        {
            var db = new CreditPricingEntities();

            var ttList = from t in db.TTRevalInputs
                         where t.RevalDate == _Date && t.TTListName == _TTListName
                         select t;


            List<TranchePrice> tplist = new List<TranchePrice>();

            // calc baseline portfolio 
            foreach (TTRevalInput tranche in ttList)
            {
                tkTranche thistranche = new tkTranche(tranche.TTNo, _Date);
                TranchePrice tp = thistranche.TranchePriceFA((int)tranche.Draws, (double)tranche.Correlation);  //(int)tranche.Draws
                tplist.Add(tp);


                if (_DefaultCreditList.Trim().Length > 0)
                {  //            If _DefaultCreditList.Trim.Length > 0 Then
                    string[] creditList = _DefaultCreditList.Split(';');
                    foreach (string defCredit in creditList)
                    {
                        if (defCredit.Contains(',') && defCredit.Length > 12)
                        {
                            TranchePrice tp2 = thistranche.TranchePriceWDefaults((int)10000, (double)tranche.Correlation, defCredit.Trim());
                            tplist.Add(tp2);
                        }
                    }
                }
            }
            return new
            {
                success=true,
                prices = tplist,
                count = tplist.Count()
            };

        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                errmsg = ex.Message
            };
        }
    }

    /// <summary>
    /// tranche valuation (fully analytic mode)
    /// return tranche prices + CDS Curves for all portfolio credits
    /// </summary>
    /// <param name="_TTNo"></param>
    /// <param name="_corr"></param>
    /// <param name="_draws"></param>
    /// <param name="_date"></param>
    /// <returns></returns>
    [WebMethod]
    public dynamic trancheValuationFA(int _TTNo, double _corr, int _draws, DateTime _date)
    {
        try
        {
            List<TranchePrice> tpList = new List<TranchePrice>();
            tkTranche tranche = new tkTranche(_TTNo, _date); //        Dim thisTranche As New tkTranche(_TTNo, _Date)
            tpList.Add(tranche.TranchePriceFA(_draws, _corr));
            //var mycurves = tranche.CreditCurves();
            return new
            {
                success=true,
                prices = tpList
              //  cdsCurves = mycurves
            };
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                errmsg = ex.Message
            };
        }
    }


    /// <summary>
    /// return a list of tranche prices rolling down from the tranche maturity in qtrly steps
    /// </summary>
    /// <param name="_TTNo"></param>
    /// <param name="_corr"></param>
    /// <param name="_draws"></param>
    /// <param name="_date"></param>
    /// <returns></returns>
    [WebMethod]
    public dynamic trancheValuationFA_Rolldown(int _TTNo, double _corr, int _draws, DateTime _date)
    {
        try
        {
            tkTranche tranche = new tkTranche(_TTNo, _date); //        Dim thisTranche As New tkTranche(_TTNo, _Date)
            List<TranchePrice> tpList = tranche.TranchePriceFA_RollDown(_draws, _corr);
            return new
            {
                success=true,
                prices = tpList,
                count = tpList.Count()
            };

        }
        catch (Exception ex)
        {
            return new { success=false,
            errmsg=ex.Message
            };
        }
    }

    /// <summary>
    /// tranch price subordination list overrides
    /// </summary>
    /// <param name="_TTNo"></param>
    /// <param name="_corr"></param>
    /// <param name="_draws"></param>
    /// <param name="_date"></param>
    /// <param name="_attachList"></param>
    /// <returns></returns>
    [WebMethod]
    public dynamic trancheValuationFA_Subordination(int _TTNo, double _corr, int _draws, DateTime _date,string _trancheList)
    {

        try
        {
            tkTranche tranche = new tkTranche(_TTNo, _date); //        Dim thisTranche As New tkTranche(_TTNo, _Date)
            List<TranchePrice> tpList = tranche.TranchePriceFA_Subordination(_draws, _corr, _trancheList);
            return new
            {
                success=true,
                prices = tpList,
                count = tpList.Count()
            };

        }
        catch (Exception ex)
        {

            return new { success=false,
            errmsg=ex.Message};
        }
    }

    /// <summary>
    ///  reprice the tranche under corr = 0 to 1 step .1
    /// </summary>
    /// <param name="_TTNo"></param>
    /// <param name="_draws"></param>
    /// <param name="_date"></param>
    /// <returns></returns>
    [WebMethod]
    public dynamic trancheValuationFA_Corr(int _TTNo, int _draws, DateTime _date)
    {
        try
        {
            // note this will be slower than the other calculators because we need to regenerate monte-carlo simulations for each given corr step
            List<TranchePrice> tpList = new List<TranchePrice>();
            tkTranche tranche = new tkTranche(_TTNo, _date); //        Dim thisTranche As New tkTranche(_TTNo, _Date)

            for (double dcorr = 0; dcorr <= 1; dcorr += (double).1)
            {
                tpList.Add(tranche.TranchePriceFA(_draws, dcorr));
            };

            return new
            {
                success=true,
                prices = tpList,
                count = tpList.Count()
            };
        }
        catch (Exception ex)
        {

            return new { 
            success=false,
            errmsg=ex.Message
            };
        }
    }

    [WebMethod]
    public dynamic trancheValuationFA_JTD(int _TTNo, double _corr, int _draws, DateTime _date, string _JTDList)
    {
        try
        {
            tkTranche tranche = new tkTranche(_TTNo, _date); //        Dim thisTranche As New tkTranche(_TTNo, _Date)
            List<TranchePrice> tpList = new List<TranchePrice>();
            tpList.Add(tranche.TranchePriceFA(_draws, _corr));

            if (_JTDList.Trim().Length > 0)
            {
                string[] creditList = _JTDList.Split(';');
                foreach (string defCredit in creditList)
                {
                    if (defCredit.Contains(","))
                    {
                        tpList.Add(tranche.TranchePriceWDefaults(_draws, _corr, defCredit.Trim()));
                    }
                }
            }
            return new
            {
                success=true,
                prices = tpList,
                count = tpList.Count()
            };
        }
        catch (Exception ex)
        {
            return new {
            success=false,
            errmsg=ex.Message
            };
        }
    }

    [WebMethod]
    public dynamic getPortfolioData()
    {
        try
        {

            var model = new CreditPricingEntities();

            var qry = from u in model.Portfolios
                      orderby u.PortfolioName
                      select new
                      {
                          Name = u.PortfolioName,
                          Class = u.PortfolioClass,
                          Description = u.PortfolioDescription,
                          Type = u.PortfolioType,
                          Calendar = u.Calendar,
                          CCY = u.Currency,
                          RECount = u.PortfolioTrades.Count
                      };

            return new
            {
                status="success", //success=true,
                results = qry.ToList(),
                count = qry.Count(),
            };


        }
        catch (Exception ex)
        {
            return new
            {
                status="fail",
                message = ex.Message
            };
        }
    }

    /// <summary>
    /// return info about portfolio credits
    /// </summary>
    /// <param name="options">options param is a pmReqOptions object.  portfolio </param>
    /// <returns>JSON object collection of: RE, TKR, CCY, TIER, RR, isfixRR, PCTWT</returns>
    [WebMethod]
    public dynamic getPortfolioCredits(pmReqOptions options)
    {
        try
        {

            var model = new CreditPricingEntities();

            var qry = from u in model.PortfolioTrades
                      where u.PortfolioName==options.portfolio
                      select new
                      {
                          RefEntity = (from r in model.REDENTITies where r.TICKER == u.TICKER select r.EntityName).SingleOrDefault(),
                          u.TICKER,
                          u.CCY,
                          Tier=u.SnrSub,
                          u.Recovery,
                          u.IsFixedRR,
                          PortPctWt = u.NotionalAmount
                      };

            return new
            {
                status="success", // = true,
                results = qry.OrderBy(a => a.RefEntity).ToList(),
                count = qry.Count(),
            };

        }
        catch (Exception ex)
        {
            return new
            {
                status="success", // = false,
                message = ex.Message
            };
        }
    }




    /// <summary>
    /// 
    /// </summary>
    /// <param name="option">futContractParam</param>
    /// <returns>JSON: {status: success, message: xxx}</returns>
    [WebMethod]
    public dynamic generateFutContractDelivery(futContractParam option)
    {

        try
        {
            var ctx = new CreditPricingEntities();
            Calendar cal = new Calendar(option.ccy, option.startDate);
            DateTime contractDate = option.startDate;
            for (int i = 0; i < option.years * (option.cycle == "M" ? 12 : 4); i++)
            {
                string contractCode = option.contract + deliveryCode(contractDate);  // ie EMZ1
                string strSQL = string.Format("DELETE from RATECODE where RATECODE ='{0}'", contractCode);
                ctx.ExecuteStoreCommand(strSQL);
                // create new object
                RATECODE rc = new RATECODE();

                rc.RATECODE1 = contractCode;
                rc.Basis = option.basis;
                rc.CCY = option.ccy;
                rc.CCYCALENDAR = option.ccy;
                rc.Period = option.depoBasis;
                rc.nDay2Start = (short)option.nDys2Settle;
                rc.nCompoundPA = 1;
                rc.RateType = 2;
                rc.CF_Period_Type = "F";
        


                if (option.settlement == "3W")
                {  // 3rd wednesday of the month...
                    rc.CF_StartDate = cal.ThirdWed(contractDate);
                    rc.CF_EndDate = cal.ThirdWed(cal.FarDate(contractDate, option.depoBasis));  // depoBasis is either 3M or 1M...
                    rc.CF_SensiEndDate = rc.CF_EndDate;
                }
                else
                {  // month end dates 
                    rc.CF_StartDate = new DateTime(contractDate.Year, contractDate.Month, 1);  // first day
                    rc.CF_EndDate = new DateTime(contractDate.Year, contractDate.Month + 1, 1).AddDays(-1); // last day
                    rc.CF_SensiEndDate = rc.CF_EndDate;
                }
                //rc.RIC = "N/A";
                ctx.AddToRATECODEs(rc);
                contractDate = cal.FarDate(contractDate, option.depoBasis); // 1M or 3M
            }

            ctx.SaveChanges();
            return new
            {
                status = "success",
                message = string.Format("Total records updated {0}", option.years * (option.cycle == "M" ? 12 : 4))
            };
        }
        catch (Exception e)
        {
            return new
            {
                status = "error",
                message = e.Message
            };
        }
    }


    /// <summary>
    /// return delivery code per date ie 12/15/2011 = "Z1"
    /// </summary>
    /// <param name="dt"></param>
    /// <returns></returns>
    public string deliveryCode(DateTime dt)
    {
        string sYr = dt.Year.ToString();
        return string.Format("{0}{1}", "FGHJKMNQUVXZ".Substring(dt.Month - 1, 1), sYr[sYr.Length - 1]);  //Strings.Mid(monthcode, DateAndTime.Month(dt), 1) + Strings.Right(DateAndTime.Year(dt), 1);
    }

    /// <summary>
    /// get all defined FX Arbitrage models (jSend API)
    /// </summary>
    /// <returns>jSend API  (JSON Envelope)</returns>
    [WebMethod]
    public dynamic getFXArbModels()
    {
        try
        {
            var data = new CreditPricingEntities();

            //last saved dates
            var qdates = (from r in data.RATETimeStamps
                        orderby r.TimeStamp descending
                        select r).Take(1).SingleOrDefault();

            var qry = from u in data.fx_arbs
                      orderby u.arbCCY, u.name
                      select new
                      {
                          u.id,
                          u.symbol,
                          u.name,
                          u.description,
                          fixingdate = qdates.SaveDate
                      };

            return new
            {
                status = "success",
                data = new
                {
                    timestamp = qdates.TimeStamp,
                    timestampUTC=qdates.TimeStampUTC,
                    results = qry.ToList()
                }
            };
        }
        catch (Exception e)
        {
            return new
            {
                status = "error",
                message = e.Message
            };
        }
    }


    /// <summary>
    /// filter by curve1 = TK_LIQSHORT
    /// </summary>
    /// <returns>jSend API  (JSON Envelope)</returns>
    [WebMethod]
    public dynamic getFXArbLiquidityModels()
    {
        try
        {
            var data = new CreditPricingEntities();

            //last saved dates
            var qdates = (from r in data.RATETimeStamps
                          orderby r.TimeStamp descending
                          select r).Take(1).SingleOrDefault();

            var qry = from u in data.fx_arbs
                      where u.curve1 == "TK_LIQSHORT"
                      orderby u.arbCCY, u.name
                      select new
                      {
                          u.id,
                          u.symbol,
                          u.name,
                          u.description,
                          u.arbCCY,
                          fixingdate = qdates.SaveDate
                      };

            return new
            {
                status = "success",
                data = new {
                    timestamp = qdates.TimeStamp,
                    timestampUTC = qdates.TimeStampUTC,
                    results = qry.ToList()
                }
            };
        }
        catch (Exception e)
        {
            return new
            {
                status = "error",
                message = e.Message
            };
        }
    }


    /// <summary>
    /// FX Arbitrage model pricing
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    [WebMethod]
    public dynamic getFXArbData(fxArbOptions options)
    {
        try
        {
            fxarbModel model = new fxarbModel(options.id, options.fixingDate);
            return model.arbReport();

        }
        catch (Exception ex)
        {
            return new
            {
                status = "error",
                message = ex.Message
            };
            
            
        }
    }

    /// <summary>
    /// FX Arbitrage model pricing
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    [WebMethod]
    public dynamic getFXArbDataLS(fxArbOptions options)
    {
        try
        {
            fxarbModel model = new fxarbModel(options.id, options.fixingDate);
            return model.arbReportLS();  //new

        }
        catch (Exception ex)
        {
            return new
            {
                status = "error",
                message = ex.Message
            };

        }
    }



    /// <summary>
    /// odd date calculator jSend API
    /// </summary>
    /// <param name="options"></param>
    /// <returns>jSend API  (JSON Envelope)</returns>
    [WebMethod]
    public dynamic getFXArbDataOddDate( fxArbOptionsOddDate options)
    {
        try
        {
            fxarbModel model = new fxarbModel(options.id, options.fixingDate);
            return model.oddDateCalculator(options.oddDate); // jSend API
        }
        catch (Exception ex)
        {
            return new
            {
                status = "error",
                message = ex.Message
            };
        }
    }

    /// <summary>
    /// odd date calculator jSend API Liquidity Spread Version
    /// </summary>
    /// <param name="options"></param>
    /// <returns>jSend API  (JSON Envelope)</returns>
    [WebMethod]
    public dynamic getFXArbDataOddDateLS(fxArbOptionsOddDate options)
    {
        try
        {
            fxarbModel model = new fxarbModel(options.id, options.fixingDate);
            return model.oddDateCalculatorLS(options.oddDate); // jSend API
        }
        catch (Exception ex)
        {
            return new
            {
                status = "error",
                message = ex.Message
            };
        }
    }


    /// <summary>
    /// return a liquidity spread > 2yr or just the odd-date (jSend API)
    /// </summary>
    /// <param name="options"></param>
    /// <returns>jSend API  (JSON Envelope)</returns>
    [WebMethod]
    public dynamic getLS(lsOptions options)
    {
        try
        {
            var ctx = new hdbOracleEntities();
            DateTime maxBSS = (from b in ctx.BSSes where b.SYMBOL.StartsWith(options.ccy) select b.TRADE_DATE).Max();
            DateTime bssFixDate;

            if (maxBSS < options.fixingDate)
            {
                bssFixDate = maxBSS;
            }
            else
            {
                bssFixDate = options.fixingDate;
            }

            //var data = new CreditPricingEntities();
            YldCurve ycTK = new YldCurve(options.curveNameTK, options.fixingDate);  // we store TK_Ungedeckt daily in CREDITNET
            YldCurve ycBS = new YldCurve(options.ccy, bssFixDate, true);  // note this constructor gathers data from HDB
            YldCurve ycBS3x1 = new YldCurve(options.ccy, bssFixDate, false); // note this constructor gathers data from HDB

            // use the basis swap curve to set the spot dates... (multi city holidays)
            DateTime valueDate = ycBS.calendar.Workdays(options.fixingDate, 2);  // use the calendar from this yieldcurve object.. (gets holidays from HDB)

            List<lsReport> lsReport = new List<lsReport>();  // lsReport defined in this webservice see above
            // new/best practice?  if the odddate is not passed in from the client, then we return multiple rows of data 
            if (!options.odddate.HasValue)
            {

                string[] periods = { "1Y","2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y", "10Y", "12Y", "15Y", "20Y", "30Y" };


                foreach (string pd in periods)
                {
                    lsReport row = new lsReport();  // liquidity spread report...

                    double pricecheck;  // new with system.data.oracleclient, must check for NaN 
                    row.Period = pd;
                    row.Date = ycBS.calendar.FarDate(valueDate, pd);

                    // calendar trick, must use own calendars basis/holidays to generate dates...
                    pricecheck = ycTK.IsValid ? ycTK.FwdRate(ycTK.spotDate(), ycTK.calendar.FarDate(ycTK.spotDate(), pd)):0;  // extra sanity checking w/ IsValid (HDB db is not 100% reliable)
                    row.TK = double.IsNaN(pricecheck) ? 0 : pricecheck;

                    pricecheck = ycBS.IsValid ? ycBS.FwdRate(ycBS.spotDate(), ycBS.calendar.FarDate(ycBS.spotDate(), pd)):0; 
                    row.xCCYBSS = double.IsNaN(pricecheck) ? 0 : pricecheck;

                    pricecheck = ycBS3x1.IsValid ?  ycBS3x1.FwdRate(ycBS3x1.spotDate(), ycBS3x1.calendar.FarDate(ycBS3x1.spotDate(), pd)):0; 
                    row.BSS3x1 = double.IsNaN(pricecheck) ? 0 : pricecheck;

                    row.bidOffer = options.bidOffer;

                    row.LS = (row.TK + row.xCCYBSS + row.BSS3x1 + row.bidOffer);

                    lsReport.Add(row);
                }


            }
            else  // we just need to create one lsReport 'row' and add it to our list
            {
                DateTime oddDate = options.odddate.Value;
                // just return the 'odd-date'
                lsReport row = new lsReport();  // liquidity spread report...
                double pricecheck;  // new with system.data.oracleclient, must check for NaN 
                row.Period = string.Format("Odd-Date ({0} days)",oddDate.Subtract(ycBS.spotDate()).Days) ;
                row.Date =  (DateTime)options.odddate;
                // calendar trick, must use own calendars basis/holidays to generate dates...
                pricecheck = ycTK.FwdRate(ycTK.spotDate(), oddDate);  // check first
                row.TK = double.IsNaN(pricecheck) ? 0 : pricecheck;

                pricecheck = ycBS.FwdRate(ycBS.spotDate(), oddDate); //valueDate, row.Date);
                row.xCCYBSS = double.IsNaN(pricecheck) ? 0 : pricecheck;

                pricecheck = ycBS3x1.IsValid ? ycBS3x1.FwdRate(ycBS3x1.spotDate(), oddDate):0; //valueDate, row.Date);
                row.BSS3x1 = double.IsNaN(pricecheck) ? 0 : pricecheck;

                row.bidOffer = options.bidOffer;

                row.LS = (row.TK + row.xCCYBSS + row.BSS3x1 + row.bidOffer);

                lsReport.Add(row);
            }


            // return this dynamic/JSON
            return new
            {
                status = "success",
                data = new
                {
                    ccy = options.ccy,
                    curve1 = ycTK.CurveName,  // treasury Kurve
                    curve2 = ycBS.CurveName,  // basis swap curve  (3m vs 3m)
                    //c2Rates = ycBS.DisplayZero(),
                    curve3 = ycBS3x1.CurveName,  // domestic market's 3x1 curve
                    holidays = ycBS.calendar.holidayCities,
                    fixingdate = options.fixingDate,
                    bssfixingdate = bssFixDate,
                    valuedate = valueDate,
                    oddDateMin = ycBS.calendar.FarDate(valueDate, "1Y"),
                    oddDateMax = ycBS.maxMaturity,
                    timestamp = ycTK.prices.TimeStamp,
                    timestampUTC = ycTK.prices.TimeStampUTC,
                    rows = lsReport
                }
            };
        }
        catch (Exception ex)
        {
            return new
            {
                status = "error",
                message = ex.Message
            };
        }

    }



    /// <summary>
    /// return the CCY codes listed in HDB in JSON format
    /// </summary>
    /// <returns>jSend API  (JSON Envelope)</returns>
    [WebMethod]
    public dynamic getHDBCCY()
    {
        try
        {

            var data = new CreditPricingEntities();
            //last saved dates
            var qdates = (from r in data.RATETimeStamps
                          orderby r.TimeStamp descending
                          select r).Take(1).SingleOrDefault();

            var ctx = new hdbOracleEntities();

            // use LINQ to query the HDB database  call the GetData method on our 'old tableAdapter'
            var ccy = (from c in ctx.BSS_ST
                       select new
                       {
                           c.CURRENCY
                       }).Distinct();


            // then use LINQ to query the 'old datatable' and return JSON
            var rows = (from c in ccy orderby c.CURRENCY
                        select new
                        {
                            ccy = c.CURRENCY
                        }).ToList();

            return new
            {
                status = "success",
                data = new
                {
                    fixingdate = qdates.SaveDate,
                    timestamp = qdates.TimeStamp,
                    timestampUTC = qdates.TimeStampUTC,
                    results = rows
                }
            };

        }
        catch (Exception ex)
        {
            return new
            {
                status = "error",
                message = ex.Message
                //results = new string[] { }  //empty
            };
        }
    }

    /// <summary>
    /// return BSS term structure data 
    /// </summary>
    /// <param name="options"></param>
    /// <returns>jSend API  (JSON Envelope)</returns>
    [WebMethod]
    public dynamic getTSData(lsOptions options)
    {
        var ctx = new hdbOracleEntities();
        try
        {

            if (options.xCCY == null)
            {
                options.xCCY = true;  // override for now
            }

            Calendar cal = new Calendar(options.ccy, options.fixingDate);
            DateTime maxBSS = (from curve in ctx.BSSes where curve.SYMBOL.StartsWith("EUR") select curve.TRADE_DATE).Max(); // (DateTime)ta.maxDatePerBSSCurve("EUR%");

            DateTime bssFixDate;

            if (maxBSS < options.fixingDate)
            {
                bssFixDate = maxBSS;
            }
            else
            {
                bssFixDate = options.fixingDate;
            }
            DateTime prevDate = cal.Workdays(bssFixDate, -1);

            // fixingdate
            YldCurve ycBS = new YldCurve(options.ccy, bssFixDate, options.xCCY);  // note this constructor gathers data from HDB
            // t-1  (previous date)
            YldCurve ycBSprev = new YldCurve(options.ccy, prevDate, options.xCCY);  // note this constructor gathers data from HDB


            // return this dynamic/JSON
            return new
            {
                status = "success",
                data = new
                {
                    ccy = options.ccy,
                    curve = ycBS.CurveName,  // basis swap curve  (3m vs 3m)
                    holidays = ycBS.calendar.holidayCities,
                    fixingdate = options.fixingDate.ToShortDateString(),
                    prevdate = prevDate.ToShortDateString(),
                    bssfixingdate = bssFixDate.ToShortDateString(),
                    valuedate = ycBS.spotDate(),
                    timestamp = ycBS.prices.TimeStamp,
                    timestampUTC = ycBS.prices.TimeStampUTC,
                    rows = ycBS.GetPrices(),
                    rowsprev = ycBSprev.GetPrices()
                }
            };
        }
        catch (Exception ex)
        {
            return new
            {
                status = "error",
                message = ex.Message
            };
        }
    }


    /// <summary>
    /// return time series for given ratecode/pillar
    /// </summary>
    /// <param name="options"></param>
    /// <returns>jSend API  (JSON Envelope)</returns>
    [WebMethod]
    public dynamic getHSData(hsOptions options)
    {
        var ctx = new hdbOracleEntities();
        try
        {
            TimeSpan ts = new TimeSpan(365,0,0,0);
            DateTime yrAgo = DateTime.Today.Subtract(ts);
            
            var data = (from r in ctx.BSSes where r.SYMBOL==options.Pillar && r.TRADE_DATE > yrAgo // hs.AsEnumerable()
                        select new
                        {
                            date = r.TRADE_DATE,
                            price = Math.Round(r.PRICE,24)
                        }).ToList();
            
            return new
            {
                status = "success",
                data = new
                {
                    pillar = options.Pillar,
                    rows = data
                }
            };
        }
        catch (Exception ex)
        {
            return new
            {
                status = "error",
                message = ex.Message
            };
        }
    }



}


