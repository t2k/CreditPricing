﻿using System;


namespace CreditPricing
{
    /// <summary>
    /// Summary description for CorrProbDefDist correlated Probability Default Distribution...
    /// </summary>
    public class CorrProbDefDist
    {
        private int m_draws;
        private int m_Assets;
        private double m_Corr;
        private double[] m_rndSys;  // rank 1 array of double
        // market or common factors
        private double[,] m_rndAssets; // rank 2 array of double...

        /// <summary>
        /// array of systematic random variables or 'Market' randoms
        /// </summary>
        public double[] MarketRands
        {
            get { return m_rndSys; }
        }
        /// <summary>
        /// joint probability default distribution
        /// </summary>
        public double[,] JointProbDefDist
        {
            get { return m_rndAssets; }
        }

        public int nDraws
        {
            get { return m_draws; }
            set { m_draws = value; }
        }

        public int nAssets
        {
            get { return m_Assets; }
            set { m_Assets = value; }
        }

        public double Corr
        {
            get { return m_Corr; }
            set { m_Corr = value; }
        }


        /// <summary>
        /// constructor: Class to generate rank 1 and rank 2 arrays of double filled with pseudo rands generated by the compiler...
        /// </summary>
        /// <param name="_draws">number of draws for monte-carlo simulation: 100000</param>
        /// <param name="_assets">number of assets (for idiosyncratic) ie 150</param>
        /// <param name="_corr">correlation >=0 <=1 </param>
        public CorrProbDefDist(int _draws, int _assets, double _corr)
        {

            m_draws = _draws;
            m_Assets = _assets;
            m_Corr = _corr;

            Random rnd = new Random();

            m_rndSys = new double[m_draws];
            m_rndAssets = new double[m_draws, m_Assets];

            for (int i = 0; i < m_draws; i++)
            {


                m_rndSys[i] = tkStats.NormSInv(rnd.NextDouble()); // DNormSInv(rnd.NextDouble);
                for (int j = 0; j < m_Assets; j++)
                {
                    double rndIDIO = tkStats.NormSInv(rnd.NextDouble());
                    m_rndAssets[i, j] = tkStats.NormSDist(m_rndSys[i] * m_Corr + Math.Sqrt(1 - (Math.Pow(m_Corr, 2))) * rndIDIO);
                }
            }
        }


        // alternate constructor:   no correlation parm,  lets use random correlation as the inverse of the systematice (market)  draws
        // so, the reasoning goes: in a negative market  (ie a low draw near zero )
        /// <summary>
        /// not sure this is valid... do not use
        /// </summary>
        /// <param name="_draws"></param>
        /// <param name="_assets"></param>
        //public CorrProbDefDist(int _draws, int _assets)
        //{
        //    m_draws = _draws;
        //    m_Assets = _assets;

        //    // create private array of doubles...
        //    m_rndSys = new double[m_draws - 1];
        //    m_rndAssets = new double[m_draws - 1, m_Assets - 1];

        //    Random rnd = new Random();
        //    // 32 bit random number generator

        //    for (int i = 0; i < m_draws; i++)
        //    {
        //        double rndMarket = rnd.NextDouble();
        //        double rndCorr = 1 - rndMarket;

        //        m_rndSys[i] = tkStats.NormSInv(rndMarket);
        //        for (int j = 0; j < m_Assets; j++)
        //        {
        //            double rndIDIO = tkStats.NormSInv(rnd.NextDouble());
        //            m_rndAssets[i, j] = tkStats.NormSDist(m_rndSys[i] * rndCorr + Math.Sqrt(1 - Math.Pow(rndCorr, 2)) * rndIDIO);
        //        }
        //    }
        //}
    }

}