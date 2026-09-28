// ============================================================================
// 1〜10本目の通し確認（2ゲーム）の見張り役
//
// 使い方：DevTools/run_playthrough.ps1 から走らせる（Play に入る → これを登録 → 待つ → Play を止める）。
// 手で走らせるときは、Play に入ってから
//   unity command eval_file DevTools/playthrough_check.cs --project-path=C:\dev\CrazyBowling
//
// ・unity command eval_file で実行する「eval のコード」。Assets の外に置いてあるので、Unity はコンパイルせず、ビルドにも入らない
// ・Play 中に EditorApplication.update へ見張り役を登録するだけ。メモリの上にしか無く、Play を止めるか、2ゲーム終わると外れる
//   （自動で走るコードではない）
// ・eval の決まりで using は書けないので、型はすべて名前空間から書く
// ・結果は Logs/playthrough_check.log（git に入らない）。最後の「まとめ」の行を見る
//
// 見ること（仕様.md の「1〜10本目の通し確認」）
//   本数の矛盾・判定のタイムアウト・警告・エラー・各レーン開始時のピン・共有設定（1ゲーム目と2ゲーム目で同じか）・
//   動いているべきものが構え中に動いているか・BGM の切り替わり・音の記録（8本目の音・前のレーンの音の残り）
// 投げ方
//   ふつう：1投目 速10・真ん中／2投目 速9・立ち位置 +0.25
//   8本目：速8・真ん中（ピンが場外に止まる）
//   9本目：1投目は神殿の入り口が奥を向いて止まる角度に置いて阻まれる投球、2投目は入り口が手前を向いて止まる角度に置いて入る投球（どちらも速8・真ん中）
// ============================================================================

var BF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var gm = UnityEngine.Object.FindFirstObjectByType<CrazyBowling.Core.GameManager>();
var seq = UnityEngine.Object.FindFirstObjectByType<CrazyBowling.Core.ThrowSequencer>();
var ball = UnityEngine.Object.FindFirstObjectByType<CrazyBowling.Ball.BallController>();
var sp = CrazyBowling.Core.SoundPlayer.Instance;
var pinSet = UnityEngine.Object.FindFirstObjectByType<CrazyBowling.Pins.PinSet>();
var blast = UnityEngine.Object.FindFirstObjectByType<CrazyBowling.Pins.PinExplosion>();
var preview = UnityEngine.Object.FindFirstObjectByType<CrazyBowling.Core.CameraPreview>();
var camCtl = UnityEngine.Object.FindFirstObjectByType<CrazyBowling.Core.CameraController>();
var resultView = UnityEngine.Object.FindFirstObjectByType<CrazyBowling.UI.ResultView>(UnityEngine.FindObjectsInactive.Include);
var title = UnityEngine.Object.FindFirstObjectByType<CrazyBowling.UI.TitleView>(UnityEngine.FindObjectsInactive.Include);
var tune = UnityEngine.Object.FindFirstObjectByType<CrazyBowling.UI.TuneInView>(UnityEngine.FindObjectsInactive.Include);
if (!UnityEditor.EditorApplication.isPlaying) return "Play 中ではない。先に Play に入ること";
if (gm == null || seq == null || ball == null || sp == null || pinSet == null || resultView == null) return "部品が見つからない（Play に入った直後なら、少し待ってからもう一度）";

var fSeqState = typeof(CrazyBowling.Core.ThrowSequencer).GetField("_state", BF);
var fSide = typeof(CrazyBowling.Ball.BallController).GetField("_sideOffset", BF);
var mApplySpawn = typeof(CrazyBowling.Ball.BallController).GetMethod("ApplySpawnPosition", BF);
var mThrow = typeof(CrazyBowling.Ball.BallController).GetMethod("Throw", BF);
var fBgm = typeof(CrazyBowling.Core.SoundPlayer).GetField("_bgm", BF);
var fLaneInst = typeof(CrazyBowling.Core.GameManager).GetField("_laneInstance", BF);
var fCulled = typeof(CrazyBowling.Pins.Pin).GetField("_culled", BF);
var fDisc = typeof(CrazyBowling.Lanes.CoffeeCupRide).GetField("_discAngle", BF);
var mRideApply = typeof(CrazyBowling.Lanes.CoffeeCupRide).GetMethod("Apply", BF);
var fFloors = typeof(CrazyBowling.Lanes.CoasterRail).GetField("movingFloors", BF);
var body = ball.GetComponent<UnityEngine.Rigidbody>();
var rollSound = ball.GetComponentInChildren<CrazyBowling.Ball.BallRollSound>(true);
var table = sp.Table;

string logPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Logs", "playthrough_check.log"));
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath));
System.IO.File.WriteAllText(logPath, "");
var buf = new System.Text.StringBuilder();
System.Action<string> W = s => buf.AppendLine(s);

// ---- 数えるもの ----
int warn = 0, err = 0, timeouts = 0, throws = 0, lanesFinished = 0, contradictions = 0;
int moveChecks = 0, moveStopped = 0, pinStartBad = 0, sharedMismatch = 0, bgmBad = 0, switches = 0, residue = 0, notFound = 0;
int lane9Enter = 0, lane9Block = 0;
var shared = new System.Collections.Generic.Dictionary<int, string>[] { new System.Collections.Generic.Dictionary<int, string>(), new System.Collections.Generic.Dictionary<int, string>() };
var bgmSeen = new System.Collections.Generic.List<string>();
int game = 1, runningFallen = 0;

// ---- ログ（警告・エラー・タイムアウト） ----
UnityEngine.Application.LogCallback onLog = (msg, st, type) =>
{
    if (type == UnityEngine.LogType.Warning) { warn++; W("★警告｜" + msg); }
    else if (type != UnityEngine.LogType.Log) { err++; W("★エラー｜" + msg + "｜" + st); }
    else if (msg.Contains("静止しないまま")) { timeouts++; W("★タイムアウト｜" + msg); }
};

// ---- 本数の矛盾 ----
System.Action<CrazyBowling.Core.ThrowJudgement> onJudged = j =>
{
    runningFallen = j.throwNumber == 1 ? j.fallen : runningFallen + j.fallen;
    W($"{UnityEngine.Time.unscaledTime:F2} {game}ゲーム {gm.LaneNumber}本目 {j.throwNumber}投目：{j.fallen}本（合計 {j.totalFallen}本）{(j.isStrike ? " ストライク" : j.isSpare ? " スペア" : "")}");
    if (j.fallen < 0 || j.totalFallen > j.pinCount || j.totalFallen != runningFallen) { contradictions++; W($"★本数の矛盾｜判定 {j.fallen}・合計 {j.totalFallen}・数え直し {runningFallen}・ピン {j.pinCount}"); }
};
System.Action<CrazyBowling.Core.LaneThrowResult> onLaneFinished = r =>
{
    lanesFinished++;
    int i = gm.LaneNumber - 1;
    var sc = gm.GetLaneScore(i);
    var data = gm.GetLaneData(i);
    var re = CrazyBowling.Core.ScoreCalculator.Calculate(r, data != null ? data.ScoreMultiplier : 1f);
    bool bad = r.TotalFallen != sc.fallen || r.firstThrowFallen < 0 || r.secondThrowFallen < 0 || r.TotalFallen > r.pinCount
               || sc.score != re.score || sc.isStrike != (r.throwCount == 1 && r.firstThrowFallen == r.pinCount);
    W($"{UnityEngine.Time.unscaledTime:F2} {game}ゲーム {gm.LaneNumber}本目 終わり｜1投目 {r.firstThrowFallen}・2投目 {r.secondThrowFallen}・{sc.score}点");
    if (bad) { contradictions++; W($"★本数の矛盾｜結果 {r.TotalFallen}本・得点表 {sc.fallen}本 {sc.score}点・計算し直し {re.score}点"); }
};

// ---- 子を名前で探す ----
System.Func<UnityEngine.Transform, string, UnityEngine.Transform> find = null;
find = (t, n) =>
{
    if (t == null) return null;
    if (t.name == n) return t;
    foreach (UnityEngine.Transform c in t) { var r = find(c, n); if (r != null) return r; }
    return null;
};
System.Func<UnityEngine.GameObject> laneObj = () => (UnityEngine.GameObject)fLaneInst.GetValue(gm);

