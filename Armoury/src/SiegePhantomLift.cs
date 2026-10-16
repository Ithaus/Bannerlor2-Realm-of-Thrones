using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace Armoury
{
    /// <summary>
    /// OB6 - OBOZ BEZ SILY ZWIJA OBLEZENIE (noc-drobne 16.10, klucze SiegePhantomLift / SiegePhantomLiftDays / SiegePhantomMinMen; otwarta pozycja
    /// "oboz-widmo pod castle_B8" z listy NIEROZWIAZANE).
    ///
    /// DOWOD [L test 6-letni, D:/Backup-Bannerlord/logs/2026-10-15-6lat]: najemnik Luco of the Sons of the Harpy (Free Folk) oblegal Eastwatch by the Sea
    /// (castle_B8) obozem 1 druzyny o sile 4, potem 0 (Armoury-2026-10-14_15-55-37 [17:53:10] d109254 "druzyn 1, sila 4"; Armoury-2026-10-14_20-17-37
    /// [20:19:35] d109556 "druzyn 1, sila 0 | wymagana odsiecz 0 ...; oblezenie od 301.9 dob") az do [22:08:38] d110050 "sila na starcie 0, najwiecej 0 |
    /// dob od wykrycia 494 ... wynik: ZDOBYTE" - ok. 796 dob oblezenia obozem, w ktorym nikt nie byl zdolny do walki. OB5 wymagal odsieczy 1.2 x 0 = 0,
    /// wiec nikogo nie wolal; gra sama takiego obozu nie zwija (BesiegerCamp konczy oblezenie tylko, gdy wyjdzie ostatnia druzyna - RemoveSiegePartyInternal
    /// :242-245). W kampanii Jeffa (WGRANIE 61, logi 06-05-03 ... 10-56-30) 0 takich linii. Dlaczego druzyna o sile 0 nie zeszla - nieustalone [P];
    /// regula nie zalezy od tej przyczyny.
    ///
    /// REGULA (raz na dobe, po SiegeWalls2.Daily): oboz, w ktorym wszystkie druzyny razem maja mniej niz SiegePhantomMinMen (5) zdrowych ludzi
    /// (MemberRoster.TotalHealthyCount - roster, nie EstimatedStrength: zywy oboz z sila 0 z modelu nie jest zwijany), przez SiegePhantomLiftDays (3) doby
    /// z rzedu, zwija sie droga gry przy pokoju (Helpers/FactionHelper.cs:337-338): kazda druzyna obozu BesiegerCamp = null (wodz armii zabiera
    /// przylaczonych), a kazda druzyna poza armia i kazdy wodz armii SetMoveModeHold - wybiera nowy cel. Ostatnia druzyna wychodzi, gra konczy
    /// oblezenie (FinalizeSiegeEvent -> OnSiegeEventEnded), wiec nasluchy konca oblezenia dzialaja jak zwykle (SiegeRansom, SiegeWalls OB4, BesiegedRelief
    /// "utrzymane", EnemyCaravans). Doba w bitwie nie liczy sie i nie zeruje serii; doba z co najmniej MinMen zdrowymi zeruje serie.
    /// POMIJANE (seria zerowana): oboz z druzyna gracza albo z jego armia i oblezenie, w ktorego murach stoi gracz (gra prowadzi go menu oblezenia);
    /// oblezenia fabuly ROT - czynne wydarzenie ROT.Events BlackwaterSiegeEvent / WallSiegeEvent / BastardsSiegeEvent / HarrenhalSiegeEvent (statyczne
    /// Instance, IsActive, SiegeLeaderParty w tym obozie; refleksja raz na sesje; CrashScribe, gdy rozstawia te oblezenia sam, wpisuje swojego dowodce
    /// w SiegeLeaderParty ROT); Inni (Undead.Party druzyny obozu, osada rodu Innych / kultury whitewalker - ROTOthersCampaignBehavior ma wlasne oblezenia).
    /// Sama mysl AI zamrozona (Ai.DoNotMakeNewDecisions) NIE wyklucza: ROT zostawia zamrozonych dowodcow po koncu wydarzen Muru i Harrenhal (CrashScribe
    /// B6b/B6c - 'zamrozony na stale (flaga w sejwie)'), a zamrozony wodz nie zejdzie z oblezenia sam - wiersz linii pokazuje stan mysli wodza.
    /// Seria tylko w pamieci (bez zapisu): po wczytaniu liczy od zera. Zamknieta ekonomia: nikt nic nie placi i nic nie dostaje - druzyny odchodza z tym,
    /// co maja (zloto, ludzie, jency, jedzenie). Wyjatek na jednym obozie - potkniecie (licznik), reszta dalej; bez globalnego wylacznika.
    /// Linie: "Oboz bez sily (OB6): start sesji" (OnSessionLaunched) i "Oboz bez sily (OB6): dzien N" (tylko, gdy jest oboz ponizej progu, zwiniecie
    /// albo potkniecie). Klucz wylaczony = stan 39f8fe72 (oboz stoi, az cos innego skonczy oblezenie).
    /// </summary>
    internal static class SiegePhantomLift
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static Settings S { get { return Settings.Current; } }
        internal static bool On { get { var s = S; return s != null && s.SiegePhantomLift; } }
        private static int LiftDays { get { var s = S; return s == null ? 3 : Math.Max(1, Math.Min(30, s.SiegePhantomLiftDays)); } }
        private static int MinMen { get { var s = S; return s == null ? 5 : Math.Max(1, Math.Min(50, s.SiegePhantomMinMen)); } }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }

        // seria dob ponizej progu (obiekt oblezenia - nowe oblezenie tej samej osady liczy od zera)
        private static readonly Dictionary<SiegeEvent, int> _days = new Dictionary<SiegeEvent, int>();
        private static int _liftedSess, _stumbles, _dStumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        // oblezenia fabuly ROT (dekompilacja ROT.dll 8.1.8: ROT.Events.*SiegeEvent : EventBase - static Instance, IsActive, pole SiegeLeaderParty)
        private static readonly string[] StoryTypes = { "ROT.Events.BlackwaterSiegeEvent", "ROT.Events.WallSiegeEvent", "ROT.Events.BastardsSiegeEvent", "ROT.Events.HarrenhalSiegeEvent" };
        private sealed class StoryRef { public string Name; public FieldInfo Inst; public PropertyInfo Active; public FieldInfo Leader; }
        private static readonly List<StoryRef> _story = new List<StoryRef>();
        private static string _storyMissing = "";

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_err.Add(where)) Log.Error("SiegePhantomLift." + where, e); } catch { }
        }

        private static string Nm(Settlement st) { try { return st != null && st.Name != null ? st.Name.ToString() : "?"; } catch { return "?"; } }
        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }

        /// <summary>Refleksja typow fabuly ROT (raz na sesje; ROT nieobecny = 0/4 - zadne oblezenie nie jest wtedy fabula ROT).</summary>
        private static void ResolveStory()
        {
            _story.Clear();
            var miss = new List<string>();
            foreach (var tn in StoryTypes)
            {
                try
                {
                    var t = AccessTools.TypeByName(tn);
                    var r = t == null ? null : new StoryRef { Name = t.Name, Inst = AccessTools.Field(t, "Instance"), Active = AccessTools.Property(t, "IsActive"), Leader = AccessTools.Field(t, "SiegeLeaderParty") };
                    if (r == null || r.Inst == null || !r.Inst.IsStatic || r.Active == null || r.Active.PropertyType != typeof(bool) || r.Leader == null || r.Leader.FieldType != typeof(MobileParty))
                    {
                        miss.Add(tn.Substring(tn.LastIndexOf('.') + 1));
                        continue;
                    }
                    _story.Add(r);
                }
                catch (Exception e) { miss.Add(tn.Substring(tn.LastIndexOf('.') + 1)); Stumble("ResolveStory", e); }
            }
            _storyMissing = miss.Count > 0 ? "; brak: " + string.Join(", ", miss.ToArray()) : "";
        }

        /// <summary>Oblezenie fabuly ROT: czynne wydarzenie z wodzem (SiegeLeaderParty) w tym obozie. Null = nie.</summary>
        private static string Story(BesiegerCamp camp, List<MobileParty> parties)
        {
            for (int i = 0; i < _story.Count; i++)
            {
                var r = _story[i];
                try
                {
                    var inst = r.Inst.GetValue(null);
                    if (inst == null || !(bool)r.Active.GetValue(inst, null)) continue;
                    var lp = r.Leader.GetValue(inst) as MobileParty;
                    if (lp != null && (parties.Contains(lp) || ReferenceEquals(lp.BesiegerCamp, camp))) return r.Name;
                }
                catch (Exception e) { Stumble("Story." + r.Name, e); }
            }
            return null;
        }

        private static bool PlayerIn(Settlement st, List<MobileParty> parties)
        {
            var main = MobileParty.MainParty;
            if (main == null) return false;
            if (ReferenceEquals(main.CurrentSettlement, st) || ReferenceEquals(main.BesiegedSettlement, st)) return true;
            for (int i = 0; i < parties.Count; i++)
            {
                var p = parties[i];
                if (ReferenceEquals(p, main) || (p.Army != null && ReferenceEquals(p.Army.LeaderParty, main))) return true;
            }
            return false;
        }

        private static bool Others(Settlement st, List<MobileParty> parties)
        {
            for (int i = 0; i < parties.Count; i++) if (Undead.Party(parties[i])) return true;
            try { return st.OwnerClan != null && (ClanIncomeBook.IsUndeadClan(st.OwnerClan) || OthersRise.IsWhiteWalkerClan(st.OwnerClan)); } catch { return false; }
        }

        private static bool InBattle(Settlement st, List<MobileParty> parties)
        {
            if (st.Party != null && st.Party.MapEvent != null) return true;
            for (int i = 0; i < parties.Count; i++) if (parties[i].MapEvent != null) return true;
            return false;
        }

        // ------------------------------------------------------------ linia startu (OnSessionLaunched)
        internal static void SessionStart()
        {
            _days.Clear(); _err.Clear();
            _liftedSess = _stumbles = _dStumbles = 0;
            try
            {
                ResolveStory();
                Log.Info("Oboz bez sily (OB6): start sesji | klucz SiegePhantomLift " + (On ? "tak" : "nie") + ", dob " + LiftDays + ", ponizej " + MinMen + " zdolnych do walki"
                         + " | pomijane: gracz (oboz, armia, w murach), oblezenia fabuly ROT (wpiete " + _story.Count + "/" + StoryTypes.Length + _storyMissing + "), Inni, bitwa w toku"
                         + " | seria dob tylko w pamieci (po wczytaniu od zera).");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        // ------------------------------------------------------------ doba (ArmouryBehavior, po SiegeWalls2.Daily)
        internal static void Daily()
        {
            if (!On) { _days.Clear(); _dStumbles = 0; return; }
            var mgr = Campaign.Current != null ? Campaign.Current.SiegeEventManager : null;
            if (mgr == null) return;
            int min = MinMen, need = LiftDays;
            int cand = 0, lifted = 0, sent = 0, still = 0, skPlayer = 0, skStory = 0, skOthers = 0, skBattle = 0, shown = 0;
            var rows = new StringBuilder(600);
            var live = new HashSet<SiegeEvent>();
            List<SiegeEvent> evs;
            try { evs = new List<SiegeEvent>(mgr.SiegeEvents); }
            catch (Exception e) { Stumble("Daily.list", e); return; }
            foreach (var ev in evs)
            {
                try
                {
                    if (ev == null || ev.ReadyToBeRemoved) continue;
                    var camp = ev.BesiegerCamp;
                    var st = ev.BesiegedSettlement;
                    if (camp == null || st == null) continue;
                    var parties = new List<MobileParty>(4);
                    foreach (var pb in camp.GetInvolvedPartiesForEventType())
                        if (pb != null && pb.MobileParty != null && !parties.Contains(pb.MobileParty)) parties.Add(pb.MobileParty);
                    if (parties.Count == 0) continue;
                    live.Add(ev);
                    int healthy = 0, wounded = 0; float str = 0f;
                    foreach (var p in parties)
                    {
                        if (p.MemberRoster != null) { healthy += p.MemberRoster.TotalHealthyCount; wounded += p.MemberRoster.TotalWounded; }
                        try { if (p.Party != null) str += p.Party.EstimatedStrength; } catch { }
                    }
                    if (healthy >= min) { _days.Remove(ev); continue; }
                    // kandydat - wykluczenia (seria zerowana; bitwa - doba bez liczenia, seria zostaje)
                    if (PlayerIn(st, parties)) { skPlayer++; _days.Remove(ev); continue; }
                    if (Story(camp, parties) != null) { skStory++; _days.Remove(ev); continue; }
                    if (Others(st, parties)) { skOthers++; _days.Remove(ev); continue; }
                    if (InBattle(st, parties)) { skBattle++; continue; }
                    cand++;
                    int d;
                    _days.TryGetValue(ev, out d);
                    d++;
                    _days[ev] = d;
                    bool lift = d >= need;
                    int gone = 0; bool ended = false;
                    if (lift)
                    {
                        // droga gry przy pokoju (FactionHelper :337-338): druzyna obozu BesiegerCamp = null (wodz armii zabiera przylaczonych - MobileParty :716-719),
                        // wolne druzyny i wodzowie armii SetMoveModeHold (mysla od nowa). Po jednej druzynie, nie RemoveAllSiegeParties: petla gry "while (Count > 0)"
                        // nie skonczylaby sie, gdyby druzyna z listy obozu nie miala go juz u siebie (stan nieznany - przyczyny obozu-widma nie znamy)
                        var free = new List<MobileParty>(parties.Count);
                        foreach (var p in parties) if (!p.IsMainParty && p.AttachedTo == null) free.Add(p);
                        foreach (var p in parties)
                        {
                            try { if (!p.IsMainParty && ReferenceEquals(p.BesiegerCamp, camp)) p.BesiegerCamp = null; }
                            catch (Exception e) { Stumble("Daily.Leave", e); }
                        }
                        foreach (var p in parties) if (p.BesiegerCamp == null) gone++;
                        foreach (var p in free)
                        {
                            try { if (p.IsActive && p.BesiegerCamp == null && p.MapEvent == null) p.SetMoveModeHold(); }
                            catch (Exception e) { Stumble("Daily.Hold", e); }
                        }
                        ended = !ReferenceEquals(st.SiegeEvent, ev);
                        sent += gone;
                        if (ended) { lifted++; _liftedSess++; _days.Remove(ev); }
                        else still++;
                    }
                    if (shown < 12)
                    {
                        if (shown > 0) rows.Append("; ");
                        var lead = camp.LeaderParty ?? parties[0];
                        var lh = lead != null ? lead.LeaderHero : null;
                        float food = 0f; bool starving = false;
                        try { food = lead.Food; starving = lead.Party != null && lead.Party.IsStarving; } catch { }
                        rows.Append(Nm(st)).Append(" (").Append(st.StringId).Append("): druzyn ").Append(parties.Count).Append(", zdrowi ").Append(healthy).Append(", ranni ").Append(wounded)
                            .Append(", sila ").Append(str.ToString("0", Inv)).Append(", dob ").Append(d).Append(", wodz ").Append(lh != null ? Nm(lh) : "brak (druzyna bez bohatera)");
                        if (lh != null) rows.Append(" (ranny ").Append(lh.IsWounded ? "tak" : "nie").Append(", w niewoli ").Append(lh.IsPrisoner ? "tak" : "nie").Append(")");
                        bool frozen = false;
                        try { frozen = lead.Ai != null && lead.Ai.DoNotMakeNewDecisions; } catch { }
                        rows.Append(", mysl AI zamrozona ").Append(frozen ? "tak" : "nie");
                        rows.Append(", frakcja ").Append(camp.MapFaction != null && camp.MapFaction.Name != null ? camp.MapFaction.Name.ToString() : "-")
                            .Append(", jedzenie ").Append(food.ToString("0.#", Inv)).Append(starving ? " (glod)" : "")
                            .Append(", oblezenie od ").Append(ev.SiegeStartTime.ElapsedDaysUntilNow.ToString("0.0", Inv)).Append(" dob");
                        if (lift) rows.Append(ended ? ", ZDJETE (druzyn odeslanych " + gone + ")" : ", NIE ZDJETE (druzyn wyszlo " + gone + " z " + parties.Count + ")");
                        shown++;
                    }
                }
                catch (Exception e) { Stumble("Daily.camp", e); }
            }
            // pamiec: oblezenia, ktorych juz nie ma
            if (_days.Count > live.Count)
            {
                var gone = new List<SiegeEvent>();
                foreach (var kv in _days) if (!live.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (var ev in gone) _days.Remove(ev);
            }
            int skipped = skPlayer + skStory + skOthers + skBattle;
            if (cand > 0 || skipped > 0 || _dStumbles > 0)
            {
                try
                {
                    Log.Info("Oboz bez sily (OB6): dzien " + Today + " | obozow ponizej " + min + " zdolnych do walki " + cand + (shown > 0 ? " [" + rows + "]" : "")
                             + " | zdjete dzis " + lifted + " (od sesji " + _liftedSess + "), druzyn odeslanych " + sent + ", oblezen nadal trwajacych po zdjeciu " + still
                             + " | pominiete: gracz " + skPlayer + ", fabula ROT " + skStory + ", Inni " + skOthers + ", w bitwie " + skBattle
                             + " | potkniecia " + _dStumbles + " (sesja " + _stumbles + ").");
                }
                catch { }
            }
            _dStumbles = 0;
        }
    }
}
