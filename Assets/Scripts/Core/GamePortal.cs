namespace CrazyBowling.Core
{
    /// <summary>
    /// どのサイト向けのビルドか（段階6。CrazyGames 向け）。ビルドのときに決まり、遊んでいる間は変わらない。
    ///
    /// 印（Scripting Define Symbols）の <c>CB_PORTAL_CRAZYGAMES</c> があれば CrazyGames 向け。
    /// 印は「CrazyGames（英語）」のビルドプロファイルにだけ付ける。言語の印 <c>CB_LANG_EN</c> とは別に分けてある。
    /// CrazyGames 向けのときだけ：
    ///   ・CLICK TO TUNE IN を1回押すと、タイトルを出さずに1本目が始まる（ナレーションは1本目の後ろで流れる）
    ///   ・名義「produced by」をタイトルとクレジットに出さない
    /// </summary>
    public static class GamePortal
    {
        /// <summary>CrazyGames 向けのビルドか。const にしないのは、使う側の if に「届かないコード」の警告を出さないため。</summary>
#if CB_PORTAL_CRAZYGAMES
        public static readonly bool IsCrazyGames = true;
#else
        public static readonly bool IsCrazyGames = false;
#endif

        /// <summary>CLICK TO TUNE IN の1回のクリックで、タイトルを出さずに1本目を始めるか。</summary>
        public static bool QuickStart => IsCrazyGames;

        /// <summary>名義「produced by」を画面に出すか。</summary>
        public static bool ShowProducerCredit => !IsCrazyGames;
    }
}
