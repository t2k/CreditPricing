using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Web;


namespace CreditPricing
{

    // Synthetic portfolio tranches...
    public class tkTranche
    {
        private int m_TTNo;
        private string m_Name;
        private tkCDSList m_Portfolio;
        private double m_TrancheNotional;
        private double m_OrigTrancheNotional;
        private double m_PctAttach;
        private double m_PctWidth;
        private double m_OrigPctAttach;
        private double m_OrigPctWidth;
        private double m_UpFrontFee;
        private double m_SpreadBP;
        private string m_Basis;
        private int m_FlowsPA;
        private DateTime m_EvaluationDate;
        private DateTime m_Maturity;
        private DateTime m_Settle;
        private tkTrancheLets m_TrancheLets;
        private string m_CCY;
        private tkSimulation m_Simulation;
        private double[,] m_matrixPortLGD;
        // 2D array/matrix 
        private double[,] m_matrixTrancheLGD;
        // 2D array/matrix 
        private int m_mcDraws;
        // monte carlo draws
        private double m_PV01;
        private double m_PVLOSS;


        public tkTranche(int _TTNo, DateTime _RevalDate)
        {
            var db = new CreditPricingEntities();
            var tRow = (from t in db.TrancheTrades
                        where t.TTNo == _TTNo
                        select t).SingleOrDefault();


            //dsCreditNetTableAdapters.TrancheTradeTableAdapter ta = new dsCreditNetTableAdapters.TrancheTradeTableAdapter();
            //dsCreditNet.TrancheTradeRow tRow = ta.GetDataByTTNo(_TTNo).Rows(0);

            YldCurve yc = new YldCurve(_RevalDate, tRow.CCY);
            m_Portfolio = new tkCDSList(tRow.PortfolioName, _RevalDate);

            m_EvaluationDate = _RevalDate;
            {
                m_TTNo = tRow.TTNo;
                m_Name = tRow.TrancheName;
                m_TrancheNotional = (double)tRow.Notional;
                m_OrigTrancheNotional = (double)tRow.Orig_Notional;
                m_PctAttach = (double)tRow.Attachment;
                m_PctWidth = (double)tRow.Width;
                m_OrigPctAttach = (double)tRow.Orig_Attach;
                m_OrigPctWidth = (double)tRow.Orig_Width;
                m_SpreadBP = (double)tRow.Premium;
                m_Basis = tRow.PremBasis;
                m_FlowsPA = (int)tRow.PremPmtPA;
                m_Maturity = (DateTime)tRow.Maturity;
                m_Settle = (DateTime)tRow.SettlementDate;
                m_UpFrontFee = (double)tRow.Fee;
                m_CCY = tRow.CCY;
            }
            m_TrancheLets = new tkTrancheLets(this, yc);
            //obj)

            m_Simulation = null;
        }

        public tkSimulation Simulation
        {
            get { return m_Simulation; }
            set { m_Simulation = value; }
        }

        // using flat spreads to model default time directly, this can be very close to FA method especially when spreads are lower
        // and it should be a touch faster 
        // NOT USED: was used to model default times directly...
        //Sub TranchePrice(ByVal _draws As Integer, ByVal _corr As Double, ByVal _recovery As Double, ByVal _flatspread As Double)
        //    m_Simulation = New tkSimulation(Me.Portfolio.Title, _draws, _corr, "T")
        //    'Dim tranche As New tkSimulation.tkTranche(yc, myPort, _TTNo, tRow.TrancheName, tRow.Notional, tRow.Attachment, tRow.Width, tRow.Fee, tRow.Premium, tRow.PremBasis, tRow.PremPmtPA, tRow.Maturity, tRow.SettlementDate, tRow.CCY)

        //    ' note: this could all be embedded/encapsulated within simulation...
        //    m_Simulation.TimeToDefault2(Me, _draws, _corr) ', _Recovery, _FlatSpread)
        //    'thisTranche = mySim.TimeToDefault(thisTranche, _Draws, _Corr, _Recovery, _FlatSpread)

        //End Sub

