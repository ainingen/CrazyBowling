using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// そのレーンにいるあいだだけ、ボールに光の尾を付ける。輪の中の軌跡を一目で分かるようにするため。
    ///
    /// ── なぜこの作りか ──────────────────────────────
    ///
    /// ボールは全レーン共有。ボールに部品を足したままにすると、次のレーンにも尾が付いてしまう。
    ///
    /// ★ボールの持ち物は書き換えない。尾は、この部品が自分で作った子（LaneTrail）に付け、
    ///   無効になったら、その子ごと消す。
    ///   「入ったときの状態を覚えて、出るときに戻す」作りにはしない
    ///   （9本目の不具合の教訓。前のレーンの後始末より前の状態を覚えてしまうことがある）。
    ///
    /// 転がっているあいだだけ尾を出す。構えに戻るときの瞬間移動で、長い線が引かれないように、
    /// 転がり終えたら尾を消す。
    ///
    /// 物理には一切触らない（子には当たり判定を付けない）。
    /// </summary>
    public class LaneBallTrail : MonoBehaviour
    {
        [Tooltip("尾のマテリアル。加算の半透明にする。")]
        [SerializeField] private Material trailMaterial;

        [Tooltip("尾が残る時間（秒）。")]
        [SerializeField] private float trailTime = 0.6f;

        [Tooltip("尾の根元の太さ（m）。ボールの直径くらい。")]
        [SerializeField] private float startWidth = 0.18f;

        [Tooltip("根元の色。先へ行くほど透明になる。")]
        [SerializeField] private Color headColor = new Color(0.4f, 0.9f, 1f, 1f);

        [Tooltip("尾の先の色。")]
        [SerializeField] private Color tailColor = new Color(0.8f, 0.3f, 1f, 0f);

        private Ball.BallController _ball;
        private GameObject _owned;
        private TrailRenderer _trail;
        private bool _wasRolling;

        private void OnEnable()
        {
            _ball = Object.FindFirstObjectByType<Ball.BallController>();
            if (_ball == null)
            {
                return;
            }

            // ボールの子として、この部品の持ち物を作る（見た目だけ。当たり判定は付けない）
            _owned = new GameObject("LaneTrail");
            _owned.transform.SetParent(_ball.transform, false);

            _trail = _owned.AddComponent<TrailRenderer>();
            _trail.sharedMaterial = trailMaterial;
            _trail.time = trailTime;
            _trail.minVertexDistance = 0.03f;
            _trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            _trail.widthMultiplier = startWidth;
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;
            _trail.numCapVertices = 2;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(headColor, 0f), new GradientColorKey(tailColor, 1f) },
                new[] { new GradientAlphaKey(headColor.a, 0f), new GradientAlphaKey(tailColor.a, 1f) });
            _trail.colorGradient = gradient;

            _trail.emitting = false;
            _wasRolling = false;
        }

        private void OnDisable()
        {
            // 自分で作った子ごと消す。ボールの持ち物には何も残らない
            if (_owned != null)
            {
                Destroy(_owned);
            }
            _owned = null;
            _trail = null;
            _ball = null;
        }

        private void LateUpdate()
        {
            if (_trail == null || _ball == null)
            {
                return;
            }

            bool rolling = _ball.IsInPlay && !_ball.IsSettled;
            if (rolling == _wasRolling)
            {
                return;
            }

            if (rolling)
            {
                // 投げた瞬間：構えの位置から残っている点を捨ててから出し始める
                _trail.Clear();
                _trail.emitting = true;
            }
            else
            {
                // 転がり終えた：瞬間移動の線を引かないよう、止めて消す
                _trail.emitting = false;
                _trail.Clear();
            }

            _wasRolling = rolling;
        }
    }
}
