"""Spis unikatow ROT (docs/ROT-UNIKATY.md + Armoury/src/RotUniques.cs).
Wymaga rot_arms.json (ekstrakcja <Item>/<CraftedItem> uzbrojenia z Modules/ROT*/ModuleData, z nazwami z jezyka).
Zasada (Jeff 05.10: "Northern Leather Boots to nie jest unikat, to buty z Polnocy - gdyby byly Jon Snow Boots, to bylby
unikat"): o unikacie decyduje NAZWA WIDOCZNA W GRZE, nie id z plikow ROT."""
import json, re, collections, sys
src = sys.argv[1] if len(sys.argv) > 1 else "rot_arms.json"
a = json.load(open(src))
# imiona postaci - jako SLOWA w nazwie widocznej w grze
PEOPLE = ["Ned", "Eddard", "Robb", "Jon Snow", "Arya", "Sansa", "Bran", "Rickon", "Catelyn", "Stannis", "Renly", "Robert",
          "Joffrey", "Tommen", "Cersei", "Jaime", "Tyrion", "Tywin", "Loras", "Margaery", "Mace Tyrell", "Olenna", "Oberyn",
          "Red Viper", "Daenerys", "Danaerys", "Dany", "Viserys", "Drogo", "Jorah", "Barristan", "Euron", "Theon", "Balon",
          "Ramsay", "Roose", "Walder", "Brienne", "Podrick", "Hound", "Sandor", "Mountain", "Gregor", "Clegane", "Melisandre",
          "Davos", "Varys", "Baelish", "Littlefinger", "Bronn", "Ygritte", "Tormund", "Mance", "Night King", "Rhaegar",
          "Lyanna", "Blackfish", "Gendry", "Arthur Dayne", "Aegon", "Missandei", "Grey Worm", "Jeor", "Lysa", "Edmure"]
# legendy i materialy z legend - w nazwie (albo id dla materialu)
LORE_NAME = ["Weirwood", "Giant", "Raven", "Dragonglass", "Obsidian", "Valyrian Steel", "Dragonbone", "Goldenheart",
             "Blackfyre", "Ice", "Longclaw", "Oathkeeper", "Widow's Wail", "Dawn", "Dark Sister", "Heartsbane",
             "Lightbringer", "Brightroar", "Lady Forlorn", "Tempest", "Nightfall", "Red Rain", "Lamentation", "Truth",
             "Vigilance", "Despair", "Orphanmaker", "Liontooth", "Needle", "Crab's Pincer", "Skull Sword", "White Walker"]
rows, ids = [], []
for i in a:
    name = i["name"].lstrip("}").strip()
    why = []
    p = [x for x in PEOPLE if re.search(r"\b" + re.escape(x) + r"\b", name, re.I)]
    if p: why.append("postac w nazwie: " + p[0])
    l = [x for x in LORE_NAME if re.search(r"\b" + re.escape(x) + r"\b", name, re.I)]
    if l: why.append("legenda/material: " + l[0])
    if re.search(r"\bCrown\b", name, re.I): why.append("korona")
    if i["value"] >= 100000: why.append("legendarna cena autora %d" % i["value"])
    if why:
        rows.append((i["id"], name, i["type"].replace("Crafted:", ""), i["culture"] or "-", "; ".join(why)))
        ids.append(i["id"])
grp = collections.OrderedDict()
def g(r):
    w = r[4]
    if "legenda" in w or "legendarna" in w: return "1. Legendy i materialy z legend"
    if "korona" in w: return "2. Korony"
    return "3. Rynsztunek postaci (imie w nazwie)"
for r in sorted(rows, key=lambda r: (g(r), r[0])): grp.setdefault(g(r), []).append(r)
out = ["# Unikaty Realm of Thrones - spis (2026-10-05, wedlug NAZW widocznych w grze)", "",
       "Jeff 05.10: \"Northern Leather Boots to nie jest unikat, to buty z Polnocy - gdyby byly Jon Snow Boots, to bylby unikat\".",
       "Zasada: unikat = nazwa w grze zawiera imie postaci, legende/material z legend (stal valyrianska, weirwood, smocze szklo,",
       "smocza kosc, olbrzymy, Kruczy Zab) albo korone, albo autor ROT dal mu legendarna cene (>= 100 000).",
       "NIE sa unikatami: rzeczy o nazwach ogolnych, nawet gdy id w plikach ROT ma imie (jon_snow_boots = Northern Leather Boots,",
       "jonsnow_armor = Northern Elite Leathers, jaime_clothes = Noble Clothes, dayne_armor = Targaryen Kingsguard Armor),",
       "stroje rodow (Mormont Armor, Dayne Plate), zbroje kultury Valyrian (Valyrian Plate Armor), Noble Default, Andal Shoes.",
       "Skutek w grze (Armoury RotUniques -> IsUnique): warsztaty ich nie robia, AI ich nie kupuje, wycena x prestiz, kronika i zdobycz.",
       "Razem: %d." % len(rows), ""]
for k, v in grp.items():
    out.append("## %s (%d)" % (k, len(v))); out.append("")
    out.append("| id | nazwa w grze | typ | kultura | powod |"); out.append("|---|---|---|---|---|")
    for r in v: out.append("| `%s` | %s | %s | %s | %s |" % r)
    out.append("")
open(r"C:\Users\GAME\Bannerlor2-Realm-of-Thrones\docs\ROT-UNIKATY.md", "w", encoding="utf-8").write("\n".join(out))
cs = ["using System.Collections.Generic;", "", "namespace Armoury", "{", "    /// <summary>",
      "    /// Unikaty Realm of Thrones (spis: docs/ROT-UNIKATY.md, generowany tools/rot_uniques.py). O unikacie decyduje NAZWA",
      "    /// widoczna w grze (Jeff 05.10): imie postaci, legenda/material z legend, korona albo legendarna cena autora.",
      "    /// Uzywane przez ArmsPricing.IsUnique i UniqueSpoils: warsztaty ich nie robia, AI nie kupuje, wycena x prestiz.",
      "    /// </summary>", "    internal static class RotUniques", "    {",
      "        internal static readonly HashSet<string> Ids = new HashSet<string>", "        {"]
for k in range(0, len(ids), 6): cs.append("            " + ", ".join('"%s"' % x for x in ids[k:k + 6]) + ",")
cs += ["        };", "    }", "}", ""]
open(r"C:\Users\GAME\Bannerlor2-Realm-of-Thrones\Armoury\src\RotUniques.cs", "w", encoding="utf-8", newline="").write("\r\n".join(cs))
print(len(rows), {k: len(v) for k, v in grp.items()})
