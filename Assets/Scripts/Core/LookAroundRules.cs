using UnityEngine;

namespace CrazyBowling.Core
{
    /// <summary>LOOK AHEAD の見回しで振れる幅（段階6）。レーンごとに変えられる（<see cref="Lanes.LaneLookAround"/>）。</summary>
    [System.Serializable]
    public struct LookAroundLimits
    {
        [Tooltip("左右に向きを変えられる角度（度。片側）。")]
        public float yaw;

        [Tooltip("上を向ける角度（度）。")]
        public float pitchUp;

        [Tooltip("下を向ける角度（度）。")]
        public float pitchDown;

        [Tooltip("寄れる量（画角を狭める度数）。")]
        public float zoomIn;

        [Tooltip("引ける量（画角を広げる度数）。")]
        public float zoomOut;

        /// <summary>既定の幅：左右 60度・上 20度・下 20度・寄り 15度・引き 5度。</summary>
        public static LookAroundLimits Default => new LookAroundLimits { yaw = 60f, pitchUp = 20f, pitchDown = 20f, zoomIn = 15f, zoomOut = 5f };
    }

    /// <summary>
    /// LOOK AHEAD の見回しの計算（段階6）。MonoBehaviour に依存しない。
    /// カメラの位置は動かさず、止まった所の向きに左右・上下の角度を足し、画角を狭めたり広げたりするだけ
    /// （壁や柱へのすり抜けを起こさないため）。
    /// </summary>
    public static class LookAroundRules
    {
        /// <summary>
        /// ドラッグを向きに足す。右へドラッグすると右を向き、上へドラッグすると上を向く。
        /// 戻り値は幅で止めた (左右, 上下)。上下は上が正。
        /// </summary>
        /// <param name="degreesPerPixel">ドラッグ1ピクセルあたりの角度。</param>
        public static Vector2 ApplyDrag(Vector2 current, Vector2 dragPixels, float degreesPerPixel, LookAroundLimits limits)
        {
            float yaw = current.x + dragPixels.x * degreesPerPixel;
            float pitch = current.y + dragPixels.y * degreesPerPixel;
            return Clamp(new Vector2(yaw, pitch), limits);
        }

        /// <summary>向きを幅に収める。</summary>
        public static Vector2 Clamp(Vector2 look, LookAroundLimits limits)
        {
            float yaw = Mathf.Clamp(look.x, -Mathf.Abs(limits.yaw), Mathf.Abs(limits.yaw));
            float pitch = Mathf.Clamp(look.y, -Mathf.Abs(limits.pitchDown), Mathf.Abs(limits.pitchUp));
            return new Vector2(yaw, pitch);
        }

        /// <summary>
        /// 寄り引きを足す（画角の変化。負で寄る＝画角を狭める、正で引く）。幅で止める。
        /// </summary>
        public static float ApplyZoom(float current, float delta, LookAroundLimits limits)
        {
            return Mathf.Clamp(current + delta, -Mathf.Abs(limits.zoomIn), Mathf.Abs(limits.zoomOut));
        }

        /// <summary>
        /// 2本指の間の距離の変化を、画角の変化にする。指を広げる（距離が増える）と寄る。
        /// </summary>
        /// <param name="degreesPerPixel">指の距離1ピクセルあたりの画角の変化。</param>
        public static float PinchToZoom(float previousDistance, float currentDistance, float degreesPerPixel)
        {
            return -(currentDistance - previousDistance) * degreesPerPixel;
        }

        /// <summary>
        /// ホイールの回転を、画角の変化にする。奥へ回す（正）と寄る。
        /// </summary>
        public static float WheelToZoom(float wheel, float degreesPerNotch)
        {
            // Input System のホイールは1目盛りが 120 前後（環境による）。符号だけでなく量も使うが、1回で動く量は抑える
            float notches = Mathf.Clamp(wheel / 120f, -3f, 3f);
            return -notches * degreesPerNotch;
        }

        /// <summary>
        /// 止まった所の向きに見回しを足した向き。左右は世界の上下の軸まわり、上下はカメラの横の軸まわり（上が正）。
        /// 見回しが 0 なら止まった所の向きと同じ（見回しを始めても画面が飛ばない）。
        /// </summary>
        public static Quaternion Rotate(Quaternion baseRotation, Vector2 look)
        {
            return Quaternion.AngleAxis(look.x, Vector3.up) * baseRotation * Quaternion.AngleAxis(-look.y, Vector3.right);
        }

        /// <summary>見回しを 0 へ近づける（BACK で戻るとき）。</summary>
        public static Vector2 Relax(Vector2 look, float degreesPerSecond, float deltaTime)
        {
            return Vector2.MoveTowards(look, Vector2.zero, degreesPerSecond * deltaTime);
        }
    }
}
