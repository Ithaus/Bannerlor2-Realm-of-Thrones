# m95-nocleg - nocleg we wspolnej izbie (20.10)

**Problem (Jeff 20.10):** "czekanie w miescie kosztuje, no ludzie placa za siebie, a ja albo spie we wlasnym domu, albo spie w gospodzie, docelowo spie w najtanszej
opcji, ale czy ceny sa odpowiednie w gospodzie - 4 denary za nocleg to nie za duzo? Sprawdz, czy stawki sa realne."
**Sprawdzenie:** ceny TownInns (czlowiek / kon / pokoj gracza): wspolna izba 1 / 1 / 3, porzadna karczma 2 / 2 / 8, dobry dom 4 / 3 / 20. Jeff sam z koniem we wspolnej
izbie: pokoj 3 + kon 1 = 4 d. Skala modu: dzien rzemieslnika 3 d (WorkshopWagePerDay), dochod wioski Jeffa ok. 7-10 d/d. Historycznie (Anglia XIV-XV w.): dniowka
robotnika 2-4 pensy, lozko w gospodzie ok. 1 pensa (zwykle we wspolnej izbie, czesto wspolne lozko), siano i owies dla konia 1-2 pensy, osobna izba - luksus.
Wniosek: stawki ludzi i koni sa realne; zawyzony byl tylko "wlasny pokoj" gracza we wspolnej izbie (3 d) - wspolna izba to wlasnie slomiane poslanie wsrod ludzi.
**Zmiana:** klucz Inn Common Room Shared Bed (InnCommonRoomSharedBed, domyslnie TAK): we wspolnej izbie gracz placi za swoje miejsce jak czlowiek (1 d); pokoj tylko
w porzadnej karczmie i dobrym domu. TownInns.RoomOf / RoomWord - rachunek, podpowiedz, opis menu, zajazdy przy drogach (RoadsideInns, RoadsideInnsView), linia logu.
Sam z koniem we wspolnej izbie: 2 d zamiast 4 d. Ludzie i towarzysze bez zmian (placa za siebie). NIE = 3 d jak dotad.
**Ryzyko:** male - tylko cena gracza w jednej jakosci; zloto dalej do kiesy ludu miasta / wsi okregu.
**Status:** DO SPRAWDZENIA (build 0; autotest nie klika noclegu - test reczny).
**Test w grze:** miasto -> Take lodgings at an inn -> Common room: w podpowiedzi "1 d your bed", "You: bed 1 + your horse 1"; zajazd przy drodze - te same ceny.
**Dla Jeffa:** Ceny dla ludzi i koni sa realne (lozko ok. jednej trzeciej dniowki rzemieslnika, jak w sredniowieczu). We wspolnej izbie spisz teraz na slomie razem ze swoimi
ludzmi i placisz jak oni - sam z koniem 2 d zamiast 4 d. Wlasny pokoj zostaje w lepszych karczmach.
