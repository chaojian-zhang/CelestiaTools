using System.Windows;

namespace VirtualTexturePreviewPicker
{
    internal static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application app = new();
            // no XAML startup
            MainWindow win = new();
            app.Run(win);
        }
    }
}
