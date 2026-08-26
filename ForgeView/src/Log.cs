using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace ForgeView
{
    internal static class Log
    {
        private static string _path;
        internal static void Init()
        {
            try
            {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var root = Path.GetFullPath(Path.Combine(dir, "..", ".."));
                _path = Path.Combine(root, "ForgeView.log");
                File.WriteAllText(_path, "=== ForgeView " + DateTime.Now + " ===" + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
        internal static void Info(string m)
        { try { if (_path != null) File.AppendAllText(_path, "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + m + Environment.NewLine); } catch { } }
        internal static void Error(string w, Exception e)
        { Info("ERROR in " + w + ": " + (e != null ? e.ToString() : "(null)")); }
    }
}
