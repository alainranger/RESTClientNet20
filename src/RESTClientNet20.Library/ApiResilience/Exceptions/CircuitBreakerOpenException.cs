using System;

namespace RESTClientNet20.Library.ApiResilience.Exceptions
{

	/// <summary>
	/// Levée lorsqu'un appel est bloqué parce que le circuit breaker est ouvert.
	/// </summary>
	public class CircuitBreakerOpenException : Exception
	{
		/// <summary>
		/// Initialise une nouvelle instance de l'exception CircuitBreakerOpenException.
		/// </summary>
		public CircuitBreakerOpenException()
		{
		}

		/// <summary>
		/// Initialise une nouvelle instance de l'exception CircuitBreakerOpenException avec un message d'erreur spécifié.
		/// </summary>
		/// <param name="message"></param>
		public CircuitBreakerOpenException(string message)
			: base(message)
		{
		}

		/// <summary>
		/// Initialise une nouvelle instance de l'exception CircuitBreakerOpenException avec un message d'erreur spécifié et une exception interne.
		/// </summary>
		/// <param name="message">Le message d'erreur.</param>
		/// <param name="innerException">L'exception interne.</param>
		public CircuitBreakerOpenException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}
