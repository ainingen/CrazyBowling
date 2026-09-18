using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CrazyBowling.Pins;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>
    /// PinFallJudge の EditMode テスト。
    /// </summary>
    public class PinFallJudgeTests
    {
        /// <summary>Inspector の仮の初期値と同じ設定。</summary>
        private static PinJudgeSettings CreateSettings()
        {
            return new PinJudgeSettings
            {
                tiltThresholdDegrees = 30f,
                fallYThreshold = -0.2f,
                horizontalMoveThreshold = 0.3f,
                restLinearSpeed = 0.05f,
                restAngularSpeed = 0.5f,
            };
        }

        /// <summary>指定した角度だけ傾いた「上方向」を作る。</summary>
        private static Vector3 TiltedUp(float degrees)
        {
            return Quaternion.AngleAxis(degrees, Vector3.forward) * Vector3.up;
        }

        /// <summary>立っているピン1本分の観測値。</summary>
        private static PinSample StandingPin()
        {
            Vector3 position = new Vector3(0f, 0.05f, 16.2f);
            return new PinSample
            {
                up = Vector3.up,
                position = position,
                initialPosition = position,
                linearSpeed = 0f,
                angularSpeed = 0f,
            };
        }

        [Test]
        public void 真上を向いていたら倒れていない()
        {
            Assert.IsFalse(PinFallJudge.IsFallen(StandingPin(), CreateSettings()));
        }

        [Test]
        public void 閾値より少し手前の傾きは倒れていない()
        {
            PinSample pin = StandingPin();
            pin.up = TiltedUp(29f);

            Assert.IsFalse(PinFallJudge.IsFallen(pin, CreateSettings()));
        }

        [Test]
        public void 閾値を超えて傾いたら倒れている()
        {
            PinSample pin = StandingPin();
            pin.up = TiltedUp(31f);

            Assert.IsTrue(PinFallJudge.IsFallen(pin, CreateSettings()));
        }

        [Test]
        public void 閾値ちょうどの傾きは倒れていない()
        {
            // 「超える」が条件なので、閾値と同じ角度は倒れていない。
            // 浮動小数の誤差を避けるため、実際に測った角度をそのまま閾値にする
            PinSample pin = StandingPin();
            pin.up = TiltedUp(30f);

            PinJudgeSettings settings = CreateSettings();
            settings.tiltThresholdDegrees = Vector3.Angle(pin.up, Vector3.up);

            Assert.IsFalse(PinFallJudge.IsFallen(pin, settings));
        }

        [Test]
        public void 逆さまなら倒れている()
        {
            PinSample pin = StandingPin();
            pin.up = Vector3.down;

            Assert.IsTrue(PinFallJudge.IsFallen(pin, CreateSettings()));
        }

        [Test]
        public void 真上を向いていても落下したら倒れている()
        {
            PinSample pin = StandingPin();
            pin.position = new Vector3(0f, -0.6f, 19f);

            Assert.IsTrue(PinFallJudge.IsFallen(pin, CreateSettings()));
        }

        [Test]
        public void 立ったまま水平に大きく動いたら倒れている()
        {
            // ガターに立ったまま滑り込んだピン。傾きも高さも基準に届かない
            PinSample pin = StandingPin();
            pin.position = pin.initialPosition + new Vector3(0.6f, 0f, 0f);

            Assert.IsTrue(PinFallJudge.IsFallen(pin, CreateSettings()));
        }

        [Test]
        public void 少しずれただけなら倒れていない()
        {
            PinSample pin = StandingPin();
            pin.position = pin.initialPosition + new Vector3(0.1f, 0f, 0.1f);

            Assert.IsFalse(PinFallJudge.IsFallen(pin, CreateSettings()));
        }

        [Test]
        public void 水平移動の判定に高さの差は含めない()
        {
            // 少し跳ねただけでは、水平方向には動いていない扱いにする
            PinSample pin = StandingPin();
            pin.position = pin.initialPosition + new Vector3(0.1f, 1.0f, 0f);

            Assert.IsFalse(PinFallJudge.IsFallen(pin, CreateSettings()));
        }

        [Test]
        public void 倒れている本数を数えられる()
        {
            List<PinSample> pins = new List<PinSample>();
            for (int i = 0; i < 10; i++)
            {
                PinSample pin = StandingPin();
                if (i < 3)
                {
                    pin.up = TiltedUp(80f);
                }
                pins.Add(pin);
            }

            Assert.AreEqual(3, PinFallJudge.CountFallen(pins, CreateSettings()));
        }

        [Test]
        public void 全部立っていたら0本_全部倒れていたら10本()
        {
            List<PinSample> standing = new List<PinSample>();
            List<PinSample> fallen = new List<PinSample>();
            for (int i = 0; i < 10; i++)
            {
                standing.Add(StandingPin());

                PinSample pin = StandingPin();
                pin.up = TiltedUp(90f);
                fallen.Add(pin);
            }

            Assert.AreEqual(0, PinFallJudge.CountFallen(standing, CreateSettings()));
            Assert.AreEqual(10, PinFallJudge.CountFallen(fallen, CreateSettings()));
        }

        [Test]
        public void 全部が遅ければ静止とみなす()
        {
            List<PinSample> pins = new List<PinSample>();
            for (int i = 0; i < 10; i++)
            {
                PinSample pin = StandingPin();
                pin.linearSpeed = 0.01f;
                pin.angularSpeed = 0.1f;
                pins.Add(pin);
            }

            Assert.IsTrue(PinFallJudge.AreAllAtRest(pins, CreateSettings()));
        }

        [Test]
        public void 一本でも速く動いていたら静止とみなさない()
        {
            List<PinSample> pins = new List<PinSample>();
            for (int i = 0; i < 10; i++)
            {
                pins.Add(StandingPin());
            }
            PinSample moving = StandingPin();
            moving.linearSpeed = 1.5f;
            pins[7] = moving;

            Assert.IsFalse(PinFallJudge.AreAllAtRest(pins, CreateSettings()));
        }

        [Test]
        public void ピットに落ちたピンはデッキから出ている()
        {
            PinSample pin = StandingPin();
            pin.position = new Vector3(0f, -0.6f, 19f);

            Assert.IsTrue(PinFallJudge.IsOutOfPlay(pin, CreateSettings()));
        }

        [Test]
        public void 台から外れるほど動いたピンはデッキから出ている()
        {
            PinSample pin = StandingPin();
            pin.position = pin.initialPosition + new Vector3(0.6f, 0f, 0f);

            Assert.IsTrue(PinFallJudge.IsOutOfPlay(pin, CreateSettings()));
        }

        [Test]
        public void 傾いているだけのピンはデッキから出ていない()
        {
            // 倒れてはいるが、まだデッキ上にある。立ち直ることも他のピンに当たることもある
            PinSample pin = StandingPin();
            pin.up = TiltedUp(80f);

            Assert.IsTrue(PinFallJudge.IsFallen(pin, CreateSettings()));
            Assert.IsFalse(PinFallJudge.IsOutOfPlay(pin, CreateSettings()));
        }

        [Test]
        public void ピットで転がり続けるピンは静止判定から除く()
        {
            // ピットに落ちたピンは止まらないので、これを待つと毎投タイムアウトしてしまう
            List<PinSample> pins = new List<PinSample>();
            for (int i = 0; i < 9; i++)
            {
                pins.Add(StandingPin());
            }
            PinSample inPit = StandingPin();
            inPit.position = new Vector3(0.2f, -0.6f, 19f);
            inPit.linearSpeed = 2f;
            inPit.angularSpeed = 20f;
            pins.Add(inPit);

            Assert.IsTrue(PinFallJudge.AreAllAtRest(pins, CreateSettings()));
        }

        [Test]
        public void デッキ上で傾いて動いているピンは静止判定を待つ()
        {
            // 倒れかけて滑っているピンは、まだ他のピンを倒しうるので待つ
            List<PinSample> pins = new List<PinSample>();
            for (int i = 0; i < 9; i++)
            {
                pins.Add(StandingPin());
            }
            PinSample sliding = StandingPin();
            sliding.up = TiltedUp(80f);
            sliding.position = sliding.initialPosition + new Vector3(0.1f, 0f, 0f);
            sliding.linearSpeed = 1.2f;
            pins.Add(sliding);

            Assert.IsFalse(PinFallJudge.AreAllAtRest(pins, CreateSettings()));
        }

        [Test]
        public void 角速度だけ大きい場合も静止とみなさない()
        {
            List<PinSample> pins = new List<PinSample>();
            for (int i = 0; i < 10; i++)
            {
                pins.Add(StandingPin());
            }
            PinSample spinning = StandingPin();
            spinning.angularSpeed = 3f;
            pins[2] = spinning;

            Assert.IsFalse(PinFallJudge.AreAllAtRest(pins, CreateSettings()));
        }
    }
}
