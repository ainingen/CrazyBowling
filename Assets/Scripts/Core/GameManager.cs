using UnityEngine;
using CrazyBowling.Ball;
using CrazyBowling.Data;
using CrazyBowling.Lanes;
using CrazyBowling.Pins;

namespace CrazyBowling.Core
{
    /// <summary>
    /// レーンをまたぐ進行。
    /// レーンを差し替える → 投球係に1本まかせる → 終わったら次のレーンへ → 全部終わったらリザルト。
    /// 得点の計算は段階3-Bで ScoreCalculator に移す。今は倒した本数をそのまま記録する。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        /// <summary>ゲームの進行状態。</summary>
        private enum GameState
        {
            /// <summary>まだ始まっていない。</summary>
            Idle,

            /// <summary>レーンを遊んでいる最中。</summary>
            Playing,

            /// <summary>レーンが終わり、次のレーンへの間を置いている。</summary>
            BetweenLanes,

            /// <summary>全レーンが終わった。</summary>
            Finished,
        }

        [Header("レーン")]
        [Tooltip("回るレーンの並び。")]
        [SerializeField] private LaneSequence laneSequence;

        [Tooltip("レーンのプレハブを入れる空の入れ物。中身は毎回作り直す。")]
        [SerializeField] private Transform laneRoot;

        [Header("共通の持ち物")]
        [Tooltip("ボールの操作。")]
        [SerializeField] private BallController ballController;

        [Tooltip("ボールの構え位置。レーンのアンカーに合わせて動かす。")]
        [SerializeField] private Transform ballSpawnPoint;

        [Tooltip("ピン10本のまとまり。")]
        [SerializeField] private PinSet pinSet;

        [Tooltip("ピットのまとまり。")]
        [SerializeField] private Transform pitRoot;

        [Tooltip("カメラの制御。")]
        [SerializeField] private CameraController cameraController;

        [Tooltip("1レーンぶんの投球係。")]
        [SerializeField] private ThrowSequencer throwSequencer;

        [Header("間合い")]
        [Tooltip("レーンが終わってから次のレーンに移るまでの間（秒）。")]
        [SerializeField] private float delayBetweenLanes = 2f;

        [Header("デバッグ")]
        [Tooltip("レーンの切り替わりと結果を Console に出す。")]
        [SerializeField] private bool logEvents = true;

        private GameState _state = GameState.Idle;
        private int _laneIndex = -1;
        private float _betweenTimer;

        /// <summary>今生きているレーンの実体。次を作る前に消す。</summary>
        private GameObject _laneInstance;

        /// <summary>今のレーンの振る舞い。付いていなければ null。</summary>
        private LaneBehaviour _laneBehaviour;

        /// <summary>レーンごとに倒した本数。段階3-Bで得点に変える。</summary>
        private int[] _laneFallen;

        /// <summary>今が何レーン目か（1から数える）。始まっていなければ0。</summary>
        public int LaneNumber => _laneIndex + 1;

        /// <summary>今のレーンの設定。始まっていなければ null。</summary>
        public LaneData CurrentLane =>
            laneSequence != null ? laneSequence.GetLane(_laneIndex) : null;

        /// <summary>全レーンが終わったか。</summary>
        public bool IsFinished => _state == GameState.Finished;

        /// <summary>ここまでに倒した合計本数。</summary>
        public int TotalFallen
        {
            get
            {
                if (_laneFallen == null)
                {
                    return 0;
                }

                int sum = 0;
                foreach (int fallen in _laneFallen)
                {
                    sum += fallen;
                }
                return sum;
            }
        }

        private void Awake()
        {
            if (throwSequencer != null)
            {
                throwSequencer.LaneFinished += OnLaneFinished;
                throwSequencer.ThrowStarted += OnThrowStarted;
                throwSequencer.ThrowEnded += OnThrowEnded;
            }
        }

        private void OnDestroy()
        {
            if (throwSequencer != null)
            {
                throwSequencer.LaneFinished -= OnLaneFinished;
                throwSequencer.ThrowStarted -= OnThrowStarted;
                throwSequencer.ThrowEnded -= OnThrowEnded;
            }
        }

        private void Start()
        {
            StartGame();
        }

        /// <summary>最初のレーンから始める。</summary>
        public void StartGame()
        {
            if (laneSequence == null || laneSequence.Count <= 0)
            {
                Debug.LogWarning("レーンの並びが入っていないので始められません", this);
                return;
            }

            _laneFallen = new int[laneSequence.Count];
            _laneIndex = -1;
            GoToNextLane();
        }

        private void Update()
        {
            if (_state != GameState.BetweenLanes)
            {
                return;
            }

            _betweenTimer += Time.deltaTime;
            if (_betweenTimer >= delayBetweenLanes)
            {
                GoToNextLane();
            }
        }

