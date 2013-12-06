using System;
using System.Data;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Web;



namespace CreditPricing
{


    public class ArbOutput
    {
        public string period { get; set; }
        public DateTime date { get; set; }
        //public string fwdTicker {get; set;}
        public double fxSwapBid { get; set; }
        public double fxSwapAsk { get; set; }
        public double index1 { get; set; }
        public double arbLend { get; set; }
        public double arbBorrow { get; set; }
        public double index2 { get; set; }
        public double strip3M { get; set; }
        public double bss1V3 { get; set; }
        public double arbLendSpread { get; set; }
        public double arbLendSpread1M { get; set; }
        //public double arbBorrowSpread {get;set;}
        public string debug { get; set; }
    }

    public class ArbOutputLS
    {
        public string period { get; set; }
        public DateTime date { get; set; }
        //public string fwdTicker {get; set;}
        public double fxSwapBid { get; set; }
        public double fxSwapAsk { get; set; }
        public double index1 { get; set; }
        public double LS { get; set; }
        public double index1LS { get; set; }
        public double arbLend { get; set; }
        public double arbBorrow { get; set; }
        public double index2 { get; set; }
        public double strip3M { get; set; }
        public double bss1V3 { get; set; }
        public double arbLendSpread { get; set; }
        public double arbLendSpread1M { get; set; }
        //public double arbBorrowSpread {get;set;}
        public string debug { get; set; }
    }


    /// <summary>
    /// composite class of fxswaps and domestic curves for each ccy pair
    /// </summary>
    /// 
    public class fxarbModel
    {
        /// <summary>
        /// arb model params, static from DB
        /// </summary>
        public fx_arb arb { get; set; }
        /// <summary>
        /// forex calendar w/ holiday from list of cities as per arb model
        /// </summary>
        //public Calendar fxCal { get; set; }
        /// <summary>
        /// forex market spot + fx swap points
        /// </summary>
        public YldCurve fxmkt { get; set; }
        /// <summary>
        /// domestic market first ccy in fx market pair
        /// </summary>
        public YldCurve curve1 { get; set; }
        /// <summary>
        /// domestic market second ccy in fx market pair
        /// </summary>
        public YldCurve curve2 { get; set; }
        /// <summary>
        /// domestic strip curve (used up to 2 yr to infer outright over 3M)
        /// </summary>
        public YldCurve curve3 { get; set; }
        /// <summary>
        /// basis swap curve 1v3  to convert 3M to 1M basis
        /// </summary>
        public YldCurve curve4 { get; set; }
        /// <summary>
        /// LS curve
        /// </summary>
        public YldCurve curve5 { get; set; }
        /// <summary>
        /// List of string for holiday cities
        /// </summary>
        public List<string> cities { get; set; }
        /// <summary>
        /// source date for rates/index
        /// </summary>
        private DateTime fixingDate { get; set; }
        /// <summary>
        /// spot date usually fix + 2
        /// </summary>
        private DateTime valueDate { get; set; }
        /// <summary>
        /// forex spot market quoted in market convention for given ccy pair 
        /// </summary>
        public double spotFX { get; set; }

        /// <summary>
        /// main constructore  
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_date"></param>
        public fxarbModel(int _id, DateTime _date)
        {
            fixingDate = _date;
            var ctx = new CreditPricingEntities();
            try
            {
                arb = (from f in ctx.fx_arbs
                       where f.id == _id
                       select f).ToList()[0];

                cities = arb.holidayList.Split(',').Select(p => p.Trim()).ToList();

                //fxCal = new Calendar(arb.holidayList.Split(',').Select(p => p.Trim()).ToList(), fixingDate);
                //valueDate = fxCal.Workdays(fixingDate, 2);

                fxmkt = new YldCurve(arb.fxswaps, fixingDate);
                fxmkt.InterpMode = tkInterModes.tkINTERPSL;  // use straight line

                valueDate = fxmkt.spotDate();

                spotFX = fxmkt.spotFX * 100;
                curve1 = new YldCurve(arb.curve1, fixingDate);
                curve2 = new YldCurve(arb.curve2, fixingDate);
                curve3 = new YldCurve(arb.curve3, fixingDate);
                curve4 = new YldCurve(arb.curve4, fixingDate);
                if (arb.curve1 == "TK_LIQSHORT")
                {
                    curve5 = new YldCurve("TK_UNGEDECKT", fixingDate);
                }
                else
                {
                    curve5 = null;
                }


            }
            catch (Exception)
            {

            }
        }


