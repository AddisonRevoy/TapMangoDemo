using TapMango_Demo.Controllers;

namespace TapMango_Test
{
    [TestFixture]
    public class RateLimiterTests
    {
        private RateLimiter _rateLimiter;

        [SetUp]
        public void Setup()
        {
            _rateLimiter = new RateLimiter(5); // Allow 5 requests per second
        }

        [Test]
        public void AllowRequest_WhenUnderLimit_ShouldReturnTrue()
        {
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(_rateLimiter.TriggerRequestCheck(), $"Request {i + 1} should be allowed.");
            }
        }

        [Test]
        public void AllowRequest_WhenExceedingLimit_ShouldReturnFalse()
        {
            for (int i = 0; i < 5; i++)
            {
                _rateLimiter.TriggerRequestCheck();
            }

            Assert.IsFalse(_rateLimiter.TriggerRequestCheck(), "6th request should be rejected.");
        }

        [Test]
        public void AllowRequest_AfterTimeWindow_ShouldResetAndAllow()
        {
            for (int i = 0; i < 5; i++)
            {
                _rateLimiter.TriggerRequestCheck();
            }

            Assert.IsFalse(_rateLimiter.TriggerRequestCheck(), "6th request should be rejected.");

            // Wait for rate limit to reset
            Thread.Sleep(1000);

            Assert.IsTrue(_rateLimiter.TriggerRequestCheck(), "Request after 1 second should be allowed.");
        }

        [Test]
        public void AllowRequest_WhenRequestsAreSpacedOut_ShouldNotThrottle()
        {
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(_rateLimiter.TriggerRequestCheck(), $"Request {i + 1} should be allowed.");
                Thread.Sleep(200); // Space out requests (5 requests in 1 sec)
            }

            Assert.IsTrue(_rateLimiter.TriggerRequestCheck(), "6th request should be allowed after spacing.");
        }

        [Test]
        public void AllowRequest_WhenRequestsExpire_ShouldAllowNewRequests()
        {
            for (int i = 0; i < 5; i++)
            {
                _rateLimiter.TriggerRequestCheck();
            }

            // Add 6th request after a delay
            Thread.Sleep(500);

            Assert.IsFalse(_rateLimiter.TriggerRequestCheck(), "6th request should be rejected.");

            // Try again after 1 second had passed since the first batch of requests
            Thread.Sleep(500);

            Assert.IsTrue(_rateLimiter.TriggerRequestCheck(), "A new request should be allowed after an old one expires.");
        }

        [Test]
        public void LastRequestDateTime_ShouldUpdateCorrectly()
        {
            DateTime before = DateTime.UtcNow;

            _rateLimiter.TriggerRequestCheck();
            DateTime lastRequest = _rateLimiter.LastRequestDateTime;

            Assert.GreaterOrEqual(lastRequest, before, "LastRequestDateTime should be updated to a recent timestamp.");
        }

        [Test]
        public void AllowRequest_WhenQueueIsEmpty_ShouldAllow()
        {
            // No requests sent yet
            Assert.IsTrue(_rateLimiter.TriggerRequestCheck(), "First request should always be allowed.");
        }
    }
}
