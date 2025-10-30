using System.Windows;

namespace VirtualTexturePreviewPicker
{
    internal static class Program
    {
        [STAThread]
        public static void Main()
        {
            // No XAML startup
            Application app = new();
            MainWindow win = new();
            app.Run(win);
        }
    }
}
