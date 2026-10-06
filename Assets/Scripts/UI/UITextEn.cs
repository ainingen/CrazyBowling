namespace CrazyBowling.UI
{
    /// <summary>
    /// 言語で変わる文言の英語版（段階6。CrazyGames 向けの英語化）。訳の元と経緯はリポジトリ直下の「英語化_下書き.md」。
    /// <see cref="UITextJa"/> と同じ名前・同じ並びで書くこと（テストで確かめている）。
    ///
    /// ── 英語の文体の方針 ──────────────────────────────────
    ///
    /// アメリカの家電の取扱説明書のような、丁寧で淡々とした真顔の言い方にする（日本語の「翻訳調の説明書」と同じ狙い）。
    ///   例：「仕様でございます」→ "This is intended behavior."
    ///       「当方は責任を負いかねます」→ "The manufacturer accepts no responsibility for ..."
    /// ふざけた語や口語は入れない。短く言えるところは短くする（1行の枠に収めるため）。
    ///
    /// ★英字は ASCII だけで書く（曲がった引用符・ダッシュ・三点リーダーの1文字は使わない。フォントに焼き込んでいないため）。
    ///   例外は、作曲者の日本語の元の名前・英語の表記の無い作曲者の名前・効果音ラボの音の日本語の題（日本語版にある字だけ）
    /// </summary>
    public static class UITextEn
    {
        /// <summary>タイトルの一言。</summary>
        public const string TitleTagline =
            "This product contains 10 slightly unusual lanes.";

        /// <summary>タイトルの注意書き。</summary>
        public const string TitleNotice =
            "For your safety, please bowl only inside the screen.";

        /// <summary>タイトルの名義（下の段・太いネオン）。声の中の「Yonaka no B-B-Q」はそのまま（声は作り直さない）。</summary>
        public const string CreditName = "Midnight BBQ";

        /// <summary>リザルトの締めの一言。</summary>
        public const string ResultComment =
            "This concludes today's bowling session. We look forward to serving you again.";

        /// <summary>記録の画面の一言。</summary>
        public const string RecordsIntro =
            "These records are stored on your device.";

        /// <summary>記録の画面の注意書き（保存場所）。</summary>
        public const string RecordsStorageNote =
            "Records are saved only in this browser on this device. Clearing the cache will also erase them.";

        /// <summary>記録の画面の注意書き（記録するゲーム）。</summary>
        public const string RecordsRuleNote =
            "Only games in which all 10 lanes are completed are recorded.";

        /// <summary>記録がまだ無いとき。</summary>
        public const string RecordsEmpty =
            "There are no records at this time.";

        /// <summary>記録を消す前の確認。</summary>
        public const string RecordsClearConfirm =
            "All records will be erased.\nErased records cannot be restored. Do you wish to proceed?";

        /// <summary>遊び方の画面の見出し（日本語の【】は ASCII に無いので、番号付きの見出しにした）。</summary>
        public static readonly string[] HowToPlayHeadings =
        {
            "1. INTRODUCTION",
            "2. THROWING",
            "3. POSITION",
            "4. APPLYING CURVE",
            "5. LOOKING AHEAD",
            "6. SCORING",
            "7. RECORDS",
            "8. CAUTION",
        };

        /// <summary>遊び方の画面の本文。<see cref="HowToPlayHeadings"/> と同じ順。</summary>
        public static readonly string[] HowToPlayBodies =
        {
            "Thank you for choosing this product. This product is a recreational activity in which a ball is rolled to knock down pins. Please read this manual carefully before use.",
            "Press and hold the screen, pull back toward you, and release. The ball will travel forward in proportion to the length of the pull.\nShifting sideways from the point where you pressed will send the ball in the opposite direction. This is intended behavior.\nTo cancel, return to the point where you pressed, or slide upward. If you release while the word CANCEL is lit, the ball will not be thrown.",
            "Select your standing position with the POSITION slider at the bottom of the screen. It may be adjusted any number of times before throwing. To return to the center, press the label.",
            "Spin may be applied to the ball with the CURVE slider at the bottom of the screen. To remove the curve, press the label.",
            "While LOOK AHEAD is held, you may view the far end of the lane. Where the view stops, you may drag the screen to look around. To return, press BACK.",
            "Up to 2 throws are permitted per lane. Knocking down all pins on the first throw is a strike (30 points). Doing so in 2 throws is a spare (20 points). Otherwise, your score is the number of pins knocked down. Your rating is based on your total score across all 10 lanes.",
            "Your results may be viewed from RECORDS.",
            "Depending on the lane, the floor may be tilted, rotating, partially missing, or located in outer space. All of these are intended behavior.\nThe manufacturer accepts no responsibility for any situation arising from the use of this product.",
        };

        /// <summary>1本目の最初の1投だけ出すヒント。1行目。</summary>
        public const string FirstThrowHintPull = "Press, pull back, and release";

        /// <summary>1本目の最初の1投だけ出すヒント。2行目。</summary>
        public const string FirstThrowHintSide = "Shift sideways to aim the opposite way";

        /// <summary>1本目の最初の1投だけ出すヒント。3行目。</summary>
        public const string FirstThrowHintPosition = "Choose your position with POSITION below";

        /// <summary>クレジットの画面の前書き。</summary>
        public const string CreditsIntro =
            "This product uses audio created by the individuals listed below. We wish to take this opportunity to express our sincere gratitude.";

        /// <summary>クレジットの画面の結び。</summary>
        public const string CreditsOutro =
            "Once again, we extend our thanks to all of the above.";

        /// <summary>
        /// クレジットの節の中身（<see cref="UITextJa.CreditsSectionLines"/> と同じ並び）。
        /// 作曲者は「英語の表記（日本語の元の名前）」。英語の表記は OpenTracks の英語版のページのもの（作者本人が付けたかは不明）。
        /// 英語の表記の無い「えだまめ88」「えすにっく・かわひろ」は日本語のまま。効果音ラボの音の題は「日本語の題 (英語の説明)」。
        /// </summary>
        public static readonly string[][] CreditsSectionLines =
        {
            new[]
            {
                "All music is used courtesy of Free BGM OpenTracks (formerly DOVA-SYNDROME). opentracks.com",
                "LANE 01\t\"Micawber-tribute to Keith Richards-\"  Composed by Yoshinori Tanaka（田中芳典）",
                "LANE 02\t\"Splendid\"  Composed by Noru（のる）",
                "LANE 03\t\"Coastal Road\"  Composed by Heitaro Ashibe",
                "LANE 04\t\"Wonderful Disco\"  Composed by Hitsuji Kitami（北見ヒツジ）",
                "LANE 05\t\"dim sum\"  Composed by えだまめ88",
                "LANE 06\t\"White ice snowing\"  Composed by Kamaboko Sachiko（蒲鉾さちこ）",
                "LANE 07\t\"GENTLE WIND\"  Composed by T.Waraya（稿屋 隆）",
                "LANE 09\t\"Shrine\"  Composed by えすにっく・かわひろ",
                "LANE 10\t\"BGM - 099 - Cyber City! Funky City!\"  Composed by Sound Of Incense",
                "RESULT\t\"Scorching Eurobeat\"  Composed by Maniira（マニーラ）",
            },
            new[]
            {
                "Sound effects are used courtesy of Sound Effect Lab (soundeffect-lab.info), Pixabay (pixabay.com), and Freesound (freesound.org).",
                "Sound Effect Lab\tボウリングのピンを倒す1 (Bowling pins falling 1) / ボウリングのピンを倒す2 (Bowling pins falling 2)",
                "DRAGON-STUDIO\tvia Pixabay  Cheering Crowd / Crowd Cheer and Applause / Crowd Booing / Button Press / Boat Horn / Cartoon Jump / Explosion Effect",
                "RibhavAgrawal\tvia Pixabay  coin recieved",
                "Fronbondi_Skegs\tvia Pixabay  SFX - Scanning for a Radio Signals Sound Effect",
                "driftworks\tFreesound (freesound_community on Pixabay)  Bowling Ball.wav",
                "Exchanger\tFreesound (freesound_community on Pixabay)  Tadaa.wav",
                "m_cel\tFreesound (freesound_community on Pixabay)  Jet Engine",
                "spanrucker\tFreesound (freesound_community on Pixabay)  Water Drip",
                "Sergenious\tFreesound (freesound_community on Pixabay)  laser.wav (CC BY 4.0; see ATTRIBUTION below)",
                "u_wxn5lzrjy3\tvia Pixabay  Military Radio Communication (radio voice on the Moon, Lane 08)",
                "The radio static and \"beep\" sounds in Lane 08 were synthesized specifically for this product.",
            },
            new[]
            {
                "The flash sound effect in Lane 10 is used under the following terms (CC BY 4.0).",
                "TITLE\tlaser.wav",
                "AUTHOR\tSergenious",
                "SOURCE\tfreesound.org/people/Sergenious/sounds/55835/",
                "LICENSE\tCC BY 4.0 (creativecommons.org/licenses/by/4.0/)",
                "CHANGES\tTrimmed and faded. Leading and trailing silence was removed, and fades were applied to the start and end.",
                "\"laser.wav\" by Sergenious (freesound.org/people/Sergenious/sounds/55835/), licensed under CC BY 4.0 (creativecommons.org/licenses/by/4.0/). Trimmed and faded.",
            },
            new[]
            {
                "Voices were generated with ElevenLabs (elevenlabs.io).",
                "DJ\tTyler Cruz - Cool Energetic DJ (also the title narration)",
                "CORNER 04, 06\tKristen - Natural, Upbeat and Focused",
                "CORNER 07\tBrian - Deep, Resonant and Comforting",
                "CORNER 08\tDarren - Smiling & Energetic",
                "CORNER 09\tHale - Expressive, Deep and Emotive",
                "CORNER 10\tHannah - Bubbly, Perky and Outgoing",
            },
        };

        /// <summary>ゲームの途中でタイトルへ戻る前の確認。</summary>
        public const string TitleReturnConfirm =
            "Return to the title screen?\nResults from this game will not be recorded.";
    }
}
