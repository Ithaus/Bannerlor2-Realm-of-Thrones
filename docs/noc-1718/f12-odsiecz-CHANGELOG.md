## 2026-10-18 (paczka f12-odsiecz - pytanie Jeffa "z mojego logu widac ze polnoc miazdzy lannisterow i krolewska przystan, czemu tak jest, jaka jest sila obu stron?": odsiecz bez szans zwalnia druzyny krolestwa)

**Mod:** Armoury (`BesiegedRelief.cs`, `Settings.cs`, `McmSettings.cs` z `gen_mcm`) | **Galaz:** `w-toku/f12-odsiecz` (od 7ca98719 = WGRANIE 80 w grze Jeffa) | **Tor:** bez gry (build Armoury kod 0; czytniki logu OB5 w `tools/sprawdz_logi.py` sprawdzone na wierszach w nowym formacie) | **Klucz (nowy, domyslnie TAK):** `BesiegedReliefOnlyWhenReachable` (MCM "Besieged Relief Only When Reachable", blok "Relief of besieged towns"). W `Armoury.json` Jeffa (13.10) nie ma zadnego klucza `BesiegedRelief*` (sprawdzone 18.10), wiec dziala domyslna z kodu. Zadna domyslna istniejacego klucza nie zmieniona. | **Zapis:** bez nowych kluczy SyncData (stan tylko w pamieci, jak cale OB5).

**Dla Jeffa (prosto):**
- Polnoc nie wygrala z Korona zadnej bitwy. Ma duzo wiecej wojska, a Korone wczesniej wykrwawilo Dorzecze. Nasza regula odsieczy (OB5) do tego dokladala: kazala wszystkim lordom Korony stac pod oblezona Krolewska Przystania, dopoki nie zbiora odsieczy 2 x silniejszej od armii Robba (24 tys. sily). Korona miala w calym polu 11 tys., wiec lordowie stali tam bez konca. Nie werbowali (gra werbuje tylko w osadzie), dojadali jedzenie do zera, a Westerlands przestalo dawac rekruta (z 70 do 3-4 ludzi na dobe).
- Teraz OB5 sprawdza, czy krolestwo w ogole moze zebrac odsiecz, ktora gra rzuci na oboz. Gra atakuje oboz dopiero, gdy odsiecz pod murami ma 1,2 x jego sily. Jesli wszyscy lordowie krolestwa w zasiegu razem nie maja nawet tyle, OB5 ich nie trzyma. Robia to, co gra uzna: werbuja, kupuja jedzenie albo ida bronic innej oblezonej twierdzy, ktora da sie obronic (np. Stoney Sept). Gdy krolestwo urosnie, OB5 sam ich znowu wola.
- Ta sama regula dla kazdego krolestwa. Z logu wynika, ze zwolnione bylyby tez druzyny Dragonstone przy Stonedance (zebrane 56% wymaganej), a pewnie i Targaryena Aegona przy Tolos. Starfall (Dorne zebralo 7,6-8,7 tys. przy progu ok. 7,9 tys.) jest na granicy. Tam, gdzie odsiecz byla osiagalna (Deep Den, Qamayl, Sunstone itd.), nic sie nie zmienia.
- Wylaczenie klucza = dokladnie jak dotad.

**Problem (dowody z logu Jeffa, WGRANIE 80, dni 109020-109043; diagnoza w tej samej nocy):**
- `Armoury-2026-10-18_07-43-11.log`, linia doby OB5 109043: "King's Landing (stolica, House Baratheon of King's Landing) zebrane 11246 / wymagane 24235 (druzyn 31, z armia)"; "Stoney Sept (miasto, ...) zebrane 0 / wymagane 4815 (druzyn 0)"; "Tolos (miasto, House Targaryen, Aegon) zebrane 3582 / wymagane 5925 (druzyn 13)"; "Demongate (zamek, ...) zebrane 0". "oceny obrony podbite 399 razy u 88 druzyn, pominiete z glodu 44" (109035: 5).
- KP oblegana od 109029, oboz Robba (OB1) 12340-12388, wiec prog gry 1.2 x to ok. 14.8k. Korona zebrala 11.2k (ok. 8.2k ludzi, cala jej armia polowa) i odsiecz nigdy nie ruszyla.
- `balans-krolestw.csv` (vlandia): ludzie w druzynach 7434 -> 8730 w 109020-109025 (+260/d), od 109030 stoja na 8010-8216 do konca sesji przy wypelnieniu limitu 61% (`budzet-rodow.csv` 8114/13370).
- `ludzie-regiony.csv` (zwerbowani_dzis, regiony Korony): przed oblezeniem 631 w 10 dob (ok. 70/d; Westerlands 549), w oblezeniu 49 w 14 dob (ok. 3.5/d; Westerlands 46, Stoney Sept 0).
- Inne krolestwa: Stonedance (zamek Dragonstone) wymagana 8290, najwiecej 4671 (56%), 33 druzyny podbite 3070 razy przez 10 dob, zamek i tak ZDOBYTY; Starfall (Dorne) werbunek regionow Dorne z 50-170/d do 0-21/d w 109036-109042.

