## m110-flaga - znacznik kryjowki znika po terminie takze po wczytaniu (23.10)

**Problem:** flaga kryjowki (zwiad albo przesluchanie jenca) po wczytaniu zapisu nie schodzila po Hideout Flag Days (3 doby) - stala do recznego odznaczenia.
Jeff 23.10: "popraw ten znacznik". Wykryte w przegladzie scalenia n34 (Z1 soczewki zapisu).

**Przyczyna:** flage (VisualTrackerManager) gra zapisuje razem z licznikiem, a termin jej zdjecia (HideoutSpotter._flagged) zyl tylko w pamieci -
SyncData bylo puste. Po wczytaniu nowa instancja z pustym _flagged nigdy jej nie zdejmowala.

**Zmiana:** klucz HideoutFlagKeepsDeadline (domyslnie TAK; NIE = jak 104). SyncData zapisuje terminy (SaveText arm_hideout_flags: "id,godzina;"),
przy siewie po wczytaniu RestoreFlags oddaje je do _flagged (po terminie - flaga schodzi w najblizszej godzinie). Zapis sprzed m110 (bez klucza): kazda
sledzona kryjowka dostaje Hideout Flag Days od wczytania - flagi, ktore u Jeffa wisza, zejda po 3 dobach. Linia "HideoutSpotter (m110): terminy flag z zapisu".

**Ryzyko / co sprawdzic:** flaga postawiona recznie przez gracza na kryjowce w starym zapisie tez zejdzie po 3 dobach (nie da sie jej odroznic);
nowy klucz zapisu - stara DLL go pomija. Test: wczytanie zapisu z flaga, linia odtworzenia, flaga znika po terminie.

**Status:** DO SPRAWDZENIA.
