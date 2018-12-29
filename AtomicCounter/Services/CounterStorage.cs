using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;

namespace AtomicCounter.Services
{
    public class CounterStorage
    {
        private readonly string Tenant;
        private readonly string App;
        private readonly string Counter;

        private string CountPartition => $"{App}-{Counter}";

        public CounterStorage(string tenant, string app, string counter)
        {
            Tenant = Sanitize(tenant);
            App = Sanitize(app);
            Counter = Sanitize(counter);
        }

        public static string Sanitize(string input)
        {
            return new string(input.Where(char.IsLetterOrDigit).ToArray());
        }

        public static string Tableize(string input)
        {
            var value = Sanitize(input);
            while (value.Length < 3)
            {
                value += 'a';
            }

            if (!char.IsLetter(value[0]))
            {
                value = 'a' + value;
            }

            if (!char.IsLetter(value[value.Length - 1]))
            {
                value += 'a';
            }

            return value;
        }

        internal async Task<CloudQueue> GetCounterQueue()
        {
            var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            var queueClient = storageAccount.CreateCloudQueueClient();

            // Retrieve a reference to a container.
            var queue = queueClient.GetQueueReference($"{Tenant}-{CountPartition}");

            // Create the queue if it doesn't already exist
            await queue.CreateIfNotExistsAsync();

            return queue;
        }

        internal async Task<CloudTable> GetCounterTable()
        {
            var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            var tableClient = storageAccount.CreateCloudTableClient();

            var tableName = Tableize(Tenant);
            var table = tableClient.GetTableReference(tableName);

            await table.CreateIfNotExistsAsync();

            return table;
        }

        public async Task IncrementAsync()
        {

            var locks = await GetCounterQueue();
            var @lock = await locks.GetMessageAsync();
            var row = @lock?.AsString ?? Guid.NewGuid().ToString();

            try
            {
                var table = await GetCounterTable();

                var read = TableOperation.Retrieve<CountEntity>(CountPartition, row);
                var result = await table.ExecuteAsync(read);

                if (result.Result != null)
                {
                    var current = (CountEntity)result.Result;

                    current.Count++;

                    var update = TableOperation.Replace(current);

                    await table.ExecuteAsync(update);
                }
                else
                {
                    var insert = TableOperation.Insert(new CountEntity()
                    {
                        PartitionKey = CountPartition,
                        RowKey = row,
                        Count = 1
                    });

                    await table.ExecuteAsync(insert);
                }
            }
            finally
            {
                if (@lock != null)
                {
                    try
                    {
                        await locks.DeleteMessageAsync(@lock);
                    }
                    catch
                    {
                        // not a big deal
                    }
                }

                if (row != null)
                {
                    try
                    {
                        await locks.AddMessageAsync(new CloudQueueMessage(row));
                    }
                    catch
                    {
                        // not a big deal
                    }
                }
            }
        }

        public async Task<long> CountAsync()
        {
            var table = await GetCounterTable();

            var query = new TableQuery<CountEntity>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, CountPartition));

            // Print the fields for each customer.
            long sum = 0;
            TableContinuationToken token = null;
            do
            {
                var resultSegment = await table.ExecuteQuerySegmentedAsync(query, token);
                token = resultSegment.ContinuationToken;

                sum += resultSegment.Results.Select(x => x.Count).ToArray().Sum();
            } while (token != null);

            return sum;
        }
    }

    public class CountEntity : TableEntity
    {
        public long Count { get; set; }
    }
}
