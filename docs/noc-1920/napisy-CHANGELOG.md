## n20-napisy - za dlugie napisy na przyciskach menu i w oknach wyboru (Armoury)

**Problem:** Jeff 19.10 (doslownie): "to samo przy inwestowaniu i wybieraniu opcji to amo za duzo tekstu na buttonie i zle to wyglada, trzeba to
poprawic, albo poiwqikszyc ramke albno dacm niej tekstu aby wszystko sie meiscilo." Dowod w kodzie (zmierzony metrykami czcionek gry, ponizej):
wiersz okna "Works in the X district" (inwestowanie) mial ok. 1090 px przy 419 px wiersza i stalej wysokosci 65 px (2 linie) - trzecia linia
wychodzila poza ramke; kwoty w drugim oknie ok. 700 px; opcje noclegu w karczmie ("Decent inn (beds, a warm hearth and ale) - 1234 d: you 12,
the men 1222, companions 30") ok. 790 px przy 403 px przycisku menu (2 linie); 14 stalych opcji kuzni i Qohoru 347-452 px (na granicy albo
ponad 403 px - lamaly sie na 2 linie); przycisk okna rabunku "Raid the next settlement (about N days)" ok. 377 px przy 164 px przycisku.

**Przyczyna:** szczegoly (kwoty, sumy, wyjasnienia) byly w samym napisie przycisku. Szerokosci z prefabow i czcionek gry (sprawdzone w plikach):
- opcja menu gry: `Native/GUI/Prefabs/GameMenu/GameMenu.xml` NormalMode.Menu.Width 493, `GameMenuItem.xml` napis MarginLeft 55 + MarginRight 35
  = **403 px**, czcionka Galahad 24 (ROT-Content nie nadpisuje tych prefabow); przycisk ma wysokosc CoverChildren - dluzszy napis lamie sie na 2 linie;
- wiersz okna wyboru (`ROT-Content/GUI/PreFabs/Information/Inquiries/MultiSelectionQueryPopup.xml`): 447 - 20 - 8 = **419 px** (z obrazkiem
  86 px mniej = 333 px), FiraSansExtraCondensed 25, wysokosc STALA 65 px = najwyzej 2 linie;
- przycisk zwyklego okna (`SingleQueryPopup.xml`): 251 - 47 - 40 = **164 px**, Galahad 24.
Szerokosc napisu = suma xadvance znakow z `GUI/GauntletUI/Fonts/<czcionka>/<czcionka>.fnt` x rozmiar / 32 (pliki .fnt bez kerningu).
Podpowiedz opcji menu w 1.4.8: `GameMenuOption.GetConditionsHold` kopiuje `MenuCallbackArgs.Tooltip`, `GameMenuItemVM.Refresh` pokazuje ja jako
`ItemHint` (dekompilacja TaleWorlds.CampaignSystem / .ViewModelCollection) - wiec szczegoly mozna przeniesc do podpowiedzi w warunku opcji.

**Zmiana:** nowy klucz MCM **Short Button Labels** (domyslnie wlaczony, `Settings.ShortButtonLabels`, przy `CraftResultPopup`). Nowy plik
`Armoury/src/ShortLabels.cs`: `Menu(krotki, dawny)` przy wpinaniu opcji, `Pick(krotki, dawny)` dla napisow liczonych na zywo i wierszy okien,
`Hint(szczegoly, dawna)` dla podpowiedzi wiersza, `Tip(warunek, szczegoly)` - owija dawny warunek opcji menu bez zmian i dokleja do podpowiedzi to,
co wypadlo z napisu (przy opcji nieaktywnej powod zostaje na gorze). Identyfikatory opcji, kolejnosc i logika bez zmian. Bez klucza - dawne napisy 1:1.
Linia kontrolna przy kazdym wczytaniu (OnAfterSessionLaunched, po SiegeRansom.AddMenus): ile opcji `arm_*` jest w menu gry, ile wpieto
z krotkim napisem, najszerszy staly napis w px (te same metryki Galahad 24 wbudowane w kod) i ile przekracza 403 px.

Lista napisow przed -> po (px = szerokosc w czcionce przycisku):

