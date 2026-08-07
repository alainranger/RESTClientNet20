using NUnit.Framework;

using RESTClientNet20.Library;

namespace RESTClientNet20.Tests
{
	[TestFixture]
	public class RetryPolicyTests
	{
		[Test]
		public void RetryPolicy_DefaultRulesAndDelayCalculation_WorkAsExpected()
		{
			RetryPolicy policy = new RetryPolicy(4, 25, true);

			Assert.AreEqual(4, policy.MaxAttempts);
			Assert.IsTrue(policy.ShouldRetry(408));
			Assert.IsTrue(policy.ShouldRetry(429));
			Assert.IsTrue(policy.ShouldRetry(503));
			Assert.IsFalse(policy.ShouldRetry(404));
			Assert.AreEqual(25, policy.GetDelay(1));
			Assert.AreEqual(50, policy.GetDelay(2));
			Assert.AreEqual(100, policy.GetDelay(3));

			policy.ShouldRetryStatusCode = delegate(int statusCode)
			{
				return statusCode == 409;
			};

			Assert.IsTrue(policy.ShouldRetry(409));
			Assert.IsFalse(policy.ShouldRetry(503));
			Assert.AreEqual(3, RetryPolicy.Default.MaxAttempts);
		}
	}
}
