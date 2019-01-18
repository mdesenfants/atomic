using AtomicCounter.Models.Events;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AtomicCounter.Services
{
    public class CounterStorage
    {
        private readonly string Tenant;
        private readonly string App;
        private readonly string Counter;

        private readonly ILogger logger;

        private string CountPartition => $"{App}-{Counter}";

        public CounterStorage(string tenant, string app, string counter, ILogger logger)
        {
            Tenant = tenant;
            App = app;
            Counter = counter;
            this.logger = logger;
        }

        public async Task SendIncrementEventAsync(long count = 1)
        {
            var queueClient = AppStorage.Storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(AppStorage.CountQueueName);

            var message = new CloudQueueMessage(new IncrementEvent()
            {
                App = App,
                Tenant = Tenant,
                Count = count,
                Counter = Counter
            }.ToString());

            await queue.AddMessageAsync(message);
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

        public async Task CreateCounterLockQueue()
        {
            var queueName = $"count-{Sanitize(Tenant)}-{Sanitize(CountPartition)}";
            try
            {
                var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                var queueClient = storageAccount.CreateCloudQueueClient();

                // Retrieve a reference to a container.
                var queue = queueClient.GetQueueReference(queueName);

                await queue.CreateIfNotExistsAsync();
            }
            catch
            {
                logger.LogError($"Could not get counter queue {queueName}.");
                throw;
            }
        }

        internal CloudQueue GetCounterLockQueue()
        {
            var queueName = $"count-{Sanitize(Tenant)}-{Sanitize(CountPartition)}";
            try
            {
                var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                var queueClient = storageAccount.CreateCloudQueueClient();

                // Retrieve a reference to a container.
                var queue = queueClient.GetQueueReference(queueName);

                return queue;
            }
            catch
            {
                logger.LogError($"Could not get counter queue {queueName}.");
                throw;
            }
        }

        internal CloudTable GetCounterTable()
        {
            var tableName = Tableize(Sanitize(Tenant) + "counts");
            try
            {
                var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                var tableClient = storageAccount.CreateCloudTableClient();
                var table = tableClient.GetTableReference(tableName);
                return table;
            }
            catch
            {
                logger.LogError($"Could not get table {tableName}");
                throw;
            }
        }



        public async Task IncrementAsync(long count = 1)
        {
            var locks = GetCounterLockQueue();
            var @lock = await locks.GetMessageAsync();
            var row = @lock?.AsString ?? Guid.NewGuid().ToString();

            try
            {
                var table = GetCounterTable();

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
                        Count = count
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
                        logger.LogWarning("Couldn't delete lock message. Skipping.");
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
                        logger.LogWarning("Could not add new lock. Skipping.");
                    }
                }
            }
        }

        public async Task<long> CountAsync()
        {
            try
            {
                var table = GetCounterTable();

                if (table == null)
                {
                    return 0;
                }

                var query = new TableQuery<CountEntity>()
                    .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, CountPartition));

                // Print the fields for each customer.
                long sum = 0;
                TableContinuationToken token = null;
                do
                {
                    var resultSegment = await table.ExecuteQuerySegmentedAsync(query, token);
                    token = resultSegment.ContinuationToken;

                    sum += resultSegment.Results.Sum(x => x.Count);
                } while (token != null);

                return sum;
            }
            catch
            {
                logger.LogWarning($"There was a problem counting {Tenant}/{App}/{Counter}. Defaulting to 0.");
                return 0;
            }
        }


    }

    public class CountEntity : TableEntity
    {
        public long Count { get; set; }
    }
}
