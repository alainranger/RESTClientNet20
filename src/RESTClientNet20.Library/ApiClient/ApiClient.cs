using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

using Newtonsoft.Json;

using RESTClientNet20.Library.ApiResilience;

namespace RESTClientNet20.Library.ApiClient
{

	/// <summary>
	/// Client HTTP générique pour consommer une API REST, avec retry, circuit breaker et timeout.
	/// Compatible .NET Framework 2.0 (pas de LINQ, pas de var, pas de lambdas).
	/// </summary>
	public class ApiClient
	{
		private readonly WebHeaderCollection _defaultHeaders = new WebHeaderCollection();

		/// <summary>
		/// Initialise un nouveau client API avec l'URL de base spécifiée.
		/// </summary>
		/// <param name="baseUrl">L'URL de base de l'API.</param>
		public ApiClient(string baseUrl) => BaseUrl = baseUrl;

		/// <summary>
		/// URL de base de l'API.
		/// </summary>
		public string BaseUrl { get; set; }

		/// <summary>
		/// Timeout par tentative HTTP, en millisecondes.
		/// </summary>
		public int Timeout { get; set; } = 30000;

		/// <summary>
		/// Timeout global couvrant l'ensemble des tentatives (0 = désactivé).
		/// Si dépassé, la boucle de retry s'arrête même s'il reste des tentatives disponibles.
		/// </summary>
		public int OperationTimeout { get; set; } = 0;

		/// <summary>
		/// Politique de retry. Laisser à null pour désactiver le retry (comportement par défaut).
		/// </summary>
		public RetryPolicy RetryPolicy { get; set; } = null;

		/// <summary>
		/// Circuit breaker partagé. Laisser à null pour désactiver (comportement par défaut).
		/// Créer une seule instance de CircuitBreaker par ApiClient (ou par ressource distante)
		/// et la réutiliser entre les appels pour que le compteur d'échecs soit cumulatif.
		/// </summary>
		public CircuitBreaker CircuitBreaker { get; set; } = null;

		/// <summary>
		/// Ajoute un en-tête HTTP par défaut qui sera inclus dans toutes les requêtes.
		/// </summary>
		/// <param name="name">Le nom de l'en-tête.</param>
		/// <param name="value">La valeur de l'en-tête.</param>
		public void AddDefaultHeader(string name, string value) => _defaultHeaders.Add(name, value);

		/// <summary>
		/// Effectue une requête HTTP GET vers l'URL relative spécifiée et retourne la réponse désérialisée en type T. 
		/// </summary>
		/// <typeparam name="T">Le type de la réponse attendue.</typeparam>
		/// <param name="relativeUrl">L'URL relative de la ressource.</param>
		/// <returns>La réponse de l'API désérialisée en type T.</returns>
		public ApiResponse<T> Get<T>(string relativeUrl) => Execute<T>(HttpMethodType.Get, relativeUrl, null);

		/// <summary>
		/// Effectue une requête HTTP POST vers l'URL relative spécifiée avec le corps fourni et retourne la réponse désérialisée en type T.
		/// </summary>
		/// <typeparam name="T">Le type de la réponse attendue.</typeparam>
		/// <param name="relativeUrl">L'URL relative de la ressource.</param>
		/// <param name="body">Le corps de la requête.</param>
		/// <returns>La réponse de l'API désérialisée en type T.</returns>
		public ApiResponse<T> Post<T>(string relativeUrl, object body) => Execute<T>(HttpMethodType.Post, relativeUrl, body);

		/// <summary>
		/// Effectue une requête HTTP PUT vers l'URL relative spécifiée avec le corps fourni et retourne la réponse désérialisée en type T.
		/// </summary>
		/// <typeparam name="T">Le type de la réponse attendue.</typeparam>
		/// <param name="relativeUrl">L'URL relative de la ressource.</param>
		/// <param name="body">Le corps de la requête.</param>
		/// <returns>La réponse de l'API désérialisée en type T.</returns>
		public ApiResponse<T> Put<T>(string relativeUrl, object body) => Execute<T>(HttpMethodType.Put, relativeUrl, body);

		/// <summary>
		///	Effectue une requête HTTP DELETE vers l'URL relative spécifiée et retourne la réponse désérialisée en type T.
		/// </summary>
		/// <typeparam name="T">Le type de la réponse attendue.</typeparam>
		/// <param name="relativeUrl">L'URL relative de la ressource.</param>
		/// <returns>La réponse de l'API désérialisée en type T.</returns>
		public ApiResponse<T> Delete<T>(string relativeUrl) => Execute<T>(HttpMethodType.Delete, relativeUrl, null);

