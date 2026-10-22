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
#       m105-drobne (8b): znany blad silnika przy wyjsciu (docs/ERRORS.md: 0xC0000005 po "Managed Interface deleted.")
#       konczy bieg OD RAZU - wywrotka w rgl_log_errors_<PID>.txt albo "Managed Interface deleted." w rgl_log_<PID>.txt
#       (po "Saving options...", czyli po zapisie engine_config / safely_exited) i proces zyje ExitQuickSec s - bez czekania 90 s;
#   (g) ZAWSZE (finally): zamkniecie gry, przywrocenie zatwierdzonych DLL i kontrola md5,
#       przywrocenie zmienionych plikow Configs, usuniecie niezuzytego przelacznika;
#   (h) wynik: logi Armoury, CrashScribe, autotest-*.log, czas, doba, kody bledow, zdjecia.
#
# AT2 - TRYB ZDJEC (-Photos / -PhotosFile): przelacznik dostaje "photos":[cele]; gra zaraz po wejsciu na mape
# (jasno: godz. 9-15, inaczej pierwsza jasna pora w miescie) ustawia kamere mapy na kazdy cel z kilku wysokosci
# (-PhotoShots "8,17,40") i robi zrzut ekranu do Documents\...\CrashScribe\zdjecia\<run>\<cel>-z08.png itd.;
# potem zwykly bieg. Cele:
#   -Photos wioski                          = cele z tools\autotest-zdjecia.json (4 wioski Armoury + wies gry)
#   -Photos "nazwa=X,Y;nazwa2=X,Y"          = polozenie na mapie (jedn. mapy, jak posX/posY; srednik miedzy celami)
#   -Photos "nazwa=id_osady" / "nazwa=id_osady,DX,DY" = osada gry + przesuniecie
#   -PhotosFile plik.json                   = "photos" / "photo_shots" / "photo_hours" z pliku (wzor: autotest-zdjecia.json)
# Bez -Photos i -PhotosFile przelacznik jest taki jak dotad (zadnych zdjec).
#
# AT5 - BITWY (-Battles N, -BattleSec S; kopia autotest.ps1 jako autotest-bitwa.ps1): przelacznik dostaje "battles":N i
# "battle_s":S; CrashScribe z trybem bitew (napis battle_s w DLL) po osadzeniu w miescie, przed pierwsza doba, rozgrywa walke
# cwiczebna na arenie miasta (bez -NoArena; napis mno_enter_practice_fight w DLL) i N bitew w polu z bandytami wedlug planu
# mocna / odwrot / slaba (prawdziwa misja: kod misji wszystkich modow), potem zwykly bieg. Raport wypisuje linie
# "AUTOTEST BITWA" i "AUTOTEST ARENA". Limit czasu i start do pierwszej doby dostaja N x (S + 240) s + 5 min na arene.
# Wywrotka silnika (proces padl bez QuitGame albo w swiezym rgl_log_errors_<PID>.txt "Unhandled Exception" przed QuitGame)
# = BLAD, takze gdy w logu jest "KONIEC OK ... dotarl". Wyjatek moda w bitwie ("AUTOTEST BITWA BLAD (") i log stoi 60 s = BLAD.
# Bez -Battles przelacznik jak dotad; po QuitGame wywrotka przy zamykaniu jak dotad (znany blad, nie wynik testu).
#
# Kody wyjscia: 0 = dotarl do N dob, przy -Battles takze misje sprawdzone, i wszystko przywrocone; 1 = test nie dotarl
# do N dob albo wywrotka (przywrocone); 2 = STOP przed testem (nic nie ruszone albo przywrocone); 3 = PRZYWRACANIE
# NIEUDANE - w grze moze zostac DLL probny (znacznik w-toku.json zostaje, -RestoreOnly); 4 = doby OK, ale misje NIE
# sprawdzone (przy -Battles: nie wszystkie bitwy z KONIEC, NIE RUSZYLA, linie BLAD, 0 ciosow wrecz, 0 wygranych,
# arena bez KONIEC albo bez ciosow) - takiego DLL nie wgrywac jako sprawdzonego w misjach.
#
# Przyklady:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\autotest.ps1 -CrashScribeDll CrashScribe\bin\Release\CrashScribe.dll -ArmouryDll Armoury\bin\Release\Armoury.dll -Days 40
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\autotest.ps1 -RestoreOnly
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\autotest.ps1 -CrashScribeDll <CrashScribe-zdjecia.dll> -ArmouryDll <Armoury.dll> -Days 2 -Photos wioski
#
# Plik w ASCII (PowerShell 5.1 czyta .ps1 bez BOM jako ANSI).

