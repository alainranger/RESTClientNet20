using System.Net;
using System.Net.Sockets;

using NUnit.Framework;

using RESTClientNet20.Library.ApiClient;
using RESTClientNet20.Library.ApiResilience;
using RESTClientNet20.Tests.Models;
using RESTClientNet20.Tests.Utils;

namespace RESTClientNet20.Tests
{
	[TestFixture]
	public class ApiClientTests
	{
		[Test]
		public void CreateRestClient_SetsExpectedDefaults()
		{
			using (var server = new TestHttpServer(delegate (TestHttpRequest request, int requestNumber)
			{
				return new TestHttpResponse(200, "text/plain", "ok");
			}))
			{
				var client = new ApiClient(server.BaseUrl);

				Assert.IsNotNull(client);
				Assert.AreEqual(server.BaseUrl, client.BaseUrl);
				Assert.AreEqual(30000, client.Timeout);
				Assert.AreEqual(0, client.OperationTimeout);
				Assert.IsNull(client.RetryPolicy);
				Assert.IsNull(client.CircuitBreaker);
			}
		}

		[Test]
		public void GetHealth_UsesTestHttpServer()
		{
			using (var server = new TestHttpServer(delegate (TestHttpRequest request, int requestNumber)
			{
				return new TestHttpResponse(200, "text/plain", "Healthy");
			}))
			{
				var client = new ApiClient(server.BaseUrl);
				var response = client.Get<string>("/health");

				Assert.IsTrue(response.Success);
				Assert.AreEqual(200, response.StatusCode);
				Assert.AreEqual("Healthy", response.Data);
			}
		}

		[Test]
		public void GetWeatherForecast_UsesTestHttpServer()
		{
			using (var server = new TestHttpServer(delegate (TestHttpRequest request, int requestNumber)
			{
				return new TestHttpResponse(200, "application/json", "[{\"Date\":\"2026-01-01T00:00:00\",\"TemperatureC\":12,\"Summary\":\"Cold\"}]");
			}))
			{
				var client = new ApiClient(server.BaseUrl);
				var response = client.Get<string>("/weatherforecast");

				Assert.IsTrue(response.Success);
				Assert.AreEqual(200, response.StatusCode);
				Assert.IsNotNull(response.RawContent);
				StringAssert.Contains("TemperatureC", response.RawContent);
			}
		}

		[Test]
		public void Post_SerializesBody_AndDeserializesJsonResponse()
		{
			string requestBody = null;
			string method = null;

			using (var server = new TestHttpServer(delegate (TestHttpRequest request, int requestNumber)
			{
				requestBody = request.Body;
				method = request.Method;
				return new TestHttpResponse(200, "application/json", "{\"Message\":\"created\",\"Value\":7}");
			}))
			{
				var client = new ApiClient(server.BaseUrl);
				var payload = new TestPayload
				{
					Message = "hello",
					Value = 3
				};

				var response = client.Post<TestPayload>("/items", payload);

				Assert.IsTrue(response.Success);
				Assert.AreEqual(200, response.StatusCode);
				Assert.AreEqual("POST", method);
				Assert.IsNotNull(requestBody);
				StringAssert.Contains("\"Message\":\"hello\"", requestBody);
				StringAssert.Contains("\"Value\":3", requestBody);
				Assert.IsNotNull(response.Data);
				Assert.AreEqual("created", response.Data.Message);
				Assert.AreEqual(7, response.Data.Value);
			}
		}

		[Test]
		public void PutAndDelete_UseExpectedHttpMethods()
		{
			string firstMethod = null;
			string secondMethod = null;

			using (var server = new TestHttpServer(delegate (TestHttpRequest request, int requestNumber)
			{
				if (requestNumber == 1)
				{
					firstMethod = request.Method;
				}
				else
				{
					secondMethod = request.Method;
				}

				return new TestHttpResponse(200, "application/json", "{\"Message\":\"ok\",\"Value\":1}");
			}))
			{
				var client = new ApiClient(server.BaseUrl);
				var payload = new TestPayload
				{
					Message = "update",
					Value = 9
				};

				var putResponse = client.Put<TestPayload>("/items/1", payload);
				var deleteResponse = client.Delete<string>("/items/1");

				Assert.IsTrue(putResponse.Success);
				Assert.IsTrue(deleteResponse.Success);
				Assert.AreEqual("PUT", firstMethod);
				Assert.AreEqual("DELETE", secondMethod);
			}
		}

		[Test]
		public void Get_HttpError_ReturnsStatusCodeAndRawContent()
		{
			using (var server = new TestHttpServer(delegate (TestHttpRequest request, int requestNumber)
			{
				return new TestHttpResponse(500, "application/json", "{\"error\":\"boom\"}");
			}))
			{
				var client = new ApiClient(server.BaseUrl);

				var response = client.Get<string>("/failure");

				Assert.IsFalse(response.Success);
				Assert.AreEqual(500, response.StatusCode);
				Assert.AreEqual("{\"error\":\"boom\"}", response.RawContent);
				Assert.IsNotNull(response.ErrorMessage);
			}
		}

		[Test]
		public void Get_RetriesTransientFailures_AndReturnsSuccessfulAttempt()
		{
			var callCount = 0;

			using (var server = new TestHttpServer(delegate (TestHttpRequest request, int requestNumber)
			{
				callCount = requestNumber;
				if (requestNumber < 3)
				{
					return new TestHttpResponse(503, "text/plain", "retry");
				}

				return new TestHttpResponse(200, "text/plain", "done");
			}))
			{
				var client = new ApiClient(server.BaseUrl)
				{
					RetryPolicy = new RetryPolicy(3, 0, false)
				};

				var response = client.Get<string>("/retry");

				Assert.IsTrue(response.Success);
				Assert.AreEqual(200, response.StatusCode);
				Assert.AreEqual("done", response.Data);
				Assert.AreEqual(3, callCount);
			}
		}

		[Test]
		public void Get_StopsRetrying_WhenOperationTimeoutWouldBeExceeded()
		{
			var callCount = 0;

			using (var server = new TestHttpServer(delegate (TestHttpRequest request, int requestNumber)
			{
				callCount = requestNumber;
				return new TestHttpResponse(503, "text/plain", "retry");
			}))
			{
				var client = new ApiClient(server.BaseUrl)
				{
					RetryPolicy = new RetryPolicy(5, 50, false),
					OperationTimeout = 10
				};

				var response = client.Get<string>("/timeout");

				Assert.IsFalse(response.Success);
				Assert.AreEqual(503, response.StatusCode);
				Assert.AreEqual(1, callCount);
				StringAssert.Contains("timeout global atteint", response.ErrorMessage);
			}
		}

		[Test]
		public void Get_TransportFailureWithoutHttpResponse_MapsTo408()
		{
			var port = GetUnusedPort();
			var client = new ApiClient("http://127.0.0.1:" + port)
			{
				Timeout = 500
			};

			var response = client.Get<string>("/unreachable");

			Assert.IsFalse(response.Success);
			Assert.AreEqual(408, response.StatusCode);
			Assert.IsNotNull(response.ErrorMessage);
		}

		private static int GetUnusedPort()
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			try
			{
				return ((IPEndPoint)listener.LocalEndpoint).Port;
			}
			finally
			{
				listener.Stop();
			}
		}
	}

	internal delegate TestHttpResponse RequestHandler(TestHttpRequest request, int requestNumber);
}
