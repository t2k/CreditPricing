using System;
using System.Runtime.Serialization;

namespace CreditPricing
{

    /// <summary>
    ///  the TKsys. Rate  Class: RateCode, bid, ask, cls
    /// </summary>
    [DataContract]
    public class Rate
    {
        [DataMember]
        public string RATECODE { get; set; }
        [DataMember]
        public double bid { get; set; }
        [DataMember]
        public double ask { get; set; }
        [DataMember]
        public double cls { get; set; }

        /// <summary>
        /// Empty constructor
        /// </summary>
        public Rate() { }


        /// <summary>
        /// standard constructor pass in params
        /// </summary>
        /// <param name="_rateCode"></param>
        /// <param name="_bid"></param>
        /// <param name="_ask"></param>
        /// <param name="_cls"></param>
        /// 
        public Rate(string _rateCode, double _bid, double _ask, double _cls)
        {
            RATECODE = _rateCode;
            bid = _bid;
            ask = _ask;
            cls = _cls;
        }

        /// <summary>
        /// constructor from Entity Framework classes
        /// </summary>
        /// <param name="_rate">a RATEHIST object from entity framework classs</param>
        public Rate(CreditPricing.RATEHIST _rate)
        {
            RATECODE = _rate.RATECODE;
            bid = _rate.bid.HasValue ? (double)_rate.bid : 0;
            ask = _rate.ask.HasValue ? (double)_rate.ask : 0;
            cls = _rate.cls.HasValue ? (double)_rate.cls : 0;
        }
    }

}