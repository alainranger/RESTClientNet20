using NUnit.Framework;

using RESTClientNet20.Library;
using RESTClientNet20.Tests.Models;
using RESTClientNet20.Tests.Utils;

using System.Net;
using System.Net.Sockets;

namespace RESTClientNet20.Tests
{
	[TestFixture]
	public class ApiClientTests
	{
		[Test]
		public void CreateRestClient_SetsExpectedDefaults()
		{
			using (TestHttpServer server = new TestHttpServer(delegate (TestHttpRequest request, int requestNumber)
			{
				return new TestHttpResponse(200, "text/plain", "ok");
			}))
			{
				ApiClient client = new ApiClient(server.BaseUrl);

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
			using (TestHttpServer server = new TestHttpServer(delegate (TestHttpRequest request, int requestNumber)
			{
				return new TestHttpResponse(200, "text/plain", "Healthy");
			}))
			{
				ApiClient client = new ApiClient(server.BaseUrl);
				ApiResponse<string> response = client.Get<string>("/health");

				Assert.IsTrue(response.Success);
				Assert.AreEqual(200, response.StatusCode);
				Assert.AreEqual("Healthy", response.Data);
			}
		}

		[Test]
		public void GetWeatherForecast_UsesTestHttpServer()
		{
			using (TestHttpServer server = new TestHttpServer(delegate (TestHttpRequest request, int requestNumber)
			{
				return new TestHttpResponse(200, "application/json", "[{\"Date\":\"2026-01-01T00:00:00\",\"TemperatureC\":12,\"Summary\":\"Cold\"}]");
			}))
			{
				ApiClient client = new ApiClient(server.BaseUrl);
				ApiResponse<string> response = client.Get<string>("/weatherforecast");

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

			using (TestHttpServer server = new TestHttpServer(delegate (TestHttpRequest request, int requestNumber)
			{
				requestBody = request.Body;
				method = request.Method;
				return new TestHttpResponse(200, "application/json", "{\"Message\":\"created\",\"Value\":7}");
			}))
			{
				ApiClient client = new ApiClient(server.BaseUrl);
				TestPayload payload = new TestPayload
				{
					Message = "hello",
					Value = 3
				};

				ApiResponse<TestPayload> response = client.Post<TestPayload>("/items", payload);

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

			using (TestHttpServer server = new TestHttpServer(delegate (TestHttpRequest request, int requestNumber)
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
				ApiClient client = new ApiClient(server.BaseUrl);
				TestPayload payload = new TestPayload
				{
					Message = "update",
					Value = 9
				};

				ApiResponse<TestPayload> putResponse = client.Put<TestPayload>("/items/1", payload);
				ApiResponse<string> deleteResponse = client.Delete<string>("/items/1");

				Assert.IsTrue(putResponse.Success);
				Assert.IsTrue(deleteResponse.Success);
				Assert.AreEqual("PUT", firstMethod);
				Assert.AreEqual("DELETE", secondMethod);
			}
		}

		[Test]
		public void Get_HttpError_ReturnsStatusCodeAndRawContent()
		{
			using (TestHttpServer server = new TestHttpServer(delegate (TestHttpRequest request, int requestNumber)
			{
				return new TestHttpResponse(500, "application/json", "{\"error\":\"boom\"}");
			}))
			{
				ApiClient client = new ApiClient(server.BaseUrl);

				ApiResponse<string> response = client.Get<string>("/failure");

				Assert.IsFalse(response.Success);
				Assert.AreEqual(500, response.StatusCode);
				Assert.AreEqual("{\"error\":\"boom\"}", response.RawContent);
				Assert.IsNotNull(response.ErrorMessage);
			}
		}

		[Test]
		public void Get_RetriesTransientFailures_AndReturnsSuccessfulAttempt()
		{
			int callCount = 0;

			using (TestHttpServer server = new TestHttpServer(delegate (TestHttpRequest request, int requestNumber)
			{
				callCount = requestNumber;
				if (requestNumber < 3)
				{
					return new TestHttpResponse(503, "text/plain", "retry");
				}

				return new TestHttpResponse(200, "text/plain", "done");
			}))
			{
				ApiClient client = new ApiClient(server.BaseUrl)
				{
					RetryPolicy = new RetryPolicy(3, 0, false)
				};

				ApiResponse<string> response = client.Get<string>("/retry");

				Assert.IsTrue(response.Success);
				Assert.AreEqual(200, response.StatusCode);
				Assert.AreEqual("done", response.Data);
				Assert.AreEqual(3, callCount);
			}
		}

		[Test]
		public void Get_StopsRetrying_WhenOperationTimeoutWouldBeExceeded()
		{
			int callCount = 0;

			using (TestHttpServer server = new TestHttpServer(delegate (TestHttpRequest request, int requestNumber)
			{
				callCount = requestNumber;
				return new TestHttpResponse(503, "text/plain", "retry");
			}))
			{
				ApiClient client = new ApiClient(server.BaseUrl)
				{
					RetryPolicy = new RetryPolicy(5, 50, false),
					OperationTimeout = 10
				};

				ApiResponse<string> response = client.Get<string>("/timeout");

				Assert.IsFalse(response.Success);
				Assert.AreEqual(503, response.StatusCode);
				Assert.AreEqual(1, callCount);
				StringAssert.Contains("timeout global atteint", response.ErrorMessage);
			}
		}

		[Test]
		public void Get_TransportFailureWithoutHttpResponse_MapsTo408()
		{
			int port = GetUnusedPort();
			ApiClient client = new ApiClient("http://127.0.0.1:" + port)
			{
				Timeout = 500
			};

			ApiResponse<string> response = client.Get<string>("/unreachable");

			Assert.IsFalse(response.Success);
			Assert.AreEqual(408, response.StatusCode);
			Assert.IsNotNull(response.ErrorMessage);
		}

		private static int GetUnusedPort()
		{
			TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
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
