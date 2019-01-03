using AtomicCounter.Models;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace AtomicCounter
{
    public interface IAuthorizationProvider
    {
        Task<T> AuthorizeAppAndExecute<T>(HttpRequestMessage req, KeyMode mode, string tenant, Func<Task<T>> action, Func<string, T> otherwise);
        Task<T> AuthorizeUserAndExecute<T>(HttpRequestMessage req, Func<UserProfile, Task<T>> action, Func<string, T> otherwise);
    }
}
