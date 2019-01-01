using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace AtomicCounter.Api
{
    public static class Increment
    {
        [FunctionName("Increment")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenant/{tenant}/app/{app}/counter/{counter}/increment")]HttpRequestMessage req,
            TraceWriter log,
            string tenant,
            string app,
            string counter)
        {
            log.Info($"Incrementing tenant/{tenant}/app/{app}/counter/{counter}/increment");
            var count = GetCount(req);

            return await req.AuthorizeAppAndExecute(KeyMode.Write, tenant, async () =>
            {
                await AppStorage.SendIncrementEventAsync(tenant, app, counter, count);
                return req.CreateResponse(HttpStatusCode.Accepted);
            },
            x => req.CreateResponse(HttpStatusCode.Unauthorized, x));
        }

        private static long GetCount(HttpRequestMessage req)
        {
            var countString = req
                .GetQueryNameValuePairs()
                .FirstOrDefault(p => p.Key.Equals("count", StringComparison.OrdinalIgnoreCase))
                .Value ?? string.Empty;

            long count = 1;
            if (long.TryParse(countString, out var parsed))
            {
                count = parsed;
            }

            return count;
        }
    }
}
