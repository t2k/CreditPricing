using System;
/// <summary>
/// tkRateCode: proprietary pillar definition for Cashrate, Futures and SwapRates
/// </summary>
/// 
namespace CreditPricing
{

    public partial class RATECODE { 

    }

    public partial class RateCode
    {
        // NEW 5/26/99  implement m_BPShift property
        // this allows each instance of a ratecode to have its price shifted

        //local variable(s) to hold property value(s)

        private string m_RATECODE;
        //local copy
        private string m_ccy;
        //local copy
        private string m_Period;
        //local copy
        private string m_Basis;
        //local copy
        private int m_nDays2Start;
        //local copy
        private int m_nCompoundPA;
        //local copy
        private int m_RateType;
        //local copy
        private string m_CFPeriodType;
        //local copy
        private DateTime m_CFStart;
        //local copy
        private DateTime m_CFEnd;
        //local copy
        private DateTime m_CFSensiEnd;
        // local copy
        private double m_BPShift;

        public string Description { get; set; }



        public RateCode(RATECODE row)
        {
            m_RATECODE = row.RATECODE1;
            m_ccy = row.CCY;
            m_Period = row.Period;
            m_Basis = row.Basis;
            m_nDays2Start = (short)row.nDay2Start;
            m_nCompoundPA = (short)row.nCompoundPA;
            m_RateType = (short)row.RateType;
            m_CFPeriodType = CFPeriodType;
            m_CFStart = (DateTime)row.CF_StartDate;
            m_CFEnd = (DateTime)row.CF_EndDate;
            m_CFSensiEnd = (DateTime)row.CF_SensiEndDate;
            m_BPShift = 0;
            Description = row.Description;

        }


        // NEW JAN 09: Create a SPOT STUB ratecode on the fly (ONLY used in HDB CYldCurve constructor)
        public RateCode(string _RateCode, string _CCY, DateTime _PeriodStart, DateTime _PeriodEnd)
        {
            m_RATECODE = _RateCode;
            m_ccy = _CCY;
            m_Period = "2D";
            m_Basis = "A360";
            m_nDays2Start = 0;
            m_nCompoundPA = 1;
            m_RateType = 1;
            m_CFPeriodType = "R";
            m_CFStart = _PeriodStart;
            m_CFEnd = _PeriodEnd;
            m_CFSensiEnd = _PeriodEnd;
            m_BPShift = 0;
        }




        public RateCode() { }


        //new Dec 08--  load ratecodes from the BSS table of the HDB database
        // NEW! Nov/2011,  m_CFStart = _cal.Workdays(_cal.TodayDate, m_nDays2Start);  // defautl T+2 business days, count holidays/weekend days between
        // changed from using _cal.WorkDate()  was causing issues around holiday
        /// <summary>
        /// default RateCode consturctor for HDB BSSRow
        /// </summary>
        /// <param name="_row">row object to translate fields</param>
        /// <param name="_cal">calendar object passed </param>
        public RateCode(string _symbol, Calendar _cal)
        {
            {
                m_RATECODE = _symbol;
                Description = "HDB Symbol " + m_RATECODE;
                m_ccy = _cal.CCY;
                m_Period = _symbol.Substring(_symbol.Length - 3, 3);
                m_Basis = "A360";
                m_nDays2Start = 2;
                m_nCompoundPA = 1;
                m_RateType = 1;
                m_CFPeriodType = "R";
                m_CFStart = _cal.Workdays(_cal.TodayDate, m_nDays2Start);  // defautl T+2 business days, count holidays/weekend days between
                // spot start 
                m_CFEnd = _cal.FarDate(m_CFStart, m_Period);
                m_CFSensiEnd = m_CFEnd;
                m_BPShift = 0;
            }
        }
        
        
        //// old school OOP stuff from WAY BACK, still works though
        //public void WriteFileData(ref CFile file)
        //{
        //    {
        //        file.WriteStr(m_RATECODE);
        //        file.WriteStr(m_ccy);
        //        file.WriteStr(m_Period);
        //        file.WriteStr(m_Basis);
        //        file.WriteInt(m_nDays2Start);
        //        file.WriteInt(m_nCompoundPA);
        //        file.WriteInt(m_RateType);
        //        file.WriteStr(m_CFPeriodType);
        //        file.WriteDate(m_CFStart);
        //        file.WriteDate(m_CFEnd);
        //        file.WriteDate(m_CFSensiEnd);
        //        file.WriteInt(m_BPShift);
        //    }
        //}

        //public void ReadFileData(ref CFile file)
        //{
        //    System.DateTime dt = default(System.DateTime);
        //    string str_Renamed = "";
        //    short i = 0;

        //    try
        //    {
        //        {
        //            file.ReadStr(str_Renamed);
        //            m_RATECODE = str_Renamed;
        //            file.ReadStr(str_Renamed);
        //            m_ccy = str_Renamed;
        //            file.ReadStr(str_Renamed);
        //            m_Period = str_Renamed;
        //            file.ReadStr(str_Renamed);
        //            m_Basis = str_Renamed;
        //            file.ReadInt(i);
        //            m_nDays2Start = i;
        //            file.ReadInt(i);
        //            m_nCompoundPA = i;
        //            file.ReadInt(i);
        //            m_RateType = i;
        //            file.ReadStr(str_Renamed);
        //            m_CFPeriodType = str_Renamed;
        //            file.ReadDate(dt);
        //            m_CFStart = dt;
        //            file.ReadDate(dt);
        //            m_CFEnd = dt;
        //            file.ReadDate(dt);
        //            m_CFSensiEnd = dt;
        //            file.ReadInt(i);
        //            m_BPShift = i;

        //        }
        //    }
        //    catch (Exception ex)
        //    {