        /// <summary>
        /// Returns table with CDSCurveKey + RefEntityName + 1 to n CDS CurvePoints
        /// compute average row
        /// compute StdDev. row
        /// </summary>
        /// <returns></returns>
        public DataTable TranchePriceInfo()
        {
            DataTable tbl = new DataTable();
            tbl.TableName = "TranchePriceInfo";
            tbl.Columns.Add("CreditCurve", Type.GetType("System.String"));
            foreach (tkTrancheLet tlet in m_TrancheLets)
            {
                tbl.Columns.Add(string.Format("{0:d}", tlet.FwdDate), Type.GetType("System.Double"));
            }

            // load data from portfolio...
            foreach (tkCDS cds in m_Portfolio)
            {
                DataRow trow = default(DataRow);
                trow = tbl.NewRow();
                trow[0] = string.Format("{0}-{1}", cds.CurveName, cds.RefEntity);
                int icol = 1;
                foreach (tkTrancheLet tlet in m_TrancheLets)
                {
                    try
                    {
                        trow[icol] = Math.Round(cds.CDSCurve.cdsMid(tlet.FwdDate) * 100, 2);
                    }
                    catch
                    {
                        trow[icol] = 0;
                    }
                    icol += 1;
                }
                tbl.Rows.Add(trow);
            }

            DataRow rowAvg = default(DataRow);
            rowAvg = tbl.NewRow();

            rowAvg[0] = "[AVERAGE]";

            for (int i = 1; i < tbl.Columns.Count ; i++)
            {
                // note we are skipping the first column (it's text)
                double dAvg = 0;

                foreach (DataRow row in tbl.Rows)
                {
                    dAvg += (double)row[i];
                }
                rowAvg[i] = Math.Round(dAvg / tbl.Rows.Count, 2);
            }

            DataRow rowSD = tbl.NewRow();

            // Standard Deviation

            rowSD[0] = "[STD. DEVIATION]";
            for (int i = 1; i < tbl.Columns.Count; i++)
            {
                // note we are skipping the first column (it's text)
                double dSDAcc = 0;
                // std dev. accumulator per column

                foreach (DataRow row in tbl.Rows)
                {
                    dSDAcc += Math.Pow(((double)row[i] - (double)rowAvg[i]), 2);
                }
                // take square rt. of the sum of the variances squared...
                rowSD[i] = Math.Round(Math.Sqrt(dSDAcc / (tbl.Rows.Count)), 2);
            }

            tbl.Rows.Add(rowAvg);
            tbl.Rows.Add(rowSD);
            return tbl;

        }

        public dynamic CreditCurves()
        {
            try
            {
                var cols = (from t in m_TrancheLets
                            select new
                            {
                                colName = t.FwdDate.ToShortDateString()
                            }).ToList();

                var creditCurves = (from cds in m_Portfolio
                                    select new
                                    {
                                        curveName = cds.CurveName,
                                        termStruct = (from tlet in m_TrancheLets
                                                      select new
                                                      {
                                                          mid = Math.Round(cds.CDSCurve.cdsMid(tlet.FwdDate) * 100, 2)
                                                      }).ToList()
                                    });

                return new
                {
                    cols,
                    creditCurves
                };

            }
            catch (Exception ex)
            {
                return new
                {
                    cols= new List<string>(),
                    creditCurves = new List<string>(),
                    errmsg=ex.Message
                };
            }
        }


        // fully analytical model  push survival probability towards default barrier in a stepped basis
        // this is the most refined method as we can employ the term structure of spreads and should be the most 'analytically correct'
        // here the tranche object is relying upon the embedded tkSimulation class to do the work
        public List<TTPriceHist> TranchePrice(int _draws, double _corr, bool _dbSave)
        {
            m_Simulation = new tkSimulation(this.Portfolio.Title, _draws, _corr, "T", m_EvaluationDate);
            // note: this could all be embedded/encapsulated within simulation...
            m_Simulation.TimeToDefaultNEW(this, _draws, _corr);
            //, _recovery, _flatspread)
            
            List<TTPriceHist> dt = m_Simulation.TranchePrice_SinglePriceNEW(this);
            //  used parspread as DM to reprice the tranche's Credit Linked Note 

            if (_dbSave)
            {
                //REWRITE THIS w/o table adapters..
                //CreditPricing.vb.dsCreditNetTableAdapters.TTPriceHistTableAdapter ta = new dsSimulationTableAdapters.TTPriceHistTableAdapter();
                //ta.Update(dt);
            }
            return dt;
        }


