# m95-turniej - okup szranek jako czesc wartosci (20.10)

**Problem (Jeff 20.10, gra na WGRANIU 94):** "jest blad w turniejach, odpadlem w drugiej rundzie, co prawda zaplacilem ponad 1k za przegrana wartosci sprzetu, ale dostalem
w tym samym czasie ponad 8k zlota."
**Dowod (GrandTourney.log sesji 10:24, kopia D:/Backup-Bannerlord/logi-jeffa/2026-10-20):** Saltpans 11:47 - "okno gracza: Ithauser pokonany przez Myles Mooton - wykup,
zaplacone 1763 (7 szt.)"; w pierwszej rundzie gracz pokonal lorda, ktory (R3-04a, TourneyRansom.Settle, toPlayer) zaplacil pelna wartosc konia i zbroi (ok. 8 tys.).
Rozliczenie wszystkich walk naraz po misji. Swiat AI: "wykupione sztuki 138-180 za 328-414 tys. zlota" na dobe, jeden rod do 96 tys./d.
**Przyczyna:** nie blad kodu - regula R3-04a liczyla pelna wartosc sprzetu: jedna wygrana = ok. 2.5 roku dochodu wioski Jeffa (7-10 d/d); duze przeplywy miedzy rodami AI;
silny gracz zarabialby fortune na turniejach.
**Zmiana:** klucz Tourney Ransom Share (TourneyRansomShare, domyslnie 0.25, zakres 0.05-1): okup sztuki = wartosc w obecnym stanie x 1/4 (ShareOf w Stake) - te same
liczby dla gracza i AI, w obie strony (placi gracz, placa gracowi, AI miedzy soba); sztuki przechodza na zwyciezce tylko, gdy nie stac nawet na okup; okno gracza
dopisuje "(the ransom of the lists: a quarter of their worth)". Sprzedaz sztuki na targ (miasto placi) bez zmian - pelna cena przedmiotu. 1 = pelna wartosc jak dotad.
**Ryzyko:** mniej sztuk oddanych (AI czesciej stac na okup) - mniej zamiennikow; przeplywy AI ok. 4x mniejsze (ok. 80-100 tys./d).
**Status:** DO SPRAWDZENIA (build 0). **Test w grze:** turniej z wygrana i przegrana walka - okno okupu ok. 1/4 wartosci; linia "Wykup (R3-04a): dzien" - kwoty ok. 4x nizsze.
**Dla Jeffa:** Wygrana walka w szrankach daje teraz okup rowny 1/4 wartosci konia i zbroi pokonanego (u Ciebie ok. 2 tys. zamiast 8 tys.), a przegrana kosztuje 1/4 Twojego
sprzetu (ok. 440 zamiast 1763). Turniej na tepa bron jest dla honoru - wciaz sie oplaca, ale nie daje fortuny.
