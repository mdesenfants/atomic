using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Diagnostics;

namespace AtomicCounter.Test
{
    [TestClass]
    public class Initialize
    {
        public const string Counter = "testcounter";

        public static CloudStorageAccount Storage;

        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext context)
        {
            const string store = @"C:\Program Files (x86)\Microsoft SDKs\Azure\Storage Emulator\AzureStorageEmulator.exe";

            var start = Process.Start(store, "start");
            start.WaitForExit();

            var clear = Process.Start(store, "clear all");
            clear.WaitForExit();

            Environment.SetEnvironmentVariable("AzureWebJobsStorage", "UseDevelopmentStorage=true;");
            Storage = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
        }
    }
}
