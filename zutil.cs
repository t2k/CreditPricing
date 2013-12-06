using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace CreditPricing
{

    public enum tkOptions
    {
        no = 0,
        yes = 1
    }

    public enum tkInterModes
    {
        tkINTERPSL = 1,
        tkINTERPLOG = 2
    }

    public enum tkPrice
    {
        tkBID = 2,
        // note, here the ordinal # is important
        tkASK = 3,
        // do not change this order without redoing other code,
        tkMID = 4,
        // by assuming that bid/ask is array column index of 2 and 3 respectively...
        tkCLS = 5
    }

    public enum tkDateCalcMode
    {
        tkNone,
        tkPreceding,
        tkFollowing,
        tkModifiedFollowing
    }

    public enum tkDayConvention
    {
        tkA360,
        // standard money market stuff
        tkA365,
        // std un UK and CAD, AUS etc
        tk30360ISDA,
        // std ISDA...
        tk30360E,
        // European
        tk30360PSA,
        // PSA
        tk30360SIA,
        // SIA
        tkACTACT,
        // govt bonds use this?
        tkBONDCOUPON
        // fixed coupon
    }




    //public class zClass
    //{
    //    public enum tkInterModes
    //    {
    //        tkINTERPSL = 1,
    //        tkINTERPLOG = 2
    //    }

    //    public enum tkPrice
    //    {
    //        tkBID = 2,
    //        // note, here the ordinal # is important
    //        tkASK = 3,
    //        // do not change this order without redoing other code,
    //        tkMID = 4,
    //        // by assuming that bid/ask is array column index of 2 and 3 respectively...
    //        tkCLS = 5
    //    }

    //    public enum tkDateCalcMode
    //    {
    //        tkNone,
    //        tkPreceding,
    //        tkFollowing,
    //        tkModifiedFollowing
    //    }

    //    public enum tkDayConvention
    //    {
    //        tkA360,
    //        // standard money market stuff
    //        tkA365,
    //        // std un UK and CAD, AUS etc
    //        tk30360ISDA,
    //        // std ISDA...
    //        tk30360E,
    //        // European
    //        tk30360PSA,
    //        // PSA
    //        tk30360SIA,
    //        // SIA
    //        tkACTACT,
    //        // govt bonds use this?
    //        tkBONDCOUPON
    //        // fixed coupon
    //    }
    //}

    [DataContract]
    public struct rtDisplay
    {
        public string CurveName { get; set; }
        public DateTime RateDate { get; set; }
        public string Period { get; set; }
        public string RATECODE { get; set; }
        public DateTime EndDate { get; set; }
        public string BASIS { get; set; }
        public int CompPA { get; set; }
        public double BPShift { get; set; }
        public double BID { get; set; }
        public double ASK { get; set; }
        public string Description { get; set; }
    }


    [Serializable]
    public class rcDisplay
    {
        public string RATECODE { get; set; }
        public string Desc { get; set; }
        public string CCY { get; set; }
        public string Period { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
        public string Description { get; set; }
    }

    [DataContract]
    public struct ycDisplay
    {
        public string Curve { get; set; }
        public string name { get; set; }
        public DateTime valueDate { get; set; }
        public DateTime maturityDate { get; set; }
        public string Date { get; set; }
        public DateTime Date2 { get; set; }
        public string Rate { get; set; }
        public double zeroRate { get; set; }
    }

    [DataContract]
    public struct rowDF
    {
        public DateTime date { get; set; }
        public double bid { get; set; }
        public double ask { get; set; }
    }

}