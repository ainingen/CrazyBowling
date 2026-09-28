# ============================================================================
# 1〜10本目の通し確認（2ゲーム）を1回で最後まで回す
#
# 使い方（PowerShell。Unity エディタを開いたまま。Play していない状態で）：
#   powershell -ExecutionPolicy Bypass -File C:\dev\CrazyBowling\DevTools\run_playthrough.ps1
#
# 1. シーンを保存して Play に入る（enter_play.cs）
# 2. ゲームの部品が揃うのを待つ（play_status.cs）
# 3. 見張り役を登録する（playthrough_check.cs）
# 4. 「まとめ」の行が出るまで待つ（2ゲームで約8分）
# 5. Play を止める（exit_play.cs）
# 6. ★の付いた行（食い違い）と、まとめを表示する
# 全部の記録は Logs/playthrough_check.log（git に入らない）
#
# ★このファイルは BOM 付き UTF-8 で保存すること（無いと Windows PowerShell 5.1 が日本語を文字化けして読み、文字の比較が外れる）
# ============================================================================
param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [int]$TimeoutMinutes = 20
)

$tools = $PSScriptRoot
$log = Join-Path $ProjectPath 'Logs\playthrough_check.log'

function Invoke-Eval([string]$file) {
    # unity command の出力（表）を1つの文字列にして返す
    return (unity command eval_file (Join-Path $tools $file) "--project-path=$ProjectPath" 2>&1 | Out-String)
}

Write-Host '1. シーンを保存して Play に入る'
Write-Host (Invoke-Eval 'enter_play.cs')

function Test-Reachable {
    # unity pipeline list の「サーバー到達可能」の欄を読む（Unity の中の処理は呼ばないので、コードの読み込み直しの最中でも安全）
    $rows = @(unity pipeline list 2>$null | Where-Object { $_ -match "`t" })
    if ($rows.Count -lt 2) { return $false }
    $head = $rows[0] -split "`t"
    $col = [Array]::IndexOf($head, 'サーバー到達可能')
    foreach ($r in $rows[1..($rows.Count - 1)]) {
        $c = $r -split "`t"
        if ($c.Count -gt $col -and $col -ge 0 -and $r -like "*$ProjectPath*") { return $c[$col] -eq 'true' }
    }
    return $false
}

Write-Host '2. ゲームの部品が揃うのを待つ'
# ★Play に入るとコードの読み込み直しで CLI の中継がいったん止まる。その最中に eval を送ると
#   「Main thread operation timed out after 5000ms」のエラーが Console に残るので、止まって戻るまで eval を送らない
for ($i = 0; $i -lt 20; $i++) { if (-not (Test-Reachable)) { break }; Start-Sleep -Milliseconds 500 }
for ($i = 0; $i -lt 120; $i++) { if (Test-Reachable) { break }; Start-Sleep -Seconds 1 }
Start-Sleep -Seconds 2
$ready = $false
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Seconds 1
    if ((Invoke-Eval 'play_status.cs') -match '準備できた') { $ready = $true; break }
}
if (-not $ready) { Write-Host '★Play に入れなかった（Unity のウィンドウを一度クリックしてからやり直す）'; exit 1 }

Write-Host '3. 見張り役を登録する'
if (Test-Path $log) { Remove-Item $log }
Write-Host (Invoke-Eval 'playthrough_check.cs')

Write-Host "4. 2ゲーム終わるのを待つ（最大 $TimeoutMinutes 分）"
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
$done = $false
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 10
    if (Test-Path $log) {
        $lines = Get-Content $log -Encoding UTF8
        $last = ($lines | Where-Object { $_ -match '^\d+\.\d+ ----' } | Select-Object -Last 1)
        if ($last) { Write-Host "   $last" }
        if ($lines | Where-Object { $_ -match '^まとめ' }) { $done = $true; break }
    }
}

Write-Host '5. Play を止める'
Write-Host (Invoke-Eval 'exit_play.cs')

Write-Host '6. 結果'
if (-not $done) { Write-Host "★$TimeoutMinutes 分で終わらなかった。$log を見ること" }
Get-Content $log -Encoding UTF8 | Where-Object { $_ -match '★' -or $_ -match '^(まとめ|BGM の並び|9本目)' -or $_ -match '回った角度' }
Write-Host "全部の記録：$log"
