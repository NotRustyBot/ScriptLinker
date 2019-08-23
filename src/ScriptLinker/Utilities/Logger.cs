﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptLinker.Utilities
{
    enum LogLevel
    {
        Info,
        Warning,
        Error,
        Exception,
    }

    static class Logger
    {
        public static string LogDirectory { get; private set; }
        public static string FileName { get; private set; }
        public static string FilePath
        {
            get { return Path.Combine(LogDirectory, FileName); }
        }
        public static string Extension { get; set; } = "txt";
        /// <summary>
        /// Date format to use in file name
        /// </summary>
        public static string DateFormat { get; set; } = "yyyy_MM_dd";
        public static LogLevel LogLevel { get; set; } = LogLevel.Info;

        static Logger()
        {
            LogDirectory = Path.Combine(ApplicationPath.ApplicationData, "Reports");
            FileName = string.Format("{0}.{1}", DateTime.Now.ToString(DateFormat), Extension);

            Directory.CreateDirectory(LogDirectory);
        }

        public static void Info(string message)
        {
            Log(message, LogLevel.Info);
        }
        public static void Warning(string message)
        {
            Log(message, LogLevel.Warning);
        }
        public static void Error(string message)
        {
            Log(message, LogLevel.Error);
        }
        public static void Log(Exception exception)
        {
            Log(GetExceptionMessage(exception), LogLevel.Exception);
        }

        private static string GetExceptionMessage(Exception ex)
        {
            var sb = new StringBuilder();
            var stackTrace = ex.StackTrace.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            var indent = new string(' ', 3);

            sb.AppendLine();
            sb.AppendLine(indent + "---");
            sb.AppendLine(indent + "Type: " + ex.GetType().FullName);
            sb.AppendLine(indent + "Source: " + ex.TargetSite == null || ex.TargetSite.DeclaringType == null
                ? ex.Source
                : string.Format("   {0}.{1}", ex.TargetSite.DeclaringType.FullName, ex.TargetSite.Name));
            sb.AppendLine(indent + "Message: " + ex.Message);
            sb.AppendLine(indent + "Stacktrace: ");

            foreach (var line in stackTrace)
            {
                sb.AppendLine(indent + line);
            }

            sb.Append(indent + "---");

            return sb.ToString();
        }

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (level < LogLevel) return;

            var sb = new StringBuilder();

            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss "));
            sb.Append(string.Format("{0,-9} ", level));
            sb.Append(GetCaller() + " ");
            sb.Append(message);

            // Use filestream to be able to explicitly specify FileShare.None
            using (var fileStream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.None))
            {
                using (var streamWriter = new StreamWriter(fileStream))
                {
                    streamWriter.WriteLine(sb.ToString());
                }
            }
        }

        private static string GetCaller()
        {
            var result = string.Empty;

            int i = 1;

            while (true)
            {
                // Walk up the stack trace ...
                var stackFrame = new StackFrame(i++);
                var methodBase = stackFrame.GetMethod();
                if (methodBase == null)
                    break;

                // Here we're at the end - nomally we should never get that far 
                Type declaringType = methodBase.DeclaringType;
                if (declaringType == null)
                    break;

                // Get class name and method of the current stack frame
                result = string.Format("{0}.{1}", declaringType.FullName, methodBase.Name);

                // Here, we're at the first method outside of Logger class. 
                // This is the method that called the log method. We're done here
                if (declaringType != typeof(Logger))
                    break;
            }

            return result;
        }
    }
}
