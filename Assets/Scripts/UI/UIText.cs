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
    ///   ○ この床は左下がりに傾いています。ボールの進路にご注意ください。
    ///   × 床ハ左下ガリニ傾イテイマス。      （カタカナ化。やらない）
    ///   × 床が左に下がってるから気をつけてね。 （ふつうの日本語すぎて演出にならない）
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

        /// <summary>見回しを終えて構えに戻るボタン（段階6）。</summary>
        public const string LookBack = "BACK";

        /// <summary>見回し中に出す小さな案内（段階6）。</summary>
        public const string LookAroundHint = "DRAG TO LOOK AROUND";

        /// <summary>タイトルの前の画面：どこかをクリックしてもらう一言（段階6）。</summary>
        public const string TuneInPrompt = "CLICK TO TUNE IN";

        /// <summary>タイトルの前の画面：指で触れる端末のとき（段階6）。</summary>
        public const string TuneInPromptTap = "TAP TO TUNE IN";

        /// <summary>タイトルの前の画面：ラジオの上の札（段階6）。</summary>
        public const string TuneInOnAir = "ON AIR";

        /// <summary>タイトルの前の画面：ラジオの名前（段階6。架空の名前）。</summary>
        public const string TuneInRadioName = "CRAZY BOWLING RADIO";

        /// <summary>タイトルの前の画面：ダイヤルの単位（昔の AM ラジオの「キロサイクル」）。</summary>
        public const string TuneInDialUnit = "KC";

        /// <summary>タイトルの前の画面：ダイヤルの目盛りの数字（昔の AM ラジオの目盛り。×10 kc）。</summary>
        public static readonly string[] TuneInDialNumbers = { "55", "60", "70", "80", "90", "100", "120", "140", "160" };

        /// <summary>音のボタン：音が出ているとき（段階6）。</summary>
        public const string SoundOn = "SOUND ON";

        /// <summary>音のボタン：音を消しているとき（段階6）。</summary>
        public const string SoundOff = "SOUND OFF";

        /// <summary>DJ のボタン：DJ のラジオ番組を流しているとき（段階6）。</summary>
        public const string DjOn = "DJ ON";

        /// <summary>DJ のボタン：DJ だけを消しているとき（段階6）。</summary>
        public const string DjOff = "DJ OFF";

        // ======== 演出（段階6） ========

        /// <summary>レーンに入ったときの大見出しの上の小さな札。{0} がレーン番号。</summary>
        public const string LaneTagFormat = "LANE {0:00}";

        /// <summary>ストライクの大文字。</summary>
        public const string CalloutStrike = "STRIKE!";

        /// <summary>スペアの大文字。</summary>
        public const string CalloutSpare = "SPARE!";

        /// <summary>1本も倒れなかった投の大文字（情けない演出）。</summary>
        public const string CalloutGutter = "GUTTER...";

        /// <summary>得点が入ったときに浮かぶ数字。{0} が増えた点。</summary>
        public const string ScoreGainFormat = "+{0}";

        /// <summary>得点板の合計の見出し。</summary>
        public const string TotalHeading = "TOTAL";

        // ======== タイトル ========

        /// <summary>タイトルのロゴ（上の段）。</summary>
        public const string TitleLogoTop = "CRAZY";

        /// <summary>タイトルのロゴ（下の段）。</summary>
        public const string TitleLogoBottom = "BOWLING";

        /// <summary>タイトルの一言。説明書口調（上の方針に従う）。</summary>
        public const string TitleTagline =
            "本製品には、少々変わったレーンが10本収録されております。";

        /// <summary>タイトルの注意書き。説明書口調。</summary>
        public const string TitleNotice =
            "安全のため、投球は画面の中でのみ行ってください。";

        /// <summary>始めるボタン。</summary>
        public const string TitleStart = "CLICK TO START";

        /// <summary>始めるボタン：指で触れる端末のとき（段階6）。</summary>
        public const string TitleStartTap = "TAP TO START";

        /// <summary>タイトルの名義（上の段・細く控えめに）。英字・日本語・英字の混在はわざと。</summary>
        public const string CreditPrefix = "produced by";

        /// <summary>タイトルの名義（下の段・太いネオン）。</summary>
        public const string CreditName = "夜中のBBQ";

        // ======== リザルト（段階6） ========

        /// <summary>リザルトの評価。{0} が S〜D。</summary>
        public const string RankFormat = "RANK {0}";

        /// <summary>リザルトで、いちばん上の RANK（SSS。満点）のときだけ RANK の下に出す一言。</summary>
        public const string PerfectGame = "PERFECT GAME!";

        /// <summary>リザルトの締めの一言。説明書口調。</summary>
        public const string ResultComment =
            "以上で本日のボウリングは終了です。またのご利用をお待ちしております。";

        // ======== 個人の記録（段階6） ========

        /// <summary>結果画面：合計点の自己ベストを更新したとき。</summary>
        public const string NewRecord = "NEW RECORD!";

        /// <summary>結果画面：そのレーンの自己ベストを更新した行の印。</summary>
        public const string RecordLaneBestMark = "BEST";

        /// <summary>タイトルの、記録の画面を開くボタン。記録の画面の見出しも同じ。</summary>
        public const string Records = "RECORDS";

        public const string RecordsBestScore = "BEST SCORE";
        public const string RecordsLaneBest = "LANE BEST";
        public const string RecordsTotals = "TOTAL";
        public const string RecordsRecent = "RECENT GAMES";
        public const string RecordsGames = "GAMES";
        public const string RecordsStrikes = "STRIKES";
        public const string RecordsSpares = "SPARES";
        public const string RecordsGutters = "GUTTERS";
        public const string RecordsDate = "DATE";
        public const string RecordsRank = "RANK";
        public const string RecordsClose = "CLOSE";
        public const string RecordsClear = "CLEAR";
        public const string RecordsYes = "YES";
        public const string RecordsNo = "NO";

        /// <summary>升目のガターの印（倒した本数0の投）。</summary>
        public const string GutterMark = "G";

        /// <summary>最近の成績の数。{0} が残っている数、{1} が残す数。</summary>
        public const string RecordsRecentCountFormat = "{0} / {1}";

        /// <summary>記録の画面の一言。説明書口調。</summary>
        public const string RecordsIntro =
            "本記録は、お使いの端末に保存されております。";

        /// <summary>記録の画面の注意書き（保存場所）。説明書口調。</summary>
        public const string RecordsStorageNote =
            "成績は、この端末のこのブラウザにのみ保存されます。キャッシュを消去すると、記録も失われます。";

        /// <summary>記録の画面の注意書き（記録するゲーム）。説明書口調。</summary>
        public const string RecordsRuleNote =
            "記録されるのは、10本すべてを投げ終えたゲームのみです。";

        /// <summary>記録がまだ無いとき。説明書口調。</summary>
        public const string RecordsEmpty =
            "記録はまだございません。";

        /// <summary>記録を消す前の確認。説明書口調。</summary>
        public const string RecordsClearConfirm =
            "すべての記録を消去します。\n消去した記録は元に戻りません。よろしいですか。";

        // ======== 遊び方（段階6） ========

        /// <summary>タイトルの、遊び方の画面を開くボタン。遊び方の画面の見出しも同じ。</summary>
        public const string HowToPlay = "HOW TO PLAY";

        /// <summary>遊び方の画面：前のページ・次のページ。</summary>
        public const string HowToPlayPrev = "◀";
        public const string HowToPlayNext = "▶";

        /// <summary>遊び方の画面：ページの書き方。{0} が今のページ、{1} が全部のページ数。</summary>
        public const string HowToPlayPageFormat = "{0} / {1}";

        /// <summary>
        /// 遊び方の画面の見出し（取扱説明書の口調。文面はユーザーの指定どおり。変えるときは操作と食い違わないか確かめる）。
        /// <see cref="HowToPlayBodies"/> と同じ順に並べる。
        /// </summary>
        public static readonly string[] HowToPlayHeadings =
        {
            "【ごあいさつ】",
            "【投げかた】",
            "【立ちかた】",
            "【回転のかけかた】",
            "【下見】",
            "【得点】",
            "【記録】",
            "【ご注意】",
        };

        /// <summary>遊び方の画面の本文。<see cref="HowToPlayHeadings"/> と同じ順。</summary>
        public static readonly string[] HowToPlayBodies =
        {
            "このたびは本製品をお選びいただき、誠にありがとうございます。本製品は、球を転がしてピンを倒す遊戯でございます。ご使用の前に、本書をよくお読みください。",
            "画面を押したまま、手前へ引いて、離してください。引いた長さに応じて、球は前へ進みます。\n押した位置から横にずらすと、球は反対の方向へ進みます。仕様でございます。",
            "構えている間、指またはマウスの左右の位置で立ち位置が決まります。",
            "画面下のつまみで、球に回転をかけられます。回転をやめるときは、文字を押してください。",
            "LOOK AHEAD を押している間、レーンの奥をご覧いただけます。止まった所では、画面をなぞって周囲を見回すことができます。お戻りの際は BACK を押してください。",
            "各レーンで2回まで投げられます。1回目ですべて倒すとストライク（30点）、2回で倒すとスペア（20点）でございます。それ以外は倒した本数が得点となります。全10レーンの合計で評価いたします。",
            "成績は RECORDS からご覧いただけます。",
            "レーンによっては、床が傾いている、回転している、途中で途切れている、宇宙である等の場合がございます。いずれも仕様でございます。\n本製品の使用により生じたいかなる事態についても、当方は責任を負いかねます。",
        };

        /// <summary>1本目の最初の1投だけ出すヒント（初めて遊ぶ人向け）。1行目。</summary>
        public const string FirstThrowHintPull = "押したまま、手前へ引いて、離してください";

        /// <summary>1本目の最初の1投だけ出すヒント。2行目。</summary>
        public const string FirstThrowHintSide = "横にずらすと、反対へ飛びます";

        // ======== クレジット（段階6） ========

        /// <summary>タイトルの、クレジットの画面を開くボタン。クレジットの画面の見出しも同じ。</summary>
        public const string Credits = "CREDITS";

        /// <summary>クレジットの画面：なぞって送れることの小さな案内。</summary>
        public const string CreditsScrollHint = "DRAG TO SCROLL";

        /// <summary>クレジットの画面の前書き。説明書口調。</summary>
        public const string CreditsIntro =
            "本製品には、下記の皆様が制作された音源を使用しております。この場を借りて、厚く御礼申し上げます。";

        /// <summary>クレジットの画面の結び。説明書口調。</summary>
        public const string CreditsOutro =
            "以上の皆様に、重ねて御礼申し上げます。";

        /// <summary>クレジットの節の見出し（英語）。<see cref="CreditsSectionLines"/> と同じ順。</summary>
        public static readonly string[] CreditsHeadings =
        {
            "MUSIC",
            "SOUND EFFECTS",
            "ATTRIBUTION (CC BY 4.0)",
            "VOICES",
        };

        /// <summary>
        /// クレジットの節の中身（出典は 音源一覧.md の「クレジット表示に載せる音」。音を足したら両方を直す）。
        /// 「札\t本文」の行は、左に小さな札（英数字の太字）、右に本文を並べる。タブの無い行は、節の説明として淡々と出す。
        /// URL は文字として載せるだけ（押しても開かない）。
        /// </summary>
        public static readonly string[][] CreditsSectionLines =
        {
            new[]
            {
                "楽曲はすべて、フリーBGM OpenTracks（旧DOVA-SYNDROME）より使用させていただきました。opentracks.com",
                "LANE 01\t「Micawber-tribute to Keith Richards-」　作曲：田中芳典",
                "LANE 02\t「絢爛」　作曲：のる",
                "LANE 03\t「Coastal Road」　作曲：Heitaro Ashibe",
                "LANE 04\t「Wonderful Disco」　作曲：北見ヒツジ",
                "LANE 05\t「飲茶」　作曲：えだまめ88",
                "LANE 06\t「White ice snowing」　作曲：蒲鉾さちこ",
                "LANE 07\t「GENTLE WIND」　作曲：稿屋 隆",
                "LANE 09\t「神殿」　作曲：えすにっく・かわひろ",
                "LANE 10\t「BGM - 099 - Cyber City! Funky City!」　作曲：Sound Of Incense",
                "RESULT\t「灼熱のユーロビート」　作曲：マニーラ",
            },
            new[]
            {
                "効果音は、効果音ラボ（soundeffect-lab.info）、Pixabay（pixabay.com）、Freesound（freesound.org）より使用させていただきました。",
                "Sound Effect Lab\t効果音ラボ　「ボウリングのピンを倒す1」「ボウリングのピンを倒す2」",
                "DRAGON-STUDIO\tvia Pixabay　Cheering Crowd／Crowd Cheer and Applause／Crowd Booing／Button Press／Boat Horn／Cartoon Jump／Explosion Effect",
                "RibhavAgrawal\tvia Pixabay　coin recieved",
                "Fronbondi_Skegs\tvia Pixabay　SFX - Scanning for a Radio Signals Sound Effect",
                "driftworks\tFreesound（Pixabay では freesound_community）　Bowling Ball.wav",
                "Exchanger\tFreesound（Pixabay では freesound_community）　Tadaa.wav",
                "m_cel\tFreesound（Pixabay では freesound_community）　Jet Engine",
                "spanrucker\tFreesound（Pixabay では freesound_community）　Water Drip",
                "Sergenious\tFreesound（Pixabay では freesound_community）　laser.wav（CC BY 4.0。条件は下の ATTRIBUTION のとおり）",
                // あいさつの台本と同じく、月の交信の声の方は効果音のいちばん最後（名前が読めない謎の無線士としてお礼を言っている）
                "u_wxn5lzrjy3\tvia Pixabay　Military Radio Communication（8本目の月面の交信の声）",
                "8本目の無線の雑音と「ピッ」という音は、本製品のために合成したものでございます。",
            },
            new[]
            {
                "10本目の閃光の効果音は、下記の条件（CC BY 4.0）に基づき使用しております。",
                "TITLE\tlaser.wav",
                "AUTHOR\tSergenious",
                "SOURCE\tfreesound.org/people/Sergenious/sounds/55835/",
                "LICENSE\tCC BY 4.0（creativecommons.org/licenses/by/4.0/）",
                "CHANGES\tTrimmed and faded. 前後の無音を切り詰め、始まりと終わりにフェードを付けて使用しております。",
                "\"laser.wav\" by Sergenious (freesound.org/people/Sergenious/sounds/55835/), licensed under CC BY 4.0 (creativecommons.org/licenses/by/4.0/). Trimmed and faded.",
            },
            new[]
            {
                "声は、ElevenLabs（elevenlabs.io）で生成したものでございます。Voice generated with ElevenLabs.",
                "DJ\tTyler Cruz - Cool Energetic DJ（タイトルのナレーションも）",
                "CORNER 04, 06\tKristen - Natural, Upbeat and Focused",
                "CORNER 07\tBrian - Deep, Resonant and Comforting",
                "CORNER 08\tDarren - Smiling & Energetic",
                "CORNER 09\tHale - Expressive, Deep and Emotive",
                "CORNER 10\tHannah - Bubbly, Perky and Outgoing",
            },
        };

        // ======== タイトルへ戻る（段階6） ========

        /// <summary>タイトルへ戻るボタン（結果画面と、ゲームの途中の画面の隅）。</summary>
        public const string TitleReturn = "TITLE";

        /// <summary>ゲームの途中でタイトルへ戻る前の確認。説明書口調。</summary>
        public const string TitleReturnConfirm =
            "タイトルに戻りますか。\nこのゲームの成績は記録されません。";

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
        public const string LaneMogulName = "MOGUL";
        public const string LaneRotatingTubeName = "ROTATING TUBE";
        public const string LaneLowGravityName = "LOW GRAVITY";
        public const string LaneTempleOfStrikeName = "TEMPLE OF STRIKE";
        public const string LaneBallColliderName = "BALL COLLIDER";

        // ======== レーンの一言（日本語・翻訳調） ========
        // 上の方針に従うこと。文法は正しく、語の選び方だけをわずかに硬くする。

        public const string LaneStraightDescription =
            "標準のレーンです。特別な仕掛けは用意されておりません。";

        public const string LaneTiltedDescription =
            "この床は左下がりに傾いています。ボールの進路にご注意ください。";

        public const string LaneRollingDescription =
            "床の高さは場所によって変化します。手前と奥では傾きが異なります。";

        public const string LaneSCurveDescription =
            "レーンは大きく曲がっております。まっすぐ投げることは推奨されません。";

        public const string LaneMovingWallDescription =
            "障害物が左右に移動しています。通過の時機をよくお計りください。";

        public const string LaneSpinningDiscDescription =
            "円盤が回転しております。ボールは意図した方向へ進まない場合があります。";

        public const string LaneMogulDescription =
            "床に起伏があります。通過する所をよくお計りください。";

        public const string LaneRotatingTubeDescription =
            "中で激しく回転いたします。進路は保証いたしません。";

        public const string LaneLowGravityDescription =
            "重力が弱く設定されています。障害物で大きく跳ねます。";

        public const string LaneTempleOfStrikeDescription =
            "台が回転しております。中まで通ると大変なことになります。";

        public const string LaneBallColliderDescription =
            "本装置はボウリングの球を加速するために建設されました。ほかの用途はございません。";
    }
}
