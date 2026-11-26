$ErrorActionPreference = 'Stop'
$storage = Join-Path $env:USERPROFILE '.local/share/codex-kimi'
$version = '2.8.1'
$binaryDir = Join-Path $storage "bin/$version"
$binary = Join-Path $binaryDir 'kimi.exe'
$venvPython = Join-Path $storage 'venv/Scripts/python.exe'
New-Item -ItemType Directory -Path $binaryDir -Force | Out-Null
if (-not (Test-Path -LiteralPath $binary)) {
    $archive = Join-Path $binaryDir 'kimi-code-win32-x64.tar.gz'
    $url = 'https://github.com/MoonshotAI/kimi-code/releases/download/%40moonshot-ai/kimi-code%402.8.1/kimi-code-win32-x64.tar.gz'
    Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing
    $actual = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLower()
    if ($actual -ne '43d7ca8dbbbe05b8e7f4b4bf1e3fe99b7f331ac8cbd00bbadccfe36261594afa') {
        throw 'Official Kimi release archive checksum mismatch'
    }
    & tar.exe -xzf $archive -C $binaryDir
    if ($LASTEXITCODE -ne 0) { throw 'Kimi extraction failed' }
}
if (-not (Test-Path -LiteralPath $venvPython)) {
    & uv venv (Join-Path $storage 'venv')
    if ($LASTEXITCODE -ne 0) { throw 'Virtual environment creation failed' }
}
& uv pip install --python $venvPython -r (Join-Path $PSScriptRoot 'requirements.txt')
if ($LASTEXITCODE -ne 0) { throw 'MCP dependency installation failed' }
& $binary --version
& $binary doctor
if ($LASTEXITCODE -ne 0) { throw 'Kimi configuration check failed' }
Write-Output "Runner ready: $binary"
Write-Output 'Existing Kimi OAuth profile is reused; if missing, run this binary with login.'
