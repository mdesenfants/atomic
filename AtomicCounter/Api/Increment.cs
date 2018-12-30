using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

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

            return await req.AuthorizeAppAndExecute(async () =>
            {
                await AppStorage.SendIncrementEventAsync(tenant, app, counter, count);
                return req.CreateResponse(HttpStatusCode.Accepted);
            },
            async () => await Task.FromResult(req.CreateResponse(HttpStatusCode.Unauthorized, "Provide a valid token to the key query parameter.")));
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