        /// <summary>
        /// fx arbitrage calc to a period ie 1W 3M 1Y etc 'liquidity spread' over domestic index2
        /// </summary>
        /// <param name="_period"></param>
        /// <returns></returns>
        private ArbOutput calcArb(string _period)
        {
            ArbOutput row = new ArbOutput();

            DateTime fxMktSpot = fxmkt.spotDate();
            DateTime mDate = fxmkt.calendar.FarDate(fxMktSpot, _period);

            int dip = mDate.Subtract(fxMktSpot).Days;
            row.period = _period;
            row.date = mDate;

            row.fxSwapBid = fxmkt.InterpRate(fxmkt.calendar.FarDate(fxMktSpot, _period), tkPrice.tkBID) * 100;
            row.fxSwapAsk = fxmkt.InterpRate(fxmkt.calendar.FarDate(fxMktSpot, _period), tkPrice.tkASK) * 100;

            // note pluck the domestic spot rates via dates
            DateTime spotdate1 = curve1.spotDate();
            row.index1 = curve1.FwdRate(spotdate1, curve1.calendar.FarDate(spotdate1, _period), tkPrice.tkASK, (int)arb.basis1);
            DateTime spotdate2 = curve2.spotDate();

            // new: if over 120 days then we are using curve3 (the futures strip up to 2 years)

            row.index2 = curve2.FwdRate(spotdate2, curve2.calendar.FarDate(spotdate2, _period), tkPrice.tkASK, (int)arb.basis2);
            row.strip3M = curve3.FwdRate(spotdate2, curve2.calendar.FarDate(spotdate2, _period), tkPrice.tkASK, (int)arb.basis2);
            row.bss1V3 = curve4.FwdRate(spotdate2, curve2.calendar.FarDate(spotdate2, _period), tkPrice.tkASK, (int)arb.basis2);

            //row.debug = string.Format("bid {0} ask: {1} period: {2} spotDate {3:d} farDate: {4:d}", row.fxSwapBid, row.fxSwapAsk, _period, fxMktSpot,mDate);

            row.arbLend = ((spotFX + row.fxSwapAsk / 10000) / spotFX * (1 + row.index1 * dip / (int)arb.basis1) - 1) * (int)arb.basis2 / dip * 100;
            row.arbBorrow = ((spotFX + row.fxSwapBid / 10000) / spotFX * (1 + row.index1 * dip / (int)arb.basis1) - 1) * (int)arb.basis2 / dip * 100;
            row.arbLendSpread = (dip < 120) ? row.arbLend - row.index2 * 100 : row.arbLend - row.strip3M * 100;
            row.arbLendSpread1M = row.arbLendSpread + row.bss1V3 * 100; // (dip < 120) ? row.arbLend - row.index2 * 100 : row.arbLend - row.strip3M * 100;
            //row.arbBorrowSpread = row.arbBorrow - row.index2*100;
            return row;
        }

