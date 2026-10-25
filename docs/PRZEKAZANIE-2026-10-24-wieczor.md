# PRZEKAZANIE 24.10.2026 (aktualne 22:30 CEST) - kontynuacja na drugim koncie (limit tygodnia tego konta 100%)

## DOPISEK 25.10 ok. 06:30 CET - WGRANE 113 (nocny pakiet gotowy przed 12:00)
- W grze: Armoury 6b4f188b = galaz noc/final 1e7593e5 (CHANGELOG 203ac63a): 114 marsz i doktryny kultur (+S1 ostrzal, S3 flanka, S4 lucznicy za piechota), 115 choragwie, 116 przeglad armii w obozie, 117 okno lupu (bitwa, oblezenie),
  118 friendly fire (strzaly 100% / oblezenie 50%, wrecz 50%, szarza 50%), 119 dzwiek bitwy, 120 przesluchanie (juz w 112), 121 podatek warsztatow (juz w 112), 122 gotowosc AI (sila z uzbrojeniem, dozbrajanie), 123 szturm murow i bebny oblezenia, 124 domyslne + nagrania.
- Pliki poza DLL w grze: Modules\Armoury\ModuleData\Lektor\Bitwa (379 ogg + manifest.tsv, 15 MB) i Modules\Armoury\GUI\Prefabs\ArmSpoilsWindow.xml. Kopia przed: Armoury.dll.bak-2026-10-25-przed-wgranie113, D:\Backup-Bannerlord\wgrane\2026-10-25-wgranie113-przed.
- Nagrania ElevenLabs: 298 glosow + 81 SFX, 16 glosow z konta Jeffa (bez dodawania z biblioteki - konto 33 glosy przy limicie 30), kredyty 88 120 -> 76 203. Narzedzia tools/lektor/nagraj_bitwa.py, sfx_bitwa.py na galezi noc/p2a (19a871a5); kopia nagran D:\Backup-Bannerlord\nagrania-bitwa-2026-10-25.
- Autotesty 124b/124d: ERROR 0, 4/4 bitwy. NIE pokryte: oblezenie (szturm, ogien, tlum, bebny), okno lupu po oblezeniu, proba P4 i T14/T15 (nie odpalily w 124c - przyczyna nieustalona), sluch i wyglad.
- Do sprawdzenia w logu Jeffa po pierwszej sesji: ERROR 0; "Dzwiek bitwy (noc-p2): misja" (wyciek 0, odrzucenia wedlug grup), "FF (pole|oblezenie)" (udzial FF), "Choragwie (CH) ekonomia" (odlozone == oddane), "Okno lupu (117)", "Gotowosc do bitwy (p9)" linia doby, "Przesluchanie (m109): pochodzenie - banda", D11-3 "w tym gracz zaplacil".
- Decyzje Jeffa otwarte: dubel SiegeAssaultCrowd z ROTBA Contextual Combat Shouts; glosy z biblioteki zamiast narratorow konta (wymaga zwolnienia miejsc); splity RBM a doktryny (Polnoc, Zelazne, Dorne, Lorath); przeglad armii v2 (przypinanie typow, pelne okno); okno lupu po turnieju (v1.1); wyzwiska (v1.1).
- Zapis testowy autotest-przekierowany przeniesiony na D:\Backup-Bannerlord\zapisy-testowe-archiwum.

## DOPISEK 25.10 ok. 04:05 CET - WGRANE 112
- W grze: Armoury 68f74d0f = galaz noc/wgranie112 (a14c76d3 + noc/p7-przesluchanie 443b13f5 + noc/p8-podatek 8be4aeca; CHANGELOG 120, 121). Kopia przed: ...\Armoury.dll.bak-2026-10-25-przed-wgranie112 i D:\Backup-Bannerlord\wgrane6-10-25-wgranie112-przed.
- Autotest 112a: ERROR 0, 4/4 bitwy. Do sprawdzenia w grze Jeffa: "Przesluchanie (m109): pochodzenie - banda", "bez migawki 0", opcja szara; D11-3 "w tym gracz zaplacil X".
- KOLEJNA INTEGRACJA (noc/calosc z workflow noc-bitwa-kod) MUSI scalic noc/wgranie112 i noc/p9-gotowosc (gotowosc AI) + dzwiek szturmu murow i bebny oblezenia (do dopisania).
- Logi sesji Jeffa 02:11-03:46: D:\Backup-Bannerlord\logi-jeffa6-10-25_03-50-po-grze. Zapis testowy = kopia jego zapisu z 03:46 (md5 0ea55e89).

