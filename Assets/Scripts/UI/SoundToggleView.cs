using CrazyBowling.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyBowling.UI
{
    /// <summary>
    /// 音を消す・戻すボタン（段階6）。画面の隅に置く。
    /// 選んだ状態は鳴らし係が残すので、次に起動したときも同じになる。
    /// 消すと BGM も効果音もすべて聞こえなくなる（AudioListener の音量を 0 にする）。
    /// </summary>
    public class SoundToggleView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>(true);
            }
            if (button != null)
            {
                button.onClick.AddListener(Toggle);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(Toggle);
            }
        }

        private void LateUpdate()
        {
            SoundPlayer player = SoundPlayer.Instance;
            if (label != null && player != null)
            {
                string text = player.Muted ? UIText.SoundOff : UIText.SoundOn;
                if (label.text != text)
                {
                    label.text = text;
                }
            }
        }

        /// <summary>音を消す・戻すを切り替える。戻したときはボタンの音を鳴らす。</summary>
        public void Toggle()
        {
            SoundPlayer player = SoundPlayer.Instance;
            if (player == null)
            {
                return;
            }
            player.Unlock();
            player.Muted = !player.Muted;
            if (!player.Muted)
            {
                player.PlayButton();
            }
        }
    }
}
