using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CreditPricing
{
    /// <summary>
    /// A dictionary of CDSMarketProvider: class stores best bid/offer and bid/offer status as well as best bid/ask provider
    /// </summary>
    // note: let this class store the individual market parameters...
    // ie best bid and best offer...
    // by updating bids and offers given a provider name...
    // ie UpdateBid(dblBid, strProvider)
    // ie UpdateAsk(dblAsk, strProvider)
    public class CDSMarketProviders : Dictionary<string, CDSMarketProvider> // System.Collections.DictionaryBase
    {
        private double m_bestBid;
        private double m_bestAsk;
        private bool m_bidStatus;
        private bool m_askStatus;
        private string m_bestBidProvider;
        private string m_bestAskProvider;

        public string BestBidProvider
        {
            get { return m_bestBidProvider; }
        }

        public string BestAskProvider
        {
            get { return m_bestAskProvider; }
        }

        public bool BidStatus
        {
            get { return m_bidStatus; }
        }

        public bool AskStatus
        {
            get { return m_askStatus; }
        }

        public double BestBid
        {
            get { return m_bestBid; }
        }

        public double BestMid
        {
            get { return (m_bestBid + m_bestAsk) / 2; }
        }

        public double BestAsk
        {
            get { return m_bestAsk; }
        }

        /// <summary>
        /// generic constructor not used dictionary is empty...?
        /// </summary>
        public CDSMarketProviders()
            : base()
        {
            m_bestBid = 0;
            m_bestAsk = 0;
            m_bidStatus = false;
            m_askStatus = false;
            m_bestBidProvider = "none";
            m_bestAskProvider = "none";
        }

        /// <summary>
        /// Standard Contruction for CDSMarketProvider
        /// instantiates the object and creates CDSMarketProvider (internal dictionary)
        /// </summary>
        /// <param name="strCDSTicker">a string value for a CDS Ticker</param>
        /// <param name="providerList">a generic List( of MarketDataProvider) </param>
        public CDSMarketProviders(string strCDSTicker, List<MarketDataProvider> providerList)
            : base()
        {
            m_bestBid = 0;
            m_bestAsk = 0;
            m_bidStatus = false;
            m_askStatus = false;
            m_bestBidProvider = "none";
            m_bestAskProvider = "none";

            foreach (MarketDataProvider tkDP in providerList)
            {
                Add(tkDP.Name, new CDSMarketProvider(tkDP, strCDSTicker));
            }
        }


        /// <summary>
        /// track the best bid/offer at the market level will speed calculations and 
        /// we gain insight...
        /// usage: Update("CAIG1U5 CBIN Curncy","BID",.25)
        /// </summary>
        /// <param name="MarketProviderKey">Unique Name of provider in list</param>
        /// <param name="BIDASK">BID or ASK</param>
        /// <param name="value">the market price (double)</param>
        public void Update(string MarketProviderKey, string BIDASK, double value)
        {
            CDSMarketProvider mp = null;
            this.TryGetValue(MarketProviderKey, out mp);

            // really liking the C# syntax here!
            //try
            //{

            //    //mp = (this.ContainsKey(MarketProviderKey)) ? this[MarketProviderKey] : null; // default(CDSMarketProvider);
            //}
            //catch (Exception)
            //{
            //    mp=null;
            //}

            //try
            //{
            //    mp = this[MarketProviderKey];  // remember *this* is a Dictionary of CDSMarketProvier  ie Dictionary<string,CDSMarketProvider>
            //}
            //catch (Exception ex)
            //{
            //    mp = null;
            //}


            if (mp != null)
            {
                if (BIDASK.ToUpper() == "BID")
                {
                    mp.Bid = value;
                    if (value >= this.maxBid)
                    {
                        //track the best bid at the market level..
                        m_bestBid = value;
                        m_bidStatus = true;
                        m_bestBidProvider = mp.Provider;

                    }
                }
                else if (BIDASK.ToUpper() == "ASK")
                {

                    mp.Ask = value;
                    if (m_bestAsk == 0 && value > 0)
                    {
                        m_bestAsk = value;
                        m_askStatus = true;
                        m_bestAskProvider = mp.Provider;
                    }
                    else if (value <= this.minAsk)
                    {
                        // a new best offer in this 'market'
                        m_bestAsk = value;
                        m_bestAskProvider = mp.Provider;
                    }
                }
            }
        }

        // that should do it!  --tk ;p

        // we'll call this routine when the user checks/unchecks the checkbox in the listview...
        // it is used to update the bestbid/bestask for the market if we need to knock out 
        /// <summary>
        /// Use the Revise method to update best bid/ask depending upon addition or removal of Market providers
        /// used mainly in WPF applications that monitor status of markets dynamically
        /// </summary>
        public void Revise()
        {
            m_bestBid = 0;
            m_bestAsk = 0;

            // scanning the market// fined best bid and offer 
            // only look to those prices where the providerStatus =true (all by default, unless unchecked by the user)
            // and only providers that have been updated...
            foreach (CDSMarketProvider mp in Values)
            {

                if (mp.ProviderStatus)
                {
                    // skip any providers that the user has unselected...

                    if (mp.BidStatus)
                    {
                        double dBid = mp.Bid;
                        if (dBid > m_bestBid) m_bestBid = dBid;
                    }

                    if (mp.AskStatus)
                    {
                        double dAsk = mp.Ask;
                        if (m_bestAsk == 0)
                        {
                            m_bestAsk = dAsk;
                        }
                        else if (dAsk < m_bestAsk)
                        {
                            m_bestAsk = mp.Ask;
                        }
                    }
                }
            }
        }


        // strProvider "BMSG" "CBIN" etc...
        // see if this is the maximum bid in this market but exlude this provider previous entry...
        /// <summary>
        /// calculated property of this class: Tracks the best bid for this market...
        /// </summary>
        public double maxBid
        {
            get
            {
                double maxPrc = 0;
                foreach (CDSMarketProvider mp in this.Values)
                {
                    double dBid = mp.Bid;
                    if ((mp.ProviderStatus && mp.BidStatus))
                    {
                        if (dBid > maxPrc)
                        {
                            maxPrc = dBid;
                        }
                    }
                }
                return maxPrc;
            }
        }

        // strProvider "BMSG" "CBIN" etc...
        /// <summary>
        /// Class Property to track the best offer in this market (lowest)
        /// </summary>
        public double minAsk
        {
            get
            {
                double minPrc = 0;
                foreach (CDSMarketProvider mp in this.Values)
                {
                    if ((mp.ProviderStatus && mp.AskStatus))
                    {
                        double dAsk = mp.Ask;

                        if (minPrc == 0)
                        {
                            minPrc = dAsk;
                        }
                        else if (dAsk < minPrc)
                        {
                            minPrc = dAsk;
                        }
                    }
                }
                return minPrc;
            }
        }
    }
}