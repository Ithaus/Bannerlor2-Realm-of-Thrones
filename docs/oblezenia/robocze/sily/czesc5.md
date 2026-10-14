
## 6. Gdzie byly armie obroncow

### 6.1 Krolewska Przystan (d530)

| Co | Pomiar |
|---|---|
| wojny Baratheonow KP | z Polnoca (wymuszona przez ROT od d10, pokoj d487 i ta sama doba nowa wojna), z Dorzeczem (ROT, od d131), ze Smocza Skala (od d404) - 3 wrogow [P] A, linie "Wojna i pokoj (E7/0)"; S, "KRONIKA WOJEN" |
| oblezenie | od d516 (spichlerz: oblezona = 1 od wiersza d516), decyzja o szturmie d524: Robb Stark, 18 druzyn, 4484 ludzi; sila 7700 / 1556 [P] A linia 204696 |
| zaloga / milicja\* | d523: 541 / 348; d529: 271 / 234; zywnosc 24 125, bez glodu [P] T `ludzie-regiony.csv`, `spichlerze-regiony.csv`, town_EW4 |
| bitwy d530 | SiegeOutside 4484 vs 1117 (obrona 5 druzyn; zabici obrony 651) -> SiegeOutside 3006 vs 227 -> szturm 2994 vs 92 [P] T `bitwy.log` linie 36830, 36845, 36855 |
| druzyny KP w tym czasie | 6426 ludzi (d529) wobec 10 711 Polnocy; armia Robba = 42 % wojska Polnocy [P] `balans-krolestw.csv` |
| gdzie walczyli lordowie KP d516-d530 | 59 bitew druzyn lordow: region Riverrun 42 (39 wygranych - wsie, karawany i male druzyny Dorzecza), Krolewska Przystan 7 (najwiekszy oddzial Boros Blount, 6 druzyn, 1323 ludzi d519; Joffrey 260 ludzi d525 - przeciw karawanom i bandom), Wayfarer's Rest 3, Acorn Hall 3, Willow Wood 2, Stoney Sept 1, Rosby 1 [P] T `bitwy.log` linie "Bitwa: H3", wyliczenie `gdzie-armie.txt` |
| ROT | wojna KP - Polnoc wymuszana przez ROT (ROTStorylineWars.EnforceWars) [P] A d10, d487 |

Wniosek z liczb: glowne sily KP staly w Dorzeczu i bily male cele; pod Krolewska Przystania nie bylo odsieczy, ktora moglaby przeciwstawic sie 18 druzynom (najwiekszy oddzial KP w regionie: 1323 ludzi d519).

### 6.2 Dorne, gdy Reach je bral (d48-d429)

- Wojna Reach - Dorne wymuszona przez ROT od d23 (pokoj d155 i nowa wojna tej samej doby), pokoj dopiero d437 po 282 dobach; Dorne mialo w tym czasie 1 wroga (od d404 takze Dorzecze, wezwanie do wojny) [P] A, "Wojna i pokoj (E7/0)" d23, d155, d437, d404.
- Wojsko w polu przy kazdym zdobyciu (Reach / Dorne, ludzi w druzynach): Starfall d48 7999 / 6626, Sandstone d98 8558 / 6836, Blackmont d149 7958 / 6490, Kingsgrave d244 10 081 / 6965, Skyreach d273 8914 / 4963, Yronwood d324 10 086 / 4508, Hellholt d377 10 512 / 4591, Wyl d429 7360 / 4907 - przewaga Reach 1.2-2.3 razy [P] `zdobycia-krolestwa-wojny.tsv` (z `balans-krolestw.csv`).
- Odsiecze Dorne byly i wygrywaly: Sandstone d75 (Dorne 43 druzyny, 5744 ludzi, wygrana), Blackmont d106 (31 druzyn, 4497, wygrana), Skyreach d257 (wypad, 31 druzyn, 4399, wygrana); Reach wracal i bral te same twierdze (Sandstone d98, Blackmont d149, Skyreach d273) [P] `bitwy-pod-murami.tsv`.
- W oknach oblezen druzyny lordow Dorne bily glownie bandy w swoich ziemiach malymi oddzialami (najwiekszy oddzial zwykle 85-462 ludzi; wyjatki: Skyreach d262-273 2007, Yronwood d307-324 2085, Wyl d414-429 1050) [P] `gdzie-armie.txt`.
- Pulap i kiesy: rozdz. 2.2 i 5.2 - Dorne ma 41 rodow, ale najmniejszy pulap zoldu wsrod duzych krolestw Westeros i wypelnia druzyny w 36-42 % w wojnie.

## 7. Pytania Jeffa

### 7.1 Jak zniknely Stormlands (d536) bez utraty zamkow w oblezeniach

