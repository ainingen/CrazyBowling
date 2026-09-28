using CrazyBowling.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyBowling.UI
{
    /// <summary>
    /// LOOK AHEAD の見回し中だけ出す「BACK」ボタンと「DRAG TO LOOK AROUND」の案内（段階6）。
    /// BACK を押すと、向きと寄り引きを元へ戻しながら構えの視点へ帰る。
    /// 出し入れは CanvasGroup で行う（GameObject ごと無効にすると、このスクリプトが止まって出せなくなる）。
    /// </summary>
    public class LookAroundView : MonoBehaviour
    {
        [Tooltip("見回しの係（カメラの CameraPreview）。空なら探す。")]
        [SerializeField] private CameraPreview cameraPreview;

        [Tooltip("BACK ボタン。")]
        [SerializeField] private Button backButton;

        [Tooltip("BACK ボタンの出し入れ。空なら BACK ボタンに足す。")]
        [SerializeField] private CanvasGroup backGroup;

        [Tooltip("「DRAG TO LOOK AROUND」の文字。")]
        [SerializeField] private TMP_Text hintLabel;

        private void Awake()
        {
            if (cameraPreview == null)
            {
                cameraPreview = FindFirstObjectByType<CameraPreview>(FindObjectsInactive.Include);
            }
            if (backButton != null)
            {
                if (backGroup == null)
                {
                    backGroup = backButton.GetComponent<CanvasGroup>();
                }
                if (backGroup == null)
                {
                    backGroup = backButton.gameObject.AddComponent<CanvasGroup>();
                }
                backButton.onClick.AddListener(OnBack);
                TMP_Text label = backButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = UIText.LookBack;
                }
            }
            if (hintLabel != null)
            {
                hintLabel.text = UIText.LookAroundHint;
            }
            Show(false);
        }

        private void OnDestroy()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveListener(OnBack);
            }
        }

        private void LateUpdate()
        {
            Show(cameraPreview != null && cameraPreview.IsLookingAround);
        }

        private void Show(bool on)
        {
            if (backGroup != null)
            {
                backGroup.alpha = on ? 1f : 0f;
                backGroup.interactable = on;
                backGroup.blocksRaycasts = on;
            }
            if (hintLabel != null)
            {
                hintLabel.enabled = on;
            }
        }

        /// <summary>BACK：見回しを終えて構えの視点へ戻る。</summary>
        private void OnBack()
        {
            if (cameraPreview != null)
            {
                cameraPreview.EndLookAround();
            }
        }
    }
}
