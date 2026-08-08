using System;

namespace RESTClientNet20.Library.ApiResilience
{
	/// <summary>
	/// Circuit breaker simple : Closed -> Open (après N échecs) -> HalfOpen (après un délai) -> Closed ou Open.
	/// Thread-safe.
	/// </summary>
	public class CircuitBreaker
	{
		private readonly object _lock = new object();
		private CircuitBreakerState _state;
		private int _failureCount;
		private DateTime _lastFailureTime;
		private readonly int _failureThreshold;
		private readonly TimeSpan _openDuration;

		/// <summary>
		///	Initialise une nouvelle instance de CircuitBreaker avec un seuil d'échec et une durée d'ouverture spécifiés.
		/// </summary>
		/// <param name="failureThreshold">Le nombre d'échecs consécutifs nécessaires pour ouvrir le circuit.</param>
		/// <param name="openDuration">La durée pendant laquelle le circuit reste ouvert avant de passer à l'état HalfOpen.</param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public CircuitBreaker(int failureThreshold, TimeSpan openDuration)
		{
			if (failureThreshold < 1)
			{
				throw new ArgumentOutOfRangeException("failureThreshold");
			}

			_failureThreshold = failureThreshold;
			_openDuration = openDuration;
			_state = CircuitBreakerState.Closed;
			_failureCount = 0;
		}

		/// <summary>
		/// Obtient l'état actuel du circuit breaker.
		/// </summary>
		public CircuitBreakerState State
		{
			get
			{
				lock (_lock)
				{
					return _state;
				}
			}
		}

		/// <summary>
		/// Indique si un appel peut être tenté. Fait aussi transitionner Open -> HalfOpen si le délai est écoulé.
		/// </summary>
		public bool CanExecute()
		{
			lock (_lock)
			{
				if (_state == CircuitBreakerState.Open)
				{
					if (DateTime.Now - _lastFailureTime >= _openDuration)
					{
						_state = CircuitBreakerState.HalfOpen;
						return true;
					}

					return false;
				}

				return true;
			}
		}

		/// <summary>
		/// Enregistre un succès d'appel. Réinitialise le compteur d'échecs et ferme le circuit si nécessaire.
		/// </summary>
		public void RecordSuccess()
		{
			lock (_lock)
			{
				_failureCount = 0;
				_state = CircuitBreakerState.Closed;
			}
		}

		/// <summary>
		/// Enregistre un échec d'appel. Incrémente le compteur d'échecs et ouvre le circuit si le seuil est atteint.
		/// </summary>
		public void RecordFailure()
		{
			lock (_lock)
			{
				_failureCount++;
				_lastFailureTime = DateTime.Now;

				if (_state == CircuitBreakerState.HalfOpen)
				{
					// L'appel de test en HalfOpen a échoué : on rouvre immédiatement.
					_state = CircuitBreakerState.Open;
				}
				else if (_failureCount >= _failureThreshold)
				{
					_state = CircuitBreakerState.Open;
				}
			}
		}

		/// <summary>
		/// Réinitialise le circuit breaker à l'état fermé et remet le compteur d'échecs à zéro.
		/// </summary>
		public void Reset()
		{
			lock (_lock)
			{
				_state = CircuitBreakerState.Closed;
				_failureCount = 0;
			}
		}
	}
}
