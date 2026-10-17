import re, sys, json, statistics
from collections import defaultdict

N = r'(-?\d+)'
# (line prefix, metric name, regex)
PAT = [
 ('Pieniadz swiata: dzien', 'kasy_miast', r'kasy miast ' + N),
 ('Pieniadz swiata: dzien', 'kasy_zamkow', r'kasy zamkow ' + N),
 ('Pieniadz swiata: dzien', 'kiesy_wsi', r'kiesy wsi ' + N),
 ('Pieniadz swiata: dzien', 'glowy_rodow', r'glowy rodow ' + N),
 ('Pieniadz swiata: dzien', 'pozostali_lordowie', r'pozostali lordowie ' + N),
 ('Pieniadz swiata: dzien', 'notable', r'notable ' + N),
 ('Pieniadz swiata: dzien', 'skarbce', r'skarbce krolestw ' + N),
 ('Pieniadz swiata: dzien', 'sakiewki_ludzi', r'sakiewki ludzi ' + N),
 ('Pieniadz swiata: dzien', 'bank', r'Bank Zelazny ' + N),
 ('Pieniadz swiata: dzien', 'kiesy_ludu_miast', r'kiesy ludu \(E5/B1\): miasta ' + N),
 ('Pieniadz swiata: dzien', 'kiesy_podzamczy', r'podzamcza ' + N),
 ('Pieniadz swiata: dzien', 'kapital_warsztatow', r'kapital warsztatow ' + N),
 ('Pieniadz swiata: dzien', 'karawany', r'karawany ' + N),
 ('Pieniadz swiata: dzien', 'swiat', r'razem ' + N),
 ('Korona: wplywy dnia', 'k_wplywy', r'wplywy dnia ' + N + r' \(nasze'),
 ('Korona: wplywy dnia', 'k_nasze', r'1/9 prawa trzecich \(178\) ' + N),
 ('Korona: wplywy dnia', 'k_184', r'w tym podatek wojenny ludu \(184\) ' + N),
 ('Korona: wplywy dnia', 'k_P1', r'danina z oszczednosci ludu \(P1\) ' + N),
 ('Korona: wplywy dnia', 'k_inne', r'splata dlugu wobec korony ' + N + r'\)'),
 ('Korona: wplywy dnia', 'k_zapas', r'zapasu ponad 500000 ' + N),
 ('Korona: wplywy dnia', 'k_do_wydania', r'do wydania ' + N),
 ('Korona: wplywy dnia', 'k_rata186', r'rata kredytu Banku \(186\) ' + N),
 ('Korona: wplywy dnia', 'k_dary', r'dary \(182\) ' + N),
 ('Korona: wplywy dnia', 'k_reparacje', r'raty reparacji ' + N),
 ('Korona: wplywy dnia', 'k_kontrakty', r'kontrakty najemnikow \(185\) ' + N),
 ('Korona: wplywy dnia', 'k_wezwania', r'wezwania sojusznikow do wojny \(168\) ' + N),
 ('Korona: wplywy dnia', 'k_zwrot', r'zwrot zoldu ' + N + r' \(nalezny'),
 ('Korona: wplywy dnia', 'k_zwrot_nal', r'zwrot zoldu -?\d+ \(nalezny ' + N),
 ('Korona: wplywy dnia', 'k_zwrot_kred', r'w tym z kredytu Banku \(186\) ' + N),
 ('Korona: wplywy dnia', 'k_renty', r'renty \(180\) ' + N),
 ('Korona: wplywy dnia', 'k_W5', r'\(W5, przed rentami\) ' + N),
 ('Korona: wplywy dnia', 'k_M1', r'\(M1\) ' + N),
 ('Korona: wplywy dnia', 'k_M3', r'\(M3, z reszty wplywow przed rentami\) ' + N),
 ('Korona: wplywy dnia', 'k_dwor', r'\(Crown Court Goods tak\) ' + N),
 ('Korona: wplywy dnia', 'k_W5b', r'\(W5b, poza wplywami dnia - z zapasu\) ' + N),
 ('Korona: wplywy dnia', 'k_zostalo', r'zostalo z wplywow dnia w skarbcach ' + N),
 ('danina wojenna z kas osad', 'lev_danina', r'danina wojenna z kas osad ' + N),
 ('danina wojenna z kas osad', 'lev_clo', r'clo od handlu miast ' + N),
 ('danina wojenna z kas osad', 'lev_mennica', r'mennica ' + N),
 ('danina wojenna z kas osad', 'lev_monopole', r'monopole ' + N),
 ('powinnosci wasali', 'dues', r'powinnosci wasali ' + N),
 ('Korona: dzien', 'zw_zold_zapl', r'% z ' + N + r' zaplaconego zoldu'),
 ('Korona: dzien', 'zw_kred', r'z kredytu Banku \(186, brak zwrotu - kapital -> skarbiec -> rody\) ' + N),
 ('Korona: dzien', 'zw_niedopl', r'niedoplata ' + N + r'; w skarbcach'),
 ('Korona: dzien', 'zw_zold_pokoj', r'zold krolestw w pokoju ' + N),
 ('Korona: dzien', 'zw_zold_najem', r'najemnikow ' + N + r'\.'),
 ('Korona: dzien', 'zw_cut_own', r'we wlasnych miastach rodu ' + N),
 ('Korona: niedoplata 28', 'niedopl28', r'swiat ([\d.]+)%'),
 ('Korona: podatek wojenny ludu', 't184', r'pobrano ' + N + r' zl \(ludzie'),
 ('Korona: podatek wojenny ludu', 't184_s_avg', r's krolestw w wojnie \(srednia, max\): ([\d.]+)%'),
 ('Kasy miast (169c)', 'zawor', r'zawor: zeszlo ' + N),
 ('Kasy miast (169c)', 'zawor_pan', r'zawor: zeszlo -?\d+ = panom ' + N),
 ('Kasy miast (169c)', 'zawor_korona', r'\+ koronie ' + N),
 ('Kasy miast (169c)', 'renta_nalezna', r'renta nalezna od ludzi \(dawny pulap, tylko liczba\) ' + N),
 ('Kasy miast (169c)', 'lud_zaplacil_kasom', r'lud zaplacil kasom \(E5/B1\) ' + N),
 ('Kasy miast (169c)', 'udzial_zysku_dzis', r'dzis ([\d.]+)% \(pobrano'),
 ('Kasy miast (169c)', 'udzial_pobrano', r'\(pobrano ' + N + ' z doplywu netto'),
 ('Kasy miast (169c)', 'udzial_doplyw', r'z doplywu netto ' + N),
 ('Kasy miast (169c)', 'zapas_kupcow', r'zapas kupcow ' + N + ', ponad'),
 ('Kasy miast (169c)', 'ponad_zapasem', r'zapas kupcow -?\d+, ponad zapasem ' + N),
 ('Zawor zamkow', 'zz_panom', r'zawor: ' + N + ' do panow'),
 ('Zawor zamkow', 'zz_podzamcze', r'czesc podzamcza do kies podzamczy \(E5/B1\) ' + N),
 ('Zawor zamkow', 'zz_korona', r'skarbcom krolestw ' + N),
 ('Prawa panskie', 'pp_lud_miast', r'od zarobku ludu miast ' + N),
 ('Prawa panskie', 'pp_podzamcza', r'podzamcza ' + N),
 ('Prawa panskie', 'pp_farma', r'farma gmin ' + N),
 ('Prawa panskie', 'pp_wsie_stale', r'oplaty stale wsi ' + N),
 ('Prawa panskie', 'pp_utarg_wsi_bk', r'utargu: z ' + N),
 ('Prawa panskie', 'pp_utarg_zostalo', r'zostalo wsiom ' + N),
 ('Gotowka wsi', 'gw_wplynelo', r'wplynelo ' + N),
 ('Gotowka wsi', 'gw_pan_licznik', r'licznik podatku wsi ' + N),
 ('Gotowka wsi', 'gw_pan_renta', r'\+ renta ' + N),
 ('Gotowka wsi', 'gw_majatki', r'majatki BK ' + N + r' \('),
 ('Gotowka wsi', 'gw_korona', r'korona ' + N + r' \('),
 ('Gotowka wsi', 'gw_W7', r'\(W7\) ' + N),
 ('Gotowka wsi', 'gw_wies', r'wies \(zostalo w kiesie\) ' + N),
 ('Podatek od nadwyzki wsi', 'pnw', r'pobrano ' + N),
 ('Podatek od nadwyzki wsi', 'pnw_glowy', r'do glow rodow ' + N),
 ('Podatek od nadwyzki wsi', 'pnw_kasy_miast', r'do kas miast ' + N),
 ('Podatek od nadwyzki wsi', 'pnw_kasy_zamkow', r'do kas zamkow ' + N),
 ('Podatek od nadwyzki wsi', 'pnw_korony', r'do skarbcow koron ' + N),
 ('Podatek od nadwyzki wsi', 'pnw_kiesy_przed', r'kiesy wsi przed / po ' + N),
 ('Podatek od nadwyzki wsi', 'pnw_poziom', r'poziom normalny ' + N),
 ('Podatek od nadwyzki ludu', 'pnl', r'pobrano ' + N + ' z'),
 ('Podatek od nadwyzki ludu', 'pnl_glowy', r'do glow rodow ' + N),
 ('Podatek od nadwyzki ludu', 'pnl_kasy_miast', r'do kas miast ' + N),
 ('Podatek od nadwyzki ludu', 'pnl_kasy_zamkow', r'do kas zamkow ' + N),
 ('Podatek od nadwyzki ludu', 'pnl_korony', r'do skarbcow koron z miast i zamkow ' + N),
 ('Podatek od nadwyzki ludu', 'pnl_stawka', r'stawka ([\d.]+)%/d'),
 ('Danina z oszczednosci', 'P1', r'pobrano ' + N + ' zl z'),
 ('Danina z oszczednosci', 'P1_W', r'pobrano -?\d+ zl z \d+ osad \(miasta \d+, zamki \d+\): W ' + N),
 ('Danina z oszczednosci', 'P1_podz', r'podzamcza ' + N + r'; wolan'),
 ('Danina z oszczednosci', 'P1_nadw_po', r'razem -?\d+ / ' + N),
 ('Myto targowe', 'myto', r'pobrano ' + N + r' ='),
 ('Myto targowe', 'myto_glowy', r'do glow rodow ' + N),
 ('Myto targowe', 'myto_korony', r'do zapasu koron ' + N),
 ('Myto targowe', 'myto_wozy', r'utarg netto -?\d+, myto ' + N),
 ('Majatki BK', 'bk_majatki', r'udzial z utargu od wczoraj \(AccumulateTradeTax, placi kasa miasta\) ' + N),
 ('Utarg wsi', 'uw_oddane', r'oddane ' + N + r' = pan'),
 ('Utarg wsi', 'uw_pan', r'pan \(licznik podatku\) ' + N),
 ('Utarg wsi', 'uw_majatki', r'wlasciciele majatkow BK ' + N),
 ('Utarg wsi', 'uw_kiesa_bk', r'kiesa wsi od BK ' + N),
 ('Utarg wsi', 'uw_bez_odb', r'bez odbiorcy ' + N + r'; z tego'),
 ('D staly', 'D_staly', r'mediana -?\d+, 10% -?\d+, 90% -?\d+, razem ' + N),
 ('D staly', 'D169', r'D169 tych rodow razem ' + N),
 ('D staly', 'D_ziemia', r'czesci D \(swiat, srednio na dobe\): ziemia ' + N),
 ('D staly', 'D_ziemia_wsiBK', r'\(podatek wsi BK ' + N),
 ('D staly', 'D_ziemia_renta_zawor', r'renta wsi i zawor miast bez wlasnych ' + N),
 ('D staly', 'D_ziemia_cla', r'cla i podatek miast ' + N + ', bez'),
 ('D staly', 'D_ziemia_zawor_zamkow', r'zawor zamkow bez wlasnych ' + N),
 ('D staly', 'D_korona', r'\), korona ' + N),
 ('D staly', 'D_kontrakt', r'kontrakt ' + N + ', majatek'),
 ('D staly', 'D_wlasne', r'poza D: wlasne ' + N),
 ('D staly', 'D_jednorazowe', r'jednorazowe ' + N + r' \(w tym podwojne'),
 ('D staly', 'Dd_podatek_wsi', r'linie modelu - podatek wsi ' + N),
 ('D staly', 'Dd_renta_wsi', r'inne -?\d+; renta wsi ' + N),
 ('D staly', 'Dd_zawor_miast', r'zawor miast ' + N + r' \(wlasne'),
 ('D staly', 'Dd_zawor_miast_wl', r'zawor miast -?\d+ \(wlasne ' + N),
 ('D staly', 'Dd_zawor_zamkow', r'zawor zamkow ' + N + r' \(wlasne'),
 ('D staly', 'Dd_zwrot', r'zwrot korony ' + N),
 ('D staly', 'Dd_renty180', r'renty korony \(180\) ' + N),
 ('D staly', 'Dd_mennica', r'mennica i monopole ' + N),
 ('D staly', 'Dd_cla_miast', r'cla i podatek miast ' + N + ', polityki'),
 ('Budzet rodow', 'bud_D', r'90% -?\d+, razem ' + N),
 ('Budzet rodow', 'bud_saldo_minus', r'ujemne u \d+, razem ' + N),
 ('Budzet rodow', 'bud_saldo_minus_n', r'ujemne u ' + N),
 ('Budzet rodow', 'bud_pulap', r'pulap zoldu wg planu budzetu razem ' + N),
 ('Budzet rodow', 'bud_zold', r'zold naliczony \(partie \+ zalogi\) ' + N),
 ('Budzet rodow', 'bud_ponizej_R', r'ponizej rezerwy wojny R: ' + N),
 ('Budzet rodow', 'bud_zalegly', r'zalegly zold: ' + N),
 ('Budzet rodow', 'bud_glowy_5000', r'glowy < 5000: ' + N),
 ('Budzet rodow', 'bud_rody', r'rody AI ' + N),
 ('Pieniadz swiata (rody - przyczyny)', 'zold_partii', r'linia Obieg\) ' + N),
 ('Pieniadz swiata (rody - przyczyny)', 'zold_zalog', r', zalog ' + N),
 ('Pieniadz swiata (rody - przyczyny)', 'zold_karawan', r'karawan lordow ' + N),
 ('Pieniadz swiata (rody - przyczyny)', 'bk_podatek_bogatych', r'podatek BK od bogatych do skarbcow ' + N),
 ('Kredyt korony', 'kk_pozyczono', r'pozyczono dzis ' + N),
 ('Kredyt korony', 'kk_splacono', r'splacono ' + N + ' zl'),
 ('Kredyt korony', 'kk_odsetki', r'odsetki narosle ' + N),
 ('Kredyt korony', 'kk_oplaty', r'oplaty ' + N),
 ('Kredyt korony', 'kk_dlug', r'dlug razem ' + N),
 ('Kredyt korony', 'kk_dlug_n', r'dlug razem -?\d+ u ' + N),
 ('Kredyt korony', 'kk_zaleglosc', r'w zaleglosci ' + N),
 ('Kredyt korony', 'kk_kapital', r'Bank: kapital ' + N),
 ('Kredyt korony', 'kk_od_pocz_poz', r'od poczatku: pozyczono ' + N),
 ('Kredyt korony', 'kk_od_pocz_spl', r'od poczatku: pozyczono -?\d+, splacono ' + N),
 ('Kredyt korony', 'kk_stracil', r'razem ' + N + ', splata ze skarbcow'),
 ('Pozyczki koron', 'pk_zawarte', r'zawarte ' + N + ' zl'),
 ('Pozyczki koron', 'pk_splaty', r'razem ' + N + r' \(do kas miast'),
 ('Pozyczki koron', 'pk_dlug', r'dlug koron u miast ' + N),
 ('Pozyczki koron', 'pk_umorzone', r'niesplacone \(umorzone po 90 dobach w pokoju\) ' + N),
 ('Pozyczki koron', 'pk_reserve', r'dzis ' + N + ' z -?\d+ zl czesci korony'),
 ('Pozyczki od ludu', 'pl_zawarte', r'\): ' + N + ' zl u'),
 ('Pozyczki od ludu', 'pl_odsetki', r'dzis: odsetki ' + N),
 ('Pozyczki od ludu', 'pl_splacone', r'splacone ' + N),
 ('Pozyczki od ludu', 'pl_umorzone', r'niesplacone \(umorzone po 90 dobach\) ' + N),
 ('Pozyczki od ludu', 'pl_dlug', r'dlug rodow u ludu ' + N),
 ('Pozyczki od ludu', 'pl_dlug_n', r'dlug rodow u ludu -?\d+ \(rodow ' + N),
 ('Pozyczki od ludu', 'pl_bank', r'dlug rodow w Banku ' + N),
 ('Pozyczki od ludu', 'pl_kandydaci', r'kandydaci \(wojna, kiesa rodziny ponizej rezerwy\) ' + N),
 ('Pozyczki od ludu', 'pl_bez_nadwyzki', r'lud lenn bez nadwyzki ' + N),
 ('IronBank: dzien', 'ib_nowe', r'nowe pozyczki ' + N),
 ('IronBank: dzien', 'ib_nowe_zl', r'nowe pozyczki -?\d+ \(' + N),
 ('IronBank: dzien', 'ib_splaty_zl', r'splaty -?\d+ \(' + N),
 ('IronBank: dzien', 'ib_spoznienia', r'spoznienia ' + N),
 ('IronBank: dzien', 'ib_bankructwa', r'bankructwa dzis ' + N),
 ('IronBank: dzien', 'ib_dluznikow', r'dluznikow ' + N),
 ('IronBank: dzien', 'ib_dlug', r'dlug razem ' + N),
 ('IronBank: dzien', 'ib_kapital', r'kapital Banku ' + N),
 ('Dlugi (168)', 'dl_kredyt', r'dluznicy: kredyt ' + N),
 ('Dlugi (168)', 'dl_zaleglosc', r'\), zaleglosc ' + N),
 ('Dlugi (168)', 'dl_zajecie', r'zaleglosc \d+, zajecie ' + N),
 ('Dlugi (168)', 'dl_okupy', r'okupy ' + N + r' \|'),
 ('Dlugi (168)', 'dl_KW', r'wyplacone ' + N + ' zl u'),
 ('Dlugi (168)', 'dl_na_limicie', r'na limicie ' + N),
 ('Doplata wedlug potrzeby', 'w5_garnek', r'garnek ' + N + r' \(danina'),
 ('Doplata wedlug potrzeby', 'w5_potrzeba', r'potrzeba razem ' + N),
 ('Doplata wedlug potrzeby', 'w5_wyplacono', r'wyplacono ' + N + r' \(rodow'),
 ('Doplata wedlug potrzeby', 'w5b', r'wyplacono ze skarbca ' + N),
 ('Doplata wedlug potrzeby', 'w5_niepokryte', r'niepokryte po wszystkich zrodlach \(danina, skarbiec, tarczowe\) ' + N),
 ('Doplata wedlug potrzeby', 'w5_niepokryte_k', r'niepokryte po wszystkich zrodlach \(danina, skarbiec, tarczowe\) -?\d+ w ' + N),
 ('Doplata wedlug potrzeby', 'w5_rody_potrzeba', r'rodow z potrzeba ' + N + ' z'),
 ('Renty korony', 'rk_renty', r'= renty ' + N),
 ('Renty korony', 'rk_zero', r'renta 0: ' + N),
 ('Kiesa ludu: dzien', 'kl_zaplacone_miasta', r'zaplacone przez lud miast ' + N),
 ('Kiesa ludu: dzien', 'kl_dochod_zewn', r'dochod ludu miast z zewnatrz i z kas ' + N),
 ('Kiesa ludu: dzien', 'kl_wojsko', r'wojsko ' + N),
 ('Kiesa ludu: dzien', 'kl_robocizna', r'robocizna ' + N + r' \(w tym'),
 ('Kiesa ludu: dzien', 'kl_obrot_P', r'obrot P ' + N),
 ('Kiesa ludu: dzien', 'kl_doplata35', r'doplata do 35% ' + N),
 ('Kiesa ludu: dzien', 'kl_czynsz_gr', r'czynsz gruntowy do panow miast E3-G4 ' + N),
 ('Kiesa ludu: dzien', 'kl_kiesy', r'\| kiesy ' + N + r' \(W'),
 ('Kiesa ludu: dzien', 'kl_wsie_zakupy', r'wsie 571: zakupy ' + N),
 ('Kiesa ludu: dzien', 'kl_wsie_oplaty', r'oplaty stale ' + N),
 ('Dziesiecina', 'tithe', r'razem ' + N),
 ('Dziesiecina', 'tithe_wsie', r'wsie ' + N),
 ('Straznik obiegu', 'sg_mnoznik', r'mnoznik ([\d.]+) \('),
 ('Straznik obiegu', 'sg_korony_proc', r'korony ' + N + r' \(([\d.]+)%'),
 ('Straznik obiegu', 'sg_lud_proc', r'lud -?\d+ \(([\d.]+)%'),
 ('Kupcy zywnosci', 'ks_kupione_zl', r'szt\. za ' + N + ' zl'),
 ('Kupcy zywnosci', 'ks_fracht', r'fracht ' + N),
 ('Kupcy zywnosci', 'ks_kasa_za_mala', r'kasa celu za mala ' + N),
 ('Kupcy zywnosci', 'ks_zamowione', r'\(zamowione ' + N),
 ('Kupcy zywnosci', 'ks_cel', r'cel ' + N),
 ('Stawka wojenna pana', 'W7', r'pobrano ' + N),
 ('Dary koron', 'dary_dothraki', r'Dothrakowie ' + N),
 ('Pompy', 'pompy', r'razem ' + N + ' zl'),
 ('Nadwyzka kas', 'nk_zapas_wojny', r'zapas wojny \(max\(500000, 60 dob sr28 zwrotu\)\) razem ' + N),
 ('Nadwyzka kas', 'nk_ponizej', r'krolestw ze skarbcem ponizej: ' + N),
 ('Nadwyzka kas', 'nk_K3', r'ciecie zwrotu dzis ' + N),
]

