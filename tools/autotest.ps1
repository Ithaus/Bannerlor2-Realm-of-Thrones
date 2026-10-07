# autotest.ps1 - autotest gry bez klikania (Jeff 07.10: "zgoda na autotest").
#
# Co robi, po kolei:
#   (a) STOP, jesli gra dziala (procesy Bannerlord*, *BLSE*, TaleWorlds.MountAndBlade.Launcher*);
#       jesli poprzedni bieg nie skonczyl przywracania (znacznik w-toku.json) - najpierw przywraca;
#   (b) kopia ZATWIERDZONYCH DLL z gry (md5) do %USERPROFILE%\autotest-kopie\<data> i lustro na
#       D:\Backup-Bannerlord\autotest-kopie\<data> (dysk C potrafi zerowac pliki), do tego kopia
#       calego Documents\...\Configs (MCM, LauncherData.xml, BannerlordConfig.txt...);
#   (c) wgranie DLL probnych (CrashScribe z trybem autotestu + opcjonalnie Armoury i inne), md5;
#   (d) plik-przelacznik Documents\...\CrashScribe\autotest.json (gra zuzywa go przy starcie);
#   (e) start gry: bin\Win64_Shipping_Client\Bannerlord.BLSE.Standalone.exe /singleplayer
#       "_MODULES_*Id1*Id2*...*_MODULES_" - te same mody i ta sama kolejnosc co w LauncherData.xml
#       (plik tylko CZYTANY); katalog roboczy MUSI byc bin\Win64_Shipping_Client (BLSE to sprawdza);
#   (f) czekanie na koniec gry z limitem czasu, z kontrola postepu w autotest-*.log;
#       po limicie / postoju / wywrotce - zamkniecie gry; po "WYJSCIE: QuitGame" proces ma ExitWaitSec
#       (90 s) - dluzej wisi tylko silnik przy sprzataniu (okno bledu z rgl_log_errors_<PID>.txt);
#   (g) ZAWSZE (finally): zamkniecie gry, przywrocenie zatwierdzonych DLL i kontrola md5,
#       przywrocenie zmienionych plikow Configs, usuniecie niezuzytego przelacznika;
#   (h) wynik: logi Armoury, CrashScribe, autotest-*.log, czas, doba, kody bledow.
#
# Kody wyjscia: 0 = dotarl do N dob i wszystko przywrocone; 1 = test nie dotarl do N dob
# (przywrocone); 2 = STOP przed testem (nic nie ruszone albo przywrocone); 3 = PRZYWRACANIE
# NIEUDANE - w grze moze zostac DLL probny (znacznik w-toku.json zostaje, -RestoreOnly).
#
# Przyklady:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\autotest.ps1 -CrashScribeDll CrashScribe\bin\Release\CrashScribe.dll -ArmouryDll Armoury\bin\Release\Armoury.dll -Days 40
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\autotest.ps1 -RestoreOnly
#
# Plik w ASCII (PowerShell 5.1 czyta .ps1 bez BOM jako ANSI).