**Przyczyna:**
- `BesiegedRelief.ThinkPostfix` (przed f12 linie 369-381): podbicie obrony `nv = Prio x max(inny cel, 0.5, dMax) + 0.01 x ocena gry` znika tylko, gdy zebrana bez tej druzyny >= R (`rest >= sg.Need`). R = 2.0 x oboz (stolica). Gdy cale krolestwo ma mniej niz R, warunek nigdy nie zachodzi. Kazda druzyna lorda w zasiegu gry (8 dob marszu) dostaje obrone ponad wszystko, takze ponad wizyte w osadzie (werbunek, jedzenie). Wyjatek jest tylko `IsStarving`.
- Gra 1.4.8 (dekompilacja, sprawdzone): `MobilePartyAi.GetNearbyPartyDataWhileDefendingSettlement` - odsiecz rusza na oboz twierdzy dopiero przy `num7 > num3 x num15`, `num15 = 1.2f` dla twierdzy. Ponizej druzyny czekaja w pozycji obrony. Werbunek lordow krolestw (`RecruitmentCampaignBehavior.CheckRecruiting`) dziala tylko w osadzie. Wolna druzyna mysli co 6 h zegara AI (`AiPartyThinkBehavior.PartyHourlyAiTick` i jego kopia w ROT `AIThinkPatch.PartyHourlyAiTick`: `num = 6`, wodz armii co 1 h). AI spiacego w obozie swiata (0-6, `SleepersHoldOrders`) jest wylaczone, a licznik godzin mysli wtedy stoi. Ocena obrony gry (`AiMilitaryBehavior.CalculateMilitaryBehaviorForSettlement`, Defender) jest dodawana kazdej druzynie krolestwa w zasiegu (`GetDistanceScoreForDefending` > 0), z wyjatkiem celu Aggressive.

**Zmiana (`BesiegedRelief.cs`):**
- **Zasieg krolestwa Z** (nowa metoda `Reach`, raz na godzine w `Rebuild` po zebranej odsieczy) = zebrana C + `Contrib` druzyn ze slownika `Track.EligAt`, ktore nie sa w zebranej odsieczy. `EligAt` to druzyna i godzina jej ostatniej mysli z ocena obrony tej osady od gry, wpisywane w `ThinkPostfix` przy kazdej mysli. Druzyna, ktora przy swojej mysli tej oceny nie dostala, wypada (wyszla z zasiegu). Wpisy starsze niz 24 h, druzyny nieczynne i z innej frakcji tez wypadaja. Czlonek armii liczy sie w sile wodza (`Contrib` wodza liczy cala armie z wezwanymi), sam nie. Przy zamku pomijane sa druzyny oblegajace twierdze wroga, bo OB5 i tak nie sciaga ich do zamku (recenzja 14.10).
- **Prog P** = min(R, 1.2 x sila obozu); 1.2 to stala gry `num15`.
- **Bramka** w petli `ThinkPostfix`, po `rest >= sg.Need`: gdy klucz TAK, minelo 13 h od wykrycia oblezenia i Z < P, ta osada nie jest podbijana i nie zostaje `top`. Wtedy nie ma tez opcji armii wladcy ani rezerwacji godziny. Wracaja oceny gry. Inne oblezone twierdze krolestwa, osiagalne, sa podbijane jak dotad (pierwszenstwo stolica > miasto > zamek bez zmian). Gdy Z >= P, wszystko jak przed f12: druzyny ida, az zebrana >= R albo przyjda wszyscy w zasiegu.
- **13 h rozruchu** (`ReachWarmupHours`) = 6 h zegara mysli + 6 h obozu swiata + 1 h zapasu. W tym czasie kazda druzyna w zasiegu zdazy raz pomyslec, a do tego momentu dziala droga sprzed f12. Bez rozruchu Z byloby zanizone w pierwszych godzinach i bramka moglaby zamknac odsiecz osiagalna. Wodz armii mysli co 1 h, takze w czasie zbiorki (patrz "Recenzja" nizej).
- **Blad liczenia** Z daje `Ready = false`, czyli droge sprzed f12, i potkniecie w linii doby. Bramka nie gasi niczego globalnie.
- **Linie:**
  - start sesji: "; odsiecz bez szans zwalnia druzyny (f12: zasieg krolestwa < min(wymagana, 1.2 x oboz), od 13 h po wykryciu) TAK/NIE";
  - linia doby: ", odsiecz bez szans (zasieg krolestwa < min(wymagana, 1.2 x oboz)): zwolnione N razy u M druzyn" (zwolniona = mysl, w ktorej bramka zamknela co najmniej jedna osade, a OB5 nie zostawil druzynie zadnej odsieczy, czyli `top == null`; recenzja ponizej), przy kluczu NIE z dopiskiem "(Besieged Relief Only When Reachable NIE - bez zwalniania)";
  - wiersz oblezenia (tylko przy kluczu): ", zasieg krolestwa Z / prog P" + " BEZ SZANS - zwolnionych dzis K" (K = druzyny zwolnione w mysli, w ktorej bramka zamknela te osade) albo " (rozruch - pierwsze 13 h)". Linie "poczatek", "armia krola" i "koniec" bez zmian.

