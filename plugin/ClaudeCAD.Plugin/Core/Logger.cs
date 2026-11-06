using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// File logger: %LocalAppData%\ClaudeCAD\logs\claudecad-YYYYMMDD.log.
    /// Logging must never throw into AutoCAD, so every failure is swallowed.
    /// </summary>
    public static class Logger
    {
        private static readonly object Gate = new object();
        private static string _dir;

        public static string LogDirectory
        {
            get
            {
                if (_dir != null) return _dir;
                try
                {
                    var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    if (string.IsNullOrEmpty(root)) root = Path.GetTempPath();
                    _dir = Path.Combine(root, "ClaudeCAD", "logs");
                    Directory.CreateDirectory(_dir);
                }
                catch
                {
                    _dir = Path.GetTempPath();
                }
                return _dir;
            }
            set { _dir = value; }
        }

        public static string CurrentFile =>
            Path.Combine(LogDirectory, "claudecad-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");

        public static void Info(string msg) => Write("INFO ", msg);
        public static void Warn(string msg) => Write("WARN ", msg);
        public static void Error(string msg) => Write("ERROR", msg);

        public static void Error(string msg, Exception ex) =>
            Write("ERROR", msg + Environment.NewLine + (ex?.ToString() ?? "(no exception)"));

        /// <summary>IPC traffic; long payloads are truncated (spec §10).</summary>
        public static void Ipc(string direction, string line) => Write("IPC  ", direction + " " + Params.Truncate(line, 2000));

        private static void Write(string level, string msg)
        {
            try
            {
                var line = new StringBuilder()
                    .Append(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
                    .Append(' ').Append(level).Append(' ')
                    .Append('[').Append(System.Threading.Thread.CurrentThread.ManagedThreadId).Append("] ")
                    .Append(msg).Append(Environment.NewLine)
                    .ToString();
                lock (Gate)
                {
                    File.AppendAllText(CurrentFile, line, new UTF8Encoding(false));
                }
            }
            catch
            {
                // never let logging break the host
            }
        }
    }
}