Opcje menu gry (403 px):
| przed | po | podpowiedz dostaje |
|---|---|---|
| Seek out the Valyrian steel masters of Qohor (405) | Seek out the masters of Qohor (280) | The Valyrian steel masters of Qohor. |
| Have a greatsword reforged into two blades (398) | Split a greatsword into two blades (313) | Have a greatsword reforged into two blades. |
| Have a blade forged to your own design and name (452) | Forge a blade of your own design (297) | Have a blade forged to your own design and name. |
| Wait in Qohor until the masters are done (372) | Wait until the masters are done (286) | Wait in Qohor until the masters are done. |
| The order book - commissions from the lords (398) | The order book (137) | Commissions from the lords. |
| Stay at the forge until the work is done (358) | Stay until the work is done (244) | Stay at the forge until the work is done. |
| The mending bench - your gear and the men's (410) | The mending bench (177) | Mend your own gear and the men's. |
| Take a piece apart to copy its pattern (347) | Take a piece apart to copy it (264) | Take a piece apart to copy its pattern. |
| Order kit for the men - the smith procures it (404) | Order kit for the men (196) | The smith procures it. |
| Muster the men's kit - the quartermaster's report (446) | The quartermaster's report (242) | Muster the men's kit. |
| Mend everything you wear - your own hands (402) | Mend all you wear yourself (242) | Mend everything you wear with your own hands. |
| Mend everything you wear - the smith's price (408) | Mend all you wear at the smith (284) | Mend everything you wear at the smith's price. |
| Restore ALL battle-worn loot from your bags (408) | Restore worn loot in your bags (274) | Restore ALL battle-worn loot from your bags. |
| Send the men's worn gear to the smith (351) | Mend the men's worn gear (242) | Send the men's worn gear to the smith. |
| Put the work aside (pay only for what is finished) (440) | Put the work aside (170) | You pay only for what is finished. |
| Karczma/zajazd: `Decent inn (beds, a warm hearth and ale) - 1234 d: you 12, the men 1222, companions 30` (ok. 790) | `Decent inn - 1234 d` (ok. 190) | pierwsza linia: `Decent inn (beds, a warm hearth and ale) - 1234 d: you 12, the men 1222, companions 30.` |
| Zajazd zamkniety: `Decent inn (beds, a warm hearth and ale)` | `Decent inn` | powod + `Decent inn: beds, a warm hearth and ale.` |

Okna wyboru (wiersz 419 px, z obrazkiem 333 px; najwyzej 2 linie):
| okno | przed | po | do podpowiedzi |
|---|---|---|---|
| Works in the X district (inwestowanie, okno 1) | `Ironrath (main village) - fish - 30 homesteads, 45% - full kit 12,345 - first coins about 25% a year, burns last` (ok. 1090) | `Ironrath - fish - 25% a year` (zamknieta: `Ironrath - fish - closed`) | `Main village: 30 homesteads, 45% of the district. Full kit 12,345; the first coins earn about 25% a year; burns last.` + dawna podpowiedz (z powodem zamkniecia) |
| j.w. odbudowa | `Rebuild your ploughs in X: 1,500 (by day 812)` | `Rebuild ploughs in X: 1,500` | `Rebuild by day 812.` |
| j.w. przelacznik | `Automatic rebuilding: ON - click to switch off` | `Automatic rebuilding: ON` | `Choose it to switch it off.` |
| X - rodzaj prac (inwestowanie, okno 2, kwoty) | `1,000 (still 12% on the last coin) - +12.3% fish - about 3.4 a day (1,234 a year, 12%) - 5.6% of its fish yours` (ok. 700) | `1,000 (best buy) - 3.4 a day (12% a year)`; dopiski `(all)`, `(full kit)` | wyjasnienie dopisku, `+12.3% fish in X. About 3.4 a day, 1,234 a year (12% of what you pay). 5.6% of its fish yours.`; przy kwocie niedostepnej powod na gorze |
| The Order Ledger - tiery ze straganu | `Tier 3 - X, 1234 gold off the stall (5 on the stalls)` | `Tier 3 - X, 1234 gold` | `1234 gold off the stall, 5 on the stalls.` |
| j.w. brak | `Tier 3 - none on the stalls of King's Landing` | `Tier 3 - none on the stalls` | (miasto bylo juz w podpowiedzi) |
| X (tier N) off the stalls - ilosc | `10 x BodyArmor (tier 3) - 1234 gold  (only 5 on the stalls)  (fills the shortage)` | `10 pieces - 1234 gold (only 5)` | `Fills the shortage.` (reszta byla w podpowiedzi) |
| The Order Ledger - tiery (bez straganu) | `Tier 3 - X, 1234 gold apiece` | `Tier 3 - X, 1234 gold` | `1234 gold apiece.` |
| X - ilosc (bez straganu) | `10 x X - 1234 gold  (fills the shortage)` | `10 pieces - 1234 gold` | `Fills the shortage.` |
| The Order Ledger - rodzaje | `HorseHarness   (racks 12 / need 100)  - SHORT 88` | `HorseHarness: 12 of 100 - short 88` | `On the racks 12, the men need 100.` |
| At the Anvil (luki/kusze/amunicja) | `LEGEND - X   (tier 6)  - needs Smithing 250` / `- pattern unknown` | `LEGEND - X (t6) - Smithing 250` / `- unknown` | (pelne zdania byly juz w podpowiedzi) |
| Town/Castle purse (BEE) | `Pay 5000 gold - <powod>` | `Pay 5000 gold` | (powod byl juz w podpowiedzi) |

