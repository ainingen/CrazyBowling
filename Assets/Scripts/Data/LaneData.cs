using UnityEngine;

namespace CrazyBowling.Data
{
    /// <summary>
    /// レーン1本ぶんの設定。Project ウィンドウで右クリック →
    /// Create → CrazyBowling → レーン から作る。
    /// </summary>
    [CreateAssetMenu(fileName = "LaneData", menuName = "CrazyBowling/レーン", order = 0)]
    public class LaneData : ScriptableObject
    {
        [Header("表示")]
        [Tooltip("画面に出すレーンの名前。例：まっすぐ、S字カーブ")]
        [SerializeField] private string laneName = "まっすぐ";

        [Tooltip("画面に出す短い説明。")]
        [TextArea(2, 4)]
        [SerializeField] private string description = "チュートリアル。まっすぐ転がるだけ。";

        [Header("レーンの中身")]
        [Tooltip("このレーンとして出すプレハブ。空ならシーンにあるレーンをそのまま使う。")]
        [SerializeField] private GameObject lanePrefab;

        [Header("操作")]
        [Tooltip("このレーンで左右に振れる最大角度（度）。既定は2度で、中央からレーンの端に届く。" +
                 "斜めや回転するレーンでは流れを打ち消すために広い角度が要るので、レーンごとに変えられる。")]
        [SerializeField] private float maxAngleDegrees = 2f;

        [Header("物理（段階4で適用する）")]
        [Tooltip("重力の倍率。段階4で適用する。")]
        [SerializeField] private float gravityScale = 1f;

        [Tooltip("ボールと床の摩擦。段階4で適用する。")]
        [SerializeField] private float ballFriction = 0.6f;

        [Header("得点")]
        [Tooltip("このレーンの得点にかける倍率。小数点以下は切り捨てる。")]
        [SerializeField] private float scoreMultiplier = 1f;

        [Tooltip("最終レーンか。ストライクやスペアでボーナス投が出る。")]
        [SerializeField] private bool isFinalLane = false;

        /// <summary>画面に出すレーンの名前。</summary>
        public string LaneName => laneName;

        /// <summary>画面に出す短い説明。</summary>
        public string Description => description;

        /// <summary>このレーンとして出すプレハブ。空ならシーンのレーンを使う。</summary>
        public GameObject LanePrefab => lanePrefab;

        /// <summary>このレーンで左右に振れる最大角度（度）。</summary>
        public float MaxAngleDegrees => maxAngleDegrees;

        /// <summary>重力の倍率。</summary>
        public float GravityScale => gravityScale;

        /// <summary>ボールと床の摩擦。</summary>
        public float BallFriction => ballFriction;

        /// <summary>このレーンの得点にかける倍率。</summary>
        public float ScoreMultiplier => scoreMultiplier;

        /// <summary>最終レーンか。</summary>
        public bool IsFinalLane => isFinalLane;
    }
}
