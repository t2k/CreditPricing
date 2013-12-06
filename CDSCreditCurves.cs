using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CreditPricing
{
    /// <summary>
    /// a generic dictionary of CDSCreditCurve objects, Summary description for CDSCreditCurves
    /// </summary>
    public class CDSCreditCurves : Dictionary<string, CDSCreditCurve>
    {
        private DateTime m_NextIMMDate;
        private Guid[] m_GUIDs;
        private CDSMarketProviders m_mpRequests;

        // SINGLE TRADE ONLY  creates an optimized list of CDS curves  one vs ~3300 for large portfolios
        // new FEB 08  to simplify the interface and limit the amount of data passed over the 'wire'  
        // let's do all the lifting inside the constructor
        //  STEP1: calendar
        //  STEP2: MarketProvider collection
        //  STEP3: get the Murex Trade
        // pass in the Evaluation Date, the MX trade NB and the Murex Instrument

        /// <summary>
        /// constructor optimized for MUREX cds pricing
        /// 2 params:  _date (cds curve date) and _instrument murex instrument name
        /// </summary>
        public CDSCreditCurves(DateTime _dateEval, string _instrument) : base()
        {
            var db = new CreditPricingEntities();
            // find the 'next quarterly (Mar,Dec,Sep,Dec) 20th date (cds roll dates)

            int iRem = 0;
            int iResult = 0;
            iResult = Math.DivRem(_dateEval.Month, (int)3, out iRem);
            if (iRem == 0)
            {
                if (_dateEval.Day < 20)
                {
                    m_NextIMMDate = new DateTime(_dateEval.Year, _dateEval.Month, 20);
                }
                else
                {
                    m_NextIMMDate = new DateTime(_dateEval.Year, _dateEval.Month, 20).AddMonths(3);
                }
            }
            else
            {
                m_NextIMMDate = new DateTime(_dateEval.Year, _dateEval.Month, 20).AddMonths(3 - iRem);
            }

            // create a "STATIC" MarketProvider collection (Only Markit for now)
            List<MarketDataProvider> providers = new List<MarketDataProvider>();
            var mplist = (from m in db.MarketProviders
                          where m.Interface == "TKStatic"
                          select m).ToList();

            foreach (MarketProvider m in mplist)
            {
                providers.Add(new MarketDataProvider(m.ProviderName, m.Interface));
            }

            string strCurveKey = (from t in db.MUREXTKRMAPs
                               where t.INSTRUMENT == _instrument
                               select t.TKR + "," + t.CCY + "," + t.TIER).FirstOrDefault().ToString();

            // use the MX trade object's INSTRUMENT to get the Markit Ticker KEYs (TICKER,CCY,TIER)
            // dsCreditNetTableAdapters.MUREXTKRMAPTableAdapter taMap = new dsCreditNetTableAdapters.MUREXTKRMAPTableAdapter();
            // string strCurveKey = taMap.GetMPTicker(_Instrument);
            // this returns a concatinated string looking like 'IBM,USD,SNRFOR'

            string[] mpKey = strCurveKey.Split(',');
            string strTKR = mpKey[0];
            string strCCY = mpKey[1];
            string strTIER = mpKey[2];

            var singleCurve = (from c in db.mpCDSCurves
                               where c.EvalDate == _dateEval && c.TICKER == strTKR && c.Tier == strTIER && c.CCY == strCCY
                               select c).SingleOrDefault();

            // add a new credit curve to the collection m_COLL
            CDSCreditCurve obj = new CDSCreditCurve(singleCurve, m_NextIMMDate, providers);
            Add(obj.CurveKeyName,obj);
        }

        // create CDSCreditCurves collection ALL MARKIT curves per date (only MARKIT provider)
        public CDSCreditCurves(DateTime _dateEval) : base()
        {
            var db = new CreditPricingEntities();
            //, ByVal providers As Collection)
            int iRem = 0;
            int iResult = 0;
            iResult = Math.DivRem(_dateEval.Month,(int)3,out iRem);
            if (iRem == 0)
            {
                // the month is a quarter
                if (_dateEval.Day < 20)
                {
                    m_NextIMMDate = new DateTime(_dateEval.Year, _dateEval.Month, 20);
                }
                else
                {
                    m_NextIMMDate = new DateTime(_dateEval.Year, _dateEval.Month, 20).AddMonths(3);
                }
            }
            else
            {
                m_NextIMMDate = new DateTime(_dateEval.Year, _dateEval.Month, 20).AddMonths(3 - iRem);
            }

            List<MarketDataProvider> providers = new List<MarketDataProvider>();
            var qry = (from m in db.MarketProviders
                       where m.Interface == "TKStatic"
                       select m).ToList();

            foreach (MarketProvider m in qry)
            {
                providers.Add(new MarketDataProvider(m.ProviderName, m.Interface)); 
            }

            var qryCurvesByDate = (from c in db.mpCDSCurves
                                  where c.EvalDate == _dateEval
                                  orderby c.TICKER
                                  select c).ToList();

            foreach (mpCDSCurve row in qryCurvesByDate)
            {
                try
                {
                    CDSCreditCurve cdsCurve = new CDSCreditCurve(row, m_NextIMMDate, providers);
                    Add(cdsCurve.CurveKeyName, cdsCurve);
                }
                catch 
                {
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_cdsCurves"></param>
        /// <param name="dtEval"></param>
        /// <param name="providers"></param>
        public CDSCreditCurves(List<mpCDSCurve> _cdsCurves, DateTime dtEval, List<MarketDataProvider> providers): base()
        {
            int iRem = 0;
            int iResult = 0;
            iResult = Math.DivRem(dtEval.Month, (int)3, out iRem);
            if (iRem == 0)
            {
                // the month is a quarter
                if (dtEval.Day < 20)
                {
                    m_NextIMMDate = new DateTime(dtEval.Year, dtEval.Month, 20);
                }
                else
                {
                    m_NextIMMDate = new DateTime(dtEval.Year, dtEval.Month, 20).AddMonths(3);
                }
            }
            else
            {
                m_NextIMMDate = new DateTime(dtEval.Year, dtEval.Month, 20).AddMonths(3 - iRem);
            }

            foreach (mpCDSCurve row in _cdsCurves)
            {
                CDSCreditCurve cdsCurve = new CDSCreditCurve(row, m_NextIMMDate, providers);
                Add(cdsCurve.CurveKeyName, cdsCurve);
            }
        }

  
        public CDSMarketProviders RTMPProviders
        {
            get { return m_mpRequests; }
        }

        public Guid[] GUIDs
        {
            get { return m_GUIDs; }
        }

        public Int32 CountRequests
        {
            get
            {
                Int32 iCount = default(Int32);

                foreach (CDSCreditCurve curve in this.Values)
                {
                    foreach (CDSCreditCurvePoint pt in curve.CurvePoints)
                    {
                        iCount += pt.Market.Count;
                    }
                }

                return iCount;
            }
        }

        /// <summary>
        /// find curve by Orbit ID... not used ORBIT is JP Morgan interface curve ID
        /// </summary>
        /// <param name="orbitID"></param>
        /// <returns></returns>
        public CDSCreditCurve GetCurveByOrbitID(string orbitID)
        {
            {
                foreach (CDSCreditCurve curve in this.Values)
                {
                    if (curve.OrbitID == orbitID)
                    {
                        return curve;
                    }
                }
                return null;
            }
        }
    }
}

        // here's the workhorse in terms of setting up the data request structure
        //Function RequestBLP(ByVal BLPFldArray() As Field) As RequestForRealtime()

        //    m_mpRequests = New CDSMarketProviders

        //    ' Iterate through the table rows and subcribe to each BLP TICKER.... 
        //    Dim iReqCtr As Int32 = 0
        //    ' property of this class...
        //    Dim iSize As Int32 = Me.CountRequests

        //    Dim arrReqs(iSize - 1) As RequestForRealtime 'dimension the array of requests..

        //    Dim strCurve As String

        //    'Dim file As System.IO.StreamWriter
        //    'file = My.Computer.FileSystem.OpenTextFileWriter("W:\Inhouse\LTP\mxD\tPOD.log", True)
        //    'file.WriteLine("Here is the first string.")

        //    For Each curve As CDSCreditCurve In Dictionary.Values
        //        For Each pt As CDSCreditCurvePoint In curve.CurvePoints
        //            For Each mp As CDSMarketProvider In pt.Market.Values

        //                If mp.ProviderInterface = "BLPNET" Then

        //                    strCurve = (curve.CorpTKR & curve.CCY & curve.Seniority)  ' "AIGUSDSenior"
        //                    Dim rtreq As New RequestForRealtime

        //                    Try
        //                        rtreq.Securities.Add(mp.CDSProviderTicker)
        //                        'file.WriteLine(mp.CDSProviderTicker)
        //                        rtreq.Fields.AddRange(BLPFldArray)

        //                        ' "AIGUSDSenior;CAIG1U5 CBIN Curncy"  this will be used in reply event...
        //                        rtreq.State = strCurve & ";" & mp.CDSProviderTicker
        //                        'rtreq.Monitor = False
        //                        rtreq.Monitor = True
        //                        rtreq.SubscriptionMode = SubscriptionMode.ByField
        //                        ''''m_mpRequests.Add(iReqCtr.ToString, mp)
        //                        arrReqs(iReqCtr) = rtreq
        //                        iReqCtr += 1
        //                    Catch ex As Exception
        //                        MsgBox(ex.Message & " " & mp.CDSProviderTicker, MsgBoxStyle.Exclamation, "tPOD - (Bloomberg .NET) ERROR")
        //                    End Try

        //                End If
        //            Next mp
        //        Next pt
        //    Next curve
        //    'MsgBox("total r/t requests: " & iReqCtr)

        //    ReDim Preserve arrReqs(iReqCtr - 1)

        //    Return arrReqs
        //    'file.Close()
        //    'Try
        //    'm_GUIDs = MarketDataAdapter.SendRequest(arrReqs)
        //    ''MarketDataAdapter.SendRequest(arrReqs)
        //    'Catch ex As Exception
        //    'MsgBox(ex.Message & " -- RealTime feeds may not update", MsgBoxStyle.Exclamation, "Error during Bloomberg MarketDataAdapter Startup")
        //    'Return m_GUIDs
        //    'End Try

        //    'm_GUIDs = Nothing ' MarketDataAdapter.SendRequest(arrReqs)
        //    'Return m_GUIDs
        //End Function

        // here's the workhorse in terms of setting up the data request structure
        //Function RequestBLPsubset(ByVal coll As Collection, ByVal BLPFldArray() As Field) As RequestForRealtime()
        //    ' Iterate through the table rows and subcribe to each BLP TICKER.... 

        //    Dim iReqCtr As Integer = 0
        //    ' property of this class...
        //    Dim iSize As Integer = Me.CountRequests  ' max
        //    Dim arrReqs(iSize - 1) As RequestForRealtime 'dimension the array of requests..

        //    For Each str As String In coll
        //        For Each curve As CDSCreditCurve In Dictionary.Values
        //            If curve.Name = str Then
        //                For Each pt As CDSCreditCurvePoint In curve.CurvePoints
        //                    For Each mp As CDSMarketProvider In pt.Market.Values
        //                        If mp.ProviderInterface = "BLPNET" Then
        //                            Dim rtreq As New RequestForRealtime
        //                            Try
        //                                rtreq.Securities.Add(mp.CDSProviderTicker)
        //                                rtreq.Fields.AddRange(BLPFldArray)
        //                                ' "AIGUSDSENIOR;CAIG1U5 CBIN Curncy"  this will be used in reply event...
        //                                rtreq.State = curve.Name & ";" & mp.CDSProviderTicker
        //                                rtreq.Monitor = True
        //                                rtreq.SubscriptionMode = SubscriptionMode.ByField
        //                                arrReqs(iReqCtr) = rtreq
        //                                iReqCtr += 1
        //                            Catch ex As Exception
        //                                MsgBox(ex.Message & " " & mp.CDSProviderTicker, MsgBoxStyle.Exclamation, "tPOD - (Bloomberg .NET) ERROR")
        //                            End Try

        //                        End If
        //                    Next mp
        //                Next pt

        //                Exit For ' this will occur after condtion curve.name=str was found
        //            End If

        //        Next curve
        //    Next
        //    ReDim Preserve arrReqs(iReqCtr - 1)
        //    Return arrReqs

        //End Function



        //Sub Save2Database(ByVal ta As dsCreditNetTableAdapters.CDSManualPXTableAdapter, ByVal dtEval As Date) ', ByVal ProviderList As Collection, ByVal cal As tkMarketPricing.CCalendar)

        //    Dim file As System.IO.StreamWriter
        //    file = My.Computer.FileSystem.OpenTextFileWriter("W:\Inhouse\LTP\mxD\tPOD.log", True)
        //    file.WriteLine("tPOD: Save CreditCurves Data on " & dtEval.ToString)

        //    'create a new temp datatable to hold 'todays' CDS prices
        //    Dim dt As New dsCreditNet.CDSManualPXDataTable
        //    Dim row As dsCreditNet.CDSManualPXRow

        //    dt.BeginLoadData()

        //    For Each curve As CDSCreditCurve In Dictionary.Values
        //        For Each pt As CDSCreditCurvePoint In curve.CurvePoints
        //            For Each mp As CDSMarketProvider In pt.Market.Values
        //                With mp
        //                    If .Bid <> 0 And .Ask <> 0 Then
        //                        ' create a new row
        //                        row = dt.NewCDSManualPXRow
        //                        With row
        //                            .pxDate = dtEval
        //                            .CORP_TICKER = curve.CorpTKR
        //                            .CCY = curve.CCY
        //                            .SENIORITY = curve.Seniority
        //                            .ProviderName = mp.Provider
        //                            .TERM = pt.CDSTerm
        //                            .Bid = Math.Round(mp.Bid * 100, 2)
        //                            .Ask = Math.Round(mp.Ask * 100, 2)
        //                        End With

        //                        Try
        //                            dt.AddCDSManualPXRow(row)
        //                        Catch ex As Exception
        //                            file.WriteLine(row.pxDate.ToShortDateString & ";" & row.CORP_TICKER & ";" & row.CCY & ";" & row.SENIORITY & ";" & row.ProviderName & ";" & row.TERM & ";" & row.Bid & ";" & row.Ask)
        //                        End Try
        //                        row = Nothing

        //                    End If
        //                End With
        //            Next mp
        //        Next pt
        //    Next curve
        //    file.Close()
        //    dt.EndLoadData()

        //    ' now we update the database...
        //    Try
        //        ' first delete any records for todays date
        //        ta.DeleteByPxDate(dtEval)
        //        ta.Update(dt)
        //    Catch ex As Exception
        //        MsgBox(ex.Message)
        //    End Try
        //End Sub







        // construct the whole shebang by passing in a single CorpTickerDataTable object 
        // convieniently supplied by our favourite DataSet... dsCreditCurves
        //Sub New(ByVal ds As dsCreditNet, ByVal ProviderList As Collection, ByVal cal As CCalendar)
        //    MyBase.New()
        //    m_Cal = cal

        //    m_NextIMMDate = m_Cal.NextIMMFrom(m_Cal.TodayDate.AddDays(1))
        //    ' heres a stupid way to convert this ALWAYS to the 20th of the month...  my next IMMDate is always a business date
        //    m_NextIMMDate = DateValue(String.Format("{0}/{1}/{2}", Month(m_NextIMMDate), 20, Year(m_NextIMMDate).ToString))


        //    'scanning a parent/child dataset 
        //    For Each row As dsCreditNet.CorpTickerRow In ds.CorpTicker.Rows
        //        'For Each row As DsCreditCurves.CorpTickerRow In ds.Tables("CDSCurves").Rows
        //        ' add a new credit curve to the collection m_COLL
        //        Dim obj As New CDSCreditCurve
        //        With obj
        //            .CorpTKR = row.CORP_TICKER
        //            .CCY = row.CCY
        //            .Seniority = row.SENIORITY
        //            .GROUP = row.INDUSTRY_GROUP
        //            .Sector = row.INDUSTRY_SECTOR
        //            .SubGROUP = row.INDUSTRY_SUBGROUP
        //            .RefEnt = row.REFERENCE_ENTITY
        //            .RefOb = row.REFERENCE_OBLIGATION
        //            .EQTicker = row.EQUITY_TICKER

        //            Try
        //                .BBCorpTKR = row.BBG_CORP_TIKER
        //            Catch ex As Exception
        //                .BBCorpTKR = ""
        //            End Try

        //            .Recovery = CDbl(0.4)

        //            Try
        //                .OrbitID = row.JPMOrbitCurveID
        //            Catch ex As Exception
        //                .OrbitID = 0
        //            End Try

        //            ' creating a CreditCurve object...  
        //            ' for this credit curve we now will all all of the CreditCurvePoints as
        //            ' defined in our database/dataset ala the user interface...
        //            ' add a new CreditCurvePoint

        //            Dim arrRows() As DataRow
        //            arrRows = row.GetChildRows("CorpTicker->CreditCurvePoint")
        //            obj.CurvePoints = New CDSCreditCurvePoints(arrRows, ProviderList, m_NextIMMDate)

        //            ' so the key would look like "AIGUSDSENIOR"
        //            'Add(obj.CorpTKR & obj.CCY & obj.Seniority, obj)
        //            Add(obj.Name, obj)
        //        End With
        //    Next
        //End Sub




        // a must to override....
        //public void Add(string strCurveKey, CDSCreditCurve cdsCurve)
        //{
        //    //Try
        //    //Catch ex As Exception
        //    //End Try
        //    base.Dictionary.Add(strCurveKey, cdsCurve);
        //}

        //// a must to override
        //public void Remove(string strKey)
        //{
        //    try
        //    {
        //        base.Dictionary.Remove(strKey);
        //    }
        //    catch (Exception ex)
        //    {
        //    }
        //}


        //// a must to override..
        //public CDSCreditCurve Item
        //{
        //    get { return (CDSCreditCurve)base.Dictionary.Item(strKey); }
        //}

        //public ICollection Values
        //{
        //    get { return base.Dictionary.Values; }
        //}

        //public ICollection Keys
        //{
        //    get { return base.Dictionary.Keys; }
        //}

