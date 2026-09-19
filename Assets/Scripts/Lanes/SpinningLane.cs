using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 回る円盤が埋め込まれたレーン。
    ///
    /// 時間を数えるのはこのレーンだけで、円盤は言われた時刻の向きに回るだけ。
    /// こうしておくと、レーンを出入りしても回転の位相がずれない。
    ///
    /// 円盤は当たり判定を持たないので、ボールを流す力はここから渡す。
    /// BallController が TryGetDrift を呼び、返ってきた加速度を加える。
    /// </summary>
    public class SpinningLane : BasicLane
    {
        [Header("回る円盤")]
        [Tooltip("このレーンで回す円盤。プレハブの中の SpinningDisc を並べる。")]
        [SerializeField] private SpinningDisc[] discs;

        [Tooltip("投げ終わるたびに回転を最初に戻すか。" +
                 "オフだと回り続けるので、1投目と2投目で向きが変わる。")]
        [SerializeField] private bool restartEachThrow = false;

        /// <summary>レーンに入ってからの経過（秒）。</summary>
        private float _time;

        /// <summary>レーンに入ってからの経過（秒）。</summary>
        public float LaneTime => _time;

        public override void OnLaneStart(LaneContext context)
        {
            base.OnLaneStart(context);

            _time = 0f;
            ResetAll();
        }

        public override void OnThrowEnd()
        {
            base.OnThrowEnd();

            if (!restartEachThrow)
            {
                return;
            }

            // 構えに戻ったところで向きを戻す。転がっている最中に飛ばさないため
            _time = 0f;
            ResetAll();
        }

        /// <summary>レーンの毎フレーム更新。ここで円盤を回す。</summary>
        protected override void OnLaneFixedUpdate(float deltaTime)
        {
            if (discs == null)
            {
                return;
            }

            _time += deltaTime;

            foreach (SpinningDisc disc in discs)
            {
                if (disc != null)
                {
                    disc.Step(_time);
                }
            }
        }

        /// <summary>
        /// その位置で受ける横向きの加速度。ボールが円盤の上にいるときだけ答える。
        ///
        /// 円盤を順に見て、最初に効いたものをそのまま使う。
        /// 円盤どうしが重なる置き方は想定していないため、足し合わせていない。
        /// 将来、重ねて置きたくなったら合算に変えること。
        /// </summary>
        public override bool TryGetDrift(Vector3 worldPosition, Vector3 worldVelocity, out Vector3 acceleration)
        {
            acceleration = Vector3.zero;

            if (discs == null)
            {
                return false;
            }

            foreach (SpinningDisc disc in discs)
            {
                if (disc == null)
                {
                    continue;
                }

                // 円盤は子オブジェクトなので、中心はワールド座標で渡す
                if (SpinningDiscField.TryCalculateDrift(
                        disc.transform.position,
                        worldPosition,
                        worldVelocity,
                        disc.Settings,
                        out acceleration))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>今の時刻の向きへ戻す。</summary>
        private void ResetAll()
        {
            if (discs == null)
            {
                return;
            }

            foreach (SpinningDisc disc in discs)
            {
                if (disc != null)
                {
                    disc.ResetTo(_time);
                }
            }
        }
    }
}