// ---- 共有設定を文字にする（1ゲーム目と2ゲーム目で比べる） ----
System.Func<string> sharedText = () =>
{
    var s = new System.Text.StringBuilder();
    foreach (var l in UnityEngine.Object.FindObjectsByType<UnityEngine.Light>(UnityEngine.FindObjectsSortMode.InstanceID))
        if (l.type == UnityEngine.LightType.Directional && !(laneObj() != null && l.transform.IsChildOf(laneObj().transform)))
            s.Append($"平行光源 {l.intensity:F3} {l.color}｜");
    s.Append($"環境光 {UnityEngine.RenderSettings.ambientMode} {UnityEngine.RenderSettings.ambientLight} {UnityEngine.RenderSettings.ambientIntensity:F3}｜");
    var camData = UnityEngine.Camera.main != null ? UnityEngine.Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
    // 追加データが「付いていない」と「付いていて切」は同じとみなす（仕様.md の注意）
    s.Append($"ポストプロセス {(camData != null && camData.renderPostProcessing ? "入" : "切")} {(camData != null ? camData.antialiasing.ToString() : "None")}｜");
    int strayVolume = 0;
    foreach (var v in UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(UnityEngine.FindObjectsSortMode.None))
        if (laneObj() == null || !v.transform.IsChildOf(laneObj().transform)) strayVolume++;
    s.Append($"レーンの外の Volume {strayVolume}｜");
    s.Append("ボール");
    foreach (var r in ball.GetComponentsInChildren<UnityEngine.Renderer>(true)) s.Append($" {r.name}:{(r.sharedMaterial != null ? r.sharedMaterial.name : "-")}");
    s.Append($" 子{ball.transform.childCount}｜");
    var pins = pinSet.Pins;
    if (pins.Count > 0)
    {
        var look = pins[0].transform.Find("Visual/Look");
        var mf = look != null ? look.GetComponent<UnityEngine.MeshFilter>() : null;
        var mr = look != null ? look.GetComponent<UnityEngine.MeshRenderer>() : null;
        s.Append($"ピン {(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "-")}");
        if (mr != null) foreach (var m in mr.sharedMaterials) s.Append($" {(m != null ? m.name : "-")}");
        s.Append("｜");
    }
    if (blast != null) s.Append($"爆発 基準 {(blast.BlastReference != null ? blast.BlastReference.name : "なし")} kinematicはボールでない {blast.KinematicIsNotBall} 有効 {blast.IsEnabled}｜");
    var col = ball.GetComponent<UnityEngine.Collider>();
    var pm = col != null ? col.sharedMaterial : null;
    s.Append($"ボールの摩擦 {(pm != null ? $"{pm.name} {pm.dynamicFriction:F2}/{pm.staticFriction:F2}" : "-")}｜");
    s.Append($"外から動かしている {ball.IsExternallyDriven} kinematic {body.isKinematic}｜");
    if (camCtl != null) s.Append($"カメラの追従のずらし {(camCtl.FollowOffsetOverride.HasValue ? camCtl.FollowOffsetOverride.Value.ToString() : "なし")}");
    return s.ToString();
};

// ---- レーン開始時のピン ----
System.Func<string> pinText = () =>
{
    int shown = 0, kin = 0, culled = 0;
    foreach (var p in pinSet.Pins)
    {
        var vis = p.transform.Find("Visual");
        if (p.IsStandingInPlay && vis != null && vis.gameObject.activeSelf) shown++;
        if (p.GetComponent<UnityEngine.Rigidbody>().isKinematic) kin++;
        if ((bool)fCulled.GetValue(p)) culled++;
    }
    return $"表示{shown}・kinematic {kin}・場外{culled}";
};

