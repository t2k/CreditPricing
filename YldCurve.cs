using System;
using System.Data;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Web;



namespace CreditPricing
{

    /// <summary>
    /// Money market Yield Curve 
    /// </summary>
    public partial class YldCurve
    {
        /// <summary>
        /// Interpolation Modes LogLinear is the default or  straight line can be set
        /// </summary>
        public tkInterModes InterpMode { get; set; }

        /// <summary>
        /// Does the yield curve contain valid data?
        /// Gets updated during constructor...
        /// </summary>
        public bool IsValid { get; set; }
        /// <summary>
        /// Unique CurveName
        /// </summary>
        public string CurveName { get; set; }

        /// <summary>
        /// 3 letter Currency code
        /// </summary>
        public string CCY { get; set; }
        /// <summary>
        /// value date is the date rates are loaded to create this object
        /// </summary>
        public DateTime ValueDate { get; set; }

        /// <summary>
        /// same as ValueDate symantically preferred 'fixingDate'
        /// </summary>
        public DateTime fixingDate { get { return ValueDate; } }

        /// <summary>
        ///  time snapshot that this yield curve was recalced, NOTE: to-be deprecated, can use the embedded prices.TimeStamp property to get the same
        /// </summary>
        public DateTime ycTime { get; set; }

        //local copy, todays date
        /// <summary>
        /// Basis Point Shift, not really used in web API, mostly used in legacy workbook API
        /// </summary>
        public double BPShift { get; set; }
        public double spotFX { get; set; }
        public int spotDays { get; set; }

        /// <summary>
        /// holds the internal array of discount factors for bid/ask pricing generated upon Recalc/Bootstrapping
        /// </summary>
        private rowDF[] m_Discount;

        /// <summary>
        /// embedded Dictionary of ratecode objects
        /// </summary>
        public RateCodes rateCodes { get; set; }
        /// <summary>
        /// embedded prices for this yldCurve
        /// </summary>
        public RealTimePrices prices { get; set; }


        /// <summary>
        /// local calendar object
        /// </summary>
        private Calendar m_cal;

        /// <summary>
        /// internal calendar (read-only)
        /// </summary>
        public Calendar calendar
        {
            get { return m_cal; }
        }


        /// <summary>
        /// return the CFEnd (date) of the last ratecode in this curves stored rateCodes dictionary (they are loaded in ascending order)
        /// </summary>
        public DateTime maxMaturity
        {
            get { return rateCodes.Values.ToArray()[rateCodes.Values.Count()-1].CFEnd; } // it's in there somewhere 
        }

        // used to create empty instance for copyYC method
        /// <summary>
        /// ONLY used internally, YldCurve will not be ready to use
        /// this constructor is used when returning a copy
        /// </summary>
        public YldCurve()
        {
            spotDays = 2;
            spotFX = 1;
            BPShift = 0;
            // this is for futures bp move
            ValueDate = DateTime.Today;
            InterpMode = tkInterModes.tkINTERPLOG;
            IsValid = true;
            rateCodes = new RateCodes();
            prices = new RealTimePrices(ValueDate);
        }







        /// <summary>
        /// create YldCurve from a date and a CCY
        /// will load the defaul YldCurve for the Currency (See locCCYDef table to control this)
        /// CCY is optional, defaults to 'USD'
        /// YldCurve is recalced and ready to use.
        /// </summary>
        /// <param name="_evalDate"></param>
        /// <param name="_strCCY"></param>
        public YldCurve(DateTime _evalDate, string _strCCY = "USD")
        {
            spotFX = 1;
            spotDays = 2;
            CCY = _strCCY;
            ValueDate = _evalDate;

            using (CreditPricingEntities db = new CreditPricingEntities())
            {
                CurveName = (from c in db.locCcyDefs
                             where c.CCY == CCY
                             select c.DefaultYCurve).FirstOrDefault().ToString();
            }


            m_cal = new Calendar(CCY);
            m_cal.TodayDate = _evalDate;
            BPShift = 0;
            // this is for futures bp move
            InterpMode = tkInterModes.tkINTERPLOG;

            rateCodes = new RateCodes(ValueDate, CurveName); // LoadRateCodes(); not needed...  new constructor 
            var rcodes = (from r in rateCodes select r.Key).ToList();
            prices = new RealTimePrices(ValueDate, rcodes);
 
            //RealTimePrices tkPrices = new RealTimePrices(ValueDate);
            this.Recalc(prices);
        }


