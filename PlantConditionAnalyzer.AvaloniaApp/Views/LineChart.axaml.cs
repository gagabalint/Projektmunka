using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using PlantConditionAnalyzer.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace PlantConditionAnalyzer.AvaloniaApp.Views;

public partial class LineChart : UserControl
{
    public LineChart()
    {
        InitializeComponent();
    }
    private List<Snapshot> snapshots = new();
    private Snapshot? selected;

    public void SetData(List<Snapshot> snapshots, Snapshot? selected)
    {
        this.snapshots = snapshots;
        this.selected = selected;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (snapshots == null || snapshots.Count < 2) return;

        double w = Bounds.Width;
        double h = Bounds.Height;
        double padL = 50, padR = 20, padT = 20, padB = 30;
        double chartW = w - padL - padR;
        double chartH = h - padT - padB;

        // Skála
        double minY = snapshots.Min(s => s.ViMean) - 0.01;
        double maxY = snapshots.Max(s => s.ViMean) + 0.01;
        double minX = snapshots.Min(s => s.Timestamp.Ticks);
        double maxX = snapshots.Max(s => s.Timestamp.Ticks);
        if (maxX == minX) maxX = minX + 1;
        if (maxY == minY) maxY = minY + 0.01;

        double ToX(Snapshot s) => padL + (s.Timestamp.Ticks - minX) / (double)(maxX - minX) * chartW;
        double ToY(Snapshot s) => padT + (1 - (s.ViMean - minY) / (maxY - minY)) * chartH;

        // Tengely
        var axisPen = new Pen(Brushes.Gray, 1);
        context.DrawLine(axisPen, new Point(padL, padT), new Point(padL, padT + chartH));
        context.DrawLine(axisPen, new Point(padL, padT + chartH), new Point(padL + chartW, padT + chartH));

        // Y tengelyen min/max értékek
        var textBrush = Brushes.Gray;
        var ft = new FormattedText($"{maxY:F3}", System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, Typeface.Default, 10, textBrush);
        context.DrawText(ft, new Point(2, padT - 6));
        var fb = new FormattedText($"{minY:F3}", System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, Typeface.Default, 10, textBrush);
        context.DrawText(fb, new Point(2, padT + chartH - 6));

        // Vonal
        var linePen = new Pen(new SolidColorBrush(Color.FromRgb(30, 160, 100)), 2);
        for (int i = 0; i < snapshots.Count - 1; i++)
        {
            context.DrawLine(linePen,
                new Point(ToX(snapshots[i]), ToY(snapshots[i])),
                new Point(ToX(snapshots[i + 1]), ToY(snapshots[i + 1])));
        }

        // Pontok
        var dotBrush = new SolidColorBrush(Color.FromRgb(30, 160, 100));
        foreach (var s in snapshots)
        {
            context.DrawEllipse(dotBrush, null, new Point(ToX(s), ToY(s)), 4, 4);

            // Dátum label
            var tl = new FormattedText(s.Timestamp.ToString("MM.dd"),
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 9, textBrush);
            context.DrawText(tl, new Point(ToX(s) - 10, padT + chartH + 5));
        }

        // Kiválasztott pont kiemelése
        if (selected != null && snapshots.Contains(selected))
        {
            context.DrawEllipse(
                new SolidColorBrush(Color.FromRgb(255, 165, 0)),
                null,
                new Point(ToX(selected), ToY(selected)), 7, 7);
        }
    }
}