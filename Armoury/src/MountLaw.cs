using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// GEOGRAFIA WIERZCHOWCOW (Jeff 14.09: "wywal mamuty - maja je tylko Wolni
    /// Ludzie i jezdza na nich giganci, nie ludzie; wielblady tylko Dorne;
    /// rydwany tylko armie na wschodzie, nie w Westeros; reszte usun i daj
    /// zamienniki konie"). Dane ROT: mammoth (kultura freefolk) siedzi
    /// w szablonie JEDNEGO oddzialu - Mammoth Riding Giant; wielblady maja
    /// Dornijczycy i Qartheen (wlasne szablony); rydwany NIE maja zadnego
    /// szablonu - zyly wylacznie w losowych pulach DTE, na targach i w naszej
    /// stajni AI (14.09: "Stajnia AI: Barristan Selmy kupil 9 koni [mammoth]");
    /// slonie tylko Volantis (Zlota Kompania).
    /// Regula: KON zawsze; MAMUT - tylko ten, kto ma go we wlasnym szablonie
    /// (giganci); WIELBLAD - kultura Dorne (aserai) albo wlasny szablon
    /// (Qartheen); RYDWAN - wylacznie kultury Essos; SLON - Volantis albo
    /// wlasny szablon. Smoki maja swoje prawo (DragonUnmount).
    /// </summary>
    internal static class MountLaw
    {
        internal enum Fam { Horse, Camel, Chariot, Elephant, Mammoth, Dragon, Other }

        // kultury ROT lezace za Waskim Morzem (patrz audyt 14.09: lista kultur ROT)
        private static readonly HashSet<string> Essos = new HashSet<string>
        {
            "empire", "ghiscari", "ibbenese", "khuzait", "lyseni", "myrish", "nord", "norvos",
            "pentoshi", "qartheen", "qohorik", "sarnor", "summer", "tyroshi", "valyrian",
            "volantine", "yiti", "yiti_bandits", "darshi"
        };

        private static readonly Dictionary<CharacterObject, int> _tplFams = new Dictionary<CharacterObject, int>();

        internal static Fam FamilyOf(ItemObject it)
        {
            try
            {
                if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Horse) return Fam.Other;
                var id = it.StringId ?? "";
                if (id == "mammoth") return Fam.Mammoth;
                var mon = it.HorseComponent != null ? it.HorseComponent.Monster : null;
                var mid = mon != null && mon.StringId != null ? mon.StringId : "";
                if (mid.StartsWith("camel")) return Fam.Camel;
                if (mid.StartsWith("chariot")) return Fam.Chariot;
                if (mid.StartsWith("elephant")) return Fam.Elephant;
                if (mid.StartsWith("dragon")) return Fam.Dragon;
                if (mid.StartsWith("horse")) return Fam.Horse;
            }
            catch { }
            return Fam.Other;
        }

        internal static bool IsExotic(ItemObject it)
        {
            var f = FamilyOf(it);
            return f == Fam.Camel || f == Fam.Chariot || f == Fam.Elephant || f == Fam.Mammoth;
        }

        internal static string Name(Fam f)
        {
            switch (f)
            {
                case Fam.Camel: return "wielblad";
                case Fam.Chariot: return "rydwan";
                case Fam.Elephant: return "slon";
                case Fam.Mammoth: return "mamut";
                default: return f.ToString().ToLowerInvariant();
            }
        }

        /// <summary>Bit-maska rodzin egzotycznych we WLASNYM szablonie jednostki.</summary>
        private static int TemplateFams(CharacterObject co)
        {
            if (co == null) return 0;
            int mask;
            if (_tplFams.TryGetValue(co, out mask)) return mask;
            mask = 0;
            try
            {
                foreach (var eq in co.BattleEquipments)
                {
                    if (eq == null) continue;
                    var f = FamilyOf(eq[(EquipmentIndex)10].Item);
                    if (f != Fam.Horse && f != Fam.Other) mask |= 1 << (int)f;
                }
            }
            catch { }
            _tplFams[co] = mask;
            return mask;
        }

        private static bool AllowedFor(string culture, bool ownTemplate, Fam f)
        {
            switch (f)
            {
                case Fam.Mammoth: return ownTemplate;
                case Fam.Camel: return culture == "aserai" || ownTemplate;
                case Fam.Chariot: return culture != null && Essos.Contains(culture);
                case Fam.Elephant: return culture == "volantine" || ownTemplate;
                default: return true;
            }
        }

        /// <summary>Czy TEN zolnierz/bohater moze siedziec na tym wierzchowcu.</summary>
        internal static bool Allowed(CharacterObject co, ItemObject mount)
        {
            try
            {
                var f = FamilyOf(mount);
                if (f == Fam.Horse || f == Fam.Other || f == Fam.Dragon) return true;
                string cul = co != null && co.Culture != null ? co.Culture.StringId : null;
                bool own = co != null && (TemplateFams(co) & (1 << (int)f)) != 0;
                return AllowedFor(cul, own, f);
            }
            catch { return true; }
        }

        private static string PartyCulture(MobileParty mp)
        {
            try
            {
                if (mp == null) return null;
                if (mp.Party != null && mp.Party.Culture != null) return mp.Party.Culture.StringId;
                if (mp.LeaderHero != null && mp.LeaderHero.Culture != null) return mp.LeaderHero.Culture.StringId;
                if (mp.ActualClan != null && mp.ActualClan.Culture != null) return mp.ActualClan.Culture.StringId;
            }
            catch { }
            return null;
        }

        /// <summary>Czy w tej partii jest komu jezdzic na takim zwierzeciu
        /// (kultura partii albo ktos z wlasnym szablonem).</summary>
        internal static bool AllowedForParty(MobileParty mp, ItemObject mount)
        {
            try
            {
                var f = FamilyOf(mount);
                if (f == Fam.Horse || f == Fam.Other || f == Fam.Dragon) return true;
                string cul = PartyCulture(mp);
                bool own = false;
                var roster = mp != null ? mp.MemberRoster : null;
                if (roster != null)
                    for (int i = 0; i < roster.Count && !own; i++)
                        own = (TemplateFams(roster.GetCharacterAtIndex(i)) & (1 << (int)f)) != 0;
                return AllowedFor(cul, own, f);
            }
            catch { return true; }
        }

        /// <summary>Czy targ tej osady ma prawo to sprzedawac (mamutow nikt).</summary>
        internal static bool AllowedForSettlement(Settlement st, ItemObject mount)
        {
            try
            {
                var f = FamilyOf(mount);
                if (f == Fam.Horse || f == Fam.Other || f == Fam.Dragon) return true;
                if (f == Fam.Mammoth) return false;
                string cul = st != null && st.Culture != null ? st.Culture.StringId : null;
                if (f == Fam.Camel) return cul == "aserai" || cul == "qartheen";
                return AllowedFor(cul, false, f);
            }
            catch { return true; }
        }
    }
}
