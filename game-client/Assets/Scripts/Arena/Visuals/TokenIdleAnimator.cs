using System.Collections.Generic;
using UnityEngine;

namespace QuizBattle.Arena.Visuals
{
    /// Gentle idle motion for characters and token accents (Vera's rings, Blaze's embers, Zephyr's
    /// orbit ring, cartoon breathing) — gives units the lively bounce of Clash Royale characters.
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
        private Transform _bodyRoot;
        private Vector3 _bodyBaseScale = Vector3.one;
        private float _seed;
        private bool _paused;

        private void Awake()
        {
            _seed = Random.Range(0f, 100f);
        }

        public void SetBodyRoot(Transform bodyRoot)
        {
            _bodyRoot = bodyRoot;
            if (_bodyRoot != null) _bodyBaseScale = _bodyRoot.localScale;
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            if (paused && _bodyRoot != null)
            {
                _bodyRoot.localScale = _bodyBaseScale;
            }
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

            // Character body breathing & subtle idle sway
            if (_bodyRoot != null && !_paused)
            {
                float breath = Mathf.Sin(t * 2.4f) * 0.035f;
                _bodyRoot.localScale = new Vector3(
                    _bodyBaseScale.x * (1f - breath * 0.5f),
                    _bodyBaseScale.y * (1f + breath),
                    _bodyBaseScale.z * (1f - breath * 0.5f)
                );
            }

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
