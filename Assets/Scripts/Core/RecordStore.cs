using UnityEngine;

namespace CrazyBowling.Core
{
    /// <summary>
    /// 記録帳の保存場所（段階6）。PlayerPrefs に JSON で残す（WebGL ではブラウザの中＝IndexedDB。最大 1MB）。
    /// 確かめ（DevTools の通し・動きの確かめ）のときは <see cref="Key"/> を別の名前に切り替え、本物の記録を汚さない。
    /// </summary>
    public static class RecordStore
    {
        /// <summary>本物の記録の鍵。</summary>
        public const string DefaultKey = "CrazyBowling.Records";

        /// <summary>今使っている鍵。確かめのときだけ別の名前にし、終わったら <see cref="DefaultKey"/> に戻す。</summary>
        public static string Key { get; set; } = DefaultKey;

        /// <summary>最後に読んだとき、壊れていて捨てたか（確かめるとき用）。</summary>
        public static bool LastLoadWasBroken { get; private set; }

        /// <summary>記録帳を読む。無ければ空、壊れていたら捨てて空（ゲームは止めない）。</summary>
        public static RecordBook Load(int laneCount)
        {
            string json = "";
            try
            {
                json = PlayerPrefs.GetString(Key, "");
            }
            catch (System.Exception e)
            {
                Debug.Log("記録を読めなかったので、空の記録で続けます：" + e.Message);
            }
            RecordBook book = RecordRules.FromJson(json, laneCount, out bool ok);
            LastLoadWasBroken = !ok;
            if (!ok)
            {
                Debug.Log("記録が壊れていたので捨てて、空の記録で続けます");
            }
            return book;
        }

        /// <summary>記録帳を保存する。</summary>
        public static void Save(RecordBook book)
        {
            try
            {
                PlayerPrefs.SetString(Key, RecordRules.ToJson(book));
                PlayerPrefs.Save();
            }
            catch (System.Exception e)
            {
                Debug.Log("記録を保存できませんでした：" + e.Message);
            }
        }

        /// <summary>記録をすべて消す。</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
