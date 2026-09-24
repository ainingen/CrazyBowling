using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 遊園地のコーヒーカップ。円盤が回り、その上のカップがそれぞれ自転し、
    /// ピンが座席に乗って一緒に動く。
    ///
    /// ── なぜピンを kinematic で運ぶのか ────────────────────
    ///
    /// 回る床の上にピンを置いて摩擦で運ばせる手もあるが、当てにならない。
    /// ピンの接地は6cm角しかなく、自転の成分も混ざるので、数秒かけてじりじりずれる。
    /// 遠心力そのものは問題ではない（実測：転倒の限界 2.26 m/s² に対して 0.32 m/s²）。
    /// **位置が再現できないことのほうが困る**ので、席に固定して運ぶ。
    ///
    /// ── 親 Transform を回してはいけない ───────────────────
    ///
    /// kinematic な Rigidbody の親 Transform を回すと、PhysX には瞬間移動として届く
    /// （4本目の動く壁で学んだこと）。
    /// そこで**世界座標の姿勢を毎ステップ計算し、MovePosition / MoveRotation で直接渡す。**
    /// 円盤とカップの「見た目」は当たり判定を持たないので、Transform で回してよい。
    ///
    /// ── kinematic 同士は衝突しない ───────────────────────
    ///
    /// 乗車中のピンはお互いをすり抜ける。**座席の間隔が常にピンの直径を超えるよう、
    /// 置き方で保証すること**（Inspector の値を変えたら間隔を測り直す）。
    ///
    /// ── ★降ろしたら kinematic の張り直しを必ず止める ─────────────
    ///
    /// 乗車中は毎ステップ kinematic を張り直している（レーンに入る流れの中で
    /// PinSet.ResetAll() に外されるため）。**降ろした後もこれが走ると、
    /// ボールが当たる寸前にピンが kinematic へ戻り、爆発の対象外になって
    /// 倒れもしなくなる。** 降ろしたら _riding を false にして完全に止める。
    ///
    /// ── カップの壁（当たり判定あり） ─────────────────────────
    ///
    /// 壁も kinematic の Rigidbody で、ピンと同じく**世界座標の姿勢を直接渡して**動かす。
    /// 降ろした後（停止後）は一切動かさないので、**ボールが当たるときには静止している。**
    /// 4本目・7本目で苦労した「動いている当たり判定に当たる」は起きない。
    /// 乗車中のピンと壁はどちらも kinematic なので、互いにぶつからない。
    ///
    /// ── 外周の囲い（神殿の柱など） ─────────────────────────
    ///
    /// 円盤の外周に立てた囲い。円盤と一緒に回る。
    /// 小さなカップの壁と違い、**ボールにも当てる**（すり抜けの対象に入れない）。
    /// 柱の間が手前に来ていればボールは中へ入れ、柱が来ていれば止められる／弾かれる。
    /// </summary>
    public class CoffeeCupRide : MonoBehaviour
    {
        [Header("置き方")]
        [Tooltip("円盤の中心の奥行き（レーン基準・m）。通常のラックの重心と同じ場所にすると、" +
                 "ボールの飛距離と到達時間が他のレーンと変わらない。")]
        [SerializeField] private float discCenterZ = 16.6f;

        [Tooltip("円盤の中心から、カップの中心までの距離（m）。大きいほどピンが散らばる。")]
        [SerializeField] private float cupRingRadius = 0.45f;

        [Tooltip("カップの中心から、座席までの距離（m）。")]
        [SerializeField] private float seatRadius = 0.15f;

        [Tooltip("ピンを置く高さ（レーン基準・m）。ピン台の上面に合わせる。")]
        [SerializeField] private float seatY = 0.05f;

        [Tooltip("カップごとの座席の数。合計がピンの本数と合うようにする。")]
        [SerializeField] private int[] seatsPerCup = { 4, 3, 3 };

        [Header("回り方")]
        [Tooltip("円盤が1回転するのにかかる秒数。")]
        [SerializeField] private float discSecondsPerTurn = 14f;

        [Tooltip("カップが1回転するのにかかる秒数。")]
        [SerializeField] private float cupSecondsPerTurn = 5f;

        [Tooltip("カップを円盤と逆向きに回すか。逆にすると並びの変化が読みにくくなる。")]
        [SerializeField] private bool cupReversed = true;

        [Tooltip("レーンに入るたびに、円盤とカップの回転角を乱数で決めるか。" +
                 "切ると毎回同じ角度から始まり、すぐ投げる人には毎回同じ柱の向きが来る。")]
        [SerializeField] private bool randomStartAngle = true;

        [Tooltip("2投目の再開時にも、回転角を乱数で決め直すか。" +
                 "座り直しの時間の中で、新しい向きまで回しながら席へ戻す。")]
        [SerializeField] private bool randomOnReseat = true;

        [Header("座り直し")]
        [Tooltip("2投目に入るとき、残ったピンを席へ戻すのにかける秒数。" +
                 "0にすると瞬間移動になり、カクッと見える。")]
        [SerializeField] private float reseatSeconds = 0.3f;

        [Header("見た目")]
        [Tooltip("円盤の見た目。当たり判定は付けないこと。")]
        [SerializeField] private Transform discVisual;

        [Tooltip("カップの見た目。座席の組と同じ数・同じ順に入れる。")]
        [SerializeField] private Transform[] cupVisuals;

        [Header("当たり判定")]
        [Tooltip("カップの壁。見た目と同じ数・同じ順に入れる。kinematic の Rigidbody を付けておくこと。" +
                 "親 Transform は回さず、世界座標の姿勢を MovePosition / MoveRotation で渡す。" +
                 "空なら壁なし（段階3の状態）。")]
        [SerializeField] private Rigidbody[] cupWalls;

        [Tooltip("外周の囲い（神殿の柱など）。円盤の中心に置いた kinematic の Rigidbody。円盤と一緒に回す。" +
                 "ボールにも当てるので、すり抜けの対象には入れない。空なら囲いなし。")]
        [SerializeField] private Rigidbody outerWall;

        /// <summary>回した角度（度）。止めても巻き戻さないので、再開しても位相が飛ばない。</summary>
        private float _discAngle;
        private float _cupAngle;

        /// <summary>席に着かせたピン。</summary>
        private Pins.Pin[] _seated;

        /// <summary>席に着かせる前の kinematic の状態。レーンを出るときに戻す。</summary>
        private bool[] _wasKinematic;

        /// <summary>
        /// いま運んでいる最中か。降ろしたら false になり、
        /// kinematic の張り直しも姿勢の上書きも止まる。
        /// </summary>
        private bool _riding;

        /// <summary>前のステップの座席の位置。降ろすときに渡す速度を出すために使う。</summary>
        private Vector3[] _previousSeat;
        private Vector3[] _seatVelocity;

        /// <summary>座り直しの残り時間と、出発の姿勢。</summary>
        private float _reseatRemaining;
        private Vector3[] _reseatFromPosition;
        private Quaternion[] _reseatFromRotation;

        /// <summary>座り直しの間に回す角度の、出発と行き先（度）。</summary>
        private float _reseatFromDisc;
        private float _reseatFromCup;
        private float _reseatToDisc;
        private float _reseatToCup;

        /// <summary>いま運んでいる最中か。</summary>
        public bool IsRiding => _riding;

        /// <summary>座り直しの最中か。</summary>
        public bool IsReseating => _reseatRemaining > 0f;

        private void OnDisable()
        {
            // レーンを出たらピンを必ず普通の状態へ戻す。
            // 戻し忘れると次のレーンのピンが kinematic のままになり、
            // ボールが壁に当たったように跳ね返る
            Release(false);
            _seated = null;
            _wasKinematic = null;
        }

        /// <summary>
        /// 円盤とカップの回転角を乱数で決め直す。レーンに入るときに、席に着かせる前に呼ぶ。
        /// すぐ投げる人に毎回同じ柱の向きが来ないようにするため。
        /// </summary>
        public void RandomizeStartAngle()
        {
            if (!randomStartAngle)
            {
                return;
            }

            _discAngle = Random.Range(0f, 360f);
            _cupAngle = Random.Range(0f, 360f);
        }

        /// <summary>
        /// ピンを席に着かせる。kinematic にして、以降は計算した姿勢で運ぶ。
        /// </summary>
        public void Seat(Pins.PinSet pinSet)
        {
            Release(false);
            _seated = null;
            _wasKinematic = null;

            if (pinSet == null)
            {
                return;
            }

            var pins = pinSet.Pins;
            _seated = new Pins.Pin[pins.Count];
            _wasKinematic = new bool[pins.Count];
            _previousSeat = new Vector3[pins.Count];
            _seatVelocity = new Vector3[pins.Count];

            for (int i = 0; i < pins.Count; i++)
            {
                _seated[i] = pins[i];
                if (pins[i] == null)
                {
                    continue;
                }

                var body = pins[i].GetComponent<Rigidbody>();
                if (body == null)
                {
                    continue;
                }

                _wasKinematic[i] = body.isKinematic;
            }

            _riding = true;
            _reseatRemaining = 0f;

            // 最初の1歩は当たりを解かずに置く。前の場所から滑ってこないように
            Apply(true, 0f);
        }

        /// <summary>
        /// ピンを席から降ろす。kinematic を外し、台の速度を渡す。
        ///
        /// ★ここで _riding を false にするのが要点。
        ///   これ以降、kinematic の張り直しも姿勢の上書きも起きない。
        /// </summary>
        /// <param name="handOverVelocity">
        /// 台の速度をピンに渡すか。減速し切っていればほぼ0だが、
        /// 残っていた場合に段差なくつなぐために渡す。
        /// </param>
        public void Release(bool handOverVelocity)
        {
            _riding = false;
            _reseatRemaining = 0f;

            if (_seated == null)
            {
                return;
            }

            for (int i = 0; i < _seated.Length; i++)
            {
                if (_seated[i] == null)
                {
                    continue;
                }

                var body = _seated[i].GetComponent<Rigidbody>();
                if (body == null)
                {
                    continue;
                }

                body.isKinematic = _wasKinematic[i];

                if (handOverVelocity && !body.isKinematic
                    && _seatVelocity != null && i < _seatVelocity.Length)
                {
                    body.linearVelocity = _seatVelocity[i];
                }
            }
        }

        /// <summary>
        /// 残っているピンを席へ戻し始める。瞬間移動にしないため時間をかける。
        /// 戻し終わると自動で運転を再開する。
        /// </summary>
        public void BeginReseat()
        {
            if (_seated == null)
            {
                return;
            }

            _reseatFromPosition = new Vector3[_seated.Length];
            _reseatFromRotation = new Quaternion[_seated.Length];

            for (int i = 0; i < _seated.Length; i++)
            {
                Pins.Pin pin = _seated[i];
                if (pin == null)
                {
                    continue;
                }

                _reseatFromPosition[i] = pin.transform.position;
                _reseatFromRotation[i] = pin.transform.rotation;

                var body = pin.GetComponent<Rigidbody>();
                if (body == null)
                {
                    continue;
                }

                // 戻している間に倒れないよう、先に kinematic にする
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }

            // 再開時の乱数：行き先の角度を決めておき、座り直しの間に回していく。
            // 一瞬で飛ばすと壁と円盤がパッと切り替わって見えるため
            _reseatFromDisc = _discAngle;
            _reseatFromCup = _cupAngle;
            _reseatToDisc = randomOnReseat ? Random.Range(0f, 360f) : _discAngle;
            _reseatToCup = randomOnReseat ? Random.Range(0f, 360f) : _cupAngle;

            _reseatRemaining = Mathf.Max(reseatSeconds, 0.0001f);
        }

        /// <summary>
        /// 乗り物を進める。速さの率は、ボールが近づいたときの減速に使う。
        /// 座り直しの最中は、回さずに戻す処理だけを進める。
        /// </summary>
        public void Step(float deltaTime, float speedScale)
        {
            if (_reseatRemaining > 0f)
            {
                AdvanceReseat(deltaTime);
                return;
            }

            if (!_riding)
            {
                return;
            }

            if (discSecondsPerTurn > Mathf.Epsilon)
            {
                _discAngle += 360f / discSecondsPerTurn * speedScale * deltaTime;
            }
            if (cupSecondsPerTurn > Mathf.Epsilon)
            {
                _cupAngle += 360f / cupSecondsPerTurn * speedScale * deltaTime;
            }

            _discAngle = Mathf.Repeat(_discAngle, 360f);
            _cupAngle = Mathf.Repeat(_cupAngle, 360f);

            Apply(false, deltaTime);
        }

        /// <summary>座り直しを進める。終わったら運転を再開する。</summary>
        private void AdvanceReseat(float deltaTime)
        {
            _reseatRemaining -= deltaTime;
            float remaining = Mathf.Max(_reseatRemaining, 0f);
            float progress = reseatSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(1f - remaining / reseatSeconds);

            // 端で滑らかに止まるよう、直線ではなく滑らかな曲線で寄せる
            float eased = progress * progress * (3f - 2f * progress);

            // 角度も新しい向きへ回す（近いほうの向きで）。壁と見た目はここで動く。
            // 運転中ではない（_riding が false）ので、Apply はピンに触らない
            _discAngle = Mathf.Repeat(
                _reseatFromDisc + Mathf.DeltaAngle(_reseatFromDisc, _reseatToDisc) * eased, 360f);
            _cupAngle = Mathf.Repeat(
                _reseatFromCup + Mathf.DeltaAngle(_reseatFromCup, _reseatToCup) * eased, 360f);
            Apply(false, deltaTime);

            for (int i = 0; i < _seated.Length; i++)
            {
                Pins.Pin pin = _seated[i];
                if (pin == null || !pin.IsStandingInPlay)
                {
                    continue;
                }

                if (!TryGetSeatPose(i, out Vector3 target, out Quaternion targetRotation))
                {
                    continue;
                }

                var body = pin.GetComponent<Rigidbody>();
                if (body == null)
                {
                    continue;
                }

                body.MovePosition(Vector3.Lerp(_reseatFromPosition[i], target, eased));
                body.MoveRotation(Quaternion.Slerp(_reseatFromRotation[i], targetRotation, eased));
            }

            if (_reseatRemaining <= 0f)
            {
                _reseatRemaining = 0f;
                _riding = true;
            }
        }

        /// <summary>
        /// 何番目のピンが座るべき世界座標の姿勢を返す。
        /// 座席の割り当ては、カップの順・座席の順にピンを並べたもの。
        /// </summary>
        private bool TryGetSeatPose(int pinIndex, out Vector3 world, out Quaternion rotation)
        {
            world = Vector3.zero;
            rotation = Quaternion.identity;

            float cupSpin = cupReversed ? -_cupAngle : _cupAngle;
            int cupCount = seatsPerCup == null ? 0 : seatsPerCup.Length;
            int running = 0;

            for (int cup = 0; cup < cupCount; cup++)
            {
                int seats = seatsPerCup[cup];
                if (pinIndex >= running + seats)
                {
                    running += seats;
                    continue;
                }

                int seat = pinIndex - running;

                float cupPlace = (_discAngle + cup * 360f / cupCount) * Mathf.Deg2Rad;
                Vector3 cupLocal = new Vector3(
                    Mathf.Sin(cupPlace) * cupRingRadius,
                    seatY,
                    discCenterZ + Mathf.Cos(cupPlace) * cupRingRadius);

                float cupFacing = cupSpin + cup * 360f / cupCount;
                float seatAngle = (cupFacing + seat * 360f / seats) * Mathf.Deg2Rad;

                Vector3 seatLocal = cupLocal + new Vector3(
                    Mathf.Sin(seatAngle) * seatRadius,
                    0f,
                    Mathf.Cos(seatAngle) * seatRadius);

                world = transform.TransformPoint(seatLocal);
                rotation = transform.rotation * Quaternion.Euler(0f, cupFacing, 0f);
                return true;
            }

            return false;
        }

        /// <summary>
        /// いまの角度から、カップと座席の姿勢を決めて反映する。
        /// </summary>
        /// <param name="warp">true なら当たりを解かずに置く（席に着かせた瞬間）。</param>
        /// <param name="deltaTime">座席の速度を出すための経過時間。</param>
        private void Apply(bool warp, float deltaTime)
        {
            // 見た目のカップと円盤は、当たり判定を持たないので Transform で動かしてよい
            float cupSpin = cupReversed ? -_cupAngle : _cupAngle;
            int cupCount = seatsPerCup == null ? 0 : seatsPerCup.Length;

            for (int cup = 0; cup < cupCount; cup++)
            {
                float cupPlace = (_discAngle + cup * 360f / cupCount) * Mathf.Deg2Rad;
                Vector3 cupLocal = new Vector3(
                    Mathf.Sin(cupPlace) * cupRingRadius,
                    seatY,
                    discCenterZ + Mathf.Cos(cupPlace) * cupRingRadius);
                float cupFacing = cupSpin + cup * 360f / cupCount;

                if (cupVisuals != null && cup < cupVisuals.Length && cupVisuals[cup] != null)
                {
                    cupVisuals[cup].localPosition = cupLocal;
                    cupVisuals[cup].localRotation = Quaternion.Euler(0f, cupFacing, 0f);
                }

                // 壁は当たり判定を持つので、見た目と違って Transform では動かさない。
                // 世界座標の姿勢を計算して直接渡す
                if (cupWalls != null && cup < cupWalls.Length && cupWalls[cup] != null)
                {
                    Rigidbody wall = cupWalls[cup];
                    Vector3 wallWorld = transform.TransformPoint(cupLocal);
                    Quaternion wallRotation = transform.rotation * Quaternion.Euler(0f, cupFacing, 0f);

                    if (warp)
                    {
                        wall.position = wallWorld;
                        wall.rotation = wallRotation;
                        wall.transform.SetPositionAndRotation(wallWorld, wallRotation);
                    }
                    else
                    {
                        wall.MovePosition(wallWorld);
                        wall.MoveRotation(wallRotation);
                    }
                }
            }

            if (discVisual != null)
            {
                discVisual.localPosition = new Vector3(0f, seatY, discCenterZ);
                discVisual.localRotation = Quaternion.Euler(0f, _discAngle, 0f);
            }

            // 外周の囲いも当たり判定を持つので、世界座標の姿勢を直接渡す
            if (outerWall != null)
            {
                Vector3 outerWorld = transform.TransformPoint(new Vector3(0f, seatY, discCenterZ));
                Quaternion outerRotation = transform.rotation * Quaternion.Euler(0f, _discAngle, 0f);

                if (warp)
                {
                    outerWall.position = outerWorld;
                    outerWall.rotation = outerRotation;
                    outerWall.transform.SetPositionAndRotation(outerWorld, outerRotation);
                }
                else
                {
                    outerWall.MovePosition(outerWorld);
                    outerWall.MoveRotation(outerRotation);
                }
            }

            if (_seated == null || !_riding)
            {
                return;
            }

            for (int i = 0; i < _seated.Length; i++)
            {
                Pins.Pin pin = _seated[i];
                if (pin == null)
                {
                    continue;
                }

                if (!TryGetSeatPose(i, out Vector3 world, out Quaternion rotation))
                {
                    continue;
                }

                // 座席そのものの速度。降ろすときにピンへ渡す
                if (deltaTime > Mathf.Epsilon && _previousSeat != null)
                {
                    _seatVelocity[i] = (world - _previousSeat[i]) / deltaTime;
                }
                _previousSeat[i] = world;

                var body = pin.GetComponent<Rigidbody>();
                if (body == null)
                {
                    continue;
                }

                // ★運んでいる間だけ kinematic を張り直す。
                //   レーンに入る流れの中で PinSet.ResetAll() が外してしまうため。
                //   降ろした後は _riding が false なのでここへ来ない
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.isKinematic = true;
                }

                if (warp)
                {
                    body.position = world;
                    body.rotation = rotation;
                    pin.transform.SetPositionAndRotation(world, rotation);
                }
                else
                {
                    // ★親を回すのではなく、世界座標の姿勢を直接渡す
                    body.MovePosition(world);
                    body.MoveRotation(rotation);
                }
            }

            if (warp)
            {
                Physics.SyncTransforms();
            }
        }

        /// <summary>
        /// カップの壁の当たり判定を集める。
        /// レーンが「ボールだけ壁をすり抜けさせる」設定に使う。
        /// </summary>
        public void CollectWallColliders(System.Collections.Generic.List<Collider> into)
        {
            if (cupWalls == null || into == null)
            {
                return;
            }

            foreach (Rigidbody wall in cupWalls)
            {
                if (wall != null)
                {
                    into.AddRange(wall.GetComponentsInChildren<Collider>(true));
                }
            }
        }

        /// <summary>
        /// 座席同士がいちばん近づく距離（m）。
        /// kinematic 同士は衝突しないので、ここがピンの直径を下回ると重なって見える。
        /// </summary>
        public float MinimumSeatGap()
        {
            float smallest = float.MaxValue;
            int cupCount = seatsPerCup == null ? 0 : seatsPerCup.Length;

            for (int cup = 0; cup < cupCount; cup++)
            {
                int seats = seatsPerCup[cup];
                if (seats < 2)
                {
                    continue;
                }

                float gap = 2f * seatRadius * Mathf.Sin(Mathf.PI / seats);
                smallest = Mathf.Min(smallest, gap);
            }

            if (cupCount >= 2)
            {
                float centerGap = 2f * cupRingRadius * Mathf.Sin(Mathf.PI / cupCount);
                smallest = Mathf.Min(smallest, centerGap - seatRadius * 2f);
            }

            return smallest;
        }
    }
}
