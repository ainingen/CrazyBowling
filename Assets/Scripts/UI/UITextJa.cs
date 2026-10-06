namespace CrazyBowling.UI
{
    /// <summary>
    /// 言語で変わる文言の日本語版（段階6。英語化のときに UIText から分けた。中身は分ける前と同じ）。
    /// 文体の方針は <see cref="UIText"/> の説明のとおり。<see cref="UITextEn"/> と同じ名前・同じ並びで書くこと（テストで確かめている）。
    /// </summary>
    public static class UITextJa
    {
        /// <summary>タイトルの一言。説明書口調（上の方針に従う）。</summary>
        public const string TitleTagline =
            "本製品には、少々変わったレーンが10本収録されております。";

        /// <summary>タイトルの注意書き。説明書口調。</summary>
        public const string TitleNotice =
            "安全のため、投球は画面の中でのみ行ってください。";

        /// <summary>タイトルの名義（下の段・太いネオン）。</summary>
        public const string CreditName = "夜中のBBQ";

        /// <summary>リザルトの締めの一言。説明書口調。</summary>
        public const string ResultComment =
            "以上で本日のボウリングは終了です。またのご利用をお待ちしております。";

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
            "画面を押したまま、手前へ引いて、離してください。引いた長さに応じて、球は前へ進みます。\n押した位置から横にずらすと、球は反対の方向へ進みます。仕様でございます。\nおやめになる場合は、押した位置まで戻すか、上へずらしてください。CANCEL の文字が光っている間に離すと、球は投げられません。",
            "画面下の POSITION のつまみで、立ち位置をお選びください。投げるまでは、いくらでも動かせます。中央に戻すときは、文字を押してください。",
            "画面下の CURVE のつまみで、球に回転をかけられます。回転をやめるときは、文字を押してください。",
            "LOOK AHEAD を押している間、レーンの奥をご覧いただけます。止まった所では、画面をなぞって周囲を見回すことができます。お戻りの際は BACK を押してください。",
            "各レーンで2回まで投げられます。1回目ですべて倒すとストライク（30点）、2回で倒すとスペア（20点）でございます。それ以外は倒した本数が得点となります。全10レーンの合計で評価いたします。",
            "成績は RECORDS からご覧いただけます。",
            "レーンによっては、床が傾いている、回転している、途中で途切れている、宇宙である等の場合がございます。いずれも仕様でございます。\n本製品の使用により生じたいかなる事態についても、当方は責任を負いかねます。",
        };

        /// <summary>1本目の最初の1投だけ出すヒント（初めて遊ぶ人向け）。1行目。</summary>
        public const string FirstThrowHintPull = "押したまま、手前へ引いて、離してください";

        /// <summary>1本目の最初の1投だけ出すヒント。2行目。</summary>
        public const string FirstThrowHintSide = "横にずらすと、反対へ飛びます";

        /// <summary>1本目の最初の1投だけ出すヒント。3行目（段階6。立ち位置をゲージで決める作りにしたため）。</summary>
        public const string FirstThrowHintPosition = "立ち位置は、画面下の POSITION でお選びください";

        /// <summary>クレジットの画面の前書き。説明書口調。</summary>
        public const string CreditsIntro =
            "本製品には、下記の皆様が制作された音源を使用しております。この場を借りて、厚く御礼申し上げます。";

        /// <summary>クレジットの画面の結び。説明書口調。</summary>
        public const string CreditsOutro =
            "以上の皆様に、重ねて御礼申し上げます。";

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

        /// <summary>ゲームの途中でタイトルへ戻る前の確認。説明書口調。</summary>
        public const string TitleReturnConfirm =
            "タイトルに戻りますか。\nこのゲームの成績は記録されません。";
    }
}
