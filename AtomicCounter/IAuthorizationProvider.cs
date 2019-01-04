using AtomicCounter.Models;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace AtomicCounter
{
    public interface IAuthorizationProvider
    {
        Task<HttpResponseMessage> AuthorizeAppAndExecute(HttpRequestMessage req, KeyMode mode, string tenant, Func<Task<HttpResponseMessage>> action);
        Task<HttpResponseMessage> AuthorizeUserAndExecute(HttpRequestMessage req, Func<UserProfile, Task<HttpResponseMessage>> action);
    }
}
