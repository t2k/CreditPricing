using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CreditPricing
{
    public class tkSimResult
    {
        private int m_SimID;
        private DateTime mDate;
        private TimeSpan mDuration;
        private string mPortfolio;
        private double mPortCorrFactor;
        private int mPortCount;
        private double mPortWAS;
        private double mPortNotl;
        private double mPortMaxLoss;
        private double mTotalLoss;
        private double mPortMeanLoss;
        private double mPortStdDevLoss;
        private double mWARR;
        private double mWARRMax;
        private double mWARRMin;
        private int mBinCount;
        private SimBins mLossBins;
        private SimBins mNumDefaultBins;
        private mcDraw[] mDistribution;
        private int mDrawCount;
        private double mAttach;
        private double mDetach;
        private int mMaxNumDefaults;



        public tkSimResult(int _SimID, mcOutput mcResult, int nBins, double Attachment, double Detachment)
        {
            // create a Simulation Bins class 
            m_SimID = _SimID;
            mBinCount = nBins;
            mAttach = Attachment;
            mDetach = Detachment;
            {
                mDate = mcResult.mcDate;
                mDuration = mcResult.mcDuration;
                mPortfolio = mcResult.mcPortfolio;
                mPortCorrFactor = mcResult.mcCorrelation;
                mPortCount = mcResult.mcPortCount;
                mPortWAS = mcResult.mcPortWAS;
                mPortNotl = mcResult.mcPortNotional;
                mPortMaxLoss = mcResult.mcMaxLoss;
                mTotalLoss = mcResult.mcTotalLoss;
                mMaxNumDefaults = mcResult.mcMaxNumDefaults;
                mDistribution = mcResult.mcDraws;
            }

            mDrawCount = mDistribution.Length;
            // this is the # of Monte-carlo draws that were performed
            mPortMeanLoss = mTotalLoss / mDrawCount;
            //total losses over all draws/# of draws

            // the loss distribution buckets
            SetLossBins(mBinCount);
            SetNumDefaultBins();
            CalcLossBins();
            CalcNumDefaultBins();
        }


        public List<PortfolioSimulationResult> GetData
        {
            get
            {
                List<PortfolioSimulationResult> list = new List<PortfolioSimulationResult>();
                PortfolioSimulationResult newRow = new PortfolioSimulationResult();

                //dsSimulation.PortfolioSimulationResultDataTable dt = new dsSimulation.PortfolioSimulationResultDataTable();
                //dsSimulation.PortfolioSimulationResultRow newRow = dt.NewPortfolioSimulationResultRow();
                {
                    newRow.SimID = m_SimID;
                    newRow.SimDate = mDate;
                    newRow.Correlation = mPortCorrFactor;
                    newRow.ExpectedLoss = this.mPortMeanLoss;
                    newRow.StdDev = this.mPortStdDevLoss;
                    newRow.TrancheAttach = this.mAttach;
                    newRow.TranchZScore = (mAttach - mPortMeanLoss) / mPortStdDevLoss;
                    newRow.WAS = this.mPortWAS * 100;
                    newRow.PctWeight = this.mPortNotl;
                    newRow.RECount = this.PortCount;
                    newRow.MaxLoss = this.mPortMaxLoss;
                    newRow.WARR = this.WARR;
                    newRow.WARRMin = this.WARRMin;
                    newRow.WARRMax = this.WARRMax;
                    newRow.TrancheWidth = mDetach - mAttach;
                    newRow.SimDuration = SimDuration.ToString();
                    newRow.Draws = mDrawCount;
                }
                list.Add(newRow);
                return list;
            }
        }


        // write Portfolio Simulation Data to table...
        public void SaveData()
        {
            var db = new CreditPricingEntities();
            PortfolioSimulationResult newRow = new PortfolioSimulationResult();

            try
            {
                // dsSimulation.PortfolioSimulationResultDataTable dt = new dsSimulation.PortfolioSimulationResultDataTable();
                //dsSimulation.PortfolioSimulationResultRow newRow = dt.NewPortfolioSimulationResultRow;
                {
                    newRow.SimID = m_SimID;
                    newRow.SimDate = mDate;
                    newRow.Correlation = mPortCorrFactor;
                    newRow.ExpectedLoss = this.mPortMeanLoss;
                    newRow.StdDev = this.mPortStdDevLoss;
                    newRow.TrancheAttach = this.mAttach;
                    newRow.TranchZScore = (mAttach - mPortMeanLoss) / mPortStdDevLoss;
                    newRow.WAS = this.mPortWAS * 100;
                    newRow.PctWeight = this.mPortNotl;
                    newRow.RECount = this.PortCount;
                    newRow.MaxLoss = this.mPortMaxLoss;
                    newRow.WARR = this.WARR;
                    newRow.WARRMin = this.WARRMin;
                    newRow.WARRMax = this.WARRMax;
                    newRow.TrancheWidth = mDetach - mAttach;
                    newRow.SimDuration = SimDuration.ToString();
                    newRow.Draws = mDrawCount;
                }
                db.AddToPortfolioSimulationResults(newRow);
                db.SaveChanges();
            }
            catch
            {

            }
        }

        // store date to our database... this can be made more general...
        public void SaveBins()
        {
            var db = new CreditPricingEntities();
            int i = 1;

            //' loss bins
            foreach (SimBin bin in mLossBins)
            {
                PortfolioSimulationBin row = new PortfolioSimulationBin();
                row.SimID = m_SimID;
                row.BinType = "L";
                row.BinOrdinal = i;
                row.BinX = bin.BinID;
                row.BinY = bin.BinPct;
                db.AddToPortfolioSimulationBins(row);
                i++;
            }

            // default bins
            i = 1;  // important: reset bin ordinal pointer...
            foreach (SimBin bin in mNumDefaultBins)
            {
                PortfolioSimulationBin row = new PortfolioSimulationBin();
                row.SimID = m_SimID;
                row.BinType = "D";
                row.BinOrdinal = i;
                row.BinX = bin.BinID;
                row.BinY = bin.BinPct;
                db.AddToPortfolioSimulationBins(row);
                i++;
            }

            try
            {
                db.SaveChanges();
                //taPortSimBins.Update(dtPortSimBins);
            }
            catch
            {
            }
        }

        public void SetNumDefaultBins()
        {
            mNumDefaultBins = new SimBins(mMaxNumDefaults, mDrawCount);
        }


        public void SetLossBins(int numBins)
        {
            mBinCount = numBins;
            mLossBins = new SimBins(mBinCount, mPortMaxLoss, mDrawCount, mAttach, mDetach, mPortNotl);
        }


        public void CalcLossBins()
        {
            // next we can calculate the stddev of our distribution...
            // Sqrt( sqrt(ave(variance))) where variance = (Xi-Mean) for each
            double dVariance = 0;
            double dWARRTotal = 0;

            double dWARRMin = 1;
            double dWARRMax = 0;
            int iZeroWARRCount = 0;


            for (int i = 0; i < mDistribution.Length; i++)
            {
                // find WARR (wtd ave recovery rate)  skip zero default draws... 
                // it happens alot in IG portfolios (w/ low spreads)
                if (mDistribution[i].dblWARR == 0)
                {
                    iZeroWARRCount += 1;
                }
                else
                {
                    double thisWARR = mDistribution[i].dblWARR;
                    if (thisWARR < dWARRMin) dWARRMin = thisWARR;
                    if (thisWARR > dWARRMax) dWARRMax = thisWARR;
                    dWARRTotal += thisWARR;
                }
                dVariance += Math.Pow((mDistribution[i].dblLGD - mPortMeanLoss), 2);
                mLossBins.BinLosses(mDistribution[i].dblLGD);
            }
            mPortStdDevLoss = Math.Sqrt(dVariance / mDrawCount);
            //Me.SimCount = nDraws
            this.WARR = dWARRTotal / (mDrawCount - iZeroWARRCount);
            this.WARRMax = dWARRMax;
            this.WARRMin = dWARRMin;
            mLossBins.PctScale();
        }


        public void CalcNumDefaultBins()
        {
            // next we can calculate the stddev of our distribution...
            // Sqrt( sqrt(ave(variance))) where variance = (Xi-Mean) for each
            for (int i = 0; i < mDistribution.Length; i++)
            {
                // find WARR (wtd ave recovery rate)  skip zero default draws... 
                // it happens alot in IG portfolios (w/ low spreads)
                mNumDefaultBins.BinDefaults(mDistribution[i].nDefaults);
            }
            mNumDefaultBins.PctScale();
        }



        public double WARRMax
        {
            get { return mWARRMax; }
            set { mWARRMax = value; }
        }

        public double WARRMin
        {
            get { return mWARRMin; }
            set { mWARRMin = value; }
        }

        public double WARR
        {
            get { return mWARR; }
            set { mWARR = value; }
        }


        public mcDraw[] LossDistribution
        {
            get { return mDistribution; }
            set { mDistribution = value; }
        }

        public SimBins BinLosses
        {
            get { return mLossBins; }
            set { mLossBins = value; }
        }

        public SimBins BinNumDefaults
        {
            get { return mNumDefaultBins; }
            set { mNumDefaultBins = value; }
        }

        public double PortCorrFactor
        {
            get { return mPortCorrFactor; }
            set { value = mPortCorrFactor; }
        }

        public DateTime SimDate
        {
            get { return mDate; }
            set { mDate = value; }
        }

        public TimeSpan SimDuration
        {
            get { return mDuration; }

            set { mDuration = value; }
        }

        public string Portfolio
        {
            get { return mPortfolio; }
            set { mPortfolio = value; }
        }

        public int PortCount
        {
            get { return mPortCount; }
            set { mPortCount = value; }
        }

        public double PortSpread
        {
            get { return mPortWAS; }
            set { mPortWAS = value; }
        }

        public double PortNotl
        {
            get { return mPortNotl; }
            set { mPortNotl = value; }
        }

        public double PortMeanLoss
        {
            get { return mPortMeanLoss; }
            set { mPortMeanLoss = value; }
        }

        public double PortMaxLoss
        {
            get { return mPortMaxLoss; }
            set { mPortMaxLoss = value; }
        }

        public double PortStdDevLoss
        {
            get { return mPortStdDevLoss; }
            set { mPortStdDevLoss = value; }
        }

        public int DrawCount
        {
            get { return mDrawCount; }
            set { mDrawCount = value; }
        }
    }
}