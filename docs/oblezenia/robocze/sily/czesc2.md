
Co z tego widac [P, ta sama tabela]:
- **Start (d2)** druzyny sa male (zimny start: 849-3022 ludzi na krolestwo), do d91 rosna 3-8 razy. Zalogi rosna z 47 235 (d1) do 92 002 (d361), potem spadaja do 65 611 (d728) [P] A, linie "Zalogi: przyrost bez werbunku (169c)".
- **Polnoc** ma najwiecej twierdz (22 -> 28) i najwiecej ludzi w druzynach przez caly bieg; jej udzial w wojsku swiata: 7.7 % (d2) -> 9.5 % (d91) -> 9.9 % (d364) -> 13.2 % (d546) -> 15.6 % (d637) -> 12.2 % (d729) [P] T `balans-krolestw.csv`, kolumna ludzie_partie_pct_swiata. Czlowiekodni w druzynach za caly bieg: Polnoc 7.33 mln, Reach 6.09, Baratheon KP 5.74, Dorzecze 5.49, Stormlands 3.48 (do d536), Dolina 3.44, Smocza Skala 3.34, Zelazne Wyspy 3.12, Dorne 3.06, Targaryen 2.12 [P] T `budzet-rodow.csv` (suma ludzi_partie), wyliczenie `doplaty-wobec-zoldu.tsv`.
- **Na glowe mieszkanca** wojska jest najwiecej tam, gdzie ludzi malo: Zelazne Wyspy 1707 / 100 tys., Smocza Skala 329, Polnoc 240-352, Dorne 488-659 (do d364); najmniej w Reach (106-124) i u Baratheonow KP (147-190) - liczba druzyn idzie za liczba rodow i twierdz z ROT, nie za ludnoscia.
- **Milicja\* na twierdze**: Reach 495-553, Baratheon KP 246-467, Dorzecze 253-330, Polnoc 183-198, Dorne 136-180, Zelazne Wyspy 121-152 (rozdz. 5.3 - regula GL4).
- **Zapasc w pokoju**: Dorne d182 8010 -> d728 409 ludzi w druzynach, Dolina d364 7683 -> d728 785, Targaryen d529 4343 -> d728 572, Wolne Miasta d182 23 371 -> d728 3162 (rozdz. 2.3).
- **fin21 (drugi bieg dwuletni)** daje ten sam obraz: Reach 16 -> 22 twierdz, Dorne 16 -> 13, Stormlands 13 -> 0, Polnoc 14.8 % wojska swiata, Dorne 70 i Dolina 25 ludzi w druzynach na koniec, 0 odpartych szturmow [P] `Logs\2026-10-13_21-14-13\balans-krolestw.csv`, wiersze 109564.

### 2.2 Pulap zoldu, wypelnienie druzyn, kiesy (budzet rodow 166)

[P] T `budzet-rodow.csv`, suma rodow krolestwa danego dnia (kolumny pulap, ludzi_partie, limit_partii, kiesa_glowy, wojna); wyliczenie `robocze\sily\budzet-krolestwa.tsv`.

