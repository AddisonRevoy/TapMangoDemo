using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TapMango_Demo.Controllers;

#pragma warning disable NUnit2005
namespace TapMango_Test
{
    [TestFixture]
    public class PhoneNumberControllerTests
    {
        private PhoneNumberController _controller;
        private Dictionary<string, string> _configData;

        private const int MaxGlobalRequests = 5;

        [SetUp]
        public void Setup()
        {
            // Mock configuration settings
            _configData = new Dictionary<string, string>
            {
                { "SmsSettings:RequestsPerSecond", MaxGlobalRequests.ToString() },
                { "SmsSettings:DefaultSenderLimit", "2" },
                { "SmsSettings:SenderInactivitySeconds", "5" }, // Short for testing
                { "SmsSettings:SenderLimits:+1111111111", "3" } // Custom limit for testing
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(_configData)
                .Build();

            _controller = new PhoneNumberController(configuration);
        }

        [Test]
        public void SendSms_ValidRequest_ShouldReturnOk()
        {
            var request = new SmsRequest { PhoneNumber = "+1234567890", Message = "Hello" };

            var result = _controller.SendSms(request) as ObjectResult;

            Assert.NotNull(result);
            //Assert.AreEqual(200, result.StatusCode);
            Assert.AreEqual("SMS sent successfully.", result.Value);
        }

        [Test]
        public void SendSms_ExceedGlobalLimit_ShouldReturnTooManyRequests()
        {
            var request = new SmsRequest { PhoneNumber = "+1234567890", Message = "Test" };

            // Simulate too many requests
            for (int i = 0; i < 5; i++)
            {
                _controller.SendSms(request);
            }

            var result = _controller.SendSms(request) as ObjectResult;

            Assert.NotNull(result);
            Assert.AreEqual(429, result.StatusCode);
            StringAssert.Contains("Too many global requests", result.Value.ToString());
        }

        [Test]
        public void SendSms_ExceedSenderLimit_ShouldReturnTooManyRequests()
        {
            var request = new SmsRequest { PhoneNumber = "+1111111111", Message = "Test" };

            // Simulate sender-specific limit being exceeded
            for (int i = 0; i < 3; i++)
            {
                _controller.SendSms(request);
            }

            var result = _controller.SendSms(request) as ObjectResult;

            Assert.NotNull(result);
            Assert.AreEqual(429, result.StatusCode);
            StringAssert.Contains("exceeded limit", result.Value.ToString());
        }

        [Test]
        public void SendSms_InvalidRequest_ShouldReturnBadRequest()
        {
            var request = new SmsRequest { PhoneNumber = "", Message = "Hello" };

            var result = _controller.SendSms(request) as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.AreEqual(400, result.StatusCode);
            StringAssert.Contains("Phone number and message are required", result.Value.ToString());
        }

        [Test]
        public void CleanupInactiveSenders_ShouldRemoveInactive()
        {
            var request = new SmsRequest { PhoneNumber = "+1234567890", Message = "Test" };
            _controller.SendSms(request);

            // Simulate sender being inactive for longer than the timeout
            Thread.Sleep(6000);

            // Trigger cleanup by sending a request to a different number
            var request2 = new SmsRequest { PhoneNumber = "+2222222222", Message = "Another test" };
            var result = _controller.SendSms(request2) as ObjectResult;

            Assert.NotNull(result);
            Assert.AreEqual(200, result.StatusCode);

            // Ensure old sender was removed
            Assert.False(_controller.SenderExists("+1234567890"));
        }

        [Test]
        public void SendSms_NewSender_ShouldBeRateLimitedByDefault()
        {
            var request = new SmsRequest { PhoneNumber = "+9999999999", Message = "Default Limit Test" };

            for (int i = 0; i < 2; i++)
            {
                _controller.SendSms(request);
            }

            var result = _controller.SendSms(request) as ObjectResult;

            Assert.NotNull(result);
            Assert.AreEqual(429, result.StatusCode);
            StringAssert.Contains("exceeded limit", result.Value.ToString());
        }
    }
}
#pragma warning restore NUnit2005