        // fully analytical model  push survival probability towards default barrier in a stepped basis
        // this is the most refined method as we can employ the term structure of spreads and should be the most 'analytically correct'
        // here the tranche object is relying upon the embedded tkSimulation class to do the work
        //Function TranchePriceFA(ByVal _draws As Integer, ByVal _corr As Double, ByVal _recovery As Double, ByVal _flatspread As Double) As dsOUT.TranchePriceDataTable

        //public dsOUT.TranchePriceDataTable TranchePriceFA(int _draws, double _corr)
        public TranchePrice TranchePriceFA(int _draws, double _corr)
        {
            m_Simulation = new tkSimulation(this.Portfolio.Title, _draws, _corr, "T", m_EvaluationDate);
            // note: this could all be embedded/encapsulated within simulation...
            m_Simulation.TimeToDefault(this, _draws, _corr);
            //, _recovery, _flatspread)
            return m_Simulation.TranchePrice_SinglePrice(this);
        }



        // _DefaultCreditList is passed in like:  TKR,CCY,TIER,RR;TKR,CCY,TIER,RR    each credit is seperated by ; and each field is seperated by ,
        public void DefaultCreditList(string _DefaultCreditList)
        {
            if (_DefaultCreditList.Contains(";"))
            {
                // multiple credits
                string[] strList = _DefaultCreditList.Split(';');
                foreach (string credit in strList)
                {
                    try
                    {
                        string[] creditParse = credit.Split(',');
                        double RR = (double)(int.Parse(creditParse[3]) / 100);
                        string strCurveName = string.Format("{0}{1}{2}", creditParse[0], creditParse[1], creditParse[2]);
                        foreach (tkCDS cds in Portfolio)
                        {
                            if (cds.CurveName == strCurveName)
                            {
                                PctAttach = PctAttach - (cds.NotionalAmt * (1 - RR));
                                // first calc the lower attachment for the tranche
                                // effectively defaults this credit ie no weighting in portfolio THEN DEFAULT
                                cds.NotionalAmt = 0;
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
            else
            {
                // only one credit in the list
                try
                {
                    string[] creditParse = _DefaultCreditList.Split(',');
                    double RR = int.Parse(creditParse[3]) / 100;
                    string strCurveName = string.Format("{0}{1}{2}", creditParse[0], creditParse[1], creditParse[2]);
                    foreach (tkCDS cds in Portfolio)
                    {
                        if (cds.CurveName == strCurveName)
                        {
                            PctAttach = PctAttach - (cds.NotionalAmt * (1 - RR));
                            // first calc the lower attachment for the tranche
                            // effectively defaults this credit ie no weighting in portfolio
                            cds.NotionalAmt = 0;
                        }
                    }
                }
                catch
                {
                }
            }
        }

        // fully analytical model  push survival probability towards default barrier in a stepped basis
        // this is the most refined method as we can employ the term structure of spreads and should be the most 'analytically correct'
        // here the tranche object is relying upon the embedded tkSimulation class to do the work
        //public dsOUT.TranchePriceDataTable TranchePriceWDefaults(int _draws, double _corr, string _DefaultedCreditList)
        public TranchePrice TranchePriceWDefaults(int _draws, double _corr, string _DefaultedCreditList)
        {
            // SUPERNEW!  9/26/08  
            this.DefaultCreditList(_DefaultedCreditList);
            m_Simulation = new tkSimulation(this.Portfolio.Title, _draws, _corr, "T", m_EvaluationDate);
            // note: this could all be embedded/encapsulated within simulation...
            m_Simulation.TimeToDefault(this, _draws, _corr);
            //, 0.4, 0)
            return m_Simulation.TranchePrice_SinglePrice(this, _DefaultedCreditList);
        }


        // fully analytical model  push survival probability towards default barrier in a stepped basis
        // this is the most refined method as we can employ the term structure of spreads and should be the most 'analytically correct'
        // here the tranche object is relying upon the embedded tkSimulation class to do the work
        //Function TranchePriceFA_Sensi01(ByVal _draws As Integer, ByVal _corr As Double, ByVal _recovery As Double, ByVal _flatspread As Double) As dsOUT.TranchePriceDataTable
        public TranchePrice TranchePriceFA_Sensi01(int _draws, double _corr)
        {
            m_Simulation = new tkSimulation(this.Portfolio.Title, _draws, _corr, "T", m_EvaluationDate);

            // note: this could all be embedded/encapsulated within simulation...
            //m_Simulation.TimeToDefault(Me, _draws, _corr, _recovery, _flatspread)
            m_Simulation.TimeToDefault(this, _draws, _corr);
            //, _recovery, _flatspread)
            return m_Simulation.TranchePrice_SinglePrice(this);
        }


        // fully analytical model  push survival probability towards default barrier in a stepped basis
        // this is the most refined method as we can employ the term structure of spreads and should be the most 'analytically correct'
        // here the tranche object is relying upon the embedded tkSimulation class to do the work
        public List<TranchePrice> TranchePriceFA_RollDown(int _draws, double _corr)
        {
            //, ByVal _recovery As Double, ByVal _flatspread As Double) As dsOUT.TranchePriceDataTable
            m_Simulation = new tkSimulation(this.Portfolio.Title, _draws, _corr, "T", m_EvaluationDate);
            // note: this could all be embedded/encapsulated within simulation...
            m_Simulation.TimeToDefault(this, _draws, _corr);
            //, _recovery, _flatspread)
            return m_Simulation.TranchePrice_Rolldown(this);
        }

        // fully analytical model  push survival probability towards default barrier in a stepped basis
        // this is the most refined method as we can employ the term structure of spreads and should be the most 'analytically correct'
        // here the tranche object is relying upon the embedded tkSimulation class to do the work
        // perform suborination analysis: hold everything constant but vary subordination up/down by +/- 10 bp
        public List<TranchePrice> TranchePriceFA_Subordination(int _draws, double _corr, string _attachWidthList)
        {
            m_Simulation = new tkSimulation(this.Portfolio.Title, _draws, _corr, "T", m_EvaluationDate);
            // note: this could all be embedded/encapsulated within simulation...
            m_Simulation.TimeToDefault(this, _draws, _corr);
            //, _recovery, _flatspread)
            return m_Simulation.TranchePrice_Subordination(this, _attachWidthList);
        }

        // fully analytical model  push survival probability towards default barrier in a stepped basis
        // this is the most refined method as we can employ the term structure of spreads and should be the most 'analytically correct'
        // here the tranche object is relying upon the embedded tkSimulation class to do the work
        // perform suborination analysis: hold everything constant but vary subordination up/down by +/- 10 bp
        public List<TranchePrice2> TranchePriceFA_Subordination2(int _draws, double _corr, string _attachWidthList)
        {
            m_Simulation = new tkSimulation(this.Portfolio.Title, _draws, _corr, "T", m_EvaluationDate);
            // note: this could all be embedded/encapsulated within simulation...
            m_Simulation.TimeToDefault(this, _draws, _corr);
            //, _recovery, _flatspread)
            return m_Simulation.TranchePrice_Subordination2(this, _attachWidthList);
        }



        public DateTime EvalDate
        {
            get { return m_EvaluationDate; }
            set { m_EvaluationDate = value; }
        }

        public double PV01
        {
            get { return m_PV01; }
            set { m_PV01 = value; }
        }

        public double PVLOSS
        {
            get { return m_PVLOSS; }
            set { m_PVLOSS = value; }
        }


        public double ParPremium
        {
            get { return m_PVLOSS / (m_PV01 * 10000); }
        }


        public int TTNo
        {
            get { return m_TTNo; }
        }

        public double[,] PortLossDist
        {
            get { return m_matrixPortLGD; }
            set { m_matrixPortLGD = value; }
        }

        public double[,] TrancheLossDist
        {
            get { return m_matrixTrancheLGD; }
            set { m_matrixTrancheLGD = value; }
        }


        public int mcDraws
        {
            get { return this.m_mcDraws; }

            set { m_mcDraws = value; }
        }


        public string CCY
        {
            get { return m_CCY; }
        }


        public DateTime TrancheSettle
        {
            get { return m_Settle; }
            set { m_Settle = value; }
        }

        public DateTime TrancheMaturity
        {
            get { return m_Maturity; }
            set { }

        }

        public double TrancheUpFront
        {
            get { return m_UpFrontFee; }
        }

        public double OrigTrancheNotional
        {
            get { return m_OrigTrancheNotional; }
        }

        public double TrancheNotional
        {
            get { return m_TrancheNotional; }
        }

        public string TrancheName
        {
            get { return m_Name; }
        }

        public tkCDSList Portfolio
        {
            get { return m_Portfolio; }
        }

        public double PctAttach
        {
            get { return m_PctAttach; }
            set { m_PctAttach = value; }
        }


        public double OrigPctAttach
        {
            get { return m_OrigPctAttach; }
            set { m_OrigPctAttach = value; }
        }

        public double PctWidth
        {
            get { return m_PctWidth; }

            set { m_PctWidth = value; }
        }

        public double OrigPctWidth
        {
            get { return m_OrigPctWidth; }
            set { m_OrigPctWidth = value; }
        }

        public double SpreadBP
        {
            get { return m_SpreadBP; }
        }

        public string Basis
        {
            get { return m_Basis; }
        }

        public int FlowsPA
        {
            get { return m_FlowsPA; }
        }

        public tkTrancheLets TrancheLets
        {
            get { return m_TrancheLets; }
        }



        public double TrancheRISKY01
        {
            get
            {
                DateTime dtPrev = System.DateTime.Today.AddDays(1);
                double d01 = 0.0001;
                double thisResult = 0;
                double dblExpLoss = 0;


                foreach (tkTrancheLet tlet in m_TrancheLets)
                {
                    dblExpLoss += tlet.ExpectedLoss;

                    thisResult += (m_TrancheNotional * (1 - dblExpLoss) * d01 * tlet.DiscountFactor * (double)tlet.FwdDate.Subtract(dtPrev).Days / 360);
                    dtPrev = tlet.FwdDate;
                }


                return thisResult;
            }

        }



        //here we transform the portfolio loss distribution into the Tranche Loss Distribution
        public void TrancheLossFunc()
        {
            // ERROR: Not supported in C#: ReDimStatement ReDim m_matrixTrancheLGD(m_matrixPortLGD.GetLength(0) - 1, m_matrixPortLGD.GetLength(1) - 1)
            m_matrixTrancheLGD = new double[m_matrixPortLGD.GetLength(0), m_matrixPortLGD.GetLength(1)];

            for (int k = 0; k < m_matrixPortLGD.GetLength(1); k++)
            {
                // matrixOUT.GetLength(1) - 1
                for (int i = 0; i < m_matrixPortLGD.GetLength(0); i++)
                {
                    if (m_matrixPortLGD[i, k] <= m_PctAttach)
                    {
                        m_matrixTrancheLGD[i, k] = 0;
                    }
                    else if (m_matrixPortLGD[i, k] <= (m_PctAttach + m_PctWidth))
                    {
                        //tr_a + tr_w) Then
                        m_matrixTrancheLGD[i, k] = (m_matrixPortLGD[i, k] - m_PctAttach) / m_PctWidth;
                    }
                    else
                    {
                        m_matrixTrancheLGD[i, k] = 1;
                    }
                }
            }
        }


        public void CalcTranchLets()
        {
            int idxTrancheLet = 0;
            int nDraws = this.m_matrixPortLGD.GetLength(0);
            foreach (tkTrancheLet tranchelet in this.TrancheLets)
            {
                double dblTotal = 0;
                for (int i = 0; i < nDraws; i++)
                {
                    dblTotal += this.m_matrixTrancheLGD[i, idxTrancheLet];
                }
                tranchelet.ExpectedLoss = dblTotal / nDraws;
                idxTrancheLet += 1;
            }
        }

        public double TrancheDLPV
        {
            get
            {
                double dPrev = 0;
                double DLPV = 0;

                foreach (tkTrancheLet tlet in m_TrancheLets)
                {
                    //DLPV += (m_TrancheNotional * tlet.DiscountFactor * System.Math.Max(0, tlet.ExpectedLoss - dPrev))
                    DLPV += (m_TrancheNotional * tlet.DiscountFactor * tlet.ExpectedLoss);
                    dPrev = tlet.ExpectedLoss;
                }
                return DLPV;
            }
        }


        public dynamic GetDataSet
        {
            get
            {
                List<TranchePrice> dt = new List<TranchePrice>(); // dsOUT.TranchePriceDataTable dt = new dsOUT.TranchePriceDataTable();
                TranchePrice tpRow = new TranchePrice(); // dsOUT.TranchePriceRow tpRow = dt.NewTranchePriceRow();
                {
                    tpRow.TTNo = m_TTNo;
                    tpRow.Portfolio = this.Portfolio.Title;
                    tpRow.Name = m_Name;
                    tpRow.Notional = m_TrancheNotional;
                    tpRow.Attach = m_PctAttach;
                    tpRow.Width = m_PctWidth;
                    if (m_UpFrontFee != 0)
                    {
                        tpRow.Fee = (PVLOSS - PV01 * m_SpreadBP) * 100;
                    }
                    else
                    {
                        tpRow.Fee = m_UpFrontFee;
                    }

                    tpRow.TranchePremium = m_SpreadBP;
                    tpRow.PVLoss = m_PVLOSS;
                    tpRow.PV01 = m_PV01;
                    if (m_UpFrontFee != 0)
                    {
                        tpRow.ParPremium = m_SpreadBP;
                    }
                    else
                    {
                        tpRow.ParPremium = this.ParPremium * 10000;
                    }

                    //row(11) = m_TrancheNotional * m_PV01 * (m_SpreadBP - (Me.ParPremium * 10000))
                    tpRow.Maturity = m_Maturity;
                    tpRow.CCY = m_CCY;
                    tpRow.PortWAS = m_Portfolio.cdsWASpreadL * 100;
                    tpRow.SimID = m_Simulation.SimID;
                    tpRow.SimDraws = m_Simulation.NumDraws;
                    tpRow.Correlation = m_Simulation.Correlation;
                    tpRow.SimElapsed = m_Simulation.ElapsedTime.TotalSeconds;
                }
                dt.Add(tpRow); // dt.AddTranchePriceRow(tpRow);

                List<TrancheLet> tlets = new List<TrancheLet>();
                foreach (tkTrancheLet tLet in m_TrancheLets)
                {
                    TrancheLet trow = new TrancheLet(); // trow = tbl.NewRow();
                    trow.ForwardDate= tLet.FwdDate;
                    trow.DiscountFactor = tLet.DiscountFactor;
                    trow.CumulativeLoss = tLet.ExpectedLoss;
                    tlets.Add(trow);
                }

                // return a dynamic type// two lists
                return new
                {
                    tranchePrice = dt,
                    trancheLets = tlets
                };
            }
        }

        public DataTable TrancheAttachStudy()
        {
            TrancheLossFunc();
            CalcTranchLets();

            System.Data.DataTable tblHdr = new System.Data.DataTable("TrancheSensi");
            tblHdr.Columns.Add("TTNo", Type.GetType("System.Int32"));
            // 0
            tblHdr.Columns.Add("Portfolio", Type.GetType("System.String"));
            // 1
            tblHdr.Columns.Add("Name", Type.GetType("System.String"));
            // 2
            tblHdr.Columns.Add("Notional", Type.GetType("System.Double"));
            // 3
            tblHdr.Columns.Add("Attach", Type.GetType("System.Double"));
            // 4
            tblHdr.Columns.Add("Width", Type.GetType("System.Double"));
            // 5
            tblHdr.Columns.Add("Fee", Type.GetType("System.Double"));
            // 6
            tblHdr.Columns.Add("Premium", Type.GetType("System.Double"));
            // 7
            tblHdr.Columns.Add("PV Def Leg", Type.GetType("System.Double"));
            // 8
            tblHdr.Columns.Add("RISKY01", Type.GetType("System.Double"));
            // 9
            tblHdr.Columns.Add("BES", Type.GetType("System.Double"));
            // 10
            tblHdr.Columns.Add("MTM", Type.GetType("System.Double"));
            // 11
            tblHdr.Columns.Add("Maturity", Type.GetType("System.DateTime"));
            // 12
            tblHdr.Columns.Add("CCY", Type.GetType("System.String"));
            // 13
            tblHdr.Columns.Add("PortWAS", Type.GetType("System.Double"));
            // 14

            DataRow row = tblHdr.NewRow();
            row[0] = m_TTNo;
            row[1] = m_Portfolio.Title;
            row[2] = m_Name;
            row[3] = m_TrancheNotional;
            row[4] = m_PctAttach;
            row[5] = m_PctWidth;
            row[6] = m_UpFrontFee;
            row[7] = m_SpreadBP;
            row[8] = TrancheDLPV;
            row[9] = TrancheRISKY01;
            row[10] = (double)row[8] / (double)row[9];
            row[11] = (m_SpreadBP - (double)row[10]) * (double)row[9];
            row[12] = m_Maturity;
            row[13] = m_CCY;
            row[14] = m_Portfolio.cdsWASpreadL * 100;
            tblHdr.Rows.Add(row);

            double dblAttach = PctAttach;
            double dblIncr = 0.0001;

            for (int i = 1; i <= 10; i++)
            {
                PctAttach = dblAttach + (i * dblIncr);
                TrancheLossFunc();
                CalcTranchLets();
                DataRow simRow = tblHdr.NewRow();
                simRow[0] = m_TTNo;
                simRow[1] = m_Portfolio.Title;
                simRow[2] = m_Name;
                simRow[3] = m_TrancheNotional;
                simRow[4] = m_PctAttach;
                simRow[5] = m_PctWidth;
                simRow[6] = m_UpFrontFee;
                simRow[7] = m_SpreadBP;
                simRow[8] = TrancheDLPV;
                simRow[9] = TrancheRISKY01;
                simRow[10] = (double)simRow[8] / (double)simRow[9];
                simRow[11] = (m_SpreadBP - (double)simRow[10]) * (double)simRow[9];
                simRow[12] = m_Maturity;
                simRow[13] = m_CCY;
                simRow[14] = m_Portfolio.cdsWASpreadL * 100;
                tblHdr.Rows.Add(simRow);
            }
            dblIncr = 0.0005;
            for (int i = 1; i <= 10; i++)
            {
                PctAttach = dblAttach + (i * dblIncr);
                TrancheLossFunc();
                CalcTranchLets();
                DataRow simRow = tblHdr.NewRow();
                simRow[0] = m_TTNo;
                simRow[1] = m_Portfolio.Title;
                simRow[2] = m_Name;
                simRow[3] = m_TrancheNotional;
                simRow[4] = m_PctAttach;
                simRow[5] = m_PctWidth;
                simRow[6] = m_UpFrontFee;
                simRow[7] = m_SpreadBP;
                simRow[8] = TrancheDLPV;
                simRow[9] = TrancheRISKY01;
                simRow[10] = (double)simRow[8] / (double)simRow[9];
                simRow[11] = (m_SpreadBP - (double)simRow[10]) * (double)simRow[9];
                simRow[12] = m_Maturity;
                simRow[13] = m_CCY;
                simRow[14] = m_Portfolio.cdsWASpreadL * 100;
                tblHdr.Rows.Add(simRow);
            }

            dblIncr = 0.0001;
            //reset
            PctAttach = dblAttach;
            for (int i = 1; i <= 10; i++)
            {
                PctAttach = dblAttach - (i * dblIncr);
                TrancheLossFunc();
                CalcTranchLets();
                DataRow simRow = tblHdr.NewRow();
                simRow[0] = m_TTNo;
                simRow[1] = m_Portfolio.Title;
                simRow[2] = m_Name;
                simRow[3] = m_TrancheNotional;
                simRow[4] = m_PctAttach;
                simRow[5] = m_PctWidth;
                simRow[6] = m_UpFrontFee;
                simRow[7] = m_SpreadBP;
                simRow[8] = TrancheDLPV;
                simRow[9] = TrancheRISKY01;
                simRow[10] = (double)simRow[8] / (double)simRow[9];
                simRow[11] = ((double)m_SpreadBP - (double)simRow[10]) * (double)simRow[9];
                simRow[12] = m_Maturity;
                simRow[13] = m_CCY;
                simRow[14] = m_Portfolio.cdsWASpreadL * 100;
                tblHdr.Rows.Add(simRow);
            }
            dblIncr = 0.0005;
            //reset
            PctAttach = dblAttach;
            for (int i = 1; i <= 10; i++)
            {
                PctAttach = dblAttach - (i * dblIncr);
                TrancheLossFunc();
                CalcTranchLets();
                DataRow simRow = tblHdr.NewRow();
                simRow[0] = m_TTNo;
                simRow[1] = m_Portfolio.Title;
                simRow[2] = m_Name;
                simRow[3] = m_TrancheNotional;
                simRow[4] = m_PctAttach;
                simRow[5] = m_PctWidth;
                simRow[6] = m_UpFrontFee;
                simRow[7] = m_SpreadBP;
                simRow[8] = TrancheDLPV;
                simRow[9] = TrancheRISKY01;
                simRow[10] = (double)simRow[8] / (double)simRow[9];
                simRow[11] = (m_SpreadBP - (double)simRow[10]) * (double)simRow[9];
                simRow[12] = m_Maturity;
                simRow[13] = m_CCY;
                simRow[14] = m_Portfolio.cdsWASpreadL * 100;
                tblHdr.Rows.Add(simRow);
            }
            //reset
            PctAttach = dblAttach;
            return tblHdr;
        }

    }

}