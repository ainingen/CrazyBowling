using UnityEngine;
using UnityEngine.InputSystem;

namespace CrazyBowling.Ball
{
    /// <summary>ボールの状態。</summary>
    public enum BallState
    {
        /// <summary>構え中。マウスの左右でボールの位置を調整できる。</summary>
        Aiming,

        /// <summary>ドラッグ中。位置は固定され、方向と強さを決めている。</summary>
        Dragging,

        /// <summary>投球後。物理に任せて転がっている。</summary>
        Rolling,
    }

    /// <summary>
    /// ボールの操作と状態管理。
    /// 構え中とドラッグ中は Rigidbody を Kinematic にして物理を止め、
    /// 投げた瞬間に物理を有効にする。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BallController : MonoBehaviour
    {
        [Header("構え位置")]
        [Tooltip("ボールが構える基準になる Transform。この前方向へ投げる。")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("マウスの動きにボールが追いつく速さ（m/s）。")]
        [SerializeField] private float sideMoveSpeed = 10f;

        [Tooltip("中央から左右に動ける範囲（m）。")]
        [SerializeField] private float sideMoveLimit = 0.4f;

        [Header("投球")]
        [Tooltip("最大の強さになる引き幅（ピクセル）。")]
        [SerializeField] private float maxDragPixels = 300f;

        [Tooltip("これ以下の引き幅では投げない（ピクセル）。")]
        [SerializeField] private float minDragPixels = 20f;

        [Tooltip("最小の初速（m/s）。")]
        [SerializeField] private float minThrowSpeed = 3f;

        [Tooltip("最大の初速（m/s）。")]
        [SerializeField] private float maxThrowSpeed = 10f;

        [Tooltip("左右に振れる最大角度（度）。")]
        [SerializeField] private float maxSideAngle = 20f;

        [Tooltip("引く向きを反転する（奥に払って投げる操作にする）。")]
        [SerializeField] private bool invertDrag = false;

        [Tooltip("左右の向きを反転する。")]
        [SerializeField] private bool invertSideAngle = false;

        [Header("リセット条件")]
        [Tooltip("これより遅くなったら「止まった」とみなす速度（m/s）。")]
        [SerializeField] private float stopVelocityThreshold = 0.2f;

        [Tooltip("止まった状態がこの秒数続いたら構えに戻す。")]
        [SerializeField] private float stopWaitSeconds = 1f;

        [Tooltip("何も起きなくてもこの秒数で構えに戻す（保険）。")]
        [SerializeField] private float autoResetSeconds = 8f;

        [Tooltip("この高さより下に落ちたら構えに戻す（m）。")]
        [SerializeField] private float fallYThreshold = -5f;

        [Header("デバッグ")]
        [Tooltip("投球の内容とリセット理由を Console に出す。")]
        [SerializeField] private bool logEvents = true;

        private Rigidbody _rigidbody;
        private BallState _state;
        private float _sideOffset;
        private Vector2 _dragStart;
        private float _rollingTimer;
        private float _stopTimer;
        private ThrowResult _dragPreview;

        /// <summary>現在の状態。</summary>
        public BallState State => _state;

        /// <summary>転がっている最中か。カメラの追従切り替えに使う。</summary>
        public bool IsRolling => _state == BallState.Rolling;

        /// <summary>ドラッグ中か。表示の出し入れに使う。</summary>
        public bool IsDragging => _state == BallState.Dragging;

        /// <summary>
        /// ドラッグ中に毎フレーム計算している、今離したらどうなるかの予測。表示専用。
        /// 実際の投球はこの値を使わず、離した時点で改めて計算する。
        /// </summary>
        public ThrowResult DragPreview => _dragPreview;

        /// <summary>構え位置。矢印の起点を知るために公開している。</summary>
        public Transform SpawnPoint => spawnPoint;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            ResetToSpawn("初期化");
        }

        private void Update()
        {
            // Input System のマウスが無い環境では何もしない
            if (Mouse.current == null)
            {
                return;
            }

            switch (_state)
            {
                case BallState.Aiming:
                    UpdateAiming();
                    break;
                case BallState.Dragging:
                    UpdateDragging();
                    break;
                case BallState.Rolling:
                    UpdateRolling();
                    break;
            }
        }

