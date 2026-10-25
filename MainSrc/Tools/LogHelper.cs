using System;
using System.IO;

namespace Project.Launch.Tools
{
    /// <summary>
    /// 统一日志写入工具（中英双语）
    /// Unified log writer (Chinese + English)
    /// </summary>
    public static class LogHelper
    {
        private static readonly object _lock = new object();

        /// <summary>
        /// 写入一条日志
        /// Write a log entry
        /// </summary>
        /// <param name="filePath">日志文件路径 / Log file path</param>
        /// <param name="zh">中文描述 / Chinese description</param>
        /// <param name="en">英文描述 / English description</param>
        public static void Write(string filePath, string zh, string en)
        {
            try
            {
                lock (_lock)
                {
                    string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - [{zh}] {en}\n";
                    File.AppendAllText(filePath, line);
                }
            }
            catch { }
        }

        /// <summary>
        /// 清空并写入首行标题
        /// Clear and write a header line
        /// </summary>
        public static void Reset(string filePath, string zhHeader, string enHeader)
        {
            try
            {
                lock (_lock)
                {
                    string header = $"=== {zhHeader} / {enHeader} - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n";
                    File.WriteAllText(filePath, header);
                }
            }
            catch { }
        }
    }
}