DAYRE = re.compile(r'dzien (\d{6})')

def parse(path):
    data = defaultdict(dict)  # day -> metric -> value
    comp = [(p, m, re.compile(r)) for p, m, r in PAT]
    king = defaultdict(lambda: defaultdict(dict))  # day -> kingdom -> metrics
    crown = defaultdict(lambda: defaultdict(dict))
    skre = re.compile(r'Skarbce: dzien (\d+) - (.+?)( \(WOJNA\))?: skarbiec (-?\d+) \(([+-]?\d+)\)(.*)')
    with open(path, encoding='utf-8', errors='replace') as f:
        for line in f:
            if len(line) < 14 or line[0] != '[':
                continue
            body = line[11:]
            if body.startswith('Skarbce: dzien'):
                m = skre.match(body)
                if m:
                    d = int(m.group(1)); k = m.group(2); rest = m.group(6)
                    r = king[d][k]
                    r['war'] = 1 if m.group(3) else 0
                    r['skarbiec'] = int(m.group(4)); r['skarbiec_d'] = int(m.group(5))
                    for nm, rx in (('zwrot', r'zwrot zoldu (\d+)'), ('dlug186', r'dlug w Banku \(186\) (\d+)'), ('krol_kiesa', r'kiesa (-?\d+)'),
                                   ('krol_bilans', r'bilans ([+-]?\d+)'), ('rodow', r'rodow (\d+), kiesy'), ('kiesy', r'kiesy razem (-?\d+)'),
                                   ('biednych', r'biednych \(<1000\) (\d+)'), ('na_minusie', r'na minusie (\d+)'), ('wojsko', r'wojsko rodow (\d+)'),
                                   ('poz186', r'pozyczone dzis (\d+)')):
                        mm = re.search(rx, rest)
                        if mm: r[nm] = int(mm.group(1))
                continue
            if body.startswith('Korona: wplywy dnia (165)'):
                dm0 = DAYRE.search(body); i = body.find('na krolestwo (wplywy+zapas, wydatki): ')
                if dm0 and i > 0:
                    seg = body[i+len('na krolestwo (wplywy+zapas, wydatki): '):]
                    hits = [(mm.start(), mm) for mm in re.finditer(r"(?:^|, )([A-Z][A-Za-z' ,]*?) (\d+)\+(\d+)", seg)]
                    for j, (pos, mm) in enumerate(hits):
                        end = hits[j+1][0] if j+1 < len(hits) else len(seg)
                        chunk = seg[mm.end():end]
                        r = crown[int(dm0.group(1))][mm.group(1).strip(', ')]
                        r['wpl'] = int(mm.group(2)); r['zapas'] = int(mm.group(3))
                        for nm, rx in (('t184', r'\(184 (\d+)\)'), ('P1', r'danina P1 (\d+)'), ('rata186', r'rata186 -(\d+)'), ('kred186', r'kredyt186 \+(\d+)'),
                                       ('dar_out', r'dar -(\d+)'), ('dar_in', r'dar \+(\d+)'), ('kontr', r'kontr\. (\d+)'), ('zwrot', r'zwrot (\d+)/'), ('zwrot_nal', r'zwrot \d+/(\d+)'),
                                       ('renty', r'renty (\d+)'), ('W5', r'doplata W5 (\d+)'), ('M1', r'z rent M1 (\d+)'), ('W5b', r'ze skarbca W5b -(\d+)'), ('M3', r'chleb M3 (\d+)')):
                            m2 = re.search(rx, chunk)
                            r[nm] = int(m2.group(1)) if m2 else 0
            dm = DAYRE.search(body)
            if not dm:
                continue
            day = int(dm.group(1))
            for p, m, rx in comp:
                if body.startswith(p) or (p in body[:120]):
                    mm = rx.search(body)
                    if mm and m not in data[day]:
                        try:
                            data[day][m] = float(mm.group(mm.lastindex if m in ('sg_korony_proc', 'sg_lud_proc') else 1))
                        except Exception:
                            pass
    return data, king, crown

if __name__ == '__main__':
    path, out = sys.argv[1], sys.argv[2]
    data, king, crown = parse(path)
    json.dump({'data': {str(k): v for k, v in data.items()}, 'king': {str(d): v for d, v in king.items()}, 'crown': {str(d): v for d, v in crown.items()}}, open(out, 'w'))
    days = sorted(data)
    print(path, 'days', days[0], days[-1], len(days))
