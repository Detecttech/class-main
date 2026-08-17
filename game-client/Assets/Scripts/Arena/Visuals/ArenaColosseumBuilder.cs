using System.Collections.Generic;
using QuizBattle.Arena.Vfx;
using UnityEngine;

namespace QuizBattle.Arena.Visuals
{
    /// Procedurally constructs a vibrant Clash Royale-style miniature 3D Colosseum
    /// surrounding the active board. Includes stone stadium walls, corner bastion towers,
    /// royal heraldic banners, burning corner braziers, cartoon trees, wooden props,
    /// and a stylized sky with drifting clouds.
    public static class ArenaColosseumBuilder
    {
        public static GameObject Build(Transform parent, int width, int height, float tileSize)
        {
            var root = new GameObject("Colosseum_Environment");
            root.transform.SetParent(parent, false);

            float cx = (width - 1) * tileSize * 0.5f;
            float cz = (height - 1) * tileSize * 0.5f;
            var center = new Vector3(cx, 0f, cz);

            float halfW = (width * tileSize) * 0.5f;
            float halfH = (height * tileSize) * 0.5f;

            CreateSkyAndClouds(root.transform, center, Mathf.Max(halfW, halfH) + 12f);
            CreateStadiumWallsAndTowers(root.transform, center, halfW, halfH);
            CreateRoyalBanners(root.transform, center, halfW, halfH);
            CreateCornerBraziers(root.transform, center, halfW, halfH);
            CreateWaterMoatAndBridges(root.transform, center, halfW, halfH);
            CreateFoliageAndProps(root.transform, center, halfW, halfH);

            return root;
        }

        #region Sky & Clouds

        private static void CreateSkyAndClouds(Transform parent, Vector3 center, float radius)
        {
            var skyObj = new GameObject("SkyBackdrop");
            skyObj.transform.SetParent(parent, false);

            // Large curved backdrop cylinder for gradient sky
            var skyCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            skyCylinder.name = "SkyDome";
            skyCylinder.transform.SetParent(skyObj.transform, false);
            skyCylinder.transform.position = center + new Vector3(0f, 6f, 18f);
            skyCylinder.transform.localScale = new Vector3(radius * 3.5f, 18f, radius * 1.5f);
            skyCylinder.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            Object.Destroy(skyCylinder.GetComponent<Collider>());

            var skyStyle = new ToonStyle
            {
                ShadowTint = QuizBattlePalette.SkyHorizon,
                RimColor = Color.white,
                RimIntensity = 0.2f,
                EmissionColor = QuizBattlePalette.SkyZenith,
                EmissionIntensity = 0.8f,
                OutlineEnabled = false,
            };
            skyCylinder.GetComponent<Renderer>().sharedMaterial = ToonMaterialFactory.Toon(QuizBattlePalette.SkyZenith, skyStyle);

            // Puffy stylized cartoon clouds floating in the upper horizon
            var cloudsContainer = new GameObject("Clouds");
            cloudsContainer.transform.SetParent(skyObj.transform, false);

            Vector3[] cloudPositions = {
                center + new Vector3(-12f, 10f, 14f),
                center + new Vector3(-3f, 12f, 16f),
                center + new Vector3(7f, 11f, 15f),
                center + new Vector3(14f, 9.5f, 13f),
                center + new Vector3(0f, 8.5f, 12f),
            };

            float[] cloudScales = { 1.3f, 1.6f, 1.2f, 1.4f, 1.0f };

            for (int i = 0; i < cloudPositions.Length; i++)
            {
                CreateCloud(cloudsContainer.transform, cloudPositions[i], cloudScales[i]);
            }
        }