[CmdletBinding()]
param(
    [string]$ArmouryDll,
    [string]$CrashScribeDll,
    [string[]]$ExtraDll = @(),
    [int]$Days = 40,
    [int]$TimeoutMin = 0,          # 0 = 25 s na dobe + 10 min
    [double]$Speed = 0,            # 0 = mnoznik jak w grze Jeffa (Armoury MCM)
    [int]$StartMin = 20,           # bez pierwszej doby po tylu minutach = gra stoi przed biegiem
    [int]$StallMin = 12,           # bez nowej doby przez tyle minut = postoj (mod sam konczy po 10)
    [int]$SwitchWaitSec = 240,     # przelacznik niezuzyty po tylu s = w grze nie ma trybu autotestu
    [int]$QuitWaitSec = 600,       # po "KONIEC" w logu gra ma tyle s na zapis i wyjscie
    [int]$ExitWaitSec = 90,        # po "WYJSCIE: QuitGame" w logu proces ma tyle s na zakonczenie (AT1b)
    [string]$EngineLogDir = 'C:\ProgramData\Mount and Blade II Bannerlord\logs',   # rgl_log_errors_<PID>.txt (tylko odczyt)
    [int]$PollSec = 10,
    [switch]$NoSave,
    [switch]$NoQuit,
    [switch]$RestoreOnly,
    [switch]$NoMirror,
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord',
    [string]$DocsDir = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Mount and Blade II Bannerlord'),
    [string]$BackupRoot = (Join-Path $env:USERPROFILE 'autotest-kopie'),
    [string]$MirrorRoot = 'D:\Backup-Bannerlord\autotest-kopie',
    # tylko do proby na sucho (atrapa gry zamiast BLSE):
    [string]$LaunchExe,
    [string]$LaunchArgs,
    [switch]$SkipSteamCheck,
    [string[]]$GameProcessNames = @('Bannerlord*', '*BLSE*', 'TaleWorlds.MountAndBlade.Launcher*')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2

$stamp = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'
$runId = 'at-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
$csDir = Join-Path $DocsDir 'CrashScribe'
$switchPath = Join-Path $csDir 'autotest.json'
$configsDir = Join-Path $DocsDir 'Configs'
$savesDir = Join-Path $DocsDir 'Game Saves'
$binDir = Join-Path $GameDir 'bin\Win64_Shipping_Client'
$marker = Join-Path $BackupRoot 'w-toku.json'
$runDir = Join-Path $BackupRoot $stamp
$script:logFile = $null
$script:started = $null
$script:proc = $null
$script:exitCode = 0
$script:exitNote = $null

# ------------------------------------------------------------------ pomocnicze

function Log([string]$msg) {
    $line = '[' + (Get-Date -Format 'HH:mm:ss') + '] ' + $msg
    Write-Host $line
    if ($script:logFile) { try { Add-Content -LiteralPath $script:logFile -Value $line -Encoding UTF8 } catch { } }
}

function Md5([string]$p) {
    if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { return $null }
    return (Get-FileHash -Algorithm MD5 -LiteralPath $p).Hash.ToLowerInvariant()
}

function Test-Pe([string]$p) {
    if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { return $false }
    $fs = [IO.File]::OpenRead($p)
    try { $b = New-Object byte[] 2; $n = $fs.Read($b, 0, 2); return ($n -eq 2 -and $b[0] -eq 0x4D -and $b[1] -eq 0x5A) }
    finally { $fs.Dispose() }
}

function Test-HasAutotest([string]$p) {
    # build z trybem autotestu ma w sobie napis "autotest.json" (literal C# = UTF-16LE)
    if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { return $false }
    $bytes = [IO.File]::ReadAllBytes($p)
    $text = [Text.Encoding]::Unicode.GetString($bytes)
    $text2 = [Text.Encoding]::Unicode.GetString($bytes, 1, $bytes.Length - 1)
    return ($text.Contains('autotest.json') -or $text2.Contains('autotest.json'))
}

function Get-GameProcs {
    $list = @()
    foreach ($n in $GameProcessNames) { $list += @(Get-Process -Name $n -ErrorAction SilentlyContinue) }
    if ($script:proc) {
        try { $script:proc.Refresh(); if (-not $script:proc.HasExited) { $list += $script:proc } } catch { }
    }
    $seen = @{}
    $out = @()
    foreach ($p in $list) { if ($p -and -not $seen.ContainsKey($p.Id)) { $seen[$p.Id] = 1; $out += $p } }
    return , $out
}

function Stop-Game([string]$why) {
    $procs = Get-GameProcs
    if ($procs.Count -eq 0) { return }
    Log ("ZAMYKAM GRE: " + $why + " (procesy: " + (($procs | ForEach-Object { $_.ProcessName + '#' + $_.Id }) -join ', ') + ")")
    foreach ($p in $procs) { try { $null = $p.CloseMainWindow() } catch { } }
    $end = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $end -and (Get-GameProcs).Count -gt 0) { Start-Sleep -Seconds 2 }
    foreach ($p in (Get-GameProcs)) {
        try { Stop-Process -Id $p.Id -Force -ErrorAction Stop; Log ("  zabity: " + $p.ProcessName + '#' + $p.Id) } catch { Log ("  nie da sie zabic " + $p.Id + ": " + $_.Exception.Message) }
    }
    $end = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $end -and (Get-GameProcs).Count -gt 0) { Start-Sleep -Seconds 1 }
}

function Copy-Verified([string]$src, [string]$dst, [string]$md5) {
    $dir = Split-Path -Parent $dst
    if (-not (Test-Path -LiteralPath $dir)) { $null = New-Item -ItemType Directory -Force -Path $dir }
    Copy-Item -LiteralPath $src -Destination $dst -Force
    $m = Md5 $dst
    if ($m -ne $md5) { throw ("kopia " + $dst + " ma md5 " + $m + ", oczekiwane " + $md5) }
}

