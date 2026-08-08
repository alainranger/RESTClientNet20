using System;

namespace RESTClientNet20.Library.ApiResilience
{
	/// <summary>
	/// Politique de retry avec délai fixe ou backoff exponentiel.
	/// </summary>
	public class RetryPolicy
	{
		private readonly int _baseDelayMilliseconds;
		private readonly bool _useExponentialBackoff;

		/// <summary>
		/// Initialise une nouvelle instance de RetryPolicy avec le nombre maximum de tentatives, le délai de base et l'indication d'utilisation du backoff exponentiel.
		/// </summary>
		/// <param name="maxAttempts">Le nombre maximum de tentatives (y compris la première).</param>
		/// <param name="baseDelayMilliseconds">Le délai de base en millisecondes.</param>
		/// <param name="useExponentialBackoff">Indique si un backoff exponentiel doit être utilisé.</param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public RetryPolicy(int maxAttempts, int baseDelayMilliseconds, bool useExponentialBackoff)
		{
			if (maxAttempts < 1)
			{
				throw new ArgumentOutOfRangeException("maxAttempts");
			}

			MaxAttempts = maxAttempts;
			_baseDelayMilliseconds = baseDelayMilliseconds;
			_useExponentialBackoff = useExponentialBackoff;
			ShouldRetryStatusCode = new Predicate<int>(DefaultShouldRetry);
		}

		/// <summary>
		/// Nombre maximum de tentatives (y compris la première).
		/// </summary>
		public int MaxAttempts { get; }

		/// <summary>
		/// Permet de personnaliser quels codes HTTP déclenchent un retry.
		/// Par défaut : 408, 429 et tout code 5xx.
		/// </summary>
		public Predicate<int> ShouldRetryStatusCode { get; set; }

		/// <summary>
		/// Indique si un code HTTP donné doit déclencher un retry selon la politique actuelle.
		/// </summary>
		/// <param name="statusCode">Le code HTTP à évaluer.</param>
		/// <returns>True si un retry doit être tenté, false sinon.</returns>
		public bool ShouldRetry(int statusCode)
		{
			if (ShouldRetryStatusCode == null)
			{
				return DefaultShouldRetry(statusCode);
			}

			return ShouldRetryStatusCode(statusCode);
		}

		/// <summary>
		/// Politique par défaut : retry sur 408, 429 et tout code 5xx.
		/// </summary>
		/// <param name="statusCode"></param>
		/// <returns></returns>
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
				var factor = Math.Pow(2, attemptNumber - 1);
				return (int)(_baseDelayMilliseconds * factor);
			}

			return _baseDelayMilliseconds;
		}

		/// <summary>
		/// Politique par défaut : 3 tentatives, 500ms de base, backoff exponentiel.
		/// </summary>
		public static RetryPolicy Default => new RetryPolicy(3, 500, true);
	}
}
