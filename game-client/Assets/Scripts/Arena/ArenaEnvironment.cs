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
            Vector3 center = grid.GridCenter();

            float depth = height * grid.tileSize;
            float horiz = width * grid.tileSize;

            float pitchRad = Pitch * Mathf.Deg2Rad;
            float fovRad = camera.fieldOfView * Mathf.Deg2Rad;
            float aspect = camera.aspect > 0f ? camera.aspect : 16f / 9f;

            const float margin = 1.5f; // token height + breathing room
            float vNeed = depth * Mathf.Cos(pitchRad) + margin;
            float hNeed = horiz + margin;

            // vNeed must fit within BoardScreenFraction of the vertical FOV, not the
            // whole frame — so zoom out enough that the reserved bottom band alone is
            // big enough, rather than fitting vNeed to the full frame and then shifting
            // it partly off-screen.
            float distV = (vNeed / BoardScreenFraction) * 0.5f / Mathf.Tan(fovRad * 0.5f);
            float horizFovRad = 2f * Mathf.Atan(Mathf.Tan(fovRad * 0.5f) * aspect);
            float distH = (hNeed * 0.5f) / Mathf.Tan(horizFovRad * 0.5f);
            float dist = Mathf.Max(distV, distH);

            var rotation = Quaternion.Euler(Pitch, 0f, 0f);
            Vector3 forward = rotation * Vector3.forward;
            Vector3 position = center - forward * dist;

            // Raising the camera without re-aiming shifts the previously-centered look-at
            // point toward the bottom of the frame — exactly what we want here.
            float viewHeightAtDist = 2f * dist * Mathf.Tan(fovRad * 0.5f);
            float shiftFraction = 0.5f - BoardScreenFraction * 0.5f;
            position += (rotation * Vector3.up) * (viewHeightAtDist * shiftFraction);

            camera.transform.SetPositionAndRotation(position, rotation);

            foreach (var label in Object.FindObjectsByType<BillboardLabel>(FindObjectsSortMode.None))
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
            camera.backgroundColor = backgroundColor;

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

            key.color = new Color(1f, 0.92f, 0.78f); // warmer, sunlit — Clash-Royale-style key light
            key.intensity = 1.25f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.65f; // partial-strength colored shadows read as stylized, not grim
            key.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            RenderSettings.sun = key;
            return key;
        }

        private static Light AcquireFillLight()
        {
            var go = new GameObject("Fill Light");
            var fill = go.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.55f, 0.70f, 1f);
            fill.intensity = 0.3f; // trimmed slightly so it doesn't wash out the warmer floor/key light
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(35f, 145f, 0f);
            return fill;
        }

        private static void ConfigureAmbient()
        {
            // Required so the headless demo runners (empty scene, no skybox) don't render
            // pure-black ambient — Trilight gives a deliberate cool-sky/warm-ground look
            // that also benefits the real Arena scene.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.44f, 0.56f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.26f, 0.28f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.17f, 0.14f); // warmer bounce off the sand/terracotta floor
            RenderSettings.ambientIntensity = 1f;
        }

        private static void ConfigureVolume(Camera camera)
        {
            var existing = Object.FindFirstObjectByType<Volume>();
            if (existing != null) return;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.0f;
            bloom.intensity.value = 0.5f; // trimmed slightly to avoid blowout from the higher saturation below
            bloom.scatter.value = 0.7f;
            bloom.downscale.value = BloomDownscaleMode.Half;
            bloom.maxIterations.value = 4;

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.Neutral; // ACES would crush the saturation push below

            var colorAdjustments = profile.Add<ColorAdjustments>(true);
            // Clash-Royale-style punchy/saturated toon look.
            colorAdjustments.saturation.value = 24f;
            colorAdjustments.contrast.value = 11f;

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.13f; // CR barely vignettes; a heavy vignette fights the saturation push
            vignette.smoothness.value = 0.6f;

            var volumeObj = new GameObject("Post Volume");
            var volume = volumeObj.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.profile = profile;
        }
    }
}