## DOPISEK 25.10 ok. 02:35 CET - NOC W TOKU (workflow noc-bitwa-kod, run wf_d049918d-367)
- Projekty z rozpoznania: scratchpad fffc991e...\scratchpad\noc\projekty\ (ai-marsz-taktyki.json, dzwiek-glosy.json, choragwie.json, przeglad-armii.json, okno-lupu.json, friendly-fire.txt, krytyk.txt - podzial P0-P5, kolizje K1-K17, zakres v1).
- Galezie: noc/p0 (baza: klucze, wpiecia, kontrakty) -> noc/p1-marsz, noc/p2a (nagrania ElevenLabs, budzet 25k znakow + 120 SFX), noc/p2b-dzwiek, noc/p3-choragwie, noc/p4-przeglad, noc/p5-lup, noc/p6-ff -> noc/calosc (scalenie, CHANGELOG 114-119) -> audyt.
  Drzewa: scratchpad\zb\noc-<nazwa>. Baza a14c76d3 (= wgrane 111).
- Potem sesja glowna: autotest bitew (UWAGA zmiana czasu), wgranie DLL + plikow poza DLL (Armoury/ModuleData/Lektor/Bitwa/**, Armoury/GUI/Prefabs/ArmSpoilsWindow.xml) przed 12:00.
- Osobno: diagnoza warsztatu Jeffa ("czemu moj warsztat nie daje kasy") - agent tylko czyta logi z D:\Backup-Bannerlord\logi-jeffa\2026-10-25_01-00-po-grze.

## DOPISEK 25.10 ok. 02:15 CET (po zmianie czasu) - WGRANE 111
- W grze: Armoury 9e301ed6 = galaz w-toku/m117-ubranie b74049c9 (111 ubranie cywilne v3, 112 szyk armii na postoju, 113 kij z domu; CHANGELOG na galezi, a14c76d3). Reszta modow bez zmian
  (CrashScribe 84205371, RC 8528f098, GrandTourney 900e32bc, ForgeView f8c2796e). Kopia przed: Modules\Armoury\bin\...\Armoury.dll.bak-2026-10-25-przed-wgranie111 i D:\Backup-Bannerlord\wgrane\2026-10-25-wgranie111-przed.
- Autotest 117a: 4 bitwy, KONIEC OK, ERROR 0; kij 58/58 potwierdzone tymczasowe; szyk 4.05 -> 0.42-0.50 jedn.; ubrania 0 przypadkow (sprawdzic w grze Jeffa). PULAPKA: zmiana czasu 03:00 -> 02:00 myli wrapper (szuka logu po czasie startu).
- Zapis testowy Game Saves\autotest-przekierowany-IronmanKkUpHftX7ZIq.sav (kopia zapisu Jeffa z 00:59, md5 9a0b4ab9) zostaje na nocne testy - po serii przeniesc na D:.
- NOC (Jeff "start" 02:55): pakiet bitwy sredniowiecznej + przeglad armii + okno lupu + friendly fire + dzwiek (przemowy rodow, motta przy szarzy, jeki rannych po bitwie, konie, szczek broni, deszcz/wiatr,
  ptaki i kruki po bitwie, ogien przy oblezeniu) - gotowe na 25.10 12:00. Rozpoznanie: workflow noc-bitwa-rozpoznanie (5 czesci + krytyk) i osobne rozpoznanie friendly fire.

## DOPISEK 25.10 00:20 (drugie konto)
- Kontrola 110 w sesji Jeffa (start 22:24, Armoury-2026-10-24_22-24-27.log): OK - wojna korona-Dorzecze raz (108869, E7/0 + kruk), sojusz Polnoc+Dorzecze
  (108870, do dnia 586), zwrot Neda 292902 raz (nr=done), Cape Wrath jedno oblezenie (bez petli), Stoneknoll 31 przy powrocie wozu, zloto przy wczytaniu +616,
  ERROR Armoury 0. Jeszcze nie bylo: ekran trofeow (bitwa gracza 22:58 jest - sprawdzic linie m114), chleb (po zapisie i wczytaniu), okno ROT o Dorzeczu (dzien 108).
- m117-ubranie (Jeff 23:05: goli zolnierze maja dostac najtansza tunike i buty swojej kultury): galaz w-toku/m117-ubranie (bb367a94, od 6cd80130, wypchnieta),
  drzewo scratchpad fffc991e...\scratchpad\zb\m117-ubranie, Armoury.dll md5 14e193c7 (tylko Armoury; CrashScribe bez zmian). CHANGELOG 111 na galezi.
  Status: ZBUDOWANE, recenzja 2x (2 uwagi minor poprawione), CZEKA na zamkniecie gry -> autotest bitwy na kopii zapisu Jeffa -> wgranie.
- Pytanie Jeffa o rozjechana armie (Gregor Clegane 1577 przy Mousedown Mill, dzien odpoczynku R3-08a): nasz kod nie rusza pozycji doczepionych; szyk gry
  (Army.GetRelativePositionForParty) stawia druzyny za wodzem do ok. 1.5-2 jedn. i nie dociaga ich na postoju. Czeka na Jeffa: najechac na grupe i ewentualnie
  zgoda na sciaganie figurek na poczatku postoju (tylko obraz, raz przy zmianie stanu).

## NAJWAZNIEJSZE - STAN NA 22:30
- W grze: WGRANIE 110 (Armoury ae412d24; galaz w-toku/m116-fabula 6cd80130, wypchnieta; drzewo scratchpad 6570af7b...\scratchpad\zb\m116-fabula). Reszta modow: CrashScribe 84205371, RC 8528f098, GrandTourney 900e32bc, ForgeView f8c2796e. Lancuch galezi: 107 (n36 d3cada05) -> 108 (n37 9300d259) -> 109 (n38 34f0db38) -> 109.1 (m115-okno e2417cd3) -> 110 (m116-fabula 6cd80130).
- Zapis Jeffa: Game Saves\IronmanKkUpHftX7ZIq.sav (21:03, md5 35d84a9c; kopia D:\Backup-Bannerlord\logi-jeffa\2026-10-24_21-05-po-grze). W Game Saves brak zapisow autotest-* (archiwum D:\Backup-Bannerlord\zapisy-testowe-archiwum, 426 plikow).
- 110 przy pierwszym wczytaniu w kampanii Jeffa: Dorzecze w wojnie z korona, sojusz Polnoc+Dorzecze, zwrot 292 902 Polnocy (raz) - SPRAWDZIC w jego logu (linie 'Wojny kanonu (m104): (4) Dorzecze', 'nr=done:292902', ERROR 0).

## DO ZROBIENIA (kolejnosc)
1. Kontrola 110 w logu Jeffa po jego pierwszej sesji (wyzej) + ekran trofeow ('Ekran trofeow (m114): bitwa ... poza ekranem 0') + wyplaty Stoneknoll przy powrocie wozu.
2. Raport swiata 24.10 (szczegoly nizej): dryf skarbcow koron (36% pieniedzy, cel 22%, bezpiecznik oszczednosci na suficie 3.7), zloto znikajace przy wczytaniu (-43..-84 tys.) i 3 doby z RESZTA ok. -55 tys., martwy front Joffrey-Robb (19 dni bez bitwy; 1 zamek zdobyty w 215 dni), biedne wsie Polnocy i Reach (wsie < 1000 zl 10 -> 51).
3. Mniejsze: ROT wojna #2 EndDay 0 (Dorzecze nie poddaje sie po Krwawych Godach - sprawdzic przy Godach), Beric w Essos, obrazki scen prologu z ROT, nagrody turniejow lokalnych 7-85 zl, Nocna Straz za silna / Dothrakowie za slabi, wywrotka silnika przy wyjsciu (zrzut).
4. Pytanie bez odpowiedzi Jeffa: limit taboru w kolumnie marszowej (MarchPackAllowance 0.25 = 1 zwierze juczne/hodowlane na 4 ludzi; Jeff pytal o -0.62 'Marching column: baggage train') - zaproponowano 1 na 2, czeka na decyzje.

## PULAPKI TESTOW (24.10)
- Wrapper scratchpad\m113\testy-m114.ps1 trzyma zdjecie bazy w m113\zapisy\baza-autotest-przekierowany-*.sav i PRZYWRACA je na starcie, jesli plik w Game Saves jest inny - przed seria na nowszym zapisie Jeffa usunac stare zdjecie (bieg 116a poszedl przez to na zapisie z 02:26).
- Gra testowa wisi przy wyjsciu po 'KONIEC OK' - zamknac PID gry testowej (Bannerlord.BLSE.Standalone), wrapper sam przywraca pliki.
- Katalog DLL z wrappera nie ma CrashScribe.dll - do wgrania dolozyc produkcyjny CrashScribe (84205371) z poprzedniego katalogu.
- Po serii przeniesc autotest-*.sav na D: (NIGDY przy wlaczonej grze).

---
## (dalej: tresc z 21:10)

## Stan gry Jeffa
- W grze: WGRANIE 109.1 (Armoury feb5abdb; galaz w-toku/m115-okno e2417cd3 = w-toku/n38-calosc 34f0db38 + okno inwestycji). Reszta modow jak 106.1 (CrashScribe 84205371, RC 8528f098, GrandTourney 900e32bc, ForgeView f8c2796e).
- Kampania Jeffa: Ironman Documents\Mount and Blade II Bannerlord\Game Saves\IronmanKkUpHftX7ZIq.sav (ostatni zapis 21:03, kopia D:\Backup-Bannerlord\logi-jeffa\2026-10-24_21-05-po-grze).
- 425 zapisow testowych przeniesione na D:\Backup-Bannerlord\zapisy-testowe-archiwum (Game Saves: 105 zapisow, Ithauser tylko jeden). Kopie Ithauser z testow: D:\Backup-Bannerlord\zapisy-testowe-ithauser.
- Jeff skonczyl grac o 21:03. Gra zamknieta.

## Co dzis wgrano (CHANGELOG 107, 108, 109, 109.1)
- 107: walka w bitwach (m113-walka2), pobor we wsiach pobocznych (pula 10%, odrost z przyrostu naturalnego E4/4), kupno ziemi (cena 14-20 lat wedlug relacji), GrowthMaxPercent 1.3 (tez w Armoury.json Jeffa).
- 108: caly lup z bronia na ekranie trofeow (SpoilsSoldierSplitOff - wylaczony ukryty podzial Spoils EnablePrioritySystem), bron ze stanem z ciala (LootWeaponsKeepCondition), dochod prac na zielono (WorksIncomeShown). Potwierdzone w grze: ekran 70/70 i 79/79 sztuk.
- 109: wyplata prac z udzialu dodatku w targu (WorksPayMarketShare; Stoneknoll 1.7 -> 4.5/dobe), komunikat musztry wedlug przyczyny, Lordship i zamek wlaczone po tescie 114a-ziemiaD (T11 Sealbay, T12 Ramsgate TAK).
- 109.1: okno 'Invest in its works' - 5 linii opisu, lista kwot widoczna.

## m116-fabula - WGRANE 22:24 jako 110 (patrz CHANGELOG); ponizej opis zakresu
Jeff 21:00 (wsciekly): "Dorzecze jest w pokoju - co to za bzdura, to bylo pierwsze, co zostalo zaatakowane". Kanon: po pojmaniu Tyriona Gora pali Dorzecze, Jaime bije lordow Dorzecza i oblega Riverrun PRZED egzekucja Neda; potem Polnoc i Dorzecze razem przeciw koronie. U nas plan wpuszczal Dorzecze do wojny ok. 108 dnia opowieci - blad.
Workflow wf_4fdd61ac-eb2 (task waydblgky) - skrypt: C:\Users\GAME\.claude\projects\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\6570af7b-c8f5-4f3e-827c-790a5efcedd4\workflows\scripts\m116-dorzecze-w-wojnie-i-fabula-wf_4fdd61ac-eb2.js (kopia: docs/przekazanie-m116-workflow.js). Worktree (jesli powstal): scratchpad 6570af7b...\scratchpad\zb\m116-fabula, galaz w-toku/m116-fabula od e2417cd3 (w klonie scratchpad\zb\n35-calosc, origin = to repo).
Zakres (jedna paczka):
1. Dorzecze w wojnie z korona od konca prologu (nowe kampanie) + JEDNORAZOWY catch-up w zapisie Jeffa przy wczytaniu: Dorzecze wypowiada wojne Joffreyowi, Polnoc i Dorzecze razem (Diplomacy jesli sie da), kruk; stary termin ~108 ma byc no-op. Sprawdzic cala mape wojen 298-300 z kanonem (Renly/Stannis vs Joffrey itd.); wojny ROT wybrane przez Jeffa (Dolina-Zelazne, Reach-Dorne) zostaja.
2. Okup Neda: blokada wykupu od konca prologu do egzekucji (Prologue.World.cs RansomDailyPrefix / EndCaptivity tylko p01/p10); jednorazowy zwrot 292 902 ze skarbca korony do Polnocy (log [20:06:26] 'Okupy (178): AI-AI - Eddard Stark ... wolny za 292902').
3. Petla oblezenia Cape Wrath (armia Renly'ego zaczyna i zwija oblezenie 60+ razy - linie 'Koniec oblezenia (KO) ... ROT_castle18 ... odejscie z obozu (zmiana celu ...)'): minimum trwania / histereza celu + kruk 'oblezone' raz na (armia, osada).
4. Chleb biedoty (TownFolk.Bread.cs:17 'pamiec sesji') - zapisac w SaveText.Sync.
5. Kruki: smierc Roberta z pustym tekstem ([18:49:27] Kruki (R3-05c)) - tekst; pojmanie Jaimego (arm_canon_moves.tsv p10 bez wiadomosci, przypisane Dorzeczu) - wiadomosc i Robb/Polnoc.
Potem: build, autotest na kopii zapisu Jeffa 2-3 doby (wojna jest, zwrot raz, ERROR 0), wgranie, CHANGELOG 110.

## Raport swiata (24.10 21:00, pelny w odpowiedzi czatu) - do zrobienia pozniej
- Dryf skarbcow koron (36% pieniedzy, cel 22%), bezpiecznik oszczednosci na suficie 3.7 od 108732; wsie < 1000 zl 10 -> 51; Polnoc i Reach biednieja.
- Pieniadze przy wczytaniu: -43..-84 tys. przy 3 z 4 wczytan; 3 dni z RESZTA ok. -55 tys. (108781, 108817, 108844).
- Wojna Joffrey-Robb bez bitew 19 dni; 1 zamek zdobyty w 215 dni (martwy swiat).
- Beric jeniec, potem w Essos; obrazki scen prologu z ROT; nagrody turniejow lokalnych 7-85 zl; Nocna Straz za silna (7900), Dothrakowie za slabi (173).
- Wywrotka silnika przy wyjsciu 0xC0000005 (znana, ERRORS.md) - zrzut pamieci.

## Jak testowac i wgrywac (sprawdzone dzis)
- Kopia zapisu Jeffa do testu: Copy IronmanKkUpHftX7ZIq.sav -> autotest-przekierowany-IronmanKkUpHftX7ZIq.sav (Ironman zapisuje pod ta nazwa); PO SERII przeniesc autotest-*.sav na D: (NIGDY gdy gra dziala - 17:19 crash przy wczytaniu).
- Wrapper: scratchpad 6570af7b...\scratchpad\m113\testy-m114.ps1 (biegi: kopia, bitwa, bitwaB, bitwaOFF, ziemia, ziemiaD, pobor, poborF, wczyt; -Dll <nowy katalog w test\>, -NowyArmoury <dll>, -Tree <drzewo>, -CsAt test\CrashScribe-at-n103.dll, -Prefix, -BezKopii, -PoborDays). Plan: m113\PLAN-TESTOW.md.
- Wgranie: test\wgraj-v5.ps1 -DllDir test\<katalog z 5 DLL> -Tree <drzewo> -Tag 2026-10-xx-wgranieNNN (katalog test = scratchpad 6ba92574...\scratchpad\test). Gry nie moze byc wlaczonej.
- Monitoring gry Jeffa: Monitor na najnowszym Modules\Armoury\Armoury-*.log (ERROR, 'Ekran trofeow (m114): bitwa', 'Ziemia (m113): KUPNO').

## Zasady Jeffa (pamiec)
- Wersje jak najszybciej ("ciagle cos naprawiamy zamiast pograc"): jedna seria testow, potem wgranie; male zmiany wgrywac po zamknieciu gry bez testu ("wgraj jak wylacze gre, na moim logu sprawdzisz").
- Luzne pytania - krotka odpowiedz bez agentow. Liczby z istniejacych modeli moda (nie nowe stale). Jedna regula gracz/AI. Nic nie wgrywac/testowac, gdy gra dziala.
