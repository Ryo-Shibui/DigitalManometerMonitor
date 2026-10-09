using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace DigitalManometerMonitor;

internal sealed class PressurePlotControl : Control
{
    private const int MaxPoints = 12_000;
    private const int Divisions = 5;
    private readonly List<PlotPoint> _points = new();

    public PressurePlotControl()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        ForeColor = Color.FromArgb(40, 40, 40);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular);
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
    }

    public void ClearData()
    {
        _points.Clear();
        Invalidate();
    }

    public void AddPoint(double timeSeconds, double pressurePa)
    {
        if (!double.IsFinite(timeSeconds) || !double.IsFinite(pressurePa))
            return;

        _points.Add(new PlotPoint(timeSeconds, pressurePa));
        if (_points.Count > MaxPoints)
            Decimate();

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        using var axisPen = new Pen(Color.FromArgb(90, 90, 90), 1F);
        using var gridPen = new Pen(Color.FromArgb(225, 225, 225), 1F);
        using var tracePen = new Pen(Color.FromArgb(0, 105, 180), 1.5F);
        using var labelBrush = new SolidBrush(ForeColor);
        using var tickFont = new Font("Segoe UI", 10F, FontStyle.Regular);
        using var axisFont = new Font("Segoe UI", 12F, FontStyle.Regular);

        double xMax = _points.Count == 0 ? 1.0 : Math.Max(1.0, _points[^1].TimeSeconds);
        (double yMin, double yMax) = GetYRange();

        string[] xLabels = new string[Divisions + 1];
        string[] yLabels = new string[Divisions + 1];
        float maxXLabelWidth = 0F;
        float maxXLabelHeight = 0F;
        float maxYLabelWidth = 0F;
        float maxYLabelHeight = 0F;

        for (int i = 0; i <= Divisions; i++)
        {
            xLabels[i] = FormatAxis(xMax * i / Divisions);
            yLabels[i] = FormatAxis(yMax - (yMax - yMin) * i / Divisions);

            SizeF xSize = g.MeasureString(xLabels[i], tickFont);
            SizeF ySize = g.MeasureString(yLabels[i], tickFont);
            maxXLabelWidth = Math.Max(maxXLabelWidth, xSize.Width);
            maxXLabelHeight = Math.Max(maxXLabelHeight, xSize.Height);
            maxYLabelWidth = Math.Max(maxYLabelWidth, ySize.Width);
            maxYLabelHeight = Math.Max(maxYLabelHeight, ySize.Height);
        }

        const string xTitle = "Measurement Time (s)";
        const string yTitle = "Pressure (Pa)";
        SizeF xTitleSize = g.MeasureString(xTitle, axisFont);
        SizeF yTitleSize = g.MeasureString(yTitle, axisFont);

        const float outerPadding = 10F;
        const float labelGap = 8F;
        const float titleGap = 10F;

        float leftMargin = outerPadding + yTitleSize.Height + titleGap + maxYLabelWidth + labelGap;
        float rightMargin = Math.Max(18F, maxXLabelWidth / 2F + outerPadding);
        float topMargin = Math.Max(16F, maxYLabelHeight / 2F + outerPadding);
        float bottomMargin = outerPadding + xTitleSize.Height + titleGap + maxXLabelHeight + labelGap;

        float plotWidth = ClientSize.Width - leftMargin - rightMargin;
        float plotHeight = ClientSize.Height - topMargin - bottomMargin;
        if (plotWidth < 40F || plotHeight < 40F)
            return;

        var plot = new RectangleF(leftMargin, topMargin, plotWidth, plotHeight);

        for (int i = 0; i <= Divisions; i++)
        {
            float x = plot.Left + plot.Width * i / Divisions;
            float y = plot.Top + plot.Height * i / Divisions;

            g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
            g.DrawLine(gridPen, plot.Left, y, plot.Right, y);

            SizeF xSize = g.MeasureString(xLabels[i], tickFont);
            g.DrawString(
                xLabels[i],
                tickFont,
                labelBrush,
                x - xSize.Width / 2F,
                plot.Bottom + labelGap / 2F);

            SizeF ySize = g.MeasureString(yLabels[i], tickFont);
            g.DrawString(
                yLabels[i],
                tickFont,
                labelBrush,
                plot.Left - labelGap - ySize.Width,
                y - ySize.Height / 2F);
        }

        g.DrawRectangle(axisPen, plot.X, plot.Y, plot.Width, plot.Height);

        if (_points.Count >= 2)
        {
            var screenPoints = new PointF[_points.Count];
            double ySpan = yMax - yMin;
            for (int i = 0; i < _points.Count; i++)
            {
                PlotPoint p = _points[i];
                float x = plot.Left + (float)(p.TimeSeconds / xMax * plot.Width);
                float y = plot.Bottom - (float)((p.PressurePa - yMin) / ySpan * plot.Height);
                screenPoints[i] = new PointF(x, y);
            }

            g.DrawLines(tracePen, screenPoints);
        }
        else if (_points.Count == 0)
        {
            const string message = "Press Start Measurement to begin plotting.";
            SizeF size = g.MeasureString(message, tickFont);
            g.DrawString(
                message,
                tickFont,
                labelBrush,
                plot.Left + (plot.Width - size.Width) / 2F,
                plot.Top + (plot.Height - size.Height) / 2F);
        }

        float xTitleY = plot.Bottom + labelGap / 2F + maxXLabelHeight + titleGap / 2F;
        g.DrawString(
            xTitle,
            axisFont,
            labelBrush,
            plot.Left + (plot.Width - xTitleSize.Width) / 2F,
            xTitleY);

        GraphicsState state = g.Save();
        float yTitleCenterX = outerPadding + yTitleSize.Height / 2F;
        g.TranslateTransform(yTitleCenterX, plot.Top + plot.Height / 2F);
        g.RotateTransform(-90F);
        g.DrawString(
            yTitle,
            axisFont,
            labelBrush,
            -yTitleSize.Width / 2F,
            -yTitleSize.Height / 2F);
        g.Restore(state);
    }

    private (double Min, double Max) GetYRange()
    {
        if (_points.Count == 0)
            return (0.0, 100_000.0);

        double min = double.PositiveInfinity;
        double max = double.NegativeInfinity;
        foreach (PlotPoint p in _points)
        {
            min = Math.Min(min, p.PressurePa);
            max = Math.Max(max, p.PressurePa);
        }

        if (Math.Abs(max - min) < 1e-12)
        {
            double pad = Math.Max(1.0, Math.Abs(max) * 0.05);
            return (min - pad, max + pad);
        }

        double range = max - min;
        double padding = range * 0.08;
        return (min - padding, max + padding);
    }

    private void Decimate()
    {
        var reduced = new List<PlotPoint>((_points.Count + 1) / 2);
        for (int i = 0; i < _points.Count; i += 2)
        {
            if (i + 1 >= _points.Count)
            {
                reduced.Add(_points[i]);
                continue;
            }

            PlotPoint a = _points[i];
            PlotPoint b = _points[i + 1];
            reduced.Add(new PlotPoint(
                (a.TimeSeconds + b.TimeSeconds) * 0.5,
                (a.PressurePa + b.PressurePa) * 0.5));
        }

        _points.Clear();
        _points.AddRange(reduced);
    }

    private static string FormatAxis(double value)
    {
        double abs = Math.Abs(value);
        if ((abs >= 1_000_000.0) || (abs > 0.0 && abs < 0.01))
            return value.ToString("0.##E+0", CultureInfo.InvariantCulture);
        if (abs >= 1000.0)
            return value.ToString("0", CultureInfo.InvariantCulture);
        if (abs >= 10.0)
            return value.ToString("0.0", CultureInfo.InvariantCulture);
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private readonly record struct PlotPoint(double TimeSeconds, double PressurePa);
}
