using SkiaSharp;

namespace VirtualTextureReferenceSet
{
    internal class Program
    {
        #region Configurations
        private const string AppName = "VirtualTextureReferenceSet";
        private const string AppVersion = "0.0.2";

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
        #endregion

        #region Entry
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
                (string outputRoot, int tile, int maxLevel, string? sourcePath) = ParseArgs(args);

                if (maxLevel < 0 || maxLevel > 12)
                    throw new ArgumentException("Levels must be between 0 and 12 inclusive.");

                if (tile <= 0)
                    throw new ArgumentException("Tile must be a positive integer.");

                Directory.CreateDirectory(outputRoot);

                if (!string.IsNullOrEmpty(sourcePath))
                {
                    using SKImage src = LoadEquirectangular(sourcePath);
                    ProcessFromSource(src, outputRoot, tile, maxLevel);
                }
                else
                {
                    ProcessSynthetic(outputRoot, tile, maxLevel);
                }

                // Configuration files next to the output folder
                WriteConfigurationFiles(outputRoot, tile);

                Console.WriteLine("Done.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                return 1;
            }
        }
        #endregion


        #region Routines
        private static void PrintHelp()
        {
            Console.WriteLine($"""
                {AppName} {AppVersion}
                Generate virtual-texture reference tiles with SkiaSharp, or slice an equirectangular source image into tiles. Writes .ctx and .ssc configuration files.

                Usage:
                  {AppName} <outputFolder> [--tile <int>] [--levels <0-12>] [--source <path>]
                  {AppName} --help
                  {AppName} --version

                Arguments:
                  <outputFolder>           Root folder for output. Subfolders level<N> are created.

                Options:
                  --tile <int>             Tile resolution (square). Default 512.
                  --levels <0-12>          Highest level to generate. Generates level0..levelN. Default 0.
                  --source <path>          Use an input image instead of synthetic tiles. Must be equirectangular (width == 2*height).
                  --help, -h               Show help.
                  --version, -v            Show version.

                Outputs:
                  Synthetic mode: level<N>/tx_<x>_<y>.png with checkerboard background, colored border, centered text "<x>_<y>".
                  Source mode:    level<N>/tx_<x>_<y>.png cropped and resampled from the input image.
                  Configuration files:
                    <FolderName>.ctx         Next to the output folder. Points ImageDirectory to <FolderName>.
                    <FolderName>.ssc         Next to the output folder. References the .ctx.
                Counts: level N has x in [0, 2^(N+1)-1], y in [0, 2^N-1] => 2^(2N+1) tiles.
                """);
        }
        private static void ProcessSynthetic(string outputRoot, int tile, int maxLevel)
        {
            for (int level = 0; level <= maxLevel; level++)
            {
                string levelDir = Path.Combine(outputRoot, $"level{level}");
                Directory.CreateDirectory(levelDir);

                long tilesX = 1L << (level + 1); // 2^(N+1)
                long tilesY = 1L << level;       // 2^N
                Console.WriteLine($"Level {level}: generating {tilesX * tilesY} tiles in {levelDir}");

                SKColor color = LevelColors[level % LevelColors.Length];

                // Parallel generation
                Parallel.For(0L, tilesX, x =>
                {
                    using SKPaint textPaint = MakeTextPaint(tile, color);
                    using SKPaint borderPaint = MakeBorderPaint(tile, color);
                    using SKPaint bgPaint = new() { Color = SKColors.White, IsAntialias = true };
                    bool drawCheckerboard = true;

                    for (long y = 0; y < tilesY; y++)
                    {
                        string file = Path.Combine(levelDir, $"tx_{x}_{y}.png");

                        using SKSurface surface = SKSurface.Create(new SKImageInfo(tile, tile, SKColorType.Rgba8888, SKAlphaType.Premul));
                        SKCanvas canvas = surface.Canvas;
                        canvas.Clear(SKColors.White);

                        // Draw background
                        if (drawCheckerboard)
                            DrawCheckerboard(canvas, tile, grid: 8, gray: 0xDD); // 0xD0–0xE0 is a good dim range
                        else
                            canvas.DrawRect(new SKRect(0, 0, tile, tile), bgPaint); // Draw pure white background; Not strictly necessary but kept for clarity

                        // Thick colored border
                        float margin = tile * 0.04f; // inner offset from the outermost edge
                        SKRect rect = new(margin, margin, tile - margin, tile - margin);
                        canvas.DrawRect(rect, borderPaint);

                        // Top text "level <N>"
                        DrawTopText(canvas, $"level {level}", tile, textPaint);

                        // Centered text "<x>_<y>"
                        DrawCenteredText(canvas, $"{x}_{y}", tile, textPaint);

                        using SKImage image = surface.Snapshot();
                        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
                        using FileStream fs = File.Open(file, FileMode.Create, FileAccess.Write, FileShare.None);
                        data.SaveTo(fs);
                    }
                });
            }
        }
        private static void DrawCheckerboard(SKCanvas canvas, int tile, int grid = 8, byte gray = 0xDD)
        {
            // grid = number of cells per side. 8 gives 64 cells total.
            float cell = tile / (float)grid;
            using SKPaint paint = new()
            {
                IsAntialias = false,
                Color = new SKColor(gray, gray, gray), // dim gray cells
                Style = SKPaintStyle.Fill
            };

            // Draw only the gray cells; white cells are the background.
            for (int y = 0; y < grid; y++)
            {
                float top = y * cell;
                float bottom = (y + 1) * cell;

                // Start parity alternates per row.
                int start = (y & 1) == 0 ? 1 : 0;

                for (int x = start; x < grid; x += 2)
                {
                    float left = x * cell;
                    float right = (x + 1) * cell;

                    // Use integer-aligned rects to avoid seams on exact divisors.
                    SKRectI rect = SKRectI.Ceiling(new SKRect(left, top, right, bottom));
                    canvas.DrawRect(rect, paint);
                }
            }
        }
        private static void DrawTopText(SKCanvas canvas, string text, int tileWidth, SKPaint paint)
        {
            // Fit text to width with a small margin
            float targetWidth = tileWidth * 0.86f;

            // Find a typeface that is italic; Since no family name is specified, SkiaSharp will use the system's default font
            SKTypeface italicTypeface = SKFontManager.Default.MatchFamily(null, SKFontStyle.Italic);
            SKFont font = new(italicTypeface, size: tileWidth * 0.1f);
            float measured = font.MeasureText(text);
            if (measured > 0f)
            {
                float scale = Math.Min(1f, targetWidth / measured);
                font.Size *= scale;
            }

            // Just pick somwhere atop
            float baseline = tileWidth * 0.2f;
            float centerX = tileWidth * 0.5f;
            canvas.DrawText(text, centerX, baseline, SKTextAlign.Center, font, paint);
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
            SKFontMetrics metrics = paint.FontMetrics;
            // Baseline such that the center of text's bounds aligns with tile/2
            float textHeight = metrics.Descent - metrics.Ascent;
            float centerY = tile * 0.5f;
            float baseline = centerY - (metrics.Ascent + textHeight / 2f);

            float centerX = tile * 0.5f;
            canvas.DrawText(text, centerX, baseline, paint);
        }
        private static void ProcessFromSource(SKImage source, string outputRoot, int tile, int maxLevel)
        {
            int srcW = source.Width;
            int srcH = source.Height;

            for (int level = 0; level <= maxLevel; level++)
            {
                string levelDir = Path.Combine(outputRoot, $"level{level}");
                Directory.CreateDirectory(levelDir);

                long tilesX = 1L << (level + 1); // 2^(N+1)
                long tilesY = 1L << level;       // 2^N
                Console.WriteLine($"Level {level}: slicing {tilesX * tilesY} tiles from source into {levelDir}");

                // Compute source cell size at this level
                double cellW = (double)srcW / tilesX;
                double cellH = (double)srcH / tilesY;

                Parallel.For(0L, tilesX, x =>
                {
                    for (long y = 0; y < tilesY; y++)
                    {
                        string file = Path.Combine(levelDir, $"tx_{x}_{y}.png");

                        // Exact pixel-aligned crop to avoid seams:
                        // left/top use floor; right/bottom use ceiling.
                        int l = (int)Math.Floor(x * cellW);
                        int t = (int)Math.Floor(y * cellH);
                        int r = (int)Math.Ceiling((x + 1) * cellW);
                        int b = (int)Math.Ceiling((y + 1) * cellH);

                        SKRectI srcRect = SKRectI.Create(l, t, Math.Max(1, r - l), Math.Max(1, b - t));
                        SKRect dstRect = new(0, 0, tile, tile);

                        using SKSurface surface = SKSurface.Create(new SKImageInfo(tile, tile, SKColorType.Rgba8888, SKAlphaType.Premul));
                        SKCanvas canvas = surface.Canvas;
                        canvas.Clear(SKColors.Black); // unused pixels if any resampling edge

                        // High-quality resample
                        using SKPaint paint = new() { FilterQuality = SKFilterQuality.High, IsAntialias = false };
                        canvas.DrawImage(source, srcRect, dstRect, paint);

                        using SKImage image = surface.Snapshot();
                        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
                        using FileStream fs = File.Open(file, FileMode.Create, FileAccess.Write, FileShare.None);
                        data.SaveTo(fs);
                    }
                });
            }
        }
        private static void WriteConfigurationFiles(string outputRoot, int tile)
        {
            // Determine sibling file locations: same parent as the output directory
            string parentDir = Directory.GetParent(outputRoot)?.FullName
                ?? throw new InvalidOperationException("cannot resolve parent directory");
            string folderName = Path.GetFileName(outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            string ctxPath = Path.Combine(parentDir, $"{folderName}.ctx");
            string sscPath = Path.Combine(parentDir, $"{folderName}.ssc");

            // .ctx content
            string ctx = $$"""
                VirtualTexture
                {
                    ImageDirectory "{{folderName}}"
                    BaseSplit 0
                    TileSize {{tile}}
                    TileType "png"
                }
                """;

            // .ssc content (exact strings per your template)
            string ssc = $$"""
                AltSurface "{{folderName}}" "Parent/Child"
                {
                    Texture "{{folderName}}.ctx"
                }
                """;

            File.WriteAllText(ctxPath, ctx);
            File.WriteAllText(sscPath, ssc);

            Console.WriteLine($"""
                Wrote:
                  {ctxPath}
                  {sscPath}
                """);
        }
        #endregion

        #region Helpers
        private static (string outputRoot, int tile, int levels, string? sourcePath) ParseArgs(string[] args)
        {
            string? outputRoot = null;
            int tile = 512;
            int levels = 0;
            string? sourcePath = null;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];

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
                if (a == "--source" && i + 1 < args.Length)
                {
                    sourcePath = args[++i];
                    continue;
                }
                if (a.StartsWith("-", StringComparison.Ordinal))
                    continue;

                // first non-flag arg is output folder
                if (outputRoot == null) outputRoot = a;
            }

