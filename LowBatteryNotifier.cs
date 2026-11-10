using System;
using System.Collections.Generic;

namespace AirPodsWidget
{
    public sealed class LowBatteryNotifier
    {
        private readonly TrayIcon _tray;
        private readonly AppSettings _settings;
        private readonly HashSet<string> _notified = new();
        public LowBatteryNotifier(TrayIcon tray, AppSettings settings) { _tray = tray; _settings = settings; }
        public void Reset() => _notified.Clear();
        public void Check(AirPodsStatus s)
        {
            if (!_settings.LowBatteryNotifications) return;
            CheckOne("left", "Левый наушник", s.Left, s.LeftCharging, _settings.NotifyLeft);
            CheckOne("right", "Правый наушник", s.Right, s.RightCharging, _settings.NotifyRight);
            CheckOne("case", "Кейс", s.Case, s.CaseCharging, _settings.NotifyCase);
        }
        private void CheckOne(string key, string label, int? level, bool charging, bool enabled)
        {
            if (!enabled || charging || !level.HasValue) return;
            if (level.Value > _settings.LowBatteryPercent) { _notified.Remove(key); return; }
            if (_notified.Add(key)) _tray.ShowNotification("Низкий заряд AirPods", $"{label}: {level.Value}%");
        }
    }
}