        private static void CreateCloud(Transform parent, Vector3 pos, float scale)
        {
            var cloud = new GameObject("Cloud");
            cloud.transform.SetParent(parent, false);
            cloud.transform.position = pos;
            cloud.transform.localScale = Vector3.one * scale;

            var cloudStyle = new ToonStyle
            {
                ShadowTint = QuizBattlePalette.CloudShadow,
                RimColor = Color.white,
                RimIntensity = 0.4f,
                EmissionColor = Color.black,
                EmissionIntensity = 0f,
                OutlineColor = QuizBattlePalette.OutlineColor,
                OutlineWidth = 1.0f,
                OutlineEnabled = true,
            };
            var cloudMat = ToonMaterialFactory.Toon(QuizBattlePalette.CloudWhite, cloudStyle);

            // Cluster of 4 overlapping spheres to form a fluffy cloud
            Vector3[] offsets = {
                new Vector3(0f, 0f, 0f),
                new Vector3(-0.9f, -0.2f, 0f),
                new Vector3(0.9f, -0.15f, 0.1f),
                new Vector3(0.1f, 0.5f, -0.1f),
            };
            Vector3[] sphereScales = {
                new Vector3(2.0f, 1.2f, 1.4f),
                new Vector3(1.3f, 1.0f, 1.1f),
                new Vector3(1.4f, 1.1f, 1.2f),
                new Vector3(1.2f, 0.9f, 1.0f),
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.transform.SetParent(cloud.transform, false);
                puff.transform.localPosition = offsets[i];
                puff.transform.localScale = sphereScales[i];
                Object.Destroy(puff.GetComponent<Collider>());
                puff.GetComponent<Renderer>().sharedMaterial = cloudMat;
            }

            var drifter = cloud.AddComponent<ColosseumCloudDrifter>();
            drifter.Speed = Random.Range(0.25f, 0.45f);
            drifter.BobSpeed = Random.Range(0.8f, 1.4f);
            drifter.BobAmount = Random.Range(0.1f, 0.2f);
        }

        #endregion

        #region Stadium Walls & Towers

        private static void CreateStadiumWallsAndTowers(Transform parent, Vector3 center, float halfW, float halfH)
        {
            var stadium = new GameObject("Stadium");
            stadium.transform.SetParent(parent, false);

            var wallStyle = new ToonStyle
            {
                ShadowTint = QuizBattlePalette.StoneDark,
                RimColor = Color.white,
                RimIntensity = 0.35f,
                EmissionColor = Color.black,
                EmissionIntensity = 0f,
                OutlineColor = QuizBattlePalette.OutlineColor,
                OutlineWidth = 1.4f,
                OutlineEnabled = true,
            };
            var wallMat = ToonMaterialFactory.Toon(QuizBattlePalette.StoneWall, wallStyle);

            var woodStyle = new ToonStyle
            {
                ShadowTint = QuizBattlePalette.WoodDark,
                RimColor = Color.white,
                RimIntensity = 0.3f,
                EmissionColor = Color.black,
                EmissionIntensity = 0f,
                OutlineColor = QuizBattlePalette.OutlineColor,
                OutlineWidth = 1.2f,
                OutlineEnabled = true,
            };
            var woodMat = ToonMaterialFactory.Toon(QuizBattlePalette.WoodPlank, woodStyle);

            float wallThick = 1.2f;
            float wallH = 1.8f;
            float margin = 1.6f;

            // North Wall (Opponent / Goal side)
            CreateWallSection(stadium.transform, center + new Vector3(0f, wallH * 0.5f - 0.7f, halfH + margin),
                new Vector3(halfW * 2f + margin * 2f, wallH, wallThick), wallMat, crenellations: true);

            // South Wall (Player side)
            CreateWallSection(stadium.transform, center + new Vector3(0f, wallH * 0.5f - 0.7f, -halfH - margin),
                new Vector3(halfW * 2f + margin * 2f, wallH, wallThick), wallMat, crenellations: true);

            // East Wall & West Wall (with tiered spectator benches)
            CreateWallSection(stadium.transform, center + new Vector3(halfW + margin + 1.2f, wallH * 0.5f - 0.7f, 0f),
                new Vector3(wallThick, wallH, halfH * 2f + margin * 2f), wallMat, crenellations: false);

            CreateWallSection(stadium.transform, center + new Vector3(-halfW - margin - 1.2f, wallH * 0.5f - 0.7f, 0f),
                new Vector3(wallThick, wallH, halfH * 2f + margin * 2f), wallMat, crenellations: false);

            // Spectator stands on West and East flanks
            CreateSpectatorBenches(stadium.transform, center + new Vector3(-halfW - margin - 0.4f, 0f, 0f), halfH * 2f + 1f, woodMat, isWest: true);
            CreateSpectatorBenches(stadium.transform, center + new Vector3(halfW + margin + 0.4f, 0f, 0f), halfH * 2f + 1f, woodMat, isWest: false);

            // 4 Corner Bastion Towers
            Vector3[] towerPositions = {
                center + new Vector3(-halfW - margin, 0f, -halfH - margin), // South-West (Blue)
                center + new Vector3(halfW + margin, 0f, -halfH - margin),  // South-East (Blue)
                center + new Vector3(-halfW - margin, 0f, halfH + margin),  // North-West (Red)
                center + new Vector3(halfW + margin, 0f, halfH + margin),   // North-East (Red)
            };

            for (int i = 0; i < towerPositions.Length; i++)
            {
                bool isNorth = i >= 2;
                Color roofColor = isNorth ? QuizBattlePalette.RoofTilesRed : QuizBattlePalette.RoofTilesBlue;
                CreateTower(stadium.transform, towerPositions[i], wallMat, roofColor);
            }
        }