// ---- 動いているべきもの（レーンごと） ----
var posT = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, System.Func<UnityEngine.Vector3>>>();
var rotT = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, System.Func<UnityEngine.Quaternion>>>();
System.Action<int> buildTargets = lane =>
{
    posT.Clear(); rotT.Clear();
    var root = laneObj() != null ? laneObj().transform : null;
    System.Action<string> addPos = n => { var t = find(root, n); if (t == null) { notFound++; W("★見つからない｜" + n); } else posT.Add(new System.Collections.Generic.KeyValuePair<string, System.Func<UnityEngine.Vector3>>(n, () => t.position)); };
    System.Action<string> addRot = n => { var t = find(root, n); if (t == null) { notFound++; W("★見つからない｜" + n); } else rotT.Add(new System.Collections.Generic.KeyValuePair<string, System.Func<UnityEngine.Quaternion>>(n, () => t.rotation)); };
    if (lane == 4) addPos("MovingWall");
    if (lane == 5) addRot("Disc");
    if (lane == 7) { addRot("RailRoot"); addRot("RailRootB"); }
    if (lane == 8) { addRot("RigDeckRight"); addRot("RigLander"); addRot("RigCraterL"); addRot("RigCraterR"); }
    if (lane == 9) addRot("Temple");
    if (lane == 10)
    {
        var rail = root != null ? root.GetComponentInChildren<CrazyBowling.Lanes.CoasterRail>() : null;
        if (rail == null) { notFound++; W("★見つからない｜CoasterRail"); }
        else
        {
            posT.Add(new System.Collections.Generic.KeyValuePair<string, System.Func<UnityEngine.Vector3>>("輪1", () => new UnityEngine.Vector3(rail.LoopSway(0), 0f, 0f)));
            posT.Add(new System.Collections.Generic.KeyValuePair<string, System.Func<UnityEngine.Vector3>>("輪2", () => new UnityEngine.Vector3(rail.LoopSway(1), 0f, 0f)));
            var floors = (System.Array)fFloors.GetValue(rail);
            var fb = floors != null && floors.Length > 0 ? (UnityEngine.Rigidbody)floors.GetValue(0).GetType().GetField("body").GetValue(floors.GetValue(0)) : null;
            if (fb == null) { notFound++; W("★見つからない｜床B"); }
            else posT.Add(new System.Collections.Generic.KeyValuePair<string, System.Func<UnityEngine.Vector3>>("床B", () => new UnityEngine.Vector3(fb.position.x, 0f, 0f)));
        }
    }
};
var samples = new System.Collections.Generic.List<object[]>();
System.Func<object[]> takeSample = () =>
{
    var o = new object[posT.Count + rotT.Count];
    for (int i = 0; i < posT.Count; i++) o[i] = posT[i].Value();
    for (int i = 0; i < rotT.Count; i++) o[posT.Count + i] = rotT[i].Value();
    return o;
};
System.Func<string> judgeMotion = () =>
{
    var s = new System.Text.StringBuilder();
    bool stopped = false;
    for (int k = 0; k < posT.Count + rotT.Count; k++)
    {
        float best = 0f;
        for (int a = 0; a < samples.Count; a++)
            for (int b = a + 1; b < samples.Count; b++)
                best = UnityEngine.Mathf.Max(best, k < posT.Count
                    ? UnityEngine.Vector3.Distance((UnityEngine.Vector3)samples[a][k], (UnityEngine.Vector3)samples[b][k]) * 100f
                    : UnityEngine.Quaternion.Angle((UnityEngine.Quaternion)samples[a][k], (UnityEngine.Quaternion)samples[b][k]));
        bool isPos = k < posT.Count;
        bool moving = best >= 0.5f;   // 0.5cm か 0.5度
        if (!moving) stopped = true;
        s.Append($" {(isPos ? posT[k].Key : rotT[k - posT.Count].Key)} {best:F1}{(isPos ? "cm" : "°")}{(moving ? "" : "（止まっていた）")}");
    }
    moveChecks++;
    if (stopped) moveStopped++;
    return (stopped ? "★" : "") + "動きの確認｜" + s;
};

