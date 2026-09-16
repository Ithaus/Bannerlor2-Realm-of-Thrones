# diag-start-crash.ps1 - diagnostyka crasha przy starcie / wczytaniu zapisu.
# TYLKO CZYTA. Jedyny zapis to plik raportu (domyslnie Documents\...\CrashScribe\diag-start-<data>.txt).
#
# Po co: Claude w zdalnym kontenerze nie widzi dysku C. Ten skrypt zbiera w jednym
# pliku wszystko, czego trzeba do rozstrzygniecia crasha (zadania A+B z 16.09):
#   1. najnowsze raporty crasha BUTR (html/zip) - wyjatek, stacktrace, Involved Modules
#   2. ogon rgl_log_*.txt i dzisiejsze pliki Configs\ModLogs
#   3. ostatni session-*.log CrashScribe, Armoury.log
#   4. LauncherData.xml: kolejnosc, zaznaczone, kopie .bak
#   5. Modules: kazdy zaznaczony mod ma folder? SubModule.xml czyta sie? zaleznosci
#      (DependedModules / DependedModuleMetadatas) sa wlaczone i we wlasciwej kolejnosci?
#      DLL z <SubModules> istnieja?
#   6. skan uszkodzen po wzorcu dysku z 01.09 (plik caly z zer albo ogon z zer od
#      granicy 4096 B): DLL zaznaczonych modow, wszystkie SubModule.xml, Configs\*
#   7. stan CrashScribe.dll: md5 w grze vs md5 w repo + git log/status repo
#   8. dziennik Windows: NTFS (55/98/130/140) i bledy aplikacji od $Since
#
# Uruchamianie (launcher i gra ZAMKNIETE):
#   powershell -ExecutionPolicy Bypass -File tools\diag-start-crash.ps1
#   opcjonalnie: -Since '2026-09-16 00:00' -Game <sciezka gry> -Docs <sciezka Documents\...>
# Wynik wklej/zalacz do rozmowy z Claude.

param(
    [string]$Game   = 'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord',
    [string]$Docs   = (Join-Path $HOME 'Documents\Mount and Blade II Bannerlord'),
    [string]$Repo   = (Split-Path $PSScriptRoot -Parent),
    [string]$Backup = 'D:\Backup-Bannerlord',
    [datetime]$Since = (Get-Date).Date,
    [string]$Out    = ''
)

$ErrorActionPreference = 'Continue'
$stamp = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'
if (-not $Out) { $Out = Join-Path $Docs "CrashScribe\diag-start-$stamp.txt" }

$script:lines = New-Object System.Collections.Generic.List[string]
function W { param([string]$s = '') $script:lines.Add($s); Write-Host $s }
function H { param([string]$t) W ''; W ('=' * 78); W "== $t"; W ('=' * 78) }
function Sekcja { param([string]$title, [scriptblock]$body)
    H $title
    try { & $body } catch { W "  !! BLAD sekcji '$title': $($_.Exception.GetType().Name): $($_.Exception.Message)" }
}
function Fmt-Time { param($t) if ($t) { return ('{0:yyyy-MM-dd HH:mm:ss}' -f $t) } else { return '?' } }

