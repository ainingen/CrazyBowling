using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 助走に置くコブ。床に埋めた球の頭だけを出す。
    ///
    /// **板を並べて作らないこと。** 板は必ず継ぎ目の角を作り、
    /// ボールがわずかに浮いた状態でその角を拾うと大きく弾かれる。
    /// このレーンで繰り返し起きた問題はすべてそれが原因だった。
    ///
    /// 球には辺も角も無い。露出する頭の部分は完全に滑らかな連続曲面になる。
    /// 床から球へ上がる継ぎ目は凹（面が上向きに折れる）なので、
    /// 確立した回避条件をそのまま満たす。
    /// 頂上付近は凸だが、これは角ではなく曲面で、
    /// ボールが離れるのは設計どおり（それがコブの働き）。離れ方は半径で決まり連続。
    /// </summary>
    [System.Serializable]
    public class Bump
    {
        [Tooltip("置く位置（レーンの手前からの奥行き・m）。")]
        public float z = 6f;

        [Tooltip("置く位置（左右・m）。負で左。左右対称に並べないこと。" +
                 "対称だと左右どちらでも同じ結果になり、探る意味が薄れる。")]
        public float x = 0f;

        [Tooltip("球の半径（m）。大きいほど広くて緩いコブになる。" +
                 "露出する頭の直径は 2×√(2×半径×高さ)。")]
        public float radius = 2f;

        [Tooltip("床から出る高さ（m）。最大の傾きは asin(√(2×高さ÷半径))。" +
                 "高くするほどボールが大きく弾かれる。控えめから始めること。")]
        public float height = 0.02f;

        [Tooltip("当たり判定の球。SphereCollider を付けたもの。")]
        public Transform body;

        [Tooltip("見た目の潰した球。当たり判定は持たせないこと。")]
        public Transform visual;
    }

    /// <summary>
    /// 途中で床が途切れ、ボールが飛び越えるレーン。
    /// 助走にはコブが散らしてあり、通る位置と強さで飛び方が変わる。
    ///
    /// ── 落ちるのではなく、跳ぶ ────────────────────────
    ///
    /// はじめは「床を切って落差を付けるだけ」で作った。しかしそれは
    /// 落下であってジャンプではない。ボールが上向きの速度を持たないので、
    /// 着地側に受け皿（面取りの斜面）が要り、その斜面が落ちかけた球を拾って
    /// 打ち上げ、速度を奪っていた。
    /// 結果、引き幅33〜71%の広い帯が「渡れるがピン入射1.0〜1.3m/s」になり、
    /// プレイヤーから見て「渡ったのに倒れない」という理由の分からない状態になった。
    ///
    /// そこで踏切に台を作り、ボールが自分で上向きの速度を持って飛ぶ形にした。
    /// 着地側に受け皿が要らなくなり、平らな床にそのまま着地する。
    ///
    /// ── 面の角を踏む問題（このレーンの歴史のほとんど）──────────
    ///
    /// 床を複数の面で作ると、ボールが「次の面の上面の後ろ角」を拾って上へ弾かれる。
    /// 実測では床から2.2mm浮いて飛んでいる最中に、1ステップで上向き2.6m/sを得た。
    /// 角はボール表面から9.7mmの距離にあり、接触とみなす距離
    /// （両者の contactOffset の和＝0.02m）の内側だった。
    /// **継ぎ目の段差が0.00mmでも起きる。**
    /// 効かなかった対処：maxDepenetrationVelocity を下げる／面を前へ重ねる（検証済み）。
    ///
    /// 回避条件はひとつだけ。
    /// **凸（下りが急になる側）では避けられない。凹では起きない。**
    /// ただし正確には「凹ではボールが浮かないので角に届かない」であって、
    /// 別の理由で浮けば凹の継ぎ目でも角を拾う。
    ///
    /// よって踏切の台は**凸の区間を一切作らない**。
    /// 平らな床から凹の弧だけで上り、リップへ繋ぐ。下る区間は無い。
    /// **コブは板ではなく球で作る。** 球には角が無いので、この問題自体が起きない。
    ///
    /// ── 左右の差はコブで付ける ──────────────────────
    ///
    /// 全力ストレートがいつでも正解だと、引き幅を最大にするだけのレーンになる。
    ///
    /// はじめは踏切の角度を左右で変えようとしたが**効かなかった**。
    /// 踏切手前の浮きが射出を支配していて、リップを10度から4度に寝かせても
    /// 射出時の上向き速度が 2.07 → 1.96 m/s としか変わらなかった。
    /// 着地側の縁を帯ごとにずらす案も、飛距離が引き幅に対して単調にならず
    /// （79%で3.67m、86%で2.56m）、設定の基準が作れなかった。
    ///
    /// 代わりに助走にコブを置く。物理は決定的でコブの位置も固定なので、
    /// 同じ投球は同じ結果を返す。運ではなく
    /// **立ち位置と強さの組み合わせを探るレーン**になる。
    /// 速度が違えばコブの当たり方も変わるので、両方の軸が効く。
    /// </summary>
    public class GapLane : BasicLane
    {
        [Header("隙間")]
        [Tooltip("隙間が始まる位置（レーンの手前からの奥行き・m）。踏切の位置そのもの。" +
                 "オイルの終わり（10m）より手前に置くこと。奥に置くと空中で曲がる。")]
        [SerializeField] private float gapStartZ = 8.5f;

        [Tooltip("隙間の幅（m）。")]
        [SerializeField] private float gapWidth = 2.2f;

        [Header("踏切の台")]
        [Tooltip("リップ（射出角を決める一枚板）の長さ（m）。ここだけは曲面にしない。")]
        [SerializeField] private float lipLength = 0.3f;

        [Tooltip("リップの傾き（度）。これが射出角になる。")]
        [SerializeField] private float lipAngleDegrees = 10f;

        [Tooltip("凹の弧の水平方向の長さ（m）。")]
        [SerializeField] private float arcSpan = 0.538f;

        [Tooltip("凹の弧を作る板。手前から順に並べる。" +
                 "1枚がボールの半径（0.11m）より短くならない枚数にすること。")]
        [SerializeField] private Transform[] arcPlates;

        [Tooltip("リップ（射出角を決める一枚板）。")]
        [SerializeField] private Transform lip;

        [Tooltip("踏切の縁に置く目印の帯。当たり判定は持たせないこと。")]
        [SerializeField] private Transform takeoffEdge;

        [Header("助走のコブ")]
        [Tooltip("助走に散らすコブ。左右対称に並べないこと。")]
        [SerializeField] private Bump[] bumps;

        [Header("着地側")]
        [Tooltip("着地側の床の、上面の手前を削る長さ（m）。垂直な前面は残す。")]
        [SerializeField] private float landingChamferLength = 0.25f;

        [Tooltip("削る角度（度）。2〜3度の緩い下り。" +
                 "ここだけは構造上どうしても凸の継ぎ目になるので、角度を小さく保つこと。")]
        [SerializeField] private float landingChamferDegrees = 2.5f;

        [Header("レーンの実寸")]
        [Tooltip("レーンの長さ（m）。")]
        [SerializeField] private float laneLength = 18f;

        [Tooltip("レーンの幅（m）。")]
        [SerializeField] private float laneWidth = 1.05f;

        [Tooltip("床の厚み（m）。")]
        [SerializeField] private float floorThickness = 0.1f;

        [Tooltip("板を前方へ重ねる長さ（m）。前の板の先端の角を、次の板の下に埋めるためのもの。" +
                 "後方へは伸ばさないこと。")]
        [SerializeField] private float plateOverlap = 0.05f;

        [Header("参照")]
        [Tooltip("手前の床。台が始まるところまで。")]
        [SerializeField] private Transform nearFloor;

        [Tooltip("手前のガター（左）。")]
        [SerializeField] private Transform nearGutterLeft;

        [Tooltip("手前のガター（右）。")]
        [SerializeField] private Transform nearGutterRight;

        [Tooltip("着地側をまとめた入れ物。落差は無いので高さは0のまま。")]
        [SerializeField] private Transform farSideRoot;

        [Tooltip("着地側の床。面取りの奥から。")]
        [SerializeField] private Transform farFloor;

        [Tooltip("着地側の縁の面取り。上面の手前を緩く削るだけの板。")]
        [SerializeField] private Transform landingChamfer;

        [Tooltip("着地側のガター（左）。")]
        [SerializeField] private Transform farGutterLeft;

        [Tooltip("着地側のガター（右）。")]
        [SerializeField] private Transform farGutterRight;

        [Tooltip("目印の帯の厚み（m）と長さ（m）。")]
        [SerializeField] private Vector2 edgeMarkSize = new Vector2(0.006f, 0.08f);

        /// <summary>床の上面の高さ（このレーンの座標で）。</summary>
        private float FloorTopY => floorThickness * 0.5f;

        /// <summary>リップの傾き（ラジアン）。</summary>
        private float LipAngle => lipAngleDegrees * Mathf.Deg2Rad;

        /// <summary>凹の弧の半径（m）。弧の長さと角度から決まる。</summary>
        public float ArcRadius
        {
            get
            {
                float sin = Mathf.Sin(LipAngle);
                return sin > Mathf.Epsilon ? arcSpan / sin : 0f;
            }
        }

        /// <summary>凹の弧で上がる高さ（m）。</summary>
        public float ArcRise => ArcRadius * (1f - Mathf.Cos(LipAngle));

        /// <summary>リップで上がる高さ（m）。</summary>
        public float LipRise => lipLength * Mathf.Tan(LipAngle);

        /// <summary>踏切が床の上面より高い量（m）。</summary>
        public float TakeoffRise => ArcRise + LipRise;

        /// <summary>凹の弧が始まる位置（m）。</summary>
        public float ArcStartZ => gapStartZ - lipLength - arcSpan;

        /// <summary>リップが始まる位置（m）。</summary>
        public float LipStartZ => gapStartZ - lipLength;

        /// <summary>隙間が終わる位置（m）。</summary>
        public float GapEndZ => gapStartZ + Mathf.Max(0f, gapWidth);

        /// <summary>コブの数。</summary>
        public int BumpCount => bumps != null ? bumps.Length : 0;

        /// <summary>コブの設定を読む。計測から使う。</summary>
        public Bump GetBump(int index)
        {
            return bumps != null && index >= 0 && index < bumps.Length ? bumps[index] : null;
        }

        /// <summary>そのコブの、床に出ている頭の半径（m）。</summary>
        public float BumpCapRadius(Bump bump)
        {
            return Mathf.Sqrt(Mathf.Max(0f, 2f * bump.radius * bump.height - bump.height * bump.height));
        }

        /// <summary>そのコブの、いちばん急なところの傾き（度）。頭の縁がいちばん急。</summary>
        public float BumpMaxSlopeDegrees(Bump bump)
        {
            float cap = BumpCapRadius(bump);
            return bump.radius > Mathf.Epsilon
                ? Mathf.Asin(Mathf.Clamp01(cap / bump.radius)) * Mathf.Rad2Deg
                : 0f;
        }

        public override void OnLaneStart(LaneContext context)
        {
            base.OnLaneStart(context);
            ApplyGap();
        }

        /// <summary>
        /// 台・リップ・コブ・隙間・着地側を組み立てる。
        /// エディタでは、このコンポーネントを右クリックして実行できる。
        /// </summary>
        [ContextMenu("隙間と台を反映する")]
        public void ApplyGap()
        {
            // 手前の床は台が始まるところまで。
            // 前へ重ねておくと、この床の先端の角が最初の板の下に埋まる
            StretchAlongZ(nearFloor, 0f, ArcStartZ + plateOverlap, 0f, laneWidth);

            // ガターは隙間のところで切る。片側だけ残すと、どこで切れているか読みにくい
            StretchAlongZ(nearGutterLeft, 0f, gapStartZ, null, null);
            StretchAlongZ(nearGutterRight, 0f, gapStartZ, null, null);

            BuildBumps();
            BuildArc();
            BuildLip();
            ApplyTakeoffEdge();

            // 落差は無い。着地側は通常の床と同じ高さ
            if (farSideRoot != null)
            {
                Vector3 position = farSideRoot.localPosition;
                position.y = 0f;
                farSideRoot.localPosition = position;
            }

            BuildLandingChamfer();
            StretchAlongZ(farFloor, GapEndZ + landingChamferLength, laneLength, 0f, laneWidth);
            StretchAlongZ(farGutterLeft, GapEndZ, laneLength, null, null);
            StretchAlongZ(farGutterRight, GapEndZ, laneLength, null, null);
        }

        /// <summary>
        /// コブを置く。床に球を埋め、頭だけを出す。
        ///
        /// 当たり判定の球は半径そのままで、中心を床の下へ沈める。
        /// 見た目は別に置いた潰した球で、当たり判定は持たせない。
        /// 半径どおりの球を見た目にすると、床の下へ何メートルも突き抜けてしまうため。
        /// </summary>
        private void BuildBumps()
        {
            if (bumps == null)
            {
                return;
            }

            foreach (Bump bump in bumps)
            {
                if (bump == null)
                {
                    continue;
                }

                // 頭が height だけ出るように、中心を沈める
                float centerY = FloorTopY + bump.height - bump.radius;

                if (bump.body != null)
                {
                    bump.body.localPosition = new Vector3(bump.x, centerY, bump.z);
                    bump.body.localRotation = Quaternion.identity;
                    bump.body.localScale = Vector3.one;

                    // SphereCollider は一番大きい軸の拡大率を使うので、拡大は1のままにして
                    // 半径そのものを入れる
                    var sphere = bump.body.GetComponent<SphereCollider>();
                    if (sphere != null)
                    {
                        sphere.center = Vector3.zero;
                        sphere.radius = bump.radius;
                    }
                }

                if (bump.visual != null)
                {
                    float cap = BumpCapRadius(bump);
                    bump.visual.localPosition = new Vector3(bump.x, FloorTopY, bump.z);
                    bump.visual.localRotation = Quaternion.identity;
                    bump.visual.localScale = new Vector3(cap * 2f, bump.height * 2f, cap * 2f);
                }
            }
        }

        /// <summary>
        /// 平らな床からリップまでを繋ぐ、凹の弧の高さ。
        /// 弧の始まりからの距離で測る。
        ///
        /// 弧の中心は、弧の始まりの真上に半径ぶん離れたところにある。
        /// こうすると始まりの傾きが0になり、平らな床と滑らかに繋がる。
        /// </summary>
        private float ArcHeight(float distance)
        {
            float radius = ArcRadius;
            if (radius <= Mathf.Epsilon)
            {
                return 0f;
            }

            float clamped = Mathf.Clamp(distance, 0f, arcSpan);
            float inside = Mathf.Max(0f, radius * radius - clamped * clamped);
            return radius - Mathf.Sqrt(inside);
        }

        /// <summary>凹の弧を、板を並べて作る。</summary>
        private void BuildArc()
        {
            if (arcPlates == null || arcPlates.Length == 0)
            {
                return;
            }

            int count = arcPlates.Length;
            float step = arcSpan / count;

            for (int i = 0; i < count; i++)
            {
                float d0 = step * i;
                float d1 = step * (i + 1);

                // 前へ重ねて、この板の先端の角を次の板の下に埋める
                PlaceSurfacePlate(
                    arcPlates[i], 0f, laneWidth,
                    ArcStartZ + d0, FloorTopY + ArcHeight(d0),
                    ArcStartZ + d1, FloorTopY + ArcHeight(d1),
                    plateOverlap);
            }
        }

        /// <summary>
        /// リップ（射出角を決める一枚板）を置く。
        ///
        /// 弧の終端の傾きは、円弧なので定義上ちょうどリップの角度に一致する。
        /// ここが一致していないと、その継ぎ目が凸になってボールが弾かれる。
        /// </summary>
        private void BuildLip()
        {
            // 前へは伸ばさない。先端が踏切点そのものなので、
            // 伸ばすと射出する位置が奥へずれて飛距離の見積もりが崩れる
            PlaceSurfacePlate(
                lip, 0f, laneWidth,
                LipStartZ, FloorTopY + ArcRise,
                gapStartZ, FloorTopY + TakeoffRise,
                0f);
        }

        /// <summary>
        /// 着地側の縁の面取り。上面の手前だけを緩く削る。
        ///
        /// 垂直な前面はわざと残す。以前は前面をまるごと斜面に置き換えていたが、
        /// その斜面が床の上面より10cm下まで伸びていたため、落ちかけた球を拾う
        /// 受け皿として働き、拾った球を打ち上げて減速させていた。
        /// </summary>
        private void BuildLandingChamfer()
        {
            float angle = landingChamferDegrees * Mathf.Deg2Rad;
            float shave = landingChamferLength * Mathf.Tan(angle);

            PlaceSurfacePlate(
                landingChamfer, 0f, laneWidth,
                GapEndZ, FloorTopY - shave,
                GapEndZ + landingChamferLength, FloorTopY,
                plateOverlap);
        }

        /// <summary>
        /// 上面が指定の2点を通るように板を置く。
        /// 板の厚みぶんは、上面から裏側へ下げる。
        /// </summary>
        private void PlaceSurfacePlate(
            Transform plate, float centerX, float width,
            float z0, float y0, float z1, float y1, float extendForward)
        {
            if (plate == null)
            {
                return;
            }

            float dz = z1 - z0;
            float dy = y1 - y0;
            float baseLength = Mathf.Sqrt(dz * dz + dy * dy);
            if (baseLength <= Mathf.Epsilon)
            {
                return;
            }

            float angle = Mathf.Atan2(dy, dz) * Mathf.Rad2Deg;
            float radians = angle * Mathf.Deg2Rad;

            Vector3 along = new Vector3(0f, Mathf.Sin(radians), Mathf.Cos(radians));
            Vector3 normal = new Vector3(0f, Mathf.Cos(radians), -Mathf.Sin(radians));

            float length = baseLength + Mathf.Max(0f, extendForward);

            Vector3 start = new Vector3(centerX, y0, z0);
            Vector3 middle = start + along * (length * 0.5f);

            plate.localPosition = middle - normal * (floorThickness * 0.5f);
            plate.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            plate.localScale = new Vector3(width, floorThickness, length);
        }

        /// <summary>踏切の縁に目印の帯を置く。当たり判定は持たせない。</summary>
        private void ApplyTakeoffEdge()
        {
            if (takeoffEdge == null)
            {
                return;
            }

            float thickness = edgeMarkSize.x;
            float length = edgeMarkSize.y;

            float centerZ = gapStartZ - length * 0.5f * Mathf.Cos(LipAngle);
            float centerY = FloorTopY + TakeoffRise
                            - length * 0.5f * Mathf.Sin(LipAngle) + thickness * 0.6f;

            takeoffEdge.localPosition = new Vector3(0f, centerY, centerZ);
            takeoffEdge.localRotation = Quaternion.Euler(-lipAngleDegrees, 0f, 0f);
            takeoffEdge.localScale = new Vector3(laneWidth, thickness, length);
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
        [ContextMenu("台とコブを書き出す")]
        public void LogProfile()
        {
            ApplyGap();

            var text = new System.Text.StringBuilder();
            text.AppendLine(
                $"踏切：弧 z={ArcStartZ:F3}→{LipStartZ:F3}（半径{ArcRadius:F2}m）／ " +
                $"リップ→{gapStartZ:F3}（{lipAngleDegrees:F1}度）／ 高さ 床＋{TakeoffRise * 100f:F1}cm ／ 隙間 {gapWidth:F2}m");

            if (bumps != null)
            {
                for (int i = 0; i < bumps.Length; i++)
                {
                    Bump bump = bumps[i];
                    if (bump == null) { continue; }
                    text.AppendLine(
                        $"  コブ{i}：z={bump.z:F2} x={bump.x:+0.00;-0.00} 半径{bump.radius:F2}m " +
                        $"高さ{bump.height * 100f:F1}cm ／ 頭の直径 {BumpCapRadius(bump) * 2f:F3}m ／ " +
                        $"最大の傾き {BumpMaxSlopeDegrees(bump):F2}度");
                }
            }

            Debug.Log(text.ToString(), this);
        }
    }
}
