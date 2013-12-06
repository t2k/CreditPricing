using System;

namespace CreditPricing
{
    public static class tkStats
    {

        /// <summary>
        /// implement the Cumulative standard normal distribution (mean=0 and StdDev=1) 
        /// from microsoft: http://support.microsoft.com/?kbid=214111
        /// http://www.mail-archive.com/advanced-dotnet@discuss.develop.com/msg06173.html
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static double NormSDist(double x)
        {
            double Z = 1 / Math.Sqrt(2 * Math.PI) * Math.Exp(-Math.Pow(x, 2) / 2);
            double p = 0.2316419;
            double b1 = 0.31938153;
            double b2 = -0.356563782;
            double b3 = 1.781477937;
            double b4 = -1.821255978;
            double b5 = 1.330274429;
            double t = 1 / (1 + p * x);
            return 1 - Z * (b1 * t + b2 * Math.Pow(t, 2) + b3 * Math.Pow(t, 3) + b4 * Math.Pow(t, 4) + b5 * Math.Pow(t, 5));
        }



        /// <summary>
        /// implement normal standard inverse
        /// The following describes an algorithm for computing the inverse normal cumulative 
        /// distribution function where the relative error has an absolute value less than 1.15·10−9 
        /// in the entire region. References to other algorithms are included.
        /// SEE HERE http://home.online.no/~pjacklam/notes/invnorm/#Visual_Basic
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static double NormSInv(double p)
        {
            double q = 0;
            double r = 0;

            //Coefficients in rational approximations.

            const double A1 = -39.6968302866538;
            const double A2 = 220.946098424521;
            const double A3 = -275.928510446969;
            const double A4 = 138.357751867269;
            const double A5 = -30.6647980661472;
            const double A6 = 2.50662827745924;

            const double B1 = -54.4760987982241;
            const double B2 = 161.585836858041;
            const double B3 = -155.698979859887;
            const double B4 = 66.8013118877197;
            const double B5 = -13.2806815528857;

            const double C1 = -0.00778489400243029;
            const double C2 = -0.322396458041136;
            const double C3 = -2.40075827716184;
            const double C4 = -2.54973253934373;
            const double C5 = 4.37466414146497;
            const double C6 = 2.93816398269878;

            const double D1 = 0.00778469570904146;
            const double D2 = 0.32246712907004;
            const double D3 = 2.445134137143;
            const double D4 = 3.75440866190742;

            //Define break-points.

            const double P_LOW = 0.02425;
            const double P_HIGH = 1 - P_LOW;

            if (p > 0 & p < P_LOW)
            {

                //Rational approximation for lower region.

                q = Math.Sqrt(-2 * Math.Log(p));


                return (((((C1 * q + C2) * q + C3) * q + C4) * q + C5) * q + C6) / ((((D1 * q + D2) * q + D3) * q + D4) * q + 1);
            }
            else if (p >= P_LOW & p <= P_HIGH)
            {

                //Rational approximation for central region.

                q = p - 0.5;
                r = q * q;


                return (((((A1 * r + A2) * r + A3) * r + A4) * r + A5) * r + A6) * q / (((((B1 * r + B2) * r + B3) * r + B4) * r + B5) * r + 1);
            }
            else if (p > P_HIGH & p < 1)
            {

                //Rational approximation for upper region.

                q = Math.Sqrt(-2 * Math.Log(1 - p));

                return -(((((C1 * q + C2) * q + C3) * q + C4) * q + C5) * q + C6) / ((((D1 * q + D2) * q + D3) * q + D4) * q + 1);
            }
            else
            {
                return 0;
                // throw new ArgumentOutOfRangeException();
            }
        }
    }
}