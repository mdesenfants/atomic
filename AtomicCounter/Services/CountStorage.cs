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
    public class CountStorage
    {
        private readonly string Counter;
        private readonly ILogger logger;
        private const string CountPartition = "counts";

        public CountStorage(string counter, ILogger logger)
        {
            Counter = counter;
            this.logger = logger;
        }

        public async Task SendIncrementEventAsync(long count = 1, string client = null)
        {
            var queueClient = AppStorage.Storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(AppStorage.CountQueueName);

            var message = new CloudQueueMessage(new IncrementEvent()
            {
                Counter = Counter,
                Count = count,
                Client = client
            }.ToString());

            await queue.AddMessageAsync(message);
        }

        public static string Sanitize(string input)
        {
            return new string(input.Where(char.IsLetterOrDigit).ToArray());
        }

        public static string Tableize(string input)
        {
            return $"count{Sanitize(input)}";
        }

        internal CloudTable GetCounterTable()
        {
            var tableName = Tableize(Counter);
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

        public async Task IncrementAsync(Guid id, long count = 1, string client = null)
        {
            var table = GetCounterTable();

            var insert = TableOperation.InsertOrReplace(new CountEntity()
            {
                PartitionKey = CountPartition,
                RowKey = id.ToString(),
                Count = count,
                Client = client
            });

            await table.ExecuteAsync(insert);
        }

        public async Task<long> CountAsync(Func<CountEntity, bool> conditions = null)
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

                    if (conditions != null)
                    {
                        sum += resultSegment.Results.Where(conditions).Sum(x => x.Count);
                    }
                    else
                    {
                        sum += resultSegment.Results.Sum(x => x.Count);
                    }
                } while (token != null);

                return sum;
            }
            catch
            {
                logger.LogWarning($"There was a problem counting {Counter}. Defaulting to 0.");
                return 0;
            }
        }

        public async Task<long> CountAsync(string client)
        {
            return await CountAsync(x => client.Equals(x.Client, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<long> CountAsync(DateTimeOffset min, DateTimeOffset max)
        {
            return await CountAsync(x => x.Timestamp >= min && x.Timestamp <= max);
        }

        public async Task<long> CountAsync(string client, DateTimeOffset min, DateTimeOffset max)
        {
            return await CountAsync(x =>
                client.Equals(x.Client, StringComparison.OrdinalIgnoreCase) &&
                x.Timestamp >= min &&
                x.Timestamp <= max);
        }
    }

    public class CountEntity : TableEntity
    {
        public long Count { get; set; }
        public string Client { get; set; }
    }
}
