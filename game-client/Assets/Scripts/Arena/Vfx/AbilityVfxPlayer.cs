using System.Collections.Generic;
using UnityEngine;

namespace QuizBattle.Arena.Vfx
{
    public static class AbilityVfxPlayer
    {
        public static List<ParticleSystem> Play(string vfxTag, Vector3 from, Vector3 to, bool eliminated)
        {
            var spawned = new List<ParticleSystem>(16);
            float travel = Mathf.Clamp(Vector3.Distance(from, to) / 9f, 0.24f, 0.55f);
            float impact;
            switch (vfxTag)
            {
            case "vfx_fireball":
                impact = PlayFireball(from, to, travel, spawned);
                break;
            case "vfx_shield_shimmer":
                impact = PlayShieldShimmer(to, spawned);
                break;
            case "vfx_wind_trail":
                impact = PlayWindTrail(from, to, travel, spawned);
                break;
            case "vfx_life_drain":
                impact = PlayLifeDrain(from, to, travel, spawned);
                break;
            case "vfx_freeze":
                impact = PlayFreeze(from, to, travel, spawned);
                break;
            default:
                impact = PlayBasicStrike(from, to, travel, spawned);
                break;
            }
            if (eliminated) PlayEliminated(to, impact + 0.06f, spawned);
            return spawned;
        }

        private static float PlayFireball(Vector3 from, Vector3 to, float travel, List<ParticleSystem> spawned)
        {
            var flame = new Color(1f, 0.24f, 0.035f);
            var gold = new Color(1f, 0.78f, 0.2f);
            Vector3 origin = from + Vector3.up * 0.8f;
            Vector3 target = to + Vector3.up * 0.65f;
            float impact = 0.1f + travel;
            Track(ParticleFactory.RingWave(origin, flame, 0.45f, 0.08f, 0.13f), spawned);
            Track(ParticleFactory.MeshPulse(origin, gold, ParticleFactory.Form.Orb, Vector3.one * 0.32f, 0.14f), spawned);
            Track(ParticleFactory.Projectile(origin, target, flame, ParticleFactory.Form.Orb, Vector3.one * 0.58f, travel, 0.45f), spawned, 0.1f);
            Track(ParticleFactory.Projectile(origin, target, gold, ParticleFactory.Form.Orb, Vector3.one * 0.3f, travel, 0.45f), spawned, 0.1f);
            Track(ParticleFactory.MeshPulse(target, gold, ParticleFactory.Form.Orb, Vector3.one * 0.85f, 0.16f), spawned, impact);
            Track(ParticleFactory.Burst(target, flame, 0.23f, 16, 3.2f, 0.42f), spawned, impact);
            Track(ParticleFactory.Burst(target, gold, 0.1f, 10, 4.6f, 0.32f), spawned, impact);
            Track(ParticleFactory.RingWave(to + Vector3.up * 0.08f, flame, 0.15f, 1.15f, 0.38f, false), spawned, impact);
            return impact;
        }

        private static float PlayShieldShimmer(Vector3 at, List<ParticleSystem> spawned)
        {
            var blue = new Color(0.18f, 0.62f, 1f);
            var gold = new Color(1f, 0.83f, 0.38f);
            Vector3 basePosition = at + Vector3.up * 0.08f;
            Track(ParticleFactory.RingWave(basePosition, gold, 0.75f, 0.25f, 0.16f, false), spawned);
            Track(ParticleFactory.MeshPulse(basePosition, new Color(0.18f, 0.52f, 1f, 0.24f), ParticleFactory.Form.Shield,
                                            new Vector3(0.85f, 1.65f, 0.85f), 0.78f), spawned, 0.1f);
            Track(ParticleFactory.RingWave(basePosition, blue, 0.4f, 0.92f, 0.7f, false), spawned, 0.1f);
            Track(ParticleFactory.RingWave(at + Vector3.up * 0.8f, gold, 0.62f, 0.85f, 0.5f), spawned, 0.24f);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.72f;
                Track(ParticleFactory.Projectile(basePosition + offset, basePosition + offset + Vector3.up * 1.3f,
                                                 gold, ParticleFactory.Form.Crystal, new Vector3(0.1f, 0.1f, 0.16f), 0.38f), spawned, 0.1f + i * 0.025f);
            }
            return 0.24f;
        }