        // new FEB 08,  contructors to optimize loading in WEB environment
        // create the default CYldCurve per Currency...
        // load the standard curve 
        /// <summary>
        /// Create a new YldCurve by name and pass in Real time prices
        /// YldCurve is Recalc'd and ready to use
        /// </summary>
        /// <param name="strCurveName"></param>
        /// <param name="tkPrices"></param>
        public YldCurve(string strCurveName, RealTimePrices tkPrices)
        {
            prices = tkPrices;
            spotFX = 1;
            spotDays = 2;
            BPShift = 0;
            // this is for futures bp move
            ValueDate = tkPrices.ValueDate;
            InterpMode = tkInterModes.tkINTERPLOG;
            IsValid = false;
            CurveName = strCurveName;

            var db = new CreditPricingEntities();
            CCY = (from y in db.ycHeaders
                   where y.Name == strCurveName
                   select y.CCY).FirstOrDefault().ToString();
            m_cal = new Calendar(CCY);
            m_cal.TodayDate = ValueDate;
            rateCodes = new RateCodes(ValueDate, CurveName); //   LoadRateCodes();
            Recalc(prices);
        }


        // new FEB 08,  contructors to optimize loading in WEB environment
        // create the default CYldCurve per Currency...
        // load the standard curve
        // this contructor is used when instantiating a single bond
        // therefore we need the pricing date to load the tkPricing object so we can recalc object
        /// <summary>
        /// Create a YldCurve by name from a give date and 
        /// recalc from pricing on that date
        /// </summary>
        /// <param name="strCurveName">YldCurve name to get</param>
        /// <param name="datePricing">A date used for pricing</param>
        public YldCurve(string strCurveName, DateTime datePricing)
        {
            spotFX = 1;
            spotDays = 2;
            BPShift = 0;
            // this is for futures bp move
            ValueDate = datePricing;
            InterpMode = tkInterModes.tkINTERPLOG;
            IsValid = false;
            CurveName = strCurveName;
            var db = new CreditPricingEntities();
            var qry = (from y in db.ycHeaders
                   where y.Name == strCurveName
                   select new { 
                       y.CCY, 
                       y.HolidayCenter,
                       y.curveType,
                       y.spotDays
                   }).SingleOrDefault();

            spotDays = (int)qry.spotDays;

            CCY = qry.CCY;
            m_cal = new Calendar(qry.HolidayCenter.Split(',').Select(p => p.Trim()).ToList(), datePricing);  // sort of nasty... use linq to convert csv string to list of strings
            rateCodes = new RateCodes(ValueDate, CurveName, m_cal); 
            var ycCodes = (from r in rateCodes select r.Key).ToList();
            prices = new RealTimePrices(ValueDate, ycCodes);
            //RealTimePrices tkPrices = new RealTimePrices(datePricing);
            if (qry.curveType == "MM")
            {
                Recalc(prices);
            }
            else
            {
                RecalcFX(prices);
            }
        }



        /// <summary>
        /// count the number of futures ratecodes contained in the current definition...
        /// </summary>
        public int nFutures
        {
            get
            {
                return (from r in rateCodes.Values
                        where r.RateType == 2
                        select r).Count();
            }
        }


        /// <summary>
        /// min futures startdate 
        /// this is the stubdate for our YC
        /// </summary>
        public DateTime FutStubDate
        {
            get
            {
                // wow! LINQ rocks!
                return (from r in rateCodes.Values
                        where r.RateType == 2
                        select r.CFStart).Min();
            }
        }


        // return a copy of this curve
        public YldCurve Copy
        {
            get
            {
                YldCurve copyYC = new YldCurve();
                {
                    copyYC.CCY = this.CCY;
                    copyYC.CurveName = this.CurveName;
                    copyYC.InterpMode = this.InterpMode;
                    copyYC.ValueDate = this.ValueDate;
                    copyYC.IsValid = this.IsValid;
                    copyYC.m_cal = new Calendar(copyYC.CCY);
                    foreach (RateCode rc in rateCodes.Values)
                    {
                        copyYC.rateCodes.Add(rc.RATECODE, rc.Copy);  // make a copy of 
                    }
                }
                return copyYC;
            }
        }


