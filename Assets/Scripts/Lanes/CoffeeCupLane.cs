using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// ピン台が遊園地のコーヒーカップのように動くレーン（9本目）。
    ///
    /// ── 乗り物はいつ回るか ─────────────────────────────
    ///
    /// **レーンに入った時点から回し続ける。** 投球の開始を待たない。
    /// 読めない側のレーンなので、構えているあいだに並びを見て狙いを決められない
    /// ほうが性格に合う。構え中はプレイヤーが画面をいちばん長く見ている時間でもある。
    ///
    /// 止まるのは停止の合図（①z／②決着）だけ。
    ///
    /// ── 1投ごとの流れ ───────────────────────────────
    ///
    ///   回転中 → （ボールが近づくと距離で減速）→ 停止（引き渡し・測り直し）
    ///   → 判定 → 構えに戻る → 座り直し → 回転中
    ///
    /// ── 停止の合図はなぜ2系統要るのか ──────────────────────
    ///
    /// 倒れ判定は「立っていた位置から水平に0.3m以上動いたら倒れた」で見ている。
    /// ピンが乗って動いたまま判定されると、立っていても**10本倒れた扱い**になる。
    /// そのため、判定が走る前に必ず止めて、その場所を基準に測り直す必要がある。
    ///
    ///   ① ボールが手前まで来た（z がしきい値に達した）
    ///       … 本来の停止。壁に当たる十分前に止め切る
    ///   ② ボールが決着した（OnThrowEnd）
    ///       … ガター、ピット直行、停止、時間切れ。①を通らない投球の保険。
    ///          ThrowSequencer は決着を見て ThrowEnd を出し、そのあとで判定に進むので、
    ///          ここで止めれば必ず判定より前になる
    ///
    /// ★片方でも取りこぼすと、乗車中のまま判定されて全部倒れた扱いになる。
    ///
    /// ── ボールだけカップの壁をすり抜ける ─────────────────────
    ///
    /// 段階4で壁をボールにも当てたところ、成立しなかった。
    ///   低い縁 → 打ち上げ台になり、ボールがピンを飛び越す（速10で1m上がった）
    ///   高い縁 → ボールが壁の前で止まる
    /// 「ちょうど乗り越えて中のピンを倒す」高さは存在しなかった。
    ///
    /// そこで壁は**ピンだけを受け止める**。ボールとの衝突は
    /// Physics.IgnoreCollision で個別に切る。全体の衝突設定には触れない。
    /// ピンはカップの中で暴れるので、コーヒーカップらしさは残る。
    ///
    /// ★レーンを出るときに必ず元へ戻す。
    ///
    /// ── しきい値をピンではなく壁に合わせる理由 ───────────────
    ///
    /// 段階4でカップに壁の当たり判定を付ける。壁はピンより手前に出るので、
    /// 「ピンの手前」で止めると壁に当たるときにまだ動いている。
    /// 基準は**壁のいちばん手前**に取る。
    ///
    /// ── 神殿（実験） ───────────────────────────────
    ///
    /// ピン台を柱で囲んだ神殿にし、入り口を1か所だけ開ける。神殿ごと回る。
    /// ボールが入り口から中へ入ったら、神殿ごと吹き飛ばしてピンを全部倒す（TempleBlast）。
    /// 入れなければ柱に阻まれて倒れない。
    /// 吹き飛ぶのは入ったときだけなので、2投目に入るとき（1投目で入れなかったとき）の神殿は無傷。
    /// </summary>
    public class CoffeeCupLane : BasicLane
    {
        [Header("停止の合図")]
        [Tooltip("ボールの中心がこの奥行き（レーン基準・m）に達したら乗り物を止める。" +
                 "カップの壁のいちばん手前より、数ステップぶん手前にすること。")]
        [SerializeField] private float stopBallZ = 15.0f;

        [Tooltip("減速を始める奥行き（レーン基準・m）。stopBallZ までの距離で0まで落とす。" +
                 "★時間で減速すると速い球で間に合わない。必ず距離で行う。")]
        [SerializeField] private float slowDownStartZ = 13.8f;

        [Tooltip("測り直す対象にする傾きの上限（度）。これを超えて傾いているピンは" +
                 "既に倒れているとみなし、測り直さない。" +
                 "倒れ判定の30度より緩くしてあるのは、席で少し揺れているだけのピンを" +
                 "巻き込まないため。")]
        [SerializeField] private float rebaseMaxTiltDegrees = 45f;

        [Header("乗り物")]
        [Tooltip("回るコーヒーカップ。空なら乗り物なしで動く（段階1の状態）。")]
        [SerializeField] private CoffeeCupRide ride;

        [Header("カップの壁")]
        [Tooltip("ボールだけカップの壁をすり抜けさせるか。ピンは壁に当たる。" +
                 "切ると壁がボールにも当たり、打ち上げや停止が起きる（段階4で確認済み）。")]
        [SerializeField] private bool ballIgnoresCupWalls = true;

        [Header("神殿")]
        [Tooltip("ボールが中へ入ったら神殿ごと吹き飛ばしてピンを全部倒す。空なら神殿なし。")]
        [SerializeField] private TempleBlast temple;

        [Header("確認用")]
        [Tooltip("止めた瞬間と、その理由を Console に出す。調整が済んだら切る。")]
        [SerializeField] private bool logEvents = true;

        /// <summary>この投球ぶんの停止が済んだか。</summary>
        private bool _stopped;

        /// <summary>この投球ぶんの座り直しを始めたか。1投につき1回にするため。</summary>
        private bool _reseatStarted;

        /// <summary>すり抜けを設定した相手。戻すために覚えておく。</summary>
        private readonly System.Collections.Generic.List<Collider> _ignoredWalls =
            new System.Collections.Generic.List<Collider>();
        private Collider[] _ignoredBall;

        /// <summary>共有の連鎖爆発。神殿に入ったときの爆発に使う。</summary>
        private Pins.PinExplosion _explosion;
        private Rigidbody _ballBody;

        /// <summary>
        /// 乗り物の速さに掛ける率。1で通常、0で停止。
        /// 段階3で、減速の区間に入ると1から0へ落ちていく。
        /// </summary>
        private float _rideSpeedScale = 1f;

        /// <summary>乗り物の速さに掛ける率。段階2以降が読む。</summary>
        public float RideSpeedScale => _rideSpeedScale;

        /// <summary>この投球ぶんの停止が済んだか。段階2以降が読む。</summary>
        public bool IsRideStopped => _stopped;

        public override void OnLaneStart(LaneContext context)
        {
            base.OnLaneStart(context);
            Arm();

            _explosion = Object.FindFirstObjectByType<Pins.PinExplosion>();
            _ballBody = context.ball != null ? context.ball.GetComponent<Rigidbody>() : null;

            // レーンに入った時点で席に着かせ、以降ずっと回す。
            // 開始角は入るたびに乱数で決める。すぐ投げる人に毎回同じ柱の向きが来ないように
            // （2投目の再開時の乱数は、座り直しの中で乗り物が行う）
            if (ride != null)
            {
                ride.RandomizeStartAngle();
                ride.Seat(context.pinSet);
            }

            SetBallIgnoresWalls(ballIgnoresCupWalls);
        }

        public override void OnThrowStart()
        {
            base.OnThrowStart();
            Arm();

            // すり抜けの設定は、当たり判定が一度無効になると消える。
            // 投げるたびに張り直しておけば取りこぼさない
            SetBallIgnoresWalls(ballIgnoresCupWalls);
        }

        public override void OnLaneEnd()
        {
            // ★他のレーンに持ち越さないよう、必ず戻す
            SetBallIgnoresWalls(false);
            base.OnLaneEnd();
        }

        private void OnDisable()
        {
            // レーンごと消されたときの保険。
            // 基底クラスの OnDestroy は private なので、ここでは上書きしない
            // （上書きするとボールの摩擦を戻す処理が呼ばれなくなる）
            SetBallIgnoresWalls(false);
        }

        /// <summary>
        /// ボールとカップの壁の衝突を、切る（true）か戻す（false）。
        /// 全体の衝突設定には触れず、当たり判定の組ごとに設定する。
        /// </summary>
        private void SetBallIgnoresWalls(bool ignore)
        {
            if (ignore)
            {
                Ball.BallController ball = Context.ball;
                if (ride == null || ball == null)
                {
                    return;
                }

                _ignoredWalls.Clear();
                ride.CollectWallColliders(_ignoredWalls);
                _ignoredBall = ball.GetComponentsInChildren<Collider>(true);
            }

            if (_ignoredBall == null)
            {
                return;
            }

            foreach (Collider ballCollider in _ignoredBall)
            {
                if (ballCollider == null)
                {
                    continue;
                }

                foreach (Collider wall in _ignoredWalls)
                {
                    if (wall != null)
                    {
                        Physics.IgnoreCollision(ballCollider, wall, ignore);
                    }
                }
            }

            if (!ignore)
            {
                _ignoredWalls.Clear();
                _ignoredBall = null;
            }
        }

        /// <summary>
        /// ボールが決着した。①を通らなかった投球（ガター、ピット直行、時間切れ）の保険。
        /// ThrowSequencer はここを通ってから判定に進むので、必ず判定より前になる。
        /// </summary>
        public override void OnThrowEnd()
        {
            base.OnThrowEnd();
            StopRide("ボールが決着した");
        }

        protected override void OnLaneFixedUpdate(float deltaTime)
        {
            base.OnLaneFixedUpdate(deltaTime);

            Ball.BallController ball = Context.ball;
            if (ball == null)
            {
                return;
            }

            // 神殿：ボールが中へ入ったら吹き飛ばす（吹き飛んだ後は何もしない）。
            // 位置は描画用の補間ではなく、物理の位置で見る
            Vector3 ballPosition = _ballBody != null ? _ballBody.position : ball.transform.position;
            if (temple != null && !temple.HasExploded && temple.IsInside(ballPosition))
            {
                BlowUpTemple();
            }

            if (_stopped)
            {
                // 判定が済んでボールが構えに戻ったら、残ったピンを席へ戻して再開する。
                // 倒れたピンの取り除きは判定の中で終わっているので、
                // ここで戻す相手は「残ったピン」だけになる
                if (!_reseatStarted && ball.IsAiming && ride != null)
                {
                    _reseatStarted = true;

                    // ★速さの率を1に戻す。止めたときに0にしたままだと、
                    //   座り直しが終わって運転を再開しても速さ0で、2投目の構え中に神殿が止まったままになる
                    //   （投げた瞬間の Arm() でしか戻らなかった）。座り直しの間はこの率を使わない
                    _rideSpeedScale = 1f;
                    ride.BeginReseat();
                    if (logEvents)
                    {
                        Debug.Log("9本目：残ったピンを席へ戻して、乗り物を再開する", this);
                    }
                }
            }
            else
            {
                // レーン基準で測る。レーンの置き場所が変わっても同じしきい値が使える
                float ballZ = transform.InverseTransformPoint(ball.transform.position).z;

                if (ballZ >= stopBallZ)
                {
                    StopRide("ボールが手前まで来た");
                    return;
                }

                // 時間ではなく距離で落とすので、速い球でも必ず0まで届く
                float span = stopBallZ - slowDownStartZ;
                _rideSpeedScale = span <= Mathf.Epsilon
                    ? 1f
                    : Mathf.Clamp01((stopBallZ - ballZ) / span);
            }

            // 座り直しの最中も進める必要があるので、停止中でも呼ぶ。
            // 乗り物の側で「運転中か・座り直し中か」を見て振り分ける
            if (ride != null)
            {
                ride.Step(deltaTime, _rideSpeedScale);
            }
        }

        /// <summary>
        /// 乗り物を止めて、ピンの「立っていた位置」をいまの場所に測り直す。
        /// 二重に呼ばれても一度しか効かない。
        /// </summary>
        private void StopRide(string reason)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            _rideSpeedScale = 0f;

            // ★10本同時に降ろす。1本でも kinematic のまま残ると、
            //   そのピンだけ爆発の対象外（CanBeBlasted が false）になって倒れなくなる。
            //   降ろすと同時に、乗り物の側で kinematic の張り直しも止まる
            if (ride != null)
            {
                ride.Release(true);
            }

            int rebased = RebasePins();

            if (logEvents)
            {
                Debug.Log($"9本目：乗り物を止めた（{reason}）／立っていた位置を測り直した {rebased} 本", this);
            }
        }

        /// <summary>
        /// 席に着いているピンについて、いまの場所を「立っていた位置」として覚え直す。
        /// これ以降は普通のピン台とまったく同じ扱いになる。
        ///
        /// ★既に倒れているピンは測り直さない。
        ///   合図②（決着）はピンが飛んだ後に来ることがあり、そこで測り直すと
        ///   **飛んだ先を「立っていた位置」にしてしまい、倒れたピンが立っている扱いになる。**
        ///   実際には合図①が必ず先に効く（ピンは z≥16 にあるので、
        ///   当たるボールは必ずしきい値を通る）が、安い保険として入れてある。
        /// </summary>
        private int RebasePins()
        {
            Pins.PinSet pinSet = Context.pinSet;
            if (pinSet == null)
            {
                return 0;
            }

            int count = 0;
            var pins = pinSet.Pins;
            for (int i = 0; i < pins.Count; i++)
            {
                Pins.Pin pin = pins[i];
                if (pin == null || !pin.IsStandingInPlay)
                {
                    continue;
                }

                // 倒れているピンは対象外。古い基準のままにしておけば倒れた扱いが残る
                if (Vector3.Angle(pin.transform.up, Vector3.up) > rebaseMaxTiltDegrees)
                {
                    continue;
                }

                pin.RebaseInitialPose();
                count++;
            }
            return count;
        }

        /// <summary>
        /// ボールが神殿に入った。神殿ごと吹き飛ばして、ピンを全部倒す。
        ///
        /// ★爆発の対象は動的なピンだけ（kinematic は対象外）。
        ///   入り口は停止の合図（z=15.0）より奥なので、ふつうはもう降ろしてある。
        ///   念のため、まだ運転中なら先に止めて降ろす。
        /// </summary>
        private void BlowUpTemple()
        {
            StopRide("ボールが神殿に入った");

            int kinematic = 0;
            Pins.PinSet pinSet = Context.pinSet;
            if (pinSet != null)
            {
                foreach (Pins.Pin pin in pinSet.Pins)
                {
                    if (pin != null && pin.IsStandingInPlay && pin.GetComponent<Rigidbody>().isKinematic)
                    {
                        kinematic++;
                    }
                }
            }

            temple.Explode(_explosion);

            if (logEvents)
            {
                Debug.Log($"9本目：神殿の爆発の瞬間、kinematic のピン {kinematic} 本（0であること）", this);
            }
        }

        /// <summary>次の投球に備えて、停止の合図を張り直す。</summary>
        private void Arm()
        {
            _stopped = false;
            _reseatStarted = false;
            _rideSpeedScale = 1f;
        }
    }
}
