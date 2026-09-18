using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 動くものが置いてあるレーン。
    ///
    /// 時間を数えるのはこのレーンだけで、障害物は言われた時刻の位置に移るだけ。
    /// こうしておくと、レーンを出入りしても動きの位相がずれない。
    ///
    /// 左右に動く壁のほかに、回る円盤や動くピン台も、
    /// MovingObstacle の向きと振れ幅を変えれば同じ形で作れる。
    /// </summary>
    public class MovingLane : BasicLane
    {
        [Header("動くもの")]
        [Tooltip("このレーンで動かすもの。プレハブの中の MovingObstacle を並べる。")]
        [SerializeField] private MovingObstacle[] obstacles;

        [Tooltip("投げ終わるたびに動きを最初に戻すか。" +
                 "オフだと動き続けるので、1投目と2投目で位置が変わる。" +
                 "オンにすると位置が飛ぶので、構えている間に戻すこと。")]
        [SerializeField] private bool restartEachThrow = false;

        /// <summary>レーンに入ってからの経過（秒）。</summary>
        private float _time;

        /// <summary>レーンに入ってからの経過（秒）。</summary>
        public float LaneTime => _time;

        public override void OnLaneStart(LaneContext context)
        {
            base.OnLaneStart(context);

            // 入った瞬間は当たりを解かずに置く。前のレーンの位置から滑ってこないように
            _time = 0f;
            WarpAll();
        }

        public override void OnThrowEnd()
        {
            base.OnThrowEnd();

            if (!restartEachThrow)
            {
                return;
            }

            // 構えに戻ったところで位置を戻す。転がっている最中に飛ばさないため
            _time = 0f;
            WarpAll();
        }

        /// <summary>レーンの毎フレーム更新。ここで動くものを進める。</summary>
        protected override void OnLaneFixedUpdate(float deltaTime)
        {
            if (obstacles == null)
            {
                return;
            }

            _time += deltaTime;

            foreach (MovingObstacle obstacle in obstacles)
            {
                if (obstacle != null)
                {
                    obstacle.Step(_time);
                }
            }
        }

        /// <summary>今の時刻の位置へ、当たりを解かずに置き直す。</summary>
        private void WarpAll()
        {
            if (obstacles == null)
            {
                return;
            }

            foreach (MovingObstacle obstacle in obstacles)
            {
                if (obstacle != null)
                {
                    obstacle.Warp(_time);
                }
            }
        }
    }
}
