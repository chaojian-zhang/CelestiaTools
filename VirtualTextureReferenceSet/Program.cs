using SkiaSharp;

namespace VirtualTextureReferenceSet
{
    internal class Program
    {
        private const string AppName = "VirtualTextureReferenceSet";
        private const string AppVersion = "0.0.1";

        private static readonly SKColor[] LevelColors =
        [
            // 13 distinct colors for levels 0..12
            SKColors.Red,
            SKColors.Blue,
            SKColors.Green,
            SKColors.Orange,
            SKColors.Purple,
            SKColors.Teal,
            SKColors.Brown,
            SKColors.Magenta,
            SKColors.Goldenrod,
            SKColors.Crimson,
            SKColors.DarkCyan,
            SKColors.SaddleBrown,
            SKColors.DarkViolet
        ];

        public static int Main(string[] args)
        {
            if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h"))
            {
                PrintHelp();
                return 0;
            }
            if (args.Contains("--version", StringComparer.OrdinalIgnoreCase) || args.Contains("-v"))
            {
                Console.WriteLine($"{AppName} {AppVersion}");
                return 0;
            }

            try
            {
                var (outputRoot, tile, maxLevel) = ParseArgs(args);

                if (maxLevel < 0 || maxLevel > 12)
                    throw new ArgumentException("Levels must be between 0 and 12 inclusive.");

                if (tile <= 0)
                    throw new ArgumentException("Tile must be a positive integer.");

                Directory.CreateDirectory(outputRoot);

                for (int level = 0; level <= maxLevel; level++)
                {
                    string levelDir = Path.Combine(outputRoot, $"level{level}");
                    Directory.CreateDirectory(levelDir);

                    long tilesX = 1L << (level + 1); // 2^(N+1)
                    long tilesY = 1L << level;       // 2^N
                    Console.WriteLine($"Level {level}: generating {tilesX * tilesY} tiles in {levelDir}");

                    var color = LevelColors[level % LevelColors.Length];

                    // Parallel generation
                    Parallel.For(0L, tilesX, x =>
                    {
                        using var textPaint = MakeTextPaint(tile, color);
                        using var borderPaint = MakeBorderPaint(tile, color);
                        using var bgPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };

                        for (long y = 0; y < tilesY; y++)
                        {
                            string file = Path.Combine(levelDir, $"tx_{x}_{y}.png");

                            using var surface = SKSurface.Create(new SKImageInfo(tile, tile, SKColorType.Rgba8888, SKAlphaType.Premul));
                            var canvas = surface.Canvas;
                            canvas.Clear(SKColors.White);

                            // Draw background (already white). Keep for clarity if white ever changes.
                            canvas.DrawRect(new SKRect(0, 0, tile, tile), bgPaint);

                            // Thick colored border
                            float margin = tile * 0.04f; // inner offset from the outermost edge
                            var rect = new SKRect(margin, margin, tile - margin, tile - margin);
                            canvas.DrawRect(rect, borderPaint);

                            // Centered text "<x>_<y>"
                            string label = $"{x}_{y}";
                            DrawCenteredText(canvas, label, tile, textPaint);

                            using var image = surface.Snapshot();
                            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                            using var fs = File.Open(file, FileMode.Create, FileAccess.Write, FileShare.None);
                            data.SaveTo(fs);
                        }
                    });
                }

                // Sidecar files next to the output folder
                WriteSidecarFiles(outputRoot, tile);

                Console.WriteLine("Done.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                return 1;
            }
        }

        private static (string outputRoot, int tile, int levels) ParseArgs(string[] args)
        {
            string? outputRoot = null;
            int tile = 512;
            int levels = 0;

            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];

                if (a == "--tile" && i + 1 < args.Length)
                {
                    if (!int.TryParse(args[++i], out tile))
                        throw new ArgumentException("invalid --tile value");
                    continue;
                }
                if (a == "--levels" && i + 1 < args.Length)
                {
                    if (!int.TryParse(args[++i], out levels))
                        throw new ArgumentException("invalid --levels value");
                    continue;
                }
                if (a.StartsWith("-", StringComparison.Ordinal))
                    continue;

                // first non-flag arg is output folder
                if (outputRoot == null) outputRoot = a;
            }

            if (outputRoot == null)
                throw new ArgumentException("missing <outputFolder> argument. See --help.");

