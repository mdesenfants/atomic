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

            //req.Properties[HttpConfigurationKey] = new HttpConfiguration();
            var logger = new TestLogger();

            // Add a tenant
            AddTenant.AuthProvider = mockAuth.Object;
            var result = await AddTenant.Run(req, Initialize.Tenant, logger);
            var content = (OkObjectResult)result;
            var tenantViewModel = (TenantViewModel)content.Value;

            Assert.IsNotNull(tenantViewModel);
            Assert.IsNotNull(tenantViewModel.ReadKeys);
            Assert.IsNotNull(tenantViewModel.WriteKeys);
            Assert.AreEqual(2, tenantViewModel.ReadKeys.Count());
            Assert.AreEqual(2, tenantViewModel.WriteKeys.Count());
            Assert.AreEqual(Initialize.Tenant, tenantViewModel.TenantName);

            // Get existing tenant
            GetTenant.AuthProvider = mockAuth.Object;
            req.Method = "GET";
            var getTenantResult = (OkObjectResult)await GetTenant.Run(req, Initialize.Tenant, logger);
            var getTenantViewModel = (TenantViewModel)getTenantResult.Value;

            Assert.IsNotNull(getTenantViewModel);
            Assert.IsNotNull(getTenantViewModel.ReadKeys);
            Assert.IsNotNull(getTenantViewModel.WriteKeys);
            Assert.AreEqual(tenantViewModel.ReadKeys.Count(), getTenantViewModel.ReadKeys.Count());
            Assert.AreEqual(tenantViewModel.WriteKeys.Count(), getTenantViewModel.WriteKeys.Count());
            Assert.AreEqual(Initialize.Tenant, getTenantViewModel.TenantName);

            // Increment counter
            await Increment(mockAuth, req, logger, getTenantViewModel);

            // Handle count event
            await HandleCountEvent(logger);

            // Get count (all but one key increments by 2, so result is (writeKeys * 2) - 1
            await GetCount(mockAuth, req, logger, getTenantViewModel, 3);

            // Rotate read keys
            await RotateReadKeys(mockAuth, req, logger, getTenantViewModel, 1);

            // Rotate write keys
            await RotateWriteKeys(mockAuth, req, logger, getTenantViewModel, 1);

            // Rotate read keys again
            await RotateReadKeys(mockAuth, req, logger, getTenantViewModel, 0);

            // Rotate write keys again
            await RotateWriteKeys(mockAuth, req, logger, getTenantViewModel, 0);

            // Increment counter
            await Increment(mockAuth, req, logger, getTenantViewModel);

            // Handle count event
            await HandleCountEvent(logger);

            // Get count (all but one key increments by 2, so result is (writeKeys * 2) - 1)
            await GetCount(mockAuth, req, logger, getTenantViewModel, 5);
        }

        private static async Task GetCount(Mock<IAuthorizationProvider> mockAuth, HttpRequest req, TestLogger logger, TenantViewModel getTenantViewModel, long expected)
        {
            Count.AuthProvider = mockAuth.Object;
            req.Method = "GET";
            var iteration = 1;
            foreach (var key in getTenantViewModel.ReadKeys)
            {
                req.QueryString = new QueryString("?key=" + key);
                var countResult = (OkObjectResult)await Count.Run(req, Initialize.Tenant, Initialize.App, Initialize.Counter, logger);
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

        private static async Task Increment(Mock<IAuthorizationProvider> mockAuth, HttpRequest req, TestLogger logger, TenantViewModel getTenantViewModel)
        {
            AtomicCounter.Api.Increment.AuthProvider = mockAuth.Object;
            req.Method = "POST";
            var defaultHasRun = false;
            foreach (var key in getTenantViewModel.WriteKeys)
            {
                var modifier = defaultHasRun ? "" : "&count=2";
                req.QueryString = new QueryString($"?key={key}{modifier}");
                var incrementResult = (AcceptedResult)await AtomicCounter.Api.Increment.Run(req, Initialize.Tenant, Initialize.App, Initialize.Counter, logger);
                Assert.IsNotNull(incrementResult);
                defaultHasRun = true;
            }
        }

        private static async Task RotateReadKeys(Mock<IAuthorizationProvider> mockAuth, HttpRequest req, TestLogger logger, TenantViewModel getTenantViewModel, int expected)
        {
            RotateKeys.Authorization = mockAuth.Object;
            req.Method = "POST";
            var readRotateResult = (OkObjectResult)await RotateKeys.Run(req, Initialize.Tenant, "read", logger);
            var readKeys = (string[])readRotateResult.Value;
            Assert.AreEqual(2, readKeys.Length);
            var readDupeCount = readKeys.Count(k => getTenantViewModel.ReadKeys.Contains(k));
            Assert.AreEqual(expected, readDupeCount);
        }

        private static async Task RotateWriteKeys(Mock<IAuthorizationProvider> mockAuth, HttpRequest req, TestLogger logger, TenantViewModel getTenantViewModel, int expected)
        {
            RotateKeys.Authorization = mockAuth.Object;
            req.Method = "POST";
            var writeRotateResult = (OkObjectResult)await RotateKeys.Run(req, Initialize.Tenant, "write", logger);
            var writeKeys = (string[])writeRotateResult.Value;
            Assert.AreEqual(2, writeKeys.Length);
            var writeDupeCount = writeKeys.Count(k => getTenantViewModel.WriteKeys.Contains(k));
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
                        Initialize.Tenant,
                        It.IsAny<AppAuthDelegate>()))
                .Returns<HttpRequest, KeyMode, string, AppAuthDelegate>((a, b, c, d) => d());

            return mockAuth;
        }
    }
}
