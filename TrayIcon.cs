using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using WinForms = System.Windows.Forms;

namespace AirPodsWidget
{
    /// <summary>Значок в системном трее с контекстным меню.</summary>
    public sealed class TrayIcon : IDisposable
    {
        private readonly WinForms.NotifyIcon _notifyIcon;
        private readonly WinForms.ToolStripMenuItem _autoStartItem;
        private bool _active;
        private bool _showPercent;
        private int? _percent;

        public event Action? ShowRequested;
        public event Action? ExitRequested;
        public event Action? SettingsRequested;

        public TrayIcon()
        {
            _notifyIcon = new WinForms.NotifyIcon
            {
                Text = "AirPods Widget",
                Icon = CreateIcon(Theme.TaskbarIsLight(), false, null),
                Visible = true
            };

            var menu = new WinForms.ContextMenuStrip
            {
                ShowImageMargin = false,
                Font = new Font("Segoe UI", 9.5f)
            };
            menu.Items.Add("Показать заряд", null, (_, __) => ShowRequested?.Invoke());

            _autoStartItem = new WinForms.ToolStripMenuItem("Запускать вместе с Windows")
            {
                CheckOnClick = true,
                Checked = AutoStart.IsEnabled
            };
            _autoStartItem.CheckedChanged += (_, __) => AutoStart.Set(_autoStartItem.Checked);
            menu.Items.Add(_autoStartItem);
            menu.Items.Add("Настройки…", null, (_, __) => SettingsRequested?.Invoke());

            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add("Выход", null, (_, __) => ExitRequested?.Invoke());
            menu.ShowCheckMargin = true;

            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.MouseClick += (_, e) =>
            {
                if (e.Button == WinForms.MouseButtons.Left) ShowRequested?.Invoke();
            };
        }

        public void SetTooltip(string text)
        {
            _notifyIcon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
        }

        public void SetActive(bool active)
        {
            _active = active;
            RefreshIcon();
        }

        public void SetBatteryPercent(int? percent, bool show)
        {
            _percent = percent;
            _showPercent = show;
            RefreshIcon();
        }
        public void ShowNotification(string title, string text)
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = text;
            _notifyIcon.BalloonTipIcon = WinForms.ToolTipIcon.Warning;
            _notifyIcon.ShowBalloonTip(5000);
        }
        public void RefreshIcon()
        {
            var old = _notifyIcon.Icon;
            _notifyIcon.Icon = CreateIcon(Theme.TaskbarIsLight(), _active, _showPercent ? _percent : null);
            old?.Dispose();
        }

        /// <summary>Рисует значок наушников глифом системного шрифта Segoe Fluent / MDL2.</summary>
        private static Icon CreateIcon(bool lightTaskbar, bool active, int? percent)
        {
            const int size = 32;
            using var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                int alpha = active ? 255 : 140;
                var color = lightTaskbar ? Color.FromArgb(alpha, 0, 0, 0) : Color.FromArgb(alpha, 255, 255, 255);

                using var font = CreateIconFont(24);
                using var brush = new SolidBrush(color);
                using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("\uE7F6", font, brush, new RectangleF(0, 1, size, size), fmt); // Headphone
                if (percent.HasValue)
                {
                    using var bg = new SolidBrush(lightTaskbar ? Color.White : Color.FromArgb(255, 28,28,30));
                    g.FillEllipse(bg, 12, 17, 20, 15);
                    using var pf = new Font("Segoe UI", percent.Value >= 100 ? 7.2f : 8.5f, FontStyle.Bold, GraphicsUnit.Pixel);
                    using var pb = new SolidBrush(color);
                    g.DrawString(percent.Value.ToString(), pf, pb, new RectangleF(12,17,20,15), fmt);
                }

                if (active)
                {
                    // маленькая зелёная точка — наушники подключены
                    using var dot = new SolidBrush(Color.FromArgb(255, 52, 199, 89));
                    g.FillEllipse(dot, size - 11, size - 11, 10, 10);
                }
            }

            IntPtr hIcon = bmp.GetHicon();
            try { return (Icon)Icon.FromHandle(hIcon).Clone(); }
            finally { DestroyIcon(hIcon); }
        }

        private static Font CreateIconFont(float px)
        {
            foreach (var name in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
            {
                var f = new Font(name, px, FontStyle.Regular, GraphicsUnit.Pixel);
                if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) return f;
                f.Dispose();
            }
            return new Font("Segoe UI Symbol", px, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.Dispose();
        }
    }
}
