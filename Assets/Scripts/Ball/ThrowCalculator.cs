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
        public float maxPullPixels;

        /// <summary>これ以下の引き幅は「投げない」とみなす（ピクセル）。</summary>
        public float minPullPixels;

        /// <summary>最大の角度になる横ずれ（ピクセル）。</summary>
        public float maxAnglePixels;

        /// <summary>左右に振れる最大角度（度）。レーンごとに上書きできる。</summary>
        public float maxAngleDegrees;

        /// <summary>この横ずれまでは角度を付けない（ピクセル）。手ブレ対策。</summary>
        public float angleDeadZonePixels;

        /// <summary>最小の初速（m/s）。</summary>
        public float minThrowSpeed;

        /// <summary>最大の初速（m/s）。</summary>
        public float maxThrowSpeed;

        /// <summary>カーブが最大になるスライダーの幅。今は使っていない（カーブは -1〜+1 で直接渡す）。</summary>
        public float maxCurvePixels;
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

        /// <summary>レーン正面から左右に振る角度（度）。正が右向き。</summary>
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
        /// 引いた向きの逆へ飛ぶ、パチンコのような操作。
        /// 縦の成分が強さ、横の成分が角度になる。
        /// </summary>
        /// <param name="dragStart">押した位置。</param>
        /// <param name="dragEnd">今の位置（離した位置）。</param>
        /// <param name="settings">調整用の設定値。</param>
        /// <param name="curve">カーブの強さと向き。構え中のスライダーで決めた値をそのまま渡す。</param>
        /// <param name="invertPull">引く向きを反転する（奥に払って投げる操作にする）。</param>
        public static ThrowResult Calculate(
            Vector2 dragStart,
            Vector2 dragEnd,
            ThrowSettings settings,
            float curve,
            bool invertPull = false)
        {
            // 0除算を避けるため、基準は必ず正の値にする
            float maxPull = Mathf.Max(settings.maxPullPixels, Mathf.Epsilon);

            // 手前（画面下）に引くと正になる
            float pull = dragStart.y - dragEnd.y;
            if (invertPull)
            {
                pull = -pull;
            }

            // 引き幅を 0〜1 に正規化する。表示に使うので、投げない場合も先に求める
            float pullRatio = Mathf.Clamp01(pull / maxPull);
            float sideAngle = CalculateSideAngle(dragEnd.x - dragStart.x, settings);

            // 引き幅が足りない（または逆向きに動かした）ときは投げない。
            // 表示のために角度とカーブの値は入れておく
            if (pull < settings.minPullPixels)
            {
                return new ThrowResult
                {
                    isValid = false,
                    speed = 0f,
                    curve = curve,
                    sideAngle = sideAngle,
                    pullRatio = pullRatio,
                };
            }

            // 最小初速と最大初速の間を取る
            float speed = Mathf.Lerp(settings.minThrowSpeed, settings.maxThrowSpeed, pullRatio);

            return new ThrowResult
            {
                isValid = true,
                speed = speed,
                curve = curve,
                sideAngle = sideAngle,
                pullRatio = pullRatio,
            };
        }

        /// <summary>
        /// 横ずれから投げ出す角度を求める。
        ///
        /// 引いたベクトルの実際の角度は使わない。15.7m先では1度で27cm動くため、
        /// ±4度を実角度で出そうとすると横17pxしか幅が無く、操作できなくなる。
        /// 「横ずれ何pxで最大何度」という対応にして、操作しやすい幅を確保する。
        /// 見た目の引き方向と実際の角度は一致しないが、実角度は予測線で見せる。
        /// </summary>
        /// <param name="sideOffsetPixels">押した位置からの横ずれ（右が正）。</param>
        public static float CalculateSideAngle(float sideOffsetPixels, ThrowSettings settings)
        {
            float deadZone = Mathf.Max(settings.angleDeadZonePixels, 0f);
            float magnitude = Mathf.Abs(sideOffsetPixels);

            // 手ブレでは角度を付けない
            if (magnitude <= deadZone)
            {
                return 0f;
            }

            // デッドゾーンのぶんを差し引いてから正規化する。
            // 単純に割ると、最大の横ずれでも最大角度に届かなくなる
            float range = Mathf.Max(settings.maxAnglePixels - deadZone, Mathf.Epsilon);
            float ratio = Mathf.Clamp01((magnitude - deadZone) / range);

            // 引いた向きの逆へ飛ぶので、符号を反転する
            float sign = sideOffsetPixels > 0f ? -1f : 1f;
            return sign * ratio * settings.maxAngleDegrees;
        }
    }
}
