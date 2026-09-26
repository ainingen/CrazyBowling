using UnityEngine;
using TMPro;

namespace CrazyBowling.UI
{
    /// <summary>
    /// 文字に、1文字ずつずれた虹色を流す（タイトルのロゴ、STRIKE!、結果の合計など）。
    /// 頂点の色を毎フレーム塗り直すだけで、文字の中身には触らない。
    ///
    /// ★色が流れるだけで、明るさは変えない（点滅させない）。
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class NeonTextRainbow : MonoBehaviour
    {
        [Tooltip("色が流れる速さ（1秒あたりの色相の回転）。")]
        [SerializeField] private float speed = 0.25f;

        [Tooltip("隣の文字との色のずれ。")]
        [SerializeField] private float spread = 0.07f;

        [Tooltip("色の鮮やかさ。")]
        [SerializeField] private float saturation = 0.75f;

        [Tooltip("文字の下の側の色の明るさ（上は白に近く、下ほど色が濃い）。")]
        [SerializeField] private float bottomValue = 1f;

        [Tooltip("上の側を白に寄せる割合。0で上下とも同じ色。")]
        [SerializeField] private float topWhiten = 0.45f;

        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void LateUpdate()
        {
            if (_text == null || !_text.enabled)
            {
                return;
            }

            _text.ForceMeshUpdate();
            TMP_TextInfo info = _text.textInfo;
            float time = Time.unscaledTime * speed;
            byte alpha = (byte)Mathf.RoundToInt(_text.color.a * 255f);

            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible)
                {
                    continue;
                }

                Color bottom = NeonUI.Hue(i * spread - time, saturation, bottomValue);
                Color top = Color.Lerp(bottom, Color.white, topWhiten);
                Color32[] colors = info.meshInfo[character.materialReferenceIndex].colors32;
                int v = character.vertexIndex;
                Color32 b = bottom; b.a = alpha;
                Color32 t = top; t.a = alpha;
                colors[v] = b;
                colors[v + 1] = t;
                colors[v + 2] = t;
                colors[v + 3] = b;
            }

            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}
