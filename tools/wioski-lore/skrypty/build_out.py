# Czesc wyjsciowa build_rody.py (wolana na koncu build_rody.py przez exec): role, pewnosc, Dar, Essos, zapis rody.csv
ROYAL = {"Targaryen", "Blackfyre"}
# E7/L P2: rody wymarle przed 298 AC, ktorych strona AWOIAF nie ma w grupie Extinct ani znacznika "X" (wyciag surowe\ nie niesie
# dopiskow "nearly extinct" / "formerly" - nie ma czego przejrzec maszynowo; lista z lore, ponowne pobranie AWOIAF tylko przegladarka):
# Hollard - rod wybity po Buncie Duskendale (277 AC), zamek w ruinie, zyje tylko Dontos (blazen) -> tylko historia, nie rycerz wioski;
# Whent - "nearly extinct": ostatnia jest Lady Shella, Harrenhal odebrany w 298-299 AC (inaczej rycerz "of Harrenhal" w dymku)
EXTINCT_298 = {"Hollard", "Whent"}

def grade(how):
    if how.startswith(("name=castle", "name=town", "alias->castle", "alias->town", "klan ROT")):
        return "A"
    if how.startswith(("name=village", "alias->village")):
        return "A-"
    if how.startswith("mapa"):
        return "B"
    if how.startswith("opis"):
        return "C"
    return "D"

def place_info(x, y):
    d, s_ = nearest(x, y, TC)
    dv, v = nearest(x, y, VIL)
    bnd = byid.get(v["bound"])
    pan = (clanbyid.get(bnd["owner"], {}).get("name", bnd["owner"]) if bnd else "")
    return s_, d, v, pan

out = []
for h in houses:
    c = rot_clan_for(h)
    ext = h["group"] in ("Extinct", "Extinct houses") or h["died"] == "X" or h["house"] in EXTINCT_298
    uw = []
    if h["house"] in EXTINCT_298:
        uw.append("E7/L P2: wymarly przed 298 AC (lore) - tylko historia")
    if c:
        rola = "pan_okregu_ROT"
        home = byid.get(c["home"])
        if home and (h["pos"] is None or math.hypot(home["x"] - h["pos"][0], home["y"] - h["pos"][1]) > 1):
            if h["pos"] is not None and not h["how"].startswith("przy panu"):
                uw.append("lore: " + (h["seat_used"] or "-") + " (" + h["how"] + "), w ROT klan siedzi w " + home["name"])
            h["pos"] = (home["x"], home["y"]); h["how"] = "klan ROT: " + home["name"]
    elif ext:
        rola = "wymarly_historia"
    elif h["house"] in ROYAL:
        rola = "krolewski"
    elif h["group"] == "Great":
        rola = "wielki_rod"
    elif h["group"] == "Knightly":
        rola = "rycerz_z_ziemia"
    elif h["group"] == "Masterly":
        rola = "pomniejszy_rod_master"
    elif h["group"] == "Lordly":
        rola = "pomniejszy_rod_lord"
    else:
        rola = "pomniejszy_rod"
    x = y = ""
    osada = okreg = okreg_pan = osada_kul = dist = ""
    if h["pos"]:
        x, y = h["pos"]
        st, d, v, okreg_pan = place_info(x, y)
        osada = st["name"]; osada_kul = st["culture"]; dist = f"{d:.0f}"
        okreg = v["id"] + "|" + v["name"]
        x = f"{x:.1f}"; y = f"{y:.1f}"
    region = h["region"]
    if osada_kul == "dragonstone" and region == "The Crownlands":
        region = "The Crownlands (Dragonstone)"
    ov = h["ov"]
    para = PARAMOUNT.get(h["region"], "")
    if region.endswith("(Dragonstone)") or ov == "Baratheon of Dragonstone":
        para = "Baratheon of Dragonstone"
    if h["house"] in ("Tully", "Stark", "Arryn", "Lannister", "Tyrell", "Baratheon", "Martell", "Greyjoy"):
        para = h["house"]
    names = " ".join(n for n in h["names"].split() if n not in ("The", "the", "Castle", "of", "Brothers", "brothers", "King's", "Commander"))
    cols = colours(h["arms"])
    g = grade(h["how"])
    if g == "D":
        uw.append("siedziba nieopublikowana / bez opisu - rod swobodny w krainie pana")
    if h["house"] == "Thorne":
        uw.append("w ROT jest klan Thorne (ROTclan_125) - to Ser Alliser w Nocnej Strazy, nie rod z ziemia")
    if h["house"] == "Tollett":
        uw.append("w ROT jest klan Tollett (clan_nord_3) - Nocna Straz (Dolorous Edd), nie Grey Glen")
    out.append(dict(kraina=region, rod="House " + h["house"], ranga=h["group"], rola=rola, siedziba=h["seat_used"],
                    x=x, y=y, osada_rot=osada, odl_do_osady=dist, okreg_rot=okreg, pewnosc=g, dopasowanie=h["how"],
                    pan=ov, lord_paramount=para, pan_okregu_rot=okreg_pan, klan_rot=(c["id"] + " " + c["name"]) if c else "",
                    herb=h["arms"], kolory=";".join(cols), kolory_hex=";".join(HEX[c_] for c_ in cols),
                    imiona=names, znacznik=h["mark"], zrodlo="awoiaf.westeros.org/index.php/House_" + h["house"].replace(" ", "_"),
                    uwagi="; ".join(uw)))

