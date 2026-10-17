import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/Armoury/src/StartCalibration.Corridors.cs'
rep(P, [("""    ///  - podaz krainy S_L = produkcja okregow krainy (z polami mieszczan i podaza spoza tabeli), popyt krainy D_L = potrzeby osad krainy + 1:1 wedlug ludzi -
    ///    zjedzone w domu jest po obu stronach, wiec nadwyzka i brak krainy sa te same co na targu;
    ///  - PROBLEM PRZEWOZU na rynek i towar jedzenia: krainy z nadwyzka -> krainy z brakiem, koszt luku = doby wozem bramy (najblizsza para miast obu krain,
    ///    nie w wojnie), luk tylko do 30 dob (trwale) albo 7 dob (psujace sie, KsCore.Perish), przepustowosc przed D z kasy miast importera (polowa kasy
    ///    28 dob na towar w drodze), po D - 1.2 x dowiezione wozami KS-H (najmniej 20 szt./d); najtanszy przeplyw (kolejne najkrotsze sciezki);
    ///  - GRADIENT krainy (cena krainy wobec ceny rynku w dochodzie bloku): importer 1.03 + tau x srednie doby dowozu (tau 0.010/d ladem, 0.005/d morzem), najwyzej 1.5;""",
"""    ///  - podaz krainy S_L = produkcja okregow krainy (z polami mieszczan i podaza spoza tabeli), popyt krainy D_L = popyt ZAPLACONY osad krainy (potrzeby po kroku 8 -
    ///    krok 5 liczy sie po kroku 8; recenzja runda 3) + odbior panow i dworow w miastach krainy + reszta 1:1 wedlug ludzi - zjedzone w domu jest po obu stronach;
    ///  - PROBLEM PRZEWOZU na rynek i towar jedzenia: krainy z nadwyzka -> krainy z brakiem, koszt luku = doby wozem bramy (najblizsza para miast obu krain,
    ///    nie w wojnie; morzem - statkiem), luk tylko do 30 dob (trwale) albo 7 dob (psujace sie, KsCore.Perish), przepustowosc przed D z JEDNEGO budzetu kasy krainy
    ///    importera (15% kasy 28 dob na dobe, polowa kasy na towar w drodze; dzielony na towary wedlug wartosci braku), po D - 1.2 x dowiezione wozami KS-H (najmniej
    ///    20 szt./d; do NKS-24 takze przewoz bez tego sufitu); najtanszy przeplyw (kolejne najkrotsze sciezki);
    ///  - GRADIENT krainy (cena krainy wobec ceny rynku w dochodzie bloku): importer 1.03 + tau x srednie doby dowozu (tau 0.025/d ladem, 0.0025/d morzem), najwyzej 1.5;""")])