        // return a money market yield rates, 1yr and under is straight annual money rate
        /// <summary>
        /// ZeroRate: Under 1 year zero coupon Money Market Rate
        /// over 365 days use compounding
        /// </summary>
        /// <param name="_dateEval"></param>
        /// <param name="_iPrice"></param>
        /// <param name="_iCompoundPA"></param>
        /// <param name="_iYrBasis"></param>
        /// <returns></returns>
        public double ZeroRate(DateTime _dateEval, tkPrice _iPrice = tkPrice.tkASK, int _iCompoundPA = 1, int _iYrBasis = 360)
        {
            {
                double yr = 0;
                double dblDisc = 0;

                try
                {
                    dblDisc = GetDF(_dateEval, _iPrice);
                    //if ((System.DateTime.FromOADate(dt.ToOADate - ValueDate.ToOADate)) <= System.DateTime.FromOADate(0))
                    if (_dateEval < ValueDate)
                    {
                        return 1;
                    }

                    //if ((System.DateTime.FromOADate(dt.ToOADate - ValueDate.ToOADate)) <= System.DateTime.FromOADate(370))
                    if (_dateEval.Subtract(ValueDate).Days < 365)
                    {
                        // simple interest
                        return (1 / dblDisc - 1) * _iYrBasis / _dateEval.Subtract(ValueDate).Days;
                    }
                    else
                    {
                        //yr = (dt.ToOADate - ValueDate.ToOADate) / 365;
                        yr = (double)_dateEval.Subtract(ValueDate).Days / _iYrBasis;
                        // compounding
                        return (Math.Pow((1 / dblDisc), (1 / (yr * _iCompoundPA))) - 1) * _iCompoundPA;

                    }
                }
                catch
                {
                    return 1;
                }
            }
        }


        /// <summary>
        /// return forward rate on simple money basis
        /// </summary>
        /// <param name="_nearDate">start date (in future)</param>
        /// <param name="_farDate">end date (past start date)</param>
        /// <param name="_iPrice">bid or ask side</param>
        /// <param name="_iBasis">360 365</param>
        /// <returns></returns>
        public double FwdRate(DateTime _nearDate, DateTime _farDate, tkPrice _iPrice = tkPrice.tkASK, int _iBasis = 360)
        {
            {

                if (_farDate > maxMaturity)
                {
                    return 0;
                }


                
                // defaults to MIDRATE
                double d1 = 0;
                double d2 = 0;
                // note: first date of curve range is in row#2

                try
                {
                    d1 = GetDF(_nearDate, _iPrice);
                    d2 = GetDF(_farDate, _iPrice);
                    return (d1 / d2 - 1) * (double)_iBasis / _farDate.Subtract(_nearDate).Days;
                }
                catch
                {
                    return 0;
                }
            }
        }



        // new 6/10/99 t.killilea
        // this added so that the yield curve can tell the caller
        // the list of futures ratecodes that it contains
        public List<RateCode> FutRateCodes
        {
            get
            {
                return (from r in rateCodes.Values
                        where r.RateType == 2
                        select r).ToList();
            }
        }

       

        //get discount factor on a give date
        public double GetDF(DateTime _dateEval, tkPrice _iPrice = tkPrice.tkASK)
        {
            int i = 0;
            int iROWCOUNT = 0;
            double dfNear = 0;
            double dfFar = 0;

            try
            {
                // true for offer, false for BID
                i = 0;
                iROWCOUNT = m_Discount.Length;
                // row bound

                while (m_Discount[i].date < _dateEval && i < iROWCOUNT)
                {
                    i ++;
                }

                if (m_Discount[i - 1].date == _dateEval)
                {
                    // no interp needed...
                    if (_iPrice == tkPrice.tkBID)
                    {
                        // column #2
                        return m_Discount[i - 1].bid;
                    }
                    else if (_iPrice == tkPrice.tkASK)
                    {
                        // column #3  the offer
                        return m_Discount[i - 1].ask;
                    }
                    else
                    {
                        // mid point
                        return (m_Discount[i - 1].bid + m_Discount[i - 1].ask) / 2;
                    }
                }
                else
                {
                    // here interpolation is needed between points...
                    if (_iPrice == tkPrice.tkBID)
                    {
                        dfNear = m_Discount[i - 1].bid;
                        dfFar = m_Discount[i].bid;
                    }
                    else if (_iPrice == tkPrice.tkASK)
                    {
                        dfNear = m_Discount[i - 1].ask;
                        dfFar = m_Discount[i].ask;
                    }
                    else
                    {
                        dfNear = (m_Discount[i - 1].bid + m_Discount[i - 1].ask) / 2;
                        dfFar = (m_Discount[i].bid + m_Discount[i].ask) / 2;

                    }
                    // this is then the interpolated
                    return Interp(m_Discount[i - 1].date, dfNear, m_Discount[i].date, dfFar, _dateEval);
                }
            }
            catch
            {
                // this is essential to the pricing model...
                return 1;
            }
        }