function Get-TreeHashes([string]$root) {
    $h = @{}
    if (-not (Test-Path -LiteralPath $root)) { return $h }
    foreach ($f in Get-ChildItem -LiteralPath $root -Recurse -File -Force) {
        $rel = $f.FullName.Substring($root.Length).TrimStart('\')
        if ($rel -like 'ModLogs\*' -or $rel -like '*.log') { continue }
        $h[$rel] = Md5 $f.FullName
    }
    return $h
}

# ------------------------------------------------------------------ przywracanie (manifest)

function Restore-Manifest([string]$manifestPath, [bool]$strict) {
    # strict = start skryptu po przerwanym biegu: ruszamy tylko plik z wersja probna (albo zepsuty);
    # nieznana wersja = ktos wgral cos nowego - NIE nadpisujemy.
    $man = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $allOk = $true
    foreach ($f in @($man.files)) {
        $cur = Md5 $f.target
        if ($cur -eq $f.approvedMd5) { Log ("  " + $f.mod + ": w grze zatwierdzony (md5 " + $cur + ")"); continue }
        if ($strict -and $cur -and $cur -ne $f.trialMd5 -and (Test-Pe $f.target)) {
            Log ("  " + $f.mod + ": w grze NIEZNANA wersja md5 " + $cur + " (ani zatwierdzona " + $f.approvedMd5 + ", ani probna " + $f.trialMd5 + ") - NIE ruszam, sprawdz recznie")
            $allOk = $false
            continue
        }
        $src = $null
        foreach ($cand in @($f.backup, $f.mirror)) {
            if ($cand -and (Md5 $cand) -eq $f.approvedMd5) { $src = $cand; break }
        }
        if (-not $src) { Log ("  " + $f.mod + ": BRAK zdrowej kopii zatwierdzonej (" + $f.backup + " / " + $f.mirror + ")!"); $allOk = $false; continue }
        if ($src -ne $f.backup) { Log ("  " + $f.mod + ": kopia na C: zepsuta (md5 " + (Md5 $f.backup) + ") - biore lustro " + $src) }
        $done = $false
        for ($i = 1; $i -le 12 -and -not $done; $i++) {
            try { Copy-Verified $src $f.target $f.approvedMd5; $done = $true }
            catch { Log ("  " + $f.mod + ": proba " + $i + " nieudana (" + $_.Exception.Message + ")"); Start-Sleep -Seconds 5 }
        }
        if ($done) { Log ("  " + $f.mod + ": PRZYWROCONY zatwierdzony, md5 " + $f.approvedMd5 + " OK") }
        else { Log ("  " + $f.mod + ": PRZYWRACANIE NIEUDANE - w grze md5 " + (Md5 $f.target)); $allOk = $false }
    }
    if ($man.PSObject.Properties.Name -contains 'configsBackup' -and $man.configsBackup) {
        if ($strict) {
            # po przerwanym biegu Jeff mogl juz grac - jego Configs zostaja; kopia sprzed testu czeka
            Log ("  Configs: po przerwanym biegu NIE przywracam automatycznie - kopia sprzed testu: " + $man.configsBackup)
        } elseif (-not (Restore-Configs $man)) { $allOk = $false }
    }
    return $allOk
}

function Restore-Configs($man) {
    # pliki Configs zmienione w czasie testu wracaja do wersji Jeffa; nowe ida do kopii
    $ok = $true
    $before = @{}
    foreach ($p in $man.configsHashes.PSObject.Properties) { $before[$p.Name] = $p.Value }
    $now = Get-TreeHashes $configsDir
    $changed = 0; $added = 0
    foreach ($rel in $before.Keys) {
        $dst = Join-Path $configsDir $rel
        if ($now.ContainsKey($rel) -and $now[$rel] -eq $before[$rel]) { continue }
        $src = $null
        foreach ($root in @($man.configsBackup, $man.configsMirror)) {
            if ($root) { $c = Join-Path $root $rel; if ((Md5 $c) -eq $before[$rel]) { $src = $c; break } }
        }
        if (-not $src) { Log ("  Configs: " + $rel + " zmieniony, a kopia zepsuta - NIE przywracam"); $ok = $false; continue }
        try { Copy-Verified $src $dst $before[$rel]; $changed++; Log ("  Configs: przywrocony " + $rel) }
        catch { Log ("  Configs: " + $rel + " przywracanie nieudane: " + $_.Exception.Message); $ok = $false }
    }
    $newDir = Join-Path (Split-Path -Parent $man.configsBackup) 'Configs-nowe-z-testu'
    foreach ($rel in $now.Keys) {
        if ($before.ContainsKey($rel)) { continue }
        $src = Join-Path $configsDir $rel
        $dst = Join-Path $newDir $rel
        try {
            $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dst)
            Move-Item -LiteralPath $src -Destination $dst -Force
            $added++
            Log ("  Configs: nowy plik z testu przeniesiony do kopii: " + $rel)
        } catch { Log ("  Configs: nie da sie przeniesc nowego " + $rel + ": " + $_.Exception.Message) }
    }
    Log ("  Configs: przywroconych " + $changed + ", nowych przeniesionych " + $added + ", reszta bez zmian")
    return $ok
}

# ------------------------------------------------------------------ log autotestu (sekcja tego biegu)

function Find-AutoLog {
    if (-not (Test-Path -LiteralPath $csDir)) { return $null }
    foreach ($f in @(Get-ChildItem -LiteralPath $csDir -Filter 'autotest-*.log' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 3)) {
        $t = ''
        try { $t = [IO.File]::ReadAllText($f.FullName) } catch { continue }
        if ($t.Contains('START run=' + $runId)) { return $f.FullName }
    }
    return $null
}

