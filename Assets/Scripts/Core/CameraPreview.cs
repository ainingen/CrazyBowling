using UnityEngine;
using UnityEngine.InputSystem;
using CrazyBowling.Ball;
using CrazyBowling.Lanes;

namespace CrazyBowling.Core
{
    /// <summary>
    /// 投球前の下見カメラ。
    /// レーンに入ったときに一度だけ奥まで行って戻る。
    /// 構え中は「奥を見る」ボタンを押している間だけ奥へ寄る。
    ///
    /// 経路はレーンが持つ（LaneCameraPath）。無ければ自動で作る。
    /// </summary>
    public class CameraPreview : MonoBehaviour
    {
        /// <summary>下見の状態。</summary>
        private enum PreviewState
        {
            /// <summary>何もしていない。カメラは通常の制御に任せる。</summary>
            Idle,

            /// <summary>自動の下見を再生中。</summary>
            Auto,

            /// <summary>手動の下見。押した分だけ奥へ進み、離すとその場で止まる。</summary>
            Manual,

            /// <summary>手動の下見から構えの視点へ戻っている最中。</summary>
            Returning,
        }

        [Header("参照")]
        [Tooltip("下見の間だけ止めるカメラ制御。")]
        [SerializeField] private CameraController cameraController;

        [Tooltip("下見の間だけ入力を止めるボール。")]
        [SerializeField] private BallController ballController;

        [Header("自動の下見")]
        [Tooltip("レーンに入ったときに自動で流す。")]
        [SerializeField] private bool playOnLaneStart = true;

        [Tooltip("往復にかかる時間（秒）。短いほど待ちが減る。")]
        [SerializeField] private float duration = 1.8f;

        [Tooltip("進み方の緩急。左端が出発、右端が帰着。")]
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("押すかキーを叩くと下見を飛ばす。")]
        [SerializeField] private bool skippable = true;

        [Header("手動の下見")]
        [Tooltip("押している間に奥へ進む速さ（1秒で進む割合）。離すとその場で止まる。")]
        [SerializeField] private float manualSpeed = 1.6f;

        [Tooltip("構えの視点へ戻る速さ（1秒で戻る割合）。" +
                 "投球操作を始めたときに自動で戻る。進む速さと同じか少し速めにする。")]
        [SerializeField] private float manualReturnSpeed = 2.2f;

        [Header("経路が無いレーンのとき")]
        [Tooltip("自動で作る経路の、途中の高さ（m）。")]
        [SerializeField] private float defaultTravelHeight = 2.2f;

        [Tooltip("自動で作る経路が見に行く場所（レーンの原点から見た奥行き）。")]
        [SerializeField] private float defaultTargetZ = 16.2f;

        [Header("デバッグ")]
        [Tooltip("下見の開始と終了を Console に出す。")]
        [SerializeField] private bool logEvents = false;

        private PreviewState _state = PreviewState.Idle;

        /// <summary>今たどっている経路。</summary>
        private CameraWaypoint[] _path;

        /// <summary>構え中のカメラの位置と向き。戻り先。</summary>
        private CameraWaypoint _home;

        /// <summary>自動の下見の進み具合（0〜1の往復）。</summary>
        private float _autoProgress;

        /// <summary>手動の下見の進み具合（0で構え、1で奥）。</summary>
        private float _manualProgress;

        /// <summary>手動の下見のボタンが押されているか。</summary>
        private bool _manualHeld;

        /// <summary>下見が動いているか。UIの出し入れに使う。</summary>
        public bool IsPlaying => _state != PreviewState.Idle;

        /// <summary>自動の下見が動いているか。スキップできるのはこの間だけ。</summary>
        public bool IsAutoPlaying => _state == PreviewState.Auto;

        /// <summary>手動の下見が使えるか。構え中だけ。</summary>
        public bool CanLookAround =>
            _state != PreviewState.Auto
            && _state != PreviewState.Returning
            && ballController != null && ballController.IsAiming;

        /// <summary>手動の下見でどこまで進んでいるか。0で構えの視点、1で経路の終点。</summary>
        public float LookAroundProgress => _manualProgress;

        /// <summary>
        /// レーンに入ったときに呼ぶ。経路を組み立てて、自動の下見を始める。
        /// GameManager がレーンを差し替えたあとに呼ぶこと。
        /// </summary>
        public void BeginLane(LaneBehaviour lane, Transform cameraAnchor, Transform laneRoot)
        {
            _home = cameraAnchor != null
                ? new CameraWaypoint(cameraAnchor.position, cameraAnchor.rotation)
                : new CameraWaypoint(transform.position, transform.rotation);

            _path = BuildPath(lane, laneRoot);
            _manualProgress = 0f;
            _manualHeld = false;

            if (!playOnLaneStart || _path == null || _path.Length < 2)
            {
                Stop();
                return;
            }

            _autoProgress = 0f;
            _state = PreviewState.Auto;
            SetExternalControl(true);

            if (logEvents)
            {
                Debug.Log($"下見をはじめる（通過点{_path.Length}個・{duration:F1}秒）", this);
            }
        }