# ---------- Dar Nocnej Strazy (AWOIAF: Gift + strony oficerow) ----------
NW = [
    ("Castle Black", [("Jeor", "Mormont", "Lord Commander"), ("Bowen", "Marsh", "Lord Steward"), ("Othell", "Yarwyck", "First Builder"),
                      ("Alliser", "Thorne", "master-at-arms"), ("Donal", "Noye", "armorer"), ("Jaremy", "Rykker", "ranger"),
                      ("Ottyn", "Wythers", "ranger"), ("Wynton", "Stout", "ranger"), ("Mallador", "Locke", "knight"), ("Endrew", "Tarth", "knight")]),
    ("Shadow Tower", [("Denys", "Mallister", "commander of the Shadow Tower")]),
    ("Eastwatch by the Sea", [("Cotter", "Pyke", "commander of Eastwatch-by-the-Sea")]),
    ("Nightfort", []), ("The Wall", []),
]
for cname, offs in NW:
    st = name_tc[norm(cname)][0]
    _, d, v, okp = place_info(st["x"], st["y"])
    out.append(dict(kraina="The Gift", rod="Night's Watch", ranga="Sworn Brotherhood", rola="pan_okregu_ROT", siedziba=cname,
                    x=f"{st['x']:.1f}", y=f"{st['y']:.1f}", osada_rot=cname, odl_do_osady="0", okreg_rot=v["id"] + "|" + v["name"],
                    pewnosc="A", dopasowanie="name=" + st["kind"], pan="", lord_paramount="Night's Watch (Lord Commander)",
                    pan_okregu_rot=okp, klan_rot=st["owner"] + " " + clanbyid.get(st["owner"], {}).get("name", ""),
                    herb="Plain black shield (brothers give up the heraldry of their houses)", kolory="black", kolory_hex=HEX["black"],
                    imiona=" ".join(f"{a}_{b}({c_.replace(' ', '_')})" for a, b, c_ in offs),
                    znacznik="NW", zrodlo="awoiaf.westeros.org/index.php/Gift ; /Night%27s_Watch ; strony oficerow",
                    uwagi="Bracia nie maja ziemi; wsie Daru (Mole's Town, Queenscrown) trzyma Straz. Rycerz wioski = brat Strazy (zarzadca Daru)."))

