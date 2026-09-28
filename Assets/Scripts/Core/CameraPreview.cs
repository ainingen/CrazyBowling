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

        [Header("見回し（LOOK AHEAD の間。段階6）")]
        [Tooltip("ドラッグ1ピクセルあたりに向きを変える角度（度）。")]
        [SerializeField] private float lookDegreesPerPixel = 0.12f;

        [Tooltip("ホイール1目盛りあたりの寄り引き（画角の度数）。")]
        [SerializeField] private float wheelZoomPerNotch = 2f;

        [Tooltip("2本指の距離1ピクセルあたりの寄り引き（画角の度数）。")]
        [SerializeField] private float pinchZoomPerPixel = 0.04f;

        [Tooltip("BACK で戻るときに、向きと寄り引きを元へ戻す速さ（度／秒）。")]
        [SerializeField] private float lookRelaxSpeed = 150f;

        [Tooltip("振れる幅の既定値。レーンに LaneLookAround があれば、そのレーンだけそちらを使う。")]
        [SerializeField] private LookAroundLimits defaultLookLimits = LookAroundLimits.Default;

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

        private Camera _camera;
        private float _baseFov = -1f;

        /// <summary>見回しで足している向き（x：左右、y：上下。上が正。度）。</summary>
        private Vector2 _look;

        /// <summary>見回しで足している画角（負で寄る。度）。</summary>
        private float _zoom;

        /// <summary>このレーンで振れる幅。</summary>
        private LookAroundLimits _limits = LookAroundLimits.Default;

        /// <summary>見回しのドラッグの最中か（押し始めがボタンなどの上なら false）。</summary>
        private bool _dragging;
        private Vector2 _lastPointer;
        private float _lastPinch = -1f;

        /// <summary>
        /// 見回し中か（LOOK AHEAD で奥へ進んでいる間と、止まっている間）。
        /// この間はドラッグで向きを、ホイール・2本指で寄り引きを変える。球は投げられない。BACK（<see cref="EndLookAround"/>）で戻る。
        /// </summary>
        public bool IsLookingAround => _state == PreviewState.Manual;

        /// <summary>見回しで足している向き（確かめるとき用）。</summary>
        public Vector2 Look => _look;

        /// <summary>見回しで足している画角（確かめるとき用）。</summary>
        public float Zoom => _zoom;

        /// <summary>このレーンで振れる幅（確かめるとき用）。</summary>
        public LookAroundLimits Limits => _limits;

        /// <summary>見回しを終えて構えの視点へ戻る（BACK ボタンから呼ぶ）。</summary>
        public void EndLookAround()
        {
            if (_state == PreviewState.Manual)
            {
                _state = PreviewState.Returning;
                _dragging = false;
                _lastPinch = -1f;
            }
        }

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

            // 見回しの幅：レーンに LaneLookAround があれば、そのレーンだけそちらを使う
            LaneLookAround laneLook = lane != null ? lane.GetComponentInChildren<LaneLookAround>(true) : null;
            _limits = laneLook != null ? laneLook.Limits : defaultLookLimits;
            ResetLook();

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
        /// 手動の下見（見回し）：押している間だけ経路を進み、離したらその位置で止まる。
        /// 止めた位置からもう一度押せば続きを進む。この間はドラッグで向きを、ホイール・2本指で寄り引きを変える
        /// （カメラの位置は経路の上から動かさない）。球は投げられない。BACK で構えの視点へ戻る。
        /// </summary>
        private void UpdateManual()
        {
            // 念のため：球が構えから外れたら（ふつうは入力を止めているので起きない）構えの視点へ戻す
            if (ballController != null && !ballController.IsAiming)
            {
                EndLookAround();
                return;
            }

            if (_manualHeld)
            {
                _manualProgress = Mathf.Clamp01(_manualProgress + manualSpeed * Time.deltaTime);
            }

            ReadLookInput();

            // 離している間は進めないだけで、その位置に居続ける
            Apply(_manualProgress);
        }

        /// <summary>構えの視点へ戻っている最中。向きと寄り引きも元へ戻す。戻りきったら通常の制御に返す。</summary>
        private void UpdateReturning()
        {
            _manualProgress = Mathf.Clamp01(_manualProgress - manualReturnSpeed * Time.deltaTime);
            _look = LookAroundRules.Relax(_look, lookRelaxSpeed, Time.deltaTime);
            _zoom = Mathf.MoveTowards(_zoom, 0f, lookRelaxSpeed * 0.25f * Time.deltaTime);

            if (_manualProgress <= 0f)
            {
                Stop();
                return;
            }

            Apply(_manualProgress);
        }

        /// <summary>見回しの入力を読む：ドラッグ（マウス・指1本）で向き、ホイール・2本指で寄り引き。</summary>
        private void ReadLookInput()
        {
            // 2本指：つまむ・広げるで寄り引き（その間は向きを変えない）
            Touchscreen touch = Touchscreen.current;
            int fingers = 0;
            Vector2 f0 = Vector2.zero, f1 = Vector2.zero;
            if (touch != null)
            {
                foreach (var t in touch.touches)
                {
                    if (!t.press.isPressed)
                    {
                        continue;
                    }
                    if (fingers == 0) f0 = t.position.ReadValue();
                    else if (fingers == 1) f1 = t.position.ReadValue();
                    fingers++;
                }
            }
            if (fingers >= 2)
            {
                float distance = Vector2.Distance(f0, f1);
                if (_lastPinch >= 0f)
                {
                    _zoom = LookAroundRules.ApplyZoom(_zoom, LookAroundRules.PinchToZoom(_lastPinch, distance, pinchZoomPerPixel), _limits);
                }
                _lastPinch = distance;
                _dragging = false;
                return;
            }
            _lastPinch = -1f;

            // ホイール
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    _zoom = LookAroundRules.ApplyZoom(_zoom, LookAroundRules.WheelToZoom(wheel, wheelZoomPerNotch), _limits);
                }
            }

            // ドラッグ：押し始めがボタンなどの上なら見回さない（LOOK AHEAD・BACK を押しても向きが動かないように）
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }
            Vector2 position = pointer.position.ReadValue();
            if (pointer.press.wasPressedThisFrame)
            {
                _dragging = !IsPointerOverUI();
                _lastPointer = position;
                return;
            }
            if (!pointer.press.isPressed)
            {
                _dragging = false;
                return;
            }
            if (_dragging)
            {
                _look = LookAroundRules.ApplyDrag(_look, position - _lastPointer, lookDegreesPerPixel, _limits);
                _lastPointer = position;
            }
        }

        private static bool IsPointerOverUI()
        {
            return UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>見回しを元に戻す（向き 0・寄り引き 0・画角を元の値へ）。</summary>
        private void ResetLook()
        {
            _look = Vector2.zero;
            _zoom = 0f;
            _dragging = false;
            _lastPinch = -1f;
            if (Cam != null && _baseFov > 0f)
            {
                Cam.fieldOfView = _baseFov;
            }
        }

        private Camera Cam
        {
            get
            {
                if (_camera == null)
                {
                    _camera = GetComponent<Camera>();
                    if (_camera != null && _baseFov < 0f)
                    {
                        _baseFov = _camera.fieldOfView;
                    }
                }
                return _camera;
            }
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
            // 位置は経路の上のまま。向きに見回しを足し、寄り引きは画角だけで行う（すり抜けを起こさない）
            transform.SetPositionAndRotation(point.position, LookAroundRules.Rotate(point.rotation, _look));
            if (Cam != null && _baseFov > 0f)
            {
                Cam.fieldOfView = _baseFov + _zoom;
            }
        }

        /// <summary>下見をやめて、構えの視点にそろえてから通常の制御に返す。</summary>
        private void Stop()
        {
            _state = PreviewState.Idle;
            _manualProgress = 0f;
            ResetLook();

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

            // 下見の間（自動の下見・見回し・戻っている最中）は投げられない。見回しのドラッグで球が投げられないように（段階6）
            if (ballController != null)
            {
                ballController.SetInputBlocked(active && _state != PreviewState.Idle);
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
