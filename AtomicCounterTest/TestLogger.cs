using Microsoft.Extensions.Logging;
using Moq;
using System;

namespace AtomicCounterTest
{
    public class TestLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
        {
            return Mock.Of<IDisposable>();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            return;
        }
    }
}
