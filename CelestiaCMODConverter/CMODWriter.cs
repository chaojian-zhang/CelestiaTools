using Assimp;
using System.Globalization;
using System.Text;
using static CelestiaCMODConverter.Definitions;

namespace CelestiaCMODConverter
{
    public static class CMODWriter
    {
        #region Methods
        public static void WriteAscii(Scene scene, string path, float scale)
        {
            using StreamWriter sw = new(path, false, new UTF8Encoding(false));
            sw.WriteLine(ASCII_HEADER);

            // Materials
            for (int i = 0; i < scene.MaterialCount; i++)
            {
                Material mat = scene.Materials[i];
                sw.WriteLine("material");
                if (!IsZero(mat.ColorDiffuse)) sw.WriteLine($"diffuse {F4(mat.ColorDiffuse)}");
                if (!IsZero(mat.ColorSpecular)) sw.WriteLine($"specular {F4(mat.ColorSpecular)}");
                if (!IsZero(mat.ColorEmissive)) sw.WriteLine($"emissive {F4(mat.ColorEmissive)}");
                if (mat.Shininess > 0) sw.WriteLine(FormattableString.Invariant($"specpower {mat.Shininess:F6}"));
                if (mat.Opacity != 0 && mat.Opacity != 1) sw.WriteLine(FormattableString.Invariant($"opacity {mat.Opacity:F6}"));

                if (mat.GetMaterialTextureCount(TextureType.Diffuse) > 0 && mat.GetMaterialTexture(TextureType.Diffuse, 0, out TextureSlot td))
                    sw.WriteLine($"texture0 \"{TrimTexturePath(td.FilePath)}\"");
                if (mat.GetMaterialTextureCount(TextureType.Normals) > 0 && mat.GetMaterialTexture(TextureType.Normals, 0, out TextureSlot tn))
                    sw.WriteLine($"normalmap \"{TrimTexturePath(tn.FilePath)}\"");
                if (mat.GetMaterialTextureCount(TextureType.Specular) > 0 && mat.GetMaterialTexture(TextureType.Specular, 0, out TextureSlot ts))
                    sw.WriteLine($"specularmap \"{TrimTexturePath(ts.FilePath)}\"");
                if (mat.GetMaterialTextureCount(TextureType.Emissive) > 0 && mat.GetMaterialTexture(TextureType.Emissive, 0, out TextureSlot te))
                    sw.WriteLine($"emissivemap \"{TrimTexturePath(te.FilePath)}\"");

                // Default blend omitted (normal). Additive if opacity < 1 and emissive present can be user decision; omitted here.
                sw.WriteLine("end_material");
                sw.WriteLine();
            }

            // Meshes
            foreach (Mesh? mesh in scene.Meshes)
            {
                VAttr[] layout = DecideLayout(mesh);
                sw.WriteLine("mesh");
                sw.WriteLine("vertexdesc");
                foreach (VAttr a in layout) sw.WriteLine($"{AsciiSem(a.Semantic)} {AsciiFmt(a.Format)}");
                sw.WriteLine("end_vertexdesc");

                sw.WriteLine($"vertices {mesh.VertexCount}");
                for (int v = 0; v < mesh.VertexCount; v++)
                {
                    StringBuilder sb = new();
                    foreach (VAttr a in layout)
                    {
                        if (a.Semantic == VS_position) AppendFloats(sb, mesh.Vertices[v].X * scale, mesh.Vertices[v].Y * scale, mesh.Vertices[v].Z * scale);
                        else if (a.Semantic == VS_normal) AppendFloats(sb, mesh.Normals[v].X, mesh.Normals[v].Y, mesh.Normals[v].Z);
                        else if (a.Semantic == VS_tangent) AppendFloats(sb, mesh.Tangents[v].X, mesh.Tangents[v].Y, mesh.Tangents[v].Z);
                        else if (a.Semantic == VS_color0)
                        {
                            Color4D c = mesh.VertexColorChannels[0][v]; // floats 0..1
                            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} {3} ", c.R, c.G, c.B, c.A);
                        }
                        else if (a.Semantic >= VS_texcoord0 && a.Semantic <= VS_texcoord3)
                        {
                            int ch = a.Semantic - VS_texcoord0;
                            Vector3D uv = mesh.TextureCoordinateChannels[ch][v];
                            AppendFloats(sb, uv.X, uv.Y);
                        }
                    }
                    sw.WriteLine(sb.ToString().TrimEnd());
                }

                // Primitive groups: write trilist per material
                // Build faces by material index
                IGrouping<int, (Face f, int idx)>[] groups = mesh.Faces
                    .Select((f, idx) => (f, idx))
                    .GroupBy(t => mesh.MaterialIndex) // single material per mesh in Assimp; but keep structure
                    .ToArray();

                // CMOD wants: <prim_type> <material_index> <count> then indices
                // We output one group for all faces with the mesh's material
                List<uint> idxList = [];
                foreach (Face? f in mesh.Faces)
                {
                    if (f.IndexCount != 3)
                        continue;
                    idxList.Add((uint)f.Indices[0]);
                    idxList.Add((uint)f.Indices[1]);
                    idxList.Add((uint)f.Indices[2]);
                }
                sw.WriteLine($"trilist {mesh.MaterialIndex} {idxList.Count}");
                // Print indices 12 per line
                for (int i = 0; i < idxList.Count; i += 12)
                {
                    IEnumerable<string> slice = idxList.Skip(i).Take(12).Select(u => u.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine(string.Join(' ', slice));
                }

                sw.WriteLine("end_mesh");
                sw.WriteLine();
            }
        }
        public static void WriteBinary(Scene scene, string path, float scale)
        {
            using FileStream fs = new(path, FileMode.Create, FileAccess.Write);
            using BinaryWriter bw = new(fs, Encoding.ASCII, leaveOpen: false);

            // 16-byte header
            byte[] header = Encoding.ASCII.GetBytes(BINARY_HEADER);
            if (header.Length != 16) throw new InvalidOperationException("Binary header must be 16 bytes.");
            bw.Write(header);

            // Materials
            for (int i = 0; i < scene.MaterialCount; i++)
            {
                Material mat = scene.Materials[i];
                WriteU16(bw, TK_material);

                if (!IsZero(mat.ColorDiffuse))
                {
                    WriteU16(bw, TK_diffuse); WriteU16(bw, DT_color);
                    WriteF32(bw, mat.ColorDiffuse.R); WriteF32(bw, mat.ColorDiffuse.G); WriteF32(bw, mat.ColorDiffuse.B);
                }
                if (!IsZero(mat.ColorSpecular))
                {
                    WriteU16(bw, TK_specular); WriteU16(bw, DT_color);
                    WriteF32(bw, mat.ColorSpecular.R); WriteF32(bw, mat.ColorSpecular.G); WriteF32(bw, mat.ColorSpecular.B);
                }
                if (!IsZero(mat.ColorEmissive))
                {
                    WriteU16(bw, TK_emissive); WriteU16(bw, DT_color);
                    WriteF32(bw, mat.ColorEmissive.R); WriteF32(bw, mat.ColorEmissive.G); WriteF32(bw, mat.ColorEmissive.B);
                }
                if (mat.Shininess > 0)
                {
                    WriteU16(bw, TK_specpower); WriteU16(bw, DT_float1); WriteF32(bw, mat.Shininess);
                }
                if (mat.Opacity != 0 && mat.Opacity != 1)
                {
                    WriteU16(bw, TK_opacity); WriteU16(bw, DT_float1); WriteF32(bw, mat.Opacity);
                }

                // texture semantics
                if (mat.GetMaterialTextureCount(TextureType.Diffuse) > 0 && mat.GetMaterialTexture(TextureType.Diffuse, 0, out TextureSlot td))
                    WriteTexture(bw, TEX_diffuse, TrimTexturePath(td.FilePath));
                if (mat.GetMaterialTextureCount(TextureType.Normals) > 0 && mat.GetMaterialTexture(TextureType.Normals, 0, out TextureSlot tn))
                    WriteTexture(bw, TEX_normal, TrimTexturePath(tn.FilePath));
                if (mat.GetMaterialTextureCount(TextureType.Specular) > 0 && mat.GetMaterialTexture(TextureType.Specular, 0, out TextureSlot ts))
                    WriteTexture(bw, TEX_specular, TrimTexturePath(ts.FilePath));
                if (mat.GetMaterialTextureCount(TextureType.Emissive) > 0 && mat.GetMaterialTexture(TextureType.Emissive, 0, out TextureSlot te))
                    WriteTexture(bw, TEX_emissive, TrimTexturePath(te.FilePath));

                // Blend omitted -> default normal; uncomment to force:
                // WriteU16(bw, TK_blend); WriteU16(bw, BL_normal);

                WriteU16(bw, TK_end_material);
            }

            // Meshes
            foreach (Mesh? mesh in scene.Meshes)
            {
                VAttr[] layout = DecideLayout(mesh);

                WriteU16(bw, TK_mesh);

                // vertexdesc
                WriteU16(bw, TK_vertexdesc);
                foreach (VAttr a in layout)
                {
                    WriteU16(bw, a.Semantic);
                    WriteU16(bw, a.Format);
                }
                WriteU16(bw, TK_end_vertexdesc);

                // vertices
                WriteU16(bw, TK_vertices);
                WriteU32(bw, (uint)mesh.VertexCount);

                for (int v = 0; v < mesh.VertexCount; v++)
                {
                    foreach (VAttr a in layout)
                    {
                        if (a.IsUB4)
                        {
                            Color4D c = mesh.VertexColorChannels[0][v];
                            bw.Write((byte)Clamp255(c.R));
                            bw.Write((byte)Clamp255(c.G));
                            bw.Write((byte)Clamp255(c.B));
                            bw.Write((byte)Clamp255(c.A));
                        }
                        else
                        {
                            if (a.Semantic == VS_position)
                            {
                                WriteF32(bw, mesh.Vertices[v].X * scale);
                                WriteF32(bw, mesh.Vertices[v].Y * scale);
                                WriteF32(bw, mesh.Vertices[v].Z * scale);
                            }
                            else if (a.Semantic == VS_normal)
                            {
                                WriteF32(bw, mesh.Normals[v].X);
                                WriteF32(bw, mesh.Normals[v].Y);
                                WriteF32(bw, mesh.Normals[v].Z);
                            }
                            else if (a.Semantic == VS_tangent)
                            {
                                WriteF32(bw, mesh.Tangents[v].X);
                                WriteF32(bw, mesh.Tangents[v].Y);
                                WriteF32(bw, mesh.Tangents[v].Z);
                            }
                            else if (a.Semantic >= VS_texcoord0 && a.Semantic <= VS_texcoord3)
                            {
                                int ch = a.Semantic - VS_texcoord0;
                                Vector3D uv = mesh.TextureCoordinateChannels[ch][v];
                                WriteF32(bw, uv.X);
                                WriteF32(bw, uv.Y);
                            }
                            // pointsize omitted
                        }
                    }
                }

                // Primitive group: trilist with material index and flattened indices
                List<uint> idxList = new(mesh.FaceCount * 3);
                foreach (Face? f in mesh.Faces)
                {
                    if (f.IndexCount != 3) continue;
                    idxList.Add((uint)f.Indices[0]);
                    idxList.Add((uint)f.Indices[1]);
                    idxList.Add((uint)f.Indices[2]);
                }
                WriteU16(bw, PR_trilist);
                WriteU32(bw, (uint)mesh.MaterialIndex);
                WriteU32(bw, (uint)idxList.Count);
                foreach (uint idx in idxList) WriteU32(bw, idx);

                WriteU16(bw, TK_end_mesh);
            }
        }
        public static void WriteAscii(CmodModel cm, string path, float scale)
        {
            using StreamWriter sw = new(path, false, new UTF8Encoding(false));
            sw.WriteLine(ASCII_HEADER);

            // Materials
            foreach (var mat in cm.Materials)
            {
                sw.WriteLine("material");
                if (!IsZero(mat.Diffuse)) sw.WriteLine($"diffuse {F3(mat.Diffuse)}");
                if (!IsZero(mat.Specular)) sw.WriteLine($"specular {F3(mat.Specular)}");
                if (!IsZero(mat.Emissive)) sw.WriteLine($"emissive {F3(mat.Emissive)}");
                if (mat.SpecPower > 0) sw.WriteLine(FormattableString.Invariant($"specpower {mat.SpecPower:F6}"));
                if (mat.Opacity != 0 && mat.Opacity != 1) sw.WriteLine(FormattableString.Invariant($"opacity {mat.Opacity:F6}"));

                if (!string.IsNullOrEmpty(mat.TexDiffuse)) sw.WriteLine($"texture0 \"{mat.TexDiffuse}\"");
                if (!string.IsNullOrEmpty(mat.TexNormal)) sw.WriteLine($"normalmap \"{mat.TexNormal}\"");
                if (!string.IsNullOrEmpty(mat.TexSpecular)) sw.WriteLine($"specularmap \"{mat.TexSpecular}\"");
                if (!string.IsNullOrEmpty(mat.TexEmissive)) sw.WriteLine($"emissivemap \"{mat.TexEmissive}\"");

                sw.WriteLine("end_material");
                sw.WriteLine();
            }

            // Meshes
            foreach (var mesh in cm.Meshes)
            {
                sw.WriteLine("mesh");

                // vertexdesc
                sw.WriteLine("vertexdesc");
                foreach (var (Semantic, Format) in mesh.VertexDesc)
                    sw.WriteLine($"{AsciiSem(Semantic)} {AsciiFmt(Format)}");
                sw.WriteLine("end_vertexdesc");

                // vertices
                sw.WriteLine($"vertices {mesh.VertexCount}");
                int fp = 0; // float pointer into VertexData
                int cp = 0; // color pointer into ColorUB4
                for (int v = 0; v < mesh.VertexCount; v++)
                {
                    StringBuilder sb = new();
                    foreach (var (sem, fmt) in mesh.VertexDesc)
                    {
                        if (fmt == VF_ub4)
                        {
                            // write as floats 0..1 like the Assimp->ASCII path
                            byte r = mesh.ColorUB4![cp++], g = mesh.ColorUB4![cp++], b = mesh.ColorUB4![cp++], a = mesh.ColorUB4![cp++];
                            AppendFloats(sb, r / 255f, g / 255f, b / 255f, a / 255f);
                        }
                        else
                        {
                            int n = fmt switch { VF_f1 => 1, VF_f2 => 2, VF_f3 => 3, VF_f4 => 4, _ => 0 };
                            // copy values, applying scale to positions
                            if (sem == VS_position && n >= 3)
                            {
                                float x = mesh.VertexData[fp++] * scale;
                                float y = mesh.VertexData[fp++] * scale;
                                float z = mesh.VertexData[fp++] * scale;
                                AppendFloats(sb, x, y, z);
                                for (int k = 3; k < n; k++) // unlikely but keep consistent
                                    AppendFloats(sb, mesh.VertexData[fp++]);
                            }
                            else
                            {
                                for (int k = 0; k < n; k++)
                                    AppendFloats(sb, mesh.VertexData[fp++]);
                            }
                        }
                    }
                    sw.WriteLine(sb.ToString().TrimEnd());
                }

                // trilists
                foreach (var tl in mesh.Trilists)
                {
                    sw.WriteLine($"trilist {tl.MaterialIndex} {tl.Indices.Length}");
                    // 12 indices per line for readability
                    for (int i = 0; i < tl.Indices.Length; i += 12)
                        sw.WriteLine(string.Join(' ', tl.Indices.Skip(i).Take(12)
                            .Select(u => u.ToString(CultureInfo.InvariantCulture))));
                }

                sw.WriteLine("end_mesh");
                sw.WriteLine();
            }
        }
        public static void WriteBinary(CmodModel cm, string path, float scale)
        {
            using FileStream fs = new(path, FileMode.Create, FileAccess.Write);
            using BinaryWriter bw = new(fs, Encoding.ASCII, leaveOpen: false);

            // header
            byte[] header = Encoding.ASCII.GetBytes(BINARY_HEADER);
            if (header.Length != 16) throw new InvalidOperationException("Binary header must be 16 bytes.");
            bw.Write(header);

            // materials
            foreach (var mat in cm.Materials)
            {
                WriteU16(bw, TK_material);

                if (!IsZero(mat.Diffuse))
                {
                    WriteU16(bw, TK_diffuse); WriteU16(bw, DT_color);
                    WriteF32(bw, mat.Diffuse.R); WriteF32(bw, mat.Diffuse.G); WriteF32(bw, mat.Diffuse.B);
                }
                if (!IsZero(mat.Specular))
                {
                    WriteU16(bw, TK_specular); WriteU16(bw, DT_color);
                    WriteF32(bw, mat.Specular.R); WriteF32(bw, mat.Specular.G); WriteF32(bw, mat.Specular.B);
                }
                if (!IsZero(mat.Emissive))
                {
                    WriteU16(bw, TK_emissive); WriteU16(bw, DT_color);
                    WriteF32(bw, mat.Emissive.R); WriteF32(bw, mat.Emissive.G); WriteF32(bw, mat.Emissive.B);
                }
                if (mat.SpecPower > 0)
                {
                    WriteU16(bw, TK_specpower); WriteU16(bw, DT_float1); WriteF32(bw, mat.SpecPower);
                }
                if (mat.Opacity != 0 && mat.Opacity != 1)
                {
                    WriteU16(bw, TK_opacity); WriteU16(bw, DT_float1); WriteF32(bw, mat.Opacity);
                }

                if (!string.IsNullOrEmpty(mat.TexDiffuse)) WriteTexture(bw, TEX_diffuse, mat.TexDiffuse);
                if (!string.IsNullOrEmpty(mat.TexNormal)) WriteTexture(bw, TEX_normal, mat.TexNormal);
                if (!string.IsNullOrEmpty(mat.TexSpecular)) WriteTexture(bw, TEX_specular, mat.TexSpecular);
                if (!string.IsNullOrEmpty(mat.TexEmissive)) WriteTexture(bw, TEX_emissive, mat.TexEmissive);

                WriteU16(bw, TK_end_material);
            }

            // meshes
            foreach (var mesh in cm.Meshes)
            {
                WriteU16(bw, TK_mesh);

                // vertexdesc
                WriteU16(bw, TK_vertexdesc);
                foreach (var (Semantic, Format) in mesh.VertexDesc)
                {
                    WriteU16(bw, Semantic);
                    WriteU16(bw, Format);
                }
                WriteU16(bw, TK_end_vertexdesc);

                // vertices block
                WriteU16(bw, TK_vertices);
                WriteU32(bw, (uint)mesh.VertexCount);

                int fp = 0, cp = 0;
                for (int v = 0; v < mesh.VertexCount; v++)
                {
                    foreach (var (sem, fmt) in mesh.VertexDesc)
                    {
                        if (fmt == VF_ub4)
                        {
                            bw.Write(mesh.ColorUB4![cp++]);
                            bw.Write(mesh.ColorUB4![cp++]);
                            bw.Write(mesh.ColorUB4![cp++]);
                            bw.Write(mesh.ColorUB4![cp++]);
                        }
                        else
                        {
                            int n = fmt switch { VF_f1 => 1, VF_f2 => 2, VF_f3 => 3, VF_f4 => 4, _ => 0 };
                            if (sem == VS_position && n >= 3)
                            {
                                WriteF32(bw, mesh.VertexData[fp++] * scale);
                                WriteF32(bw, mesh.VertexData[fp++] * scale);
                                WriteF32(bw, mesh.VertexData[fp++] * scale);
                                for (int k = 3; k < n; k++) WriteF32(bw, mesh.VertexData[fp++]);
                            }
                            else
                            {
                                for (int k = 0; k < n; k++) WriteF32(bw, mesh.VertexData[fp++]);
                            }
                        }
                    }
                }

                // trilists as-is
                foreach (var tl in mesh.Trilists)
                {
                    WriteU16(bw, PR_trilist);
                    WriteU32(bw, (uint)tl.MaterialIndex);
                    WriteU32(bw, (uint)tl.Indices.Length);
                    foreach (uint idx in tl.Indices) WriteU32(bw, idx);
                }

                WriteU16(bw, TK_end_mesh);
            }
        }
        #endregion

