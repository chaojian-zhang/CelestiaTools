namespace CelestiaStarDatabaseConverter.Types
{
    struct SpectralYaml
    {
        public string? packed;   // e.g., "0426" or "0x0426"
        public int? kind;        // K
        public int? type;        // T
        public int? subtype;     // S
        public int? lum;         // L
    }

    struct StarYaml
    {
        public uint hip;
        public float? x;
        public float? y;
        public float? z;

        public double? ra;          // degrees
        public double? dec;         // degrees
        public double? distance_ly; // ly

        public double abs_mag;      // real; stored as q8.8*256
        public SpectralYaml spectral;
    }

    struct RootYaml
    {
        public string version;  // expect "0x0100" or "0100" or 0x0100
        public List<StarYaml> stars;
    }
}
