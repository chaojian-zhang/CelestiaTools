using Assimp;
using System.Globalization;
using static CelestiaCMODConverter.Definitions;
using System.Text;

namespace CelestiaCMODConverter
{
    public sealed class CmodModel
    {
        public List<CmodMaterial> Materials { get; } = [];
        public List<CmodMesh> Meshes { get; } = [];
    }
    public sealed class CmodMaterial
    {
        public Color3D Diffuse;
        public Color3D Specular;
        public Color3D Emissive;
        public float SpecPower;
        public float Opacity = 1f;
        public string? TexDiffuse, TexNormal, TexSpecular, TexEmissive;
    }
    /// <summary>
    /// Triangle list, a kind of CmodPrimitive.
    /// </summary>
    /// <remarks>   
    /// Notice we don't use inheritance to avoid unnecesary virtual tables.
    /// </remarks>
    public sealed class CmodTrilist
    {
        public int MaterialIndex;
        /// <remarks>
        /// A single Trilist can define whole bunch of triangles
        /// </remarks>
        public uint[] Indices = [];
    }
    public sealed class CmodMesh
    {
        public List<(ushort Semantic, ushort Format)> VertexDesc { get; } = [];
        public int VertexCount;
        /// <summary>
        /// Float stream for f* attrs; UB4 colors kept as floats 0..1
        /// </summary>
        public float[] VertexData = [];
        /// <summary>
        /// Optional packed colors when format is ub4
        /// </summary>
        public byte[]? ColorUB4;

        #region Primitives
        /// <remarks>
        /// Notice a primitive in CMOD can store lots of indices;
        /// New primitives are needed only for new material definitions.
        /// </remarks>
        public List<CmodTrilist> Trilists = [];
        #endregion
    }