# ---------- Essos (krotko) ----------
ESSOS = [
 ("Volantis", "Volantis", "Old Blood (triarchs)", [("Maegyr", "Malaquo"), ("Paenymion", "Doniphos"), ("Vhassar", "Nyessos"), ("Tagaros", "Marqelo"), ("Qhaedar", "Alios"), ("Vaelaros", "Parquello"), ("Staegone", "Belicho")], "Triarch_of_Volantis"),
 ("Pentos", "Pentos", "Magisters (Prince of Pentos)", [("Mopatis", "Illyrio"), ("Haratis", "Gessio"), ("Narratys", "Nevio"), ("Draz", "Rego")], "Pentos"),
 ("Lys", "Lys", "Magisters", [("Rogare", "Lysandro Larra Drazenko Lysaro Lotho Roggerio Moredo"), ("Lohar", "Sharako"), ("Pendaerys", "Silvario Pereno"), ("Dagareon", "Moreo"), ("Orthys", "Matteno"), ("Haen", "Torreo"), ("Bazanne", "Bambarro")], "Lys"),
 ("Tyrosh", "Tyrosh", "Archon", [("Ryndoon", "Racallio"), ("Quaynis", "Collio"), ("Adarys", "Alequo"), ("Uhoris", "Roro"), ("Tumitis", "Moreo")], "Tyrosh"),
 ("Myr", "Myr", "Magisters", [("Drahar", "Craghas")], "Myr"),
 ("Norvos", "Norvos", "Bearded priests / magisters", [("Votyris", "Byan"), ("Golathis", "Doro"), ("Hotah", "Areo")], "Norvos"),
 ("Qohor", "Qohor", "Magisters", [("Mott", "Tobho")], "Qohor"),
 ("Braavos", "Braavos", "Sealord / keyholders", [("Antaryon", "Ferrego"), ("Zalyne", "Uthero"), ("Nhai", "Jogos"), ("Prestayn", "Luco Moredo"), ("Reyaan", "Bessaro"), ("Otherys", "Bellegere Bellenora"), ("Fregar", "Tormo"), ("Otharys", "")], "Keyholders"),
 ("Qarth", "Qarth", "Pureborn / the Thirteen", [("Xhoan Daxos", "Xaro"), ("Xhore", "Xandarro"), ("Votar", "Colloquo")], "Thirteen"),
 ("Meereen", "Meereen", "Great Masters", [("zo Loraq", "Hizdahr"), ("zo Pahl", "Oznak"), ("mo Kandaq", "Skahaz"), ("mo Reznak", "Reznak"), ("Galare", "Galazza Grazdar"), ("zo Ghazeen", ""), ("zo Merreq", ""), ("zo Dhazak", ""), ("zo Naqqan", ""), ("zo Zhak", ""), ("zo Quazzar", ""), ("zo Hazkar", ""), ("zo Uhlez", ""), ("zo Yherizan", ""), ("zo Rhazdar", "")], "Great_Masters"),
 ("Astapor", "Astapor", "Good Masters", [("mo Nakloz", "Kraznys"), ("mo Ullhor", "Grazdan")], "Good_Masters"),
 ("Yunkai", "Yunkai", "Wise Masters", [("mo Eraz", "Grazdan Gorzhak"), ("zo Yunzak", "Yurkhaz"), ("zo Qaggaz", "Yezzan"), ("zo Myraq", "Paezhar"), ("zo Rhaezn", "Chezdhar Maezon Grazdhan"), ("zo Ahlaq", "Ghazdor"), ("zo Zherzyn", "Morghaz"), ("zo Faez", "Faezhar")], "Wise_Masters"),
]
for city, tname, rulers, fams, page in ESSOS:
    st = name_tc[norm(tname)][0]
    _, d, v, okp = place_info(st["x"], st["y"])
    for fam, given in fams:
        key = fam.split()[-1].lower()
        c = rot_clan_by_key.get(key, [None])[0]
        rod = ("House " + fam) if not fam.startswith(("zo ", "mo ")) else "House of " + fam.split()[-1] + " (" + fam + ")"
        out.append(dict(kraina="Essos: " + city, rod=rod, ranga=rulers, rola=("pan_okregu_ROT" if c else "rod_essos"), siedziba=city,
                        x=f"{st['x']:.1f}", y=f"{st['y']:.1f}", osada_rot=city, odl_do_osady="0", okreg_rot=v["id"] + "|" + v["name"],
                        pewnosc="C", dopasowanie="miasto (rod z miasta)", pan=rulers, lord_paramount=rulers, pan_okregu_rot=okp,
                        klan_rot=(c["id"] + " " + c["name"]) if c else "", herb="", kolory="", kolory_hex="", imiona=given, znacznik="ES",
                        zrodlo="awoiaf.westeros.org/index.php/" + page,
                        uwagi="Essos: bez herbow w lore; wsie wokol miasta = posiadlosci (estates) rodow miasta"))

json.dump(out, open(os.path.join(B, "rody_westeros.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=0)
open(os.path.join(B, "kalibracja.txt"), "w", encoding="utf-8").write("\n".join(klog) + "\n")
cols_ = ["kraina", "rod", "ranga", "rola", "siedziba", "x", "y", "osada_rot", "odl_do_osady", "okreg_rot", "pewnosc", "dopasowanie",
         "pan", "lord_paramount", "pan_okregu_rot", "klan_rot", "herb", "kolory", "kolory_hex", "imiona", "znacznik", "zrodlo", "uwagi"]
ORDER = ["The North", "The Gift", "The Riverlands", "The Vale", "The Westerlands", "The Reach", "The Stormlands", "The Crownlands",
         "The Crownlands (Dragonstone)", "Dorne", "The Iron Islands"]
RORD = {"pan_okregu_ROT": 0, "wielki_rod": 0, "pomniejszy_rod_lord": 1, "pomniejszy_rod_master": 1, "rycerz_z_ziemia": 2,
        "pomniejszy_rod": 3, "rod_essos": 3, "krolewski": 4, "wymarly_historia": 5}
out.sort(key=lambda r: (ORDER.index(r["kraina"]) if r["kraina"] in ORDER else 50, r["kraina"], RORD.get(r["rola"], 6), r["rod"]))
with open(os.path.join(L, "rody.csv"), "w", encoding="utf-8-sig", newline="") as f:
    w = csv.DictWriter(f, fieldnames=cols_, delimiter=";")
    w.writeheader()
    for r in out:
        w.writerow(r)
print("\n".join(klog))
print(len(out), Counter(o["rola"] for o in out))
print(Counter(o["pewnosc"] for o in out))
print(Counter(o["kraina"] for o in out))
