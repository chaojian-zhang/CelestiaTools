using CelestiaStarDatabaseConverter.Types;
using System.Globalization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace CelestiaStarDatabaseConverter
{
    internal class Program
    {
        #region Constants
        const string Magic = "CELSTARS";
        const ushort Version0100 = 0x0100;
        const double Deg2Rad = Math.PI / 180.0;
        const double ObliquityDeg = 23.4392911; // ε
        #endregion

        #region Entry
        static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0 || Has(args, "--help", "-h"))
                {
                    PrintHelp();
                    return 0;
                }
                if (Has(args, "--version", "-v"))
                {
                    Console.WriteLine("StarDataBaseConverter 0.0.1");
                    return 0;
                }

                if (Has(args, "--print"))
                {
                    string inPath = args.Length >= 2 ? args[1] : throw new ArgumentException("Require input path.");
                    PrintFlat(inPath);
                    return 0;
                }
                if (Has(args, "--to-yaml"))
                {
                    (string inPath, string outPath) = ExtractIOArguments(args, 1);
                    ToYaml(inPath, outPath);
                    return 0;
                }
                if (Has(args, "--to-binary"))
                {
                    (string inPath, string outPath) = ExtractIOArguments(args, 1);
                    ToBinary(inPath, outPath);
                    return 0;
                }

                Console.Error.WriteLine("Unknown command. Use --help.");
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
        #endregion

        #region Commands
        static void ToYaml(string binPath, string yamlPath)
        {
            List<StarYaml> stars = ReadStarsFromBinary(binPath);

            RootYaml root = new()
            {
                version = "0x0100",
                stars = stars
            };

            ISerializer serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build();

            string yaml = serializer.Serialize(root);
            File.WriteAllText(yamlPath, yaml);
        }
        static void ToBinary(string yamlPath, string binPath)
        {
            IDeserializer deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            RootYaml root = deserializer.Deserialize<RootYaml>(File.ReadAllText(yamlPath));
            ushort ver = ParseVersion(root.version);
            if (ver != Version0100)
                throw new InvalidDataException("Only version 0x0100 is supported for output.");

            // Precompute x,y,z if user supplied RA/Dec and distance.
            List<StarYaml> stars = root.stars.Select(CoerceCoordinates).ToList();

            using FileStream fs = File.Create(binPath);
            using BinaryWriter bw = new(fs);

            // Header
            bw.Write(System.Text.Encoding.ASCII.GetBytes(Magic));
            bw.Write(Version0100);
            bw.Write((uint)stars.Count);

            foreach (StarYaml s in stars)
            {
                bw.Write(s.hip);
                bw.Write(s.x!.Value);
                bw.Write(s.y!.Value);
                bw.Write(s.z!.Value);
                bw.Write((short)Math.Round(s.abs_mag * 256.0));
                bw.Write(PackSpectral(s.spectral));
            }
        }
        static void PrintFlat(string inPath)
        {
            List<StarYaml> stars = LoadStars(inPath);

            foreach (var s0 in stars)
            {
                // Ensure x/y/z exist if only ra/dec/distance_ly were provided.
                var s = (s0.x.HasValue && s0.y.HasValue && s0.z.HasValue) ? s0 : CoerceCoordinates(s0);

                // Normalize spectral fields and packed code.
                ushort packed = PackSpectral(s.spectral);
                var (k, t, sub, l) = UnpackSpectral(packed);

                // Flat Key: Value output
                Console.WriteLine($"hip: {s.hip}");
                Console.WriteLine($"x: {s.x!.Value.ToString("R", CultureInfo.InvariantCulture)}");
                Console.WriteLine($"y: {s.y!.Value.ToString("R", CultureInfo.InvariantCulture)}");
                Console.WriteLine($"z: {s.z!.Value.ToString("R", CultureInfo.InvariantCulture)}");
                Console.WriteLine($"abs_mag: {s.abs_mag.ToString("R", CultureInfo.InvariantCulture)}");
                Console.WriteLine($"spectral_packed: 0x{packed:X4}");
                Console.WriteLine($"spectral_kind: {k}");
                Console.WriteLine($"spectral_type: {t}");
                Console.WriteLine($"spectral_subtype: {sub}");
                Console.WriteLine($"spectral_lum: {l}");
                Console.WriteLine(); // blank line between entries
            }
        }
        #endregion

        #region Routines
        static List<StarYaml> LoadStars(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".dat" => ReadStarsFromBinary(path),
                ".yaml" or ".yml" => ReadStarsFromYaml(path),
                _ => throw new InvalidDataException("Unsupported input. Use .dat or .yaml.")
            };
        }
        static List<StarYaml> ReadStarsFromBinary(string binaryPath)
        {
            using FileStream fs = File.OpenRead(binaryPath);
            using BinaryReader br = new(fs);

            // Header
            byte[] magicBytes = br.ReadBytes(8);
            string magic = System.Text.Encoding.ASCII.GetString(magicBytes);
            if (magic != Magic) 
                throw new InvalidDataException("Bad magic");
            ushort ver = br.ReadUInt16();
            if (ver != Version0100) 
                throw new InvalidDataException($"Unsupported version 0x{ver:X4}");
            uint count = br.ReadUInt32();

            // Body
            List<StarYaml> stars = new((int)count);
            for (int i = 0; i < count; i++)
            {
                uint hip = br.ReadUInt32();
                float x = br.ReadSingle();
                float y = br.ReadSingle();
                float z = br.ReadSingle();
                short abs256 = br.ReadInt16();
                ushort spec = br.ReadUInt16();

                (int k, int t, int s, int l) = UnpackSpectral(spec);

                stars.Add(new StarYaml
                {
                    hip = hip,
                    x = x,
                    y = y,
                    z = z,
                    abs_mag = abs256 / 256.0,
                    spectral = new SpectralYaml
                    {
                        packed = spec.ToString("X4"),
                        kind = k,
                        type = t,
                        subtype = s,
                        lum = l
                    }
                });
            }
            return stars;
        }
        static List<StarYaml> ReadStarsFromYaml(string yamlPath)
        {
            IDeserializer deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            RootYaml root = deserializer.Deserialize<RootYaml>(File.ReadAllText(yamlPath));
            return root.stars ?? [];
        }
        static void PrintHelp()
        {
            Console.WriteLine("""
                StarDataBaseConverter
                Usage:
                  StarDataBaseConverter --to-yaml   <input.dat> <output.yaml>
                  StarDataBaseConverter --to-binary <input.yaml> <output.dat>
                  StarDataBaseConverter --print    <input.dat|input.yaml>
                  StarDataBaseConverter --version
                  StarDataBaseConverter --help
                
                Notes:
                  - Supports binary version 0x0100 only.
                  - Binary is little-endian.
                  - YAML accepts x/y/z or ra/dec/distance_ly. If RA/Dec provided, XYZ are computed using ε=23.4392911°.
                """);
        }
        #endregion

        #region Helpers
        static bool Has(string[] args, params string[] flags) =>
            args.Any(a => flags.Contains(a, StringComparer.OrdinalIgnoreCase));
        static (string input, string output) ExtractIOArguments(string[] args, int startIndex)
        {
            string[] rest = [.. args.Skip(startIndex)];
            if (rest.Length < 2) 
                throw new ArgumentException("Require input and output paths.");
            return (rest[0], rest[1]);
        }
        static ushort ParseVersion(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) 
                return 0;
            v = v.Trim();
            if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return ushort.Parse(v.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (ushort.TryParse(v, out ushort u)) 
                return u;
            return ushort.Parse(v, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        static StarYaml CoerceCoordinates(StarYaml s)
        {
            // If x/y/z present, use them. Otherwise compute from RA/Dec/Distance using the matrix in the prompt.
            if (s.x.HasValue && s.y.HasValue && s.z.HasValue) return s;
            if (!(s.ra.HasValue && s.dec.HasValue && s.distance_ly.HasValue))
                throw new InvalidDataException("Provide either x/y/z or ra/dec/distance_ly.");

            double d = s.distance_ly!.Value;
            double theta = (s.ra!.Value + 180.0) * Deg2Rad;
            double phi = (s.dec!.Value - 90.0) * Deg2Rad;
            double eps = ObliquityDeg * Deg2Rad;

            // Column vector v = ( d*cosθ*sinφ, d*cosφ, -d*sinθ*sinφ )
            double vx = d * Math.Cos(theta) * Math.Sin(phi);
            double vy = d * Math.Cos(phi);
            double vz = -d * Math.Sin(theta) * Math.Sin(phi);

            // Apply rotation about X by +ε:
            // [1 0 0; 0 cosε sinε; 0 -sinε cosε] * v
            double cy = Math.Cos(eps);
            double sy = Math.Sin(eps);
            double x = vx;
            double y = vy * cy + vz * sy;
            double z = -vy * sy + vz * cy;

            s.x = (float)x;
            s.y = (float)y;
            s.z = (float)z;
            return s;
        }
        static ushort PackSpectral(SpectralYaml sp)
        {
            if (!string.IsNullOrWhiteSpace(sp.packed))
            {
                string t = sp.packed!.Trim();
                if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
                return ushort.Parse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            int k = Clamp(sp.kind ?? 0, 0, 3);
            int tval = Clamp(sp.type ?? 0, 0, 15); // 0..15 normal, 16..23 WD allowed if given
            int s = Clamp(MapAlpha(sp.subtype ?? 0), 0, 10); // 0..9, 10=unknown
            int l = Clamp(sp.lum ?? 8, 0, 8); // 0..7, 8=unknown

            int packed = (k << 12) | (tval << 8) | (s << 4) | l;
            return (ushort)packed;
        }
        static (int k, int t, int s, int l) UnpackSpectral(ushort spec)
        {
            int l = spec & 0xF;
            int s = (spec >> 4) & 0xF;
            int t = (spec >> 8) & 0xF;
            int k = (spec >> 12) & 0xF;
            return (k, t, s, l);
        }
        /// <summary>
        /// Allow 10 for 'a' unknown subtype
        /// </summary>
        static int MapAlpha(int value)
        {
            // Accept already-numeric 0..10
            return value;
        }
        static int Clamp(int v, int lo, int hi) 
            => Math.Max(lo, Math.Min(hi, v));
        #endregion
    }
}