function Read-Section([string]$path) {
    if (-not $path) { return '' }
    $t = ''
    try {
        $fs = New-Object IO.FileStream($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]'ReadWrite, Delete')
        try { $sr = New-Object IO.StreamReader($fs, [Text.Encoding]::UTF8); $t = $sr.ReadToEnd() } finally { $fs.Dispose() }
    } catch { return '' }
    $i = $t.IndexOf('START run=' + $runId)
    if ($i -lt 0) { return '' }
    $s = $t.Substring($i)
    $j = $s.IndexOf('START run=', 10)
    if ($j -gt 0) { $s = $s.Substring(0, $j) }
    return $s
}

function Get-ExitCrash {
    # AT1b: silnik potrafi wywrocic sie PO zamknieciu czesci zarzadzanej (rgl_log: "Managed Interface deleted",
    # potem 0xC0000005 w TaleWorlds.Native) i czekac na klikniecie w swoim oknie bledu - tak samo w grze Jeffa
    # (05.10 07:46 i 15:29: ten sam adres ...E20A; 06.10 14:14), wiec to nie wynik testu.
    if (-not $script:proc) { return $null }
    $f = Join-Path $EngineLogDir ('rgl_log_errors_' + $script:proc.Id + '.txt')
    if (-not (Test-Path -LiteralPath $f)) { return $null }
    $t = ''
    try {
        $fs = New-Object IO.FileStream($f, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]'ReadWrite, Delete')
        try { $t = (New-Object IO.StreamReader($fs)).ReadToEnd() } finally { $fs.Dispose() }
    } catch { return $null }
    $m = [regex]::Match($t, 'Unhandled Exception Code (0x[0-9A-Fa-f]+) at adress (0x[0-9A-Fa-f]+)')
    if (-not $m.Success) { return $null }
    return ('silnik wywrocil sie przy zamykaniu: ' + $m.Groups[1].Value + ' pod ' + $m.Groups[2].Value + ' (' + $f + '); okno bledu silnika czeka na klikniecie - znany blad przy wyjsciu z gry, nie wynik testu')
}

function Get-LastDay([string]$sec) {
    $m = [regex]::Matches($sec, '\] DOBA (\d+)/')
    if ($m.Count -eq 0) { return 0 }
    return [int]$m[$m.Count - 1].Groups[1].Value
}

# ------------------------------------------------------------------ (a) stan wyjsciowy

$null = New-Item -ItemType Directory -Force -Path $BackupRoot
$script:logFile = Join-Path $BackupRoot ('autotest-skrypt-' + $stamp + '.log')
Log ("autotest.ps1 start | run " + $runId + " | dni " + $Days + " | gra " + $GameDir)

$running = Get-GameProcs
if ($running.Count -gt 0) {
    Log ("STOP: gra dziala (" + (($running | ForEach-Object { $_.ProcessName + '#' + $_.Id }) -join ', ') + ") - Jeff gra albo gra wisi. Nic nie ruszam.")
    exit 2
}

if (Test-Path -LiteralPath $marker) {
    $prev = Get-Content -LiteralPath $marker -Raw -Encoding UTF8 | ConvertFrom-Json
    Log ("Poprzedni bieg nie skonczyl przywracania (" + $prev.manifest + ") - przywracam teraz")
    if (Restore-Manifest $prev.manifest $true) { Remove-Item -LiteralPath $marker -Force; Log "Przywrocone, znacznik usuniety." }
    else { Log "STOP: przywracanie po poprzednim biegu NIEUDANE albo nieznana wersja w grze - znacznik zostaje."; exit 3 }
    if (Test-Path -LiteralPath $switchPath) { Remove-Item -LiteralPath $switchPath -Force; Log "Usuniety stary przelacznik autotest.json." }
}
if ($RestoreOnly) { Log "Tryb -RestoreOnly: koniec."; exit 0 }

# ------------------------------------------------------------------ wejscie: DLL probne

