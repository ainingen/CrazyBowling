using UnityEngine;

namespace CrazyBowling.Core
{
    /// <summary>
    /// 遊んだ人の成績を記録する係（段階6）。GameManager と同じ物に置く。
    /// 1本目から10本目まで最後まで遊んで結果画面が出たゲームだけを記録する。次のゲームは記録しない：
    /// 途中でやめたゲーム（終わらないので知らせが来ない）・レーンへ直行したゲーム・10本すべてを遊んでいないゲーム。
    /// 得点の計算には関わらない（GameManager が出した得点を写すだけ）。
    /// </summary>
    public class RecordKeeper : MonoBehaviour
    {
        [Tooltip("得点を読む相手。空なら同じシーンから探す。")]
        [SerializeField] private GameManager gameManager;

        [Tooltip("RANK の境目を読む結果画面。空なら同じシーンから探す。")]
        [SerializeField] private UI.ResultView resultView;

        [Tooltip("最近の成績を何ゲームぶん残すか。これを超えたら古いものから消す。")]
        [SerializeField] private int keepRecentGames = 20;

        /// <summary>シーンに置いた記録の係。無ければ null。</summary>
        public static RecordKeeper Instance { get; private set; }

        /// <summary>最後に終わったゲームを記録したか（結果画面の NEW RECORD! は、記録したときだけ出す）。</summary>
        public bool LastGameRecorded { get; private set; }

        /// <summary>最後に記録したときに更新したもの。</summary>
        public RecordUpdate LastUpdate { get; private set; }

        /// <summary>最後に記録しなかった理由（確かめるとき用）。</summary>
        public string LastSkipReason { get; private set; } = "";

        /// <summary>最近の成績を残す数。</summary>
        public int KeepRecentGames => keepRecentGames;

        /// <summary>今の記録帳を読む（記録の画面が使う）。</summary>
        public RecordBook Load()
        {
            return RecordStore.Load(gameManager != null ? gameManager.LaneCount : 10);
        }

        private void Awake()
        {
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManager>();
            }
            if (resultView == null)
            {
                resultView = FindFirstObjectByType<UI.ResultView>(FindObjectsInactive.Include);
            }
        }

        private void OnEnable()
        {
            Instance = this;
            if (gameManager != null)
            {
                gameManager.GameFinished += OnGameFinished;
            }
        }

        private void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.GameFinished -= OnGameFinished;
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnGameFinished()
        {
            LastGameRecorded = false;
            LastUpdate = default;
            if (!gameManager.IsCleanGame)
            {
                LastSkipReason = "レーンへ直行したゲームなので記録しない";
                Debug.Log(LastSkipReason, this);
                return;
            }
            for (int i = 0; i < gameManager.LaneCount; i++)
            {
                if (!gameManager.IsLanePlayed(i))
                {
                    LastSkipReason = $"{i + 1}本目を遊んでいないので記録しない";
                    Debug.Log(LastSkipReason, this);
                    return;
                }
            }
            Record(BuildGame());
        }

        /// <summary>今のゲームの記録を作る（GameManager の得点を写すだけ）。</summary>
        private GameRecord BuildGame()
        {
            int count = gameManager.LaneCount;
            var lanes = new LaneRecord[count];
            for (int i = 0; i < count; i++)
            {
                LaneThrowResult r = gameManager.GetLaneResult(i);
                lanes[i] = RecordRules.MakeLane(r.firstThrowFallen, r.secondThrowFallen, r.throwCount, r.pinCount, gameManager.GetLaneScore(i).score);
            }
            return new GameRecord
            {
                playedAt = System.DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
                total = gameManager.TotalScore,
                // 結果画面と同じ RANK の表で決める（表が無ければ既定の値）
                rank = Data.RankTable.Decide(resultView != null ? resultView.RankTable : null, gameManager.TotalScore),
                lanes = lanes,
            };
        }

        /// <summary>1ゲームを記録帳に足して保存する。</summary>
        public RecordUpdate Record(GameRecord game)
        {
            RecordBook book = Load();
            RecordUpdate update = RecordRules.Add(book, game, keepRecentGames);
            RecordStore.Save(book);
            LastGameRecorded = true;
            LastUpdate = update;
            LastSkipReason = "";
            Debug.Log($"成績を記録した：{game.total}点・RANK {game.rank}（自己ベストの更新 {update.newTotalBest}）", this);
            return update;
        }
    }
}
