using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 10本目の宙返りのレール。輪の区間だけ、ボールを当たり判定ではなくスクリプトで曲線に乗せて運ぶ。
    ///
    /// ── なぜ当たり判定で輪を作らないか ─────────────────────
    ///
    /// 輪の内側は板を並べるか非凸の形でしか作れず、継ぎ目の角を拾う（6本目・7本目の教訓）。
    /// しかも頂上は押し付けがいちばん弱く、浮いたところで角に当たる。
    /// 床と輪の突き合わせは「つなぎ目で跳ねる」（9本目段階3）。
    /// **曲線に乗せて運べば、角の問題は原理的に起きない。**
    ///
    /// ── 1回の輪の流れ ───────────────────────────────
    ///
    ///   床の上 → 乗せる（床の終わりより手前）→ まっすぐ → 円（上へ回る）→ まっすぐ
    ///   → 降ろす（次の床の始まりより先）→ 床の上
    ///
    /// 輪の下には床が無い。頂上で速さが足りなければ手を離し、ボールは奈落へ落ちる
    /// （y＜−5 で「場外に落ちた」になり0本。今までの仕組みのまま）。
    ///
    /// ── 速さの計算 ─────────────────────────────────
    ///
    /// 転がる球なので、高さ h を登ると 速さ² が (10/7)·g·h 減る（回転の分も持ち上げるため）。
    /// 抵抗は1mあたり lossPerMeter（ボールの抵抗0.05と同じ。床の上と同じ減り方にする）。
    /// 頂上付近で「押し付けの力 = v²/ρ + g·cosφ」が0を下回ったら落とす。
    ///
    /// ── 立ち位置・角度・カーブ ─────────────────────────
    ///
    /// 乗せた瞬間の「軌道の中心からの横のずれ」「横向きの速さ」「縦軸の回転（カーブ）」を覚えておく。
    /// 軌道の上では横のずれを保ったまま走らせ、降ろすときに横向きの速さと縦軸の回転を戻す。
    /// だから狙いは輪を越えても生きる。
    ///
    /// ── 輪と床の揺れ（段階2） ───────────────────────────
    ///
    /// 輪と中間の床は、それぞれ別の周期で左右に往復する（割り切れない周期・位相は乱数）。
    /// 強さが足りても、次の区間とずれていれば奈落へ落ちる。力とタイミングの両方が要る。
    ///   ・輪は軌道ごと横へずらす。乗っているボールはそのずれに乗って運ばれる
    ///   ・動く床は kinematic の箱を MovePosition で動かす（親の Transform は動かさない。4本目の教訓）
    ///   ・降ろすときは、降りる先に対して滑らない横の速さを渡す
    ///     （動く床なら床の速さ、止まった床なら輪の揺れの速さを足す）
    ///
    /// 座標はすべてこの部品を置いたオブジェクト（レーンの根元）の基準で扱う。
    /// </summary>
    public class CoasterRail : MonoBehaviour
    {
        /// <summary>輪1つぶんの形。</summary>
        [System.Serializable]
        public class Loop
        {
            [Tooltip("輪のいちばん下（入口）の奥行き（レーン基準・m）。")]
            public float centerZ = 5f;

            [Tooltip("ボールの中心が通る円の半径（m）。大きいほど回るのに速さが要る。")]
            public float radius = 0.85f;

            [Tooltip("輪を1周するあいだに横へずれる量（m）。入口と出口が重ならないようにする。")]
            public float shiftX = 1.1f;

            [Tooltip("輪ごと左右へ揺れる振れ幅（m）。0で動かない。")]
            public float swayAmplitude = 0.18f;

            [Tooltip("左右の揺れの周期（秒）。ほかの輪や床と割り切れない値にする。")]
            public float swayPeriod = 3.1f;

            [Tooltip("確認用の線など、輪と一緒に左右へ動かす見た目。当たり判定を付けないこと。")]
            public Transform visual;
        }

        /// <summary>左右に動く床ひとつぶん。</summary>
        [System.Serializable]
        public class MovingFloor
        {
            [Tooltip("床の kinematic の Rigidbody。MovePosition で動かす。")]
            public Rigidbody body;

            [Tooltip("左右の振れ幅（m）。")]
            public float swayAmplitude = 0.18f;

            [Tooltip("左右の揺れの周期（秒）。輪と割り切れない値にする。")]
            public float swayPeriod = 4.3f;

            [Tooltip("床と一緒に動かす見た目の飾り（磁石など）。当たり判定は付けないこと。空でもよい。")]
            public Transform visual;
        }

        [Header("輪")]
        [Tooltip("手前から順に並べる。")]
        [SerializeField] private Loop[] loops =
        {
            new Loop { centerZ = 5f, radius = 0.85f, shiftX = 1.1f, swayPeriod = 3.1f },
            new Loop { centerZ = 10.5f, radius = 1.0f, shiftX = -1.1f, swayPeriod = 5.7f },
        };

        [Header("動く床")]
        [Tooltip("左右に動く床（10本目は輪の間の床B）。床A とピン台の床C は入れない。")]
        [SerializeField] private MovingFloor[] movingFloors;

        [Header("床とのつなぎ")]
        [Tooltip("床の上面の高さ（レーン基準・m）。")]
        [SerializeField] private float floorTop = 0.05f;

        [Tooltip("ボールの半径（m）。ボールの中心は床の上面からこの高さを走る。")]
        [SerializeField] private float ballRadius = 0.11f;

        [Tooltip("床と軌道の帯の半分の幅（m）。これより外にいた球は乗れずに落ちる。")]
        [SerializeField] private float courseHalfWidth = 0.525f;

        [Tooltip("輪の足もとから床の切れ目までの余白（m）。輪から落ちた球が床に乗らないようにする。")]
        [SerializeField] private float gapMargin = 0.35f;

        [Tooltip("乗せる点は床の終わりのこれだけ手前、降ろす点は次の床の始まりのこれだけ先（m）。" +
                 "床の端の角をボールに踏ませないため。")]
        [SerializeField] private float handoffDistance = 0.3f;

        [Tooltip("乗せる条件：ボールの中心の高さのずれ（m）。跳ねている球は乗せない。")]
        [SerializeField] private float captureHeightTolerance = 0.05f;

        [Header("速さ")]
        [Tooltip("軌道の上の抵抗。1m進むごとに減る速さ（m/s）。ボールの抵抗0.05と同じにして、床の上と揃える。")]
        [SerializeField] private float lossPerMeter = 0.05f;

        [Tooltip("高さ h を登ると 速さ² が この値×g×h 減る。転がる球なら 10/7。")]
        [SerializeField] private float rollingEnergyFactor = 10f / 7f;

        [Header("確認用")]
        [SerializeField] private bool logEvents = true;

        /// <summary>いま乗っている輪（乗っていなければ −1）。</summary>
        private int _loop = -1;

        /// <summary>軌道の上の道のり（m）と速さ（m/s）。</summary>
        private float _s;
        private float _v;

        /// <summary>乗せた瞬間に覚えたもの：横のずれ、横向きの速さ、縦軸の回転。</summary>
        private float _lateral;
        private float _lateralVelocity;
        private float _spinY;

        /// <summary>乗せる点を越えたかを見るための、前のステップの奥行き。</summary>
        private float _previousZ = float.NegativeInfinity;

        /// <summary>乗せた瞬間の速さ（確認用）。</summary>
        private float _captureSpeed;

        /// <summary>揺れの時刻（秒）。乗っていなくても毎ステップ進む。</summary>
        private float _time;

        /// <summary>揺れの位相（ラジアン）。レーンに入るたびに乱数で決める。</summary>
        private float[] _loopPhase;
        private float[] _floorPhase;

        /// <summary>動く床と見た目の、揺れていないときの位置（レーン基準）。</summary>
        private Vector3[] _floorBase;
        private Vector3[] _visualBase;
        private Vector3[] _floorVisualBase;

        /// <summary>この投球で落ちた輪（落ちていなければ −1）。確認用。</summary>
        public int FailedLoop { get; private set; } = -1;

        /// <summary>この投球で回り切った輪の数。確認用。</summary>
        public int PassedLoops { get; private set; }

        /// <summary>いまレールに乗っているか。</summary>
        public bool IsRiding => _loop >= 0;

        /// <summary>輪の数。</summary>
        public int LoopCount => loops == null ? 0 : loops.Length;

        private float BallCenterY => floorTop + ballRadius;

        // ================= 揺れ =================

        private void Awake()
        {
            int nf = movingFloors == null ? 0 : movingFloors.Length;
            _floorBase = new Vector3[nf];
            _floorVisualBase = new Vector3[nf];
            for (int i = 0; i < nf; i++)
            {
                if (movingFloors[i].body != null)
                {
                    _floorBase[i] = transform.InverseTransformPoint(movingFloors[i].body.position);
                }
                if (movingFloors[i].visual != null)
                {
                    _floorVisualBase[i] = movingFloors[i].visual.localPosition;
                }
            }

            _visualBase = new Vector3[loops.Length];
            for (int k = 0; k < loops.Length; k++)
            {
                if (loops[k].visual != null)
                {
                    _visualBase[k] = loops[k].visual.localPosition;
                }
            }
        }

        /// <summary>揺れの位相を乱数で決め直す。レーンに入るたびに呼ぶ（投げるたびには呼ばない）。</summary>
        public void RandomizePhases()
        {
            _loopPhase = new float[loops.Length];
            for (int k = 0; k < loops.Length; k++)
            {
                _loopPhase[k] = Random.Range(0f, 2f * Mathf.PI);
            }

            int nf = movingFloors == null ? 0 : movingFloors.Length;
            _floorPhase = new float[nf];
            for (int i = 0; i < nf; i++)
            {
                _floorPhase[i] = Random.Range(0f, 2f * Mathf.PI);
            }

            _time = 0f;
        }

        private float LoopPhase(int k) => _loopPhase != null && k < _loopPhase.Length ? _loopPhase[k] : 0f;
        private float FloorPhase(int i) => _floorPhase != null && i < _floorPhase.Length ? _floorPhase[i] : 0f;

        private static float Wave(float amplitude, float period, float phase, float time)
            => period <= 0f ? 0f : amplitude * Mathf.Sin(2f * Mathf.PI * time / period + phase);

        private static float WaveVelocity(float amplitude, float period, float phase, float time)
            => period <= 0f ? 0f : amplitude * 2f * Mathf.PI / period * Mathf.Cos(2f * Mathf.PI * time / period + phase);

        /// <summary>k番目の輪の、いまの横のずれ（m）。</summary>
        public float LoopSway(int k)
        {
            float phase = _loopPhase != null && k < _loopPhase.Length ? _loopPhase[k] : 0f;
            return Wave(loops[k].swayAmplitude, loops[k].swayPeriod, phase, _time);
        }

        /// <summary>k番目の輪の、いまの横の速さ（m/s）。</summary>
        public float LoopSwayVelocity(int k)
        {
            float phase = _loopPhase != null && k < _loopPhase.Length ? _loopPhase[k] : 0f;
            return WaveVelocity(loops[k].swayAmplitude, loops[k].swayPeriod, phase, _time);
        }

        private float FloorSway(int i)
        {
            float phase = _floorPhase != null && i < _floorPhase.Length ? _floorPhase[i] : 0f;
            return Wave(movingFloors[i].swayAmplitude, movingFloors[i].swayPeriod, phase, _time);
        }

        private float FloorSwayVelocity(int i)
        {
            float phase = _floorPhase != null && i < _floorPhase.Length ? _floorPhase[i] : 0f;
            return WaveVelocity(movingFloors[i].swayAmplitude, movingFloors[i].swayPeriod, phase, _time);
        }

        /// <summary>
        /// 奥行き z の地点にある動く床の横の速さ。動く床が無ければ false。
        /// 床の奥行きの範囲は、床の見た目の長さ（Scale の z）から読む。
        /// </summary>
        private bool TryGetFloorVelocityAt(float z, out float velocity)
        {
            velocity = 0f;
            if (movingFloors == null)
            {
                return false;
            }

            for (int i = 0; i < movingFloors.Length; i++)
            {
                Rigidbody body = movingFloors[i].body;
                if (body == null)
                {
                    continue;
                }

                float half = body.transform.localScale.z * 0.5f;
                if (z >= _floorBase[i].z - half && z <= _floorBase[i].z + half)
                {
                    velocity = FloorSwayVelocity(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>時刻を進め、動く床（当たり判定のある体）を動かす。見た目は LateUpdate で動かす。</summary>
        private void AdvanceSway(float deltaTime)
        {
            _time += deltaTime;

            if (movingFloors != null)
            {
                for (int i = 0; i < movingFloors.Length; i++)
                {
                    Rigidbody body = movingFloors[i].body;
                    if (body == null)
                    {
                        continue;
                    }

                    // ★親の Transform ではなく、体そのものを MovePosition で動かす
                    Vector3 target = _floorBase[i] + Vector3.right * FloorSway(i);
                    body.MovePosition(transform.TransformPoint(target));
                }
            }
        }

        /// <summary>
        /// 輪と動く床の見た目を、描画のたびに動かす。
        /// 物理の刻み（50回／秒）で動かすと、補間して滑らかに動くボールや床に対して
        /// 見た目だけがカクついてずれて見える。Rigidbody の補間と同じく
        /// 「ひとつ前のステップから今のステップまでの途中」の時刻で揺れを計算する。
        /// 物理には一切触らない（当たり判定のある床の体は AdvanceSway が動かす）。
        /// </summary>
        private void LateUpdate()
        {
            float step = Time.fixedDeltaTime;
            float alpha = step > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / step) : 1f;
            float drawTime = _time - step * (1f - alpha);

            for (int k = 0; k < loops.Length; k++)
            {
                if (loops[k].visual != null)
                {
                    float sway = Wave(loops[k].swayAmplitude, loops[k].swayPeriod, LoopPhase(k), drawTime);
                    loops[k].visual.localPosition = _visualBase[k] + Vector3.right * sway;
                }
            }

            if (movingFloors == null)
            {
                return;
            }

            for (int i = 0; i < movingFloors.Length; i++)
            {
                if (movingFloors[i].visual != null)
                {
                    float sway = Wave(movingFloors[i].swayAmplitude, movingFloors[i].swayPeriod, FloorPhase(i), drawTime);
                    movingFloors[i].visual.localPosition = _floorVisualBase[i] + Vector3.right * sway;
                }
            }
        }

        // ================= 形 =================

        /// <summary>k番目の輪の手前を通る床の中心の横位置。輪のずれを手前から足していく。</summary>
        public float CourseXBefore(int k)
        {
            float x = 0f;
            for (int i = 0; i < k && i < loops.Length; i++)
            {
                x += loops[i].shiftX;
            }
            return x;
        }

        /// <summary>k番目の輪の手前の床が終わる奥行き。</summary>
        public float GapStartZ(int k) => loops[k].centerZ - loops[k].radius - gapMargin;

        /// <summary>k番目の輪の後の床が始まる奥行き。</summary>
        public float GapEndZ(int k) => loops[k].centerZ + loops[k].radius + gapMargin;

        /// <summary>乗せる点・降ろす点の奥行き。</summary>
        public float CaptureZ(int k) => GapStartZ(k) - handoffDistance;
        public float ReleaseZ(int k) => GapEndZ(k) + handoffDistance;

        /// <summary>軌道の区間の長さ：手前のまっすぐ・円・後のまっすぐ。</summary>
        private void Lengths(int k, out float lead, out float circle, out float tail)
        {
            lead = loops[k].centerZ - CaptureZ(k);
            circle = 2f * Mathf.PI * loops[k].radius;
            tail = ReleaseZ(k) - loops[k].centerZ;
        }

        /// <summary>軌道の区間の全長（m）。</summary>
        public float TotalLength(int k)
        {
            Lengths(k, out float lead, out float circle, out float tail);
            return lead + circle + tail;
        }

        /// <summary>
        /// 道のり s の点（ボールの中心・レーン基準）と、進む向き、
        /// ボールを支える面の法線（ボールの中心側）、円の上ならその角度を返す。
        /// 横のずれ lateral は、軌道の中心線からのずれ。
        /// </summary>
        public void Evaluate(int k, float s, float lateral,
                             out Vector3 position, out Vector3 tangent, out Vector3 normal, out float phi, out bool onCircle)
        {
            Loop loop = loops[k];
            Lengths(k, out float lead, out float circle, out float tail);
            float baseX = CourseXBefore(k) + LoopSway(k) + lateral;
            float y0 = BallCenterY;
            onCircle = false;
            phi = 0f;

            if (s <= lead)
            {
                position = new Vector3(baseX, y0, CaptureZ(k) + s);
                tangent = Vector3.forward;
                normal = Vector3.up;
                return;
            }

            if (s <= lead + circle)
            {
                onCircle = true;
                phi = (s - lead) / loop.radius;
                float twoPi = 2f * Mathf.PI;
                // 横のずれは、入口と出口で横向きの速さが0になる形で入れる
                float x = baseX + loop.shiftX * (phi - Mathf.Sin(phi)) / twoPi;
                float dxds = loop.shiftX * (1f - Mathf.Cos(phi)) / (twoPi * loop.radius);
                position = new Vector3(x, y0 + loop.radius * (1f - Mathf.Cos(phi)), loop.centerZ + loop.radius * Mathf.Sin(phi));
                tangent = new Vector3(dxds, Mathf.Sin(phi), Mathf.Cos(phi)).normalized;
                // 円の中心を向く向き（ボールは円の内側を、外側の軌道に押し付けられて走る）
                normal = new Vector3(0f, Mathf.Cos(phi), -Mathf.Sin(phi));
                return;
            }

            float t = Mathf.Min(s - lead - circle, tail);
            position = new Vector3(baseX + loop.shiftX, y0, loop.centerZ + t);
            tangent = Vector3.forward;
            normal = Vector3.up;
        }

        /// <summary>確認用の線：k番目の輪の軌道の中心線を並べた点（レーン基準）。</summary>
        public Vector3[] SamplePath(int k, int count)
        {
            var points = new Vector3[count];
            float total = TotalLength(k);
            for (int i = 0; i < count; i++)
            {
                Evaluate(k, total * i / (count - 1), 0f, out Vector3 p, out _, out _, out _, out _);
                points[i] = p;
            }
            return points;
        }

        // ================= 乗せ降ろし =================

        /// <summary>投げるたび・レーンに入るたびに呼ぶ。</summary>
        public void ResetState()
        {
            _loop = -1;
            _previousZ = float.NegativeInfinity;
            FailedLoop = -1;
            PassedLoops = 0;
        }

        /// <summary>毎ステップ呼ぶ。乗っていなければ乗せる点を見張り、乗っていれば運ぶ。</summary>
        public void Tick(Ball.BallController ball, float deltaTime)
        {
            if (ball == null)
            {
                AdvanceSway(deltaTime);
                return;
            }

            var body = ball.GetComponent<Rigidbody>();
            if (body == null)
            {
                return;
            }

            AdvanceSway(deltaTime);

            if (_loop >= 0)
            {
                Ride(ball, body, deltaTime);
                return;
            }

            if (!ball.IsRolling || body.isKinematic)
            {
                _previousZ = float.NegativeInfinity;
                return;
            }

            Vector3 local = transform.InverseTransformPoint(body.position);
            for (int k = 0; k < loops.Length; k++)
            {
                float captureZ = CaptureZ(k);
                if (_previousZ < captureZ && local.z >= captureZ)
                {
                    TryCapture(k, ball, body, local);
                    break;
                }
            }
            _previousZ = local.z;
        }

        /// <summary>乗せる点を越えた。床の上にいて帯の中なら乗せる。</summary>
        private void TryCapture(int k, Ball.BallController ball, Rigidbody body, Vector3 local)
        {
            // 帯の中にいるかは、その瞬間の輪の位置を基準にする
            float lateral = local.x - (CourseXBefore(k) + LoopSway(k));
            bool onFloor = Mathf.Abs(local.y - BallCenterY) <= captureHeightTolerance;
            bool inside = Mathf.Abs(lateral) <= courseHalfWidth;
            if (!onFloor || !inside)
            {
                // 奈落へ落ちている最中の球は記録しない（床から大きく離れている）
                if (logEvents && Mathf.Abs(local.y - BallCenterY) < 0.5f)
                {
                    Debug.Log($"10本目：輪{k + 1}に乗れない（高さのずれ {(local.y - BallCenterY) * 1000f:F0}mm・横のずれ {lateral:F2}m）", this);
                }
                return;
            }

            // ★速度は kinematic にする前に読む（kinematic の球に速度を書くと警告になる）
            Vector3 localVelocity = transform.InverseTransformDirection(body.linearVelocity);
            Vector3 localSpin = transform.InverseTransformDirection(body.angularVelocity);

            _loop = k;
            _v = Mathf.Max(localVelocity.z, 0f);
            _captureSpeed = _v;
            _lateral = lateral;
            // 「乗る前に転がっていた床」に対する横の速さとして覚える（角度で付いた横向きの速さ）。
            // 軌道の上ではボールを輪と一緒に動かす（輪に対して横には止まっている）ので、
            // 輪から見た速さで覚えると、降ろすときに輪の揺れの分だけ横へ飛び出してしまう（実測 0.36m/s）。
            // 降ろすときに、降りる先の速さを足して戻す
            TryGetFloorVelocityAt(local.z, out float floorBelow);
            _lateralVelocity = localVelocity.x - floorBelow;
            _spinY = localSpin.y;
            // 乗せる点を行き過ぎた分は、軌道の上に繰り越す
            _s = local.z - CaptureZ(k);

            body.isKinematic = true;
            ball.IsExternallyDriven = true;

            Place(body, k, _s, 0f);

            if (logEvents)
            {
                Debug.Log($"10本目：輪{k + 1}に乗せた（速さ {_v:F2} m/s・ボールの実速 {localVelocity.magnitude:F2}・" +
                          $"横のずれ {_lateral:+0.00;-0.00}m・横向き {_lateralVelocity:+0.00;-0.00}m/s・行き過ぎ {_s * 1000f:F0}mm）", this);
            }
        }

        /// <summary>軌道の上を1ステップ進める。落ちるか、終わりまで来たら降ろす。</summary>
        private void Ride(Ball.BallController ball, Rigidbody body, float deltaTime)
        {
            int k = _loop;
            float g = Physics.gravity.magnitude;

            Evaluate(k, _s, _lateral, out Vector3 before, out _, out _, out _, out _);
            float ds = _v * deltaTime;
            _s += ds;

            float total = TotalLength(k);
            float s = Mathf.Min(_s, total);
            Evaluate(k, s, _lateral, out Vector3 position, out Vector3 tangent, out Vector3 normal, out float phi, out bool onCircle);

            // 高さの変化ぶんエネルギーをやり取りし、抵抗で道のりに比例して減らす
            float v2 = _v * _v - rollingEnergyFactor * g * (position.y - before.y);
            _v = Mathf.Max(Mathf.Sqrt(Mathf.Max(v2, 0f)) - lossPerMeter * ds, 0f);

            // 頂上付近：押し付けの力が0を下回ったら手を離す
            if (onCircle)
            {
                float press = _v * _v / loops[k].radius + g * Mathf.Cos(phi);
                if (press < 0f || _v <= 0f)
                {
                    FailedLoop = k;
                    Release(ball, body, position, tangent * _v + Vector3.right * LoopSwayVelocity(k), normal, false, 0f);
                    if (logEvents)
                    {
                        Debug.Log($"10本目：輪{k + 1}の {phi * Mathf.Rad2Deg:F0}度で落ちた（速さ {_v:F2} m/s・高さ {position.y:F2}m）", this);
                    }
                    return;
                }
            }

            if (_s >= total)
            {
                // 終わりを行き過ぎた分は、床の上へまっすぐ繰り越す
                position += Vector3.forward * (_s - total);
                PassedLoops++;
                // 降りる先に対して滑らない横の速さ：動く床なら床の速さ、止まった床なら輪の揺れの速さを足す
                float surface = TryGetFloorVelocityAt(position.z, out float floorVelocity)
                    ? floorVelocity
                    : LoopSwayVelocity(k);
                Vector3 velocity = Vector3.forward * _v + Vector3.right * (_lateralVelocity + surface);
                Release(ball, body, position, velocity, Vector3.up, true, surface);
                if (logEvents)
                {
                    Debug.Log($"10本目：輪{k + 1}を回り切って降ろした（乗せたとき {_captureSpeed:F2} → 降ろすとき {_v:F2} m/s）", this);
                }
                return;
            }

            Place(body, k, _s, deltaTime);
        }

        /// <summary>道のり s の点へ動かし、転がって見えるように回す。</summary>
        private void Place(Rigidbody body, int k, float s, float deltaTime)
        {
            Evaluate(k, s, _lateral, out Vector3 position, out Vector3 tangent, out Vector3 normal, out _, out _);
            body.MovePosition(transform.TransformPoint(position));

            if (deltaTime > 0f)
            {
                // 支える面の上を転がる回転：軸は「法線 × 進む向き」
                Vector3 axis = transform.TransformDirection(Vector3.Cross(normal, tangent));
                float degrees = _v / ballRadius * deltaTime * Mathf.Rad2Deg;
                body.MoveRotation(Quaternion.AngleAxis(degrees, axis) * body.rotation);
            }
        }

        /// <summary>
        /// 軌道から降ろす。kinematic を外し、速度と回転を渡す。
        /// normalRelease が true なら床の上へ（縦軸の回転＝カーブも戻す）、false なら落とす。
        /// </summary>
        private void Release(Ball.BallController ball, Rigidbody body, Vector3 localPosition,
                             Vector3 localVelocity, Vector3 localNormal, bool normalRelease, float surfaceVelocityX)
        {
            _loop = -1;
            _previousZ = localPosition.z;

            Vector3 world = transform.TransformPoint(localPosition);
            body.isKinematic = false;
            body.position = world;
            body.transform.position = world;

            Vector3 velocity = transform.TransformDirection(localVelocity);
            body.linearVelocity = velocity;

            // 転がりの回転：軸は「法線 × 進む向き」、速さは（床に対する速さ）/ r
            Vector3 roll = Vector3.zero;
            Vector3 relative = velocity - transform.right * surfaceVelocityX;
            if (relative.sqrMagnitude > 1e-6f)
            {
                Vector3 n = transform.TransformDirection(localNormal);
                roll = Vector3.Cross(n, relative) / ballRadius;
            }
            if (normalRelease)
            {
                roll += transform.up * _spinY;
            }
            body.angularVelocity = roll;

            ball.IsExternallyDriven = false;
        }

        /// <summary>
        /// 乗っている最中でも、その場で降ろす（レーンを出るとき）。
        /// ボールは全レーン共有なので、kinematic と切り替えを必ず戻す。
        /// </summary>
        public void ForceRelease(Ball.BallController ball)
        {
            if (_loop < 0 || ball == null)
            {
                _loop = -1;
                return;
            }

            var body = ball.GetComponent<Rigidbody>();
            _loop = -1;
            if (body != null)
            {
                body.isKinematic = false;
            }
            ball.IsExternallyDriven = false;
        }
    }
}
