using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CrazyBowling.UI
{
    /// <summary>
    /// 光る UI を実行時に組み立てるための道具（段階6）。
    /// 得点板の升目や結果画面の行のように、数が決まるまで作れないものに使う。
    ///
    /// ★どの演出も、時間は Time.unscaledTime で進める（ゲームの時間が遅くなっても UI は止まらない）。
    /// ★ぱかぱか点滅させない。明るさはゆっくり呼吸させるか、色を流すだけにする。
    /// </summary>
    public static class NeonUI
    {
        /// <summary>子の RectTransform を作る。</summary>
        public static RectTransform CreateRect(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        /// <summary>親いっぱいに広がる RectTransform を作る。</summary>
        public static RectTransform CreateStretch(Transform parent, string name)
        {
            return CreateRect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        /// <summary>画像を作る。押せないようにしておく（下の操作を邪魔しない）。</summary>
        public static Image CreateImage(RectTransform rect, Sprite sprite, Color color, bool sliced)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>文字を作る。押せないようにしておく。</summary>
        public static TextMeshProUGUI CreateText(RectTransform rect, TMP_FontAsset font, Material material,
            float size, Color color, TextAlignmentOptions alignment)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
            }
            if (material != null)
            {
                label.fontSharedMaterial = material;
            }
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        /// <summary>
        /// 光る太字の縁取りと光の色を変える（その文字だけのマテリアルを作って変える）。
        /// 光の強さ glow は 0〜1。
        /// </summary>
        public static void SetNeonColor(TMP_Text label, Color color, float glow)
        {
            if (label == null)
            {
                return;
            }

            Material material = label.fontMaterial;
            material.SetColor(ShaderUtilities.ID_OutlineColor, color);
            Color glowColor = color;
            glowColor.a = Mathf.Clamp01(glow);
            material.SetColor(ShaderUtilities.ID_GlowColor, glowColor);
        }

        /// <summary>色相から色を作る。</summary>
        public static Color Hue(float hue, float saturation = 0.9f, float value = 1f)
        {
            return Color.HSVToRGB(Mathf.Repeat(hue, 1f), saturation, value);
        }

        /// <summary>明るさだけを掛ける（透明度は変えない）。</summary>
        public static Color Scale(Color color, float brightness)
        {
            return new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
        }

        /// <summary>透明度だけを変える。</summary>
        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>ゆっくりした呼吸（0〜1）。period 秒で1回。点滅ではなく、なめらかに明るさが上下する。</summary>
        public static float Breath(float period, float offset = 0f)
        {
            return 0.5f + 0.5f * Mathf.Sin((Time.unscaledTime / Mathf.Max(period, 0.01f) + offset) * Mathf.PI * 2f);
        }

        /// <summary>行きすぎてから戻る（飛び込んでくる動き）。</summary>
        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        /// <summary>速く動いてゆっくり止まる。</summary>
        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        /// <summary>ゆっくり動き出して速く抜ける。</summary>
        public static float EaseInCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        /// <summary>弾んで止まる（上から落ちてくる動き）。</summary>
        public static float EaseOutBounce(float t)
        {
            t = Mathf.Clamp01(t);
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }
}
