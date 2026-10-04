import json, re, collections
a=json.load(open("rot_arms.json"))
# postacie - tylko na POCZATKU id (lub calym slowem jak jon_snow) - domy (stark_, mormont_, dayne_, royce_) to stroje oddzialow, nie unikaty
PEOPLE=["ned_","robb_","jon_snow","jonsnow_","arya_","sansa_","bran_","stannis_","renly_","robert_","joffrey_","cersei_","jaime_","tyrion_","tywin_","loras_","margaery_","mace_","olenna_","oberyn_","dany_","daenerys_","viserys_","drogo_","jorah_","barristan_","euron_","theon_","balon_","ramsay_","roose_","walder_","brienne_","podrick_armor","hound_","sandor_","mountain_","gregor_","melisandre_","davos_","varys_","baelish_","bronn_","ygritte_","tormund_","mance_","nightking_","whitewalker_","white_walker_","rhaegar_","lyanna_","blackfish_","gendry_","bull_helmet","needle","viper_spear","celtigar_axe","westerling_axe","baratheon_hammer","skull_sword","tyrell_sword","bolton_sword"]
LORE=["weirwood","giant_","ravens_teeth","dragonglass","obsidian","val_steel","valyrian_steel","dragonbone","goldenheart","blackfyre","whyt_","koa_sword","ice_sword","longclaw","oathkeeper","widows_wail","ww2_","darksister","heartsbane","lightbringer","brightroar","lady_forlorn","nightfall","red_rain","lamentation","vigilance_sword","assist_sword","truth"]
rows=[]; ids=[]
for i in a:
    id_=i["id"].lower(); why=[]
    if i["merch"]=="false": why.append("poza handlem (ROT)")
    if i["value"]>=10000: why.append("cena wpisana przez autora: %d"%i["value"])
    p=[x for x in PEOPLE if id_.startswith(x) or id_==x.rstrip("_")]
    if p: why.append("postac/imienny: "+p[0].rstrip("_"))
    l=[x for x in LORE if (id_==x or id_.startswith(x) or ("_"+x) in id_ or x in id_ and x.endswith("_"))]
    if l: why.append("legenda/material: "+l[0].rstrip("_"))
    if id_.endswith("_crown"): why.append("korona")
    if why:
        rows.append((i["id"],i["name"].lstrip("}"),i["type"].replace("Crafted:",""),i["culture"] or "-","; ".join(why)))
        ids.append(i["id"])
grp=collections.OrderedDict()
def g(r):
    w=r[4]
    if "legenda" in w: return "1. Legendy i materialy z legend"
    if "korona" in w: return "2. Korony"
    if "postac" in w: return "3. Rynsztunek imiennych postaci"
    if "cena wpisana" in w: return "4. Rzadkie (cena wpisana przez autora ROT)"
    return "5. Poza handlem w ROT (stroje, fryzury, umarli)"
for r in sorted(rows,key=lambda r:(g(r),r[0])): grp.setdefault(g(r),[]).append(r)
out=["# Unikaty Realm of Thrones - spis (2026-10-04)","",
"Jeff 04.10: \"zrob audyt - tabele przedmiotow, ktore dodaje Gra o Tron, i wyklucz przedmioty unikatowe\".",
"Zrodlo: wszystkie `<Item>`/`<CraftedItem>` uzbrojenia w `Modules/ROT*/ModuleData` (%d sztuk). Kryteria unikatu:" % len(a),
"(1) ROT oznacza je `is_merchandise=\"false\"`; (2) autor wpisal recznie cene (>= 10 000 - zwykle przedmioty ROT nie maja ceny, liczy ja gra);",
"(3) id zaczyna sie od imienia postaci (Brienne, Ogar, Gora, Ramsay, Loras, Renly, Rhaegar...); (4) legenda albo material z legend",
"(stal valyrianska, weirwood, smocze szklo, smocza kosc, olbrzymy, Kruczy Zab); (5) korony.",
"NIE sa unikatami: stroje oddzialow rodow (stark_, mormont_, dayne_, royce_, lannister_...), zbroje kultury valyrian (valyrian_plate - stroj kultury),",
"zwykla bron z nazwa krainy (crownlands_halberd, spiked_mace).","",
"Skutek w grze (Armoury `RotUniques.cs` -> `ArmsPricing.IsUnique`): warsztaty ich nie robia, AI ich nie kupuje na targu, wycena historyczna x prestiz (`HistUniquePrestige`).",
"Razem unikatow: %d." % len(rows),""]
for k,v in grp.items():
    out.append("## %s (%d)" % (k,len(v))); out.append("")
    out.append("| id | nazwa | typ | kultura | powod |"); out.append("|---|---|---|---|---|")
    for r in v: out.append("| `%s` | %s | %s | %s | %s |" % r)
    out.append("")
open(r"C:\Users\GAME\Bannerlor2-Realm-of-Thrones\docs\ROT-UNIKATY.md","w",encoding="utf-8").write("\n".join(out))
cs=["using System.Collections.Generic;","","namespace Armoury","{","    /// <summary>","    /// Unikaty Realm of Thrones (spis: docs/ROT-UNIKATY.md, generowany skryptem z ModuleData ROT 04.10). Jeff: \"wyklucz przedmioty","    /// unikatowe\" - legendy, materialy z legend, rynsztunek imiennych postaci, korony, przedmioty z cena wpisana przez autora","    /// i poza handlem. Uzywane przez ArmsPricing.IsUnique: warsztaty ich nie robia, AI nie kupuje, wycena x prestiz.","    /// </summary>","    internal static class RotUniques","    {","        internal static readonly HashSet<string> Ids = new HashSet<string>","        {"]
for i in range(0,len(ids),6): cs.append("            "+", ".join('"%s"'%x for x in ids[i:i+6])+",")
cs+=["        };","    }","}",""]
open(r"C:\Users\GAME\Bannerlor2-Realm-of-Thrones\Armoury\src\RotUniques.cs","w",encoding="utf-8").write("\r\n".join(cs))
print(len(rows)); print({k:len(v) for k,v in grp.items()})