        /// <summary>
        /// calc FXarb to on odd-date
        /// </summary>
        /// <param name="_arbDate"></param>
        /// <returns></returns>
        public ArbOutput calcArb(DateTime _arbDate)
        {
            ArbOutput row = new ArbOutput();

            DateTime fxMktSpot = fxmkt.spotDate();
            DateTime mDate = _arbDate;

            int dip = mDate.Subtract(fxMktSpot).Days;
            row.period = string.Format("{0}-days", dip);
            row.date = mDate;

            row.fxSwapBid = fxmkt.InterpRate(mDate, tkPrice.tkBID) * 100;
            row.fxSwapAsk = fxmkt.InterpRate(mDate, tkPrice.tkASK) * 100;

            // note pluck the domestic spot rates via dates
            DateTime spotdate1 = curve1.spotDate();
            row.index1 = curve1.FwdRate(spotdate1, mDate, tkPrice.tkASK, (int)arb.basis1);
            DateTime spotdate2 = curve2.spotDate();
            row.index2 = curve2.FwdRate(spotdate2, mDate, tkPrice.tkASK, (int)arb.basis2); // curve2.FwdRate(valueDate, curve2.calendar.FarDate(valueDate, _period));
            row.strip3M = curve3.FwdRate(spotdate2, mDate, tkPrice.tkASK, (int)arb.basis2);
            row.bss1V3 = curve4.FwdRate(spotdate2, mDate, tkPrice.tkASK, (int)arb.basis2);
            //row.debug = string.Format("bid {0} ask: {1} period: {2} spotDate {3:d} farDate: {4:d}", row.fxSwapBid, row.fxSwapAsk, _period, fxMktSpot, mDate);

            row.arbLend = ((spotFX + row.fxSwapAsk / 10000) / spotFX * (1 + row.index1 * dip / (int)arb.basis1) - 1) * (int)arb.basis2 / dip * 100;
            row.arbBorrow = ((spotFX + row.fxSwapBid / 10000) / spotFX * (1 + row.index1 * dip / (int)arb.basis1) - 1) * (int)arb.basis2 / dip * 100;

            row.arbLendSpread = (dip < 120) ? row.arbLend - row.index2 * 100 : row.arbLend - row.strip3M * 100;
            row.arbLendSpread1M = row.arbLendSpread + row.bss1V3 * 100; // (dip < 120) ? row.arbLend - row.index2 * 100 : row.arbLend - row.strip3M * 100;
            //row.arbLendSpread = row.arbLend - row.index2 * 100;
            //row.arbBorrowSpread = row.arbBorrow - row.index2 * 100;
            return row;
        }



        /// <summary>
        /// fx arbitrage calc to a period ie 1W 3M 1Y etc 'liquidity spread' over domestic index2
        /// </summary>
        /// <param name="_period"></param>
        /// <returns></returns>
        private ArbOutputLS calcArbLS(string _period)
        {
            ArbOutputLS row = new ArbOutputLS();

            DateTime fxMktSpot = fxmkt.spotDate();
            DateTime mDate = fxmkt.calendar.FarDate(fxMktSpot, _period);

            int dip = mDate.Subtract(fxMktSpot).Days;
            row.period = _period;
            row.date = mDate;

            row.fxSwapBid = fxmkt.InterpRate(fxmkt.calendar.FarDate(fxMktSpot, _period), tkPrice.tkBID) * 100;
            row.fxSwapAsk = fxmkt.InterpRate(fxmkt.calendar.FarDate(fxMktSpot, _period), tkPrice.tkASK) * 100;

            // note pluck the domestic spot rates via dates
            DateTime spotdate1 = curve1.spotDate();
            row.index1 = curve1.FwdRate(spotdate1, curve1.calendar.FarDate(spotdate1, _period), tkPrice.tkASK, (int)arb.basis1);
            row.LS = curve5.FwdRate(spotdate1, curve1.calendar.FarDate(spotdate1, _period), tkPrice.tkASK, (int)arb.basis1);
            row.index1LS = (row.index1 + row.LS);

            DateTime spotdate2 = curve2.spotDate();


            // new: if over 120 days then we are using curve3 (the futures strip up to 2 years)

            row.index2 = curve2.FwdRate(spotdate2, curve2.calendar.FarDate(spotdate2, _period), tkPrice.tkASK, (int)arb.basis2);
            row.strip3M = curve3.FwdRate(spotdate2, curve2.calendar.FarDate(spotdate2, _period), tkPrice.tkASK, (int)arb.basis2);
            row.bss1V3 = curve4.FwdRate(spotdate2, curve2.calendar.FarDate(spotdate2, _period), tkPrice.tkASK, (int)arb.basis2);

            //row.debug = string.Format("bid {0} ask: {1} period: {2} spotDate {3:d} farDate: {4:d}", row.fxSwapBid, row.fxSwapAsk, _period, fxMktSpot,mDate);

            row.arbLend = ((spotFX + row.fxSwapAsk / 10000) / spotFX * (1 + row.index1LS * dip / (int)arb.basis1) - 1) * (int)arb.basis2 / dip * 100;
            row.arbBorrow = ((spotFX + row.fxSwapBid / 10000) / spotFX * (1 + row.index1LS * dip / (int)arb.basis1) - 1) * (int)arb.basis2 / dip * 100;
            row.arbLendSpread = (dip < 120) ? row.arbLend - row.index2 * 100 : row.arbLend - row.strip3M * 100;
            row.arbLendSpread1M = row.arbLendSpread + row.bss1V3 * 100; // (dip < 120) ? row.arbLend - row.index2 * 100 : row.arbLend - row.strip3M * 100;
            //row.arbBorrowSpread = row.arbBorrow - row.index2*100;
            return row;
        }