        private static float PlayWindTrail(Vector3 from, Vector3 to, float travel, List<ParticleSystem> spawned)
        {
            var mint = new Color(0.3f, 1f, 0.67f);
            var white = new Color(0.8f, 1f, 0.92f);
            Vector3 origin = from + Vector3.up * 0.65f;
            Vector3 target = to + Vector3.up * 0.65f;
            travel *= 0.8f;
            float impact = 0.08f + travel;
            Track(ParticleFactory.RingWave(from + Vector3.up * 0.1f, mint, 0.6f, 0.15f, 0.13f, false), spawned);
            for (int i = 0; i < 3; i++)
            {
                Track(ParticleFactory.Projectile(origin, target, i == 1 ? white : mint, ParticleFactory.Form.Crescent,
                                                 Vector3.one * (0.9f + i * 0.15f), travel, 0.15f, i * 120f), spawned, 0.08f + i * 0.035f);
            }
            Track(ParticleFactory.Streaks(target, target + Vector3.up * 1.1f, white, 0.28f, 8), spawned, impact);
            Track(ParticleFactory.RingWave(to + Vector3.up * 0.1f, mint, 0.2f, 1.1f, 0.32f, false), spawned, impact);
            Track(ParticleFactory.MeshPulse(target, white, ParticleFactory.Form.Crescent, Vector3.one * 1.4f, 0.26f,
                                            Quaternion.Euler(35f, 30f, 65f)), spawned, impact + 0.07f);
            return impact;
        }

        private static float PlayLifeDrain(Vector3 from, Vector3 to, float travel, List<ParticleSystem> spawned)
        {
            var violet = new Color(0.62f, 0.16f, 1f);
            var pink = new Color(1f, 0.38f, 0.8f);
            Vector3 origin = from + Vector3.up * 0.85f;
            Vector3 target = to + Vector3.up * 0.75f;
            float impact = 0.1f + travel;
            Track(ParticleFactory.RingWave(origin, violet, 0.45f, 0.12f, 0.16f), spawned);
            Track(ParticleFactory.Projectile(origin, target, violet, ParticleFactory.Form.Crystal,
                                             new Vector3(0.22f, 0.22f, 0.35f), travel, 0.25f), spawned, 0.1f);
            Track(ParticleFactory.RingWave(to + Vector3.up * 0.1f, violet, 0.85f, 0.18f, 0.48f, false), spawned, impact);
            Track(ParticleFactory.Burst(target, pink, 0.14f, 8, 1.2f, 0.22f), spawned, impact);
            for (int i = 0; i < 3; i++)
            {
                float delay = impact + 0.06f + i * 0.07f;
                Track(ParticleFactory.Projectile(target, origin, i == 1 ? pink : violet, ParticleFactory.Form.Orb,
                                                 Vector3.one * 0.21f, travel, 0.3f + i * 0.22f), spawned, delay);
            }
            Track(ParticleFactory.RingWave(origin, pink, 0.15f, 0.6f, 0.3f), spawned, impact + 0.06f + travel);
            Track(ParticleFactory.Burst(origin, violet, 0.12f, 8, 1.5f, 0.3f), spawned, impact + 0.2f + travel);
            return impact;
        }

