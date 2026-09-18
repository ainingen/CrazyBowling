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

        [Tooltip("投球前の下見カメラ。無くてもよい。")]
        [SerializeField] private CameraPreview cameraPreview;

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

        /// <summary>レーンごとの得点。まだ遊んでいないレーンは既定値のまま。</summary>
        private LaneScore[] _laneScores;

        /// <summary>そのレーンをもう遊んだか。得点表で空欄と0点を区別するのに使う。</summary>
        private bool[] _lanePlayed;

        /// <summary>今が何レーン目か（1から数える）。始まっていなければ0。</summary>
        public int LaneNumber => _laneIndex + 1;

        /// <summary>今のレーンの設定。始まっていなければ null。</summary>
        public LaneData CurrentLane =>
            laneSequence != null ? laneSequence.GetLane(_laneIndex) : null;

        /// <summary>全レーンが終わったか。</summary>
        public bool IsFinished => _state == GameState.Finished;

        /// <summary>ここまでの合計得点。</summary>
        public int TotalScore => ScoreCalculator.CalculateTotal(_laneScores);

        /// <summary>ここまでに倒した合計本数。</summary>
        public int TotalFallen
        {
            get
            {
                if (_laneScores == null)
                {
                    return 0;
                }

                int sum = 0;
                foreach (LaneScore lane in _laneScores)
                {
                    sum += lane.fallen;
                }
                return sum;
            }
        }

        /// <summary>倍率なしで取れる満点。リザルトで「◯点中」と出すのに使う。</summary>
        public int PerfectScore => ScoreCalculator.CalculatePerfectScore(LaneCount);

        /// <summary>指定した位置のレーンの得点。まだ遊んでいなければ既定値。</summary>
        public LaneScore GetLaneScore(int laneIndex)
        {
            if (_laneScores == null || laneIndex < 0 || laneIndex >= _laneScores.Length)
            {
                return default;
            }
            return _laneScores[laneIndex];
        }

        /// <summary>そのレーンをもう遊んだか。得点表で空欄と0点を分けるのに使う。</summary>
        public bool IsLanePlayed(int laneIndex)
        {
            return _lanePlayed != null
                && laneIndex >= 0 && laneIndex < _lanePlayed.Length
                && _lanePlayed[laneIndex];
        }

        /// <summary>指定した位置のレーンの設定。得点表の名前に使う。</summary>
        public LaneData GetLaneData(int laneIndex)
        {
            return laneSequence != null ? laneSequence.GetLane(laneIndex) : null;
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

        /// <summary>レーンの本数。</summary>
        public int LaneCount => laneSequence != null ? laneSequence.Count : 0;

        /// <summary>最初のレーンから始める。</summary>
        public void StartGame()
        {
            StartGame(0);
        }

        /// <summary>指定した位置のレーンから始める。0で1レーン目。</summary>
        public void StartGame(int startLaneIndex)
        {
            if (laneSequence == null || laneSequence.Count <= 0)
            {
                Debug.LogWarning("レーンの並びが入っていないので始められません", this);
                return;
            }

            ResetScores();
            _laneIndex = Mathf.Clamp(startLaneIndex, 0, laneSequence.Count - 1) - 1;
            GoToNextLane();
        }

        /// <summary>
        /// 指定したレーンへ飛ぶ。1で1レーン目。
        /// 途中から始めるので、それまでのレーンの得点は0のままになる。
        /// 開発中の確認用。
        /// </summary>
        public void JumpToLane(int laneNumber)
        {
            if (laneSequence == null || laneSequence.Count <= 0)
            {
                return;
            }

            if (_laneScores == null || _laneScores.Length != laneSequence.Count)
            {
                ResetScores();
            }

            _state = GameState.Idle;
            _laneIndex = Mathf.Clamp(laneNumber, 1, laneSequence.Count) - 2;
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
            AlignToFloor();
            RefreshFloorLook();

            ballController.SetFloorSampler(BuildFloorSampler());
            ballController.SetFrictionSampler(BuildFrictionSampler());
            ballController.ApplyLaneSettings(data.MaxAngleDegrees);
            ballController.ReturnToSpawn();

            StartPreview();
        }

        /// <summary>
        /// 下見カメラを始める。レーンに入ったときの1回だけ流れる。
        /// 2投目では呼ばれない（投球係が内部で回すため）。
        /// </summary>
        private void StartPreview()
        {
            if (cameraPreview == null)
            {
                return;
            }

            LaneAnchors anchors = _laneInstance != null
                ? _laneInstance.GetComponentInChildren<LaneAnchors>()
                : null;

            cameraPreview.BeginLane(
                _laneBehaviour,
                anchors != null ? anchors.CameraAnchor : null,
                _laneInstance != null ? _laneInstance.transform : null);
        }

        /// <summary>
        /// 今のレーンに床の高さを聞く役を作る。形を知らないレーンでは null を返す。
        /// </summary>
        private BallController.FloorSampler BuildFloorSampler()
        {
            if (_laneBehaviour == null)
            {
                return null;
            }

            LaneBehaviour lane = _laneBehaviour;
            return (Vector3 worldPosition, out float height) =>
                lane != null && lane.TrySampleFloor(worldPosition, out height, out _)
                    ? true
                    : Fail(out height);
        }

        /// <summary>
        /// 今のレーンに摩擦を聞く役を作る。レーンが無ければ null を返し、全域が乾いた扱いになる。
        /// </summary>
        private BallController.FrictionSampler BuildFrictionSampler()
        {
            if (_laneBehaviour == null)
            {
                return null;
            }

            LaneBehaviour lane = _laneBehaviour;
            return worldPosition => lane != null ? lane.GetFrictionScale(worldPosition) : 1f;
        }

        /// <summary>床の高さが分からなかったときの返し方をまとめる。</summary>
        private static bool Fail(out float height)
        {
            height = 0f;
            return false;
        }

        /// <summary>
        /// ボールとピン台を、床の高さに合わせて置き直す。
        /// 起伏のあるレーンでは、アンカーの高さと実際の床の高さがずれるため。
        /// 平らなレーンは TrySampleFloor が false を返すので何も起きない。
        /// </summary>
        private void AlignToFloor()
        {
            if (_laneBehaviour == null)
            {
                return;
            }

            // ボールは床から半径のぶん浮かせる。
            // 構え中に左右へずらしたときも追従するよう、調べる手段ごと渡す
            if (ballSpawnPoint != null && ballController != null
                && _laneBehaviour.TrySampleFloor(ballSpawnPoint.position, out float spawnFloorY, out _))
            {
                Vector3 position = ballSpawnPoint.position;
                position.y = spawnFloorY + ballController.BallRadius;
                ballSpawnPoint.position = position;
            }

            // ピン台は、ヘッドピンの足元の床に合わせる。
            // ピン台の手前から奥は平らに保たれているので、1点で測れば10本ぶん足りる
            if (pinSet != null
                && _laneBehaviour.TrySampleFloor(pinSet.DeckSamplePoint, out float deckFloorY, out _))
            {
                Vector3 position = pinSet.transform.position;
                position.y = deckFloorY - pinSet.BaseY;
                pinSet.transform.position = position;
                pinSet.ApplyLayout();
            }
        }

        /// <summary>
        /// 床の見た目（格子と高さの色分け）を測り直す。
        /// LaneFloorLook の Awake はプレハブを作った時点で走るので、
        /// レーンが傾きや起伏を決める前の「平らな床」を測ってしまう。
        /// 形が決まったこの時点で測り直す。
        /// </summary>
        private void RefreshFloorLook()
        {
            if (_laneInstance == null)
            {
                return;
            }

            foreach (LaneFloorLook look in _laneInstance.GetComponentsInChildren<LaneFloorLook>(true))
            {
                look.Apply();
            }
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

        /// <summary>得点の記録を作り直す。</summary>
        private void ResetScores()
        {
            int count = laneSequence != null ? laneSequence.Count : 0;
            _laneScores = new LaneScore[count];
            _lanePlayed = new bool[count];
        }

        /// <summary>投球係から「レーンが終わった」と知らされた。ここで得点にする。</summary>
        private void OnLaneFinished(LaneThrowResult result)
        {
            if (_state != GameState.Playing)
            {
                return;
            }

            LaneData data = CurrentLane;
            LaneScore score = ScoreCalculator.Calculate(
                result, data != null ? data.ScoreMultiplier : 1f);

            if (_laneScores != null && _laneIndex >= 0 && _laneIndex < _laneScores.Length)
            {
                _laneScores[_laneIndex] = score;
                _lanePlayed[_laneIndex] = true;
            }

            if (logEvents)
            {
                string kind = score.isStrike ? "ストライク"
                    : score.isSpare ? "スペア"
                    : score.fallen + "本";
                Debug.Log($"{LaneNumber}レーン目の結果：{kind} → {score.score}点"
                    + $"（ここまでの合計 {TotalScore}点）", this);
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
                Debug.Log($"══ 全{laneSequence.Count}レーン終了："
                    + $"{TotalScore}点 / {PerfectScore}点（{TotalFallen}本） ══", this);
            }
        }
    }
}