        #region Helpers
        private struct VAttr
        {
            public ushort Semantic;
            public ushort Format;
            public int FloatCount;
            public bool IsUB4;
        }
        /// <summary>
        /// Build per-mesh vertex layout
        /// </summary>
        private static VAttr[] DecideLayout(Mesh mesh)
        {
            // Order: position, normal, tangent, color0, uv0..uv3, pointsize if present
            List<VAttr> list = [new VAttr { Semantic = VS_position, Format = VF_f3, FloatCount = 3 }];
            if (mesh.HasNormals) list.Add(new VAttr { Semantic = VS_normal, Format = VF_f3, FloatCount = 3 });
            if (mesh.Tangents != null && mesh.Tangents.Count == mesh.VertexCount) list.Add(new VAttr { Semantic = VS_tangent, Format = VF_f3, FloatCount = 3 });
            if (mesh.HasVertexColors(0))
                list.Add(new VAttr { Semantic = VS_color0, Format = VF_ub4, FloatCount = 0, IsUB4 = true });
            for (int ch = 0; ch < 4; ch++)
            {
                if (mesh.HasTextureCoords(ch))
                    list.Add(new VAttr { Semantic = (ushort)(VS_texcoord0 + ch), Format = VF_f2, FloatCount = 2 });
            }
            // No automatic pointsize
            return [.. list];
        }
        private static void AppendFloats(StringBuilder sb, params float[] vals)
        {
            foreach (float f in vals)
                sb.Append(f.ToString("F6", CultureInfo.InvariantCulture)).Append(' ');
        }
        internal static string TrimTexturePath(string path)
            => Path.GetFileName(path);
        internal static string AsciiSem(ushort sem)
            => sem switch
            {
                VS_position => "position",
                VS_color0 => "color0",
                VS_color1 => "color1",
                VS_normal => "normal",
                VS_tangent => "tangent",
                VS_texcoord0 => "texcoord0",
                VS_texcoord1 => "texcoord1",
                VS_texcoord2 => "texcoord2",
                VS_texcoord3 => "texcoord3",
                VS_pointsize => "pointsize",
                _ => "position"
            };
        internal static string AsciiFmt(ushort fmt)
            => fmt switch
            {
                VF_f1 => "f1",
                VF_f2 => "f2",
                VF_f3 => "f3",
                VF_f4 => "f4",
                VF_ub4 => "ub4",
                _ => "f3"
            };
        private static bool IsZero(Color3D c)
            => c.R == 0 && c.G == 0 && c.B == 0;
        private static bool IsZero(Color4D c)
            => c.R == 0 && c.G == 0 && c.B == 0 && c.A == 0;
        private static string F3(Color3D c)
            => FormattableString.Invariant($"{c.R:F6} {c.G:F6} {c.B:F6}");
        private static string F4(Color4D c)
            => FormattableString.Invariant($"{c.R:F6} {c.G:F6} {c.B:F6} {c.A:F6}");
        internal static string Format(Color4D c)
            => $"{c.R:F3},{c.G:F3},{c.B:F3},{c.A:F3}";
        internal static string Fmt(Color3D c)
            => $"{c.R:F3},{c.G:F3},{c.B:F3}";
        internal static byte Clamp255(float f)
            => (byte)Math.Clamp((int)Math.Round(f * 255.0f), 0, 255);
        /// <remarks>
        /// BinaryWriter is LE
        /// </remarks>
        private static void WriteU16(BinaryWriter writer, ushort v)
            => writer.Write(v);
        private static void WriteU32(BinaryWriter writer, uint v)
            => writer.Write(v);
        private static void WriteF32(BinaryWriter writer, float f)
            => writer.Write(f);
        private static void WriteTexture(BinaryWriter writer, ushort semantic, string path)
        {
            WriteU16(writer, TK_texture);
            WriteU16(writer, semantic);
            WriteU16(writer, DT_string);
            byte[] bytes = Encoding.ASCII.GetBytes(path);
            if (bytes.Length > ushort.MaxValue)
                throw new InvalidOperationException("Texture path too long.");
            WriteU16(writer, (ushort)bytes.Length);
            writer.Write(bytes);
        }
        #endregion
    }
}
