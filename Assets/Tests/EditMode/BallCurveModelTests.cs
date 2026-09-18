using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CrazyBowling.Ball;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>
    /// BallCurveModel（カーブの計算）の EditMode テスト。
    /// </summary>
    public class BallCurveModelTests
    {
        private const float Radius = 0.11f;

        private static BallCurveSettings CreateSettings()
        {
            return new BallCurveSettings
            {
                radius = Radius,
                maxSideSpin = 30f,
                curveForce = 0.25f,
                spinDecay = 1.5f,
                slipThreshold = 0.15f,
            };
        }

        /// <summary>+Z へ進みながら、滑らずに転がっている状態の角速度。</summary>
        private static Vector3 RollingSpin(float speed)
        {
            return new Vector3(speed / Radius, 0f, 0f);
        }

        // ---- 滑りの計算 ----

        [Test]
        public void 滑らずに転がっていれば滑りは0()
        {
            float slip = BallCurveModel.CalculateSlipSpeed(
                new Vector3(0f, 0f, 7f), RollingSpin(7f), Radius);

            Assert.AreEqual(0f, slip, 0.0001f);
        }

        [Test]
        public void 無回転で進んでいれば速度そのものが滑りになる()
        {
            float slip = BallCurveModel.CalculateSlipSpeed(
                new Vector3(0f, 0f, 7f), Vector3.zero, Radius);

            Assert.AreEqual(7f, slip, 0.0001f);
        }

        [Test]
        public void 前回転が足りなければその差が滑りになる()
        {
            // 7 m/s で進み、回転は7割ぶんだけ与えた状態
            float slip = BallCurveModel.CalculateSlipSpeed(
                new Vector3(0f, 0f, 7f), RollingSpin(7f) * 0.7f, Radius);

            Assert.AreEqual(7f * 0.3f, slip, 0.0001f);
        }

        [Test]
        public void 縦軸まわりの回転は滑りに影響しない()
        {
            // Unity の物理で横回転をかけても曲がらない理由がこれ
            Vector3 spin = RollingSpin(7f) + Vector3.up * 30f;
            float slip = BallCurveModel.CalculateSlipSpeed(new Vector3(0f, 0f, 7f), spin, Radius);

            Assert.AreEqual(0f, slip, 0.0001f);
        }

        [Test]
        public void 閾値を超えていれば滑っているとみなす()
        {
            BallCurveSettings s = CreateSettings();

            Assert.IsTrue(BallCurveModel.IsSliding(0.2f, s));
            Assert.IsFalse(BallCurveModel.IsSliding(0.15f, s));
            Assert.IsFalse(BallCurveModel.IsSliding(0.1f, s));
        }

        // ---- 横向きの加速度 ----

        [Test]
        public void 横回転が正なら進行方向の右へ加速する()
        {
            Vector3 a = BallCurveModel.CalculateLateralAcceleration(
                new Vector3(0f, 0f, 7f), 10f, CreateSettings());

            Assert.Greater(a.x, 0f);
            Assert.AreEqual(0f, a.z, 0.0001f);
            Assert.AreEqual(0f, a.y, 0.0001f);
        }

        [Test]
        public void 横回転が負なら進行方向の左へ加速する()
        {
            Vector3 a = BallCurveModel.CalculateLateralAcceleration(
                new Vector3(0f, 0f, 7f), -10f, CreateSettings());

            Assert.Less(a.x, 0f);
        }

        [Test]
        public void 横回転が0なら加速しない()
        {
            Vector3 a = BallCurveModel.CalculateLateralAcceleration(
                new Vector3(0f, 0f, 7f), 0f, CreateSettings());

            Assert.AreEqual(0f, a.magnitude, 0.0001f);
        }

        [Test]
        public void 横回転が強いほど加速度も大きい()
        {
            BallCurveSettings s = CreateSettings();
            float weak = BallCurveModel.CalculateLateralAcceleration(new Vector3(0f, 0f, 7f), 5f, s).magnitude;
            float strong = BallCurveModel.CalculateLateralAcceleration(new Vector3(0f, 0f, 7f), 20f, s).magnitude;

            Assert.Greater(strong, weak);
        }

        [Test]
        public void 斜めに進んでいても進行方向に対して直角に加速する()
        {
            Vector3 velocity = new Vector3(1f, 0f, 1f).normalized * 7f;
            Vector3 a = BallCurveModel.CalculateLateralAcceleration(velocity, 10f, CreateSettings());

            Assert.AreEqual(0f, Vector3.Dot(a.normalized, velocity.normalized), 0.0001f);
        }

        [Test]
        public void 止まっていたら加速しない()
        {
            Vector3 a = BallCurveModel.CalculateLateralAcceleration(Vector3.zero, 10f, CreateSettings());

            Assert.AreEqual(0f, a.magnitude, 0.0001f);
        }

        // ---- 横回転の減衰 ----

        [Test]
        public void 横回転は時間とともに減る()
        {
            BallCurveSettings s = CreateSettings();
            float after = BallCurveModel.DecaySideSpin(30f, 0.5f, s);

            Assert.Less(after, 30f);
            Assert.Greater(after, 0f);
        }

        [Test]
        public void 減衰を0にすると横回転が減らない()
        {
            BallCurveSettings s = CreateSettings();
            s.spinDecay = 0f;

            Assert.AreEqual(30f, BallCurveModel.DecaySideSpin(30f, 1f, s), 0.0001f);
        }

        [Test]
        public void 減衰が大きいほど早く減る()
        {
            BallCurveSettings slow = CreateSettings();
            slow.spinDecay = 0.5f;
            BallCurveSettings fast = CreateSettings();
            fast.spinDecay = 5f;

            Assert.Greater(
                BallCurveModel.DecaySideSpin(30f, 1f, slow),
                BallCurveModel.DecaySideSpin(30f, 1f, fast));
        }

        [Test]
        public void 減衰しても符号は変わらない()
        {
            BallCurveSettings s = CreateSettings();

            Assert.Less(BallCurveModel.DecaySideSpin(-30f, 1f, s), 0f);
        }

        // ---- 予測線 ----

        [Test]
        public void 予測線は指定した点数を返す()
        {
            var points = new List<Vector3>();
            BallCurveModel.PredictPath(
                Vector3.zero, Vector3.forward, 7f, 0f, 2f, 20f, CreateSettings(), 1f, 16, points);

            Assert.AreEqual(16, points.Count);
        }

        [Test]
        public void カーブ無しなら予測線はまっすぐ()
        {
            var points = new List<Vector3>();
            BallCurveModel.PredictPath(
                Vector3.zero, Vector3.forward, 7f, 0f, 2f, 20f, CreateSettings(), 1f, 16, points);

            for (int i = 0; i < points.Count; i++)
            {
                Assert.AreEqual(0f, points[i].x, 0.0001f);
            }
        }

        [Test]
        public void 右カーブなら予測線が右へ曲がる()
        {
            var points = new List<Vector3>();
            BallCurveModel.PredictPath(
                Vector3.zero, Vector3.forward, 7f, 30f, 2f, 20f, CreateSettings(), 1f, 32, points);

            Assert.Greater(points[points.Count - 1].x, 0f);
        }

        [Test]
        public void 左カーブなら予測線が左へ曲がる()
        {
            var points = new List<Vector3>();
            BallCurveModel.PredictPath(
                Vector3.zero, Vector3.forward, 7f, -30f, 2f, 20f, CreateSettings(), 1f, 32, points);

            Assert.Less(points[points.Count - 1].x, 0f);
        }

        [Test]
        public void 滑りが無ければ曲がらない()
        {
            // 最初から転がっている状態なら、横回転があっても曲がらない
            var points = new List<Vector3>();
            BallCurveModel.PredictPath(
                Vector3.zero, Vector3.forward, 7f, 30f, 0f, 20f, CreateSettings(), 1f, 32, points);

            Assert.AreEqual(0f, points[points.Count - 1].x, 0.0001f);
        }

        [Test]
        public void 曲がりは滑っている間だけで止まる()
        {
            // 滑りが尽きたあとは横向きの速度が増えないので、進路は直線になる
            var points = new List<Vector3>();
            BallCurveModel.PredictPath(
                Vector3.zero, Vector3.forward, 7f, 30f, 1f, 20f, CreateSettings(), 1.5f, 64, points);

            int last = points.Count - 1;
            Vector3 lateA = points[last] - points[last - 1];
            Vector3 lateB = points[last - 1] - points[last - 2];

            Assert.AreEqual(lateB.x, lateA.x, 0.0001f);
        }
    }
}