        //get discount factor on a give date
        public double GetDF2(DateTime dt, DateTime pvDate, tkPrice iPrice = tkPrice.tkASK)
        {
            //dt is far date
            //pvdate is date to pv future amount to... usually 'today'
            int i = 0;
            int iROWCOUNT = 0;
            double dfNear = 0;
            double dfFar = 0;
            double dblRetVar = 0;

            // ERROR: Not supported in C#: OnErrorStatement

            // true for offer, false for BID
            i = 0;
            iROWCOUNT = m_Discount.Length;
            // row bound
            try
            {
                while (m_Discount[i].date < dt && i < iROWCOUNT)
                {
                    i ++;
                }
                if (m_Discount[i - 1].date == dt)
                {
                    // no interp needed...
                    if (iPrice == tkPrice.tkBID)
                    {
                        // column #2
                        dblRetVar = m_Discount[i - 1].bid;
                    }
                    else if (iPrice == tkPrice.tkASK)
                    {
                        // column #3  the offer
                        dblRetVar = m_Discount[i - 1].ask;
                    }
                    else
                    {
                        // mid point
                        dblRetVar = (m_Discount[i - 1].bid + m_Discount[i - 1].ask) / 2;
                    }
                }
                else
                {
                    // here interpolation is needed between points...
                    if (iPrice == tkPrice.tkBID)
                    {
                        dfNear = m_Discount[i - 1].bid;
                        dfFar = m_Discount[i].bid;
                    }
                    else if (iPrice == tkPrice.tkASK)
                    {
                        dfNear = m_Discount[i - 1].ask;
                        dfFar = m_Discount[i].ask;
                    }
                    else
                    {
                        dfNear = (m_Discount[i - 1].bid + m_Discount[i - 1].ask) / 2;
                        dfFar = (m_Discount[i].bid + m_Discount[i].ask) / 2;
                    }
                    // this is then the interpolated
                    dblRetVar = Interp(m_Discount[i - 1].date, dfNear, m_Discount[i].date, dfFar, dt);
                }
                if (pvDate > ValueDate)
                {
                    dblRetVar = dblRetVar / GetDF(pvDate, iPrice);
                }
                return dblRetVar;
            }
            catch
            {
                return 1;
            }
        }


        private double Interp(DateTime date1, double rate1, DateTime date2, double rate2, DateTime Evald)
        {
            if (InterpMode == tkInterModes.tkINTERPLOG)
            {
                return Math.Exp(System.Math.Log(rate1) + (Math.Log(rate2) - Math.Log(rate1)) * ((double)Evald.Subtract(date1).Days / (double)date2.Subtract(date1).Days));
            }
            else
            {
                return rate1 + (double)Evald.Subtract(date1).Days / (double)date2.Subtract(date1).Days * (rate2-rate1);
            }
        }

