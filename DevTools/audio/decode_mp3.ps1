# ============================================================================
# mp3（や wav）から波形を取り出す（Unity に読み込ませて、float の生データにする）
#
# このパソコンには ffmpeg が無いので、Unity の読み込みを使う。
#   1. Assets/Temp に一時的にコピー → 2. 読み込む → 3. PCM（圧縮なし）・元のチャンネル数・元の周波数で読み込み直す
#   4. 波形を <出力フォルダ>/<名前>.f32（32bit float・チャンネルが交互に並ぶ）と <名前>.json（周波数・チャンネル数・長さ）に書く
#   5. Assets/Temp ごと消す（プロジェクトには何も残らない）
# ★1本ずつ・1手順ずつ eval を分けて送る。まとめるとメインスレッドの 5秒の上限を超える
# ★Unity エディタを開いたまま（Play していない状態で）走らせる
#
# 使い方（PowerShell の中から & で呼ぶ。★powershell -File で呼ぶとファイルの並びが1つずつに分かれて渡らない）：
#   & C:\dev\CrazyBowling\DevTools\audio\decode_mp3.ps1 `
#     -Files 'C:\dev\CrazyBowling\AudioOriginals\飲茶.mp3','C:\...\b.mp3' -Names 't05','t06' -OutDir C:\Users\offic\AppData\Local\Temp\CrazyBowling_audio
#   -Names を省くと n01, n02 … になる。-OutDir を省くと %TEMP%\CrazyBowling_audio
#
# ★このファイルは BOM 付き UTF-8 で保存すること
# ============================================================================
param(
    [Parameter(Mandatory = $true)][string[]]$Files,
    [string[]]$Names,
    [string]$OutDir = (Join-Path $env:TEMP 'CrazyBowling_audio'),
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

New-Item -ItemType Directory -Force $OutDir | Out-Null
$evalDir = Join-Path $OutDir 'eval'
New-Item -ItemType Directory -Force $evalDir | Out-Null
$tempAssets = Join-Path $ProjectPath 'Assets\Temp'
if (Test-Path "$tempAssets") { Write-Host "★$tempAssets が既にある。中身を確かめてから消してやり直すこと"; exit 1 }

function Invoke-Eval([string]$name, [string]$code) {
    $f = Join-Path $evalDir $name
    [System.IO.File]::WriteAllText($f, $code, (New-Object System.Text.UTF8Encoding $true))
    $r = unity command eval_file $f "--project-path=$ProjectPath" 2>&1 | Out-String
    if ($r -match '"result":"([^"]*)"') { return $matches[1] }
    return "★失敗：$r"
}

for ($i = 0; $i -lt $Files.Count; $i++) {
    $src = (Resolve-Path -LiteralPath $Files[$i]).Path
    $n = if ($Names -and $i -lt $Names.Count) { $Names[$i] } else { 'n{0:D2}' -f ($i + 1) }
    $ext = [System.IO.Path]::GetExtension($src).ToLower()
    $asset = "Assets/Temp/$n$ext"
    $out = Join-Path $OutDir $n
    $srcEsc = $src -replace '"', '""'
    $dstEsc = (Join-Path $tempAssets "$n$ext") -replace '"', '""'

    Write-Host "== $n ← $src"
    Write-Host ('  ' + (Invoke-Eval "copy_$n.cs" @"
System.IO.Directory.CreateDirectory(@"$tempAssets");
System.IO.File.Copy(@"$srcEsc", @"$dstEsc", true);
return "コピーした";
"@))
    Write-Host ('  ' + (Invoke-Eval "import_$n.cs" @"
UnityEditor.AssetDatabase.ImportAsset("$asset");
return "読み込んだ";
"@))
    Write-Host ('  ' + (Invoke-Eval "pcm_$n.cs" @"
var imp = (UnityEditor.AudioImporter)UnityEditor.AssetImporter.GetAtPath("$asset");
imp.forceToMono = false;
var s = imp.defaultSampleSettings;
s.loadType = UnityEngine.AudioClipLoadType.DecompressOnLoad;
s.compressionFormat = UnityEngine.AudioCompressionFormat.PCM;
s.sampleRateSetting = UnityEditor.AudioSampleRateSetting.PreserveSampleRate;
imp.defaultSampleSettings = s;
imp.SaveAndReimport();
var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AudioClip>("$asset");
return `$"PCM で読み込み直した｜{clip.length:F3}秒・{clip.channels}ch・{clip.frequency}Hz";
"@))
    Write-Host ('  ' + (Invoke-Eval "dump_$n.cs" @"
var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AudioClip>("$asset");
clip.LoadAudioData();
var data = new float[clip.samples * clip.channels];
bool ok = clip.GetData(data, 0);
var bytes = new byte[data.Length * 4];
System.Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
System.IO.File.WriteAllBytes(@"$out.f32", bytes);
System.IO.File.WriteAllText(@"$out.json", `$"{{\"sr\":{clip.frequency},\"ch\":{clip.channels},\"samples\":{clip.samples}}}");
return `$"書き出した｜GetData {ok}・{clip.channels}ch・{clip.frequency}Hz・{clip.samples}サンプル";
"@))
    Write-Host ('  ' + (Invoke-Eval "delete_$n.cs" @"
bool ok = UnityEditor.AssetDatabase.DeleteAsset("Assets/Temp");
return `$"一時フォルダを消した {ok}";
"@))
}
if (Test-Path "$tempAssets") { Write-Host "★$tempAssets が残っている" } else { Write-Host "Assets/Temp は残っていない" }
Write-Host "出力：$OutDir"
