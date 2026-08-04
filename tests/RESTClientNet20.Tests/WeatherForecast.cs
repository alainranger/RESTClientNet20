using System;

namespace RESTClientNet20.Tests
{
	namespace LegacyRestClient.Tests
	{
		internal class WeatherForecast
		{
			private DateTime _date;
			private int _temperatureC;
			private string _summary;

			public WeatherForecast(DateTime date, int temperatureC, string summary)
			{
				this._date = date;
				this._temperatureC = temperatureC;
				this._summary = summary;
			}

			public DateTime Date
			{
				get { return _date; }
			}

			public int TemperatureC
			{
				get { return _temperatureC; }
			}

			public string Summary
			{
				get { return _summary; }
			}

			public int TemperatureF
			{
				get { return 32 + (int)(_temperatureC / 0.5556); }
			}
		}

	}
}
