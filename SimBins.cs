using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CreditPricing
{

    public class SimBins : List<SimBin> // System.Collections.CollectionBase
    {
        private int nTotalDraws;
        //Private mPortNotl As Double
        private double mAttachNotl;
        private double mDetachNotl;


        // number of sim Bins, maxloss, # of draws in total simulation (pct scale/probability)
        public SimBins(int nBins, int iDraws)
            : base()
        {
            mAttachNotl = 0;
            mDetachNotl = 1;
            nTotalDraws = iDraws;

            for (int i = 0; i <= nBins; i++)
            {
                SimBin obj = new SimBin();
                obj.BinThreshold = i;
                obj.BinID = i.ToString();
                obj.BinCount = 0;
                Add(obj);
            }
        }



        // number of sim Bins, maxloss, # of draws in total simulation (pct scale/probability)
        public SimBins(int nBins, double dMaxLoss, int iDraws, double AttachmentPct, double DetachmentPct, double PortNotl)
            : base()
        {
            mAttachNotl = AttachmentPct * PortNotl;
            mDetachNotl = DetachmentPct * PortNotl;
            nTotalDraws = iDraws;
            for (int i = 1; i <= nBins; i++)
            {
                SimBin obj = new SimBin();
                obj.BinThreshold = ((dMaxLoss - mAttachNotl) / nBins) * i;
                obj.BinID = string.Format("{0:P3}", obj.BinThreshold);
                obj.BinCount = 0;
                Add(obj);
            }
        }


        public void CalcLossBins(mcDraw[] mDistribution)
        {
            // next we can calculate the stddev of our distribution...
            // Sqrt( sqrt(ave(variance))) where variance = (Xi-Mean) for each
            for (int i = 0; i < mDistribution.Length; i++)  // Information.UBound(mDistribution); i++)
            {
                // find WARR (wtd ave recovery rate)  skip zero default draws... 
                this.BinLosses(mDistribution[i].dblLGD);
            }
            PctScale();
        }


        public void CalcNumDefaultBins(mcDraw[] mDistribution)
        {
            for (int i = 0; i < mDistribution.Length; i++)
            {
                BinDefaults(mDistribution[i].nDefaults);
            }
            PctScale();
        }


        public void BinDefaults(int numDefaults)
        {
            foreach (SimBin obj in this)
            {
                if (numDefaults == obj.BinThreshold)
                {
                    obj.Increment();
                    break; // TODO: might not be correct. Was : Exit For
                }
            }
        }


        public void BinLosses(double dLossAmount)
        {
            foreach (SimBin obj in this)
            {
                double trancheloss = Math.Max(dLossAmount - mAttachNotl, 0);
                if (trancheloss <= obj.BinThreshold)
                {
                    obj.Increment();
                    break; // TODO: might not be correct. Was : Exit For
                }
            }
        }

        public void BinLossesOld(double dLossAmount)
        {
            foreach (SimBin obj in this)
            {
                if (dLossAmount <= obj.BinThreshold)
                {
                    obj.Increment();
                    break; // TODO: might not be correct. Was : Exit For
                }
            }
        }


        public void PctScale()
        {
            foreach (SimBin obj in this)
            {
                obj.BinPct = obj.BinCount / nTotalDraws;
            }
        }

        public double Attachment
        {
            get { return this.mAttachNotl; }
            set { mAttachNotl = value; }
        }

        public double Detachment
        {
            get { return mDetachNotl; }
            set { mDetachNotl = value; }
        }


        //return a datatable for processing support of .NET interface
        public DataTable GetProbData
        {
            get
            {
                // create globally unique ID
                string guid = System.Guid.NewGuid().ToString();
                System.Data.DataTable tbl = new System.Data.DataTable("BinLosses " + guid);
                tbl.Columns.Add("BinID", Type.GetType("System.String"));
                //0
                tbl.Columns.Add("BinProbability", Type.GetType("System.Double"));
                //0

                tbl.BeginLoadData();
                foreach (SimBin obj in this)
                {
                    DataRow row = default(DataRow);
                    row = tbl.NewRow();
                    {
                        row[0] = obj.BinID;
                        row[1] = obj.BinPct;
                    }
                    tbl.Rows.Add(row);
                }
                tbl.EndLoadData();
                return tbl;
            }
        }

        //return a datatable for processing support of .NET interface
        public DataTable GetFreqData
        {
            get
            {
                System.Data.DataTable tbl = new System.Data.DataTable("BinDefaults");
                tbl.Columns.Add("BinID", Type.GetType("System.String"));
                //0
                tbl.Columns.Add("BinCount", Type.GetType("System.Int32"));
                //0

                tbl.BeginLoadData();
                foreach (SimBin obj in this)
                {
                    DataRow row = default(DataRow);
                    row = tbl.NewRow();
                    {
                        row[0] = obj.BinID;
                        row[1] = obj.BinCount;
                    }
                    tbl.Rows.Add(row);
                }
                tbl.EndLoadData();
                return tbl;
            }
        }
    }
}