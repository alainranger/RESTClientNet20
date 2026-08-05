namespace RESTClientNet20.Library
{
	/// <summary>
	/// Encapsule le résultat d'un appel à l'API REST.
	/// </summary>
	/// <typeparam name="T">Type de données attendu dans la réponse.</typeparam>
	public class ApiResponse<T>
	{
		private bool _success;
		private int _statusCode;
		private T _data;
		private string _errorMessage;
		private string _rawContent;

		public bool Success
		{
			get { return _success; }
			set { _success = value; }
		}

		public int StatusCode
		{
			get { return _statusCode; }
			set { _statusCode = value; }
		}

		public T Data
		{
			get { return _data; }
			set { _data = value; }
		}

		public string ErrorMessage
		{
			get { return _errorMessage; }
			set { _errorMessage = value; }
		}

		/// <summary>
		/// Contenu JSON brut retourné par le serveur (utile pour le débogage).
		/// </summary>
		public string RawContent
		{
			get { return _rawContent; }
			set { _rawContent = value; }
		}
	}
}
