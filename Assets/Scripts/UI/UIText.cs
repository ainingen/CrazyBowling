namespace CrazyBowling.UI
{
    /// <summary>
    /// 画面に出す文言をまとめた場所。文言を足すときは必ずここに書く。
    ///
    /// ── 文体の方針 ──────────────────────────────────
    ///
    /// ■ 固定文言とレーン名は英語
    ///   TOTAL / STRIKE / SPARE / TILTED FLOOR のように、短く大文字で書く。
    ///
    /// ■ レーンの一言説明は日本語。ただし「翻訳調」で書く
    ///   狙いは「アメリカ製の商品に付いてきた、微妙な日本語の説明書」の文体。
    ///   意図的な演出であって、間違った日本語を書いてよいという意味ではない。
    ///
    ///   守ること：
    ///     ・文法は正しく保つ。助詞の誤りや語順の乱れは入れない
    ///     ・選ぶ語をわずかに不自然にする。硬すぎる語、直訳めいた言い回しを使う
    ///       例：「気をつけて」→「ご注意ください」
    ///           「やめたほうがいい」→「推奨されません」
    ///           「タイミングを見て」→「時機をよくお計りください」
    ///     ・主語や対象を省略せず、律儀に書く
    ///     ・2文までに収める。説明書らしく、淡々と言い切る
    ///
    ///   やらないこと：
    ///     ・カタカナ化（「床ハ右ニ傾イテイマス」のような書き方）はしない
    ///     ・読みにくくなるほど崩さない。ぱっと読んで意味が通ることが最優先
    ///     ・ふざけた語やネットスラングは入れない。あくまで生真面目な説明書の顔をする
    ///
    /// ── 参考：良い例と行きすぎた例 ─────────────────────
    ///
    ///   ○ この床は右に傾いています。ボールの進路にご注意ください。
    ///   × 床ハ右ニ傾イテイマス。            （カタカナ化。やらない）
    ///   × 床が右に傾いてるから気をつけてね。 （ふつうの日本語すぎて演出にならない）
    ///
    /// ─────────────────────────────────────────────
    ///
    /// 新しい文字を使うときは、フォントの焼き直しが要る。
    /// Assets/Fonts/焼き込んだ文字.md を見ること。
    /// </summary>
    public static class UIText
    {
        // ======== 得点表 ========

        /// <summary>合計の書き方。{0} が合計、{1} が満点。</summary>
        public const string TotalFormat = "TOTAL {0}";

        /// <summary>まだ遊んでいないレーンの升目に出す文字。</summary>
        public const string NotPlayed = "-";

        /// <summary>升目のストライクの印。</summary>
        public const string StrikeMark = "X";

        /// <summary>升目のスペアの印。</summary>
        public const string SpareMark = "/";

        // ======== レーン表示 ========

        /// <summary>進み具合の書き方。{0} が今のレーン、{1} が全部の数。</summary>
        public const string ProgressFormat = "{0} / {1}";

        // ======== リザルト ========

        /// <summary>リザルトの見出し。</summary>
        public const string ResultTitle = "RESULT";

        /// <summary>リザルトの合計。{0} が合計、{1} が満点。</summary>
        public const string ResultTotalFormat = "{0} / {1}";

        /// <summary>内訳1行。{0} 番号、{1} レーン名、{2} 結果、{3} 得点。</summary>
        public const string ResultLineFormat = "{0,2}  {1}  {2}  {3}";

        /// <summary>リザルトのストライク。</summary>
        public const string Strike = "STRIKE";

        /// <summary>リザルトのスペア。</summary>
        public const string Spare = "SPARE";

        /// <summary>リザルトの本数。{0} が倒した本数。</summary>
        public const string PinsFormat = "{0} PINS";

        /// <summary>もう一度遊ぶボタン。</summary>
        public const string PlayAgain = "PLAY AGAIN";

        // ======== 投球まわり ========

        /// <summary>強さゲージの初速。{0} が数値。</summary>
        public const string SpeedFormat = "{0:F1} m/s";

        /// <summary>引き幅が足りないときの表示。</summary>
        public const string Cancel = "CANCEL";

        /// <summary>左カーブ。{0} が強さのパーセント。</summary>
        public const string CurveLeftFormat = "◀ LEFT {0:F0}%";

        /// <summary>右カーブ。{0} が強さのパーセント。</summary>
        public const string CurveRightFormat = "RIGHT {0:F0}% ▶";

        /// <summary>カーブ無し。</summary>
        public const string CurveNone = "STRAIGHT";

        /// <summary>奥を見るボタン。</summary>
        public const string LookAhead = "LOOK AHEAD";

        // ======== その他 ========

        /// <summary>縦画面のときの案内。</summary>
        public const string RotateDevice = "PLEASE ROTATE YOUR DEVICE";

        // ======== レーン名（英語） ========

        public const string LaneStraightName = "STRAIGHT";
        public const string LaneTiltedName = "TILTED FLOOR";
        public const string LaneRollingName = "ROLLING FLOOR";
        public const string LaneSCurveName = "S-CURVE";
        public const string LaneMovingWallName = "MOVING WALL";
        public const string LaneSpinningDiscName = "SPINNING DISC";
        public const string LaneGapName = "THE GAP";
        public const string LaneRotatingTubeName = "ROTATING TUBE";
        public const string LaneLowGravityName = "LOW GRAVITY";
        public const string LaneMovingPinsName = "MOVING PINS";
        public const string LaneCoasterName = "ROLLER COASTER";

        // ======== レーンの一言（日本語・翻訳調） ========
        // 上の方針に従うこと。文法は正しく、語の選び方だけをわずかに硬くする。

        public const string LaneStraightDescription =
            "標準のレーンです。特別な仕掛けは用意されておりません。";

        public const string LaneTiltedDescription =
            "この床は右に傾いています。ボールの進路にご注意ください。";

        public const string LaneRollingDescription =
            "床の高さは場所によって変化します。手前と奥では傾きが異なります。";

        public const string LaneSCurveDescription =
            "レーンは大きく曲がっております。まっすぐ投げることは推奨されません。";

        public const string LaneMovingWallDescription =
            "障害物が左右に移動しています。通過の時機をよくお計りください。";

        public const string LaneSpinningDiscDescription =
            "円盤が回転しております。ボールは意図した方向へ進まない場合があります。";

        public const string LaneGapDescription =
            "レーンの中央に空間があります。速度が不足した場合、ボールは落下します。";

        public const string LaneRotatingTubeDescription =
            "レーン全体がゆっくりと回転いたします。床の傾きは常に変化しています。";

        public const string LaneLowGravityDescription =
            "重力が弱く設定されています。ボールとピンはよく跳ねます。";

        public const string LaneMovingPinsDescription =
            "ピンの台が左右に移動します。停止することはございません。";

        public const string LaneCoasterDescription =
            "起伏の激しい経路です。ボールの安全は保証されません。";
    }
}
