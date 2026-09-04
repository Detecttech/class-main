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
        private const float FrameOffset = 0.31f;

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
            if (rig == null || rig.Camera == null || grid == null || width < 1 || height < 1) return;
            var camera = rig.Camera;
            float tileSize = grid.tileSize;
            float centerX = (width - 1) * tileSize * 0.5f;
            float centerZ = (height - 1) * tileSize * 0.5f;
            var center = grid.transform.TransformPoint(new Vector3(centerX, 0f, centerZ));
            var rotation = grid.transform.rotation * Quaternion.Euler(Pitch, 0f, 0f);
            var inverseRotation = Quaternion.Inverse(rotation);
            float tangent = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float aspect = camera.aspect > 0f ? camera.aspect : 16f / 9f;
            float distance = 1f;
            for (int corner = 0; corner < 8; corner++)
            {
                var local = new Vector3(
                    centerX + ((corner & 1) == 0 ? -1f : 1f) * (width * tileSize * 0.5f + 0.6f),
                    (corner & 2) == 0 ? 0f : 2.8f,
                    centerZ + ((corner & 4) == 0 ? -1f : 1f) * (height * tileSize * 0.5f + 0.6f));
                var point = inverseRotation * (grid.transform.TransformPoint(local) - center);
                distance = Mathf.Max(distance, RequiredDistance(point, tangent, aspect, 0.24f));
            }
            float gateHalfWidth = Mathf.Clamp(width * tileSize * 0.325f, 1.6f, 3f) + 0.8f;
            for (int corner = 0; corner < 4; corner++)
            {
                var local = new Vector3(centerX + ((corner & 1) == 0 ? -gateHalfWidth : gateHalfWidth),
                                        (corner & 2) == 0 ? 0f : 4.5f, centerZ + height * tileSize * 0.5f + 3.2f);
                var point = inverseRotation * (grid.transform.TransformPoint(local) - center);
                distance = Mathf.Max(distance, RequiredDistance(point, tangent, aspect, 0.36f));
            }
            var position = center + rotation * new Vector3(0f, FrameOffset * distance * tangent, -distance);
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = Mathf.Max(100f, distance + grid.transform.TransformVector(new Vector3(width * tileSize + 10f, 10f, height * tileSize + 10f)).magnitude);

            var framing = camera.GetComponent<ArenaViewportFraming>();
            if (framing == null) framing = camera.gameObject.AddComponent<ArenaViewportFraming>();
            framing.Track(rig, grid, width, height);

            foreach (var label in Object.FindObjectsByType<BillboardLabel>(FindObjectsInactive.Exclude))
                label.Align(camera);
        }

        private static float RequiredDistance(Vector3 point, float tangent, float aspect, float top)
        {
            float horizontal = Mathf.Abs(point.x) / (0.86f * tangent * aspect) - point.z;
            float upper = (point.y - top * tangent * point.z) / (tangent * (top + FrameOffset));
            float lower = (-0.86f * tangent * point.z - point.y) / (tangent * (0.86f - FrameOffset));
            return Mathf.Max(Mathf.Max(horizontal, upper), Mathf.Max(lower, 1f - point.z));
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
                if (light.type == LightType.Directional && light.name != "Fill Light") { key = light; break; }
            }

            if (key == null)
            {
                var go = new GameObject("Key Light");
                key = go.AddComponent<Light>();
                key.type = LightType.Directional;
            }

            key.color = new Color(1.00f, 0.88f, 0.72f);
            key.intensity = 1.2f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.55f;
            key.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            RenderSettings.sun = key;
            return key;
        }

        private static Light AcquireFillLight()
        {
            Light fill = null;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional && light.name == "Fill Light") { fill = light; break; }
            }
            if (fill == null) fill = new GameObject("Fill Light").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.46f, 0.69f, 1.00f);
            fill.intensity = 0.42f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(38f, 145f, 0f);
            return fill;
        }

        private static void ConfigureAmbient()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.43f, 0.49f, 0.70f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.37f, 0.49f);
            RenderSettings.ambientGroundColor = new Color(0.19f, 0.27f, 0.31f);
            RenderSettings.ambientIntensity = 1f;
        }

        private static void ConfigureVolume(Camera camera)
        {
            var existing = Object.FindFirstObjectByType<Volume>();
            if (existing != null) return;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.15f;
            bloom.intensity.value = 0.28f;
            bloom.scatter.value = 0.55f;
            bloom.downscale.value = BloomDownscaleMode.Half;
            bloom.maxIterations.value = 3;

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.Neutral;

            var colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.saturation.value = 8f;
            colorAdjustments.contrast.value = 10f;
            colorAdjustments.postExposure.value = 0.08f;

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.08f;
            vignette.smoothness.value = 0.6f;

            var volumeObj = new GameObject("Post Volume");
            var volume = volumeObj.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.profile = profile;
        }
    }

    public class ArenaViewportFraming : MonoBehaviour
    {
        private ArenaRig _rig;
        private GridController _grid;
        private int _width;
        private int _height;
        private float _aspect;
        private float _fieldOfView;
        private Matrix4x4 _gridMatrix;

        public void Track(ArenaRig rig, GridController grid, int width, int height)
        {
            _rig = rig;
            _grid = grid;
            _width = width;
            _height = height;
            _aspect = rig.Camera.aspect;
            _fieldOfView = rig.Camera.fieldOfView;
            _gridMatrix = grid.transform.localToWorldMatrix;
        }

        private void LateUpdate()
        {
            if (_rig == null || _rig.Camera == null || _grid == null) return;
            if (!Mathf.Approximately(_aspect, _rig.Camera.aspect)
                    || !Mathf.Approximately(_fieldOfView, _rig.Camera.fieldOfView)
                    || _gridMatrix != _grid.transform.localToWorldMatrix)
                ArenaEnvironment.FrameGrid(_rig, _grid, _width, _height);
        }
    }
}
