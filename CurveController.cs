using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using CreditPricing;

// Liquidity Spread Controllers
namespace ZD.Controllers
{
	public class CurveController : ApiController
	{
		// GET /zd-api/<controller>/id/_date
		public dynamic Get(string id, DateTime? _date)
		{
			try
			{
				var data = new CreditPricingEntities();
				List<tsData2> series = new List<tsData2>();
				YldCurve yc = new YldCurve(id, _date.Value);
				Calendar cal = new Calendar(yc.CCY,_date.Value); // options.HolidayCenter, options.curveDate);
				for (var i = 0; i < 12; i++)
				{
					tsData2 seriesItem = new tsData2();
					seriesItem.Period = string.Format("{0}Y", i+1);
					seriesItem.Date = cal.FarDate(_date.Value, seriesItem.Period);
					seriesItem.Yield = yc.ZeroRate(seriesItem.Date);
					seriesItem.discFactor = yc.GetDF(seriesItem.Date);
					series.Add(seriesItem);
				}

				return new
				{
					status = "success",
					series = series,
					properties = new {
						curvename = id,
						timestamp = yc.ycTime,
						zerocurve = yc.DisplayZero(),
						ratecodes = yc.GetPrices(),  // perfect returns an array of rates with bid/ask/cls
					}
				};
			}
			catch (Exception e)
			{
				return new
				{
					status = "error",
					message = e.Message
				};
			}
		}
	}
}