        // rebuild the discount factors from money market rates, futures prices and swap rates...
        // the class member m_varTWblend is an array with 3 columns: Date, BID, OFFER
        // of the discount factors.
        // #CHANGE ON 5/26/99 t.killilea
        // added support for BPShift stored in each instance of ratecode, this to implement
        // curve shifts and scenario's...
        /// <summary>
        /// bootstrap the money-market curve...
        /// </summary>
        /// <param name="Prices"></param>
        public void Recalc(RealTimePrices Prices)
        {
            short iRow = 0;
            // array 'row' index
            DateTime dtStart = default(System.DateTime);
            DateTime dtEnd = default(System.DateTime);
            //Dim dtSpot As Date
            //Dim dfSpot As Double '(discount factor for spot adjust)
            double dTotalDiscBid = 0;
            // irs discouting
            double dTotalDiscOffer = 0;
            // irs discouting
            short i = 0;
            //loop counter for swap legs
            //Dim iYears As Single
            short nCoupons = 0;
            //Dim rc As CRateCode
            double dBID = 0;
            double dASK = 0;
            DateTime dtNextCoupon = default(System.DateTime);
            // coupon dates for bootstrapping
            DateTime dtPrevCoupon = default(System.DateTime);
            Rate objRate = default(Rate);
            double dblDayFrac = 0;

            m_Discount = new rowDF[rateCodes.Count+1];

            iRow = 0;
            IsValid = true;
            m_Discount[iRow].date = this.ValueDate;
            m_Discount[iRow].bid = 1;
            m_Discount[iRow].ask = 1;

            iRow = 1;
            foreach (RateCode rc in rateCodes.Values)
            {
                {
                    objRate = Prices.Rates[rc.RATECODE];
                    if ((objRate != null))
                    {
                        switch (rc.RateType)
                        {
                            case 1:
                                // cash rates or fra rates
                                // note change here: BPShift is class member for parallel shifts
                                // rc.bpshift is instance specific shift stored with ratecode/curve instance...
                                dBID = objRate.bid + (BPShift + rc.BPShift) / 10000;
                                dASK = objRate.ask + (BPShift + rc.BPShift) / 10000;
                                dtStart = rc.CFStart;
                                dtEnd = rc.CFEnd;
                                dblDayFrac = rc.DayCountFrac;
                                m_Discount[iRow].date = dtEnd;

                                // capture front stub if not entered (curves with spot start)
                                try
                                {
                                    m_Discount[iRow].bid = GetDF(dtStart, tkPrice.tkBID) / (1 + dBID * dblDayFrac);
                                    m_Discount[iRow].ask = GetDF(dtStart, tkPrice.tkASK) / (1 + dASK * dblDayFrac);
                                }
                                catch
                                {
                                    m_Discount[iRow].bid = 1 / (1 + dBID * dblDayFrac);
                                    m_Discount[iRow].ask = 1 / (1 + dASK * dblDayFrac);
                                }
                                break;
                            case 2:
                                // futures prices
                                dtStart = rc.CFStart;
                                dtEnd = rc.CFEnd;
                                dblDayFrac = rc.DayCountFrac;
                                //# .BPShift is new here
                                dBID = objRate.ask - (BPShift + rc.BPShift) / 100;
                                dASK = objRate.bid - (BPShift + rc.BPShift) / 100;
                                m_Discount[iRow].date = dtEnd;
                                m_Discount[iRow].bid = GetDF(dtStart, tkPrice.tkBID) / (1 + (100 - dBID) / 100 * dblDayFrac);
                                m_Discount[iRow].ask = GetDF(dtStart, tkPrice.tkASK) / (1 + (100 - dASK) / 100 * dblDayFrac);
                                break;
                            case 3:
                                // swap coupon rates
                                dtStart = rc.CFStart;
                                dtPrevCoupon = dtStart;
                                dtEnd = rc.CFEnd;
                                dTotalDiscBid = 0;
                                dTotalDiscOffer = 0;
                                //# .BPShift is new here
                                dBID = objRate.bid + (BPShift + rc.BPShift) / 10000;
                                dASK = objRate.ask + (BPShift + rc.BPShift) / 10000;
                                m_Discount[iRow].date = dtEnd;
                                nCoupons = (short)(rc.nCompoundPA * float.Parse(rc.Period.Substring(0, rc.Period.Length - 1))); //. //Strings.Left(rc.Period, Strings.Len(rc.Period) - 1);
                                for (i = 1; i <= nCoupons - 1; i++)
                                {
                                    dtNextCoupon = m_cal.FwdDate(dtStart, i * (12 / rc.nCompoundPA));
                                    dblDayFrac = rc.CpnDayCountFrac(dtPrevCoupon, dtNextCoupon);
                                    dTotalDiscBid = dTotalDiscBid + ((dBID * dblDayFrac) / rc.nCompoundPA * GetDF(dtNextCoupon, tkPrice.tkBID));
                                    dTotalDiscOffer = dTotalDiscOffer + ((dASK * dblDayFrac) / rc.nCompoundPA * GetDF(dtNextCoupon, tkPrice.tkASK));
                                    dtPrevCoupon = dtNextCoupon;
                                }

                                dblDayFrac = rc.CpnDayCountFrac(dtPrevCoupon, dtEnd);

                                m_Discount[iRow].bid = ((GetDF(m_cal.Workdays(ValueDate, rc.nDays2Start), tkPrice.tkBID) - dTotalDiscBid) / (1 + (dBID * dblDayFrac) / rc.nCompoundPA));
                                m_Discount[iRow].ask = ((GetDF(m_cal.Workdays(ValueDate, rc.nDays2Start), tkPrice.tkASK) - dTotalDiscOffer) / (1 + (dASK * dblDayFrac) / rc.nCompoundPA));
                                break;
                        }
                        iRow ++;
                    }
                    else
                    {
                        IsValid = false;
                    }
                }
            }
            ycTime = Prices.TimeStamp;
        }


