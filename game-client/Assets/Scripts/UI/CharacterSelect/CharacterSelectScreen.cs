using QuizBattle.Arena;
using QuizBattle.Arena.Visuals;
using QuizBattle.Bootstrap;
using QuizBattle.Characters;
using QuizBattle.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QuizBattle.UI.CharacterSelect
{
    /// Shows the 4 v1 characters as cards with a live 3D preview of their actual in-game
    /// model (same CharacterVisualBuilder used in the arena — imported ithappy bodies,
    /// tinted + accessorized per archetype); picking one sends select_character. Picks
    /// aren't exclusive — multiple players can play as the same character (see
    /// server/src/matchEngine/LiveMatchRegistry.ts selectCharacter) since lobbies can
    /// have up to 8 players but only 4 characters exist.
    public class CharacterSelectScreen : MonoBehaviour
    {
        private const float CardWidth = 230f;
        private const float CardHeight = 330f;
        private const float StageSpacing = 3f;
        private const float SpinDegreesPerSecond = 18f;

        private TMP_Text _statusText;
        private Button[] _characterButtons;
        private Transform[] _stagedRoots;
        private RenderTexture[] _previewTextures;

        private void Start()
        {
            Build();

            var store = AppRoot.Instance.Store;
            // The server confirms a pick via character_locked, not lobby_state — both
            // need to trigger a re-check, or a solo confirmation (nobody else's lobby_state
            // update happens to follow it) leaves this screen stuck forever.
            store.LobbyUpdated += OnStoreChanged;
            store.CharacterLocked += OnCharacterLocked;
        }

        private void OnDestroy()
        {
            if (AppRoot.Instance != null)
            {
                var store = AppRoot.Instance.Store;
                store.LobbyUpdated -= OnStoreChanged;
                store.CharacterLocked -= OnCharacterLocked;
            }

            if (_previewTextures == null) return;
            foreach (var rt in _previewTextures)
            {
                if (rt == null) continue;
                rt.Release();
                Destroy(rt);
            }
        }

        private void Update()
        {
            if (_stagedRoots == null) return;
            float delta = SpinDegreesPerSecond * Time.deltaTime;
            foreach (var root in _stagedRoots)
            {
                if (root != null) root.Rotate(Vector3.up, delta, Space.World);
            }
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas();

            // Warm backdrop instead of the default empty/black canvas — this screen has no
            // arena/environment behind it otherwise.
            UiFactory.CreatePanel(canvas.transform, "Backdrop", new Vector2(0.5f, 0.5f), new Vector2(1280, 720), QuizBattlePalette.PanelDeep);

            var title = UiFactory.CreateText(canvas.transform, "Title", new Vector2(0.5f, 0.9f), new Vector2(700, 60), 34);
            title.text = "Choose Your Character";
            title.fontStyle = FontStyles.Bold;
            title.color = QuizBattlePalette.GoldTrim;
            title.outlineWidth = 0.2f;
            title.outlineColor = Color.black;

            // Visible identity confirmation — students (and testers running multiple
            // clients on one machine) should always be able to see who they're actually
            // logged in as.
            var whoAmI = UiFactory.CreateText(canvas.transform, "WhoAmI", new Vector2(0.5f, 0.97f), new Vector2(500, 30), 16);
            whoAmI.color = QuizBattlePalette.CreamText;
            whoAmI.text = $"Playing as: {SessionManager.StudentName} (id {SessionManager.PlayerId})";

            var defs = CharacterCatalogLoader.LoadAll();
            _characterButtons = new Button[defs.Length];
            _stagedRoots = new Transform[defs.Length];
            _previewTextures = new RenderTexture[defs.Length];
            float[] xPositions = { 0.2f, 0.4f, 0.6f, 0.8f };

            BuildPreviewLight();

            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                var stagePos = new Vector3(i * StageSpacing, 0f, 0f);
                _stagedRoots[i] = BuildStagedCharacter(def, stagePos);
                _previewTextures[i] = new RenderTexture(320, 260, 16) { name = $"Preview_{def.characterId}" };
                BuildPreviewCamera(stagePos, _previewTextures[i]);

                var anchor = new Vector2(xPositions[i % xPositions.Length], 0.53f);
                var button = BuildCharacterCard(canvas.transform, def, anchor, _previewTextures[i]);
                _characterButtons[i] = button;
            }

            _statusText = UiFactory.CreateText(canvas.transform, "Status", new Vector2(0.5f, 0.1f), new Vector2(700, 60), 20);
            _statusText.color = QuizBattlePalette.CreamText;
        }

        /// 3-Point Studio Lighting Rig giving characters vivid specular highlights and rim backlights
        private static void BuildPreviewLight()
        {
            // Key Light (warm gold from front-right)
            var keyLight = new GameObject("PreviewKeyLight").AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1f, 0.95f, 0.85f);
            keyLight.intensity = 1.45f;
            keyLight.shadows = LightShadows.None;
            keyLight.transform.rotation = Quaternion.Euler(35f, -35f, 0f);

            // Rim / Back Light (cool cyan backlight from behind-left for crisp rim highlights)
            var rimLight = new GameObject("PreviewRimLight").AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.color = new Color(0.65f, 0.90f, 1f);
            rimLight.intensity = 2.2f;
            rimLight.shadows = LightShadows.None;
            rimLight.transform.rotation = Quaternion.Euler(-35f, 145f, 0f);

            // Fill Light (soft sky fill from front-left)
            var fillLight = new GameObject("PreviewFillLight").AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(0.85f, 0.88f, 1f);
            fillLight.intensity = 0.75f;
            fillLight.shadows = LightShadows.None;
            fillLight.transform.rotation = Quaternion.Euler(15f, 45f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.40f, 0.48f);
        }

        private static Transform BuildStagedCharacter(CharacterDefinitionSO def, Vector3 stagePos)
        {
            var root = new GameObject($"Preview_{def.characterId}");
            root.transform.position = stagePos;

            // 3D Golden Turntable Pedestal
            var pedestal = new GameObject("Pedestal");
            pedestal.transform.SetParent(root.transform, false);

            var goldMat = ToonMaterialFactory.Toon(QuizBattlePalette.GoldTrim, ToonStyle.GlossyToy);
            var slateMat = ToonMaterialFactory.Toon(new Color(0.12f, 0.14f, 0.20f), ToonStyle.GlossyToy);

            // Outer gold base
            var baseCyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseCyl.name = "GoldBase";
            baseCyl.transform.SetParent(pedestal.transform, false);
            baseCyl.transform.localPosition = new Vector3(0f, -0.04f, 0f);
            baseCyl.transform.localScale = new Vector3(1.35f, 0.04f, 1.35f);
            Destroy(baseCyl.GetComponent<Collider>());
            baseCyl.GetComponent<Renderer>().sharedMaterial = goldMat;

            // Slate disc
            var slateCyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            slateCyl.name = "SlateDisc";
            slateCyl.transform.SetParent(pedestal.transform, false);
            slateCyl.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            slateCyl.transform.localScale = new Vector3(1.15f, 0.03f, 1.15f);
            Destroy(slateCyl.GetComponent<Collider>());
            slateCyl.GetComponent<Renderer>().sharedMaterial = slateMat;

            // Glowing archetype power ring
            var glowRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glowRing.name = "GlowRing";
            glowRing.transform.SetParent(pedestal.transform, false);
            glowRing.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            glowRing.transform.localScale = new Vector3(1.05f, 0.015f, 1.05f);
            Destroy(glowRing.GetComponent<Collider>());
            glowRing.GetComponent<Renderer>().sharedMaterial =
                ToonMaterialFactory.Glow(def.placeholderColor, intensity: 1.6f, softEdge: 0.25f, pulseSpeed: 2f, pulseAmount: 0.35f);

            // Character model on the pedestal
            CharacterVisualBuilder.Build(CharacterVisual.From(def), root.transform);
            return root.transform;
        }

        private static void BuildPreviewCamera(Vector3 stagePos, RenderTexture target)
        {
            var camObj = new GameObject("PreviewCamera");
            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.22f); // Deep Royal Navy
            cam.fieldOfView = 25f;
            cam.nearClipPlane = 0.05f;
            cam.targetTexture = target;
            cam.transform.position = stagePos + new Vector3(0f, 0.90f, -2.4f);
            cam.transform.LookAt(stagePos + new Vector3(0f, 0.60f, 0f));
        }

        private Button BuildCharacterCard(Transform parent, CharacterDefinitionSO def, Vector2 anchor, RenderTexture preview)
        {
            var (frame, innerCard) = UiFactory.CreatePlacardPanel(parent, $"Char_{def.characterId}", anchor, new Vector2(CardWidth, CardHeight), QuizBattlePalette.PanelDeep);

            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = innerCard;
            var colors = button.colors;
            colors.highlightedColor = QuizBattlePalette.PanelHighlighted;
            colors.pressedColor = QuizBattlePalette.PanelPressed;
            button.colors = colors;

            // Character 3D RawImage Portrait
            var portraitRect = UiFactory.CreateRect(frame, "Portrait", new Vector2(0.5f, 0.5f), new Vector2(210, 165), new Vector2(0, 68));
            var portrait = portraitRect.gameObject.AddComponent<RawImage>();
            portrait.texture = preview;
            portrait.raycastTarget = false;

            // Archetype Banner Ribbon behind name
            var (bannerRect, _) = UiFactory.CreateBannerPanel(frame, "NameBanner", new Vector2(0.5f, 0.5f), new Vector2(210, 32), def.placeholderColor, new Vector2(0, -32));
            var name = UiFactory.CreateText(bannerRect, "Name", new Vector2(0.5f, 0.5f), new Vector2(200, 26), 18);
            name.text = def.displayName;
            name.fontStyle = FontStyles.Bold;
            name.color = Color.white;
            name.outlineWidth = 0.22f;
            name.outlineColor = Color.black;

            // Ability Description Box
            var ability = UiFactory.CreateText(frame, "Ability", new Vector2(0.5f, 0.5f), new Vector2(210, 95), 13, new Vector2(0, -100));
            ability.text = $"<b><color=#{ColorUtility.ToHtmlStringRGB(QuizBattlePalette.GoldTrim)}>{def.abilityName}</color></b>\n{def.abilityDescription}";
            ability.color = QuizBattlePalette.CreamText;

            var capturedId = def.characterId;
            button.onClick.AddListener(() => OnCharacterClicked(capturedId));
            return button;
        }

        public void OnCharacterClicked(string characterId)
        {
            SessionManager.SelectedCharacterId = characterId;
            AppRoot.Instance.Client.Send("select_character", new { characterId });
            _statusText.text = $"Selected {characterId}. Waiting for confirmation...";
        }

        private void OnStoreChanged(Networking.Protocol.LobbyStatePayload _) => RefreshTakenState();
        private void OnCharacterLocked(Networking.Protocol.CharacterLockedPayload _) => RefreshTakenState();

        private void RefreshTakenState()
        {
            var store = AppRoot.Instance.Store;
            var mine = store.LobbyPlayers.Find(p => p.playerId == SessionManager.PlayerId);
            if (mine?.characterId == SessionManager.SelectedCharacterId && !string.IsNullOrEmpty(mine.characterId))
            {
                _statusText.text = $"Locked in {mine.characterId}!";
                SceneManager.LoadScene("Lobby");
            }
        }
    }
}
