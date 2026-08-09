using System.Collections.Generic;
using UnityEngine;

namespace QuizBattle.Arena.Visuals
{
    /// Gentle idle motion for token accents (Vera's rings, Blaze's embers, Zephyr's
    /// orbit ring) — purely cosmetic polish. A no-op in the headless demo runners since
    /// they drive matches synchronously with no player loop (Update never fires there),
    /// so screenshots stay static — that's fine, there's nothing to assert on here.
    public class TokenIdleAnimator : MonoBehaviour
    {
        private struct Accent
        {
            public Transform Transform;
            public Vector3 BasePosition;
            public float BobSpeed;
            public float BobAmount;
            public float SpinSpeed;
        }

        private readonly List<Accent> _accents = new List<Accent>();
        private float _seed;

        private void Awake()
        {
            _seed = Random.Range(0f, 100f);
        }

        public void Register(Transform t, float bobSpeed = 1.2f, float bobAmount = 0.02f, float spinSpeed = 0f)
        {
            _accents.Add(new Accent
            {
                Transform = t,
                BasePosition = t.localPosition,
                BobSpeed = bobSpeed,
                BobAmount = bobAmount,
                SpinSpeed = spinSpeed,
            });
        }

        private void Update()
        {
            float t = Time.time + _seed;
            foreach (var accent in _accents)
            {
                if (accent.Transform == null) continue;

                if (accent.BobAmount > 0f)
                {
                    var pos = accent.BasePosition;
                    pos.y += Mathf.Sin(t * accent.BobSpeed) * accent.BobAmount;
                    accent.Transform.localPosition = pos;
                }

                if (accent.SpinSpeed != 0f)
                {
                    accent.Transform.Rotate(Vector3.up, accent.SpinSpeed * Time.deltaTime, Space.Self);
                }
            }
        }
    }
}