        private static void CreateWallSection(Transform parent, Vector3 pos, Vector3 size, Material wallMat, bool crenellations)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "WallSection";
            wall.transform.SetParent(parent, false);
            wall.transform.position = pos;
            wall.transform.localScale = size;
            Object.Destroy(wall.GetComponent<Collider>());
            wall.GetComponent<Renderer>().sharedMaterial = wallMat;

            if (crenellations)
            {
                int count = Mathf.Max(3, Mathf.RoundToInt(size.x / 1.1f));
                float step = size.x / count;
                float startX = -size.x * 0.5f + step * 0.5f;

                for (int i = 0; i < count; i += 2)
                {
                    var cren = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cren.name = "Crenellation";
                    cren.transform.SetParent(parent, false);
                    cren.transform.position = pos + new Vector3(startX + i * step, size.y * 0.5f + 0.18f, 0f);
                    cren.transform.localScale = new Vector3(step * 0.85f, 0.35f, size.z * 1.05f);
                    Object.Destroy(cren.GetComponent<Collider>());
                    cren.GetComponent<Renderer>().sharedMaterial = wallMat;
                }
            }
        }

        private static void CreateSpectatorBenches(Transform parent, Vector3 pos, float length, Material woodMat, bool isWest)
        {
            var stands = new GameObject(isWest ? "West_Stands" : "East_Stands");
            stands.transform.SetParent(parent, false);
            stands.transform.position = pos;

            float dir = isWest ? -1f : 1f;
            for (int tier = 0; tier < 3; tier++)
            {
                var bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bench.name = $"Bench_Tier_{tier}";
                bench.transform.SetParent(stands.transform, false);
                bench.transform.localPosition = new Vector3(dir * tier * 0.45f, -0.2f + tier * 0.25f, 0f);
                bench.transform.localScale = new Vector3(0.4f, 0.2f, length);
                Object.Destroy(bench.GetComponent<Collider>());
                bench.GetComponent<Renderer>().sharedMaterial = woodMat;
            }
        }

        private static void CreateTower(Transform parent, Vector3 pos, Material wallMat, Color roofColor)
        {
            var tower = new GameObject("BastionTower");
            tower.transform.SetParent(parent, false);
            tower.transform.position = pos;

            // Stone cylinder shaft
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "TowerShaft";
            shaft.transform.SetParent(tower.transform, false);
            shaft.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            shaft.transform.localScale = new Vector3(1.6f, 1.4f, 1.6f);
            Object.Destroy(shaft.GetComponent<Collider>());
            shaft.GetComponent<Renderer>().sharedMaterial = wallMat;

            // Stone balcony rim
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "TowerRim";
            rim.transform.SetParent(tower.transform, false);
            rim.transform.localPosition = new Vector3(0f, 2.55f, 0f);
            rim.transform.localScale = new Vector3(1.85f, 0.15f, 1.85f);
            Object.Destroy(rim.GetComponent<Collider>());
            rim.GetComponent<Renderer>().sharedMaterial = wallMat;

            // Conical Roof
            var roof = new GameObject("TowerRoof");
            roof.transform.SetParent(tower.transform, false);
            roof.transform.localPosition = new Vector3(0f, 2.62f, 0f);
            var mf = roof.AddComponent<MeshFilter>();
            var mr = roof.AddComponent<MeshRenderer>();
            mf.sharedMesh = PrimitiveMeshFactory.Cone(16, 1.0f, 0.05f, 1.6f);

            var roofStyle = new ToonStyle
            {
                ShadowTint = QuizBattlePalette.ShadowTint,
                RimColor = Color.white,
                RimIntensity = 0.45f,
                EmissionColor = Color.black,
                EmissionIntensity = 0f,
                OutlineColor = QuizBattlePalette.OutlineColor,
                OutlineWidth = 1.4f,
                OutlineEnabled = true,
            };
            mr.sharedMaterial = ToonMaterialFactory.Toon(roofColor, roofStyle);

            // Golden spire at the apex
            var spire = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spire.name = "GoldSpire";
            spire.transform.SetParent(tower.transform, false);
            spire.transform.localPosition = new Vector3(0f, 4.28f, 0f);
            spire.transform.localScale = Vector3.one * 0.28f;
            Object.Destroy(spire.GetComponent<Collider>());
            spire.GetComponent<Renderer>().sharedMaterial = ToonMaterialFactory.Toon(QuizBattlePalette.GoldTrim, ToonStyle.Default);
        }

