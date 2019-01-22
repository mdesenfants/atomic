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

        [FunctionName(nameof(RotateKeys))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "{counter}/keys/{readorwrite}/rotate")]HttpRequest req,
            string counter,
            string readorwrite,
            ILogger log)
        {
            log.LogInformation($"Start rotating {readorwrite} keys for {counter}.");

            return await Authorization.AuthorizeUserAndExecute(req, counter, async (user, meta) =>
            {
                var result = await AppStorage.RotateKeysAsync(meta, (KeyMode)Enum.Parse(typeof(KeyMode), readorwrite, true));
                return new OkObjectResult(result);
            });
        }
    }
}