| krolestwo | d91: druzyny / limit (wyp.) / pulap zl/d | d364 | d529 | d728 |
|---|---|---|---|---|
| Polnoc | 10 440 / 14 237 (0.73) / 69 899 | 10 785 / 13 624 (0.79) / 105 176 | 10 567 / 14 280 (0.74) / 118 593 | 9 170 / 13 921 (0.66) / 79 766 |
| Reach | 8 602 / 12 382 (0.69) / 98 145 | 9 900 / 13 203 (0.75) / 183 697 | pokoj: 3 350 / 6 179 (0.54) / 31 203 | 10 544 / 13 181 (0.80) / 232 460 |
| Dorne | 6 710 / 12 147 (0.55) / 60 429 | 4 545 / 10 942 (0.42) / 57 483 | 4 341 / 11 961 (0.36) / 32 972 | pokoj: 390 / 7 214 (0.05) / 7 925 |
| Baratheon KP | 9 509 / 12 883 (0.74) / 69 865 | 9 373 / 11 571 (0.81) / 174 133 | 6 302 / 10 976 (0.57) / 79 857 | 7 060 / 13 194 (0.54) / 112 260 |
| Dorzecze | 7 427 / 10 895 (0.68) / 71 097 | 7 570 / 12 049 (0.63) / 118 204 | 8 320 / 12 330 (0.67) / 220 061 | 7 425 / 11 365 (0.65) / 84 237 |
| Stormlands | 6 225 / 9 712 (0.64) / 53 144 | 5 349 / 8 711 (0.61) / 44 023 | 8 650 / 10 184 (0.85) / 114 184 | - (zniklo d536) |
| Dolina | 5 699 / 9 125 (0.62) / 77 205 | 7 674 / 9 967 (0.77) / 74 792 | pokoj: 2 008 / 5 199 (0.39) / 25 082 | pokoj: 747 / 5 199 (0.14) / 12 914 |
| Smocza Skala | 4 308 / 7 346 (0.59) / 41 520 | 3 870 / 8 633 (0.45) / 30 280 | 4 204 / 7 513 (0.56) / 53 593 | 9 193 / 14 195 (0.65) / 196 739 |
| Zelazne Wyspy | 4 545 / 8 828 (0.51) / 36 348 | 3 608 / 7 785 (0.46) / 28 377 | 3 199 / 7 409 (0.43) / 28 381 | 6 628 / 8 391 (0.79) / 156 559 |
| Targaryen (Dany) | 2 434 / 4 538 (0.54) / 56 950 | 3 261 / 6 319 (0.52) / 84 870 | 4 351 / 6 308 (0.69) / 95 314 | pokoj: 565 / 2 937 (0.19) / 14 883 |

- Dorne ma najwiecej rodow (41) i limit druzyn podobny do Reach, ale pulap zoldu 3.2 razy mniejszy (d364: 57 483 wobec 183 697) i wypelnienie 0.36-0.42 w wojnie [P].
- Rody Dorne, ktore stracily twierdze, maja pulap przy zerze: Yronwood (stracil Yronwood d324) pulap 186 zl/d, kiesa 110, 14 ludzi w druzynie przy limicie 602; Dayne (Starfall d48) pulap 525, 28 ludzi przy limicie 1097 [P] T `budzet-rodow.csv`, dzien 109199 (d364).
- Pulap w wojnie liczy sie z dochodu rodu D (0.28 x D) i skrzyni wojennej - Z.1 [P] A, linia "Budzet rodow (166): dzien 109564" ("pulap pokoju razem ... (0.28 D + 0.40 x skrzynia wojenna)"). Wiecej ludzi i wiecej zlota startowego (rozdz. 5.2) = wyzszy D = wyzszy pulap.

### 2.3 Wojsko w pokoju i zwolnienia z budzetu

- Krolestwa w pokoju na koniec biegu: 126 rodow, 5718 ludzi w druzynach, 11 062 w zalogach [P] A, linia "Budzet rodow (166): dzien 109564". Z.1 (wojsko w pokoju) jest czynne w fin30 (ta sama linia: "pokoj (Z.1)").
- Zwolnieni przez budzet (rod ponad 1.10 x pulap) do wsi: rok 1 - 47 171 (z druzyn 41 814, z zalog 5 357); rok 2 - 125 059 (z druzyn 75 873, **z zalog 49 186**) [P] A, linie "Budzet rodow (166)", pole "zwolnieni: do wsi". Zalogi swiata w tym czasie: 92 091 (d391) -> 64 016 (d601) [P] A, "Zalogi: przyrost".
- Bieg fin30 to kod "bez pieniedzy roku 2" (STAN-PRAC 14.10 15:41) - B4 (GarrisonPeaceShare) nie wystepuje w logu fin30 (0 trafien "GarrisonPeaceShare") [P].

