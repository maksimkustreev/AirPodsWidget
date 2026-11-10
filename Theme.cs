using System;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Microsoft.Win32;

namespace AirPodsWidget
{
    /// <summary>Палитра в стиле iOS / macOS (светлая и тёмная).</summary>
    public sealed class Theme
    {
        public bool IsDark { get; private init; }
        public Brush Background { get; private init; } = null!;
        public Brush Border { get; private init; } = null!;
        public Brush Primary { get; private init; } = null!;
        public Brush Secondary { get; private init; } = null!;
        public Brush Track { get; private init; } = null!;
        public Brush Green { get; private init; } = null!;
        public Brush Red { get; private init; } = null!;
        public Brush Device { get; private init; } = null!;
        public Brush DeviceStroke { get; private init; } = null!;
        public Brush CloseBackground { get; private init; } = null!;
        public Brush CloseForeground { get; private init; } = null!;
        public Color ShadowColor { get; private init; }
        public double ShadowOpacity { get; private init; }

        public static readonly Theme Dark = new()
        {
            IsDark = true,
            Background = B("#F21C1C1E"),
            Border = B("#26FFFFFF"),
            Primary = B("#FFFFFFFF"),
            Secondary = B("#99EBEBF5"),
            Track = B("#FF3A3A3C"),
            Green = B("#FF30D158"),
            Red = B("#FFFF453A"),
            Device = B("#FFF2F2F7"),
            DeviceStroke = B("#00000000"),
            CloseBackground = B("#FF3A3A3C"),
            CloseForeground = B("#FFAEAEB2"),
            ShadowColor = Colors.Black,
            ShadowOpacity = 0.55
        };

        public static readonly Theme Light = new()
        {
            IsDark = false,
            Background = B("#F7FFFFFF"),
            Border = B("#14000000"),
            Primary = B("#FF000000"),
            Secondary = B("#FF8A8A8E"),
            Track = B("#FFE5E5EA"),
            Green = B("#FF34C759"),
            Red = B("#FFFF3B30"),
            Device = B("#FFFFFFFF"),
            DeviceStroke = B("#FFC7C7CC"),
            CloseBackground = B("#FFE9E9EB"),
            CloseForeground = B("#FF8A8A8E"),
            ShadowColor = Colors.Black,
            ShadowOpacity = 0.22
        };

        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public static Theme Detect() => ReadDword("AppsUseLightTheme", 1) == 0 ? Dark : Light;

        public static bool TaskbarIsLight() => ReadDword("SystemUsesLightTheme", 0) == 1;

        private static int ReadDword(string name, int fallback)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
                return key?.GetValue(name) is int i ? i : fallback;
            }
            catch { return fallback; }
        }

        private static Brush B(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }
}
