using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace TapMango_Demo.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class PhoneNumberController : ControllerBase
    {
        private static ConcurrentDictionary<string, RateLimiter> _senderLimiters;
        private static RateLimiter _globalRateLimiter;
        private static readonly object _lock = new();

        private readonly int _globalRequestsPerSecond;
        private readonly int _defaultSenderLimit;
        private readonly int _senderInactivitySeconds;
        private readonly Dictionary<string, int> _senderLimits;

        public PhoneNumberController(IConfiguration configuration)
        {
            _globalRequestsPerSecond = configuration.GetValue<int>("SmsSettings:RequestsPerSecond");
            _defaultSenderLimit = configuration.GetValue<int>("SmsSettings:DefaultSenderLimit", 2);
            _senderInactivitySeconds = configuration.GetValue<int>("SmsSettings:SenderInactivitySeconds", 60);
            _senderLimits = configuration.GetSection("SmsSettings:SenderLimits").Get<Dictionary<string, int>>() ?? [];

            _globalRateLimiter = new RateLimiter(_globalRequestsPerSecond);
            _senderLimiters = new();
        }

        [HttpPost("send")]
        public IActionResult SendSms([FromBody] SmsRequest request)
        {
            if (string.IsNullOrEmpty(request.PhoneNumber) || string.IsNullOrEmpty(request.Message))
            {
                return BadRequest("Phone number and message are required.");
            }

            lock (_lock)
            {
                // Check global rate limit
                if (!_globalRateLimiter.TriggerRequestCheck())
                {
                    return StatusCode(429, "Too many global requests.");
                }

                // Get or create sender-specific rate limiter
                if (!_senderLimiters.ContainsKey(request.PhoneNumber))
                {
                    int senderLimit = _senderLimits.ContainsKey(request.PhoneNumber) ? _senderLimits[request.PhoneNumber] : _defaultSenderLimit;
                    _senderLimiters[request.PhoneNumber] = new RateLimiter(senderLimit);
                }

                RateLimiter senderLimiter = _senderLimiters[request.PhoneNumber];

                if (!senderLimiter.TriggerRequestCheck())
                {
                    return StatusCode(429, $"Sender {request.PhoneNumber} exceeded limit ({senderLimiter.RequestsPerSecond} requests per second).");
                }
            }

            // Simulate SMS sending
            bool success = SendSmsToNumber(request.PhoneNumber, request.Message);

            if (!success)
            {
                return StatusCode(500, "Failed to send SMS.");
            }

            // Perform cleanup after processing the request
            CleanupInactiveSenders();

            return Ok("SMS sent successfully.");
        }

        private bool SendSmsToNumber(string phoneNumber, string message)
        {
            Console.WriteLine($"Sending SMS to {phoneNumber}: {message}");
            return true;
        }

        private void CleanupInactiveSenders()
        {
            DateTime now = DateTime.UtcNow;
            var inactiveSenders = _senderLimiters
                .Where(s => (now - s.Value.LastRequestDateTime).TotalSeconds > _senderInactivitySeconds)
                .Select(s => s.Key)
                .ToList();

            foreach (string sender in inactiveSenders)
            {
                _senderLimiters.TryRemove(sender, out _);
            }
        }

        internal bool SenderExists(string phoneNumber)
        {
            return _senderLimiters.ContainsKey(phoneNumber);
        }
    }

    /// <summary>
    /// Performs continuous rate limiting to a specified number of requests per second, inside a sliding window.
    /// </summary>
    public class RateLimiter
    {
        private readonly int _requestsPerSecond;
        private readonly Queue<DateTime> _requestTimestamps;
        private readonly object _lock = new();

        public int RequestsPerSecond => _requestsPerSecond;
        public DateTime LastRequestDateTime => _requestTimestamps.Count > 0 ? _requestTimestamps.Peek() : DateTime.MinValue;

        public RateLimiter(int requestsPerSecond)
        {
            _requestsPerSecond = requestsPerSecond;
            _requestTimestamps = new Queue<DateTime>();
        }

        /// <summary>
        /// Checks if a request will exceed the rate limit and stores the request if allowed.
        /// </summary>
        /// <returns>Returns true if the rate limit had not been exceeded, false if the limit has been.</returns>
        public bool TriggerRequestCheck()
        {
            lock (_lock)
            {
                DateTime now = DateTime.UtcNow;

                // Remove old requests outside the time window
                while (_requestTimestamps.Count > 0 && now - _requestTimestamps.Peek() > TimeSpan.FromSeconds(1))
                {
                    _requestTimestamps.Dequeue();
                }

                if (_requestTimestamps.Count < _requestsPerSecond)
                {
                    _requestTimestamps.Enqueue(now);
                    return true;
                }

                return false;
            }
        }
    }

    public class SmsRequest
    {
        public string PhoneNumber { get; set; }
        public string Message { get; set; }
    }
}
