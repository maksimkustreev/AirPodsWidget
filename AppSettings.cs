using System;
using Microsoft.Win32;

namespace AirPodsWidget
{
    public sealed class AppSettings
    {
        private const string KeyPath = @"Software\AirPodsWidget";
        public bool ShowTrayPercent { get; set; } = true;
        public bool LowBatteryNotifications { get; set; } = true;
        public int LowBatteryPercent { get; set; } = 20;
        public bool NotifyLeft { get; set; } = true;
        public bool NotifyRight { get; set; } = true;
        public bool NotifyCase { get; set; } = true;

        public static AppSettings Load()
        {
            var s = new AppSettings();
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
                if (key == null) return s;
                s.ShowTrayPercent = ReadBool(key, nameof(ShowTrayPercent), true);
                s.LowBatteryNotifications = ReadBool(key, nameof(LowBatteryNotifications), true);
                s.LowBatteryPercent = Math.Clamp(ReadInt(key, nameof(LowBatteryPercent), 20), 10, 100);
                s.NotifyLeft = ReadBool(key, nameof(NotifyLeft), true);
                s.NotifyRight = ReadBool(key, nameof(NotifyRight), true);
                s.NotifyCase = ReadBool(key, nameof(NotifyCase), true);
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
                key.SetValue(nameof(ShowTrayPercent), ShowTrayPercent ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(nameof(LowBatteryNotifications), LowBatteryNotifications ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(nameof(LowBatteryPercent), Math.Clamp(LowBatteryPercent, 10, 100), RegistryValueKind.DWord);
                key.SetValue(nameof(NotifyLeft), NotifyLeft ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(nameof(NotifyRight), NotifyRight ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(nameof(NotifyCase), NotifyCase ? 1 : 0, RegistryValueKind.DWord);
            }
            catch { }
        }

        private static bool ReadBool(RegistryKey k, string name, bool fallback) =>
            k.GetValue(name) is int i ? i != 0 : fallback;
        private static int ReadInt(RegistryKey k, string name, int fallback) =>
            k.GetValue(name) is int i ? i : fallback;
    }
}
