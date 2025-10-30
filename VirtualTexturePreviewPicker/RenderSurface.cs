using SkiaSharp;
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
        public Pen[] GridPens;
        public Brush[] LevelBrushes;

        // Zoom state
        public double UserScale = 1.0;          // 1.0 = no extra zoom
        public Vector UserOffset = new(0, 0); // pan in screen pixels

        // Internal cached fit info
        private double _baseScale = 1.0;
        private Point _baseOrigin = new(0, 0); // top-left of fitted image with no zoom/pan
        #endregion

        #region Events
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double viewW = ActualWidth;
            double viewH = ActualHeight;

            // Background
            dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, viewW, viewH));

            if (Bitmap == null) 
                return;

            double imgW = Bitmap.PixelWidth;
            double imgH = Bitmap.PixelHeight;
            if (imgW <= 0 || imgH <= 0)
                return;

            // Compute base fit to window (no zoom)
            double fitScaleX = viewW / imgW;
            double fitScaleY = viewH / imgH;
            _baseScale = Math.Min(fitScaleX, fitScaleY);

            double baseDrawW = imgW * _baseScale;
            double baseDrawH = imgH * _baseScale;

            _baseOrigin = new Point(
                (viewW - baseDrawW) * 0.5,
                (viewH - baseDrawH) * 0.5
            );

            // Final effective scale
            double effScale = _baseScale * UserScale;

            // Destination rect after zoom and pan
            Rect imgRect = new(
                _baseOrigin.X + UserOffset.X,
                _baseOrigin.Y + UserOffset.Y,
                imgW * effScale,
                imgH * effScale
            );

            // Draw image
            dc.DrawImage(Bitmap, imgRect);

            // Grid math using effScale and origin+offset
            DrawGridAndLabels(dc, imgW, imgH, effScale);
        }
        #endregion

        #region Routines
        private void DrawGridAndLabels(DrawingContext dc, double imgW, double imgH, double effScale)
        {
            // Cols and rows
            int cols = 1 << (SplitLevel + 1); // 2^(N+1)
            int rows = 1 << SplitLevel;       // 2^N

            // Per-cell size in image pixels
            double cellImgW = imgW / cols;
            double cellImgH = imgH / rows;

            // Convert helper: image pixel -> screen point
            Point ImgToScreen(double px, double py)
            {
                return new Point(
                    _baseOrigin.X + UserOffset.X + px * effScale,
                    _baseOrigin.Y + UserOffset.Y + py * effScale
                );
            }

            // Draw grid lines
            if (GridPens != null)
            {
                Pen gridPen = GridPens[Math.Min(Math.Max(SplitLevel, 0), LevelBrushes.Length - 1)];

                // Verticals
                for (int c = 0; c <= cols; c++)
                {
                    double xImg = c * cellImgW;
                    Point p1 = ImgToScreen(xImg, 0);
                    Point p2 = ImgToScreen(xImg, imgH);
                    dc.DrawLine(gridPen, p1, p2);
                }

                // Horizontals
                for (int r = 0; r <= rows; r++)
                {
                    double yImg = r * cellImgH;
                    Point p1 = ImgToScreen(0, yImg);
                    Point p2 = ImgToScreen(imgW, yImg);
                    dc.DrawLine(gridPen, p1, p2);
                }
            }

            // Performance clamp:
            // Only render text if total cells <= 4096 (64x64)
            // This triggers for splitLevel <= 5.
            long totalCells = (long)cols * (long)rows;
            bool drawLabels = totalCells <= 4096;

            // Labels
            if (!drawLabels || LevelBrushes == null || LevelBrushes.Length == 0)
                return;

            Brush textBrush = LevelBrushes[Math.Min(Math.Max(SplitLevel, 0), LevelBrushes.Length - 1)];

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    string label = $"{x}, {y}";

                    // Cell center in image pixel coords
                    double cxImg = (x + 0.5) * cellImgW;
                    double cyImg = (y + 0.5) * cellImgH;

                    // Map to screen
                    Point centerScreen = ImgToScreen(cxImg, cyImg);

                    // Choose font size: scale with effScale and cell size
                    double cellScreenW = cellImgW * effScale;
                    double cellScreenH = cellImgH * effScale;
                    // Choose size proportional to min(cellW, cellH)
                    double baseSize = Math.Max(10.0, Math.Min(cellScreenW, cellScreenH) * 0.33);

                    FormattedText ft = CreateFormattedText(label, textBrush, baseSize);

                    double textX = centerScreen.X - ft.Width * 0.5;
                    double textY = centerScreen.Y - ft.Height * 0.5;

                    dc.DrawText(ft, new Point(textX, textY));
                }
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Public zoom API called by MainWindow
        /// </summary>
        public void ZoomAt(Point screenPos, double zoomFactor)
        {
            // Clamp future scale
            double oldScale = UserScale;
            double newScale = oldScale * zoomFactor;
            if (newScale < 0.05) newScale = 0.05;
            if (newScale > 64.0) newScale = 64.0;

            // Get current state numbers we need
            double effScaleOld = _baseScale * oldScale;

            // Solve for image pixel under cursor before zoom
            // Pimg = (Scursor - baseOrigin - UserOffset) / effScaleOld
            Vector vCursorOld = (Vector)screenPos;
            Vector vBaseOrigin = (Vector)_baseOrigin;
            Vector numer = vCursorOld - vBaseOrigin - UserOffset;
            double px = numer.X / effScaleOld;
            double py = numer.Y / effScaleOld;

            // Compute new offset to keep that same image pixel under the cursor
            double effScaleNew = _baseScale * newScale;

            // UserOffset_new = Scursor - baseOrigin - effScaleNew * Pimg
            Vector newOffset = vCursorOld - vBaseOrigin - new Vector(px * effScaleNew, py * effScaleNew);

            UserScale = newScale;
            UserOffset = newOffset;

            InvalidateVisual();
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
        public bool TryGetTileFromScreenPoint(Point screenPt, out int tileX, out int tileY, out int level, out SKRectI srcPixelRect)
        {
            tileX = 0;
            tileY = 0;
            level = SplitLevel;
            srcPixelRect = SKRectI.Empty;

            if (Bitmap == null)
                return false;

            // Image pixel size
            double imgW = Bitmap.PixelWidth;
            double imgH = Bitmap.PixelHeight;
            if (imgW <= 0 || imgH <= 0)
                return false;

            // First, map screen point -> image pixel coordinates (Pimg)
            // Recall mapping:
            // screen = _baseOrigin + UserOffset + effScale * Pimg
            // => Pimg = (screen - _baseOrigin - UserOffset) / effScale
            double effScale = _baseScale * UserScale;

            Vector vScreen = (Vector)screenPt;
            Vector vBase = (Vector)_baseOrigin;
            Vector rel = vScreen - vBase - UserOffset;
            double px = rel.X / effScale;
            double py = rel.Y / effScale;

            // Outside image?
            if (px < 0 || py < 0 || px >= imgW || py >= imgH)
                return false;

            // Compute cols/rows at this level
            int cols = 1 << (SplitLevel + 1); // 2^(N+1)
            int rows = 1 << SplitLevel;       // 2^N

            // size of each cell in source pixels
            double cellSrcW = imgW / cols;
            double cellSrcH = imgH / rows;

            // Which tile are we in?
            int tx = (int)Math.Floor(px / cellSrcW);
            int ty = (int)Math.Floor(py / cellSrcH);

            // safety clamp
            if (tx < 0) tx = 0;
            if (ty < 0) ty = 0;
            if (tx >= cols) tx = cols - 1;
            if (ty >= rows) ty = rows - 1;

            tileX = tx;
            tileY = ty;

            // Compute exact integer crop rect in source pixel coords.
            // Match your reference ProcessFromSource logic:
            // left/top floor, right/bottom ceiling
            int l = (int)Math.Floor(tx * cellSrcW);
            int t = (int)Math.Floor(ty * cellSrcH);
            int r = (int)Math.Ceiling((tx + 1) * cellSrcW);
            int b = (int)Math.Ceiling((ty + 1) * cellSrcH);

            // build SKRectI
            int w = Math.Max(1, r - l);
            int h = Math.Max(1, b - t);
            srcPixelRect = SKRectI.Create(l, t, w, h);

            return true;
        }
        #endregion
    }
}
