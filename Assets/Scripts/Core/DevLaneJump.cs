using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace CrazyBowling.Core
{
    /// <summary>
    /// 開発中に、好きなレーンから始めたり途中で飛んだりするための道具。
    ///
    /// 中身は UNITY_EDITOR か DEVELOPMENT_BUILD のときだけ組み込まれる。
    /// 製品版のビルドでは何も実行されず、入力も受け付けない。
    /// PLiCy に上げるビルドでは Development Build のチェックを外すこと。
    /// </summary>
    public class DevLaneJump : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("参照")]
        [Tooltip("飛ばす相手。空なら同じシーンから探す。")]
        [SerializeField] private GameManager gameManager;

        [Header("開始レーン")]
        [Tooltip("再生を始めたときに入るレーン。1で1レーン目。")]
        [SerializeField] private int startLaneNumber = 1;

        [Header("操作")]
        [Tooltip("実行中にキーでレーンを飛べるようにする。")]
        [SerializeField] private bool enableHotkeys = true;

        [Tooltip("数字キー（1〜9、0で10レーン目）で直接飛ぶ。")]
        [SerializeField] private bool useNumberKeys = true;

        [Header("デバッグ")]
        [Tooltip("飛んだことを Console に出す。")]
        [SerializeField] private bool logEvents = true;

        /// <summary>開始レーンへの移動を済ませたか。Start の順番に左右されないよう Update で行う。</summary>
        private bool _appliedStartLane;

        private void Start()
        {
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManager>();
            }
        }

        private void Update()
        {
            if (gameManager == null)
            {
                return;
            }

            // GameManager の Start より先に走っても効くよう、最初の Update で飛ぶ
            if (!_appliedStartLane)
            {
                _appliedStartLane = true;
                if (startLaneNumber > 1)
                {
                    Jump(startLaneNumber, "開始レーンの指定");
                }
            }

            if (enableHotkeys)
            {
                ReadHotkeys();
            }
        }

        /// <summary>キーを読んでレーンを飛ぶ。</summary>
        private void ReadHotkeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.rightBracketKey.wasPressedThisFrame)
            {
                Jump(gameManager.LaneNumber + 1, "次のレーンへ");
                return;
            }

            if (keyboard.leftBracketKey.wasPressedThisFrame)
            {
                Jump(gameManager.LaneNumber - 1, "前のレーンへ");
                return;
            }

            if (!useNumberKeys)
            {
                return;
            }

            // 1〜9 でそのレーン、0 で10レーン目
            Key[] digits =
            {
                Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
                Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0,
            };

            for (int i = 0; i < digits.Length; i++)
            {
                if (keyboard[digits[i]].wasPressedThisFrame)
                {
                    Jump(i + 1, "数字キー");
                    return;
                }
            }
        }

        /// <summary>範囲に収めてから飛ぶ。</summary>
        private void Jump(int laneNumber, string reason)
        {
            int count = gameManager.LaneCount;
            if (count <= 0)
            {
                return;
            }

            int clamped = Mathf.Clamp(laneNumber, 1, count);
            gameManager.JumpToLane(clamped);

            if (logEvents)
            {
                Debug.Log($"[開発用] {clamped}レーン目へ（{reason}）", this);
            }
        }
#else
        // 製品版では何も持たず、何も実行しない
#endif
    }
}
