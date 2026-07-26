using System;

namespace RESTClientNet20.Library
{
	public enum CircuitBreakerState
	{
		Closed,
		Open,
		HalfOpen
	}

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

		public void RecordSuccess()
		{
			lock (_lock)
			{
				_failureCount = 0;
				_state = CircuitBreakerState.Closed;
			}
		}

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

		public void Reset()
		{
			lock (_lock)
			{
				_state = CircuitBreakerState.Closed;
				_failureCount = 0;
			}
		}
	}

	/// <summary>
	/// Politique de retry avec délai fixe ou backoff exponentiel.
	/// </summary>
	public class RetryPolicy
	{
		private readonly int _maxAttempts;
		private readonly int _baseDelayMilliseconds;
		private readonly bool _useExponentialBackoff;
		private Predicate<int> _shouldRetryStatusCode;

		public RetryPolicy(int maxAttempts, int baseDelayMilliseconds, bool useExponentialBackoff)
		{
			if (maxAttempts < 1)
			{
				throw new ArgumentOutOfRangeException("maxAttempts");
			}

			_maxAttempts = maxAttempts;
			_baseDelayMilliseconds = baseDelayMilliseconds;
			_useExponentialBackoff = useExponentialBackoff;
			_shouldRetryStatusCode = new Predicate<int>(DefaultShouldRetry);
		}

		public int MaxAttempts
		{
			get { return _maxAttempts; }
		}

		/// <summary>
		/// Permet de personnaliser quels codes HTTP déclenchent un retry.
		/// Par défaut : 408, 429 et tout code 5xx.
		/// </summary>
		public Predicate<int> ShouldRetryStatusCode
		{
			get { return _shouldRetryStatusCode; }
			set { _shouldRetryStatusCode = value; }
		}

		public bool ShouldRetry(int statusCode)
		{
			if (_shouldRetryStatusCode == null)
			{
				return DefaultShouldRetry(statusCode);
			}

			return _shouldRetryStatusCode(statusCode);
		}

		private static bool DefaultShouldRetry(int statusCode)
		{
			if (statusCode == 408 || statusCode == 429)
			{
				return true;
			}

			return statusCode >= 500 && statusCode < 600;
		}

		/// <summary>
		/// Calcule le délai d'attente avant la tentative numéro <paramref name="attemptNumber"/> (1-based).
		/// </summary>
		public int GetDelay(int attemptNumber)
		{
			if (_useExponentialBackoff)
			{
				double factor = Math.Pow(2, attemptNumber - 1);
				return (int)(_baseDelayMilliseconds * factor);
			}

			return _baseDelayMilliseconds;
		}

		/// <summary>
		/// Politique par défaut : 3 tentatives, 500ms de base, backoff exponentiel.
		/// </summary>
		public static RetryPolicy Default
		{
			get { return new RetryPolicy(3, 500, true); }
		}
	}

	/// <summary>
	/// Levée lorsqu'un appel est bloqué parce que le circuit breaker est ouvert.
	/// </summary>
	public class CircuitBreakerOpenException : Exception
	{
		public CircuitBreakerOpenException(string message)
			: base(message)
		{
		}
	}
}
