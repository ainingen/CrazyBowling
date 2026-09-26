using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// そのレーンにいるあいだだけ、連鎖爆発の「厚い当たり」を測る基準点を差し替える。
    ///
    /// ── なぜこの作りか ──────────────────────────────
    ///
    /// 連鎖爆発を持つ PinExplosion はシーンにひとつしかない共有のもので、
    /// レーンごとには用意されていない。直接書き換えると全レーンの爆発条件が変わる。
    ///
    /// そこでレーンのプレハブにこの部品を置き、
    /// レーンが現れたときに差し替え、消えるときに必ず元へ戻す。
    /// 考え方は LaneBallLook・LaneLighting・LanePostFx と同じ。
    ///
    /// 通常は「ヘッドピンが立っていた位置」からの横ずれで厚みを測るが、
    /// ピン台が回るレーンでは並びが散らばり、ヘッドピンに意味がなくなる。
    /// 円盤の中心などを基準にしたいときに使う。
    ///
    /// あわせて「kinematic の相手をボールとみなさない」切り替えも入れる。
    /// 動かされる壁を持つレーンで、ピンが壁にぶつかっただけで爆発しないようにするため。
    ///
    /// 物理には一切触らない。
    /// </summary>
    public class LaneBlastReference : MonoBehaviour
    {
        [Tooltip("このレーンにいるあいだ、厚い当たりを測る基準にする点。" +
                 "空なら何もしない（ヘッドピンの立っていた位置のまま）。")]
        [SerializeField] private Transform reference;

        [Tooltip("kinematic の相手（動かされる壁など）を、起点の判定でボールとみなさないか。" +
                 "9本目はオン。オフだと、ピンがカップの壁にぶつかっただけで満威力の爆発になりうる。")]
        [SerializeField] private bool kinematicIsNotBall = true;

        /// <summary>元に戻すために覚えておく。</summary>
        private Pins.PinExplosion _explosion;
        private Transform _previous;
        private bool _previousKinematicIsNotBall;
        private bool _applied;

        private void OnEnable()
        {
            if ((reference == null && !kinematicIsNotBall) || _applied)
            {
                return;
            }

            _explosion = Object.FindFirstObjectByType<Pins.PinExplosion>();
            if (_explosion == null)
            {
                return;
            }

            _previous = _explosion.BlastReference;
            _previousKinematicIsNotBall = _explosion.KinematicIsNotBall;
            if (reference != null)
            {
                _explosion.BlastReference = reference;
            }
            _explosion.KinematicIsNotBall = kinematicIsNotBall;
            _applied = true;
        }

        private void OnDisable()
        {
            Restore();
        }

        private void OnDestroy()
        {
            Restore();
        }

        /// <summary>基準点と判定の切り替えを元に戻す。戻し忘れると次のレーンに持ち越してしまう。</summary>
        private void Restore()
        {
            if (!_applied)
            {
                return;
            }

            if (_explosion != null)
            {
                _explosion.BlastReference = _previous;
                _explosion.KinematicIsNotBall = _previousKinematicIsNotBall;
            }

            _explosion = null;
            _previous = null;
            _applied = false;
        }
    }
}
