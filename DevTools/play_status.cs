// 通しの確認の状態を返す（run_playthrough.ps1 から使う）。Play 中で、ゲームの部品が揃ったら「準備できた」
if (!UnityEditor.EditorApplication.isPlaying) return "Play 中ではない";
var gm = UnityEngine.Object.FindFirstObjectByType<CrazyBowling.Core.GameManager>();
if (gm == null || CrazyBowling.Core.SoundPlayer.Instance == null || UnityEngine.Time.unscaledTime < 1f) return "準備中";
return "準備できた";