// ---- BGM（切り替わってから 2.5秒後に、流れている曲と音量を見る） ----
System.Func<string> checkBgm = () =>
{
    UnityEngine.AudioClip want = null; float wantVol = 0f;
    if (gm.IsFinished) { want = table.resultBgm; wantVol = table.resultBgmVolume; }
    else if (gm.CurrentLane != null && !gm.CurrentLane.Vacuum)
    {
        var e = table.FindLaneBgm(gm.CurrentLane);
        if (e != null) { want = e.clip; wantVol = e.volume; }
    }
    wantVol *= table.bgmMaster;
    var srcs = (UnityEngine.AudioSource[])fBgm.GetValue(sp);
    int playing = 0; bool ok = true; var s = new System.Text.StringBuilder();
    foreach (var a in srcs)
    {
        if (!a.isPlaying) continue;
        playing++;
        s.Append($" {(a.clip != null ? a.clip.name : "-")} {a.volume:F3}");
        if (a.clip != want || UnityEngine.Mathf.Abs(a.volume - wantVol * sp.BgmDuck) > 0.01f) ok = false;
    }
    if (want == null ? playing != 0 : playing != 1) ok = false;
    if (!ok) bgmBad++;
    string label = $"{(want != null ? want.name : "なし")}";
    bgmSeen.Add($"{game}-{(gm.IsFinished ? "結果" : gm.LaneNumber.ToString())}:{label}");
    return $"{(ok ? "" : "★")}BGM｜期待 {label} {wantVol:F2}｜鳴っている{(playing == 0 ? " なし" : s.ToString())}";
};

// ---- 前のレーンの音の残り（切り替わってから 0.3秒後） ----
var laneSe = new System.Text.RegularExpressions.Regex(@"^se_lane(\d\d)");
System.Func<string> checkResidue = () =>
{
    int cur = gm.IsFinished ? -1 : gm.LaneNumber;
    var s = new System.Text.StringBuilder();
    if (rollSound != null && rollSound.IsPlaying) s.Append(" 転がる音");
    foreach (var a in UnityEngine.Object.FindObjectsByType<UnityEngine.AudioSource>(UnityEngine.FindObjectsSortMode.None))
    {
        if (!a.isPlaying || a.clip == null) continue;
        var m = laneSe.Match(a.clip.name);
        if (m.Success && int.Parse(m.Groups[1].Value) != cur) s.Append(" " + a.clip.name);
    }
    switches++;
    if (s.Length > 0) { residue++; return "★前のレーンの音の残り｜" + s; }
    return "前のレーンの音の残り なし";
};

