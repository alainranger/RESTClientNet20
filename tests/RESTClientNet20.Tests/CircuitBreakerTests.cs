using NUnit.Framework;

using RESTClientNet20.Library;

using System;
using System.Threading;

namespace RESTClientNet20.Tests
{
	[TestFixture]
	public class CircuitBreakerTests
	{
		[Test]
		public void CircuitBreaker_TransitionsFromOpenToHalfOpenToClosedAfterSuccess()
		{
			CircuitBreaker breaker = new CircuitBreaker(2, TimeSpan.FromMilliseconds(75));

			breaker.RecordFailure();
			breaker.RecordFailure();

			Assert.AreEqual(CircuitBreakerState.Open, breaker.State);
			Assert.IsFalse(breaker.CanExecute());

			Thread.Sleep(100);

			Assert.IsTrue(breaker.CanExecute());
			Assert.AreEqual(CircuitBreakerState.HalfOpen, breaker.State);

			breaker.RecordSuccess();

			Assert.AreEqual(CircuitBreakerState.Closed, breaker.State);
			Assert.IsTrue(breaker.CanExecute());
		}

		[Test]
		public void CircuitBreaker_FailureInHalfOpen_ReopensCircuit_AndResetClosesIt()
		{
			CircuitBreaker breaker = new CircuitBreaker(1, TimeSpan.FromMilliseconds(50));

			breaker.RecordFailure();
			Assert.AreEqual(CircuitBreakerState.Open, breaker.State);

			Thread.Sleep(75);
			Assert.IsTrue(breaker.CanExecute());
			Assert.AreEqual(CircuitBreakerState.HalfOpen, breaker.State);

			breaker.RecordFailure();
			Assert.AreEqual(CircuitBreakerState.Open, breaker.State);

			breaker.Reset();
			Assert.AreEqual(CircuitBreakerState.Closed, breaker.State);
		}

		[Test]
		public void CircuitBreakerOpenException_PreservesMessage()
		{
			CircuitBreakerOpenException exception = new CircuitBreakerOpenException("blocked");

			Assert.AreEqual("blocked", exception.Message);
		}
	}
}
