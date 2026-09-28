using NUnit.Framework;
using CrazyBowling.UI;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>タイトルへ戻るボタンの決まり（段階6）。</summary>
    public class TitleReturnRulesTests
    {
        [Test]
        public void ゲームの途中だけ隅のボタンを出す()
        {
            Assert.That(TitleReturnRules.ShowInGameButton(false, false, false), Is.True);
            // タイトルが出ている・結果画面（専用のボタンがある）・ほかの画面が開いている
            Assert.That(TitleReturnRules.ShowInGameButton(true, false, false), Is.False);
            Assert.That(TitleReturnRules.ShowInGameButton(false, true, false), Is.False);
            Assert.That(TitleReturnRules.ShowInGameButton(false, false, true), Is.False);
        }

        [Test]
        public void 球が転がっている間と下見の間は押せない()
        {
            Assert.That(TitleReturnRules.CanPressInGame(false, false), Is.True);
            Assert.That(TitleReturnRules.CanPressInGame(true, false), Is.False);
            Assert.That(TitleReturnRules.CanPressInGame(false, true), Is.False);
        }
    }
}
