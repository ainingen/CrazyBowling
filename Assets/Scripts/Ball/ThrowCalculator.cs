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

        /// <summary>カーブが最大になるマウスの横移動量（ピクセル）。</summary>
        public float maxCurvePixels;

        /// <summary>横に動かさずに離した場合も投げられるか。false なら投球にならない。</summary>
        public bool allowStraightThrow;
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

        /// <summary>カーブの強さと向き。-1で左いっぱい、0でカーブ無し、+1で右いっぱい。</summary>
        public float curve;

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
        /// <param name="dragStart">ボタンを押した位置。</param>
        /// <param name="dragEnd">ボタンを離した位置。</param>
        /// <param name="settings">調整用の設定値。</param>
        /// <param name="curveToRight">右に曲げるか。ドラッグを始めたボタンで決まる。</param>
        /// <param name="invertPull">引く向きを反転する（奥に払って投げる操作にする）。</param>
        public static ThrowResult Calculate(
            Vector2 dragStart,
            Vector2 dragEnd,
            ThrowSettings settings,
            bool curveToRight,
            bool invertPull = false)
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

            // カーブの強さは横に動かした量で決まる。向きは押したボタンで決まるので、
            // 左右どちらに動かしても強さは同じになる
            float maxCurve = Mathf.Max(settings.maxCurvePixels, Mathf.Epsilon);
            float curveRatio = Mathf.Clamp01(Mathf.Abs(dragEnd.x - dragStart.x) / maxCurve);
            float curve = curveToRight ? curveRatio : -curveRatio;

            // 引き幅が足りない（または逆向きに動かした）ときは投げない。
            // 表示のためにカーブの値は入れておく
            if (pull < settings.minDragPixels)
            {
                return new ThrowResult { isValid = false, speed = 0f, curve = curve, pullRatio = pullRatio };
            }

            // 横に動かさずに離した場合の扱い。既定では、カーブ0でそのまま投げられる
            if (!settings.allowStraightThrow && curveRatio <= 0f)
            {
                return new ThrowResult { isValid = false, speed = 0f, curve = curve, pullRatio = pullRatio };
            }

            // 最小初速と最大初速の間を取る
            float speed = Mathf.Lerp(settings.minThrowSpeed, settings.maxThrowSpeed, pullRatio);

            return new ThrowResult { isValid = true, speed = speed, curve = curve, pullRatio = pullRatio };
        }
    }
}
