using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using CrazyBowling.Core;

namespace CrazyBowling.UI
{
    /// <summary>
    /// 押している間だけ奥を見るボタン。
    /// 自動の下見は1レーンに1回しか流れないので、見たいときに自分で見るための手段。
    /// スマホを前提にするので、キーではなく画面のボタンにしている。
    /// </summary>
    public class HoldLookButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("参照")]
        [Tooltip("押していることを伝える相手。")]
        [SerializeField] private CameraPreview cameraPreview;

        [Tooltip("押せないときに薄くして触れなくするための CanvasGroup。空なら自動で足す。" +
                 "GameObject ごと無効にすると、このスクリプトが動かなくなって復帰できない。")]
        [SerializeField] private CanvasGroup buttonGroup;

        [Header("キーでも使う")]
        [Tooltip("キーボードでも押せるようにする。手元で試すとき用。")]
        [SerializeField] private bool enableKey = true;

        [Tooltip("押しているあいだ奥を見るキー。")]
        [SerializeField] private Key lookKey = Key.Space;

        /// <summary>ボタンを指で押しているか。</summary>
        private bool _pointerHeld;

        private void Awake()
        {
            if (buttonGroup == null)
            {
                buttonGroup = GetComponent<CanvasGroup>();
            }
            if (buttonGroup == null)
            {
                buttonGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointerHeld = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pointerHeld = false;
        }

        private void OnDisable()
        {
            // 押したまま隠されたときに、押しっぱなしが残らないようにする
            _pointerHeld = false;
            if (cameraPreview != null)
            {
                cameraPreview.SetLookAroundHeld(false);
            }
        }

        private void Update()
        {
            if (cameraPreview == null)
            {
                return;
            }

            bool canUse = cameraPreview.CanLookAround;
            if (buttonGroup != null)
            {
                buttonGroup.alpha = canUse ? 1f : 0f;
                buttonGroup.interactable = canUse;
                buttonGroup.blocksRaycasts = canUse;
            }

            bool keyHeld = enableKey && Keyboard.current != null && Keyboard.current[lookKey].isPressed;
            cameraPreview.SetLookAroundHeld(canUse && (_pointerHeld || keyHeld));
        }
    }
}