// ---- 進行 ----
int laneKey = -1; float laneT0 = 0f; bool pinDone = false, residueDone = false, bgmDone = false;
float nextBgmLog = 0f, finishedAt = -1f;
int phase = 0; float phaseT = 0f; float speed = 10f, side = 0f; float discTarget = -1f;
CrazyBowling.Lanes.CoffeeCupRide ride = null; CrazyBowling.Lanes.CoffeeCupLane cupLane = null;
float discAtThrow = -1f; bool waitStop = false;
UnityEditor.EditorApplication.CallbackFunction tick = null;
System.Action finish = null;
finish = () =>
{
    UnityEditor.EditorApplication.update -= tick;
    UnityEngine.Application.logMessageReceived -= onLog;
    seq.ThrowJudged -= onJudged;
    seq.LaneFinished -= onLaneFinished;
    if (ball != null) ball.SetInputBlocked(false);
    // 8本目（真空）で鳴ったボール・ピン・歓声の音
    int vac = 0;
    foreach (var r in sp.Records)
        if (r.lane == 8 && (r.name.StartsWith("投げた瞬間") || r.name.StartsWith("転がる") || r.name.StartsWith("ピン（") || r.name.StartsWith("歓声（"))) vac++;
    W("BGM の並び｜" + string.Join(" ", bgmSeen));
    W($"9本目｜阻まれた {lane9Block}・入った {lane9Enter}");
    W($"まとめ｜終わったレーン {lanesFinished}（期待 20）｜投球 {throws}｜タイムアウト {timeouts}｜本数の矛盾 {contradictions}｜警告 {warn}｜エラー {err}｜" +
      $"開始時のピンの食い違い {pinStartBad}｜共有設定の食い違い {sharedMismatch}｜動きの確認 {moveChecks}回・止まっていた {moveStopped}回｜" +
      $"BGM の食い違い {bgmBad}｜レーンの切り替え {switches}回・前のレーンの音の残り {residue}回｜8本目のボール・ピン・歓声の音 {vac}回｜見つからない部品 {notFound}｜" +
      $"{(UnityEditor.EditorApplication.isPlaying ? "" : "★途中で Play が止まった｜")}終わり");
    System.IO.File.AppendAllText(logPath, buf.ToString()); buf.Clear();
};
tick = () =>
{
    if (!UnityEditor.EditorApplication.isPlaying) { finish(); return; }
    float t = UnityEngine.Time.unscaledTime;
    int key = game * 100 + gm.LaneNumber;
    if (key != laneKey)
    {
        laneKey = key; laneT0 = t; pinDone = residueDone = bgmDone = false; nextBgmLog = t;
        phase = 0; waitStop = false;
        W($"{t:F2} ---- {game}ゲーム {(gm.IsFinished ? "結果画面" : gm.LaneNumber + "本目 " + (gm.CurrentLane != null ? gm.CurrentLane.name : ""))}");
        if (!gm.IsFinished)
        {
            buildTargets(gm.LaneNumber);
            var lo = laneObj();
            ride = lo != null ? lo.GetComponentInChildren<CrazyBowling.Lanes.CoffeeCupRide>() : null;
            cupLane = lo != null ? lo.GetComponentInChildren<CrazyBowling.Lanes.CoffeeCupLane>() : null;
        }
    }
    // 切り替わりの直後 2.5秒は、BGM の2つの口を 0.25秒ごとに書く（曲の入れ替わりが見える）
    if (t - laneT0 <= 2.5f && t >= nextBgmLog)
    {
        nextBgmLog = t + 0.25f;
        var s = new System.Text.StringBuilder($"{t:F2}   BGM の口");
        foreach (var a in (UnityEngine.AudioSource[])fBgm.GetValue(sp)) s.Append($"｜{(a.clip != null ? a.clip.name : "-")} {(a.isPlaying ? "鳴" : "止")} {a.volume:F3}");
        W(s.ToString());
    }
    if (!residueDone && t - laneT0 >= 0.3f) { residueDone = true; W($"{t:F2}   " + checkResidue()); }
    if (!pinDone && !gm.IsFinished && t - laneT0 >= 0.2f)
    {
        pinDone = true;
        string p = pinText();
        bool ok = p == (gm.LaneNumber == 9 ? "表示10・kinematic 10・場外0" : "表示10・kinematic 0・場外0");
        if (!ok) pinStartBad++;
        W($"{t:F2}   {(ok ? "" : "★")}開始時のピン｜{p}");
        string sh = sharedText();
        shared[game - 1][gm.LaneNumber] = sh;
        if (game == 2)
        {
            string g1;
            if (!shared[0].TryGetValue(gm.LaneNumber, out g1)) W("★共有設定｜1ゲーム目の記録が無い");
            else if (g1 != sh) { sharedMismatch++; W($"★共有設定の食い違い｜1ゲーム目 {g1}\n  2ゲーム目 {sh}"); }
            else W($"{t:F2}   共有設定は1ゲーム目と同じ");
        }
        else W($"{t:F2}   共有設定｜{sh}");
    }
    if (!bgmDone && t - laneT0 >= 2.5f) { bgmDone = true; W($"{t:F2}   " + checkBgm()); }
    if (buf.Length > 0) { System.IO.File.AppendAllText(logPath, buf.ToString()); buf.Clear(); }

    // 9本目：投げた瞬間から止まるまでに神殿が回った角度を書く
    if (waitStop && cupLane != null && cupLane.IsRideStopped)
    {
        waitStop = false;
        float now = (float)fDisc.GetValue(ride);
        W($"{t:F2}   9本目：投げてから止まるまでに回った角度 {UnityEngine.Mathf.Repeat(now - discAtThrow, 360f):F1}°（止まった向き {now:F1}°）");
    }

    // 結果画面：1ゲーム目なら 3秒後に「やり直し」、2ゲーム目なら終わる
    if (gm.IsFinished)
    {
        if (finishedAt < 0f) finishedAt = t;
        if (t - finishedAt > 3f && bgmDone)
        {
            finishedAt = -1f;
            if (game == 1) { game = 2; W($"{t:F2} ==== 2ゲーム目を始める"); resultView.Restart(); }
            else finish();
        }
        return;
    }

    // 投げる：構え中（下見が終わっている・9本目は座り直しが終わっている）→ 0.15秒おきに5点測る → 置く → 次のフレームで投げる
    string st = fSeqState.GetValue(seq).ToString();
    bool ready = st == "Ready" && ball.IsAiming && (preview == null || !preview.IsPlaying) && (ride == null || !ride.IsReseating);
    if (phase == 0)
    {
        if (ready) { phase = 1; phaseT = t; samples.Clear(); samples.Add(takeSample()); ball.SetInputBlocked(true); }
    }
    else if (phase == 1)
    {
        ball.SetInputBlocked(true);   // 構え中のボールがマウスに付いて動かないようにする（下見の終わりに外されるので毎フレーム）
        if (!ready) { phase = 0; return; }
        if (t - phaseT >= 0.15f * samples.Count) samples.Add(takeSample());
        if (samples.Count >= 5)
        {
            if (posT.Count + rotT.Count > 0) W($"{t:F2}   {judgeMotion()}");
            int lane = gm.LaneNumber, n = seq.ThrowNumber;
            speed = lane == 8 || lane == 9 ? 8f : (n == 1 ? 10f : 9f);
            side = lane == 8 || lane == 9 || n == 1 ? 0f : 0.25f;
            // 9本目：速8・真ん中なら、投げてから止まるまでに神殿は約144°回る。入り口の向き（_discAngle）が止まったときに
            // 180°（手前）なら入り、0°（奥）なら阻まれる。1投目は阻まれる角度、2投目は入る角度に置く
            discTarget = lane == 9 && ride != null ? UnityEngine.Mathf.Repeat((n == 1 ? 0f : 180f) - 143.7f, 360f) : -1f;
            ball.ReturnToSpawn();
            fSide.SetValue(ball, side);
            mApplySpawn.Invoke(ball, null);
            body.position = ball.transform.position;
            UnityEngine.Physics.SyncTransforms();
            phase = 2;
        }
    }
    else if (phase == 2)
    {
        body.position = ball.transform.position;
        UnityEngine.Physics.SyncTransforms();
        if (discTarget >= 0f)
        {
            fDisc.SetValue(ride, discTarget);
            mRideApply.Invoke(ride, new object[] { true, 0f });
            discAtThrow = discTarget; waitStop = true;
            if (seq.ThrowNumber == 1) lane9Block++; else lane9Enter++;
        }
        var r = new CrazyBowling.Ball.ThrowResult { isValid = true, speed = speed, curve = 0f, sideAngle = 0f, pullRatio = 1f };
        mThrow.Invoke(ball, new object[] { r });
        // ★投げたらすぐ入力の止めを外す。止めたままだと BallController.Update が頭で抜け、
        //   転がり中の「止まった・時間切れ」の決着が効かない（ピットに入らずに止まった球が永久に決着しない）
        ball.SetInputBlocked(false);
        throws++;
        W($"{t:F2}   {seq.ThrowNumber}投目を投げた｜速{speed}・立{side:+0.00;-0.00;0}{(discTarget >= 0f ? $"・神殿の向き {discTarget:F1}°（{(seq.ThrowNumber == 1 ? "阻まれる" : "入る")}角度）" : "")}");
        phase = 3; phaseT = t;
    }
    else if (phase == 3)
    {
        // 転がり始めたら、次の構えを待つ（万一転がり出さなかったら 3秒で待ち直す）
        if (!ball.IsAiming || t - phaseT > 3f) phase = 0;
    }
};

seq.ThrowJudged += onJudged;
seq.LaneFinished += onLaneFinished;
UnityEngine.Application.logMessageReceived += onLog;
if (tune != null) tune.HideImmediately();
if (title != null) title.HideImmediately();
sp.ClearRecords();
W($"通しの確認を始めた（{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}）");
gm.StartGame(0);
UnityEditor.EditorApplication.update += tick;
return "見張り役を登録し、1ゲーム目を1本目から始めた。結果は " + logPath;