        /// <summary>
        /// bootstrap the fx forwards curve 
        /// </summary>
        /// <param name="Prices"></param>
        public void RecalcFX(RealTimePrices Prices)
        {
            spotFX = 1;
            short iRow = 0;
            // array 'row' index
            DateTime dtStart; 
            DateTime dtEnd;  

            double dBID = 0;
            double dASK = 0;
            Rate objRate;
            double dblDayFrac = 0;

            m_Discount = new rowDF[rateCodes.Count + 1];
            iRow = 0;
            IsValid = true;
            m_Discount[iRow].date = this.ValueDate;
            m_Discount[iRow].bid = 1;
            m_Discount[iRow].ask = 1;

            iRow++;  // 
            foreach (RateCode rc in rateCodes.Values)
            {
                {
                    objRate = Prices.Rates[rc.RATECODE];
                    if (objRate != null)
                    {
                        switch (rc.RateType)
                        {
                            case 4:
                                if (iRow == 1) // fx market spot DF =1
                                {
                                    spotFX = objRate.ask;
                                    m_Discount[iRow].date = rc.CFEnd;
                                    m_Discount[iRow].bid = 1;
                                    m_Discount[iRow].ask = 1; 
                                }
                                else
                                {
                                    dBID = objRate.bid + (BPShift + rc.BPShift) / 10000;
                                    dASK = objRate.ask + (BPShift + rc.BPShift) / 10000;
                                    dtStart = rc.CFStart;
                                    dtEnd = rc.CFEnd;
                                    dblDayFrac = rc.DayCountFrac;
                                    m_Discount[iRow].date = dtEnd;
                                    m_Discount[iRow].bid = GetDF(dtStart, tkPrice.tkBID) / (1 + dBID * dblDayFrac);
                                    m_Discount[iRow].ask = GetDF(dtStart, tkPrice.tkASK) / (1 + dASK * dblDayFrac);
                                }
                                break;
                        }
                        iRow++;
                    }
                    else
                    {
                        IsValid = false;
                    }
                }
            }
            ycTime = Prices.TimeStamp;
        }




        //**** CRITICAL ****
        //new 5/26/99  picks up the BPShift from the ycITEMHISTORY database table...
        //new 6/16/99  struc of ycITEMHISTORY table has been changed by dropping the CCY field...
        //FEB 08  totally updated this routine to read from .NET datatable row..
        //private void LoadRateCodes()
        //{
        //    rateCodes = new RateCodes(ValueDate,CurveName);

        //    // select unique records based upon two fields from ycITEMHistory table
        //    // new 6/16/99  struc of ycITEMHISTORY table has been changed by dropping the CCY field...
        //    //Set rs = db.OpenRecordset("Select DISTINCTROW RATECODE, BPSHIFT from ycITEMHistory where DATE = #" & Me.ValueDate & "# and CurveName =" & Chr(34) & CurveName & Chr(34) & " and CCY= " & Chr(34) & CCY & Chr(34) & " Order by SortOrder")
        //    var db = new CreditPricing.DataEntities();
        //    var cvrates = (from r in db.ycItemHistories
        //                   where r.Date == this.ValueDate && r.CurveName == this.CurveName
        //                   orderby r.SORTORDER
        //                   select r).ToList();


