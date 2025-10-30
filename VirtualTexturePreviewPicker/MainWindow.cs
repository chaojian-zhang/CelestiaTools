using Microsoft.Win32;
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
        private readonly Pen _gridPen;
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

            _gridPen = new Pen(Brushes.Gray, 1.0);
            _gridPen.Freeze();

            // Create the drawing surface
            _surface = new RenderSurface
            {
                Focusable = true,
                Bitmap = null,
                SplitLevel = _splitLevel,
                GridPen = _gridPen,
                LevelBrushes = _levelBrushes
            };

            // Make the surface fill the window
            Content = _surface;

            // Input hooks
            KeyDown += OnKeyDown;
            MouseWheel += OnMouseWheel;
            MouseDoubleClick += OnMouseDoubleClick;

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
                Title = $"Split Level: {_splitLevel} | {System.IO.Path.GetFileName(_filePath)} ({_surface.Bitmap.PixelWidth}x{_surface.Bitmap.PixelHeight})";
            else
                Title = $"Split Level: {_splitLevel}";
        }
        #endregion
    }
}