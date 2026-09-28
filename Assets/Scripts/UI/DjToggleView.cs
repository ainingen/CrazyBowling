using CrazyBowling.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyBowling.UI
{
    /// <summary>
    /// DJ のラジオ番組だけを消す・戻すボタン（段階6）。音を消すボタンの隣に置く（見た目は同じネオン）。
    /// 選んだ状態は番組の係が残すので、次に起動したときも同じになる。BGM と効果音はそのまま。
    /// </summary>
    public class DjToggleView : MonoBehaviour
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
            DjRadio dj = DjRadio.Instance;
            if (label != null && dj != null)
            {
                string text = dj.DjMuted ? UIText.DjOff : UIText.DjOn;
                if (label.text != text)
                {
                    label.text = text;
                }
            }
        }

        /// <summary>DJ を消す・戻すを切り替える。ボタンの音を鳴らす。</summary>
        public void Toggle()
        {
            DjRadio dj = DjRadio.Instance;
            if (dj == null)
            {
                return;
            }
            SoundPlayer player = SoundPlayer.Instance;
            if (player != null)
            {
                player.PlayButton();
            }
            dj.DjMuted = !dj.DjMuted;
        }
    }
}
