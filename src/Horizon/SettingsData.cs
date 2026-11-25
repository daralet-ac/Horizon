using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Horizon.Feed;

namespace Horizon {
    /// <summary>
    /// Everything settings.json holds. SetDefaults runs both when one is made and before one is read from the file:
    /// a DataContract reader skips field initialisers, so a setting the file doesn't have keeps its default.
    /// The graphics settings keep the names Daralet Toolkit saved them under, so its settings.json can be read.
    /// </summary>
    [DataContract]
    public class SettingsData {
        public const float MinTextScale = 0.5f;
        public const float MaxTextScale = 2.5f;

        /// <summary>Each window's place, size and look, by its id.</summary>
        [DataMember(Name = "windows")] public Dictionary<string, WindowPlacement> Windows;

        /// <summary>The look of Horizon's windows, a UiThemes name; null for Daralet, the default.</summary>
        [DataMember(Name = "theme")] public string Theme;

        /// <summary>The size of the text in Horizon's windows.</summary>
        [DataMember(Name = "textScale")] public float TextScale;

        /// <summary>Tooltips show only while TooltipKey is held.</summary>
        [DataMember(Name = "tooltipsNeedAlt")] public bool TooltipsNeedAlt;

        /// <summary>The key TooltipsNeedAlt has held, a Windows virtual key code: Alt (0x12) to start.</summary>
        [DataMember(Name = "tooltipKey")] public int TooltipKey;

        /// <summary>Whether Daralet Toolkit's graphics settings have been brought over (Settings.ImportFromToolkit).</summary>
        [DataMember(Name = "toolkitImported")] public bool ToolkitImported;

        /// <summary>
        /// Whether the plugin reads Windows' crash records for the game at start (Ui/SessionLog.cs): read-only, but
        /// players may wonder, so they opt in.
        /// </summary>
        [DataMember(Name = "readCrashRecords")] public bool ReadCrashRecords;

        /// <summary>A window being dragged snaps to the screen's and other windows' edges (WindowSnap).</summary>
        [DataMember(Name = "snapWindows")] public bool SnapWindows;

        /// <summary>The edge of a window being resized snaps as a dragged window does (WindowSnap.SnapEdge).</summary>
        [DataMember(Name = "snapResize")] public bool SnapResize;

        /// <summary>How near an edge has to come to snap, in pixels.</summary>
        [DataMember(Name = "snapDistance")] public int SnapDistance;

        /// <summary>A dragged window snaps to a grid where no edge is near.</summary>
        [DataMember(Name = "snapGrid")] public bool SnapGrid;

        /// <summary>Resizing snaps to the grid, whether or not moving does (SnapGrid).</summary>
        [DataMember(Name = "snapResizeGrid")] public bool SnapResizeGrid;

        /// <summary>The grid's spacing, in pixels.</summary>
        [DataMember(Name = "snapGridSize")] public int SnapGridSize;

        /// <summary>The grid is drawn while a window's dragged.</summary>
        [DataMember(Name = "snapGridShown")] public bool SnapGridShown;

        /// <summary>Draw distance past the game's own settings (DrawDistance).</summary>
        [DataMember(Name = "drawDistance")] public DrawDistanceOptions DrawDistance;

        /// <summary>The frame rate the budget bar aims for.</summary>
        [DataMember(Name = "graphicsTargetFps")] public int GraphicsTargetFps;

        /// <summary>Shading from the game's depth buffer (DepthShading).</summary>
        [DataMember(Name = "shading")] public ShadingOptions Shading;

        /// <summary>Water reflections (DepthShading).</summary>
        [DataMember(Name = "water")] public WaterOptions Water;

        /// <summary>Anti-aliasing, sharpening and glow of the 3D world (DepthShading).</summary>
        [DataMember(Name = "image")] public ImageOptions Image;

        /// <summary>Grass and reeds near you (Grass).</summary>
        [DataMember(Name = "grass")] public GrassOptions Grass;

        /// <summary>Shadows cast by the sun (ShadowCasters, DepthShading).</summary>
        [DataMember(Name = "sunShadows")] public SunShadowOptions SunShadows;

        /// <summary>Light from the game's own lights, with shadows (DepthShading.Lamps).</summary>
        [DataMember(Name = "lampLight")] public LampLightOptions LampLight;

        public SettingsData() {
            SetDefaults();
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) {
            SetDefaults();
        }

        public float ClampedTextScale => Math.Max(MinTextScale, Math.Min(MaxTextScale, TextScale));

        private void SetDefaults() {
            Windows = new Dictionary<string, WindowPlacement>();
            TextScale = 1;
            TooltipsNeedAlt = false;
            TooltipKey = Keys.Alt;
            ReadCrashRecords = false;
            SnapWindows = true;
            SnapResize = true;
            SnapResizeGrid = true;
            SnapDistance = 10;
            SnapGridSize = 20;
            SnapGridShown = true;
            DrawDistance = new DrawDistanceOptions();
            GraphicsTargetFps = 60;
            Shading = new ShadingOptions();
            Water = new WaterOptions();
            Image = new ImageOptions();
            Grass = new GrassOptions();
            SunShadows = new SunShadowOptions();
            LampLight = new LampLightOptions();
        }
    }
}
