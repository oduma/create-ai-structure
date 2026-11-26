$ErrorActionPreference = "Stop"

$firstLine = (git worktree list --porcelain | Select-Object -First 1)
if (-not $firstLine -or -not $firstLine.StartsWith("worktree ")) {
    exit 0
}

$mainRoot = $firstLine.Substring(9).Trim()
$currentRoot = (Get-Location).Path

if ($mainRoot -eq $currentRoot) {
    exit 0
}

$filesToCopy = @(
    ".env",
    "appsettings.Development.json"
)

foreach ($relPath in $filesToCopy) {
    $src = Join-Path $mainRoot $relPath
    $dst = Join-Path $currentRoot $relPath
    if ((Test-Path $src) -and -not (Test-Path $dst)) {
        $dstDir = Split-Path -Parent $dst
        if (-not (Test-Path $dstDir)) {
            New-Item -ItemType Directory -Path $dstDir -Force | Out-Null
        }
        Copy-Item -Path $src -Destination $dst -Force
        Write-Host "Copied $relPath into worktree."
    }
}