        //    // ratecodes are sorted for us in ascending order...
        //    //dsCreditNetTableAdapters.ycRATECODESTableAdapter ta = new dsCreditNetTableAdapters.ycRATECODESTableAdapter();
        //    //dsCreditNet.ycRATECODESDataTable dt = ta.GetData(this.ValueDate, this.CurveName);
        //    //foreach (dsCreditNet.ycRATECODESRow row in dt.Rows)
        //    foreach ( var row in cvrates)
        //    {
        //        {
        //            // need to dynamically set 'R'elative dates
        //            // each day these dates are dynamically calculated
        //            if (row.CF_Period_Type == "R")
        //            {
        //                // relative (calc forward dates)
        //                row.CF_StartDate = m_cal.Workdays(ValueDate, row.nDay2Start);
        //                row.CF_EndDate = m_cal.FarDate(row.CF_StartDate, row.Period);
        //                row.CF_SensiEndDate = row.CF_EndDate;
        //            }

        //            try
        //            {
        //                //
        //                rateCodes.Add(RateCode(row), row.RATECODE);
        //            }
        //            catch (Exception ex)
        //            {

        //            }
        //        }
        //    }
        //}


        public dynamic Diag()
        {
            return (from r in m_Discount
                    select new
                    {
                        date =r.date.ToShortDateString(),
                        r.bid,
                        r.ask
                    }).ToList();

        }

        public dynamic DisplayZero()
        {
            return (
                from r in rateCodes.Values
                select new
                {
                    Curve = this.CurveName,
                    ratecode = r.RATECODE,
                    desc = r.Description,
                    valueDate = r.CFStart,
                    Spot = r.CFStart.ToShortDateString(),
                    MatDate= r.CFEnd.ToShortDateString(),
                    maturityDate = r.CFSensiEnd,
                    name = r.Description,
                    Date2 = r.CFEnd,
                    zeroRate = ZeroRate(r.CFEnd)
                }).ToList();

        }

        // calculate Zero Rates and return an array of (offers)
        public List<ycDisplay> Display
        {
            get
            {
                List<ycDisplay> list = new List<ycDisplay>();

                foreach (RateCode rc in rateCodes.Values)
                {
                    ycDisplay row = new ycDisplay();
                    row.name = rc.Description;
                    row.valueDate = rc.CFStart;
                    row.maturityDate = rc.CFEnd;
                    row.Curve = this.CurveName;
                    row.Date2 = rc.CFEnd;
                    row.Date = rc.CFEnd.ToShortDateString();
                    row.zeroRate = ZeroRate(rc.CFEnd);
                    row.Rate = row.zeroRate.ToString("#.0000%");
                    list.Add(row);
                }
                return list;
            }
        }

        // display underlying ratecodes...
        public List<rcDisplay> DisplayRates(RealTimePrices _prices)
        {
            {
                List<rcDisplay> list = new List<rcDisplay>();

                foreach (RateCode rc in rateCodes.Values)
                {
                    rcDisplay row = new rcDisplay();
                    row.Period = rc.Period;
                    row.CCY = rc.CCY;
                    row.RATECODE = rc.RATECODE;
                    row.StartDate = rc.CFStart;
                    row.EndDate = rc.CFEnd;
                    row.Desc = rc.Description;
                    if (rc.RateType == 2)
                    {
                        row.Bid = 100 - _prices.Rates[rc.RATECODE].bid;
                        row.Ask = 100 - _prices.Rates[rc.RATECODE].ask;
                    }
                    else
                    {
                        row.Bid = _prices.Rates[rc.RATECODE].bid * 100;
                        row.Ask = _prices.Rates[rc.RATECODE].ask * 100;
                    }
                    list.Add(row);
                }
                return list;
            }
        }

        // display underlying ratecodes...
        public List<rtDisplay> DisplayRTRates(RealTimePrices prices)
        {
            {
                List<rtDisplay> list = new List<rtDisplay>();


                foreach (RateCode rc in rateCodes.Values)
                {
                    rtDisplay row = new rtDisplay();

                    row.CurveName = CurveName;
                    row.RateDate = prices.ValueDate;
                    row.RATECODE = rc.RATECODE;
                    row.EndDate = rc.CFEnd;
                    row.Period = rc.Period;
                    row.BASIS = rc.Basis;
                    row.CompPA = rc.nCompoundPA;
                    row.BPShift = rc.BPShift;
                    row.Description = rc.Description;
                    if (rc.RateType == 2)
                    {
                        row.BID = 100 - prices.Rates[rc.RATECODE].bid;
                        row.ASK = 100 - prices.Rates[rc.RATECODE].ask;
                    }
                    else
                    {
                        row.BID = prices.Rates[rc.RATECODE].bid * 100;
                        row.ASK = prices.Rates[rc.RATECODE].ask * 100;
                    }
                    list.Add(row);
                }
                return list;
            }
        }