    public static class CMODReader
    {
        /// <summary>
        /// ASCII CMOD reader
        /// </summary>
        public static CmodModel ReadCmodAscii(string path)
        {
            using StreamReader reader = new(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            string? line = reader.ReadLine();
            if (line == null || !line.StartsWith(ASCII_HEADER, StringComparison.Ordinal))
                throw new InvalidDataException("Bad CMOD ASCII header.");

            CmodModel model = new(); // A single model file may contain multiple meshes
            CmodMaterial? currentMaterial = null; // The material being parsed
            CmodMesh? currentMesh = null;
            int expectVerts = 0;
            int writtenVerts = 0;
            // Build a vertex writer based on current desc
            List<(ushort Semantic, ushort DataFormat)> desc = [];
            int floatsPerVertex = 0;
            bool useUB4 = false; // Four unsigned bytes (the usual format for colors)

            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0 || line.StartsWith("#")) 
                    continue;

                string[] tokens = SplitTokens(line);
                switch (tokens[0])
                {
                    case "material":
                        currentMaterial = new CmodMaterial();
                        model.Materials.Add(currentMaterial);
                        break;
                    case "end_material":
                        currentMaterial = null;
                        break;
                    case "diffuse":
                        Ensure(currentMaterial != null, "diffuse outside material");
                        currentMaterial!.Diffuse = new Color3D(ParseF(tokens, 1), ParseF(tokens, 2), ParseF(tokens, 3));
                        break;
                    case "specular":
                        Ensure(currentMaterial != null, "specular outside material");
                        currentMaterial!.Specular = new Color3D(ParseF(tokens, 1), ParseF(tokens, 2), ParseF(tokens, 3));
                        break;
                    case "emissive":
                        Ensure(currentMaterial != null, "emissive outside material");
                        currentMaterial!.Emissive = new Color3D(ParseF(tokens, 1), ParseF(tokens, 2), ParseF(tokens, 3));
                        break;
                    case "specpower":
                        Ensure(currentMaterial != null, "specpower outside material");
                        currentMaterial!.SpecPower = ParseF(tokens, 1);
                        break;
                    case "opacity":
                        Ensure(currentMaterial != null, "opacity outside material");
                        currentMaterial!.Opacity = ParseF(tokens, 1);
                        break;
                    case "texture0":
                        Ensure(currentMaterial != null, "texture0 outside material");
                        currentMaterial!.TexDiffuse = Unquote(tokens[1]);
                        break;
                    case "normalmap":
                        Ensure(currentMaterial != null, "normalmap outside material");
                        currentMaterial!.TexNormal = Unquote(tokens[1]);
                        break;
                    case "specularmap":
                        Ensure(currentMaterial != null, "specularmap outside material");
                        currentMaterial!.TexSpecular = Unquote(tokens[1]);
                        break;
                    case "emissivemap":
                        Ensure(currentMaterial != null, "emissivemap outside material");
                        currentMaterial!.TexEmissive = Unquote(tokens[1]);
                        break;

                    case "mesh":
                        currentMesh = new CmodMesh();
                        model.Meshes.Add(currentMesh);
                        desc.Clear();
                        currentMesh.VertexDesc.Clear();
                        floatsPerVertex = 0;
                        useUB4 = false;
                        expectVerts = 0;
                        writtenVerts = 0;
                        break;
                    case "end_mesh":
                        currentMesh = null;
                        break;
                    case "vertexdesc":
                        // read lines until end_vertexdesc
                        while ((line = reader.ReadLine()) != null)
                        {
                            line = line.Trim();
                            if (line.Length == 0) continue;
                            if (line == "end_vertexdesc") break;
                            string[] tt = SplitTokens(line);
                            ushort semantic = tt[0] switch
                            {
                                "position" => VS_position,
                                "color0" => VS_color0,
                                "color1" => VS_color1,
                                "normal" => VS_normal,
                                "tangent" => VS_tangent,
                                "texcoord0" => VS_texcoord0,
                                "texcoord1" => VS_texcoord1,
                                "texcoord2" => VS_texcoord2,
                                "texcoord3" => VS_texcoord3,
                                "pointsize" => VS_pointsize,
                                _ => throw new InvalidDataException($"Unknown semantic {tt[0]}")
                            };
                            ushort dataFormat = tt[1] switch
                            {
                                "f1" => VF_f1,
                                "f2" => VF_f2,
                                "f3" => VF_f3,
                                "f4" => VF_f4,
                                "ub4" => VF_ub4,
                                _ => throw new InvalidDataException($"Unknown format {tt[1]}")
                            };
                            desc.Add((semantic, dataFormat));
                            if (dataFormat == VF_ub4) 
                                useUB4 = true;
                            else
                                floatsPerVertex += dataFormat switch { VF_f1 => 1, VF_f2 => 2, VF_f3 => 3, VF_f4 => 4, _ => 0 };
                        }
                        Ensure(currentMesh != null, "vertexdesc outside mesh");
                        currentMesh!.VertexDesc.AddRange(desc);
                        break;
                    case "vertices":
                        Ensure(currentMesh != null, "vertices outside mesh");
                        expectVerts = int.Parse(tokens[1], CultureInfo.InvariantCulture);
                        currentMesh!.VertexCount = expectVerts;
                        currentMesh.VertexData = new float[expectVerts * floatsPerVertex];
                        if (useUB4) currentMesh.ColorUB4 = new byte[expectVerts * 4];

                        // Read next expectVerts lines
                        int floatPos = 0;
                        int colorPos = 0;
                        for (int v = 0; v < expectVerts; v++)
                        {
                            string? vl;
                            do { vl = reader.ReadLine(); if (vl == null) 
                                    throw new InvalidDataException("Unexpected EOF in vertices"); vl = vl.Trim(); } while (vl.Length == 0);
                            string[] parts = SplitTokens(vl);
                            int p = 0;
                            foreach ((ushort sem, ushort fmt) in desc)
                            {
                                if (fmt == VF_ub4)
                                {
                                    // Expecting 4 floats 0..1 or 4 ints 0..255; CMOD ASCII uses float channel, keep as bytes 0..255
                                    byte r = ParseAsByte(parts[p++]); byte g = ParseAsByte(parts[p++]);
                                    byte b = ParseAsByte(parts[p++]); byte a = ParseAsByte(parts[p++]);
                                    currentMesh.ColorUB4![colorPos++] = r;
                                    currentMesh.ColorUB4![colorPos++] = g;
                                    currentMesh.ColorUB4![colorPos++] = b;
                                    currentMesh.ColorUB4![colorPos++] = a;
                                }
                                else
                                {
                                    int count = fmt switch { VF_f1 => 1, VF_f2 => 2, VF_f3 => 3, VF_f4 => 4, _ => 0 };
                                    for (int k = 0; k < count; k++) 
                                        currentMesh.VertexData[floatPos++] = float.Parse(parts[p++], CultureInfo.InvariantCulture);
                                }
                            }
                            writtenVerts++;
                        }
                        Ensure(writtenVerts == expectVerts, "vertex count mismatch");
                        break;
                    case "trilist":
                        {
                            Ensure(currentMesh != null, "trilist outside mesh");

                            // Parse trilist
                            CmodTrilist trilist = new()
                            {
                                MaterialIndex = int.Parse(tokens[1], CultureInfo.InvariantCulture)
                            };
                            int vertexCount = int.Parse(tokens[2], CultureInfo.InvariantCulture);
                            List<uint> indices = new(vertexCount);
                            while (indices.Count < vertexCount)
                            {
                                string? il = reader.ReadLine();
                                if (il == null) 
                                    throw new InvalidDataException("Unexpected EOF in indices");
                                il = il.Trim();
                                if (il.Length == 0) 
                                    continue;
                                foreach (string s in SplitTokens(il))
                                {
                                    indices.Add(uint.Parse(s, CultureInfo.InvariantCulture));
                                    if (indices.Count == vertexCount)
                                        break;
                                }
                            }
                            trilist.Indices = [.. indices];

                            // Add to mesh
                            currentMesh.Trilists.Add(trilist);
                        }
                        break;
                    default:
                        throw new InvalidDataException($"Unknown token: {tokens[0]}");
                }
            }

