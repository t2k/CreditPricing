using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;


namespace CreditPricing
{
    /// <summary>
    /// mxCDSCF  a Murex CDS cashflow record
    /// CDS CASHFLOW OBJECT... also has a strongly typed collection class associated
    /// </summary>
    /// <remarks>
    /// author: T Killilea
    /// </remarks>
    public class mxCDSCF
    {
        private string m_NB;        // mx Deal # for this flow
        private DateTime m_SDate;        // cashflow start date
        private DateTime m_EDate;        // cashflow end date
        private DateTime m_PmtDate;        // cashflow payment date
        private double m_NAmt;        // notl amount
        private string m_Index;        // index    Private m_dSprd As Double ' deal spread 
        private double m_dSprd;        // deal spread 
        private double m_eSprd;        // evaluation Sprd or mtm spread
        private double m_df;        // discount factor 
        private double m_sProb;        //survival probability
        private double m_MDP;        // marginal default probablity
        private string m_ccy;        // ccy code
        private double m_baserate;        // base index rate or fixed rate
        private double m_prinPmt;        // prin. payment (for cash bonds)
        private string m_basis;        //  basis ie A360/30E360 etc...
        private double m_FwdRate;        // used for projected cashflows 
        private bool m_IsStub;
        private double m_CPR;
        private double m_Factor;
        private double m_DMFlow;

        public mxCDSCF() { }

        /// <summary>
        /// Cashflow constructor for a OPICSBond// opics system no longer used,,, this can be deprecated eventually
        /// </summary>
        /// <param name="row">OPICSBond</param>
        /// <param name="yc">YldCurve</param>
        /// <param name="dtSettle">Settle Date</param>
        public mxCDSCF(OPICSCashFlow row, YldCurve yc, DateTime dtSettle, double _FaceAmt)
        {
            m_baserate = (double)row.INTRATE_8 / 100;

            //CDbl(rs("INTRATE_8").Value) / 100  ' ie 3M LIBOR AS SET OR A FIXED RATE on a FIXED bond
            m_ccy = row.CCY;
            m_NB = row.DEALID;
            m_dSprd = (double)row.SPREAD_8 / 100;
            m_eSprd = 0;
            m_SDate = (DateTime)row.INTSTRTDTE;
            m_EDate = (DateTime)row.INTENDDTE;
            m_NAmt = (double)row.PRINAMT;
            m_Index = row.RATECODE;
            m_prinPmt = (double)row.PPAYAMT;
            m_sProb = (double)1;
            // not really used for BONDCF purposes...
            m_basis = row.BASIS.Trim();
            m_Factor = (double)row.PRINAMT / _FaceAmt;  // note orig face is from another table

            try
            {
                m_CPR = (double)row.PPAYAMT / _FaceAmt * (365 / (double)row.INTENDDTE.Value.Subtract(row.INTSTRTDTE.Value).Days);
            }
            catch
            {
            }

            if (dtSettle >= row.INTSTRTDTE && dtSettle < row.INTENDDTE)
            {
                m_IsStub = true;
            }
            else
            {
                m_IsStub = false;
            }

            try
            {
                // see if the yc object is loaded
                m_df = yc.GetDF2(m_EDate, dtSettle);
                m_FwdRate = yc.FwdRate(m_SDate, m_EDate);
            }
            catch
            {
                m_df = 1;
                m_FwdRate = 0;
            }
        }