- To wydarzenie fabularne ROT **RenlyDeathEvent**: popup "Influence of Melisandre" (start d520), po 14 dobach "A Shadow's Deed" [P] S linie 12326, 12629; S "FABULA: ... ostatni start: 520".
- Co robi ROT [K] `robocze\sily\dekompilacja\RenlyDeathEvent.cs`: warunek - co najmniej 120 dob od startu kampanii (:50); po 14 dobach od startu (:89) `ExecuteRenly` (:106): Storm's End i Smocza Skala, jesli Renly'ego, do Stannisa (:112-118), rod Renly'ego zniszczony (:120), Renly zabity, rod Buckler do Westerlands = Baratheon KP (:130), **wszystkie pozostale rody Stormlands przechodza do Smoczej Skaly** (:136), krolestwo Stormlands zniszczone (:145).
- Data wedlug naszego CrashScribe: os fabuly ROT 120 dob x FabulaTimeScale 4.33 = doba 519-520 [K] `zb\int\CrashScribe\src\Fabula.cs:42`, `Config.cs:31`; [P] S "Fabula: wojny fabularne x4.33".
- Skutek [P]: d536 16 twierdz Stormlands do Smoczej Skaly (Storm's End, Cape Wrath, Fellwood, Stonedance, Sweetport - Stannis; Evenfall Hall, Sharp Point - Tarth; Griffin's Roost, Weeping Town, Haystack Hall, Greenstone, Rain House, Mistwood, Blackhaven, Stonehelm, Harvest Hall), Bronzegate (Buckler) do Baratheon KP [P] `zmiany-krolestwa-osad.tsv`. Smocza Skala: 5 -> 21 twierdz, druzyny 4410 -> 5518, zalogi 2468 -> 7054 (d529 -> d546) [P] `balans-krolestw.csv`. Upadek krolestwa: "upadek bez szturmu i bez pokoju (smierc wladcy, bunt, inne)" [P] A linia 209469, "KROLESTWO ZNISZCZONE" linia 209470.
- Te same Stormlands przed d536 same zdobyly 5 twierdz Smoczej Skaly (Cape Wrath, Fellwood, Sweetport, Stonedance, Sharp Point) - wracaja do Stannisa razem z rodami.
- fin21: Stormlands tez 13 -> 0 [P] `Logs\2026-10-13_21-14-13\balans-krolestw.csv`.

### 7.2 Jak Harroway przeszedl do Polnocy

- W tabelach **nie przeszedl**: Harroway (ROT_town4) przez caly bieg fin30 nalezy do Dorzecza, rod Tully (Blackfish) - jedna para (krolestwo, rod) od d1 do d728; tak samo w fin21 [P] T `ludzie-regiony.csv` i `kasy-miast.csv`, ROT_town4; `Logs\2026-10-13_21-14-13\ludzie-regiony.csv`. Brak go w zdobyciach i w zmianach wlasciciela bez oblezenia.
- Do Polnocy w tej okolicy przeszly: Harrenhal (d128, fabula ROT), potem Krolewska Przystan, Rosby, Duskendale (d530-d559) [P].
- Kolory map w plikach: Dorzecze (ROT) 1f527e / c22628, Polnoc (gra) 2D3F1D [P] `ROT-Content\ModuleData\ROT_spkingdoms.xml`, `SandBox\ModuleData\spkingdoms.xml`. Co Jeff zobaczyl na mapie - nie wiem (rozdz. 9).

### 7.3 Kto trzyma Krolewska Przystan na koniec

- **Rod Bolton (Polnoc)** od zdobycia do konca biegu (wiersz d530 ma juz Bolton; d728: zaloga 531, milicja\* 283) [P] T `ludzie-regiony.csv` town_EW4. Rod zdobywcy (Stark) nie zdazyl pojawic sie w wierszu dnia.
- Tak samo wszystkie zdobycze Polnocy (najpierw doba u Starkow, potem Bolton): Harrenhal (Stark d128 -> Bolton d129), Rosby (d540 -> d541), Duskendale (d559 -> d560), Lonely Light (d611 -> d612) [P]. Bolton: 1 twierdza na starcie -> 6 na koniec; jego zalogi 385 -> 2914 ludzi, zold zalog 1210 -> 10 761 zl/d, kiesa 1.13 mln (d565) -> 171 tys. (d728) [P] T `budzet-rodow.csv`, rod Bolton.
- Kto przydziela: decyzja krolestwa o lennie (SettlementClaimantDecision) z latka BK - kandydaci = rody z prawem do lenna, ocena = BK `CalculateHeroFiefScore`, poparcie zmniejszane, gdy rod ma ponad limit domeny [K] `bkfull\BannerKings.Patches.Government\FiefOwnerPatches.cs:18-59` (kandydaci, ocena) i `:70` (poparcie). Dlaczego za kazdym razem Bolton - nie liczone (rozdz. 9).
- Widok Jeffa (herb Boltonow nad Krolewska Przystania) zgadza sie z danymi [P].

### 7.4 Harrenhal d128

Szturm fabularny ROT: popupy "Roose Bolton Strikes a Deal", "Roose Bolton Besieges Harrenhal", "A Northern Stronghold in the South" [P] S linie 3591, 4326, 4541. Bitwa: Roose Bolton 52 druzyny, 9708 ludzi vs Harrenhal 5 druzyn, 6545 ludzi - jedyny szturm blisko 1.5 : 1 [P] T `bitwy.log` linia 7453.