if (-not $CrashScribeDll) {
    $def = Join-Path $PSScriptRoot '..\CrashScribe\bin\Release\CrashScribe.dll'
    if (Test-Path -LiteralPath $def) { $CrashScribeDll = (Resolve-Path -LiteralPath $def).Path }
}
$trials = @()
function Add-Trial([string]$mod, [string]$path) {
    if (-not $path) { return }
    $full = (Resolve-Path -LiteralPath $path).Path
    $target = Join-Path $GameDir ('Modules\' + $mod + '\bin\Win64_Shipping_Client\' + $mod + '.dll')
    if (-not (Test-Path -LiteralPath $target)) { throw ("brak " + $target + " - podmieniamy tylko istniejace mody") }
    if (-not (Test-Pe $full)) { throw ("DLL probny " + $full + " nie jest plikiem PE (zera?)") }
    $script:trials += [pscustomobject]@{ mod = $mod; trial = $full; trialMd5 = (Md5 $full); target = $target }
}
try {
    if ($CrashScribeDll) { Add-Trial 'CrashScribe' $CrashScribeDll }
    if ($ArmouryDll) { Add-Trial 'Armoury' $ArmouryDll }
    foreach ($x in $ExtraDll) { Add-Trial ([IO.Path]::GetFileNameWithoutExtension($x)) $x }
} catch { Log ("STOP: " + $_.Exception.Message); exit 2 }

$csInGame = Join-Path $GameDir 'Modules\CrashScribe\bin\Win64_Shipping_Client\CrashScribe.dll'
$csTrial = @($trials | Where-Object { $_.mod -eq 'CrashScribe' })
if ($csTrial.Count -gt 0) {
    if (-not (Test-HasAutotest $csTrial[0].trial)) { Log ("STOP: " + $csTrial[0].trial + " nie ma trybu autotestu (brak napisu autotest.json)"); exit 2 }
} elseif (-not (Test-HasAutotest $csInGame)) {
    Log "STOP: CrashScribe w grze nie ma trybu autotestu, a nie podano -CrashScribeDll"; exit 2
}

# ------------------------------------------------------------------ lista modow z LauncherData.xml (tylko odczyt)

$ldPath = Join-Path $configsDir 'LauncherData.xml'
if (-not (Test-Path -LiteralPath $ldPath)) { Log ("STOP: brak " + $ldPath); exit 2 }
[xml]$ld = Get-Content -LiteralPath $ldPath -Raw -Encoding UTF8
$known = @{}
foreach ($sm in Get-ChildItem -LiteralPath (Join-Path $GameDir 'Modules') -Directory) {
    $x = Join-Path $sm.FullName 'SubModule.xml'
    if (-not (Test-Path -LiteralPath $x)) { continue }
    try { [xml]$mx = Get-Content -LiteralPath $x -Raw; $id = $mx.Module.Id.value; if ($id) { $known[$id] = $sm.Name } } catch { }
}
$ids = @()
foreach ($m in @($ld.UserData.SingleplayerData.ModDatas.UserModData)) {
    if ($m.IsSelected -ne 'true') { continue }
    if ($known.ContainsKey($m.Id)) { $ids += $m.Id } else { Log ("UWAGA: wlaczony w LauncherData mod " + $m.Id + " - brak na dysku, pomijam (launcher tez by go nie podal)") }
}
if ($ids -notcontains 'CrashScribe') { Log "STOP: CrashScribe nie jest wlaczony w LauncherData.xml"; exit 2 }
$modArg = '_MODULES_*' + ($ids -join '*') + '*_MODULES_'
Log ("Mody (" + $ids.Count + ", kolejnosc LauncherData.xml): " + ($ids -join ', '))

# BLSE Standalone sprawdza, czy katalog roboczy to bin\Win64_Shipping_Client (inaczej okno bledu i wyjscie)
$exe = Join-Path $binDir 'Bannerlord.BLSE.Standalone.exe'
$argLine = '/singleplayer "' + $modArg + '"'
$workDir = $binDir
if ($LaunchExe) { $exe = $LaunchExe; if ($LaunchArgs) { $argLine = $LaunchArgs } }
if (-not (Test-Path -LiteralPath $exe)) { Log ("STOP: brak " + $exe); exit 2 }
if (-not (Test-Path -LiteralPath $workDir)) { Log ("STOP: brak " + $workDir); exit 2 }
if (-not $SkipSteamCheck -and -not $LaunchExe) {
    if (@(Get-Process -Name 'steam' -ErrorAction SilentlyContinue).Count -eq 0) {
        Log "STOP: Steam nie dziala. Gra ze Steam (steam_appid.txt 261550) potrzebuje klienta Steam; uruchom Steam (jako zwykly uzytkownik, nie admin - inaczej BLSE wyswietli blad i czeka) i powtorz."
        exit 2
    }
}
Log ("Polecenie: [" + $workDir + "] " + $exe + " " + $argLine)

if ($TimeoutMin -le 0) { $TimeoutMin = [int][Math]::Ceiling($Days * 25 / 60.0) + 10 }
Log ("Limit czasu: " + $TimeoutMin + " min (" + $Days + " dob x 25 s + 10 min); start do pierwszej doby " + $StartMin + " min; postoj " + $StallMin + " min")

# ------------------------------------------------------------------ stan Jeffa przed testem (do porownania)

function Get-JeffSaves {
    $h = @{}
    if (Test-Path -LiteralPath $savesDir) {
        foreach ($f in Get-ChildItem -LiteralPath $savesDir -Filter '*.sav' -File) {
            if ($f.Name -like 'autotest-*') { continue }
            if ($f.Name -like 'saveauto*') { $h[$f.Name] = Md5 $f.FullName } else { $h[$f.Name] = [string]$f.Length + '|' + $f.LastWriteTimeUtc.Ticks }
        }
    }
    return $h
}
$savesBefore = Get-JeffSaves

# ------------------------------------------------------------------ (b) kopie + (c) wgranie + (d) przelacznik + (e) start + (f) czekanie

$null = New-Item -ItemType Directory -Force -Path $runDir
$mirrorDir = $null
if (-not $NoMirror -and (Test-Path -LiteralPath (Split-Path -Parent $MirrorRoot))) { $mirrorDir = Join-Path $MirrorRoot $stamp }
$manifestPath = Join-Path $runDir 'manifest.json'
$t0 = Get-Date
$result = 'nie-ruszyl'
$reachedDay = 0
$restoredOk = $false
$installed = $false

try {
    # (b) kopie zatwierdzonych
    $files = @()
    foreach ($tr in $trials) {
        $appr = Md5 $tr.target
        if (-not (Test-Pe $tr.target)) { Log ("UWAGA: zatwierdzony " + $tr.target + " nie zaczyna sie od MZ (zera?) - kopiuje taki, jaki jest") }
        $bk = Join-Path $runDir ($tr.mod + '.dll')
        Copy-Verified $tr.target $bk $appr
        $mi = $null
        if ($mirrorDir) {
            try { $mi = Join-Path $mirrorDir ($tr.mod + '.dll'); Copy-Verified $tr.target $mi $appr } catch { Log ("UWAGA: lustro " + $mi + " nieudane: " + $_.Exception.Message); $mi = $null }
        }
        Log ("Kopia zatwierdzonego " + $tr.mod + ": md5 " + $appr + " -> " + $bk + $(if ($mi) { " + " + $mi } else { "" }))
        $files += [pscustomobject]@{ mod = $tr.mod; target = $tr.target; backup = $bk; mirror = $mi; approvedMd5 = $appr; trial = $tr.trial; trialMd5 = $tr.trialMd5 }
    }
    $cfgBk = Join-Path $runDir 'Configs'
    $cfgHashes = Get-TreeHashes $configsDir
    foreach ($rel in $cfgHashes.Keys) { Copy-Verified (Join-Path $configsDir $rel) (Join-Path $cfgBk $rel) $cfgHashes[$rel] }
    $cfgMi = $null
    if ($mirrorDir) {
        try { $cfgMi = Join-Path $mirrorDir 'Configs'; foreach ($rel in $cfgHashes.Keys) { Copy-Verified (Join-Path $configsDir $rel) (Join-Path $cfgMi $rel) $cfgHashes[$rel] } }
        catch { Log ("UWAGA: lustro Configs nieudane: " + $_.Exception.Message); $cfgMi = $null }
    }
    Log ("Kopia Configs: " + $cfgHashes.Count + " plikow (md5) -> " + $cfgBk)
    $man = [pscustomobject]@{ stamp = $stamp; runId = $runId; files = $files; configsBackup = $cfgBk; configsMirror = $cfgMi; configsHashes = [pscustomobject]$cfgHashes }
    $man | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    if ($mirrorDir) { try { Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $mirrorDir 'manifest.json') -Force } catch { } }
    [pscustomobject]@{ manifest = $manifestPath; runId = $runId; started = $stamp } | ConvertTo-Json | Set-Content -LiteralPath $marker -Encoding UTF8

    # (c) wgranie probnych
    $installed = $true
    foreach ($f in $files) {
        if ($f.approvedMd5 -eq $f.trialMd5) { Log ("" + $f.mod + ": probny = zatwierdzony (md5 " + $f.trialMd5 + "), bez podmiany"); continue }
        Copy-Verified $f.trial $f.target $f.trialMd5
        Log ("Wgrany probny " + $f.mod + ": md5 " + $f.trialMd5 + " (z " + $f.trial + ")")
    }

    # (d) przelacznik
    $null = New-Item -ItemType Directory -Force -Path $csDir
    if (Test-Path -LiteralPath $switchPath) { Remove-Item -LiteralPath $switchPath -Force; Log "Usuniety stary przelacznik." }
    $limitS = [Math]::Max(0, $TimeoutMin * 60 - 180)
    $sw = '{"run":"' + $runId + '","days":' + $Days + ',"save":' + $(if ($NoSave) { 'false' } else { 'true' }) + ',"quit":' + $(if ($NoQuit) { 'false' } else { 'true' }) +
          ',"speed":' + $Speed.ToString([Globalization.CultureInfo]::InvariantCulture) + ',"limit_s":' + $limitS + '}'
    [IO.File]::WriteAllText($switchPath, $sw, (New-Object Text.UTF8Encoding($false)))
    Log ("Przelacznik: " + $switchPath + " = " + $sw)

    # (e) start
    $t0 = Get-Date
    $script:proc = Start-Process -FilePath $exe -ArgumentList $argLine -WorkingDirectory $workDir -PassThru
    Log ("Gra uruchomiona: PID " + $script:proc.Id)

    # (f) czekanie
    $deadline = $t0.AddMinutes($TimeoutMin)
    $consumed = $false
    $lastDay = 0
    $lastProgress = Get-Date
    $koniecAt = $null
    $quitAt = $null
    $autoLog = $null
    $lastReport = Get-Date
    $result = 'w-toku'
    while ($true) {
        Start-Sleep -Seconds $PollSec
        $alive = Get-GameProcs
        if (-not $autoLog) { $autoLog = Find-AutoLog }
        $sec = Read-Section $autoLog
        $d = Get-LastDay $sec
        if ($d -gt $lastDay) { $lastDay = $d; $lastProgress = Get-Date }
        if (-not $consumed -and -not (Test-Path -LiteralPath $switchPath)) { $consumed = $true; Log "Przelacznik zuzyty przez gre (CrashScribe rusza autotest)." }
        if (-not $koniecAt -and $sec -match '\] KONIEC (OK|BLAD)') { $koniecAt = Get-Date; Log ("Gra zglosila koniec: " + (([regex]::Match($sec, '\] (KONIEC [^\r\n]*)')).Groups[1].Value)) }
        if (-not $quitAt -and $sec -match '\] WYJSCIE: QuitGame') { $quitAt = Get-Date; Log "Gra zglosila QuitGame - czekam na koniec procesu." }
        if (((Get-Date) - $lastReport).TotalSeconds -ge 60) {
            $lastReport = Get-Date
            Log ("  postep: doba " + $lastDay + "/" + $Days + ", " + [int]((Get-Date) - $t0).TotalMinutes + " min, procesy gry: " + $alive.Count)
        }
        if ($alive.Count -eq 0) {
            if ($koniecAt) { $result = 'koniec-gry' } else { $result = 'wywrotka' }
            if ($quitAt) { $script:exitNote = 'proces zakonczyl sie sam po QuitGame' }
            Log ("Gra zakonczona (" + $result + "), kod wyjscia " + $(try { $script:proc.ExitCode } catch { '?' }))
            break
        }
        $el = ((Get-Date) - $t0).TotalSeconds
        # AT1b: po QuitGame czesc zarzadzana juz sie zamknela - proces, ktory nie konczy sie w ExitWaitSec, to silnik
        # przy sprzataniu (bieg 07.10: 0xC0000005 po "Managed Interface deleted" i okno bledu; 10 min czekania na nic)
        if ($quitAt -and ((Get-Date) - $quitAt).TotalSeconds -gt $ExitWaitSec) {
            $result = 'wyjscie-wisi'
            $crash = Get-ExitCrash
            $script:exitNote = 'proces nie wyszedl w ' + $ExitWaitSec + ' s po QuitGame' + $(if ($crash) { ' - ' + $crash } else { ' (w rgl_log_errors brak wywrotki)' })
            Stop-Game ("po QuitGame: " + $script:exitNote)
            break
        }
        if (-not $consumed -and $el -gt $SwitchWaitSec) { $result = 'przelacznik-niezuzyty'; Stop-Game ("przelacznik niezuzyty po " + $SwitchWaitSec + " s - CrashScribe w grze bez autotestu?"); break }
        if ($koniecAt -and ((Get-Date) - $koniecAt).TotalSeconds -gt $QuitWaitSec) { $result = 'koniec-bez-wyjscia'; Stop-Game ("po KONIEC gra nie wyszla w " + $QuitWaitSec + " s"); break }
        if ((Get-Date) -gt $deadline) { $result = 'limit-czasu'; Stop-Game ("limit " + $TimeoutMin + " min"); break }
        if (-not $koniecAt -and $lastDay -eq 0 -and $el -gt $StartMin * 60) { $result = 'brak-startu'; Stop-Game ("brak pierwszej doby po " + $StartMin + " min"); break }
        if (-not $koniecAt -and $lastDay -gt 0 -and ((Get-Date) - $lastProgress).TotalMinutes -gt $StallMin) { $result = 'postoj'; Stop-Game ("brak nowej doby od " + $StallMin + " min (ostatnia " + $lastDay + ")"); break }
    }
    $reachedDay = $lastDay
}
catch {
    Log ("BLAD SKRYPTU: " + $_.Exception.Message)
    $result = 'blad-skryptu'
}
finally {
    # (g) ZAWSZE: gra zamknieta, zatwierdzone DLL z powrotem, Configs Jeffa z powrotem
    try { Stop-Game "sprzatanie po tescie" } catch { Log ("Stop-Game: " + $_.Exception.Message) }
    if (Test-Path -LiteralPath $switchPath) {
        try { Remove-Item -LiteralPath $switchPath -Force; Log "Przelacznik NIEZUZYTY - usuniety (nastepne uruchomienie Jeffa bedzie zwykle)." } catch { Log ("NIE DA SIE usunac przelacznika " + $switchPath + ": " + $_.Exception.Message) }
    }
    if (Test-Path -LiteralPath $manifestPath) {
        Log "Przywracanie zatwierdzonych:"
        $restoredOk = $false
        try { $restoredOk = Restore-Manifest $manifestPath $false } catch { Log ("Przywracanie: " + $_.Exception.Message) }
        if ($restoredOk) { Remove-Item -LiteralPath $marker -Force -ErrorAction SilentlyContinue; Log "Przywracanie OK - znacznik usuniety." }
        else { Log ("PRZYWRACANIE NIEUDANE - znacznik " + $marker + " zostaje; uruchom: autotest.ps1 -RestoreOnly") }
    } else { $restoredOk = $true }
}

