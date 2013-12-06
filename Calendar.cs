using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CreditPricing
{

    /// <summary>
    /// Summary description for Calendar
    /// </summary>
    /// 
    
    public class Calendar
    {
        public DateTime TodayDate { get; set; }
        public string CCY { get; set; }
        public List<HolidayHDB> Holidays { get; set; }
        public tkDateCalcMode CalcMode;
        public string holidayCities { get; set; }
 
        public Dictionary<string, string> ccymap = new Dictionary<string, string>() {
               { "AUD", "Syndey" },
               { "NOK", "Oslo" },
               { "SEK", "Stockholm" },
               { "CHF", "Zurich" },
               { "JPY", "Tokyo" },
               { "SGD", "Singapore" },
               { "NZD", "Wellington" },
               { "CAD", "Toronto" },
               { "EUR", "Frankfurt" },
               { "GBP", "London" },
               { "HKD", "Hong Kong" },
               { "ZAR", "Johannesburg" },
               { "RUB", "Moscow" },
               { "DKK", "Copenhagen" },
               { "TRY", "Istanbul" },
               { "USD", "New York" }
        };

        public Calendar(List<string> _cities, DateTime _date)
        {
            TodayDate = _date; // _date.HasValue ? (DateTime)_date : DateTime.Today;
            CCY = "USD";
            CalcMode = tkDateCalcMode.tkModifiedFollowing;
            // convert the List<string> to a CSV

            holidayCities = string.Join<string>(", ", _cities);

            var db = new CreditPricingEntities();
            Holidays = (from h in db.HolidayHDBs
                        where _cities.Contains(h.City) // == city //where h.NSCODE == CCY.Substring(0, 2) // note: take out 5/30/2011  && h.NonSettleDate >= TodayDate
                        orderby h.Holiday // h.NonSettleDate
                        select h).ToList();

        }

        // standard contructor uses first two letters of CCY code
        public Calendar(string strCCY)
        {
            TodayDate = DateTime.Today; // _date.HasValue ? (DateTime)_date : DateTime.Today;
            CCY = strCCY;
            CalcMode = tkDateCalcMode.tkModifiedFollowing;
            holidayCities = cityfromCCY(strCCY);

            var db = new CreditPricingEntities();
            Holidays = (from h in db.HolidayHDBs
                        where h.City == holidayCities //where h.NSCODE == CCY.Substring(0, 2) // note: take out 5/30/2011  && h.NonSettleDate >= TodayDate
                            orderby h.Holiday // h.NonSettleDate
                             select h).ToList();
        }

        public Calendar(string strCCY, DateTime _evalDate)
        {
            TodayDate = _evalDate; // _date.HasValue ? (DateTime)_date : DateTime.Today;
            CCY = strCCY;
            CalcMode = tkDateCalcMode.tkModifiedFollowing;
            holidayCities = cityfromCCY(strCCY);
            var db = new CreditPricingEntities();
            Holidays = (from h in db.HolidayHDBs
                             where h.City == holidayCities // h.NSCODE == CCY.Substring(0, 2) // && h.NonSettleDate == TodayDate
                             orderby h.Holiday //.NonSettleDate
                             select h).ToList();
        }
        

        /// <summary>
        /// create calendar from multiple CCY codes, taking holidays per CCY
        /// </summary>
        /// <param name="fixingDate"></param>
        /// <param name="listCCY"></param>
        public Calendar(DateTime fixingDate, List<string> listCCY )
        {
            TodayDate = fixingDate;
            CalcMode = tkDateCalcMode.tkModifiedFollowing;

            // map list of CCYs to a list of cities (for holidays)
            List<string> cities = new List<string>();

            foreach (string c in listCCY)
            {
                cities.Add(cityfromCCY(c));
            }

            holidayCities = string.Join(", ", cities);  // create a CSV from our city list ie Frankfurt,New York
            CCY = listCCY[0]; // string.Join(",", listCCY.ToArray());  // create a CSV from our CCY list ie EUR,USD

            // load holidays
            var db = new CreditPricingEntities();
            Holidays = (from h in db.HolidayHDBs
                        where cities.Contains(h.City)
                        orderby h.Holiday
                        select h).ToList();
        }


        private string cityfromCCY(string _ccy)
        {
            if (ccymap.ContainsKey(_ccy))
            {
                return ccymap[_ccy];
            }
            else
            {
                return "[n/a]";
            }
        }

        // return a date that is iWorkdayCount days from dt using the
        // class calendar and holidays...
        public DateTime Workdays(DateTime dt, int iWorkDayCount)
        {
            {
                int iWorkDays = 0;
                int iDays = 0;

                while (iWorkDays < Math.Abs(iWorkDayCount))
                {
                    if (iWorkDayCount > 0)
                    {
                        iDays += 1;
                    }
                    else
                    {
                        iDays -= 1;
                    }
                    if (!(IsWeekEnd(dt.AddDays(iDays)) || IsHoliday(dt.AddDays(iDays))))
                    {
                        // = iCount + 1
                        iWorkDays += 1;
                    }
                }
                return dt.AddDays(iDays);
            }
        }


        // generic forward date generation
        //  return fardate a date
        public DateTime FarDate(DateTime dt, string sPeriod)
        {
            {
                int iperiodNum = 0;
                string sPer = null;
                // pick up the period ie "D", "M", "Y"
                sPer = sPeriod.Substring(sPeriod.Length - 1, 1).ToUpper(); // Strings.Right(Strings.UCase(sPeriod), 1);
                iperiodNum = int.Parse(sPeriod.Substring(0, sPeriod.Length - 1)); //, Strings.Len(sPeriod) - 1);

                switch (sPer)
                {
                    case "Y":
                        return FwdDate(dt, iperiodNum * 12);

                    case "M":
                        return FwdDate(dt, iperiodNum);

                    case "W":
                        return WorkDate(dt, iperiodNum * 7);

                    case "D":
                        return Workdays(dt, iperiodNum);

                    default:
                        return dt;
                }
            }
        }


        // forward date generator, return nmonths forward from StartDate
        // ie FwdDate(7/1/97,10)
        public DateTime FwdDate(DateTime StartDate, int nMonthsFwd)
        {
            {
                DateTime theDate = default(System.DateTime);
                //Dim targetyear As Integer
                int targetmonth = 0;
                DateTime dtMMEoM = default(System.DateTime);
                // the money market end of month date

                if (CalcMode == tkDateCalcMode.tkNone)
                {
                    // DateAdd(Microsoft.VisualBasic.DateInterval.Month, nMonthsFwd, StartDate)
                    return StartDate.AddMonths(nMonthsFwd);
                }


                // handle end of month  rolls ie end,end
                dtMMEoM = MM_EOMDate(StartDate);
                // MM_EndDate(StartDate)
                if (StartDate == dtMMEoM)
                {
                    //DateAdd(Microsoft.VisualBasic.DateInterval.Month, nMonthsFwd, StartDate))
                    theDate = MM_EOMDate(StartDate.AddMonths(nMonthsFwd));
                }
                else
                {
                    // DateAdd(Microsoft.VisualBasic.DateInterval.Month, nMonthsFwd, StartDate)
                    theDate = StartDate.AddMonths(nMonthsFwd);
                }

                targetmonth = theDate.Month;

                if (this.CalcMode == tkDateCalcMode.tkPreceding)
                {
                    while (IsWeekEnd(theDate) || IsHoliday(theDate))
                    {
                        //System.DateTime.FromOADate(theDate.ToOADate - 1)
                        theDate = theDate.AddDays(-1);
                    }
                    while (theDate.Month != targetmonth)
                    {
                        theDate = theDate.AddDays(1);
                        // System.DateTime.FromOADate(theDate.ToOADate + 1)
                        while ((IsWeekEnd(theDate) || IsHoliday(theDate)))
                        {
                            // System.DateTime.FromOADate(theDate.ToOADate + 1)
                            theDate = theDate.AddDays(1);
                        }
                    }
                    return theDate;

                }

                //targetyear = Year(theDate)
                // don't land on w/e or holiday
                while ((IsWeekEnd(theDate) || IsHoliday(theDate)))
                {
                    // System.DateTime.FromOADate(theDate.ToOADate + 1)
                    theDate = theDate.AddDays(1);
                }

                if (CalcMode == tkDateCalcMode.tkFollowing)
                {
                    return theDate;

                }

                // don't roll past the target month or target year
                while (theDate.Month != targetmonth)
                {
                    //Or Year(theDate) <> targetyear
                    theDate = theDate.AddDays(-1);
                    // System.DateTime.FromOADate(theDate.ToOADate - 1)
                    while ((IsWeekEnd(theDate) || IsHoliday(theDate)))
                    {
                        //System.DateTime.FromOADate(theDate.ToOADate - 1)
                        theDate = theDate.AddDays(-1);
                    }
                }
                // modified following
                return theDate;
            }
        }




        public bool IsWeekEnd(DateTime aDate)
        {
            //IsWeekEnd = False
            //If aDate.DayOfWeek = DayOfWeek.Saturday Or aDate.DayOfWeek = DayOfWeek.Sunday Then
            //    IsWeekEnd = True
            //End If
            return (aDate.DayOfWeek == DayOfWeek.Saturday) || (aDate.DayOfWeek == DayOfWeek.Sunday);
        }

        public bool IsHoliday(DateTime _date)
        {
            // find the date in the list of holidays  
            // if its there then we have a holiday
            // if it's not there we return a nothing
            return Holidays.Find(delegate(HolidayHDB h) { return h.Holiday == _date; }) != null; // fastest way to find object using generics...
        }


        //
        public DateTime WorkDate(DateTime dt, int iDaysFrom)
        {
            DateTime dtReturn = dt.AddDays(iDaysFrom);

            // don't land on w/e or holiday
            while (IsWeekEnd(dtReturn) || IsHoliday(dtReturn))
            {
                if (iDaysFrom > 0)
                {
                    // System.DateTime.FromOADate(dtReturn.ToOADate + 1)
                    dtReturn = dtReturn.AddDays(1);
                }
                else
                {
                    // System.DateTime.FromOADate(dtReturn.ToOADate - 1)
                    dtReturn = dtReturn.AddDays(-1);
                }
            }
            return dtReturn;
        }



        // return the end of month date including w/e or holidays
        public DateTime MM_EOMDate(DateTime dt)
        {
            System.DateTime dtTmp = dt.AddMonths(1);
            // add a month
            DateTime dtReturn = new DateTime(dtTmp.Year, dtTmp.Month, 1);
            // first day of next month
            dtReturn = dtReturn.AddDays(-1);
            while ((IsWeekEnd(dtReturn) || IsHoliday(dtReturn)))
            {
                //System.DateTime.FromOADate(dtReturn.ToOADate - 1)
                dtReturn = dtReturn.AddDays(-1);
            }
            return dtReturn;
        }

        // same as above???
        //Public Function MM_EndDate(ByRef dt As Date) As Date
        //    Dim dtTmp As Date = dt.AddMonths(1)
        //    Dim dtReturn As New DateTime(dtTmp.Year, dtTmp.Month, 1) ' first day of next month
        //    dtReturn.AddDays(-1)  ' go back a day

        //    Do While (IsWeekEnd(dtReturn) Or IsHoliday(dtReturn))
        //        dtReturn = dtReturn.AddDays(-1) ' System.DateTime.FromOADate(dtReturn.ToOADate - 1)
        //    Loop
        //    Return dtReturn
        //End Function

        // return first of month date
        public System.DateTime MM_FOMDate(ref System.DateTime dt)
        {
            System.DateTime dtReturn = new System.DateTime(dt.Year, dt.Month, 1);
            while ((IsWeekEnd(dtReturn) | IsHoliday(dtReturn)))
            {
                dtReturn = dtReturn.AddDays(1);
            }
            return dtReturn;
        }


        // here an algoritm should be used but I couldn't think of a better one... 
        public System.DateTime ThirdWed(System.DateTime dt)
        {
            short iDayShift = 0;
            // first of month
            System.DateTime dtFOM = new System.DateTime(dt.Year, dt.Month, 1);
            switch (dtFOM.DayOfWeek)
            {
                case DayOfWeek.Sunday:
                    iDayShift = 3;
                    break;
                case DayOfWeek.Monday:
                    iDayShift = 2;
                    break;
                case DayOfWeek.Tuesday:
                    iDayShift = 1;
                    break;
                case DayOfWeek.Wednesday:
                    iDayShift = 0;
                    break;
                case DayOfWeek.Thursday:
                    iDayShift = 6;
                    break;
                case DayOfWeek.Friday:
                    iDayShift = 5;
                    break;
                case System.DayOfWeek.Saturday:
                    iDayShift = 4;
                    break;
            }

            //Select Case Weekday(dtFOM) '.DayOfWeek)
            //    Case 1
            //        iDayShift = 3
            //    Case 2
            //        iDayShift = 2
            //    Case 3
            //        iDayShift = 1
            //    Case 4
            //        iDayShift = 0
            //    Case 5
            //        iDayShift = 6
            //    Case 6
            //        iDayShift = 5
            //    Case 7
            //        iDayShift = 4
            //End Select
            // System.DateTime.FromOADate(dtFOM.ToOADate + iDayShift + 14)
            return dtFOM.AddDays(iDayShift + 14);
        }

        // utility function return the next standard index roll dates
        // index roll dates are on the 20th of Mar,Jun,Sep,Dec
        // CDX, ITRAX and standardized tranches and all single name CDS will use these rolls... 
        public DateTime NextINDEXRollFrom(DateTime dtEval)
        {
            int iRem = 0;
            // remainder
            int iNum = dtEval.Month;
            // numerator
            int iDen = 3;
            // denominator
            int iQuotient = 0;
            // "it is the mystery of the quotient..."  Led Zeppelin

            iQuotient = System.Math.DivRem(iNum, iDen, out iRem);
            if (iRem == 0)
            {
                // either month 3,6,9,12
                if (dtEval.Day < 20)
                {
                    // current 20th
                    return new System.DateTime(dtEval.Year, dtEval.Month, 20);
                }
                else
                {
                    // current 20th +3 months
                    return new System.DateTime(dtEval.Year, dtEval.Month, 20).AddMonths(3);
                }
            }
            else
            {
                // current 20th + less then 3
                return new System.DateTime(dtEval.Year, dtEval.Month, 20).AddMonths(3 - iRem);
            }
        }

        // return the next IMM date from a given date
        public DateTime NextIMMFrom(DateTime dt)
        {
            System.DateTime dtThirdWed = default(System.DateTime);
            short iMonthAdd = 0;

            dtThirdWed = ThirdWed(dt);
            switch (dt.Month)
            {
                case 1:
                    iMonthAdd = 2;
                    break;
                case 2:
                    iMonthAdd = 1;
                    break;
                case 3:
                    iMonthAdd = 0;
                    break;
                case 4:
                    iMonthAdd = 2;
                    break;
                case 5:
                    iMonthAdd = 1;
                    break;
                case 6:
                    iMonthAdd = 0;
                    break;
                case 7:
                    iMonthAdd = 2;
                    break;
                case 8:
                    iMonthAdd = 1;
                    break;
                case 9:
                    iMonthAdd = 0;
                    break;
                case 10:
                    iMonthAdd = 2;
                    break;
                case 11:
                    iMonthAdd = 1;
                    break;
                case 12:
                    iMonthAdd = 0;
                    break;
            }
            if (iMonthAdd == 0)
            {
                if (dt >= dtThirdWed)
                {
                    iMonthAdd = 3;
                }
            }
            //DateAdd(Microsoft.VisualBasic.DateInterval.Month, iMonthAdd, dt))
            return ThirdWed(dt.AddMonths(iMonthAdd));
        }


        // standard ISDA 30/360 day count or european
        public double Days360(DateTime dtStart, DateTime dtEnd, tkDayConvention dtConv = tkDayConvention.tk30360ISDA)
        {
            int iDayStart = 0;
            int iDayEnd = 0;

            iDayStart = dtStart.Day == 31 ? 30 : dtStart.Day;
            switch (dtConv)
            {
                case tkDayConvention.tk30360ISDA:
                    iDayEnd = (dtEnd.Day == 31 & iDayStart == 30 ? 30 : dtEnd.Day);
                    // both dates must be 30...
                    break;
                case tkDayConvention.tk30360E:
                    iDayEnd = (dtEnd.Day == 31 ? 30 : dtEnd.Day);
                    // this is all for 30/360E  only compare second day in isolation
                    break;
                case tkDayConvention.tk30360PSA:
                    // to do code PSAA rule
                    iDayEnd = (dtEnd.Day == 31 & iDayStart == 30 ? 30 : dtEnd.Day);
                    // both dates must be 30...
                    break;
                // to do code SIA rule
                case tkDayConvention.tk30360SIA:
                    iDayEnd = (dtEnd.Day == 31 & iDayStart == 30 ? 30 : dtEnd.Day);
                    // both dates must be 30...
                    break;
                // error to this standard ISDA... this should never fall through
                default:
                    iDayEnd = (dtEnd.Day == 31 & iDayStart == 30 ? 30 : dtEnd.Day);
                    // both dates must be 30...
                    break;
            }
            return 360 * (dtEnd.Year - dtStart.Year) + 30 * (dtEnd.Month - dtStart.Month) + (iDayEnd - iDayStart);
        }

        public double DayCountFrac(string _basis, DateTime _dtStart, DateTime _dtEnd)
        {
            switch (_basis.Trim())
            {
                case "A360":
                    return (double)_dtEnd.Subtract(_dtStart).Days / 360;
                case "A365":
                case "ACT/ACT":
                    return (double)_dtEnd.Subtract(_dtStart).Days / 365;
                case "30/360":
                case "30F360":
                case "BOND":
                    // this is ISDA convention
                    return (double)this.Days360(_dtStart, _dtEnd, tkDayConvention.tk30360ISDA) / 360;
                case "30E360":
                case "EBOND":
                    // this is european convention
                    return (double)this.Days360(_dtStart, _dtEnd, tkDayConvention.tk30360E) / 360;
                default:
                    // default to this...
                    return (double)_dtEnd.Subtract(_dtStart).Days / 365;
            }
        }
    }
}