using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace AirPodsWidget
{
    public partial class PopupWindow : Window
    {
        private readonly BatteryItem _left;
        private readonly BatteryItem _right;
        private readonly BatteryItem _case;
        private readonly BatteryItem _max;
        private readonly DispatcherTimer _hideTimer;
        private readonly DispatcherTimer _refreshTimer;

        private AirPodsStatus? _status;
        private string? _deviceName;
        private bool _connected;
        private bool _isShown;
        private bool _autoMode;
        private int? _lastCaseLevel;
        private bool _lastCaseCharging;

        public DateTime LastHiddenAt { get; private set; } = DateTime.MinValue;
        public bool IsShownNow => _isShown;
        public event Action? HiddenCompletely;

        public PopupWindow()
        {
            InitializeComponent();

            _left = new BatteryItem(ItemKind.LeftBud, "Левый");
            _right = new BatteryItem(ItemKind.RightBud, "Правый");
            _case = new BatteryItem(ItemKind.Case, "Кейс");
            _max = new BatteryItem(ItemKind.Headphones, "Наушники") { Visibility = Visibility.Collapsed };
            ItemsGrid.Children.Add(_left);
            ItemsGrid.Children.Add(_right);
            ItemsGrid.Children.Add(_case);
            ItemsGrid.Children.Add(_max);

            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
            _hideTimer.Tick += (_, __) =>
            {
                _hideTimer.Stop();
                if (IsMouseOver) _hideTimer.Start();
                else HideAnimated();
            };

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _refreshTimer.Tick += (_, __) => UpdateTexts();

            CloseButton.Click += (_, __) => HideAnimated();
            MouseEnter += (_, __) => _hideTimer.Stop();
            MouseLeave += (_, __) => { if (_isShown && _autoMode) _hideTimer.Start(); };
            Deactivated += (_, __) => { if (_isShown && !_autoMode) HideAnimated(); };
            KeyDown += (_, e) => { if (e.Key == Key.Escape) HideAnimated(); };

            ApplyTheme(Theme.Detect());
            Render();
        }

        // ───────────────────────── данные ─────────────────────────

        public void SetConnection(string? deviceName, bool connected)
        {
            if (!string.IsNullOrWhiteSpace(deviceName)) _deviceName = deviceName;
            _connected = connected;
            Render();
        }

        public void SetStatus(AirPodsStatus? status)
        {
            if (status?.Case is int caseLevel)
            {
                _lastCaseLevel = caseLevel;
                _lastCaseCharging = status.CaseCharging;
            }
            _status = status;
            Render();
        }

        private void Render()
        {
            var s = _status;

            if (s != null && s.IsMax)
            {
                _left.Visibility = _right.Visibility = _case.Visibility = Visibility.Collapsed;
                _max.Visibility = Visibility.Visible;
                _max.SetLevel(s.Left ?? s.Right, s.LeftCharging || s.RightCharging);
            }
            else
            {
                _max.Visibility = Visibility.Collapsed;
                _left.SetLevel(s?.Left, s?.LeftCharging ?? false);
                _right.SetLevel(s?.Right, s?.RightCharging ?? false);
                _case.SetLevel(s?.Case ?? _lastCaseLevel, s?.Case != null ? s.CaseCharging : _lastCaseCharging);

                bool hasData = s?.HasAnyData == true;
                _left.Visibility = !hasData || s!.Left.HasValue ? Visibility.Visible : Visibility.Collapsed;
                _right.Visibility = !hasData || s!.Right.HasValue ? Visibility.Visible : Visibility.Collapsed;
                // Как на iPhone: кейс показываем, только когда о нём есть данные
                _case.Visibility = !hasData || s!.Case.HasValue || _lastCaseLevel.HasValue ? Visibility.Visible : Visibility.Collapsed;
            }

            HintText.Visibility = s?.HasAnyData == true ? Visibility.Collapsed : Visibility.Visible;
            UpdateTexts();
        }

        private void UpdateTexts()
        {
            TitleText.Text = _deviceName ?? _status?.ModelName ?? "AirPods";

            string subtitle;
            if (!_connected)
                subtitle = "Не подключены";
            else if (_status == null || !_status.HasAnyData)
                subtitle = "Подключены · получение данных…";
            else if ((DateTime.Now - _status.Timestamp).TotalSeconds > 90)
                subtitle = $"Подключены · обновлено в {_status.Timestamp:HH:mm}";
            else
                subtitle = "Подключены";

            if (_status != null && _deviceName != null && _status.ModelName != "AirPods" &&
                !_deviceName.Contains(_status.ModelName, StringComparison.OrdinalIgnoreCase))
                subtitle = $"{_status.ModelName} · {subtitle}";

            SubtitleText.Text = subtitle;
        }

        // ───────────────────────── оформление ─────────────────────────

        public void ApplyTheme(Theme t)
        {
            Card.Background = t.Background;
            Card.BorderBrush = t.Border;
            ShadowLayer.Background = t.Background;
            if (ShadowLayer.Effect is System.Windows.Media.Effects.DropShadowEffect shadow)
            {
                shadow.Color = t.ShadowColor;
                shadow.Opacity = t.ShadowOpacity;
            }
            TitleText.Foreground = t.Primary;
            SubtitleText.Foreground = t.Secondary;
            HintText.Foreground = t.Secondary;
            CloseButton.Background = t.CloseBackground;
            CloseButton.Foreground = t.CloseForeground;
            _left.ApplyTheme(t);
            _right.ApplyTheme(t);
            _case.ApplyTheme(t);
            _max.ApplyTheme(t);
        }

        // ───────────────────────── показ / скрытие ─────────────────────────

        /// <param name="activate">true — открыто по клику на значок (закрывается при потере фокуса);
        /// false — всплыло само при подключении (закрывается по таймеру).</param>
        public void ShowAnimated(bool activate)
        {
            _autoMode = !activate;
            UpdateTexts();

            if (!_isShown)
            {
                _isShown = true;
                Opacity = 0;
                Show();
                UpdateLayout();
                PositionNearTray();

                var ease = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut };
                var slow = TimeSpan.FromMilliseconds(420);

                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
                RootTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                    new DoubleAnimation(28, 0, slow) { EasingFunction = ease });
                RootScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(0.94, 1, slow) { EasingFunction = ease });
                RootScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(0.94, 1, slow) { EasingFunction = ease });

                _refreshTimer.Start();
            }

            if (activate)
            {
                _hideTimer.Stop();
                Activate();
            }
            else
            {
                _hideTimer.Stop();
                _hideTimer.Start();
            }
        }

        public void HideAnimated()
        {
            if (!_isShown) return;
            _isShown = false;
            _hideTimer.Stop();
            _refreshTimer.Stop();
            LastHiddenAt = DateTime.Now;

            var dur = TimeSpan.FromMilliseconds(180);
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            var fade = new DoubleAnimation(0, dur) { EasingFunction = ease };
            fade.Completed += (_, __) =>
            {
                if (_isShown) return; // успели открыть снова
                Hide();
                HiddenCompletely?.Invoke();
            };
            RootTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                new DoubleAnimation(14, dur) { EasingFunction = ease });
            BeginAnimation(OpacityProperty, fade);
        }

        private void PositionNearTray()
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Right - ActualWidth;
            Top = wa.Bottom - ActualHeight;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Alt+F4 только прячет окно; закрытие — через меню «Выход»
            if (!App.IsExiting)
            {
                e.Cancel = true;
                HideAnimated();
            }
            base.OnClosing(e);
        }
    }
}
