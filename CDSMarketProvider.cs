using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CreditPricing
{
    /// <summary>
    /// Summary description for CDSMarketProvider
    /// </summary>
    // price data quotes given by a provider...

    public class CDSMarketProvider
    {
        // bloomberg price provider ie MSDN, CBGN,CBIN, BASD etc...
        private string m_Provider;
        private string m_ProviderInterface;
        // bloomberg price provider
        private string m_CDSProviderTicker;
        private double m_Bid;
        private double m_Ask;
        private bool m_BidStatus;
        private bool m_AskStatus;
        private bool m_ProviderUseStatus;
        private string m_Time;


        // constructor...  
        public CDSMarketProvider(MarketDataProvider tkDP, string strCDSTICKER)
        {
            m_Provider = tkDP.Name;
            m_ProviderInterface = tkDP.InterfaceName;
            m_ProviderUseStatus = true;
            m_Bid = 0.0;
            m_BidStatus = false;
            m_Ask = 0.0;
            m_AskStatus = false;
            // ie Store  "CAIG1U5 BASD Curncy"  see implementation below
            CDSProviderTicker = strCDSTICKER;
        }


        public string CDSProviderTicker
        {
            get { return m_CDSProviderTicker; }

            set
            {
                try
                {
                    string[] tkrParse = value.Split(' ');

                    // ie pass in "CAIG1U5 Curncy"  
                    m_CDSProviderTicker = string.Format("{0} {1} {2}", tkrParse[0], m_Provider, tkrParse[1]);
                    // ie if class member m_Provider = "BASD" then want to store "CAIG1U5 BASD Curncy"  (this ultimately builds our request object) for bloomberg feeds etc.
                }
                catch
                {
                    //Interaction.MsgBox("Missing Bloomberg 'Market' name " + value + " should be " + value + " Curncy", MsgBoxStyle.Information, "TICKER FORMAT ERROR!");
                }
            }
        }

        public string Provider
        {
            get { return m_Provider; }
        }

        public string ProviderInterface
        {
            get { return m_ProviderInterface; }
        }

        public double Bid
        {
            get { return m_Bid; }
            set
            {
                m_BidStatus = true;
                m_Bid = value;
            }
        }

        public double Mid
        {
            get { return (m_Bid + m_Ask) / 2; }
        }

        public double Ask
        {
            get { return m_Ask; }
            set
            {
                m_AskStatus = true;
                m_Ask = value;
            }
        }

        public bool BidStatus
        {
            get { return m_BidStatus; }
            set { m_BidStatus = value; }
        }

        public bool AskStatus
        {
            get { return m_AskStatus; }
            set { m_AskStatus = value; }
        }

        public bool ProviderStatus
        {
            get { return m_ProviderUseStatus; }
            set { m_ProviderUseStatus = value; }
        }

        public string PXTime
        {
            get { return m_Time; }
            set { m_Time = value; }
        }

    }
}