using UnityEngine;

namespace QuizBattle.Arena
{
    /// Screen-aligned billboard (matches the camera's rotation rather than looking at its
    /// position, so text stays upright and unskewed near the edges of frame). LateUpdate
    /// covers normal play mode; Align(camera) is also called explicitly right after
    /// creation and from ArenaEnvironment.FrameGrid because the headless demo runners
    /// drive matches synchronously with no player loop, so LateUpdate never fires there —
    /// a LateUpdate-only billboard would face the wrong way in every screenshot.
    public class BillboardLabel : MonoBehaviour
    {
        private Camera _camera;

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera != null) Align(_camera);
        }

        public void Align(Camera camera)
        {
            if (camera == null) return;
            _camera = camera;
            transform.rotation = camera.transform.rotation;
        }
    }
}