**Ryzyko / co sprawdzic (kontrola calosci, CLAUDE.md 8.0):**
- **Regresje:**
  - Klucz NIE: ten sam kod podbicia, a `EligAt` i Z to tylko ksiegowanie i nieuzywana linia. Kolejnosc `Eligible.Add` jest ta sama (tylko przy ocenie obrony od gry). Linia doby dostaje segment z 0 i dopiskiem NIE, wiersze bez zmian.
  - Klucz TAK, odsiecz osiagalna (Z >= P): bez zmian.
  - Pierwsze 13 h oblezenia (takze po kazdym wczytaniu, bo stan nie idzie do zapisu): bez zmian.
- **Czytelnicy:**
  - `SiegeWalls2.Oblezenia3` (`ReliefOnWay`) czyta tylko `LastC` / `LastNeed`, bez zmian.
  - `sprawdz_logi.py` sprawdzony na wierszach w nowym formacie: `OD_START`, `OD_DAY` (te same 14 pol), `OD_OWN`, `NKS_OB5D`, `NKS_OB5T` (nazwy twierdz z wierszy z dopiskiem zasiegu, "BEZ SZANS" i "rozruch") czytaja jak dotad. Dopiski nie zawieraja ";" ani "|".
  - Inne narzedzia linii OB5 nie czytaja.
  - **OD-2a / OD-2b** (grupa `odsiecz`: odsiecz >= 1 druzyna w >= 95% oblezen stolic i miast; zebrana >= wymagana albo >= 80% druzyn w zasiegu) beda NIE przy oblezeniach bez szans. Tak ma byc. Linia "koniec" nie mowi o bramce, wiec checker tego nie odrozni. Progi do decyzji prowadzacego.
- **Kolizje:**
  - Ten sam cel latki (`CampaignEventDispatcher.AiHourlyTick`) maja tez `CrossingLaw` A12 (wynik -1 zablokowanym osadom) i ROT `AIThinkPatch.Patch1`. Bramka tylko nie podnosi oceny, wiec -1 i filtr ROT zostaja.
  - BK `BKArmyBehavior` (armie Defender do najblizszej oblezonej twierdzy) bez zmian: armia BK pod osada bez szans dostaje oceny gry, a nie podbicie OB5.
  - Wodz armii (mysli co 1 h) pod osada bez szans moze dostac od gry cel spoza GoToSettlement/Defend/Patrol/Raid/Besiege. Wtedy gra rozwiazuje armie (`DisbandArmyAction`, droga gry), a czlonkowie stana sie wolni.
  - AC (spoistosc armii) bez zmian.
- **Spojnosc:**
  - Jedna regula dla wszystkich krolestw, tez dla krolestwa gracza (druzyna gracza nigdy nie jest ruszana).
  - Z liczy `EstimatedStrength` jak C i R, a gra porownuje `EstimatedStrength` sojusznikow z `GetCustomStrength(Defender, PlainBattle)` obozu. Prog jest wiec przyblizony, tak samo jak R od 14.10.
  - Z liczy kazda osade osobno, wiec ta sama druzyna wchodzi do Z stolicy i miasta. To zamierzone: Z mowi "gdyby wszyscy w zasiegu poszli tutaj".
