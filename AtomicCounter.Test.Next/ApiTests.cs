using System;
using System.Linq;
using System.Threading.Tasks;
using AtomicCounter.Api;
using AtomicCounter.EventHandlers;
using AtomicCounter.Models;
using AtomicCounter.Models.Events;
using AtomicCounter.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using AppAuthDelegate = System.Func<System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.IActionResult>>;
using UserAuthDelegate = System.Func<AtomicCounter.Models.UserProfile, System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.IActionResult>>;

namespace AtomicCounter.Test
{
    [TestClass]
    public class ApiTests
    {
        [TestMethod]
        public async Task HappyPathTests()
        {
            var profile = new UserProfile()
            {
                Email = "billg@microsoft.com",
                Id = Guid.Parse("4E4393B1-E825-493D-A03B-DC53F58BDD92")
            };

            var mockAuth = GetMockAuthProvider(profile);

            var req = new DefaultHttpRequest(new DefaultHttpContext())
            {
                Method = "POST",
                Path = new PathString()
            };

            var logger = new TestLogger();

            // Add a counter
            var counterViewModel = await AddCounter(mockAuth, req, logger);

            // Get existing counter
            var getCounterViewModel = await GetExistingCounter(mockAuth, req, logger, counterViewModel);

            // Add counter to counter
            CreateCounter.AuthProvider = mockAuth.Object;
            req.Method = "POST";
            var res = await CreateCounter.Run(req, Initialize.Counter, logger);
            Assert.IsNotNull(res as CreatedAtRouteResult);

            // Increment counter
            await Increment(mockAuth, req, logger, getCounterViewModel);

            // Handle count event
            await HandleCountEvent(logger);

            // Get count (all but one key increments by 2, so result is (writeKeys * 2) - 1
            await GetCount(mockAuth, req, logger, getCounterViewModel, 3);

            // Rotate read keys
            await RotateReadKeys(mockAuth, req, logger, getCounterViewModel, 1);

            // Rotate write keys
            await RotateWriteKeys(mockAuth, req, logger, getCounterViewModel, 1);

            // Rotate read keys again
            await RotateReadKeys(mockAuth, req, logger, getCounterViewModel, 0);

            // Rotate write keys again
            await RotateWriteKeys(mockAuth, req, logger, getCounterViewModel, 0);

            // Increment counter
            await Increment(mockAuth, req, logger, getCounterViewModel);

            // Handle count event
            await HandleCountEvent(logger);

            // Get count (all but one key increments by 2, so result is (writeKeys * 2) - 1)
            await GetCount(mockAuth, req, logger, getCounterViewModel, 5);

            // Should reset count to zero
            await RunResetCounter(mockAuth, req, logger);

            // Makes sure counter was reset
            await GetCount(mockAuth, req, logger, getCounterViewModel, 0);
        }

        private static async Task<CounterViewModel> AddCounter(Mock<IAuthorizationProvider> mockAuth, DefaultHttpRequest req, TestLogger logger)
        {
            CreateCounter.AuthProvider = mockAuth.Object;
            var result = await CreateCounter.Run(req, Initialize.Counter, logger);
            var content = (OkObjectResult)result;
            var counterViewModel = (CounterViewModel)content.Value;

            Assert.IsNotNull(counterViewModel);
            Assert.IsNotNull(counterViewModel.ReadKeys);
            Assert.IsNotNull(counterViewModel.WriteKeys);
            Assert.AreEqual(2, counterViewModel.ReadKeys.Count());
            Assert.AreEqual(2, counterViewModel.WriteKeys.Count());
            Assert.AreEqual(Initialize.Counter, counterViewModel.CounterName);
            return counterViewModel;
        }

        private static async Task<CounterViewModel> GetExistingCounter(Mock<IAuthorizationProvider> mockAuth, DefaultHttpRequest req, TestLogger logger, CounterViewModel counterViewModel)
        {
            GetCounter.AuthProvider = mockAuth.Object;
            req.Method = "GET";
            var getCounterResult = (OkObjectResult)await GetCounter.Run(req, Initialize.Counter, logger);
            var getCounterViewModel = (CounterViewModel)getCounterResult.Value;

            Assert.IsNotNull(getCounterViewModel);
            Assert.IsNotNull(getCounterViewModel.ReadKeys);
            Assert.IsNotNull(getCounterViewModel.WriteKeys);
            Assert.AreEqual(counterViewModel.ReadKeys.Count(), getCounterViewModel.ReadKeys.Count());
            Assert.AreEqual(counterViewModel.WriteKeys.Count(), getCounterViewModel.WriteKeys.Count());
            Assert.AreEqual(Initialize.Counter, getCounterViewModel.CounterName);
            return getCounterViewModel;
        }

