using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace AtomicCounter.Api
{
    public static class Increment
    {
        public static IAuthorizationProvider AuthProvider = new AuthorizationProvider();

        [FunctionName("Increment")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenant/{tenant}/app/{app}/counter/{counter}/increment")]HttpRequestMessage req,
            string tenant,
            string app,
            string counter,
            ILogger log)
        {
            log.LogInformation($"Incrementing tenant/{tenant}/app/{app}/counter/{counter}/increment");
            var count = GetCount(req);

            return await AuthProvider.AuthorizeAppAndExecute(req, KeyMode.Write, tenant,
            async () =>
            {
                await AppStorage.SendIncrementEventAsync(tenant, app, counter, count);
                return req.CreateResponse(HttpStatusCode.Accepted);
            });
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
