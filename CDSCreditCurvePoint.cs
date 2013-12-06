using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;


namespace CreditPricing
{

    public class CDSCreditCurvePoint
    {
        /// <summary>
        /// Bloomberg Code: Used for desktop API applications...
        /// </summary>
        public string BLPCode { get; set; }
        public string Ticker { get; set; }
        public string CCY { get; set; }
        public string Tier { get; set; }
        public string EQTicker { get; set; }
        public int CDSTerm { get; set; }
        public bool MarketStatus { get; set; }

        private DateTime m_CDSDate;
        /// <summary>
        /// To set the date just pass nextIMM date and the CDS Term will be added to that
        ///  this is important... the 1Y,2Y 3Y etc date is pass in as the nextIMM date and then year is added
        /// NOTE: CDSTerm must be loaded first!!!
        /// </summary>
        public DateTime ValueDate
        {
            get { return m_CDSDate; }
            set { m_CDSDate = value.AddMonths(CDSTerm); }
        }


        /// <summary>
        /// bloomberg Item: Splits teh BLPCode property to return the first part... (API Usage)
        /// </summary>
        public string BBItem
        {
            get
            {
                string[] strTmp = this.BLPCode.Split(' ');
                return strTmp[0];
            }
        }
        public CDSMarketProviders Market { get; set; } // m_Market;

    }
}