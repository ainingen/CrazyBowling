using UnityEngine;
using CrazyBowling.Ball;
using CrazyBowling.Pins;

namespace CrazyBowling.Core
{
    /// <summary>
    /// 段階2の投球進行。
    /// 投げる → ボールが決着 → ピンの静止を待つ → 倒れた本数を判定 → 次の投球。
    /// 段階3で GameManager に統合する前提の暫定クラス。スコア計算はここでは行わない。
    /// </summary>
    public class ThrowSequencer : MonoBehaviour
    {
        /// <summary>進行の状態。</summary>
        private enum SequenceState
        {
            /// <summary>構え中。投げられるのを待っている。</summary>
            Ready,

            /// <summary>ボールが転がっている。</summary>
            Rolling,

            /// <summary>ボールは決着した。ピンが静止するのを待っている。</summary>
            WaitingForPins,

            /// <summary>判定が終わり、次の投球の準備をしている。</summary>
            Preparing,
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

        private SequenceState _state;
        private int _throwNumber = 1;
        private float _judgeTimer;
        private float _prepareTimer;
        private bool _resetPinsNext;

        /// <summary>今が何投目か（1 か 2）。</summary>
        public int ThrowNumber => _throwNumber;

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
            }
        }

        /// <summary>構え中：投げられたら転がり中へ。</summary>
        private void UpdateReady()
        {
            if (ballController.IsRolling)
            {
                _state = SequenceState.Rolling;
            }
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

        /// <summary>準備中：少し待ってからボールを構えに戻す。</summary>
        private void UpdatePreparing()
        {
            _prepareTimer += Time.deltaTime;
            if (_prepareTimer < delayBeforeNextThrow)
            {
                return;
            }

            if (_resetPinsNext)
            {
                pinSet.ResetAll();
                _throwNumber = 1;
            }
            else
            {
                _throwNumber = 2;
            }

            ballController.ReturnToSpawn();
            _state = SequenceState.Ready;
        }

        /// <summary>倒れた本数を数えて Console に出し、次に何をするか決める。</summary>
        private void Judge(bool timedOut)
        {
            int fallen = pinSet.JudgeThrow(_throwNumber);

            if (logEvents && timedOut)
            {
                Debug.Log($"ピンが静止しないまま {judgeTimeoutSeconds:F0} 秒経過したので判定します", this);
            }

            if (_throwNumber <= 1)
            {
                bool isStrike = fallen >= pinSet.PinCount;

                if (logEvents)
                {
                    Debug.Log($"1投目：{fallen}本", this);
                }

                if (isStrike)
                {
                    if (logEvents)
                    {
                        Debug.Log("ストライク", this);
                        Debug.Log($"合計：{pinSet.TotalFallen}本", this);
                    }
                    _resetPinsNext = true;
                }
                else
                {
                    // 倒れたピンを取り除いてから2投目へ
                    pinSet.RemoveFallen();
                    _resetPinsNext = false;
                }
            }
            else
            {
                if (logEvents)
                {
                    Debug.Log($"2投目：{fallen}本（新たに倒れた本数）", this);
                    Debug.Log($"合計：{pinSet.TotalFallen}本", this);
                }
                _resetPinsNext = true;
            }

            _prepareTimer = 0f;
            _state = SequenceState.Preparing;
        }
    }
}
