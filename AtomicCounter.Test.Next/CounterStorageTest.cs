using AtomicCounter.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AtomicCounter.Test
{
    [TestClass]
    public class CounterStorageTest
    {
        [TestMethod]
        public async Task CounterThroughputTest()
        {
            const string testa = "testa";

            #region cleanup
            Environment.SetEnvironmentVariable("AzureWebJobsStorage", "UseDevelopmentStorage=true;");
            var storage = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));

            var queueClient = storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference($"lock-{CountStorage.Sanitize(testa)}");
            await queue.DeleteIfExistsAsync();

            var tableClient = storage.CreateCloudTableClient();
            var table = tableClient.GetTableReference(CountStorage.Tableize(testa));
            await table.DeleteIfExistsAsync();

            var blobClient = storage.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(AppStorage.CountersKey);
            await blobClient.GetContainerReference(AppStorage.CountersKey).GetBlockBlobReference(testa).DeleteIfExistsAsync();
            #endregion

            var logger = new TestLogger();
            var client = new CountStorage(testa, logger);
            Assert.AreEqual(0, await client.CountAsync());

            var profile = new Models.UserProfile() { Id = Guid.NewGuid() };
            await AppStorage.GetOrCreateCounterAsync(profile, testa, logger);

            var expected = 100;
            Parallel.For(0, expected, new ParallelOptions()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
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

        [TestMethod]
        public void KeyTest()
        {
            Func<string> a, b;
            a = b = () => AuthorizationHelpers.CombineAndHash("a", "b");
            Assert.AreEqual(a(), b());
        }
    }
}
