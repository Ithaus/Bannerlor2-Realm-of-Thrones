# Czemu brakuje plotna? (pytanie Jeffa, 14.10) - rozpoznanie, bez zmian kodu

Logi: A = bieg dwuletni `Armoury-2026-10-13_21-14-13.log` (WGRANIE 49 + lup 5b, nowa kampania, 728 dob, d = doba od startu, d1 = dzien 108837),
R = bieg roczny `..._19-21-12.log` (len 883), B0 = 120 dob bez paczki `..._18-04-21.log`, B5 = 120 dob z paczka B (P5) `..._18-49-17.log`.
Kod: wersja w grze (WGRANIE 51), `SC(ae222cb7)\zb\int\Armoury\src`. [P] pomiar, [S] szacunek. Skrypty i wyciagi: `skrypty\`, `tow-*.json` w tym katalogu.

## 0. Dla Jeffa - prostym jezykiem

1. Lnu jest juz duzo (paczka P5 go podwoila) i prawie caly idzie do tkaczy: plotna powstaje ok. 270 sztuk dziennie - tyle, ile zakladal projekt (290).
2. Ale plotno tka sie tylko w ok. 33 miastach, do ktorych len dowoza wsie lniane (82% lnu daje szesc krolestw: Reach, Volantis, King's Landing, Qarth, Braavos, Stormlands). 49 z 97 miast nie ma lnu wcale.
3. W tych 33 miastach mieszczanie wykupuja 2/3 plotna jeszcze tego samego dnia - za "pieniadze na zbytki" (nadwyzke kiesy). Na plotno idzie prawie polowa tej nadwyzki (114 tys. zl dziennie, ok. 4 razy wiecej, niz warte jest cale plotno swiata), bo regula zbytkow przerzuca na plotno udzial drogiej bawelny, aksamitu i futra.
4. Tkacz tka tylko tyle, ile kupi jego wlasne miasto. Dlatego nadwyzki nigdy nie ma, a bez nadwyzki ani karawana, ani woz kupcow, ani kontrakt nie wiezie plotna dalej - 0 sztuk dziennie.
5. Skutek: ok. 80 z 97 miast ma pusta polke. Wojsko chce 90 sztuk dziennie, a dostaje 16 (6%). Zdanie z projektu "wojsko wykupuje polowe plotna swiata" jest bledne - polowe (dwie trzecie) wykupuja mieszczanie miast tkackich.
6. Paczka P5 dala +90 sztuk plotna dziennie, ale 69% z tego zjedli ci sami mieszczanie, wojsko dostalo 4% - stad tylko 87 -> 83 miast bez plotna.
7. W roku 2 przychodzi zima (od doby 459, plon wsi -41%) i plotna jest jeszcze mniej (273 -> 239 dziennie).
8. Najlepsza poprawka: mieszczanie kupuja plotno i skore "na zbytki" dopiero ponad zapas dla wojska i warsztatow i nie drozej niz 1.5 ceny (jak cala reszta zbytkow; wsie juz dzis kupuja tylko ponad 5 sztuk na polce).
9. Druga: tkacz tka tez na wywoz, a kontrakt / woz kupcow wiezie nadwyzke do miast z pusta polka. Trzecia, pozniej: rozwozic sam len do miast bez lnu (ich tkacze stoja w 77-79%), a ilosc lnu podnosic dopiero po pomiarze.

## 1. Lancuch na dobe (bieg dwuletni A; rok 1 = d1-364, rok 2 = d365-728)

| ogniwo | rok 1 | rok 2 | zrodlo |
|---|---:|---:|---|
| len z wsi (36 wsi lnianych, w tym 16 nowych z P5) | 859 | 717 (zima od d459) | [P] Towary flax |
| regulator lnu | x1.30 (sufit) od d112 do konca, zuzycie/produkcja 0.97-1.02 | x1.30 | [P] Regulator wsi |
| len wozami wsi do swojego miasta | 756 | 633 | [P] Towary flax "wozy do osad" |
| len miedzy miastami: woz kupcow / kontrakty / karawany | 41.6 / 0.8 / 5.5 (ok. 5% lnu) | 30.8 / 0.3 / 3.9 | [P] Kontrakty surowca, Towary |
| miast bez lnu (z 97) | 49.7 | 49.5 | [P] Ceny surowcow |
| len przerobiony: rzemioslo miasta (148) / tkalnie notabli / mieszczanie | 723 / 97 / 9 | 621 / 98 / 10 | [P] Towary flax |
| len na polkach miast (koniec doby) | 5 808 sr. (7 568 w d364, indeks 1.06; stos = 15 dni zuzycia) | 4 595 | [P] Rzemioslo miasta, Regulator |
| rzemioslo miasta: miast tkajacych dziennie | 33.0 | 33.0 | [P] |
| rzemioslo miasta: koniec przerobu bez zysku / brak lnu / rece | 32.7 / 62.3 / 2.0 miast | 34.7 / 61.4 / 1.0 | [P] |
| rece rzemiosla zajete | 4 258 z 17 594 roboczodni (24%) | 3 593 z 20 232 (18%) | [P] |
| tkalnie notabli (linen_weavery) | 10 w swiecie, pracuje 7.6, 16.1 cykli/d | 10.9, pracuje 7.4, 16.3 cykli/d | [P] Warsztaty towarowe |
| PLOTNO zrobione: rzemioslo / tkalnie / rzemieslnicy BK | 241 / 32 / 0 = **273** | 207 / 33 / 0 = **239** | [P] Towary linen |
| plotno kupione: mieszczanie | **182 (67%)** | **150 (63%)** | [P] |
| - wsie w miescie (E5/B1, lista P5) | 36 (13%) | 41 (17%) | [P] |
| - warsztaty zbrojne | 38 (14%) | 31 (13%) | [P] |
| - wojsko (Odziez 150) | **16 (6%)** | 16 (7%) | [P] |
| - naprawy kowali / karawany | 0.7 / 0.3 | 0.8 / 0.1 | [P] |
| plotno na polkach swiata (koniec doby) | 93 sr. (0.3 dnia produkcji) | 129 | [P] |
| miast BEZ plotna (koniec doby) | 79.7 (z plotnem 17) | 76.4 (z plotnem 21) | [P] Ceny surowcow |
| indeks ceny plotna (mediana miast) | 8.97 (sufit 10) | 9.38 | [P] |
| wojsko: potrzeba narasta / kupione | 89.6 / 15.8 szt./d | 69.5 / 15.6 | [P] Odziez wojska |
| wojsko: czeka (koniec roku) / przepada (lachmany) | 6 927 / 26.8 szt./d | 3 984 / 38.7 | [P] |
| wojsko: wizyt bez towaru na polce / za chuda sakiewka | 53.8 / 0.6 na dobe | 44.4 / 0.6 | [P] |
| warsztaty zbrojne: cykle stracone na brak lnu/welny | 155 /d | 144 /d | [P] Warsztaty |
| oferta nadwyzki ludu na plotno / kupione z niej | 114 054 zl/d (46% oferty) / 20 039 zl/d | 125 188 (50%) / 18 681 | [P] Kupione z oferty (P1) |
| cena sztuki kupionej przez lud | ok. 110 zl (indeks ok. 1.1 = prog oplacalnosci tkacza) | ok. 125 zl | [S] iloraz zl / szt. |

Porownanie biegow (srednie, d61-119 dla 120-dobowych): plotno B0 144 -> B5 234 szt./d (+90): mieszczanie +62 (69%), wsie +15 (17%), warsztaty zbrojne +8.5 (9%), wojsko +3.5 (4%); miast tkajacych 15.2 -> 25.8; bez lnu 72.3 -> 56.0; **bez plotna 86.9 -> 82.7** [P]. Bieg roczny R (len 883): plotno 277/d, mieszczanie 150, wsie 78, warsztaty 34, wojsko 15, bez plotna 79.4 [P].

Gdzie jest len [P, linia GL1 d364, A:139780]: baza modelu 653 szt./d - The Reach 130, Volantis 116, King's Landing 99, Braavos 76, Qarth 73, Stormlands 55 (razem 549 = 84%), Norvos 31, Polnoc 23, Dolina 13, Pentos 12, Dorzecze 11, Lorath 7, Wolni Ludzie 3, Wyspy 2, Sarnor 2; Dorne, Tyrosh, Myr, Lys, Qohor, Meereen, Ibben - 0.

"Czy len stoi w miastach BEZ tkalni?" - **nie**. Rzemioslo miasta (148) tka w kazdym z 97 miast (A:320). Len lezy (5.8-7.6 tys. szt.) w miastach z tkaczami, ktore konczy dzien "bez zysku": lokalne plotno potanialo do progu tkacza (koszt lnu 3 x 20 zl x 1.06 + praca 6.7 roboczodnia + 25%). To bufor ok. 15 dni, nie martwy towar. Brak zlota w rzemiosle nie wystepuje (polka -> polka). Dla 2.4 nieczynnych tkalni notabli powodu log nie rozbija - nie wiem.

## 2. Przyczyny (z udzialami)

**A. Mieszczanie miast tkackich zjadaja plotno z "oferty zbytkow" - 63-67% plotna swiata, 69% przyrostu z P5 [P].**
Kod: lista dostatku P3 `TownFolk.Luxury.cs:43` (grupa "sukno i odzienie" 55%: felt, cotton, velvet, linen, leather, fur, garment), `:50` plotno i skora "bez sygnalu", `:154` cena graniczna 1.5 sprawdzana TYLKO dla kategorii z sygnalem - plotno i skora jej nie maja, `:155/:161` podzial grupy liczony od nowa po kategoriach, ktore zostaly - bawelna, aksamit, futro, filc sa ponad 1.5 w 84-97 miastach (A:139871: bawelna 88, aksamit 94, futro 97, filc 84 z 97), wiec ich udzial przechodzi na plotno; `:177` dopisek / czesc domowa 0.4 (x2.5). Wynik: plotno 43-53% calej oferty (stara regula BetterIds `TownFolk.Live.cs:41` - 7% wiekszej oferty, 57-194 tys. zl/d - dzialala tak samo, R/B0/B5). Lud oferuje za plotno ok. 4 razy jego wartosc swiatowa [S] i kupuje w 34-40 miastach, w ktorych w chwili konsumpcji lezy plotno (bez wiersza 57-63 z 97), zostawiajac je w 17-21.

**B. Plotno nie wyjezdza z miasta tkalni [P].** Tkacz liczy tylko cene swojego miasta (`TownCrafts.cs:301-303`) i staje "bez zysku" w kazdym miescie z lnem (32.7-34.7 z ok. 35). Polka nie przekracza progu nadwyzki (`CaravanBulk.cs:354-362`), wiec kontrakty 174 i woz kupcow P3/Z1: plotno 0 szt., "brak zrodla z nadwyzka w zasiegu" 95.9-96.4 miast; karawany kupuja 0.1-0.3 szt./d. To B decyduje, ze ok. 60 miast bez lnu nigdy plotna nie widzi.

**C. Len w polowie swiata [P].** 84% lnu w 6 krolestwach, 49 miast bez lnu. Len miedzy miastami: ok. 5% (woz kupcow 31-42 szt./d); kontrakty lnu "brak zrodla z nadwyzka" w 69-76 miastach, bo miasto z lnem trzyma 9-15 dni zuzycia, ponizej progu nadwyzki.

**D. Ilosc i dwie skale [S].** Nawet przy idealnym podziale: wzorzec cywilny 192 + wojsko 70-90 + warsztaty zbrojne 31-38 (i 144-155 cykli/d stojacych na len/welne) = ok. 320-400 szt./d wobec 273 (rok 1) i 239 (rok 2). Wojsko w kilogramach prawdziwych (A:352: 0.69 szt. / 1000 ludzi / d w polu) chce 1/3 plotna swiata, choc to 0.3% ludzi. Rok 2: zima (A:176643, od d459, plon wsi -40.8%) tnie len o 17% i plotno o 12% [P].

Udzial w odpowiedzi "czemu 80 miast i wojsko nie maja plotna": A + B razem - prawie calosc dzisiejszego braku na polkach (dwie trzecie plotna znika lokalnie, reszta tez nie wyjezdza); C - czemu tkalnie sa tylko w polowie miast; D - granica, ktora zostanie po naprawie A+B (ok. 50-130 szt./d [S]).

## 3. Poprawki (od najlepszej; nic nie zmienione)

**1. Zbytki ludu nie kupuja wsadu wojska ponizej zapasu** - `TownFolk.Luxury.cs:154-161` (LadderOffer; to samo w drugim podziale `:344`) i `TownFolk.Live.cs:390-414` (AddOffer, gdy FolkLuxuryList wylaczony). Regula: (a) plotno i skora podlegaja cenie granicznej 1.5 jak reszta listy; (b) udzial kategorii, ktore wypadly ponad 1.5, nie przechodzi na plotno/skore (liczyc je w sumie grupy - ta czesc zostaje w kiesie); (c) z oferty lud bierze tylko sztuki ponad prog nadwyzki miasta (`CaravanBulk.Keep`) - jak wies, ktora kupuje tylko ponad 5 sztuk (`TownFolk.Live.cs:43`). Skutek [S]: uwalnia ok. 150-180 szt./d (rok 1: lud kupowal 182) - wiecej niz potrzeba wojska (70-90); ok. 20 tys. zl/d zostaje w kiesach ludu albo idzie na inne pozycje listy. Obieg zamkniety: nic nie powstaje ani nie znika, zmienia sie tylko kupujacy (kasa kupcow dostaje zamiast od ludu - od sakiewek wojska i kas zamkow). Ryzyko: tkacze miast tkackich traca glownego kupca - cena plotna spadnie, rzemioslo stanie wczesniej "bez zysku", len zostanie na polkach i regulator po 56 dobach moze zejsc z x1.30; lud ludnych miast kupi mniej koszul (wzorzec cywilny), skora - to samo (lud kupowal 5.2 tys. zl/d). Sprawdzic biegiem 120 dob: miast bez plotna d91-120 <= 60, wojsko kupuje >= 50 szt./d, regulator lnu >= 1.2, kiesy ludu bez skoku.

**2. Tkacz tka tez na wywoz** - `TownCrafts.cs:301-303`. Regula: cena wyrobu przy decyzji = wieksza z (cena miasta, cena miasta z otwartym zamowieniem plotna w zasiegu wozu minus koszt drogi), najwyzej do zapasu = prog nadwyzki + zamowienia; nadwyzke wiezie istniejacy kontrakt 174 / woz kupcow P3/Z1 (plotno jest juz na ich liscie; zamowienia czekaja w 96 miastach). Skutek [S]: bufor lnu (7.6 tys. szt. = ok. 2.5 tys. plotna) i caly doplyw ida w plotno, regulator nie tnie lnu, plotno trafia do miast z pusta polka - ile miast, nie wiem (bieg). Obieg zamkniety jak przy rudzie (kasa celu -> kasa zrodla). Ryzyko: bez poprawki 1 lud miasta tkackiego zje i ten zapas; przepustowosc wozow (ruda: 42 tys. szt./rok, plotno potrzebowaloby ok. 30-60 tys. [S]).

**3. Len do miast bez lnu, potem ilosc** - prog nadwyzki lnu (`CaravanBulk.cs:354-362` / MaterialOrders): w miescie, ktorego rzemioslo skonczylo dzien "bez zysku", len ponad ok. 5 dni zuzycia jest nadwyzka - jedzie do 49 miast bez lnu (ich tkacze stoja, rece zajete 18-24%). Ilosc "z rak" (wiecej wsi lnianych w ludnych krainach bez lnu: Polnoc, Dolina, Dorzecze, Dorne) dopiero po pomiarze 1+2; potrzeba [S] +20-60% wobec dzis. Ryzyko: maly zysk wozu na tanim lnie (20 zl), dluzsze drogi; skali wojska (dzielenie odziezy przez ok. 30) nie polecam - furtka z projektu.

## 4. Czego nie wiem

- Ile plotna lezy w ktorym miescie i ktore miasta tkaja - log nie podaje polek wedlug miast (tylko sumy i liczby miast).
- Cena sztuki kupionej przez lud (110-125 zl) - to iloraz zl / sztuk, nie pomiar; czesc zakupu ludu z budzetu gry poza oferta - log jej nie rozdziela (wychodzi blisko zera).
- Ile miast bedzie mialo plotno po poprawkach 1-2 i czy wojsko je kupi (zalezy od tras partii) - tylko bieg.
- Kolejnosc w ticku dobowym miasta (konsumpcja ludu wzgledem rzemiosla) - nie sprawdzalem; wniosek "tego samego dnia" opieram na polkach konca doby (0.3-0.5 dnia produkcji).
- Czemu 2.4 z 10 tkalni notabli nie pracuje.

## 5. Dowody

Linie logu A (bieg dwuletni):
- A:262 Receptury (216): linen_weavery 6 lnu -> 2 plotna. A:278 Stawki towarow (GL5): 16 nowych wsi lnianych, 36 na mapie. A:320 Rzemioslo miasta (148) WLACZONE w 97 miastach: 3 lnu na sztuke, praca 6.7 roboczodnia, +25%, bez zlota. A:352 Odziez wojska (150): plotno 0.69 / 1000 w polu, 0.27 w garnizonie.
- d364: A:139780 GL1 len wedlug krolestw; A:139812 Kontrakty surowca (plotno 0, "brak zrodla z nadwyzka"); A:139871 Zbytki na sucho: linen "bez sygnalu", cotton/velvet/fur/felt ponad 1.50 w 88/94/97/84 miastach; A:139872 tkalnie lnu w 10 miastach (Dragonstone, Highgarden, Selhorys, Meereen, Pentos, Barrowton, Hornvale, Ashford, Pebbleton, Hardhome); A:139936 Rzemioslo: 213 plotna w 35 miastach, koniec 33/61/4, len na polkach 7 568 (indeks 1.06), plotno 185 (indeks 8.90), rece 21%; A:139937 Warsztaty: brak surowca len/welna 139 cykli; A:140021 Towary flax: 943 z wsi, rzemioslo 645, tkalnie 114; A:140034 Towary linen: 253 zrobione, mieszczanie 162, wsie 56, warsztaty zbrojne 36, wojsko 21, przewoz [-]; A:140059 Regulator: flax x1.30, stos 15 dni; A:140136 Odziez: potrzeba 99.5, kupione 5+8+8, czeka 6 958, przepadlo 38.2, wizyt bez towaru 53; A:140253 Kupione z oferty: linen 81 296 / 16 795 / 53 215, 58 z 97 konsumpcji bez wiersza.
- A:176643 Zima od dnia 109295 (d459), 1 316 dni, ciecie plonu wsi 40.8%. A:279366 / A:279379 Towary flax / linen d728.
- Srednie roczne: skrypty `okna.py` (Towary), `rzemioslo.py` (Rzemioslo + Ceny surowcow), `odziez.py`, `oferta.py` (Kupione z oferty), `kontrakty.py`, `tkalnie.py`, `krol_len.py`; plon wsi lnianych `Logs\2026-10-13_21-14-13\plon-wsi.csv` (d448 -> d476: Westeros -35..-50%).

Kod (WGRANIE 51): `TownFolk.Luxury.cs:43, :50, :154-161, :177, :344`; `TownFolk.Live.cs:41-43, :390-414`; `TownCrafts.cs:301-303`; `CaravanBulk.cs:354-362`; `HistoricalPrices.cs:1001-1019` (budzet mieszczan x TownUseLinen 0.4, `Settings.cs:732`); `Settings.cs:1739` (FolkLuxuryList = true); BK `EconomyPatches.cs:501-603, :1085-1100` (konsumpcja: budzet / cena, cala polka do wyczerpania budzetu).
