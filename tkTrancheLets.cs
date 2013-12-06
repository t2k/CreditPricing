using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CreditPricing
{
    // a strongly typed collection of trancheLet object
    /// <summary>
    ///  tranchelets contructor: pass in a tkTranche and built the translets reverse from Maturity Date backwards
    /// </summary>
    public class tkTrancheLets : List<tkTrancheLet> //System.Collections.CollectionBase
    {
        public tkTrancheLets(tkTranche _tranche, YldCurve _yc)
            : base()
        {
            // cds As mxCDS)
            DateTime dateEval = _yc.ValueDate;
            DateTime dtFlow = _tranche.TrancheMaturity;
            //DateTime dtPrev = default(System.DateTime);
            int iMonthRoll = -(12 / Math.Abs(_tranche.FlowsPA));
            // ensure roll backwards
            do
            {
                Insert(0, new tkTrancheLet(dtFlow, _yc));
                //InsertFirst(tLet);
                //dtPrev = dtFlow;
                dtFlow = dtFlow.AddMonths(iMonthRoll);
            }
            while (!(dtFlow <= dateEval));
        }
    }
}