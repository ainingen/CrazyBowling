// 通しの確認の後始末：Play を止める（run_playthrough.ps1 から使う）。Play を止めると見張り役も外れる
if (!UnityEditor.EditorApplication.isPlaying) return "Play 中ではない";
UnityEditor.EditorApplication.isPlaying = false;
return "Play を止めた";
