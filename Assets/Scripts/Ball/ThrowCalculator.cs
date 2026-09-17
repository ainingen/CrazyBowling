using UnityEngine;

namespace CrazyBowling.Ball
{
    /// <summary>
    /// 投球の計算に使う設定値。Inspector から渡される。
    /// </summary>
    [System.Serializable]
    public struct ThrowSettings
    {
        /// <summary>最大の強さになる引き幅（ピクセル）。</summary>
        public float maxDragPixels;

        /// <summary>これ以下の引き幅は「投げない」とみなす（ピクセル）。</summary>
        public float minDragPixels;

        /// <summary>最小の初速（m/s）。</summary>
        public float minThrowSpeed;

        /// <summary>最大の初速（m/s）。</summary>
        public float maxThrowSpeed;

        /// <summary>左右に振れる最大角度（度）。</summary>
        public float maxSideAngle;
    }

    /// <summary>
    /// 投球の計算結果。
    /// </summary>
    public struct ThrowResult
    {
        /// <summary>投球として成立したか。false なら投げない。</summary>
        public bool isValid;

        /// <summary>初速（m/s）。</summary>
        public float speed;

        /// <summary>レーン正面から左右に振る角度（度）。正が右回り。</summary>
        public float sideAngle;

        /// <summary>引き幅の割合（0〜1）。表示用。isValid が false のときも値が入る。</summary>
        public float pullRatio;
    }

    /// <summary>
    /// ドラッグ操作から初速と角度を求める純粋なC#クラス。
    /// MonoBehaviour に依存しないので EditMode テストで検証できる。
    /// </summary>
    public static class ThrowCalculator
    {
        /// <summary>
        /// ドラッグの開始点と終了点（スクリーン座標・ピクセル）から投球内容を計算する。
        /// 手前（画面下）に引くほど強くなる、パチンコのような操作を想定している。
        /// </summary>
        /// <param name="dragStart">左ボタンを押した位置。</param>
        /// <param name="dragEnd">左ボタンを離した位置。</param>
        /// <param name="settings">調整用の設定値。</param>
        /// <param name="invertPull">引く向きを反転する（奥に払って投げる操作にする）。</param>
        /// <param name="invertSide">左右の向きを反転する。</param>
        public static ThrowResult Calculate(
            Vector2 dragStart,
            Vector2 dragEnd,
            ThrowSettings settings,
            bool invertPull = false,
            bool invertSide = false)
        {
            // 0除算を避けるため、引き幅の基準は必ず正の値にする
            float maxDrag = Mathf.Max(settings.maxDragPixels, Mathf.Epsilon);

            // 手前（画面下）に引くと正になる
            float pull = dragStart.y - dragEnd.y;
            if (invertPull)
            {
                pull = -pull;
            }

            // 引き幅を 0〜1 に正規化する。表示に使うので、投げない場合も先に求める
            float pullRatio = Mathf.Clamp01(pull / maxDrag);

            // 引き幅が足りない（または逆向きに動かした）ときは投げない
            if (pull < settings.minDragPixels)
            {
                return new ThrowResult { isValid = false, speed = 0f, sideAngle = 0f, pullRatio = pullRatio };
            }

            // 最小初速と最大初速の間を取る
            float speed = Mathf.Lerp(settings.minThrowSpeed, settings.maxThrowSpeed, pullRatio);

            // 右にずらすと正。パチンコと同じで、引いた向きと逆に飛ぶ
            float dragX = dragEnd.x - dragStart.x;
            float sideRatio = Mathf.Clamp(dragX / maxDrag, -1f, 1f);
            float sideAngle = -sideRatio * settings.maxSideAngle;
            if (invertSide)
            {
                sideAngle = -sideAngle;
            }

            return new ThrowResult { isValid = true, speed = speed, sideAngle = sideAngle, pullRatio = pullRatio };
        }
    }
}
