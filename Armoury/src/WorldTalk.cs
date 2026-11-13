using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 474-P10 (Jeff 13.11 "Wioski, i karczmy rob wszystko ... 5. tez, ale glosy pozniej, na razie zrob tylko teksty"; projekt docs/PROJEKT-WSIE-POBOCZNE-KARCZMY-2026-11-13.md
    /// pkt 3 "Tematy wybiera migawka swiata liczona raz na dobe"): MIGAWKA SWIATA dla rozmow karczmy (TavernLines, plik arm_tavern_talk.tsv). Liczona LENIWIE - przy
    /// pierwszym siegnieciu po kwestie w danej dobie (rozmowa, kufel, gwar we wnetrzu zajazdu), nigdy z ticku mapy; poza karczma nic sie nie liczy.
    /// Zawartosc: oblezenia (osady z SiegeEvent - nazwa, pozycja, oblegajacy), glod (miasta i zamki z Town.FoodStocks &lt;= 0), zgony lordow z ostatnich 14 dni
    /// (Hero.DeadOrDisabledHeroes, Hero.DeathDay - ta sama droga co inne nasze liczniki zgonow), sluby z ostatnich 20 dni (zdarzenie BeforeHeroesMarried - lista w
    /// pamieci, tylko odczyt; po wczytaniu zapisu pusta az do nastepnego slubu), Inni (klan ROTclan_126 albo kultura "whitewalker" ma partie wojenne lub osady -
    /// to samo kryterium co CrashScribe NightKingGate.IsOthersParty). Tylko odczyt stanu gry - nic nie zapisuje, nie zmienia, nie placi.
    /// </summary>
    internal static class WorldTalk
    {
        internal const string P = "Rozmowy karczmy (474-P10): ";
        private const float HungerReach = 300f;
        private const int DeathDays = 14, WeddingDays = 20;

        private sealed class Place { public string Name, By; public Vec2 Pos; }
        private sealed class Wed { public string Groom, Bride; public int Day; }

        private static int _day = int.MinValue;
        private static object _for;
        private static readonly List<Place> _sieges = new List<Place>();
        private static readonly List<Place> _hungry = new List<Place>();
        private static string _lord, _house;
        private static int _deaths;
        private static bool _others;
        private static readonly List<Wed> _weds = new List<Wed>();
        private static object _wedsFor;
        private static int _st;
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);

        private static void Stumble(string where, Exception e)
        {
            _st++;
            try { if (_err.Add(where)) Log.Error("WorldTalk." + where + " (potkniecie " + _st + ")", e); } catch { }
        }

        /// <summary>BeforeHeroesMarried (rejestracja w ArmouryBehavior obok AiDowry) - tylko zapis imion i doby; lista do 20 wpisow.</summary>
        internal static void OnMarried(Hero a, Hero b, bool show)
        {
            try
            {
                if (!TavernLines.On || a == null || b == null) return;
                if (!ReferenceEquals(_wedsFor, Campaign.Current)) { _weds.Clear(); _wedsFor = Campaign.Current; }
                var groom = a.IsFemale && !b.IsFemale ? b : a;
                var bride = ReferenceEquals(groom, a) ? b : a;
                _weds.Add(new Wed { Groom = Name(groom), Bride = Name(bride), Day = (int)CampaignTime.Now.ToDays });
                if (_weds.Count > 20) _weds.RemoveAt(0);
            }
            catch (Exception e) { Stumble("OnMarried", e); }
        }

        private static string Name(Hero h)
        {
            try { return h.Name != null ? h.Name.ToString() : null; } catch { return null; }
        }

        private static void Ensure()
        {
            int today = (int)CampaignTime.Now.ToDays;
            if (today == _day && ReferenceEquals(_for, Campaign.Current)) return;
            _day = today; _for = Campaign.Current;
            if (!ReferenceEquals(_wedsFor, Campaign.Current)) { _weds.Clear(); _wedsFor = Campaign.Current; }
            long t0 = Stopwatch.GetTimestamp();
            _sieges.Clear(); _hungry.Clear(); _lord = _house = null; _deaths = 0; _others = false;
            try
            {
                foreach (var s in Settlement.All)
                {
                    if (s == null || (!s.IsTown && !s.IsCastle)) continue;
                    try
                    {
                        if (s.SiegeEvent != null)
                        {
                            string by = null;
                            try { var bp = s.SiegeEvent.BesiegerCamp != null ? s.SiegeEvent.BesiegerCamp.LeaderParty : null; by = bp != null && bp.MapFaction != null ? bp.MapFaction.Name.ToString() : null; } catch { }
                            _sieges.Add(new Place { Name = s.Name.ToString(), Pos = s.GetPosition2D, By = by });
                        }
                        if (s.Town != null && s.Town.FoodStocks <= 0f) _hungry.Add(new Place { Name = s.Name.ToString(), Pos = s.GetPosition2D });
                    }
                    catch (Exception e) { Stumble("Settlement", e); }
                }
            }
            catch (Exception e) { Stumble("Settlements", e); }
            try
            {
                double best = double.MinValue;
                foreach (var h in Hero.DeadOrDisabledHeroes)
                {
                    if (h == null || !h.IsDead || !h.IsLord || h.Clan == null) continue;
                    double dd;
                    try { dd = h.DeathDay.ToDays; } catch { continue; }
                    if (dd < today - DeathDays || IsOthers(h.Clan)) continue;
                    _deaths++;
                    if (dd > best) { best = dd; _lord = Name(h); try { _house = h.Clan.Name.ToString(); } catch { _house = null; } }
                }
            }
            catch (Exception e) { Stumble("Deaths", e); }
            try
            {
                foreach (var c in Clan.All)
                {
                    if (c == null || c.IsEliminated || !IsOthers(c)) continue;
                    if ((c.WarPartyComponents != null && c.WarPartyComponents.Count > 0) || (c.Settlements != null && c.Settlements.Count > 0)) { _others = true; break; }
                }
            }
            catch (Exception e) { Stumble("Others", e); }
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            int weds = 0;
            foreach (var w in _weds) if (w.Day >= today - WeddingDays) weds++;
            try
            {
                Log.Info(P + "migawka swiata dzien " + today + " - oblezenia " + _sieges.Count + ", glod " + _hungry.Count + " (miasta/zamki bez zapasu), zgony lordow z " + DeathDays + " dni " + _deaths
                         + (_lord != null ? " (ostatni " + _lord + ")" : "") + ", sluby z " + WeddingDays + " dni " + weds + ", Inni " + (_others ? "TAK" : "nie")
                         + ", czas " + ms.ToString("0.0", CultureInfo.InvariantCulture) + " ms | potkniecia " + _st + ".");
            }
            catch { }
        }

        private static bool IsOthers(Clan c)
        {
            try { return c != null && (c.StringId == "ROTclan_126" || (c.Culture != null && c.Culture.StringId == "whitewalker")); } catch { return false; }
        }

        private static Place Nearest(List<Place> l, Vec2 at, float reach)
        {
            Place best = null; float bd = float.MaxValue;
            foreach (var p in l)
            {
                float d = p.Pos.Distance(at);
                if (d < bd && d <= reach) { bd = d; best = p; }
            }
            return best;
        }

        private static Wed LastWed()
        {
            Wed best = null;
            int today = (int)CampaignTime.Now.ToDays;
            foreach (var w in _weds) if (w.Day >= today - WeddingDays && w.Groom != null && w.Bride != null && (best == null || w.Day >= best.Day)) best = w;
            return best;
        }

        /// <summary>Temat biezacy prawdziwy w miejscu at (siege - jakiekolwiek oblezenie; hunger - glod w promieniu 300; death / wedding - ostatnie dni; others - Inni).</summary>
        internal static bool Topic(string topic, Vec2 at)
        {
            try
            {
                Ensure();
                switch (topic)
                {
                    case "siege": return _sieges.Count > 0;
                    case "hunger": return Nearest(_hungry, at, HungerReach) != null;
                    case "death": return _lord != null;
                    case "wedding": return LastWed() != null;
                    case "others": return _others;
                    default: return false;
                }
            }
            catch (Exception e) { Stumble("Topic", e); return false; }
        }

        /// <summary>Wartosc wstawki ({SIEGE}, {BESIEGER}, {HUNGRY}, {LORD}, {HOUSE}, {GROOM}, {BRIDE}); null - brak (kwestia odpada).</summary>
        internal static string Value(string key, Vec2 at)
        {
            try
            {
                Ensure();
                switch (key)
                {
                    case "SIEGE": { var p = Nearest(_sieges, at, float.MaxValue); return p != null ? p.Name : null; }
                    case "BESIEGER": { var p = Nearest(_sieges, at, float.MaxValue); return p != null ? p.By : null; }
                    case "HUNGRY": { var p = Nearest(_hungry, at, HungerReach); return p != null ? p.Name : null; }
                    case "LORD": return _lord;
                    case "HOUSE": return _house;
                    case "GROOM": { var w = LastWed(); return w != null ? w.Groom : null; }
                    case "BRIDE": { var w = LastWed(); return w != null ? w.Bride : null; }
                    default: return null;
                }
            }
            catch (Exception e) { Stumble("Value", e); return null; }
        }
    }
}
