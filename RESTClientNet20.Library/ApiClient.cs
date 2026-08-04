using Newtonsoft.Json;

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace RESTClientNet20.Library
{
	public enum HttpMethodType
	{
		Get,
		Post,
		Put,
		Delete
	}

	/// <summary>
	/// Client HTTP générique pour consommer une API REST, avec retry, circuit breaker et timeout.
	/// Compatible .NET Framework 2.0 (pas de LINQ, pas de var, pas de lambdas).
	/// </summary>
	public class ApiClient
	{
		private string _baseUrl;
		private int _requestTimeout;
		private int _operationTimeoutMilliseconds;
		private WebHeaderCollection _defaultHeaders;
		private RetryPolicy _retryPolicy;
		private CircuitBreaker _circuitBreaker;

		public ApiClient(string baseUrl)
		{
			_baseUrl = baseUrl;
			_requestTimeout = 30000; // timeout par tentative (ms)
			_operationTimeoutMilliseconds = 0; // 0 = pas de limite globale
			_defaultHeaders = new WebHeaderCollection();
			_retryPolicy = null;
			_circuitBreaker = null;
		}

		public string BaseUrl
		{
			get { return _baseUrl; }
			set { _baseUrl = value; }
		}

		/// <summary>
		/// Timeout par tentative HTTP, en millisecondes.
		/// </summary>
		public int Timeout
		{
			get { return _requestTimeout; }
			set { _requestTimeout = value; }
		}

		/// <summary>
		/// Timeout global couvrant l'ensemble des tentatives (0 = désactivé).
		/// Si dépassé, la boucle de retry s'arrête même s'il reste des tentatives disponibles.
		/// </summary>
		public int OperationTimeout
		{
			get { return _operationTimeoutMilliseconds; }
			set { _operationTimeoutMilliseconds = value; }
		}

		/// <summary>
		/// Politique de retry. Laisser à null pour désactiver le retry (comportement par défaut).
		/// </summary>
		public RetryPolicy RetryPolicy
		{
			get { return _retryPolicy; }
			set { _retryPolicy = value; }
		}

		/// <summary>
		/// Circuit breaker partagé. Laisser à null pour désactiver (comportement par défaut).
		/// Créer une seule instance de CircuitBreaker par ApiClient (ou par ressource distante)
		/// et la réutiliser entre les appels pour que le compteur d'échecs soit cumulatif.
		/// </summary>
		public CircuitBreaker CircuitBreaker
		{
			get { return _circuitBreaker; }
			set { _circuitBreaker = value; }
		}

		public void AddDefaultHeader(string name, string value)
		{
			_defaultHeaders.Add(name, value);
		}

		public ApiResponse<T> Get<T>(string relativeUrl)
		{
			return Execute<T>(HttpMethodType.Get, relativeUrl, null);
		}

		public ApiResponse<T> Post<T>(string relativeUrl, object body)
		{
			return Execute<T>(HttpMethodType.Post, relativeUrl, body);
		}

		public ApiResponse<T> Put<T>(string relativeUrl, object body)
		{
			return Execute<T>(HttpMethodType.Put, relativeUrl, body);
		}

		public ApiResponse<T> Delete<T>(string relativeUrl)
		{
			return Execute<T>(HttpMethodType.Delete, relativeUrl, null);
		}

		private ApiResponse<T> Execute<T>(HttpMethodType method, string relativeUrl, object body)
		{
			DateTime operationStart = DateTime.Now;
			int attempt = 0;
			ApiResponse<T> response = new ApiResponse<T>();

			while (true)
			{
				attempt++;

				if (_circuitBreaker != null && !_circuitBreaker.CanExecute())
				{
					response.Success = false;
					response.StatusCode = 0;
					response.ErrorMessage = "Circuit breaker ouvert : les appels sont temporairement bloqués.";
					return response;
				}

				ExecuteOnce<T>(method, relativeUrl, body, response);

				if (response.Success)
				{
					if (_circuitBreaker != null)
					{
						_circuitBreaker.RecordSuccess();
					}

					return response;
				}

				if (_circuitBreaker != null)
				{
					_circuitBreaker.RecordFailure();
				}

				bool canRetry = _retryPolicy != null
					&& attempt < _retryPolicy.MaxAttempts
					&& _retryPolicy.ShouldRetry(response.StatusCode);

				if (!canRetry)
				{
					return response;
				}

				int delay = _retryPolicy.GetDelay(attempt);

				if (_operationTimeoutMilliseconds > 0)
				{
					double elapsed = (DateTime.Now - operationStart).TotalMilliseconds;

					if (elapsed + delay >= _operationTimeoutMilliseconds)
					{
						response.ErrorMessage = response.ErrorMessage
							+ " (timeout global atteint, tentatives arrêtées après " + attempt + " essai(s))";
						return response;
					}
				}

				Thread.Sleep(delay);
			}
		}

		/// <summary>
		/// Exécute une seule tentative HTTP et remplit directement l'objet response fourni.
		/// </summary>
		private void ExecuteOnce<T>(HttpMethodType method, string relativeUrl, object body, ApiResponse<T> response)
		{
			HttpWebRequest request = null;

			try
			{
				string url = _baseUrl + relativeUrl;
				request = (HttpWebRequest)WebRequest.Create(url);
				request.Method = MethodToString(method);
				request.Timeout = _requestTimeout;
				request.ContentType = "application/json";
				request.Accept = "application/json";

				foreach (string key in _defaultHeaders.AllKeys)
				{
					request.Headers.Add(key, _defaultHeaders[key]);
				}

				if (body != null && (method == HttpMethodType.Post || method == HttpMethodType.Put))
				{
					string json = JsonConvert.SerializeObject(body);
					byte[] data = Encoding.UTF8.GetBytes(json);
					request.ContentLength = data.Length;

					Stream requestStream = request.GetRequestStream();
					try
					{
						requestStream.Write(data, 0, data.Length);
					}
					finally
					{
						requestStream.Close();
					}
				}

				HttpWebResponse webResponse = (HttpWebResponse)request.GetResponse();
				try
				{
					response.StatusCode = (int)webResponse.StatusCode;
					response.RawContent = ReadStream(webResponse.GetResponseStream());
					response.Success = true;
					response.ErrorMessage = null;

					if (response.RawContent != null && response.RawContent.Length > 0)
					{
						if (typeof(T) == typeof(string))
						{
							response.Data = (T)(object)response.RawContent;
						}
						else
						{
							response.Data = JsonConvert.DeserializeObject<T>(response.RawContent);
						}
					}
				}
				finally
				{
					webResponse.Close();
				}
			}
			catch (WebException webEx)
			{
				response.Success = false;

				if (webEx.Response != null)
				{
					HttpWebResponse errorResponse = (HttpWebResponse)webEx.Response;
					response.StatusCode = (int)errorResponse.StatusCode;
					response.RawContent = ReadStream(errorResponse.GetResponseStream());
					errorResponse.Close();
				}
				else
				{
					// Timeout, refus de connexion, DNS, etc. : pas de code HTTP disponible.
					// On utilise 408 pour que la politique de retry par défaut le considère comme transitoire.
					response.StatusCode = 408;
				}

				response.ErrorMessage = webEx.Message;
			}
			catch (Exception ex)
			{
				response.Success = false;
				response.StatusCode = 0;
				response.ErrorMessage = ex.Message;
			}
		}

		private string MethodToString(HttpMethodType method)
		{
			switch (method)
			{
				case HttpMethodType.Get:
					return "GET";
				case HttpMethodType.Post:
					return "POST";
				case HttpMethodType.Put:
					return "PUT";
				case HttpMethodType.Delete:
					return "DELETE";
				default:
					return "GET";
			}
		}

		private string ReadStream(Stream stream)
		{
			if (stream == null)
			{
				return null;
			}

			StreamReader reader = new StreamReader(stream, Encoding.UTF8);
			try
			{
				return reader.ReadToEnd();
			}
			finally
			{
				reader.Close();
			}
		}
	}
}
