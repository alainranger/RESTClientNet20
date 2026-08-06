using RESTClientNet20.Tests.Models;

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RESTClientNet20.Tests.Utils
{
	internal sealed class TestHttpServer : IDisposable
	{
		private readonly TcpListener _listener;
		private readonly Thread _worker;
		private readonly RequestHandler _handler;
		private bool _running;
		private int _requestCount;
		private string _baseUrl;

		public TestHttpServer(RequestHandler handler)
		{
			_handler = handler;
			_listener = new TcpListener(IPAddress.Loopback, 0);
			_listener.Start();
			_running = true;
			_baseUrl = "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port;

			_worker = new Thread(new ThreadStart(Listen))
			{
				IsBackground = true
			};

			_worker.Start();
		}

		public string BaseUrl
		{
			get { return _baseUrl; }
		}

		public void Dispose()
		{
			_running = false;
			try
			{
				_listener.Stop();
			}
			catch
			{
			}

			if (_worker != null && _worker.IsAlive)
			{
				_worker.Join(1000);
			}
		}

		private void Listen()
		{
			while (_running)
			{
				TcpClient client = null;
				try
				{
					client = _listener.AcceptTcpClient();
					HandleClient(client);
				}
				catch (SocketException)
				{
					if (!_running)
					{
						return;
					}
				}
				catch (ObjectDisposedException)
				{
					return;
				}
				finally
				{
					client?.Close();
				}
			}
		}

		private void HandleClient(TcpClient client)
		{
			NetworkStream stream = client.GetStream();
			TestHttpRequest request = ReadRequest(stream);
			if (request == null)
			{
				return;
			}

			_requestCount++;
			TestHttpResponse response = _handler(request, _requestCount);
			WriteResponse(stream, response);
		}

		private TestHttpRequest ReadRequest(NetworkStream stream)
		{
			StreamReader reader = new StreamReader(stream, Encoding.UTF8);
			string requestLine = reader.ReadLine();
			if (requestLine == null)
			{
				return null;
			}

			string[] parts = requestLine.Split(' ');
			TestHttpRequest request = new TestHttpRequest
			{
				Method = parts.Length > 0 ? parts[0] : string.Empty,
				Path = parts.Length > 1 ? parts[1] : string.Empty,
				Headers = new WebHeaderCollection()
			};

			string line = reader.ReadLine();
			while (line != null && line.Length > 0)
			{
				int separatorIndex = line.IndexOf(':');
				if (separatorIndex > 0)
				{
					string name = line.Substring(0, separatorIndex).Trim();
					string value = line.Substring(separatorIndex + 1).Trim();
					request.Headers.Add(name, value);
				}

				line = reader.ReadLine();
			}

			int contentLength = 0;
			string contentLengthValue = request.Headers["Content-Length"];
			if (contentLengthValue != null)
			{
				int.TryParse(contentLengthValue, out contentLength);
			}

			if (string.Compare(request.Headers["Expect"], "100-continue", true) == 0)
			{
				byte[] continueBytes = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
				stream.Write(continueBytes, 0, continueBytes.Length);
				stream.Flush();
			}

			if (contentLength > 0)
			{
				char[] buffer = new char[contentLength];
				int totalRead = 0;
				while (totalRead < contentLength)
				{
					int read = reader.Read(buffer, totalRead, contentLength - totalRead);
					if (read <= 0)
					{
						break;
					}

					totalRead += read;
				}

				request.Body = new string(buffer, 0, totalRead);
			}
			else
			{
				request.Body = null;
			}

			return request;
		}

		private void WriteResponse(NetworkStream stream, TestHttpResponse response)
		{
			string body = response.Body ?? string.Empty;
			byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
			string headers = "HTTP/1.1 " + response.StatusCode + " " + GetReasonPhrase(response.StatusCode) + "\r\n"
				+ "Content-Type: " + response.ContentType + "\r\n"
				+ "Content-Length: " + bodyBytes.Length + "\r\n"
				+ "Connection: close\r\n"
				+ "\r\n";
			byte[] headerBytes = Encoding.ASCII.GetBytes(headers);

			stream.Write(headerBytes, 0, headerBytes.Length);
			if (bodyBytes.Length > 0)
			{
				stream.Write(bodyBytes, 0, bodyBytes.Length);
			}

			stream.Flush();
		}

		private static string GetReasonPhrase(int statusCode)
		{
			switch (statusCode)
			{
				case 200:
					return "OK";
				case 201:
					return "Created";
				case 404:
					return "Not Found";
				case 408:
					return "Request Timeout";
				case 429:
					return "Too Many Requests";
				case 500:
					return "Internal Server Error";
				case 503:
					return "Service Unavailable";
				default:
					return "OK";
			}
		}
	}
}
