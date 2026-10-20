using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// p6-okup (R3-03a, Jeff 20.10 "pozostale tak"; docs/POMYSLY-35-2026-10-20.md poz. 4, audyt 10-pomysly Z10, 04-wojna N4) - OKUP OKOLICY ZAMIAST PALENIA WSI
    /// (patis / appatis wojny stuletniej: zalogi i kompanie zyly z oplat okolicy "za ochrone" zamiast palic). Klucz RaidTributeEnabled.
    ///
    /// REGULA (jedna dla AI i gracza):
    ///  - sila: q = ludzie napastnika (partia albo cala armia, zdrowi) / obroncy wsi (milicja + zdrowi ludzie partii stojacych we wsi, wrogich napastnikowi).
    ///    q &lt; 1 - wies stawia opor, okupu nie ma (rabunek idzie jak w grze: najpierw bitwa z milicja);
    ///  - stawka r = RaidTributeShareLow + (High - Low) x min(1, (q - 1) / 4): przewaga 1:1 - 10%, 3:1 - 15%, 5:1 i wiecej - 20% kiesy wsi (Village.Gold - ta sama
    ///    kiesa, z ktorej placi lup rabunku SmallGold.LootPayer: utarg wsi zostaje we wsi, paczka 112);
    ///  - okup T = floor(r x kiesa wsi); placi wies (GiveGoldAction strona wsi -&gt; wodz napastnika, jak lup E7/R1: ksiega pieniadza widzi przelew wies -&gt; lord/gracz,
    ///    ksiega dochodu rodow - wplyw spoza rodu), nic z niczego;
    ///  - po okupie wies jest CHRONIONA przed krolestwem napastnika (MapFaction - krolestwo albo rod bez krolestwa) RaidTributeSpareDays dob: jego lordowie jej nie
    ///    rabuja (opcja rabunku AI wycieta w mysli, a partia, ktora i tak dojdzie - staje i mysli od nowa) i nie zadaja drugi raz okupu; gracz z tego krolestwa
    ///    nie moze jej spladrowac ani zabrac zapasow (opcje gry zablokowane z dymkiem). Inne krolestwa w wojnie - bez zmian;
    ///  - relacja: wodz z panem wsi -2, z notablami -1 (rabunek gry: -6 i -3 - CharacterRelationCampaignBehavior.OnRaidCompleted; ta sama wyjatkowosc wsi rodu gracza).
    ///
    /// AI (lord albo wodz armii AI, wojna z panem wsi): chwila decyzji = przybycie do wsi z rozkazem rabunku - prefiks EncounterManager.StartSettlementEncounter
    /// z tym samym warunkiem co galaz rabunku gry (G EncounterManager:221 -&gt; StartBattleAction.ApplyStartRaid). Okup (spotkanie nie zachodzi, partia SetMoveModeHold
    /// i RethinkAtNextHourlyTick - jak gra przy wsi juz spladrowanej, G :147-150) albo rabunek (gra bez zmian), z biezacego zysku:
    ///  - Zelazni Ludzie i Wolni Ludzie (ReaverLaw.IsReaver) - zawsze rabunek (zelazna cena, nie zloto okupu);
    ///  - wies stawia opor (q &lt; 1) - rabunek;
    ///  - armia glodna (jedzenia na mniej niz HungryDays dob, srednio na druzyne jak gra) - rabunek (spichlerz okregu karmi napastnikow, E7/R1 K4);
    ///  - rod w potrzebie (CitySack.NeedOf.InNeed - ta sama miara co okup oblezenia: zalegly zold albo kiesa rodu ponizej 14 dob zoldu druzyn rodu) - rabunek;
    ///  - okup ponizej doby zoldu armii (NeedOf.ArmyWage) - za malo, zeby oszczedzac wies - rabunek;
    ///  - rabunek oplaca sie bardziej: T &lt; E - C x D, gdzie E = s x (kiesa + wartosc magazynu wsi) - lup kroku, D - doby kroku (DistrictRaid.StepEstimate - ta sama
    ///    arytmetyka co krok R1), C = doba zoldu armii - rabunek; inaczej OKUP (pewny i od razu, bez spustoszenia i uchodzcow).
    /// GRACZ: opcja "Demand tribute" w menu "village_hostile_action" (tylko w wojnie z panem wsi) - dymek z kwota, stawka, sila i dniami ochrony; zablokowana z powodem,
    /// gdy wies stawia opor, ma pusta kiese albo juz zaplacila krolestwu gracza. Wynik: zloto do gracza, ochrona, relacja, komunikat, powrot do menu wsi
    /// (zwykle menu, nie oczekiwania - GameMenu.SwitchToMenu wolno, jak "Forget it" gry).
    /// WATKI: postfiks mysli AI (CampaignEventDispatcher.AiHourlyTick) czyta tylko migawke ochrony (slownik podmieniany w calosci - publikacja jednym zapisem
    /// referencji, bez blokad i bez alokacji), liczniki Interlocked. Reszta - watek kampanii.
    /// ZAPIS: klucz arm_raidtribute (wies, frakcja, do doby, doba, zloto); brak klucza (stary zapis, nowa kampania) = brak ochron. Reset w konstruktorze ArmouryBehavior.
    /// Linia doby "Okup okolicy (R3-03a): dzien N" i linia startu sesji. Wyjatek per zdarzenie - licznik potkniec, bez globalnego wylacznika (CLAUDE.md 7).
    /// </summary>
    internal static class RaidTribute
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const string P = "Okup okolicy (R3-03a): ";
        internal const string OptId = "arm_raid_tribute";
        private const double HungryDays = 5.0;          // armia z jedzeniem na mniej niz tyle dob rabuje (spichlerz okregu)
        private const double FullAt = 5.0;              // przewaga, przy ktorej stawka dochodzi do gornej (q = 5:1)
        private const int RelLord = -2, RelNotable = -1; // rabunek gry: -6 i -3 (pelny rabunek)
        private const int MaxExamples = 3;

        internal static bool On { get { var s = Settings.Current; return s != null && s.RaidTributeEnabled; } }
        private static double Low { get { var s = Settings.Current; return s == null ? 0.10 : Clamp(s.RaidTributeShareLow, 0.0, 1.0); } }
        private static double High { get { var s = Settings.Current; return s == null ? 0.20 : Math.Max(Low, Clamp(s.RaidTributeShareHigh, 0.0, 1.0)); } }
        private static int SpareDays { get { var s = Settings.Current; return s == null ? 30 : Math.Max(1, Math.Min(364, s.RaidTributeSpareDays)); } }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        private static double Clamp(double x, double lo, double hi) { return double.IsNaN(x) ? lo : (x < lo ? lo : (x > hi ? hi : x)); }
        private static string N(long v) { return v.ToString(Inv); }
        private static string F(double v, string f) { return v.ToString(f, Inv); }
        private static string Nm(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }

        // ------------------------------------------------------------ ochrona: rekordy (watek kampanii) i migawka do odczytu z mysli AI
        private sealed class Rec { public string Vid, Fid; public int Until, Day; public long Gold; }
        private struct Prot { public IFaction F; public int Until; }
        private static readonly List<Rec> _recs = new List<Rec>();
        private static readonly Dictionary<Settlement, Prot[]> Empty = new Dictionary<Settlement, Prot[]>();
        private static Dictionary<Settlement, Prot[]> _snap = Empty;   // podmieniany w calosci (Volatile)
        private static bool _dirty;
        private static int _importN = -1, _importBad, _unresolved;

        // ------------------------------------------------------------ liczniki doby
        private static int _dAiT, _dPT, _dRaid, _rReaver, _rResist, _rHungry, _rNeed, _rSmall, _rBetter, _dNoEst, _dGuard, _dPBlock, _dRel, _dExpired, _dStumbles;
        private static long _dAiGold, _dPGold, _dRaidE, _dTribE, _dBal, _dPurse;
        private static double _dRateSum;
        private static int _dCut;                                      // Interlocked (mysl AI)
        private static readonly List<string> _ex = new List<string>();
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _hooked;
        private static string _wired = "nie probowano";
        private static bool _menuAdded;

        internal static void Reset()
        {
            _recs.Clear(); Volatile.Write(ref _snap, Empty); _dirty = false;
            _importN = -1; _importBad = 0; _unresolved = 0;
            lock (_err) _err.Clear();
            ZeroDay();
        }

        private static void ZeroDay()
        {
            _dAiT = _dPT = _dRaid = _rReaver = _rResist = _rHungry = _rNeed = _rSmall = _rBetter = _dNoEst = _dGuard = _dPBlock = _dRel = _dExpired = _dStumbles = 0;
            _dAiGold = _dPGold = _dRaidE = _dTribE = _dBal = _dPurse = 0;
            _dRateSum = 0.0;
            Interlocked.Exchange(ref _dCut, 0);
            _ex.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles); Interlocked.Increment(ref _dStumbles);
            bool first;
            lock (_err) first = _err.Add(where);
            if (first) { try { Log.Error("RaidTribute." + where, e); } catch { } }
        }

        // ------------------------------------------------------------ wpiecie (ArmouryBehavior.RegisterEvents - jedna linia)
        internal static void RegisterEvents(CampaignBehaviorBase b)
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(b, delegate (CampaignGameStarter s) { try { SessionStart(s); } catch (Exception e) { Stumble("SessionStart", e); } });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(b, delegate { try { Daily(); } catch (Exception e) { Stumble("Daily", e); } });
        }

        private static void SessionStart(CampaignGameStarter starter)
        {
            EnsureHooks();
            _menuAdded = false;
            if (starter != null)
            {
                try
                {
                    // index 1: gdy menu gry juz ma opcje - za "Raid the village"; gra dodaje swoje dopiero w OnAfterSessionLaunched (VillageHostileActionCampaignBehavior),
                    // wiec zwykle nasza opcja stoi na gorze listy (GetPresumedGameMenu - menu powstaje z nasza opcja, gra je potem inicjuje). Kolejnosc nie jest regula
                    starter.AddGameMenuOption("village_hostile_action", OptId, "{=!}Demand tribute", MenuCondition, MenuConsequence, false, 1);
                    _menuAdded = true;
                }
                catch (Exception e) { Stumble("AddMenu", e); }
            }
            Rebuild();
            Log.Info(P + "start sesji | klucz Raid Tribute Enabled " + (On ? "tak" : "nie") + " | latki " + _wired + " | opcja menu wsi " + (_menuAdded ? "tak" : "nie")
                     + " | ochron " + _recs.Count + (_importN >= 0 ? " (z zapisu " + _importN + ", bledne " + _importBad + ")" : " (brak klucza arm_raidtribute - nowa kampania albo stary zapis)")
                     + (_unresolved > 0 ? ", nierozpoznane wsie/frakcje " + _unresolved : "")
                     + " | stawka " + F(Low * 100.0, "0") + "-" + F(High * 100.0, "0") + "% kiesy wsi (przewaga 1:1 - " + F(FullAt, "0") + ":1), ochrona " + SpareDays + " dob.");
        }

        private static void EnsureHooks()
        {
            if (_hooked) return;
            _hooked = true;
            var ok = new List<string>(); var miss = new List<string>();
            try
            {
                var h = new Harmony("com.jeff.armoury.raidtribute");
                Patch(h, "EncounterManager.StartSettlementEncounter", AccessTools.DeclaredMethod(typeof(EncounterManager), "StartSettlementEncounter", new[] { typeof(MobileParty), typeof(Settlement) }),
                      nameof(EncounterPrefix), null, Priority.Normal, ok, miss);
                Patch(h, "CampaignEventDispatcher.AiHourlyTick", AccessTools.DeclaredMethod(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) }),
                      null, nameof(ThinkPostfix), Priority.VeryLow, ok, miss);
                Patch(h, "VillageHostileAction.raid_on_condition", AccessTools.DeclaredMethod(typeof(VillageHostileActionCampaignBehavior), "game_menu_village_hostile_action_raid_village_on_condition"),
                      null, nameof(PlunderCondPostfix), Priority.Low, ok, miss);
                Patch(h, "VillageHostileAction.take_food_on_condition", AccessTools.DeclaredMethod(typeof(VillageHostileActionCampaignBehavior), "game_menu_village_hostile_action_take_food_on_condition"),
                      null, nameof(PlunderCondPostfix), Priority.Low, ok, miss);
            }
            catch (Exception e) { miss.Add("Harmony (" + e.GetType().Name + ")"); Stumble("EnsureHooks", e); }
            _wired = ok.Count + "/" + (ok.Count + miss.Count) + (miss.Count > 0 ? " (BRAK: " + string.Join(", ", miss) + ")" : "");
        }

        private static void Patch(Harmony h, string label, System.Reflection.MethodBase m, string pre, string post, int prio, List<string> ok, List<string> miss)
        {
            try
            {
                if (m == null) { miss.Add(label); return; }
                h.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(RaidTribute), pre) { priority = prio } : null,
                           postfix: post != null ? new HarmonyMethod(typeof(RaidTribute), post) { priority = prio } : null);
                ok.Add(label);
            }
            catch (Exception e) { miss.Add(label + " (" + e.GetType().Name + ")"); Stumble("Patch " + label, e); }
        }

        // ------------------------------------------------------------ rachunek (jedna regula dla AI i gracza)
        internal struct Quote { public int Men, Def; public double Q, Rate; public long Purse, T; }

        private static int Healthy(MobileParty p) { try { return p != null && p.MemberRoster != null ? Math.Max(0, p.MemberRoster.TotalHealthyCount) : 0; } catch { return 0; } }

        /// <summary>Ludzie napastnika: partia albo cala armia, ktorej jest wodzem (zdrowi).</summary>
        private static int MenOf(MobileParty mp)
        {
            if (mp == null) return 0;
            var a = mp.Army;
            if (a != null && a.LeaderParty == mp && a.Parties != null)
            {
                int n = 0;
                for (int i = 0; i < a.Parties.Count; i++) n += Healthy(a.Parties[i]);
                return Math.Max(n, Healthy(mp));
            }
            return Healthy(mp);
        }

        /// <summary>Obroncy wsi: milicja + zdrowi ludzie partii stojacych we wsi, ktore sa w wojnie z napastnikiem (stanelyby w obronie).</summary>
        private static int DefOf(Settlement st, IFaction att)
        {
            double d = 0.0;
            try { d = Math.Max(0f, st.Militia); } catch { d = 0.0; }
            try
            {
                var ps = st.Parties;
                if (ps != null && att != null)
                    for (int i = 0; i < ps.Count; i++)
                    {
                        var p = ps[i];
                        if (p == null || p.MapFaction == null || !FactionManager.IsAtWarAgainstFaction(p.MapFaction, att)) continue;
                        d += Healthy(p);
                    }
            }
            catch (Exception e) { Stumble("DefOf", e); }
            return (int)Math.Ceiling(d);
        }

        internal static Quote QuoteOf(MobileParty mp, Settlement st)
        {
            var q = new Quote();
            q.Men = MenOf(mp);
            q.Def = DefOf(st, mp != null ? mp.MapFaction : null);
            q.Q = (double)q.Men / Math.Max(1, q.Def);
            q.Rate = Low + (High - Low) * Clamp((q.Q - 1.0) / (FullAt - 1.0), 0.0, 1.0);
            q.Purse = st.Village != null ? Math.Max(0, st.Village.Gold) : 0;
            q.T = q.Q >= 1.0 ? (long)Math.Floor(q.Rate * q.Purse) : 0;
            return q;
        }

        // ------------------------------------------------------------ ochrona
        /// <summary>Czy wies jest chroniona przed frakcja (odczyt migawki - bezpieczny z kazdego watku, bez alokacji).</summary>
        internal static bool Spared(Settlement st, IFaction f, int today)
        {
            var d = Volatile.Read(ref _snap);
            if (d.Count == 0 || st == null || f == null) return false;
            Prot[] a;
            if (!d.TryGetValue(st, out a) || a == null) return false;
            for (int i = 0; i < a.Length; i++) if (ReferenceEquals(a[i].F, f) && a[i].Until > today) return true;
            return false;
        }

        private static int SpareUntil(Settlement st, IFaction f)
        {
            if (st == null || f == null) return 0;
            for (int i = 0; i < _recs.Count; i++) { var r = _recs[i]; if (r.Vid == st.StringId && r.Fid == f.StringId) return r.Until; }
            return 0;
        }

        private static void Protect(Settlement st, IFaction f, long gold)
        {
            int today = Today;
            Rec rec = null;
            for (int i = 0; i < _recs.Count; i++) { var r = _recs[i]; if (r.Vid == st.StringId && r.Fid == f.StringId) { rec = r; break; } }
            if (rec == null) { rec = new Rec { Vid = st.StringId, Fid = f.StringId }; _recs.Add(rec); }
            rec.Day = today; rec.Until = today + SpareDays; rec.Gold = gold;
            Rebuild();
        }

        /// <summary>Migawka od nowa z rekordow (watek kampanii): wies -&gt; [frakcja, do doby]. Rekordy z nierozpoznana wsia albo frakcja zostaja (licznik).</summary>
        private static void Rebuild()
        {
            try
            {
                _dirty = false;
                if (_recs.Count == 0) { Volatile.Write(ref _snap, Empty); _unresolved = 0; return; }
                var fac = new Dictionary<string, IFaction>(StringComparer.Ordinal);
                var c = Campaign.Current;
                if (c != null && c.Factions != null) foreach (var f in c.Factions) if (f != null && f.StringId != null && !fac.ContainsKey(f.StringId)) fac[f.StringId] = f;
                var tmp = new Dictionary<Settlement, List<Prot>>();
                int bad = 0;
                foreach (var r in _recs)
                {
                    IFaction f; Settlement st = null;
                    try { st = Settlement.Find(r.Vid); } catch { st = null; }
                    if (st == null || !fac.TryGetValue(r.Fid, out f)) { bad++; continue; }
                    List<Prot> l;
                    if (!tmp.TryGetValue(st, out l)) { l = new List<Prot>(2); tmp[st] = l; }
                    l.Add(new Prot { F = f, Until = r.Until });
                }
                var snap = new Dictionary<Settlement, Prot[]>(tmp.Count);
                foreach (var kv in tmp) snap[kv.Key] = kv.Value.ToArray();
                _unresolved = bad;
                Volatile.Write(ref _snap, snap);
            }
            catch (Exception e) { _dirty = true; Stumble("Rebuild", e); }
        }

        // ------------------------------------------------------------ AI: przybycie do wsi z rozkazem rabunku
        /// <summary>Prefiks EncounterManager.StartSettlementEncounter(partia, osada). false = spotkanie nie zachodzi (okup albo wies chroniona - partia staje i mysli).</summary>
        public static bool EncounterPrefix(MobileParty __0, Settlement __1, bool __runOriginal)
        {
            if (!__runOriginal) return false;                       // inny prefiks (np. brama Innych CrashScribe) juz zatrzymal spotkanie
            if (__0 == null || __1 == null || !On) return true;
            MobileParty mp = __0; Settlement st = __1;
            try
            {
                var main = MobileParty.MainParty;
                if (mp == main || !st.IsVillage || st.Village == null || st.Party == null) return true;
                // ten sam warunek co galaz rabunku gry (G EncounterManager.StartSettlementEncounter :221-229)
                if (mp.ShortTermBehavior != AiBehavior.RaidSettlement || mp.ShortTermTargetSettlement != st) return true;
                if (mp.DefaultBehavior == AiBehavior.BesiegeSettlement) return true;
                if (st.Party.MapEvent != null || st.Village.VillageState != Village.VillageStates.Normal || !(st.SettlementHitPoints > 0.001f)) return true;
                if (mp.MapFaction == null || st.MapFaction == null || !FactionManager.IsAtWarAgainstFaction(mp.MapFaction, st.MapFaction)) return true;
                var pm = PartyBase.MainParty;
                if (pm != null && pm.MapEvent != null && pm.MapEvent.MapEventSettlement == st) return true;   // spotkanie gracza w tej wsi - gra
                if (main != null && main.CurrentSettlement == st) return true;                                 // gracz we wsi - gra (jego spotkanie z napastnikiem)
                if (mp.Army != null && mp.Army.LeaderParty != mp) return true;                                 // czlonek armii - decyduje wodz
                var leader = mp.LeaderHero;
                if (leader == null || !mp.IsLordParty || Undead.Party(mp)) return true;
                int today = Today;
                if (Spared(st, mp.MapFaction, today))
                {
                    _dGuard++;                                                                                  // wies juz zaplacila temu krolestwu - nie rabuje, mysli od nowa
                    Hold(mp);
                    return false;
                }
                if (ReaverLaw.IsReaver(mp.MapFaction)) { _rReaver++; _dRaid++; return true; }
                var q = QuoteOf(mp, st);
                if (q.Q < 1.0) { _rResist++; _dRaid++; return true; }
                if (FoodDays(mp) < HungryDays) { _rHungry++; _dRaid++; return true; }
                var need = CitySack.NeedOf(mp);
                if (need.InNeed) { _rNeed++; _dRaid++; return true; }
                long wage = Math.Max(1L, need.ArmyWage);
                if (q.T < wage) { _rSmall++; _dRaid++; return true; }
                double share, days, e = 0.0;
                if (DistrictRaid.StepEstimate(st.Village, q.Men, out share, out days))
                {
                    e = share * (q.Purse + StockValue(st));
                    if (q.T < e - wage * days) { _rBetter++; _dRaid++; _dRaidE += (long)e; return true; }
                }
                else { _dNoEst++; days = 0.0; }
                Pay(mp, leader, st, q, e, days);
                Hold(mp);
                return false;
            }
            catch (Exception ex) { Stumble("EncounterPrefix", ex); return true; }
        }

        private static void Hold(MobileParty mp)
        {
            try { mp.SetMoveModeHold(); if (mp.Ai != null) mp.Ai.RethinkAtNextHourlyTick = true; }
            catch (Exception e) { Stumble("Hold", e); }
        }

        /// <summary>Jedzenie armii w dobach - srednio na druzyne, jak gra (AiMilitaryBehavior.GetFoodScoreForActionType).</summary>
        private static double FoodDays(MobileParty mp)
        {
            try
            {
                double sum = mp.GetNumDaysForFoodToLast(); int n = 1;
                var a = mp.Army;
                if (a != null && a.LeaderParty == mp && mp.AttachedParties != null)
                    foreach (var p in mp.AttachedParties) if (p != null) { sum += p.GetNumDaysForFoodToLast(); n++; }
                return sum / n;
            }
            catch (Exception e) { Stumble("FoodDays", e); return 999.0; }
        }

        private static long StockValue(Settlement st)
        {
            double v = 0.0;
            var r = st.ItemRoster;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Amount > 0 && el.EquipmentElement.Item != null) v += (double)el.Amount * el.EquipmentElement.Item.Value;
            }
            return (long)v;
        }

        /// <summary>Okup AI: przelew kiesa wsi -&gt; wodz (najwyzej tyle, ile w kiesie), ochrona, relacja, komunikat graczowi (jego wies albo jego armia).</summary>
        private static void Pay(MobileParty mp, Hero leader, Settlement st, Quote q, double e, double days)
        {
            var v = st.Village;
            long v0 = v.Gold, g0 = leader.Gold;
            int due = (int)Math.Min(int.MaxValue, Math.Min(q.T, Math.Max(0, v.Gold)));
            if (due <= 0) return;
            GiveGoldAction.ApplyForPartyToCharacter(st.Party, leader, due, true);
            long paid = v0 - v.Gold, got = leader.Gold - g0;
            _dBal += got - paid;
            _dAiT++; _dAiGold += paid; _dRateSum += q.Rate; _dTribE += (long)e; _dPurse += v0;
            Protect(st, mp.MapFaction, paid);
            Relations(leader, st);
            if (_ex.Count < MaxExamples)
                _ex.Add(Nm(leader) + " <- " + Nm(st) + " " + paid + " zl (" + F(q.Rate * 100.0, "0") + "% z " + v0 + ", sila " + q.Men + ":" + q.Def
                        + ", rabunek szac. " + F(e, "0") + " zl w " + F(days, "0.0") + " dob)");
            try
            {
                var main = MobileParty.MainParty;
                if (st.OwnerClan == Clan.PlayerClan)
                    Log.Player(Nm(st) + " paid " + paid + " denars of tribute to " + Nm(leader) + " (" + Nm(mp.MapFaction) + ") to be spared.", true);
                else if (main != null && main.Army != null && main.Army == mp.Army)
                    Log.Player("Your army took " + paid + " denars of tribute from " + Nm(st) + " and spared it.");
            }
            catch (Exception ex) { Stumble("Pay(komunikat)", ex); }
        }

        /// <summary>Relacja jak rabunek gry, ale mniejsza: pan wsi -2, notable -1; wies rodu gracza - bez zmiany (jak G CharacterRelationCampaignBehavior.OnRaidCompleted).</summary>
        private static void Relations(Hero h, Settlement st)
        {
            try
            {
                if (h == null || st.OwnerClan == null || (st.OwnerClan == Clan.PlayerClan && h != Hero.MainHero)) return;
                var lord = st.OwnerClan.Leader;
                if (lord != null && lord != h) { ChangeRelationAction.ApplyRelationChangeBetweenHeroes(h, lord, RelLord); _dRel++; }
                var ns = st.Notables;
                if (ns != null)
                    for (int i = 0; i < ns.Count; i++)
                        if (ns[i] != null && ns[i] != h) { ChangeRelationAction.ApplyRelationChangeBetweenHeroes(h, ns[i], RelNotable); _dRel++; }
            }
            catch (Exception e) { Stumble("Relations", e); }
        }

        // ------------------------------------------------------------ AI: mysl - opcja rabunku wsi chronionej wycieta
        /// <summary>Postfiks VeryLow na CampaignEventDispatcher.AiHourlyTick (po wszystkich odbiorcach i po WW3 ReaverLaw): opcje RaidSettlement na wies chroniona przed
        /// krolestwem partii dostaja wynik -1 (petla gry wybiera tylko wynik &gt; -1 - AiPartyThinkBehavior). Tylko odczyt migawki, bez alokacji.</summary>
        public static void ThinkPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            var d = Volatile.Read(ref _snap);
            if (d.Count == 0 || party == null || partyThinkParams == null || !On) return;
            try
            {
                var f = party.MapFaction;
                var list = partyThinkParams.AIBehaviorScores;
                if (f == null || list == null) return;
                int today = Today;
                for (int i = 0; i < list.Count; i++)
                {
                    var it = list[i];
                    if (it.Item1.AiBehavior != AiBehavior.RaidSettlement || it.Item2 <= -1f) continue;
                    var st = it.Item1.Party as Settlement;
                    if (st == null || !Spared(st, f, today)) continue;
                    var data = it.Item1;
                    partyThinkParams.SetBehaviorScore(in data, -1f);
                    Interlocked.Increment(ref _dCut);
                }
            }
            catch (Exception e) { Stumble("ThinkPostfix", e); }
        }

        // ------------------------------------------------------------ GRACZ: menu wrogiej wsi
        private static bool MenuCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Bribe;
                if (!On) return false;
                var st = Settlement.CurrentSettlement;
                var main = MobileParty.MainParty;
                var pf = Clan.PlayerClan != null ? Clan.PlayerClan.MapFaction : null;
                if (st == null || !st.IsVillage || st.Village == null || main == null || pf == null || st.MapFaction == null) return false;
                if (!FactionManager.IsAtWarAgainstFaction(pf, st.MapFaction)) return false;            // tylko w wojnie (poza wojna - opcje gry z ostrzezeniem)
                if (st.MapFaction.NotAttackableByPlayerUntilTime.IsFuture)
                {
                    args.IsEnabled = false;
                    args.Tooltip = new TextObject("{=!}You cannot move against this realm yet.");
                    return true;
                }
                int today = Today;
                int until = SpareUntil(st, pf);
                if (until > today)
                {
                    args.IsEnabled = false;
                    args.Tooltip = new TextObject("{=!}This village has already paid tribute to your realm. It is spared for " + (until - today) + " more days.");
                    return true;
                }
                var q = QuoteOf(main, st);
                if (q.Q < 1.0)
                {
                    args.IsEnabled = false;
                    args.Tooltip = new TextObject("{=!}The villagers think they can fight you off: " + q.Def + " defenders against your " + q.Men + " men.");
                    return true;
                }
                if (q.T < 1)
                {
                    args.IsEnabled = false;
                    args.Tooltip = new TextObject("{=!}The village has no coin to pay you.");
                    return true;
                }
                args.Tooltip = new TextObject("{=!}The elders will pay " + q.T + " denars (" + F(q.Rate * 100.0, "0") + "% of the village purse of " + q.Purse
                                              + "; your " + q.Men + " men against " + q.Def + " defenders) to be spared. The village is not burned, but its lord and elders will think less of you."
                                              + " Your realm will leave it in peace for " + SpareDays + " days.");
                return true;
            }
            catch (Exception e) { Stumble("MenuCondition", e); return false; }
        }

        private static void MenuConsequence(MenuCallbackArgs args)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                var main = MobileParty.MainParty;
                var me = Hero.MainHero;
                var pf = Clan.PlayerClan != null ? Clan.PlayerClan.MapFaction : null;
                if (st == null || st.Village == null || main == null || me == null || pf == null) return;
                var q = QuoteOf(main, st);
                if (!On || q.Q < 1.0 || q.T < 1 || SpareUntil(st, pf) > Today) return;
                var v = st.Village;
                long v0 = v.Gold, g0 = me.Gold;
                int due = (int)Math.Min(int.MaxValue, Math.Min(q.T, Math.Max(0, v.Gold)));
                GiveGoldAction.ApplyForPartyToCharacter(st.Party, me, due, false);
                long paid = v0 - v.Gold, got = me.Gold - g0;
                _dBal += got - paid;
                _dPT++; _dPGold += paid; _dRateSum += q.Rate; _dPurse += v0;
                Protect(st, pf, paid);
                Relations(me, st);
                if (_ex.Count < MaxExamples)
                    _ex.Add("gracz <- " + Nm(st) + " " + paid + " zl (" + F(q.Rate * 100.0, "0") + "% z " + v0 + ", sila " + q.Men + ":" + q.Def + ")");
                Log.Player("The elders of " + Nm(st) + " paid you " + paid + " denars of tribute. Your realm will spare the village for " + SpareDays + " days.");
                GameMenu.SwitchToMenu("village");                                                        // zwykle menu (jak "Forget it" gry), nie menu oczekiwania
            }
            catch (Exception e) { Stumble("MenuConsequence", e); }
        }

        /// <summary>Postfiks warunkow gry "Raid the village" i "Force peasants to give you goods": wies chroniona przed krolestwem gracza - opcja zablokowana z dymkiem.</summary>
        public static void PlunderCondPostfix(MenuCallbackArgs args, bool __result)
        {
            try
            {
                if (!__result || args == null || !On || !args.IsEnabled) return;
                var st = Settlement.CurrentSettlement;
                var pf = Clan.PlayerClan != null ? Clan.PlayerClan.MapFaction : null;
                if (st == null || pf == null) return;
                int today = Today, until = SpareUntil(st, pf);
                if (until <= today) return;
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=!}This village paid tribute to your realm to be spared. It is under your realm's word for " + (until - today) + " more days.");
                _dPBlock++;
            }
            catch (Exception e) { Stumble("PlunderCondPostfix", e); }
        }

        // ------------------------------------------------------------ doba
        private static void Daily()
        {
            int today = Today;
            int before = _recs.Count;
            _recs.RemoveAll(r => r.Until <= today);
            _dExpired = before - _recs.Count;
            if (_dExpired > 0 || _dirty) Rebuild();
            int cut = Interlocked.Exchange(ref _dCut, 0);
            int t = _dAiT + _dPT;
            var sb = new StringBuilder(512);
            sb.Append(P).Append("dzien ").Append(today)
              .Append(" | okupy: AI ").Append(_dAiT).Append(" (").Append(N(_dAiGold)).Append(" zl), gracz ").Append(_dPT).Append(" (").Append(N(_dPGold)).Append(" zl)")
              .Append(", stawka srednio ").Append(t > 0 ? F(_dRateSum / t * 100.0, "0.0") + "%" : "-")
              .Append(", kiesy tych wsi ").Append(N(_dPurse)).Append(" zl, szac. lup rabunku tych wsi ").Append(N(_dTribE)).Append(" zl")
              .Append(" | rabunki AI zaczete ").Append(_dRaid).Append(" (najezdzcy ").Append(_rReaver).Append(", opor wsi ").Append(_rResist).Append(", glod ").Append(_rHungry)
              .Append(", rod w potrzebie ").Append(_rNeed).Append(", okup ponizej doby zoldu ").Append(_rSmall).Append(", rabunek oplaca sie bardziej ").Append(_rBetter)
              .Append(" - szac. lup ").Append(N(_dRaidE)).Append(" zl; lup naprawde - linia Rabunek osada po osadzie)")
              .Append(" | ochrona: odprawione u wsi ").Append(_dGuard).Append(", opcje rabunku AI wyciete ").Append(cut).Append(", gracz zablokowany ").Append(_dPBlock)
              .Append(", par wies-krolestwo ").Append(_recs.Count).Append(" (wygaslo ").Append(_dExpired).Append(")")
              .Append(" | relacja zmieniona ").Append(_dRel).Append(" razy, bez szacunku rabunku (R1 nieczynne) ").Append(_dNoEst)
              .Append(" | kontrola: zloto wodzow - kiesy wsi ").Append(N(_dBal))
              .Append(" | potkniecia ").Append(_dStumbles).Append(" (razem ").Append(_stumbles).Append(")");
            if (_ex.Count > 0) sb.Append(" | przyklady: ").Append(string.Join("; ", _ex));
            sb.Append('.');
            if (On || t > 0 || _recs.Count > 0) Log.Info(sb.ToString());
            ZeroDay();
        }

        // ------------------------------------------------------------ zapis (arm_raidtribute)
        internal static string Export()
        {
            if (_recs.Count == 0) return "";
            var sb = new StringBuilder(_recs.Count * 40);
            foreach (var r in _recs)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(r.Vid).Append(',').Append(r.Fid).Append(',').Append(r.Until.ToString(Inv)).Append(',').Append(r.Day.ToString(Inv)).Append(',').Append(r.Gold.ToString(Inv));
            }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _recs.Clear(); _importBad = 0;
            if (s == null) { _importN = -1; Volatile.Write(ref _snap, Empty); return; }
            _importN = 0;
            foreach (var part in s.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var f = part.Split(',');
                int until, day; long gold;
                if (f.Length < 5 || f[0].Length == 0 || f[1].Length == 0
                    || !int.TryParse(f[2], NumberStyles.Integer, Inv, out until) || !int.TryParse(f[3], NumberStyles.Integer, Inv, out day)
                    || !long.TryParse(f[4], NumberStyles.Integer, Inv, out gold)) { _importBad++; continue; }
                _recs.Add(new Rec { Vid = f[0], Fid = f[1], Until = until, Day = day, Gold = gold });
                _importN++;
            }
            _dirty = true;                                                 // migawka przy starcie sesji (obiekty swiata juz sa)
            Volatile.Write(ref _snap, Empty);
        }
    }
}
