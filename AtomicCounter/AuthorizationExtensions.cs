using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace AtomicCounter
{
    internal static class AuthorizationExtensions
    {
        internal const string UnauthorizedMessage = "Unauthorized";

        internal static async Task<T> AuthorizeAndExecute<T>(this HttpRequestMessage req, Func<Task<T>> action)
        {
            var key = req
                .GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Equals(q.Key, "key", StringComparison.OrdinalIgnoreCase))
                .Value;

            if (key == null)
            {
                throw new InvalidOperationException(UnauthorizedMessage);
            }

            return await action();
        }
    }
}