# ---------- pomocnicze: skan zer (wzorzec awarii dysku) ----------
function Test-TailZero {
    # zwraca opis, jesli ogon pliku (do 4096 B) jest samymi zerami albo plik pusty; inaczej $null
    param([string]$p, [int]$n = 4096)
    $fs = [System.IO.File]::Open($p, 'Open', 'Read', 'ReadWrite')
    try {
        $len = $fs.Length
        if ($len -eq 0) { return 'PUSTY (0 B)' }
        $k = [int][Math]::Min($n, $len)
        $buf = New-Object byte[] $k
        $null = $fs.Seek(-$k, 'End'); $null = $fs.Read($buf, 0, $k)
        foreach ($b in $buf) { if ($b -ne 0) { return $null } }
        return "OGON $k B SAMYCH ZER (plik $len B)"
    } finally { $fs.Dispose() }
}
function Test-NulRun {
    # dla malych plikow tekstowych: najdluzszy ciag zer >= $min
    param([string]$p, [int]$min = 16)
    $bytes = [System.IO.File]::ReadAllBytes($p)
    if ($bytes.Length -eq 0) { return 'PUSTY (0 B)' }
    $run = 0; $best = 0; $at = -1
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        if ($bytes[$i] -eq 0) { $run++; if ($run -gt $best) { $best = $run; $at = $i - $run + 1 } } else { $run = 0 }
    }
    if ($best -ge $min) { return "ciag $best zer od bajtu $at (plik $($bytes.Length) B)" }
    return $null
}
function Test-ManagedDll {
    # 'OK <nazwa, wersja>' / 'NATYWNA lub USZKODZONA: <blad>' - bez ladowania do procesu
    param([string]$p)
    try { $an = [System.Reflection.AssemblyName]::GetAssemblyName($p); return "OK $($an.Name) $($an.Version)" }
    catch { return "NIE-ZARZADZANA/USZKODZONA: $($_.Exception.GetType().Name): $($_.Exception.Message)" }
}
function Test-Mz { param([string]$p)
    $fs = [System.IO.File]::Open($p, 'Open', 'Read', 'ReadWrite')
    try { if ($fs.Length -lt 2) { return $false }; $a = $fs.ReadByte(); $b = $fs.ReadByte(); return ($a -eq 0x4D -and $b -eq 0x5A) }
    finally { $fs.Dispose() }
}
function Backup-Info { param([string]$full)
    # czy w mirrorze na D: jest kopia tego pliku (nie kopiujemy - tylko meldujemy)
    if (-not (Test-Path $Backup)) { return '' }
    $rel = $null
    if ($full.StartsWith($Game, [StringComparison]::OrdinalIgnoreCase)) { $rel = Join-Path 'Modules' ($full.Substring((Join-Path $Game 'Modules').Length).TrimStart('\')) }
    elseif ($full.StartsWith($Docs, [StringComparison]::OrdinalIgnoreCase)) { $rel = Join-Path 'Documents' ($full.Substring($Docs.Length).TrimStart('\')) }
    if (-not $rel) { return '' }
    $b = Join-Path $Backup $rel
    if (Test-Path -LiteralPath $b) { $bf = Get-Item -LiteralPath $b; return "  [backup D: $($bf.Length) B, $(Fmt-Time $bf.LastWriteTime)]" }
    return '  [backup D: BRAK]'
}

# ---------- pomocnicze: HTML raportu BUTR -> tekst ----------
function ConvertFrom-HtmlText { param([string]$html)
    $t = [regex]::Replace($html, '(?is)<(script|style)[^>]*>.*?</\1>', '')
    $t = [regex]::Replace($t, '(?i)<br\s*/?>|</(p|div|li|tr|h[1-6]|pre|summary|details|table|ul|ol)>', "`n")
    $t = [regex]::Replace($t, '(?s)<[^>]+>', '')
    $t = [System.Net.WebUtility]::HtmlDecode($t)
    $ls = @()
    foreach ($l in ($t -split "`r?`n")) { $x = $l.Trim(); if ($x -ne '') { $ls += $x } }
    return $ls
}
function Show-FromAnchor { param([string[]]$ls, [string]$pattern, [int]$count, [string]$label)
    $idx = -1
    for ($i = 0; $i -lt $ls.Count; $i++) { if ($ls[$i] -match $pattern) { $idx = $i; break } }
    if ($idx -lt 0) { W "    (kotwica '$label' nie znaleziona)"; return $false }
    W "    --- $label (od linii $idx, do $count linii) ---"
    $end = [Math]::Min($ls.Count - 1, $idx + $count)
    for ($i = $idx; $i -le $end; $i++) { W "    $($ls[$i])" }
    return $true
}
function Show-CrashReport { param([string]$path)
    W ''
    W "--- RAPORT: $path"
    $f = Get-Item -LiteralPath $path
    W "    czas $(Fmt-Time $f.LastWriteTime)  rozmiar $($f.Length) B"
    $html = $null
    if ($f.Extension -ieq '.zip') {
        $tmp = Join-Path $env:TEMP ('blcrash-' + [guid]::NewGuid().ToString('N'))
        Expand-Archive -LiteralPath $path -DestinationPath $tmp -Force
        $inner = Get-ChildItem $tmp -Recurse -File
        W ('    w zipie: ' + (($inner | ForEach-Object { "$($_.Name) ($($_.Length) B)" }) -join ', '))
        $h = $inner | Where-Object { $_.Extension -in '.html', '.htm' } | Select-Object -First 1
        if ($h) { $html = Get-Content -LiteralPath $h.FullName -Raw -Encoding UTF8 }
        else {
            $j = $inner | Where-Object { $_.Extension -in '.json', '.txt', '.log' } | Select-Object -First 3
            foreach ($jf in $j) { W "    --- $($jf.Name) pierwsze 150 linii ---"; Get-Content -LiteralPath $jf.FullName -TotalCount 150 | ForEach-Object { W "    $_" } }
        }
    } else { $html = Get-Content -LiteralPath $path -Raw -Encoding UTF8 }
    if (-not $html) { W '    brak HTML do odczytu'; return }
    $ls = ConvertFrom-HtmlText $html
    W "    linii tekstu po zdjeciu HTML: $($ls.Count)"
    W '    --- naglowek (pierwsze 30 linii) ---'
    for ($i = 0; $i -lt [Math]::Min(30, $ls.Count); $i++) { W "    $($ls[$i])" }
    $a1 = Show-FromAnchor $ls '^(Exception|Exception information|Exception Information)\b' 140 'Exception'
    $a2 = Show-FromAnchor $ls '^Involved Modules' 80 'Involved Modules'
    $a3 = Show-FromAnchor $ls '^Enhanced Stacktrace' 120 'Enhanced Stacktrace'
    if (-not ($a1 -or $a2 -or $a3)) {
        W '    (zaden znany naglowek BUTR - pierwsze 250 linii tekstu)'
        for ($i = 0; $i -lt [Math]::Min(250, $ls.Count); $i++) { W "    $($ls[$i])" }
    }
    W '    --- linie z typami wyjatkow / "at ..." (do 60) ---'
    $hits = $ls | Where-Object { $_ -match 'Exception|^at |Cannot load|BadImage|Could not load|FileNotFound|TypeInitialization|Inner' } | Select-Object -First 60
    foreach ($x in $hits) { W "    $x" }
}

# ================================================================
W "diag-start-crash.ps1  $stamp"
W "Game:   $Game  (istnieje: $(Test-Path $Game))"
W "Docs:   $Docs  (istnieje: $(Test-Path $Docs))"
W "Repo:   $Repo  (istnieje: $(Test-Path $Repo))"
W "Backup: $Backup  (istnieje: $(Test-Path $Backup))"
W "Since:  $(Fmt-Time $Since)"
W "PowerShell $($PSVersionTable.PSVersion)  |  uzytkownik $env:USERNAME"

# ---------- 1. raporty crasha ----------
Sekcja '1. RAPORTY CRASHA BUTR (html/zip) od Since' {
    $roots = @((Join-Path $HOME 'Desktop'), (Join-Path $HOME 'Documents'), (Join-Path $HOME 'Downloads'), $Docs,
               (Join-Path $Game 'bin\Win64_Shipping_Client'), $env:TEMP)
    $found = @()
    foreach ($r in ($roots | Select-Object -Unique)) {
        if (-not (Test-Path $r)) { continue }
        $depth = 3
        try {
            $found += Get-ChildItem -LiteralPath $r -Recurse -Depth $depth -File -ErrorAction SilentlyContinue |
                Where-Object { ($_.Extension -in '.html', '.htm', '.zip') -and $_.LastWriteTime -ge $Since -and
                               ($_.Name -match 'crash|report|butr|bannerlord|blse|\d{4}-\d{2}-\d{2}' -or $_.Extension -in '.html', '.htm') }
        } catch { W "  (pominiete $r : $($_.Exception.Message))" }
    }
    $found = $found | Sort-Object FullName -Unique | Sort-Object LastWriteTime -Descending
    W "znalezione kandydaty: $($found.Count)"
    foreach ($f in ($found | Select-Object -First 15)) { W ("  {0}  {1,10} B  {2}" -f (Fmt-Time $f.LastWriteTime), $f.Length, $f.FullName) }
    foreach ($f in ($found | Select-Object -First 2)) { Show-CrashReport $f.FullName }
    if ($found.Count -eq 0) { W '  BRAK - jesli okno BUTR jeszcze wisi, kliknij "Save Report" i zapisz na Pulpit, potem uruchom ponownie.' }
}

# ---------- 2. rgl_log ----------
Sekcja '2. rgl_log_*.txt (Documents\...\logs) - dwa najnowsze' {
    $ld = Join-Path $Docs 'logs'
    if (-not (Test-Path $ld)) { W "  brak folderu $ld"; return }
    $logs = Get-ChildItem $ld -Filter 'rgl_log_*.txt' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 2
    if (-not $logs) { W '  brak rgl_log_*.txt' }
    foreach ($l in $logs) {
        W ''; W "--- $($l.FullName)  $(Fmt-Time $l.LastWriteTime)  $($l.Length) B"
        $tz = Test-TailZero $l.FullName 512; if ($tz) { W "    !! $tz" }
        $all = Get-Content -LiteralPath $l.FullName -ErrorAction SilentlyContinue
        W "    linii: $($all.Count)"
        W '    --- ostatnie 60 linii ---'
        $all | Select-Object -Last 60 | ForEach-Object { W "    $_" }
        W '    --- linie z error/exception/cannot/fail/missing (ostatnie 30) ---'
        $all | Where-Object { $_ -match 'error|exception|cannot|fail|missing|not found|crash' } | Select-Object -Last 30 | ForEach-Object { W "    $_" }
    }
}

# ---------- 3. ModLogs ----------
Sekcja '3. Configs\ModLogs - pliki zmienione od Since' {
    $md = Join-Path $Docs 'Configs\ModLogs'
    if (-not (Test-Path $md)) { W "  brak folderu $md"; return }
    $fs = Get-ChildItem $md -Recurse -File | Where-Object { $_.LastWriteTime -ge $Since } | Sort-Object LastWriteTime -Descending
    W "plikow od Since: $($fs.Count)"
    foreach ($f in $fs) { W ("  {0}  {1,10} B  {2}" -f (Fmt-Time $f.LastWriteTime), $f.Length, $f.FullName) }
    foreach ($f in ($fs | Select-Object -First 10)) {
        W ''; W "--- $($f.Name) (ostatnie 40 linii)"
        Get-Content -LiteralPath $f.FullName -Tail 40 -ErrorAction SilentlyContinue | ForEach-Object { W "    $_" }
    }
}

# ---------- 4. CrashScribe / Armoury ----------
Sekcja '4. CrashScribe session-*.log (2 najnowsze) + Armoury.log' {
    $cd = Join-Path $Docs 'CrashScribe'
    if (Test-Path $cd) {
        $ss = Get-ChildItem $cd -Filter 'session-*.log' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 2
        foreach ($s in $ss) {
            W ''; W "--- $($s.FullName)  $(Fmt-Time $s.LastWriteTime)  $($s.Length) B"
            $tz = Test-TailZero $s.FullName 512; if ($tz) { W "    !! $tz" }
            $all = Get-Content -LiteralPath $s.FullName -ErrorAction SilentlyContinue
            W '    --- pierwsze 30 linii ---'
            $all | Select-Object -First 30 | ForEach-Object { W "    $_" }
            W '    --- UNHANDLED / DEATH / PIERWOTNY / Mends: latka / Exception (pierwsze 40) ---'
            $all | Where-Object { $_ -match 'UNHANDLED|DEATH|PIERWOTNY|Mends: latka|TypeInitialization|Exception' } | Select-Object -First 40 | ForEach-Object { W "    $_" }
            W '    --- ostatnie 15 linii ---'
            $all | Select-Object -Last 15 | ForEach-Object { W "    $_" }
        }
        if (-not $ss) { W '  brak session-*.log' }
    } else { W "  brak folderu $cd" }
    $al = Join-Path $Game 'Modules\Armoury\Armoury.log'
    if (Test-Path $al) {
        $a = Get-Item $al; W ''; W "--- $al  $(Fmt-Time $a.LastWriteTime)  $($a.Length) B (ostatnie 25 linii)"
        Get-Content -LiteralPath $al -Tail 25 -ErrorAction SilentlyContinue | ForEach-Object { W "    $_" }
    } else { W "  brak $al" }
}

# ---------- 5. LauncherData.xml ----------
$script:order = @()      # lista @{Idx; Id; Sel; Ver}
$script:enabledIds = @()
Sekcja '5. LauncherData.xml - kolejnosc i zaznaczenia' {
    $cfg = Join-Path $Docs 'Configs'
    $ldp = Join-Path $cfg 'LauncherData.xml'
    W '--- kopie w Configs (LauncherData*):'
    Get-ChildItem $cfg -Filter 'LauncherData*' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending |
        ForEach-Object { W ("  {0}  {1,8} B  {2}" -f (Fmt-Time $_.LastWriteTime), $_.Length, $_.Name) }
    if (-not (Test-Path $ldp)) { W "  !! BRAK $ldp"; return }
    $nz = Test-NulRun $ldp 4; if ($nz) { W "  !! LauncherData.xml ma ZERA: $nz" }
    $xml = [xml](Get-Content -LiteralPath $ldp -Raw)
    $node = $xml.SelectSingleNode('//SingleplayerData/ModDatas')
    if (-not $node) { $node = $xml.SelectSingleNode('//ModDatas') }
    if (-not $node) { W '  !! nie znaleziono ModDatas'; return }
    $i = 0
    foreach ($m in $node.SelectNodes('UserModData')) {
        $id = $m.Id; $sel = ($m.IsSelected -eq 'true'); $ver = $m.LastVersionV2; if (-not $ver) { $ver = $m.LastVersion }
        $script:order += @{ Idx = $i; Id = $id; Sel = $sel; Ver = $ver }
        if ($sel) { $script:enabledIds += $id }
        $i++
    }
    W "wpisow: $($script:order.Count), zaznaczonych: $($script:enabledIds.Count)"
    W '--- kolejnosc (# id [x=zaznaczony] wersja):'
    foreach ($o in $script:order) { $mark = ' '; if ($o.Sel) { $mark = 'x' }; W ("  {0,2}. [{1}] {2}  {3}" -f $o.Idx, $mark, $o.Id, $o.Ver) }
}

# ---------- 6. Modules ----------
$script:mods = @{}       # Id -> @{Folder; Path; Ver; Deps=@(); Dlls=@(); Err}
Sekcja '6. Modules - SubModule.xml kazdego folderu (+ workshop, jesli jest)' {
    $dirs = @()
    $mroot = Join-Path $Game 'Modules'
    if (Test-Path $mroot) { $dirs += Get-ChildItem $mroot -Directory }
    $ws = Join-Path (Split-Path (Split-Path $Game -Parent) -Parent) 'workshop\content\261550'
    if (Test-Path $ws) { W "  workshop: $ws"; $dirs += Get-ChildItem $ws -Directory }
    W "folderow modulow: $($dirs.Count)"
    foreach ($d in $dirs) {
        $sm = Join-Path $d.FullName 'SubModule.xml'
        if (-not (Test-Path $sm)) { W "  (bez SubModule.xml) $($d.FullName)"; continue }
        $rec = @{ Folder = $d.Name; Path = $d.FullName; Ver = ''; Deps = @(); Dlls = @(); Err = '' }
        try {
            $nz = Test-NulRun $sm 4; if ($nz) { $rec.Err = "ZERA w SubModule.xml: $nz" }
            $x = [xml](Get-Content -LiteralPath $sm -Raw)
            $mo = $x.Module
            $id = $mo.Id.value; $rec.Ver = $mo.Version.value
            if (-not $id) { W "  !! $($d.Name): SubModule.xml bez <Id>"; continue }
            foreach ($dm in $mo.SelectNodes('DependedModules/DependedModule')) {
                $opt = $false; if ($dm.Optional -and $dm.Optional -ieq 'true') { $opt = $true }
                $rec.Deps += @{ Id = $dm.Id; Order = 'LoadBeforeThis'; Optional = $opt; Src = 'DependedModule'; Ver = $dm.DependentVersion }
            }
            foreach ($dm in $mo.SelectNodes('DependedModuleMetadatas/DependedModuleMetadata')) {
                $opt = $false; if ($dm.optional -and $dm.optional -ieq 'true') { $opt = $true }
                $rec.Deps += @{ Id = $dm.id; Order = $dm.order; Optional = $opt; Src = 'Metadata'; Ver = $dm.version }
            }
            foreach ($s in $mo.SelectNodes('SubModules/SubModule')) { if ($s.DLLName.value) { $rec.Dlls += $s.DLLName.value } }
            if ($script:mods.ContainsKey($id)) { W "  !! DUPLIKAT Id '$id': $($script:mods[$id].Folder) i $($d.Name)" }
            $script:mods[$id] = $rec
        } catch { $rec.Err = "SubModule.xml NIE PARSUJE SIE: $($_.Exception.Message)"; $script:mods["?" + $d.Name] = $rec; W "  !! $($d.Name): $($rec.Err)" }
    }
    W "modulow z Id: $($script:mods.Count)"
    W '--- Id -> folder (wersja) [DLL]:'
    foreach ($k in ($script:mods.Keys | Sort-Object)) { $r = $script:mods[$k]; $e = ''; if ($r.Err) { $e = "  !! $($r.Err)" }; W ("  {0,-34} {1,-34} {2,-10} [{3}]{4}" -f $k, $r.Folder, $r.Ver, ($r.Dlls -join ', '), $e) }
}

# ---------- 7. kontrola zaznaczonych vs Modules ----------
Sekcja '7. KONTROLA: zaznaczone mody vs foldery, zaleznosci, kolejnosc, DLL' {
    if ($script:order.Count -eq 0) { W '  brak danych z LauncherData'; return }
    $pos = @{}; foreach ($o in $script:order) { if ($o.Sel) { $pos[$o.Id] = $o.Idx } }
    $errs = 0; $warns = 0
    foreach ($o in $script:order) {
        if (-not $o.Sel) { continue }
        $id = $o.Id
        if (-not $script:mods.ContainsKey($id)) { W "  BLAD  #$($o.Idx) $id : zaznaczony, ale NIE MA folderu z takim Id w Modules"; $errs++; continue }
        $r = $script:mods[$id]
        if ($r.Err) { W "  BLAD  #$($o.Idx) $id : $($r.Err)"; $errs++ }
        foreach ($dp in $r.Deps) {
            $did = $dp.Id
            if (-not $pos.ContainsKey($did)) {
                if ($dp.Optional) { continue }
                if ($script:mods.ContainsKey($did)) { W "  BLAD  #$($o.Idx) $id wymaga '$did' ($($dp.Src)) - jest w Modules, ale WYLACZONY"; $errs++ }
                else { W "  BLAD  #$($o.Idx) $id wymaga '$did' ($($dp.Src)) - NIE MA go w Modules"; $errs++ }
                continue
            }
            $dpos = $pos[$did]
            if ($dp.Order -eq 'LoadBeforeThis' -and $dpos -gt $o.Idx) { W "  KOLEJ #$($o.Idx) $id : '$did' ma byc PRZED nim, a jest na #$dpos"; $warns++ }
            if ($dp.Order -eq 'LoadAfterThis' -and $dpos -lt $o.Idx) { W "  KOLEJ #$($o.Idx) $id : '$did' ma byc PO nim, a jest na #$dpos"; $warns++ }
            if ($dp.Ver -and $script:mods.ContainsKey($did)) { $have = $script:mods[$did].Ver; if ($have -and ($dp.Ver -ne $have)) { W "  INFO  #$($o.Idx) $id chce '$did' $($dp.Ver), jest $have (sprawdz, czy to problem)" } }
        }
        foreach ($dll in $r.Dlls) {
            $dp1 = Join-Path $r.Path "bin\Win64_Shipping_Client\$dll"
            if (-not (Test-Path -LiteralPath $dp1)) { W "  BLAD  #$($o.Idx) $id : brak DLL $dp1"; $errs++ }
        }
    }
    W ''
    W '--- pozycje kluczowych modow (zaznaczone):'
    $keys = @('Bannerlord.Harmony', 'Bannerlord.ButterLib', 'Bannerlord.UIExtenderEx', 'Bannerlord.MBOptionScreen', 'BLSE*', 'Native', 'SandBoxCore', 'Sandbox', 'StoryMode', 'CustomBattle', 'NavalDLC',
              'ROT-Core', 'ROT-Content', 'ROT_Map', 'ROT-Dragon', 'BannerKings.Redux', 'BKROTPatch', 'ROTFinishNullFix', 'RoyalArmouryFix', 'VoiceActingPatch*', 'AIInfluence', 'ROT_AIInfluence_Compat',
              'CrashScribe', 'Armoury', 'RealisticCaptivity', 'GrandTourney', 'ForgeView')
    foreach ($k in $keys) {
        $m = $script:order | Where-Object { $_.Id -like $k }
        if (-not $m) { W ("  {0,-26} -- nie ma w LauncherData" -f $k); continue }
        foreach ($mm in $m) { $mark = 'WYLACZONY'; if ($mm.Sel) { $mark = "#$($mm.Idx)" }; W ("  {0,-26} {1}" -f $mm.Id, $mark) }
    }
    W ''
    W '--- reguly z 16.09 (ROT przed latkami, Compat po AIInfluence):'
    $rotIdx = @(); foreach ($k in 'ROT-Core', 'ROT-Content', 'ROT_Map', 'ROT-Dragon') { if ($pos.ContainsKey($k)) { $rotIdx += $pos[$k] } }
    $rotMax = -1; if ($rotIdx.Count -gt 0) { $rotMax = ($rotIdx | Measure-Object -Maximum).Maximum }
    W "  ostatni modul ROT na #$rotMax (z $($rotIdx.Count) znalezionych)"
    foreach ($p in ($script:order | Where-Object { $_.Sel -and ($_.Id -like 'ROTFinishNullFix*' -or $_.Id -like 'RoyalArmouryFix*' -or $_.Id -like 'VoiceActingPatch*' -or $_.Id -like 'ROT_AIInfluence_Compat*') })) {
        if ($p.Idx -lt $rotMax) { W "  KOLEJ $($p.Id) #$($p.Idx) jest PRZED ostatnim ROT (#$rotMax)"; $warns++ } else { W "  ok    $($p.Id) #$($p.Idx) po ROT" }
    }
    if ($pos.ContainsKey('AIInfluence') -and $pos.ContainsKey('ROT_AIInfluence_Compat')) {
        if ($pos['ROT_AIInfluence_Compat'] -lt $pos['AIInfluence']) { W "  KOLEJ ROT_AIInfluence_Compat #$($pos['ROT_AIInfluence_Compat']) PRZED AIInfluence #$($pos['AIInfluence'])"; $warns++ } else { W '  ok    ROT_AIInfluence_Compat po AIInfluence' }
    }
    W ''
    W "PODSUMOWANIE kontroli: BLAD=$errs  KOLEJ=$warns"
}

# ---------- 8. skan uszkodzen ----------
Sekcja '8. SKAN USZKODZEN (wzorzec dysku 01.09: plik z zer / ogon z zer)' {
    $bad = 0
    W '--- a) DLL zaznaczonych modow (MZ, ogon, manifest .NET):'
    foreach ($o in $script:order) {
        if (-not $o.Sel -or -not $script:mods.ContainsKey($o.Id)) { continue }
        $r = $script:mods[$o.Id]
        $bin = Join-Path $r.Path 'bin\Win64_Shipping_Client'
        if (-not (Test-Path $bin)) { continue }
        foreach ($f in (Get-ChildItem $bin -File | Where-Object { $_.Extension -ieq '.dll' })) {
            $probs = @()
            if (-not (Test-Mz $f.FullName)) { $probs += 'brak naglowka MZ' }
            $tz = Test-TailZero $f.FullName 4096; if ($tz) { $probs += $tz }
            $mg = Test-ManagedDll $f.FullName
            if ($mg -notlike 'OK*' -and ($r.Dlls -contains $f.Name)) { $probs += $mg }
            if ($probs.Count -gt 0) { $bad++; W "  !! $($f.FullName)  $($f.Length) B  $(Fmt-Time $f.LastWriteTime): $($probs -join '; ')$(Backup-Info $f.FullName)" }
        }
    }
    W '--- b) wszystkie SubModule.xml:'
    foreach ($k in $script:mods.Keys) { $sm = Join-Path $script:mods[$k].Path 'SubModule.xml'; if (Test-Path $sm) { $nz = Test-NulRun $sm 4; if ($nz) { $bad++; W "  !! $sm : $nz$(Backup-Info $sm)" } } }
    W '--- c) Configs\* (pliki < 8 MB, ciag >= 16 zer):'
    $cfg = Join-Path $Docs 'Configs'
    if (Test-Path $cfg) {
        foreach ($f in (Get-ChildItem $cfg -Recurse -File | Where-Object { $_.Length -lt 8MB -and $_.Name -notmatch 'zeroed|uszkodz|corrupt' })) {
            $nz = Test-NulRun $f.FullName 16
            if ($nz) { $bad++; W "  !! $($f.FullName)  $(Fmt-Time $f.LastWriteTime): $nz$(Backup-Info $f.FullName)" }
        }
    }
    W '--- d) pliki w Modules zapisane od Since-3 dni (< 64 MB, ogon 4096 B):'
    $mroot = Join-Path $Game 'Modules'
    $recent = Get-ChildItem $mroot -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $Since.AddDays(-3) -and $_.Length -lt 64MB -and $_.Extension -notin '.log', '.txt' }
    W "  plikow: $(@($recent).Count)"
    foreach ($f in $recent) { $tz = Test-TailZero $f.FullName 4096; if ($tz) { $bad++; W "  !! $($f.FullName)  $(Fmt-Time $f.LastWriteTime): $tz$(Backup-Info $f.FullName)" } }
    W '--- e) found.000 / chkdsk:'
    foreach ($fd in 'C:\found.000', 'C:\found.001') { if (Test-Path $fd) { $c = (Get-ChildItem $fd -Recurse -File -ErrorAction SilentlyContinue | Measure-Object).Count; W "  $fd istnieje: $c plikow" } }
    W ''
    W "PODSUMOWANIE skanu: podejrzanych plikow = $bad"
}

# ---------- 9. CrashScribe.dll i repo ----------
Sekcja '9. CrashScribe.dll: gra vs repo, git log/status' {
    $g = Join-Path $Game 'Modules\CrashScribe\bin\Win64_Shipping_Client\CrashScribe.dll'
    $r = Join-Path $Repo 'CrashScribe\bin\Release\CrashScribe.dll'
    foreach ($p in $g, $r) {
        if (Test-Path $p) { $f = Get-Item $p; $h = (Get-FileHash $p -Algorithm MD5).Hash.ToLower(); W "  $p"; W "      $($f.Length) B  $(Fmt-Time $f.LastWriteTime)  md5 $h  $(Test-ManagedDll $p)" }
        else { W "  BRAK: $p" }
    }
    foreach ($m in 'Armoury', 'RealisticCaptivity', 'GrandTourney', 'ForgeView') {
        $gm = Join-Path $Game "Modules\$m\bin\Win64_Shipping_Client\$m.dll"; $rm = Join-Path $Repo "$m\bin\Release\$m.dll"
        $hg = '-'; $hr = '-'
        if (Test-Path $gm) { $hg = (Get-FileHash $gm -Algorithm MD5).Hash.ToLower() }
        if (Test-Path $rm) { $hr = (Get-FileHash $rm -Algorithm MD5).Hash.ToLower() }
        $same = 'ROZNE'; if ($hg -eq $hr) { $same = 'zgodne' }; W ("  {0,-20} gra {1}  repo {2}  {3}" -f $m, $hg, $hr, $same)
    }
    $git = Get-Command git -ErrorAction SilentlyContinue
    if ($git -and (Test-Path (Join-Path $Repo '.git'))) {
        W '--- git log -6:'; & git -C $Repo log -6 --format='  %h %ad %s' --date=short 2>&1 | ForEach-Object { W "$_" }
        W '--- git status --short (pierwsze 30):'; & git -C $Repo status --short 2>&1 | Select-Object -First 30 | ForEach-Object { W "  $_" }
        W '--- galaz i czy wypchnieta:'; & git -C $Repo status -sb 2>&1 | Select-Object -First 1 | ForEach-Object { W "  $_" }
    } else { W '  git niedostepny albo Repo bez .git' }
}

# ---------- 10. dziennik Windows ----------
Sekcja '10. Dziennik Windows od Since: NTFS (55/98/130/140), bledy aplikacji Bannerlord/.NET' {
    try {
        $ev = Get-WinEvent -FilterHashtable @{ LogName = 'System'; Id = @(55, 98, 130, 140); StartTime = $Since } -MaxEvents 20 -ErrorAction Stop
        foreach ($e in $ev) { W "  SYSTEM $(Fmt-Time $e.TimeCreated) id=$($e.Id) $($e.ProviderName): $(($e.Message -replace '\s+', ' ').Substring(0, [Math]::Min(220, ($e.Message -replace '\s+', ' ').Length)))" }
    } catch { W "  (System/NTFS: $($_.Exception.Message))" }
    try {
        $ev = Get-WinEvent -FilterHashtable @{ LogName = 'Application'; Level = 2; StartTime = $Since } -MaxEvents 200 -ErrorAction Stop |
              Where-Object { $_.Message -match 'Bannerlord|TaleWorlds|BLSE|Launcher|\.NET Runtime' -or $_.ProviderName -match '\.NET Runtime|Application Error' } | Select-Object -First 12
        foreach ($e in $ev) { $m = ($e.Message -replace '\s+', ' '); W "  APP $(Fmt-Time $e.TimeCreated) id=$($e.Id) $($e.ProviderName): $($m.Substring(0, [Math]::Min(400, $m.Length)))" }
        if (-not $ev) { W '  (brak bledow aplikacji pasujacych do Bannerlord/.NET)' }
    } catch { W "  (Application: $($_.Exception.Message))" }
}

# ---------- zapis ----------
W ''
W "KONIEC. Raport: $Out"
try { $null = New-Item -ItemType Directory -Force (Split-Path $Out -Parent); $script:lines | Set-Content -LiteralPath $Out -Encoding UTF8 } catch { Write-Host "!! nie zapisalem raportu: $($_.Exception.Message)" }
