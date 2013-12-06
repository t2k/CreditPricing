using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;

namespace CreditPricing
{

    public struct mcPort
    {
        public double Notional { get; set; }        // trade notl
        public DateTime Maturity { get; set; }        // trade maturity
        public double CDP { get; set; }        // Cum default probability (RNP from CDS Spreads)
        public double RR { get; set; }        // Recovery Rate
        public bool RRFixed { get; set; }        // Is RR fixed? T/F
        public double sdRR { get; set; }        // standard dev of RR (can be assigned by sector )
        public double Sprd { get; set; }        // CDS Spread...
    }
    public struct mcPort2
    {
        public double Notional { get; set; }        // trade notl
        public double Spread { get; set; }        
        public double[] CDP { get; set; }        // Cum default probability (from CDS market spreads Spreads) 
        public double RR { get; set; }        // Recovery Rate
        public bool RRFixed { get; set; }        // Is RR fixed? T/F
        public double sdRR { get; set; }        // standard dev of RR (can be assigned by sector )
    }
    public struct mcDraw
    {
        public int nDefaults { get; set; }        // number of defaults in the portfolio per draw
        public double dblLGD { get; set; }        // portfolio LGD per draw
        public double dblWARR { get; set; }        // warr per DRAW
        public double dblTrancheLGD { get; set; }        // scaled by tranche loss function
    }
    public struct mcOutput
    {
        public DateTime mcDate { get; set; }        // simu date
        public TimeSpan mcDuration { get; set; }        // time of simulation run in timespan units (can show user #seconds)
        public double mcCorrelation { get; set; }        // one factor correlation ie .13
        public double mcMaxLoss { get; set; }        // max loss of all draws in the simulation
        public int mcMaxNumDefaults { get; set; }        // max number of defaults for all draws in the simulation
        public string mcPortfolio { get; set; }        // portfolio name/title
        public int mcPortCount { get; set; }        // #Credits in portfolio
        public double mcPortWAS { get; set; }        // wtd Ave Spread...
        public double mcPortNotional { get; set; }        // notional total of credits in theportfolio
        public double mcTotalLoss { get; set; }        // total loss of 
        public mcDraw[] mcDraws { get; set; }
    }

    public class tkSimulation
    {
        public int SimID { get; set; }        //private int m_SimID;
        private DateTime m_SimDate;
        private DateTime m_StartTime;
        private DateTime m_EndTime;
        private int m_Draws;
        private double m_Correlation;
        private double[,] m_pld;
        private double[] m_PV01;
        private double[] m_PVLOSS;


        public tkSimulation(string _Portfolio, int _Draws, double _Corr, string _SimType, DateTime _RevalDate)
        {
            m_Draws = _Draws;
            m_Correlation = _Corr;
            m_SimDate = _RevalDate;
            m_StartTime = DateTime.Now;

            var db = new CreditPricingEntities();
            PortfolioSimulation ps = new PortfolioSimulation();
            ps.PortfolioName = _Portfolio;
            ps.RevalDate = _RevalDate;
            ps.SimulationDate = m_StartTime;
            ps.Correlation = _Corr;
            ps.Draws = _Draws;
            ps.SimType = _SimType;
            db.PortfolioSimulations.AddObject(ps);
            db.SaveChanges();
            SimID = ps.SimID;  // by SaveChanges() we have posted to the datastore and returned the next SimID
        }

        public DateTime SimDate
        {
            get { return m_SimDate; }
        }

        public double[,] LossDistribution
        {
            get { return m_pld; }
        }


        public DateTime StartTime
        {
            get { return m_StartTime; }
            set { m_StartTime = value; }
        }
        public DateTime EndTime
        {
            get { return m_EndTime; }
            set { m_EndTime = value; }
        }
        public int NumDraws
        {
            get { return m_Draws; }
            set { m_Draws = value; }
        }

        public double Correlation
        {
            get { return m_Correlation; }
            set { m_Correlation = value; }
        }

        public TimeSpan ElapsedTime
        {
            get { return (m_EndTime - m_StartTime); }
        }


        // general contructor, not really used 
        //public tkSimulation()
        //{
        //    m_StartTime = System.DateTime.Now;
        //    m_Draws = (int)25000;
        //    m_Correlation = (double)0.2;
        //}



        public mcOutput PortfolioCumLoss(tkCDSList port, int nDraws, double dCorr)
        {
            m_StartTime = DateTime.Now;
            mcPort[] arPort = new mcPort[port.Count];
            // CDS portfolio
            mcDraw[] arMCOut = new mcDraw[nDraws];
            // array of monte carlo output (results of each draw)
            int iPath = 0;
            //mc draw loop counter
            int iAsset = 0;
            double dTPN = 0;

            // new class based generator...
            CorrProbDefDist tkCorr = new CorrProbDefDist(nDraws, port.Count, dCorr);
            double[,] rndAssets = tkCorr.JointProbDefDist;
            double[] MarketRands = tkCorr.MarketRands;


            foreach (tkCDS obj in port)
            {
                {
                    arPort[iAsset].Notional = obj.NotionalAmt;
                    dTPN += obj.NotionalAmt;
                    arPort[iAsset].CDP = obj.CLP_CC(obj.dtMaturity);
                    arPort[iAsset].Maturity = obj.dtMaturity;


                    arPort[iAsset].RRFixed = obj.IsFixedRR;
                    if (arPort[iAsset].RRFixed)
                    {
                        arPort[iAsset].RR = obj.RecoveryOnDefault;
                    }
                    else
                    {
                        try
                        {
                            // take recovery from MARKIT curve...
                            arPort[iAsset].RR = obj.CDSCurve.Recovery;
                        }
                        catch 
                        {
                            // not so rare
                            arPort[iAsset].RR = 0.4;

                        }
                    }

                    arPort[iAsset].sdRR = (double)0.05;
                    // 5% std. dev. of recoveries around the mean...
                    arPort[iAsset].Sprd = obj.EvalSpread / 100;
                }
                iAsset += 1;
            }

            double drawLGD = 0;
            //draw loss (i)
            double dLossTotal = 0;
            double dMaxLoss = 0;
            //max draw loss for our distribution
            int iLossCount = 0;
            // lets also count the default events...
            int iMaxLossCount = 0;

            for (iPath = 0; iPath <= nDraws - 1; iPath++)
            {
                drawLGD = 0;
                // double portfolio loss
                iLossCount = 0;
                // integer #loss count
                double dWARR = 0;
                // for each draw, tally Wtd Ave Recovery Rate
                double drawPortLossNotl = 0;
                // needed for WARR (Total notl of just the losses)
                for (iAsset = 0; iAsset <= arPort.Length - 1; iAsset++)
                {

                    // our credit index is less than our default threshold...
                    if (rndAssets[iPath, iAsset] < arPort[iAsset].CDP)
                    {
                        // A DEFAULT HAS OCCURRED IN OUR SIMULATION... (firm value is less than default threshold in our model)
                        drawPortLossNotl += arPort[iAsset].Notional;
                        if (arPort[iAsset].RRFixed)
                        {
                            // if recoveries are fixed then LGD = 1 - RR
                            drawLGD += arPort[iAsset].Notional * (1 - arPort[iAsset].RR);
                            dWARR += arPort[iAsset].Notional * arPort[iAsset].RR;
                        }
                        else
                        {

                            // if recoveries are not fixed then RR is Systematic Rand # of stdDevs from the mean)
                            // recoveries will be LESS in real negative market samples (R1 RAND is the driver for this)
                            // ensure RR >=0 and <=1
                            double thisRR = Math.Max(Math.Min((arPort[iAsset].RR + (MarketRands[iPath] * arPort[iAsset].sdRR)), 1), 0);
                            drawLGD += arPort[iAsset].Notional * (1 - thisRR);
                            dWARR += arPort[iAsset].Notional * thisRR;
                        }
                        iLossCount += 1;
                    }
                }
                // looping thru portfolio items...

                // done with all credits in this draw tally results in our array of draws
                arMCOut[iPath].nDefaults = iLossCount;
                // this is the # of losses in this draw
                arMCOut[iPath].dblLGD = drawLGD;
                /// dTPN  ' pct of total portfolio notl...  (no scaling required)

                if (iLossCount > 0)
                {
                    arMCOut[iPath].dblWARR = dWARR / drawPortLossNotl;
                }
                else
                {
                    arMCOut[iPath].dblWARR = 0;
                }

                // total losses and count defaults
                double dDrawLoss = arMCOut[iPath].dblLGD;
                // accumulate total losses
                dLossTotal += dDrawLoss;
                //keep track of max loss across all draws...
                if (dDrawLoss > dMaxLoss) dMaxLoss = dDrawLoss;
                //keep track of max # defaults across all draws
                if (iLossCount > iMaxLossCount) iMaxLossCount = iLossCount;
            }
            // monte carlo draws
            // monte carlo simulation is completed...
            m_EndTime = DateTime.Now;

            mcOutput retval = default(mcOutput);

            {
                retval.mcDate = m_SimDate;
                retval.mcDuration = this.ElapsedTime;
                retval.mcCorrelation = dCorr;
                retval.mcPortfolio = port.Title;
                retval.mcPortNotional = dTPN;
                // totalled portfolio notional
                retval.mcPortCount = port.Count;
                retval.mcTotalLoss = dLossTotal;
                retval.mcMaxLoss = dMaxLoss;
                retval.mcMaxNumDefaults = iMaxLossCount;
                retval.mcPortWAS = port.cdsWASpreadL;
                retval.mcDraws = arMCOut;
            }
            return retval;
        }
    