        /// <summary>
        /// calc FXarb w/ LiquiditySpread to on odd-date
        /// </summary>
        /// <param name="_arbDate"></param>
        /// <returns></returns>
        public ArbOutputLS calcArbLS(DateTime _arbDate)
        {
            ArbOutputLS row = new ArbOutputLS();

            DateTime fxMktSpot = fxmkt.spotDate();
            DateTime mDate = _arbDate;

            int dip = mDate.Subtract(fxMktSpot).Days;
            row.period = string.Format("{0}-days", dip);
            row.date = mDate;

            row.fxSwapBid = fxmkt.InterpRate(mDate, tkPrice.tkBID) * 100;
            row.fxSwapAsk = fxmkt.InterpRate(mDate, tkPrice.tkASK) * 100;

            // note pluck the domestic spot rates via dates
            DateTime spotdate1 = curve1.spotDate();
            row.LS = curve5.FwdRate(spotdate1, mDate, tkPrice.tkASK, (int)arb.basis1);
            row.index1 = curve1.FwdRate(spotdate1, mDate, tkPrice.tkASK, (int)arb.basis1);
            row.index1LS = (row.LS + row.index1);
            DateTime spotdate2 = curve2.spotDate();
            row.index2 = curve2.FwdRate(spotdate2, mDate, tkPrice.tkASK, (int)arb.basis2); // curve2.FwdRate(valueDate, curve2.calendar.FarDate(valueDate, _period));
            row.strip3M = curve3.FwdRate(spotdate2, mDate, tkPrice.tkASK, (int)arb.basis2);
            row.bss1V3 = curve4.FwdRate(spotdate2, mDate, tkPrice.tkASK, (int)arb.basis2);
            //row.debug = string.Format("bid {0} ask: {1} period: {2} spotDate {3:d} farDate: {4:d}", row.fxSwapBid, row.fxSwapAsk, _period, fxMktSpot, mDate);

            row.arbLend = ((spotFX + row.fxSwapAsk / 10000) / spotFX * (1 + row.index1LS * dip / (int)arb.basis1) - 1) * (int)arb.basis2 / dip * 100;
            row.arbBorrow = ((spotFX + row.fxSwapBid / 10000) / spotFX * (1 + row.index1LS * dip / (int)arb.basis1) - 1) * (int)arb.basis2 / dip * 100;

            row.arbLendSpread = (dip < 120) ? row.arbLend - row.index2 * 100 : row.arbLend - row.strip3M * 100;
            row.arbLendSpread1M = row.arbLendSpread + row.bss1V3 * 100; // (dip < 120) ? row.arbLend - row.index2 * 100 : row.arbLend - row.strip3M * 100;
            //row.arbLendSpread = row.arbLend - row.index2 * 100;
            //row.arbBorrowSpread = row.arbBorrow - row.index2 * 100;
            return row;
        }



