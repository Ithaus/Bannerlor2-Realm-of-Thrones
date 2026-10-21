# m103-sceny - E4: sceny prologu 298 jako okna ROT, obrazki z plikow (prolog, okna ROT, pozniej Kronika)

Galaz `w-toku/m103-sceny` (baza n30-calosc ed9273fa = WGRANIE 101). Projekt: PROJEKT-PROLOG-298-2026-10-21.md pkt 2 (os dni 0-188, 23 sceny),
PROMPTY-OBRAZKI-PROLOG-2026-10-21.md (obrazki prolog_01..23). Rozpoznanie: 5 agentow + krytyk (journal wf_6b8877cd-0c3), architektura krytyka kroki 1-12.
Jeff 21.10: "I zrob wydarzenia wedlug ksiazki"; o Kronice: "Ale Pamietaj ze ja che wydarzenia z calej ksiazki i potem z serialu jak sie konczy ksiazka aby to
obejmowalo cala fabule" - dlatego okno (ArmStoryEvent) i obrazki (StoryPictures) sa OGOLNE: prolog teraz, Kronika Westeros (cala ksiazka, potem serial) pozniej.

## Stan pracy (aktualizowany po kazdym kroku)

| Krok | Co | Stan |
|---|---|---|
| K1 | Armoury.csproj: referencje ROT.dll i TaleWorlds.TwoDimension.dll (RotLibs / TwoDimLibs: $(GameLibs), zapas katalogi gry; Private=false), libs/README.md | ZROBIONE (build Armoury 0) |
| K2 | StoryPictures (obrazki z plikow jako sprite'y Gauntlet) + ArmStoryEvent / StoryWindow (ogolne okno w wygladzie ROT, zapas ShowInquiry) | w toku |
| K3 | PrologueScenes.Data.cs - 23 sceny (TextObject ARM_PRL_NN_T/U/L), dlugosci linie.py, zlagodzone 118/125/155 | - |
| K4 | Prologue.Scenes.cs - straznik w OnApplicationTick, maska sc=/sk= w zapisie, pomijanie, klucz PrologueScenes | - |
| K5 | RotWindowPictures - obrazki Jeffa w oknach wydarzen ROT (rot_<Klasa>_<n>.png), klucz StoryImagesForRotWindows | - |
| K6 | tools/obrazki_okien.py + skrypty wgrania/testu (katalog Armoury/Images) | - |
| K7 | grupa tools/sprawdz_logi.py m103sceny + testy na logach syntetycznych | - |
