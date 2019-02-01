﻿using AtomicCounter.Api;
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
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppAuthDelegate = System.Func<System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.IActionResult>>;
using UserAuthDelegate = System.Func<AtomicCounter.Models.UserProfile, System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.IActionResult>>;
using UserCounterDelegate = System.Func<AtomicCounter.Models.UserProfile, AtomicCounter.Models.Counter, System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.IActionResult>>;

namespace AtomicCounter.Test
{
    [TestClass]
    public class ApiTests
    {
        private const string ClientName = "client";

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
            var counterViewModel = await AddCounter(mockAuth, req, logger);

            // Get existing counter
            var getCounterViewModel = await GetExistingCounter(mockAuth, req, logger, counterViewModel);

            // Increment counter
            await Increment(mockAuth, req, logger, getCounterViewModel);

            // Handle count event
            await HandleCountEvents(logger);

            // Get count (all but one key increments by 2, so result is (writeKeys * 2) - 1
            await GetCount(mockAuth, req, logger, getCounterViewModel, 3, "First count.");

            // Increment counter for client and time rnage
            var min = new DateTime(DateTime.UtcNow.Ticks, DateTimeKind.Utc);

            await SetPrice(mockAuth, req, counterViewModel.CounterName, logger, 1);
            getCounterViewModel = await GetExistingCounter(mockAuth, req, logger, counterViewModel);
            Assert.IsNotNull(getCounterViewModel.PriceChanges.SingleOrDefault());
            await Increment(mockAuth, req, logger, getCounterViewModel, ClientName);
            await HandleCountEvents(logger);

            await SetPrice(mockAuth, req, counterViewModel.CounterName, logger, 2);
            getCounterViewModel = await GetExistingCounter(mockAuth, req, logger, counterViewModel);
            Assert.IsTrue(getCounterViewModel.PriceChanges.Count() == 2);
            await Increment(mockAuth, req, logger, getCounterViewModel, ClientName);
            await HandleCountEvents(logger);

            var max = DateTimeOffset.UtcNow;

            // Check counts for different slices
            await GetCount(mockAuth, req, logger, getCounterViewModel, 6, "Client count.", client: ClientName);
            await GetCount(mockAuth, req, logger, getCounterViewModel, 6, "Date count.", min: min, max: max);
            await GetCount(mockAuth, req, logger, getCounterViewModel, 6, "Client and date count", min: min, max: max, client: ClientName);

            // Get the invoice for the last 6 entries
            var counterClient = new CountStorage(getCounterViewModel.CounterName, logger);
            var invoice = await counterClient.GetInvoiceDataAsync(min, max);
            Assert.AreEqual(1, invoice.Keys.Count());
            var lines = invoice.Values.First();
            Assert.AreEqual(2, lines.Count());
            Assert.AreEqual(9, lines.Sum(l => l.Price * l.Quantity));

            // Get count (all but one key increments by 2, so result is (writeKeys * 2) - 1
            await GetCount(mockAuth, req, logger, getCounterViewModel, 9, "First count.");

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
            await HandleCountEvents(logger);

            // Get count (all but one key increments by 2, so result is (writeKeys * 2) - 1)
            await GetCount(mockAuth, req, logger, getCounterViewModel, 12, "Count after rotation.");

            // Should reset count to zero
            await RunResetCounter(mockAuth, req, logger);

            // Makes sure counter was reset
            await GetCount(mockAuth, req, logger, getCounterViewModel, 0, "Count after reset.");
        }

        private static async Task SetPrice(Mock<IAuthorizationProvider> mockAuth, HttpRequest req, string counter, TestLogger logger, decimal amount)
        {
            SubmitPriceChange.AuthProvider = mockAuth.Object;
            req.Method = "POST";

            var change = JsonConvert.SerializeObject(new PriceChangeEvent()
            {
                Counter = counter,
                Amount = amount,
                Currency = "usd",
            });

            using (var body = new MemoryStream(Encoding.UTF8.GetBytes(change)))
            {
                req.Body = body;
                var readRotateResult = (AcceptedResult)await SubmitPriceChange.Run(req, counter, logger);
                Assert.IsNotNull(readRotateResult);
            }

            var queueClient = Initialize.Storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(AppStorage.PriceChangeEventsQueueName);

            do
            {
                var countEvents = await queue.GetMessagesAsync(30);
                if (countEvents == null || countEvents.Count() == 0) return;

                foreach (var evt in countEvents)
                {
                    await PriceChangeEventHandler.Run(evt.AsString.FromJson<PriceChangeEvent>(), logger);
                    await queue.DeleteMessageAsync(evt);
                }
            } while (true);
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

            await ResetEventHandler.Run(Initialize.Counter, logger);

            await RecreateEventHandler.Run(Initialize.Counter, logger);
        }

        private static async Task GetCount(Mock<IAuthorizationProvider> mockAuth, HttpRequest req, TestLogger logger, CounterViewModel getCounterViewModel, long expected, string message, string client = null, DateTimeOffset? min = null, DateTimeOffset? max = null)
        {
            Count.AuthProvider = mockAuth.Object;
            req.Method = "GET";
            var iteration = 1;
            foreach (var key in getCounterViewModel.ReadKeys)
            {
                var query = "?key=" + key;
                query += string.IsNullOrEmpty(client) ? string.Empty : $"&client={client}";
                query += min == null ? string.Empty : $"&min={Uri.EscapeDataString(min?.ToString("o", CultureInfo.InvariantCulture))}";
                query += max == null ? string.Empty : $"&max={Uri.EscapeDataString(max?.ToString("o", CultureInfo.InvariantCulture))}";

                req.QueryString = new QueryString(query);
                var countResult = (OkObjectResult)await Count.Run(req, Initialize.Counter, logger);
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
                var countEvents = await queue.GetMessagesAsync(30);
                if (countEvents == null || countEvents.Count() == 0) return;

                foreach (var evt in countEvents)
                {
                    await IncrementEventHandler.Run(JsonConvert.DeserializeObject<IncrementEvent>(evt.AsString), logger);
                    await queue.DeleteMessageAsync(evt);
                }
            } while (true);
        }

        private static async Task Increment(Mock<IAuthorizationProvider> mockAuth, HttpRequest req, TestLogger logger, CounterViewModel getCounterViewModel, string client = null)
        {
            Api.Increment.AuthProvider = mockAuth.Object;
            req.Method = "POST";
            var defaultHasRun = false;
            foreach (var key in getCounterViewModel.WriteKeys)
            {
                var modifier = defaultHasRun ? string.Empty : "&count=2";
                modifier += string.IsNullOrEmpty(client) ? string.Empty : $"&client={client}";

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
    }
}