        /// <summary>
        /// one year and under 
        /// </summary>
        /// <returns></returns>
        public dynamic arbReport()
        {
            int[] weeks;
            int[] months;
            weeks = new int[] { 1, 2, 3 };
            months = new int[] { 1, 2, 3, 4, 5, 6, 9, 10, 11, 12 };

            try
            {
                //DateTime spotdate = fxmkt.ValueDate;
                List<ArbOutput> rpt = new List<ArbOutput>();

                for (int i = 0; i < weeks.Count(); i++)
                {
                    string id = string.Format("{0}W", weeks[i]);
                    ArbOutput row = calcArb(id);
                    rpt.Add(row);
                }
                //months
                for (int i = 0; i < months.Count(); i++)
                {
                    string id = string.Format("{0}M", months[i]);
                    ArbOutput row = calcArb(id);
                    rpt.Add(row);
                }


                return new
                {
                    status = "success",
                    data = new
                    {
                        model = arb,
                        timestamp = fxmkt.prices.TimeStamp,
                        timestampUTC = fxmkt.prices.TimeStampUTC,
                        fixing = this.fixingDate,
                        valuedate = this.valueDate,
                        maxmaturity = fxmkt.maxMaturity,
                        spotfx = spotFX,
                        fxmkt = fxmkt.GetPrices(),
                        fxho1 = fxmkt.calendar.holidayCities,
                        curve1 = curve1.GetPrices(),
                        ho11 = curve1.calendar.holidayCities,
                        index1 = curve1.CurveName,
                        index2 = curve2.CurveName,
                        index3 = curve3.CurveName,
                        curve2 = curve2.GetPrices(),
                        curve3 = curve3.GetPrices(),
                        curve4 = curve4.GetPrices(),
                        hol2 = curve2.calendar.holidayCities,
                        rows = rpt
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
        /// one year and under 
        /// </summary>
        /// <returns></returns>
        public dynamic arbReportLS()
        {
            int[] weeks;
            int[] months;
            weeks = new int[] { 1, 2, 3 };
            months = new int[] { 1, 2, 3, 4, 5, 6, 9, 10, 11, 12 };

            try
            {
                //DateTime spotdate = fxmkt.ValueDate;
                List<ArbOutputLS> rpt = new List<ArbOutputLS>();

                for (int i = 0; i < weeks.Count(); i++)
                {
                    string id = string.Format("{0}W", weeks[i]);
                    ArbOutputLS row = calcArbLS(id);
                    rpt.Add(row);
                }
                //months
                for (int i = 0; i < months.Count(); i++)
                {
                    string id = string.Format("{0}M", months[i]);
                    ArbOutputLS row = calcArbLS(id);
                    rpt.Add(row);
                }


                return new
                {
                    status = "success",
                    data = new
                    {
                        model = arb,
                        timestamp = fxmkt.prices.TimeStamp,
                        timestampUTC = fxmkt.prices.TimeStampUTC,
                        fixing = this.fixingDate,
                        valuedate = this.valueDate,
                        maxmaturity = fxmkt.maxMaturity,
                        spotfx = spotFX,
                        fxmkt = fxmkt.GetPrices(),
                        fxho1 = fxmkt.calendar.holidayCities,
                        curve1 = curve1.GetPrices(),
                        ho11 = curve1.calendar.holidayCities,
                        index1 = curve1.CurveName,
                        index2 = curve2.CurveName,
                        index3 = curve3.CurveName,
                        index5 = curve5.CurveName,
                        curve2 = curve2.GetPrices(),
                        curve3 = curve3.GetPrices(),
                        curve4 = curve4.GetPrices(),
                        curve5 = curve5.GetPrices(),
                        hol2 = curve2.calendar.holidayCities,
                        rows = rpt
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

        public dynamic arbReportLSapi()
        {
            int[] weeks;
            int[] months;
            weeks = new int[] { 1, 2, 3 };
            months = new int[] { 1, 2, 3, 4, 5, 6, 9, 10, 11, 12 };

            try
            {
                //DateTime spotdate = fxmkt.ValueDate;
                List<ArbOutputLS> rpt = new List<ArbOutputLS>();

                for (int i = 0; i < weeks.Count(); i++)
                {
                    string id = string.Format("{0}W", weeks[i]);
                    ArbOutputLS row = calcArbLS(id);
                    rpt.Add(row);
                }
                //months
                for (int i = 0; i < months.Count(); i++)
                {
                    string id = string.Format("{0}M", months[i]);
                    ArbOutputLS row = calcArbLS(id);
                    rpt.Add(row);
                }


                return new
                {
                    status = "success",
                    data = new
                    {
                        arbModel = new { arb.id, arb.arbCCY, arb.basis1, arb.basis2, arb.curve1, arb.curve2, arb.curve3, arb.curve4, arb.description },
                        timestamp = fxmkt.prices.TimeStamp,
                        timestampUTC = fxmkt.prices.TimeStampUTC,
                        fixing = this.fixingDate,
                        valuedate = this.valueDate,
                        maxmaturity = fxmkt.maxMaturity,
                        spotfx = spotFX,
                        fxmkt = fxmkt.GetPrices(),
                        fxho1 = fxmkt.calendar.holidayCities,
                        curve1 = curve1.GetPrices(),
                        ho11 = curve1.calendar.holidayCities,
                        index1 = curve1.CurveName,
                        index2 = curve2.CurveName,
                        index3 = curve3.CurveName,
                        index5 = curve5.CurveName,
                        curve2 = curve2.GetPrices(),
                        curve3 = curve3.GetPrices(),
                        curve4 = curve4.GetPrices(),
                        curve5 = curve5.GetPrices(),
                        hol2 = curve2.calendar.holidayCities,
                        rows = rpt
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

        public dynamic arbReportLSapi(DateTime bespokeDate)
        {
            try
            {
                List<ArbOutputLS> rpt = new List<ArbOutputLS>();
                ArbOutputLS row = calcArbLS(bespokeDate);
                rpt.Add(row);

                return new
                {
                    status = "success",
                    data = new
                    {
                        arbModel = new { arb.id, arb.arbCCY, arb.basis1, arb.basis2, arb.curve1, arb.curve2, arb.curve3, arb.curve4, arb.description },
                        timestamp = fxmkt.prices.TimeStamp,
                        timestampUTC = fxmkt.prices.TimeStampUTC,
                        fixing = this.fixingDate,
                        valuedate = this.valueDate,
                        maxmaturity = fxmkt.maxMaturity,
                        spotfx = spotFX,
                        fxmkt = fxmkt.GetPrices(),
                        fxho1 = fxmkt.calendar.holidayCities,
                        curve1 = curve1.GetPrices(),
                        ho11 = curve1.calendar.holidayCities,
                        index1 = curve1.CurveName,
                        index2 = curve2.CurveName,
                        index3 = curve3.CurveName,
                        index5 = curve5.CurveName,
                        curve2 = curve2.GetPrices(),
                        curve3 = curve3.GetPrices(),
                        curve4 = curve4.GetPrices(),
                        curve5 = curve5.GetPrices(),
                        hol2 = curve2.calendar.holidayCities,
                        rows = rpt
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

        public dynamic oddDateCalculator(DateTime _oddDate)
        {
            try
            {
                List<ArbOutput> rpt = new List<ArbOutput>();
                rpt.Add(calcArb(_oddDate));

                return new
                {
                    status = "success",
                    data = new
                    {
                        model = this.arb,
                        timestamp = this.fxmkt.prices.TimeStamp,
                        timestampUTC = this.fxmkt.prices.TimeStampUTC,
                        fixing = this.fixingDate,
                        valuedate = this.valueDate,
                        maxmaturity = this.fxmkt.maxMaturity,
                        spotfx = this.spotFX,
                        fxmkt = this.fxmkt.GetPrices(),
                        fxho1 = this.fxmkt.calendar.holidayCities,
                        curve1 = this.curve1.GetPrices(),
                        ho11 = this.curve1.calendar.holidayCities,
                        index1 = this.curve1.CurveName,
                        index2 = this.curve2.CurveName,
                        index3 = this.curve3.CurveName,
                        curve2 = this.curve2.GetPrices(),
                        curve3 = this.curve3.GetPrices(),
                        curve4 = this.curve4.GetPrices(),
                        hol2 = this.curve2.calendar.holidayCities,
                        rows = rpt  // just add one row
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

        public dynamic oddDateCalculatorLS(DateTime _oddDate)
        {
            try
            {
                List<ArbOutputLS> rpt = new List<ArbOutputLS>();
                rpt.Add(calcArbLS(_oddDate));

                return new
                {
                    status = "success",
                    data = new
                    {
                        model = this.arb,
                        timestamp = this.fxmkt.prices.TimeStamp,
                        timestampUTC = this.fxmkt.prices.TimeStampUTC,
                        fixing = this.fixingDate,
                        valuedate = this.valueDate,
                        maxmaturity = this.fxmkt.maxMaturity,
                        spotfx = this.spotFX,
                        fxmkt = this.fxmkt.GetPrices(),
                        fxho1 = this.fxmkt.calendar.holidayCities,
                        curve1 = this.curve1.GetPrices(),
                        ho11 = this.curve1.calendar.holidayCities,
                        index1 = this.curve1.CurveName,
                        index2 = this.curve2.CurveName,
                        index3 = this.curve3.CurveName,
                        index5 = this.curve5.CurveName,
                        curve2 = this.curve2.GetPrices(),
                        curve3 = this.curve3.GetPrices(),
                        curve4 = this.curve4.GetPrices(),
                        curve5 = this.curve5.GetPrices(),
                        hol2 = this.curve2.calendar.holidayCities,
                        rows = rpt  // just add one row
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
    }

}



