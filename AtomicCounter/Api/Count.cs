using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace AtomicCounter.Api
{
    public static class Count
    {
        public static IAuthorizationProvider AuthProvider = new AuthorizationProvider();

        [FunctionName("Count")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenant/{tenant}/app/{app}/counter/{counter}/count")]HttpRequestMessage req,
            string tenant,
            string app,
            string counter,
            ILogger log)
        {
            log.LogInformation($"Getting a count for tenant/{tenant}/app/{app}/counter/{counter}.");

            return await AuthProvider.AuthorizeAppAndExecute(req, KeyMode.Read, tenant, async () =>
            {
                var storage = new CounterStorage(tenant, app, counter);
                return req.CreateResponse(HttpStatusCode.OK, await storage.CountAsync());
            },
            x => req.CreateResponse(HttpStatusCode.Unauthorized, x));
        }
    }
}
