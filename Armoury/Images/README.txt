Obrazki okien opowiesci (m103-sceny) - gra czyta je z Modules\Armoury\Images przy KAZDYM pokazaniu okna (bez przebudowy DLL).

  Prolog\prolog_01.png ... prolog_23.png   - 23 sceny prologu 298 (opisy i prompty: docs/PROMPTY-OBRAZKI-PROLOG-2026-10-21.md)
  ROT\rot_<KlasaROT>_<n>.png               - okna wydarzen Realm of Thrones, np. rot_NedEvent_0.png (egzekucja Neda); log Armoury
                                             (linia "Obrazki (m103): okno ROT ... brak pliku ...") podaje nazwe pliku dla kazdego okna
  Kronika\kronika_<id>.png                 - Kronika Westeros (pozniej: cala ksiazka, potem serial)

Rozmiar 1024x576 (ramka okna ROT), PNG albo JPG. Pliki przygotowuje narzedzie (sprawdza, kadruje do 16:9, zmniejsza, md5):
    python -I tools/obrazki_okien.py            (zrodlo: assets/prolog, assets/rot, assets/kronika; wynik: ten katalog)
Wgranie: wgraj-v5.ps1 i bieg-bitwa.ps1 kopiuja ten katalog rekursywnie (*.png, *.jpg, *.jpeg).
Plik zly albo wyzerowany na dysku = okno bierze obrazek ROT, a log Armoury ma linie "ZLY PLIK".
