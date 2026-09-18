using NUnit.Framework;
using UnityEngine;
using CrazyBowling.Pins;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>
    /// PinBlastCalculator（連鎖爆発の計算）の EditMode テスト。
    /// </summary>
    public class PinBlastCalculatorTests
    {
        /// <summary>Inspector の初期値と同じ設定。</summary>
        private static PinBlastSettings CreateSettings()
        {
            return new PinBlastSettings
            {
                triggerSpeed = 4.5f,
                force = 7f,
                upwardRatio = 0.5f,
                radius = 0.45f,
                chainFalloff = 0.6f,
                maxChainCount = 4,
                spinStrength = 25f,
                originOffsetMin = 0f,
                originOffsetMax = 0.12f,
                originFalloffWidth = 0.03f,
            };
        }

        /// <summary>レーン中央を奥へ転がるボールと、その先のヘッドピン。</summary>
        private static Vector3 HeadPin()
        {
            return new Vector3(0f, 0.05f, 16.2f);
        }

        [Test]
        public void 閾値を超えた勢いなら発動する()
        {
            Assert.IsTrue(PinBlastCalculator.ShouldExplode(5f, 0, CreateSettings()));
        }

        [Test]
        public void 閾値ちょうどでも発動する()
        {
            Assert.IsTrue(PinBlastCalculator.ShouldExplode(4.5f, 0, CreateSettings()));
        }

        [Test]
        public void 閾値に届かなければ発動しない()
        {
            Assert.IsFalse(PinBlastCalculator.ShouldExplode(4.4f, 0, CreateSettings()));
        }

        [Test]
        public void 連鎖の上限に達したら発動しない()
        {
            PinBlastSettings settings = CreateSettings();
            Assert.IsTrue(PinBlastCalculator.ShouldExplode(9f, settings.maxChainCount - 1, settings));
            Assert.IsFalse(PinBlastCalculator.ShouldExplode(9f, settings.maxChainCount, settings));
        }

        [Test]
        public void 威力は連鎖するたびに減衰率ぶん弱くなる()
        {
            PinBlastSettings settings = CreateSettings();

            Assert.AreEqual(settings.force, PinBlastCalculator.StrengthAt(0, settings), 0.0001f);
            Assert.AreEqual(settings.force * 0.6f, PinBlastCalculator.StrengthAt(1, settings), 0.0001f);
            Assert.AreEqual(settings.force * 0.36f, PinBlastCalculator.StrengthAt(2, settings), 0.0001f);
        }

        [Test]
        public void 回転も同じ率で減衰する()
        {
            PinBlastSettings settings = CreateSettings();

            Assert.AreEqual(settings.spinStrength, PinBlastCalculator.SpinAt(0, settings), 0.0001f);
            Assert.AreEqual(settings.spinStrength * 0.6f, PinBlastCalculator.SpinAt(1, settings), 0.0001f);
        }

        [Test]
        public void 範囲の外のピンは飛ばさない()
        {
            PinBlastSettings settings = CreateSettings();
            Vector3 origin = Vector3.zero;
            Vector3 target = new Vector3(settings.radius + 0.01f, 0f, 0f);

            Vector3 velocityChange;
            Assert.IsFalse(PinBlastCalculator.TryCalculateBlast(origin, target, 0, settings, out velocityChange));
            Assert.AreEqual(Vector3.zero, velocityChange);
        }

        [Test]
        public void 範囲の中のピンは飛ばす()
        {
            PinBlastSettings settings = CreateSettings();
            Vector3 velocityChange;

            Assert.IsTrue(PinBlastCalculator.TryCalculateBlast(
                Vector3.zero, new Vector3(0.3f, 0f, 0f), 0, settings, out velocityChange));
            Assert.Greater(velocityChange.magnitude, 0f);
        }

        [Test]
        public void 起点から見て外向きに飛ぶ()
        {
            PinBlastSettings settings = CreateSettings();
            Vector3 origin = new Vector3(0f, 0.05f, 16.2f);
            Vector3 target = origin + new Vector3(0.3f, 0f, 0f);

            Vector3 velocityChange;
            PinBlastCalculator.TryCalculateBlast(origin, target, 0, settings, out velocityChange);

            Assert.Greater(velocityChange.x, 0f, "起点の反対側へ飛ぶ");
            Assert.Greater(velocityChange.y, 0f, "上向き成分がある");
            Assert.AreEqual(0f, velocityChange.z, 0.0001f, "横にずれていない");
        }

        [Test]
        public void 上向きの割合を0にすると真横に飛ぶ()
        {
            PinBlastSettings settings = CreateSettings();
            settings.upwardRatio = 0f;

            Vector3 velocityChange;
            PinBlastCalculator.TryCalculateBlast(
                Vector3.zero, new Vector3(0.3f, 0f, 0f), 0, settings, out velocityChange);

            Assert.AreEqual(0f, velocityChange.y, 0.0001f);
        }

        [Test]
        public void 上向きの割合を上げると上に飛ぶ割合が増える()
        {
            PinBlastSettings low = CreateSettings();
            low.upwardRatio = 0.2f;
            PinBlastSettings high = CreateSettings();
            high.upwardRatio = 1.0f;

            Vector3 a, b;
            PinBlastCalculator.TryCalculateBlast(Vector3.zero, new Vector3(0.3f, 0f, 0f), 0, low, out a);
            PinBlastCalculator.TryCalculateBlast(Vector3.zero, new Vector3(0.3f, 0f, 0f), 0, high, out b);

            Assert.Greater(b.y, a.y);
        }

        [Test]
        public void 高さの差は距離に数えない()
        {
            // 真上に浮いているピンも、水平距離が同じなら同じ強さで飛ぶ
            PinBlastSettings settings = CreateSettings();

            Vector3 onFloor, inAir;
            PinBlastCalculator.TryCalculateBlast(Vector3.zero, new Vector3(0.3f, 0f, 0f), 0, settings, out onFloor);
            PinBlastCalculator.TryCalculateBlast(Vector3.zero, new Vector3(0.3f, 0.5f, 0f), 0, settings, out inAir);

            Assert.AreEqual(onFloor.magnitude, inAir.magnitude, 0.0001f);
        }

        [Test]
        public void 起点に近いほど強く飛ぶ()
        {
            PinBlastSettings settings = CreateSettings();

            Vector3 near, far;
            PinBlastCalculator.TryCalculateBlast(Vector3.zero, new Vector3(0.05f, 0f, 0f), 0, settings, out near);
            PinBlastCalculator.TryCalculateBlast(Vector3.zero, new Vector3(0.44f, 0f, 0f), 0, settings, out far);

            Assert.Greater(near.magnitude, far.magnitude);
        }

        [Test]
        public void 範囲の端ではちょうど半分の強さになる()
        {
            PinBlastSettings settings = CreateSettings();

            Vector3 atEdge;
            PinBlastCalculator.TryCalculateBlast(
                Vector3.zero, new Vector3(settings.radius, 0f, 0f), 0, settings, out atEdge);

            Assert.AreEqual(settings.force * 0.5f, atEdge.magnitude, 0.0001f);
        }

        [Test]
        public void 真上に重なっているピンは飛ばさない()
        {
            // 向きが決められないので何もしない
            PinBlastSettings settings = CreateSettings();

            Vector3 velocityChange;
            Assert.IsFalse(PinBlastCalculator.TryCalculateBlast(
                Vector3.zero, new Vector3(0f, 0.3f, 0f), 0, settings, out velocityChange));
        }

        // ---- 横ずれ（厚く当たったかの判定） ----

        [Test]
        public void ど真ん中に当たると横ずれは0()
        {
            float offset = PinBlastCalculator.CalculateLateralOffset(
                new Vector3(0f, 0.16f, 16.0f), Vector3.forward * 7f, HeadPin());

            Assert.AreEqual(0f, offset, 0.0001f);
        }

        [Test]
        public void 横にずれた分だけ横ずれになる()
        {
            float offset = PinBlastCalculator.CalculateLateralOffset(
                new Vector3(0.09f, 0.16f, 16.0f), Vector3.forward * 7f, HeadPin());

            Assert.AreEqual(0.09f, offset, 0.0001f);
        }

        [Test]
        public void 手前からの距離は横ずれに含めない()
        {
            // 同じ横位置なら、ピンまでの残り距離が変わっても横ずれは同じ
            float near = PinBlastCalculator.CalculateLateralOffset(
                new Vector3(0.09f, 0.16f, 16.0f), Vector3.forward * 7f, HeadPin());
            float far = PinBlastCalculator.CalculateLateralOffset(
                new Vector3(0.09f, 0.16f, 14.0f), Vector3.forward * 7f, HeadPin());

            Assert.AreEqual(near, far, 0.0001f);
        }

        [Test]
        public void 斜めから入ってきても進行方向に対する横ずれで測る()
        {
            // 45度の向きに進むボールが、その進路の真上にヘッドピンがある場合は横ずれ0
            Vector3 direction = new Vector3(1f, 0f, 1f).normalized;
            Vector3 ball = HeadPin() - direction * 2f;
            ball.y = 0.16f;

            float offset = PinBlastCalculator.CalculateLateralOffset(ball, direction * 7f, HeadPin());

            Assert.AreEqual(0f, offset, 0.0001f);
        }

        [Test]
        public void 斜めから入って進路が外れていれば横ずれが出る()
        {
            Vector3 direction = new Vector3(1f, 0f, 1f).normalized;
            Vector3 right = new Vector3(direction.z, 0f, -direction.x);
            Vector3 ball = HeadPin() - direction * 2f + right * 0.09f;
            ball.y = 0.16f;

            float offset = PinBlastCalculator.CalculateLateralOffset(ball, direction * 7f, HeadPin());

            Assert.AreEqual(0.09f, offset, 0.0001f);
        }

        [Test]
        public void 止まっているボールでも横ずれを返す()
        {
            float offset = PinBlastCalculator.CalculateLateralOffset(
                new Vector3(0.09f, 0.16f, 16.2f), Vector3.zero, HeadPin());

            Assert.AreEqual(0.09f, offset, 0.0001f);
        }

        // ---- 横ずれによる威力の倍率 ----

        [Test]
        public void 範囲の内側なら満威力()
        {
            PinBlastSettings s = CreateSettings();

            Assert.AreEqual(1f, PinBlastCalculator.OriginStrengthScale(0f, s), 0.0001f);
            Assert.AreEqual(1f, PinBlastCalculator.OriginStrengthScale(0.05f, s), 0.0001f);
            Assert.AreEqual(1f, PinBlastCalculator.OriginStrengthScale(0.09f, s), 0.0001f);
        }

        [Test]
        public void 上限を超えたら爆発しない()
        {
            PinBlastSettings s = CreateSettings();

            Assert.AreEqual(0f, PinBlastCalculator.OriginStrengthScale(0.121f, s), 0.0001f);
            Assert.AreEqual(0f, PinBlastCalculator.OriginStrengthScale(0.15f, s), 0.0001f);
        }

        [Test]
        public void 下限に届かなければ爆発しない()
        {
            PinBlastSettings s = CreateSettings();
            s.originOffsetMin = 0.04f;

            Assert.AreEqual(0f, PinBlastCalculator.OriginStrengthScale(0.02f, s), 0.0001f);
            Assert.Greater(PinBlastCalculator.OriginStrengthScale(0.05f, s), 0f);
        }

        [Test]
        public void 上限に近づくほど威力が落ちる()
        {
            PinBlastSettings s = CreateSettings();

            float at9 = PinBlastCalculator.OriginStrengthScale(0.09f, s);
            float at10 = PinBlastCalculator.OriginStrengthScale(0.10f, s);
            float at11 = PinBlastCalculator.OriginStrengthScale(0.11f, s);

            Assert.AreEqual(1f, at9, 0.0001f);
            Assert.Less(at10, at9);
            Assert.Less(at11, at10);
            Assert.Greater(at11, 0f);
        }

        [Test]
        public void 補間の幅を0にすると境界まで満威力()
        {
            PinBlastSettings s = CreateSettings();
            s.originFalloffWidth = 0f;

            Assert.AreEqual(1f, PinBlastCalculator.OriginStrengthScale(0.119f, s), 0.0001f);
            Assert.AreEqual(0f, PinBlastCalculator.OriginStrengthScale(0.121f, s), 0.0001f);
        }

        [Test]
        public void 威力の倍率は飛ばす強さに掛かる()
        {
            PinBlastSettings s = CreateSettings();

            Vector3 full, half;
            PinBlastCalculator.TryCalculateBlast(Vector3.zero, new Vector3(0.3f, 0f, 0f), 0, 1f, s, out full);
            PinBlastCalculator.TryCalculateBlast(Vector3.zero, new Vector3(0.3f, 0f, 0f), 0, 0.5f, s, out half);

            Assert.AreEqual(full.magnitude * 0.5f, half.magnitude, 0.0001f);
        }

        [Test]
        public void 連鎖が進むと飛ばす強さも落ちる()
        {
            PinBlastSettings settings = CreateSettings();

            Vector3 first, second;
            PinBlastCalculator.TryCalculateBlast(Vector3.zero, new Vector3(0.3f, 0f, 0f), 0, settings, out first);
            PinBlastCalculator.TryCalculateBlast(Vector3.zero, new Vector3(0.3f, 0f, 0f), 1, settings, out second);

            Assert.AreEqual(first.magnitude * 0.6f, second.magnitude, 0.0001f);
        }
    }
}
