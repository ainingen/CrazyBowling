using UnityEngine;

namespace CrazyBowling.Core
{
    /// <summary>
    /// 初めて遊ぶ人にだけ出す、1本目の最初の1投のヒントを出すかどうかの決まり（段階6）。
    /// MonoBehaviour に依らない（EditMode テストあり）。
    /// </summary>
    public static class FirstThrowHintRules
    {
        /// <summary>
        /// ヒントを出すか。
        /// 一度でも投げたことがあれば出さない。1本目の1投目の構え中で、上に重なる画面（タイトル・記録・遊び方など）が無く、
        /// 下見のカメラが動いていないときだけ出す。
        /// </summary>
        /// <param name="alreadyThrown">この端末で一度でも投げたか。</param>
        /// <param name="laneNumber">今のレーン（1から数える）。</param>
        /// <param name="throwNumber">今が何投目か（1から数える）。</param>
        /// <param name="aiming">構え中か（押し始めると false になる）。</param>
        /// <param name="overlayOpen">タイトル・記録・遊び方・結果などの画面が上に出ているか。</param>
        /// <param name="previewPlaying">下見のカメラが動いているか（自動の下見・LOOK AHEAD の見回し）。</param>
        public static bool ShouldShow(bool alreadyThrown, int laneNumber, int throwNumber,
            bool aiming, bool overlayOpen, bool previewPlaying)
        {
            return !alreadyThrown
                && laneNumber == 1
                && throwNumber == 1
                && aiming
                && !overlayOpen
                && !previewPlaying;
        }

        /// <summary>
        /// 指の丸の動き（1周ぶん）。t は1周の中の秒。
        /// 押す所で現れ（fade 秒）、手前へ引き（stroke 秒）、離す所で消え（fade 秒）、少し休む（rest 秒）。
        /// 点滅に見えないよう、明るさはゆっくり上げ下げするだけ。
        /// </summary>
        /// <param name="progress">引いた割合（0 が押す所、1 が離す所）。</param>
        /// <param name="alpha">指の丸の濃さ（0〜1）。</param>
        public static void Gesture(float t, float fade, float stroke, float rest, out float progress, out float alpha)
        {
            fade = Mathf.Max(fade, 0.01f);
            stroke = Mathf.Max(stroke, 0.01f);
            rest = Mathf.Max(rest, 0f);
            float period = fade + stroke + fade + rest;
            t = Mathf.Repeat(t, period);

            if (t < fade)
            {
                progress = 0f;
                alpha = Mathf.SmoothStep(0f, 1f, t / fade);
                return;
            }
            t -= fade;
            if (t < stroke)
            {
                progress = Mathf.SmoothStep(0f, 1f, t / stroke);
                alpha = 1f;
                return;
            }
            t -= stroke;
            progress = 1f;
            alpha = t < fade ? Mathf.SmoothStep(1f, 0f, t / fade) : 0f;
        }
    }

    /// <summary>
    /// 「この端末で一度投げたか」の保存場所（段階6）。記録と同じく PlayerPrefs に残す（Web ではブラウザの中）。
    /// 確かめ（DevTools の通し）のときは <see cref="Key"/> を別の名前に切り替え、本物の状態を汚さない。
    /// </summary>
    public static class FirstThrowHintStore
    {
        /// <summary>本物の鍵。</summary>
        public const string DefaultKey = "CrazyBowling.FirstThrowDone";

        /// <summary>今使っている鍵。確かめのときだけ別の名前にし、終わったら <see cref="DefaultKey"/> に戻す。</summary>
        public static string Key { get; set; } = DefaultKey;

        /// <summary>この端末で一度でも投げたか。読めなければ「まだ」とみなす（ヒントが出るだけで、ゲームは止めない）。</summary>
        public static bool IsDone
        {
            get
            {
                try
                {
                    return PlayerPrefs.GetInt(Key, 0) == 1;
                }
                catch (System.Exception e)
                {
                    Debug.Log("ヒントの状態を読めなかったので、まだ投げていないとみなします：" + e.Message);
                    return false;
                }
            }
        }

        /// <summary>投げたことを残す（もう残っていれば何もしない）。</summary>
        public static void MarkDone()
        {
            try
            {
                if (PlayerPrefs.GetInt(Key, 0) == 1)
                {
                    return;
                }
                PlayerPrefs.SetInt(Key, 1);
                PlayerPrefs.Save();
            }
            catch (System.Exception e)
            {
                Debug.Log("ヒントの状態を保存できませんでした：" + e.Message);
            }
        }

        /// <summary>消す（もう一度ヒントを出したいとき・確かめのあと）。</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
