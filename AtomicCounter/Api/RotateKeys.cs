using AtomicCounter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AtomicCounter.Api
{
    public static class RotateKeys
    {
        public static IAuthorizationProvider Authorization = new AuthorizationProvider();

        [FunctionName("RotateKeys")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "{tenant}/keys/{readorwrite}/rotate")]HttpRequest req,
            string tenant,
            string readorwrite,
            ILogger log)
        {
            log.LogInformation($"Start rotating {readorwrite} keys for {tenant}.");

            return await Authorization.AuthorizeUserAndExecute(req, async (user) =>
            {
                var result = await AppStorage.RotateKeysAsync(user, tenant, (KeyMode)Enum.Parse(typeof(KeyMode), readorwrite, true));

                if (result == null) return new NotFoundResult();

                return new OkObjectResult(result);
            });
        }
    }
}
