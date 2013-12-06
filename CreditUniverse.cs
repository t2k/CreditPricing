using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CreditPricing
{
    public struct cuTally
    {
        public DateTime date { get; set; }
        public string name { get; set; }
        public string refEntity { get; set; }
        public string ticker { get; set; }
        public string ccy { get; set; }
        public string tier { get; set; }
        public int countPort { get; set; }
        public double cdsSpread { get; set; }
    }

    /// <summary>
    /// Summary description for CreditUniverse
    /// </summary>
    // **** DICTIONARY COLLECTION OF mxCDSCollection objects *****
    public class CreditUniverse : Dictionary<string, tkCDSList> // DictionaryBase
    {
        // a strongly typed Dictionary/collection class to hold tkCDSList Objects...

        // class members
        public DateTime EvalDate { get; set; }
        private Dictionary<string, tkCDS> m_UniqueCredits = new Dictionary<string, tkCDS>();
        public string Name { get; set; }


        public int CountTranches
        {
            get { return base.Count; }
        }

        // fill my collection class from
        public CreditUniverse(string _CreditUniverseName, DateTime _Date)
            : base()
        {
            //ByVal DTABSEvalCurves As dsCreditNet.ABSEvalCurveDataTable)
            // load from query
            EvalDate = _Date;
            Name = _CreditUniverseName;
            var db = new CreditPricingEntities();
            var plist = (from p in db.CrUniverseLists
                         where p.CreditUniverseName == Name
                         select p).ToList();

            foreach (var p in plist) //dsSimulation.PortfolioListsRow row in dt.Rows)
            {
                this.Add(p.PortfolioName, new tkCDSList(p.PortfolioName, EvalDate));
            }
            LoadUniqueCredits();
        }


        public int CountAllCredits
        {
            get
            {
                int iCount = 0;
                foreach (tkCDSList port in this.Values)
                {
                    iCount += port.Count;
                }
                return iCount;
            }
        }


        public int CountUniqueCredits
        {
            get { return m_UniqueCredits.Count; }
        }

        public struct cuSummary
        {
            public DateTime CreditUniverseDate { get; set; }
            public string CreditUniverse { get; set; }
            public int CountTranches { get; set; }
            public int CountAllCredits { get; set; }
            public int CountUniqueCredits { get; set; }
        }

        public List<cuSummary> Summary() // dsOUT.CreditUniverseSummaryDataTable Summary()
        {
            List<cuSummary> list = new List<cuSummary>();
            cuSummary row = new cuSummary();

            //dsOUT.CreditUniverseSummaryDataTable dt = new dsOUT.CreditUniverseSummaryDataTable();
            //dsOUT.CreditUniverseSummaryRow row = dt.NewCreditUniverseSummaryRow;
            {
                row.CreditUniverseDate = EvalDate;
                row.CreditUniverse = Name;
                row.CountTranches = this.CountTranches;
                row.CountAllCredits = this.CountAllCredits;
                row.CountUniqueCredits = this.CountUniqueCredits;
            }
            list.Add(row);

            // dt.AddCreditUniverseSummaryRow(row);
            return list; // dt;
        }


        public List<cuTally> TallyUniqueCredits()
        {
            List<cuTally> list = new List<cuTally>();
            // dsCreditNet.CreditUniverseTallyDataTable dt = new dsCreditNet.CreditUniverseTallyDataTable();
            //foreach (KeyValuePair<string, mxCDS> kvp in m_UniqueCredits)
            foreach (tkCDS cds in m_UniqueCredits.Values)
            {
                cuTally row = new cuTally();
                {
                    row.date = EvalDate;
                    row.name = Name;
                    row.refEntity = cds.RefEntity;
                    row.ticker = cds.Ticker;
                    row.ccy = cds.CCY;
                    row.tier = cds.SENIORITY;
                    row.countPort = this.InPortfolios(cds.CurveName);
                    row.cdsSpread = cds.EvalSpread * 100;
                }
                list.Add(row);
            }
            return list;
        }

        private int InPortfolios(string _CreditKey)
        {
            int iPorts = 0;
            foreach (tkCDSList port in this.Values)
            {
                foreach (tkCDS cds in port)
                {
                    if (cds.CurveName == _CreditKey)
                    {
                        iPorts += 1;
                        break; 
                    }
                }
            }
            return iPorts;
        }

        private void LoadUniqueCredits()
        {
            foreach (tkCDSList port in this.Values)
            {
                foreach (tkCDS cds in port)
                {
                    try
                    {
                        m_UniqueCredits.Add(cds.CurveName, cds);
                    }
                    catch
                    {
                    }
                }
            }
        }

        public List<cuTally> OverLap(tkCDSList _portfolio) // dsCreditNet.CreditUniverseTallyDataTable OverLap(mxCDSCollection _portfolio)
        {
            List<cuTally> list = new List<cuTally>();
            foreach (tkCDS cds in _portfolio)
            {
                cuTally row = new cuTally();
                {
                    row.date = EvalDate;
                    row.name = Name;
                    row.refEntity = cds.RefEntity;
                    row.ticker = cds.Ticker;
                    row.ccy = cds.CCY;
                    row.tier = cds.SENIORITY;
                    row.countPort = InPortfolios(cds.CurveName);
                    row.cdsSpread = Math.Round(cds.EvalSpread * 100, 2);
                }
                list.Add(row);
            }

            int i = 0;
            foreach (cuTally trow in list) 
            {
                if (trow.countPort != 0)
                {
                    i++;
                }
            }

            cuTally srow = new cuTally();
            {
                srow.refEntity = string.Format("Overlapping Credits: {0:p} [{1} of {2}]", (double)i / _portfolio.Count, i, _portfolio.Count);
            }
            list.Add(srow);
            return list;
        }
    }
}