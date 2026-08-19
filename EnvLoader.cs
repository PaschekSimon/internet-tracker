using System;
using System.IO;

namespace RouterTaskbarMonitor
{
    internal static class EnvLoader
    {
        private static string _cachedEnvPath;

        public static string Get(string key)
        {
            string envPath = FindEnvFile();
            if (envPath == null) return null;

            foreach (string line in File.ReadAllLines(envPath))
            {
                line = line.Trim();
                if (line.Length == 0 || line.StartsWith("#") || !line.Contains("=")) continue;

                int idx = line.IndexOf('=');
                string k = line.Substring(0, idx).Trim();
                string v = line.Substring(idx + 1).Trim();
                if (k == key) return v;
            }

            return null;
        }

        private static string FindEnvFile()
        {
            if (_cachedEnvPath != null) return _cachedEnvPath;

            string[] starts = {
                AppDomain.CurrentDomain.BaseDirectory,
                Directory.GetCurrentDirectory()
            };

            foreach (string start in starts)
            {
                string dir = start;
                for (int i = 0; dir != null && i < 6; i++)
                {
                    string candidate = Path.Combine(dir, ".env");
                    if (File.Exists(candidate))
                    {
                        _cachedEnvPath = candidate;
                        return candidate;
                    }
                    dir = Directory.GetParent(dir)?.FullName;
                }
            }

            return null;
        }
    }
}
