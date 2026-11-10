using System;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace AirPodsWidget
{
    public partial class App : System.Windows.Application
    {
        public static bool IsExiting { get; private set; }

        private Mutex? _mutex;
        private PopupWindow? _popup;
        private TrayIcon? _tray;
        private AirPodsBatteryMonitor? _monitor;
        private AirPodsConnectionWatcher? _connection;

        private AirPodsStatus? _status;
        private string? _deviceName;
        private bool _connected;
        private DateTime _lastAutoPopup = DateTime.MinValue;
        private AppSettings _settings = null!;
        private LowBatteryNotifier? _notifier;
        private SettingsWindow? _settingsWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, "AirPodsWidget.SingleInstance.5B1E", out bool isFirst);
            if (!isFirst)
            {
                IsExiting = true;
                Shutdown();
                return;
            }

            base.OnStartup(e);
            _settings = AppSettings.Load();

            _popup = new PopupWindow();
            _popup.HiddenCompletely += UpdateScanning;

            _tray = new TrayIcon();
            _tray.ShowRequested += TogglePopup;
            _tray.ExitRequested += () => Shutdown();
            _tray.SettingsRequested += ShowSettings;
            _notifier = new LowBatteryNotifier(_tray, _settings);

            _monitor = new AirPodsBatteryMonitor();
            _monitor.StatusUpdated += s => Dispatcher.InvokeAsync(() => OnStatus(s));

            _connection = new AirPodsConnectionWatcher();
            _connection.ConnectionChanged += (name, connected, initial) =>
                Dispatcher.InvokeAsync(() => OnConnectionChanged(name, connected, initial));
            _connection.Start();

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            UpdateTooltip();
        }

        private void OnConnectionChanged(string name, bool connected, bool initial)
        {
            _deviceName = name;
            _connected = connected;

            if (connected && _status != null && (DateTime.Now - _status.Timestamp).TotalMinutes > 2)
            {
                _status = null; // старые данные не показываем
                _popup!.SetStatus(null);
            }

            _popup!.SetConnection(name, connected);
            _tray!.SetActive(connected);
            if (!connected) _notifier?.Reset();
            UpdateTrayPercent();
            UpdateTooltip();
            UpdateScanning();

            if (connected)
            {
                // Windows иногда «моргает» подключением — не всплываем слишком часто
                if (!initial && (DateTime.Now - _lastAutoPopup).TotalSeconds > 15)
                {
                    _lastAutoPopup = DateTime.Now;
                    _popup.ShowAnimated(activate: false);
                }
            }
            else
            {
                _popup.HideAnimated();
            }
        }

        private void OnStatus(AirPodsStatus s)
        {
            // Если рядом несколько пар AirPods — держимся за ту, у которой сигнал сильнее
            if (_status != null && s.ModelId != _status.ModelId &&
                (s.Timestamp - _status.Timestamp).TotalSeconds < 10 &&
                s.Rssi < _status.Rssi - 6)
                return;

            _status = s;
            _popup!.SetStatus(s);
            _notifier?.Check(s);
            UpdateTrayPercent();
            UpdateTooltip();
        }

        private void TogglePopup()
        {
            if (_popup == null) return;

            if (_popup.IsShownNow)
            {
                _popup.HideAnimated();
                return;
            }

            // Клик по значку сначала снимает фокус с окна (и прячет его) — не открываем заново
            if ((DateTime.Now - _popup.LastHiddenAt).TotalMilliseconds < 350) return;

            _monitor?.Start();
            _popup.ShowAnimated(activate: true);
        }

        /// <summary>BLE-сканирование работает, только пока наушники подключены или открыт виджет.</summary>
        private void UpdateScanning()
        {
            if (_monitor == null) return;
            if (_connected || (_popup?.IsShownNow ?? false)) _monitor.Start();
            else _monitor.Stop();
        }

        private void UpdateTooltip()
        {
            if (_tray == null) return;

            string name = _deviceName ?? "AirPods";
            if (!_connected)
            {
                _tray.SetTooltip($"{name}: не подключены");
                return;
            }

            var s = _status;
            if (s == null || !s.HasAnyData)
            {
                _tray.SetTooltip($"{name}: подключены");
                return;
            }

            string Fmt(int? v) => v.HasValue ? $"{v}%" : "—";
            string text = s.IsMax
                ? $"{name}\n{Fmt(s.Left ?? s.Right)}"
                : $"{name}\nЛ {Fmt(s.Left)} · П {Fmt(s.Right)} · Кейс {Fmt(s.Case)}";
            _tray.SetTooltip(text);
        }

        private void UpdateTrayPercent()
        {
            if (_tray == null) return;
            int? p = null;
            if (_connected && _status != null)
            {
                if (_status.IsMax) p = _status.Left ?? _status.Right;
                else
                {
                    var vals = new[] { _status.Left, _status.Right };
                    foreach (var v in vals) if (v.HasValue && (!p.HasValue || v.Value < p.Value)) p = v;
                }
            }
            _tray.SetBatteryPercent(p, _settings.ShowTrayPercent);
        }
        private void ShowSettings()
        {
            if (_settingsWindow?.IsVisible == true) { _settingsWindow.Activate(); return; }
            _settingsWindow = new SettingsWindow(_settings);
            _settingsWindow.SettingsChanged += () => { UpdateTrayPercent(); _notifier?.Reset(); };
            _settingsWindow.Closed += (_, __) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }
        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General) return;
            Dispatcher.InvokeAsync(() =>
            {
                _popup?.ApplyTheme(Theme.Detect());
                _tray?.RefreshIcon();
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            IsExiting = true;
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _monitor?.Dispose();
            _connection?.Dispose();
            _tray?.Dispose();
            _popup?.Close();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
