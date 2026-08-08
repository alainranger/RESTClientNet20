namespace RESTClientNet20.Library.ApiClient
{
	/// <summary>
	/// Encapsule le résultat d'un appel à l'API REST.
	/// </summary>
	/// <typeparam name="T">Type de données attendu dans la réponse.</typeparam>
	public class ApiResponse<T>
	{

		/// <summary>
		/// Indique si l'appel à l'API a réussi (code HTTP 2xx).
		/// </summary>
		public bool Success { get; set; }

		/// <summary>
		/// Code HTTP retourné par le serveur.
		/// </summary>
		public int StatusCode { get; set; }

		/// <summary>
		/// Données retournées par l'API, désérialisées en type T.
		/// </summary>
		public T Data { get; set; }

		/// <summary>
		/// Message d'erreur retourné par l'API en cas d'échec.
		/// </summary>
		public string ErrorMessage { get; set; }

		/// <summary>
		/// Contenu JSON brut retourné par le serveur (utile pour le débogage).
		/// </summary>
		public string RawContent { get; set; }
	}
}
