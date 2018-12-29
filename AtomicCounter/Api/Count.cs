using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace AtomicCounter.Api
{
    public static class Count
    {
        [FunctionName("Count")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenant/{tenant}/app/{app}/counter/{counter}/count")]HttpRequestMessage req,
            string tenant,
            string app,
            string counter,
            TraceWriter log)
        {
            log.Info($"Getting a count for {tenant}/{app}/{counter}.");

            var storage = new CounterStorage(tenant, app, counter);

            return req.CreateResponse(HttpStatusCode.OK, await storage.CountAsync());
        }
    }
}