Przyciski zwyklych okien (164 px):
| okno | przed | po | dawny napis idzie do |
|---|---|---|---|
| X burns (rabunek okregu) | `Raid the next settlement (about N days)` / `Raid the next M settlements (about N days)` (ok. 377) | `Raid on` | tekst okna: osobny akapit `Raid the next settlement (about N days).` |
| wioska okregu - "Hear more" | `Back to the villages` (173) | `Back` | - |

Bez zmian (zmierzone, mieszcza sie): pozostale opcje menu Armoury, RealisticCaptivity, GrandTourney (najdluzsza `Have a blade reforged in a new form` 327 px),
przyciski okien (najdluzsze RC `I accept the debt` / `I would rather rot` 160 px z 164 - na granicy, zostaja), wiersze z nazwami przedmiotow/oddzialow
(MusterBook, Orders, kuznia - najwyzej 2 linie). Ramka plotek w gospodach nieruszana (zadanie "plotki"). CrashScribe i ForgeView bez zmian.

**Ryzyko / co sprawdzic:**
- Regresje: zmieniaja sie tylko napisy i podpowiedzi; identyfikatory opcji, indeksy, warunki (owiniete 1:1, wynik i IsEnabled z dawnego warunku),
  skutki i obieg zlota bez zmian. Nic w repo nie szuka opcji po napisie (grep tools/*.py, *.ps1, src - tylko komentarze).
- `ShortLabels.Tip` czyta klucz przy wpinaniu menu - opcje menu zmieniaja sie po wczytaniu zapisu (opisane w HintText), wiersze okien i opcje noclegu od razu.
- Napis opcji Qohoru "arm_qohor_enter" mial juz podpowiedz z liczba zlecen - zostaje, doklejona pod nowym zdaniem.
- Linia kontrolna czyta prywatne pole `GameMenuManager._gameMenus` (AccessTools.Field) tylko raz na wczytanie, w try (blad = Log.Error, gra dalej).
- Kolizje: inne galezie nocy moga ruszac te same linie (TownInns - "plotki" nie, ale opcja "Listen to the gossip" jest obok; DistrictWorks.Menu.cs - "wioska"
  moze dodac opcje). Przy scaleniu sprawdzic, czy nowe opcje innych galezi nie przekraczaja 403 px (linia kontrolna to pokaze: "za szerokich: N").
- Szerokosci liczone z metryk; rzeczywisty uklad Gauntlet moze sie roznic o kilka px - dlatego cel ok. 85% przycisku dla napisow stalych.

**Status:** DO SPRAWDZENIA

### Decyzje (podjete samodzielnie)
- Zamiast powiekszac ramki (prefaby gry/ROT - nadpisanie GUI dla 40 modow to ryzyko i zmiana wygladu wszystkich okien) - krotszy napis, szczegoly w podpowiedzi.
  Wylacznik: MCM Armoury **Short Button Labels** = off (dawne napisy, menu po wczytaniu zapisu).
- Regula napisu: stala opcja menu do ok. 85% z 403 px (jedna linia z zapasem); wiersz okna - jedna linia, gdy nazwy sa typowe, najwyzej 2 przy dlugich
  nazwach wiosek/przedmiotow (tych nie skracam - to nazwy wlasne).
- Na przycisku zostaje to, co rozni opcje miedzy soba (kwota, dochod dzienny, procent na rok, suma noclegu); wszystko inne do podpowiedzi.
- Kwota "still 12% on the last coin" nazwana na przycisku `(best buy)`, z wyjasnieniem w podpowiedzi ("The most that still earns 12% a year on the last coin.").
- RealisticCaptivity i GrandTourney bez zmian (nic nie przekracza szerokosci) - wiec bez nowych kluczy w tych modach.

### Test w grze
- Autotest 5 dob + bitwa (zwykly, bez flag): w logu Armoury przy kazdym wczytaniu linia
  `Napisy przyciskow (n20-napisy): krotkie WLACZONE | opcji menu Armoury N, wpietych z krotkim napisem 15, liczonych na zywo M | najszerszy staly napis ok. 327 px z 403 ('Have a blade reforged in a new form') | za szerokich: 0` (px z wbudowanej tabeli, moze sie roznic o 1)
  (oczekiwane: WLACZONE, wpietych 15 = 10 kuznia + 1 "Put the work aside" + 4 Qohor, za szerokich 0; po scaleniu z innymi galeziami najszerszy moze sie zmienic - wazne "za szerokich: 0").
  Brak `ERROR ShortLabels.Audit`.
- Na oko (Jeff albo reczny test): wies -> "Invest in the district's works" (wiersze w jednej linii, podpowiedz z danymi), wybor czesci -> kwoty;
  miasto -> "Take lodgings at an inn" (3 opcje w jednej linii, podpowiedz z rozbiciem kosztow); kuznia -> "Work the forge" i "The mending bench".
- Specjalny plik/flaga: nie.
