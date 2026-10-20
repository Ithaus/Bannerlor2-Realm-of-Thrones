# Uprzaz UW - test zawieszenia GC na malej metodzie z latka Harmony

Konsola .NET Framework 4.8 x64 z 0Harmony.dll z gry (skopiuj do lib/ z
Modules\Bannerlord.Harmony\bin\Win64_Shipping_Client). Repair.cs to kopia rdzenia
naprawy z CrashScribe/src/UnwindRepair.cs.

Budowa: dotnet build -c Release. Uruchomienie przez zewnetrznego straznika (gdy GC
zakleszcza proces, wszystkie watki zarzadzane staja - straznik musi byc osobnym procesem):

  python -I straz.py <harness.exe> <wariant> <sekundy> <watki_alokujace> "" <MB_wypelnienia>

Warianty: A = pusta metoda wirtualna z latka (uklad kodu jak w grze, wypelnienie 64 MB) - ZAWIESZA;
B = metoda nie-tycia z latka - nie; C = mala metoda BEZ latki (nasza regula siec-bez-malych) - nie;
D = A + naprawa UnwindRepair - nie. Wynik 19.10: A 3/3 zawieszenie < 15 s, B/C/D czysto (B 300 s,
C 600 s, D 11 min przy 1,6 mln GC gen0).