            if (outputRoot == null)
                throw new ArgumentException("missing <outputFolder> argument. See --help.");

            if (!string.IsNullOrEmpty(sourcePath) && !File.Exists(sourcePath))
                throw new FileNotFoundException("source file not found", sourcePath);

            return (Path.GetFullPath(outputRoot), tile, levels, sourcePath);
        }
        private static SKImage LoadEquirectangular(string path)
        {
            using SKData data = SKData.Create(path);
            using SKCodec codec = SKCodec.Create(data);
            if (codec == null) 
                throw new ArgumentException("unsupported or corrupted image");

            SKImageInfo info = codec.Info;
            if (info.Width != 2 * info.Height)
                throw new ArgumentException($"source is not equirectangular: width={info.Width}, height={info.Height}, expected width==2*height.");

            // Decode once to an SKImage
            using SKBitmap bmp = SKBitmap.Decode(codec);
            if (bmp == null) 
                throw new ArgumentException("failed to decode source image");
            return SKImage.FromBitmap(bmp);
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
            SKPaint paint = new()
            {
                IsAntialias = true,
                Color = color,
                IsStroke = false,
                TextAlign = SKTextAlign.Center
            };

            // Choose a platform-safe typeface if available, else default.
            // SkiaSharp will fallback gracefully.
            SKTypeface tf = SKTypeface.FromFamilyName("Arial") ?? SKTypeface.Default;
            paint.Typeface = tf;

            paint.TextSize = baseSize;
            return paint;
        }
        #endregion
    }
}
