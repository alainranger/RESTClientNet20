namespace RESTClientNet20.Library.ApiResilience
{
	/// <summary>
	/// Représente l'état du circuit breaker.
	/// </summary>
	public enum CircuitBreakerState
	{
		/// <summary>
		/// Le circuit breaker est fermé, les appels sont autorisés.
		/// </summary>
		Closed,
		/// <summary>
		/// Le circuit breaker est ouvert, les appels sont bloqués.
		/// </summary>
		Open,
		/// <summary>
		/// Le circuit breaker est à moitié ouvert, certains appels sont autorisés.
		/// </summary>
		HalfOpen
	}
}
