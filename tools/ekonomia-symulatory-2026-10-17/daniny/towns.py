import re, os
base = os.path.dirname(os.path.abspath(__file__))
P = [
 ("kasy miast stan","Przeplywy osad (kasy miast)", r"stan (\d+)"),
 ("kasy miast zmiana","Przeplywy osad (kasy miast)", r"zmiana ([-+]\d+)"),
 ("taborom wsi","Przeplywy osad (kasy miast)", r"zaplata taborom wsi (-?\d+)"),
 ("lud za towar","Przeplywy osad (kasy miast)", r"lud za towar \(E5/B1\) ([-+]\d+)"),
 ("GiveGold netto","Przeplywy osad (kasy miast)", r"przelewy gry \(GiveGoldAction\) ([-+]\d+)"),
 ("GG lordowie","Przeplywy osad (kasy miast)", r"lordowie (\+\d+/-\d+)"),
 ("GG notable","Przeplywy osad (kasy miast)", r"notable (\+\d+/-\d+)"),
 ("GG karawany","Przeplywy osad (kasy miast)", r"karawany (\+\d+/-\d+)"),
 ("zold garnizonow","Przeplywy osad (kasy miast)", r"zold garnizonow (\+\d+)"),
 ("sakiewki zycie","Przeplywy osad (kasy miast)", r"sakiewki ludzi - zycie w miastach (\+\d+)"),
 ("zakupy sprzetu AI","Przeplywy osad (kasy miast)", r"zakupy sprzetu AI (\+\d+)"),
 ("warsztaty zbrojne","Przeplywy osad (kasy miast)", r"warsztaty zbrojne (\+\d+/-\d+)"),
 ("cena sprawiedliwa","Przeplywy osad (kasy miast)", r"cena sprawiedliwa\) ([-+]\d+)"),
 ("rzemieslnicy BK","Przeplywy osad (kasy miast)", r"z ich kapitalu ([-+]\d+)"),
 ("renty(zawor pan)","Przeplywy osad (kasy miast)", r"tick dobowy -?\d+ \[P\] \(renty (-?\d+)"),
 ("korona danina","Przeplywy osad (kasy miast)", r"korona \(danina, clo, mennica\) (-?\d+)"),
 ("paser","Przeplywy osad (kasy miast)", r"paser band \(skup lupu\) (-?\d+)"),
 ("bandy zycie","Przeplywy osad (kasy miast)", r"bandy i kryjowki \(zycie w miastach\) ([-+]?\d+)"),
 ("wywoz kupcow","Przeplywy osad (kasy miast)", r"uprzaz\) ([-+]?\d+)"),
 ("dwor 166","Przeplywy osad (kasy miast)", r"sakiewki zwolnionych do kies wsi ([-+]?\d+)"),
 ("zawor korona","Przeplywy osad (kasy miast)", r"skarbce krolestw, E5/A1\) (-?\d+)"),
 ("markietani","Przeplywy osad (kasy miast)", r"kasy miast i kiesy wsi ([-+]?\d+)"),
 ("kiesa ludu B1","Przeplywy osad (kasy miast)", r"kiesy ludu i wsi\) (-?\d+)"),
 ("zawor dla ludu","Przeplywy osad (kasy miast)", r"-> kiesy ludu (-\d+)"),
 ("KS kalibracja","Przeplywy osad (kasy miast)", r"\(kasy miast -> kiesy ludu\) (-?\d+)"),
 ("KS-H kupcy","Przeplywy osad (kasy miast)", r"zwroty ([-+]?\d+)"),
 ("reszta R","Przeplywy osad (kasy miast)", r"tickow\) ([-+]\d+) \[R\]"),
 ("zamki stan","Przeplywy osad (kasy zamkow)", r"stan (\d+)"),
 ("zamki zawor pan","Przeplywy osad (kasy zamkow)", r"kasy zamkow -> panowie\) (-?\d+)"),
 ("zamki zawor korona","Przeplywy osad (kasy zamkow)", r"skarbce krolestw, 114\) (-?\d+)"),
 ("zamki dwor 166","Przeplywy osad (kasy zamkow)", r"sakiewki zwolnionych do kies wsi ([-+]?\d+)"),
 ("zamki zold garn","Przeplywy osad (kasy zamkow)", r"zold garnizonow (\+\d+)"),
 ("wsie stan","Przeplywy osad (kiesy wsi)", r"stan (\d+)"),
 ("wsie zmiana","Przeplywy osad (kiesy wsi)", r"zmiana ([-+]\d+)"),
 ("ludu purses","Kiesa ludu:", r"\| kiesy (\d+ \(W \d+ / R \d+ / P \d+\))"),
 ("zapas dni","Kiesa ludu:", r"zapas dni (W [\d.]+ / R [\d.]+ / P [\d.]+)"),
 ("lud zaplacil","Kiesa ludu:", r"zaplacone przez lud miast (\d+)"),
 ("dochod ludu z zew","Kiesa ludu:", r"dochod ludu miast z zewnatrz i z kas (\d+)"),
 ("robocizna","Kiesa ludu:", r"robocizna (\d+) \(w tym"),
 ("obrot P","Kiesa ludu:", r"obrot P (\d+)"),
 ("doplata 35","Kiesa ludu:", r"doplata do 35% (\d+)"),
 ("czynsz","Kiesa ludu:", r"czynsz (\d+) \(czynsz"),
 ("czynsz gruntowy","Kiesa ludu:", r"E3-G4 (\d+)\)"),
 ("podatki panu","Kiesa ludu:", r"podatki panu: (W \d+ / R \d+ / P \d+)"),
 ("cech","Kiesa ludu:", r"cech (\d+)"),
 ("farma gmin","Kiesa ludu:", r"farma gmin (\d+)"),
 ("akcyza","Kiesa ludu:", r"akcyza do gmin (\d+)"),
 ("P1 danina","Kiesa ludu:", r"danina z oszczednosci do korony \(P1\) (\d+)"),
 ("lokata W","Kiesa ludu:", r"lokata \(kiesa W ponad zapas dni\) (\d+)"),
 ("kiesa2","Podatek od nadwyzki ludu (kiesa2)", r"pobrano (\d+) z"),
 ("kiesa2 do glow","Podatek od nadwyzki ludu (kiesa2)", r"do glow rodow (\d+)"),
 ("guard korony m+z","Podatek od nadwyzki ludu (kiesa2)", r"do skarbcow koron z miast i zamkow (\d+)"),
 ("guard mnoznik","Straznik obiegu", r"mnoznik ([\d.]+) \("),
 ("wsie hoard","Podatek od nadwyzki wsi", r"pobrano (\d+): do glow rodow (\d+)"),
 ("wsie hoard korona","Podatek od nadwyzki wsi", r"do skarbcow koron (\d+) \|"),
 ("dziesiecina","Dziesiecina", r"razem (\d+) = wsie (\d+)"),
 ("myto","Myto targowe", r"pobrano (\d+) = pan"),
 ("notable do kas","Notable (E5/A3)", r"nadwyzka do kas (\d+)"),
 ("notable z kas","Notable (E5/A3)", r"brak z kas (\d+)"),
 ("notable uciete","Notable (E5/A3)", r"uciete (\d+) \(kasa"),
 ("notable kiesy","Obieg: notable i karawany", r"kiesy razem (\d+)"),
 ("notable z aktywow","Obieg: notable i karawany", r"wyplata zgloszona jako z niczego \+(\d+)"),
 ("karawany zysk wlasc","Obieg: notable i karawany", r"\(E3-T2, przelew kiesa karawany -> wlasciciel, nie z niczego\) (\d+)"),
 ("doplaty notabli do karawan","Obieg: notable i karawany", r"doplaty notabli do 5000 \+(\d+)"),
 ("majatki BK","Majatki BK", r"placi kasa miasta\) (\d+)"),
 ("KS-H kupione zl","Kupcy zywnosci", r"kupione \d+ szt. za (\d+) zl"),
 ("KS-H kasa za mala","Kupcy zywnosci", r"kasa celu za mala (\d+ szt. \(zamowione \d+\))"),
 ("markietani wydaly","Markietani", r"z sakiewek (\d+)"),
 ("dwor 166 razem","Budzet rodow (166)", r"wplacone do kas siedzib (\d+) \(w tym miasta"),
 ("sprzet niewydany->dwor","Budzet rodow (166)", r"wplacone do kas siedzib (\d+) u \d+ rodow \(miasta"),
 ("pulap/zold","Budzet rodow (166)", r"pulap zoldu razem (\d+), zold naliczony \(partie \+ zalogi \+ karawany\) (\d+)"),
]
for name in ["ks_first","ks_last","base_last","jeff_last","jeff_first"]:
    lines = open(os.path.join(base,"ex",name+".txt"),encoding="utf-8").read().splitlines()
    days = sorted(set(int(x) for l in lines for x in re.findall(r"dzien (\d{6})", l[:120])))
    D = days[0]
    out=[]
    for lab,pref,pat in P:
        v=None
        for l in lines:
            if l[11:].startswith(pref) and ("dzien %d"%D) in l[:200]:
                m=re.search(pat,l)
                if m: v=" ".join(m.groups()); break
        out.append("%s=%s"%(lab,v))
    print("=== %s day %d\n  %s"%(name,D,"\n  ".join(out)))