        /// <summary>自動の下見を打ち切って構えに戻す。</summary>
        public void Skip()
        {
            if (_state != PreviewState.Auto)
            {
                return;
            }

            if (logEvents)
            {
                Debug.Log("下見を飛ばした", this);
            }
            Stop();
        }

        /// <summary>手動の下見のボタンを押した／離した。UI から呼ぶ。</summary>
        public void SetLookAroundHeld(bool held)
        {
            _manualHeld = held;
        }

        private void LateUpdate()
        {
            switch (_state)
            {
                case PreviewState.Auto:
                    UpdateAuto();
                    break;
                case PreviewState.Manual:
                    UpdateManual();
                    break;
                case PreviewState.Returning:
                    UpdateReturning();
                    break;
                default:
                    UpdateIdle();
                    break;
            }
        }

        /// <summary>自動の下見：経路を往復して、終わったら構えに返す。</summary>
        private void UpdateAuto()
        {
            if (skippable && WasSkipRequested())
            {
                Skip();
                return;
            }

            _autoProgress += Time.deltaTime / Mathf.Max(duration, 0.01f);

            if (_autoProgress >= 1f)
            {
                Stop();
                return;
            }

            float eased = ease != null ? ease.Evaluate(_autoProgress) : _autoProgress;
            Apply(CameraPathSampler.ToOneWayProgress(eased));
        }

        /// <summary>
        /// 手動の下見：押している間だけ経路を進み、離したらその位置で止まる。
        /// 構えの視点へは戻さない。止めた位置からもう一度押せば続きを進む。
        /// </summary>
        private void UpdateManual()
        {
            // 投球操作を始めたら構えの視点へ戻す。戻す専用のボタンは作らない
            if (ballController != null && !ballController.IsAiming)
            {
                _state = PreviewState.Returning;
                return;
            }

            if (_manualHeld)
            {
                _manualProgress = Mathf.Clamp01(_manualProgress + manualSpeed * Time.deltaTime);
            }

            // 離している間は進めないだけで、その位置に居続ける
            Apply(_manualProgress);
        }

        /// <summary>構えの視点へ戻っている最中。戻りきったら通常の制御に返す。</summary>
        private void UpdateReturning()
        {
            _manualProgress = Mathf.Clamp01(_manualProgress - manualReturnSpeed * Time.deltaTime);

            if (_manualProgress <= 0f)
            {
                Stop();
                return;
            }

            Apply(_manualProgress);
        }

        /// <summary>止まっている間：ボタンが押されたら手動の下見を始める。</summary>
        private void UpdateIdle()
        {
            if (!_manualHeld || !CanLookAround || _path == null || _path.Length < 2)
            {
                return;
            }

            _state = PreviewState.Manual;
            SetExternalControl(true);
        }

        /// <summary>経路上の1点へカメラを置く。</summary>
        private void Apply(float progress)
        {
            if (_path == null || _path.Length == 0)
            {
                return;
            }

            CameraWaypoint point = CameraPathSampler.Sample(_path, progress);
            transform.SetPositionAndRotation(point.position, point.rotation);
        }

        /// <summary>下見をやめて、構えの視点にそろえてから通常の制御に返す。</summary>
        private void Stop()
        {
            _state = PreviewState.Idle;
            _manualProgress = 0f;

            // 経路の先頭＝構えの視点なので、ここで一致させておけば画面が飛ばない
            transform.SetPositionAndRotation(_home.position, _home.rotation);
            SetExternalControl(false);
        }

        /// <summary>カメラ制御とボールの入力を、下見の間だけ止める。</summary>
        private void SetExternalControl(bool active)
        {
            if (cameraController != null)
            {
                cameraController.ExternalControl = active;
            }

            // 手動の下見では投げられてよい（投げ始めたら戻る）。自動の下見の間だけ止める
            if (ballController != null)
            {
                ballController.SetInputBlocked(active && _state == PreviewState.Auto);
            }
        }

        /// <summary>下見を飛ばす入力があったか。</summary>
        private static bool WasSkipRequested()
        {
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                return true;
            }

            return Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        }

        /// <summary>レーンの経路を使う。無ければ自動で作る。</summary>
        private CameraWaypoint[] BuildPath(LaneBehaviour lane, Transform laneRoot)
        {
            if (lane != null)
            {
                LaneCameraPath path = lane.GetComponentInChildren<LaneCameraPath>(true);
                if (path != null)
                {
                    CameraWaypoint[] built = path.BuildPath(_home);
                    if (built != null && built.Length >= 2)
                    {
                        return built;
                    }
                }
            }

            Transform basis = laneRoot != null ? laneRoot : transform;
            Vector3 target = basis.TransformPoint(new Vector3(0f, 0.2f, defaultTargetZ));

            return CameraPathSampler.BuildDefaultPath(_home, target, defaultTravelHeight);
        }
    }
}
