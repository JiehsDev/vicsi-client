param([string]$ProjectPath = (Get-Location).Path)
$ErrorActionPreference = 'Stop'
$guard = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'stable-guard.sh')).Replace("`r`n", "`n")
$utf8 = New-Object System.Text.UTF8Encoding($false)
foreach ($hook in @('pre-commit','pre-merge-commit')) {
    $target = & git -C $ProjectPath rev-parse --git-path "hooks/$hook"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot locate Git hooks for this project.' }
    if (-not [IO.Path]::IsPathRooted($target)) { $target = Join-Path $ProjectPath $target }
    $target = [IO.Path]::GetFullPath($target)
    if ((Test-Path -LiteralPath $target) -and -not ([IO.File]::ReadAllText($target).Contains('ViCSI stable checkpoint guard'))) {
        throw "An existing $hook hook was found. Preserve it and chain the ViCSI guard manually."
    }
    [IO.File]::WriteAllText($target, $guard, $utf8)
}
Write-Output 'Installed stable-branch guards without changing Git LFS hooks.'
