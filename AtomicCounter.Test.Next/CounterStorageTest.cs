using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace AtomicCounter.Test
{
    [TestClass]
    public class CounterStorageTest
    {
        [TestMethod]
        public void KeyTest()
        {
            Func<string> a, b;
            a = b = () => AuthorizationHelpers.CombineAndHash("a", "b");
            Assert.AreEqual(a(), b());
        }
    }
}