            return model;
        }
        /// <summary>
        /// Binary CMOD reader
        /// </summary>
        public static CmodModel ReadCmodBinary(string path)
        {
            using FileStream fs = new(path, FileMode.Open, FileAccess.Read);
            using BinaryReader br = new(fs, Encoding.ASCII, leaveOpen: false);

            string header = Encoding.ASCII.GetString(br.ReadBytes(16));
            if (header != BINARY_HEADER) throw new InvalidDataException("Bad CMOD binary header.");

            CmodModel model = new();

            // helper lambdas
            ushort U16() => br.ReadUInt16();          // LE by BinaryReader
            uint U32() => br.ReadUInt32();
            float F32() => br.ReadSingle();

            while (fs.Position < fs.Length)
            {
                ushort tk = U16();
                switch (tk)
                {
                    case TK_material:
                        {
                            CmodMaterial m = new();
                            while (true)
                            {
                                ushort sub = U16();
                                if (sub == TK_end_material) { model.Materials.Add(m); break; }
                                if (sub == TK_diffuse) { Expect(DT_color, U16()); m.Diffuse = new Color3D(F32(), F32(), F32()); }
                                else if (sub == TK_specular) { Expect(DT_color, U16()); m.Specular = new Color3D(F32(), F32(), F32()); }
                                else if (sub == TK_emissive) { Expect(DT_color, U16()); m.Emissive = new Color3D(F32(), F32(), F32()); }
                                else if (sub == TK_specpower) { Expect(DT_float1, U16()); m.SpecPower = F32(); }
                                else if (sub == TK_opacity) { Expect(DT_float1, U16()); m.Opacity = F32(); }
                                else if (sub == TK_texture)
                                {
                                    ushort sem = U16();
                                    Expect(DT_string, U16());
                                    ushort len = U16();
                                    string pathStr = Encoding.ASCII.GetString(br.ReadBytes(len));
                                    switch (sem)
                                    {
                                        case TEX_diffuse: m.TexDiffuse = pathStr; break;
                                        case TEX_normal: m.TexNormal = pathStr; break;
                                        case TEX_specular: m.TexSpecular = pathStr; break;
                                        case TEX_emissive: m.TexEmissive = pathStr; break;
                                        default: /* ignore unknown */ break;
                                    }
                                }
                                else { /* unknown material token -> skip not expected in spec */ }
                            }
                            break;
                        }
                    case TK_mesh:
                        {
                            CmodMesh mesh = new();
                            while (true)
                            {
                                ushort sub = U16();
                                if (sub == TK_end_mesh) 
                                { 
                                    model.Meshes.Add(mesh);
                                    break; 
                                }

                                if (sub == TK_vertexdesc)
                                {
                                    while (true)
                                    {
                                        ushort sem = U16();
                                        if (sem == TK_end_vertexdesc) 
                                            break;
                                        ushort fmt = U16();
                                        mesh.VertexDesc.Add((sem, fmt));
                                    }
                                }
                                else if (sub == TK_vertices)
                                {
                                    int vcount = checked((int)U32());
                                    mesh.VertexCount = vcount;

                                    int floatsPerV = 0;
                                    bool hasUB4 = mesh.VertexDesc.Any(d => d.Format == VF_ub4);
                                    foreach ((ushort Semantic, ushort Format) d in mesh.VertexDesc)
                                        if (d.Format != VF_ub4) floatsPerV += d.Format switch { VF_f1 => 1, VF_f2 => 2, VF_f3 => 3, VF_f4 => 4, _ => 0 };

                                    mesh.VertexData = new float[vcount * floatsPerV];
                                    if (hasUB4) mesh.ColorUB4 = new byte[vcount * 4];

                                    int fp = 0, cp = 0;
                                    for (int v = 0; v < vcount; v++)
                                    {
                                        foreach ((ushort Semantic, ushort Format) d in mesh.VertexDesc)
                                        {
                                            if (d.Format == VF_ub4)
                                            {
                                                mesh.ColorUB4![cp++] = br.ReadByte();
                                                mesh.ColorUB4![cp++] = br.ReadByte();
                                                mesh.ColorUB4![cp++] = br.ReadByte();
                                                mesh.ColorUB4![cp++] = br.ReadByte();
                                            }
                                            else
                                            {
                                                int n = d.Format switch { VF_f1 => 1, VF_f2 => 2, VF_f3 => 3, VF_f4 => 4, _ => 0 };
                                                for (int k = 0; k < n; k++) mesh.VertexData[fp++] = F32();
                                            }
                                        }
                                    }
                                }
                                else if (sub == PR_trilist)
                                {
                                    CmodTrilist trilist = new();
                                    trilist.MaterialIndex = checked((int)U32());
                                    int count = checked((int)U32());
                                    trilist.Indices = new uint[count];
                                    for (int i = 0; i < count; i++)
                                        trilist.Indices[i] = U32();
                                    mesh.Trilists.Add(trilist);
                                }
                                else
                                {
                                    // Unknown sub-chunk: unsupported in this tool
                                    throw new InvalidDataException($"Unknown mesh token {sub}");
                                }
                            }
                            break;
                        }
                    default:
                        throw new InvalidDataException($"Unknown top-level token {tk}");
                }
            }

            return model;

            static void Expect(ushort expected, ushort got)
            {
                if (expected != got) throw new InvalidDataException($"Datatype mismatch. Expected {expected}, got {got}");
            }
        }

