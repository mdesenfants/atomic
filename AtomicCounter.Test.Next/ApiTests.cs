using AtomicCounter.Api;
using AtomicCounter.EventHandlers;
using AtomicCounter.Models;
using AtomicCounter.Models.Events;
using AtomicCounter.Models.ViewModels;
using AtomicCounter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AppAuthDelegate = System.Func<System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.IActionResult>>;
using UserAuthDelegate = System.Func<AtomicCounter.Models.UserProfile, System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.IActionResult>>;
using UserCounterDelegate = System.Func<AtomicCounter.Models.UserProfile, AtomicCounter.Models.Counter, System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.IActionResult>>;

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

            AppStorage.CreateAppStorage();

            // Add a counter
            var counterViewModel = await AddCounter(mockAuth, req, logger).ConfigureAwait(false);

            // Get existing counter
            var getCounterViewModel = await GetExistingCounter(mockAuth, req, logger, counterViewModel).ConfigureAwait(false);

            // Create a counter client
            var increment = new IncrementMetadata()
            {
                MockAuth = mockAuth,
                Req = req,
                Logger = logger,
                GetCounterViewModel = getCounterViewModel
            };

            decimal totalValue = 0;
            long totalIncrements = 0;

            // Increment counter
            var transaction = await increment.Increment().ConfigureAwait(false);
            totalIncrements += transaction.inc;
            totalValue += transaction.val;

            // Handle count event
            await HandleCountEvents(logger).ConfigureAwait(false);

            // Get count (all but one key increments by 2, so result is (writeKeys * 2) - 1
            await GetCount(mockAuth, req, logger, getCounterViewModel, totalIncrements, "First count.").ConfigureAwait(false);

            // Increment counter for client and time rnage
            var min = new DateTime(DateTime.UtcNow.Ticks, DateTimeKind.Utc);

            transaction = await increment.Increment().ConfigureAwait(false);
            totalIncrements += transaction.inc;
            totalValue += transaction.val;

            await HandleCountEvents(logger).ConfigureAwait(false);

            var max = DateTimeOffset.UtcNow;

            // Check counts for different slices
            await GetCount(mockAuth, req, logger, getCounterViewModel, transaction.inc, "Date count.", min: min, max: max).ConfigureAwait(false);

            // Get the invoice for the last time slice
            var counterClient = new CountStorage(getCounterViewModel.CounterName, logger);
            var invoice = await counterClient.GetInvoiceDataAsync(min, max).ConfigureAwait(false);
            Assert.AreEqual(1, invoice.Count());
            var lines = invoice.First();
            Assert.AreEqual(2, lines.Quantity);
            Assert.AreEqual(0, lines.Price);

            // Get count (all but one key increments by 2, so result is (writeKeys * 2) - 1
            await GetCount(mockAuth, req, logger, getCounterViewModel, totalIncrements, "First count.").ConfigureAwait(false);

            // Rotate read keys
            await RotateReadKeys(mockAuth, req, logger, getCounterViewModel, 1).ConfigureAwait(false);

            // Rotate write keys
            await RotateWriteKeys(mockAuth, req, logger, getCounterViewModel, 1).ConfigureAwait(false);

            // Rotate read keys again
            await RotateReadKeys(mockAuth, req, logger, getCounterViewModel, 0).ConfigureAwait(false);

            // Rotate write keys again
            await RotateWriteKeys(mockAuth, req, logger, getCounterViewModel, 0).ConfigureAwait(false);

            // Increment counter
            transaction = await increment.Increment().ConfigureAwait(false);
            totalIncrements += transaction.inc;
            totalValue += transaction.val;

            // Handle count event
            await HandleCountEvents(logger).ConfigureAwait(false);

            // Get count (all but one key increments by 2, so result is (writeKeys * 2) - 1)
            await GetCount(mockAuth, req, logger, getCounterViewModel, totalIncrements, "Count after rotation.").ConfigureAwait(false);

            // Should reset count to zero
            await RunResetCounter(mockAuth, req, logger).ConfigureAwait(false);

            // Makes sure counter was reset

            await GetCount(mockAuth, req, logger, getCounterViewModel, 0, "Count after reset.").ConfigureAwait(false);

        }

        private static async Task<CounterViewModel> AddCounter(Mock<IAuthorizationProvider> mockAuth, DefaultHttpRequest req, TestLogger logger)
        {
            CreateCounter.AuthProvider = mockAuth.Object;
            var result = await CreateCounter.Run(req, Initialize.Counter, logger).ConfigureAwait(false);
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
            var getCounterResult = (OkObjectResult)await GetCounter.Run(req, Initialize.Counter, logger).ConfigureAwait(false);
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
            var resetResult = (AcceptedResult)await ResetCounter.Run(req, Initialize.Counter, logger).ConfigureAwait(false);
            Assert.IsNotNull(resetResult);

            await ResetEventHandler.Run(Initialize.Counter, logger).ConfigureAwait(false);

            await RecreateEventHandler.Run(Initialize.Counter, logger).ConfigureAwait(false);
        }

        private static async Task GetCount(Mock<IAuthorizationProvider> mockAuth, HttpRequest req, TestLogger logger, CounterViewModel getCounterViewModel, long expected, string message, DateTimeOffset? min = null, DateTimeOffset? max = null)
        {
            Count.AuthProvider = mockAuth.Object;
            req.Method = "GET";
            var iteration = 1;
            foreach (var key in getCounterViewModel.ReadKeys)
            {
                var query = "?key=" + key;
                query += min == null ? string.Empty : $"&min={Uri.EscapeDataString(min?.ToString("o", CultureInfo.InvariantCulture))}";
                query += max == null ? string.Empty : $"&max={Uri.EscapeDataString(max?.ToString("o", CultureInfo.InvariantCulture))}";

                req.QueryString = new QueryString(query);
                var countResult = (OkObjectResult)await Count.Run(req, Initialize.Counter, logger).ConfigureAwait(false);
                var finalCount = (long)countResult.Value;
                Assert.AreEqual(expected, finalCount, "Mismatch on iteration {0} with key {1}. {2}", iteration, key, message);
                iteration++;
            }
        }

        private static async Task HandleCountEvents(TestLogger logger)
        {
            var queueClient = Initialize.Storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(AppStorage.CountQueueName);

            do
            {
                var countEvents = await queue.GetMessagesAsync(30).ConfigureAwait(false);
                if (countEvents == null || countEvents.Count() == 0) return;

                foreach (var evt in countEvents)
                {
                    await IncrementEventHandler.Run(evt.AsString.FromJson<IncrementEvent>(), logger).ConfigureAwait(false);
                    await queue.DeleteMessageAsync(evt).ConfigureAwait(false);
                }
            } while (true);
        }

        private static async Task RotateReadKeys(Mock<IAuthorizationProvider> mockAuth, HttpRequest req, TestLogger logger, CounterViewModel getCounterViewModel, int expected)
        {
            RotateKeys.Authorization = mockAuth.Object;
            req.Method = "POST";
            var readRotateResult = (OkObjectResult)await RotateKeys.Run(req, Initialize.Counter, "read", logger).ConfigureAwait(false);
            var readKeys = (string[])readRotateResult.Value;
            Assert.AreEqual(2, readKeys.Length);
            var readDupeCount = readKeys.Count(k => getCounterViewModel.ReadKeys.Contains(k));
            Assert.AreEqual(expected, readDupeCount);
        }

        private static async Task RotateWriteKeys(Mock<IAuthorizationProvider> mockAuth, HttpRequest req, TestLogger logger, CounterViewModel getCounterViewModel, int expected)
        {
            RotateKeys.Authorization = mockAuth.Object;
            req.Method = "POST";
            var writeRotateResult = (OkObjectResult)await RotateKeys.Run(req, Initialize.Counter, "write", logger).ConfigureAwait(false);
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

            mockAuth
                .Setup(m =>
                    m.AuthorizeUserAndExecute(
                        It.IsAny<HttpRequest>(),
                        It.IsAny<string>(),
                        It.IsAny<UserCounterDelegate>()))
                .Returns<HttpRequest, string, UserCounterDelegate>((a, b, c) =>
                {
                    var meta = AppStorage.GetCounterMetadataAsync(b).Result;
                    return c(profile, meta);
                });

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

        class IncrementMetadata
        {
            public Mock<IAuthorizationProvider> MockAuth { get; set; }
            public HttpRequest Req { get; set; }
            public TestLogger Logger { get; set; }
            public CounterViewModel GetCounterViewModel { get; set; }
            public string Client { get; set; }

            /// <summary>
            /// Produces the same increment opreation using all availble write keys
            /// </summary>
            /// <param name="count">Total increments</param>
            /// <param name="value">Value of each increment</param>
            /// <returns></returns>
            public async Task<(long inc, decimal val)> Increment(long count = 1, decimal value = 0)
            {
                long increments = 0;
                decimal totalValue = 0;
                Api.Increment.AuthProvider = MockAuth.Object;
                Req.Method = "POST";
                foreach (var key in GetCounterViewModel.WriteKeys)
                {
                    var modifier = count == 1 ? string.Empty : $"&count={count}";
                    modifier += value == 0 ? string.Empty : $"&value={value}";

                    Req.QueryString = new QueryString($"?key={key}{modifier}");
                    var incrementResult = (AcceptedResult)await Api.Increment.Run(Req, Initialize.Counter, Logger).ConfigureAwait(false);
                    Assert.IsNotNull(incrementResult);
                    increments++;
                    totalValue += value;
                }

                return (inc: increments, val: value);
            }
        }
    }
}
