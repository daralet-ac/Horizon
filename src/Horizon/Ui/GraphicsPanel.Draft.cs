using System.Reflection;
using Horizon.Feed;
using ImGuiNET;

namespace Horizon.Ui {
    /// <summary>
    /// The graphics settings wait for Apply (the user's call, November 2026: "a user could play around with the settings
    /// until they get a fps/memory estimate they like before applying"). The rows (draw distance, shading, sun shadows,
    /// lamps, water, grass, image) edit copies of the settings, made afresh every frame while nothing's changed, so a
    /// change from anywhere else (a test run, a profile swap) shows; the budget bars work out the copies' cost against
    /// what's in use. Apply puts the copies' values into the settings (field by field, into the same objects, which other
    /// parts read every frame) and does what a change always did (Commit: saved, the draw distance looked at again, a
    /// mark on the frame rate graph); Revert drops them.
    /// </summary>
    internal sealed partial class GraphicsPanel {
        private bool draftChanged;
        private DrawDistanceOptions drawDistanceDraft;
        private ShadingOptions shadingDraft;
        private SunShadowOptions sunShadowsDraft;
        private LampLightOptions lampLightDraft;
        private WaterOptions waterDraft;
        private GrassOptions grassDraft;
        private ImageOptions imageDraft;

        private DrawDistanceOptions DrawDistanceSettings => drawDistanceDraft ?? settings.Data.DrawDistance;
        private ShadingOptions ShadingSettings => shadingDraft ?? settings.Data.Shading;
        private SunShadowOptions SunShadowSettings => sunShadowsDraft ?? settings.Data.SunShadows;
        private LampLightOptions LampLightSettings => lampLightDraft ?? settings.Data.LampLight;
        private WaterOptions WaterSettings => waterDraft ?? settings.Data.Water;
        private GrassOptions GrassSettings => grassDraft ?? settings.Data.Grass;
        private ImageOptions ImageSettings => imageDraft ?? settings.Data.Image;

        /// <summary>Fresh copies of what's in use, unless the player has changed something since the last Apply.</summary>
        private void RefreshDraft() {
            if (draftChanged) {
                return;
            }

            var data = settings.Data;
            drawDistanceDraft = data.DrawDistance.Copy();
            shadingDraft = Clone(data.Shading);
            sunShadowsDraft = Clone(data.SunShadows);
            lampLightDraft = Clone(data.LampLight);
            waterDraft = Clone(data.Water);
            grassDraft = Clone(data.Grass);
            imageDraft = Clone(data.Image);
        }

        /// <summary>A row changed something: it waits for Apply.</summary>
        private void Changed() {
            draftChanged = true;
        }

        /// <summary>Apply and Revert, lit while something's waiting.</summary>
        private void RenderApplyRow() {
            ImGui.BeginDisabled(!draftChanged);

            if (Form.LitButton("Apply##graphics-apply", draftChanged)) {
                ApplyDraft();
            }

            Widgets.Tip("Puts the changes on. Until then the bars show what they'd cost.");
            ImGui.SameLine();

            if (ImGui.Button("Revert##graphics-revert")) {
                draftChanged = false;
                RefreshDraft();
            }

            Widgets.Tip("Back to the settings in use.");
            ImGui.EndDisabled();

            if (draftChanged) {
                ImGui.SameLine();
                Widgets.TextColored(Colors.Warning, "Not applied yet");
            }
        }

        private void ApplyDraft() {
            var data = settings.Data;
            CopyFields(drawDistanceDraft, data.DrawDistance);
            CopyFields(shadingDraft, data.Shading);
            CopyFields(sunShadowsDraft, data.SunShadows);
            CopyFields(lampLightDraft, data.LampLight);
            CopyFields(waterDraft, data.Water);
            CopyFields(grassDraft, data.Grass);
            CopyFields(imageDraft, data.Image);
            draftChanged = false;
            Commit();
        }

        /// <summary>A change put on: saved, the draw distance looked at again, and marked on the frame rate graph.</summary>
        private void Commit() {
            settings.Changed();
            drawDistance.Soon();
            FrameRateWindow.Mark(settings.Data.DrawDistance.Summary() + "; " + settings.Data.Shading.Summary() + "; "
                + settings.Data.Water.Summary() + "; " + settings.Data.Image.Summary() + "; "
                + settings.Data.Grass.Summary());
        }

        private static T Clone<T>(T value) where T : class {
            return value == null ? null : (T)typeof(object)
                .GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, null);
        }

        private static void CopyFields<T>(T from, T to) where T : class {
            if (from == null || to == null) {
                return;
            }

            foreach (var field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public
                | BindingFlags.NonPublic)) {
                if (!field.IsInitOnly) {
                    field.SetValue(to, field.GetValue(from));
                }
            }
        }
    }
}
