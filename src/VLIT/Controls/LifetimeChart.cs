using System.Collections;
using System.Windows;
using System.Windows.Media;

namespace VLIT.Controls;

public sealed class LifetimeChart : FrameworkElement
{
    public static readonly DependencyProperty FilesProperty = DependencyProperty.Register(
        nameof(Files),
        typeof(IEnumerable),
        typeof(LifetimeChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? Files
    {
        get => (IEnumerable?)GetValue(FilesProperty);
        set => SetValue(FilesProperty, value);
    }

    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        return new System.Windows.Size(Math.Max(240, availableSize.Width), 150);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(Palette.Brush("#0D141B"), null, bounds);

        var files = Files?.OfType<LogFileItem>()
            .Where(f => f.IsAvailable)
            .OrderBy(f => f.StartTimestamp)
            .ToList() ?? [];

        if (files.Count == 0)
        {
            DrawCentered(dc, "No discovered logs", bounds);
            return;
        }

        var min = files.Min(f => f.StartTimestamp.Ticks);
        var max = files.Max(f => f.LastActivityTimestamp.Ticks);
        if (max <= min)
        {
            max = min + TimeSpan.FromMinutes(1).Ticks;
        }

        var left = 12.0;
        var right = Math.Max(left + 1, ActualWidth - 12);
        var top = 18.0;
        var rowHeight = Math.Max(18, Math.Min(30, (ActualHeight - 30) / Math.Max(1, files.Count)));

        var axisPen = new Pen(Palette.Brush("#263542"), 1);
        dc.DrawLine(axisPen, new Point(left, 10), new Point(right, 10));
        dc.DrawText(MakeText(files.First().StartTimestamp.ToString("HH:mm"), 10, "#7A8A99"), new Point(left, 0));
        dc.DrawText(MakeText(files.Last().LastActivityTimestamp.ToString("HH:mm"), 10, "#7A8A99"), new Point(Math.Max(left, right - 36), 0));

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var y = top + (i * rowHeight) + rowHeight / 2;
            var x1 = Scale(file.StartTimestamp.Ticks, min, max, left, right);
            var x2 = Scale(file.LastActivityTimestamp.Ticks, min, max, left, right);
            if (x2 < x1 + 4)
            {
                x2 = x1 + 4;
            }

            var brush = Palette.Brush(file.Color);
            var pen = new Pen(brush, file.IncludeInTimeline ? 3 : 1.5)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            dc.DrawLine(pen, new Point(x1, y), new Point(x2, y));
            dc.DrawLine(pen, new Point(x1, y), new Point(x1, Math.Max(14, y - rowHeight * 0.45)));
            dc.DrawEllipse(brush, null, new Point(x1, y), 3, 3);
            dc.DrawEllipse(brush, null, new Point(x2, y), 3, 3);
        }
    }

    private static double Scale(long value, long min, long max, double left, double right)
    {
        return left + ((value - min) / (double)(max - min)) * (right - left);
    }

    private static void DrawCentered(DrawingContext dc, string text, Rect bounds)
    {
        var formatted = MakeText(text, 12, "#7A8A99");
        dc.DrawText(formatted, new Point(bounds.Left + (bounds.Width - formatted.Width) / 2, bounds.Top + (bounds.Height - formatted.Height) / 2));
    }

    private static FormattedText MakeText(string text, double size, string color)
    {
        return new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            size,
            Palette.Brush(color),
            VisualTreeHelper.GetDpi(System.Windows.Application.Current.MainWindow).PixelsPerDip);
    }
}
