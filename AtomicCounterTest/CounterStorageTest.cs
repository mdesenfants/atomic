using System;
using System.Threading;
using System.Threading.Tasks;
using AtomicCounter;
using AtomicCounter.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AtomicCounterTest
{
    [TestClass]
    public class CounterStorageTest
    {
        [TestMethod]
        [Ignore]
        public async Task CounterThroughputTest()
        {
            var client = new CounterStorage(
                "testa",
                "testa",
                "testa");

            Assert.AreEqual(0, await client.CountAsync());

            var expected = 1001;
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
