using CrazyBowling.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyBowling.UI
{
    /// <summary>
    /// ボタンを押したときの音（段階6）。ボタンに付ける。
    /// Button なら押したとき、Button でない押しっぱなしのボタン（LOOK AHEAD）なら押し始めたときに鳴らす。
    /// 押したときに、ブラウザで音を鳴らせる状態にもする（タイトルの CLICK TO START が最初の1回）。
    /// ボタンの働きには触らない。
    /// </summary>
    public class ButtonSound : MonoBehaviour, IPointerDownHandler
    {
        [Tooltip("押し始めたときに鳴らす（Button でない、押しっぱなしのボタン用）。切ると、押し終えたとき（Button の onClick）に鳴らす。")]
        [SerializeField] private bool onPointerDown;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_button != null && !onPointerDown)
            {
                _button.onClick.AddListener(PlaySound);
            }
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(PlaySound);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (onPointerDown)
            {
                PlaySound();
            }
        }

        private void PlaySound()
        {
            SoundPlayer player = SoundPlayer.Instance;
            if (player != null)
            {
                player.PlayButton();
            }
        }
    }
}