        // new June 16 2006: for ASSET SWAP CASHFLOWS, convert FIXED (given) flows to floating A360 quarterly...
        // ASSET SWAP CASHFLOWS:  disregard fixed flows (it's a PAR asset swap) dynamically generate a 3M floating sched assume rolling over end-date...
        // dynamically creating cashflows for ASSET SWAPS... converting FIXED flows to 3MLIBOR on the fly...
        /// <summary>
        /// CashFlow constructor for ASSET SWAPS:
        /// quick generation of 3M floating CF's from EndDate...
        /// not exact but close enough for pricing...
        /// </summary>
        /// <param name="bond"></param>
        /// <param name="cfStart"></param>
        /// <param name="cfEnd"></param>
        /// <param name="yc"></param>
        public mxCDSCF(OPICSBond bond, DateTime cfStart, DateTime cfEnd, YldCurve yc)
        {
            m_baserate = bond.BaseRate;
            //0 'CDbl(row.INTRATE_8 / 100) 'CDbl(rs("INTRATE_8").Value) / 100  ' ie 3M LIBOR AS SET OR A FIXED RATE on a FIXED bond
            m_ccy = bond.CCY;
            m_NB = bond.DealID;
            m_dSprd = bond.AswSprd;
            // CDbl(row.SPREAD_8 / 100)
            m_eSprd = bond.AswSprd;
            m_SDate = cfStart;
            m_EDate = cfEnd;
            m_NAmt = bond.OutAmt;
            m_Index = "LIBOR-ASWP";
            m_basis = "A360";
            // remember this is an ASSET SWAP 
            m_sProb = (double)1;

            // not really used for BONDCF purposes...

            if (cfEnd == bond.MaturityDate)
            {
                m_prinPmt = bond.OutAmt;
            }
            else
            {
                m_prinPmt = 0;
            }

            // stub principal cashflow record 
            if (cfStart == cfEnd)
            {
                // this only happens on principal flow date...
                m_prinPmt = -bond.OutAmt;
            }

            // if cashflow enddate is before eval date then just assing 1 and no fwdrate
            if (m_EDate < bond.EvalDate)
            {
                m_df = 1;
                m_FwdRate = 0;
            }
            else
            {

                m_df = yc.GetDF2(m_EDate, bond.EvalDate);

                // do you follow this?  front stub rate must only go from EvalDate to cfEnd...
                if ((m_SDate < bond.EvalDate) & (bond.EvalDate < m_EDate))
                {
                    m_FwdRate = yc.FwdRate(bond.EvalDate, m_EDate);
                }
                else
                {
                    m_FwdRate = yc.FwdRate(m_SDate, m_EDate);
                }

            }
        }

        // CONSTRUCTOR to create a mxCDSCF object by passing values in directs (CDS) format
        public mxCDSCF(string sNB, string strCCY, DateTime dtSDate, DateTime dtEDate, double dAmt, double dDSPrd, double dVSprd, double dDF, double dSProb)
        {
            m_NB = sNB;
            m_SDate = dtSDate;
            m_EDate = dtEDate;
            m_NAmt = dAmt;
            m_dSprd = dDSPrd;
            // deal spread (Fixed @ contract date)
            m_eSprd = dVSprd;
            // valuation spread
            m_df = dDF;
            m_sProb = dSProb;
            m_basis = "A360";
            m_ccy = strCCY;
        }

        public bool IsStub
        {
            get { return m_IsStub; }
            set { m_IsStub = value; }
        }

        public double ProjectedCoupon
        {
            get
            {
                if (m_baserate == 0)
                {
                    return m_FwdRate + m_dSprd;
                }
                else
                {
                    return m_baserate + m_dSprd;
                }
            }
        }

        public double DMFlatCoupon(double flatIndex)
        {
            return flatIndex + m_dSprd;
        }

        public double Coupon
        {
            get { return m_baserate + m_dSprd; }
        }

        public double FwdRate
        {
            get { return m_FwdRate; }
            set { m_FwdRate = value; }
        }

        public string Basis
        {
            get { return m_basis; }
            set { m_basis = value; }
        }

        public double PrinPayment
        {
            get { return m_prinPmt; }
            set { m_prinPmt = value; }
        }

        public string CCY
        {
            get { return m_ccy; }
            set { m_ccy = value; }
        }

        public string MktIndex
        {
            get { return m_Index; }
            set { m_Index = value; }
        }

        public double BaseRate
        {
            get { return m_baserate; }
            set { m_baserate = value; }
        }

        public double BondCF
        {
            get { return m_prinPmt + m_NAmt * ProjectedCoupon * DayCountFrac(m_basis, m_SDate, m_EDate); }
        }

        public double BondCFPV
        {
            get { return BondCF * m_df; }
        }


        //  simple A360 calculation for now...  a CDS calculation
        public double NetCDSCFPremium
        {
            // this is the individual cashflow calc ofr CDS... Notl x spread x dys/360
            get { return m_NAmt * ((m_dSprd - m_eSprd) / 100) * DayCountFrac(m_basis, m_SDate, m_EDate); }
        }


        public double cdspvBPV
        {
            get { return BPV * DF * pSurv; }
        }

        public double BPV
        {
            get { return -m_NAmt * DayCountFrac(m_basis, m_SDate, m_EDate) * (double)0.0001; }
        }


        // MUREX NUMBER
        public string NB
        {
            get { return m_NB; }
            set { m_NB = value; }
        }

        //cf start date
        public DateTime CFStart
        {
            get { return m_SDate; }
            set { m_SDate = value; }
        }

