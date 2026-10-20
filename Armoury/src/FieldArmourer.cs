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
    /// Poprawki po recenzji: brak materialu na najgorsza NIEZACZETA sztuke - platnerz bierze nastepna, na ktora material jest (jak "stos czeka, nastepny
    /// dalej" u kowali miasta, MendMaterial.Order); zaczeta sztuka (postep > 0) zostaje na kowadle, takze gdy platnerz jest chwilowo ranny.
    /// R3-04c, drugi czlon (klucz CampQuartermasterSteward): KWATERMISTRZ ZBROJOWNI WEDLUG ZARZADZANIA. Kwatermistrz druzyny (rola gry, MobileParty.
    /// EffectiveQuartermaster - bez przydzialu sam dowodca) wydaje platnerzowi sztuki z magazynu, odmierza material z taboru i sortuje robote: przy jego
    /// Zarzadzaniu (Steward) platnerz traci mniej czasu - robota x (1 + Steward/800, najwyzej +25%; razem najwyzej 0.94 roboczogodziny na godzine, wciaz
    /// wolniej niz pelny warsztat). Kwatermistrz uczy sie Zarzadzania tylko za godziny, w ktorych kuznia naprawde pracowala (2 XP na godzine, sufit 25
    /// na dobe). Ten sam czlowiek nie robi dwoch rzeczy naraz: kwatermistrz bedacy platnerzem, ranny albo w niewoli - bez premii i bez XP. Material bez zmian.
    /// </summary>
    internal static class FieldArmourer
    {
        private const float XpPerHour = 8f, XpPerDay = 100f;
        private const float QmXpPerHour = 2f, QmXpPerDay = 25f, QmMaxBonus = 0.25f, QmPerPoint = 1f / 800f;   // R3-04c: kwatermistrz wedlug Steward
        private const int MaxScan = 60;   // ile sztuk z kolejki sprawdzic co godzine, szukajac materialu (najgorsze najpierw)
        private const int MinSkill = 20;
        private static float _bench;
        private static string _benchKey;
        private static int _xpDay = -1;
        private static float _xpToday;
        private static int _dayPieces, _dayWaitMat, _errN, _logDay = -1;
        private static float _dayHours, _dayXp, _qmXpToday, _dayQmXp, _dayQmBonus;
        private static string _dayWho, _dayQm;
        private static int _qmXpDay = -1, _dayQmHours;

        internal static bool On { get { var s = Settings.Current; return s != null && s.CampArmourer; } }

        internal static bool QmOn { get { var s = Settings.Current; return s != null && s.CampArmourer && s.CampQuartermasterSteward; } }

        internal static void Reset()
        {
            _bench = 0f; _benchKey = null; _xpDay = -1; _xpToday = 0f; _dayPieces = 0; _dayWaitMat = 0; _dayHours = 0f; _dayXp = 0f; _dayWho = null; _logDay = -1;
            _qmXpDay = -1; _qmXpToday = 0f; _dayQmXp = 0f; _dayQmBonus = 0f; _dayQm = null; _dayQmHours = 0;
        }

        /// <summary>R3-04c: kwatermistrz druzyny, ktory pomaga platnerzowi (null - wylaczone, ten sam czlowiek co platnerz, ranny albo w niewoli).</summary>
        private static Hero Quartermaster(MobileParty main, Hero armourer, out int steward)
        {
            steward = 0;
            if (!QmOn) return null;
            var q = main.EffectiveQuartermaster;
            if (q == null || q == armourer || !q.IsAlive || q.IsWounded || q.IsPrisoner) return null;
            steward = q.GetSkillValue(DefaultSkills.Steward);
            return q;
        }

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

        private static string KeyOf(EquipmentElement ee) { return ee.Item.StringId + "|" + (ee.ItemModifier != null ? ee.ItemModifier.StringId : ""); }

        /// <summary>Czy tabor ma caly material tej naprawy (to samo sprawdzenie co dotad dla jednej sztuki).</summary>
        private static bool HasAll(List<Recipes.Part> mats)
        {
            if (mats == null) return true;
            foreach (var p in mats)
                if (p.Item != null && Recipes.CountInInventory(p.Item) < p.Count) return false;
            return true;
        }

        internal static void Hourly()
        {
            try
            {
                if (!On) return;
                var s = Settings.Current;
                var main = MobileParty.MainParty;
                if (main == null || Hero.MainHero == null || Hero.MainHero.IsPrisoner || !main.IsActive) return;   // w niewoli tabor nie pracuje
                FlushDay();
                var st = main.CurrentSettlement;
                if (st != null && st.IsTown) return;   // w miescie lataja kowale miasta (zaczeta sztuka czeka na kowadle; naprawiona przez kowali - lawa od nowa)
                if (main.IsMoving || main.MapEvent != null) return;      // w marszu i w bitwie nikt nie kuje (lawa czeka)
                if (s.WorkshopNightRest) { int hh = CampaignTime.Now.GetHourOfDay; if (hh >= 23 || hh < 5) return; }
                int skill;
                var who = Pick(main, out skill);
                if (who == null) return;   // nikt nie umie albo platnerz ranny - zaczeta sztuka czeka na kowadle (postep zostaje; recenzja)
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;
                // tylko robota kowala z tabeli godzin (bron, kolczuga, plyta, helm, metalowy kropierz) z receptura kowala - jak kowale miasta (MendMaterial.Needs:
                // bez receptury "nie robota kowala"); wraki i konie odpadaja juz w MenWorn (Mendable); amunicja i sztandary - nie
                var worn = new List<ItemRosterElement>();
                foreach (var el in TroopSelfMend.MenWorn(armory))   // tylko czesc ludzi (Twoja czesc kazdego id pomijana - jak kowale miasta)
                    if (el.EquipmentElement.Item != null && GuildMend.IsSmithWork(el.EquipmentElement.Item)) worn.Add(el);
                if (worn.Count == 0) { _bench = 0f; _benchKey = null; return; }
                worn.Sort((a, b) => a.EquipmentElement.ItemModifier.PriceMultiplier.CompareTo(b.EquipmentElement.ItemModifier.PriceMultiplier));   // najgorsze najpierw
                // sztuka zaczeta (postep > 0) zostaje na kowadle do konca (nowy, gorszy lup po bitwie nie kasuje godzin juz przepracowanych) - brak materialu: czeka;
                // inaczej najgorsza z receptura, NA KTORA JEST MATERIAL W TABORZE (recenzja: jedna brakujaca sztaba nie blokuje calej kolejki)
                int pick = -1;
                List<Recipes.Part> mats = null;
                if (_benchKey != null && _bench > 0f)
                    for (int i = 0; i < worn.Count; i++) if (KeyOf(worn[i].EquipmentElement) == _benchKey) { pick = i; break; }
                if (pick >= 0)
                {
                    mats = MendMaterial.SelfPartsOf(worn[pick].EquipmentElement);
                    if (!HasAll(mats)) { _dayWaitMat++; return; }   // zaczeta sztuka czeka na material (postep zostaje)
                }
                else
                {
                    bool missing = false;
                    int scanned = 0;
                    for (int i = 0; i < worn.Count && scanned < MaxScan; i++)
                    {
                        if (MendMaterial.Needs(worn[i].EquipmentElement) == null) continue;
                        scanned++;
                        var m = MendMaterial.SelfPartsOf(worn[i].EquipmentElement);
                        if (HasAll(m)) { pick = i; mats = m; break; }
                        missing = true;   // ta sztuka czeka na material - nastepna dalej
                    }
                    if (pick < 0) { _bench = 0f; _benchKey = null; if (missing) _dayWaitMat++; return; }   // zadna sztuka nie ma materialu w taborze
                }
                var head = worn[pick].EquipmentElement;
                string key = KeyOf(head);
                if (key != _benchKey) { _bench = 0f; _benchKey = key; }   // inna sztuka na kowadle - zaczyna od nowa
                float eff = Math.Min(0.75f, 0.25f + skill / 400f);
                int stw;
                var qm = Quartermaster(main, who, out stw);   // R3-04c: kwatermistrz wedlug Steward - mniej straconego czasu przy kowadle
                float bonus = qm != null ? Math.Min(QmMaxBonus, stw * QmPerPoint) : 0f;
                eff *= 1f + bonus;
                float need = GuildMend.Hours(head);
                _bench += eff;
                _dayHours += eff; _dayWho = who.Name != null ? who.Name.ToString() : who.StringId;
                int d = (int)CampaignTime.Now.ToDays;
                if (qm != null)
                {
                    _dayQm = qm.Name != null ? qm.Name.ToString() : qm.StringId; _dayQmBonus = Math.Max(_dayQmBonus, bonus); _dayQmHours++;
                    if (d != _qmXpDay) { _qmXpDay = d; _qmXpToday = 0f; }
                    float qx = Math.Min(QmXpPerHour, Math.Max(0f, QmXpPerDay - _qmXpToday));   // tylko za godzine, w ktorej kuznia pracowala; sufit dobowy
                    if (qx > 0f && qm.HeroDeveloper != null) { qm.HeroDeveloper.AddSkillXp(DefaultSkills.Steward, qx); _qmXpToday += qx; _dayQmXp += qx; }
                }
                // doswiadczenie za przepracowana godzine, z sufitem dobowym
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
                         + GuildMend.F1(_dayHours) + " roboczogodz., XP Kowalstwa " + (int)Math.Round(_dayXp) + " (sufit " + (int)XpPerDay + "/dobe), godzin bez materialu " + _dayWaitMat
                         + " | kwatermistrz (Steward): " + (_dayQm != null ? _dayQm + " - " + _dayQmHours + " godz. pomocy, premia do +" + (int)Math.Round(_dayQmBonus * 100f) + "%, XP Zarzadzania "
                         + (int)Math.Round(_dayQmXp) + " (sufit " + (int)QmXpPerDay + "/dobe)" : (QmOn ? "brak (ten sam co platnerz, ranny albo nikt)" : "wylaczone")) + ".");
                if (_dayPieces > 0)
                    Log.Player(_dayWho + " mended " + _dayPieces + (_dayPieces == 1 ? " piece" : " pieces") + " of your men's kit at the camp forge yesterday.");
                else if (_dayWaitMat > 0 && _dayHours <= 0f)
                    Log.Player("Your armourer had nothing to mend with - the baggage lacks the iron or other materials.", true);
            }
            _logDay = d; _dayPieces = 0; _dayWaitMat = 0; _dayHours = 0f; _dayXp = 0f; _dayWho = null; _dayQm = null; _dayQmXp = 0f; _dayQmBonus = 0f; _dayQmHours = 0;
        }
    }
}
