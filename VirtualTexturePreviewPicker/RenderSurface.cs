using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VirtualTexturePreviewPicker
{
    public sealed class RenderSurface : FrameworkElement
    {
        #region States
        public BitmapSource Bitmap;
        public int SplitLevel;
        public Pen GridPen;
        public Brush[] LevelBrushes;
        #endregion

        #region Properties
        // These properties are computed in OnRender and also needed by title update
        public double ImgPixelW;
        public double ImgPixelH;
        #endregion

        #region Events
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double winW = ActualWidth;
            double winH = ActualHeight;

            // Background
            dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, winW, winH));

            if (Bitmap == null) 
                return;

            double imgW = Bitmap.PixelWidth;
            double imgH = Bitmap.PixelHeight;
            ImgPixelW = imgW;
            ImgPixelH = imgH;

            if (imgW <= 0 || imgH <= 0) 
                return;

            double scaleX = winW / imgW;
            double scaleY = winH / imgH;
            double scale = Math.Min(scaleX, scaleY);

            double drawW = imgW * scale;
            double drawH = imgH * scale;

            double offX = (winW - drawW) * 0.5;
            double offY = (winH - drawH) * 0.5;

            Rect imgRect = new(offX, offY, drawW, drawH);

            // Draw image
            dc.DrawImage(Bitmap, imgRect);

            // Grid math
            int cols = 1 << (SplitLevel + 1); // 2^(N+1)
            int rows = 1 << SplitLevel;       // 2^N

            double cellW = drawW / cols;
            double cellH = drawH / rows;

            // Grid lines
            DrawGridLines(dc, offX, offY, cols, rows, cellW, cellH);

            // Labels
            DrawCellLabels(dc, offX, offY, cols, rows, cellW, cellH);
        }
        #endregion

        #region Routines
        private void DrawGridLines(
            DrawingContext dc,
            double offX,
            double offY,
            int cols,
            int rows,
            double cellW,
            double cellH)
        {
            if (GridPen == null) 
                return;

            // Verticals
            for (int c = 0; c <= cols; c++)
            {
                double x = offX + c * cellW;
                dc.DrawLine(GridPen,
                    new Point(x, offY),
                    new Point(x, offY + rows * cellH));
            }

            // Horizontals
            for (int r = 0; r <= rows; r++)
            {
                double y = offY + r * cellH;
                dc.DrawLine(GridPen,
                    new Point(offX, y),
                    new Point(offX + cols * cellW, y));
            }
        }
        private void DrawCellLabels(
            DrawingContext dc,
            double offX,
            double offY,
            int cols,
            int rows,
            double cellW,
            double cellH)
        {
            if (LevelBrushes == null || LevelBrushes.Length == 0) 
                return;

            // Performance clamp:
            // Only render text if total cells <= 4096 (64x64)
            // This triggers for splitLevel <= 5.
            long totalCells = (long)cols * (long)rows;
            bool drawLabels = totalCells <= 4096; // perf guard

            if (!drawLabels) 
                return;

            Brush textBrush = LevelBrushes[
                Math.Min(Math.Max(SplitLevel, 0), LevelBrushes.Length - 1)
            ];

            // Font size roughly 12 scaled with cell size but not tiny;
            // Choose size proportional to min(cellW, cellH)
            double baseSize = Math.Max(10.0, Math.Min(cellW, cellH) * 0.33);

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    string label = $"{x}, {y}";

                    FormattedText ft = CreateFormattedText(label, textBrush, baseSize);

                    double cellLeft = offX + x * cellW;
                    double cellTop = offY + y * cellH;

                    double textX = cellLeft + (cellW - ft.Width) * 0.5;
                    double textY = cellTop + (cellH - ft.Height) * 0.5;

                    dc.DrawText(ft, new Point(textX, textY));
                }
            }
        }
        #endregion

        #region Helpers
        private static FormattedText CreateFormattedText(string text, Brush brush, double fontSize)
        {
            return new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                fontSize,
                brush,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip
            );
        }
        #endregion
    }
}
