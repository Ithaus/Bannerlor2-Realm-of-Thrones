using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PRAWO ZARAZY OBOZOWEJ (Jeff 31.08, "rob"). Historycznie dyzenteria
    /// i tyfus zabijaly wiecej wojska niz zelazo - a u nas armia stala pod
    /// murami pol roku i nikt nie kichnal. Odtad kazde oblezenie ma zegar:
    ///  - inkubacja (dom. 14 dni, 398) - swiezy oboz jest czysty;
    ///  - potem dzienne zachorowania: baza 0.6%/dzien, rosnaca +10% za kazdy
    ///    dzien ponad inkubacje (398: najwyzej do x4), razy TLOK
    ///    (sqrt(ludzi/500) - wielkie doomstacki gnija najszybciej);
    ///  - chorzy ida W RANNYCH (gorczka klade, rzadko scina) - wracaja naszym
    ///    powolnym leczeniem; co dziesiaty chory umiera;
    ///  - OBRONCY lapia 40% tempa oblegajacych, ale GLOD (pusty spichlerz)
    ///    podwaja ich tempo - oblezenie to wyscig spichlerza z latrynami;
    ///  - MEDYCYNA to tarcza: skill chirurga partii -0.25%/pkt (cap -50%),
    ///    Preventive Medicine -15% zachorowan, Siege Medic smiertelnosc /2,
    ///    Pristine Streets (gubernator) -30% za murami i glod boli o polowe
    ///    mniej; reszta perkow leczenia dziala sama, bo chorzy sa rannymi;
    ///  - BOHATEROWIE nie choruja z ticka; UMARLI nie choruja wcale - horda
    ///    Nocnego Krola oblega bez zegara (lore: zaraza to bron na zywych).
    /// 398 (Jeff 11.11 "rob 1" - glod i choroby w oblezeniu; porzadki, sufity, tlok calego obozu):
    ///  - TLOK liczony z CALEGO obozu (suma ludzi wszystkich druzyn obozu; w murach: zaloga + milicja + druzyny lordow),
    ///    nie z jednej druzyny - dotad armia AI z kilkunastu druzyn po 100-300 ludzi miala tlok 0.6-0.77 i prawie nie chorowala
    ///    (klucz SiegeSicknessCrowdWholeCamp; wylaczony = stary rachunek na druzyne);
    ///  - sufit narostu (SiegeSicknessRampCap) i sufit dobowy zachorowan druzyny (SiegeSicknessDailyCapPercent zdrowych);
    ///  - druzyna (albo osada) w bitwie tej doby nie choruje (roster nalezy wtedy do bitwy);
    ///  - oblezenie fabuly ROT (SiegePhantomLift.StoryOf) bez zarazy obu stron (SiegeFeverSparesStorySieges);
    ///  - w obroncach takze milicja osady; pamiec ostrzezen czyszczona miedzy kampaniami;
    ///  - funkcje wspolne modelu choroby dla 399 (trupy, zima) i 402 (loch): WinterTerm, MedicCut, DeathShare, Active.
    /// 399 (Jeff 11.11 "rob 1"): CZYNNIK zarazy, DODAWANY (nie mnozony): 1 + ZIMA + RANNI + TRUPY, najwyzej 3.0 -
    ///  - ZIMA = (SiegeFeverWinterFactor - 1) x kraina (WinterRealm), tylko zima (WinterBite.WinterNow); oblegajacy wedlug krainy pod druzyna, mury wedlug krainy warowni;
    ///  - RANNI = min(0.5, udzial rannych szeregowych druzyny) (SiegeFeverWoundedCounts) - chorzy leza w rannych, wiec zaraza sama sie karmi (stad +0.5 i sufit dobowy);
    ///  - TRUPY = SiegeMisery.CorpseTerm strony (niepogrzebani pod murami / w murach); zmarli zarazy ida do tego licznika (SiegeMisery.AddCorpses).
    /// </summary>
    internal static class CampFever
    {
        // ostrzezenie "zaraza wstaje" - raz na oblezenie (klucz: osada)
        private static readonly HashSet<string> _warned = new HashSet<string>();
        // 398: linia "oblezenie fabuly ROT - bez zarazy" raz na oblezenie (klucz: osada)
        private static readonly HashSet<string> _storyNoted = new HashSet<string>();
        private static readonly List<string> _tmpKeys = new List<string>();
        private static Campaign _campaign;      // 398: kampania, do ktorej nalezy pamiec powyzej
        private static int _stumbles;           // 398: potkniecia (wyjatki per obiekt) - liczone, nic sie nie gasi

        /// <summary>398: pamiec od zera (nowa kampania i kazde wczytanie - wykrywane w OnDaily po zmianie Campaign.Current).</summary>
        internal static void Reset()
        {
            _warned.Clear(); _storyNoted.Clear(); _tmpKeys.Clear(); _stumbles = 0;
            _campaign = Campaign.Current;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5 || _stumbles % 50 == 0) Log.Error("CampFever." + where + " (potkniecie " + _stumbles + ")", e);
        }

        // ---------------------------------------------------------------- funkcje wspolne modelu choroby (398; uzyja 399 i 402)

        /// <summary>399: czlon ZIMY czynnika zarazy dla osady (mury): (SiegeFeverWinterFactor - 1) x kraina warowni; 0 poza zima i dla wsi.</summary>
        internal static float WinterTerm(Settlement st)
        {
            try
            {
                float b = WinterBase();
                if (b <= 0f || st == null || st.Town == null) return 0f;
                return b * MathF.Clamp(WinterRealm.OfTown(st.Town), 0f, 1f);
            }
            catch (Exception e) { Stumble("WinterTerm.Settlement", e); return 0f; }
        }

        /// <summary>399: czlon ZIMY czynnika zarazy dla druzyny (oboz): (SiegeFeverWinterFactor - 1) x kraina pod druzyna; 0 poza zima.</summary>
        internal static float WinterTerm(MobileParty mp)
        {
            try
            {
                float b = WinterBase();
                if (b <= 0f || mp == null) return 0f;
                return b * MathF.Clamp(WinterRealm.OfParty(mp), 0f, 1f);
            }
            catch (Exception e) { Stumble("WinterTerm.Party", e); return 0f; }
        }

        /// <summary>399: SiegeFeverWinterFactor - 1 w zimie (WinterBite.WinterNow: wlaczona zima i pora roku = zima), inaczej 0.</summary>
        private static float WinterBase()
        {
            var s = Settings.Current;
            if (s == null || s.SiegeFeverWinterFactor <= 1f) return 0f;
            if (!WinterBite.WinterNow()) return 0f;
            return s.SiegeFeverWinterFactor - 1f;
        }

        /// <summary>399: sufit czynnika zarazy (1 + zima + ranni + trupy).</summary>
        internal const float FactorMax = 3f;

        /// <summary>Ulga chirurga: 0.25% ryzyka za punkt Medycyny, najwyzej SiegeSicknessMedicineMax (ulamek 0..1 odejmowany od ryzyka).</summary>
        internal static float MedicCut(int medicine)
        {
            var s = Settings.Current;
            float cap = s != null ? Math.Max(0, s.SiegeSicknessMedicineMax) / 100f : 0.5f;
            return MathF.Min(cap, Math.Max(0, medicine) * 0.0025f);
        }

        /// <summary>Udzial chorych, ktorzy umieraja zamiast pojsc w rannych (SiegeSicknessDeathShare; bez perkow - Siege Medic dzieli wynik u wolajacego).</summary>
        internal static float DeathShare()
        {
            var s = Settings.Current;
            return s != null ? Math.Max(0, s.SiegeSicknessDeathShare) / 100f : 0.1f;
        }

        /// <summary>Tylko odczyt: zaraza obozowa wlaczona i oblezenie tej osady trwa dluzej niz inkubacja. Nie rozstrzyga o fabule ROT (patrz Spared).</summary>
        internal static bool Active(Settlement st)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.SiegeSicknessEnabled || st == null) return false;
                var siege = st.SiegeEvent;
                if (siege == null) return false;
                return (int)siege.SiegeStartTime.ElapsedDaysUntilNow >= Math.Max(1, s.SiegeSicknessIncubationDays);
            }
            catch (Exception e) { Stumble("Active", e); return false; }
        }

        /// <summary>399-p, tylko odczyt: jak Active, ale juz od marginDays dob PRZED koncem inkubacji (trupy z tych dob waza jeszcze, gdy zaraza wstanie).</summary>
        internal static bool ActiveWithin(Settlement st, int marginDays)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.SiegeSicknessEnabled || st == null) return false;
                var siege = st.SiegeEvent;
                if (siege == null) return false;
                return (int)siege.SiegeStartTime.ElapsedDaysUntilNow >= Math.Max(1, s.SiegeSicknessIncubationDays) - Math.Max(0, marginDays);
            }
            catch (Exception e) { Stumble("ActiveWithin", e); return false; }
        }

        /// <summary>Tylko odczyt: oblezenie fabuly ROT oszczedzone przez zaraze (klucz SiegeFeverSparesStorySieges). Nazwa wydarzenia albo null.</summary>
        internal static string Spared(SiegeEvent siege)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.SiegeFeverSparesStorySieges || siege == null || siege.BesiegerCamp == null) return null;
                return SiegePhantomLift.StoryOf(siege.BesiegerCamp);
            }
            catch (Exception e) { Stumble("Spared", e); return null; }
        }

        // ---------------------------------------------------------------- doba

        internal static void OnDaily()
        {
            try
            {
                if (!ReferenceEquals(_campaign, Campaign.Current)) Reset();   // 398: stan nie przechodzi miedzy kampaniami ani przez wczytanie
                var s = Settings.Current;
                if (s == null || !s.SiegeSicknessEnabled) return;
                var mgr = Campaign.Current != null ? Campaign.Current.SiegeEventManager : null;
                if (mgr == null || mgr.SiegeEvents == null) return;

                foreach (var siege in mgr.SiegeEvents)
                {
                    try { TickSiege(s, siege); }
                    catch (Exception e) { Stumble("TickSiege", e); }
                }
                Prune(_warned); Prune(_storyNoted);
            }
            catch (Exception e) { Log.Error("CampFever.OnDaily", e); }
        }

        /// <summary>398: wpisy osad, ktore nie sa juz oblegane, znikaja (nowe oblezenie tej samej osady ostrzega i notuje od nowa).</summary>
        private static void Prune(HashSet<string> set)
        {
            if (set.Count == 0) return;
            _tmpKeys.Clear();
            foreach (var id in set)
            {
                Settlement st = null;
                try { st = Settlement.Find(id); } catch { }
                if (st == null || st.SiegeEvent == null) _tmpKeys.Add(id);
            }
            for (int i = 0; i < _tmpKeys.Count; i++) set.Remove(_tmpKeys[i]);
            _tmpKeys.Clear();
        }

        /// <summary>398: licznik jednej strony za dobe.</summary>
        private struct Tally { public int Sick, Dead, Capped, InBattle, WithHero, Struck; public float FSum, ZSum, RSum, Corpse; }

        private static void TickSiege(Settings s, SiegeEvent siege)
        {
            if (siege == null || siege.BesiegedSettlement == null) return;
            var st = siege.BesiegedSettlement;
            int days = (int)siege.SiegeStartTime.ElapsedDaysUntilNow;
            int incub = Math.Max(1, s.SiegeSicknessIncubationDays);
            if (days < incub) { _warned.Remove(st.StringId); _storyNoted.Remove(st.StringId); return; }

            // 398: oblezenie fabuly ROT - bez zarazy obu stron; linia raz na oblezenie
            string story = Spared(siege);
            if (story != null)
            {
                if (_storyNoted.Add(st.StringId))
                    Log.Info("CampFever: oblezenie " + st.StringId + " dzien " + days + " - fabula ROT (" + story + "), bez zarazy obu stron (klucz Siege Fever Spares Story Sieges).");
                return;
            }

            float rampCap = Math.Max(1f, s.SiegeSicknessRampCap);
            float rampRaw = 1f + (days - incub) * Math.Max(0, s.SiegeSicknessRampPercent) / 100f;
            bool rampCapped = rampRaw > rampCap;
            float ramp = rampCapped ? rampCap : rampRaw;

            // ostrzezenie, gdy zaraza wstaje a gracz jest strona
            bool mainAttacks = MobileParty.MainParty != null && MobileParty.MainParty.BesiegerCamp == siege.BesiegerCamp;
            bool mainDefends = MobileParty.MainParty != null && MobileParty.MainParty.CurrentSettlement == st;
            if (!_warned.Contains(st.StringId))
            {
                _warned.Add(st.StringId);
                // 398-p: _warned nie jest w zapisie - po wczytaniu trwajacego oblezenia graczowi nie powtarzamy (linia logu zostaje)
                if ((mainDefends || (mainAttacks && !SiegeCamp471.On)) && days - incub <= 1)   // 471b-2: oboz choruje tylko w wybuchu (SiegeCamp471 pisze graczowi przy wybuchu)
                    Log.Player("Camp fever stirs among the tents at " + st.Name + " - the longer the siege, the deeper it bites.", true);
                Log.Info("CampFever: oblezenie " + st.StringId + " dzien " + days + " - zaraza wstaje.");
            }

            bool whole = s.SiegeSicknessCrowdWholeCamp;
            var att = new Tally(); var def = new Tally();

            // --- OBLEGAJACY ---
            var attackers = new List<MobileParty>(8);
            int campMen = 0;
            if (siege.BesiegerCamp != null)
            {
                foreach (var pb in siege.BesiegerCamp.GetInvolvedPartiesForEventType())
                {
                    var mp = pb != null ? pb.MobileParty : null;
                    if (mp == null || mp.MemberRoster == null || attackers.Contains(mp)) continue;
                    attackers.Add(mp);
                    if (!Undead.Party(mp)) campMen += mp.MemberRoster.TotalManCount;   // umarli nie choruja i nie robia tloku zywym
                }
            }
            float campCrowd = whole ? Crowd(campMen) : -1f;
            att.Corpse = SiegeMisery.CorpseTerm(st, false, campMen);   // 399: niepogrzebani pod murami (stan sprzed tej doby)
            // 471b-2 (D2): przy kluczu Siege Camp Attrition By Medicine oboz choruje tylko w czasie WYBUCHU (szansa dobowa wedlug najlepszej Medycyny obozu),
            // narost od dnia wybuchu; poza wybuchem - bez zarazy obozu. Klucz NIE: narost od konca inkubacji, codziennie (jak dotad).
            float attRamp = ramp;
            bool campSick = !SiegeCamp471.On || SiegeCamp471.Outbreak(siege, st, Math.Max(0, s.SiegeSicknessRampPercent), rampCap, out attRamp);
            if (campSick)
                for (int i = 0; i < attackers.Count; i++)
                    StrikeParty(s, attackers[i], st, attRamp, 1f, campCrowd, false, mainAttacks || mainDefends, false, WinterTerm(attackers[i]), ref att);
            if (SiegeCamp471.On) SiegeCamp471.NoteFever(siege, att.Sick, att.Dead);

            // --- OBRONCY: garnizon + milicja (398) + partie lordow w srodku ---
            var town = st.Town;
            bool hunger = town != null && town.FoodStocks <= 0f;
            var gov = town != null ? town.Governor : null;
            bool pristine = gov != null && gov.GetPerkValue(DefaultPerks.Medicine.PristineStreets);
            float defMul = Math.Max(0, s.SiegeSicknessDefenderFactor) / 100f;
            if (pristine) defMul *= 0.7f;
            if (hunger) defMul *= pristine ? 1.5f : 2f;

            var defenders = new List<MobileParty>(4);
            if (town != null && town.GarrisonParty != null) defenders.Add(town.GarrisonParty);
            var mc = st.MilitiaPartyComponent;
            var militia = mc != null ? mc.MobileParty : null;
            if (militia != null && militia.IsActive && militia.MemberRoster != null && !defenders.Contains(militia)) defenders.Add(militia);
            foreach (var mp in st.Parties)
                if (mp != null && mp.IsLordParty && mp.CurrentSettlement == st && !defenders.Contains(mp)) defenders.Add(mp);
            int wallMen = 0;
            for (int i = 0; i < defenders.Count; i++)
                if (defenders[i].MemberRoster != null && !Undead.Party(defenders[i])) wallMen += defenders[i].MemberRoster.TotalManCount;
            float wallCrowd = whole ? Crowd(wallMen) : -1f;
            bool wallsFight = st.Party != null && st.Party.MapEvent != null;   // 398: szturm albo wypad w toku - cale mury bez zarazy tej doby
            def.Corpse = SiegeMisery.CorpseTerm(st, true, wallMen);   // 399: niepogrzebani w murach
            float wallWinter = WinterTerm(st);
            for (int i = 0; i < defenders.Count; i++)
                StrikeParty(s, defenders[i], st, ramp, defMul, wallCrowd, wallsFight, mainAttacks || mainDefends, true, wallWinter, ref def);

            // 398-p (pomiar): zdrowi na murach po zarazie tej doby - gra oddaje oblezenie bez walki, gdy strona nie ma zdrowych (PlayerEncounter.Update)
            int wallHealthy = 0;
            for (int i = 0; i < defenders.Count; i++)
            {
                try { if (defenders[i].Party != null) wallHealthy += defenders[i].Party.NumberOfHealthyMembers; }
                catch (Exception e) { Stumble("WallHealthy", e); }
            }

            if (att.Sick + def.Sick + att.Dead + def.Dead + att.Capped + def.Capped + att.InBattle + def.InBattle > 0)
                Log.Info("CampFever: " + st.StringId + " dzien " + days + " - oblegajacy " + att.Sick
                         + " chorych/" + att.Dead + " zmarlych, obroncy " + def.Sick + "/" + def.Dead
                         + (hunger ? " (GLOD w miescie)" : "")
                         + " | zdrowi na murach " + wallHealthy + (wallHealthy == 0 && wallMen > 0 ? " (MURY BEZ ZDROWYCH)" : "")
                         + " | narost " + ramp.ToString("0.00") + " (sufit " + (rampCapped ? "tak" : "nie") + ")"
                         + ", tlok " + (whole ? "oboz " + campCrowd.ToString("0.00") + " (" + campMen + " ludzi) / mury " + wallCrowd.ToString("0.00") + " (" + wallMen + " ludzi)" : "na druzyne (stary rachunek)")
                         + " | czynnik oboz " + FactorText(att) + " / mury " + FactorText(def)
                         + " | sufit dobowy trafiony " + (att.Capped + def.Capped) + " partii"
                         + " | pominiete w bitwie " + (att.InBattle + def.InBattle)
                         + " | partie z chorymi i bohaterem: " + (att.WithHero + def.WithHero)
                         + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
        }

        /// <summary>399: "x (zima a, ranni b, trupy c)" - srednia po druzynach strony, ktore zaraza dzis liczyla ("-" gdy zadnej).</summary>
        private static string FactorText(Tally t)
        {
            if (t.Struck <= 0) return "- (trupy " + t.Corpse.ToString("0.00") + ")";
            return (t.FSum / t.Struck).ToString("0.00") + " (zima " + (t.ZSum / t.Struck).ToString("0.00") + ", ranni " + (t.RSum / t.Struck).ToString("0.00") + ", trupy " + t.Corpse.ToString("0.00") + ")";
        }

        /// <summary>Tlok strony: sqrt(ludzie / 500) w widelkach 0.6-2.5.</summary>
        private static float Crowd(int men)
        {
            return MathF.Clamp(MathF.Sqrt(Math.Max(0, men) / 500f), 0.6f, 2.5f);
        }

        /// <summary>398: jedna druzyna jednej strony - bitwa pomija, potem zniwo, pomiar i ksiega ludzi (jak dotad), krzyk do gracza.</summary>
        private static void StrikeParty(Settings s, MobileParty mp, Settlement st, float ramp, float sideMul, float sideCrowd, bool sideFights, bool playerInvolved, bool within, float winter, ref Tally t)
        {
            try
            {
                if (mp == null || mp.MemberRoster == null) return;
                if (sideFights || mp.MapEvent != null) { t.InBattle++; return; }   // 398: roster w bitwie nalezy do bitwy - ta doba bez zarazy
                int sick, dead; bool capped; float factor, woundTerm;
                int m0 = Measure40.MenOf(mp);   // E4/0: stan przed zaraza (MenOf ma wlasny try; tylko pomiar)
                int p0 = PeopleBook.MenOf(mp);  // E4/7: szeregowi bez wightow przed zaraza
                Strike(s, mp, ramp, sideMul, sideCrowd, winter, t.Corpse, out sick, out dead, out capped, out factor, out woundTerm);
                if (factor > 0f) { t.Struck++; t.FSum += factor; t.ZSum += winter; t.RSum += woundTerm; }   // 399: tylko druzyny, ktore zaraza dzis liczyla
                if (dead > 0) SiegeMisery.AddCorpses(st, within, dead, SiegeMisery.SrcFever);   // 399: zmarli zarazy leza niepogrzebani (znacznik, nie ksiega ludzi; AddCorpses nie rzuca)
                try { Measure40.NoteDisease(mp, m0 - Measure40.MenOf(mp), dead); } catch { }   // E4/0: zmarli z rosteru i zadani (U9; tylko pomiar)
                try { int pd = p0 - PeopleBook.MenOf(mp); if (pd > 0) PeopleBook.DieShares(PeopleBook.Take(mp.MemberRoster, pd, PeopleBook.FDiePlague), PeopleBook.FDiePlague); } catch { }   // E4/7 (R12): POLEGLI regionu pochodzenia
                t.Sick += sick; t.Dead += dead;
                if (capped) t.Capped++;
                if (sick > 0 && mp.MemberRoster.TotalHeroes > 0) t.WithHero++;   // tylko licznik (bohaterow zaraza nie dotyka; punkt wpiecia ran - HeroWounds)
                Shout(mp, sick, dead, st, playerInvolved);
            }
            catch (Exception e) { Stumble("StrikeParty", e); }
        }

        /// <summary>Dzienne zniwo w jednej partii. Chorzy ida w rannych, czesc umiera. sideCrowd &lt; 0 = stary tlok z rosteru tej partii.</summary>
        private static void Strike(Settings s, MobileParty mp, float ramp, float sideMul, float sideCrowd, float winter, float corpse, out int sick, out int dead, out bool capped, out float factor, out float woundTerm)
        {
            sick = 0; dead = 0; capped = false; factor = 0f; woundTerm = 0f;
            if (mp == null || mp.MemberRoster == null || sideMul <= 0f) return;
            if (Undead.Party(mp)) return;                      // umarli nie choruja

            var roster = mp.MemberRoster;
            int healthy = 0, regulars = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster.GetCharacterAtIndex(i);
                if (ch == null || ch.IsHero) continue;
                regulars += roster.GetElementNumber(i);
                healthy += roster.GetElementNumber(i) - roster.GetElementWoundedNumber(i);
            }
            if (healthy <= 0) return;

            // 399: czynnik DODAWANY 1 + zima + ranni + trupy, najwyzej FactorMax
            if (s.SiegeFeverWoundedCounts && regulars > 0) woundTerm = MathF.Min(0.5f, (regulars - healthy) / (float)regulars);
            factor = MathF.Min(FactorMax, 1f + Math.Max(0f, winter) + woundTerm + Math.Max(0f, corpse));

            float crowd = sideCrowd >= 0f ? sideCrowd : Crowd(roster.TotalManCount);

            var surgeon = mp.EffectiveSurgeon;
            int med = surgeon != null ? surgeon.GetSkillValue(DefaultSkills.Medicine) : 0;
            float relief = MedicCut(med);
            bool prevent = surgeon != null && surgeon.GetPerkValue(DefaultPerks.Medicine.PreventiveMedicine);
            bool siegeMedic = surgeon != null && surgeon.GetPerkValue(DefaultPerks.Medicine.SiegeMedic);

            float risk = Math.Max(0f, s.SiegeSicknessBasePercent) / 100f
                       * ramp * crowd * sideMul * (1f - relief) * (prevent ? 0.85f : 1f) * factor;
            float expected = healthy * risk;
            // 398: sufit dobowy - druzyna nie traci jednej doby wiecej niz ten udzial zdrowych (0 = bez sufitu)
            float capPct = s.SiegeSicknessDailyCapPercent;
            if (capPct > 0f)
            {
                float max = healthy * capPct / 100f;
                if (expected > max) { expected = max; capped = true; }
            }
            sick = (int)expected;
            if (MBRandom.RandomFloat < expected - sick) sick++;
            if (sick <= 0) return;
            if (sick > healthy) sick = healthy;

            float deathShare = DeathShare() * (siegeMedic ? 0.5f : 1f);
            float dexp = sick * deathShare;
            dead = (int)dexp;
            if (MBRandom.RandomFloat < dexp - dead) dead++;
            int wounded = sick - dead;

            for (int n = 0; n < wounded; n++) HitRandom(roster, false);
            for (int n = 0; n < dead; n++) HitRandom(roster, true);
        }

        /// <summary>Losowy zdrowy szeregowy (wazony liczebnoscia) - ranny albo do grobu.</summary>
        private static void HitRandom(TroopRoster roster, bool kill)
        {
            try
            {
                int pool = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var ch = roster.GetCharacterAtIndex(i);
                    if (ch == null || ch.IsHero) continue;
                    pool += roster.GetElementNumber(i) - roster.GetElementWoundedNumber(i);
                }
                if (pool <= 0) return;
                int pick = MBRandom.RandomInt(pool);
                for (int i = 0; i < roster.Count; i++)
                {
                    var ch = roster.GetCharacterAtIndex(i);
                    if (ch == null || ch.IsHero) continue;
                    int h = roster.GetElementNumber(i) - roster.GetElementWoundedNumber(i);
                    if (h <= 0) continue;
                    if (pick >= h) { pick -= h; continue; }
                    if (kill) roster.AddToCounts(ch, -1);
                    else roster.WoundTroop(ch);
                    return;
                }
            }
            catch { }
        }

        private static void Shout(MobileParty mp, int sick, int dead, Settlement st, bool playerInvolved)
        {
            try
            {
                if (sick <= 0 || mp != MobileParty.MainParty) return;
                string line = "Camp fever: " + sick + " of your men down with the flux"
                              + (dead > 0 ? ", " + dead + " dead" : "") + ".";
                Log.Player(line, dead > 0);
            }
            catch { }
        }
    }
}
