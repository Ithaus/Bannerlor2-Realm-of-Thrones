# Stan sesji f16095a4 (07.10, ok. 13:10) - koszty w miescie, naprawy, warsztaty

TU = C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\f16095a4-010b-4254-b1b4-9353fc402da7\scratchpad\lawa
LANCUCH = ...\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad\lancuch (klon; galezie glowne/w-toku/* pobrane z repo Jeffa)
Worktree: TU\repo (z LANCUCH). Repo Jeffa C:\Users\GAME\Bannerlor2-Realm-of-Thrones NIETKNIETE. Nic nie wgrane na stale, nic nie wypchniete.
Build: dotnet build <Mod>.csproj -c Release -v q --nologo "-p:GameLibs=C:/Users/GAME/Bannerlor2-Realm-of-Thrones/libs" (sciezka ze slashami!).
Proba: TU\proba\uruchom.ps1 (zmienne PROBA_RC, PROBA_GT = DLL RealisticCaptivity / GrandTourney) - 111/111 na c471705, baza f0aef78 7/7.
Proba kwatermistrza: TU\proba-kwatermistrz (zmienna REGULA_KOWALI 0/1).

## Stos galezi (kazda na poprzedniej), baza f0aef78 = paczka kwatermistrza (w-toku/spoils-kwatermistrz-z-materialem)
- n131b: 81109ab lawa naprawcza (material, bez wrakow) + b36a6d6 robota kowali z dniowek x dobrobyt (naprawy: lawa, kwatermistrz, ludzie, AI)
- n131c: cb21de2 warsztaty (dniowka mistrza wedle tieru, place i utrzymanie x poziom plac; NOWY TownWage.cs) - AUTOTEST 12:15 OK (log Armoury-2026-10-07_12-15-40.log)
- n131d: a9bb933 wynajem kuzni; n131e: 3290b01 budowy; n131f: 14504a5 niewola (RealisticCaptivity); n131g: e010a25 turnieje (GrandTourney)
- n131h: 5b121ea kuznia za kazdy dzien + puste kasy (Niewola); c471705 jedna cena dnia kuzni (3 d x poziom plac, bez tieru) + rezerwa miasta 20 000 przy skupie domu i utargu turnieju
Wpisy CHANGELOG: TU\CHANGELOG-wpis.md (naprawy), CHANGELOG-wpis-2-warsztaty.md, CHANGELOG-wpisy-3-6.md (do dopisania: c471705 - kuznia jedna cena; rezerwa).
Plan: TU\PLAN-KOSZTY-MIASTA-2026-10-07.md. Uwaga przy skladaniu: k13-3 (TownCrafts) placi 3 d bez TownWage - przelaczyc.

## Decyzje Jeffa 07.10 (ta sesja)
- robota kowala zalezna od dobrobytu; wszystkie koszty w miescie ze stawek historycznych x dobrobyt; "wszystko co dotyczy placenia spojne"
- "za kazdy dzien kuznia"; "koszt kuzni to koszt kuzni, a co kuje to moja sprawa" (bez tieru)
- puste kasy - pilnowac wszedzie
- 7a, 7b, 7c - TAK (konie: rekrut konny placi cene konia z targu, hodowca po cenie targu; zwierzeta w cenach historycznych; diagnoza nadmiaru koni)
- NOWE: audyt produkcji: ile czego sie hoduje / wytwarza i gdzie, bez glupot (oliwki na Polnocy), warsztaty w miastach dobrane do okolicznych surowcow
  (wsie z ruda -> cos z ruda w miescie), zgodne z lore (bez oliwek w sniegu)

## W toku (agenci w tle, raporty do TU\raporty\)
- 7a + 7b: implementacja (galaz n131i od c471705)
- 7c: diagnoza nadmiaru koni (tylko odczyt)
- audyt produkcji / warsztatow / lore (tylko odczyt)
- Paczka 8 (zamowienie sprzetu dla ludzi): Jeff "cena tak, wybor tak, brak towarow tak" - agent w worktree TU\repo-zamowienia, galaz n131j od c471705
  (cena z polki + 0.2 dniowki kowala x poziom plac; wybor sposrod sztuk na polce; brak - popyt dla warsztatow); 7a+7b - agent w TU\repo-konie, galaz n131i.
- NA POZNIEJ (Jeff 07.10): kasy miast w tarapatach - dlugi, pozyczki, podatek nadzwyczajny miasta (szkic w PLAN-KOSZTY-MIASTA-2026-10-07.md, sekcja 6).
- Paczka 8 GOTOWA: ce26642 na n131j (129/129); w toku poprawka: wycena bez zdejmowania z polki (ten sam agent, nowy commit na n131j).
- DECYZJA JEFFA 07.10: "dokoncz i wszystko przekaz jemu" = po dokonczeniu paczek (7ab, 7c, audyt, poprawka 8) i przegladzie
  przekazac WSZYSTKO czatowi "Realm of Thrones mody — przegląd stanu prac" (SendMessage do niego) z dokumentem przekazania
  TU\PRZEKAZANIE-DLA-DRUGIEGO-CZATU.md; on sklada paczki i prowadzi STAN-PRAC; ten czat konczy prace.
- 7c GOTOWE: raport TU\raporty\DIAGNOZA-NADMIARU-KONI.md (zrodlo: 71 stadnin ok. 333/dobe + wozy biora caly magazyn + juczne z niczego; odplyw Stajnia 3.2/dobe; poprawki 0-3 + higiena cen) - DO WYKONANIA przez drugi czat (nie zaczete).
- Paczka 8 POPRAWIONA: a645723 na n131j (wycena bez zdejmowania z polki: SupplyDemand.Hold/Release; proba TU\proba-zamowienia 130/130; ceny bez zmian). Wpis: TU\raporty\CHANGELOG-wpis-8-zamowienia.md
- AUDYT PRODUKCJI GOTOWY: TU\raporty\AUDYT-PRODUKCJI-I-WARSZTATOW.md (674 linii; bawelna w zimnych krainach 10 z 13 wsi, 12 wsi wbrew klimatowi; tloczni oliwy w snieg w 62% kampanii przez losowy wybor warsztatow; 24 miasta z kopalniami, kuznia tylko w ok. 55%; 76 z 291 warsztatow bez surowca z wlasnych wsi; blad CrashScribe Mends.cs:2134/2140 - sprawdza zamek wsi zamiast miasta handlowego, dziala po przydziale warsztatow) - poprawki DO WYKONANIA przez drugi czat (nie zaczete; decyzje Jeffa przed zmiana danych swiata - nowa kampania).
- ZASADA JEFFA 07.10 (lore): "trzymajmy sie lore swiata" - smoki TYLKO Daenerys + misja gracza; mamuty tylko Za Murem / Polnoc; wielblady tylko w cieplych
  krainach (w Dorne lore: piaskowe rumaki - do decyzji); jednorozce tylko Skagos. Sprawdzone 07.10: zadna wies nie hoduje smokow, jednorozcow, mamutow, rydwanow,
  sloni; w logach (03:18, 12:15) nie ma ich na targach ani w lupach. Wielblady: Dorne 3/dobe, Lys/Tyrosh/Myr/Volantis 7/dobe. Problem tylko w liczeniu: gra liczy
  "srednia cene konia" (PartiesBuyHorseCampaignBehavior.CalculateAverageHorsePrice) ze WSZYSTKICH wpisow kategorii horse (z ROT: smoki, jednorozce, rydwany, slon,
  mamut) = ok. 11 000 zamiast ok. 1 100 -> lordowie nie kupuja koni. Poprawka (raport koni, "higiena cen"): srednia tylko ze zwyklych wierzchowcow w handlu.
- DECYZJE JEFFA 07.10 do poprawek koni (raport DIAGNOZA-NADMIARU-KONI.md):
  P0 pomiar (linia "Konie:") - TAK.
  P1 hodowla wedle popytu - TAK + "moze stadnin jest za duzo" -> sprawdzic liczbe stadnin (71: europe 20, desert 15, steppe 13, vlandian 10, sturgian 7,
     battanian 6) wobec lore regionow i zbytu; ewentualnie zmienic czesc wsi-stadnin na typy zgodne z regionem (nowa kampania).
  P2 ZMIENIONE: "kon ginie jak ginie, jak nie ginie to nie ginie" - BEZ sztucznego "30% rannych". Kon ginie tylko zabity; ocalaly kon poleglego / pokonanego
     jezdzca = LUP zwyciezcy (takze AI - dzis AI dostaje zloto zamiast przedmiotow, konie jazdy AI sa wirtualne). Bitwy z graczem (misja): liczyc naprawde
     zabite wierzchowce; bitwy AI-AI (symulacja, gra nie liczy koni): regula z proporcji zmierzonych w prawdziwych bitwach. Przegrani odkupuja konie (popyt).
  P3 juczne dla karawan i taborow z targu - TAK ("nie ma nic za darmo, zadnego sztucznego dosypywania").
  Higiena cen (srednia cena konia tylko ze zwyklych wierzchowcow w handlu) - wyjasnione Jeffowi; zalecane.
- 7a+7b GOTOWE: galaz n131i-konie-i-zwierzeta (worktree TU\repo-konie, od c471705): e10b449 (7a HorsesAtMarketPrice: rekrut konny placi cene konia z targu,
  hodowca po cenie targu) + 22336e0 (7b HistLivestockPrices: wol 300->157, krowa 200->113, swinia 60->30, owca 80->17, ges 50->4, kura 50->1; konie bez zmian).
  646 ustawien. Proba TU\proba-konie 135/135. Wpis TU\raporty\CHANGELOG-wpis-7ab-konie-zwierzeta.md. RYZYKA: cena konia przez Town.GetItemPrice (bez regul
  SupplyDemand - niespojne z VolunteerKit); AI mniej zaciaga jazdy, najemnicy z koniem szlachetnym ponad limit gry 5000 (AI ich nie zaciagnie); dochod wsi z
  hodowli spada (owce 320->68 / dobe); dwie nowe latki na prywatnych metodach gry. -> PRZEGLAD przed przekazaniem.
- Galezie n131i i n131j (obie od c471705) koliduja w Settings.cs / McmSettings.cs - po scaleniu uruchomic gen_mcm (spodziewane 646 + 1 = 647).
- PRZEGLAD calosci w toku: workflow wf_bd58f0e5-4ca (5 wymiarow + 3 sceptykow na zgloszenie); skrypt w .claude\projects\...\workflows\scripts\przeglad-stosu-n131-wf_bd58f0e5-4ca.js. Po nim: poprawki potwierdzonych usterek, dokument TU\PRZEKAZANIE-DLA-DRUGIEGO-CZATU.md, SendMessage do 'Realm of Thrones mody — przegląd stanu prac'. Otwarte pytanie do Jeffa: czy lordowie maja trzymac zapasowe konie dla piechoty (wtedy poprawka sredniej ceny konia).
- DECYZJA JEFFA 07.10: "Musza miec zapasowe konie" (+ "zwieksza udzwig, masz konia i juki"). -> (1) poprawka sredniej ceny konia (PartiesBuyHorseCampaignBehavior.
  CalculateAverageHorsePrice tylko ze zwyklych wierzchowcow w handlu) - lordowie kupuja zapasowe do zasady gry (wartosc ok. 8% zlota, max ok. 7 przy 100 tys.);
  (2) udzwig: gra (DefaultInventoryCapacityModel, sprawdzone) liczy czlowiek 20 kg, zapasowy kon wierzchowy 20 kg, juczny/mul 100 kg, kon pod jezdzcem 0.
  Historycznie kon ok. 20-25% wagi (100-120 kg razem): pod jezdzcem prawie pelny (+ok. 10 kg juk), zapasowy z jukami 60-80 kg, juczny 100-120, mul 90-110.
  Propozycja (do wykonania przez drugi czat): zapasowy kon wierzchowy 60 kg, jezdziec +10 kg, juczne bez zmian. UWAGA: MarchPace.cs (Armoury) czyta udzwig
  przy tempie marszu - zmiana przesunie tez tempo; sprawdzic czynny model udzwigu (czy RBM / BK / RealisticBannerlord go nie podmieniaja).
- PRZEGLAD (wf_bd58f0e5-4ca) ZAKONCZONY 07.10 ok. 14:00: 34 zgloszenia, 10 potwierdzonych (6 usterek) - WSZYSTKIE POPRAWIONE:
  n131h: 1fe5c35 (doba kuzni w jednym rejestrze z karnetem BK, w save; placi kazda robota, takze zdalna; karnet bez pieniedzy nie wydawany),
  100112a (ResetSlotCondition zapisuje nowy stan sztuki; apel kwatermistrza = PlanTroops), ad6f6ce (GrandTourney: TournamentCancelled zwraca pule);
  n131i: 7d919ee (Stables.ShelfPrice - jedna cena konia: rekrut, hodowca, lord z polki = notabl i gracz). Proby: 118/118, 136/136, 130/130. Wpis: raporty\CHANGELOG-wpis-9-przeglad-poprawki.md.
- PROBA ZLOZENIA: galaz n131k-sklad-proba (TU\repo-sklad) = ad6f6ce + n131i + n131j, bez konfliktow, gen_mcm bez zmian (647/40/104), build 3 x 0, proby zielone; DLL TU\dll\sklad.
- AUTOTEST CALEGO STOSU 14:18 (at-20261007-141807): OK 40/40 dob, kod 0, DLL Jeffa przywrocone (md5 OK), zapisy Jeffa nietkniete; CrashScribe bledy 8, okna 3, wywrotka silnika przy wyjsciu (znana).
  Analiza logow: workflow wf_283fe6bf-e3d (w toku).
- DECYZJE JEFFA 07.10 ok. 14:25: "nasza robocizna jest lepsza" - robocizna n131 zostaje, projektu cedac4d (koszt naprawy wedle godzin, drugi czat) nie wdrazac w jej miejsce;
  "nie puszczaj zadnych innych prac w drugim czacie, wszystko idzie w jednym" (pamiec jeff-jeden-czat-bez-rownoleglych-prac). Jeff spytal, ktory czat wybrac - zalecilem tamten
  (prowadzi STAN-PRAC i cala reszte); przekazanie po analizie autotestu: TU\PRZEKAZANIE-DLA-DRUGIEGO-CZATU.md.
