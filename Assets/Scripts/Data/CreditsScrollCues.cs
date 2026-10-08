using System.Collections.Generic;
using UnityEngine;

namespace CrazyBowling.Data
{
    /// <summary>クレジットの画面の、自動送りの目印にするまとまり（段階6）。</summary>
    public enum CreditsAnchor
    {
        /// <summary>はじめのあいさつ（いちばん上の produced by。CrazyGames 版は名義が無いので前書き）。</summary>
        Top,

        /// <summary>MUSIC の見出し。</summary>
        Music,

        /// <summary>SOUND EFFECTS の見出し。</summary>
        SoundEffects,

        /// <summary>月の無線士（u_wxn5lzrjy3）の行。</summary>
        MoonOperator,

        /// <summary>VOICES の見出し。</summary>
        Voices,

        /// <summary>提供（結びの一言）。</summary>
        Sponsor,

        /// <summary>締め（いちばん下の produced by。CrazyGames 版は名義のあった位置の見えない印）。</summary>
        Closing,
    }

    /// <summary>目印1つ：あいさつの声のこの時刻に、このまとまりの見出しを画面の上から決めた位置に来させる。</summary>
    [System.Serializable]
    public class CreditsCue
    {
        [Tooltip("メモ（台本のどこか）。動きには関わらない。")]
        public string note;

        [Tooltip("どのまとまりか。")]
        public CreditsAnchor anchor;

        [Tooltip("あいさつの声の中の時刻（秒）。このときに見出しが決めた位置に来る。")]
        public float time;
    }

    /// <summary>
    /// クレジットの画面を、あいさつの声に合わせて自動で送る設定（段階6）。
    /// 目印の時刻は声の「間」を道具で探して出した案。聞いて直すときはここの時刻を変える（`音源一覧.md` の「クレジットのあいさつ」）。
    /// 目印と目印の間は一定の速さで送る（速さの上限あり）。
    /// </summary>
    [CreateAssetMenu(menuName = "CrazyBowling/Credits Scroll Cues", fileName = "CreditsScrollCues")]
    public class CreditsScrollCues : ScriptableObject
    {
        [Tooltip("自動で送るか。切ると、あいさつが流れても送らない。")]
        public bool autoScroll = true;

        [Tooltip("目印（時刻の早い順に並べる）。")]
        public List<CreditsCue> cues = new List<CreditsCue>();

        [Tooltip("見出しを来させる位置（見える範囲の上からの割合。0.33 で上から3分の1）。")]
        [Range(0f, 1f)]
        public float anchorFromTop = 0.33f;

        [Tooltip("送る速さの上限（画面の単位／秒。高さ1080の画面のピクセルと同じ）。急に飛ばないように。")]
        public float maxSpeed = 400f;
    }
}
