using AtomicCounter.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace AtomicCounter
{
    public interface IAuthorizationProvider
    {
        Task<IActionResult> AuthorizeAppAndExecute(HttpRequest req, KeyMode mode, string tenant, Func<Task<IActionResult>> action);
        Task<IActionResult> AuthorizeUserAndExecute(HttpRequest req, Func<UserProfile, Task<IActionResult>> action);
    }
}
