namespace Horizon.Feed {
    /// <summary>A scenery object where the game would put it (SceneryLayout), in its landblock's meters.</summary>
    public struct PlacedScenery {
        public PlacedScenery(uint model, float x, float y, float z, float yaw, float scale) {
            Model = model;
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
            Scale = scale;
        }

        /// <summary>A GfxObj (0x01...) or a Setup (0x02...).</summary>
        public uint Model { get; }

        /// <summary>East from the landblock's south-west corner, 0 to 192.</summary>
        public float X { get; }

        /// <summary>North, 0 to 192.</summary>
        public float Y { get; }

        /// <summary>The ground's height there.</summary>
        public float Z { get; }

        /// <summary>Radians.</summary>
        public float Yaw { get; }

        public float Scale { get; }
    }
}
