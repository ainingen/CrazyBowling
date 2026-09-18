using UnityEngine;
using CrazyBowling.Ball;
using CrazyBowling.Data;
using CrazyBowling.Pins;

namespace CrazyBowling.Core
{
    /// <summary>
    /// レーン1本ぶんの投球進行。
    /// 投げる → ボールが決着 → ピンの静止を待つ → 倒れた本数を判定 → 次の投球 or レーン終了。
    /// レーンをまたぐ進行は GameManager が持つ。ここは1本の中だけを見る。
    /// </summary>
    public class ThrowSequencer : MonoBehaviour
    {
        /// <summary>進行の状態。</summary>
        private enum SequenceState
        {
            /// <summary>まだ始まっていない。GameManager が BeginLane を呼ぶのを待っている。</summary>
            Idle,

            /// <summary>構え中。投げられるのを待っている。</summary>
            Ready,

            /// <summary>ボールが転がっている。</summary>
            Rolling,

            /// <summary>ボールは決着した。ピンが静止するのを待っている。</summary>
            WaitingForPins,

            /// <summary>判定が終わり、次の投球の準備をしている。</summary>
            Preparing,

            /// <summary>判定が終わり、レーンの終わりを知らせる前の間を置いている。</summary>
            Finishing,
        }

        [Header("参照")]
        [SerializeField] private BallController ballController;
        [SerializeField] private PinSet pinSet;

        [Header("判定")]
        [Tooltip("ボールが決着してから、ピンの静止を待つ上限（秒）。ここを超えたら静止を待たずに判定する。")]
        [SerializeField] private float judgeTimeoutSeconds = 6f;

        [Tooltip("ボールが決着した直後に誤判定しないための最低待ち（秒）。")]
        [SerializeField] private float minJudgeDelay = 0.5f;

        [Header("次の投球まで")]
        [Tooltip("判定してから、ボールを構えに戻すまでの間（秒）。倒れたピンを見る時間。")]
        [SerializeField] private float delayBeforeNextThrow = 1.5f;

        [Header("デバッグ")]
        [Tooltip("投球の結果を Console に出す。")]
        [SerializeField] private bool logEvents = true;

        private SequenceState _state = SequenceState.Idle;
        private int _throwNumber = 1;
        private int _maxThrows = 2;
        private float _judgeTimer;
        private float _prepareTimer;

        /// <summary>今が何投目か（1から数える）。</summary>
        public int ThrowNumber => _throwNumber;

        /// <summary>
        /// レーンが終わったときに呼ばれる。
        /// 投球の結果をそのまま渡す。得点にするのは GameManager の仕事。
        /// </summary>
        public event System.Action<LaneThrowResult> LaneFinished;

        /// <summary>投げた瞬間に呼ばれる。レーン側の演出に使う。</summary>
        public event System.Action ThrowStarted;

        /// <summary>1投が決着した瞬間に呼ばれる。</summary>
        public event System.Action ThrowEnded;

        /// <summary>
        /// レーン1本を始める。ピンを立て直し、1投目から数え直す。
        /// GameManager が呼ぶ。
        /// </summary>
        public void BeginLane(LaneData data)
        {
            _maxThrows = data != null ? data.ThrowCount : 2;
            _throwNumber = 1;
            _judgeTimer = 0f;
            _prepareTimer = 0f;

            if (pinSet != null)
            {
                pinSet.ResetAll();
            }

            _state = SequenceState.Ready;
        }

        /// <summary>進行を止める。レーンの切り替え中に投球を受け付けないようにする。</summary>
        public void StopLane()
        {
            _state = SequenceState.Idle;
        }

        private void Update()
        {
            if (ballController == null || pinSet == null)
            {
                return;
            }

            switch (_state)
            {
                case SequenceState.Ready:
                    UpdateReady();
                    break;
                case SequenceState.Rolling:
                    UpdateRolling();
                    break;
                case SequenceState.WaitingForPins:
                    UpdateWaitingForPins();
                    break;
                case SequenceState.Preparing:
                    UpdatePreparing();
                    break;
                case SequenceState.Finishing:
                    UpdateFinishing();
                    break;
            }
        }

