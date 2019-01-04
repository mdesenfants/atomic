using System;
using System.Threading;
using System.Threading.Tasks;
using AtomicCounter;
using AtomicCounter.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;

namespace AtomicCounterTest
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
            var queue = queueClient.GetQueueReference($"{CounterStorage.Sanitize(testa)}-{CounterStorage.Sanitize(testa)}-{CounterStorage.Sanitize(testa)}");
            await queue.DeleteIfExistsAsync();

            var tableClient = storage.CreateCloudTableClient();
            var table = tableClient.GetTableReference($"{CounterStorage.Tableize(testa)}");
            await table.DeleteIfExistsAsync();

            var blobClient = storage.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference("tenants");
            await blobClient.GetContainerReference("tenants").GetBlockBlobReference(testa).DeleteIfExistsAsync();
            #endregion

            var client = new CounterStorage(
                testa,
                testa,
                testa);

            Assert.AreEqual(0, await client.CountAsync());

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
