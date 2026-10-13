using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// E6/K3 (219, PLAN 6.14, R12; projekt etapu 6 v2 rozdz. 3 "E6/K3", krytyka K1-05/K1-06/KB K3-klucze, KB K3-przerzut, KB K3-pompa) - JAZDA WEDLUG
    /// SZABLONU PARTII (175), RYCERZ Z KONIEM RODU. Klucze CavalryShareByTemplate, KnightHorseFromHouse, KnightHorseMarketShare; obie czesci tylko z czynnym
    /// K2 (AiSellsSpareHorses - kupno do zapasu wedlug kategorii) i ze Stajnia AI (CavalryNeedsMounts, AiBuysMounts).
    /// (1) Cel partii lorda AI (nie gracz, nie rod gracza) = udzial ludzi jednostek konnych (IsMounted - J + KL) w stosach t3+ szablonu rodu wodza
    ///     (Clan.DefaultPartyTemplate; rod bez wlasnego szablonu - szablon kultury, tak zwraca gra), wagi MaxValue - ta sama liczba co ROT
    ///     ROTTroopRecruiter.GetTemplateComposition i kolumna "Cel" PROJEKT-175 (Polnoc 24%, Dothrakowie 100%, Zelazne Wyspy 0, Harlaw 21%). Liczony leniwie raz
    ///     na sesje na szablon - CrashScribe zmienia szablony 175 przy wczytaniu (AfterRegisterSubModuleObjects), przed kampania. Udzial partii liczony tak samo
    ///     (ludzie t3+, IsMounted). Przy rozwidleniu awansu (lista celow ma cel konny i pieszy, zrodlo piesze) Stables.FilterTargets zostawia tylko te strone,
    ///     ktora przybliza partie do celu: konny, gdy m3 + n/2 < cel x (t3 + n) (n - ludzie tego awansu), inaczej pieszy. Konny bez konia -> lista pusta ->
    ///     czeka (XP zostaje), NeedByCategory liczy go do zapasu K2 i Stajnia kupuje. NeedByCategory stosuje te sama regule (czlowiek, ktory pojdzie pieszo,
    ///     nie czeka na konia) - kupno, zapas K2 i pomiar "czeka" 175 widza jedna decyzje.
    /// (2) Kupno wedlug kategorii - K2 (Stables.Reserve, kwota kategorii ponizej zapasu); K3 nie dodaje drugiej reguly.
    /// (3) Rycerz = cel konny z linii szlacheckiej kultury celu (drzewo Culture.EliteBasicTroop, jak HouseLevies.IsElite i ROT IsEliteTroop) albo z linii
    ///     wlasnego szablonu rodu (drzewo Clan.DefaultPartyTemplate, gdy rod ma szablon inny niz kultury - rody ROT: stark_*, bolton_*...). Kon rycerza kupuje
    ///     rod (Stables.KnightBuy): najpierw stado wsi rodu przypietej do osady postoju (BoundVillages osady rodu; wies postoju - ona sama, gdy rodu), zloto do
    ///     wsi bez narzutu; potem targ miasta postoju do KnightHorseMarketShare polki; z budzetu sprzetu rodu (ClanBudget.GearCap/GearSpent, 166). Na zamku rodu
    ///     tylko stado (zamek nie ma targu w Stajni). Dalekie lenna bez przerzutu (R15). Kon -> tabor -> PayInHorses -> zbrojownia jezdzca, jak kazdy kon Stajni.
    ///     Przerwa AiMountBuyCooldownDays zdjeta dla partii z czekajacymi awansami tylko przy czynnej sprzedazy K2(b) (SpareHorses.SellOn - gra nie sprzedaje
    ///     koni lordow, nie ma pompy); inaczej przerwa zostaje (licznik "K3 bez K2").
    /// (4) Stajnia kupuje tylko wierzchowce rodziny konia (Stables.IsPlainMount) - MountLaw.Allowed: kon zawsze; wielblady, slonie, mamuty poza Stajnia.
    /// Linia doby "Rycerze i jazda (219)" (spis partii w petli MountedWage.Daily - bez drugiej petli po partiach).
    /// </summary>
    internal static class CavalryTemplate
    {
        private static bool Base { get { var s = Settings.Current; return s != null && s.CavalryNeedsMounts && s.AiBuysMounts && SpareHorses.On; } }
        internal static bool ShareOn { get { var s = Settings.Current; return s != null && s.CavalryShareByTemplate && Base; } }
        internal static bool KnightOn { get { var s = Settings.Current; return s != null && s.KnightHorseFromHouse && Base; } }
        internal static bool AnyOn { get { return ShareOn || KnightOn; } }
        /// <summary>Spis doby i linia: przy czynnym K3 albo pomiarze etapu 6.</summary>
        internal static bool LineOn { get { var s = Settings.Current; return s != null && (s.Stage6MeasureLog || s.CavalryShareByTemplate || s.KnightHorseFromHouse); } }

        // cele szablonow i drzewa (raz na sesje - szablony i drzewa awansow sa stale po wczytaniu)
        private static readonly Dictionary<PartyTemplateObject, float> _target = new Dictionary<PartyTemplateObject, float>();
        private static readonly Dictionary<PartyTemplateObject, HashSet<CharacterObject>> _houseTree = new Dictionary<PartyTemplateObject, HashSet<CharacterObject>>();
        private static readonly Dictionary<CharacterObject, bool> _elite = new Dictionary<CharacterObject, bool>();

        // liczniki doby
        private static long _dToHorse, _dToFoot, _dLifted, _dKept, _dKnVisits, _dKnNoGold, _dGold, _dGoldGear;
        // F5 (K3-3): zwykle zakupy koni z przydzialu sprzetu rodu (Stables.OnSettlementEntered) i rezerwa kiesy przy koniach rycerzy
        private static long _dPlStops, _dPlByShare, _dPlNoShare, _dPlNoGold, _dPlGold, _dKnReserve;
        private static readonly long[] _dHerd = new long[3], _dMarket = new long[3];
        private sealed class Row { public long Parties, NoTarget, Men, Mounted, Men3, Mounted3; public double Tgt3; }
        private static readonly Dictionary<string, Row> _rows = new Dictionary<string, Row>();
        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        internal static void Reset()
        {
            _target.Clear(); _houseTree.Clear(); _elite.Clear();
            DayReset();
            _stumbles = 0; _errSites.Clear();
        }

        private static void DayReset()
        {
            _dToHorse = _dToFoot = _dLifted = _dKept = _dKnVisits = _dKnNoGold = _dGold = _dGoldGear = 0;
            _dPlStops = _dPlByShare = _dPlNoShare = _dPlNoGold = _dPlGold = _dKnReserve = 0;
            Array.Clear(_dHerd, 0, 3); Array.Clear(_dMarket, 0, 3);
            _rows.Clear();
            _stumblesDay = 0;
        }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_errSites.Add(where)) Log.Error("CavalryTemplate." + where, e);
        }

        // ------------------------------------------------------------ (1) cel szablonu i udzial partii

        internal static Clan ClanOf(MobileParty mp)
        {
            if (mp == null) return null;
            var h = mp.LeaderHero;
            return h != null && h.Clan != null ? h.Clan : mp.ActualClan;
        }

        /// <summary>Partia sterowana przez K3: partia lorda AI z wodzem-bohaterem, nie gracz i nie rod gracza (ten sam zbior co Stajnia AI, bez rodu gracza).</summary>
        internal static bool Steered(MobileParty mp)
        {
            if (mp == null || mp.IsMainParty || !mp.IsLordParty || mp.LeaderHero == null) return false;
            var c = ClanOf(mp);
            return c != null && c != Clan.PlayerClan;
        }

        /// <summary>Udzial konnych (IsMounted) w stosach t3+ szablonu rodu (wagi MaxValue, jak ROT GetTemplateComposition); -1 - szablon bez stosow t3+.</summary>
        internal static float TargetOf(Clan clan)
        {
            PartyTemplateObject t = null;
            try { t = clan != null ? clan.DefaultPartyTemplate : null; } catch { t = null; }
            if (t == null) return -1f;
            float v;
            if (_target.TryGetValue(t, out v)) return v;
            long all = 0, m = 0;
            try
            {
                if (t.Stacks != null)
                    foreach (var st in t.Stacks)
                    {
                        var ch = st.Character;
                        if (ch == null || ch.IsHero || ch.Tier < 3) continue;
                        int n = Math.Max(0, st.MaxValue);
                        all += n;
                        if (ch.IsMounted) m += n;
                    }
            }
            catch (Exception e) { Stumble("TargetOf", e); }
            v = all > 0 ? (float)m / all : -1f;
            _target[t] = v;
            return v;
        }

        /// <summary>Ludzie t3+ partii (bez bohaterow, z rannymi) i w tym konni (IsMounted) - ta sama miara co cel szablonu.</summary>
        internal static void Share(MobileParty mp, out int m3, out int t3)
        {
            m3 = 0; t3 = 0;
            var r = mp != null ? mp.MemberRoster : null;
            if (r == null) return;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || ch.IsHero || el.Number <= 0 || ch.Tier < 3) continue;
                t3 += el.Number;
                if (ch.IsMounted) m3 += el.Number;
            }
        }

        /// <summary>Czy awans n ludzi na jezdnego przybliza udzial konnych t3+ partii do celu c (inaczej pieszy): m3 + n/2 &lt; c x (t3 + n).</summary>
        internal static bool WantRider(int m3, int t3, int n, float c)
        {
            return m3 + 0.5 * Math.Max(0, n) < c * (t3 + Math.Max(0, n));
        }

        /// <summary>
        /// Stables.FilterTargets (postfiks GetPossibleUpgradeTargets), PRZED filtrem koni: przy rozwidleniu pieszy/konny zostaje tylko strona, ktora przybliza
        /// partie do celu szablonu. Zrodlo konne - bez zmian (awans w siodle jest darmowy, FilterTargets go przepuszcza).
        /// </summary>
        internal static void Steer(PartyBase party, IList list)
        {
            if (list == null || list.Count < 2 || party == null || !ShareOn) return;
            try
            {
                var mp = party.MobileParty;
                if (!Steered(mp)) return;
                bool anyM = false, anyF = false;
                int n = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    var boxed = list[i];
                    if (boxed == null) continue;
                    var tr = Traverse.Create(boxed);
                    var tg = tr.Field("UpgradeTarget").GetValue<CharacterObject>();
                    if (tg == null) continue;
                    var src = tr.Field("Target").GetValue<CharacterObject>();
                    if (src == null || src.IsHero || src.IsMounted) return;
                    if (tg.IsMounted) anyM = true; else anyF = true;
                    n = Math.Max(n, tr.Field("PossibleUpgradeCount").GetValue<int>());
                }
                if (!anyM || !anyF || n <= 0) return;
                float c = TargetOf(ClanOf(mp));
                if (c < 0f) return;
                int m3, t3;
                Share(mp, out m3, out t3);
                bool rider = WantRider(m3, t3, n, c);
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var boxed = list[i];
                    if (boxed == null) continue;
                    var tg = Traverse.Create(boxed).Field("UpgradeTarget").GetValue<CharacterObject>();
                    if (tg != null && tg.IsMounted != rider) list.RemoveAt(i);
                }
                if (rider) _dToHorse += n; else _dToFoot += n;
            }
            catch (Exception e) { Stumble("Steer", e); }
        }

        // ------------------------------------------------------------ (3) rycerz

        /// <summary>Rycerz: cel konny z linii szlacheckiej swojej kultury albo z linii wlasnego szablonu rodu (rod z szablonem innym niz kultury).</summary>
        internal static bool IsKnight(CharacterObject tg, Clan clan)
        {
            if (tg == null || tg.IsHero || !tg.IsMounted) return false;
            try { return InElite(tg) || InHouse(tg, clan); }
            catch (Exception e) { Stumble("IsKnight", e); return false; }
        }

        private static bool InElite(CharacterObject tg)
        {
            bool v;
            if (_elite.TryGetValue(tg, out v)) return v;
            v = false;
            var cult = tg.Culture;
            var root = cult != null ? cult.EliteBasicTroop : null;
            if (root != null)
            {
                var stack = new Stack<CharacterObject>(); var seen = new HashSet<CharacterObject>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    var ch = stack.Pop();
                    if (ch == null || !seen.Add(ch)) continue;
                    if (ch == tg) { v = true; break; }
                    if (ch.UpgradeTargets != null) foreach (var u in ch.UpgradeTargets) stack.Push(u);
                }
            }
            _elite[tg] = v;
            return v;
        }

        private static bool InHouse(CharacterObject tg, Clan clan)
        {
            if (clan == null) return false;
            var t = clan.DefaultPartyTemplate;
            var ct = clan.Culture != null ? clan.Culture.DefaultPartyTemplate : null;
            if (t == null || t == ct) return false;   // rod bez wlasnego szablonu - linia kultury to nie linia rodu
            HashSet<CharacterObject> set;
            if (!_houseTree.TryGetValue(t, out set))
            {
                set = new HashSet<CharacterObject>();
                var stack = new Stack<CharacterObject>();
                if (t.Stacks != null) foreach (var st in t.Stacks) if (st.Character != null) stack.Push(st.Character);
                while (stack.Count > 0)
                {
                    var ch = stack.Pop();
                    if (ch == null || !set.Add(ch)) continue;
                    if (ch.UpgradeTargets != null) foreach (var u in ch.UpgradeTargets) stack.Push(u);
                }
                _houseTree[t] = set;
            }
            return set.Contains(tg);
        }

        // ------------------------------------------------------------ liczniki (Stables)

        /// <summary>Przerwa AiMountBuyCooldownDays przy czekajacych awansach: zdjeta (K2(b) czynne) albo zostaje (K3 bez K2(b)).</summary>
        internal static void NoteCooldown(bool lifted) { if (lifted) _dLifted++; else _dKept++; }

        internal static void NoteKnightVisit(bool noGold) { _dKnVisits++; if (noGold) _dKnNoGold++; }

        internal static void NoteKnightBuy(int k, int n, bool herd)
        {
            if (k < 0 || k > 2 || n <= 0) return;
            if (herd) _dHerd[k] += n; else _dMarket[k] += n;
        }

        internal static void NoteKnightGold(int gold, bool gear) { if (gold <= 0) return; _dGold += gold; if (gear) _dGoldGear += gold; }

        /// <summary>F5 (K3-3): postoj ze zwyklym zakupem koni liczonym z przydzialu sprzetu rodu. byShare - budzet postoju wyznaczyl przydzial rodu (mniejszy niz
        /// udzial kiesy pana ponad rezerwe); fail: 0 - budzet byl, 1 - brak przydzialu (kiesa by starczyla), 2 - brak zlota ponad rezerwe. Tylko liczniki.</summary>
        internal static void NotePlainStop(bool byShare, int fail)
        {
            _dPlStops++;
            if (fail == 1) _dPlNoShare++;
            else if (fail == 2) _dPlNoGold++;
            else if (byShare) _dPlByShare++;
        }

        /// <summary>F5 (K3-3): zloto zwyklych zakupow koni zdjete z przydzialu sprzetu rodu (targ + wsie-hodowcy); konie rycerzy - NoteKnightGold.</summary>
        internal static void NotePlainGold(int gold) { if (gold > 0) _dPlGold += gold; }

        /// <summary>F5 (K3-3): postoj z rycerzami, w ktorym kon rycerza nie zostal kupiony tylko przez rezerwe kiesy pana (AiGearGoldReserve).</summary>
        internal static void NoteKnightReserve() { _dKnReserve++; }

        // ------------------------------------------------------------ spis doby i linia

        /// <summary>Z petli MountedWage.Daily (bez drugiej petli po partiach): partia lorda AI - ludzie, konni, t3+ i cel szablonu, wedlug krolestwa.</summary>
        internal static void CountParty(MobileParty mp)
        {
            try
            {
                if (!Steered(mp)) return;
                string key = HorseCensus.KeyOf(mp.MapFaction);
                Row row;
                if (!_rows.TryGetValue(key, out row)) { row = new Row(); _rows[key] = row; }
                var r = mp.MemberRoster;
                if (r == null) return;
                long men = 0, mounted = 0;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var ch = el.Character;
                    if (ch == null || ch.IsHero || el.Number <= 0) continue;
                    men += el.Number;
                    if (ch.IsMounted) mounted += el.Number;
                }
                int m3, t3;
                Share(mp, out m3, out t3);
                row.Parties++; row.Men += men; row.Mounted += mounted; row.Men3 += t3; row.Mounted3 += m3;
                float c = TargetOf(ClanOf(mp));
                if (c < 0f) row.NoTarget++;
                else row.Tgt3 += c * t3;
            }
            catch (Exception e) { Stumble("CountParty", e); }
        }

        private static string Pct(double a, double b) { return b > 0 ? (100.0 * a / b).ToString("0.0", CultureInfo.InvariantCulture) : "0.0"; }

        private static string State(bool key, bool on)
        {
            return !key ? "WYLACZONE" : on ? "CZYNNE" : "NIECZYNNE (wymaga AiSellsSpareHorses, CavalryNeedsMounts i AiBuysMounts)";
        }

        /// <summary>Linia "Rycerze i jazda (219)" - po MountedWage.Daily (spis), PRZED HorseCensus.Daily (czyta jego stan "czeka" przed zerowaniem).</summary>
        internal static void Daily()
        {
            if (!LineOn) { DayReset(); return; }
            try
            {
                var s = Settings.Current;
                var inv = CultureInfo.InvariantCulture;
                var t = new Row();
                var keys = new List<string>(_rows.Keys);
                keys.Sort((a, b) => _rows[b].Men.CompareTo(_rows[a].Men));
                var per = new StringBuilder();
                foreach (var k in keys)
                {
                    var r = _rows[k];
                    t.Parties += r.Parties; t.NoTarget += r.NoTarget; t.Men += r.Men; t.Mounted += r.Mounted; t.Men3 += r.Men3; t.Mounted3 += r.Mounted3; t.Tgt3 += r.Tgt3;
                    if (r.Men <= 0) continue;
                    if (per.Length > 0) per.Append(", ");
                    per.Append(k).Append(' ').Append(Pct(r.Mounted, r.Men)).Append("%/t3 ").Append(Pct(r.Mounted3, r.Men3))
                       .Append("% (cel ").Append(Pct(r.Tgt3, r.Men3)).Append("%, ").Append(r.Men).Append(" ludzi)");
                }
                long wait, other;
                string w = HorseCensus.WaitToday(out wait, out other) ? wait + " (w tym mimo koni innej kategorii " + other + ")" : "- (pomiar Army175Measure wylaczony)";
                var sb = new StringBuilder();
                sb.Append("Rycerze i jazda (219): dzien ").Append((int)CampaignTime.Now.ToDays)
                  .Append(" | jazda wedlug szablonu ").Append(State(s.CavalryShareByTemplate, ShareOn))
                  .Append(", kon rycerza od rodu ").Append(State(s.KnightHorseFromHouse, KnightOn))
                  .Append(" (sprzedaz koni lordow przez gre pominieta K2(b): ").Append(SpareHorses.SellOn ? "TAK" : "NIE").Append(')')
                  .Append(" | jazda partii ").Append(Pct(t.Mounted, t.Men)).Append("% (konnych ").Append(t.Mounted).Append(" z ").Append(t.Men).Append(" ludzi w ")
                  .Append(t.Parties).Append(" partiach rodow AI; t3+ ").Append(Pct(t.Mounted3, t.Men3)).Append("% z ").Append(t.Men3)
                  .Append(" wobec celu szablonow ").Append(Pct(t.Tgt3, t.Men3)).Append("%; partie bez celu ").Append(t.NoTarget).Append(')')
                  .Append(", wedlug krain [").Append(per).Append(']')
                  .Append(" | awanse przy rozwidleniu: do jazdy ").Append(_dToHorse).Append(", do piechoty ").Append(_dToFoot).Append(" ludzi")
                  .Append(" | konie kupione wedlug kategorii (zwykly/bojowy/szlachetny): ze stad wsi postoju ").Append(_dHerd[0]).Append('/').Append(_dHerd[1]).Append('/').Append(_dHerd[2])
                  .Append(", z targu ").Append(_dMarket[0]).Append('/').Append(_dMarket[1]).Append('/').Append(_dMarket[2])
                  .Append(", za ").Append(_dGold).Append(" zl (budzet sprzetu ").Append(_dGoldGear).Append("); postoje z rycerzami ").Append(_dKnVisits)
                  .Append(" (bez zlota ").Append(_dKnNoGold).Append(')')
                  .Append(" | zwykle konie z przydzialu sprzetu rodu (F5): ").Append(s.AiMountGearPurseShare <= 0f ? "WYLACZONE" : !ClanBudget.On ? "NIECZYNNE (wymaga budzetu rodow 166 - ClanBudgetEnabled)" : "CZYNNE udzial kiesy ponad rezerwe " + s.AiMountGearPurseShare.ToString("0.00", inv))
                  .Append(", postoje ").Append(_dPlStops).Append(" (budzet wyznaczyl przydzial ").Append(_dPlByShare).Append(", kiesa ").Append(_dPlStops - _dPlByShare - _dPlNoShare - _dPlNoGold)
                  .Append("; bez budzetu: brak przydzialu ").Append(_dPlNoShare).Append(", brak zlota ponad rezerwe ").Append(_dPlNoGold)
                  .Append("), zdjete z przydzialu ").Append(_dPlGold).Append(" zl; rycerze zatrzymani rezerwa kiesy ").Append(_dKnReserve)
                  .Append(" | czeka ").Append(w)
                  .Append(" | przerwa: zdjeta ").Append(_dLifted).Append(" / zostaje (K3 bez K2) ").Append(_dKept)
                  .Append(" | cele szablonow ").Append(_target.Count)
                  .Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (razem ").Append(_stumbles).Append(')');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            DayReset();
        }
    }
}
