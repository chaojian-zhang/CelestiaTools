using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VirtualTexturePreviewPicker
{
    public partial class MainWindow : Window
    {
        #region Configurations
        private BitmapSource _bitmap;
        private int _splitLevel = 0;

        private readonly Brush[] _levelBrushes;
        private readonly Pen _gridPen;
        #endregion

        public MainWindow()
        {
            Title = $"Split Level: {_splitLevel}";
            Width = 1200;
            Height = 800;
            Background = Brushes.Black;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // define 13 distinct brushes (0..12)
            _levelBrushes =
            [
                // 13 distinct colors for levels 0..12
                Brushes.Red,
                Brushes.Blue,
                Brushes.Green,
                Brushes.Orange,
                Brushes.Purple,
                Brushes.Teal,
                Brushes.Brown,
                Brushes.Magenta,
                Brushes.Goldenrod,
                Brushes.Crimson,
                Brushes.DarkCyan,
                Brushes.SaddleBrown,
                Brushes.DarkViolet
            ];

            _gridPen = new Pen(Brushes.Gray, 1.0);
            _gridPen.Freeze();

            // Input hooks
            KeyDown += OnKeyDown;
            MouseWheel += OnMouseWheel;
            MouseDoubleClick += OnMouseDoubleClick;

            // Re-render on size change
            SizeChanged += (_, __) => InvalidateVisual();
        }

        #region Routines
        /// <summary>
        /// Image load logic
        /// </summary>
        private void LoadImageFromDisk()
        {
            OpenFileDialog dlg = new()
            {
                Filter =
                    "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All Files|*.*",
                Title = "Open Image"
            };

            bool? ok = dlg.ShowDialog(this);
            if (ok == true && File.Exists(dlg.FileName))
            {
                try
                {
                    BitmapImage bmp = new();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(dlg.FileName);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    bmp.EndInit();
                    bmp.Freeze(); // make cross thread safe and faster

                    _bitmap = bmp;
                    Title = $"Split Level: {_splitLevel} | {System.IO.Path.GetFileName(dlg.FileName)} ({_bitmap.PixelWidth}x{_bitmap.PixelHeight})";
                    InvalidateVisual();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this,
                        "Failed to load image: " + ex.Message,
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
        /// <summary>
        /// Split level adjust
        /// </summary>
        private void ChangeSplitLevel(int delta)
        {
            int newLevel = _splitLevel + delta;
            if (newLevel < 0) newLevel = 0;
            if (newLevel > 12) newLevel = 12;
            if (newLevel != _splitLevel)
            {
                _splitLevel = newLevel;
                UpdateTitle();
                InvalidateVisual();
            }
        }
        private void UpdateTitle()
        {
            string imgPart = "";
            if (_bitmap != null)
            {
                imgPart = $" | {_bitmap.PixelWidth}x{_bitmap.PixelHeight}";
            }

            Title = $"Split Level: {_splitLevel}{imgPart}";
        }
        #endregion

        #region Window Events
        private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
            => LoadImageFromDisk();
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+O to open
            if (e.Key == Key.O && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                LoadImageFromDisk();
                e.Handled = true;
                return;
            }

            switch (e.Key)
            {
                // Up controls
                case Key.Up:
                case Key.OemPlus:
                case Key.W:
                case Key.PageUp:
                    ChangeSplitLevel(+1);
                    e.Handled = true;
                    break;

                // Down controls
                case Key.Down:
                case Key.OemMinus:
                case Key.S:
                case Key.PageDown:
                    ChangeSplitLevel(-1);
                    e.Handled = true;
                    break;
            }
        }
        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // standard: positive = up
            ChangeSplitLevel(e.Delta > 0 ? +1 : -1);
            e.Handled = true;
        }
        /// <summary>
        /// Core drawing
        /// </summary>
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double winW = ActualWidth;
            double winH = ActualHeight;

            // Background
            dc.DrawRectangle(Background, null, new Rect(0, 0, winW, winH));

            if (_bitmap == null) 
                return;

            // Compute fit scale keep aspect ratio
            double imgW = _bitmap.PixelWidth;
            double imgH = _bitmap.PixelHeight;

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

            // Render image
            dc.DrawImage(_bitmap, imgRect);

            // Compute grid divs
            int cols = 1 << (_splitLevel + 1); // 2^(N+1)
            int rows = 1 << _splitLevel;       // 2^N

            double cellW = drawW / cols;
            double cellH = drawH / rows;

            // Draw grid lines
            DrawGridLines(dc, offX, offY, cols, rows, cellW, cellH);

            // Draw labels
            DrawCellLabels(dc, offX, offY, cols, rows, cellW, cellH);
        }
        #endregion

        #region Drawing Routines
        private void DrawGridLines(
            DrawingContext dc,
            double offX,
            double offY,
            int cols,
            int rows,
            double cellW,
            double cellH)
        {
            // vertical lines
            for (int c = 0; c <= cols; c++)
            {
                double x = offX + c * cellW;
                dc.DrawLine(_gridPen,
                    new Point(x, offY),
                    new Point(x, offY + rows * cellH));
            }

            // horizontal lines
            for (int r = 0; r <= rows; r++)
            {
                double y = offY + r * cellH;
                dc.DrawLine(_gridPen,
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
            // Performance clamp:
            // Only render text if total cells <= 4096 (64x64)
            // This triggers for splitLevel <= 5.
            long totalCells = (long)cols * (long)rows;
            bool drawLabels = totalCells <= 4096;

            Brush textBrush = _levelBrushes[
                Math.Min(Math.Max(_splitLevel, 0), _levelBrushes.Length - 1)
            ];

            if (!drawLabels)
                return;

            // font size roughly 12 scaled with cell size but not tiny
            // choose size proportional to min(cellW, cellH)
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
            // The constructor with CultureInfo etc is obsolete but still works in classic WPF.
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