        //    }
        //}


        public string FutContract
        {
            get
            {
                if (m_RateType == 2)
                {
                    return m_RATECODE.Substring(0, m_RATECODE.Length - 1); // Strings.Left(m_RATECODE, Strings.Len(m_RATECODE) - 2);
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        // end of privat members....
        public double BPShift
        {
            get { return m_BPShift; }
            set { m_BPShift = value; }
        }



        public System.DateTime CFEnd
        {
            get { return m_CFEnd; }
            set { m_CFEnd = value; }
        }




        public System.DateTime CFSensiEnd
        {
            get { return m_CFSensiEnd; }
            set { m_CFSensiEnd = value; }
        }




        public System.DateTime CFStart
        {
            get { return m_CFStart; }
            set { m_CFStart = value; }
        }


        public string CFPeriodType
        {
            get { return m_CFPeriodType; }
            set { m_CFPeriodType = value; }
        }


        public int RateType
        {
            get { return m_RateType; }
            set { m_RateType = value; }
        }


        public int nCompoundPA
        {
            get { return m_nCompoundPA; }
            set { m_nCompoundPA = value; }
        }



        public int nDays2Start
        {
            get { return m_nDays2Start; }
            set { m_nDays2Start = value; }
        }




        public string Basis
        {
            get { return m_Basis; }
            set { m_Basis = value; }
        }


        public string Period
        {
            get { return m_Period; }
            set { m_Period = value; }
        }

        public string CCY
        {
            get { return m_ccy; }
            set { m_ccy = value; }
        }


        public string RATECODE
        {
            get { return m_RATECODE; }
            set { m_RATECODE = value; }
        }

        public double DayCountFrac
        {
            get
            {
                switch (m_Basis.Trim())
                {

                    case "A360":
                        return (double)m_CFEnd.Subtract(m_CFStart).Days / 360;
                        //break;
                    case "A365":
                    case "ACT/ACT":
                        return (double)m_CFEnd.Subtract(m_CFStart).Days / 365;
                        //break;
                    case "30/360":
                    case "30F360":
                    case "BOND":
                        // 30F360 is FIXED coupon
                        return (double)this.Days360(m_CFStart, m_CFEnd) / 360;
                        //break;
                    //case "BOND":
                    //    DayCountFrac = DateDiff(Microsoft.VisualBasic.DateInterval.Month, m_CFStart, m_CFEnd) / 12;
                    //    break;
                    case "30E360":
                    case "EBOND":
                        return (double)this.Days360E(m_CFStart, m_CFEnd) / 360;
                        //break;
                    default:
                        // default to this...
                        return (double)m_CFEnd.Subtract(m_CFStart).Days / 365;
                        //break;
                }
            }
        }


        public double CpnDayCountFrac(DateTime dtStart, DateTime dtEnd)
        {
            
            {
                switch (m_Basis.Trim())
                {
                    case "A360":
                        return (double)dtEnd.Subtract(dtStart).Days / 360;
                       
                    case "30/360":
                    case "30F360":
                    case "BOND":
                        return (double)Days360(dtStart, dtEnd) / 360;
                        
                    //case "BOND":
                    //    CpnDayCountFrac = DateDiff(Microsoft.VisualBasic.DateInterval.Month, dtStart, dtEnd) / 12;
                    //    break;
                    case "A365":
                    case "ACT/ACT":
                        return (double)dtEnd.Subtract(dtStart).Days / 365;
                        
                    case "30E360":
                    case "EBOND":
                        return (double)this.Days360E(dtStart, dtEnd) / 360;
                        
                    default:
                        // default to this...
                        return (double)dtEnd.Subtract(dtStart).Days / 365;
                }
            }
        }


        // return a new object that is a copy of self
        public RateCode Copy
        {
            get
            {
                RateCode tmpRC = new RateCode();
                {
                    tmpRC.Basis = Basis;
                    tmpRC.BPShift = BPShift;
                    tmpRC.CCY = CCY;
                    tmpRC.CFEnd = CFEnd;
                    tmpRC.CFPeriodType = CFPeriodType;
                    tmpRC.CFStart = CFStart;
                    tmpRC.nCompoundPA = nCompoundPA;
                    tmpRC.nDays2Start = nDays2Start;
                    tmpRC.Period = Period;
                    tmpRC.RATECODE = RATECODE;
                    tmpRC.RateType = RateType;
                    tmpRC.Description = Description;
                }
                return tmpRC;
            }
        }


        // standard ISDA 30/360 day count
        public double Days360(DateTime dtStart, DateTime dtEnd)
        {
            int iDayStart = 0;
            int iDayEnd = 0;

            iDayStart = dtStart.Day == 31 ? 30 : dtStart.Day;
            iDayEnd = (dtEnd.Day == 31 && iDayStart == 30 ? 30 : dtEnd.Day);

            // 30/360E method
            //iDayEnd = IIf(Day(dtEnd) = 31, 30, Day(dtEnd))  ' this is all for 30/360E  only compare second day in isolation

            return 360 * (dtEnd.Year - dtStart.Year) + 30 * (dtEnd.Month - dtStart.Month) + (iDayEnd - iDayStart);
        }

        // standard ISDA 30/360 day count
        public double Days360E(DateTime dtStart, DateTime dtEnd)
        {
            int iDayStart = 0;
            int iDayEnd = 0;

            iDayStart = dtStart.Day == 31 ? 30 : dtStart.Day;
            iDayEnd = dtEnd.Day == 31 ? 30 : dtEnd.Day;

            // this is all for 30/360E  only compare second day in isolation
            return 360 * (dtEnd.Year - dtStart.Year) + 30 * (dtEnd.Month - dtStart.Month) + (iDayEnd - iDayStart);
        }

    }
}