        private static float PlayFreeze(Vector3 from, Vector3 to, float travel, List<ParticleSystem> spawned)
        {
            var ice = new Color(0.22f, 0.76f, 1f);
            var white = new Color(0.8f, 0.97f, 1f);
            Vector3 origin = from + Vector3.up * 0.8f;
            Vector3 target = to + Vector3.up * 0.6f;
            float impact = 0.12f + travel;
            Track(ParticleFactory.MeshPulse(origin, white, ParticleFactory.Form.Crystal,
                                            new Vector3(0.25f, 0.25f, 0.45f), 0.17f, Quaternion.Euler(0f, 0f, 45f)), spawned);
            Track(ParticleFactory.Projectile(origin, target, ice, ParticleFactory.Form.Crystal,
                                             new Vector3(0.32f, 0.32f, 0.55f), travel), spawned, 0.12f);
            Track(ParticleFactory.RingWave(to + Vector3.up * 0.09f, ice, 0.15f, 0.95f, 0.38f, false), spawned, impact);
            Track(ParticleFactory.Burst(target, white, 0.12f, 12, 2.2f, 0.32f), spawned, impact);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 0.53f;
                Track(ParticleFactory.MeshPulse(to + offset + Vector3.up * 0.45f, i % 2 == 0 ? ice : white,
                                                ParticleFactory.Form.Crystal, new Vector3(0.22f, 0.22f, 0.55f), 0.55f,
                                                Quaternion.Euler(-72f, angle, 0f)), spawned, impact + i * 0.025f);
            }
            return impact;
        }

        private static float PlayBasicStrike(Vector3 from, Vector3 to, float travel, List<ParticleSystem> spawned)
        {
            var gold = new Color(1f, 0.78f, 0.32f);
            var white = new Color(1f, 0.95f, 0.76f);
            Vector3 origin = from + Vector3.up * 0.7f;
            Vector3 target = to + Vector3.up * 0.7f;
            travel *= 0.65f;
            float impact = 0.07f + travel;
            Track(ParticleFactory.MeshPulse(origin, gold, ParticleFactory.Form.Slash, Vector3.one * 0.65f,
                                            0.13f, Quaternion.Euler(20f, 0f, -35f)), spawned);
            Track(ParticleFactory.Projectile(origin, target, white, ParticleFactory.Form.Slash, Vector3.one * 0.9f,
                                             travel, 0.18f, -35f), spawned, 0.07f);
            Track(ParticleFactory.MeshPulse(target, white, ParticleFactory.Form.Slash, new Vector3(1.25f, 1.25f, 1f),
                                            0.19f, Quaternion.Euler(20f, 0f, -40f)), spawned, impact);
            Track(ParticleFactory.MeshPulse(target, gold, ParticleFactory.Form.Slash, Vector3.one,
                                            0.17f, Quaternion.Euler(-20f, 15f, 135f)), spawned, impact + 0.045f);
            Track(ParticleFactory.Burst(target, gold, 0.1f, 10, 3f, 0.25f), spawned, impact);
            return impact;
        }

        private static void PlayEliminated(Vector3 at, float delay, List<ParticleSystem> spawned)
        {
            var ash = new Color(0.5f, 0.48f, 0.7f);
            Track(ParticleFactory.Burst(at + Vector3.up * 0.5f, ash, 0.18f, 14, 2f, 0.55f), spawned, delay);
            Track(ParticleFactory.RingWave(at + Vector3.up * 0.1f, new Color(1f, 0.72f, 0.3f),
                                           0.3f, 1.2f, 0.42f, false), spawned, delay);
        }

        private static void Track(GameObject go, List<ParticleSystem> spawned, float delay = 0f)
        {
            if (delay > 0f) ParticleFactory.Delay(go, delay);
            var ps = go.GetComponent<ParticleSystem>();
            spawned.Add(ps);
        }

        public static void SimulateAll(IEnumerable<ParticleSystem> systems, float t)
        {
            if (systems == null) return;
            t = float.IsNaN(t) || float.IsInfinity(t) ? 0f : Mathf.Max(0f, t);
            foreach (var ps in systems)
            {
                if (ps == null) continue;
                var main = ps.main;
                var stopAction = main.stopAction;
                main.stopAction = ParticleSystemStopAction.None;
                ps.Simulate(t, true, true, true);
                main.stopAction = stopAction;
            }
        }
    }
}
