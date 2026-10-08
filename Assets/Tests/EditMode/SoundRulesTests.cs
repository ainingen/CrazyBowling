using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CrazyBowling.Core;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>
    /// 音の決まり（段階6）の EditMode テスト。
    ///
    /// 確かめたいのは：
    /// ・判定の歓声の選び方（ガターは1本も倒れなかった投。真空のレーンでは鳴らさない）
    /// ・転がる音が速さで大きく高くなり、床から離れると小さくなること
    /// ・ピンの音が上限・間隔で鳴りすぎないこと
    /// ・数え上げの音の間隔の下限、交信の間隔と選び方、起伏の山の探し方
    /// </summary>
    public class SoundRulesTests
    {
        // ======== 判定の歓声 ========

        private static ThrowJudgement Judge(int fallen, bool strike = false, bool spare = false)
        {
            return new ThrowJudgement { throwNumber = 1, fallen = fallen, totalFallen = fallen, pinCount = 10, isStrike = strike, isSpare = spare };
        }

        [Test]
        public void ストライクならストライクの歓声()
        {
            Assert.That(JudgementSound.Choose(Judge(10, strike: true), false), Is.EqualTo(JudgementSoundKind.Strike));
        }

        [Test]
        public void スペアならスペアの歓声()
        {
            Assert.That(JudgementSound.Choose(Judge(3, spare: true), false), Is.EqualTo(JudgementSoundKind.Spare));
        }

        [Test]
        public void 一本も倒れなかった投はガター()
        {
            Assert.That(JudgementSound.Choose(Judge(0), false), Is.EqualTo(JudgementSoundKind.Gutter));
        }

        [Test]
        public void 何本か倒れただけなら歓声は無し()
        {
            Assert.That(JudgementSound.Choose(Judge(4), false), Is.EqualTo(JudgementSoundKind.None));
        }

        [Test]
        public void 真空のレーンでは歓声を鳴らさない()
        {
            Assert.That(JudgementSound.Choose(Judge(10, strike: true), true), Is.EqualTo(JudgementSoundKind.None));
            Assert.That(JudgementSound.Choose(Judge(0), true), Is.EqualTo(JudgementSoundKind.None));
        }

        // ======== 転がる音 ========

        private static RollSoundSettings Roll()
        {
            return new RollSoundSettings { minSpeed = 0.4f, maxSpeed = 9f, volumeAtMin = 0.25f, pitchAtMin = 0.75f, pitchAtMax = 1.15f, volumeInAir = 0f };
        }

        [Test]
        public void 遅すぎると転がる音は鳴らさない()
        {
            Assert.That(RollSound.TargetVolume(0.2f, true, Roll()), Is.EqualTo(0f));
        }

        [Test]
        public void 速いほど転がる音は大きい()
        {
            float slow = RollSound.TargetVolume(2f, true, Roll());
            float fast = RollSound.TargetVolume(8f, true, Roll());
            Assert.That(fast, Is.GreaterThan(slow));
            Assert.That(RollSound.TargetVolume(9f, true, Roll()), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void 速いほど転がる音は高い()
        {
            Assert.That(RollSound.Pitch(0.4f, Roll()), Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(RollSound.Pitch(9f, Roll()), Is.EqualTo(1.15f).Within(1e-5f));
            Assert.That(RollSound.Pitch(5f, Roll()), Is.GreaterThan(RollSound.Pitch(2f, Roll())));
        }

        [Test]
        public void 床から離れていると空中の割合がかかる()
        {
            RollSoundSettings s = Roll();
            Assert.That(RollSound.TargetVolume(8f, false, s), Is.EqualTo(0f));
            s.volumeInAir = 0.5f;
            Assert.That(RollSound.TargetVolume(8f, false, s), Is.EqualTo(RollSound.TargetVolume(8f, true, s) * 0.5f).Within(1e-5f));
        }

        [Test]
        public void 音量は目標へなめらかに近づく()
        {
            float v = RollSound.Approach(0f, 1f, 12f, 0.016f);
            Assert.That(v, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(RollSound.Approach(0f, 1f, 12f, 10f), Is.EqualTo(1f).Within(1e-4f));
        }

        // ======== ピンの音 ========

        [Test]
        public void 遅い当たりは鳴らさない()
        {
            var gate = new PinHitGate(0.8f, 3f, 0.15f, 4);
            Assert.That(gate.Decide(1, false, 0, 0.5f, 0f, 0), Is.EqualTo(PinHitKind.None));
        }

        [Test]
        public void 速さで強と弱に分ける()
        {
            var gate = new PinHitGate(0.8f, 3f, 0.15f, 4);
            Assert.That(gate.Decide(1, false, 0, 5f, 0f, 0), Is.EqualTo(PinHitKind.Strong));
            Assert.That(gate.Decide(2, false, 0, 1.5f, 0f, 0), Is.EqualTo(PinHitKind.Weak));
        }

        [Test]
        public void 同時に鳴っている数が上限なら鳴らさない()
        {
            var gate = new PinHitGate(0.8f, 3f, 0.15f, 4);
            Assert.That(gate.Decide(1, false, 0, 5f, 0f, 4), Is.EqualTo(PinHitKind.None));
            Assert.That(gate.Decide(1, false, 0, 5f, 0f, 3), Is.EqualTo(PinHitKind.Strong));
        }

        [Test]
        public void 同じピンは間を空けるまで鳴らさない()
        {
            var gate = new PinHitGate(0.8f, 3f, 0.15f, 4);
            Assert.That(gate.Decide(1, false, 0, 5f, 0f, 0), Is.EqualTo(PinHitKind.Strong));
            Assert.That(gate.Decide(1, false, 0, 5f, 0.1f, 0), Is.EqualTo(PinHitKind.None));
            Assert.That(gate.Decide(1, false, 0, 5f, 0.2f, 0), Is.EqualTo(PinHitKind.Strong));
        }

        [Test]
        public void ピン同士は相手のピンも間を空ける()
        {
            var gate = new PinHitGate(0.8f, 3f, 0.15f, 4);
            Assert.That(gate.Decide(1, true, 2, 2f, 0f, 0), Is.EqualTo(PinHitKind.Weak));
            Assert.That(gate.Decide(2, false, 0, 5f, 0.05f, 0), Is.EqualTo(PinHitKind.None));
        }

        [Test]
        public void 記録を消すとすぐ鳴らせる()
        {
            var gate = new PinHitGate(0.8f, 3f, 0.15f, 4);
            gate.Decide(1, false, 0, 5f, 0f, 0);
            gate.Reset();
            Assert.That(gate.Decide(1, false, 0, 5f, 0.01f, 0), Is.EqualTo(PinHitKind.Strong));
        }

        [Test]
        public void 負の番号のピンでも間を空ける()
        {
            // Unity の GetInstanceID は負の数になることがある
            var gate = new PinHitGate(0.8f, 3f, 0.15f, 4);
            Assert.That(gate.Decide(-5, true, -9, 2f, 0f, 0), Is.EqualTo(PinHitKind.Weak));
            Assert.That(gate.Decide(-9, false, 0, 2f, 0.05f, 0), Is.EqualTo(PinHitKind.None));
        }

        // ======== 数え上げの間隔 ========

        [Test]
        public void 間隔の下限より短いと通さない()
        {
            var gate = new IntervalGate(0.09f);
            Assert.That(gate.TryPass(1f), Is.True);
            Assert.That(gate.TryPass(1.05f), Is.False);
            Assert.That(gate.TryPass(1.1f), Is.True);
        }

        [Test]
        public void 数字が毎フレーム変わっても鳴る回数は下限で決まる()
        {
            var gate = new IntervalGate(0.09f);
            int passed = 0;
            for (int frame = 0; frame < 60; frame++)
            {
                if (gate.TryPass(frame / 60f))
                {
                    passed++;
                }
            }
            // 1秒のあいだに 0.09 秒ごと → 12回以下
            Assert.That(passed, Is.LessThanOrEqualTo(12).And.GreaterThanOrEqualTo(10));
        }

        // ======== 交信の間隔と選び方 ========

        [Test]
        public void 間隔は範囲の中に収まる()
        {
            var range = new Vector2(12f, 25f);
            Assert.That(SoundSchedule.NextDelay(range, 0f), Is.EqualTo(12f));
            Assert.That(SoundSchedule.NextDelay(range, 1f), Is.EqualTo(25f));
            Assert.That(SoundSchedule.NextDelay(new Vector2(25f, 12f), 0.5f), Is.EqualTo(18.5f).Within(1e-4f));
        }

        [Test]
        public void 候補から前回と同じものは選ばない()
        {
            for (int i = 0; i <= 100; i++)
            {
                int pick = SoundSchedule.PickIndex(4, 2, i / 100f);
                Assert.That(pick, Is.InRange(0, 3));
                Assert.That(pick, Is.Not.EqualTo(2));
            }
        }

        [Test]
        public void 候補はどれも選ばれうる()
        {
            var seen = new HashSet<int>();
            for (int i = 0; i <= 100; i++)
            {
                seen.Add(SoundSchedule.PickIndex(6, -1, i / 100f));
            }
            Assert.That(seen.Count, Is.EqualTo(6));
        }

        [Test]
        public void 候補が1つならそれを選び無ければマイナス1()
        {
            Assert.That(SoundSchedule.PickIndex(1, 0, 0.7f), Is.EqualTo(0));
            Assert.That(SoundSchedule.PickIndex(0, -1, 0.7f), Is.EqualTo(-1));
        }

        // ======== 7本目のジェット ========

        [Test]
        public void 筒に入ったら鳴らす()
        {
            var gate = new JetSoundGate(2f, 2);
            Assert.That(gate.TryEnter(10f), Is.True);
            Assert.That(gate.Count, Is.EqualTo(1));
        }

        [Test]
        public void 入った音の直後の推力は重ねない()
        {
            var gate = new JetSoundGate(2f, 2);
            gate.TryEnter(10f);
            Assert.That(gate.TryThrust(10.5f), Is.False);
            Assert.That(gate.Count, Is.EqualTo(1));
        }

        [Test]
        public void 止まりかけて間が空いた推力はもう一度鳴らす()
        {
            var gate = new JetSoundGate(2f, 2);
            gate.TryEnter(10f);
            Assert.That(gate.TryThrust(13f), Is.True);
            Assert.That(gate.Count, Is.EqualTo(2));
        }

        [Test]
        public void 一投の上限を超えては鳴らさない()
        {
            var gate = new JetSoundGate(0f, 2);
            Assert.That(gate.TryEnter(1f), Is.True);
            Assert.That(gate.TryThrust(5f), Is.True);
            Assert.That(gate.TryThrust(9f), Is.False);
            Assert.That(gate.TryEnter(12f), Is.False);
        }

        [Test]
        public void 新しい投で数え直す()
        {
            var gate = new JetSoundGate(2f, 1);
            gate.TryEnter(1f);
            gate.ResetThrow();
            Assert.That(gate.TryEnter(1.5f), Is.True);
        }

        [Test]
        public void 筒に入らずに推力だけ効いたら鳴らす()
        {
            var gate = new JetSoundGate(2f, 2);
            Assert.That(gate.TryThrust(4f), Is.True);
        }

        // ======== タイトルのナレーション ========

        [Test]
        public void タイトルが出ていて音を消していなければ流す()
        {
            Assert.That(TitleNarrationRule.CanStart(false, true, false), Is.True);
        }

        [Test]
        public void 音を消していたらナレーションを流さない()
        {
            Assert.That(TitleNarrationRule.CanStart(true, true, false), Is.False);
        }

        [Test]
        public void タイトルが閉じ始めていたらナレーションを流さない()
        {
            Assert.That(TitleNarrationRule.CanStart(false, true, true), Is.False);
            Assert.That(TitleNarrationRule.CanStart(false, false, false), Is.False);
        }

        [Test]
        public void タイトルを閉じたら小さくして止める()
        {
            Assert.That(TitleNarrationRule.ShouldFadeOut(true, true), Is.True);
            Assert.That(TitleNarrationRule.ShouldFadeOut(false, false), Is.True);
            Assert.That(TitleNarrationRule.ShouldFadeOut(true, false), Is.False);
        }

        [Test]
        public void 途中で音を消したらすぐ止める()
        {
            Assert.That(TitleNarrationRule.ShouldStopNow(true), Is.True);
            Assert.That(TitleNarrationRule.ShouldStopNow(false), Is.False);
        }

        // ======== すぐ始める版（CrazyGames 版）のナレーションと DJ ========

        [Test]
        public void すぐ始める版はタイトルが出ていなくてもナレーションを流す()
        {
            Assert.That(TitleNarrationRule.CanStartQuick(false, false, false), Is.True);
        }

        [Test]
        public void すぐ始める版は音かDJを消していたらナレーションを流さない()
        {
            Assert.That(TitleNarrationRule.CanStartQuick(true, false, false), Is.False);
            Assert.That(TitleNarrationRule.CanStartQuick(false, true, false), Is.False);
        }

        [Test]
        public void すぐ始める版はタイトルに戻っていたらナレーションを流さない()
        {
            Assert.That(TitleNarrationRule.CanStartQuick(false, false, true), Is.False);
        }

        [Test]
        public void すぐ始める版はタイトルに戻るかDJを消したら小さくして止める()
        {
            Assert.That(TitleNarrationRule.ShouldFadeOutQuick(false, false), Is.False);
            Assert.That(TitleNarrationRule.ShouldFadeOutQuick(false, true), Is.True);
            Assert.That(TitleNarrationRule.ShouldFadeOutQuick(true, false), Is.True);
        }

        [Test]
        public void いつもの版のDJはタイトルとナレーションが閉じてから始める()
        {
            Assert.That(DjStartRule.CanStart(true, false, false, false, false, false), Is.True);
            Assert.That(DjStartRule.CanStart(true, true, false, false, false, false), Is.False);
            Assert.That(DjStartRule.CanStart(true, false, true, false, false, false), Is.False);
            Assert.That(DjStartRule.CanStart(true, false, false, false, true, false), Is.False);
        }

        [Test]
        public void すぐ始める版のDJはナレーションが終わったらタイトルの上でも始める()
        {
            Assert.That(DjStartRule.CanStart(true, true, false, false, false, true), Is.True);
            Assert.That(DjStartRule.CanStart(true, false, false, false, true, true), Is.False);
            Assert.That(DjStartRule.CanStart(true, false, true, false, false, true), Is.False);
        }

        [Test]
        public void DJは音を消している間と一時停止の間は始めない()
        {
            foreach (bool quick in new[] { false, true })
            {
                Assert.That(DjStartRule.CanStart(false, false, false, false, false, quick), Is.False);
                Assert.That(DjStartRule.CanStart(true, false, false, true, false, quick), Is.False);
            }
        }

        [Test]
        public void RANKが出るまでは結果画面の曲を流さない()
        {
            Assert.That(ResultBgmRule.ShouldPlay(-1f, 100f, 1f), Is.False);
        }

        [Test]
        public void RANKが出てから決めた秒数たつまでは流さない()
        {
            Assert.That(ResultBgmRule.ShouldPlay(10f, 10.5f, 1f), Is.False);
            Assert.That(ResultBgmRule.ShouldPlay(10f, 11f, 1f), Is.True);
            Assert.That(ResultBgmRule.ShouldPlay(10f, 30f, 1f), Is.True);
        }

        [Test]
        public void 待つ秒数が0ならRANKと同時に流す()
        {
            Assert.That(ResultBgmRule.ShouldPlay(10f, 10f, 0f), Is.True);
        }

        [Test]
        public void dBを音量の倍率にする()
        {
            Assert.That(SoundLevel.DbToLinear(0f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(SoundLevel.DbToLinear(-10f), Is.EqualTo(0.3162f).Within(1e-4f));
            Assert.That(SoundLevel.DbToLinear(-20f), Is.EqualTo(0.1f).Within(1e-5f));
        }

        // ======== 起伏の山 ========

        [Test]
        public void 一つの山の頂上を見つける()
        {
            var heights = new List<float>();
            for (int i = 0; i <= 100; i++)
            {
                heights.Add(0.05f * Mathf.Exp(-Mathf.Pow((i - 40) / 12f, 2f)));
            }
            List<int> crests = LaneCrests.Find(heights, 40, 0.01f);
            Assert.That(crests, Is.EqualTo(new List<int> { 40 }));
        }

        [Test]
        public void 小さなうねりは山にしない()
        {
            var heights = new List<float>();
            for (int i = 0; i <= 100; i++)
            {
                heights.Add(0.002f * Mathf.Sin(i * 0.3f));
            }
            Assert.That(LaneCrests.Find(heights, 40, 0.01f), Is.Empty);
        }

        [Test]
        public void 奥へ越えたときだけ越えたとみなす()
        {
            Assert.That(LaneCrests.CrossedForward(7.9f, 8.1f, 8f), Is.True);
            Assert.That(LaneCrests.CrossedForward(8.1f, 7.9f, 8f), Is.False);
            Assert.That(LaneCrests.CrossedForward(8.1f, 8.3f, 8f), Is.False);
            Assert.That(LaneCrests.CrossedForward(float.NegativeInfinity, 8.3f, 8f), Is.True);
        }
    }
}
