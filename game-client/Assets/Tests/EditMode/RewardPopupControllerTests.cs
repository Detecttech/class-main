using System.Collections.Generic;
using NUnit.Framework;
using QuizBattle.UI.RewardPopup;
using UnityEngine;

namespace QuizBattle.Tests.EditMode
{
    /// Regression test for a crash found via a Player.log trace: RewardPopupController
    /// used to activate its panel *after* spawning the choice/ack buttons, but those
    /// buttons are built with UiFactory.CreateButton, which sets TMP_Text.outlineWidth —
    /// and a TMP_Text added under a still-inactive GameObject never runs the Awake/OnEnable
    /// material setup that property needs, so it threw a NullReferenceException and aborted
    /// before the popup ever became visible. Every streak reward (attack/freeze/bonus_move)
    /// went through this exact path, matching "no attacks no bonuses nothing" reported live.
    public class RewardPopupControllerTests
    {
        private GameObject _canvasObj;

        [SetUp]
        public void SetUp()
        {
            _canvasObj = new GameObject("Canvas");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvasObj);
        }

        [Test]
        public void ShowAttackChoiceDoesNotThrowAndActivatesThePanel()
        {
            var popup = RewardPopupController.Create(_canvasObj.transform);
            var opponents = new List<(int playerId, string name)> { (2, "Bob"), (3, "Cara") };

            Assert.DoesNotThrow(() => popup.ShowAttackChoice(opponents, _ => { }));

            var panel = _canvasObj.transform.Find("RewardPopup/Panel");
            Assert.IsNotNull(panel, "expected a Panel child to exist");
            Assert.IsTrue(panel.gameObject.activeSelf, "panel should be active after ShowAttackChoice");
        }

        [Test]
        public void ShowBonusMoveDoesNotThrowAndActivatesThePanel()
        {
            var popup = RewardPopupController.Create(_canvasObj.transform);

            Assert.DoesNotThrow(() => popup.ShowBonusMove(() => { }));

            var panel = _canvasObj.transform.Find("RewardPopup/Panel");
            Assert.IsTrue(panel.gameObject.activeSelf, "panel should be active after ShowBonusMove");
        }

        [Test]
        public void HideDeactivatesPanelAndBackdrop()
        {
            var popup = RewardPopupController.Create(_canvasObj.transform);
            popup.ShowBonusMove(() => { });
            popup.Hide();

            var panel = _canvasObj.transform.Find("RewardPopup/Panel");
            var backdrop = _canvasObj.transform.Find("RewardPopup/Backdrop");
            Assert.IsFalse(panel.gameObject.activeSelf);
            Assert.IsFalse(backdrop.gameObject.activeSelf);
        }
    }
}
