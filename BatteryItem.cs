using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace AirPodsWidget
{
    public enum ItemKind { LeftBud, RightBud, Case, Headphones }

    /// <summary>Кольцевой индикатор заряда в стиле виджета «Элементы питания» iOS.</summary>
    public sealed class BatteryItem : System.Windows.Controls.UserControl
    {
        private const double RingSize = 68;
        private const double RingStroke = 5.5;

        private readonly Ellipse _track;
        private readonly Path _arc;
        private readonly Path _icon;
        private readonly Path _bolt;
        private readonly TextBlock _percent;
        private readonly TextBlock _label;

        private Theme _theme = Theme.Light;
        private int? _level = -1; // -1 — ещё не задано
        private bool _charging;

        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
            nameof(Progress), typeof(double), typeof(BatteryItem),
            new PropertyMetadata(0.0, (d, _) => ((BatteryItem)d).UpdateArc()));

        public double Progress
        {
            get => (double)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public BatteryItem(ItemKind kind, string label)
        {
            var ring = new Grid { Width = RingSize, Height = RingSize, HorizontalAlignment = HorizontalAlignment.Center };

            _track = new Ellipse { StrokeThickness = RingStroke };
            _arc = new Path
            {
                StrokeThickness = RingStroke,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            _icon = CreateIcon(kind);

            ring.Children.Add(_track);
            ring.Children.Add(_arc);
            ring.Children.Add(_icon);

            _bolt = new Path
            {
                Data = Geometry.Parse("M 6.5,0 L 0,8.5 L 4.2,8.5 L 3.2,14 L 9.5,5.2 L 5.4,5.2 Z"),
                Width = 7,
                Height = 11,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 1, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };

            _percent = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold, Text = "—" };

            var percentRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };
            percentRow.Children.Add(_bolt);
            percentRow.Children.Add(_percent);

            _label = new TextBlock
            {
                Text = label,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };

            var root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            root.Children.Add(ring);
            root.Children.Add(percentRow);
            root.Children.Add(_label);
            Content = root;

            ApplyTheme(_theme);
        }

        public string Label
        {
            get => _label.Text;
            set => _label.Text = value;
        }

        public void SetLevel(int? level, bool charging)
        {
            bool changed = level != _level;
            _level = level;
            _charging = charging && level.HasValue;

            _percent.Text = level.HasValue ? $"{level}%" : "—";
            _bolt.Visibility = _charging ? Visibility.Visible : Visibility.Collapsed;

            if (changed)
            {
                var anim = new DoubleAnimation((level ?? 0) / 100.0, TimeSpan.FromMilliseconds(750))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                BeginAnimation(ProgressProperty, anim);
            }
            UpdateColors();
        }

        public void ApplyTheme(Theme theme)
        {
            _theme = theme;
            _track.Stroke = theme.Track;
            _icon.Fill = theme.Device;
            _icon.Stroke = theme.DeviceStroke;
            _percent.Foreground = theme.Primary;
            _label.Foreground = theme.Secondary;
            UpdateColors();
        }

        private void UpdateColors()
        {
            bool low = _level is int l && l >= 0 && l <= 20 && !_charging;
            _arc.Stroke = low ? _theme.Red : _theme.Green;
            _bolt.Fill = _theme.Green;
        }

        private void UpdateArc()
        {
            double p = Math.Clamp(Progress, 0, 1);
            double c = RingSize / 2;
            double r = (RingSize - RingStroke) / 2;

            if (p <= 0.001) { _arc.Data = null; return; }
            if (p >= 0.999) { _arc.Data = new EllipseGeometry(new Point(c, c), r, r); return; }

            double a = p * 2 * Math.PI;
            var figure = new PathFigure { StartPoint = new Point(c, c - r), IsClosed = false, IsFilled = false };
            figure.Segments.Add(new ArcSegment(
                new Point(c + r * Math.Sin(a), c - r * Math.Cos(a)),
                new Size(r, r), 0, p > 0.5, SweepDirection.Clockwise, true));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            _arc.Data = geometry;
        }

        /// <summary>Минималистичные силуэты наушника / кейса / AirPods Max.</summary>
        private static Path CreateIcon(ItemKind kind)
        {
            Geometry g;
            double w, h;

            switch (kind)
            {
                case ItemKind.Case:
                {
                    var body = new RectangleGeometry(new Rect(0, 0, 30, 23), 7, 7);
                    var seam = new RectangleGeometry(new Rect(-1, 7.6, 32, 1.1));
                    var hinge = new EllipseGeometry(new Point(15, 15.5), 1.3, 1.3);
                    g = new CombinedGeometry(GeometryCombineMode.Exclude,
                        new CombinedGeometry(GeometryCombineMode.Exclude, body, seam), hinge);
                    w = 30; h = 23;
                    break;
                }
                case ItemKind.Headphones:
                {
                    var band = new CombinedGeometry(GeometryCombineMode.Exclude,
                        new EllipseGeometry(new Point(15, 14), 13, 13),
                        new EllipseGeometry(new Point(15, 14), 10.5, 10.5));
                    var cutBottom = new RectangleGeometry(new Rect(0, 14, 30, 20));
                    var arc = new CombinedGeometry(GeometryCombineMode.Exclude, band, cutBottom);
                    var cups = new GeometryGroup { FillRule = FillRule.Nonzero };
                    cups.Children.Add(new RectangleGeometry(new Rect(0, 13, 8, 13), 3, 3));
                    cups.Children.Add(new RectangleGeometry(new Rect(22, 13, 8, 13), 3, 3));
                    g = new CombinedGeometry(GeometryCombineMode.Union, arc, cups);
                    w = 28; h = 25;
                    break;
                }
                default:
                {
                    var bud = new GeometryGroup { FillRule = FillRule.Nonzero };
                    bud.Children.Add(new EllipseGeometry(new Point(8, 7), 7.2, 6.6));
                    bud.Children.Add(new RectangleGeometry(new Rect(9.6, 6, 5.2, 20), 2.6, 2.6));
                    if (kind == ItemKind.LeftBud)
                        bud.Transform = new ScaleTransform(-1, 1);
                    g = bud;
                    w = 19; h = 28;
                    break;
                }
            }

            g.Freeze();
            return new Path
            {
                Data = g,
                Width = w,
                Height = h,
                Stretch = Stretch.Uniform,
                StrokeThickness = 0.8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
    }
}
