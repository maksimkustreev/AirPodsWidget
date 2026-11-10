using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace AirPodsWidget
{
    /// <summary>
    /// Следит за сопряжёнными Bluetooth-устройствами и сообщает о подключении/отключении AirPods.
    /// </summary>
    public sealed class AirPodsConnectionWatcher : IDisposable
    {
        private const string IsConnectedKey = "System.Devices.Aep.IsConnected";

        /// <summary>
        /// Какие устройства считать наушниками. Если вы переименовали AirPods
        /// (например, «Наушники Максима»), добавьте сюда своё название.
        /// </summary>
        public static readonly string[] NameKeywords = { "AirPods", "Beats" };

        private readonly object _lock = new();
        private readonly Dictionary<string, (string Name, bool Connected)> _devices = new();
        private DeviceWatcher? _watcher;
        private bool _enumerated;

        /// <summary>(имя устройства, подключено?, событие при первичном сканировании?)</summary>
        public event Action<string, bool, bool>? ConnectionChanged;

        public void Start()
        {
            try
            {
                _watcher = DeviceInformation.CreateWatcher(
                    BluetoothDevice.GetDeviceSelectorFromPairingState(true),
                    new[] { IsConnectedKey },
                    DeviceInformationKind.AssociationEndpoint);

                _watcher.Added += (_, di) => Handle(di.Id, di.Name, di.Properties);
                _watcher.Updated += (_, u) => Handle(u.Id, null, u.Properties);
                _watcher.Removed += (_, u) => HandleRemoved(u.Id);
                _watcher.EnumerationCompleted += (_, __) => _enumerated = true;
                _watcher.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeviceWatcher start failed: {ex}");
            }
        }

        private static bool IsHeadphones(string name)
        {
            foreach (var k in NameKeywords)
                if (name.Contains(k, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void Handle(string id, string? name, IReadOnlyDictionary<string, object> props)
        {
            bool? connected = props.TryGetValue(IsConnectedKey, out var v) && v is bool b ? b : null;

            string fireName = "";
            bool fireConnected = false;
            bool initial = false;

            lock (_lock)
            {
                bool known = _devices.TryGetValue(id, out var prev);
                string n = name ?? prev.Name ?? "";
                bool c = connected ?? prev.Connected;
                _devices[id] = (n, c);

                if (!IsHeadphones(n)) return;
                if (!known && !c) return;          // просто сопряжено, но не подключено
                if (known && prev.Connected == c) return; // состояние не изменилось

                fireName = n;
                fireConnected = c;
                initial = !_enumerated;
            }

            ConnectionChanged?.Invoke(fireName, fireConnected, initial);
        }

        private void HandleRemoved(string id)
        {
            string? name = null;
            lock (_lock)
            {
                if (_devices.TryGetValue(id, out var prev) && prev.Connected && IsHeadphones(prev.Name))
                    name = prev.Name;
                _devices.Remove(id);
            }
            if (name != null) ConnectionChanged?.Invoke(name, false, false);
        }

        public void Dispose()
        {
            try
            {
                if (_watcher != null &&
                    (_watcher.Status == DeviceWatcherStatus.Started ||
                     _watcher.Status == DeviceWatcherStatus.EnumerationCompleted))
                    _watcher.Stop();
            }
            catch { /* ignore */ }
            _watcher = null;
        }
    }
}
