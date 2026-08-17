using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace QuizBattle.Arena
{
    public class ArenaRig
    {
        public Camera Camera;
        public Light KeyLight;
        public Light FillLight;
    }

    /// Single source of truth for camera + lighting + post-processing, replacing four
    /// separate camera-creation/framing duplicates (GameManager, ArenaController,
    /// NetworkedArenaView, NetworkedMatchDemoRunner). Acquire() reuses an existing
    /// Camera.main/directional Light when the scene already authored one (the real
    /// Arena.unity scene does — this also fixes ArenaController previously creating a
    /// second MainCamera-tagged camera on top of it) and creates them when absent (the
    /// headless demo runners build an empty scene with neither).
    public static class ArenaEnvironment
    {
        private const float Pitch = 52f;
        private const float FieldOfView = 32f;
        // Fraction of the vertical frame reserved for the board, bottom-anchored — the
        // HUD's question/choice UI occupies roughly the top 45% of the screen, so the
        // board is framed into the bottom 55% rather than centered under it.
        private const float BoardScreenFraction = 0.55f;

        public static ArenaRig Acquire(Color backgroundColor)
        {
            var rig = new ArenaRig
            {
                Camera = AcquireCamera(backgroundColor),
                KeyLight = AcquireKeyLight(),
                FillLight = AcquireFillLight(),
            };

            ConfigureAmbient();
            ConfigureVolume(rig.Camera);
            return rig;
        }

        public static void FrameGrid(ArenaRig rig, GridController grid, int width, int height)
        {
            var camera = rig.Camera;
            float centerX = (width - 1) * 0.5f;
            float centerZ = (height - 1) * 0.5f;

            float pitchRad = Pitch * Mathf.Deg2Rad;
            float fovRad = camera.fieldOfView * Mathf.Deg2Rad;
            float aspect = camera.aspect > 0f ? camera.aspect : 16f / 9f;

            // We want the entire playable board + margins (from z = -1.2 to z = height + 0.8)
            // to map comfortably into the viewport vertical range [0.06, 0.68] (total span 0.62 of screen).
            float zSpan = height + 2.2f;
            float xSpan = width + 2.4f;

            float targetNdcHeight = 1.25f;
            float distV = (zSpan * Mathf.Sin(pitchRad)) / (targetNdcHeight * Mathf.Tan(fovRad * 0.5f));

            float targetNdcWidth = 1.50f;
            float distH = xSpan / (targetNdcWidth * Mathf.Tan(fovRad * 0.5f) * aspect);

            float dist = Mathf.Max(distV, distH);

            // Aim the camera so that the bottom start line (z = 0) is well above the bottom screen edge (viewport y ≈ 0.08),
            // and the top goal line (z = height - 1) is below the HUD (viewport y ≈ 0.55 .. 0.65).
            float targetNdcMidY = -0.28f;
            float zAimOffset = (dist * (-targetNdcMidY) * Mathf.Tan(fovRad * 0.5f)) / Mathf.Sin(pitchRad);
            float aimZ = centerZ + zAimOffset;

            var rotation = Quaternion.Euler(Pitch, 0f, 0f);
            Vector3 forward = rotation * Vector3.forward;
            Vector3 lookAtPoint = new Vector3(centerX, 0f, aimZ);
            Vector3 position = lookAtPoint - forward * dist;

            camera.transform.SetPositionAndRotation(position, rotation);

            foreach (var label in Object.FindObjectsByType<BillboardLabel>(FindObjectsInactive.Exclude))
                label.Align(camera);
        }

        private static Camera AcquireCamera(Color backgroundColor)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var camObj = new GameObject("Main Camera");
                camera = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }

            camera.orthographic = false;
            camera.fieldOfView = FieldOfView;
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Default to Clash Royale bright sky horizon if a dark fallback was passed
            camera.backgroundColor = (backgroundColor.r < 0.2f && backgroundColor.g < 0.2f && backgroundColor.b < 0.2f)
                ? QuizBattlePalette.SkyHorizon
                : backgroundColor;

            var camData = camera.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;

            return camera;
        }

        private static Light AcquireKeyLight()
        {
            Light key = null;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional) { key = light; break; }
            }

            if (key == null)
            {
                var go = new GameObject("Key Light");
                key = go.AddComponent<Light>();
                key.type = LightType.Directional;
            }

            key.color = new Color(1.00f, 0.94f, 0.84f); // warm sunlit key light
            key.intensity = 1.35f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.68f; // crisp stylized cartoon shadow
            key.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            RenderSettings.sun = key;
            return key;
        }

        private static Light AcquireFillLight()
        {
            var go = new GameObject("Fill Light");
            var fill = go.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.48f, 0.65f, 0.95f); // saturated sky fill
            fill.intensity = 0.38f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(38f, 145f, 0f);
            return fill;
        }

        private static void ConfigureAmbient()
        {
            // Trilight with cool sky, neutral equator, and lush grass ground bounce
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.58f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.42f, 0.35f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.44f, 0.18f); // lush green grass bounce
            RenderSettings.ambientIntensity = 1f;
        }

        private static void ConfigureVolume(Camera camera)
        {
            var existing = Object.FindFirstObjectByType<Volume>();
            if (existing != null) return;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.0f;
            bloom.intensity.value = 0.65f;
            bloom.scatter.value = 0.75f;
            bloom.downscale.value = BloomDownscaleMode.Half;
            bloom.maxIterations.value = 4;

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.Neutral;

            var colorAdjustments = profile.Add<ColorAdjustments>(true);
            // Clash-Royale punchy/saturated toon look
            colorAdjustments.saturation.value = 28f;
            colorAdjustments.contrast.value = 14f;
            colorAdjustments.postExposure.value = 0.12f;

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.12f;
            vignette.smoothness.value = 0.6f;

            var volumeObj = new GameObject("Post Volume");
            var volume = volumeObj.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.profile = profile;
        }
    }
}