        /// <summary>次のレーンへ。並びを使い切ったらリザルトへ。</summary>
        private void GoToNextLane()
        {
            _laneIndex++;

            if (_laneIndex >= laneSequence.Count)
            {
                FinishGame();
                return;
            }

            LaneData data = laneSequence.GetLane(_laneIndex);
            if (data == null)
            {
                Debug.LogWarning($"{LaneNumber}レーン目が空です。飛ばします", this);
                GoToNextLane();
                return;
            }

            SetUpLane(data);

            _state = GameState.Playing;
            throwSequencer.BeginLane(data);

            if (logEvents)
            {
                Debug.Log($"── {LaneNumber}レーン目：{data.LaneName} ──", this);
                if (!string.IsNullOrEmpty(data.Description))
                {
                    Debug.Log(data.Description, this);
                }
            }
        }

        /// <summary>レーンを差し替えて、共通の持ち物をアンカーの位置に置く。</summary>
        private void SetUpLane(LaneData data)
        {
            if (throwSequencer != null)
            {
                throwSequencer.StopLane();
            }

            DestroyCurrentLane();

            if (data.LanePrefab != null && laneRoot != null)
            {
                _laneInstance = Instantiate(data.LanePrefab, laneRoot);
                _laneInstance.name = data.LanePrefab.name;
            }

            // レーン側に先に形を決めさせる。傾きなどを反映してからアンカーを読む
            _laneBehaviour = _laneInstance != null
                ? _laneInstance.GetComponent<LaneBehaviour>()
                : null;

            if (_laneBehaviour != null)
            {
                _laneBehaviour.OnLaneStart(BuildContext(data));
            }

            PlaceByAnchors();

            ballController.ApplyLaneSettings(data.MaxAngleDegrees);
            ballController.ReturnToSpawn();
        }

        /// <summary>レーンに渡す持ち物をまとめる。</summary>
        private LaneContext BuildContext(LaneData data)
        {
            return new LaneContext
            {
                ball = ballController,
                ballCollider = ballController != null
                    ? ballController.GetComponent<Collider>()
                    : null,
                pinSet = pinSet,
                data = data,
            };
        }

        /// <summary>
        /// レーンの目印に合わせて、ボール・ピン・ピット・カメラを置く。
        /// 目印が無いレーンでは、今の場所をそのまま使う。
        /// </summary>
        private void PlaceByAnchors()
        {
            LaneAnchors anchors = _laneInstance != null
                ? _laneInstance.GetComponentInChildren<LaneAnchors>()
                : null;

            if (anchors == null)
            {
                return;
            }

            MoveTo(ballSpawnPoint, anchors.SpawnAnchor);
            MoveTo(pitRoot, anchors.PitAnchor);

            if (pinSet != null && anchors.PinAnchor != null)
            {
                MoveTo(pinSet.transform, anchors.PinAnchor);

                // ピンはピン台の向きに合わせて並び直す
                pinSet.ApplyLayout();
            }

            if (cameraController != null && anchors.CameraAnchor != null)
            {
                cameraController.SetAimingView(anchors.CameraAnchor);
            }
        }

        /// <summary>目印の位置と向きに合わせる。どちらかが無ければ何もしない。</summary>
        private static void MoveTo(Transform target, Transform anchor)
        {
            if (target == null || anchor == null)
            {
                return;
            }
            target.SetPositionAndRotation(anchor.position, anchor.rotation);
        }

        /// <summary>今のレーンを片付ける。</summary>
        private void DestroyCurrentLane()
        {
            if (_laneBehaviour != null)
            {
                _laneBehaviour.OnLaneEnd();
                _laneBehaviour = null;
            }

            if (_laneInstance != null)
            {
                Destroy(_laneInstance);
                _laneInstance = null;
            }
        }

        /// <summary>投球係から「レーンが終わった」と知らされた。</summary>
        private void OnLaneFinished(int fallen)
        {
            if (_state != GameState.Playing)
            {
                return;
            }

            if (_laneFallen != null && _laneIndex >= 0 && _laneIndex < _laneFallen.Length)
            {
                _laneFallen[_laneIndex] = fallen;
            }

            if (logEvents)
            {
                Debug.Log($"{LaneNumber}レーン目の結果：{fallen}本（ここまでの合計 {TotalFallen}本）", this);
            }

            _betweenTimer = 0f;
            _state = GameState.BetweenLanes;
        }

        private void OnThrowStarted()
        {
            if (_laneBehaviour != null)
            {
                _laneBehaviour.OnThrowStart();
            }
        }

        private void OnThrowEnded()
        {
            if (_laneBehaviour != null)
            {
                _laneBehaviour.OnThrowEnd();
            }
        }

        /// <summary>全レーンが終わった。</summary>
        private void FinishGame()
        {
            _state = GameState.Finished;

            if (throwSequencer != null)
            {
                throwSequencer.StopLane();
            }

            if (logEvents)
            {
                Debug.Log($"══ 全{laneSequence.Count}レーン終了：合計 {TotalFallen}本 ══", this);
            }
        }
    }
}
