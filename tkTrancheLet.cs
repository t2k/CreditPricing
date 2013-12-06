using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CreditPricing
{
    public class tkTrancheLet
    {
        public DateTime FwdDate { get; set; }
        public double DiscountFactor { get; set; }
        public double ExpectedLoss { get; set; }

        // other stats about distribution can be added
        public tkTrancheLet(mxCDSCF tkCDSFlow)
        {
            FwdDate = tkCDSFlow.CFEnd;
            DiscountFactor = tkCDSFlow.DF;
            ExpectedLoss = 0;
        }


        public tkTrancheLet(DateTime dtFlow, YldCurve yc)
        {
            FwdDate = dtFlow;
            DiscountFactor = yc.GetDF(dtFlow);
            ExpectedLoss = 0;
        }
    }
}