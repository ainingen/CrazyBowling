using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// レーンの途中が筒になっているレーン。
    ///
    /// ── 当たり判定は「底の帯」だけを板で作る ────────────────
    ///
    /// Unity の標準コライダーに、筒の内側を表せるものは無い。
    /// Box / Sphere / Capsule はすべて中身の詰まった凸形状で、
    /// CapsuleCollider も「円柱＋両端の半球」なので内壁にはならない。
    /// 凹形状を表せるのは非凸 MeshCollider だけだが、
    /// 円筒メッシュは四角形を三角形に割るため、
    /// **進行方向を横切る対角線の稜線**ができてそこを踏む
    /// （`ROLLING FLOOR` の1.8cmの浮きと同じ問題。「保留にしてあること 1」を参照）。
    ///
    /// そこで、**ボールが通る底の帯だけ**を、
    /// レーンの全長を1枚で通す長い箱で作る。
    ///
    /// **これは6本目の助走より安全な形になっている。**
    /// 6本目で弾かれたのは、進行方向を横切る継ぎ目を乗り越えるときだった。
    /// この作りでは継ぎ目が**進行方向と平行にしか無い**。
    /// さらに筒の内側は全域が凹（底から横へ行くほど面が上向きに折れる）なので、
    /// ボールは常に面へ押し付けられ、浮いて角に届くことがない。
    ///
    /// ── 当たり判定の筒は回さない ──────────────────────
    ///
    /// 回るコライダーでボールを持ち上げると、**摩擦で登る高さが回転速度で決まらない**。
    /// 釣り合いは mg·sinφ ≤ μ·mg·cosφ すなわち tanφ ≤ μ なので、
    /// 床の摩擦0.6では回転が速かろうが遅かろうが約31度まで登って止まる。
    /// 「ゆっくり回す」という見た目の調整が効き具合に反映されない。
    /// 5本目 SPINNING DISC で分かったことと同じ構図。
    ///
    /// そこで**当たり判定の筒は固定し、見た目だけ回す**。
    /// ボールを横へ流す力はスクリプトで加え、強さは見た目の回転速度と切り離す。
    /// 加える口は段階3で作った `LaneBehaviour.TryGetDrift` をそのまま使う。
    ///
    /// ── 入口は「口を広げて」つなぐ ───────────────────────
    ///
    /// 筒の面は底から離れれば必ず床より高くなるので、
    /// 板の先端の小口（進行方向を向いた垂直な面）が床の上に出る。
    /// これは筒の形そのものなので、板の置き方（外接／内接）では消せない。
    /// 実測では、内接にして筒の底を床と面一（±0.000mm）にしても
    /// 同じ位置で同じように弾かれた（+0.73 → +0.95m/s）。
    ///
    /// 小口の高さは、平らな床の上のボール（中心 y=0.160）と
    /// 筒のV字に収まったボール（中心 y=0.05+0.11/cos7.5°=0.16094）の差、**0.94mm**。
    /// 板を継ぎ足して段階的に立ち上げても、継ぎ目ごとに小口ができ、
    /// 3分割で 0.52mm が残るだけでゼロにはならない。
    ///
    /// そこで**縁を薄くするのではなく、縁をボールの届かない所へ動かす**。
    /// 筒の板そのものを入口側で左右へ開き（<see cref="mouthFlare"/>）、
    /// 底に空いた隙間は床で埋める。板は増えない。
    ///
    /// ピンは回さない。筒はピン台の手前で終わらせる。
    /// ピンが動かないので、爆発の判定（ヘッドピンの初期位置が基準）は変更不要。
    /// </summary>
    public class TubeLane : BasicLane
    {
        [Header("筒")]
        [Tooltip("筒が始まる位置（レーンの手前からの奥行き・m）。" +
                 "オイルの終わり（10m）より奥に置くこと。手前に置くとカーブと同時に効いて読めなくなる。")]
        [SerializeField] private float tubeStartZ = 10f;

        [Tooltip("筒の長さ（m）。ピン台（16.2m）の手前で終わらせること。")]
        [SerializeField] private float tubeLength = 4.5f;

        [Tooltip("筒の半径（m）。底はレーンの床と同じ高さになる。" +
                 "0.6mだと、当たり判定の端（±60度）がちょうどレーンの端（±0.525m）に来る。")]
        [SerializeField] private float radius = 0.6f;

        [Tooltip("当たり判定を作る角度の範囲（度）。底を中心に左右へこのぶん。" +
                 "ボールは底付近しか通らないので、全周ぶんは要らない。")]
        [SerializeField] private float colliderArcDegrees = 120f;

        [Tooltip("見た目の筒と当たり判定の隙間（m）。" +
                 "見た目を少し外側に置いて、面が重なってちらつくのを避ける。")]
        [SerializeField] private float visualGap = 0.005f;

        [Tooltip("板の厚み（m）。")]
        [SerializeField] private float plateThickness = 0.1f;

        [Tooltip("筒の口を左右へ広げる幅（m）。入口で最大、奥で0になる。" +
                 "これが漏斗。板を継ぎ足すのではなく、筒の板そのものを入口側で外へずらす。" +
                 "0にすると板の先端の小口がボールの表面に 0.98mm めり込み、+0.95m/s で弾かれる。" +
                 "0.03mにすると小口はボールから 4.0mm 離れる。" +
                 "底に開く隙間は床が埋めるので、ボールは床の上をまっすぐ走る。")]
        [SerializeField] private float mouthFlare = 0.10f;

        [Tooltip("筒の終わりでの開き幅（m）。0にすると真円の筒になる。" +
                 "0より大きくすると、筒の底がその幅だけ平らなままになる。" +
                 "口を大きく開くと入口では蹴られなくなるが、閉じきると全部の球が中央へ集まってしまう。" +
                 "閉じきらないでおくと、ボールは自分の走っている位置を保ったまま筒を通れる。")]
        [SerializeField] private float mouthFlareEnd = 0f;

        [Tooltip("見た目の板の厚み（m）。")]
        [SerializeField] private float visualThickness = 0.02f;

        [Header("入口：壁へ上げる静止レール（回さない）")]
        [Tooltip("レールが始まる奥行き（m）。筒の入口のすぐ奥から。")]
        [SerializeField] private float launchStartZ = 10.2f;

        [Tooltip("レールの長さ（m）。奥のプロペラに場所を譲るので短くする。")]
        [SerializeField] private float launchLength = 1.7f;

        [Tooltip("レールの手前側の端の角度（度）。筒の底が0、正が右。")]
        [SerializeField] private float launchStartAngle = -20f;

        [Tooltip("レールの奥側の端の角度（度）。" +
                 "短くしたぶんねじりを強めて、同じだけ壁へ上げる。")]
        [SerializeField] private float launchEndAngle = 45f;

        [Tooltip("レールの太さ（半径・m）。")]
        [SerializeField] private float launchRadius = 0.05f;

        [Tooltip("レールの軸を通す半径（筒の中心から・m）。" +
                 "**壁沿い（筒の半径−レールの太さ）に置く。**" +
                 "ここは押し上げるのが仕事なので、あえてボールの中心より下を突いて持ち上げる。")]
        [SerializeField] private float launchTrackRadius = 0.55f;

        [Tooltip("壁へ上げる静止レール本体。CapsuleCollider を持たせること。回さない入れ物に置く。")]
        [SerializeField] private Transform launchRail;

        [Header("奥：プロペラ（回す）")]
        [Tooltip("羽根が始まる奥行き（m）。" +
                 "**ボールがまだ筒の底を這っている区間には、羽根を1枚も置かないこと。**" +
                 "底にいるボールに羽根の先端が当たると、毎回大きく打ち上げられる（実測 最大277mm）。" +
                 "静止レールで壁へ上げきった位置より奥から始める。")]
        [SerializeField] private float railStartZ = 11.9f;

        [Tooltip("羽根の長さ（m）。筒の終わり（14.5m）の手前で終わらせる。")]
        [SerializeField] private float railLength = 2.5f;

        [Tooltip("羽根の手前側の端の角度（度）。")]
        [SerializeField] private float railStartAngle = 0f;

        [Tooltip("羽根の奥側の端の角度（度）。" +
                 "**手前と同じにして、ねじりを0にすること。**" +
                 "ねじると羽根がスクリューになり、軸方向にボールを押してしまう。" +
                 "ねじり30度だと1回転でポケットが31m進む計算になり、" +
                 "実測ではボールが筒の手前（z=8.7）まで押し戻された。" +
                 "ねじり0なら押す力は周方向だけになる。")]
        [SerializeField] private float railEndAngle = 0f;

        [Tooltip("羽根の太さ（半径・m）。角を出さないため CapsuleCollider で作る。")]
        [SerializeField] private float railRadius = 0.04f;

        [Tooltip("羽根の手前側の端の、軸を通す半径（筒の中心から・m）。" +
                 "**筒の壁の中（0.64以上）に入れて、先端をボールから隠すこと。**" +
                 "先端をボールの通り道に出すと、ボールが先端に正面衝突して真後ろへ弾き返される" +
                 "（実測：全投球が z=6.8〜11.4 まで逆走した）。" +
                 "壁の板は 0.60 から外へ 0.10 の厚みがあるので、そこに埋める。")]
        [SerializeField] private float railStartTrackRadius = 0.66f;

        [Tooltip("羽根の奥側の端の、軸を通す半径（筒の中心から・m）。" +
                 "**ボールの中心が通る半径（筒の半径−ボールの半径＝0.49）に合わせる。**" +
                 "壁沿い（0.56）にするとボールの中心より下を押すことになり、" +
                 "押す力が斜め上を向いてボールが打ち上がる（実測 入口で277mm）。" +
                 "ボールの中心の高さに合わせると、押す力が筒の接線方向だけになる。" +
                 "手前から奥へ向かってこの半径まで寄せるので、" +
                 "羽根は壁から少しずつ生えてくる形になり、先端の段差ができない。")]
        [SerializeField] private float railTrackRadius = 0.49f;

        [Header("入口のすぼめる案内")]
        [Tooltip("案内が始まる奥行き（m）。筒より手前の、平らな床の上に置く。")]
        [SerializeField] private float guideStartZ = 3.4f;

        [Tooltip("案内の長さ（m）。筒の入口の手前で終わらせる。")]
        [SerializeField] private float guideLength = 2.4f;

        [Tooltip("案内の手前側の半幅（m）。" +
                 "立ち位置の端（±0.40）のボールが先端の半球に正面衝突しないよう、" +
                 "ボールの半径＋案内の半径より外から始める。")]
        [SerializeField] private float guideStartX = 0.65f;

        [Tooltip("案内の奥側の半幅（m）。ここでボールの中心が収まる範囲が決まる。" +
                 "**寄せすぎると狙いが効かなくなる。** " +
                 "筒に入れる最低限（|x|≒0.11、段差1.0cm）に留めること。")]
        [SerializeField] private float guideEndX = 0.30f;

        [Tooltip("案内の太さ（半径・m）。角を出さないため CapsuleCollider で作る。")]
        [SerializeField] private float guideRadius = 0.08f;

        [Tooltip("案内の軸をボールの中心と同じ高さに置く（床からの高さ・m）。" +
                 "**ここを下げると押す力が斜め上を向いてボールが跳ねる。** " +
                 "ボールの半径（0.11）に合わせること。")]
        [SerializeField] private float guideAxisHeight = 0.11f;

        [Tooltip("案内の奥に付ける「まっすぐな喉」の長さ（m）。" +
                 "**これが無いと、寄せられたボールが横向きの速度を持ったまま筒に入る。** " +
                 "実測では、傾き8.3度の案内だけだと横に約1.2m/s を持って入り、" +
                 "筒の中で壁を駆け上がりすぎて失速・逆走した（8投中4投が時間切れ）。" +
                 "まっすぐな区間を通すと、横向きの速度が壁に吸われてから筒に入る。")]
        [SerializeField] private float guideThroatLength = 1f;

        [Tooltip("入口のすぼめる案内（左）。CapsuleCollider を持たせること。回さない。")]
        [SerializeField] private Transform guideLeft;

        [Tooltip("入口のすぼめる案内（右）。")]
        [SerializeField] private Transform guideRight;

        [Tooltip("まっすぐな喉（左）。CapsuleCollider を持たせること。")]
        [SerializeField] private Transform guideThroatLeft;

        [Tooltip("まっすぐな喉（右）。")]
        [SerializeField] private Transform guideThroatRight;

        [Header("奥の羽根（2組目・逆回り）")]
        [Tooltip("2組目の羽根が始まる奥行き（m）。" +
                 "1組目で壁へ上がった後の位置に置く。ここも先端は壁の中に埋める。")]
        [SerializeField] private float railBStartZ = 7.2f;

        [Tooltip("2組目の羽根の長さ（m）。")]
        [SerializeField] private float railBLength = 0.9f;

        [Tooltip("2組目を1組目と逆向きに回す。" +
                 "打ち消し合って揃うか、2回すくって荒れるかは実測で決める。")]
        [SerializeField] private bool railBReversed = true;

        [Tooltip("2組目の開始角度のずらし（1で一回転ぶん）。")]
        [SerializeField] private float railBPhase = 0f;

        [Header("回転（見た目とレール）")]
        [Tooltip("一回転にかかる時間（秒）。負にすると逆回り。0なら止まる。" +
                 "**見た目だけの値。** ボールの流され方はここでは決まらない。")]
        [SerializeField] private float rotationPeriod = 1f;

        [Tooltip("開始角度のずらし（1で一回転ぶん）。")]
        [SerializeField] private float rotationPhase = 0f;

        [Header("レーンの実寸")]
        [Tooltip("レーンの長さ（m）。")]
        [SerializeField] private float laneLength = 18f;

        [Tooltip("レーンの幅（m）。")]
        [SerializeField] private float laneWidth = 1.05f;

        [Tooltip("床の厚み（m）。")]
        [SerializeField] private float floorThickness = 0.1f;

        [Header("参照")]
        [Tooltip("手前の床。筒が始まるところまで。")]
        [SerializeField] private Transform nearFloor;

        [Tooltip("手前のガター（左）。")]
        [SerializeField] private Transform nearGutterLeft;

        [Tooltip("手前のガター（右）。")]
        [SerializeField] private Transform nearGutterRight;

        [Tooltip("奥の床。筒が終わったところから。ピン台はここに乗る。")]
        [SerializeField] private Transform farFloor;

        [Tooltip("筒の奥をガターではなく「跳ね返る壁」にする。" +
                 "ガターが無くなるので落ちて失敗することは無くなるが、" +
                 "代わりに壁で何回跳ね返るかで結果が変わる。")]
        [SerializeField] private bool useBounceWalls = true;

        [Tooltip("筒の奥の跳ね返る壁（左）。内側の面が床の端とぴったり合うように置く。")]
        [SerializeField] private Transform bounceWallLeft;

        [Tooltip("筒の奥の跳ね返る壁（右）。")]
        [SerializeField] private Transform bounceWallRight;

        [Tooltip("跳ね返る壁の高さ（m）。")]
        [SerializeField] private float bounceWallHeight = 0.5f;

        [Tooltip("跳ね返る壁の厚み（m）。")]
        [SerializeField] private float bounceWallThickness = 0.1f;

        [Tooltip("奥のガター（左）。")]
        [SerializeField] private Transform farGutterLeft;

        [Tooltip("奥のガター（右）。")]
        [SerializeField] private Transform farGutterRight;

        [Tooltip("筒の当たり判定を作る板。底から順に並べる。回さない。")]
        [SerializeField] private Transform[] colliderFacets;

        [Tooltip("見た目の筒をまとめた入れ物。これを回す。")]
        [SerializeField] private Transform visualRoot;

        [Tooltip("羽根をぶら下げる入れ物。筒の中心に置き、見た目と同じ角度で回す。")]
        [SerializeField] private Transform railRoot;

        [Tooltip("静止レールをぶら下げる入れ物。筒の中心に置くが、**回さない**。")]
        [SerializeField] private Transform launchRoot;

        [Tooltip("2組目の羽根をぶら下げる入れ物。1組目と逆向きに回す。")]
        [SerializeField] private Transform railRootB;

        [Tooltip("2組目の羽根。1組目と同じ枚数にすること。")]
        [SerializeField] private Transform[] railsB;

        [Tooltip("跳ね上げの羽根。CapsuleCollider を持たせること。" +
                 "周方向に等間隔で並べる。1本だと回したとき当たり外れの二択になる。")]
        [SerializeField] private Transform[] rails;

        [Tooltip("見た目の筒を作る板。全周ぶん。当たり判定は持たせないこと。")]
        [SerializeField] private Transform[] visualFacets;

        /// <summary>床の上面の高さ（このレーンの座標で）。</summary>
        private float FloorTopY => floorThickness * 0.5f;

        /// <summary>筒の中心の高さ（m）。底が床の上面に一致するように置く。</summary>
        public float CenterY => FloorTopY + radius;

        /// <summary>筒が終わる位置（m）。</summary>
        public float TubeEndZ => tubeStartZ + Mathf.Max(0f, tubeLength);

        /// <summary>当たり判定の板の枚数。</summary>
        public int ColliderFacetCount => colliderFacets != null ? colliderFacets.Length : 0;

        /// <summary>当たり判定の板1枚ぶんの角度（度）。</summary>
        public float ColliderFacetStepDegrees =>
            ColliderFacetCount > 0 ? colliderArcDegrees / ColliderFacetCount : 0f;

        /// <summary>その板の中心の角度（度）。底が0で、正が右。</summary>
        public float ColliderFacetAngle(int index)
        {
            float step = ColliderFacetStepDegrees;
            return -colliderArcDegrees * 0.5f + step * (index + 0.5f);
        }

        /// <summary>当たり判定の端が届く左右の位置（m）。</summary>
        public float ColliderReachX =>
            radius * Mathf.Sin(colliderArcDegrees * 0.5f * Mathf.Deg2Rad);

        /// <summary>当たり判定の端の高さ（床の上面から・m）。</summary>
        public float ColliderReachHeight =>
            radius * (1f - Mathf.Cos(colliderArcDegrees * 0.5f * Mathf.Deg2Rad));

        /// <summary>レーンに入ってからの経過（秒）。</summary>
        private float _time;

        public override void OnLaneStart(LaneContext context)
        {
            base.OnLaneStart(context);
            _time = 0f;
            ApplyTube();
            ApplyRotation();
        }

        /// <summary>
        /// 筒と床を組み立てる。
        /// エディタでは、このコンポーネントを右クリックして実行できる。
        /// </summary>
        [ContextMenu("筒を反映する")]
        public void ApplyTube()
        {
            // 床はレーンの端から端まで1枚で通す。筒の中も床のまま。
            //
            // 理由は2つある。
            // 1. 口を広げると筒の底に隙間が開く。そこを床が埋める。
            //    埋めないとボールが隙間に 4.2mm 沈んでしまう。
            // 2. 床を2枚に割ると、そこに進行方向を横切る継ぎ目ができる。
            //    1枚で通せば、レーン全体で横切る継ぎ目は筒の板の前後だけになる。
            //
            // 床の上面（0.05）は筒の底と同じ高さで、
            // 底から離れた所では筒の面のほうが上にあるので、床は筒の下に隠れる。
            StretchAlongZ(
                nearFloor, 0f, laneLength,
                FloorTopY - floorThickness * 0.5f, laneWidth);
            StretchAlongZ(nearGutterLeft, 0f, tubeStartZ, null, null);
            StretchAlongZ(nearGutterRight, 0f, tubeStartZ, null, null);

            // 奥の床は使わない。床は1枚で通っている
            if (farFloor != null && farFloor.gameObject.activeSelf)
            {
                farFloor.gameObject.SetActive(false);
            }

            StretchAlongZ(farGutterLeft, TubeEndZ, laneLength, null, null);
            StretchAlongZ(farGutterRight, TubeEndZ, laneLength, null, null);

            BuildBounceWalls();
            BuildGuides();

            BuildColliderFacets();
            BuildVisualFacets();
            BuildLaunchRail();
            BuildRail();
        }

        /// <summary>
        /// 当たり判定の板を、筒の内側に沿って並べる。
        ///
        /// 1枚はレーンの全長を通すので、進行方向を横切る継ぎ目ができない。
        /// 横方向の継ぎ目は、底から離れるほど面が上向きに折れる＝すべて凹になる。
        /// </summary>
        private void BuildColliderFacets()
        {
            if (colliderFacets == null)
            {
                return;
            }

            float step = ColliderFacetStepDegrees;

            // 内接で置く。継ぎ目が円の上に乗るので、
            // 筒の底の継ぎ目が床の高さとぴったり一致する
            float width = FacetWidth(radius, step);
            float offset = FacetOffset(radius, step);
            float centerZ = tubeStartZ + tubeLength * 0.5f;

            for (int i = 0; i < colliderFacets.Length; i++)
            {
                PlaceFacet(
                    colliderFacets[i], ColliderFacetAngle(i),
                    offset, plateThickness, width, centerZ, tubeLength,
                    mouthFlare, mouthFlareEnd);
            }
        }

        /// <summary>入口で筒の口が左右へ開く幅（m）。調整の確認用。</summary>
        public float MouthFlare => mouthFlare;

        /// <summary>
        /// その奥行きで、筒の半分がどれだけ横へずれているか（m）。
        /// 入口で <see cref="mouthFlare"/>、筒の終わりで0。
        /// </summary>
        public float FlareAt(float z)
        {
            if (tubeLength <= Mathf.Epsilon)
            {
                return 0f;
            }

            float t = Mathf.Clamp01((TubeEndZ - z) / tubeLength);
            return Mathf.Lerp(mouthFlareEnd, mouthFlare, t);
        }

        /// <summary>見た目の筒を全周ぶん並べる。当たり判定は持たせない。</summary>
        private void BuildVisualFacets()
        {
            if (visualRoot != null)
            {
                visualRoot.localPosition = new Vector3(0f, CenterY, tubeStartZ + tubeLength * 0.5f);
            }

            if (visualFacets == null || visualFacets.Length == 0)
            {
                return;
            }

            int count = visualFacets.Length;
            float step = 360f / count;
            float visualRadius = radius + visualGap;
            float width = FacetWidth(visualRadius, step);
            float offset = FacetOffset(visualRadius, step);

            for (int i = 0; i < count; i++)
            {
                // 見た目は回る入れ物の子なので、中心は原点として置く
                // 見た目は回るので広げない
                PlaceFacet(
                    visualFacets[i], step * i,
                    offset, visualThickness, width, 0f, tubeLength, 0f, 0f);
            }
        }

        /// <summary>
        /// 板1枚の幅（m）。**内接**（継ぎ目が円の上に乗る）ときの値。
        ///
        /// ── 外接と内接の違い（曲面を板で近似するときの基本）──────────
        ///
        /// **外接**：板が接点で円に接する。継ぎ目は円の外に出る。
        ///   幅は接線の長さ 2R·tan(角度/2)。弦にすると隣に届かず隙間が空く。
        ///   凹の面では、継ぎ目が理想の円より最大 R·(1/cos(角度/2)−1) だけ深くなる。
        ///
        /// **内接**：継ぎ目が円の上に乗る。板の中央が円の内側へ寄る。
        ///   幅は弦の長さ 2R·sin(角度/2)。中心までの距離は R·cos(角度/2)。
        ///   凹の面では、板の中央が理想の円より最大 R·(1−cos(角度/2)) だけ浅くなる。
        ///
        /// **凹の面を作るなら内接を使う。**
        /// 継ぎ目が円の上に乗るので、筒の底の継ぎ目が床の高さとぴったり一致し、
        /// 床から筒へ移るところに段差ができない。
        /// 外接だと底の継ぎ目が床より少し低くなり、そこに段差ができる。
        ///
        /// どちらの置き方でも継ぎ目の折れ角は同じ（すべて凹）で、凸の角はできない。
        /// </summary>
        private static float FacetWidth(float surfaceRadius, float stepDegrees)
        {
            return 2f * surfaceRadius * Mathf.Sin(stepDegrees * 0.5f * Mathf.Deg2Rad);
        }

        /// <summary>
        /// 板の面の中心が、筒の中心からどれだけ離れるか（m）。**内接**のときの値。
        /// 継ぎ目を円の上に乗せるため、面の中心は半径より内側へ寄る。
        /// </summary>
        private static float FacetOffset(float surfaceRadius, float stepDegrees)
        {
            return surfaceRadius * Mathf.Cos(stepDegrees * 0.5f * Mathf.Deg2Rad);
        }

        /// <summary>
        /// 板を1枚、筒の内側に接するように置く。
        /// 面は筒の中心を向き、板の厚みぶんは外側へ逃がす。
        /// </summary>
        /// <param name="angleDegrees">底を0とした角度。正が右。</param>
        /// <param name="surfaceOffset">面の中心が筒の中心から離れる距離（m）。内接なので半径より内側。</param>
        /// <param name="centerZ">板の中心の奥行き。見た目は回る入れ物の子なので0を渡す。</param>
        /// <param name="flare">
        /// 入口で筒の半分を横へずらす幅（m）。0で真円の筒。
        ///
        /// ── これが漏斗の正体 ──────────────────────────
        ///
        /// 板を継ぎ足して漏斗にすると、**継ぎ目ごとに新しい先端の小口ができる**。
        /// 小口の高さが分担されて小さくなるだけで、ゼロにはならない。
        /// 3分割しても 0.52mm の小口が残り、そこで数百mm/s 弾かれる。
        ///
        /// そこで板は1枚のまま、**入口側だけ左右へ開く**。
        /// 右半分を +flare、左半分を −flare ずらし、奥へ行くほど0に近づける。
        /// ずれが奥行きに比例するので、板はY軸まわりに少し振るだけで表せる（＝平面のまま）。
        ///
        /// 効果：
        /// ・板の先端の小口が x=±flare へ逃げ、ボールの表面から離れる
        ///   （flare=0 では 0.98mm めり込む。0.03m なら 4.0mm 離れる）
        /// ・底のV字が閉じるのに1m以上かかるので、
        ///   平らな床（中心 y=0.160）から筒のV字（中心 y=0.16094）への
        ///   0.94mm の持ち上がりが、毎秒5mm まで薄まる
        ///
        /// 開いたぶん底に隙間が空くが、そこは床が埋めている。
        /// </param>
        private void PlaceFacet(
            Transform facet, float angleDegrees, float surfaceOffset,
            float thickness, float width, float centerZ, float length,
            float flare, float flareEnd)
        {
            if (facet == null || length <= Mathf.Epsilon)
            {
                return;
            }

            float radians = angleDegrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);

            // 見た目は回る入れ物の子なので中心が原点。当たり判定は筒の中心の高さを足す
            float baseY = centerZ > 0f ? CenterY : 0f;

            // 板の中心の奥行きでの開き幅。板は入口で外、奥で内に寄る
            float side = angleDegrees >= 0f ? 1f : -1f;
            float shift = side * (flare + flareEnd) * 0.5f;

            // 面の両側の縁（断面での位置）。ここが隣の板と接する線になる
            var surfaceCenter = new Vector3(
                surfaceOffset * sin + shift, baseY - surfaceOffset * cos, centerZ);
            var edgeStep = new Vector3(cos, sin, 0f) * (width * 0.5f);
            Vector3 inner = surfaceCenter - edgeStep;
            Vector3 outer = surfaceCenter + edgeStep;

            // 縁の走る向き。開きが奥へ行くほど狭まるので、縁は斜めに走る。
            // 隣の板との継ぎ目もこの向きに走るので、箱の辺をこれに合わせれば食い違わない
            float slide = -side * (flare - flareEnd) / length;
            Vector3 along = new Vector3(slide, 0f, 1f).normalized;

            // 幅は「縁の向きに直角な成分」だけ。斜めに走るぶんを幅に数えない
            Vector3 across = outer - inner;
            Vector3 acrossPerp = across - along * Vector3.Dot(across, along);
            float realWidth = acrossPerp.magnitude;
            if (realWidth <= Mathf.Epsilon)
            {
                return;
            }

            // 面の向き。筒の中心側を向かせる。
            // 全周ぶん作ると上側の板も出るので、「上向き」ではなく「中心向き」で判定する
            Vector3 inward = new Vector3(-sin, cos, 0f);
            Vector3 normal = Vector3.Cross(along, acrossPerp).normalized;
            if (Vector3.Dot(normal, inward) < 0f)
            {
                normal = -normal;
            }

            // 箱は長方形なので、平行四辺形の斜めのぶんは端がずれる。
            // 奥行きを少し伸ばして、筒の範囲を確実に覆う
            float realLength = length * Mathf.Sqrt(1f + slide * slide)
                + Mathf.Abs(Vector3.Dot(across, along)) + 0.05f;

            Vector3 surfaceMid = (inner + outer) * 0.5f;
            facet.localPosition = surfaceMid - normal * (thickness * 0.5f);
            facet.localRotation = Quaternion.LookRotation(along, normal);
            facet.localScale = new Vector3(realWidth, thickness, realLength);
        }

        /// <summary>
        /// 筒の手前に、ボールを中央へ寄せる案内を置く。
        ///
        /// ── なぜ要るか ────────────────────────────
        ///
        /// 筒の床は底から離れるほど高い。|x|=0.35 では通常の床より 11.3cm 高いので、
        /// 端に立って投げたボールは筒の入口の壁に正面衝突して止まる（実測：3投とも停止）。
        /// 立ち位置の可動範囲は ±0.40 なので、これは実際に出せる投球。
        ///
        /// ── なぜカプセルで、なぜ浮かせるか ──────────────────
        ///
        /// カプセルは円柱と両端の半球だけでできていて角が無い。
        /// 角はボールが触れていなくても蹴るので、この案件では角のある形は使えない。
        ///
        /// 軸をボールの中心と同じ高さに置くと、押す力が真横だけになる。
        /// 床に寝かせる（軸が低い）と押す力が斜め上を向き、ボールが跳ねる。
        /// 底との隙間は3cmしか空かないので、ボールが下へ潜ることはない。
        /// </summary>
        private void BuildGuides()
        {
            float y = FloorTopY + guideAxisHeight;
            float convergeEndZ = guideStartZ + guideLength;

            // すぼめる区間
            PlaceCapsule(guideLeft,
                new Vector3(-guideStartX, y, guideStartZ),
                new Vector3(-guideEndX, y, convergeEndZ), guideRadius);
            PlaceCapsule(guideRight,
                new Vector3(guideStartX, y, guideStartZ),
                new Vector3(guideEndX, y, convergeEndZ), guideRadius);

            // まっすぐな喉。すぼめる区間の端と同じ点から始めるので、
            // 両端の半球が重なって継ぎ目にすき間ができない
            PlaceCapsule(guideThroatLeft,
                new Vector3(-guideEndX, y, convergeEndZ),
                new Vector3(-guideEndX, y, convergeEndZ + guideThroatLength), guideRadius);
            PlaceCapsule(guideThroatRight,
                new Vector3(guideEndX, y, convergeEndZ),
                new Vector3(guideEndX, y, convergeEndZ + guideThroatLength), guideRadius);
        }

        /// <summary>案内の傾き（度）。横向きに与える速度の大きさを決める。</summary>
        public float GuideAngleDegrees =>
            guideLength > Mathf.Epsilon
                ? Mathf.Atan2(guideStartX - guideEndX, guideLength) * Mathf.Rad2Deg
                : 0f;

        /// <summary>まっすぐな喉が終わる奥行き（m）。</summary>
        public float GuideThroatEndZ => guideStartZ + guideLength + guideThroatLength;

        /// <summary>案内の出口で、ボールの中心が収まる左右の幅（m）。</summary>
        public float GuideExitHalfWidth => Mathf.Max(0f, guideEndX - guideRadius - 0.11f);

        /// <summary>
        /// 筒の奥に、跳ね返る壁を置く。
        ///
        /// 壁の内側の面を床の端（レーンの幅の半分）とぴったり合わせるので、
        /// ボールはガターへ落ちずに壁で跳ね返る。
        /// 筒を手前に置くと奥が長く空くので、そこを跳ね返りながら進む区間にする。
        ///
        /// 壁を使うときは奥のガターを消す。見えていても届かないので紛らわしい。
        /// </summary>
        private void BuildBounceWalls()
        {
            if (farGutterLeft != null)
            {
                farGutterLeft.gameObject.SetActive(!useBounceWalls);
            }

            if (farGutterRight != null)
            {
                farGutterRight.gameObject.SetActive(!useBounceWalls);
            }

            PlaceBounceWall(bounceWallLeft, -1f);
            PlaceBounceWall(bounceWallRight, 1f);
        }

        /// <summary>跳ね返る壁を1枚置く。</summary>
        private void PlaceBounceWall(Transform wall, float side)
        {
            if (wall == null)
            {
                return;
            }

            wall.gameObject.SetActive(useBounceWalls);
            if (!useBounceWalls)
            {
                return;
            }

            float length = Mathf.Max(0f, laneLength - TubeEndZ);

            // 内側の面を床の端に合わせる。厚みのぶんだけ外へ逃がす
            wall.localPosition = new Vector3(
                side * (laneWidth * 0.5f + bounceWallThickness * 0.5f),
                FloorTopY + bounceWallHeight * 0.5f,
                TubeEndZ + length * 0.5f);
            wall.localRotation = Quaternion.identity;
            wall.localScale = new Vector3(bounceWallThickness, bounceWallHeight, length);
        }

        /// <summary>
        /// 跳ね上げのレールを、筒の内壁に沿わせて置く。
        ///
        /// ── なぜカプセルなのか ──────────────────────────
        ///
        /// 6本目と7本目で繰り返し分かったのは、
        /// **角はボールが触れていなくても蹴る**ということ。
        /// Unity は接触予測（両者の contactOffset の和＝2cm）の範囲に入った角に対し、
        /// 前進速度を打ち消す向きの力を出す。その向きが上向きなら跳ね上がる。
        /// 実測では、角から 4.6mm 離れた位置で +0.713m/s 弾かれた。
        ///
        /// CapsuleCollider は円柱と両端の半球だけでできていて、角が一つも無い。
        /// だから、どこで当たっても滑らかに逸れる。
        ///
        /// ── なぜ長いのか ────────────────────────────
        ///
        /// 短いレールだと、当たった瞬間の回転の位相で結果が大きく変わる。
        /// 長くして面に沿わせれば、ボールは乗っている間ずっと押されるので、
        /// 位相の違いが均されて結果が連続的になる（5本目の円盤と同じ考え方）。
        ///
        /// ── なぜ1本ではなく複数枚（プロペラ）なのか ──────────────
        ///
        /// 1本のレールを回すと、**当たるか外れるかの二択**になる。
        /// 実測では、同じ投球（立0.000・速8・周期1秒）を4回投げて
        /// 3回が回転0度（完全に素通り）、1回が+45度だった。
        /// レールが底を通る時間は一周のうち羽根の角度ぶんしかないので、
        /// ボールが筒を抜ける0.5秒のあいだに底へ来るかどうかが運任せになる。
        ///
        /// そこで羽根を周方向に等間隔で並べる。
        /// ボールは直径22cmで、半径0.6mの壁の上では約21度ぶんを占めるので、
        /// 羽根の間隔をその前後まで詰めると、
        /// ボールは常にどれかの羽根に挟まれた状態になり、二択が消える。
        ///
        /// ── 弦のたわみ ─────────────────────────────
        ///
        /// カプセルは直線なので、両端を筒の内壁に置くと真ん中が内側へ寄る。
        /// たわみは 軸までの距離×(1−cos(角度差/2))。
        /// 角度差50度なら 0.55×(1−cos25°)=5.2cm で、レールの太さとほぼ同じ。
        /// これ以上広げると真ん中が壁から浮き、ボールが下に潜る隙間ができる。
        /// もっと巻きたければレールを継ぎ足す（段階を分ける）。
        /// </summary>
        private void BuildRail()
        {
            if (railRoot != null)
            {
                railRoot.localPosition = new Vector3(0f, CenterY, 0f);
            }

            if (rails == null || rails.Length == 0)
            {
                return;
            }

            // 手前は壁の中に隠し、奥へ行くほどボールの中心の高さへ寄せる
            float spacing = 360f / rails.Length;

            for (int i = 0; i < rails.Length; i++)
            {
                float offset = spacing * i;
                PlaceBlade(
                    rails[i], railStartAngle + offset, railEndAngle + offset,
                    railStartZ, railLength,
                    railStartTrackRadius, railTrackRadius, railRadius);
            }

            BuildRailB();
        }

        /// <summary>
        /// 2組目の羽根を、1組目の奥に置く。
        ///
        /// 1組目が底からすくって壁へ上げ、2組目が壁の上で反対向きに振る。
        /// 2組目は既にボールが壁にいる位置から始まるので、
        /// 「底にいるボールに先端をぶつけない」原則は1組目より守りやすい。
        /// </summary>
        private void BuildRailB()
        {
            if (railRootB != null)
            {
                railRootB.localPosition = new Vector3(0f, CenterY, 0f);
            }

            if (railsB == null || railsB.Length == 0)
            {
                return;
            }

            float spacing = 360f / railsB.Length;
            for (int i = 0; i < railsB.Length; i++)
            {
                float offset = spacing * i;
                PlaceBlade(
                    railsB[i], railStartAngle + offset, railEndAngle + offset,
                    railBStartZ, railBLength,
                    railStartTrackRadius, railTrackRadius, railRadius);
            }
        }

        /// <summary>2組目の羽根が壁から顔を出しはじめる奥行き（m）。</summary>
        public float RailBEmergeZ
        {
            get
            {
                float emergeRadius = radius - railRadius;
                float span = railStartTrackRadius - railTrackRadius;
                if (span <= Mathf.Epsilon)
                {
                    return railBStartZ;
                }

                float t = Mathf.Clamp01((railStartTrackRadius - emergeRadius) / span);
                return railBStartZ + railBLength * t;
            }
        }

        /// <summary>
        /// 入口の静止レールを置く。
        ///
        /// これは**回さない**。回すと、底を這っているボールに当たるかどうかが
        /// 回転の位相任せになり、同じ投球が 0度 と +45度 に割れる（実測）。
        /// 静止させると、速さにも立ち位置にも単調に反応し、跳びが無くなる（実測）。
        ///
        /// 役目は「ボールを筒の底から壁へ上げること」だけ。
        /// 壁へ上がってしまえば、そこから先はプロペラが受け持つ。
        /// </summary>
        private void BuildLaunchRail()
        {
            if (launchRoot != null)
            {
                launchRoot.localPosition = new Vector3(0f, CenterY, 0f);
                launchRoot.localRotation = Quaternion.identity;   // 回さない
            }

            float axisRadius = Mathf.Clamp(launchTrackRadius, 0.01f, radius - launchRadius);
            PlaceBlade(
                launchRail, launchStartAngle, launchEndAngle,
                launchStartZ, launchLength, axisRadius, axisRadius, launchRadius);
        }

        /// <summary>
        /// カプセル1本を、筒の中の螺旋に沿って置く。
        /// 静止レールもプロペラの羽根も、置き方は同じ。
        /// </summary>
        private void PlaceBlade(
            Transform blade, float startAngle, float endAngle,
            float startZ, float length, float startRadius, float endRadius, float bladeRadius)
        {
            if (blade == null || length <= Mathf.Epsilon)
            {
                return;
            }

            Vector3 head = RailAxisPoint(startAngle, startZ, startRadius);
            Vector3 tail = RailAxisPoint(endAngle, startZ + length, endRadius);
            PlaceCapsule(blade, head, tail, bladeRadius);
        }

        /// <summary>
        /// カプセル1本を、2点を結ぶ位置に置く。
        /// 親は Scale 1 のままにして、当たり判定に実寸を入れる（プロジェクトの取り決め）。
        /// </summary>
        private void PlaceCapsule(Transform target, Vector3 head, Vector3 tail, float capsuleRadius)
        {
            if (target == null)
            {
                return;
            }

            Vector3 axis = tail - head;
            float axisLength = axis.magnitude;
            if (axisLength <= Mathf.Epsilon)
            {
                return;
            }

            target.localPosition = (head + tail) * 0.5f;
            target.localRotation = Quaternion.LookRotation(axis / axisLength, Vector3.up);
            target.localScale = Vector3.one;

            var capsule = target.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.center = Vector3.zero;
                capsule.direction = 2;       // ローカルZが軸
                capsule.radius = capsuleRadius;
                capsule.height = axisLength + capsuleRadius * 2f;
            }

            // 見た目は子。物理には関わらせない
            var visual = target.Find("Visual");
            if (visual != null)
            {
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.Euler(90f, 0f, 0f);
                visual.localScale = new Vector3(
                    capsuleRadius * 2f, (axisLength + capsuleRadius * 2f) * 0.5f, capsuleRadius * 2f);
            }
        }

        /// <summary>羽根の枚数。</summary>
        public int RailCount => rails != null ? rails.Length : 0;

        /// <summary>
        /// 羽根と羽根のあいだの角度（度）。
        /// ボールは直径22cmで、半径0.6mの壁の上では約21度ぶんを占める。
        /// この間隔を21度前後まで詰めると、ボールは常にどれかの羽根に挟まれた状態になり、
        /// 「当たるか外れるか」の二択が消える。
        /// </summary>
        public float RailSpacingDegrees => RailCount > 0 ? 360f / RailCount : 0f;

        /// <summary>レールの軸の端の位置（回る入れ物の中での座標）。</summary>
        private Vector3 RailAxisPoint(float angleDegrees, float z, float axisRadius)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector3(
                axisRadius * Mathf.Sin(radians), -axisRadius * Mathf.Cos(radians), z);
        }

        /// <summary>羽根の弦が、本来の円弧から内側へずれる量（m）。調整の目安。</summary>
        public float RailSag =>
            railTrackRadius *
            (1f - Mathf.Cos((railEndAngle - railStartAngle) * 0.5f * Mathf.Deg2Rad));

        /// <summary>静止レールの弦が、本来の円弧から内側へずれる量（m）。</summary>
        public float LaunchSag =>
            launchTrackRadius *
            (1f - Mathf.Cos((launchEndAngle - launchStartAngle) * 0.5f * Mathf.Deg2Rad));

        /// <summary>静止レールが終わる奥行き（m）。ここから先がプロペラの領分。</summary>
        public float LaunchEndZ => launchStartZ + launchLength;

        /// <summary>
        /// 羽根が筒の壁から顔を出しはじめる奥行き（m）。
        /// ここより手前では羽根は壁の中に隠れていて、ボールには触れない。
        /// </summary>
        public float RailEmergeZ
        {
            get
            {
                float emergeRadius = radius - railRadius;   // ここより内側に入ると顔を出す
                float span = railStartTrackRadius - railTrackRadius;
                if (span <= Mathf.Epsilon)
                {
                    return railStartZ;
                }

                float t = Mathf.Clamp01((railStartTrackRadius - emergeRadius) / span);
                return railStartZ + railLength * t;
            }
        }

        /// <summary>羽根が始まる奥行き（m）。</summary>
        public float RailStartZ => railStartZ;

        /// <summary>羽根がボールを押す速さ（m/s）。周方向へ回す力の源。</summary>
        public float RailSurfaceSpeed =>
            Mathf.Abs(rotationPeriod) > Mathf.Epsilon
                ? 2f * Mathf.PI * railTrackRadius / Mathf.Abs(rotationPeriod)
                : 0f;

        /// <summary>レーンの毎フレーム更新。見た目の筒とレールを回す。</summary>
        protected override void OnLaneFixedUpdate(float deltaTime)
        {
            _time += deltaTime;
            ApplyRotation();
        }

        /// <summary>
        /// 見た目の筒とレールの向きを、今の時刻に合わせる。
        /// レールは筒に付いているように見せたいので、同じ角度で回す。
        /// </summary>
        private void ApplyRotation()
        {
            float angle = ObstacleMotion.GetAngle(_time, rotationPeriod, rotationPhase);
            var spin = Quaternion.Euler(0f, 0f, angle);

            if (visualRoot != null)
            {
                visualRoot.localRotation = spin;
            }

            if (railRoot != null)
            {
                railRoot.localRotation = spin;
            }

            if (railRootB != null)
            {
                float periodB = railBReversed ? -rotationPeriod : rotationPeriod;
                float angleB = ObstacleMotion.GetAngle(_time, periodB, railBPhase);
                railRootB.localRotation = Quaternion.Euler(0f, 0f, angleB);
            }

            // launchRoot は回さない。回すと当たり外れの二択に戻る
        }

        /// <summary>
        /// 奥行きだけを、始まりと終わりに合わせて伸ばす。
        /// centerY に値を渡すと高さも、width に値を渡すと幅も合わせる。
        /// </summary>
        private void StretchAlongZ(
            Transform target, float startZ, float endZ, float? centerY, float? width)
        {
            if (target == null)
            {
                return;
            }

            float length = Mathf.Max(0f, endZ - startZ);

            Vector3 scale = target.localScale;
            scale.z = length;
            if (centerY.HasValue) { scale.y = floorThickness; }
            if (width.HasValue) { scale.x = width.Value; }
            target.localScale = scale;

            Vector3 position = target.localPosition;
            position.z = startZ + length * 0.5f;
            if (centerY.HasValue) { position.y = centerY.Value; }
            target.localPosition = position;
            target.localRotation = Quaternion.identity;
        }

        /// <summary>組み上がった形の数値を Console に出す。調整の目安にする。</summary>
        [ContextMenu("筒の形を書き出す")]
        public void LogProfile()
        {
            ApplyTube();

            var text = new System.Text.StringBuilder();
            text.AppendLine(
                $"筒：z={tubeStartZ:F2}→{TubeEndZ:F2}（長さ {tubeLength:F2}m）／ 半径 {radius:F2}m ／ " +
                $"中心の高さ {CenterY:F3}");
            text.AppendLine(
                $"当たり判定：{colliderArcDegrees:F0}度ぶん・{ColliderFacetCount}枚（1枚 {ColliderFacetStepDegrees:F1}度）／ " +
                $"端は x=±{ColliderReachX:F3} 高さ {ColliderReachHeight * 100f:F1}cm");
            text.AppendLine(
                $"口の開き：入口 ±{mouthFlare * 100f:F1}cm → 筒の終わり ±{mouthFlareEnd * 100f:F1}cm");
            text.AppendLine(
                $"見た目：{(visualFacets != null ? visualFacets.Length : 0)}枚・一回転 {rotationPeriod:F1}秒");
            text.AppendLine(
                $"静止レール：z={launchStartZ:F2}→{LaunchEndZ:F2}（{launchLength:F2}m）／ " +
                $"{launchStartAngle:F0}度→{launchEndAngle:F0}度 ／ 軸の半径 {launchTrackRadius:F2}m ／ " +
                $"太さ半径 {launchRadius * 100f:F1}cm ／ 弦のずれ {LaunchSag * 100f:F1}cm");
            text.AppendLine(
                $"羽根：{RailCount}枚・{RailSpacingDegrees:F1}度おき ／ " +
                $"z={railStartZ:F2}→{railStartZ + railLength:F2}（{railLength:F2}m）／ " +
                $"ねじり {railEndAngle - railStartAngle:F0}度 ／ 軸の半径 {railTrackRadius:F2}m ／ " +
                $"太さ半径 {railRadius * 100f:F1}cm ／ 押す速さ {RailSurfaceSpeed:F2}m/s");
            text.AppendLine(
                $"羽根の軸の半径：手前 {railStartTrackRadius:F2}m（壁の中）→ 奥 {railTrackRadius:F2}m ／ " +
                $"壁から顔を出す位置 z={RailEmergeZ:F2}");
            text.AppendLine(
                $"入口の案内：すぼめる z={guideStartZ:F2}→{guideStartZ + guideLength:F2}" +
                $"（±{guideStartX:F2}m→±{guideEndX:F2}m・傾き {GuideAngleDegrees:F1}度）" +
                $"＋ まっすぐな喉 →{GuideThroatEndZ:F2} ／ 太さ半径 {guideRadius * 100f:F0}cm ／ " +
                $"出口でボールの中心が収まる幅 ±{GuideExitHalfWidth:F3}m");
            text.AppendLine(
                $"2組目の羽根：{(railsB != null ? railsB.Length : 0)}枚 ／ " +
                $"z={railBStartZ:F2}→{railBStartZ + railBLength:F2} ／ " +
                $"{(railBReversed ? "逆回り" : "同じ向き")} ／ 壁から8cm出る位置 z={RailBEmergeZ:F2}");
            text.AppendLine(
                $"筒の奥：{(useBounceWalls ? $"跳ね返る壁（z={TubeEndZ:F2}→{laneLength:F2}・内側の面 x=±{laneWidth * 0.5f:F3}）" : "ガター")}");
            text.AppendLine(
                $"レールの終わり {LaunchEndZ:F2} ／ 羽根の始まり {railStartZ:F2} ／ " +
                $"すきま {(railStartZ - LaunchEndZ) * 100f:F0}cm" +
                $"{(railStartZ < LaunchEndZ ? "　★重なっている" : string.Empty)}");

            Debug.Log(text.ToString(), this);
        }
    }
}
