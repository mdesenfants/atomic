using AtomicCounter.EventHandlers;
using AtomicCounter.Models;
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

        [TestMethod]
        public void IntervalTest()
        {
            var year = 2020;
            var month = 1;
            var first = 1;

            var stamp = new DateTimeOffset(year, month, first, 0, 0, 0, DateTimeOffset.Now.Offset);

            Assert.AreEqual(DateTimeOffset.MaxValue, HandleInvoiceRequestEvent.GetNextDate(stamp, InvoiceFrequency.Never));

            Assert.AreEqual(stamp.AddDays(7), HandleInvoiceRequestEvent.GetNextDate(stamp, InvoiceFrequency.Weekly));

            Assert.AreEqual(stamp.AddDays(14), HandleInvoiceRequestEvent.GetNextDate(stamp, InvoiceFrequency.EveryOtherWeek));

            var firstHalf = new DateTimeOffset(year, month, 5, 0, 0, 0, stamp.Offset);
            var middle = new DateTimeOffset(year, month, 15, 0, 0, 0, stamp.Offset);
            Assert.AreEqual(middle, HandleInvoiceRequestEvent.GetNextDate(firstHalf, InvoiceFrequency.TwiceMonthly));

            var secondHalf = new DateTimeOffset(year, month, 15, 0, 0, 1, stamp.Offset);
            var end = new DateTimeOffset(year, 2, 1, 0, 0, 0, stamp.Offset);
            Assert.AreEqual(end, HandleInvoiceRequestEvent.GetNextDate(secondHalf, InvoiceFrequency.TwiceMonthly));

            Assert.AreEqual(stamp.AddMonths(1), HandleInvoiceRequestEvent.GetNextDate(stamp, InvoiceFrequency.Monthly));

            Assert.AreEqual(stamp.AddMonths(3), HandleInvoiceRequestEvent.GetNextDate(stamp, InvoiceFrequency.Quarterly));

            Assert.AreEqual(stamp.AddMonths(6), HandleInvoiceRequestEvent.GetNextDate(stamp, InvoiceFrequency.TwiceAnnually));

            Assert.AreEqual(stamp.AddYears(1), HandleInvoiceRequestEvent.GetNextDate(stamp, InvoiceFrequency.Annually));
        }
    }
}
