using UnityEngine;

namespace CrazyBowling.Data
{
    /// <summary>
    /// レーン1本ぶんの設定。Project ウィンドウで右クリック →
    /// Create → CrazyBowling → レーン から作る。
    /// ここが持つのは「並び順・表示・得点」だけ。
    /// 物理の癖（重力や摩擦）はレーンのプレハブに付けた LaneBehaviour が持つ。
    /// </summary>
    [CreateAssetMenu(fileName = "LaneData", menuName = "CrazyBowling/レーン", order = 0)]
    public class LaneData : ScriptableObject
    {
        [Header("表示")]
        [Tooltip("画面に出すレーンの名前。例：まっすぐ、傾いた床")]
        [SerializeField] private string laneName = "まっすぐ";

        [Tooltip("画面に出す短い説明。")]
        [TextArea(2, 4)]
        [SerializeField] private string description = "チュートリアル。まっすぐ転がるだけ。";

        [Tooltip("レーンに入ったときに数秒だけ出す一言。空なら出さない。")]
        [SerializeField] private string hintText = "";

        [Header("レーンの中身")]
        [Tooltip("このレーンとして出すプレハブ。空ならレーンを差し替えず、今あるものをそのまま使う。")]
        [SerializeField] private GameObject lanePrefab;

        [Header("操作")]
        [Tooltip("このレーンで左右に振れる最大角度（度）。既定は2度で、中央からレーンの端に届く。" +
                 "斜めや回転するレーンでは流れを打ち消すために広い角度が要るので、レーンごとに変えられる。")]
        [SerializeField] private float maxAngleDegrees = 2f;

        [Header("進行")]
        [Tooltip("このレーンで投げられる回数。通常は2。全部倒したらそこで終わる。")]
        [SerializeField] private int throwCount = 2;

        [Header("得点")]
        [Tooltip("このレーンの得点にかける倍率。小数点以下は切り捨てる。")]
        [SerializeField] private float scoreMultiplier = 1f;

        /// <summary>画面に出すレーンの名前。</summary>
        public string LaneName => laneName;

        /// <summary>画面に出す短い説明。</summary>
        public string Description => description;

        /// <summary>レーンに入ったときに出す一言。空なら出さない。</summary>
        public string HintText => hintText;

        /// <summary>このレーンとして出すプレハブ。空ならレーンを差し替えない。</summary>
        public GameObject LanePrefab => lanePrefab;

        /// <summary>このレーンで左右に振れる最大角度（度）。</summary>
        public float MaxAngleDegrees => maxAngleDegrees;

        /// <summary>このレーンで投げられる回数。</summary>
        public int ThrowCount => Mathf.Max(1, throwCount);

        /// <summary>このレーンの得点にかける倍率。</summary>
        public float ScoreMultiplier => scoreMultiplier;
    }
}
