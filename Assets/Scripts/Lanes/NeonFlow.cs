using System.Collections.Generic;
using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// ネオンの部品を光らせて流す。
    ///
    /// ── なぜこの作りか ──────────────────────────────
    ///
    /// 光っているだけで動いていないと、数を増やしても地味に見える。
    /// 「流れる」「回る」「順に点く」を足すと、同じ部品数でも一気に派手になる。
    ///
    /// マテリアルは数枚を大勢で共有しているので、マテリアルそのものは書き換えない。
    /// LaneFloorLook と同じく MaterialPropertyBlock で色を差し替える。
    ///
    /// 部品の振り分けは名前で行う。ネオンを組み立てる側と名前の付け方を揃えること。
    ///   Edge?##            … レーンの脇を手前からピンへ流れる（?で列を分ける）
    ///   Panel##            … ピンの奥のLED壁。斜めに波が走る
    ///   Arch*##            … レーンをまたぐ門。手前から順に点く（滑走路の誘導灯）
    ///   Ring#_##           … クレーターの輪。色が回る
    ///   SkyOrb## / Star##  … 空の球。ゆっくり明滅
    ///   それ以外           … ゆっくり明滅
    ///
    /// 当たり判定には一切触らない。色だけを変える。
    /// </summary>
    public class NeonFlow : MonoBehaviour
    {
        /// <summary>光らせ方の種類。名前から振り分ける。</summary>
        private enum Channel
        {
            Edge,
            Arch,
            Ring,
            Panel,
            Orb,
            Misc,
        }

        [Header("明るさ")]
        [Tooltip("いちばん暗いときの明るさ。0にすると消灯まで落ちる。")]
        [SerializeField] private float dimLevel = 0.35f;

        [Tooltip("光が通り過ぎる瞬間の明るさ。1を超えるとHDRになり、Bloomがにじむ。")]
        [SerializeField] private float brightLevel = 6f;

        [Tooltip("色の鮮やかさ。1で原色。下げると白っぽくなる。")]
        [Range(0f, 1f)]
        [SerializeField] private float saturation = 0.9f;

        [Tooltip("物そのものの色の濃さ。光っていない面が真っ黒だと形が見えないので少しだけ乗せる。")]
        [Range(0f, 1f)]
        [SerializeField] private float baseColorStrength = 0.35f;

        [Header("流れ")]
        [Tooltip("両脇のLEDが流れる速さ（1秒あたり何周するか）。")]
        [SerializeField] private float edgeFlowSpeed = 0.55f;

        [Tooltip("両脇のLEDに、光の固まりをいくつ並べるか。")]
        [SerializeField] private float edgePulseCount = 3f;

        [Tooltip("光の尾の鋭さ。大きいほど粒が細かく、小さいほど尾を長く引く。")]
        [SerializeField] private float edgeTailSharpness = 3.5f;

        [Tooltip("アーチが手前から順に点く速さ（1秒あたり何周するか）。")]
        [SerializeField] private float archFlowSpeed = 0.7f;

        [Tooltip("アーチの尾の鋭さ。")]
        [SerializeField] private float archTailSharpness = 2.5f;

        [Tooltip("クレーターの輪で色が回る速さ（1秒あたり何周するか）。")]
        [SerializeField] private float ringSpinSpeed = 0.35f;

        [Tooltip("クレーターの輪の尾の鋭さ。")]
        [SerializeField] private float ringTailSharpness = 2f;

        [Tooltip("ピンの奥のLED壁を波が走る速さ（1秒あたり何周するか）。")]
        [SerializeField] private float panelFlowSpeed = 0.9f;

        [Tooltip("LED壁の尾の鋭さ。")]
        [SerializeField] private float panelTailSharpness = 1.6f;

        [Tooltip("LED壁だけの明るさの倍率。ピンの真後ろにあるので、" +
                 "明るすぎるとピンが影絵になって本数が読めなくなる。")]
        [Range(0f, 1f)]
        [SerializeField] private float panelLevelScale = 1f;

        [Tooltip("空の球などがゆっくり明滅する速さ（1秒あたり何回）。")]
        [SerializeField] private float pulseSpeed = 0.5f;

        [Header("色")]
        [Tooltip("色の輪をまわす速さ（1秒あたり何周するか）。0で色が固定になる。")]
        [SerializeField] private float hueSpeed = 0.08f;

        [Tooltip("ひとつながりの中で、色をどれだけずらすか。1で虹が1周ぶん入る。")]
        [SerializeField] private float hueSpread = 0.8f;

        /// <summary>光らせる相手ひとつぶん。</summary>
        private struct Lamp
        {
            public Renderer Renderer;
            public Channel Channel;

            /// <summary>ひとつながりの中での位置（0が手前、1が奥）。</summary>
            public float Position;

            /// <summary>色の輪の出発点。</summary>
            public float HueOffset;
        }

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Lamp[] _lamps;
        private MaterialPropertyBlock _block;

        private void OnEnable()
        {
            Collect();
        }

        private void OnDisable()
        {
            // 付けた色を外しておく。付けたままでも他のレーンには移らないが、
            // 組み立て直したときに前の色が残って見えるのを防ぐ
            if (_lamps == null)
            {
                return;
            }

            for (int i = 0; i < _lamps.Length; i++)
            {
                if (_lamps[i].Renderer != null)
                {
                    _lamps[i].Renderer.SetPropertyBlock(null);
                }
            }

            _lamps = null;
        }

        /// <summary>子をたどって、名前から光らせ方を振り分ける。</summary>
        [ContextMenu("ネオンを集め直す")]
        private void Collect()
        {
            _block = new MaterialPropertyBlock();

            // いったん名前ごとに集めて、あとで「何番目か」を0〜1に直す
            var raw = new List<Lamp>();
            var counts = new Dictionary<Channel, int>();

            foreach (Renderer meshRenderer in GetComponentsInChildren<Renderer>(true))
            {
                string partName = meshRenderer.gameObject.name;
                Channel channel = ClassifyName(partName, out int order, out int group);

                raw.Add(new Lamp
                {
                    Renderer = meshRenderer,
                    Channel = channel,

                    // ここではまだ「何番目か」をそのまま入れておく
                    Position = order,

                    // 輪や列のかたまりごとに色の出発点をずらし、全部が同じ色にならないようにする
                    HueOffset = group * 0.17f,
                });

                counts.TryGetValue(channel, out int count);
                counts[channel] = Mathf.Max(count, order + 1);
            }

            for (int i = 0; i < raw.Count; i++)
            {
                Lamp lamp = raw[i];
                counts.TryGetValue(lamp.Channel, out int count);
                lamp.Position = lamp.Position / Mathf.Max(1, count - 1);
                raw[i] = lamp;
            }

            _lamps = raw.ToArray();
        }

        /// <summary>名前から光らせ方と並び順を読む。数字が無ければ0番とみなす。</summary>
        private static Channel ClassifyName(string partName, out int order, out int group)
        {
            order = 0;
            group = 0;

            if (partName.StartsWith("Edge"))
            {
                // Edge{列を表す1文字}{番号}。列ごとに色の出発点をずらす
                order = TrailingNumber(partName);
                group = partName.Length > 4 ? partName[4] - 'L' : 0;
                return Channel.Edge;
            }

            if (partName.StartsWith("Panel"))
            {
                order = TrailingNumber(partName);
                return Channel.Panel;
            }

            if (partName.StartsWith("Arch"))
            {
                order = TrailingNumber(partName);
                return Channel.Arch;
            }

            if (partName.StartsWith("Ring"))
            {
                // Ring{クレーター番号}_{輪の中の番号}
                int separator = partName.IndexOf('_');
                if (separator > 4)
                {
                    order = TrailingNumber(partName);
                    int.TryParse(partName.Substring(4, separator - 4), out group);
                }

                return Channel.Ring;
            }

            if (partName.StartsWith("SkyOrb") || partName.StartsWith("Star"))
            {
                order = TrailingNumber(partName);
                group = order;
                return Channel.Orb;
            }

            return Channel.Misc;
        }

        /// <summary>名前の末尾に付いている数字を読む。</summary>
        private static int TrailingNumber(string partName)
        {
            int end = partName.Length;
            int start = end;
            while (start > 0 && char.IsDigit(partName[start - 1]))
            {
                start--;
            }

            if (start == end)
            {
                return 0;
            }

            int.TryParse(partName.Substring(start, end - start), out int value);
            return value;
        }

        private void Update()
        {
            if (_lamps == null)
            {
                return;
            }

            float time = Time.time;

            for (int i = 0; i < _lamps.Length; i++)
            {
                Lamp lamp = _lamps[i];
                if (lamp.Renderer == null)
                {
                    continue;
                }

                float band = BandFor(lamp, time);
                float level = Mathf.Lerp(dimLevel, brightLevel, band);
                if (lamp.Channel == Channel.Panel)
                {
                    level *= panelLevelScale;
                }

                float hue = Mathf.Repeat(
                    lamp.HueOffset + lamp.Position * hueSpread - time * hueSpeed, 1f);
                Color color = Color.HSVToRGB(hue, saturation, 1f);

                _block.Clear();
                _block.SetColor(EmissionColorId, color * level);
                _block.SetColor(BaseColorId, color * baseColorStrength);
                lamp.Renderer.SetPropertyBlock(_block);
            }
        }

        /// <summary>
        /// その瞬間の明るさを0〜1で返す。
        /// 流れるものは彗星のようにする。光の頭がいちばん明るく、うしろへ尾を引く。
        /// </summary>
        private float BandFor(Lamp lamp, float time)
        {
            switch (lamp.Channel)
            {
                case Channel.Edge:
                {
                    float w = Mathf.Repeat(
                        lamp.Position * edgePulseCount - time * edgeFlowSpeed * edgePulseCount, 1f);
                    return Mathf.Pow(1f - w, edgeTailSharpness);
                }

                case Channel.Arch:
                {
                    float w = Mathf.Repeat(lamp.Position - time * archFlowSpeed, 1f);
                    return Mathf.Pow(1f - w, archTailSharpness);
                }

                case Channel.Ring:
                {
                    float w = Mathf.Repeat(lamp.Position - time * ringSpinSpeed, 1f);
                    return Mathf.Pow(1f - w, ringTailSharpness);
                }

                case Channel.Panel:
                {
                    float w = Mathf.Repeat(lamp.Position - time * panelFlowSpeed, 1f);
                    return Mathf.Pow(1f - w, panelTailSharpness);
                }

                default:
                {
                    float phase = (time * pulseSpeed + lamp.Position) * Mathf.PI * 2f;
                    return 0.5f + 0.5f * Mathf.Sin(phase);
                }
            }
        }
    }
}