        // new June, 10 99 T. Killilea
        // tweak individual ratecodes
        // ie yc.tweak("EDZ9",-1)  will lower rates by 1bp, (for futures it will raise price)

        public void Tweak(string strRATECODE, double dBPYldShift)
        {
            //RateCode rc = RateCode;
            RateCode rc = rateCodes[strRATECODE];
            if (rc != null)
            {
                if (rc.RateType == 2)
                {
                    rc.BPShift = (rc.BPShift - dBPYldShift);
                }
                else
                {
                    rc.BPShift = (rc.BPShift + dBPYldShift);
                }
            }
        }


        /// <summary>
        /// Term structure 
        /// </summary>
        /// <returns></returns>
        public dynamic GetPrices()
        {
            return (from r in this.rateCodes.Values
                    orderby r.CFEnd
                    select new 
                    {
                        RATECODE = r.RATECODE,
                        tkrDesc = r.Description,
                        period = r.Period,
                        vDate = r.CFStart,
                        mDate = r.CFEnd,
                        compPA = r.nCompoundPA,
                        basis = r.Basis,
                        bid = r.RateType == 2 ? prices.Rates[r.RATECODE].bid : prices.Rates[r.RATECODE].bid * 100,
                        ask = r.RateType == 2 ? prices.Rates[r.RATECODE].ask : prices.Rates[r.RATECODE].ask * 100
                    }).ToArray();
        }


        public DateTime spotDate()
        {
            return calendar.Workdays(ValueDate, spotDays);
        }


        /// <summary>
        /// rate interpolation of the curve 'ratecodes' as per date and indicated price (bid/ask/mid)
        /// </summary>
        /// <param name="_dateEval"></param>
        /// <param name="_iPrice"></param>
        /// <returns></returns>
        public double InterpRate(DateTime _dateEval, tkPrice _iPrice = tkPrice.tkASK)
        {
            int i = 0;
            int iROWCOUNT = 0;
            double dfNear;
            double dfFar;

            try
            {
                var x = rateCodes.Values.ToArray();  // convert the ratecodes dictionary.values to an array (it's already ordered)

                // true for offer, false for BID
                i = 0;
                iROWCOUNT = x.Length;

                // row bound

                // traverse the array of values going just PAST the evaldate
                while (x[i].CFEnd < _dateEval && i < iROWCOUNT)
                {
                    i++;
                }

                if (x[i - 1].CFEnd == _dateEval)  // here NO interp is needed!!
                {
                    // no interp needed...
                    if (_iPrice == tkPrice.tkBID)
                    {
                        // column #2
                        return  prices.Rates[x[i - 1].RATECODE].bid;
                    }
                    else if (_iPrice == tkPrice.tkASK)
                    {
                        // column #3  the offer
                        return prices.Rates[x[i - 1].RATECODE].ask;
                    }
                    else
                    {
                        // mid point
                        return (prices.Rates[x[i - 1].RATECODE].bid + prices.Rates[x[i - 1].RATECODE].ask) / 2;
                    }
                }
                else
                {
                    // here interpolation is needed between points...
                    if (_iPrice == tkPrice.tkBID)
                    {
                        dfNear = prices.Rates[x[i - 1].RATECODE].bid;
                        dfFar = prices.Rates[x[i].RATECODE].bid;
                    }
                    else if (_iPrice == tkPrice.tkASK)
                    {
                        dfNear = prices.Rates[x[i - 1].RATECODE].ask;
                        dfFar = prices.Rates[x[i].RATECODE].ask;
                    }
                    else
                    {
                        dfNear = (prices.Rates[x[i - 1].RATECODE].bid + prices.Rates[x[i - 1].RATECODE].ask) / 2;
                        dfFar = (prices.Rates[x[i].RATECODE].bid + prices.Rates[x[i].RATECODE].ask) / 2;

                    }
                    // this is then the interpolated
                    return Interp(x[i - 1].CFEnd, dfNear, x[i].CFEnd, dfFar, _dateEval);
                }
            }
            catch
            {
                // this is essential to the pricing model...
                return 1;
            }
        }



    }
}