        #endregion

        #region Royal Banners

        private static void CreateRoyalBanners(Transform parent, Vector3 center, float halfW, float halfH)
        {
            var banners = new GameObject("RoyalBanners");
            banners.transform.SetParent(parent, false);

            float margin = 1.55f;

            // Blue team banners on South wall
            CreateBanner(banners.transform, center + new Vector3(-halfW * 0.5f, 0.4f, -halfH - margin + 0.1f), QuizBattlePalette.BannerBlue, true);
            CreateBanner(banners.transform, center + new Vector3(halfW * 0.5f, 0.4f, -halfH - margin + 0.1f), QuizBattlePalette.BannerBlue, true);

            // Red team banners on North wall
            CreateBanner(banners.transform, center + new Vector3(-halfW * 0.5f, 0.4f, halfH + margin - 0.1f), QuizBattlePalette.BannerRed, false);
            CreateBanner(banners.transform, center + new Vector3(halfW * 0.5f, 0.4f, halfH + margin - 0.1f), QuizBattlePalette.BannerRed, false);
        }

        private static void CreateBanner(Transform parent, Vector3 pos, Color bannerColor, bool facingNorth)
        {
            var banner = new GameObject("Banner");
            banner.transform.SetParent(parent, false);
            banner.transform.position = pos;
            banner.transform.rotation = facingNorth ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);

            var bannerStyle = new ToonStyle
            {
                ShadowTint = QuizBattlePalette.ShadowTint,
                RimColor = Color.white,
                RimIntensity = 0.35f,
                EmissionColor = Color.black,
                EmissionIntensity = 0f,
                OutlineColor = QuizBattlePalette.OutlineColor,
                OutlineWidth = 1.2f,
                OutlineEnabled = true,
            };
            var bannerMat = ToonMaterialFactory.Toon(bannerColor, bannerStyle);

            // Main fabric cloth
            var cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cloth.name = "Cloth";
            cloth.transform.SetParent(banner.transform, false);
            cloth.transform.localPosition = new Vector3(0f, 0f, 0f);
            cloth.transform.localScale = new Vector3(0.75f, 1.2f, 0.04f);
            Object.Destroy(cloth.GetComponent<Collider>());
            cloth.GetComponent<Renderer>().sharedMaterial = bannerMat;

            // Golden mounting bar
            var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rod.name = "Rod";
            rod.transform.SetParent(banner.transform, false);
            rod.transform.localPosition = new Vector3(0f, 0.62f, 0.02f);
            rod.transform.localScale = new Vector3(0.08f, 0.45f, 0.08f);
            rod.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            Object.Destroy(rod.GetComponent<Collider>());
            rod.GetComponent<Renderer>().sharedMaterial = ToonMaterialFactory.Toon(QuizBattlePalette.BannerGoldTrim, ToonStyle.Default);

