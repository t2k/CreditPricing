using System;
using System.Data;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Web;


/// <summary>
/// Summary description for YldCurveHDBPart
/// </summary>
/// 
namespace CreditPricing
{
    public partial class YldCurve
    {
                //  TODO: Let's try to add this method in a 'Partial Class with access to the 
        //// new Jun 09, a constructor to create a YC object from HDB database BSS table
        //// now we need to create a xCCY (Cross ccy basis swap) or 
        //// if _boolCrossCCYBBB = true then we grab the 3M CCY vs 3M EURIBOR 
        //// if _bookCrossCCYBSS = false we grab the 3x1 straight basis swap in CCY (not a cross ccy swap)
        //// new in Dec08 Ted Killilea  Query the HDB BSS table directly...
        //// containing basis swap spreads in BP to optimize loading in WEB environment
        //// new stuff: teach the object how to load ratecodes on the fly from BSS table in HDB database
        public YldCurve(string _ccy, DateTime _EvalDate, bool _boolCrossCCYBSS)
        {
            var ctx = new hdbOracleEntities();

            try
            {
                spotFX = 1;
                spotDays = 2;
                CCY = _ccy;
                string strFirstLetter = _ccy.Substring(0, 1);
                ValueDate = _EvalDate;
                // return the default curve per CCY

                //dsCreditNetTableAdapters.locCcyDefTableAdapter taCCY = new dsCreditNetTableAdapters.locCcyDefTableAdapter();
                if (_boolCrossCCYBSS)
                {
                    //taCCY.GetYldCurveName(strCCY)
                    CurveName = string.Format("{0}BSS3{1}3E", _ccy, strFirstLetter);
                }
                else
                {
                    //taCCY.GetYldCurveName(strCCY)
                    CurveName = string.Format("{0}BSS1{1}3{1}", _ccy, strFirstLetter);
                }

                // new Nov/2011 create a list of CCYs to load holidays from...
                List<string> ccylist = new List<string>();  // cross ccy swaps, we load EUR always...

                ccylist.Add("EUR");
                if (_ccy != "EUR")
                {
                    ccylist.Add(_ccy);  // second ccy pair from xCCY swap
                }

                m_cal = new Calendar(_EvalDate, ccylist);

                BPShift = 0;
                InterpMode = tkInterModes.tkINTERPLOG;

                // create an empty ratecodes collection
                rateCodes = new RateCodes();  //standard constructor
                rateCodes.ValueDate = _EvalDate;

                // create an empty _prices collection
                prices = new RealTimePrices();
                prices.ValueDate = _EvalDate;
                //prices.TimeStamp = _EvalDate;

                
                var bssCurve = (from s in ctx.BSSes where s.SYMBOL.StartsWith(CurveName) && s.TRADE_DATE == _EvalDate select new {
                            symbol = s.SYMBOL,
                            tradeDate=s.TRADE_DATE,
                            price = Math.Round(s.PRICE,24)
                }).ToList();
                // this contructor is tied to HDB database
                // query data from hDB database, this is a BRITTLE solution
                // but for this constructor only the SYMBOL column ends with a 3 digit period ie 01W 01M 02Y etc
                // the qry & q2 below are using LINQ to sort the symbols in date order as required by my yield curve class's embedded ratecodes
                // collection, the interp logic assumes ratecodes are in asc order...
                // don't forget the wildcard% character on the end
                // direct injection of ratecodes via BSS table...
                // tweak the BSS table into my Ratecodes and Rate/price structure
                // doing 2 things very important here.. loading ratecodes and prices
                Rate prStub = new Rate(_ccy + "STUB", 0, 0, 0);
                prices.Rates.Add(prStub.RATECODE, prStub);
                rateCodes.Add(prStub.RATECODE, new RateCode(prStub.RATECODE, _ccy, this.fixingDate, this.spotDate()));
                // new Apr 10 2012, use LINQ to convert SYMBOL field to an INT column denoting order, then we sort....
                // ie SYMBOL looks like USDBSS3U3E01W  where last 3 chars are the period, they come down from HDB in non sorted order so this
                // is a must for the interpolation to work
                var qry = (from row in bssCurve
                          select new
                          {
                              SYMBOL = row.symbol,
                              PRICE = row.price, // note PRICE in HDB is stored in Basis Points ie 50.5  we need to load this into my class as 1/10000's
                              DYS = row.symbol.EndsWith("W") ? int.Parse(row.symbol.Substring(row.symbol.Length - 3, 2)) * 7 : row.symbol.EndsWith("M") ? int.Parse(row.symbol.Substring(row.symbol.Length - 3, 2)) * 30 : int.Parse(row.symbol.Substring(row.symbol.Length - 3, 2)) * 365
                          }).ToList();

                // important, sort by DYS 
                var q2 = from bss in qry
                         orderby bss.DYS
                         select bss;


                foreach (var row in q2)
                {
                    Rate pr = new Rate(row.SYMBOL, (double)row.PRICE / 10000, (double)row.PRICE / 10000, (double)row.PRICE / 10000);
                    prices.Rates.Add(pr.RATECODE, pr);
                    // unique collection...
                    // unique collections...

                    rateCodes.Add(row.SYMBOL, new RateCode(row.SYMBOL, m_cal));  // constructor to make 'on the fly' a ratecode from a BSS row and a calendar
                }
                Recalc(prices);
            }
            catch (Exception ex)
            {
                string err = ex.Message;

                this.IsValid = false;
            }
        }
        /// <summary>
        /// New constructor
        /// </summary>
        /// <param name="_ccy"></param>
        /// <param name="_EvalDate"></param>
        /// <param name="_liquidityPeriod"></param>
        public YldCurve(string _ccy, DateTime _EvalDate, string _liquidityPeriod)
        {
            var ctx = new hdbOracleEntities();

            try
            {
                spotFX = 1;
                spotDays = 2;
                CCY = _ccy;
                string strFirstLetter = _ccy.Substring(0, 1);
                ValueDate = _EvalDate;
                // return the default curve per CCY


                switch (_liquidityPeriod)
                {
                    case "1M":  // 1M BSS not XCCY 
                        CurveName = string.Format("{0}BSS1{1}3{1}", _ccy, strFirstLetter);
                        break;
                    case "3M":  // 3M always get's XCCY swapped CCY << EUR (that's the global liquidity policy: TK is 3M EURIBOR)
                        CurveName = string.Format("{0}BSS3{1}3E", _ccy, strFirstLetter);
                        break;
                    case "6M": // fall through
                    case "12M":
                        // _ccy=USD _liquidityPeriod = "6M"  yields: USDBSS3U6U
                        CurveName = string.Format("{0}BSS3{1}{2}{1}", _ccy, strFirstLetter,int.Parse(_liquidityPeriod.Substring(0,_liquidityPeriod.Length-1)));
                        break;
                    default:
                        break;
                }

                // new Nov/2011 create a list of CCYs to load holidays from...
                List<string> ccylist = new List<string>();  // cross ccy swaps, we load EUR always...
                ccylist.Add("EUR");

                if (_ccy != "EUR")
                {
                    ccylist.Add(_ccy);  // second ccy pair from xCCY swap
                }

                m_cal = new Calendar(_EvalDate, ccylist);

                BPShift = 0;
                InterpMode = tkInterModes.tkINTERPLOG;

                // create an empty ratecodes collection
                rateCodes = new RateCodes();  //standard constructor
                rateCodes.ValueDate = _EvalDate;

                // create an empty _prices collection
                prices = new RealTimePrices();
                prices.ValueDate = _EvalDate;
                //prices.TimeStamp = _EvalDate;


                var bssCurve = (from s in ctx.BSSes
                                where s.SYMBOL.StartsWith(CurveName) && s.TRADE_DATE == _EvalDate
                                select new
                                {
                                    symbol = s.SYMBOL,
                                    tradeDate = s.TRADE_DATE,
                                    price = Math.Round(s.PRICE, 24)
                                }).ToList();

                // this contructor is tied to HDB database
                // query data from hDB database, this is a BRITTLE solution
                // but for this constructor only the SYMBOL column ends with a 3 digit period ie 01W 01M 02Y etc
                // the qry & q2 below are using LINQ to sort the symbols in date order as required by my yield curve class's embedded ratecodes
                // collection, the interp logic assumes ratecodes are in asc order...
                // don't forget the wildcard% character on the end
                // direct injection of ratecodes via BSS table...
                // tweak the BSS table into my Ratecodes and Rate/price structure
                // doing 2 things very important here.. loading ratecodes and prices

                Rate prStub = new Rate(_ccy + "STUB", 0, 0, 0);
                prices.Rates.Add(prStub.RATECODE, prStub);
                rateCodes.Add(prStub.RATECODE, new RateCode(prStub.RATECODE, _ccy, this.fixingDate, this.spotDate()));
                // new Apr 10 2012, use LINQ to convert SYMBOL field to an INT column denoting order, then we sort....
                // ie SYMBOL looks like USDBSS3U3E01W  where last 3 chars are the period, they come down from HDB in non sorted order so this
                // is a must for the interpolation to work

                var qry = (from row in bssCurve
                           select new
                           {
                               SYMBOL = row.symbol,
                               PRICE = row.price, // note PRICE in HDB is stored in Basis Points ie 50.5  we need to load this into my class as 1/10000's
                               DYS = row.symbol.EndsWith("W") ? int.Parse(row.symbol.Substring(row.symbol.Length - 3, 2)) * 7 : row.symbol.EndsWith("M") ? int.Parse(row.symbol.Substring(row.symbol.Length - 3, 2)) * 30 : int.Parse(row.symbol.Substring(row.symbol.Length - 3, 2)) * 365
                           }).ToList();

                // important, sort by DYS 
                var q2 = from bss in qry
                         orderby bss.DYS
                         select bss;

                foreach (var row in q2)
                {
                    Rate pr = new Rate(row.SYMBOL, (double)row.PRICE / 10000, (double)row.PRICE / 10000, (double)row.PRICE / 10000);
                    prices.Rates.Add(pr.RATECODE, pr);
                    // unique collection...
                    // unique collections...
                    rateCodes.Add(row.SYMBOL, new RateCode(row.SYMBOL, m_cal));  // constructor to make 'on the fly' a ratecode from a BSS row and a calendar
                }
                Recalc(prices);
            }
            catch (Exception ex)
            {
                string err = ex.Message;
                this.IsValid = false;
            }
        }


    } // partial class YldCurve





}
