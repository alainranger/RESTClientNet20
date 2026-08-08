namespace RESTClientNet20.Library.ApiClient
{
	/// <summary>
	/// Représente les types de méthodes HTTP supportées par l'API REST.
	/// </summary>
	public enum HttpMethodType
	{
		/// <summary>
		/// Méthode HTTP GET pour récupérer des ressources.
		/// </summary>
		Get,
		/// <summary>
		/// Méthode HTTP POST pour créer de nouvelles ressources ou soumettre des données.
		/// </summary>
		Post,
		/// <summary>
		/// Méthode HTTP PUT pour mettre à jour des ressources existantes.
		/// </summary>
		Put,
		/// <summary>
		/// Méthode HTTP DELETE pour supprimer des ressources.
		/// </summary>
		Delete
	}
}