        //day count
        public int Dys
        {
            get { return CFEnd.Subtract(CFStart).Days; }
        }

        //cf end date
        public DateTime CFEnd
        {
            get { return m_EDate; }
            set { m_EDate = value; }
        }

        //notional amt
        public double NAmt
        {
            get { return m_NAmt; }
            set { m_NAmt = value; }
        }

        //dealspred for CF
        public double dSpread
        {
            get { return m_dSprd; }
            set { m_dSprd = value; }
        }

        //evaluation spread
        public double eSpread
        {
            get { return m_eSprd; }
            set { m_eSprd = value; }
        }

        //discount factor
        public double DF
        {
            get { return m_df; }
            set { m_df = value; }
        }

        public double rnpCLP
        {
            get { return (1 - m_sProb); }
        }

        // new added 5/9/2007 to store the Marginal Default Probabilty on a collection basis its (1- m_sProb(i)) - (1-msProb(i-1))
        public double MDP
        {
            get { return m_MDP; }
            set { m_MDP = value; }
        }
        //survival probability


        public double pSurv
        {
            get { return m_sProb; }

            set { m_sProb = value; }
        }

        // read only property
        public double cdsPVCF
        {
            get { return NetCDSCFPremium * DF * pSurv; }
        }


        public double Days360(DateTime dtStart, DateTime dtEnd, tkDayConvention dtConv = tkDayConvention.tk30360ISDA)
        {
            int iDayStart = 0;
            int iDayEnd = 0;

            iDayStart = dtStart.Day == 31 ? 30 : dtStart.Day;
            switch (dtConv)
            {
                case tkDayConvention.tk30360ISDA:
                    iDayEnd = (dtEnd.Day == 31 && iDayStart == 30) ? 30 : dtEnd.Day;
                    break;
                case tkDayConvention.tk30360E:
                    iDayEnd = (dtEnd.Day == 31) ? 30 : dtEnd.Day;
                    // this is all for 30/360E  only compare second day in isolation
                    break;
                case tkDayConvention.tk30360PSA:
                    // to do code PSAA rule
                    iDayEnd = dtEnd.Day == 31 && iDayStart == 30 ? 30 : dtEnd.Day;
                    // both dates must be 30...
                    break;
                // to do code SIA rule
                case tkDayConvention.tk30360SIA:
                    iDayEnd = dtEnd.Day == 31 && iDayStart == 30 ? 30 : dtEnd.Day;
                    // both dates must be 30...
                    break;
                // error to this standard ISDA... this should never fall through
                default:
                    iDayEnd = dtEnd.Day == 31 && iDayStart == 30 ? 30 : dtEnd.Day;
                    // both dates must be 30...
                    break;

            }

            // return 360 * (Year(dtEnd) - Year(dtStart)) + 30 * (Month(dtEnd) - Month(dtStart)) + (iDayEnd - iDayStart);
            return 360 * (dtEnd.Year - dtStart.Year) + 30 * (dtEnd.Month - dtStart.Month) + (iDayEnd - iDayStart);
        }

        public double DayCountFrac(string str, DateTime dtStart, DateTime dtEnd)
        {
            double functionReturnValue = 0;
            switch (str)
            {
                case "A360":
                    functionReturnValue = (double)dtEnd.Subtract(dtStart).Days / 360;
                    break;
                case "A365":   // note fall thru
                case "ACT/ACT": // note fall thru
                    functionReturnValue = (double)dtEnd.Subtract(dtStart).Days / 365;
                    break;
                case "30/360":
                case "BOND":
                    // this is ISDA convention
                    functionReturnValue = this.Days360(dtStart, dtEnd, tkDayConvention.tk30360ISDA) / 360;

                    break;
                case "EBOND":
                case "30E360":
                case "30F360":
                    // this is european convention
                    functionReturnValue = this.Days360(dtStart, dtEnd, tkDayConvention.tk30360E) / 360;

                    break;
                default:
                    // default to this...
                    functionReturnValue = (double)dtEnd.Subtract(dtStart).Days / 365;
                    break;
            }
            return functionReturnValue;
        }

        public double CPR
        {
            get { return m_CPR; }
            set { m_CPR = value; }
        }


        public DateTime CFPayDate
        {
            get { return m_PmtDate; }
            set { m_PmtDate = value; }
        }

        public double Factor
        {
            get { return m_Factor; }
            set { m_Factor = value; }
        }


        public double DMFlow
        {
            get { return m_DMFlow; }
            set { m_DMFlow = value; }
        }

    }
}