        /// <summary>構え中：投げられたら転がり中へ。</summary>
        private void UpdateReady()
        {
            if (!ballController.IsRolling)
            {
                return;
            }

            _state = SequenceState.Rolling;
            ThrowStarted?.Invoke();
        }

        /// <summary>転がり中：ボールが決着したら、そこからピンの静止を待ち始める。</summary>
        private void UpdateRolling()
        {
            if (!ballController.IsSettled)
            {
                return;
            }

            // タイマーは「投げた瞬間」ではなく「ボールが決着した瞬間」から数える。
            // 弱い球でピンに届かなかった場合でも、途中で打ち切られないようにするため
            _judgeTimer = 0f;
            _state = SequenceState.WaitingForPins;
            ThrowEnded?.Invoke();
        }

        /// <summary>ピン待ち：全部静止するか、上限の秒数が過ぎたら判定する。</summary>
        private void UpdateWaitingForPins()
        {
            _judgeTimer += Time.deltaTime;

            if (_judgeTimer < minJudgeDelay)
            {
                return;
            }

            bool timedOut = _judgeTimer >= judgeTimeoutSeconds;
            if (!pinSet.AreAllAtRest() && !timedOut)
            {
                return;
            }

            Judge(timedOut);
        }

        /// <summary>準備中：少し待ってから次の投球へ。</summary>
        private void UpdatePreparing()
        {
            _prepareTimer += Time.deltaTime;
            if (_prepareTimer < delayBeforeNextThrow)
            {
                return;
            }

            _throwNumber++;
            ballController.ReturnToSpawn();
            _state = SequenceState.Ready;
        }

        /// <summary>終了待ち：倒れたピンを見せてから、レーンの終わりを知らせる。</summary>
        private void UpdateFinishing()
        {
            _prepareTimer += Time.deltaTime;
            if (_prepareTimer < delayBeforeNextThrow)
            {
                return;
            }

            _state = SequenceState.Idle;
            LaneFinished?.Invoke(new LaneThrowResult
            {
                firstThrowFallen = pinSet.FirstThrowFallen,
                secondThrowFallen = pinSet.SecondThrowFallen,
                throwCount = _throwNumber,
                pinCount = pinSet.PinCount,
            });
        }

        /// <summary>倒れた本数を数えて、次に何をするか決める。</summary>
        private void Judge(bool timedOut)
        {
            int fallen = pinSet.JudgeThrow(_throwNumber);
            int total = pinSet.TotalFallen;

            var settings = new ThrowProgressSettings
            {
                maxThrows = _maxThrows,
                pinCount = pinSet.PinCount,
            };

            if (logEvents)
            {
                if (timedOut)
                {
                    Debug.Log($"ピンが静止しないまま {judgeTimeoutSeconds:F0} 秒経過したので判定します", this);
                }

                Debug.Log($"{_throwNumber}投目：{fallen}本（合計 {total}本）", this);

                if (ThrowProgress.IsStrike(_throwNumber, total, settings))
                {
                    Debug.Log("ストライク", this);
                }
                else if (ThrowProgress.IsSpare(_throwNumber, total, settings))
                {
                    Debug.Log("スペア", this);
                }
            }

            _prepareTimer = 0f;

            if (ThrowProgress.HasNextThrow(_throwNumber, total, settings))
            {
                // 倒れたピンを取り除いてから次の投球へ。
                // 連鎖爆発は1投につき1回なので、残ったピンの記録も消しておく
                pinSet.RemoveFallen();
                pinSet.PrepareNextThrow();
                _state = SequenceState.Preparing;
                return;
            }

            _state = SequenceState.Finishing;
        }
    }
}
