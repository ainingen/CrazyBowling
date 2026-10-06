namespace CrazyBowling.UI
{
    /// <summary>
    /// 画面に出す言葉の言語（段階6。英語化）。ビルドのときに決まり、遊んでいる間は変わらない。
    ///
    /// 言語の印（Scripting Define Symbols）の <c>CB_LANG_EN</c> があれば英語、無ければ日本語。
    /// 印はビルドプロファイルごとに付ける（「CrazyGames（英語）」のプロファイルにだけ付けてある）。
    /// 文言は <see cref="UITextJa"/>・<see cref="UITextEn"/> に同じ名前で並べ、<see cref="UIText"/> がどちらかを指す。
    /// </summary>
    public static class GameLanguage
    {
        /// <summary>英語で出すか。const にしないのは、使う側の if に「届かないコード」の警告を出さないため。</summary>
#if CB_LANG_EN
        public static readonly bool IsEnglish = true;
#else
        public static readonly bool IsEnglish = false;
#endif
    }
}
