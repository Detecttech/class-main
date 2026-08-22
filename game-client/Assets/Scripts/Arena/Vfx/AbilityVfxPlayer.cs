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
                case "vfx_freeze":
                    PlayFreeze(from, to, spawned);
                    break;
                case "vfx_basic_strike":
                default:
                    PlayBasicStrike(from, to, spawned);
                    break;
            }

            if (eliminated) PlayEliminated(to, spawned);

            return spawned;
        }

        private static void PlayFireball(Vector3 from, Vector3 to, List<ParticleSystem> spawned)
        {
            var color = new Color(1f, 0.45f, 0.10f);
            var gold = new Color(1f, 0.85f, 0.25f);
            Track(ParticleFactory.Streaks(from + Vector3.up * 0.5f, to + Vector3.up * 0.5f, color, duration: 0.30f, count: 20), spawned);
            Track(ParticleFactory.Streaks(from + Vector3.up * 0.5f, to + Vector3.up * 0.5f, gold, duration: 0.28f, count: 12), spawned);
            Track(ParticleFactory.Burst(to + Vector3.up * 0.5f, color, size: 0.48f, count: 35, speed: 3.5f, lifetime: 0.45f), spawned);
            Track(ParticleFactory.RingWave(to, color, startRadius: 0.2f, endRadius: 1.4f, duration: 0.45f, upright: false), spawned);
        }

        private static void PlayShieldShimmer(Vector3 at, List<ParticleSystem> spawned)
        {
            var color = new Color(0.35f, 0.75f, 1f);
            var gold = new Color(1f, 0.85f, 0.35f);
            Track(ParticleFactory.RingWave(at + Vector3.up * 0.6f, color, startRadius: 0.15f, endRadius: 1.1f, duration: 0.6f), spawned);
            Track(ParticleFactory.Burst(at + Vector3.up * 0.6f, gold, size: 0.28f, count: 24, speed: 2.2f, lifetime: 0.5f), spawned);
        }

        private static void PlayWindTrail(Vector3 from, Vector3 to, List<ParticleSystem> spawned)
        {
            var color = new Color(0.45f, 1f, 0.75f);
            Track(ParticleFactory.Streaks(from + Vector3.up * 0.4f, to + Vector3.up * 0.4f, color, duration: 0.35f, count: 22), spawned);
            Track(ParticleFactory.RingWave(to, color, startRadius: 0.15f, endRadius: 1.1f, duration: 0.4f, upright: false), spawned);
            Track(ParticleFactory.Burst(to + Vector3.up * 0.5f, color, size: 0.32f, count: 20, speed: 2.6f, lifetime: 0.4f), spawned);
        }

        private static void PlayLifeDrain(Vector3 from, Vector3 to, List<ParticleSystem> spawned)
        {
            var color = new Color(0.90f, 0.25f, 1f);
            var darkViolet = new Color(0.55f, 0.10f, 0.85f);
            ParticleFactory.Beam(from + Vector3.up * 0.6f, to + Vector3.up * 0.6f, color, duration: 0.55f, width: 0.14f);
            Track(ParticleFactory.Burst(to + Vector3.up * 0.6f, color, size: 0.38f, count: 24, speed: 2.2f, lifetime: 0.45f), spawned);
            Track(ParticleFactory.Burst(from + Vector3.up * 0.6f, darkViolet, size: 0.28f, count: 16, speed: 1.8f, lifetime: 0.4f), spawned);
        }

        private static void PlayFreeze(Vector3 from, Vector3 to, List<ParticleSystem> spawned)
        {
            var color = new Color(0.50f, 0.90f, 1f);
            var white = Color.white;
            Track(ParticleFactory.Streaks(from + Vector3.up * 0.5f, to + Vector3.up * 0.5f, color, duration: 0.26f, count: 18), spawned);
            Track(ParticleFactory.RingWave(to, color, startRadius: 0.15f, endRadius: 1.25f, duration: 0.45f, upright: false), spawned);
            Track(ParticleFactory.Burst(to + Vector3.up * 0.6f, white, size: 0.36f, count: 30, speed: 2.8f, lifetime: 0.45f), spawned);
        }

        private static void PlayBasicStrike(Vector3 from, Vector3 to, List<ParticleSystem> spawned)
        {
            var color = new Color(1f, 0.88f, 0.45f);
            var spark = new Color(1f, 0.60f, 0.20f);
            Track(ParticleFactory.Streaks(from + Vector3.up * 0.5f, to + Vector3.up * 0.5f, color, duration: 0.20f, count: 16), spawned);
            Track(ParticleFactory.Burst(to + Vector3.up * 0.5f, spark, size: 0.34f, count: 26, speed: 2.8f, lifetime: 0.35f), spawned);
            Track(ParticleFactory.RingWave(to, color, startRadius: 0.1f, endRadius: 0.85f, duration: 0.35f, upright: false), spawned);
        }

        private static void PlayEliminated(Vector3 at, List<ParticleSystem> spawned)
        {
            Track(ParticleFactory.Burst(at + Vector3.up * 0.5f, new Color(0.7f, 0.7f, 0.8f), size: 0.42f, count: 36, speed: 3.2f, lifetime: 0.6f), spawned);
            Track(ParticleFactory.RingWave(at, new Color(0.6f, 0.6f, 0.7f), startRadius: 0.2f, endRadius: 1.3f, duration: 0.5f, upright: false), spawned);
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
