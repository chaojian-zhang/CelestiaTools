using Assimp.Configs;
using Assimp;
using static CelestiaCMODConverter.Configurations;
using static CelestiaCMODConverter.Definitions;
using System.Globalization;
using System.Text;

namespace CelestiaCMODConverter
{
    internal class Program
    {
        private enum ModelKind { Assimp, CmodAscii, CmodBinary }

        #region Entry
        static int Main(string[] args)
        {
            if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
            {
                PrintHelp();
                return 0;
            }
            if (args.Contains("--version") || args.Contains("-v"))
            {
                Console.WriteLine($"{TOOL_NAME} {TOOL_VERSION}");
                return 0;
            }

            string cmd = args[0].ToLowerInvariant();
            try
            {
                switch (cmd)
                {
                    case "inspect":
                        {
                            string modelPath = RequireArg(args, "--input", 1);
                            Inspect(modelPath);
                            return 0;
                        }
                    case "convert":
                        {
                            string input = RequireArg(args, "--input");
                            string output = RequireArg(args, "--output");
                            string fmt = RequireArg(args, "--format").ToLowerInvariant(); // ascii | bin
                            float scale = ParseFloatArg(args, "--scale", 1.0f);
                            ConvertToCmod(input, output, fmt, scale);
                            Console.WriteLine($"Wrote: {output}");
                            return 0;
                        }
                    default:
                        Console.Error.WriteLine("Unknown command.");
                        PrintHelp();
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
        #endregion

        #region Commands
        private static void Inspect(string modelPath)
        {
            // CMOD?
            ModelKind kind = ProbeModelKind(modelPath);
            if (kind == ModelKind.CmodAscii || kind == ModelKind.CmodBinary)
            {
                CmodModel cm = kind == ModelKind.CmodAscii 
                    ? CMODReader.ReadCmodAscii(modelPath) 
                    : CMODReader.ReadCmodBinary(modelPath);
                Console.WriteLine($"Model loaded: {modelPath}");
                Console.WriteLine($"Type: {kind}");
                Console.WriteLine($"Meshes: {cm.Meshes.Count}");
                Console.WriteLine($"Materials: {cm.Materials.Count}");
                Console.WriteLine($"Animations: 0");
                Console.WriteLine($"Textures: {cm.Materials.Count(m => m.TexDiffuse != null || m.TexNormal != null || m.TexSpecular != null || m.TexEmissive != null)}");
                Console.WriteLine();

                for (int i = 0; i < cm.Meshes.Count; i++)
                {
                    CmodMesh m = cm.Meshes[i];
                    bool hasNormal = m.VertexDesc.Any(v => v.Semantic == VS_normal);
                    bool hasTangent = m.VertexDesc.Any(v => v.Semantic == VS_tangent);
                    bool hasColor0 = m.VertexDesc.Any(v => v.Semantic == VS_color0);
                    bool hasUV0 = m.VertexDesc.Any(v => v.Semantic == VS_texcoord0);
                    bool hasUV1 = m.VertexDesc.Any(v => v.Semantic == VS_texcoord1);

                    Console.WriteLine($"--- Mesh {i} ---");
                    Console.WriteLine($"Vertices: {m.VertexCount} Primitives: {m.Trilists.Count}");
                    Console.WriteLine($"Normals: {(hasNormal ? "Yes" : "No")}, Tangents: {(hasTangent ? "Yes" : "No")}");
                    Console.WriteLine($"UV0: {(hasUV0 ? "Yes" : "No")}  UV1: {(hasUV1 ? "Yes" : "No")}");
                    Console.WriteLine($"Colors0: {(hasColor0 ? "Yes" : "No")}");
                    Console.WriteLine($"MaterialIndex: {string.Join(", ", m.Trilists.Select(t => t.MaterialIndex))}");
                    Console.WriteLine("VertexDesc: " + string.Join(", ", m.VertexDesc.Select(v => $"{CMODWriter.AsciiSem(v.Semantic)} {CMODWriter.AsciiFmt(v.Format)}")));
                    Console.WriteLine();
                }

                for (int i = 0; i < cm.Materials.Count; i++)
                {
                    CmodMaterial mat = cm.Materials[i];
                    Console.WriteLine($"--- Material {i} ---");
                    Console.WriteLine($"Diffuse: {CMODWriter.Fmt(mat.Diffuse)}  Specular: {CMODWriter.Fmt(mat.Specular)}  Emissive: {CMODWriter.Fmt(mat.Emissive)}");
                    Console.WriteLine($"Opacity: {mat.Opacity}  Shininess: {mat.SpecPower}");
                    Console.WriteLine($"Tex(Diffuse): {mat.TexDiffuse ?? "(none)"}");
                    Console.WriteLine($"Tex(Normal): {mat.TexNormal ?? "(none)"}");
                    Console.WriteLine($"Tex(Specular): {mat.TexSpecular ?? "(none)"}");
                    Console.WriteLine($"Tex(Emissive): {mat.TexEmissive ?? "(none)"}");
                    Console.WriteLine();
                }
                return;
            }

            // Fallback to Assimp
            Scene scene = LoadScene(modelPath);
            Console.WriteLine($"Model loaded: {modelPath}");
            Console.WriteLine($"Type: {kind}");
            Console.WriteLine($"Meshes: {scene.MeshCount}");
            Console.WriteLine($"Materials: {scene.MaterialCount}");
            Console.WriteLine($"Animations: {scene.AnimationCount}");
            Console.WriteLine($"Textures: {scene.TextureCount}");
            Console.WriteLine();

            for (int i = 0; i < scene.MeshCount; i++)
            {
                Mesh m = scene.Meshes[i];
                Console.WriteLine($"--- Mesh {i} ---");
                Console.WriteLine($"Name: {m.Name}");
                Console.WriteLine($"Vertices: {m.VertexCount} Faces: {m.FaceCount}");
                Console.WriteLine($"Normals: {(m.HasNormals ? "Yes" : "No")}, Tangents: {(m.Tangents?.Count > 0 ? "Yes" : "No")}");
                Console.WriteLine($"UV0: {(m.HasTextureCoords(0) ? "Yes" : "No")}  UV1: {(m.HasTextureCoords(1) ? "Yes" : "No")}");
                Console.WriteLine($"Colors0: {(m.HasVertexColors(0) ? "Yes" : "No")}");
                Console.WriteLine($"MaterialIndex: {m.MaterialIndex}");
                Console.WriteLine();
            }

            for (int i = 0; i < scene.MaterialCount; i++)
            {
                Material mat = scene.Materials[i];
                Console.WriteLine($"--- Material {i} ---");
                Console.WriteLine($"Name: {mat.Name}");
                Console.WriteLine($"Diffuse: {CMODWriter.Format(mat.ColorDiffuse)}  Specular: {CMODWriter.Format(mat.ColorSpecular)}  Emissive: {CMODWriter.Format(mat.ColorEmissive)}");
                Console.WriteLine($"Opacity: {mat.Opacity}  Shininess: {mat.Shininess}");
                Console.WriteLine($"Tex(Diffuse): {TryGetTexturePath(mat, TextureType.Diffuse)}");
                Console.WriteLine($"Tex(Normal): {TryGetTexturePath(mat, TextureType.Normals)}");
                Console.WriteLine($"Tex(Specular): {TryGetTexturePath(mat, TextureType.Specular)}");
                Console.WriteLine($"Tex(Emissive): {TryGetTexturePath(mat, TextureType.Emissive)}");
                Console.WriteLine();
            }
        }
        private static void ConvertToCmod(string input, string output, string targetFormat, float scale)
        {
            // CMOD?
            ModelKind kind = ProbeModelKind(input);
            if (kind == ModelKind.CmodAscii || kind == ModelKind.CmodBinary)
            {
                CmodModel cm = kind == ModelKind.CmodAscii
                    ? CMODReader.ReadCmodAscii(input)
                    : CMODReader.ReadCmodBinary(input);
                if (targetFormat == "ascii")
                    CMODWriter.WriteAscii(cm, output, scale);
                else if (targetFormat is "bin" or "binary")
                    CMODWriter.WriteBinary(cm, output, scale);
                else throw new ArgumentException("Unknown format. Use ascii or bin.");
            }
            // Other asset types
            else
            {
                Scene scene = LoadScene(input);
                if (targetFormat == "ascii")
                    CMODWriter.WriteAscii(scene, output, scale);
                else if (targetFormat is "bin" or "binary")
                    CMODWriter.WriteBinary(scene, output, scale);
                else throw new ArgumentException("Unknown format. Use ascii or bin.");
            }
        }
        #endregion

        #region Routines
        /// <summary>
        /// Load with Assimp
        /// </summary>
        private static Scene LoadScene(string modelPath)
        {
            // Create an importer
            AssimpContext importer = new();

            // Optional: apply configuration (triangulate meshes, join identical vertices, etc.)
            importer.SetConfig(new NormalSmoothingAngleConfig(66.0f));
            PostProcessSteps flags =
                PostProcessSteps.Triangulate |
                PostProcessSteps.JoinIdenticalVertices |
                PostProcessSteps.GenerateSmoothNormals |
                PostProcessSteps.CalculateTangentSpace |
                PostProcessSteps.ImproveCacheLocality |
                PostProcessSteps.OptimizeMeshes |
                PostProcessSteps.OptimizeGraph;

            // Import the model
            Scene scene = importer.ImportFile(modelPath, flags);
            importer.Dispose();
            if (scene == null || !scene.HasMeshes) 
                throw new InvalidOperationException("Failed to load model or no meshes found.");
            return scene;
        }
        private static void PrintHelp()
        {
            Console.WriteLine($"{TOOL_NAME} {TOOL_VERSION}");
            Console.WriteLine("Usage:");
            Console.WriteLine("  inspect --input <model>");
            Console.WriteLine("  convert --input <model> --output <file.cmod> --format <ascii|bin> [--scale <float>]");
            Console.WriteLine("  --help | -h");
            Console.WriteLine("  --version | -v");
            Console.WriteLine();
            Console.WriteLine("Notes:");
            Console.WriteLine("  - Triangulates and writes trilist groups.");
            Console.WriteLine("  - Vertex layout auto-detects: position, normal, tangent, color0, uv0..uv3.");
            Console.WriteLine("  - Materials: diffuse/specular/emissive/specpower/opacity and textures 0..3.");
        }
        #endregion

        #region Helpers
        private static ModelKind ProbeModelKind(string path)
        {
            using FileStream fs = File.OpenRead(path);
            Span<byte> hdr = stackalloc byte[16];
            int n = fs.Read(hdr);
            if (n >= 16)
            {
                string header = Encoding.ASCII.GetString(hdr[..16]);
                if (header == ASCII_HEADER) 
                    return ModelKind.CmodAscii;
                else if (header == BINARY_HEADER) 
                    return ModelKind.CmodBinary;
            }
            return ModelKind.Assimp;
        }
        private static string RequireArg(string[] args, string name, int minIndexIfPositional = -1)
        {
            int i = Array.IndexOf(args, name);
            if (i >= 0 && i + 1 < args.Length) 
                return args[i + 1];
            if (minIndexIfPositional >= 0 && args.Length > minIndexIfPositional) 
                return args[minIndexIfPositional];
            throw new ArgumentException($"Missing {name}");
        }
        private static float ParseFloatArg(string[] args, string name, float def)
        {
            int i = Array.IndexOf(args, name);
            if (i >= 0 && i + 1 < args.Length && float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                return v;
            return def;
        }
        private static string TryGetTexturePath(Material m, TextureType t)
            => m.GetMaterialTextureCount(t) > 0 && m.GetMaterialTexture(t, 0, out TextureSlot tx) 
                ? tx.FilePath 
                : "(none)";
        #endregion
    }
}
