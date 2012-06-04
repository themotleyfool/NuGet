using System;
using System.Web;
using Elmah;

namespace NuGet.Server
{
    public class InfoException : Exception
    {
        public InfoException(string message)
            : base(message)
        {
        }
    }

    public static class Log
    {
        public static void Info(string message)
        {
            DefaultLog.Log(new Error(new InfoException(message)));
        }

        public static void Error(string message, Exception ex)
        {
            DefaultLog.Log(new Error(new Exception(message, ex)));
        }

        public static void Error(Exception ex)
        {
            DefaultLog.Log(new Error(ex));
        }

        private static ErrorLog DefaultLog
        {
            get { return ErrorLog.GetDefault(HttpContext.Current); }
        }
    }
}