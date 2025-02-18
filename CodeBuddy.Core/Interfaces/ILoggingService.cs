using System;
using CodeBuddy.Core.Models.Logging;
using System.Threading.Tasks;

namespace CodeBuddy.Core.Interfaces
{
    public interface ILoggingService
    {
        void Log(LogEntry entry);
        Task LogAsync(LogEntry entry);
        
        void Debug(string message, string component = null, string correlationId = null, object data = null);
        void Info(string message, string component = null, string correlationId = null, object data = null);
        void Warning(string message, string component = null, string correlationId = null, object data = null);
        void Error(string message, Exception ex = null, string component = null, string correlationId = null, object data = null);
        void Critical(string message, Exception ex = null, string component = null, string correlationId = null, object data = null);
        
        Task DebugAsync(string message, string component = null, string correlationId = null, object data = null);
        Task InfoAsync(string message, string component = null, string correlationId = null, object data = null);
        Task WarningAsync(string message, string component = null, string correlationId = null, object data = null);
        Task ErrorAsync(string message, Exception ex = null, string component = null, string correlationId = null, object data = null);
        Task CriticalAsync(string message, Exception ex = null, string component = null, string correlationId = null, object data = null);
        
        void Configure(LoggingConfiguration config);
        void Flush();
    }
}