[CmdletBinding()]
param(
    [string]$ArmouryDll,
    [string]$CrashScribeDll,
    [string[]]$ExtraDll = @(),
    [string[]]$ExtraFile = @(),    # AT4: pliki poza DLL na czas testu: "Armoury\ModuleData\plik.tsv=C:\zrodlo\plik.tsv" (cel wzgledem Modules; plik, ktorego przed testem nie bylo, po tescie znika)
    [int]$Days = 40,
    [int]$TimeoutMin = 0,          # 0 = 25 s na dobe + 10 min
    [double]$Speed = 0,            # 0 = mnoznik jak w grze Jeffa (Armoury MCM)
    [int]$StartMin = 20,           # bez pierwszej doby po tylu minutach = gra stoi przed biegiem
    [int]$StallMin = 12,           # bez nowej doby przez tyle minut = postoj (mod sam konczy po 10)
    [int]$SwitchWaitSec = 480,     # przelacznik niezuzyty po tylu s = w grze nie ma trybu autotestu (16.10: przy 39 modach gra zuzyla go po ok. 246 s)
    [int]$QuitWaitSec = 600,       # po "KONIEC" w logu gra ma tyle s na zapis i wyjscie
    [int]$ExitWaitSec = 90,        # po "WYJSCIE: QuitGame" w logu proces ma tyle s na zakonczenie (AT1b)
    [int]$ExitQuickSec = 5,        # m105-drobne (8b): po "Managed Interface deleted." w rgl_log_<PID> proces zyje tyle s = znany blad przy wyjsciu, zamkniecie od razu (0 = jak dotad, tylko ExitWaitSec)
    [string]$EngineLogDir = 'C:\ProgramData\Mount and Blade II Bannerlord\logs',   # rgl_log_errors_<PID>.txt (tylko odczyt)
    [int]$PollSec = 10,
    [string[]]$Photos = @(),       # AT2: cele zdjec (opis wyzej); "wioski" = tools\autotest-zdjecia.json
    [string]$PhotosFile,           # AT2: plik JSON z "photos" (i opcjonalnie "photo_shots", "photo_hours")
    [string]$PhotoShots = '',      # AT2: wysokosci kamery nad celem, np. "8,17,40" (puste = z pliku albo 8,17,40)
    [string]$PhotoHours = '',      # AT2: jasna pora, np. "9-15" (puste = z pliku albo 9-15)
    [string]$LoadSave = '',        # AT3: zamiast nowej kampanii wczytaj ten zapis (nazwa bez .sav, np. autotest-rok-364); -Days liczone od wczytania
    [int]$Census = 0,              # AT3: spis swiata co tyle dob (0 = brak)
    [switch]$Profile,              # AT3: pomiar klatki (sekcje silnika + sluchacze zdarzen kampanii)
    [int]$Battles = 0,             # AT5: tyle bitew w polu przed pierwsza doba (0 = brak)
    [int]$BattleSec = 150,         # AT5: sekundy rzeczywiste na jedna bitwe
    [switch]$NoArena,              # AT5: bez walki cwiczebnej na arenie przed bitwami
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
# AT5: kopia skryptu lezy poza repo - pliki obok oryginalu (wzor zdjec, domyslny CrashScribe) jak dotad
$ToolsDir = 'C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad\dzien-6\autotest\repo\tools'

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
$script:midAt = $null   # m105-drobne (8b): chwila zobaczenia "Managed Interface deleted." w rgl_log_<PID> po QuitGame
$script:crashNote = $null
$script:modNote = $null

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

function Test-HasText([string]$p, [string]$needle) {
    # literal C# w DLL = UTF-16LE
    if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { return $false }
    $bytes = [IO.File]::ReadAllBytes($p)
    $text = [Text.Encoding]::Unicode.GetString($bytes)
    $text2 = [Text.Encoding]::Unicode.GetString($bytes, 1, $bytes.Length - 1)
    return ($text.Contains($needle) -or $text2.Contains($needle))
}

function Test-HasAutotest([string]$p) {
    # build z trybem autotestu ma w sobie napis "autotest.json"
    return (Test-HasText $p 'autotest.json')
}

# ------------------------------------------------------------------ AT2: cele zdjec

$inv = [Globalization.CultureInfo]::InvariantCulture
$script:photoItems = @()
$script:photoNames = @()

function Get-Num([string]$s) {
    $v = 0.0
    if ($null -ne $s -and [double]::TryParse($s.Trim(), [Globalization.NumberStyles]::Float, $inv, [ref]$v)) { return $v }
    return $null
}

function Get-Prop($o, [string]$k) {
    $p = $o.PSObject.Properties[$k]
    if ($p) { return $p.Value }
    return $null
}

function Add-PhotoTarget([string]$name, $x, $y, [string]$settlement, $dx, $dy, $bearing) {
    $n = ($name -replace '[^A-Za-z0-9_.-]', '_').Trim('.')
    $s = ($settlement -replace '[^A-Za-z0-9_.:-]', '')
    if (-not $n) { $n = $(if ($s) { $s -replace ':', '_' } else { 'cel' + ($script:photoItems.Count + 1) }) }
    $parts = @('"name":"' + $n + '"')
    if ($null -ne $x -and $null -ne $y) {
        $parts += ('"x":' + ([double]$x).ToString('0.###', $inv))
        $parts += ('"y":' + ([double]$y).ToString('0.###', $inv))
    } elseif ($s) {
        $parts += ('"settlement":"' + $s + '"')
        if ($null -ne $dx) { $parts += ('"dx":' + ([double]$dx).ToString('0.###', $inv)) }
        if ($null -ne $dy) { $parts += ('"dy":' + ([double]$dy).ToString('0.###', $inv)) }
    } else { throw ("cel zdjecia '" + $name + "': brak x,y i brak id osady") }
    if ($null -ne $bearing) { $parts += ('"bearing":' + ([double]$bearing).ToString('0.###', $inv)) }
    $script:photoItems += ('{' + ($parts -join ',') + '}')
    $script:photoNames += $n
}

function Read-PhotoFile([string]$path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw ("brak pliku celow zdjec " + $path) }
    $j = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    $res = @{ shots = [string](Get-Prop $j 'photo_shots'); hours = [string](Get-Prop $j 'photo_hours') }
    foreach ($o in @(Get-Prop $j 'photos')) {
        if ($null -eq $o) { continue }
        $x = Get-Prop $o 'x'; $y = Get-Prop $o 'y'
        Add-PhotoTarget ([string](Get-Prop $o 'name')) $(if ($null -ne $x) { [double]$x } else { $null }) $(if ($null -ne $y) { [double]$y } else { $null }) ([string](Get-Prop $o 'settlement')) (Get-Prop $o 'dx') (Get-Prop $o 'dy') (Get-Prop $o 'bearing')
    }
    return $res
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

function Stop-Game([string]$why, [switch]$Now) {
    $procs = Get-GameProcs
    if ($procs.Count -eq 0) { return }
    Log ("ZAMYKAM GRE: " + $why + " (procesy: " + (($procs | ForEach-Object { $_.ProcessName + '#' + $_.Id }) -join ', ') + ")" + $(if ($Now) { ' - od razu, bez CloseMainWindow' } else { '' }))
    # m105-drobne (8b): -Now tylko po zapisie opcji silnika i usunieciu czesci zarzadzanej (znany blad przy wyjsciu) - okno bledu silnika i tak nie zamknie sie samo
    if (-not $Now) {
        foreach ($p in $procs) { try { $null = $p.CloseMainWindow() } catch { } }
        $end = (Get-Date).AddSeconds(30)
        while ((Get-Date) -lt $end -and (Get-GameProcs).Count -gt 0) { Start-Sleep -Seconds 2 }
    }
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
        $isFile = ($f.PSObject.Properties['kind'] -and $f.kind -eq 'file')
        if ($isFile -and -not $f.approvedMd5) {
            # AT4: pliku przed testem nie bylo - usuwamy probny (przy strict tylko, gdy to na pewno probny)
            if (-not $cur) { Log ("  " + $f.mod + ": brak w grze, jak przed testem"); continue }
            if ($strict -and $cur -ne $f.trialMd5) { Log ("  " + $f.mod + ": w grze NIEZNANA wersja md5 " + $cur + " (probna " + $f.trialMd5 + ", przed testem pliku nie bylo) - NIE ruszam, sprawdz recznie"); $allOk = $false; continue }
            try { Remove-Item -LiteralPath $f.target -Force -ErrorAction Stop; Log ("  " + $f.mod + ": probny plik USUNIETY (przed testem go nie bylo)") }
            catch { Log ("  " + $f.mod + ": NIE DA SIE usunac probnego pliku: " + $_.Exception.Message); $allOk = $false }
            continue
        }
        if ($cur -eq $f.approvedMd5) { Log ("  " + $f.mod + ": w grze zatwierdzony (md5 " + $cur + ")"); continue }
        if ($strict -and $cur -and $cur -ne $f.trialMd5 -and ($isFile -or (Test-Pe $f.target))) {
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

function Read-EngineLog {
    # AT5: tylko SWIEZY rgl_log_errors_<PID>.txt tego biegu - plik zapisany po starcie gry i tylko tresc za ostatnim
    # "Starting new log file..." z data >= start gry. Gra trzyma kilka takich plikow (w wiekszosci "Unhandled Exception"
    # ze znanej wywrotki przy wyjsciu); gdy Windows da ten sam PID, stary plik nie moze zabic dobrego biegu.
    if (-not $script:proc) { return $null }
    $f = Join-Path $EngineLogDir ('rgl_log_errors_' + $script:proc.Id + '.txt')
    if (-not (Test-Path -LiteralPath $f)) { return $null }
    try { if ((Get-Item -LiteralPath $f).LastWriteTime -lt $t0) { return $null } } catch { return $null }
    $t = ''
    try {
        $fs = New-Object IO.FileStream($f, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]'ReadWrite, Delete')
        try { $t = (New-Object IO.StreamReader($fs)).ReadToEnd() } finally { $fs.Dispose() }
    } catch { return $null }
    $i = $t.LastIndexOf('Starting new log file')
    if ($i -lt 0) { return $null }   # silnik nie zaczal jeszcze nowego pliku - to, co jest, jest ze starego biegu
    $seg = $t.Substring($i)
    $hm = [regex]::Match($seg, '\] (\d{1,2} [A-Za-z]{3} \d{4}) [A-Za-z]+ (\d{1,2}:\d{2})')
    if ($hm.Success) {
        $d = [datetime]::MinValue
        if ([datetime]::TryParseExact($hm.Groups[1].Value + ' ' + $hm.Groups[2].Value, 'd MMM yyyy H:mm', $inv, [Globalization.DateTimeStyles]::None, [ref]$d)) {
            if ($d -lt $t0.AddSeconds(-$t0.Second - 60)) { return $null }   # naglowek ma minute: start gry minus minuta
        }
    }
    return [pscustomobject]@{ Path = $f; Text = $seg }
}

function Get-ExitCrash {
    # AT1b: silnik potrafi wywrocic sie PO zamknieciu czesci zarzadzanej (rgl_log: "Managed Interface deleted",
    # potem 0xC0000005 w TaleWorlds.Native) i czekac na klikniecie w swoim oknie bledu - tak samo w grze Jeffa
    # (05.10 07:46 i 15:29: ten sam adres ...E20A; 06.10 14:14), wiec to nie wynik testu.
    $el = Read-EngineLog
    if (-not $el) { return $null }
    $m = [regex]::Match($el.Text, 'Unhandled Exception Code (0x[0-9A-Fa-f]+) at adress (0x[0-9A-Fa-f]+)')
    if (-not $m.Success) { return $null }
    return ('silnik wywrocil sie przy zamykaniu: ' + $m.Groups[1].Value + ' pod ' + $m.Groups[2].Value + ' (' + $el.Path + '); okno bledu silnika czeka na klikniecie - znany blad przy wyjsciu z gry, nie wynik testu')
}

function Get-ExitMarker {
    # m105-drobne (8b): koniec rgl_log_<PID>.txt tego biegu (ostatnie 256 KB, plik zapisany po starcie gry) - "Managed Interface deleted." po "Saving options..."
    # (silnik zapisal juz opcje, w tym safely_exited, i usunal czesc zarzadzana; potem przychodzi znany 0xC0000005 przy sprzataniu). true = znacznik jest.
    if (-not $script:proc) { return $false }
    $f = Join-Path $EngineLogDir ('rgl_log_' + $script:proc.Id + '.txt')
    if (-not (Test-Path -LiteralPath $f)) { return $false }
    try { if ((Get-Item -LiteralPath $f).LastWriteTime -lt $t0) { return $false } } catch { return $false }
    $t = ''
    try {
        $fs = New-Object IO.FileStream($f, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]'ReadWrite, Delete')
        try {
            $n = [Math]::Min([long]262144, $fs.Length)
            $null = $fs.Seek(-$n, [IO.SeekOrigin]::End)
            $buf = New-Object byte[] ([int]$n)
            $got = $fs.Read($buf, 0, [int]$n)
            $t = [Text.Encoding]::UTF8.GetString($buf, 0, $got)
        } finally { $fs.Dispose() }
    } catch { return $false }
    $i = $t.LastIndexOf('Managed Interface deleted.')
    if ($i -lt 0) { return $false }
    $s = $t.LastIndexOf('Saving options', $i)
    $n0 = $t.LastIndexOf('Starting new log file', $i)
    return ($s -ge 0 -and $s -gt $n0)
}

function Get-EngineCrash {
    # AT5: wywrotka silnika w trakcie biegu (np. 0xC0000005 w misji) - swiezy rgl_log_errors_<PID>.txt ma "Unhandled Exception"
    $el = Read-EngineLog
    if (-not $el) { return $null }
    $m = [regex]::Match($el.Text, 'Unhandled Exception Code (0x[0-9A-Fa-f]+) at adress (0x[0-9A-Fa-f]+)')
    if ($m.Success) { return ('silnik: ' + $m.Groups[1].Value + ' pod ' + $m.Groups[2].Value + ' (' + $el.Path + ')') }
    return $null
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
    $def = Join-Path $ToolsDir '..\CrashScribe\bin\Release\CrashScribe.dll'
    if (Test-Path -LiteralPath $def) { $CrashScribeDll = (Resolve-Path -LiteralPath $def).Path }
}
$trials = @()
function Add-Trial([string]$mod, [string]$path) {
    if (-not $path) { return }
    $full = (Resolve-Path -LiteralPath $path).Path
    $target = Join-Path $GameDir ('Modules\' + $mod + '\bin\Win64_Shipping_Client\' + $mod + '.dll')
    if (-not (Test-Path -LiteralPath $target)) { throw ("brak " + $target + " - podmieniamy tylko istniejace mody") }
    if (-not (Test-Pe $full)) { throw ("DLL probny " + $full + " nie jest plikiem PE (zera?)") }
    $script:trials += [pscustomobject]@{ mod = $mod; kind = 'dll'; trial = $full; trialMd5 = (Md5 $full); target = $target }
}
function Add-TrialFile([string]$spec) {
    # AT4: "cel wzgledem Modules=zrodlo"; cel moze nie istniec (nowy plik moda) - wtedy po tescie jest usuwany
    $kv = $spec -split '=', 2
    if ($kv.Count -ne 2 -or -not $kv[0].Trim() -or -not $kv[1].Trim()) { throw ("-ExtraFile '" + $spec + "': oczekiwane cel=zrodlo") }
    $rel = $kv[0].Trim().TrimStart('\').Replace('/', '\')
    if ($rel.Contains('..') -or $rel.Contains(':')) { throw ("-ExtraFile '" + $spec + "': cel ma byc sciezka wzgledem Modules") }
    if ($rel.ToLowerInvariant().EndsWith('.dll')) { throw ("-ExtraFile '" + $spec + "': DLL podaj przez -ExtraDll") }
    $full = (Resolve-Path -LiteralPath $kv[1].Trim()).Path
    $modDir = Join-Path $GameDir ('Modules\' + ($rel -split '\\')[0])
    if (-not (Test-Path -LiteralPath $modDir -PathType Container)) { throw ("brak " + $modDir + " - pliki dokladamy tylko do istniejacych modow") }
    if ((Get-Item -LiteralPath $full).Length -eq 0) { throw ("plik probny " + $full + " jest pusty") }
    $script:trials += [pscustomobject]@{ mod = $rel; kind = 'file'; trial = $full; trialMd5 = (Md5 $full); target = (Join-Path $GameDir ('Modules\' + $rel)) }
}
try {
    if ($CrashScribeDll) { Add-Trial 'CrashScribe' $CrashScribeDll }
    if ($ArmouryDll) { Add-Trial 'Armoury' $ArmouryDll }
    foreach ($x in $ExtraDll) { Add-Trial ([IO.Path]::GetFileNameWithoutExtension($x)) $x }
    foreach ($x in $ExtraFile) { foreach ($y in ($x -split ';')) { if ($y.Trim()) { Add-TrialFile $y } } }
} catch { Log ("STOP: " + $_.Exception.Message); exit 2 }

$csInGame = Join-Path $GameDir 'Modules\CrashScribe\bin\Win64_Shipping_Client\CrashScribe.dll'
$csTrial = @($trials | Where-Object { $_.mod -eq 'CrashScribe' })
if ($csTrial.Count -gt 0) {
    if (-not (Test-HasAutotest $csTrial[0].trial)) { Log ("STOP: " + $csTrial[0].trial + " nie ma trybu autotestu (brak napisu autotest.json)"); exit 2 }
} elseif (-not (Test-HasAutotest $csInGame)) {
    Log "STOP: CrashScribe w grze nie ma trybu autotestu, a nie podano -CrashScribeDll"; exit 2
}

# AT2: cele zdjec z -Photos / -PhotosFile (bez nich przelacznik jak dotad)
$photoShotsEff = $PhotoShots
$photoHoursEff = $PhotoHours
function Use-PhotoFile([string]$pf) {
    # cele z pliku; ujecia i pora z pliku tylko, gdy nie podane w linii polecen
    $r = Read-PhotoFile $pf
    if (-not $script:photoShotsEff -and $r.shots) { $script:photoShotsEff = $r.shots }
    if (-not $script:photoHoursEff -and $r.hours) { $script:photoHoursEff = $r.hours }
}
try {
    # cele w kolejnosci podania: najpierw -PhotosFile, potem -Photos ("wioski" = plik wzoru w tym miejscu listy)
    if ($PhotosFile) { Use-PhotoFile $PhotosFile }
    foreach ($p in $Photos) {
        foreach ($q in ($p -split ';')) {
            $q = $q.Trim()
            if (-not $q) { continue }
            if ($q -eq 'wioski') { Use-PhotoFile (Join-Path $ToolsDir 'autotest-zdjecia.json'); continue }
            $kv = $q -split '=', 2
            if ($kv.Count -ne 2) { throw ("cel zdjecia '" + $q + "': brak '=' (nazwa=X,Y albo nazwa=id_osady[,DX,DY])") }
            $vals = @($kv[1] -split ',' | ForEach-Object { $_.Trim() })
            $a = Get-Num $vals[0]
            $b = $(if ($vals.Count -ge 2) { Get-Num $vals[1] } else { $null })
            if ($null -ne $a -and $null -ne $b -and $vals.Count -eq 2) { Add-PhotoTarget $kv[0].Trim() $a $b '' $null $null $null }
            elseif ($null -eq $a -and $vals[0]) {
                $dx = $(if ($vals.Count -ge 2) { Get-Num $vals[1] } else { $null })
                $dy = $(if ($vals.Count -ge 3) { Get-Num $vals[2] } else { $null })
                Add-PhotoTarget $kv[0].Trim() $null $null $vals[0] $dx $dy $null
            }
            else { throw ("cel zdjecia '" + $q + "': ani X,Y, ani id osady") }
        }
    }
} catch { Log ("STOP: " + $_.Exception.Message); exit 2 }
$photoShotsEff = ($photoShotsEff -replace '[^0-9.,; ]', '')
$photoHoursEff = ($photoHoursEff -replace '[^0-9-]', '')
if ($script:photoItems.Count -gt 0) {
    $csCheck = $(if ($csTrial.Count -gt 0) { $csTrial[0].trial } else { $csInGame })
    if (-not (Test-HasText $csCheck 'photo_shots')) { Log ("STOP: " + $csCheck + " nie ma trybu zdjec (AT2, brak napisu photo_shots) - podaj -CrashScribeDll z trybem zdjec"); exit 2 }
    Log ("Zdjecia: " + $script:photoItems.Count + " cel(e): " + ($script:photoNames -join ', ') + " | ujecia " + $(if ($photoShotsEff) { $photoShotsEff } else { '8,17,40 (domyslne)' }) + " | jasno " + $(if ($photoHoursEff) { $photoHoursEff } else { '9-15 (domyslne)' }) + " | katalog " + (Join-Path $csDir ('zdjecia\' + $runId)))
}

# AT5: bitwy
if ($Battles -lt 0) { $Battles = 0 }
if ($Battles -gt 0) {
    $csCheckB = $(if ($csTrial.Count -gt 0) { $csTrial[0].trial } else { $csInGame })
    if (-not (Test-HasText $csCheckB 'battle_s')) { Log ("STOP: " + $csCheckB + " nie ma trybu bitew (AT5, brak napisu battle_s) - podaj -CrashScribeDll z trybem bitew"); exit 2 }
    if (-not $NoArena -and -not (Test-HasText $csCheckB 'mno_enter_practice_fight')) { Log ("STOP: " + $csCheckB + " nie ma etapu areny (AT5, brak napisu mno_enter_practice_fight) - podaj nowszy CrashScribe z trybem bitew albo -NoArena"); exit 2 }
    if ($BattleSec -lt 20) { $BattleSec = 20 }
    Log ("Bitwy: " + $Battles + " x do " + $BattleSec + " s, plan mocna / odwrot / slaba" + $(if ($NoArena) { ", BEZ areny" } else { " + arena (walka cwiczebna) przed nimi" }) + " (przed pierwsza doba, linie AUTOTEST BITWA / ARENA w autotest log)")
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

$photoMin = $(if ($script:photoItems.Count -gt 0) { [int][Math]::Ceiling(3 + 1.5 * $script:photoItems.Count) } else { 0 })
$battleMin = $(if ($Battles -gt 0) { [int][Math]::Ceiling($Battles * ($BattleSec + 240) / 60.0) + $(if ($NoArena) { 0 } else { 5 }) } else { 0 })
if ($TimeoutMin -le 0) { $TimeoutMin = [int][Math]::Ceiling($Days * 25 / 60.0) + 10 + $photoMin + $battleMin }
if ($battleMin -gt 0) { $StartMin += $battleMin }   # AT5: bitwy sa przed pierwsza doba
Log ("Limit czasu: " + $TimeoutMin + " min (" + $Days + " dob x 25 s + 10 min" + $(if ($photoMin -gt 0) { " + zdjecia " + $photoMin + " min" } else { "" }) + $(if ($battleMin -gt 0) { " + bitwy " + $battleMin + " min" } else { "" }) + "); start do pierwszej doby " + $StartMin + " min; postoj " + $StallMin + " min")

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
        if ($tr.kind -eq 'file' -and -not $appr) {
            # AT4: pliku przed testem nie ma - nic do kopiowania, po tescie znika
            Log ("Plik " + $tr.mod + ": przed testem BRAK w grze - po tescie zostanie usuniety")
            $files += [pscustomobject]@{ mod = $tr.mod; kind = 'file'; target = $tr.target; backup = $null; mirror = $null; approvedMd5 = ''; trial = $tr.trial; trialMd5 = $tr.trialMd5 }
            continue
        }
        if ($tr.kind -eq 'dll' -and -not (Test-Pe $tr.target)) { Log ("UWAGA: zatwierdzony " + $tr.target + " nie zaczyna sie od MZ (zera?) - kopiuje taki, jaki jest") }
        $bkName = $(if ($tr.kind -eq 'file') { 'pliki\' + $tr.mod } else { $tr.mod + '.dll' })
        $bk = Join-Path $runDir $bkName
        Copy-Verified $tr.target $bk $appr
        $mi = $null
        if ($mirrorDir) {
            try { $mi = Join-Path $mirrorDir $bkName; Copy-Verified $tr.target $mi $appr } catch { Log ("UWAGA: lustro " + $mi + " nieudane: " + $_.Exception.Message); $mi = $null }
        }
        Log ("Kopia zatwierdzonego " + $tr.mod + ": md5 " + $appr + " -> " + $bk + $(if ($mi) { " + " + $mi } else { "" }))
        $files += [pscustomobject]@{ mod = $tr.mod; kind = $tr.kind; target = $tr.target; backup = $bk; mirror = $mi; approvedMd5 = $appr; trial = $tr.trial; trialMd5 = $tr.trialMd5 }
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
    # miekki limit w grze liczy sie od startu procesu gry (CrashScribe AT5); przy bitwach margines 8 min: laska 90 s na odwrot,
    # do 60 s na zamkniecie misji, zapis i wyjscie - zanim twardy limit skryptu zabije gre w srodku misji
    $limitS = [Math]::Max(0, $TimeoutMin * 60 - $(if ($Battles -gt 0) { 480 } else { 180 }))
    $sw = '{"run":"' + $runId + '","days":' + $Days + ',"save":' + $(if ($NoSave) { 'false' } else { 'true' }) + ',"quit":' + $(if ($NoQuit) { 'false' } else { 'true' }) +
          ',"speed":' + $Speed.ToString([Globalization.CultureInfo]::InvariantCulture) + ',"limit_s":' + $limitS
    if ($LoadSave) { $sw += ',"load":"' + $LoadSave + '"' }
    if ($Census -gt 0) { $sw += ',"census":' + $Census }
    if ($Profile) { $sw += ',"profile":true' }
    if ($Battles -gt 0) { $sw += ',"battles":' + $Battles + ',"battle_s":' + $BattleSec + $(if ($NoArena) { ',"arena":false' } else { '' }) }
    if ($script:photoItems.Count -gt 0) {
        $sw += ',"photos":[' + ($script:photoItems -join ',') + ']'
        if ($photoShotsEff) { $sw += ',"photo_shots":"' + $photoShotsEff + '"' }
        if ($photoHoursEff) { $sw += ',"photo_hours":"' + $photoHoursEff + '"' }
    }
    $sw += '}'
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
    $lastSecLen = -1
    $lastGrowAt = Get-Date
    $result = 'w-toku'
    while ($true) {
        Start-Sleep -Seconds $PollSec
        $alive = Get-GameProcs
        if (-not $autoLog) { $autoLog = Find-AutoLog }
        $sec = Read-Section $autoLog
        if ($sec.Length -ne $lastSecLen) { $lastSecLen = $sec.Length; $lastGrowAt = Get-Date }
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
            # AT5: proces padl po KONIEC, ale przed QuitGame (np. przy zapisie) - to wywrotka, nie koniec gry
            if ($koniecAt -and -not $quitAt) { $result = 'wywrotka-po-koncu' }
            if (-not $quitAt) { $script:crashNote = Get-EngineCrash }
            if ($quitAt) { $script:exitNote = 'proces zakonczyl sie sam po QuitGame' }
            Log ("Gra zakonczona (" + $result + "), kod wyjscia " + $(try { $script:proc.ExitCode } catch { '?' }))
            break
        }
        $el = ((Get-Date) - $t0).TotalSeconds
        # AT5: wyjatek innego moda w bitwie celowo wychodzi z OnApplicationTick (jak w zwyklej grze); z BetterExceptionWindow
        # gra stoi wtedy z oknem bledu - log autotestu nie rosnie. 60 s ciszy po takiej linii = BLAD 'wyjatek-moda', bez czekania na limit
        if (-not $quitAt -and $sec -match '\] AUTOTEST BITWA BLAD \(' -and ((Get-Date) - $lastGrowAt).TotalSeconds -gt 60) {
            $result = 'wyjatek-moda'
            $script:modNote = ([regex]::Match($sec, '\] (AUTOTEST BITWA BLAD \([^\r\n]*)')).Groups[1].Value
            Log ("WYJATEK MODA w bitwie, a log autotestu stoi od 60 s (gra czeka w oknie bledu?): " + $script:modNote)
            Stop-Game ("wyjatek moda w bitwie, log stoi 60 s")
            break
        }
        # AT5: wywrotka silnika przed QuitGame (okno bledu silnika czeka, proces wisi) - od razu BLAD, bez czekania na limit
        if (-not $quitAt) {
            $ec = Get-EngineCrash
            if ($ec) {
                $result = 'wywrotka-silnika'
                $script:crashNote = $ec
                Log ("WYWROTKA SILNIKA w trakcie biegu: " + $ec)
                Stop-Game ("wywrotka silnika: " + $ec)
                break
            }
        }
        # m105-drobne (8b): po QuitGame znany blad silnika przy wyjsciu - wywrotka w rgl_log_errors_<PID> albo "Managed Interface deleted." w rgl_log_<PID>
        # (opcje silnika juz zapisane) i proces zyje ExitQuickSec s - zamkniecie od razu (wynik jak po 90 s: 'wyjscie-wisi', nie wywrotka)
        if ($quitAt -and $ExitQuickSec -gt 0) {
            $crashQ = Get-ExitCrash
            if (-not $crashQ -and -not $script:midAt -and (Get-ExitMarker)) { $script:midAt = Get-Date; Log ("rgl_log: 'Managed Interface deleted.' po QuitGame (" + [int]((Get-Date) - $quitAt).TotalSeconds + " s) - proces jeszcze zyje.") }
            if ($crashQ -or ($script:midAt -and ((Get-Date) - $script:midAt).TotalSeconds -ge $ExitQuickSec)) {
                $result = 'wyjscie-wisi'
                $script:exitNote = 'znany blad silnika przy wyjsciu - zamkniety od razu, ' + [int]((Get-Date) - $quitAt).TotalSeconds + ' s po QuitGame (bez czekania ' + $ExitWaitSec + ' s)' + $(if ($crashQ) { ' - ' + $crashQ } else { ' - rgl_log: "Managed Interface deleted." po "Saving options...", proces zyl jeszcze ' + $ExitQuickSec + '+ s' })
                Stop-Game ("po QuitGame: " + $script:exitNote) -Now
                break
            }
        }
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
$crashFail = @('wywrotka', 'wywrotka-silnika', 'wywrotka-po-koncu', 'wyjatek-moda') -contains $result   # AT5
$ok = ($sec -match '\] KONIEC OK') -and $reachedDay -ge $Days -and -not $crashFail
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
# AT2: zdjecia tego biegu
$photoDir = Join-Path $csDir ('zdjecia\' + $runId)
$photoOut = @()
if (Test-Path -LiteralPath $photoDir) { $photoOut = @(Get-ChildItem -LiteralPath $photoDir -File | Sort-Object Name) }
$photoEndM = [regex]::Matches($sec, '\] (ZDJECIA (koniec|przerwane)[^\r\n]*)')
$photoEnd = $(if ($photoEndM.Count -gt 0) { $photoEndM[$photoEndM.Count - 1].Groups[1].Value } else { '' })
$photoMiss = ([regex]::Matches($sec, '\] ZDJECIE BRAK ')).Count
# AT5: linie bitew tego biegu
$bitwyM = [regex]::Matches($sec, '\] (AUTOTEST (BITW[AY]|ARENA)[^\r\n]*)')
$bitwyKoniec = ([regex]::Matches($sec, '\] AUTOTEST BITWA KONIEC ')).Count
$bitwyNie = ([regex]::Matches($sec, '\] AUTOTEST BITWA NIE RUSZYLA')).Count
$bitwyBlad = ([regex]::Matches($sec, '\] AUTOTEST BITWA BLAD')).Count
# AT5 (recenzja 16.10): kod wyjscia patrzy tez na bitwy - doby OK przy niesprawdzonych misjach to kod 4, nie 0
$bitwyCiosy = 0
$bitwyWygrane = 0
foreach ($k in [regex]::Matches($sec, '\] AUTOTEST BITWA KONIEC [^\r\n]*')) {
    $mm = [regex]::Match($k.Value, 'ciosy wrecz (\d+)')
    if ($mm.Success) { $bitwyCiosy += [int]$mm.Groups[1].Value }
    if ($k.Value -match 'wynik won') { $bitwyWygrane++ }
}
$arenaK = [regex]::Match($sec, '\] (AUTOTEST ARENA KONIEC[^\r\n]*)')
$arenaCiosy = 0
if ($arenaK.Success) { $am = [regex]::Match($arenaK.Value, 'ciosy wrecz (\d+)'); if ($am.Success) { $arenaCiosy = [int]$am.Groups[1].Value } }
$arenaNie = ([regex]::Matches($sec, '\] AUTOTEST ARENA NIE RUSZYLA')).Count
$battleWhy = @()
if ($Battles -gt 0) {
    if ($bitwyKoniec -lt $Battles) { $battleWhy += ('bitwy z linia KONIEC ' + $bitwyKoniec + '/' + $Battles) }
    if ($bitwyNie -gt 0) { $battleWhy += ('NIE RUSZYLA: ' + $bitwyNie) }
    if ($bitwyBlad -gt 0) { $battleWhy += ('linie AUTOTEST BITWA BLAD: ' + $bitwyBlad) }
    if ($bitwyCiosy -le 0) { $battleWhy += '0 ciosow wrecz w bitwach' }
    # 16.10 (Claude): 0 wygranych to los bitwy z bandytami, nie blad moda - tylko UWAGA w linii 'bitwy' (bez kodu 4)
    if (-not $NoArena) {
        $arenaTurniej = ([regex]::Matches($sec, '\] AUTOTEST ARENA NIE RUSZYLA[^\r\n]*trwa turniej')).Count   # 16.10: turniej w miescie blokuje walke cwiczebna - UWAGA, nie blad
        if ($arenaNie -gt 0 -and $arenaTurniej -ge $arenaNie) { }   # tylko turniej w miescie - UWAGA w linii 'arena', bez kodu 4
        elseif ($arenaNie -gt 0) { $battleWhy += 'arena NIE RUSZYLA' }
        elseif (-not $arenaK.Success) { $battleWhy += 'arena bez linii KONIEC' }
        elseif ($arenaCiosy -le 0) { $battleWhy += '0 ciosow wrecz na arenie' }
    }
}
$battleOk = ($battleWhy.Count -eq 0)

if (-not $restoredOk) { $script:exitCode = 3 } elseif ($ok -and $battleOk) { $script:exitCode = 0 } elseif ($ok) { $script:exitCode = 4 } else { $script:exitCode = 1 }

$rep = @()
$rep += '=============================== WYNIK AUTOTESTU ==============================='
$rep += ('run          : ' + $runId + '   (' + $stamp + ')')
$rep += ('wynik        : ' + $(if ($ok -and $battleOk) { 'OK - dotarl do ' + $reachedDay + '/' + $Days + ' dob' + $(if ($Battles -gt 0) { ', misje sprawdzone' } else { '' }) } elseif ($ok) { 'DOBY OK (' + $reachedDay + '/' + $Days + '), MISJE NIE SPRAWDZONE: ' + ($battleWhy -join '; ') } elseif ($crashFail -and ($sec -match '\] KONIEC OK')) { 'BLAD - dotarl do ' + $reachedDay + '/' + $Days + ' dob, ale WYWROTKA (' + $result + ')' } else { 'NIE DOTARL (' + $result + ') - doba ' + $reachedDay + '/' + $Days }))
$rep += ('koniec w logu: ' + $(if ($koniec) { $koniec } else { '(brak linii KONIEC)' }))
$rep += ('czas         : ' + [int]$elapsed.TotalMinutes + ' min ' + $elapsed.Seconds + ' s od startu gry')
$rep += ('okna zamkn.  : ' + $okna + ' | bledy CrashScribe: ' + $bledy)
$rep += ('autotest log : ' + $(if ($autoLog) { $autoLog } else { '(brak - gra nie zuzyla przelacznika?)' }))
$rep += ('CrashScribe  : ' + $(if ($sesLog) { $sesLog.FullName } else { '(brak nowego session-*.log)' }))
$rep += ('Armoury      : ' + $(if ($armLog) { $armLog.FullName } else { '(brak nowego Armoury-*.log)' }))
$rep += ('zawieszenia  : ' + $(if ($hangs.Count -gt 0) { ($hangs | ForEach-Object { $_.FullName }) -join ', ' } else { 'brak hang-*.log' }))
$rep += ('wyjscie gry  : ' + $(if ($script:exitNote) { $script:exitNote } else { '(gra nie doszla do QuitGame)' }))
if ($crashFail -or $script:crashNote) { $rep += ('wywrotka     : ' + $result + $(if ($script:crashNote) { ' | ' + $script:crashNote } elseif ($script:modNote) { ' | ' + $script:modNote } else { ' | w rgl_log_errors brak linii Unhandled Exception' })) }
if ($Battles -gt 0) {
    $rep += ('bitwy        : ' + $bitwyKoniec + '/' + $Battles + ' z linia KONIEC, ciosy wrecz ' + $bitwyCiosy + ', wygrane ' + $bitwyWygrane + $(if ($bitwyWygrane -lt 1) { ' (UWAGA: wygrana i lup NIE sprawdzone)' } else { '' }) + $(if ($bitwyNie -gt 0) { ' | NIE RUSZYLA: ' + $bitwyNie } else { '' }) + $(if ($bitwyBlad -gt 0) { ' | BLAD: ' + $bitwyBlad } else { '' }) + $(if ($battleOk) { ' | misje sprawdzone' } else { ' | MISJE NIE SPRAWDZONE: ' + ($battleWhy -join '; ') }))
    $rep += ('arena        : ' + $(if ($NoArena) { 'wylaczona (-NoArena)' } elseif ($arenaK.Success) { $arenaK.Groups[1].Value } elseif ($arenaNie -gt 0) { 'NIE RUSZYLA' } else { 'brak linii AUTOTEST ARENA KONIEC' }))
    foreach ($bm in $bitwyM) { $rep += ('  ' + $bm.Groups[1].Value) }
}
$rep += ('zapisy testu : ' + $(if ($newSaves.Count -gt 0) { ($newSaves | ForEach-Object { $_.Name }) -join ', ' } else { 'brak' }))
if ($script:photoItems.Count -gt 0) {
    $rep += ('zdjecia      : ' + $photoOut.Count + ' plikow w ' + $photoDir + $(if ($photoOut.Count -gt 0) { ' (' + (($photoOut | ForEach-Object { $_.Name }) -join ', ') + ')' } else { '' }) + $(if ($photoMiss -gt 0) { ' | BRAK ujec: ' + $photoMiss } else { '' }))
    $rep += ('zdjecia log  : ' + $(if ($photoEnd) { $photoEnd } else { '(brak linii "ZDJECIA koniec" - zob. autotest log, linie ZDJECIA / ZDJECIE)' }))
}
$rep += ('zapisy Jeffa : ' + $(if ($savesChanged.Count -eq 0) { 'nietkniete (' + $savesBefore.Count + ' plikow, saveauto1..3 md5 bez zmian)' } else { 'UWAGA ZMIENIONE: ' + ($savesChanged -join ', ') }))
$rep += ('DLL w grze   : ' + $(if ($restoredOk) { 'zatwierdzone (md5 sprawdzone)' } else { 'PRZYWRACANIE NIEUDANE - patrz log skryptu, -RestoreOnly' }))
$rep += ('kopie        : ' + $runDir + $(if ($mirrorDir) { ' + ' + $mirrorDir } else { '' }))
$rep += ('log skryptu  : ' + $script:logFile)
$rep += ('kod wyjscia  : ' + $script:exitCode + ' (0 OK, 1 nie dotarl / wywrotka, 2 stop przed testem, 3 przywracanie nieudane, 4 doby OK - misje NIE sprawdzone)')
$rep += '==============================================================================='
foreach ($l in $rep) { Log $l }
try { $rep | Set-Content -LiteralPath (Join-Path $runDir 'wynik.txt') -Encoding UTF8 } catch { }
exit $script:exitCode
