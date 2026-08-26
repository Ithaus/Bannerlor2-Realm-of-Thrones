# DZIENNIK ZMIAN

> **ZASADA OBOWIAZKOWA DLA KAZDEGO CLAUDE (kazde konto, kazdy komputer):**
> po **kazdej** zmianie w kodzie dopisz wpis na GORZE tej listy i zrob commit + push.
> Dzieki temu drugie konto zawsze wie, co sie stalo, bez czytania calej rozmowy.
>
> Format wpisu:
> ```
> ## RRRR-MM-DD — krotki tytul
> **Mod:** nazwa | **Pliki:** sciezki
> **Problem:** co bylo nie tak (objaw u gracza + dowod z logu, jesli byl)
> **Przyczyna:** co konkretnie w kodzie
> **Zmiana:** co zrobiono
> **Ryzyko / co sprawdzic:** na co patrzec w grze i w logu
> **Status:** WGRANE / COFNIETE / DO SPRAWDZENIA
> ```

---

## 2026-08-26 — lista zbroi w CRAFT: zamkniete wzory na czerwono zamiast dopisku "- LOCKED"
**Mody:** ForgeView, Armoury | **Pliki:** `ForgeView/src/ArmourListColour.cs` (NOWY), `Armoury/src/BkArmourList.cs`, `docs/PLAN-kuznia-1do1.md`
**Problem:** zamkniete wzory na liscie zbroi mialy tekstowy dopisek "- LOCKED" - malo czytelne,
Jeff chcial kolor jak przy zablokowanych czesciach broni. Zadanie czekalo "na Windows",
bo prefab BK jest w plikach modulu, nie w DLL.
**Przyczyna:** wiersz nazwy renderuje prefab BannerKings ArmorCraftingCategory.xml - znaleziony
w ZRODLACH BK na githubie (R-Vaccari/bannerlord-banner-kings): RichTextWidget z Text="@ItemName"
w ItemTemplate listy SmeltableItemList. Semantyka Replace i moment wolania gettera Nodes
potwierdzone w zrodlach UIExtenderEx (PrefabComponent: XPath przez SelectSingleNode, przy chybieniu
blad na ekranie i prefab NIETKNIETY; getter Nodes biegnie dopiero PO trafieniu XPath).
**Zmiana:** (1) ForgeView.ArmourListColour - PrefabExtension na prefab "ArmorCraftingCategory",
XPath descendant::*[@Text='@ItemName'] (celuje w wiazanie, nie strukture - odporniejsze na roznice
w BK.Redux), InsertType Replace: wezel nazwy podmieniony na dwa warianty przelaczane IsVisible -
normalny @FvKnown i czerwony #D65252FF @FvLocked (mixin ArmourItemMixin juz te pola ma). ZADNEGO
SetAttribute - ten wieszal ekran (lekcja z MaterialRowExtension). (2) BkArmourList.NamePostfix:
dopisek "- LOCKED" schodzi TYLKO gdy flaga ArmourListColour.Applied (przez reflection) mowi,
ze latka realnie weszla; inaczej dopisek zostaje jako zapas.
**Ryzyko / co sprawdzic:** w CRAFT wiersze nieodkrytych wzorow czerwone, znanych normalne, bez
dopisku "- LOCKED"; przy wejsciu w zakladke log ForgeView "ArmourListColour: wiersze listy zbroi
dostaly warianty known/locked". Jesli Redux ma inny prefab: czerwony komunikat UIExtenderEx
"Failed to apply extension to ArmorCraftingCategory" i stary dopisek dalej widoczny (nic nie znika).
Kolor odswieza sie przy przebudowie listy - wzor odkryty w trakcie sesji moze zostac czerwony
do ponownego wejscia w zakladke (dopisek zachowywal sie tak samo). NIEZBUDOWANE - pisane
w kontenerze bez bibliotek gry; przed wgraniem `./build.sh` na maszynie z libs.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — kryjowka: realny okrzyk alarmu (audio) + halas 0,2 m/kg
**Mod:** Armoury | **Pliki:** `src/HideoutAlarm.cs`, `src/Settings.cs`
**Zmiany:** (1) sztafeta alarmu jest SLYSZALNA: jeden czlowiek na kazdy krag fali wydaje
prawdziwy okrzyk bojowy gry (Agent.MakeVoice, VoiceType.Yell) - lancuch pojedynczych
wrzaskow niesie sie przez oboz, celowo nie chor wszystkich naraz (lekcja z BrokenMen);
wylacznik Hideout Alarm Voice (ON). (2) Hideout Noise Per Armor Kg z 0,4 na 0,2 m/kg
(decyzja Jeffa) - skora ~21,6 m noca, plyta ~26 m.
**Ryzyko / co sprawdzic:** przy alarmie slychac okrzyki kolejnych ludzi; zadnego chóru.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — kryjowka: sztafeta krzyku, zaniepokojenie zamiast alarmu, halas wg wagi pancerza
**Mod:** Armoury | **Pliki:** `src/HideoutAlarm.cs`, `src/Settings.cs`
**Zmiany (trzy zamowienia Jeffa):**
1. SZTAFETA KRZYKU: obudzony zbojca TEZ krzyczy - alarm skacze od czlowieka do czlowieka
   (kazdy kolejny musi stac w zasiegu krzyku poprzedniego), wiec niesie sie przez oboz
   lancuchem, ale nigdy nie przeskakuje pustki wiekszej niz jeden krzyk. BFS falami,
   bezpiecznik 64 kregi. Przy cichej likwidacji swiadek przy ciele (12 m) krzyczy juz
   PELNYM glosem i sam uruchamia sztafete. Wylacznik Hideout Alarm Relay (ON).
