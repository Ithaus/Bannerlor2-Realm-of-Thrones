# bieg-zima.ps1 - proba zimy: plik-przelacznik armoury-proba-zimy.flag na czas biegu (nowa kampania zaczyna sie zima), potem ZAWSZE usuniety
param(
    [Parameter(Mandatory = $true)][string]$DllDir,
    [Parameter(Mandatory = $true)][string]$CsAt,
    [Parameter(Mandatory = $true)][string]$Tree,
    [Parameter(Mandatory = $true)][string]$Out,
    [int]$Days = 120
)
$flag = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Mount and Blade II Bannerlord\CrashScribe\armoury-proba-zimy.flag'
$kod = 1
try {
    Set-Content -LiteralPath $flag -Value 'proba zimy - autotest' -Encoding ASCII
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'bieg.ps1') -DllDir $DllDir -CsAt $CsAt -Tree $Tree -Out $Out -Days $Days
    $kod = $LASTEXITCODE
}
finally {
    if (Test-Path -LiteralPath $flag) { Remove-Item -LiteralPath $flag -Force }
    Write-Host ('przelacznik proby zimy usuniety: ' + (-not (Test-Path -LiteralPath $flag)))
}
exit $kod
