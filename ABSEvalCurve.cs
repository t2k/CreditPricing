using System;


/// <summary>
/// Summary description for ABSEvalCurve: a spread record with spreads over LIBOR in BP
/// partial class to exend ABSEvalCurve entity...
/// </summary>


namespace CreditPricing
{

// extend methods on top of the Entity Class from .NET

    public partial class ABSEvalCurve
    {
        // class knows how ot interpolate
        public double InterpSpreadYRS(double yrs)
        {
            DateTime dateInFuture =  this.EvalDate.AddDays(yrs * 365);
            return InterpSpread(dateInFuture);
        }

        // interp from simple term structure 
        public double InterpSpread(DateTime _toDate)
        {
            DateTime nearDate = default(System.DateTime);
            System.DateTime farDate = default(System.DateTime);

            double nearSprd = 0;
            double farSprd = 0;

            try
            {
                if (_toDate < EvalDate.AddYears(1))
                {

                    return (double)C1;
                }
                else if (_toDate < EvalDate.AddYears(3))
                {
                    nearDate = EvalDate.AddYears(1);
                    farDate = EvalDate.AddYears(3);
                    nearSprd = (double) C1;

                    farSprd = (double) C3;
                }
                else if (_toDate < EvalDate.AddYears(5))
                {
                    nearDate = EvalDate.AddYears(3);
                    farDate = EvalDate.AddYears(5);
                    nearSprd = (double) C3;

                    farSprd = (double) C5;
                }
                else if (_toDate < EvalDate.AddYears(7))
                {
                    nearDate = EvalDate.AddYears(5);
                    farDate = EvalDate.AddYears(7);
                    nearSprd = (double) C5;
                    farSprd = (double) C7;
                }
                else if (_toDate < EvalDate.AddYears(10))
                {
                    nearDate = EvalDate.AddYears(7);
                    farDate = EvalDate.AddYears(10);
                    nearSprd = (double) C7;

                    farSprd = (double) C10;
                }
                else if (_toDate >= EvalDate.AddYears(10))
                {
                    return (double)C10;
                }


                return nearSprd + (farSprd - nearSprd) * ((double)_toDate.Subtract(nearDate).Days / (double)farDate.Subtract(nearDate).Days);
            }
            catch
            {
                return 0;
            }
        }
    }




}
