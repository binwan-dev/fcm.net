using System;

namespace Fcm.Logging
{
    public interface ILogger<T> where T : class
    {
        void Debug(string msg, params object[] args);

        void Info(string msg, params object[] args);

        void Warning(string msg, params object[] args);

        void Error(Exception ex, string msg, params object[] args);
    }
}
