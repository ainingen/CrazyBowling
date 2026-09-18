using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CrazyBowling.Ball
{
    /// <summary>ボールの状態。</summary>
    public enum BallState
    {
        /// <summary>構え中。マウスの左右でボールの位置を調整できる。</summary>
        Aiming,

        /// <summary>引いている最中。立ち位置は固定され、角度と強さを決めている。</summary>
        Pulling,

        /// <summary>投球後。物理に任せて転がっている。</summary>
        Rolling,

        /// <summary>転がり終わって決着した。判定が済むまで、この場で待つ。</summary>
        Settled,
    }

    /// <summary>
    /// ボールの操作と状態管理。
    /// 構え中とドラッグ中は Rigidbody を Kinematic にして物理を止め、
    /// 投げた瞬間に物理を有効にする。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BallController : MonoBehaviour
    {
        /// <summary>
        /// その場所の床の高さを教える役。起伏のあるレーンで、構え中のボールを床に乗せるのに使う。
        /// 床の形を知らないレーンでは false を返す。
        /// </summary>
        public delegate bool FloorSampler(Vector3 worldPosition, out float height);

        [Header("構え位置")]
        [Tooltip("ボールが構える基準になる Transform。この前方向へ投げる。")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("マウスの動きにボールが追いつく速さ（m/s）。")]
        [SerializeField] private float sideMoveSpeed = 10f;

        [Tooltip("中央から左右に動ける範囲（m）。")]
        [SerializeField] private float sideMoveLimit = 0.4f;

        [Header("投球")]
        [Tooltip("最大の強さになる引き幅（ピクセル）。")]
        [SerializeField] private float maxPullPixels = 300f;

        [Tooltip("これ以下の引き幅では投げない（ピクセル）。")]
        [SerializeField] private float minPullPixels = 20f;

        [Tooltip("最大の角度になる横ずれ（ピクセル）。")]
        [SerializeField] private float maxAnglePixels = 250f;

        [Tooltip("左右に振れる最大角度（度）。レーンごとに上書きできる。" +
                 "15.7m先では1度で約28cm動く。レーンは中心から±0.53mなので、" +
                 "2度で中央からレーンの端にちょうど届く。")]
        [SerializeField] private float maxAngleDegrees = 2f;

        [Tooltip("この横ずれまでは角度を付けない（ピクセル）。手ブレ対策。")]
        [SerializeField] private float angleDeadZonePixels = 10f;

        [Tooltip("最小の初速（m/s）。")]
        [SerializeField] private float minThrowSpeed = 3f;

        [Tooltip("最大の初速（m/s）。")]
        [SerializeField] private float maxThrowSpeed = 10f;

        [Tooltip("引く向きを反転する（奥に払って投げる操作にする）。")]
        [SerializeField] private bool invertDrag = false;

        [Header("カーブ")]
        [Tooltip("カーブ最大のときに与える縦軸まわりの回転（rad/s）。")]
        [SerializeField] private float maxSideSpin = 30f;

        [Tooltip("横回転1rad/s あたりに加える横向きの加速度（m/s^2）。曲がりの強さの主な調整項目。")]
        [SerializeField] private float curveForce = 0.11f;

        [Tooltip("横回転が失われる速さ（1/秒）。大きいほど早く曲がりが止まる。")]
        [SerializeField] private float sideSpinDecay = 1.5f;

        [Tooltip("接地点の滑りがこれ以下なら「転がっている」とみなす（m/s）。転がったら曲がりは止まる。")]
        [SerializeField] private float slipThreshold = 0.15f;

        [Header("転がり")]
        [Tooltip("回転の速さの上限（rad/s）。既定の50では、半径0.11mのボールは5.5m/sまでしか転がれず、" +
                 "それより速い球は滑り続けて摩擦で減速してしまう。")]
        [SerializeField] private float maxAngularVelocity = 200f;

        [Tooltip("放す瞬間に与える前回転の量。1で「滑らずに転がる」状態、0で無回転。" +
                 "大きいほど速度を保ったままピンに届き、ピンがよく飛ぶ。")]
        [Range(0f, 1f)]
        [SerializeField] private float releaseSpinRatio = 0.7f;

        [Header("決着の条件")]
        [Tooltip("これより遅くなったら「止まった」とみなす速度（m/s）。")]
        [SerializeField] private float stopVelocityThreshold = 0.2f;

        [Tooltip("止まった状態がこの秒数続いたら決着とみなす。構えには戻らない。")]
        [SerializeField] private float stopWaitSeconds = 1f;

        [Tooltip("転がり続けても、この秒数で強制的に決着とみなす（保険）。判定が止まらないようにするため。")]
        [SerializeField] private float autoResetSeconds = 8f;

        [Tooltip("この高さより下に落ちたら決着とみなす（m）。")]
        [SerializeField] private float fallYThreshold = -5f;

        [Header("デバッグ")]
        [Tooltip("投球の内容と決着の理由を Console に出す。")]
        [SerializeField] private bool logEvents = true;

        private Rigidbody _rigidbody;
        private BallState _state;
        private float _sideOffset;
        private Vector2 _dragStart;
        private float _rollingTimer;
        private float _stopTimer;
        private ThrowResult _dragPreview;

        /// <summary>構え中のスライダーで決めたカーブ。投球後も次の投球まで残る。</summary>
        private float _selectedCurve;

        /// <summary>今かかっている横回転（rad/s）。正で右へ曲がる。</summary>
        private float _sideSpin;

        /// <summary>床の高さを調べる手段。起伏のあるレーンでだけ入る。</summary>
        private FloorSampler _floorSampler;

        /// <summary>
        /// 転がりに変わって、曲がりが終わったか。
        /// 一度終わったら、そのあと滑りが増えても曲げ直さない。
        /// </summary>
        private bool _curveFinished;

        /// <summary>現在の状態。</summary>
        public BallState State => _state;

        /// <summary>転がっている最中か。カメラの追従切り替えに使う。</summary>
        public bool IsRolling => _state == BallState.Rolling;

        /// <summary>引いている最中か。表示の出し入れに使う。</summary>
        public bool IsPulling => _state == BallState.Pulling;

        /// <summary>構え中か。カーブのスライダーを触れるのはこの間だけ。</summary>
        public bool IsAiming => _state == BallState.Aiming;

        /// <summary>今選んでいるカーブ。-1で左いっぱい、0でカーブ無し、+1で右いっぱい。</summary>
        public float SelectedCurve => _selectedCurve;

        /// <summary>カーブのスライダーから設定する。構え中だけ効く。</summary>
        public void SetSelectedCurve(float curve)
        {
            if (_state != BallState.Aiming)
            {
                return;
            }
            _selectedCurve = Mathf.Clamp(curve, -1f, 1f);
        }

        /// <summary>
        /// レーンごとの上書き。斜めや回転するレーンでは、流れを打ち消すために広い角度が要る。
        /// 段階3で LaneData から呼ぶ。
        /// </summary>
        public void ApplyLaneSettings(float laneMaxAngleDegrees)
        {
            maxAngleDegrees = laneMaxAngleDegrees;
        }

        /// <summary>転がり終わって決着したか。判定はここから始まる。</summary>
        public bool IsSettled => _state == BallState.Settled;

        /// <summary>投球中か（転がり中または決着待ち）。カメラの追従に使う。</summary>
        public bool IsInPlay => _state == BallState.Rolling || _state == BallState.Settled;

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
            _rigidbody.maxAngularVelocity = maxAngularVelocity;
            ResetToSpawn("初期化");
        }

        private void Update()
        {
            // マウス・タッチ・ペンのどれも無い環境では何もしない
            if (Pointer.current == null)
            {
                return;
            }

            switch (_state)
            {
                case BallState.Aiming:
                    UpdateAiming();
                    break;
                case BallState.Pulling:
                    UpdatePulling();
                    break;
                case BallState.Rolling:
                    UpdateRolling();
                    break;
            }
        }

        /// <summary>
        /// 構え中：ポインタの左右でボールをスライドさせ、押されたら引きに移る。
        /// マウスはホバーで追従し、タッチは指を置いた位置で決まる。
        /// </summary>
        private void UpdateAiming()
        {
            // カーブのスライダーなど、UIの上にポインタがあるときは投球操作をしない。
            // これをしないと、スライダーを触りに行くだけでボールが動いてしまう
            if (IsPointerOverUI())
            {
                return;
            }

            Vector2 pointerPosition = Pointer.current.position.ReadValue();

            // 画面の左端から右端を、左右の可動範囲に対応させる
            float screenRatio = Mathf.Clamp01(pointerPosition.x / Mathf.Max(Screen.width, 1));
            float targetOffset = Mathf.Lerp(-sideMoveLimit, sideMoveLimit, screenRatio);

            // タッチは指を置いた瞬間に決まるので、ホバーで追う余地が無い。
            // 押された時点では補間せず、その位置へ合わせる
            bool pressed = Pointer.current.press.wasPressedThisFrame;
            _sideOffset = pressed
                ? targetOffset
                : Mathf.MoveTowards(_sideOffset, targetOffset, sideMoveSpeed * Time.deltaTime);
            ApplySpawnPosition();

            if (pressed)
            {
                _dragStart = pointerPosition;
                _dragPreview = default;
                _state = BallState.Pulling;
            }
        }

        /// <summary>引いている最中：立ち位置は固定。離したら投球を計算する。</summary>
        private void UpdatePulling()
        {
            Vector2 pointerPosition = Pointer.current.position.ReadValue();

            // 表示用の予測を毎フレーム更新する（投球には使わない）
            _dragPreview = ThrowCalculator.Calculate(
                _dragStart, pointerPosition, BuildThrowSettings(), _selectedCurve, invertDrag);

            if (!Pointer.current.press.wasReleasedThisFrame)
            {
                return;
            }

            ThrowResult result = ThrowCalculator.Calculate(
                _dragStart, pointerPosition, BuildThrowSettings(), _selectedCurve, invertDrag);

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

        /// <summary>
        /// ポインタがUIの上にあるか。EventSystem が無いシーンでは常に false を返す。
        /// </summary>
        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>転がり中：止まった、落ちた、時間切れで決着とみなす。構えには戻さない。</summary>
        private void UpdateRolling()
        {
            _rollingTimer += Time.deltaTime;

            if (transform.position.y < fallYThreshold)
            {
                MarkSettled("場外に落ちた");
                return;
            }

            if (_rigidbody.linearVelocity.magnitude < stopVelocityThreshold)
            {
                _stopTimer += Time.deltaTime;
                if (_stopTimer >= stopWaitSeconds)
                {
                    MarkSettled("停止した");
                    return;
                }
            }
            else
            {
                _stopTimer = 0f;
            }

            if (_rollingTimer >= autoResetSeconds)
            {
                MarkSettled("時間切れ");
            }
        }

        /// <summary>投球する。ここで初めて物理を有効にする。</summary>
        private void Throw(ThrowResult result)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            // 再生中に Inspector で変えた値も、次の投球から効くようにする
            _rigidbody.maxAngularVelocity = maxAngularVelocity;

            // 引きの横成分で決まった角度を、構え位置の正面から左右に振る
            Vector3 direction = CalculateThrowDirection(result.sideAngle);

            // 初速（m/s）をそのまま速度差として与える
            _rigidbody.AddForce(direction * result.speed, ForceMode.VelocityChange);

            ApplyReleaseSpin(direction, result.speed);
            ApplySideSpin(result.curve);

            _state = BallState.Rolling;
            _rollingTimer = 0f;
            _stopTimer = 0f;

            if (logEvents)
            {
                Debug.Log($"投球：初速 {result.speed:F2} m/s ／ 角度 {result.sideAngle:F1} 度 ／ カーブ {result.curve:F2}", this);
            }
        }

        /// <summary>
        /// 縦軸まわりの回転を与える。見た目の回転と、カーブの計算に使う値の両方を用意する。
        /// 右へ曲がるボールは、上から見て時計回りに回る。
        /// </summary>
        private void ApplySideSpin(float curve)
        {
            _sideSpin = curve * maxSideSpin;
            _curveFinished = false;
            _rigidbody.angularVelocity += Vector3.up * (-_sideSpin);
        }

        /// <summary>
        /// 転がり中のカーブ。滑っている間だけ横向きの力を加え、横回転は徐々に失う。
        /// Unity の物理は縦軸の回転でボールを曲げてくれないので、ここで補う。
        /// </summary>
        private void FixedUpdate()
        {
            if (_state != BallState.Rolling || _rigidbody == null || _rigidbody.isKinematic)
            {
                return;
            }

            if (Mathf.Approximately(_sideSpin, 0f))
            {
                return;
            }

            // 投げた直後の1回目は、まだ速度が反映されていない（AddForce は次のステップで効く）。
            // ここで判定すると「速度0＝転がっている」と誤解して、曲がる前に終わってしまう
            Vector3 horizontalVelocity = _rigidbody.linearVelocity;
            horizontalVelocity.y = 0f;
            if (horizontalVelocity.sqrMagnitude < 0.01f)
            {
                return;
            }

            BallCurveSettings settings = BuildCurveSettings();

            // 進む向きの滑りだけを見る。横向きの速度が増えたぶんを数えると、
            // 曲がるほど滑りが増えて永久に曲がり続けてしまう
            float forwardSlip = BallCurveModel.CalculateForwardSlip(
                _rigidbody.linearVelocity, _rigidbody.angularVelocity, settings.radius);

            if (!_curveFinished && !BallCurveModel.IsSliding(forwardSlip, settings))
            {
                _curveFinished = true;
            }

            // 転がりに変わったら、そこで曲がりを止める
            if (!_curveFinished)
            {
                Vector3 acceleration = BallCurveModel.CalculateLateralAcceleration(
                    _rigidbody.linearVelocity, _sideSpin, settings);
                _rigidbody.AddForce(acceleration, ForceMode.Acceleration);
            }

            // 横回転を減らす。見た目の回転も同じ割合で落とす
            float decayed = BallCurveModel.DecaySideSpin(_sideSpin, Time.fixedDeltaTime, settings);
            float removed = _sideSpin - decayed;
            _rigidbody.angularVelocity += Vector3.up * removed;
            _sideSpin = decayed;
        }

        /// <summary>Inspector の値をカーブの計算用にまとめる。</summary>
        public BallCurveSettings BuildCurveSettings()
        {
            return new BallCurveSettings
            {
                radius = GetBallRadius(),
                maxSideSpin = maxSideSpin,
                curveForce = curveForce,
                spinDecay = sideSpinDecay,
                slipThreshold = slipThreshold,
            };
        }

        /// <summary>
        /// 放す瞬間の前回転を与える。
        /// 無回転で放すと、床の摩擦で転がり始めるまでに速度の約3割を失う。
        /// あらかじめ「滑らずに転がる」角速度の何割かを与えて、その損失を減らす。
        /// </summary>
        private void ApplyReleaseSpin(Vector3 direction, float speed)
        {
            if (releaseSpinRatio <= 0f)
            {
                return;
            }

            float radius = GetBallRadius();
            if (radius <= Mathf.Epsilon)
            {
                return;
            }

            // 進行方向に対して右向きの軸で回すと、前に転がる向きになる
            Vector3 spinAxis = Vector3.Cross(Vector3.up, direction);
            _rigidbody.angularVelocity = spinAxis * (speed / radius) * releaseSpinRatio;
        }

        /// <summary>
        /// 当たり判定から見たボールの半径（m）。
        /// 見た目の子（Visual）ではなく、物理の親の Collider を使う。
        /// </summary>
        private float GetBallRadius()
        {
            SphereCollider sphere = GetComponent<SphereCollider>();
            if (sphere == null)
            {
                return 0f;
            }

            Vector3 scale = transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return sphere.radius * maxScale;
        }

        /// <summary>
        /// 当たり判定から見たボールの半径（m）。
        /// 起伏のあるレーンで、床の上に置くときの高さに使う。
        /// </summary>
        public float BallRadius => GetBallRadius();

        /// <summary>構え位置の正面。角度0のときの投球方向。</summary>
        public Vector3 GetForwardDirection()
        {
            Vector3 forward = spawnPoint != null ? spawnPoint.forward : Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > Mathf.Epsilon ? forward.normalized : Vector3.forward;
        }

        /// <summary>
        /// 左右の角度から、実際に投げる方向を求める。
        /// 予測線が実際の進路とズレないよう、投球と同じこのメソッドを使う。
        /// </summary>
        public Vector3 CalculateThrowDirection(float sideAngle)
        {
            Vector3 direction = Quaternion.AngleAxis(sideAngle, Vector3.up) * GetForwardDirection();
            direction.y = 0f;
            return direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : Vector3.forward;
        }

        /// <summary>今かかっている横回転（rad/s）。表示が読む。</summary>
        public float CurrentSideSpin => _sideSpin;

        /// <summary>
        /// 放した直後の接地点の滑り（m/s）。予測線が、どこまで曲がるかを見積もるのに使う。
        /// 前回転を与えたぶんだけ滑りは小さくなる。
        /// </summary>
        public float EstimateInitialSlip(float speed)
        {
            return speed * (1f - releaseSpinRatio);
        }

        /// <summary>
        /// 転がり終わったことにする。構えには戻さず、その場で止まって判定を待つ。
        /// ピットに入ったときは BallPit から呼ばれる。
        /// </summary>
        public void MarkSettled(string reason)
        {
            if (_state != BallState.Rolling)
            {
                return;
            }

            _state = BallState.Settled;

            if (logEvents)
            {
                Debug.Log($"決着：{reason}", this);
            }
        }

        /// <summary>構え位置に戻して、次の投球を受け付ける。進行役（ThrowSequencer）が呼ぶ。</summary>
        public void ReturnToSpawn()
        {
            ResetToSpawn("次の投球へ");
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
            _sideSpin = 0f;
            _curveFinished = false;

            if (logEvents)
            {
                Debug.Log($"構えに戻す：{reason}", this);
            }
        }

        /// <summary>
        /// 床の高さを調べる手段を渡す。GameManager がレーンを差し替えるたびに呼ぶ。
        /// null を渡すと、構え位置の高さをそのまま使う。
        /// </summary>
        public void SetFloorSampler(FloorSampler sampler)
        {
            _floorSampler = sampler;
            ApplySpawnPosition();
        }

        /// <summary>構え位置と左右のずれを実際の座標に反映する。</summary>
        private void ApplySpawnPosition()
        {
            if (spawnPoint == null)
            {
                return;
            }

            Vector3 position = spawnPoint.position + spawnPoint.right * _sideOffset;

            // 起伏のあるレーンでは、左右にずれた先の床の高さに合わせる。
            // 構え位置の高さのままだと、端へ寄るほど床に埋まったり浮いたりする
            if (_floorSampler != null && _floorSampler(position, out float floorY))
            {
                position.y = floorY + GetBallRadius();
            }

            transform.position = position;
        }

        /// <summary>Inspector の値を計算用の設定にまとめる。</summary>
        private ThrowSettings BuildThrowSettings()
        {
            return new ThrowSettings
            {
                maxPullPixels = maxPullPixels,
                minPullPixels = minPullPixels,
                maxAnglePixels = maxAnglePixels,
                maxAngleDegrees = maxAngleDegrees,
                angleDeadZonePixels = angleDeadZonePixels,
                minThrowSpeed = minThrowSpeed,
                maxThrowSpeed = maxThrowSpeed,
            };
        }
    }
}
