namespace Horizon.Ui {
    /// <summary>One of the game's lights, as GameLights reads it, in the device's world.</summary>
    internal struct GameLight {
        public bool Dynamic;

        /// <summary>0 point, 1 distant, 2 spot.</summary>
        public int Type;

        public uint Cell;
        public float East;
        public float North;
        public float Height;
        public float Red;
        public float Green;
        public float Blue;
        public float Intensity;

        /// <summary>How far it reaches, in meters.</summary>
        public float Falloff;

        public float Cone;
        public float DistanceSquared;
    }
}
