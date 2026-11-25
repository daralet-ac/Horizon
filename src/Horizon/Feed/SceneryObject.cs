namespace Horizon.Feed {
    /// <summary>
    /// One object a scene may place (the portal dat's Scene, ObjectDesc), as SceneryLayout needs it, so it can be tested
    /// without the dat reader.
    /// </summary>
    public sealed class SceneryObject {
        public SceneryObject(uint model, float baseX, float baseY, float frequency, float displaceX, float displaceY,
            float minScale, float maxScale, float maxRotation, uint weenie, float minSlope = 0, float maxSlope = 1) {
            MinSlope = minSlope;
            MaxSlope = maxSlope;
            Model = model;
            BaseX = baseX;
            BaseY = baseY;
            Frequency = frequency;
            DisplaceX = displaceX;
            DisplaceY = displaceY;
            MinScale = minScale;
            MaxScale = maxScale;
            MaxRotation = maxRotation;
            Weenie = weenie;
        }

        /// <summary>The model: a GfxObj (0x01...) or a Setup (0x02...).</summary>
        public uint Model { get; }

        public float BaseX { get; }
        public float BaseY { get; }

        /// <summary>The chance it's placed in a cell, 0 to 1.</summary>
        public float Frequency { get; }

        public float DisplaceX { get; }
        public float DisplaceY { get; }
        public float MinScale { get; }
        public float MaxScale { get; }

        /// <summary>Degrees.</summary>
        public float MaxRotation { get; }

        /// <summary>
        /// The ground's slope it's placed on, as the ground's normal's upward part (1 flat, less as it steepens): kept only
        /// from MinSlope to MaxSlope (ObjectDesc::CheckSlope, 0x52FDD0).
        /// </summary>
        public float MinSlope { get; }

        public float MaxSlope { get; }

        /// <summary>Non-zero for an object the server places (a weenie), which the scenery leaves out.</summary>
        public uint Weenie { get; }
    }
}
