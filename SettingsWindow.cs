using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;

namespace AirPodsWidget
{
    public sealed class SettingsWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly CheckBox _trayPercent, _notifications, _left, _right, _case;
        private readonly ComboBox _threshold;
        public event Action? SettingsChanged;

        public SettingsWindow(AppSettings settings)
        {
            _settings = settings;
            var dark = Theme.Detect().IsDark;
            var bg = B(dark ? "#FF1C1C1E" : "#FFF7F7F9");
            var card = B(dark ? "#FF2C2C2E" : "#FFFFFFFF");
            var fg = B(dark ? "#FFF5F5F7" : "#FF171719");
            var secondary = B(dark ? "#FFAEAEB2" : "#FF6E6E73");
            var border = B(dark ? "#FF48484A" : "#FFD7D7DC");
            var accent = B("#FF34C759");

            Title = "Настройки AirPods Widget";
            Width = 430; Height = 510;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = bg;
            Foreground = fg;
            FontFamily = new FontFamily("Segoe UI");

            var root = new StackPanel { Margin = new Thickness(28) };
            root.Children.Add(new TextBlock { Text = "Настройки", FontSize = 26, FontWeight = FontWeights.SemiBold, Foreground = fg, Margin = new Thickness(0,0,0,4) });
            root.Children.Add(new TextBlock { Text = "AirPods Widget", FontSize = 13, Foreground = secondary, Margin = new Thickness(0,0,0,22) });

            _trayPercent = Box("Показывать процент заряда на значке в трее", settings.ShowTrayPercent, fg);
            root.Children.Add(Card(_trayPercent, card, border));

            root.Children.Add(Section("Уведомления о низком заряде", fg));
            var notifyPanel = new StackPanel();
            _notifications = Box("Включить уведомления", settings.LowBatteryNotifications, fg);
            notifyPanel.Children.Add(_notifications);

            var row = new Grid { Margin = new Thickness(0,12,0,10) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = "Порог заряда", Foreground = fg, VerticalAlignment = VerticalAlignment.Center });
            _threshold = new ComboBox { Width = 90, Height = 30, Foreground = Brushes.Black, Background = Brushes.White };
            foreach (int p in new[] { 10,20,30,40,50,60,70,80,90,100 }) _threshold.Items.Add($"{p}%");
            _threshold.SelectedItem = $"{settings.LowBatteryPercent}%";
            Grid.SetColumn(_threshold, 1); row.Children.Add(_threshold); notifyPanel.Children.Add(row);
            notifyPanel.Children.Add(new TextBlock { Text = "Предупреждать для:", FontSize = 12, Foreground = secondary, Margin = new Thickness(0,5,0,5) });
            _left = Box("Левый наушник", settings.NotifyLeft, fg);
            _right = Box("Правый наушник", settings.NotifyRight, fg);
            _case = Box("Кейс", settings.NotifyCase, fg);
            notifyPanel.Children.Add(_left); notifyPanel.Children.Add(_right); notifyPanel.Children.Add(_case);
            root.Children.Add(Card(notifyPanel, card, border));

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,22,0,0) };
            var cancel = MakeButton("Отмена", card, fg, border);
            cancel.Margin = new Thickness(0,0,10,0);
            var save = MakeButton("Сохранить", accent, Brushes.Black, accent);
            save.IsDefault = true;
            cancel.Click += (_,__) => Close();
            save.Click += (_,__) => SaveAndClose();
            buttons.Children.Add(cancel); buttons.Children.Add(save); root.Children.Add(buttons);
            Content = root;
        }

        private static Border Card(UIElement child, Brush bg, Brush border) => new() { Child = child, Background = bg, BorderBrush = border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(16), Margin = new Thickness(0,6,0,0) };
        private static TextBlock Section(string text, Brush fg) => new() { Text = text, Foreground = fg, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,22,0,7) };
        private static CheckBox Box(string text, bool value, Brush fg) => new() { Content = text, IsChecked = value, Foreground = fg, Margin = new Thickness(0,7,0,7) };
        private static Button MakeButton(string text, Brush bg, Brush fg, Brush border) => new() { Content = text, MinWidth = 100, Padding = new Thickness(14,8,14,8), Background = bg, Foreground = fg, BorderBrush = border, BorderThickness = new Thickness(1) };
        private static Brush B(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }

        private void SaveAndClose()
        {
            _settings.ShowTrayPercent = _trayPercent.IsChecked == true;
            _settings.LowBatteryNotifications = _notifications.IsChecked == true;
            _settings.LowBatteryPercent = int.TryParse((_threshold.SelectedItem?.ToString() ?? "20%").TrimEnd('%'), out var p) ? Math.Clamp(p, 10, 100) : 20;
            _settings.NotifyLeft = _left.IsChecked == true;
            _settings.NotifyRight = _right.IsChecked == true;
            _settings.NotifyCase = _case.IsChecked == true;
            _settings.Save();
            SettingsChanged?.Invoke();
            Close();
        }
    }
}
