using Microsoft.Win32;
using SkiaSharp;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VirtualTexturePreviewPicker
{
    public sealed class MainWindow : Window
    {
        #region States
        private readonly RenderSurface _surface;

        private int _splitLevel = 0;
        private string? _filePath;
        private readonly Brush[] _levelBrushes;
        private readonly Pen[] _gridPens;
        #endregion

        public MainWindow()
        {
            Title = $"Split Level: {_splitLevel}";
            Width = 1200;
            Height = 800;
            Background = Brushes.Black;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Prepare brushes and pen
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

            _gridPens = [.. _levelBrushes.Select(b => new Pen(b, 4.0))];
            foreach (Pen pen in _gridPens)
                pen.Freeze();

            // Create the drawing surface
            _surface = new RenderSurface
            {
                Focusable = true,
                Bitmap = null,
                SplitLevel = _splitLevel,
                GridPens = _gridPens,
                LevelBrushes = _levelBrushes,
                UserScale = 1.0,
                UserOffset = new System.Windows.Vector(0, 0)
            };

            // Make the surface fill the window
            Content = _surface;

            // Input hooks
            KeyDown += OnKeyDown;
            MouseWheel += OnMouseWheel;
            MouseDoubleClick += OnMouseDoubleClick;
            MouseRightButtonDown += OnMouseRightButtonDown;

            // Re-render on size change
            SizeChanged += (_, __) => _surface.InvalidateVisual();
        }

        #region Routines
        /// <summary>
        /// Image load logic
        /// </summary>
        private void LoadImageFromDisk()
        {
            OpenFileDialog dialog = new()
            {
                Filter =
                    "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All Files|*.*",
                Title = "Open Image"
            };

            bool? ok = dialog.ShowDialog(this);
            if (ok == true && File.Exists(dialog.FileName))
            {
                try
                {
                    _filePath = dialog.FileName;

                    BitmapImage bmp = new();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(dialog.FileName);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    bmp.EndInit();
                    bmp.Freeze(); // make cross thread safe and faster

                    _surface.Bitmap = bmp;

                    // Reset zoom when new image loads
                    _surface.UserScale = 1.0;
                    _surface.UserOffset = new System.Windows.Vector(0, 0);

                    _surface.InvalidateVisual();

                    UpdateTitleWithImageInfo();
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
        private void ExportTileToPng(int tileX, int tileY, int level, SKRectI srcRect)
        {
            // if we don't have a path we cannot export
            if (string.IsNullOrEmpty(_filePath))
                return;

            const int TILE_SIZE = 512;

            // Pick output path
            SaveFileDialog dlg = new()
            {
                Title = "Save Tile",
                Filter = "PNG Image|*.png",
                FileName = $"level{level}_tx_{tileX}_{tileY}.png",
                AddExtension = true,
                DefaultExt = ".png",
                OverwritePrompt = true
            };

            bool? ok = dlg.ShowDialog(this);
            if (ok != true)
                return;

            string outPath = dlg.FileName;

            // Load original image via SkiaSharp directly from disk
            using SKBitmap fullBmp = SKBitmap.Decode(_filePath);
            if (fullBmp == null)
                return;

            using SKImage fullImg = SKImage.FromBitmap(fullBmp);
            // Safety clamp srcRect to source bounds in case of rounding edges
            SKRectI clampedRect = ClampRectToImage(srcRect, fullImg.Width, fullImg.Height);

            // Render cropped region into TILE_SIZE x TILE_SIZE
            using SKSurface surface = SKSurface.Create(new SKImageInfo(TILE_SIZE, TILE_SIZE, SKColorType.Rgba8888, SKAlphaType.Premul));

            SKCanvas canvas = surface.Canvas;
            canvas.Clear(SKColors.Black);

            using (SKPaint paint = new()
            {
                FilterQuality = SKFilterQuality.High,
                IsAntialias = false
            })
            {
                SKRect dstRect = new(0, 0, TILE_SIZE, TILE_SIZE);
                canvas.DrawImage(fullImg, clampedRect, dstRect, paint);
            }

            // Encode to PNG
            using SKImage tileImage = surface.Snapshot();
            using SKData data = tileImage.Encode(SKEncodedImageFormat.Png, 100);
            using FileStream fs = File.Open(outPath, FileMode.Create, FileAccess.Write, FileShare.None);
            data.SaveTo(fs);
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

                case Key.F:
                    // Center frame (reset zoom + offset)
                    _surface.UserScale = 1.0;
                    _surface.UserOffset = new System.Windows.Vector(0, 0);
                    _surface.InvalidateVisual();
                    UpdateTitleWithImageInfo();
                    e.Handled = true;
                    break;
            }
        }
        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            bool ctrlDown = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            if (ctrlDown)
            {
                // zoom
                // positive delta = zoom in
                double step = e.Delta > 0 ? 1.25 : 0.8; // multiplicative
                // get cursor position relative to the RenderSurface
                Point pos = e.GetPosition(_surface);

                _surface.ZoomAt(pos, step);

                UpdateTitleWithImageInfo();

                e.Handled = true;
            }
            else
            {
                // change split level
                ChangeSplitLevel(e.Delta > 0 ? +1 : -1);
                e.Handled = true;
            }
        }
        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // get click position in surface space
            Point p = e.GetPosition(_surface);

            if (_surface.TryGetTileFromScreenPoint(
                    p,
                    out int tileX,
                    out int tileY,
                    out int level,
                    out SKRectI srcRect))
            {
                // we have a tile. proceed to export
                ExportTileToPng(tileX, tileY, level, srcRect);
            }

            e.Handled = true;
        }
        #endregion

        #region Helpers
        private void ChangeSplitLevel(int delta)
        {
            int newLevel = _splitLevel + delta;
            if (newLevel < 0) 
                newLevel = 0;
            if (newLevel > 12) 
                newLevel = 12;
            if (newLevel != _splitLevel)
            {
                _splitLevel = newLevel;
                _surface.SplitLevel = _splitLevel;
                _surface.InvalidateVisual();
                UpdateTitleWithImageInfo();
            }
        }
        private void UpdateTitleWithImageInfo()
        {
            if (_surface.Bitmap != null)
                Title = $"Split Level: {_splitLevel} | {System.IO.Path.GetFileName(_filePath)} ({_surface.Bitmap.PixelWidth}x{_surface.Bitmap.PixelHeight}) | Zoom {Math.Round(_surface.UserScale, 2)}x";
            else
                Title = $"Split Level: {_splitLevel} | Zoom {Math.Round(_surface.UserScale, 2)}x";
        }
        private static SKRectI ClampRectToImage(SKRectI r, int imgW, int imgH)
        {
            int l = Math.Max(0, r.Left);
            int t = Math.Max(0, r.Top);
            int rgt = Math.Min(imgW, r.Right);
            int bot = Math.Min(imgH, r.Bottom);

            if (rgt < l) rgt = l;
            if (bot < t) bot = t;

            return SKRectI.Create(l, t, Math.Max(1, rgt - l), Math.Max(1, bot - t));
        }
        #endregion
    }
}