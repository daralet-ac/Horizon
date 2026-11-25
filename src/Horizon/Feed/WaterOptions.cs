using System.Runtime.Serialization;

namespace Horizon.Feed {
    /// <summary>
    /// Water reflections (Ui/DepthShading.Water.cs), in settings.json as "water". Off to start; each piece has its own
    /// switch so they can be mixed and matched (the user's call, November 2026).
    /// </summary>
    [DataContract]
    public class WaterOptions {
        [DataMember(Name = "on")] public bool On;

        /// <summary>How much the reflection covers the game's water, in percent.</summary>
        [DataMember(Name = "strength")] public int Strength;

        /// <summary>Small moving waves that break the reflection up.</summary>
        [DataMember(Name = "ripples")] public bool Ripples;

        /// <summary>How big and strong the ripples are, in percent of the usual.</summary>
        [DataMember(Name = "rippleSize")] public int RippleSize;

        /// <summary>Little reflection looking straight down, much at a glancing angle, as real water.</summary>
        [DataMember(Name = "fresnel")] public bool Fresnel;

        /// <summary>The sun's glitter on the ripples.</summary>
        [DataMember(Name = "glints")] public bool Glints;

        /// <summary>Reflections softer the further what they show is from the water.</summary>
        [DataMember(Name = "blur")] public bool Blur;

        /// <summary>The reflection fading out toward the shore instead of ending in a hard line.</summary>
        [DataMember(Name = "softShore")] public bool SoftShore;

        public WaterOptions() {
            SetDefaults();
        }

        public int ClampedStrength => Strength < 0 ? 0 : Strength > 100 ? 100 : Strength;

        public int ClampedRippleSize => RippleSize < 10 ? 10 : RippleSize > 300 ? 300 : RippleSize;

        /// <summary>The settings in a line, for the Frame Rate window's marks and test runs.</summary>
        public string Summary() {
            if (!On) {
                return "water off";
            }

            return $"water {ClampedStrength}%" + (Ripples ? $", ripples {ClampedRippleSize}%" : "")
                + (Fresnel ? ", fresnel" : "") + (Glints ? ", glints" : "") + (Blur ? ", blur" : "")
                + (SoftShore ? ", soft shore" : "");
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) {
            SetDefaults();
        }

        private void SetDefaults() {
            On = false;
            Strength = 35;
            Ripples = true;
            RippleSize = 100;
            Fresnel = true;
            Glints = true;
            Blur = true;
            SoftShore = true;
        }
    }
}