# ------------------------------------------------------------------ (h) wynik

$elapsed = (Get-Date) - $t0
$autoLog = Find-AutoLog
$sec = Read-Section $autoLog
if ($reachedDay -lt (Get-LastDay $sec)) { $reachedDay = Get-LastDay $sec }
$ok = ($sec -match '\] KONIEC OK') -and $reachedDay -ge $Days
$armLog = Get-ChildItem -LiteralPath (Join-Path $GameDir 'Modules\Armoury') -Filter 'Armoury-*.log' -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $t0 } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$sesLog = Get-ChildItem -LiteralPath $csDir -Filter 'session-*.log' -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $t0 } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$hangs = @(Get-ChildItem -LiteralPath $csDir -Filter 'hang-*.log' -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $t0 })
$newSaves = @(Get-ChildItem -LiteralPath $savesDir -Filter 'autotest-*.sav' -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $t0 })
$savesAfter = Get-JeffSaves
$savesChanged = @()
foreach ($k in $savesBefore.Keys) { if (-not $savesAfter.ContainsKey($k) -or $savesAfter[$k] -ne $savesBefore[$k]) { $savesChanged += $k } }
foreach ($k in $savesAfter.Keys) { if (-not $savesBefore.ContainsKey($k)) { $savesChanged += ($k + ' (nowy)') } }
$koniec = ([regex]::Match($sec, '\] (KONIEC [^\r\n]*)')).Groups[1].Value
$okna = ([regex]::Matches($sec, '\] OKNO ')).Count
$bledy = ([regex]::Matches($sec, '\] BLAD: ')).Count

