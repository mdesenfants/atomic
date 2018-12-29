using System.Threading;
using System.Threading.Tasks;
using AtomicCounter.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;

namespace AtomicCounterTest
{
    [TestClass]
    public class CounterStorageTest
    {
        private const string Tenant = "test_tenant";
        private const string App = "test_app";
        private const string Count = "test_count";

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            var storageAccount = CloudStorageAccount.Parse("UseDevelopmentStorage=true");

            var queueClient = storageAccount.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference($"{CounterStorage.Sanitize(Tenant)}-{CounterStorage.Sanitize(App)}-{CounterStorage.Sanitize(Count)}");
            var what = await queue.DeleteIfExistsAsync();

            var tableClient = storageAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference($"{CounterStorage.Tableize(Tenant)}");
            var what1 = await table.DeleteIfExistsAsync();
        }

        [TestMethod]
        public async Task HappyPathTest()
        {
            var client = new CounterStorage(Tenant, App, Count);

            Assert.AreEqual(0, await client.CountAsync());

            var expected = 1001;
            Parallel.For(0, expected, new ParallelOptions()
            {
                MaxDegreeOfParallelism = 4
            }, async i => await client.IncrementAsync());

            long actual = 0;
            long prev = -1;

            while (actual == 0 || actual > prev)
            {
                prev = actual;
                Thread.Sleep(10000);
                actual = await client.CountAsync();
            }

            Assert.AreEqual(expected, actual);
        }
    }
}
