# Generuje Armoury/src/PrologueScenes.Data.cs z prolog298_sceny.json (rozpoznanie m103 "teksty") + zlagodzone sceny 14/15/19 (dni 118/125/155 - bez obietnic
# turnieju i band, E5 jeszcze nie ma). Wypisuje tez JSON koncowych tekstow do sprawdzenia skryptem linie.py.
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from apply import write_crlf

SRC = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/7aee5f12-7939-45a5-8ff5-01d4910ef5ca/scratchpad/e4c/prolog298_sceny.json'
OUT_JSON = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'sceny_final.json')

data = json.load(open(SRC, encoding='utf-8'))['scenes']
assert len(data) == 23

SOFT = {
    'prolog_14': ("The clans of the Vale answer to no lord and fall on any traveller they can catch. Their warbands now haunt the mountain passes. Whoever crosses "
                  "the Vale by the high road should ride well armed, or not at all.",
                  "The clans of the Vale answer to no lord and fall on any traveller they can catch. Several of Lady Catelyn's men die on the road before the "
                  "party reaches the Bloody Gate, where her uncle Ser Brynden Tully, the Blackfish, rides out to meet them."),
    'prolog_15': ("Loras yields the final to the Hound, and Thoros of Myr wins the melee. Ned notes that the dead knight was once Jon Arryn's squire. In these "
                  "lists, a skilled fighter could win gold and a name in a single day.",
                  "Loras yields the final to the Hound, and Thoros of Myr wins the melee. Ned notes that the dead knight, Ser Hugh of the Vale, was newly "
                  "knighted and had once been Jon Arryn's squire."),
    'prolog_19': ("Lord Tywin answers the seizure of his son with fire, while pretending to have no hand in it. Refugees crowd the roads to Riverrun and the "
                  "capital. Travellers in the Riverlands should expect armed bands and burned fields.",
                  "Lord Tywin answers the seizure of his son with fire, while pretending to have no hand in it. Refugees crowd the roads to Riverrun, and the "
                  "river lords send survivors to King's Landing to beg the king's justice."),
}

BLACKWATER = ("Stannis Sails for King's Landing", "Stannis Claims the Iron Throne")


def cs(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'


rows, final = [], []
for i, sc in enumerate(data):
    nn = '%02d' % (i + 1)
    assert sc['id'] == 'prolog_' + nn, sc['id']
    title, up, low = sc['Title'], sc['UpperText'], sc['LowerText']
    if sc['id'] in SOFT:
        old, new = SOFT[sc['id']]
        assert low == old, (sc['id'], low)
        low = new
    for t in (title, up, low):
        assert all(32 <= ord(c) < 127 for c in t), (sc['id'], t)
        assert '{' not in t and '}' not in t
    assert title not in BLACKWATER
    final.append(dict(id=sc['id'], day=sc['day'], Title=title, UpperText=up, LowerText=low, fallback_sprite=sc['fallback_sprite']))
    rows.append('            new Scene(%d, "%s", %s,\n                %s,\n                %s,\n                %s),'
                % (sc['day'], sc['id'], cs(sc['fallback_sprite']), cs('{=ARM_PRL_%s_T}' % nn + title), cs('{=ARM_PRL_%s_U}' % nn + up),
                   cs('{=ARM_PRL_%s_L}' % nn + low)))

days = [s['day'] for s in final]
assert days == sorted(days) and days[0] == 0 and days[-1] == 186, days
json.dump({'scenes': final}, open(OUT_JSON, 'w', encoding='utf-8'), indent=1, ensure_ascii=True)

text = '''using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m103-sceny (E4): TRESC 23 SCEN PROLOGU 298 (projekt PROJEKT-PROLOG-298 pkt 2, Jeff 21.10 "I zrob wydarzenia wedlug ksiazki"). Teksty EN wedlug A Game of Thrones
    /// wlasnymi slowami (rozpoznanie m103 "teksty": prolog298_sceny.json; bez cytatow z ksiazek i serialu). Kazdy napis w TextObject z wlasnym id (ARM_PRL_NN_T/U/L) -
    /// tlumaczenie mozna dolozyc plikiem jezyka bez zmiany kodu; przycisk jak w ROT ({=ROTasSnYXW5}Continue...). Pojemnosc okna (prefab EventXml z ROT-Content:
    /// Galahad 25, ok. 100 znakow na linie, srodki akapitow 120 px od siebie - razem do 9 linii): akapit gorny do 4 linii, dolny do 3, razem do 7 (linie.py).
    /// Sceny dni 118, 125, 155 bez zdan obiecujacych graczowi turniej i bandy (sceny zmieniajace swiat E5 jeszcze nie ma) - tekst zostaje przy ksiazce.
    /// Obrazek: Modules\\Armoury\\Images\\Prolog\\prolog_NN.png|jpg (StoryPictures), bez pliku - sprite ROT z ui_event_category (Fallback).
    /// Plik generowany skryptem z JSON rozpoznania - zmiany tekstu tu i w JSON razem.
    /// </summary>
    internal static class PrologueScenes
    {
        internal const string ContinueText = "{=ROTasSnYXW5}Continue...";

        internal sealed class Scene
        {
            public readonly int Day;          // dzien prologu (0 = wyjazd krola z Krolewskiej Przystani, 186 = smierc krola)
            public readonly string Stem;      // nazwa pliku obrazka bez rozszerzenia (prolog_NN)
            public readonly string Fallback;  // sprite ROT (ui_event_category), gdy pliku nie ma albo jest zly
            private readonly string _t, _u, _l;

            public Scene(int day, string stem, string fallback, string t, string u, string l)
            {
                Day = day; Stem = stem; Fallback = fallback; _t = t; _u = u; _l = l;
            }

            public string Title() { return new TextObject(_t).ToString(); }
            public string Upper() { return new TextObject(_u).ToString(); }
            public string Lower() { return new TextObject(_l).ToString(); }
        }

        internal static string Continue() { return new TextObject(ContinueText).ToString(); }

        internal static readonly Scene[] All =
        {
%ROWS%
        };
    }
}
'''.replace('%ROWS%', '\n'.join(rows))
write_crlf('Armoury/src/PrologueScenes.Data.cs', text)
print('dni:', days)
