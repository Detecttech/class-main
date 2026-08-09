using QuizBattle.Bootstrap;
using QuizBattle.Characters;
using QuizBattle.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QuizBattle.UI.CharacterSelect
{
    /// Shows the 4 v1 characters as buttons; picking one sends select_character and the
    /// server is the arbiter of "already taken" (see server/src/matchEngine/LiveMatchRegistry.ts
    /// selectCharacter — first pick wins, this screen just reflects whatever the server says).
    public class CharacterSelectScreen : MonoBehaviour
    {
        private TMP_Text _statusText;
        private Button[] _characterButtons;

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
            if (AppRoot.Instance == null) return;
            var store = AppRoot.Instance.Store;
            store.LobbyUpdated -= OnStoreChanged;
            store.CharacterLocked -= OnCharacterLocked;
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas();
            var title = UiFactory.CreateText(canvas.transform, "Title", new Vector2(0.5f, 0.85f), new Vector2(700, 60), 32);
            title.text = "Choose Your Character";

            // Visible identity confirmation — students (and testers running multiple
            // clients on one machine) should always be able to see who they're actually
            // logged in as.
            var whoAmI = UiFactory.CreateText(canvas.transform, "WhoAmI", new Vector2(0.5f, 0.95f), new Vector2(500, 30), 16);
            whoAmI.text = $"Playing as: {SessionManager.StudentName} (id {SessionManager.PlayerId})";

            var defs = CharacterCatalogLoader.LoadAll();
            _characterButtons = new Button[defs.Length];
            float[] xPositions = { 0.2f, 0.4f, 0.6f, 0.8f };

            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                var button = UiFactory.CreateButton(canvas.transform, $"Char_{def.characterId}", new Vector2(xPositions[i % xPositions.Length], 0.55f), new Vector2(220, 260), "");
                button.image.color = def.placeholderColor;

                var name = button.GetComponentInChildren<TMP_Text>();
                name.text = $"{def.displayName}\n\n{def.abilityName}\n{def.abilityDescription}";
                name.color = Color.black;
                name.fontSize = 15;

                var capturedId = def.characterId;
                button.onClick.AddListener(() => OnCharacterClicked(capturedId));
                _characterButtons[i] = button;
            }

            _statusText = UiFactory.CreateText(canvas.transform, "Status", new Vector2(0.5f, 0.15f), new Vector2(700, 60), 20);
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