        /// <summary>
        /// 
        /// </summary>
        /// <param name="tranche"></param>
        /// <param name="nDraws"></param>
        /// <param name="dCorr"></param>
        /// <param name="_Recovery"></param>
        /// <param name="_FlatSpread"></param>
        /// <returns></returns>
        public tkTranche TimeToDefaultOLD(tkTranche tranche, int nDraws, double dCorr, double _Recovery, double _FlatSpread)
        {
            m_StartTime = DateTime.Now;

            // ensure recovery is not out of bounds!
            if (_Recovery > 1 | _Recovery < 0)
            {
                _Recovery = 0.4;
            }
            // same for correlation
            if (dCorr > 1 | dCorr < 0)
            {
                dCorr = 0.2;
            }

            double tr_a = tranche.PctAttach;
            double tr_w = tranche.PctWidth;

            // this returns a 2d array of double values containing the CLP term structure for our portfolio
            // this member is the prob loss distribution (marginal)
            // ERROR: Not supported in C#: ReDimStatement


            // for tranche pricing...
            // ERROR: Not supported in C#: ReDimStatement

            // ERROR: Not supported in C#: ReDimStatement


            //initialize array to zeros (not sure if this is needed)
            //For iRow As Integer = 0 To m_pld.GetLength(0) - 1
            //    For iCol As Integer = 0 To m_pld.GetLength(1) - 1
            //        m_pld(iRow, iCol) = 0
            //    Next
            //Next

            mcPort2[] arPort = new mcPort2[tranche.Portfolio.Count];
            // NEW mcPORT2 contains a dynamic array of CLP's (per tranchePeriod)
            // create array of portfolio data for speed up...
            int idxtrade = 0;
            int i = 0;
            foreach (tkCDS cds in tranche.Portfolio)
            {
                if (_FlatSpread != 0)
                {
                    if (!cds.IsFixedRR)
                    {
                        cds.RecoveryOnDefault = _Recovery;
                    }
                    cds.EvalSpread = _FlatSpread / 100;
                }


                {
                    arPort[idxtrade].Notional = cds.NotionalAmt;
                    // note trades here are entered as Portfolio %Weights
                    // ERROR: Not supported in C#: ReDimStatement

                    // get the tranche
                    i = 0;
                    foreach (tkTrancheLet tlet in tranche.TrancheLets)
                    {
                        // note: hazardrate aka cleanspread is not a function of RecoveryOnDefault (we generally assume 40% RecoveryPricing) so CumLoss is not effected by FixedRecovery in the model
                        arPort[idxtrade].CDP[i] = Math.Max(cds.CLP_CC_Curve(tlet.FwdDate), 0.0001);
                        // note cum loss probability with flat spread and continuous compounding
                        i += 1;
                    }
                    //.Maturity = cds.dtMaturity

                    // lets model the RR
                    if (cds.IsFixedRR)
                    {
                        arPort[idxtrade].RR = cds.RecoveryOnDefault;
                    }
                    else
                    {
                        arPort[idxtrade].RR = _Recovery;
                    }
                    arPort[idxtrade].RRFixed = cds.IsFixedRR;
                    // 5% std. dev. of recoveries around the mean...
                    arPort[idxtrade].sdRR = (double)0.05;
                }
                idxtrade += 1;
            }

            CorrProbDefDist tkdist = new CorrProbDefDist(nDraws, tranche.Portfolio.Count, dCorr);
            double[,] rndAssets = tkdist.JointProbDefDist;
            double[] rndMarket = tkdist.MarketRands;

            // start your engines...  iPath looping through each monte-carlo draw
            for (int iPath = 0; iPath <= nDraws - 1; iPath++)
            {
                // (each draw ie 100,000)

                // for each monte-carlo draw, we simulated each trades loss over the tranche premium legs...
                for (int iAsset = 0; iAsset <= arPort.Length - 1; iAsset++)
                {
                    //(each trade ie 150)

                    for (int iPeriod = 0; iPeriod <= tranche.TrancheLets.Count - 1; iPeriod++)
                    {
                        if (rndAssets[iPath, iAsset] > (1 - arPort[iAsset].CDP[iPeriod]))
                        {
                            // at this point, a  DEFAULT HAS OCCURRED the asset didn't survive this period...
                            if (arPort[iAsset].RRFixed)
                            {
                                m_pld[iPath, iPeriod] += arPort[iAsset].Notional * (1 - arPort[iAsset].RR);
                            }
                            else
                            {
                                // STOCHASTIC RECOVERIES
                                // if recoveries are not fixed then RR is Systematic Rand # of stdDevs from the mean)
                                // recoveries will be LESS in real negative market samples (R1 RAND is the driver for this)
                                // ensure RR >=0 and <=1
                                double thisRR = Math.Min(Math.Max((arPort[iAsset].RR + (rndMarket[iPath] * arPort[iAsset].sdRR)), 0), 1);
                                m_pld[iPath, iPeriod] += arPort[iAsset].Notional * (1 - thisRR);
                            }
                            // this is important, only default ONCE per credit per portfolio draw...
                            break; // TODO: might not be correct. Was : Exit For
                        }
                        // trade cashflows/tranchelets
                    }
                }
                // portfolio cds trades

                // we are at the end of a draw...(all portfolio periodic contingent marginal losses are distributed 
                // and  all trades have been simulated and now this 'ROW' contains
                // portfolio losses per period (iPeriod), contingent upon surviving until (iPeriod-1). 
                // otherwise known as contingent marginal losses

                // IMPORTANT!, convert this paths marginal losses to cum losses (just summing them)
                double pathCumLoss = 0;
                double pathPrem = 0;
                double prevPrem = tr_w;
                double sumPathPrem = 0;
                double sumPathLoss = 0;

                for (int k = 0; k < m_pld.GetLength(1); k++)
                {
                    pathCumLoss += m_pld[iPath, k];
                    m_pld[iPath, k] = pathCumLoss;
                    // sum the losses to create cumulative losses in time for this path

                    pathPrem = Math.Min(Math.Max(0, (tr_a + tr_w) - pathCumLoss), tr_w);
                    // tranche absorbing cumulative losses from attachment point to detachment point
                    double periodDF = tranche.TrancheLets[k].DiscountFactor;

                    sumPathPrem += (pathPrem * periodDF);
                    // PV periodic premium
                    sumPathLoss += ((prevPrem - pathPrem) * periodDF);
                    // PV losses
                    prevPrem = pathPrem;
                }

                m_PV01[iPath] = .0001 * (1 / tr_w) * sumPathPrem;
                // scaled by 1 basis point (BP)
                m_PVLOSS[iPath] = (1 / tr_w) * sumPathLoss;
            }
            // simulation draws

            // calculate exploss and expPV01  (the mean)
            double sumPV01 = 0;
            double sumPVLOSS = 0;
            for (int iPath = 0; iPath <= nDraws - 1; iPath++)
            {
                sumPV01 += m_PV01[iPath];
                sumPVLOSS += m_PVLOSS[iPath];
            }

            tranche.PV01 = sumPV01 / nDraws;
            tranche.PVLOSS = sumPVLOSS / nDraws;

            // here we've completed all calculations and are left with a 2D array (MATRIX)  (rows,cols) ie (100000,20) 
            // containing portfolio losses per period...
            int idxTrancheLet = 0;

            foreach (tkTrancheLet tranchelet in tranche.TrancheLets)
            {
                double dblTotal = 0;
                for (int iPath = 0; iPath <= nDraws - 1; iPath++)
                {
                    dblTotal += m_pld[iPath, idxTrancheLet];
                }
                idxTrancheLet += 1;

                double dblTLetEL = dblTotal / nDraws;

                tranchelet.ExpectedLoss = dblTLetEL;
            }
            // tranchelet
            m_EndTime = DateTime.Now;
            // go F yurselves...
            return tranche;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="_tranche"></param>
        /// <param name="_defaultTKR"></param>
        /// <returns></returns>
        public TranchePrice TranchePrice_SinglePrice(tkTranche _tranche, string _defaultTKR = "baseline")
        {
            // here the simulation is already run
            // m_PLD(,) is a 2D array (iPath,iPeriod) of CUMULATIVE LOSSES (the result of the sumulation
            //CreditPricing.CreditBLL.BLL.vb.dsOUT.TranchePriceDataTable dt = new CreditPricing.CreditBLL.BLL.vb.dsOUT.TranchePriceDataTable();

            //List<TranchePrice> dt = new List<TranchePrice>();

            //dt.TableName = string.Format("{0}_{1}", dt.TableName, m_SimID);

            double dAttach = Math.Max(0, _tranche.PctAttach);
            if (_tranche.PctAttach < 0)
            {
                _tranche.PctWidth = Math.Max(0, _tranche.PctAttach + _tranche.PctWidth);
                _tranche.PctAttach = 0;
            }

            double dWidth = _tranche.PctWidth;
            int iTrancheLets = _tranche.TrancheLets.Count;


            double dPV01 = 0;
            double dPVLOSS = 0;

            for (int ipath = 0; ipath < m_Draws; ipath++)
            {
                // begin processing a path/draw
                double pathPrem = 0;
                double prevPrem = dWidth;
                double sumPathPrem = 0;
                double sumPathLoss = 0;

                // accumulated cumloss across this path (horizontally I like to think) upto the iPeriod
                for (int idx = 0; idx < iTrancheLets; idx++)
                {
                    // cumulative for each tranchelet period...
                    pathPrem = Math.Min(Math.Max((dAttach + dWidth) - m_pld[ipath, idx], 0), dWidth);
                    // tranche absorbing cumulative losses from attachment point to detachment point
                    double periodDF = _tranche.TrancheLets[idx].DiscountFactor;
                    sumPathPrem += (pathPrem * periodDF);
                    // PV periodic premium
                    sumPathLoss += ((prevPrem - pathPrem) * periodDF);
                    // PV losses
                    prevPrem = pathPrem;
                }
                // tally the pv01 and pvLoss  
                dPV01 += .0001 * (1 / dWidth) * sumPathPrem;
                // scaled by 1 basis point (BP)
                dPVLOSS += (1 / dWidth) * sumPathLoss;
            }

            // take average
            double meanPV01 = dPV01 / m_Draws;
            double meanPVLOSS = dPVLOSS / m_Draws;

            // create a row and add it to our datatable
            //dsOUT.TranchePriceRow row = dt.NewTranchePriceRow();
            TranchePrice row = new TranchePrice();
            {
                row.TTNo = _tranche.TTNo;
                row.Portfolio = _tranche.Portfolio.Title;
                row.Name = _tranche.TrancheName;
                row.Notional = _tranche.OrigTrancheNotional * _tranche.PctWidth / _tranche.OrigPctWidth;
                row.Attach = dAttach;
                // reflect the change here....
                row.Width = _tranche.PctWidth;

                if (_tranche.TrancheUpFront != 0)
                {
                    row.Fee = (meanPVLOSS - meanPV01 * _tranche.SpreadBP) * 100;
                }
                else
                {
                    row.Fee = 0;
                }

                row.TranchePremium = _tranche.SpreadBP;
                row.PV01 = meanPV01;
                row.PVLoss = meanPVLOSS;

                if (_tranche.TrancheUpFront != 0)
                {
                    row.ParPremium = _tranche.SpreadBP;
                }
                else
                {
                    row.ParPremium = (meanPV01 == 0) ? 0 : meanPVLOSS / meanPV01;
                }
                row.Maturity = _tranche.TrancheLets[iTrancheLets - 1].FwdDate;
                row.CCY = _tranche.CCY;
                row.PortWAS = _tranche.Portfolio.cdsWASpreadL * 100;
                row.SimID = SimID; // m_SimID;
                row.SimDraws = m_Draws;
                row.Correlation = m_Correlation;
                row.SimElapsed = this.ElapsedTime.TotalSeconds;
                row.DefaultTKR = _defaultTKR;
            }
            //dt.Add(row);
            // here we are only sending one price row back
            return row; // dt;
        }


        // call after class has been created New(...,,)
        // and after TimeToDefaultStudy has been performed
        // NEW for Sep 12 2008, return a TTPriceHist table... to facilitate persistant storage (SQL Server)
        public List<TTPriceHist> TranchePrice_SinglePriceNEW(tkTranche _tranche)
        {
            // here the simulation is already run
            // m_PLD(,) is a 2D array (iPath,iPeriod) of CUMULATIVE LOSSES (the result of the sumulation
            //CreditPricing.CreditBLL.BLL.vb.dsSimulation.TTPriceHistDataTable dt = new CreditPricing.CreditBLL.BLL.vb.dsSimulation.TTPriceHistDataTable();
            List<TTPriceHist> dt = new List<TTPriceHist>();



            //dt.TableName = string.Format("{0}_{1}", dt.TableName, m_SimID);
            double dAttach = _tranche.PctAttach;
            double dWidth = _tranche.PctWidth;
            int iTrancheLets = _tranche.TrancheLets.Count;


            double dPV01 = 0;
            double dPVLOSS = 0;

            for (int ipath = 0; ipath < m_Draws; ipath++)
            {
                // begin processing a path/draw
                double pathPrem = 0;
                double prevPrem = dWidth;
                double sumPathPrem = 0;
                double sumPathLoss = 0;

                // accumulated cumloss across this path (horizontally I like to think) upto the iPeriod
                for (int idx = 0; idx <= iTrancheLets - 1; idx++)
                {
                    // cumulative for each tranchelet period...
                    pathPrem = Math.Min(Math.Max((dAttach + dWidth) - m_pld[ipath, idx], 0), dWidth);
                    // tranche absorbing cumulative losses from attachment point to detachment point
                    double periodDF = _tranche.TrancheLets[idx].DiscountFactor;
                    sumPathPrem += (pathPrem * periodDF);
                    // PV periodic premium
                    sumPathLoss += ((prevPrem - pathPrem) * periodDF);
                    // PV losses
                    prevPrem = pathPrem;
                }
                // tally the pv01 and pvLoss  
                dPV01 += .0001 * (1 / dWidth) * sumPathPrem;
                // scaled by 1 basis point (BP)
                dPVLOSS += (1 / dWidth) * sumPathLoss;
            }


            // take average
            double meanPV01 = dPV01 / m_Draws;
            double meanPVLOSS = dPVLOSS / m_Draws;


            // create a row and add it to our datatable
            //CreditPricing.CreditBLL.BLL.vb.dsSimulation.TTPriceHistRow row = dt.NewTTPriceHistRow();
            TTPriceHist row = new TTPriceHist();
            {
                row.TTNo = _tranche.TTNo;
                row.Portfolio = _tranche.Portfolio.Title;
                row.Notional = _tranche.TrancheNotional;
                row.Attach = dAttach;
                // reflect the change here....
                row.Width = _tranche.PctWidth;

                if (_tranche.TrancheUpFront != 0)
                {
                    row.UpfrontFee = (meanPVLOSS - meanPV01 * _tranche.SpreadBP) * 100;
                }
                else
                {
                    row.UpfrontFee = 0;
                }

                row.Premium = _tranche.SpreadBP;
                //.PV01 = meanPV01
                //.PVLoss = meanPVLOSS

                if (_tranche.TrancheUpFront != 0)
                {
                    row.ParPremium = _tranche.SpreadBP;
                }
                else
                {
                    row.ParPremium = meanPVLOSS / meanPV01;
                }
                row.Maturity = _tranche.TrancheLets[iTrancheLets - 1].FwdDate;
                row.CCY = _tranche.CCY;
                row.pWAS = _tranche.Portfolio.cdsWASpreadL * 100;
                row.pCredits = _tranche.Portfolio.Count;
                row.SimID = SimID; // m_SimID;
                row.SimElapsed = this.ElapsedTime.ToString();
                row.pCorrelation = m_Correlation;
                //SUPER NEW!!!! price this tranche
                try
                {
                    // USE parPremium as Discount Margin (this is not the same thing when spreads get very wide!!!)
                    //Dim bond As New OPICSBond(.TTNo, _tranche.EvalDate, .ParPremium)
                    // 
                    row.TranchePrice = (1 - meanPVLOSS) * 100;
                }
                catch 
                {
                    row.TranchePrice = 0;

                }
            }
            //dt.AddTTPriceHistRow(row);
            dt.Add(row);
            // here we are only sending one price row back
            return dt;
        }



        // call after class has been created New(...,,)
        // and after TimeToDefaultStudy has been performed
       // public dsOUT.TranchePriceDataTable TranchePrice_Rolldown(tkTranche _tranche)
        /// <summary>
        /// reprice the same tranche over successive qtrly maturity date rolling down the term structure
        /// </summary>
        /// <param name="_tranche"></param>
        /// <returns></returns>
        public List<TranchePrice> TranchePrice_Rolldown(tkTranche _tranche)
        {
            // here the simulation is already run
            // m_PLD(,) is a 2D array (iPath,iPeriod) of CUMULATIVE LOSSES (the result of the sumulation
            //

            //dsOUT.TranchePriceDataTable dt = new dsOUT.TranchePriceDataTable();
            List<TranchePrice> tpList = new List<TranchePrice>();

            double dAttach = _tranche.PctAttach;
            double dWidth = _tranche.PctWidth;
            int iTrancheLets = _tranche.TrancheLets.Count;

            // evaluate portfolio cumlosses up to each period
            for (int iPeriod = 0; iPeriod < iTrancheLets; iPeriod++)
            {
                double dPV01 = 0;
                double dPVLOSS = 0;

                for (int ipath = 0; ipath < m_Draws; ipath++)
                {
                    // begin processing a path/draw
                    double pathPrem = 0;
                    double prevPrem = dWidth;
                    double sumPathPrem = 0;
                    double sumPathLoss = 0;

                    // accumulated cumloss across this path (horizontally I like to think) upto the iPeriod
                    for (int idx = 0; idx < iPeriod; idx++)
                    {
                        // cumulative for each tranchelet period...
                        pathPrem = Math.Min(Math.Max((dAttach + dWidth) - m_pld[ipath, idx], 0), dWidth);
                        // tranche absorbing cumulative losses from attachment point to detachment point
                        double periodDF = _tranche.TrancheLets[idx].DiscountFactor;
                        sumPathPrem += (pathPrem * periodDF);
                        // PV periodic premium
                        sumPathLoss += ((prevPrem - pathPrem) * periodDF);
                        // PV losses
                        prevPrem = pathPrem;
                    }
                    // tally the pv01 and pvLoss  
                    dPV01 += .0001 * (1 / dWidth) * sumPathPrem;
                    // scaled by 1 basis point (BP)

                    dPVLOSS += (1 / dWidth) * sumPathLoss;
                }

                // take average
                double meanPV01 = dPV01 / m_Draws;
                double meanPVLOSS = dPVLOSS / m_Draws;

                TranchePrice tp = new TranchePrice();
                {
                    tp.TTNo = _tranche.TTNo;
                    tp.Portfolio = _tranche.Portfolio.Title;
                    tp.Name = _tranche.TrancheName;
                    tp.Notional = _tranche.TrancheNotional;
                    tp.Attach = _tranche.PctAttach;
                    tp.Width = _tranche.PctWidth;

                    if (_tranche.TrancheUpFront != 0)
                    {
                        tp.Fee = (meanPVLOSS - meanPV01 * _tranche.SpreadBP) * 100;
                    }
                    else
                    {
                        //.Fee = meanPVLOSS - meanPV01 * _tranche.SpreadBP
                        tp.Fee = 0;
                    }

                    tp.TranchePremium = _tranche.SpreadBP;
                    tp.PV01 = meanPV01;
                    tp.PVLoss = meanPVLOSS;

                    if (_tranche.TrancheUpFront != 0)
                    {
                        tp.ParPremium = _tranche.SpreadBP;
                    }
                    else
                    {
                        tp.ParPremium = (meanPV01==0) ? 0: meanPVLOSS / meanPV01;
                    }

                    tp.Maturity = _tranche.TrancheLets[iPeriod].FwdDate;
                    tp.CCY = _tranche.CCY;
                    tp.PortWAS = _tranche.Portfolio.cdsWASpreadL * 100;
                    tp.SimID = SimID; // m_SimID;
                    tp.SimDraws = m_Draws;
                    tp.Correlation = m_Correlation;
                    tp.SimElapsed = this.ElapsedTime.TotalSeconds;
                }
                //dt.AddTranchePriceRow(row);
                tpList.Add(tp);
            }
            return tpList;  // not sure we need a list of 
        }



        // call after class has been created New(...,,)
        // and after TimeToDefaultStudy has been performed
        // lets evaluatin the impact 10BP +/- of tranche subordination
        // NEW APR 2009 param _AttachDetachList pass in alternate tranches ie 3-7,7-10 to price up and return...

        //public dsOUT.TranchePriceDataTable TranchePrice_Subordination(tkTranche _tranche, string _attachdetachList)
        public List<TranchePrice> TranchePrice_Subordination(tkTranche _tranche, string _attachdetachList)
        {
            // here the simulation is already run
            // m_PLD(,) is a 2D array (iPath,iPeriod) of CUMULATIVE LOSSES (the result of the sumulation
            //
            //dsOUT.TranchePriceDataTable dt = new dsOUT.TranchePriceDataTable();
            List<TranchePrice> dt = new List<TranchePrice>();
            double dAttach = _tranche.PctAttach;
            double dWidth = _tranche.PctWidth;
            int iTrancheLets = _tranche.TrancheLets.Count;


            string[] strTrancheList = _attachdetachList.Split(',');

            // lets evaluatin the impact 10BP +/- of tranche subordination
            //dAttach = (dAttach - 0.001)


            // evaluate portfolio cumlosses up to each period
            for (int iTranche = 0; iTranche <= strTrancheList.Length; iTranche++)
            {
                // note we are going 1 past the max here on purpose!
                //note on first pass just take the current attach/width of tranche...
                if (iTranche > 0)
                {
                    try
                    {
                        string[] strTranche = strTrancheList[iTranche - 1].Split('-');
                        dAttach = Convert.ToDouble(strTranche[0]) / 100;
                        dWidth = (Convert.ToDouble(strTranche[1]) / 100) - dAttach;
                    }
                    catch 
                    {
                        dAttach = 0.07;
                        dWidth = 0.03;
                    }
                    finally
                    {
                        if (dWidth < 0 | dAttach < 0 | dAttach > 1)
                        {
                            dAttach = 0.07;
                            dWidth = 0.03;
                        }
                    }
                }

                double dPV01 = 0;
                double dPVLOSS = 0;

                for (int ipath = 0; ipath < m_Draws ; ipath++)
                {
                    // begin processing a path/draw
                    double pathPrem = 0;
                    double prevPrem = dWidth;
                    double sumPathPrem = 0;
                    double sumPathLoss = 0;

                    // accumulated cumloss across this path (horizontally I like to think) upto the iPeriod
                    for (int idx = 0; idx < iTrancheLets; idx++)
                    {
                        // cumulative for each tranchelet period...
                        pathPrem = Math.Min(Math.Max((dAttach + dWidth) - m_pld[ipath, idx], 0), dWidth);
                        // tranche absorbing cumulative losses from attachment point to detachment point
                        double periodDF = _tranche.TrancheLets[idx].DiscountFactor;
                        sumPathPrem += (pathPrem * periodDF);
                        // PV periodic premium
                        sumPathLoss += ((prevPrem - pathPrem) * periodDF);
                        // PV losses
                        prevPrem = pathPrem;
                    }
                    // tally the pv01 and pvLoss  
                    dPV01 += .0001 * (1 / dWidth) * sumPathPrem;
                    // scaled by 1 basis point (BP)
                    dPVLOSS += (1 / dWidth) * sumPathLoss;
                }

                // take average
                double meanPV01 = dPV01 / m_Draws;
                double meanPVLOSS = dPVLOSS / m_Draws;

                // create a row and add it to our datatable
                //dsOUT.TranchePriceRow row = dt.NewTranchePriceRow();
                TranchePrice row = new TranchePrice();
                {
                    row.TTNo = _tranche.TTNo;
                    row.Portfolio = _tranche.Portfolio.Title;
                    row.Name = _tranche.TrancheName;
                    row.Notional = _tranche.TrancheNotional;
                    row.Attach = dAttach;
                    // reflect the change here....
                    row.Width = dWidth;

                    if (_tranche.TrancheUpFront != 0)
                    {
                        row.Fee = (meanPVLOSS - meanPV01 * _tranche.SpreadBP) * 100;
                    }
                    else
                    {
                        row.Fee = 0;
                    }

                    row.TranchePremium = _tranche.SpreadBP;
                    row.PV01 = meanPV01;
                    row.PVLoss = meanPVLOSS;

                    if (_tranche.TrancheUpFront != 0)
                    {
                        row.ParPremium = _tranche.SpreadBP;
                    }
                    else
                    {
                        row.ParPremium = (meanPV01 == 0) ? 0 : meanPVLOSS / meanPV01;
                        //row.ParPremium = meanPVLOSS / meanPV01;
                    }

                    row.Maturity = _tranche.TrancheLets[iTrancheLets - 1].FwdDate;
                    row.CCY = _tranche.CCY;
                    row.PortWAS = _tranche.Portfolio.cdsWASpreadL * 100;
                    row.SimID = SimID; // m_SimID;
                    row.SimDraws = m_Draws;
                    row.Correlation = m_Correlation;
                    row.SimElapsed = this.ElapsedTime.TotalSeconds;
                }
                dt.Add(row);
            }
            return dt;
        }


        // call after class has been created New(...,,)
        // and after TimeToDefaultStudy has been performed
        // lets evaluatin the impact 10BP +/- of tranche subordination
        // NEW APR 2009 param _AttachDetachList pass in alternate tranches ie 3-7,7-10 to price up and return...
        // NEW MAR 2009 return slightly differnet output///  include modelprice and zeroYTM columns in output
        //public dsOUT.TranchePrice2DataTable TranchePrice_Subordination2(tkTranche _tranche, string _attachdetachList)
         public List<TranchePrice2> TranchePrice_Subordination2(tkTranche _tranche, string _attachdetachList)
        {
            // here the simulation is already run
            // m_PLD(,) is a 2D array (iPath,iPeriod) of CUMULATIVE LOSSES (the result of the sumulation
            //
            //dsOUT.TranchePrice2DataTable dt = new dsOUT.TranchePrice2DataTable();
            List<TranchePrice2> dt = new List<TranchePrice2>();
            double dAttach = _tranche.PctAttach;
            double dWidth = _tranche.PctWidth;
            int iTrancheLets = _tranche.TrancheLets.Count;


            string[] strTrancheList = _attachdetachList.Split(',');

            // lets evaluatin the impact 10BP +/- of tranche subordination
            //dAttach = (dAttach - 0.001)


            // evaluate portfolio cumlosses up to each period
            for (int iTranche = 0; iTranche <= strTrancheList.Length; iTranche++)
            {
                // note we are going 1 past the max here on purpose!
                //note on first pass just take the current attach/width of tranche...
                if (iTranche > 0)
                {
                    try
                    {
                        string[] strTranche = strTrancheList[iTranche - 1].Split('-');
                        dAttach = Convert.ToDouble(strTranche[0]) / 100;
                        dWidth = (Convert.ToDouble(strTranche[1]) / 100) - dAttach;
                    }
                    catch 
                    {
                        dAttach = 0.07;
                        dWidth = 0.03;
                    }
                    finally
                    {
                        if (dWidth < 0 | dAttach < 0 | dAttach > 1)
                        {
                            dAttach = 0.07;
                            dWidth = 0.03;
                        }
                    }
                }

                double dPV01 = 0;
                double dPVLOSS = 0;

                for (int ipath = 0; ipath <= m_Draws - 1; ipath++)
                {
                    // begin processing a path/draw
                    double pathPrem = 0;
                    double prevPrem = dWidth;
                    double sumPathPrem = 0;
                    double sumPathLoss = 0;

                    // accumulated cumloss across this path (horizontally I like to think) upto the iPeriod
                    for (int idx = 0; idx <= iTrancheLets - 1; idx++)
                    {
                        // cumulative for each tranchelet period...
                        pathPrem = Math.Min(Math.Max((dAttach + dWidth) - m_pld[ipath, idx], 0), dWidth);
                        // tranche absorbing cumulative losses from attachment point to detachment point
                        double periodDF = _tranche.TrancheLets[idx].DiscountFactor;
                        sumPathPrem += (pathPrem * periodDF);
                        // PV periodic premium
                        sumPathLoss += ((prevPrem - pathPrem) * periodDF);
                        // PV losses
                        prevPrem = pathPrem;
                    }
                    // tally the pv01 and pvLoss  
                    dPV01 += .0001 * (1 / dWidth) * sumPathPrem;
                    // scaled by 1 basis point (BP)
                    dPVLOSS += (1 / dWidth) * sumPathLoss;
                }

                // take average
                double meanPV01 = dPV01 / m_Draws;
                double meanPVLOSS = dPVLOSS / m_Draws;

                // create a row and add it to our datatable
                //dsOUT.TranchePrice2Row row = dt.NewTranchePrice2Row();
                TranchePrice2 row = new TranchePrice2();
                {
                    row.TTNo = _tranche.TTNo;
                    row.Portfolio = _tranche.Portfolio.Title;
                    row.Name = _tranche.TrancheName;
                    row.Notional = _tranche.TrancheNotional;
                    row.Attach = dAttach;
                    // reflect the change here....
                    row.Width = dWidth;

                    if (_tranche.TrancheUpFront != 0)
                    {
                        row.Fee = (meanPVLOSS - meanPV01 * _tranche.SpreadBP) * 100;
                    }
                    else
                    {
                        row.Fee = 0;
                    }

                    row.TranchePremium = _tranche.SpreadBP;
                    row.PV01 = meanPV01;
                    row.PVLoss = meanPVLOSS;
                    // NEW MAY09
                    row.ModelPrice = 1 - meanPVLOSS;

                    if (_tranche.TrancheUpFront != 0)
                    {
                        row.ParPremium = _tranche.SpreadBP;
                    }
                    else
                    {
                        //row.ParPremium = meanPVLOSS / meanPV01;
                        row.ParPremium = (meanPV01 == 0) ? 0 : meanPVLOSS / meanPV01;
                    }

                    row.Maturity = _tranche.TrancheLets[iTrancheLets - 1].FwdDate;
                    row.CCY = _tranche.CCY;
                    row.PortWAS = _tranche.Portfolio.cdsWASpreadL * 100;
                    row.SimID = SimID; // m_SimID;
                    row.SimDraws = m_Draws;
                    row.Correlation = m_Correlation;
                    row.SimElapsed = this.ElapsedTime;

                    // NEW MAY09
                    row.ZeroYTM = Math.Pow((1 / row.ModelPrice), (1 / ((double)row.Maturity.Subtract(_tranche.EvalDate).Days / 365))) - 1;
                }
                dt.Add(row);
            }

            return dt;
        }



        //calculation of Single Tranche pricing
        public void TimeToDefaultNEW(tkTranche _tranche, int nDraws, double dCorr)
        {
            // the Tranche has it's portfolio embedded for ease...
            m_StartTime = DateTime.Now;

            // ensure recovery is not out of bounds!
            //If _Recovery > 1 Or _Recovery < 0 Then
            //    _Recovery = 0.4
            //End If
            // same for correlation
            if (dCorr > 1 | dCorr < 0)
            {
                dCorr = 0.2;
            }


            // this member is the prob loss distribution (marginal)
            // ERROR: Not supported in C#: ReDimStatement


            mcPort2[] arPort = new mcPort2[_tranche.Portfolio.Count];
            // NEW mcPORT2 contains a dynamic array of CLP's (per tranchePeriod)
            // create array of portfolio data for speed up...
            int idxtrade = 0;
            int i = 0;
            foreach (tkCDS cds in _tranche.Portfolio)
            {
                //If _FlatSpread <> 0 Then
                //    If Not cds.IsFixedRR Then
                //        cds.RecoveryOnDefault = _Recovery
                //    End If
                //    cds.EvalSpread = _FlatSpread / 100
                //End If
                {
                    // ERROR: Not supported in C#: ReDimStatement

                    arPort[idxtrade].Notional = cds.NotionalAmt;
                    // note trades here are entered as Portfolio %Weights
                    // lets model the RR
                    arPort[idxtrade].RRFixed = cds.IsFixedRR;
                    if (arPort[idxtrade].RRFixed)
                    {
                        arPort[idxtrade].RR = cds.RecoveryOnDefault;
                    }
                    else
                    {
                        try
                        {
                            // used MARKIT recovery
                            arPort[idxtrade].RR = cds.CDSCurve.Recovery;
                        }
                        catch 
                        {
                            // if curve not assigned!  (not so rare)
                            arPort[idxtrade].RR = 0.4;

                        }
                    }


                    arPort[idxtrade].sdRR = (double)0.05;
                    // 5% std. dev. of recoveries around the mean...

                    //If cds.IsFixedRR Then
                    //Else
                    //    .RR = _Recovery
                    //End If

                    i = 0;
                    foreach (tkTrancheLet tlet in _tranche.TrancheLets)
                    {
                        // note: hazardrate aka cleanspread is not a function of RecoveryOnDefault (we generally assume 40% RecoveryPricing) so CumLoss is not effected by FixedRecovery in the model
                        //If _FlatSpread <> 0 Then
                        //.CDP(i) = cds.CLP_CC(tlet.FwdDate) ' note cum loss probability with flat spread and continuous compounding
                        //Else
                        arPort[idxtrade].CDP[i] = cds.CLP_CC_Curve(tlet.FwdDate);
                        // note cum loss probability with term structure of CDS spread and continuous compounding
                        //End If
                        i += 1;
                    }
                }
                idxtrade += 1;
            }

            // class to create 2d array of joint default distributions
            CorrProbDefDist tkdist = new CorrProbDefDist(nDraws, _tranche.Portfolio.Count, dCorr);
            double[,] rndAssets = tkdist.JointProbDefDist;
            double[] rndMarket = tkdist.MarketRands;

            int iAssetCount = arPort.Length;
            // start your engines...  iPath looping through each monte-carlo draw
            for (int iPath = 0; iPath < nDraws; iPath++)
            {
                // (each draw ie 100,000)
                for (int iAsset = 0; iAsset < iAssetCount; iAsset++)
                {
                    //(each trade ie 150)
                    for (int iPeriod = 0; iPeriod < _tranche.TrancheLets.Count; iPeriod++)
                    {
                        if (rndAssets[iPath, iPeriod] > 1 - arPort[iAsset].CDP[iPeriod])
                        {
                            // at this point, a  DEFAULT HAS OCCURRED the asset didn't survive this period...
                            if (arPort[iAsset].RRFixed)
                            {
                                m_pld[iPath, iPeriod] += arPort[iAsset].Notional * (1 - arPort[iAsset].RR);
                            }
                            else
                            {
                                // STOCHASTIC RECOVERIES
                                // if recoveries are not fixed then RR is Systematic Rand # of stdDevs from the mean)
                                // recoveries will be LESS in real negative market samples (R1 RAND is the driver for this)
                                double thisRR = Math.Min(Math.Max(arPort[iAsset].RR + (rndMarket[iPath] * arPort[iAsset].sdRR), 0), 1);
                                m_pld[iPath, iPeriod] += arPort[iAsset].Notional * (1 - thisRR);
                            }
                            // this is important, only default ONCE per credit per portfolio draw...
                            break; // TODO: might not be correct. Was : Exit For
                        }
                        // trade cashflows/tranchelets
                    }
                }
                // portfolio cds trades

                // we are at the end of a draw...(all portfolio periodic contingent marginal losses are distributed 
                // and  all trades have been simulated and now this 'ROW' contains
                // portfolio losses per period (iPeriod), contingent upon surviving until (iPeriod-1). known as contingent marginal losses
                // IMPORTANT!, convert this paths marginal losses to cum losses (just summing across)
                double pathCumLoss = 0;
                for (int k = 0; k < m_pld.GetLength(1); k++)
                {
                    pathCumLoss += m_pld[iPath, k];
                    // sum the losses to create cumulative losses in time for this path
                    m_pld[iPath, k] = pathCumLoss;
                }
            }
            // simulation draws
            m_EndTime = DateTime.Now;
        }


        //calculation of Single Tranche pricing
        public void TimeToDefault(tkTranche _tranche, int nDraws, double dCorr)
        {
            //, ByVal _Recovery As Double, ByVal _FlatSpread As Double)
            // the Tranche has it's portfolio embedded for ease...
            m_StartTime = DateTime.Now;

            if (dCorr > 1 || dCorr < 0)
            {
                dCorr = 0.2;
            }

            m_pld = new double[nDraws, _tranche.Portfolio.Count];

            mcPort2[] arPort = new mcPort2[_tranche.Portfolio.Count];
            // NEWer mcPORT2 contains a dynamic array of CLP's (per tranchePeriod) cumulative loss probabilities

            int idxtrade = 0;
            int i = 0;
            foreach (tkCDS cds in _tranche.Portfolio)
            {
                {
                    arPort[idxtrade].CDP = new double[_tranche.TrancheLets.Count];
                    arPort[idxtrade].Notional = cds.NotionalAmt;
                    if (cds.IsFixedRR)
                    {
                        arPort[idxtrade].RR = cds.RecoveryOnDefault;
                    }
                    else
                    {
                        // use MARKIT Recovery
                        try
                        {
                            arPort[idxtrade].RR = cds.CDSCurve.Recovery;
                        }
                        catch 
                        {
                            arPort[idxtrade].RR = 0.4;
                        }
                    }
                    arPort[idxtrade].RRFixed = cds.IsFixedRR;
                    arPort[idxtrade].sdRR = (double)0.05;
                    // 5% std. dev. of recoveries around the mean...


                    i = 0;
                    foreach (tkTrancheLet tlet in _tranche.TrancheLets)
                    {
                        // store the cum. loss prob. continuously compounded as per the trans
                        arPort[idxtrade].CDP[i] = cds.CLP_CC_Curve(tlet.FwdDate);
                        // note cum loss probability with term structure of CDS spread and continuous compounding
                        //End If
                        i += 1;
                    }
                }
                idxtrade += 1;
            }

            int assetCount = arPort.Length;
            int trancheLetCount = _tranche.TrancheLets.Count;



            // class to create 2d array of joint default distributions
            CorrProbDefDist tkdist = new CorrProbDefDist(nDraws, _tranche.Portfolio.Count, dCorr);
            double[,] rndAssets = tkdist.JointProbDefDist;
            double[] rndMarket = tkdist.MarketRands;


            // start your engines...  iPath looping through each monte-carlo draw
            for (int iPath = 0; iPath < nDraws; iPath++)
            {
                // (each draw ie 100,000)
                for (int iAsset = 0; iAsset < assetCount; iAsset++)
                {
                    //(each trade ie 150)
                    for (int iPeriod = 0; iPeriod < trancheLetCount; iPeriod++)
                    {
                        if (rndAssets[iPath, iPeriod] > 1 - arPort[iAsset].CDP[iPeriod])
                        {
                            // at this point, a  DEFAULT HAS OCCURRED the asset didn't survive this period...
                            if (arPort[iAsset].RRFixed)
                            {
                                m_pld[iPath, iPeriod] += arPort[iAsset].Notional * (1 - arPort[iAsset].RR);
                            }
                            else
                            {
                                // STOCHASTIC RECOVERIES
                                // if recoveries are not fixed then RR is Systematic Rand # of stdDevs from the mean)
                                // recoveries will be LESS in real negative market samples (R1 RAND is the driver for this)
                                double thisRR = Math.Min(Math.Max((arPort[iAsset].RR + (rndMarket[iPath] * arPort[iAsset].sdRR)), 0), 1);
                                m_pld[iPath, iPeriod] += arPort[iAsset].Notional * (1 - thisRR);
                            }
                            // this is important, only default ONCE per credit per portfolio draw...
                            break; 
                        }
                        // trade cashflows/tranchelets
                    }
                }

                // portfolio cds trades
                // we are at the end of a draw...(all portfolio periodic contingent marginal losses are distributed 
                // and  all trades have been simulated and now this 'ROW' contains
                // portfolio losses per period (iPeriod), contingent upon surviving until (iPeriod-1). known as contingent marginal losses
                // IMPORTANT!, convert this paths marginal losses to cum losses (just summing across)
                double pathCumLoss = 0;
                double colCount = m_pld.GetLength(1);
                //for (int k = 0; k < m_pld.GetLength(1); k++)
                for (int k = 0; k < colCount; k++)
                {
                    pathCumLoss += m_pld[iPath, k];
                    // sum the losses to create cumulative losses in time for this path
                    m_pld[iPath, k] = pathCumLoss;
                }
            }
            // simulation draws
            m_EndTime = DateTime.Now;
        }

        //calculation of Single Tranche pricing
        //Function TimeToDefaultSAVE(ByVal tranche As tkTranche, ByVal nDraws As Integer, ByVal dCorr As Double, ByVal _Recovery As Double, ByVal _FlatSpread As Double) As tkTranche
        //    m_StartTime = Now

        //    ' ensure recovery is not out of bounds!
        //    If _Recovery > 1 Or _Recovery < 0 Then
        //        _Recovery = 0.4
        //    End If
        //    ' same for correlation
        //    If dCorr > 1 Or dCorr < 0 Then
        //        dCorr = 0.2
        //    End If


        //    Dim tr_a As Double = tranche.PctAttach
        //    Dim tr_w As Double = tranche.PctWidth

        //    Dim rnd As New Random ' 32 bit double random number generator class, quite suitable for this task
        //    ' see microsoft docs http://msdn2.microsoft.com/en-US/library/system.random.aspx
        //    Dim X As Double = 0 ' x is the Credit index of this MC Draw
        //    Dim H As Double = 0 'H is the percentile rank of the Cumulative Loss Probablity for each CDS/tranchelet cashflow

        //    Dim randSYS As Double = 0  ' random var (systematic/market risk)
        //    Dim randIDIO As Double = 0  ' random var (idiosyncratic risk)


        //    ' this returns a 2d array of double values containing the CLP term structure for our portfolio

        //    ' this member is the prob loss distribution (marginal)
        //    ReDim m_pld(nDraws - 1, tranche.TrancheLets.Count - 1)

        //    ' for tranche pricing...
        //    ReDim m_PV01(nDraws - 1)
        //    ReDim m_PVLOSS(nDraws - 1)


        //    'initialize array to zeros (not sure if this is needed)
        //    For iRow As Integer = 0 To m_pld.GetLength(0) - 1
        //        For iCol As Integer = 0 To m_pld.GetLength(1) - 1
        //            m_pld(iRow, iCol) = 0
        //        Next
        //    Next

        //    Dim arPort(tranche.Portfolio.Count - 1) As mcPort2  ' NEW mcPORT2 contains a dynamic array of CLP's (per tranchePeriod)
        //    ' create array of portfolio data for speed up...
        //    Dim idxtrade As Integer = 0
        //    Dim i As Integer = 0
        //    For Each cds As mxCDS In tranche.Portfolio
        //        If _FlatSpread <> 0 Then
        //            If Not cds.IsFixedRR Then
        //                cds.RecoveryOnDefault = _Recovery
        //            End If
        //            cds.EvalSpread = _FlatSpread / 100
        //        End If


        //        With arPort(idxtrade)
        //            .Notional = cds.NotionalAmt ' note trades here are entered as Portfolio %Weights
        //            ReDim .CDP(tranche.TrancheLets.Count - 1)
        //            ' get the tranche
        //            i = 0
        //            For Each tlet As tkTrancheLet In tranche.TrancheLets
        //                ' note: hazardrate aka cleanspread is not a function of RecoveryOnDefault (we generally assume 40% RecoveryPricing) so CumLoss is not effected by FixedRecovery in the model
        //                .CDP(i) = cds.CLP_CC(tlet.FwdDate) ' note cum loss probability with flat spread and continuous compounding
        //                i += 1
        //            Next
        //            .Maturity = cds.dtMaturity

        //            ' lets model the RR
        //            If cds.IsFixedRR Then
        //                .RR = cds.RecoveryOnDefault
        //            Else
        //                .RR = _Recovery
        //            End If
        //            .RRFixed = cds.IsFixedRR
        //            .sdRR = CType(0.08, Double) ' 5% std. dev. of recoveries around the mean...
        //        End With
        //        idxtrade += 1
        //    Next



        //    ' start your engines...  iPath looping through each monte-carlo draw
        //    For iPath As Integer = 0 To nDraws - 1  ' (each draw ie 100,000)
        //        randSYS = NormSInv(rnd.NextDouble)  '  is the MARKET/ SYSTEMATIC VARIABLE...
        //        'randSYS = rnd.NextDouble  '  is the MARKET/ SYSTEMATIC VARIABLE...

        //        ' for each monte-carlo draw, we simulated each trades loss over the tranche premium legs...
        //        For iAsset As Integer = 0 To arPort.Length - 1  '(each trade ie 150)
        //            randIDIO = NormSInv(rnd.NextDouble)  ' idiosyntratic RANDOM VARIABLE a draw for each FIRM/crediit

        //            ' double Normal Inverse Gaussian with 1 factor 'equicorrelation'
        //            X = dCorr * randSYS + randIDIO  ' standard 1 factor copula  ' this seems to better mimick standard tranches with (BASE CORRELATION)
        //            'X = (Sqrt(dCorr) * randSYS) + (Sqrt(1 - dCorr) * randIDIO)  ' see STRUCTURED FINANCE MODELING (Evan Tick), see page 282 Ex. 7.12
        //            'X = (dCorr * randSYS) + (Sqrt(1 - dCorr ^ 2) * randIDIO) ' cholesky decomposition via market (randSYS) and firm specific (randIDIO) ' this seems to better price a flat correlation (a fatter tail...)
        //            X = NormSDist(X)

        //            For iPeriod As Integer = 0 To tranche.TrancheLets.Count - 1
        //                H = 1 - arPort(iAsset).CDP(iPeriod) ' cumulative survival probabiltiy by period 
        //                If X > H Then  ' at this point, a  DEFAULT HAS OCCURRED the asset didn't survive this period...
        //                    If arPort(iAsset).RRFixed Then
        //                        m_pld(iPath, iPeriod) += arPort(iAsset).Notional * (1 - arPort(iAsset).RR)
        //                    Else
        //                        ' STOCHASTIC RECOVERIES
        //                        ' if recoveries are not fixed then RR is Systematic Rand # of stdDevs from the mean)
        //                        ' recoveries will be LESS in real negative market samples (R1 RAND is the driver for this)
        //                        ' ensure RR >=0 and <=1
        //                        Dim thisRR As Double = (arPort(iAsset).RR + (randSYS * arPort(iAsset).sdRR))
        //                        If thisRR > 1 Then thisRR = 1
        //                        If thisRR < 0 Then thisRR = 0
        //                        m_pld(iPath, iPeriod) += arPort(iAsset).Notional * (1 - thisRR)
        //                    End If
        //                    Exit For  ' this is important, only default ONCE per credit per portfolio draw...
        //                End If
        //            Next  ' trade cashflows/tranchelets
        //        Next   ' portfolio cds trades

        //        ' we are at the end of a draw...(all portfolio periodic contingent marginal losses are distributed 
        //        ' and  all trades have been simulated and now this 'ROW' contains
        //        ' portfolio losses per period (iPeriod), contingent upon surviving until (iPeriod-1). 
        //        ' otherwise known as contingent marginal losses

        //        ' IMPORTANT!, convert this paths marginal losses to cum losses (just summing them)
        //        Dim pathCumLoss As Double = 0
        //        Dim pathPrem As Double = 0
        //        Dim prevPrem As Double = tr_w
        //        Dim sumPathPrem As Double = 0
        //        Dim sumPathLoss As Double = 0

        //        For k As Integer = 0 To m_pld.GetLength(1) - 1
        //            pathCumLoss += m_pld(iPath, k)
        //            m_pld(iPath, k) = pathCumLoss  ' sum the losses to create cumulative losses in time for this path

        //            pathPrem = Min(Max(0, (tr_a + tr_w) - pathCumLoss), tr_w)      ' tranche absorbing cumulative losses from attachment point to detachment point
        //            Dim periodDF As Double = tranche.TrancheLets(k).DiscountFactor
        //            sumPathPrem += (pathPrem * periodDF)  ' PV periodic premium
        //            sumPathLoss += ((prevPrem - pathPrem) * periodDF) ' PV losses
        //            prevPrem = pathPrem
        //        Next

        //        m_PV01(iPath) = (1 / 10000) * (1 / tr_w) * sumPathPrem ' scaled by 1 basis point (BP)
        //        m_PVLOSS(iPath) = (1 / tr_w) * sumPathLoss

        //        ' update the completion bar in the user interface/multi-threaded
        //        'Try
        //        '    DivRem(iPath, CInt(nDraws / 100), iResult)
        //        'Catch ex As Exception
        //        '    iResult = 0
        //        'End Try

        //        'If iResult = 0 Then
        //        '    worker.ReportProgress(CInt((iPath / nDraws) * 100), tranche.Portfolio.Title + " Simulation in progress...")
        //        'End If
        //    Next iPath ' simulation draws

        //    ' calculate exploss and expPV01  (the mean)
        //    Dim sumPV01 As Double = 0
        //    Dim sumPVLOSS As Double = 0
        //    For iPath As Integer = 0 To nDraws - 1
        //        sumPV01 += m_PV01(iPath)
        //        sumPVLOSS += m_PVLOSS(iPath)
        //    Next

        //    tranche.PV01 = sumPV01 / nDraws
        //    tranche.PVLOSS = sumPVLOSS / nDraws

        //    ' here we've completed all calculations and are left with a 2D array (MATRIX)  (rows,cols) ie (100000,20) 
        //    ' containing portfolio losses per period...
        //    Dim idxTrancheLet As Integer = 0

        //    For Each tranchelet As tkTrancheLet In tranche.TrancheLets
        //        Dim dblTotal As Double = 0
        //        For iPath As Integer = 0 To nDraws - 1
        //            dblTotal += m_pld(iPath, idxTrancheLet)
        //        Next
        //        idxTrancheLet += 1

        //        Dim dblTLetEL As Double = dblTotal / nDraws

        //        tranchelet.ExpectedLoss = dblTLetEL
        //    Next  ' tranchelet
        //    m_EndTime = Now
        //    Return tranche
        //    ' go F yurselves...
        //End Function


        //portfolio list calculations
        //Function MonteCarloSim2(ByVal tranche As tkTranche, ByVal nDraws As Integer, ByVal dCorr As Double, ByVal worker As BackgroundWorker, ByVal e As DoWorkEventArgs) As tkTranche
        //    Dim dtStart As DateTime = System.DateTime.Now
        //    ' contains the discrete cds cashflows to be used for

        //    ' the Tranche has it's portfolio embedded for ease...
        //    Dim p As mxCDSCollection = tranche.Portfolio
        //    Dim arMCOut(nDraws - 1) As mcDraw    ' array of monte carlo output (results of each draw)
        //    Dim i As Integer = 0
        //    'Dim idxTrade As Integer = 0

        //    'Dim dTPN As Double = 0 ' total portfolio notional

        //    Dim tr_a As Double = tranche.PctAttach
        //    Dim tr_w As Double = tranche.PctWidth

        //    Dim idxTrancheLet As Integer = 0
        //    For Each tranchelet As tkTrancheLet In tranche.TrancheLets
        //        worker.ReportProgress((idxTrancheLet / tranche.TrancheLets.Count * 100), p.Title + " Monte-Carlo Simulation in progress...")
        //        Dim arPort(p.Count - 1) As mcPort
        //        Dim idxtrade As Integer = 0
        //        For Each cds As mxCDS In p
        //            With arPort(idxtrade)
        //                .Notional = cds.NotionalAmt ' note trades here are entered as Portfolio Weights
        //                ' we are looking deep into the individual CDS Cashflow here 
        //                .CDP = cds.Cashflows(idxTrancheLet).rnpCLP
        //                .Maturity = cds.dtMaturity
        //                .RR = cds.RecoveryOnDefault
        //                .RRFixed = cds.IsFixedRR
        //                .sdRR = CType(0.05, Double) ' 5% std. dev. of recoveries around the mean...
        //            End With
        //            idxtrade += 1
        //        Next

        //        idxTrancheLet += 1


        //        Dim rnd As New Random ' 32 bit double random number generator class, quite suitable for this task
        //        ' see microsoft docs http://msdn2.microsoft.com/en-US/library/system.random.aspx
        //        Dim j As Integer = 0 'mc draw loop counter
        //        Dim X As Double = 0 ' x is the Credit index of this MC Draw
        //        Dim H As Double = 0 'H is the percentile rank of the Cumulative Loss Probablity for each CDS
        //        'Dim P As Double = 0 ' probabilty from joint distribution
        //        Dim drawLGD As Double = 0 'draw loss (i)
        //        Dim dLossTotal As Double = 0
        //        Dim dMaxLoss As Double = 0 'max draw loss for our distribution
        //        Dim iLossCount As Integer = 0 ' lets also count the default events...
        //        Dim iMaxLossCount As Integer = 0
        //        Dim R2 As Double = 0  ' random var (idiosyncratic risk)
        //        Dim R1 As Double = 0  ' random var (systematic/market risk)
        //        'Dim iResult As Integer  ' progress tracker
        //        Dim dblCLP As Double    ' cumulative loss probability of (i) credit (gotten from RNP of CDS spread calculation)

        //        For j = 0 To nDraws - 1
        //            ' R1 is the systematic random variable (same for entire portfolio)
        //            R1 = NormSINV(rnd.NextDouble)

        //            drawLGD = 0  ' double portfolio loss
        //            iLossCount = 0  ' integer #loss count
        //            Dim dWARR As Double = 0  ' for each draw, tally Wtd Ave Recovery Rate
        //            Dim drawPortLossNotl As Double = 0 ' needed for WARR (Total notl of just the losses)

        //            For i = 0 To UBound(arPort)
        //                ' X is credit Index of this draw  R1 is random representing a systematic market
        //                ' R2 is the idiosyncratic random variable that is unique for each credit in the portfolio array: arPORT
        //                R2 = NormSINV(rnd.NextDouble)
        //                X = (dCorr * R1) + Sqrt(1 - dCorr ^ 2) * R2
        //                'X = (Sqrt(dCorr) * R1) + (Sqrt(1 - dCorr) * R2)
        //                dblCLP = arPort(i).CDP ' cummulative loss probablility
        //                H = NormSINV(dblCLP)    ' default threshold/barrier...

        //                'P = NormSDIST((H - dCorr * R1) / Sqrt(1 - dCorr ^ 2))
        //                ' our credit index is less than our default threshold...
        //                If X < H Then  ' A DEFAULT HAS OCCURRED IN OUR SIMULATION...
        //                    'p(i).SimDefaulted = True  ' this credit has defaulted in simulation

        //                    drawPortLossNotl += arPort(i).Notional
        //                    If arPort(i).RRFixed Then
        //                        ' if recoveries are fixed then LGD = 1 - RR
        //                        drawLGD += arPort(i).Notional * (1 - arPort(i).RR)
        //                        dWARR += arPort(i).Notional * arPort(i).RR
        //                    Else
        //                        ' if recoveries are not fixed then RR is Systematic Rand # of stdDevs from the mean)
        //                        ' recoveries will be LESS in real negative market samples (R1 RAND is the driver for this)
        //                        'dLossNotl += arPort(i).Notional
        //                        ' ensure RR >=0 and <=1
        //                        Dim thisRR As Double = (arPort(i).RR + (R1 * arPort(i).sdRR))
        //                        If thisRR > 1 Then thisRR = 1
        //                        If thisRR < 0 Then thisRR = 0
        //                        drawLGD += arPort(i).Notional * (1 - thisRR)
        //                        dWARR += arPort(i).Notional * thisRR
        //                    End If
        //                    iLossCount += 1
        //                End If
        //            Next i  ' looping thru portfolio items...

        //            ' done with all credits in this draw tally results in our array of draws
        //            arMCOut(j).nDefaults = iLossCount ' this is the # of losses in this draw
        //            arMCOut(j).dblLGD = drawLGD  ' pct scale no longer needed.../ dTPN  ' pct of total portfolio notl.


        //            ' here is the Tranche Loss Function applied to the drawLGD
        //            If drawLGD < tr_a Then
        //                arMCOut(j).dblTrancheLGD = 0
        //            ElseIf drawLGD < (tr_a + tr_w) Then
        //                arMCOut(j).dblTrancheLGD = (drawLGD - tr_a) / tr_w
        //            Else
        //                arMCOut(j).dblTrancheLGD = 1
        //            End If


        //            dLossTotal += arMCOut(j).dblTrancheLGD

        //        Next j ' monte carlo draws


        //        ' all draws are done for this tranchelet... now summarize results
        //        ' as we've tallied things up nicely, the expectedloss or Mean is just the average tranche loss
        //        tranchelet.ExpectedLoss = dLossTotal / nDraws

        //    Next  ' tranchelet

        //    Return tranche

        //End Function


        //'calculation of Single Tranche pricing
        //Function MonteCarloSim3(ByVal tranche As tkTranche, ByVal nDraws As Integer, ByVal dCorr As Double, ByVal worker As BackgroundWorker, ByVal e As DoWorkEventArgs) As tkTranche
        //    ' the Tranche has it's portfolio embedded for ease...
        //    ' loop counters 
        //    'Dim iPath As Integer = 0    ' monte carlo simulation counter
        //    'Dim iAsset As Integer = 0    ' portfolio trade counter
        //    'Dim iPeriod As Integer = 0    ' trade cashflow/tranche date counter
        //    Dim iResult As Integer

        //    Dim tr_a As Double = tranche.PctAttach
        //    Dim tr_w As Double = tranche.PctWidth
        //    Dim rnd As New Random ' 32 bit double random number generator class, quite suitable for this task
        //    ' see microsoft docs http://msdn2.microsoft.com/en-US/library/system.random.aspx
        //    Dim X As Double = 0 ' x is the Credit index of this MC Draw
        //    Dim H As Double = 0 'H is the percentile rank of the Cumulative Loss Probablity for each CDS/tranchelet cashflow

        //    'Dim R1() As Double   ' random var (systematic/market risk)
        //    Dim R1 As Double = 0  ' random var (systematic/market risk)
        //    Dim R2 As Double = 0  ' random var (idiosyncratic risk)


        //    ' this returns a 2d array of double values containing the CLP term structure for our portfolio
        //    Dim matrixOUT As Double(,)
        //    ReDim matrixOUT(nDraws - 1, tranche.TrancheLets.Count - 1)

        //    'initialize

        //    For iRow As Integer = 0 To matrixOUT.GetLength(0) - 1
        //        For iCol As Integer = 0 To matrixOUT.GetLength(1) - 1
        //            matrixOUT(iRow, iCol) = 0
        //        Next
        //    Next



        //    Dim arPort(tranche.Portfolio.Count - 1) As mcPort2  ' NEW mcPORT2 contains a dynamic array of CLP's (per cashflow)
        //    ' create array of portfolio data for speed up...

        //    Dim idxtrade As Integer = 0
        //    Dim i As Integer = 0
        //    For Each cds As mxCDS In tranche.Portfolio
        //        With arPort(idxtrade)
        //            .Notional = cds.NotionalAmt ' note trades here are entered as Portfolio Weights

        //            ' looking deep into the individual CDS Cashflow here 
        //            ReDim .CDP(tranche.TrancheLets.Count - 1)
        //            ' get the tranche

        //            i = 0
        //            For Each tlet As tkTrancheLet In tranche.TrancheLets
        //                '.CDP(k) = cf.rnpCLP
        //                ' new new new... using continuous compounding instead of discrete A/360 basis
        //                Dim dblCumLossProb As Double = cds.CLP_CC(tlet.FwdDate)
        //                .CDP(i) = dblCumLossProb
        //                i += 1
        //            Next

        //            .Maturity = cds.dtMaturity
        //            .RR = cds.RecoveryOnDefault
        //            .RRFixed = cds.IsFixedRR
        //            .sdRR = CType(0.05, Double) ' 5% std. dev. of recoveries around the mean...
        //        End With
        //        idxtrade += 1
        //    Next


        //    'ReDim R1(matrixCLP.GetLength(1) - 1)
        //    ' i,j,k  i is Draw counter, j is deal/cds counter, k is tranchlet counter

        //    worker.ReportProgress(0, tranche.Portfolio.Title + " Simulation in progress...")

        //    ' start your engines...  iPath looping through each monte-carlo draw
        //    For iPath As Integer = 0 To nDraws - 1  ' (each draw ie 100,000)
        //        R1 = NormSINV(rnd.NextDouble)  ' R1 is the SYSTEMATIC VARIABLE...

        //        ' for each monte-carlo draw, we simulated each trades loss over the tranche premium legs...
        //        ' j looping thru each trade in out synthetic portoflio...
        //        For iAsset As Integer = 0 To arPort.Length - 1  '(each trade ie 150)
        //            R2 = NormSINV(rnd.NextDouble)  ' idiosyntratic RANDOM VARIABLE
        //            X = (Sqrt(dCorr) * R1) + (Sqrt(1 - dCorr) * R2)
        //            'X = dCorr * R1 + Sqrt(1 - dCorr ^ 2) * R2

        //            'k looping thru each tranchLet...
        //            For iPeriod As Integer = 0 To tranche.TrancheLets.Count - 1 'arPort(j).CDP.Length - 1 ' (each cashflow in the tranche premium legs)
        //                H = NormSINV(arPort(iAsset).CDP(iPeriod)) 'arPort(j).CDP(k))    ' default threshold/barrier...
        //                If X < H Then  ' at this point, a  DEFAULT HAS OCCURRED IN OUR SIMULATION...
        //                    'If arPort(j).RRFixed Then
        //                    ' if recoveries are fixed then LGD = 1 - RR
        //                    'arDrawOut(k) += arPort(j).Notional * (1 - arPort(j).RR)
        //                    matrixOUT(iPath, iPeriod) += arPort(iAsset).Notional * (1 - arPort(iAsset).RR)

        //                    'Else
        //                    ' if recoveries are not fixed then RR is Systematic Rand # of stdDevs from the mean)
        //                    ' recoveries will be LESS in real negative market samples (R1 RAND is the driver for this)
        //                    ' ensure RR >=0 and <=1
        //                    'Dim thisRR As Double = (arPort(j).RR + (R1 * arPort(j).sdRR))
        //                    'If thisRR > 1 Then thisRR = 1
        //                    'If thisRR < 0 Then thisRR = 0
        //                    'matrixOUT(i, k) += arPort(j).Notional * (1 - thisRR)
        //                    'End If
        //                    Exit For  ' this is important, only default ONCE per credit per portfolio draw...
        //                End If
        //            Next  ' trade cashflows/tranchelets
        //        Next   ' portfolio cds trades


        //        ' we are at the end of a draw... all trades have been simulated and now this 'ROW' contains
        //        ' portfolio losses per tranche period. Note: portfolio losses are stated as a percentage, here we apply the Tranche Loss Function (Tranche Attachment + Tranche Width)
        //        ' in laymans terms: we're just replacing portfolio losses with tranche losses...
        //        'For k As Integer = 0 To matrixOUT.GetLength(1) - 1 ' matrixOUT.GetLength(1) - 1
        //        '    If matrixOUT(iPath, k) <= tr_a Then
        //        '        matrixOUT(iPath, k) = 0
        //        '    ElseIf matrixOUT(iPath, k) <= (tr_a + tr_w) Then
        //        '        matrixOUT(iPath, k) = (matrixOUT(iPath, k) - tr_a) / tr_w
        //        '    Else
        //        '        matrixOUT(iPath, k) = 1
        //        '    End If
        //        'Next

        //        ' update the completion bar in the user interface/multi-threaded

        //        Try
        //            DivRem(iPath, CInt(nDraws / 100), iResult)
        //        Catch ex As Exception
        //            iResult = 0
        //        End Try

        //        If iResult = 0 Then
        //            worker.ReportProgress(CInt((iPath / nDraws) * 100), tranche.Portfolio.Title + " Simulation in progress...")
        //        End If
        //    Next iPath ' simulation draws


        //    ' here we've completed all calculations and are left with a 2D array (MATRIX)  (rows,cols) ie (100000,20) 
        //    ' containing tranche losses


        //    Dim idxTrancheLet As Integer = 0
        //    For Each tranchelet As tkTrancheLet In tranche.TrancheLets
        //        Dim dblTotal As Double = 0
        //        For iPath As Integer = 0 To nDraws - 1
        //            dblTotal += matrixOUT(iPath, idxTrancheLet)
        //            iPath += 1
        //        Next

        //        idxTrancheLet += 1

        //        Dim dblTranchExpLoss As Double = dblTotal / nDraws
        //        If dblTranchExpLoss <= tr_a Then
        //            dblTranchExpLoss = 0
        //        ElseIf dblTranchExpLoss < (tr_a + tr_w) Then
        //            dblTranchExpLoss = (dblTranchExpLoss - tr_a) / tr_w
        //        Else
        //            dblTranchExpLoss = 1
        //        End If

        //        tranchelet.ExpectedLoss = dblTranchExpLoss
        //        ' all draws are done for this tranchelet... now summarize results
        //        ' as we've tallied things up nicely, the expectedloss or Mean is just the average tranche loss
        //    Next  ' tranchelet


        //    Return tranche
        //    ' go F yurselves...
        //End Function


        //'Single Tranche Calculation
        //' Tranche Attach Study
        //Function MonteCarloSim4(ByVal tranche As tkTranche, ByVal nDraws As Integer, ByVal dCorr As Double, ByVal worker As BackgroundWorker, ByVal e As DoWorkEventArgs) As tkTranche
        //    ' the Tranche has it's portfolio embedded for ease...
        //    ' loop counters 
        //    Dim i As Integer = 0    ' monte carlo simulation counter
        //    Dim j As Integer = 0    ' portfolio trade counter
        //    Dim k As Integer = 0    ' trade cashflow/tranche date counter
        //    Dim iResult As Integer

        //    Dim tr_a As Double = tranche.PctAttach
        //    Dim tr_w As Double = tranche.PctWidth
        //    Dim rnd As New Random ' 32 bit double random number generator class, quite suitable for this task
        //    ' see microsoft docs http://msdn2.microsoft.com/en-US/library/system.random.aspx
        //    Dim X As Double = 0 ' x is the Credit index of this MC Draw
        //    Dim H As Double = 0 'H is the percentile rank of the Cumulative Loss Probablity for each CDS/tranchelet cashflow

        //    'Dim R1() As Double   ' random var (systematic/market risk)
        //    Dim R1 As Double = 0  ' random var (systematic/market risk)
        //    Dim R2 As Double = 0  ' random var (idiosyncratic risk)


        //    Dim idxtrade As Integer = 0
        //    ' this returns a 2d array of double values containing the CLP term structure for our portfolio
        //    Dim matrixOUT As Double(,)
        //    Dim matrixCLP As Double(,) = tranche.Portfolio.CLPMatrix ' array dimensions (row,col)  rows are cds trades, cols are cds cashflows...
        //    ReDim matrixOUT((nDraws - 1), (matrixCLP.GetLength(1) - 1))


        //    Dim arPort(tranche.Portfolio.Count - 1) As mcPort2  ' NEW mcPORT2 contains a dynamic array of CLP's (per cashflow)
        //    ' create array of portfolio data for speed up...
        //    For Each cds As mxCDS In tranche.Portfolio
        //        With arPort(idxtrade)
        //            .Notional = cds.NotionalAmt ' note trades here are entered as Portfolio Weights
        //            ' looking deep into the individual CDS Cashflow here 
        //            ReDim .CDP(cds.Cashflows.Count - 1)
        //            k = 0
        //            For Each cf As mxCDSCF In cds.Cashflows
        //                '.CDP(k) = cf.rnpCLP
        //                ' new new new... using continuous compounding instead of discrete A/360 basis
        //                .CDP(k) = cds.CLP_CC(cf.CFEnd)
        //                k += 1
        //            Next
        //            .Maturity = cds.dtMaturity
        //            .RR = cds.RecoveryOnDefault
        //            .RRFixed = cds.IsFixedRR
        //            .sdRR = CType(0.05, Double) ' 5% std. dev. of recoveries around the mean...
        //        End With
        //        idxtrade += 1
        //    Next


        //    'ReDim R1(matrixCLP.GetLength(1) - 1)
        //    ' i,j,k  i is Draw counter, j is deal/cds counter, k is tranchlet counter

        //    worker.ReportProgress(0, tranche.Portfolio.Title + " Simulation in progress...")



        //    ' start your engines...  i looping through each draw
        //    For i = 0 To nDraws - 1  ' (each draw ie 100,000)
        //        R1 = NormSINV(rnd.NextDouble)  ' R1 is the SYSTEMATIC VARIABLE...

        //        'For q As Integer = 0 To R1.GetLength(0) - 1
        //        '    R1(q) = NormSINV(rnd.NextDouble)
        //        'Next
        //        'remdimDim(R1())


        //        ' j looping thru each trade in out synthetic portoflio...
        //        For j = 0 To arPort.Length - 1  '(each trade ie 150)

        //            'k looping thru each tranchLet...
        //            For k = 0 To arPort(j).CDP.Length - 1 ' (each cashflow in the CDS Trade)
        //                R2 = NormSINV(rnd.NextDouble)  ' idiosyntratic RANDOM VARIABLE
        //                ' new June 07
        //                X = dCorr * R1 + Sqrt(1 - dCorr ^ 2) * R2
        //                'X = (Sqrt(dCorr) * R1) + (Sqrt(1 - dCorr) * R2)
        //                H = NormSINV(arPort(j).CDP(k)) 'arPort(j).CDP(k))    ' default threshold/barrier...

        //                If X < H Then  ' at this point, a  DEFAULT HAS OCCURRED IN OUR SIMULATION...
        //                    'If arPort(j).RRFixed Then
        //                    ' if recoveries are fixed then LGD = 1 - RR
        //                    'arDrawOut(k) += arPort(j).Notional * (1 - arPort(j).RR)
        //                    matrixOUT(i, k) += arPort(j).Notional * (1 - arPort(j).RR)

        //                    'Else
        //                    ' if recoveries are not fixed then RR is Systematic Rand # of stdDevs from the mean)
        //                    ' recoveries will be LESS in real negative market samples (R1 RAND is the driver for this)
        //                    ' ensure RR >=0 and <=1
        //                    'Dim thisRR As Double = (arPort(j).RR + (R1 * arPort(j).sdRR))
        //                    'If thisRR > 1 Then thisRR = 1
        //                    'If thisRR < 0 Then thisRR = 0
        //                    'matrixOUT(i, k) += arPort(j).Notional * (1 - thisRR)
        //                    'End If
        //                    Exit For  ' this is important, only default ONCE per credit per portfolio draw...
        //                End If
        //            Next k ' trade cashflows/tranchelets
        //        Next j  ' portfolio cds trades



        //        ' we are at the end of a draw... all trades have been simulated and now this 'ROW' contains
        //        ' portfolio losses per tranche period. Note: portfolio losses are stated as a percentage, here we apply the Tranche Loss Function (Tranche Attachment + Tranche Width)
        //        ' in laymans terms: I'm just replacing portfolio losses with tranche losses...
        //        'For k = 0 To matrixOUT.GetLength(1) - 1 ' matrixOUT.GetLength(1) - 1
        //        '    If matrixOUT(i, k) <= tr_a Then
        //        '        matrixOUT(i, k) = 0
        //        '    ElseIf matrixOUT(i, k) <= (tr_a + tr_w) Then
        //        '        matrixOUT(i, k) = (matrixOUT(i, k) - tr_a) / tr_w
        //        '    Else
        //        '        matrixOUT(i, k) = 1
        //        '    End If
        //        'Next

        //        ' update the completion bar in the user interface/multi-threaded
        //        Try
        //            DivRem(i, CInt(nDraws / 100), iResult)
        //        Catch ex As Exception
        //            iResult = 0
        //        End Try

        //        If iResult = 0 Then
        //            worker.ReportProgress(CInt((i / nDraws) * 100), tranche.Portfolio.Title + " Simulation in progress...")
        //        End If
        //    Next i ' simulation draws

        //    tranche.PortLossDist = matrixOUT



        //    ' here we've completed all calculations and are left with a 2D array (MATRIX)  (rows,cols) ie (100000,20) 
        //    ' containing tranche losses


        //    Dim idxTrancheLet As Integer = 0
        //    For Each tranchelet As tkTrancheLet In tranche.TrancheLets
        //        Dim dblTotal As Double = 0
        //        For i = 0 To nDraws - 1
        //            dblTotal += matrixOUT(i, idxTrancheLet)
        //        Next
        //        tranchelet.ExpectedLoss = dblTotal / nDraws
        //        idxTrancheLet += 1
        //        ' all draws are done for this tranchelet... now summarize results
        //        ' as we've tallied things up nicely, the expectedloss or Mean is just the average tranche loss
        //    Next  ' tranchelet

        //    Return tranche
        //    ' go F yourself...
        //End Function

        //http://www.mail-archive.com/advanced-dotnet@discuss.develop.com/msg06173.html
        // implement the Cumulative standard normal distribution (mean=0 and StdDev=1)
        //from microsoft: http://support.microsoft.com/?kbid=214111
        //    public static double NormSDist(double x)
        //    {
        //        double Z = 1 / Sqrt(2 * PI) * Exp(-Math.Pow(x, 2) / 2);
        //        double p = 0.2316419;
        //        double b1 = 0.31938153;
        //        double b2 = -0.356563782;
        //        double b3 = 1.781477937;
        //        double b4 = -1.821255978;
        //        double b5 = 1.330274429;
        //        double t = 1 / (1 + p * x);
        //        return 1 - Z * (b1 * t + b2 * Math.Pow(t, 2) + b3 * Math.Pow(t, 3) + b4 * Math.Pow(t, 4) + b5 * Math.Pow(t, 5));
        //    }



        //    // implement normal standard inverse // 
        //    //The following describes an algorithm for computing the inverse normal cumulative distribution function where the relative error has an absolute value less than 1.15·10−9 in the entire region. References to other algorithms are included.
        //    //SEE HERE
        //    //http://home.online.no/~pjacklam/notes/invnorm/#Visual_Basic
        //    public double NormSInv(double p)
        //    {
        //        double q = 0;
        //        double r = 0;

        //        //Coefficients in rational approximations.

        //        const double A1 = -39.6968302866538;
        //        const double A2 = 220.946098424521;
        //        const double A3 = -275.928510446969;
        //        const double A4 = 138.357751867269;
        //        const double A5 = -30.6647980661472;
        //        const double A6 = 2.50662827745924;

        //        const double B1 = -54.4760987982241;
        //        const double B2 = 161.585836858041;
        //        const double B3 = -155.698979859887;
        //        const double B4 = 66.8013118877197;
        //        const double B5 = -13.2806815528857;

        //        const double C1 = -0.00778489400243029;
        //        const double C2 = -0.322396458041136;
        //        const double C3 = -2.40075827716184;
        //        const double C4 = -2.54973253934373;
        //        const double C5 = 4.37466414146497;
        //        const double C6 = 2.93816398269878;

        //        const double D1 = 0.00778469570904146;
        //        const double D2 = 0.32246712907004;
        //        const double D3 = 2.445134137143;
        //        const double D4 = 3.75440866190742;

        //        //Define break-points.

        //        const double P_LOW = 0.02425;
        //        const double P_HIGH = 1 - P_LOW;

        //        if (p > 0 && p < P_LOW)
        //        {

        //            //Rational approximation for lower region.

        //            q = Sqrt(-2 * Log(p));


        //            return (((((C1 * q + C2) * q + C3) * q + C4) * q + C5) * q + C6) / ((((D1 * q + D2) * q + D3) * q + D4) * q + 1);
        //        }
        //        else if (p >= P_LOW && p <= P_HIGH)
        //        {

        //            //Rational approximation for central region.

        //            q = p - 0.5;
        //            r = q * q;


        //            return (((((A1 * r + A2) * r + A3) * r + A4) * r + A5) * r + A6) * q / (((((B1 * r + B2) * r + B3) * r + B4) * r + B5) * r + 1);
        //        }
        //        else if (p > P_HIGH && p < 1)
        //        {

        //            //Rational approximation for upper region.

        //            q = Sqrt(-2 * Log(1 - p));


        //            return -(((((C1 * q + C2) * q + C3) * q + C4) * q + C5) * q + C6) / ((((D1 * q + D2) * q + D3) * q + D4) * q + 1);
        //        }
        //        else
        //        {


        //            throw new ArgumentOutOfRangeException();

        //        }
        //    }


        }



}