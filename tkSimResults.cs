using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CreditPricing
{

    // **** DICTIONARY COLLECTION OF tkSimResult objects *****
    // strongly typed tkSimResult Object that are stored and retrieved by string key...
    // can be used to retr
    public class tkSimResults : Dictionary<string, tkSimResult> // System.Collections.DictionaryBase
    {
        // a strongly typed collection class to hold tkSimResult Objects...

        public tkSimResults()
            : base()
        {

        }



        // this can be extended ...
        //return a datatable for processing support of .NET interface
        public DataTable GetData
        {
            get
            {
                System.Data.DataTable tbl = new System.Data.DataTable("SimBins");
                tbl.Columns.Add("Portfolio", Type.GetType("System.String"));
                //0
                tbl.Columns.Add("nCredits", Type.GetType("System.Int32"));
                //0

                tbl.BeginLoadData();
                foreach (tkSimResult obj in this.Values)
                {
                    DataRow row = default(DataRow);
                    row = tbl.NewRow();
                    {
                        row[0] = obj.Portfolio;
                        row[1] = obj.PortCount;
                    }
                    tbl.Rows.Add(row);
                }
                tbl.EndLoadData();
                return tbl;
            }
        }
    }
}