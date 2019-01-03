using AtomicCounter.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace AtomicCounterTest
{
    [TestClass]
    public class Initialize
    {
        private const string Tenant = "test_tenant";
        private const string App = "test_app";
        private const string Count = "test_count";

        [AssemblyInitialize]
        public static async Task AssemblyInitialize(TestContext context)
        {
            Environment.SetEnvironmentVariable("AzureWebJobsStorage", "UseDevelopmentStorage=true;");
            var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));

            var queueClient = storageAccount.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference($"{CounterStorage.Sanitize(Tenant)}-{CounterStorage.Sanitize(App)}-{CounterStorage.Sanitize(Count)}");
            var what = await queue.DeleteIfExistsAsync();

            var tableClient = storageAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference($"{CounterStorage.Tableize(Tenant)}");
            var what1 = await table.DeleteIfExistsAsync();
        }
    }
}
