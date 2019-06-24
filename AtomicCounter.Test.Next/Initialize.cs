using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Diagnostics;

namespace AtomicCounter.Test
{
    [TestClass]
    public static class Initialize
    {
        public const string Counter = "1test-counter";

        public static CloudStorageAccount Storage { get; set; }

        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext context)
        {
            Debug.Assert(context != null);
            const string store = @"C:\Program Files (x86)\Microsoft SDKs\Azure\Storage Emulator\AzureStorageEmulator.exe";

            try
            {
                var start = Process.Start(store, "start");
                start.WaitForExit();

                var clear = Process.Start(store, "clear all");
                clear.WaitForExit();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            Environment.SetEnvironmentVariable("AzureWebJobsStorage", "UseDevelopmentStorage=true;");
            Storage = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
        }
    }
}
