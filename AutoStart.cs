using System;
using Microsoft.Win32;

namespace AirPodsWidget
{
    /// <summary>Автозапуск через HKCU\...\Run (права администратора не нужны).</summary>
    public static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "AirPodsWidget";

        private static string ExePath => Environment.ProcessPath ?? "";

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                    return key?.GetValue(ValueName) is string s &&
                           s.Contains(ExePath, StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            }
        }

        public static void Set(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (enabled) key.SetValue(ValueName, $"\"{ExePath}\"");
                else key.DeleteValue(ValueName, false);
            }
            catch { /* нет доступа к реестру — просто игнорируем */ }
        }
    }
}
