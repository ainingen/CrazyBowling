// 通しの確認の前準備：シーンに未保存の変更があれば保存してから Play に入る（run_playthrough.ps1 から使う）
// 保存しないまま Play やテストに入ると、保存ダイアログでエディタが固まることがある
if (UnityEditor.EditorApplication.isPlaying) return "もう Play 中";
var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
bool dirty = sc.isDirty;
if (dirty) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.EditorApplication.isPlaying = true;
return $"Play に入る｜{sc.path}（未保存だった {dirty}）";
