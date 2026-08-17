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

        /// One shared directional light for every staged character — this screen has no
        /// arena/ArenaEnvironment, so QB_Toon's main-light-driven cel shading would
        /// otherwise render pitch black.
        private static void BuildPreviewLight()
        {
            var light = new GameObject("PreviewLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.92f, 0.78f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.30f, 0.36f);
        }

        private static Transform BuildStagedCharacter(CharacterDefinitionSO def, Vector3 stagePos)
        {
            var root = new GameObject($"Preview_{def.characterId}");
            root.transform.position = stagePos;
            CharacterVisualBuilder.Build(CharacterVisual.From(def), root.transform);
            return root.transform;
        }

        private static void BuildPreviewCamera(Vector3 stagePos, RenderTexture target)
        {
            var camObj = new GameObject("PreviewCamera");
            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = QuizBattlePalette.PanelFill;
            cam.fieldOfView = 24f;
            cam.nearClipPlane = 0.05f;
            cam.targetTexture = target;
            cam.transform.position = stagePos + new Vector3(0f, 0.85f, -2.6f);
            cam.transform.LookAt(stagePos + new Vector3(0f, 0.62f, 0f));
        }

        private Button BuildCharacterCard(Transform parent, CharacterDefinitionSO def, Vector2 anchor, RenderTexture preview)
        {
            var (frame, fill) = UiFactory.CreateBannerPanel(parent, $"Char_{def.characterId}", anchor, new Vector2(CardWidth, CardHeight), QuizBattlePalette.PanelFill);

            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            var colors = button.colors;
            colors.highlightedColor = QuizBattlePalette.PanelHighlighted;
            colors.pressedColor = QuizBattlePalette.PanelPressed;
            button.colors = colors;

            var portraitRect = UiFactory.CreateRect(frame, "Portrait", new Vector2(0.5f, 0.5f), new Vector2(210, 170), new Vector2(0, 65));
            var portrait = portraitRect.gameObject.AddComponent<RawImage>();
            portrait.texture = preview;
            portrait.raycastTarget = false;

            // Colored accent strip behind the name — the one place per-character identity
            // (def.placeholderColor) shows through the otherwise uniform card chrome.
            var accentRect = UiFactory.CreateRect(frame, "Accent", new Vector2(0.5f, 0.5f), new Vector2(210, 32), new Vector2(0, -39));
            var accent = accentRect.gameObject.AddComponent<Image>();
            accent.color = def.placeholderColor;
            accent.raycastTarget = false;

            var name = UiFactory.CreateText(frame, "Name", new Vector2(0.5f, 0.5f), new Vector2(210, 28), 20, new Vector2(0, -39));
            name.text = def.displayName;
            name.fontStyle = FontStyles.Bold;
            name.color = Color.black;

            var ability = UiFactory.CreateText(frame, "Ability", new Vector2(0.5f, 0.5f), new Vector2(210, 100), 14, new Vector2(0, -108));
            ability.text = $"{def.abilityName}\n{def.abilityDescription}";
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
