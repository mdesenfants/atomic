﻿using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Hosting;
using AtomicCounter;
using AtomicCounter.Api;
using AtomicCounter.EventHandlers;
using AtomicCounter.Models;
using AtomicCounter.Models.Events;
using AtomicCounter.Models.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using AppAuthDelegate = System.Func<System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage>>;
using FailedAuthDelegate = System.Func<string, System.Net.Http.HttpResponseMessage>;
using UserAuthDelegate = System.Func<AtomicCounter.Models.UserProfile, System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage>>;

namespace AtomicCounterTest
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

            var mockAuth = new Mock<IAuthorizationProvider>();

            mockAuth
                .Setup(m =>
                    m.AuthorizeUserAndExecute(
                        It.IsAny<HttpRequestMessage>(),
                        It.IsAny<UserAuthDelegate>(),
                        It.IsAny<FailedAuthDelegate>()))
                .Returns<HttpRequestMessage, UserAuthDelegate, FailedAuthDelegate>((a, b, c) => b(profile));

            mockAuth
                .Setup(m =>
                    m.AuthorizeAppAndExecute(
                        It.IsAny<HttpRequestMessage>(),
                        It.IsAny<KeyMode>(),
                        Initialize.Tenant,
                        It.IsAny<AppAuthDelegate>(),
                        It.IsAny<FailedAuthDelegate>()))
                .Returns<HttpRequestMessage, KeyMode, string, AppAuthDelegate, FailedAuthDelegate>((a, b, c, d, e) => d());

            var req = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://google.com")
            };

            req.Properties[HttpPropertyKeys.HttpConfigurationKey] = new HttpConfiguration();
            var logger = new TestLogger();

            // Add a tenant
            AddTenant.AuthProvider = mockAuth.Object;
            var result = await AddTenant.Run(req, Initialize.Tenant, logger);
            var content = await result.Content.ReadAsStringAsync();
            var tenantViewModel = JsonConvert.DeserializeObject<TenantViewModel>(content);

            Assert.IsNotNull(tenantViewModel);
            Assert.IsNotNull(tenantViewModel.ReadKeys);
            Assert.IsNotNull(tenantViewModel.WriteKeys);
            Assert.AreEqual(2, tenantViewModel.ReadKeys.Count());
            Assert.AreEqual(2, tenantViewModel.WriteKeys.Count());
            Assert.AreEqual(Initialize.Tenant, tenantViewModel.TenantName);

            // Get existing tenant
            GetTenant.AuthProvider = mockAuth.Object;
            req.Method = HttpMethod.Get;
            var getTenantResult = await GetTenant.Run(req, Initialize.Tenant, logger);
            var getTenantContent = await getTenantResult.Content.ReadAsStringAsync();
            var getTenantViewModel = JsonConvert.DeserializeObject<TenantViewModel>(content);

            Assert.IsNotNull(getTenantViewModel);
            Assert.IsNotNull(getTenantViewModel.ReadKeys);
            Assert.IsNotNull(getTenantViewModel.WriteKeys);
            Assert.AreEqual(tenantViewModel.ReadKeys.Count(), getTenantViewModel.ReadKeys.Count());
            Assert.AreEqual(tenantViewModel.WriteKeys.Count(), getTenantViewModel.WriteKeys.Count());
            Assert.AreEqual(Initialize.Tenant, getTenantViewModel.TenantName);

            // Increment counter
            Increment.AuthProvider = mockAuth.Object;
            req.Method = HttpMethod.Post;
            bool defaultHasRun = false;
            foreach (var key in getTenantViewModel.WriteKeys)
            {
                var modifier = defaultHasRun ? "": "&count=2";
                req.RequestUri = new Uri($"https://localhost:2020/?key={key}{modifier}");
                var incrementResult = await Increment.Run(req, Initialize.Tenant, Initialize.App, Initialize.Counter, logger);
                Assert.AreEqual(HttpStatusCode.Accepted, incrementResult.StatusCode);
                defaultHasRun = true;
            }

            // Handle count event
            var queueClient = Initialize.Storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference("increment-items");
            var countEvents = await queue.GetMessagesAsync(2);

            foreach (var evt in countEvents)
            {
                await IncrementEventHandler.Run(JsonConvert.DeserializeObject<IncrementEvent>(evt.AsString), logger);
                await queue.DeleteMessageAsync(evt);
            }

            // Get count
            Count.AuthProvider = mockAuth.Object;
            req.Method = HttpMethod.Get;
            var iteration = 1;
            foreach (var key in getTenantViewModel.ReadKeys)
            {
                req.RequestUri = new Uri("https://localhost:2020/?key=" + key);
                var countResult = await Count.Run(req, Initialize.Tenant, Initialize.App, Initialize.Counter, logger);
                Assert.AreEqual(HttpStatusCode.OK, countResult.StatusCode);

                var finalCount = long.Parse(await countResult.Content.ReadAsStringAsync());
                Assert.AreEqual((getTenantViewModel.ReadKeys.Count() * 2) - 1, finalCount, "Mismatch on iteration {0} with key {1}.", iteration, key);
                iteration++;
            }
        }
    }
}