            return (Path.GetFullPath(outputRoot), tile, levels);
        }

        private static void PrintHelp()
        {
            Console.WriteLine($@"{AppName} {AppVersion}
Generate virtual-texture reference tiles with SkiaSharp and write .ctx and .ssc sidecars.

Usage:
  {AppName} <outputFolder> [--tile <int>] [--levels <0-12>]
  {AppName} --help
  {AppName} --version

Arguments:
  <outputFolder>           Root folder for output. Subfolders level<N> are created.

Options:
  --tile <int>             Tile resolution (square). Default 512.
  --levels <0-12>          Highest level to generate. Generates level0..levelN. Default 0.
  --help, -h               Show help.
  --version, -v            Show version.

Outputs:
  level<N>/tx_<x>_<y>.png  White background, thick colored border, centered text ""<x>_<y>"".
  <FolderName>.ctx         Next to the output folder. Points ImageDirectory to <FolderName>.
  <FolderName>.ssc         Next to the output folder. References the .ctx.
Counts: level N has x in [0, 2^(N+1)-1], y in [0, 2^N-1] => 2^(2N+1) tiles.");
        }

        private static SKPaint MakeBorderPaint(int tile, SKColor color)
        {
            return new SKPaint
            {
                IsAntialias = true,
                Color = color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(6f, tile * 0.06f),
                StrokeJoin = SKStrokeJoin.Miter
            };
        }

        private static SKPaint MakeTextPaint(int tile, SKColor color)
        {
            // Start with a size proportional to tile. Adjust below to fit.
            float baseSize = tile * 0.28f;
            var paint = new SKPaint
            {
                IsAntialias = true,
                Color = color,
                IsStroke = false,
                TextAlign = SKTextAlign.Center
            };

            // Choose a platform-safe typeface if available, else default.
            // SkiaSharp will fallback gracefully.
            var tf = SKTypeface.FromFamilyName("Arial") ?? SKTypeface.Default;
            paint.Typeface = tf;

            paint.TextSize = baseSize;
            return paint;
        }

        private static void DrawCenteredText(SKCanvas canvas, string text, int tile, SKPaint paint)
        {
            // Fit text to width with a small margin
            float targetWidth = tile * 0.86f;

            float measured = paint.MeasureText(text);
            if (measured > 0f)
            {
                float scale = Math.Min(1f, targetWidth / measured);
                paint.TextSize *= scale;
            }

            // Vertical centering using font metrics
            var metrics = paint.FontMetrics;
            // Baseline such that the center of text's bounds aligns with tile/2
            float textHeight = metrics.Descent - metrics.Ascent;
            float centerY = tile * 0.5f;
            float baseline = centerY - (metrics.Ascent + textHeight / 2f);

            float centerX = tile * 0.5f;
            canvas.DrawText(text, centerX, baseline, paint);
        }

        private static void WriteSidecarFiles(string outputRoot, int tile)
        {
            // Determine sibling file locations: same parent as the output directory
            string parentDir = Directory.GetParent(outputRoot)?.FullName
                               ?? throw new InvalidOperationException("cannot resolve parent directory");
            string folderName = Path.GetFileName(outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            string ctxPath = Path.Combine(parentDir, $"{folderName}.ctx");
            string sscPath = Path.Combine(parentDir, $"{folderName}.ssc");

            // .ctx content
            string ctx = $"VirtualTexture{Environment.NewLine}" +
                         $"{{{Environment.NewLine}" +
                         $"        ImageDirectory \"{folderName}\"{Environment.NewLine}" +
                         $"        BaseSplit 0{Environment.NewLine}" +
                         $"        TileSize {tile}{Environment.NewLine}" +
                         $"        TileType \"png\"{Environment.NewLine}" +
                         $"}}{Environment.NewLine}";

            // .ssc content (exact strings per your template)
            string ssc = $"AltSurface \"{folderName}\" \"Parent/Child\"{Environment.NewLine}" +
                         $"{{{Environment.NewLine}" +
                         $"    Texture \"{folderName}.ctx\"{Environment.NewLine}" +
                         $"}}{Environment.NewLine}";

            File.WriteAllText(ctxPath, ctx);
            File.WriteAllText(sscPath, ssc);

            Console.WriteLine($@"Wrote:
  {ctxPath}
  {sscPath}");
        }
    }
}
