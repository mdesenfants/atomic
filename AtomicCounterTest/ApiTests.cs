using AtomicCounter;
using AtomicCounter.Api;
using AtomicCounter.Models;
using AtomicCounter.Models.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Hosting;

namespace AtomicCounterTest
{
    [TestClass]
    public class ApiTests
    {
        [TestMethod]
        public async Task AddTenantTest()
        {
            var tenant = "wilhelm";

            var profile = new UserProfile()
            {
                Email = "billg@microsoft.com",
                Id = Guid.NewGuid()
            };

            var mockAuth = new Mock<IAuthorizationProvider>();

            mockAuth
                .Setup(m =>
                    m.AuthorizeUserAndExecute(
                        It.IsAny<HttpRequestMessage>(),
                        It.IsAny<Func<UserProfile, Task<HttpResponseMessage>>>(),
                        It.IsAny<Func<string, HttpResponseMessage>>()))
                .Returns<HttpRequestMessage, Func<UserProfile, Task<HttpResponseMessage>>, Func<string, HttpResponseMessage>>((a, b, c) => b(profile));

            var req = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("http://google.com")
            };

            req.Properties[HttpPropertyKeys.HttpConfigurationKey] = new HttpConfiguration();

            AddTenant.AuthProvider = mockAuth.Object;
            var result = await AddTenant.Run(req, "wilhelm", new TestLogger());
            var content = await result.Content.ReadAsStringAsync();
            var tenantViewModel = JsonConvert.DeserializeObject<TenantViewModel>(content);

            Assert.IsNotNull(tenantViewModel);
            Assert.IsNotNull(tenantViewModel.ReadKeys);
            Assert.IsNotNull(tenantViewModel.WriteKeys);
            Assert.AreEqual(2, tenantViewModel.ReadKeys.Count());
            Assert.AreEqual(2, tenantViewModel.WriteKeys.Count());
            Assert.AreEqual(tenant, tenantViewModel.TenantName);
        }

        [TestMethod]
        public async Task CountTest()
        {
            await Task.CompletedTask;
        }

        [TestMethod]
        public async Task GetTenantTest()
        {
            await Task.CompletedTask;
        }

        [TestMethod]
        public async Task IncrementTEst()
        {
            await Task.CompletedTask;
        }
    }
}
