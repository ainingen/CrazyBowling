using NUnit.Framework;
using UnityEngine;
using CrazyBowling.Core;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>
    /// CameraPathSampler の EditMode テスト。
    /// 下見の終わりが構えの視点とぴったり一致しないと、最後にカクッと飛ぶ。
    /// </summary>
    public class CameraPathSamplerTests
    {
        private static CameraWaypoint[] Straight()
        {
            return new[]
            {
                new CameraWaypoint(new Vector3(0f, 1.6f, -2.5f), Quaternion.identity),
                new CameraWaypoint(new Vector3(0f, 2.0f, 5f), Quaternion.identity),
                new CameraWaypoint(new Vector3(0f, 2.4f, 12f), Quaternion.identity),
                new CameraWaypoint(new Vector3(0f, 2.0f, 15f), Quaternion.identity),
            };
        }

        // ---- 往復 ----

        [Test]
        public void 往復の始めは出発点()
        {
            Assert.AreEqual(0f, CameraPathSampler.ToOneWayProgress(0f), 0.0001f);
        }

        [Test]
        public void 往復の真ん中が折り返し点()
        {
            Assert.AreEqual(1f, CameraPathSampler.ToOneWayProgress(0.5f), 0.0001f);
        }

        [Test]
        public void 往復の終わりは出発点に戻る()
        {
            Assert.AreEqual(0f, CameraPathSampler.ToOneWayProgress(1f), 0.0001f);
        }

        [Test]
        public void 往復は真ん中をはさんで対称になる()
        {
            Assert.AreEqual(
                CameraPathSampler.ToOneWayProgress(0.3f),
                CameraPathSampler.ToOneWayProgress(0.7f),
                0.0001f);
        }

        [Test]
        public void 往復の進み具合は範囲外でも収まる()
        {
            Assert.AreEqual(0f, CameraPathSampler.ToOneWayProgress(-1f), 0.0001f);
            Assert.AreEqual(0f, CameraPathSampler.ToOneWayProgress(2f), 0.0001f);
        }

        // ---- 経路をたどる ----

        [Test]
        public void 進み具合0では先頭の点になる()
        {
            CameraWaypoint sample = CameraPathSampler.Sample(Straight(), 0f);

            Assert.AreEqual(new Vector3(0f, 1.6f, -2.5f), sample.position);
        }

        [Test]
        public void 進み具合1では末尾の点になる()
        {
            CameraWaypoint sample = CameraPathSampler.Sample(Straight(), 1f);

            Assert.AreEqual(0f, Vector3.Distance(new Vector3(0f, 2.0f, 15f), sample.position), 0.0001f);
        }

        [Test]
        public void 下見の終わりは構えの視点とぴったり一致する()
        {
            // 往復の終わり（進み具合1）は片道の0、つまり先頭の点
            CameraWaypoint[] path = Straight();
            float oneWay = CameraPathSampler.ToOneWayProgress(1f);
            CameraWaypoint sample = CameraPathSampler.Sample(path, oneWay);

            Assert.AreEqual(0f, Vector3.Distance(path[0].position, sample.position), 0.0001f,
                "ここがずれると、下見の最後に画面が飛ぶ");
        }

        [Test]
        public void 途中では先頭より奥へ進んでいる()
        {
            CameraWaypoint sample = CameraPathSampler.Sample(Straight(), 0.5f);

            Assert.Greater(sample.position.z, -2.5f);
            Assert.Less(sample.position.z, 15f);
        }

        [Test]
        public void 進み具合が範囲外でも端で止まる()
        {
            Assert.AreEqual(
                CameraPathSampler.Sample(Straight(), 0f).position,
                CameraPathSampler.Sample(Straight(), -5f).position);

            Assert.AreEqual(
                CameraPathSampler.Sample(Straight(), 1f).position,
                CameraPathSampler.Sample(Straight(), 5f).position);
        }

        [Test]
        public void 通過点が1つでも壊れない()
        {
            var one = new[] { new CameraWaypoint(new Vector3(1f, 2f, 3f), Quaternion.identity) };

            Assert.AreEqual(new Vector3(1f, 2f, 3f), CameraPathSampler.Sample(one, 0.5f).position);
        }

        [Test]
        public void 通過点が空でも壊れない()
        {
            Assert.AreEqual(Vector3.zero, CameraPathSampler.Sample(new CameraWaypoint[0], 0.5f).position);
            Assert.AreEqual(Vector3.zero, CameraPathSampler.Sample(null, 0.5f).position);
        }

        [Test]
        public void 通過点が2つでも間をつなぐ()
        {
            var two = new[]
            {
                new CameraWaypoint(Vector3.zero, Quaternion.identity),
                new CameraWaypoint(new Vector3(0f, 0f, 10f), Quaternion.identity),
            };

            Assert.AreEqual(5f, CameraPathSampler.Sample(two, 0.5f).position.z, 0.0001f);
        }

        [Test]
        public void 経路は途切れずつながっている()
        {
            CameraWaypoint[] path = Straight();
            Vector3 previous = CameraPathSampler.Sample(path, 0f).position;

            for (int i = 1; i <= 100; i++)
            {
                Vector3 current = CameraPathSampler.Sample(path, i / 100f).position;

                Assert.Less(Vector3.Distance(previous, current), 1f, "隣り合う点が離れすぎている");
                previous = current;
            }
        }

        // ---- 既定の経路 ----

        [Test]
        public void 既定の経路は構えの視点から始まる()
        {
            var home = new CameraWaypoint(new Vector3(0f, 1.6f, -2.5f), Quaternion.identity);

            CameraWaypoint[] path = CameraPathSampler.BuildDefaultPath(home, new Vector3(0f, 0.2f, 16.2f), 2.2f);

            Assert.AreEqual(home.position, path[0].position);
        }

        [Test]
        public void 既定の経路は目標に近づいていく()
        {
            var home = new CameraWaypoint(new Vector3(0f, 1.6f, -2.5f), Quaternion.identity);
            var target = new Vector3(0f, 0.2f, 16.2f);

            CameraWaypoint[] path = CameraPathSampler.BuildDefaultPath(home, target, 2.2f);

            for (int i = 1; i < path.Length; i++)
            {
                Assert.Less(
                    Vector3.Distance(path[i].position, target),
                    Vector3.Distance(path[i - 1].position, target),
                    $"{i}番目の点が目標から遠ざかっている");
            }
        }

        [Test]
        public void 既定の経路の終わりは目標を向いている()
        {
            var home = new CameraWaypoint(new Vector3(0f, 1.6f, -2.5f), Quaternion.identity);
            var target = new Vector3(0f, 0.2f, 16.2f);

            CameraWaypoint[] path = CameraPathSampler.BuildDefaultPath(home, target, 2.2f);
            CameraWaypoint last = path[path.Length - 1];

            Vector3 forward = last.rotation * Vector3.forward;
            Vector3 toTarget = (target - last.position).normalized;

            Assert.Greater(Vector3.Dot(forward, toTarget), 0.99f);
        }

        [Test]
        public void 目標が構えの位置と同じなら1点だけ返す()
        {
            var home = new CameraWaypoint(new Vector3(0f, 1.6f, -2.5f), Quaternion.identity);

            CameraWaypoint[] path = CameraPathSampler.BuildDefaultPath(home, home.position, 2.2f);

            Assert.AreEqual(1, path.Length);
        }
    }
}