2. ZANIEPOKOJENIE: kroki biegnacego NIE stawiaja juz obozu na nogi - patrolujacy w zasiegu
   sluchu przechodzi w stan Cautious (bron w dloni, rozglada sie, prowadzi go silnik);
   pelny alarm robi dopiero walka albo to, co zobaczy natywnym wzrokiem.
3. HALAS WG PANCERZA: do promienia slyszalnosci biegu dochodzi waga noszonego pancerza
   x Hideout Noise Per Armor Kg (0,4 m/kg; GetTotalWeightOfArmor jak w FieldCraft) -
   zwiadowca w skorze biega niemal cicho, rycerz w plycie dudni na pol obozu.
**Ryzyko / co sprawdzic:** bieg przy obozie -> zbojcy TYLKO czujni (szukaja, nie szarzuja);
strzal niecelny -> lancuch alarmu poprzez stojacych blisko siebie (log "N zbojcow w M
kregach"); cichy strzal bez swiadkow -> dalej cisza; ciezki pancerz -> budzisz z daleka.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — mijane obozy z namiotem od reki, natury band, sluch w kryjowce, kolor wzoru w CRAFT
**Mody:** Armoury, ForgeView | **Pliki:** `Armoury/src/NightRest.cs`, `Armoury/src/HideoutAlarm.cs`, `Armoury/src/Settings.cs`, `ForgeView/src/ArmourItemMixin.cs`, `ForgeView/src/TableauExtension.cs`
**Zmiany:**
1. MIJANE OBOZY (Jeff: "stoi konik, ma byc namiot"): przydzial namiotow szedl raz na
   godzine GRY - podjezdzajac do spiacego widziales figurke. Teraz lista spiacych (_camping)
   + RefreshNearbyTents co ~2 s REALNE z OnTick: namioty wchodza, gdy podjezdzasz, schodza
   gdy odjezdzasz albo partia sie budzi (Ai.IsDisabled jako sygnal snu). Limit AiTentCap
   bez zmian, wizerunki ruszane tylko przy zmianie stanu.
2. NATURY BAND (Jeff: "niektorzy poluja w dzien, inni w nocy"): stala natura z Id bandy -
   3/4 to nocni lowcy (leza 10-16, chodza noca), 1/4 dzienni (poluja w slonce, leza 23-5);
   o swicie i wieczorem wszyscy na nogach. Pogon/ucieczka/wrogi lord ucina drzemke.
3. SLUCH W KRYJOWCE (Jeff: "chodzilo mi o hideout, nie mape"): wzrok AI w misji jest
   natywny, ale SLUCH dokladamy: biegnacy czlowiek (predkosc > 3,5 m/s) budzi zbojcow
   w promieniu Hideout Hear Night (20 m) noca / Hear Day (10 m) za dnia - chod na
   Left Ctrl (WalkKey) jest cichy, wiec skradanie dziala. Tick co 1 s.
4. KOLOR WZORU W CRAFT (Jeff: "locked malo czytelne, ma byc jak przy broniach"):
   pod podgladem 3D w zakladce CRAFT stoi teraz czerwone "PATTERN NOT LEARNED" albo
   zielone "PATTERN KNOWN" (Brush.FontColor, wlasny widget ForgeView). Kolorowanie
   SAMYCH WIERSZY listy wymaga plikow prefab BK, ktore sa TYLKO na maszynie z gra -
   ZADANIE DLA CLAUDE NA WINDOWS: patrz docs/PLAN-kuznia-1do1.md.
**Ryzyko / co sprawdzic:** podjazd noca pod spiacego lorda - namiot w ~2 s; bandyci
w dzien rzadsi, noca gestsi; w kryjowce bieg budzi (log HideoutAlarm), chod nie;
zakladka CRAFT - kolorowy stan wzoru pod modelem 3D.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — paczka uwag Jeffa: wzrok dzien/noc, namioty przy graczu, bandyci noca, brama DTE w kryjowce, wspolne etykiety czasu
**Mod:** Armoury | **Pliki:** `src/SightRange.cs` (NOWY), `src/HideoutSpawnShim.cs` (NOWY), `src/NightRest.cs`, `src/Project.cs`, `src/ArmouryBehavior.cs`, `src/SmithMenu.cs`, `src/SubModuleMain.cs`, `src/Settings.cs`
**Zmiany:**
1. WZROK ZA SLONCEM: zasieg wykrywania partii za dnia x1,15, w nocy x0,65 (lagodnie,
   bo w nocy slychac wiecej); postfix na GetPartySpottingRange we wszystkich modelach.
2. WEDRUJACY NAMIOT naprawiony: straznik CO KLATKE zdejmuje wizerunek namiotu gracza,
   gdy partia sie ruszy (stary kod gasil tylko flage raz na godzine - namiot jechal po mapie).
3. NAMIOTY TYLKO WOKOL GRACZA: ikona namiotu AI tylko w promieniu Ai Tent Radius (35)
   od gracza; po odjezdzie obrazki schodza (partie dalej spia). Limit AiTentCap bez zmian.
4. NIE KAZDY OBOZUJE: Ai Camp Skip Percent (15%) kolumn maszeruje przez cala noc
   (deterministycznie per partia i noc); pogon/ucieczka/wrog w poblizu jak dotad nie spia.
5. BANDYCI NOCNI LOWCY (Bandits Rest By Day, ON): w dzien 10-16 wieksza czesc band lezy
   w ukryciu (AI stoi, bez namiotow), cwierc poluje mimo slonca; noca normalnie - razem
   z krotszym wzrokiem nocnym robi sie ich pora.
6. BRAMA DTE W KRYJOWCE (Hideout Armoury Gear, ON): patch spawnu DTE wymaga
   IMissionAgentSpawnLogic, ktorego zwykla kryjowka nie ma (zasadzke wyjatkowali) - stad
   wojsko szlo we wzorcowym rynsztunku zamiast magazynowego. HideoutSpawnShim implementuje
   interfejs pusto (DTE uzywa go TYLKO jako testu obecnosci, silnik w kryjowce nie wola) -
   przydzial z magazynu dziala jak w polu. Log "HideoutSpawnShim: brama DTE otwarta".
7. JEDNA ETYKIETA CZASU (Jeff: "raz 2,5 dnia, raz 7,5 godziny"): Project.TimeLabel -
   ponizej 2 dob godziny, wyzej dni+godziny; uzyta w tempie kucia, komunikacie wykonczenia
   broni, banerze i podsumowaniu projektow (z dopiskiem, ze zegar tyka tylko w tej osadzie).
**Ryzyko / co sprawdzic:** kryjowka - wojsko w sprzecie z magazynu (i log bramy); namiot
gracza znika w chwili ruszenia; w nocy krotszy zasieg wykrywania (tooltip predkosci
"Darkness"); bandyci rzadsi za dnia; obce namioty widac tylko blisko gracza.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — znikajacy miecz: dostawa broni BEZ drugiego rzutu + glosne banery
**Mod:** Armoury | **Pliki:** `src/Project.cs`, `src/ArmouryBehavior.cs`, `src/Forge.cs`
**Problem (Jeff):** wykul miecz w vanilla kuzni, gra napisala "dodano do ekwipunku",
po czym miecz zniknal i po czasie nie wrocil. Do tego wynik wielodniowej roboty byl
cicha linijka na czacie.
**Przyczyna:** OnNewItemCrafted zabiera wyrob na "wykonczenie" (tier x WeaponDaysPerTier dni)
i oddaje przez Forge.Finish - ktore rzuca DRUGI RAZ na porazke wedle NASZYCH wymogow skilla
(vanilla juz osadzila sukces i jakosc przy kowadle). Podwojny osad: przy pechu miecz "pekal
przy hartowaniu" mimo udanego kucia, a wylosowana przez vanilla jakosc i tak przepadala.
Projekt do tego stoi, gdy gracz wyjedzie z osady - wyglada na "minal czas i nic".
**Zmiana:** projekty maja rodzaj: "van" (bron z vanilla kucia) = DOSTAWA przez nowe
Forge.Deliver - bez rzutu, z TA SAMA jakoscia (modyfikator jedzie w zapisie projektu,
pola 5-6, stare zapisy kompatybilne). Zabranie wyrobu krzyczy banerem na srodku ekranu
(ile dni i w KTORYM miescie + "Stay or return to collect it"), dostawa i wyniki projektow
naszej kuzni (sukces/pekniecie) tez ida banerem.
**Ryzyko / co sprawdzic:** wykuty miecz ma wrocic po N dniach POBYTU w tym samym miescie,
z jakoscia z momentu kucia; baner przy zabraniu i przy dostawie. Stare projekty z zapisu
(sprzed tej zmiany) nie maja znacznika "van" i przejda jeszcze stara sciezka z rzutem.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — kuznia 1:1 kroki 2-3: nauka za przetop, pancerze i luki w zakladce Smelt + przeglad sprzetu wojska
**Mod:** Armoury | **Pliki:** `src/SmeltTab.cs` (NOWY), `src/RangedLore.cs`, `src/Forge.cs`, `src/SmithMenu.cs`, `src/SubModuleMain.cs`
**Zmiana (krok 2):** przetop uczy wzorow (Study, pol stawki kucia); odblokowanie wzoru
pokazuje vanillowy baner (MBInformationManager.AddQuickInformation) obok zielonej linijki.
**Zmiana (krok 3):** zakladka Smelt ekranu kuzni przyjmuje metalowe pancerze/tarcze i
luki/kusze: postfix SmeltingVM.RefreshList (doklada pozycje tym samym SmeltingItemVM,
delegaty do prywatnych callbackow), postfix GetSmeltingOutputForItem we wszystkich modelach
SmithingModel (wyjscie z naszej receptury SmeltYield), prefix DoSmelting dla przedmiotow
bez WeaponDesign (oryginal walil w item.WeaponDesign.Template = NullReference; nasza
sciezka: materialy, XP, stamina, nauka wzorow). Stary tygiel w menu miasta ZOSTAJE.
**Zmiana (przeglad):** nowa opcja "Muster the men's kit" w The mending bench (Jeff: "gdzie
sprawdze srednia jakosc sprzetu wojska") - per typ: sztuki na stanie / potrzebne, braki,
sredni tier, sredni stan %, ile zuzytych; na dole rachunek naprawy calosci z rabatem.
**Ryzyko / co sprawdzic:** zakladka Smelt - pancerz na liscie, wyjscie materialow sie
zgadza, przetop nie crashuje (NullReference by byl bez prefixa); baner przy odblokowaniu;
przeglad kwatermistrza pokazuje sensowne srednie. UWAGA: BK/RBM moga podmieniac SmithingModel
- patch lapie wszystkie pochodne (log "wyjscie w N modelach").
**Status:** DO SPRAWDZENIA

## 2026-08-26 — kuznia 1:1 krok 1: jakosc pancerzy ta sama formula co bron
**Mod:** Armoury | **Pliki:** `src/Forge.cs`, `src/Settings.cs`
**Problem:** jakosc naszych wyrobow (pancerze, luki) chodzila po wlasnej krzywej
(Shoddy*/Jackpot), innej niz vanilla kucie broni - Jeff kazal zrownac 1:1
(docs/PLAN-kuznia-1do1.md).
**Zmiana:** RollQuality to teraz wierna kopia DefaultSmithingModel.GetCraftedWeaponModifier:
ExplainedNumber(-trudnosc) + bonus Smithing (SkillHelper/DefaultSkillEffects, limit +-300),
sigmoidy k=0,018 (Poor 0,36/Inferior 0,45/Common/Fine 0,36/Masterwork 0,27/Legendary 0,18
przy progach -70/-55/25/40/70/115), normalizacja, perki Experienced/Master/Legendary Smith
przesuwaja szanse jak w oryginale (Legendary + (skill-275)/5 x 1%), sufit od tieru wyrobu
(1-3 max Fine, 4 max Masterwork, 5-6 bez limitu; vanillowy AdjustQualityRegardingDesignTier
liczony z tieru WYROBU zamiast sredniej czesci), na koncu GetModifiersBasedOnQuality na
grupie modyfikatorow przedmiotu. Tempo (nasze): z dbaloscia +20 do wyniku, w pospiechu ~-17.
Ustawienia ShoddyChanceAtZeroMargin, ShoddyMarginRange, JackpotChance,
LegendaryPerkJackpotBonus USUNIETE (stare XML-y je zignoruja).
**Ryzyko / co sprawdzic:** rozklad jakosci wykutych pancerzy ma odpowiadac broni przy tym
samym marginesie skilla; przy niskim skillu pojawiaja sie Battered/Rusty (odpowiedniki Poor/
Inferior), przy wysokim Fine/Masterwork/Legendary wedle tieru.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — alarm w kryjowce: walka budzi oboz, cichy strzal nie
**Mod:** Armoury | **Pliki:** `src/HideoutAlarm.cs` (NOWY), `src/SubModuleMain.cs`, `src/Settings.cs`
**Problem:** w kryjowce zbojcy 10 m od bijatyki udaja, ze nic sie nie dzieje - vanilla budzi
tylko zaczepiona grupke.
**Zmiana:** nowy MissionBehavior (tylko misje z HideoutMissionController): kazde wrogie
trafienie, ktore NIE zabija, budzi wrogow w promieniu Scream Radius (40 m) od ofiary
(ranny krzyczy); zabojstwo JEDNYM ciosem budzi tylko swiadkow w Witness Radius (12 m)
od ciala - bez swiadkow oboz spi (cicha likwidacja lukiem dziala). Ci 200 m dalej nie
slysza nic. Budzenie publicznym Agent.SetAlarmState(Alarmed) - ten sam stan co vanilla;
lancuch niesie sie sam (obudzeni dobiegaja, walka przy nich budzi nastepnych). 3 ustawienia
MCM, Hideout Alarm Enabled domyslnie ON.
**Ryzyko / co sprawdzic:** wejsc do kryjowki, strzelic w goscia przy grupce - grupka ma
ruszyc (log "HideoutAlarm: walka obudzila N"); zabic samotnego jedna strzala - cisza.
UWAGA: mod Blackmamba's Hideout Overhaul rusza te same mechanizmy - nie laczyc bez testu.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — ranni przestaja zapetlac krzyk (BrokenMen z odstepem prob)
**Mod:** Armoury | **Pliki:** `src/BrokenMen.cs`
**Problem:** zolnierz ponizej progu ucieczki stal w miejscu i krzyczal ZAPETLONYM, nakladajacym
sie na siebie glosem ("jakby ktos puszczal kilka nagran naraz nie czekajac konca") i czasem
wcale sie nie wycofywal.
**Przyczyna:** model morale (RBM) potrafi cofnac Retreat (StopRetreating zeruje flagi), a nasz
kod pchal czlowieka od nowa CO POL SEKUNDY - kazde pchniecie odpalalo wrzask od poczatku,
a czlowiek szarpany tam i nazad stal w miejscu.
**Zmiana:** proby z rosnacym odstepem: Panic, potem Retreat po 3 s, 6 s, 9 s; po trzech
nieudanych 20 s spokoju (albo odejdzie, albo morale go pozbieralo i walczy dalej). Zaden
krzyk nie nachodzi na poprzedni. Slowniki czyszczone w OnAgentDeleted.
**Ryzyko / co sprawdzic:** ranni schodza z pola plynnie, krzyk pojedynczy; przy RBM patrzec,
czy ktos nie zostaje w wiecznym tam-i-nazad (wtedy zwiekszyc odstepy).
**Status:** DO SPRAWDZENIA

## 2026-08-26 — oboz i sen: namiot przy spaniu, rozliczenie od reki, bandyci za przelacznikiem, bitwa w obozie (eksperyment)
**Mod:** Armoury | **Pliki:** `src/NightRest.cs`, `src/CampScene.cs` (NOWY), `src/SubModuleMain.cs`, `src/Settings.cs`
**Problem:** (1) ikona gracza nie zmieniala sie w namiot przy spaniu ("kiedys dzialalo") -
namiot stawal tylko przy wlasnym obozie z klawisza O, a jedna wywrotka Tent() gasila namioty
CALEMU swiatu (_tentBroken); (2) splata dlugu snu czekala do przeliczenia o 6:00, nawet gdy
wojsko dospalo w poludnie; (3) ustawienia Ai Bandits Camp Too i Ai Tent Cap byly MARTWE
(bandyci nigdy nie obozowali, limit ikon nieegzekwowany); (4) napasc na oboz = bitwa na
golym polu.
**Zmiana:** (1) wejscie w sen (arm_sleep_wait) stawia namiot, pobudka zwija (chyba ze stoi
wlasny oboz - wtedy zwija go Break camp); _tentBroken zastapiony licznikiem 3 potkniec
Z RZEDU (sukces zeruje) wedle zasady "licz potkniecia, nie gas funkcji". (2) dlug snu
schodzi NATYCHMIAST, gdy uzbieraja sie godziny (flaga _credited, swit nie liczy drugi raz;
zapis 3-polowy, stary 2-polowy wczytuje sie dalej). (3) bandyci obozuja TYLKO gdy Ai Bandits
Camp Too = ON (domyslnie OFF - ich hurtowe wlaczenie polozylo gre 25.08); limit ikon
Ai Tent Cap (40) egzekwowany, reszta spi bez obrazka. (4) NOWE Camp Battle Props (EKSPERYMENT,
domyslnie OFF): napadnieci W OBOZIE dostaja scenografie obozu na polu bitwy (3 namioty
polkolem za graczem, ognisko, 4 pochodnie, plot) mechanizmem z open-source Homesteads
(GameEntity.Instantiate); nazwy prefabow to kandydaci - brakujace pomijane z logiem
"CampScene: brak prefabu X". Do tego NightRest.PlayerCamped sledzi stan obozu gracza
(takze obozu BK; ruch zeruje).
**Ryzyko / co sprawdzic:** namiot pojawia sie przy Bed down i znika po pobudce; komunikat
splaty dlugu przychodzi w chwili dospania; po wlaczeniu Camp Battle Props w MCM dac sie
napasc w obozie i przyslac linie "CampScene:" z logu (czyscimy liste prefabow po tescie);
z wlaczonymi bandytami patrzec na wydajnosc nocy i crashe menu.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — goli zolnierze w kryjowce: DressCode dopelnia KAZDY pusty slot
**Mod:** Armoury | **Pliki:** `src/DressCode.cs`
**Problem:** zolnierze gracza stoja nadzy podczas walki w kryjowce rozbojnikow (w polu OK).
**Przyczyna (ze ZRODEL DTE, github tategotoazarasi/Bannerlord.DynamicTroop):** (1) DTE
druzynie gracza CELOWO nie dopelnia pustych slotow (kara underequipped) - zolnierz dostaje
tylko to, co magazyn ma; (2) awaryjne ubranie DTE, ktore ratuje w polu, jest w kryjowkach
WYLACZONE; (3) nasz stary DressCode odpuszczal, gdy byla CHOC JEDNA czesc pancerza (same
buty = "ubrany"), i modyfikowal wspoldzielony Equipment przydzialu DTE, co moglo falszowac
zdejmowanie sztuk z polek magazynu w postfixie DTE.
**Zmiana:** DressCode dopelnia kazdy pusty slot pancerza Z OSOBNA (glowa/korpus/nogi/rece/
peleryna) ubraniem ze wzorca oddzialu, na KLONIE ekwipunku - przydzial DTE nietkniety,
rozliczenie magazynu bez zmian. Prefix dalej Priority.Last (po prefixie DTE).
**Ryzyko / co sprawdzic:** kryjowka - nikt nagi; w logu linie "DressCode: ... pustych slotow
pancerza"; po bitwie polowej stan magazynu DTE bez dziwnych ubytkow. Kara morale
underequipped DTE dalej dziala (to tylko ubranie, nie pancerz z magazynu).
**Status:** DO SPRAWDZENIA

## 2026-08-26 — liczniki surowcow i staminy w ekranie kuzni odswiezaja sie na zywo
**Mod:** Armoury | **Pliki:** `src/FletchForge.cs`
**Problem:** po wykuciu (np. luku) w zakladce CRAFT panel dolny dalej pokazywal stara ilosc
drewna/zelaza/wegla i stara stamine - trzeba bylo sie przeklikac miedzy zakladkami.
**Przyczyna:** vanilla przelicza panel (CraftingVM.UpdateAll: materialy, stamina, skille,
dostepnosc przycisku) tylko po WLASNYCH akcjach; nasze Forge.Smith zdejmuje materialy wprost
z sakw, wiec ekran nie wiedzial. Stary refresh w prefixie robil tylko stamine bohatera i
OnRefresh mixina BK - za malo.
**Zmiana:** RefreshCraftScreen wolany z postfixa ExecuteMainActionBK (postfix biegnie TAKZE
gdy prefix przejal robote strzelecka, i przy pancerzach BK): CraftingVM.UpdateAll przez
reflection + OnRefresh mixina. Podwojny refresh z prefixa usuniety.
**Ryzyko / co sprawdzic:** po wykuciu luku licznik drewna/zelaza i stamina maja zejsc OD RAZU;
to samo po pancerzu z zakladki BK. UpdateAll jest prywatne - jesli nazwa sie kiedys zmieni,
Traverse zwroci null i zostanie stary objaw (bez crasha).
**Status:** DO SPRAWDZENIA

## 2026-08-26 — tansza naprawa wojska: wrak max 10% wartosci + rabat hurtowy
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`, `src/Settings.cs`
**Problem:** Jeff po obejrzeniu przykladow: wrak (1%) placony ~12,5% wartosci to za drogo,
i ma byc "jeszcze taniej jesli hurtowo naprawiamy".
**Przyczyna:** stara stawka liczona jako polowa ceny naprawy wlasnej sztuki.
**Zmiana:** cena sztuki wojska liczy sie teraz WPROST od wartosci: wrak (1%) = Troop Mend
Wreck Share (10%) wartosci, lzejsze zuzycie liniowo mniej (stan 60% = 4% wartosci). Do tego
rabat hurtowy z calej roboty: kazda sztuka -0,5% z rachunku (Troop Mend Bulk Discount PP),
pulap 30% (Max). Rabat w podpowiedzi i przy placeniu liczony z tej samej liczby sztuk.
Ustawienie Troop Mend Cost Factor USUNIETE (zastapione przez Wreck Share); stare XML-e je
zignoruja.
**Ryzyko / co sprawdzic:** kwoty w podpowiedzi (np. kolczuga 2000 przy 3%: bylo 242, ma byc
~194 minus rabat); przy pelnych polkach rabat do 30%.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — porzadek w menu kuzni: naprawy pod jednym wejsciem
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`
**Problem:** menu Work the forge uroslo do 12 pozycji, z czego 5 to rozne odmiany naprawy -
nie bylo widac, ktora do czego (Jeff: "sprawdz czy opcje maja sens i czy czegos nie wywalic").
**Przyczyna:** kazda nowa funkcja dokladala wlasny wiersz do glownego menu.
**Zmiana:** nowe podmenu "The mending bench - your gear and the men's" (armoury_mend), a w nim:
wybor sztuki, naprawa calego rynsztunku wlasnorecznie / u kowala, lupy z sakw hurtem, sprzet
wojska. Podpowiedz wejscia od razu liczy: ile zuzytych na grzbiecie / w sakwach / na polkach
wojska. Glowne menu ma teraz 8 pozycji. Nic nie wylecialo - wszystkie funkcje zostaly,
zmienilo sie tylko ulozenie. Po skonczonej robocie wracasz do glownego menu kuzni.
**Ryzyko / co sprawdzic:** wejscie i wyjscie z podmenu (SwitchToMenu miedzy zwyklymi menu,
NIE z menu oczekiwania - pulapka CTD nie dotyczy); wszystkie 5 opcji dziala jak przedtem.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — zamawianie brakujacego sprzetu wojska u kowala
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`, `src/QuartermasterLaw.cs`, `src/Settings.cs`
**Problem:** kwatermistrz melduje "brakuje Thrown 5/24", ale nie bylo JAK dokupic brakow -
trzeba bylo biegac po kramach i zgadywac.
**Przyczyna:** brak takiej funkcji.
**Zmiana:** opcja "Order kit for the men - the smith procures it": lista typow ze stanem
polki/potrzeba (braki podswietlone), wybor tieru 1-6 z najtansza kupna sztuka i cena od
sztuki (wartosc rynkowa x Troop Order Markup, domyslnie 1,15), ilosc 1/5/10/uzupelnij brak;
dostawa wprost na polki zbrojowni DTE po 1 h + 0,2 h na sztuke (max 24 h). Konie celowo
wylaczone (od tego stajnie). 2 nowe ustawienia MCM.
**Ryzyko / co sprawdzic:** ceny przy tierach maja wygladac rynkowo; po odbiorze zloto schodzi
o wypisana kwote, a sztuki laduja w zbrojowni (DTE sam ubiera ludzi).
**Status:** DO SPRAWDZENIA

## 2026-08-26 — naprawa sprzetu WOJSKA u kowala (zbrojownia DTE)
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`, `src/QuartermasterLaw.cs`, `src/Settings.cs`
**Problem:** zbrojownia DTE trzyma modyfikatory stanu, a ConditionScaling skaluje pancerz
KAZDEGO noszacego - zolnierz w kirysie 3% ma realnie ~11% ochrony (krzywa: 1% ~ -89%).
Nie bylo ZADNEJ drogi naprawy polek wojska ani informacji, ze taka potrzeba istnieje.
**Przyczyna:** opcje kowala obejmowaly tylko sakwy i grzbiet gracza.
**Zmiana:** nowa opcja "Send the men's worn gear to the smith" w Work the forge: skan zbrojowni
DTE, stawka HURTOWA (Troop Mend Cost Factor, domyslnie 50% ceny naprawy wlasnej sztuki),
najtansze najpierw, pelny koszt wypisany Z GORY w podpowiedzi, robota max Troop Mend Max Hours
(24 h). Kwatermistrz przy otwarciu zbrojowni melduje na zolto ile sztuk jest zuzytych i ze
kowal je naprawi. 3 nowe ustawienia MCM (grupa The men's gear).
**Ryzyko / co sprawdzic:** tooltip z kwotami na zdrowym rozsadku; po naprawie polki maja czyste
przedmioty (bez stanu) w tej samej liczbie; zloto schodzi dokladnie o wypisana kwote.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — luczarnia: pamiec ostatniej polki + stamina i ryzyko jak przy broni
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`, `src/Recipes.cs`, `src/Forge.cs`, `src/Settings.cs`, `tools/gen_mcm.py`
**Problem:** (1) wejscie w luki wymaga za kazdym razem klikania typ->tier od nowa;
(2) luk kosztowal tier x 25 x 0,8 staminy (t5 = 100 - DWA luki i koniec sesji), a ryzyko
zepsucia bylo takie samo jak przy hartowaniu plachy ("zawsze 50%, bez sensu" - Jeff);
kuznia broni pozwala kuc duzo wiecej.
**Przyczyna:** mnoznik 0.8 zaszyty w BuildRecipe; FailureChance nie odroznia luczarni od platnerki;
brak pamieci nawigacji.
**Zmiana:** (1) menu luczarni pamieta ostatni typ+tier i pokazuje na gorze "Back to the bench
you left"; po kuciu natychmiastowym (zakladka CRAFT) lista zostaje otwarta na tej samej polce.
(2) nowe ustawienia: Ranged Stamina Factor (0,35 - t5 luk = 43 staminy, 4-5 lukow na sesje)
i Ranged Failure Factor (0,5 - ryzyko lukow/kusz/amunicji o polowe nizsze). Flaga Ranged w
recepturze. PRZY OKAZJI: gen_mcm.py mial zaszyte sciezki /home/claude ze starego komputera -
teraz liczy sciezki wzgledem repo.
**Ryzyko / co sprawdzic:** procent ryzyka na polce lukow (ma byc ~polowa dawnego), ile lukow
schodzi na jednej stamince, czy "Back to the bench" wraca na wlasciwa polke.
**Status:** DO SPRAWDZENIA

## 2026-08-26 — miniatury 3D w listach rozbiorki, naprawy i tygla
**Mod:** Armoury | **Pliki:** `src/SmithMenu.cs`
**Problem:** w listach "Take a piece apart", "Pick a damaged piece to mend" i "Break metal down"
po samym opisie nie widac, co to za przedmiot - lista kucia miala obrazek, te trzy nie.
**Przyczyna:** InquiryElement dostawal `null` zamiast ItemImageIdentifier.
**Zmiana:** wspolny pomocnik `ItemPic(item)` (ItemImageIdentifier w try/catch), uzyty we
wszystkich czterech listach (kucie tez przepiete na niego).
**Ryzyko / co sprawdzic:** czy miniatury sie renderuja i nic nie wywala przy otwieraniu list.
**Status:** DO SPRAWDZENIA

## 2026-08-25 — wyjscie ze snu przez ExitToLast (naprawa CTD)
**Mod:** Armoury | **Pliki:** `src/NightRest.cs`
**Problem:** klikniecie **"Rouse the men early"** w menu snu wywalalo gre. Log:
`NullReferenceException | BLAME: SandBox | GameMenuVM.OnFrameTick()`, `Menu: camp`.
**Przyczyna:** obie drogi wyjscia ze snu wolaly `GameMenu.SwitchToMenu(_sleepReturn)`
z wnetrza opcji menu oczekiwania — VM menu jest wtedy w polowie klatki.
Sasiedni, dzialajacy przycisk "Break camp" od zawsze uzywal `ExitToLast()`.
**Zmiana:** nowa metoda `LeaveSleep()` wolajaca `GameMenu.ExitToLast()`; uzyta w opcji
"Rouse the men early" i przy naturalnym koncu snu.
**Ryzyko / co sprawdzic:** wyjscie ze snu wraca teraz o poziom wyzej (zwykle na mape),
a nie do menu obozu. Sprawdzic, czy nie ma crasha.
**Status:** WGRANE

## 2026-08-25 — cofniecie calego dziennego bloku obozowania
**Mod:** Armoury | **Pliki:** `src/NightRest.cs`, `src/Settings.cs`
**Problem:** crash w `GameMenuVM.OnFrameTick` w menu obozu. Przeszukanie 23 logow z 5 dni:
sygnatura wystepuje w **2 sesjach po zmianie**, w **21 wczesniejszych zero**.
**Przyczyna:** naprawa `_tentBroken` (globalnego wylacznika namiotow) **plus** wlaczenie
obozowania bandom naraz. Do tej pory tent-system praktycznie sie wylaczal po pierwszym
wyjatku; po naprawie zaczal obslugiwac setki partii i grzebac w ich wizerunkach na mapie
przy kazdym nocnym ticku.
**Zmiana:** cofniete do stanu porannego: obozuja tylko lordowie i karawany, bez dlugu snu
w pogoni, bez limitu namiotow, ze starym `_tentBroken`.
**Ryzyko / co sprawdzic:** **wraca stary blad** — namiot gracza moze zniknac po pierwszej
wywrotce. Do zrobienia na nowo, ostroznie, po jednej rzeczy.
**Status:** COFNIETE

## 2026-08-25 — kultura duchownych i ochotnicy miejscowi
**Mod:** CrashScribe | **Pliki:** `src/Mends.cs`, `src/WarReport.cs`, `src/Config.cs`
**Problem:** w polnocnych wsiach (White Ranch, Last River, Snowwood) mozna bylo zobaczyc
**wolny lud** i **Westerlands Noble Youth** wsrod ochotnikow.
**Przyczyna:** BK tworzy duchownego z presetu wiary (`HeroCreator.CreateSpecialHero`),
wiec kaplan ma kulture presetu, nie osady — a ochotnikow dobiera sie wedle kultury notabla.
Nasza wczesniejsza latka na `GetPossibleSpawns` leczyla objaw; lista i tak sie odnawiala.
**Zmiana:** `Mends.LocalLevies(Settlement)` prostuje kulture notabla do kultury osady
i zeruje obce sloty ochotnikow — przy wejsciu do osady i raz dziennie.
**Ryzyko / co sprawdzic:** w logu `Mends: <imie> w <osada> byl X, jest Y (Preacher)`.
W sesji 25.08: **558 poprawionych notabli**, spisy osad czyste.
**Status:** WGRANE

## 2026-08-25 — odblokowanie BannerKings (dwie latki na BKROTPatch)
**Mod:** CrashScribe | **Pliki:** `src/Mends.cs`
**Problem:** **pusty ekran rekrutacji** ("Recruit All (0)"), 11 487 wyjatkow na sesje.
**Przyczyna:** patrz `docs/ERRORS.md` A1 i A2 — BKROTPatch dusil generowanie duchownych
(`Invoke(null)` na metodzie instancyjnej) i przerywal inicjalizacje stylow zycia.
**Zmiana:** `Mends.Unhook()` zdejmuje te dwa prefixy z metod BannerKings.
**Ryzyko / co sprawdzic:** w logu `Mends: BKROTPatch nie dusi juz duchownych BK...`.
Ekran rekrutacji ma byc pelny.
**Status:** WGRANE

## 2026-08-25 — reszta prac tego dnia (skrot)
**Mody:** Armoury, RealisticCaptivity, CrashScribe
- **Ksiega wzorow** — po kazdym wykuciu widac ile wzorow znasz i ile do nastepnego.
- **"Take a piece apart to copy its pattern"** — rozbiorka zdobytej sztuki omija kolejke nauki.
- **Kolejka pancerza rozbita na 7 galezi** (helmy/kirysy/nogawice/rekawice/plaszcze/tarcze/kropierze).
- **Ucieczka konno ze spotkania** (`HorseFlight.cs`) — caly oddzial w siodle moze uciekac,
  straz tylna tylko przeciw jezdzie wroga.
- **Lamanie sie rannych** (`BrokenMen.cs`) — ponizej 20% HP AI panikuje i schodzi z pola.
- **Stamina kowalska** regeneruje sie wszedzie poza kuznia (przedtem tylko w 4 konkretnych menu).
- **Rozrusznik fabuly ROT** + dziennik kolejki (`Fabula.cs`).
- **Spis notabli** przy wejsciu do osady (`WarReport.cs`).
**Status:** WGRANE
