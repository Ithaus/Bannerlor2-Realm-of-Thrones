# RAPORT NOCNY 08/09.10.2026 (sesja 7016f733) - wersja robocza, uzupelniana do rana

Zgoda Jeffa na te noc: "Tak, wgraj sam" - wgrywane TYLKO to, co przeszlo autotest bez bledow, zawsze z kopia poprzedniej wersji; zapisy Jeffa nietkniete.

## 1. Co jest W GRZE (stan na ok. 20:10 zegara komputera)

| Wgranie | Kiedy | Armoury | CrashScribe | Co doszlo |
|---|---|---|---|---|
| przed noca | - | c01a54ba | 11fa0214 | (160 + 161) |
| 1 | 19:07 | 28a7456e | aff275de | 170 BetterEconomy domkniete, T1 obozy 0:00-6:00, T2 kalendarz Innych, T3 metal napraw, T4 dezerterzy do puli, T5 rodzina splaca rate Banku, T6 miara marszu (log), T7 mlyny na brzegu + "held by" w dymku wioski |
| 2 | 19:33 | 3bf72dfd | cfb33950 | T8 krainy (papirus/las wedlug klimatu, 23 wsie z uprawa niezgodna z klimatem podmienione, bawelna wyrownana), T2b Inni bez dosypki z niczego |
| 3 | 20:08 | 04d1cc99 | cfb33950 | 169 KSIEGA OBIEGU (sam log) |

Kopie kazdej poprzedniej wersji: obok plikow (*.bak-2026-10-09-przed-nocN) i D:\Backup-Bannerlord\wgrane\2026-10-09-nocN-przed.
Armoury.json i CrashScribe.settings.xml Jeffa bez zmian (nowe ustawienia dzialaja z wartosci domyslnych kodu).

## 2. Testy (autotest, nowa kampania 40 dob, + wczytanie zapisu z doby 362)

- 170 sam, T2 sam, T1 sam, stos S1, S1 zapis, wgranie 1, S2 + zapis, 169 sama, S3 + zapis - wszystkie 40/40 (zapis 8-10 dob), 0 bledow Armoury, 8 znanych bledow startowych CrashScribe.
- Tempo doby bez zmian: 13.0-13.5 s (nowa kampania), ok. 25 s (zapis z doby 362).

## 3. Zablokowane przez zabezpieczenia Claude Code (do decyzji Jeffa)

- Skrypt BetterEconomy (13 kluczy, wariant b - zbrojownia zamknieta wszystkim): powershell -NoProfile -ExecutionPolicy Bypass -File tools\bee\zamknij-ujscia-bee.ps1 -ListaZFundamentu
  (gra zamknieta; kopia ustawien jest w D:\Backup-Bannerlord\bee-2026-10-08\). Bez niego paczka 170 zamyka akcje gracza i bierne zrodla, ale klucze AI/swiata BEE sa otwarte (6/19).
- Proba zimy (zmiana domyslnych w probnej kopii) i proba drog na mapie (plik danych w folderze gry na czas testu) - gotowe, nieuruchomione.

## 4. W toku (uzupelnie)

- H3 przegrani uchodza, 171 zbrojenie zalog + 172 strzaly, 169b poprawki pomiaru, projekt KIESA LUDU, test 120 dob.
