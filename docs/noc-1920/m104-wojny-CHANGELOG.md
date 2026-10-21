# m104-wojny - wojny wedlug ksiazki: Essos na kalendarzu, Reach i Dorne z korona, mlode smoki Daenerys

Galaz `w-toku/m104-wojny` (baza 756e685f = m103-sceny: prolog 298 + okna scen + obrazki; WGRANIE 101 + m103-sceny).
Jeff 21.10: "Kod scen zmieniajacych swiat i wojen wedlug ksiazki - rob to teraz" (decyzje "tak, rob to"). Rozpoznanie: wlasne (dekompilacja ROT 8.1.8,
gry 1.4.8) + wyniki rownoleglego toru (journal wf_d4f0c951-09f: "wojny:A", "smoki" - wnioski zgodne, roznice opisane nizej).

## Stan pracy (aktualizowany po kazdym kroku)

| Krok | Co | Stan |
|---|---|---|
| K1 | (1) Essos na kalendarzu: klucz PrologueEssosWarsCalendar, prefiks/finalizer ROTStorylineWar.Enforced (flaga w StoryStartOf), doba Armoury w prologu, linie startu/doby, CanonWarsBehavior (arm_m104), klucze (2)/(3) w Settings | ZROBIONE (cda60892, build Armoury 0, gen_mcm) |
| K2 | (2) Reach i Dorne z korona: Prologue.Crown.cs - po smierci Renly'ego pokoj Reach-Dorne, pokoj i SOJUSZ korona-Reach (przedluzany), pakt Reach-Dorne; po zareczynach Myrcelli (dzien osi 70 x skala) pokoj i pakt korona-Dorne; pilnowane co dobe do najazdu Daenerys (pakty wygaszone) | ZROBIONE (8e0d7fec, build Armoury 0) |
| K3 | (3) mlode smoki Daenerys: Prologue.Dragons.cs - slot konia Daenerys wedlug etapu (jaja/smoczeta bez smoka - kon z jej taboru; mlode dragon_green 75; podrosle dragon_brown 85; dorosle dragon_black 105); start sesji, doba przed DragonFeed, po niewoli; komunikat gracza przy zmianie etapu | ZROBIONE (build Armoury 0) |
| K4 | grupa tools/sprawdz_logi.py m104wojny | DO ZROBIENIA |
| K5 | build 5 modow, gen_mcm, raport | DO ZROBIENIA |