        #region Helpers
        /// <summary>
        /// ASCII parsing helper.
        /// </summary>
        private static string[] SplitTokens(string line)
        {
            List<string> list = new();
            int i = 0;
            while (i < line.Length)
            {
                while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
                if (i >= line.Length) break;
                if (line[i] == '"')
                {
                    int j = i + 1;
                    while (j < line.Length && line[j] != '"') j++;
                    if (j >= line.Length) throw new InvalidDataException("Unclosed quote");
                    list.Add(line.Substring(i, j - i + 1));
                    i = j + 1;
                }
                else
                {
                    int j = i;
                    while (j < line.Length && !char.IsWhiteSpace(line[j])) j++;
                    list.Add(line.Substring(i, j - i));
                    i = j;
                }
            }
            return list.ToArray();
        }
        private static float ParseF(string[] t, int idx) => float.Parse(t[idx], CultureInfo.InvariantCulture);
        private static string Unquote(string s) => s.Length >= 2 && s[0] == '"' && s[^1] == '"' ? s[1..^1] : s;
        private static void Ensure(bool cond, string message) { if (!cond) throw new InvalidDataException(message); }
        private static byte ParseAsByte(string s)
        {
            if (s.IndexOf('.') >= 0) { float f = float.Parse(s, CultureInfo.InvariantCulture); return CMODWriter.Clamp255(f); }
            int v = int.Parse(s, CultureInfo.InvariantCulture);
            return (byte)Math.Clamp(v, 0, 255);
        }
        #endregion
    }
}
