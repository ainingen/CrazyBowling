// テスト（run_tests）や Play の前に必ず走らせる：シーンに未保存の変更があれば保存する
//   unity command eval_file DevTools/save_scene.cs --project-path=C:\dev\CrazyBowling
// 未保存のままテストや Play に入ると「Scene(s) Have Been Modified」のダイアログが出て、エディタが止まる
// （CLI には 30秒の時間切れとしか見えない）。「未保存だった True」と出たら、何を変えたかを確かめて報告すること
if (UnityEditor.EditorApplication.isPlaying) return "Play 中なので保存しない（先に Play を止める）";
var sb = new System.Text.StringBuilder();
for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
{
    var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
    bool dirty = sc.isDirty;
    if (dirty) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
    sb.Append($"{sc.path}（未保存だった {dirty}）");
}
UnityEditor.AssetDatabase.SaveAssets();
return "保存を確かめた｜" + sb;
