# wyciag.py LOG OUT.json - jeden przebieg po logu Armoury, liczby dobowe do rozpoznania towarow 2 (A2/A6/A7)
import sys, re, json
log, outp = sys.argv[1], sys.argv[2]
START = 108836
D = {}
def put(d, k, v):
    if v is None: return
    D.setdefault(d, {})
    if k not in D[d]: D[d][k] = v
def f(p, s, cast=float):
    m = re.search(p, s)
    return cast(m.group(1)) if m else None
ITEMS = {'flax','linen','hides','leather','hardwood','hog','cow','sheep','grain','bread','beer','wine','grape','planks','silver','jewelry','iron','tools','pottery','clay','meat','salt','wool','felt','fur','cotton','velvet','honey','olives','oil','butter','cheese','fish'}
RAW = ['ruda','drewno','skory surowe','skora','len','plotno','welna']
LUX = ['garum','Ink','Dyes','gems','honey','apple','Eggs','fur','gold','Papyrus']
dz = re.compile(r'dzien (\d{6})')
with open(log, encoding='utf-8', errors='replace') as fh:
    for line in fh:
        i = line.find('] ')
        if i < 0: continue
        body = line[i+2:]
        m = dz.search(body[:80])
        if not m: continue
        d = int(m.group(1)) - START
        if body.startswith('Towary: dzien'):
            mm = re.match(r'Towary: dzien \d+ - (\S+) \(', body)
            if not mm or mm.group(1) not in ITEMS: continue
            it = mm.group(1)
            put(d, it+'.zapas', f(r'zapas (\d+)', body))
            put(d, it+'.prod', f(r'produkcja (\d+)', body))
            put(d, it+'.zuz', f(r'zuzycie (\d+)', body))
            for k, p in [('wsie', r'wsie \(zakupy w miescie, E5/B1\) (\d+)'), ('mieszcz', r'mieszczanie (\d+)'), ('wojsko', r'odziez wojska \(150\) (\d+)'),
                         ('warszt', r'warsztaty zbrojne (\d+)'), ('kowale', r'naprawy kowali miasta \(135\) (\d+)'), ('karkup', r'karawany kupily (\d+)'),
                         ('karsprz', r'sprzedaly (\d+)'), ('pos_miasta', r'posiadacze \[miasta (\d+)'), ('pos_karaw', r'karawany (\d+)[,\]]'),
                         ('rzem148', r'rzemioslo miasta \(148\) (\d+)'), ('tannery', r'tannery (\d+)'), ('bk', r'rzemieslnicy BK (\d+)'),
                         ('butcher', r'butcher (\d+)'), ('wsieprod', r'\[wsie (\d+)'), ('budowy', r'budowy[^ ]* (\d+)'), ('warsztaty', r'\[warsztaty (\d+)'),
                         ('linie', r'linie warsztatow (\d+)'), ('bezwyj', r'bez wyjasnienia ([+-]?\d+)')]:
                put(d, it+'.'+k, f(p, body))
        elif body.startswith('Ceny surowcow: dzien'):
            for r in RAW:
                put(d, 'bez.'+r, f(r'(?:- |; )' + r + r' \(wartosc \d+ d\): [^;]*?bez towaru (\d+) miast', body))
                put(d, 'idx.'+r, f(r'(?:- |; )' + r + r' \(wartosc \d+ d\): indeks min/mediana/max [\d.]+/([\d.]+)/', body))
            for r in LUX:
                put(d, 'pusto.'+r, f(r'(?:; |\. )' + re.escape(r) + r' (?:\([^)]*\) )?/[\d.]+ [\d.]+/[\d.]+/[\d.]+, na polkach \d+ szt\., pusto w (\d+) miastach', body))
                put(d, 'budz.'+r, f(r'(?:; |\. )' + re.escape(r) + r' (?:\([^)]*\) )?/[\d.]+ [\d.]+/[\d.]+/[\d.]+, na polkach \d+ szt\., pusto w \d+ miastach, budzet mieszczan (\d+) d', body))
        elif body.startswith('Rzemioslo miasta (148): dzien'):
            put(d, 'r148.plotno', f(r'len -> plotno: zrobiono (\d+)', body)); put(d, 'r148.plotno_miast', f(r'len -> plotno: zrobiono \d+, zuzyto \d+ lnu \(wedle proporcji [\d.]+\) w (\d+) miastach', body))
            put(d, 'r148.skora', f(r'skory -> skora: zrobiono (\d+)', body)); put(d, 'r148.skora_miast', f(r'skory -> skora: zrobiono \d+, zuzyto \d+ skor \(wedle proporcji [\d.]+\) w (\d+) miastach', body))
            m2 = re.search(r'len -> plotno (\d+)/(\d+)/(\d+), welna -> filc (\d+)/(\d+)/(\d+), skory -> skora (\d+)/(\d+)/(\d+)', body)
            if m2:
                g = [int(x) for x in m2.groups()]
                for k, v in zip(['len.bz','len.bs','len.rc','wel.bz','wel.bs','wel.rc','sk.bz','sk.bs','sk.rc'], g): put(d, 'r148.'+k, v)
            put(d, 'r148.rece_proc', f(r'rece: zajete [\d.]+ z [\d.]+ roboczodni \((\d+)%\)', body))
            put(d, 'r148.polka_len', f(r'na polkach miast \[len (\d+)', body)); put(d, 'r148.polka_plotno', f(r'plotno (\d+) \(indeks', body))
            put(d, 'r148.polka_skory', f(r'skory (\d+) \(indeks', body)); put(d, 'r148.polka_skora', f(r'skora (\d+) \(indeks', body))
            put(d, 'r148.idx_skora', f(r'skora \d+ \(indeks ([\d.]+)\)', body))
            put(d, 'r148.wywoz_szt', f(r'tkane tylko dzieki cenie wywozu (\d+) szt', body)); put(d, 'r148.miast_z_lnem', f(r'miast z lnem: z cena wywozu (\d+)', body))
        elif body.startswith('Warsztaty: dzien'):
            put(d, 'w.wyk', f(r'wykonano (\d+) szt', body))
            for c in ['Shield','BodyArmor','HeadArmor','HandArmor','LegArmor','Bow','Crossbow','Thrown']:
                put(d, 'w.'+c, f(r'\b' + c + r' (\d+)[,\]]', body))
            put(d, 'w.bezzysku', f(r'bez zysku (\d+), brak surowca', body)); put(d, 'w.braksur', f(r'brak surowca (\d+) \[', body))
            for k, p in [('ruda', r'\[ruda (\d+)'), ('drewno', r'drewno (\d+), skora'), ('skora', r'skora (\d+), len/welna'), ('lenwel', r'len/welna (\d+)')]:
                put(d, 'w.brak_'+k, f(p, body))
            put(d, 'w.brakzlota', f(r'brak zlota/kupca (\d+)', body)); put(d, 'w.bezczyn', f(r'bezczynne \d+ = ([\d.]+)%', body))
            put(d, 'w.receswiata', f(r'rece swiata (\d+)', body))
            for g in ['krawiec','platnerz','miecznik','siodlarz','lucznik','tarczownik']:
                mm = re.search(g + r' (\d+)/(\d+)', body)
                if mm: put(d, 'w.'+g+'_p', int(mm.group(1))); put(d, 'w.'+g+'_z', int(mm.group(2)))
            put(d, 'w.t56', f(r'zrobiono t5-6: (\d+)', body))
        elif body.startswith('Drewno: dzien'):
            for k, p in [('wsie', r'wsie dopisaly (\d+) ladunkow'), ('las', r'las wsi \(126\) \+(\d+)'), ('warszt', r'warsztaty zbrojne (\d+)'), ('deski', r'\(deski\) (\d+)'),
                         ('budowy', r'budowy (\d+)'), ('strzel', r'strzelarze \(172\) (\d+)'), ('dymarki', r'dymarki przy kopalniach \(214\) (\d+)'), ('razem', r'razem (\d+) \('),
                         ('miasta', r'zapas: miasta (\d+)'), ('bez', r'miast bez towaru (\d+) z')]:
                put(d, 'drewno.'+k, f(p, body))
        elif body.startswith('Strzelarze (172): dzien'):
            put(d, 's.strzaly', f(r'strzaly: zrobiono (\d+) snopow', body)); put(d, 's.ai_bez', f(r'strzaly: [^;]*AI bez towaru (\d+) razy', body))
            put(d, 's.rece', f(r'rece: zajete [\d.]+ z [\d.]+ roboczodni \((\d+)%\)', body)); put(d, 's.bez_strzal', f(r'miast bez strzal (\d+)', body))
        elif body.startswith('Warsztaty towarowe: dzien'):
            for t in ['mines','linen_weavery','tannery','silversmithy','brewery','wine_press','bakery','wood_WorkshopType','velvet_weavery']:
                mm = re.search(r'\b' + t + r' (\d+) \(pracowalo (\d+), cykli (\d+)', body)
                if mm: put(d, 'wt.'+t+'_n', int(mm.group(1))); put(d, 'wt.'+t+'_prac', int(mm.group(2))); put(d, 'wt.'+t+'_cyk', int(mm.group(3)))
        elif body.startswith('Kontrakty surowca (174): dzien'):
            put(d, 'k.woz_plotno', f(r'wozem kupcow \(P3/Z1\): CZYNNE - zawarto \d+ \[[^\]]*plotno (\d+)', body))
            put(d, 'k.kon_plotno', f(r'zawarto \d+ \[ruda \d+, drewno \d+, skora \d+, plotno (\d+)', body))
            put(d, 'k.woz_skora', f(r'wozem kupcow \(P3/Z1\): CZYNNE - zawarto \d+ \[ruda \d+, drewno \d+, skora (\d+)', body))
            put(d, 'k.woz_drewno', f(r'wozem kupcow \(P3/Z1\): CZYNNE - zawarto \d+ \[ruda \d+, drewno (\d+)', body))
            put(d, 'k.bezlnu', f(r'bez lnu (\d+)', body)); put(d, 'k.bezrudy', f(r'miast bez rudy (\d+) z 97', body))
            put(d, 'k.plotno_brakzr', f(r'plotno (\d+) \(brak zrodla', body)); put(d, 'k.skora_brakzr', f(r'skora (\d+) \(brak zrodla', body))
            put(d, 'k.drewno_brakzr', f(r'drewno (\d+) \(brak zrodla', body))
        elif body.startswith('Karawany: dzien'):
            put(d, 'kar.sprz_plotno', f(r'sprzedane miastom z brakiem: [^;]*plotno (\d+)', body)); put(d, 'kar.kup_plotno', f(r'kupione z nadwyzek: [^;]*plotno (\d+)', body))
            put(d, 'kar.sprz_skora', f(r'sprzedane miastom z brakiem: [^;]*\bskora (\d+)', body)); put(d, 'kar.kup_skora', f(r'kupione z nadwyzek: [^;]*\bskora (\d+)', body))
            put(d, 'kar.wstrzym', f(r'sprzedaz wstrzymana rezerwa kasy miasta (\d+)', body))
        elif body.startswith('Karawany (kierunek): dzien'):
            put(d, 'kar.juki_plotno', f(r'w jukach teraz: [^.]*plotno (\d+) w', body)); put(d, 'kar.juki_skory', f(r'w jukach teraz: [^.]*skory surowe (\d+) w', body))
            put(d, 'kar.pl_wjazdy_brak', f(r'plotno: wjazdy \d+, w tym do miasta z brakiem (\d+)', body)); put(d, 'kar.pl_dostalo', f(r'plotno: wjazdy \d+, w tym do miasta z brakiem \d+ \(dostawe dostalo (\d+)', body))
        elif body.startswith('Pokrycie zbrojowni AI (171): dzien'):
            put(d, 'p.korpus_par', f(r'partie rodow AI \(\d+ ludzi\): korpus (\d+)%', body)); put(d, 'p.tarcza_par', f(r'partie rodow AI[^|]*tarcza (\d+)%', body))
            put(d, 'p.helm_par', f(r'partie rodow AI[^|]*helm (\d+)%', body)); put(d, 'p.t3_par', f(r'przedmioty t3\+: korpus (\d+)%', body))
            put(d, 'p.razem', f(r'korpus szczebla ([\d.]+)%', body)); put(d, 'p.cokolwiek', f(r'cokolwiek na tulowiu ([\d.]+)%', body))
            put(d, 'p.ludzie_par', f(r'partie rodow AI \((\d+) ludzi\)', body))
        elif body.startswith('Odziez wojska (150): dzien'):
            put(d, 'o.potrz_skora', f(r'potrzeba narosla: skora ([\d.]+)', body)); put(d, 'o.potrz_plotno', f(r'potrzeba narosla: [^|]*plotno ([\d.]+)', body))
            put(d, 'o.czeka_plotno', f(r'czeka: [^|]*plotno (\d+)', body)); put(d, 'o.czeka_skora', f(r'czeka: skora (\d+)', body))
            put(d, 'o.bez_towaru', f(r'wizyt bez towaru na polce (\d+)', body))
        elif body.startswith('Rzemieslnicy BK (147): dzien'):
            put(d, 'bk.hog', f(r'\bhog (\d+)', body)); put(d, 'bk.cow', f(r'\bcow (\d+)', body)); put(d, 'bk.sheep', f(r'\bsheep (\d+)', body))
            put(d, 'bk.hides', f(r'hides (\d+)/', body)); put(d, 'bk.hides_zost', f(r'hides \d+/(\d+)/', body))
        elif body.startswith('Zima (E4/0): dzien'):
            put(d, 'zima.pora', f(r'pora \(ekonomia\) (\w+)', body, str))
        elif body.startswith('Zbytki ludu (P3-P5): dzien'):
            put(d, 'zb.podz_kup', f(r'podzamcza \(P5\): z nadwyzka \d+, chciane \d+ zl, kupione (\d+) zl', body))
            put(d, 'zb.podz_bez', f(r'podzamcza \(P5\): [^|]*bez towaru (\d+)', body))
            put(d, 'zb.zamki_oferta', f(r'zamki w konsumpcji zamku \(P3 bez P5\): oferta (\d+)', body))
        elif body.startswith('Zadowolenie BK (E5/0): dzien'):
            put(d, 'zad.line', body[:600])
json.dump(D, open(outp, 'w'))
print(len(D), 'dob')
