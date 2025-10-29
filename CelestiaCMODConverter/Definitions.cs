namespace CelestiaCMODConverter
{
    public static class Configurations
    {
        #region Version
        public const string TOOL_NAME = "CelestiaCMODConverter";
        public const string TOOL_VERSION = "0.0.1";
        #endregion
    }
    public static class Definitions
    {
        #region CMOD constants
        public const string ASCII_HEADER = "#celmodel__ascii";
        public const string BINARY_HEADER = "#celmodel_binary";
        #endregion

        // Tokens
        public const ushort TK_material = 1001;
        public const ushort TK_end_material = 1002;
        public const ushort TK_diffuse = 1003;
        public const ushort TK_specular = 1004;
        public const ushort TK_specpower = 1005;
        public const ushort TK_opacity = 1006;
        public const ushort TK_texture = 1007;
        public const ushort TK_mesh = 1009;
        public const ushort TK_end_mesh = 1010;
        public const ushort TK_vertexdesc = 1011;
        public const ushort TK_end_vertexdesc = 1012;
        public const ushort TK_vertices = 1013;
        public const ushort TK_emissive = 1014;
        public const ushort TK_blend = 1015;
        // Texture semantics
        public const ushort TEX_diffuse = 0;
        public const ushort TEX_normal = 1;
        public const ushort TEX_specular = 2;
        public const ushort TEX_emissive = 3;
        // Data types
        public const ushort DT_float1 = 1;
        public const ushort DT_float2 = 2;
        public const ushort DT_float3 = 3;
        public const ushort DT_float4 = 4;
        public const ushort DT_string = 5;
        public const ushort DT_uint32 = 6;
        public const ushort DT_color = 7;
        // Blend
        public const ushort BL_normal = 0;
        public const ushort BL_add = 1;
        public const ushort BL_premul = 2;
        // Vertex semantics
        public const ushort VS_position = 0;
        public const ushort VS_color0 = 1;
        public const ushort VS_color1 = 2;
        public const ushort VS_normal = 3;
        public const ushort VS_tangent = 4;
        public const ushort VS_texcoord0 = 5;
        public const ushort VS_texcoord1 = 6;
        public const ushort VS_texcoord2 = 7;
        public const ushort VS_texcoord3 = 8;
        public const ushort VS_pointsize = 9;
        // Vertex formats
        public const ushort VF_f1 = 0;
        public const ushort VF_f2 = 1;
        public const ushort VF_f3 = 2;
        public const ushort VF_f4 = 3;
        public const ushort VF_ub4 = 4;
        // Primitive
        public const ushort PR_trilist = 0;
    }
}