- **Skutek dla gry:**
  - Pod osada bez szans nie stoi odsiecz, ktora i tak nie zaatakowalaby obozu (ponizej 1.2 x gra tylko czeka). Szturmu i obrony w murach to nie zmienia.
  - KP u Jeffa: mury 82%, jedzenie na 154 doby, w miescie 640 zalogi i 172 milicji. Do sprawdzenia, czy KP nie padnie szybciej. Bitw pod murami w tym oblezeniu i tak nie bylo.
  - Druzyny Korony przy bramce zamknietej dla KP moga isc na odsiecz Stoney Sept (miasto, wymagana 4815, prog 3852). Tylko tyle, ile trzeba do R miasta, a reszta jest wolna.
- **Mozliwe miganie bramki:** gdy Z krazy wokol P (straty, druzyna wychodzi z zasiegu), druzyny moga dostawac podbicie na zmiane z jego brakiem co kilka mysli. Z nie zalezy od tego, czy druzyna idzie, czy nie (C + reszta w zasiegu), wiec miga tylko przy zmianie sily albo zasiegu. Sprawdzic w wierszach doby, czy stan "BEZ SZANS" nie skacze co dobe przy tym samym oblezeniu.
- **Wydajnosc:** w `ThinkPostfix` dochodzi petla po oblezeniach krolestwa (zwykle 1-5) z `Contains` na liscie <= 5. W `Rebuild` raz na godzine przejscie po `EligAt` (druzyny krolestwa w zasiegu, kilkadziesiat) z `Contrib`.
- **Zamknieta ekonomia:** nie dotyczy, bo nie ma przeplywu zlota ani towaru.
- **Cudzy kod:** zalozenia sprawdzone w dekompilacji gry 1.4.8 (`MobilePartyAi`, `AiPartyThinkBehavior`, `AiMilitaryBehavior`) i ROT (`ROT.HarmonyPatches.Core.AIThinkPatch.PartyHourlyAiTick` - ten sam zegar 6 h).
- **Co sprawdzic w tescie (sesja Jeffa, KP oblegana):**
  - linia startu OB5: "... zwalnia druzyny ... TAK";
  - wiersz KP po 13 h: "BEZ SZANS", zasieg okolo 11-12k wobec progu okolo 14.8k (szacunek: zebrane 11246 to prawie cala Korona);
  - "zwolnione N razy u M druzyn" > 0;
  - `ludzie-regiony.csv`: zwerbowani_dzis regionow Korony (Westerlands) wraca w strone ok. 50-70/d;
  - `balans-krolestw.csv`: ludzie vlandia rosna w czasie oblezenia KP;
  - "pominiete z glodu" maleje;
  - gdy Korona dorosnie do progu, wiersz KP bez "BEZ SZANS", zebrane rosnie, a bitwa pod murami / zdjecie oblezenia przez SCA145 (1.25 x);
  - Stoney Sept: zebrane > 0;
  - Stonedance / Tolos / Starfall: ten sam wzor;
  - przy armii (BK albo gry) zbieranej dluzej niz doba w zasiegu twierdzy: zasieg krolestwa w wierszu doby nie spada o sile tej armii w 2.-3. dobie zbiorki (recenzja 1);
  - potkniecia OB5 = 0.

