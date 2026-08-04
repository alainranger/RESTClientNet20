using NUnit.Framework;

namespace RESTClientNet20.Tests
{
	namespace LegacyRestClient.Tests
	{

		[TestFixture]
		public class RestClientTests
		{
			[Test]
			public void CreateRestClient()
			{
				var client = new Library.ApiClient(TestSettings.ApiBaseUrl);
				Assert.IsNotNull(client);
			}

			[Test]
			public void GetHealth()
			{
				var client = new Library.ApiClient(TestSettings.ApiBaseUrl);
				Assert.IsNotNull(client);

				var healthResponse = client.Get<string>("/health");

				Assert.AreEqual(200, healthResponse.StatusCode);
				Assert.AreEqual("Healthy", healthResponse.Data);
			}

			[Test]
			public void GetWeatherForecast()
			{
				var client = new Library.ApiClient(TestSettings.ApiBaseUrl);

				Assert.IsNotNull(client);

				var weatherResponse = client.Get<WeatherForecast[]>("/weatherforecast");

				Assert.AreEqual(200, weatherResponse.StatusCode);
			}
		}
	}
}
