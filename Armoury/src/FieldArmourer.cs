using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// R3-04c (pomysly 35, Jeff 20.10 "tak"): PLATNERZ W TABORZE. Towarzysz z najlepszym Kowalstwem w druzynie gracza lata metalowy sprzet Twoich
    /// ludzi z magazynu wojska (bron, kolczugi, plyty, helmy - robota kowala), gdy druzyna stoi poza miastem (oboz, oblezenie, zamek, wies);
    /// w miescie robia to kowale miasta (TroopSelfMend), w marszu nikt nie kuje. Godziny sztuki te same co u kowali miasta (GuildMend.Hours),
    /// ale jeden czlowiek przy polowym kowadle jest wolniejszy: na godzine zegara robi 0.25 + Kowalstwo/400 roboczogodziny (najwyzej 0.75 -
    /// zawsze wolniej niz pelny warsztat). Noca spi (WorkshopNightRest). Material z taboru jak dzis przy naprawie wlasnymi rekami (MendMaterial.SelfPartsOf,
    /// ta sama pamiec ulamkow sakw) - brak materialu: sztuka czeka, platnerz nie pracuje i nie dostaje doswiadczenia. Robocizna nic nie kosztuje
    /// (towarzysz bierze juz zold). Doswiadczenie Kowalstwa tylko za przepracowane godziny (XpPerHour), najwyzej XpPerDay na dobe - jak dni przy kowadle.
    /// Wraki (stan <= 10%) i konie - nie (jak kowale miasta). Klucz CampArmourer; stan tylko w pamieci (lawa ginie przy wczytaniu - kilka godzin pracy).
    /// </summary>
    internal static class FieldArmourer
    {
        private const float XpPerHour = 8f, XpPerDay = 100f;
        private const int MinSkill = 20;
        private static float _bench;
        private static string _benchKey;
        private static int _xpDay = -1;
        private static float _xpToday;
        private static int _dayPieces, _dayWaitMat, _errN, _logDay = -1;
        private static float _dayHours, _dayXp;
        private static string _dayWho;

        internal static bool On { get { var s = Settings.Current; return s != null && s.CampArmourer; } }

        internal static void Reset() { _bench = 0f; _benchKey = null; _xpDay = -1; _xpToday = 0f; _dayPieces = 0; _dayWaitMat = 0; _dayHours = 0f; _dayXp = 0f; _dayWho = null; _logDay = -1; }

        /// <summary>Towarzysz z najlepszym Kowalstwem w druzynie gracza (bez gracza, rannych i jencow); null - nikt nie umie (ponizej MinSkill).</summary>
        private static Hero Pick(MobileParty main, out int skill)
        {
            skill = 0;
            Hero best = null;
            var roster = main.MemberRoster;
            if (roster == null) return null;
            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster.GetCharacterAtIndex(i);
                if (ch == null || !ch.IsHero) continue;
                var h = ch.HeroObject;
                if (h == null || h == Hero.MainHero || !h.IsAlive || h.IsWounded || h.IsPrisoner) continue;
                int v = h.GetSkillValue(DefaultSkills.Crafting);
                if (v > skill) { skill = v; best = h; }
            }
            return skill >= MinSkill ? best : null;
        }

        internal static void Hourly()
        {
            try
            {
                if (!On) return;
                var s = Settings.Current;
                var main = MobileParty.MainParty;
                if (main == null || Hero.MainHero == null) return;
                FlushDay();
                var st = main.CurrentSettlement;
                if (st != null && st.IsTown) { _bench = 0f; return; }   // w miescie lataja kowale miasta
                if (main.IsMoving || main.MapEvent != null) return;      // w marszu i w bitwie nikt nie kuje (lawa czeka)
                if (s.WorkshopNightRest) { int hh = CampaignTime.Now.GetHourOfDay; if (hh >= 23 || hh < 5) return; }
                int skill;
                var who = Pick(main, out skill);
                if (who == null) { _bench = 0f; return; }
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;
                var worn = new List<ItemRosterElement>();
                foreach (var el in TroopSelfMend.MenWorn(armory))   // tylko czesc ludzi (Twoja czesc kazdego id pomijana - jak kowale miasta)
                    if (el.EquipmentElement.Item != null && GuildMend.TradeOf(el.EquipmentElement.Item) == GuildMend.Smith) worn.Add(el);
                if (worn.Count == 0) { _bench = 0f; return; }
                worn.Sort((a, b) => a.EquipmentElement.ItemModifier.PriceMultiplier.CompareTo(b.EquipmentElement.ItemModifier.PriceMultiplier));   // najgorsze najpierw
                var head = worn[0].EquipmentElement;
                string key = head.Item.StringId + "|" + (head.ItemModifier != null ? head.ItemModifier.StringId : "");
                if (key != _benchKey) { _bench = 0f; _benchKey = key; }   // inna sztuka na kowadle - zaczyna od nowa
                var mats = MendMaterial.SelfPartsOf(head);
                foreach (var p in mats)
                    if (p.Item != null && Recipes.CountInInventory(p.Item) < p.Count) { _dayWaitMat++; return; }   // brak materialu w taborze - nie pracuje
                float eff = Math.Min(0.75f, 0.25f + skill / 400f);
                float need = GuildMend.Hours(head);
                _bench += eff;
                _dayHours += eff; _dayWho = who.Name != null ? who.Name.ToString() : who.StringId;
                // doswiadczenie za przepracowana godzine, z sufitem dobowym
                int d = (int)CampaignTime.Now.ToDays;
                if (d != _xpDay) { _xpDay = d; _xpToday = 0f; }
                float xp = Math.Min(XpPerHour, Math.Max(0f, XpPerDay - _xpToday));
                if (xp > 0f) { who.HeroDeveloper.AddSkillXp(DefaultSkills.Crafting, xp); _xpToday += xp; _dayXp += xp; }
                if (_bench + 0.0001f < need) return;
                // sztuka gotowa: material z taboru (jak "Mend it yourself" dla magazynu wojska), magazyn - zbita na czysta
                var bag = main.ItemRoster;
                foreach (var p in mats)
                {
                    if (p.Item == null || p.Count <= 0) continue;
                    if (s.MendTakeByKind) Recipes.Take(bag, p.Item, p.Count); else bag.AddToCounts(p.Item, -p.Count);
                }
                if (MendMaterial.SelfSameRuleOn) MendMaterial.SelfMendDoneOf(head, mats);
                armory.AddToCounts(head, -1);
                armory.AddToCounts(new EquipmentElement(head.Item), 1);
                _bench = 0f; _benchKey = null;
                _dayPieces++;
            }
            catch (Exception e) { if (++_errN <= 3) Log.Error("FieldArmourer.Hourly (potkniecie " + _errN + ")", e); }   // licznik potkniec, bez wylacznika (CLAUDE.md 7)
        }

        /// <summary>Linia doby (log_grep: "Platnerz w taborze (R3-04c)") i komunikat dla gracza - raz na dobe, gdy byla praca albo czekanie.</summary>
        private static void FlushDay()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (_logDay < 0) { _logDay = d; return; }
            if (d == _logDay) return;
            if (_dayPieces > 0 || _dayHours > 0f || _dayWaitMat > 0)
            {
                Log.Info("Platnerz w taborze (R3-04c): doba " + _logDay + " | " + (_dayWho ?? "-") + " - naprawil " + _dayPieces + " szt. metalu ludzi, przepracowal "
                         + GuildMend.F1(_dayHours) + " roboczogodz., XP Kowalstwa " + (int)Math.Round(_dayXp) + " (sufit " + (int)XpPerDay + "/dobe), godzin bez materialu " + _dayWaitMat + ".");
                if (_dayPieces > 0)
                    Log.Player(_dayWho + " mended " + _dayPieces + (_dayPieces == 1 ? " piece" : " pieces") + " of your men's kit at the camp forge yesterday.");
                else if (_dayWaitMat > 0 && _dayHours <= 0f)
                    Log.Player("Your armourer had nothing to mend with - the baggage lacks the iron or other materials.", true);
            }
            _logDay = d; _dayPieces = 0; _dayWaitMat = 0; _dayHours = 0f; _dayXp = 0f; _dayWho = null;
        }
    }
}
