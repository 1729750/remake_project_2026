$ErrorActionPreference = 'Stop'
$target = 'c:\Git-Directory\UpnL_FirstGame\remake_project_2026'
$run = Join-Path $target '.agents\tasks\github-unity-import-2026'
$temp = Join-Path $run 'temp-remote'
function Normalize([string]$p) { $p.Replace('\','/').Trim() }
function Excluded([string]$p) {
  $q = Normalize $p
  return ($q -match '(^|/)(\.git|Library|obj|Binaries|Temp|Build|UserSettings|\.vs|\.kiro)(/|$)' -or $q -match '(^|/)([^/]+\.(csproj|user|vsconfig))$' -or $q -match '^\.agents/tasks/github-unity-import-2026(/|$)')
}
$remote = @(git -C $temp ls-files | ForEach-Object { Normalize $_ } | Where-Object { $_ -and -not (Excluded $_) })
$local = @(git -C $target ls-files | ForEach-Object { Normalize $_ } | Where-Object { $_ -and -not (Excluded $_) })
$remoteSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$remote | ForEach-Object { [void]$remoteSet.Add($_) }
$localSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$local | ForEach-Object { [void]$localSet.Add($_) }
$untracked = @()
foreach ($line in @(git -C $target status --porcelain)) {
  if ($line -match '^\?\? (.+)$') {
    $p = Normalize $Matches[1]
    $full = Join-Path $target ($p.Replace('/','\'))
    if (Test-Path -LiteralPath $full -PathType Leaf) { if (-not (Excluded $p)) { $untracked += $p } }
    elseif (Test-Path -LiteralPath $full -PathType Container) {
      $untracked += @(Get-ChildItem -LiteralPath $full -File -Recurse | ForEach-Object { $z = Normalize $_.FullName.Substring($target.Length + 1); if (-not (Excluded $z)) { $z } })
    }
  }
}
$add = @($remote | Where-Object { -not $localSet.Contains($_) } | Sort-Object -Unique)
$replace = @($remote | Where-Object { $localSet.Contains($_) } | Sort-Object -Unique)
$delete = @($local | Where-Object { -not $remoteSet.Contains($_) } | Sort-Object -Unique)
$diff = @()
foreach ($p in $replace) {
  $a = Join-Path $target ($p.Replace('/','\')); $b = Join-Path $temp ($p.Replace('/','\'))
  if ((Get-FileHash -LiteralPath $a -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $b -Algorithm SHA256).Hash) { $diff += $p }
}
$result = [ordered]@{ add = $add; replace = $replace; replaceDiffers = $diff; delete = $delete; localOnlyPreserved = @($untracked | Sort-Object -Unique) }
$result | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $run 'classify.json') -Encoding UTF8
Write-Output "add=$($add.Count) replace=$($replace.Count) differs=$($diff.Count) delete=$($delete.Count) localOnly=$(@($untracked | Sort-Object -Unique).Count)"
