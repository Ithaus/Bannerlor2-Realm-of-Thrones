using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Generic;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>401 (Jeff 11.11 "rob 4" - rany i blizny bohaterow; PLAN-396 commit 401) - KSIEGA RAN I LOZE. Bez smierci, bez okaleczen, bez AI (403, 408).
    ///
    /// Wyzwalacz: CampaignEvents.HeroWounded (gra wola go w seterze Hero.HitPoints przy przejsciu ponad prog rany -> pod prog). Liczy sie tylko rana W BITWIE
    /// (partia bohatera w MapEvent albo gracz w PlayerEncounter.Battle), lord / gracz / towarzysz i rod gracza, dorosly, zywy, bez znaku smierci, nie
    /// Occupation.Special, nie Bialy Wedrowiec, bez czynnej rany Kroniki (Chronicle.Actions - kanon ma pierwszenstwo), bez wpisu w ksiedze.
    /// Rzut: Grave (Hero Wounds Grave Percent + 0.4 pp za rok ponad 50, sufit 20%), Serious (Hero Wounds Serious Percent), reszta Light (tylko licznik).
    /// Bohater potrzebny fabule ROT (StoryGuard.Needed): Grave -> Serious, a loze konczy sie najpozniej 7 dni przed jego wydarzeniem (minelo -> Light).
    ///
    /// Loze = HP nie przekracza progu rany (Hero.WoundedHealthLimit - model, nie stala 20) do konca terminu; bohater zostaje w rosterze "ranny" (nie staje w misji
    /// ani w symulacji, pobity idzie do niewoli z szansa 1.0 - zasady gry). Dwie latki: prefiks Hero.HealByAmountInternal (przycina leczenie do progu; przy zerze
    /// pomija metode - chirurg nie bierze XP Medycyny za leczenie bez skutku) i prefiks setera Hero.HitPoints (sciezki bez Heal: bohater bez partii, cudze mody).
    /// Seter NIE przenosi przez prog: rusza tylko wartosc ponad prog przy biezacym HP pod progiem, wiec zadnego HeroWounded i zadnego przeskoku rostera.
    /// Doba: pozostalo -1; -2 w nieoblezonej twierdzy / miescie (Hero Wounds Fortress Rest Double); jeniec zawsze -1. Koniec terminu: wpis znika, gra leczy dalej.
    /// Przesilenie rany Grave (dzien rany + 3..10) jest w zapisie od razu; w 401 mija BEZ rzutu (liczone) - rzut smierci dopisze 403.
    ///
    /// Rana krytyczna RealisticBannerlord (15% przy kazdym HeroWounded, -4 HP/dobe) - wylaczona prefiksem przy kluczu Hero Wounds Replace Rb Critical (jedna zasada).
    /// Zapis arm_herowounds_v1: wiersz "id|stopien|dzienRany|pozostalo|dzienPrzesilenia|miejsce|zrodlo" (stary zapis = pusta ksiega). Statyki: Reset w konstruktorze
    /// ArmouryBehavior i w OnGameEnd; bohaterowie rozwiazywani w SessionStart. Potkniecia liczone per miejsce - nic sie nie gasi.</summary>
    internal static class HeroWounds
    {
        private const string Tag = "Rany (HW): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const int Serious = 1, Grave = 2;

        private sealed class Row
        {
            public string Id = "", Place = "", Src = "";
            public int Grade, Left, Crisis;      // Crisis: dzien kampanii przesilenia (Grave); 0 = brak; -1 = minelo
            public double Day;                   // dzien kampanii rany (z ulamkiem)
            public Hero H;
        }

        // ---------------------------------------------------------------- stan (zapis: _rows)
        private static readonly Dictionary<string, Row> _rows = new Dictionary<string, Row>(StringComparer.Ordinal);
        private static readonly Dictionary<Hero, Row> _bed = new Dictionary<Hero, Row>();   // lezacy (rozwiazani bohaterowie) - czytane przez latki
        private static int _n;                                                              // _bed.Count - szybkie wyjscie latek

        // ---------------------------------------------------------------- latki (raz na proces)
        private static Harmony _harmony;
        private static bool _tried;
        private static MethodInfo _mHeal, _mSet, _mRb, _mEnc, _mTip;
        private static bool _rbPresent;
        private static AccessTools.FieldRef<EncyclopediaHeroPageVM, Hero> _encHero;

        // ---------------------------------------------------------------- liczniki (poza zapisem)
        private static int _dEv, _dBattle, _dMission, _dSim, _dLight, _dSerious, _dGrave, _dGuard, _dNoBattle, _dOut, _dCanon, _dHas, _dRose, _dCrisis, _dGone, _dRb;
        private static int _sBattle, _sSerious, _sGrave, _sDays;
        private static int _healCuts, _setCuts, _dStumbles, _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);

        internal static void SetHarmony(Harmony h) { _harmony = h; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_errSites.Add(where)) Log.Error("HeroWounds." + where, e); } catch { }
        }

        private static string YN(bool b) { return b ? "TAK" : "NIE"; }
        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "?"; } catch { return "?"; } }
        private static bool KeyOn { get { var s = Settings.Current; return s != null && s.HeroWoundsEnabled; } }
        private static string Clean(string t) { return string.IsNullOrEmpty(t) ? "" : t.Replace('|', '/').Replace('\n', ' ').Replace('\r', ' '); }
        private static string GradeName(int g) { return g == Grave ? "GRAVE" : g == Serious ? "SERIOUS" : "LIGHT"; }

        // ---------------------------------------------------------------- cykl zycia
        /// <summary>Konstruktor ArmouryBehavior (nowa kampania i wczytanie - przed SyncData) oraz OnGameEnd: ksiega i liczniki od zera.</summary>
        internal static void Reset()
        {
            _rows.Clear(); _bed.Clear(); _n = 0; _errSites.Clear();
            _dEv = _dBattle = _dMission = _dSim = _dLight = _dSerious = _dGrave = _dGuard = _dNoBattle = _dOut = _dCanon = _dHas = _dRose = _dCrisis = _dGone = _dRb = 0;
            _sBattle = _sSerious = _sGrave = _sDays = 0;
            _healCuts = _setCuts = _dStumbles = _stumbles = 0;
        }

        internal static string Export()
        {
            if (_rows.Count == 0) return "";
            var sb = new StringBuilder(64 * _rows.Count);
            foreach (var r in _rows.Values)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(r.Id).Append('|').Append(r.Grade).Append('|').Append(r.Day.ToString("0.00", Inv)).Append('|').Append(r.Left).Append('|').Append(r.Crisis)
                  .Append('|').Append(Clean(r.Place)).Append('|').Append(Clean(r.Src));
            }
            return sb.ToString();
        }

        /// <summary>Wczytanie: same wiersze (bohaterow rozwiazuje SessionStart - w SyncData swiat nie musi byc gotowy). Zly wiersz pominiety z potknieciem.</summary>
        internal static void Import(string text)
        {
            _rows.Clear(); _bed.Clear(); _n = 0;
            if (string.IsNullOrEmpty(text)) return;
            foreach (var line in text.Split('\n'))
            {
                try
                {
                    var p = line.Split('|');
                    if (p.Length < 5 || p[0].Length == 0) continue;
                    var r = new Row { Id = p[0], Grade = int.Parse(p[1], Inv), Day = double.Parse(p[2], Inv), Left = int.Parse(p[3], Inv), Crisis = int.Parse(p[4], Inv),
                                      Place = p.Length > 5 ? p[5] : "", Src = p.Length > 6 ? p[6] : "" };
                    if (r.Grade != Serious && r.Grade != Grave) continue;
                    _rows[r.Id] = r;
                }
                catch (Exception e) { Stumble("Import", e); }
            }
        }

        /// <summary>OnSessionLaunched (po StoryGuard): bohaterowie ksiegi rozwiazani, latki raz na proces, linia startu.</summary>
        internal static void SessionStart()
        {
            try
            {
                _bed.Clear();
                List<string> gone = null;
                foreach (var r in _rows.Values)
                {
                    Hero h = null;
                    try { string id = r.Id; h = Hero.FindFirst(x => x != null && x.StringId == id); } catch (Exception e) { Stumble("SessionStart.Find", e); }
                    if (h == null || !h.IsAlive) { (gone ?? (gone = new List<string>())).Add(r.Id); continue; }
                    r.H = h; _bed[h] = r;
                }
                if (gone != null) foreach (var id in gone) _rows.Remove(id);
                _n = _bed.Count;
                Wire();
                var s = Settings.Current;
                int sr = 0, gr = 0;
                foreach (var r in _rows.Values) { if (r.Grade == Grave) gr++; else sr++; }
                Log.Info("Rany bohaterow (HW): start | klucze: wlaczone " + YN(s != null && s.HeroWoundsEnabled)
                         + (s != null ? ", serious " + s.HeroWoundsSeriousPercent.ToString("0.#", Inv) + "% / " + s.HeroWoundsSeriousDays + " dni, grave "
                                        + s.HeroWoundsGravePercent.ToString("0.#", Inv) + "% / " + s.HeroWoundsGraveDays + " dni, twierdza x2 " + YN(s.HeroWoundsFortressRestDouble)
                                        + ", gracz " + YN(s.HeroWoundsPlayer) + " (loze " + s.HeroWoundsPlayerBedPercent + "%), towarzysze i rod gracza " + YN(s.HeroWoundsCompanions)
                                        + ", zamiast rany krytycznej RB " + YN(s.HeroWoundsReplaceRbCritical) : "")
                         + " | latki: leczenie (Hero.HealByAmountInternal) " + YN(_mHeal != null) + ", seter HP " + YN(_mSet != null)
                         + ", RB krytyczna " + (_mRb != null ? "TAK" : _rbPresent ? "BLAD" : "BRAK (nie ma RealisticBannerlord)")
                         + ", encyklopedia " + YN(_mEnc != null) + ", dymek " + YN(_mTip != null)
                         + " | ksiega " + _rows.Count + " (serious " + sr + ", grave " + gr + (gone != null ? "; usuniete bez bohatera " + gone.Count : "") + ")"
                         + " | 401: bez smierci, okaleczen i AI (przesilenie mija bez rzutu).");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        private static void Wire()
        {
            if (_tried || _harmony == null || Campaign.Current == null) return;
            _tried = true;
            // wszystkie cele zadeklarowane w podanych klasach (DeclaredMethod / DeclaredPropertySetter - pulapka 15.10 nie dotyczy)
            _mHeal = Patch("Hero.HealByAmountInternal", () => AccessTools.DeclaredMethod(typeof(Hero), "HealByAmountInternal", new[] { typeof(int), typeof(bool) }),
                           nameof(HealPrefix), null);
            _mSet = Patch("Hero.set_HitPoints", () => AccessTools.DeclaredPropertySetter(typeof(Hero), "HitPoints"), nameof(HpSetPrefix), null);
            Type rb = null;
            try { rb = AccessTools.TypeByName("RealisticBannerlord.Systems.Medicine.CriticalWoundBehavior"); } catch { rb = null; }
            _rbPresent = rb != null;
            if (rb != null) _mRb = Patch("RealisticBannerlord CriticalWoundBehavior.OnHeroWounded", () => AccessTools.DeclaredMethod(rb, "OnHeroWounded", new[] { typeof(Hero) }),
                                         nameof(RbCriticalPrefix), null);
            try { _encHero = AccessTools.FieldRefAccess<EncyclopediaHeroPageVM, Hero>("_hero"); } catch (Exception e) { _encHero = null; Log.Error("HeroWounds.Wire _hero", e); }
            if (_encHero != null)
                _mEnc = Patch("EncyclopediaHeroPageVM.Refresh", () => AccessTools.DeclaredMethod(typeof(EncyclopediaHeroPageVM), "Refresh", Type.EmptyTypes), null, nameof(EncPostfix));
            _mTip = Patch("TooltipRefresherCollection.RefreshHeroTooltip",
                          () => AccessTools.DeclaredMethod(typeof(TooltipRefresherCollection), "RefreshHeroTooltip", new[] { typeof(PropertyBasedTooltipVM), typeof(object[]) }),
                          null, nameof(TipPostfix));
        }

        /// <summary>Zwraca MethodInfo tylko, gdy latka weszla (null = brak celu albo blad Harmony).</summary>
        private static MethodInfo Patch(string label, Func<MethodInfo> find, string pre, string post)
        {
            try
            {
                var m = find();
                if (m == null) { Log.Info(Tag + "BLAD latki 401: nie ma metody " + label + "."); return null; }
                _harmony.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(HeroWounds), pre) : null, postfix: post != null ? new HarmonyMethod(typeof(HeroWounds), post) : null);
                return m;
            }
            catch (Exception e) { Log.Error("HeroWounds.Patch " + label, e); return null; }
        }

        // ---------------------------------------------------------------- API (373, FieldDuel, pozniej 402/403/408)
        /// <summary>Bohater lezy (czynny wpis Serious albo Grave).</summary>
        internal static bool Abed(Hero h) { return _n != 0 && h != null && _bed.ContainsKey(h); }

        /// <summary>0 = bez wpisu, 1 = Serious, 2 = Grave.</summary>
        internal static int GradeOf(Hero h) { Row r; return _n != 0 && h != null && _bed.TryGetValue(h, out r) ? r.Grade : 0; }

        /// <summary>Dni spoczynku do konca loza (0 = bez wpisu).</summary>
        internal static int DaysLeft(Hero h) { Row r; return _n != 0 && h != null && _bed.TryGetValue(h, out r) ? Math.Max(1, r.Left) : 0; }

        /// <summary>Dzien kampanii (z ulamkiem), od ktorego bohater lezy; double.MaxValue gdy nie lezy (porownanie "lezal przed bitwa" daje wtedy NIE).</summary>
        internal static double BedriddenSince(Hero h) { Row r; return _n != 0 && h != null && _bed.TryGetValue(h, out r) ? r.Day : double.MaxValue; }

        /// <summary>Bohater lezal juz PRZED ta bitwa (rana sprzed jej poczatku) - nie jest "rannym z tej bitwy" (SiegeWounded373).</summary>
        internal static bool AbedBefore(Hero h, MapEvent me)
        {
            try
            {
                if (_n == 0 || h == null || me == null) return false;
                Row r;
                return _bed.TryGetValue(h, out r) && r.Day < me.BattleStartTime.ToDays;
            }
            catch (Exception e) { Stumble("AbedBefore", e); return false; }
        }

        /// <summary>Rana Grave, ktorej przesilenie jeszcze nie minelo (403: rzut smierci; 402: goraczka lochu pomija).</summary>
        internal static bool GraveCrisisAhead(Hero h) { Row r; return _n != 0 && h != null && _bed.TryGetValue(h, out r) && r.Grade == Grave && r.Crisis > 0; }

        // ---------------------------------------------------------------- latki
        /// <summary>Hero.HealByAmountInternal(int healingAmount, bool addXp): lezacemu leczenie przyciete do progu rany; nic do wyleczenia = metoda pominieta
        /// (bez XP Medycyny dla chirurga). Bohater ponad progiem (nie powinno sie zdarzyc) - bez zmian.</summary>
        private static bool HealPrefix(Hero __instance, ref int healingAmount)
        {
            if (_n == 0) return true;
            try
            {
                if (!_bed.ContainsKey(__instance) || !KeyOn) return true;
                int hp = __instance.HitPoints, lim = __instance.WoundedHealthLimit;
                if (hp > lim) return true;
                int room = lim - hp;
                if (room <= 0) { _healCuts++; return false; }
                if (healingAmount > room) { healingAmount = room; _healCuts++; }
                return true;
            }
            catch (Exception e) { Stumble("HealPrefix", e); return true; }
        }

        /// <summary>Seter Hero.HitPoints: lezacemu wartosc ponad prog rany przycieta do progu - tylko gdy biezace HP jest pod progiem albo na nim (nigdy nie
        /// przenosi przez prog, wiec gra nie wola HeroWounded ani nie przestawia rosterow).</summary>
        private static void HpSetPrefix(Hero __instance, ref int value)
        {
            if (_n == 0) return;
            try
            {
                if (!_bed.ContainsKey(__instance) || !KeyOn) return;
                int lim = __instance.WoundedHealthLimit;
                if (value <= lim || __instance.HitPoints > lim) return;
                value = lim; _setCuts++;
            }
            catch (Exception e) { Stumble("HpSetPrefix", e); }
        }

        /// <summary>RealisticBannerlord CriticalWoundBehavior.OnHeroWounded: przy naszych kluczach nie losuje swojej rany krytycznej (stare wpisy RB wygasaja same).</summary>
        private static bool RbCriticalPrefix()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HeroWoundsEnabled || !s.HeroWoundsReplaceRbCritical || Campaign.Current == null) return true;
                _dRb++;
                return false;
            }
            catch { return true; }
        }

        private static string StateText(Row r)
        {
            return r.Grade == Grave && r.Crisis > 0 ? "Gravely wounded - the crisis has not passed" : "Bedridden (" + Math.Max(1, r.Left) + " days of rest left)";
        }

        /// <summary>Encyklopedia bohatera: wiersz "Wounds" w statystykach lezacego (nie przy ukrytych informacjach o bohaterze).</summary>
        private static void EncPostfix(EncyclopediaHeroPageVM __instance)
        {
            if (_n == 0) return;
            try
            {
                if (_encHero == null || __instance == null || __instance.IsInformationHidden || __instance.Stats == null) return;
                var h = _encHero(__instance);
                Row r;
                if (h == null || !_bed.TryGetValue(h, out r)) return;
                __instance.Stats.Add(new StringPairItemVM("Wounds:", StateText(r)));
            }
            catch (Exception e) { Stumble("EncPostfix", e); }
        }

        /// <summary>Dymek bohatera: jedna linia o lozu.</summary>
        private static void TipPostfix(PropertyBasedTooltipVM propertyBasedTooltipVM, object[] args)
        {
            if (_n == 0) return;
            try
            {
                var h = args != null && args.Length > 0 ? args[0] as Hero : null;
                Row r;
                if (h == null || propertyBasedTooltipVM == null || !_bed.TryGetValue(h, out r)) return;
                propertyBasedTooltipVM.AddProperty("", StateText(r));
            }
            catch (Exception e) { Stumble("TipPostfix", e); }
        }

        // ---------------------------------------------------------------- rana
        private static bool Walker(Hero h) { var c = h.CharacterObject; return c != null && c.Race == 1; }

        /// <summary>Komunikat tylko, gdy rzecz dotyczy gracza: on sam, jego rod i towarzysze, jego jeniec, a przy ranie Grave takze lord jego krolestwa.</summary>
        private static bool Concerns(Hero h, bool grave)
        {
            try
            {
                if (h == Hero.MainHero || h.Clan == Clan.PlayerClan || h.IsPlayerCompanion) return true;
                var cap = h.PartyBelongedToAsPrisoner;
                if (cap != null && (cap == PartyBase.MainParty || (cap.IsSettlement && cap.Settlement != null && cap.Settlement.OwnerClan == Clan.PlayerClan))) return true;
                var mf = Hero.MainHero != null ? Hero.MainHero.MapFaction : null;
                return grave && mf != null && mf.IsKingdomFaction && h.MapFaction == mf;
            }
            catch { return false; }
        }

        private static string PlaceOf(Hero h, MapEvent me)
        {
            try
            {
                var st = me.MapEventSettlement;
                if (st != null && st.Name != null) return "at " + st.Name;
                var mp = h.PartyBelongedTo ?? MobileParty.MainParty;
                if (mp == null) return "in the field";
                Vec2 pos = mp.GetPosition2D;
                Settlement best = null;
                float bd = float.MaxValue;
                foreach (var s in Settlement.All)
                {
                    if (s == null || !(s.IsTown || s.IsCastle || s.IsVillage)) continue;
                    float d = s.GetPosition2D.DistanceSquared(pos);
                    if (d < bd) { bd = d; best = s; }
                }
                return best != null && best.Name != null ? "near " + best.Name : "in the field";
            }
            catch { return "in the field"; }
        }

        /// <summary>CampaignEvents.HeroWounded: rzut stopnia rany i wpis do ksiegi. Wolane z wnetrza setera HP - bez zmian HP, bez akcji gry.</summary>
        internal static void OnHeroWounded(Hero h)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HeroWoundsEnabled || h == null || Campaign.Current == null) return;
                _dEv++;
                bool player = h == Hero.MainHero;
                bool mine = !player && (h.IsPlayerCompanion || h.Clan == Clan.PlayerClan);
                if (!h.IsAlive || h.IsChild || h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None || h.Occupation == Occupation.Special || Walker(h)
                    || (player ? !s.HeroWoundsPlayer : mine ? !s.HeroWoundsCompanions : !h.IsLord))
                { _dOut++; return; }
                var hp = h.PartyBelongedTo;
                MapEvent me = hp != null ? hp.MapEvent : null;
                if (me == null && player) { try { me = PlayerEncounter.Battle; } catch { me = null; } }
                if (me == null) { _dNoBattle++; return; }
                string id = h.StringId;
                if (string.IsNullOrEmpty(id)) { _dOut++; return; }
                if (_rows.ContainsKey(id)) { _dHas++; return; }
                if (Chronicle.Actions.HasWound(id)) { _dCanon++; return; }

                bool mission = false;
                try { mission = TaleWorlds.MountAndBlade.Mission.Current != null; } catch { mission = false; }
                _dBattle++; _sBattle++;
                if (mission) _dMission++; else _dSim++;

                // rzut stopnia
                float age = h.Age;
                float gp = Math.Max(0f, Math.Min(100f, s.HeroWoundsGravePercent));
                if (age > 50f && gp > 0f) gp = Math.Min(Math.Max(20f, gp), gp + 0.4f * (age - 50f));
                float sp = Math.Max(0f, Math.Min(100f, s.HeroWoundsSeriousPercent));
                float roll = MBRandom.RandomFloat * 100f;
                int grade = roll < gp ? Grave : roll < gp + sp ? Serious : 0;
                string guard = "nie";
                bool needed = false;
                if (grade != 0)
                {
                    needed = StoryGuard.Needed(h);
                    if (needed && grade == Grave) { grade = Serious; guard = "Grave -> Serious"; }
                    else if (needed) guard = "chroniony";
                }
                int days = 0;
                if (grade != 0)
                {
                    int baseDays = Math.Max(1, grade == Grave ? s.HeroWoundsGraveDays : s.HeroWoundsSeriousDays);
                    days = Math.Max(1, (int)Math.Round(baseDays * (0.67f + 0.66f * MBRandom.RandomFloat)));
                    if (player) days = (int)Math.Round(days * Math.Max(0, Math.Min(100, s.HeroWoundsPlayerBedPercent)) / 100.0);
                    if (needed)
                    {
                        double need = StoryGuard.NextNeedDay(h);
                        if (need >= 0)
                        {
                            int cap = (int)Math.Floor(need - 7.0 - CanonCompanions.StoryDay());
                            if (cap < days) { days = cap; guard += ", loze do 7 dni przed wydarzeniem (plan " + need.ToString("0", Inv) + ")"; }
                        }
                    }
                    if (days <= 0) { grade = 0; guard += ", loze 0 -> Light"; }
                }
                string where = PlaceOf(h, me);
                if (grade == 0)
                {
                    _dLight++;
                    if (s.HeroWoundsLog)
                        Log.Info(Tag + Nm(h) + " (" + id + ", wiek " + ((int)age) + ") ranny " + where + " [" + (mission ? "misja" : "symulacja") + "] - rzut "
                                 + (roll / 100f).ToString("0.00", Inv) + " (grave < " + (gp / 100f).ToString("0.00", Inv) + ", serious < " + ((gp + sp) / 100f).ToString("0.00", Inv)
                                 + ") -> LIGHT | straz fabuly: " + guard + ".");
                    return;
                }
                double now = CampaignTime.Now.ToDays;
                var r = new Row { Id = id, Grade = grade, Day = now, Left = days, Place = where, Src = mission ? "misja" : "symulacja", H = h,
                                  Crisis = grade == Grave ? (int)now + 3 + MBRandom.RandomInt(8) : 0 };
                _rows[id] = r; _bed[h] = r; _n = _bed.Count;
                if (grade == Grave) { _dGrave++; _sGrave++; } else { _dSerious++; _sSerious++; }
                if (needed) _dGuard++;
                _sDays += days;
                if (s.HeroWoundsLog)
                    Log.Info(Tag + Nm(h) + " (" + id + ", wiek " + ((int)age) + (h.Clan != null && h.Clan.Name != null ? ", " + h.Clan.Name : "") + ") ranny " + where
                             + " [" + r.Src + "] - rzut " + (roll / 100f).ToString("0.00", Inv) + " (grave < " + (gp / 100f).ToString("0.00", Inv) + ", serious < "
                             + ((gp + sp) / 100f).ToString("0.00", Inv) + ") -> " + GradeName(grade) + ", loze " + days + " dni spoczynku"
                             + (grade == Grave ? ", przesilenie dnia " + r.Crisis + " (401: bez rzutu smierci)" : "") + " | HP " + h.HitPoints + ", prog " + h.WoundedHealthLimit
                             + " | straz fabuly: " + guard + ".");
                try
                {
                    if (player)
                        Log.Player((grade == Grave ? "You are gravely wounded." : "You have taken a serious wound.") + " You can ride, but you cannot fight for about " + days + " days."
                                   + (s.HeroWoundsFortressRestDouble ? " Rest within castle or town walls mends you twice as fast." : ""), true);
                    else if (Concerns(h, grade == Grave))
                        Log.Player(grade == Grave
                            ? Nm(h) + " lies gravely wounded. The healers cannot say whether " + (h.IsFemale ? "she" : "he") + " will live."
                            : Nm(h) + " has taken a serious wound and will be abed for about " + days + " days.", h.MapFaction == Hero.MainHero.MapFaction);
                }
                catch (Exception e) { Stumble("OnHeroWounded.Player", e); }
            }
            catch (Exception e) { Stumble("OnHeroWounded", e); }
        }

        // ---------------------------------------------------------------- doba
        /// <summary>Doba ran (po StoryGuard.Daily, przed zaraza): termin loza, przesilenie bez rzutu (401), wstajacy, sprzatanie, linia doby.</summary>
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                var s = Settings.Current;
                int day = (int)CampaignTime.Now.ToDays;
                int sr = 0, gr = 0, pris = 0, fort = 0;
                List<Row> done = null;
                foreach (var r in _rows.Values)
                {
                    try
                    {
                        var h = r.H;
                        if (h == null || !h.IsAlive)
                        {
                            (done ?? (done = new List<Row>())).Add(r); _dGone++;
                            Log.Info(Tag + (h != null ? Nm(h) : r.Id) + " - wpis usuniety: " + (h == null ? "brak bohatera w grze" : "nie zyje") + " (rana " + GradeName(r.Grade) + " " + r.Place + ").");
                            continue;
                        }
                        int step = 1;
                        if (h.IsPrisoner) pris++;
                        else
                        {
                            var cs = h.CurrentSettlement;
                            if (cs != null && cs.IsFortification && !cs.IsUnderSiege) { fort++; if (s == null || s.HeroWoundsFortressRestDouble) step = 2; }
                        }
                        r.Left -= step;
                        if (r.Grade == Grave && r.Crisis > 0 && day >= r.Crisis)
                        {
                            r.Crisis = -1; _dCrisis++;
                            if (s == null || s.HeroWoundsLog)
                                Log.Info(Tag + Nm(h) + " (" + r.Id + ", wiek " + ((int)h.Age) + ") - przesilenie minelo bez rzutu smierci (401) | jeniec " + YN(h.IsPrisoner)
                                         + ", w twierdzy " + YN(step == 2 || (!h.IsPrisoner && h.CurrentSettlement != null && h.CurrentSettlement.IsFortification)) + ".");
                        }
                        if (r.Left <= 0)
                        {
                            (done ?? (done = new List<Row>())).Add(r); _dRose++;
                            if (s == null || s.HeroWoundsLog)
                                Log.Info(Tag + Nm(h) + " (" + r.Id + ") wstal z loza - rana " + GradeName(r.Grade) + " z dnia " + r.Day.ToString("0", Inv) + " " + r.Place
                                         + ", HP " + h.HitPoints + " (gra leczy dalej).");
                            try
                            {
                                if (KeyOn)
                                {
                                    if (h == Hero.MainHero) Log.Player("You have risen from the sickbed.");
                                    else if (Concerns(h, r.Grade == Grave)) Log.Player(Nm(h) + " has risen from the sickbed.");
                                }
                            }
                            catch (Exception e) { Stumble("Daily.Player", e); }
                            continue;
                        }
                        if (r.Grade == Grave) gr++; else sr++;
                    }
                    catch (Exception e) { Stumble("Daily(" + r.Id + ")", e); }
                }
                if (done != null)
                {
                    foreach (var r in done) { _rows.Remove(r.Id); if (r.H != null) _bed.Remove(r.H); }
                    _n = _bed.Count;
                }
                if (_dEv + _dRose + _dCrisis + _dGone + _dStumbles + _dRb + _rows.Count + _healCuts + _setCuts > 0)
                    Log.Info(Tag + "dzien " + day + " | rany w bitwie dzis " + _dBattle + " (misja " + _dMission + " / symulacja " + _dSim + "): light " + _dLight + ", serious "
                             + _dSerious + ", grave " + _dGrave + ", chronieni fabula " + _dGuard + " | zdarzen HeroWounded " + _dEv + " (poza bitwa " + _dNoBattle + ", poza zakresem "
                             + _dOut + ", rana Kroniki " + _dCanon + ", juz na lozu " + _dHas + ") | na lozu " + sr + " serious / " + gr + " grave (jency " + pris + ", w twierdzy " + fort
                             + ") | wstali " + _dRose + ", przesilenia bez rzutu " + _dCrisis + ", wpisy bez bohatera " + _dGone + " | przyciecia: leczenie " + _healCuts + ", seter "
                             + _setCuts + " | RB rana krytyczna pominieta " + _dRb + " | od startu sesji: ran w bitwie " + _sBattle + ", serious " + _sSerious + ", grave " + _sGrave
                             + ", dni loza " + _sDays + " | potkniecia " + _dStumbles + " (sesja " + _stumbles + ").");
                _dEv = _dBattle = _dMission = _dSim = _dLight = _dSerious = _dGrave = _dGuard = _dNoBattle = _dOut = _dCanon = _dHas = _dRose = _dCrisis = _dGone = _dRb = 0;
                _healCuts = _setCuts = _dStumbles = 0;
            }
            catch (Exception e) { Stumble("Daily", e); }
        }
    }
}