            // Golden crest emblem
            var emblem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            emblem.name = "Emblem";
            emblem.transform.SetParent(banner.transform, false);
            emblem.transform.localPosition = new Vector3(0f, 0.15f, 0.03f);
            emblem.transform.localScale = new Vector3(0.24f, 0.02f, 0.24f);
            emblem.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Object.Destroy(emblem.GetComponent<Collider>());
            emblem.GetComponent<Renderer>().sharedMaterial = ToonMaterialFactory.Toon(QuizBattlePalette.BannerGoldTrim, ToonStyle.Default);
        }

        #endregion

        #region Corner Braziers & Torches

        private static void CreateCornerBraziers(Transform parent, Vector3 center, float halfW, float halfH)
        {
            var braziers = new GameObject("CornerBraziers");
            braziers.transform.SetParent(parent, false);

            float offsetW = halfW + 0.35f;
            float offsetH = halfH + 0.35f;

            Vector3[] brazierPositions = {
                center + new Vector3(-offsetW, 0f, -offsetH),
                center + new Vector3(offsetW, 0f, -offsetH),
                center + new Vector3(-offsetW, 0f, offsetH),
                center + new Vector3(offsetW, 0f, offsetH),
            };

            foreach (var pos in brazierPositions)
            {
                CreateBrazier(braziers.transform, pos);
            }
        }

        private static void CreateBrazier(Transform parent, Vector3 pos)
        {
            var brazier = new GameObject("Brazier");
            brazier.transform.SetParent(parent, false);
            brazier.transform.position = pos;

            var stoneMat = ToonMaterialFactory.Toon(QuizBattlePalette.StoneDark, ToonStyle.Default);
            var goldMat = ToonMaterialFactory.Toon(QuizBattlePalette.BrazierGold, ToonStyle.Default);

            // Stone base pedestal
            var basePed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            basePed.name = "Base";
            basePed.transform.SetParent(brazier.transform, false);
            basePed.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            basePed.transform.localScale = new Vector3(0.6f, 0.35f, 0.6f);
            Object.Destroy(basePed.GetComponent<Collider>());
            basePed.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // Stone pillar
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Pillar";
            pillar.transform.SetParent(brazier.transform, false);
            pillar.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            pillar.transform.localScale = new Vector3(0.42f, 0.38f, 0.42f);
            Object.Destroy(pillar.GetComponent<Collider>());
            pillar.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // Golden iron bowl / cauldron
            var bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bowl.name = "Bowl";
            bowl.transform.SetParent(brazier.transform, false);
            bowl.transform.localPosition = new Vector3(0f, 0.76f, 0f);
            bowl.transform.localScale = new Vector3(0.65f, 0.12f, 0.65f);
            Object.Destroy(bowl.GetComponent<Collider>());
            bowl.GetComponent<Renderer>().sharedMaterial = goldMat;

            // Glowing hot coal / fire core
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "FireCore";
            core.transform.SetParent(brazier.transform, false);
            core.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            core.transform.localScale = new Vector3(0.38f, 0.22f, 0.38f);
            Object.Destroy(core.GetComponent<Collider>());
            core.GetComponent<Renderer>().sharedMaterial =
                ToonMaterialFactory.Glow(QuizBattlePalette.FireGlow, intensity: 2.4f, softEdge: 0.3f, pulseSpeed: 5.0f, pulseAmount: 0.25f);

            // Warm flickering point light
            var lightObj = new GameObject("BrazierLight");
            lightObj.transform.SetParent(brazier.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = QuizBattlePalette.FireGlow;
            light.range = 3.2f;
            light.intensity = 1.3f;
            light.shadows = LightShadows.None;

            var flicker = lightObj.AddComponent<ColosseumLightFlicker>();
            flicker.BaseIntensity = 1.3f;
            flicker.FlickerSpeed = 8f;
            flicker.FlickerAmount = 0.35f;

            // Animated stylized flame particles
            CreateBrazierParticles(brazier.transform, new Vector3(0f, 0.88f, 0f));
        }

        private static void CreateBrazierParticles(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("FireParticles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1.0f;
            main.loop = true;
            main.startLifetime = 0.55f;
            main.startSpeed = 0.85f;
            main.startSize = 0.22f;
            main.startColor = QuizBattlePalette.FireGlow;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 14f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.World;
            vol.y = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));

            var col = ps.colorOverLifetime;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.9f, 0.3f), 0f), new GradientColorKey(QuizBattlePalette.FireGlow, 0.6f), new GradientColorKey(new Color(0.8f, 0.2f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.enabled = true;
            col.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = ToonMaterialFactory.GlowInstance(QuizBattlePalette.FireGlow, intensity: 2.2f, softEdge: 0.4f);

            ps.Play();
        }

        #endregion

        #region Moat & Bridges

        private static void CreateWaterMoatAndBridges(Transform parent, Vector3 center, float halfW, float halfH)
        {
            var moat = new GameObject("MoatChannel");
            moat.transform.SetParent(parent, false);

            // Water stream along the eastern perimeter
            var water = GameObject.CreatePrimitive(PrimitiveType.Cube);
            water.name = "Water";
            water.transform.SetParent(moat.transform, false);
            water.transform.position = center + new Vector3(halfW + 0.95f, -0.62f, 0f);
            water.transform.localScale = new Vector3(1.1f, 0.2f, halfH * 2f + 2f);
            Object.Destroy(water.GetComponent<Collider>());

            var waterStyle = new ToonStyle
            {
                ShadowTint = new Color(0.12f, 0.36f, 0.65f),
                RimColor = QuizBattlePalette.WaterFoam,
                RimIntensity = 0.7f,
                EmissionColor = new Color(0.08f, 0.22f, 0.45f),
                EmissionIntensity = 0.3f,
                OutlineColor = QuizBattlePalette.OutlineColor,
                OutlineWidth = 0.8f,
                OutlineEnabled = true,
            };
            water.GetComponent<Renderer>().sharedMaterial = ToonMaterialFactory.Toon(QuizBattlePalette.WaterBlue, waterStyle);

            // Wooden arched bridges crossing the moat
            CreateBridge(moat.transform, center + new Vector3(halfW + 0.95f, -0.42f, -halfH * 0.4f));
            CreateBridge(moat.transform, center + new Vector3(halfW + 0.95f, -0.42f, halfH * 0.4f));
        }

        private static void CreateBridge(Transform parent, Vector3 pos)
        {
            var bridge = new GameObject("Bridge");
            bridge.transform.SetParent(parent, false);
            bridge.transform.position = pos;

            var woodMat = ToonMaterialFactory.Toon(QuizBattlePalette.WoodPlank, ToonStyle.Default);

            // Plank road
            var planks = GameObject.CreatePrimitive(PrimitiveType.Cube);
            planks.name = "Planks";
            planks.transform.SetParent(bridge.transform, false);
            planks.transform.localScale = new Vector3(1.4f, 0.12f, 0.9f);
            Object.Destroy(planks.GetComponent<Collider>());
            planks.GetComponent<Renderer>().sharedMaterial = woodMat;

            // Left & Right railings
            for (int r = -1; r <= 1; r += 2)
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = "Rail";
                rail.transform.SetParent(bridge.transform, false);
                rail.transform.localPosition = new Vector3(0f, 0.18f, r * 0.42f);
                rail.transform.localScale = new Vector3(1.4f, 0.16f, 0.08f);
                Object.Destroy(rail.GetComponent<Collider>());
                rail.GetComponent<Renderer>().sharedMaterial = woodMat;
            }
        }

        #endregion

        #region Foliage & Decorative Props

        private static void CreateFoliageAndProps(Transform parent, Vector3 center, float halfW, float halfH)
        {
            var props = new GameObject("FoliageAndProps");
            props.transform.SetParent(parent, false);

            // Low-poly cartoon trees in outer corners
            Vector3[] treePositions = {
                center + new Vector3(-halfW - 3.4f, -0.5f, -halfH - 2.8f),
                center + new Vector3(-halfW - 3.8f, -0.5f, 0f),
                center + new Vector3(-halfW - 3.2f, -0.5f, halfH + 2.6f),
                center + new Vector3(halfW + 3.2f, -0.5f, -halfH - 2.8f),
                center + new Vector3(halfW + 3.6f, -0.5f, halfH + 2.8f),
                center + new Vector3(0f, -0.5f, halfH + 3.4f),
            };

            foreach (var tPos in treePositions)
            {
                CreateCartoonTree(props.transform, tPos);
            }

            // Wooden crates & barrels
            CreateBarrel(props.transform, center + new Vector3(-halfW - 0.75f, -0.3f, -halfH + 0.4f));
            CreateCrate(props.transform, center + new Vector3(-halfW - 0.75f, -0.35f, -halfH - 0.15f));
            CreateBarrel(props.transform, center + new Vector3(halfW + 0.35f, -0.3f, halfH - 0.3f));
        }

        private static void CreateCartoonTree(Transform parent, Vector3 pos)
        {
            var tree = new GameObject("CartoonTree");
            tree.transform.SetParent(parent, false);
            tree.transform.position = pos;

            var trunkMat = ToonMaterialFactory.Toon(QuizBattlePalette.WoodDark, ToonStyle.Default);
            var leafMat = ToonMaterialFactory.Toon(QuizBattlePalette.FoliageGreen, ToonStyle.Default);

            // Trunk
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            trunk.transform.localScale = new Vector3(0.45f, 0.9f, 0.45f);
            Object.Destroy(trunk.GetComponent<Collider>());
            trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;

            // Puffy foliage crown (3 overlapping spheres)
            Vector3[] leafOffsets = {
                new Vector3(0f, 2.1f, 0f),
                new Vector3(-0.35f, 2.6f, 0.15f),
                new Vector3(0.35f, 2.5f, -0.2f),
                new Vector3(0f, 3.1f, 0f),
            };
            float[] leafScales = { 1.6f, 1.35f, 1.3f, 1.1f };

            for (int i = 0; i < leafOffsets.Length; i++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.name = "FoliagePuff";
                puff.transform.SetParent(tree.transform, false);
                puff.transform.localPosition = leafOffsets[i];
                puff.transform.localScale = Vector3.one * leafScales[i];
                Object.Destroy(puff.GetComponent<Collider>());
                puff.GetComponent<Renderer>().sharedMaterial = leafMat;
            }
        }

        private static void CreateBarrel(Transform parent, Vector3 pos)
        {
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "WoodenBarrel";
            barrel.transform.SetParent(parent, false);
            barrel.transform.position = pos;
            barrel.transform.localScale = new Vector3(0.42f, 0.32f, 0.42f);
            Object.Destroy(barrel.GetComponent<Collider>());
            barrel.GetComponent<Renderer>().sharedMaterial = ToonMaterialFactory.Toon(QuizBattlePalette.WoodPlank, ToonStyle.Default);
        }

        private static void CreateCrate(Transform parent, Vector3 pos)
        {
            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "WoodenCrate";
            crate.transform.SetParent(parent, false);
            crate.transform.position = pos;
            crate.transform.localScale = new Vector3(0.48f, 0.48f, 0.48f);
            Object.Destroy(crate.GetComponent<Collider>());
            crate.GetComponent<Renderer>().sharedMaterial = ToonMaterialFactory.Toon(QuizBattlePalette.WoodDark, ToonStyle.Default);
        }

        #endregion
    }

    /// Animates subtle floating drift for cartoon clouds.
    public class ColosseumCloudDrifter : MonoBehaviour
    {
        public float Speed = 0.3f;
        public float BobSpeed = 1f;
        public float BobAmount = 0.15f;

        private Vector3 _startPos;
        private float _seed;

        private void Awake()
        {
            _startPos = transform.position;
            _seed = Random.Range(0f, 50f);
        }

        private void Update()
        {
            float t = Time.time + _seed;
            var pos = _startPos;
            pos.x += Mathf.Sin(t * Speed * 0.2f) * 1.5f;
            pos.y += Mathf.Sin(t * BobSpeed) * BobAmount;
            transform.position = pos;
        }
    }

    /// Animates subtle warmth and light flicker for braziers.
    public class ColosseumLightFlicker : MonoBehaviour
    {
        public float BaseIntensity = 1.3f;
        public float FlickerSpeed = 8f;
        public float FlickerAmount = 0.3f;

        private Light _light;
        private float _seed;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _seed = Random.Range(0f, 100f);
        }

        private void Update()
        {
            if (_light == null) return;
            float noise = Mathf.PerlinNoise((Time.time + _seed) * FlickerSpeed, 0f);
            _light.intensity = BaseIntensity + (noise - 0.5f) * FlickerAmount * 2f;
        }
    }
}