if (-not $restoredOk) { $script:exitCode = 3 } elseif ($ok) { $script:exitCode = 0 } else { $script:exitCode = 1 }

$rep = @()
$rep += '=============================== WYNIK AUTOTESTU ==============================='
$rep += ('run          : ' + $runId + '   (' + $stamp + ')')
$rep += ('wynik        : ' + $(if ($ok) { 'OK - dotarl do ' + $reachedDay + '/' + $Days + ' dob' } else { 'NIE DOTARL (' + $result + ') - doba ' + $reachedDay + '/' + $Days }))
$rep += ('koniec w logu: ' + $(if ($koniec) { $koniec } else { '(brak linii KONIEC)' }))
$rep += ('czas         : ' + [int]$elapsed.TotalMinutes + ' min ' + $elapsed.Seconds + ' s od startu gry')
$rep += ('okna zamkn.  : ' + $okna + ' | bledy CrashScribe: ' + $bledy)
$rep += ('autotest log : ' + $(if ($autoLog) { $autoLog } else { '(brak - gra nie zuzyla przelacznika?)' }))
$rep += ('CrashScribe  : ' + $(if ($sesLog) { $sesLog.FullName } else { '(brak nowego session-*.log)' }))
$rep += ('Armoury      : ' + $(if ($armLog) { $armLog.FullName } else { '(brak nowego Armoury-*.log)' }))
$rep += ('zawieszenia  : ' + $(if ($hangs.Count -gt 0) { ($hangs | ForEach-Object { $_.FullName }) -join ', ' } else { 'brak hang-*.log' }))
$rep += ('wyjscie gry  : ' + $(if ($script:exitNote) { $script:exitNote } else { '(gra nie doszla do QuitGame)' }))
$rep += ('zapisy testu : ' + $(if ($newSaves.Count -gt 0) { ($newSaves | ForEach-Object { $_.Name }) -join ', ' } else { 'brak' }))
$rep += ('zapisy Jeffa : ' + $(if ($savesChanged.Count -eq 0) { 'nietkniete (' + $savesBefore.Count + ' plikow, saveauto1..3 md5 bez zmian)' } else { 'UWAGA ZMIENIONE: ' + ($savesChanged -join ', ') }))
$rep += ('DLL w grze   : ' + $(if ($restoredOk) { 'zatwierdzone (md5 sprawdzone)' } else { 'PRZYWRACANIE NIEUDANE - patrz log skryptu, -RestoreOnly' }))
$rep += ('kopie        : ' + $runDir + $(if ($mirrorDir) { ' + ' + $mirrorDir } else { '' }))
$rep += ('log skryptu  : ' + $script:logFile)
$rep += ('kod wyjscia  : ' + $script:exitCode + ' (0 OK, 1 nie dotarl, 2 stop przed testem, 3 przywracanie nieudane)')
$rep += '==============================================================================='
foreach ($l in $rep) { Log $l }
try { $rep | Set-Content -LiteralPath (Join-Path $runDir 'wynik.txt') -Encoding UTF8 } catch { }
exit $script:exitCode