		/// <summary>
		/// Effectue une requête HTTP avec la méthode, l'URL relative et le corps spécifiés, en gérant le retry, le circuit breaker et le timeout global.
		/// </summary>
		/// <typeparam name="T">Le type de la réponse attendue.</typeparam>
		/// <param name="method">La méthode HTTP à utiliser.</param>
		/// <param name="relativeUrl">L'URL relative de la ressource.</param>
		/// <param name="body">Le corps de la requête.</param>
		/// <returns>La réponse de l'API désérialisée en type T.</returns>
		private ApiResponse<T> Execute<T>(HttpMethodType method, string relativeUrl, object body)
		{
			var operationStart = DateTime.Now;
			var attempt = 0;
			var response = new ApiResponse<T>();

			while (true)
			{
				attempt++;

				if (CircuitBreaker != null && !CircuitBreaker.CanExecute())
				{
					response.Success = false;
					response.StatusCode = 0;
					response.ErrorMessage = "Circuit breaker ouvert : les appels sont temporairement bloqués.";
					return response;
				}

				ExecuteOnce<T>(method, relativeUrl, body, response);

				if (response.Success)
				{
					CircuitBreaker?.RecordSuccess();

					return response;
				}

				CircuitBreaker?.RecordFailure();

				var canRetry = RetryPolicy != null
					&& attempt < RetryPolicy.MaxAttempts
					&& RetryPolicy.ShouldRetry(response.StatusCode);

				if (!canRetry)
				{
					return response;
				}

				var delay = RetryPolicy.GetDelay(attempt);

				if (OperationTimeout > 0)
				{
					var elapsed = (DateTime.Now - operationStart).TotalMilliseconds;

					if (elapsed + delay >= OperationTimeout)
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
		/// <typeparam name="T">Le type de la réponse attendue.</typeparam>
		/// <param name="method">La méthode HTTP à utiliser.</param>
		/// <param name="relativeUrl">L'URL relative de la ressource.</param>
		/// <param name="body">Le corps de la requête.</param>
		/// <param name="response">L'objet ApiResponse à remplir.</param>
		/// <exception cref="WebException">Lancée lorsque la requête HTTP échoue.</exception>
		/// <exception cref="Exception">Lancée lorsque une erreur non prévue se produit.</exception>
		private void ExecuteOnce<T>(HttpMethodType method, string relativeUrl, object body, ApiResponse<T> response)
		{
			try
			{
				var url = BaseUrl + relativeUrl;
				var request = (HttpWebRequest)WebRequest.Create(url);
				request.Method = MethodToString(method);
				request.Timeout = Timeout;
				request.ContentType = "application/json";
				request.Accept = "application/json";

				foreach (var key in _defaultHeaders.AllKeys)
				{
					request.Headers.Add(key, _defaultHeaders[key]);
				}

				if (body != null && (method == HttpMethodType.Post || method == HttpMethodType.Put))
				{
					var json = JsonConvert.SerializeObject(body);
					var data = Encoding.UTF8.GetBytes(json);
					request.ContentLength = data.Length;

					var requestStream = request.GetRequestStream();
					try
					{
						requestStream.Write(data, 0, data.Length);
					}
					finally
					{
						requestStream.Close();
					}
				}

				var webResponse = (HttpWebResponse)request.GetResponse();
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
					var errorResponse = (HttpWebResponse)webEx.Response;
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

		/// <summary>
		/// Convertit le type HttpMethodType en chaîne de caractères HTTP standard (GET, POST, PUT, DELETE).
		/// </summary>
		/// <param name="method">Le type de la méthode HTTP.</param>
		/// <returns>La chaîne de caractères représentant la méthode HTTP.</returns>
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

		/// <summary>
		/// Lit le contenu d'un flux et le retourne sous forme de chaîne UTF-8. Ferme le flux après lecture.
		/// </summary>
		/// <param name="stream">Le flux à lire.</param>
		/// <returns>Le contenu du flux sous forme de chaîne UTF-8.</returns>
		private string ReadStream(Stream stream)
		{
			if (stream == null)
			{
				return null;
			}

			var reader = new StreamReader(stream, Encoding.UTF8);
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