        /// <summary>構え中：マウスの左右でボールをスライドさせ、押されたらドラッグに移る。</summary>
        private void UpdateAiming()
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            // 画面の左端から右端を、左右の可動範囲に対応させる
            float screenRatio = Mathf.Clamp01(mousePosition.x / Mathf.Max(Screen.width, 1));
            float targetOffset = Mathf.Lerp(-sideMoveLimit, sideMoveLimit, screenRatio);
            _sideOffset = Mathf.MoveTowards(_sideOffset, targetOffset, sideMoveSpeed * Time.deltaTime);
            ApplySpawnPosition();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                _dragStart = mousePosition;
                _dragPreview = default;
                _state = BallState.Dragging;
            }
        }

        /// <summary>ドラッグ中：位置は固定。離したら投球を計算する。</summary>
        private void UpdateDragging()
        {
            // 表示用の予測を毎フレーム更新する（投球には使わない）
            _dragPreview = ThrowCalculator.Calculate(
                _dragStart, Mouse.current.position.ReadValue(), BuildThrowSettings(), invertDrag, invertSideAngle);

            if (!Mouse.current.leftButton.wasReleasedThisFrame)
            {
                return;
            }

            Vector2 dragEnd = Mouse.current.position.ReadValue();
            ThrowResult result = ThrowCalculator.Calculate(
                _dragStart, dragEnd, BuildThrowSettings(), invertDrag, invertSideAngle);

            if (result.isValid)
            {
                Throw(result);
            }
            else
            {
                // 引き幅が足りなかったので、投げずに構えに戻る
                _state = BallState.Aiming;
            }
        }

        /// <summary>転がり中：止まった、落ちた、時間切れで構えに戻す。</summary>
        private void UpdateRolling()
        {
            _rollingTimer += Time.deltaTime;

            if (transform.position.y < fallYThreshold)
            {
                ResetToSpawn("場外に落ちた");
                return;
            }

            if (_rigidbody.linearVelocity.magnitude < stopVelocityThreshold)
            {
                _stopTimer += Time.deltaTime;
                if (_stopTimer >= stopWaitSeconds)
                {
                    ResetToSpawn("停止した");
                    return;
                }
            }
            else
            {
                _stopTimer = 0f;
            }

            if (_rollingTimer >= autoResetSeconds)
            {
                ResetToSpawn("時間切れ");
            }
        }

        /// <summary>投球する。ここで初めて物理を有効にする。</summary>
        private void Throw(ThrowResult result)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            Vector3 direction = CalculateThrowDirection(result.sideAngle);

            // 初速（m/s）をそのまま速度差として与える
            _rigidbody.AddForce(direction * result.speed, ForceMode.VelocityChange);

            _state = BallState.Rolling;
            _rollingTimer = 0f;
            _stopTimer = 0f;

            if (logEvents)
            {
                Debug.Log($"投球：初速 {result.speed:F2} m/s ／ 角度 {result.sideAngle:F1} 度", this);
            }
        }

        /// <summary>
        /// 左右の角度から、実際に投げる方向を求める。
        /// 表示の矢印が実際の進路とズレないよう、投球と同じこのメソッドを使う。
        /// </summary>
        public Vector3 CalculateThrowDirection(float sideAngle)
        {
            // 構え位置の前方向を基準に、左右へ角度を振る
            Vector3 forward = spawnPoint != null ? spawnPoint.forward : Vector3.forward;
            Vector3 direction = Quaternion.AngleAxis(sideAngle, Vector3.up) * forward;
            direction.y = 0f;
            return direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : Vector3.forward;
        }

        /// <summary>構え位置に戻す。速度を0にしてから Kinematic に戻す。</summary>
        private void ResetToSpawn(string reason)
        {
            // Kinematic のまま速度を書くと Unity が警告を出すので、順序を守る
            if (!_rigidbody.isKinematic)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
            _rigidbody.isKinematic = true;

            _sideOffset = 0f;
            if (spawnPoint != null)
            {
                transform.rotation = spawnPoint.rotation;
            }
            ApplySpawnPosition();

            _state = BallState.Aiming;
            _rollingTimer = 0f;
            _stopTimer = 0f;

            if (logEvents)
            {
                Debug.Log($"構えに戻す：{reason}", this);
            }
        }

        /// <summary>構え位置と左右のずれを実際の座標に反映する。</summary>
        private void ApplySpawnPosition()
        {
            if (spawnPoint == null)
            {
                return;
            }
            transform.position = spawnPoint.position + spawnPoint.right * _sideOffset;
        }

        /// <summary>Inspector の値を計算用の設定にまとめる。</summary>
        private ThrowSettings BuildThrowSettings()
        {
            return new ThrowSettings
            {
                maxDragPixels = maxDragPixels,
                minDragPixels = minDragPixels,
                minThrowSpeed = minThrowSpeed,
                maxThrowSpeed = maxThrowSpeed,
                maxSideAngle = maxSideAngle,
            };
        }
    }
}
