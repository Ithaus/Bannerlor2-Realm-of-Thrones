import re, sys, os
base = os.path.dirname(os.path.abspath(__file__))
def g(pat, txt, d=None):
    m = re.search(pat, txt)
    return int(m.group(1)) if m else d
for name in ["ks_first","ks_last","base_last","jeff_last","jeff_first"]:
    lines = open(os.path.join(base,"ex",name+".txt"),encoding="utf-8").read().splitlines()
    # take last day blocks: use the later of the two days
    days = sorted(set(int(x) for l in lines for x in re.findall(r"dzien (\d{6})", l[:120])))
    D = days[-1]  # crown lines carry today, ledger carries D-1
    def L(prefix, day):
        for l in lines:
            if prefix in l[:140] and ("dzien %d"%day) in l[:200]: return l
        return ""
    kor_in = L("Korona: wplywy dnia", D); kor_lev = L("Korona: dzien %d - danina"%D, D); kor_due = L("Korona: dzien %d - powinnosci"%D, D)
    kasy = L("Kasy miast (169c)", D-1); zz = L("Zawor zamkow (110)", D-1); pomp = L("Pompy", D-1); dst = L("D staly (169c)", D-1)
    straz = L("Straznik obiegu", D-1); kl = L("Kiesa ludu:", D-1); pw = L("Prawa panskie", D-1); ut = L("Utarg wsi (112)", D-1)
    r = {}
    r['wplywy'] = g(r"wplywy dnia (\d+) \(nasze", kor_in); r['nasze'] = g(r"trzecich \(178\) (\d+)", kor_in); r['inne_BK'] = g(r"splata dlugu wobec korony (\d+)\)", kor_in)
    r['release'] = g(r"1/450 zapasu ponad 500000 (\d+)", kor_in); r['P1'] = g(r"danina z oszczednosci ludu \(P1\) (\d+)", kor_in); r['t184'] = g(r"podatek wojenny ludu \(184\) (\d+)", kor_in)
    r['zwrot'] = g(r"zwrot zoldu (\d+) \(nalezny", kor_in); r['renty'] = g(r"renty \(180\) (\d+)", kor_in); r['W5'] = g(r"\(W5, przed rentami\) (\d+)", kor_in); r['W5b'] = g(r"\(W5b, poza wplywami dnia - z zapasu\) (\d+)", kor_in)
    r['M3'] = g(r"\(M3, z reszty wplywow przed rentami\) (\d+)", kor_in); r['kredyt186'] = g(r"w tym z kredytu Banku \(186\) (\d+)", kor_in)
    r['danina'] = g(r"danina wojenna z kas osad (\d+)", kor_lev); r['clo'] = g(r"clo od handlu miast (\d+)", kor_lev); r['dues'] = g(r"powinnosci wasali (\d+)", kor_due)
    r['valve_town_crown'] = g(r"koronie (\d+) \(\d+ krolestw\)", kasy); r['valve_town_lord'] = g(r"zawor: zeszlo \d+ = panom (\d+)", kasy); r['valve_town_all'] = g(r"zawor: zeszlo (\d+)", kasy)
    r['valve_castle_lord'] = g(r"zawor: (\d+) do panow", zz); r['valve_castle_crown'] = g(r"skarbcom krolestw (\d+) z", zz); r['valve_castle_folk']=g(r"czesc podzamcza do kies podzamczy \(E5/B1\) (\d+)", zz)
    r['vil_crown'] = g(r"korona z utargu wsi (\d+)", pomp)
    r['thirds'] = (r['nasze'] or 0) - sum((r[k] or 0) for k in ['dues','danina','clo','valve_town_crown','valve_castle_crown','t184','P1'])
    r['hoard_crown'] = g(r"do skarbcow koron (\d+) \|", straz); r['hoard_all']=g(r"razem (\d+); do glow", straz); r['hoard_lord']=g(r"razem \d+; do glow rodow (\d+)", straz)
    r['D_ziemia'] = g(r"ziemia (\d+) \(podatek wsi BK", dst); r['D_bkvil'] = g(r"podatek wsi BK (\d+), renta wsi", dst); r['D_rentvalve'] = g(r"renta wsi i zawor miast bez wlasnych (\d+)", dst)
    r['D_cla'] = g(r"cla i podatek miast (\d+), bez osady", dst); r['D_castle']=g(r"zawor zamkow bez wlasnych (\d+)\)", dst); r['D_korona'] = g(r"\), korona (\d+), kontrakt", dst); r['D_kontrakt']=g(r"kontrakt (\d+), majatek", dst); r['D_all']=g(r"razem (\d+); D169", dst)
    r['today_podatek_wsi'] = g(r"linie modelu - podatek wsi (\d+)", dst); r['today_renta_wsi']=g(r"; renta wsi (\d+), zawor miast", dst); r['today_cla']=g(r"cla i podatek miast (\d+), polityki", dst); r['today_polityki']=g(r"polityki krolow (\d+)", dst)
    r['vil_fee'] = g(r"oplaty stale wsi (\d+)", pw); r['folk_tax_lord']=g(r"od zarobku ludu miast (\d+)", pw); r['farm']=g(r"farma gmin (\d+)", pw); r['castlefolk_tax']=g(r"podzamcza (\d+), farma", pw)
    r['util_lord'] = g(r"pan \(licznik podatku\) (\d+)", ut); r['util_estate']=g(r"wlasciciele majatkow BK (\d+)", ut); r['util_vil']=g(r"kiesa wsi od BK (\d+)", ut); r['util_all']=g(r"oddane (\d+) =", ut)
    print(name, D, r)