        private async Task RunResetCounter(Mock<IAuthorizationProvider> mockAuth, DefaultHttpRequest req, TestLogger logger)
        {
            ResetCounter.AuthProvider = mockAuth.Object;
            req.Method = "DELETE";
            var resetResult = (AcceptedResult)await ResetCounter.Run(req, Initialize.Counter, logger);
            Assert.IsNotNull(resetResult);
        }

        private static async Task GetCount(Mock<IAuthorizationProvider> mockAuth, HttpRequest req, TestLogger logger, CounterViewModel getCounterViewModel, long expected)
        {
            Count.AuthProvider = mockAuth.Object;
            req.Method = "GET";
            var iteration = 1;
            foreach (var key in getCounterViewModel.ReadKeys)
            {
                req.QueryString = new QueryString("?key=" + key);
                var countResult = (OkObjectResult)await Count.Run(req, Initialize.Counter, logger);
                var finalCount = (long)countResult.Value;
                Assert.AreEqual(expected, finalCount, "Mismatch on iteration {0} with key {1}.", iteration, key);
                iteration++;
            }
        }

        private static async Task HandleCountEvent(TestLogger logger)
        {
            var queueClient = Initialize.Storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference("increment-items");
            var countEvents = await queue.GetMessagesAsync(2);

            foreach (var evt in countEvents)
            {
                await IncrementEventHandler.Run(JsonConvert.DeserializeObject<IncrementEvent>(evt.AsString), logger);
                await queue.DeleteMessageAsync(evt);
            }
        }

        private static async Task Increment(Mock<IAuthorizationProvider> mockAuth, HttpRequest req, TestLogger logger, CounterViewModel getCounterViewModel)
        {
            Api.Increment.AuthProvider = mockAuth.Object;
            req.Method = "POST";
            var defaultHasRun = false;
            foreach (var key in getCounterViewModel.WriteKeys)
            {
                var modifier = defaultHasRun ? "" : "&count=2";
                req.QueryString = new QueryString($"?key={key}{modifier}");
                var incrementResult = (AcceptedResult)await Api.Increment.Run(req, Initialize.Counter, logger);
                Assert.IsNotNull(incrementResult);
                defaultHasRun = true;
            }
        }

        private static async Task RotateReadKeys(Mock<IAuthorizationProvider> mockAuth, HttpRequest req, TestLogger logger, CounterViewModel getCounterViewModel, int expected)
        {
            RotateKeys.Authorization = mockAuth.Object;
            req.Method = "POST";
            var readRotateResult = (OkObjectResult)await RotateKeys.Run(req, Initialize.Counter, "read", logger);
            var readKeys = (string[])readRotateResult.Value;
            Assert.AreEqual(2, readKeys.Length);
            var readDupeCount = readKeys.Count(k => getCounterViewModel.ReadKeys.Contains(k));
            Assert.AreEqual(expected, readDupeCount);
        }

        private static async Task RotateWriteKeys(Mock<IAuthorizationProvider> mockAuth, HttpRequest req, TestLogger logger, CounterViewModel getCounterViewModel, int expected)
        {
            RotateKeys.Authorization = mockAuth.Object;
            req.Method = "POST";
            var writeRotateResult = (OkObjectResult)await RotateKeys.Run(req, Initialize.Counter, "write", logger);
            var writeKeys = (string[])writeRotateResult.Value;
            Assert.AreEqual(2, writeKeys.Length);
            var writeDupeCount = writeKeys.Count(k => getCounterViewModel.WriteKeys.Contains(k));
            Assert.AreEqual(expected, writeDupeCount);
        }

        private static Mock<IAuthorizationProvider> GetMockAuthProvider(UserProfile profile)
        {
            var mockAuth = new Mock<IAuthorizationProvider>();

            // User auth always passes
            mockAuth
                .Setup(m =>
                    m.AuthorizeUserAndExecute(
                        It.IsAny<HttpRequest>(),
                        It.IsAny<UserAuthDelegate>()))
                .Returns<HttpRequest, UserAuthDelegate>((a, b) => b(profile));

            // App auth always passes
            mockAuth
                .Setup(m =>
                    m.AuthorizeAppAndExecute(
                        It.IsAny<HttpRequest>(),
                        It.IsAny<KeyMode>(),
                        Initialize.Counter,
                        It.IsAny<AppAuthDelegate>()))
                .Returns<HttpRequest, KeyMode, string, AppAuthDelegate>((a, b, c, d) => d());

            return mockAuth;
        }
    }
}
