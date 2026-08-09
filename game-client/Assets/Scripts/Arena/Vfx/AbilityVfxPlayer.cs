using System.Collections.Generic;
using UnityEngine;

namespace QuizBattle.Arena.Vfx
{
    /// Plays a small fixed effect per ability vfxTag. `vfxTag` already flows end-to-end
    /// from CharacterDefinitionSO through AttackResultPayload/AttackOutcome — no new
    /// characterId lookup table needed here, callers just pass the tag straight through.
    /// Five tags are handled, including "vfx_basic_strike" (the server's default ability
    /// — most ordinary attacks use it, so skipping it would leave most attacks silent).
    public static class AbilityVfxPlayer
    {
        /// Returns the spawned ParticleSystems (possibly empty) so a caller that needs
        /// deterministic, synchronous playback — the headless demo runner, which has no
        /// player loop for particles to simulate naturally — can advance them manually
        /// via ParticleSystem.Simulate. Real gameplay callers can ignore the return value.
        public static List<ParticleSystem> Play(string vfxTag, Vector3 from, Vector3 to, bool eliminated)
        {
            var spawned = new List<ParticleSystem>();

            switch (vfxTag)
            {
                case "vfx_fireball":
                    PlayFireball(from, to, spawned);
                    break;
                case "vfx_shield_shimmer":
                    PlayShieldShimmer(to, spawned);
                    break;
                case "vfx_wind_trail":
                    PlayWindTrail(from, to, spawned);
                    break;
                case "vfx_life_drain":
                    PlayLifeDrain(from, to, spawned);
                    break;
                case "vfx_basic_strike":
                default:
                    PlayBasicStrike(to, spawned);
                    break;
            }

            if (eliminated) PlayEliminated(to, spawned);

            return spawned;
        }

        private static void PlayFireball(Vector3 from, Vector3 to, List<ParticleSystem> spawned)
        {
            var color = new Color(1f, 0.5f, 0.15f);
            Track(ParticleFactory.Streaks(from, to, color, duration: 0.32f), spawned);
            Track(ParticleFactory.Burst(to, color, size: 0.26f, count: 22, speed: 2.8f, lifetime: 0.4f), spawned);
        }

        private static void PlayShieldShimmer(Vector3 at, List<ParticleSystem> spawned)
        {
            var color = new Color(0.4f, 0.7f, 1f);
            Track(ParticleFactory.RingWave(at + Vector3.up * 0.5f, color, startRadius: 0.15f, endRadius: 0.7f, duration: 0.55f), spawned);
        }

        private static void PlayWindTrail(Vector3 from, Vector3 to, List<ParticleSystem> spawned)
        {
            var color = new Color(0.6f, 1f, 0.75f);
            Track(ParticleFactory.Streaks(from, to, color, duration: 0.4f, count: 14), spawned);
            Track(ParticleFactory.RingWave(to, color, startRadius: 0.1f, endRadius: 0.5f, duration: 0.4f, upright: false), spawned);
        }

        private static void PlayLifeDrain(Vector3 from, Vector3 to, List<ParticleSystem> spawned)
        {
            var color = new Color(0.85f, 0.35f, 1f);
            // Not particle-based, so nothing to track/simulate — it's visible immediately.
            ParticleFactory.Beam(from + Vector3.up * 0.5f, to + Vector3.up * 0.5f, color, duration: 0.5f, width: 0.07f);
            Track(ParticleFactory.Burst(to + Vector3.up * 0.5f, color, size: 0.2f, count: 12, speed: 1.4f, lifetime: 0.4f), spawned);
        }

        private static void PlayBasicStrike(Vector3 at, List<ParticleSystem> spawned)
        {
            Track(ParticleFactory.Burst(at + Vector3.up * 0.4f, new Color(1f, 0.9f, 0.6f), size: 0.16f, count: 12, speed: 1.8f, lifetime: 0.3f), spawned);
        }

        private static void PlayEliminated(Vector3 at, List<ParticleSystem> spawned)
        {
            Track(ParticleFactory.Burst(at + Vector3.up * 0.4f, new Color(0.6f, 0.6f, 0.65f), size: 0.22f, count: 26, speed: 2.2f, lifetime: 0.55f), spawned);
        }

        private static void Track(GameObject go, List<ParticleSystem> spawned)
        {
            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null) spawned.Add(ps);
        }

        /// Advances every given particle system to time t — used only by the headless
        /// demo runner to make otherwise-invisible-at-t=0 particles show up in a screenshot.
        public static void SimulateAll(IEnumerable<ParticleSystem> systems, float t)
        {
            foreach (var ps in systems)
            {
                if (ps != null) ps.Simulate(t, true, true);
            }
        }
    }
}