**Recenzja (18.10, 2 uwagi "minor" - obie potwierdzone w kodzie i poprawione):**
1. **Armia gry albo BK typu Defender w zbiorce dluzszej niz 24 h wypadala z zasiegu Z.**
   - Sprawdzone w dekompilacji gry 1.4.8:
     - `AiMilitaryBehavior.AiHourlyTick` (l. 496-499) ustawia `p.DoNotChangeBehavior = true` wodzowi armii, gdy `Army.IsWaitingForArmyMembers()`. To jedyne miejsce w grze, ktore to pole wlacza. W modulach odwoluje sie do niego tylko ROT (czyta je w kopii mysli) i Armoury. Metoda nie konczy sie na tym: liczy dalej oceny 4 typow misji, w tym obrone (Defender) dla wodza armii.
     - Wodz armii mysli co 1 h (ROT `AIThinkPatch.PartyHourlyAiTick`: `flag` -> `num = 1`).
     - Zbiorka trwa do `MaximumWaitTime` = 3 x 24 h, a potem az armia ma ponad 0.75 - 0.01/h sily (`Army.IsWaitingForArmyMembers`). `StrategicArmyManagementModel` (SCA145, model armii u Jeffa) dziedziczy po `DefaultArmyManagementCalculationModel` i tego nie zmienia. Przed dojsciem wodza do miejsca zbiorki (`_armyGatheringStartTime = 0`) armia tez "czeka".
     - Miejscem zbiorki (`Army.FindBestGatheringSettlementAndMoveTheLeader`) nigdy nie jest oblegana twierdza (`IsUnderSiege` pominiete, cel tez). Dlatego `Rebuild` wlicza armie w zbiorce do C tylko przez `_armyFor` (armia krola OB5).
   - Skutek przed poprawka: `ThinkPostfix` wychodzil na `DoNotChangeBehavior` przed odswiezeniem `EligAt`. Po 24 h zbiorki wpis wodza sie przedawnial, a czlonkowie sa pomijani jako czlonkowie, wiec Z tracilo cala armie. Bramka mogla wtedy pokazac BEZ SZANS i zwolnic wolnych lordow odsieczy, ktora jest osiagalna. Trwalo to najwyzej ok. 48 h na armie, do konca zbiorki.
   - Poprawka (`ThinkPostfix`): `DoNotChangeBehavior` jest zapamietane (`dnc`) i sprawdzane dopiero PO odswiezeniu `EligAt`. Wodz w zbiorce z ocena obrony od gry ma wiec swiezy wpis i wnosi do Z cala armie (`Contrib`), a gdy tej oceny nie dostaje (poza zasiegiem), wypada od razu.
   - Bez zmian zostaje:
     - podbicie: `if (dnc) return;` stoi przed `_tmpIdx.Count == 0` i przed glodem, wiec licznik "pominiete z glodu" liczy jak dotad;
     - `Eligible` (linia "koniec", OD-2b): wodz w zbiorce do niego nie wchodzi (`if (!dnc)`), tak jak przed f12;
     - wyjscia dla bitwy, oblezonej osady i pustych ocen.
   - Wybrany wariant "odswiez przed wyjsciem" zamiast "licz ostatni wpis bez limitu 24 h": gra liczy wodzowi oceny co godzine, wiec zasieg jest swiezy, a nie przeterminowany. Dziala tez dla armii Besieger, bo jej wodz po zbiorce mysli co 1 h i OB5 moze go podbic, tak jak liczylo sie go wczesniej jako wolnego lorda.
   - Uwaga, bez zmian: ROT `AIThinkPatch.Patch1` (postfiks na tym samym celu, przed naszym Low) przy odfiltrowaniu celu wola `partyThinkParams.Reset`, a to zeruje `DoNotChangeBehavior`. Tak bylo i przed f12.
2. **Liczniki "zwolnione" liczyly tez lordow, ktorzy i tak szli na inna odsiecz.**
   - Gdy bramka zamknela KP, a dla innej osady (np. Stoney Sept) ocena gry byla juz >= `nv`, ta osada zostawala `top`. Lord byl na nia rezerwowany (`top.In.Add`), a `raisedAny` zostawalo false. Lord liczyl sie wtedy w `_dRel` / `_dRelParties` i w `DayRel` KP jako zwolniony. Takiej ocenie (>= `Prio x b`, gdzie `b` >= najlepszy inny cel) nic nie dorownuje, wiec lord idzie na te odsiecz.
   - Poprawka: osady zamkniete bramka trafiaja w petli do `_tmpGated`. Po petli lord jest liczony jako zwolniony (`_dRel`, `_dRelParties`, `DayRel` kazdej zamknietej osady) tylko przy `top == null`. `raisedAny` i `gated` usuniete, bo sluzyly tylko licznikom.
   - Zachowanie bez zmian: `top`, podbicia, opcja armii wladcy i rezerwacja licza sie jak dotad. Format linii tez sie nie zmienia. `sprawdz_logi.py` i inne narzedzia tych licznikow nie czytaja (grep `zwolnion` w `tools/` - tylko inne linie).
- Build Armoury: kod 0. `gen_mcm` niepotrzebny, bo `Settings.cs` bez zmian.

**Nie zrobione (poza zadaniem):**
- limit czasu odsieczy (wariant 3 diagnozy);
- osobny wyjatek "druzyna mocno niepelna albo z malym zapasem jedzenia idzie najpierw do osady" (wariant 2 diagnozy);
- podzial dziennych licznikow OB5 wedlug krolestw;
- zmiana progow OD-2 w checkerze;
- ochrona fabuly KP (decyzja: bez ochrony).

**Status:** DO SPRAWDZENIA (kod, build Armoury kod 0, `gen_mcm` uruchomiony: 1 nowe ustawienie w Armoury; po recenzji 2 poprawki w `BesiegedRelief.cs`, build kod 0; bez testu w grze).
