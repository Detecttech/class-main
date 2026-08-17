using System.Linq;
using QuizBattle.Arena;
using QuizBattle.Bootstrap;
using QuizBattle.Characters;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QuizBattle.UI
{
    /// Results recap — read straight from MatchStateStore.MatchResult / LastXpAward,
    /// which ArenaController's scene transition guarantees are already populated by the
    /// time this scene loads (the server sends xp_award before match_end — see
    /// server/src/matchEngine/LiveMatchRegistry.ts endMatch).
    public class PostMatchScreen : MonoBehaviour
    {
        private void Start()
        {
            var canvas = UiFactory.CreateCanvas();
            var title = UiFactory.CreateText(canvas.transform, "Title", new Vector2(0.5f, 0.78f), new Vector2(700, 60), 32);

            var result = AppRoot.Instance.Store.MatchResult;
            title.text = result != null ? $"Winner: {result.WinnerId}\n({result.Reason})" : "Match ended";

            var xp = AppRoot.Instance.Store.LastXpAward;
            if (xp != null)
            {
                var xpPanel = UiFactory.CreatePanel(canvas.transform, "XpPanel", new Vector2(0.5f, 0.55f), new Vector2(420, 130), new Color(QuizBattlePalette.PanelDeep.r, QuizBattlePalette.PanelDeep.g, QuizBattlePalette.PanelDeep.b, 0.9f));
                var xpText = UiFactory.CreateText(xpPanel.transform, "XpText", new Vector2(0.5f, 0.5f), new Vector2(400, 110), 20);
                xpText.text = $"+{xp.XpGained} XP\nTotal: {xp.NewTotalXp} XP";

                if (xp.NewUnlocks != null && xp.NewUnlocks.Count > 0)
                {
                    var defs = CharacterCatalogLoader.LoadAll();
                    var names = xp.NewUnlocks
                        .Select(id => defs.FirstOrDefault(d => d.characterId == id)?.displayName ?? id)
                        .ToList();

                    var unlockPanel = UiFactory.CreatePanel(canvas.transform, "UnlockPanel", new Vector2(0.5f, 0.38f), new Vector2(420, 90), new Color(QuizBattlePalette.GoldTrimDark.r, QuizBattlePalette.GoldTrimDark.g, QuizBattlePalette.GoldTrimDark.b, 0.9f));
                    var unlockText = UiFactory.CreateText(unlockPanel.transform, "UnlockText", new Vector2(0.5f, 0.5f), new Vector2(400, 70), 18);
                    unlockText.text = $"New character unlocked!\n{string.Join(", ", names)}";
                }
            }

            var again = UiFactory.CreateButton(canvas.transform, "AgainButton", new Vector2(0.5f, 0.18f), new Vector2(240, 50), "Join Another Match");
            again.onClick.AddListener(() => SceneManager.LoadScene("NameEntry"));
        }
    }
}
