using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace Armoury
{
    /// <summary>
    /// O1 - OKUP AI Z AI (klucz SiegeRansomAi; off = 449d453 co do zlotowki). Projekt rozdz. 2 po P1-P10, regula w SiegeRansom.cs (Evaluate).
    ///  - W chwili decyzji gry o szturmie (cykl O0): obronca zaplacilby (okup >= doba zoldu, bez wykluczen) i AI-oblegajacy przyjmuje (potrzeba / szturm bez zdobycia
    ///    / krwawy szturm) - umowa (Deal). Od tej chwili postfiks decyzji daje false (szturm czeka), a umowe wykonuje NASTEPNY tick kampanii (CampaignEvents.TickEvent
    ///    - biegnie tylko przy plynacym czasie, wewnatrz Campaign.Tick, tam gdzie gra sama zwija oblezenia z mysli AI; P5). Nigdy z mysli AI, z opcji menu ani z okna.
    ///  - Wykonanie tylko gdy: oblezenie to samo (osada.SiegeEvent), osada i zadna druzyna obozu nie sa w bitwie (inaczej czeka; FailedAssert gry :259), frakcja obozu
    ///    ta sama i dalej w wojnie z osada; umowa starsza niz doba - przepada (linia "przepadl").
    ///  - Zdjecie: z kazdego zbiornika najwyzej to, co uzgodniono przy decyzji (Want), i najwyzej tyle, ile lezy teraz - kasa kupcow (Town.ChangeGold, ksiega
    ///    MoneyLedger NRansom - P10, osobno od lupu NSack) i kiesy ludu (TownFolk.SackTake - ksiega kies r = 0). Bez GiveGoldAction.
    ///  - Podzial (P4): wszystkie druzyny OBOZU (BesiegerCamp, takze lordowie spoza armii) wedlug ludzi - CitySack.SharePool, ten sam kod co lup L2: 2/3 sakiewki,
    ///    1/3 wodz, korona trzecia czesc z czesci wodza nastepnego ranka (Ransom178 / CrownThirdsShare); Inni bez udzialu, sam oboz Innych - okupu nie ma (O0).
    ///  - Koniec oblezenia (P5): BesiegerCamp.RemoveAllSiegeParties() - ta sama akcja gry co LiftSiegeAction (i co StrategicCampaignAI przy odwrocie): kazda druzyna
    ///    BesiegerCamp = null (gra: SetMoveModeHold, rozprzezenie), przy zerze FinalizeSiegeEvent -> OnSiegeEventEnded; gracz w osadzie dostaje menu gry
    ///    "siege_attacker_left" (PlayerSiege.OnSiegeEventFinalized) - zwykla droga, jak przy odejsciu oblegajacych.
    ///  - Rozejm (P6, P9): frakcje druzyn, ktore wziely okup, nie oblegaja tej osady TruceDays (60) dob - postfiks TargetScoreCalculatingModel.GetTargetScoreForFaction
    ///    = 0 dla (osada, frakcja) przy ArmyTypes.Besieger (bazowy Default - wola go tez StrategicCampaignAI145 - i model z kampanii, jesli ma wlasne nadpisanie);
    ///    druzyny tych frakcji juz w drodze na oblezenie tej osady (DefaultBehavior BesiegeSettlement) - SetMoveModeHold (mysl od nowa, bez tej osady). Pole zapisu
    ///    "arm_siege_ransom" (wersja 1; osobno od arm_sack, ktorego Import odrzuca nieznana wersje): rozejmy i szturmy bez zdobycia trwajacych oblezen.
    ///    Oblezenie wbrew rozejmowi (nasluch OnSiegeEventStarted) - linia "Okup oblezenia (rozejm zlamany)" (prog O-3 = 0).
    ///  - BK ArmySiegePersistence (wlaczone u Jeffa): przy okupie tylko pomiar - czy BK ma wpis armii z celem tej osady i z ostatnim marszem na oblezenie w 24 h
    ///    (tylko wtedy BK moze ja trzymac w miejscu); BK nie jest zmieniany.
    /// Gracz: w obozie AI - wiadomosc z czescia jego druzyny (jak L2); pan osady albo w osadzie (bez O2 albo po 24 h bez odpowiedzi) - wiadomosc; osada krolestwa
    /// gracza - wiadomosc. Wyjatek - licznik potkniec; wyjatek przy podziale - reszta wraca do zbiornikow (SharePool).
    /// </summary>
    internal static partial class SiegeRansom
    {
        internal static bool AiOn { get { var s = Settings.Current; return s != null && s.SiegeRansomAi; } }
        private static bool ActOn { get { return AiOn || PlayerDefOn || PlayerBesOn; } }
        internal static int TruceDays { get { var s = Settings.Current; return s != null ? Math.Max(0, Math.Min(364, s.SiegeRansomTruceDays)) : 60; } }

        // rodzaje okupu (linia O1, liczniki doby)
        internal const int KAi = 0, KDefPaid = 1, KDefLate = 2, KBes = 3;
        private static readonly string[] KindName = { "AI z AI", "gracz-obronca zaplacil", "gracz-obronca bez odpowiedzi (24 h) - regula AI", "gracz-oblegajacy" };

        // ------------------------------------------------------------ umowy w toku
        internal sealed class Deal
        {
            public SiegeEvent Ev; public Settlement St; public IFaction Fac; public Judge J; public int Kind;
            public float Day0;                    // chwila decyzji o szturmie (dni kampanii)
            public float Agreed;                  // chwila wyboru "placi" (AI - przy decyzji; gracz - odpowiedz albo koniec terminu): od niej doba na wykonanie
            public int Choice;                    // 0 czeka na gracza, 1 placi / przyjmuje, 2 odmowa
            public float Deadline;                // gracz-obronca: do kiedy czekamy (24 h), potem regula AI
            public bool Shown; public int AskFails;
        }
        private static readonly List<Deal> _deals = new List<Deal>();

        private static Deal DealOf(SiegeEvent ev)
        {
            for (int i = 0; i < _deals.Count; i++) if (_deals[i].Ev == ev) return _deals[i];
            return null;
        }

        /// <summary>Szturm czeka: umowa tego oblezenia uzgodniona (wykonanie w ticku) albo pytanie gracza bez odpowiedzi.</summary>
        private static bool Hold(Siege s, bool count)
        {
            if (s == null || _deals.Count == 0) return false;
            var d = DealOf(s.Ev);
            if (d == null || d.Choice == 2) return false;
            if (count) _dHeld++;
            return true;
        }

        /// <summary>Wynik cyklu decyzji (napis "wynik" linii O0) - przy kluczu O1 umowa AI z AI. Gracz-obronca (O2) i gracz-oblegajacy (O3) - DecidePlayer.</summary>
        private static string Decide(Siege s, Judge j)
        {
            if (!ActOn) return "szturm (gra; okup wylaczony)";
            if (j.Excl != null) return "szturm (wykluczone: " + j.Excl + ")";
            if (!j.DefPays) return "szturm (obronca nie zaplaci: " + j.DefWhy + ")";
            if (!j.AiAccepts) return "szturm (AI nie przyjmuje: " + j.AccWhy + ")";
            string p = DecidePlayer(s, j);                                  // O2: pytanie gracza-obroncy; null - regula AI
            if (p != null) return p;
            if (!AiOn) return "szturm (okup AI wylaczony - Siege Ransom Ai)";
            NewDeal(s, j, KAi, 1);
            return "okup AI z AI (" + j.AccWhy + ")";
        }

        private static Deal NewDeal(Siege s, Judge j, int kind, int choice)
        {
            var d = new Deal { Ev = s.Ev, St = s.St, Fac = j.Faction, J = j, Kind = kind, Day0 = Now, Agreed = Now, Choice = choice };
            _deals.Add(d);
            _dDeals[kind]++;
            return d;
        }

        private static void DropDeals(SiegeEvent ev, string why)
        {
            if (ev == null || _deals.Count == 0) return;
            for (int i = _deals.Count - 1; i >= 0; i--)
            {
                var d = _deals[i];
                if (d.Ev != ev) continue;
                _deals.RemoveAt(i);
                Dropped(d, why);
            }
        }

        private static void Dropped(Deal d, string why)
        {
            if (d.Choice == 2) return;                                          // odmowa gracza - znacznik do konca oblezenia, nie umowa, ktora przepadla
            _dDropped++;
            try
            {
                if (MeasureOn)
                    Log.Info("Okup oblezenia (przepadl): dzien " + Today + " | osada " + (d.St != null ? d.St.StringId : "?") + " | rodzaj " + KindName[d.Kind] + " | wybor "
                             + (d.Choice == 0 ? "czeka na gracza" : d.Choice == 1 ? "placi" : "odmowa") + " | uzgodniony dzien " + (int)d.Day0 + " | powod: " + why + ".");
            }
            catch { }
        }

        // ------------------------------------------------------------ tick kampanii: wykonanie (P5)
        /// <summary>CampaignEvents.TickEvent (ArmouryBehavior.RegisterEvents): przy braku umow - jedno porownanie. Umowa z wyborem "placi" - wykonanie, gdy oblezenie
        /// jest wolne od bitew; O2 - pytanie gracza-obroncy (okno, termin 24 h). Nie rzuca.</summary>
        internal static void Tick()
        {
            if (_deals.Count == 0) return;
            try
            {
                var arr = _deals.ToArray();
                foreach (var d in arr)
                {
                    if (!_deals.Contains(d)) continue;                          // zdjeta w tym ticku (koniec oblezenia innej umowy)
                    try { Step(d); }
                    catch (Exception e) { _deals.Remove(d); Stumble("Tick.Step", e); }
                }
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        private static void Step(Deal d)
        {
            var st = d.St;
            if (st == null || st.SiegeEvent != d.Ev || d.Ev.BesiegerCamp == null) { _deals.Remove(d); Dropped(d, "oblezenia juz nie ma"); return; }
            if (d.Choice == 0) { StepAsk(d); return; }                           // O2: pytanie gracza
            if (d.Choice == 2) return;                                          // odmowa - umowa trzyma tylko znacznik do konca oblezenia
            var camp = d.Ev.BesiegerCamp;
            if (camp.MapFaction != d.Fac) { _deals.Remove(d); Dropped(d, "oboz zmienil frakcje"); return; }
            if (st.MapFaction == null || d.Fac == null || !FactionManager.IsAtWarAgainstFaction(d.Fac, st.MapFaction)) { _deals.Remove(d); Dropped(d, "nie ma juz wojny"); return; }
            if (Now - d.Agreed > 1f) { _deals.Remove(d); Dropped(d, "doba bez wykonania (bitwa przy oblezeniu)"); return; }
            if (st.Party != null && st.Party.MapEvent != null) { _dWaitBattle++; return; }
            if (d.Kind == KBes && PlayerEncounter.Current != null) { _dWaitBattle++; return; }   // O3: gracz w spotkaniu - czekamy (zwiniecie obozu bez spotkania gry)
            foreach (var pb in camp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege))
                if (pb != null && pb.MapEvent != null) { _dWaitBattle++; return; }
            _deals.Remove(d);                                                   // przed zwinieciem obozu (OnSiegeEventEnded nie znajdzie juz tej umowy)
            Execute(d, camp);
        }

        private static void Execute(Deal d, BesiegerCamp camp)
        {
            var st = d.St;
            var town = st.Town;
            var j = d.J;
            // ---- odbiorcy: wszystkie druzyny obozu wedlug ludzi (P4)
            var contrib = new Dictionary<MobileParty, float>();
            foreach (var pb in camp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege))
                if (pb != null && pb.MobileParty != null && !contrib.ContainsKey(pb.MobileParty)) contrib[pb.MobileParty] = 1f;
            int men, uMen;
            var recs = CitySack.Recipients(camp.LeaderParty, contrib, out men, out uMen);
            var live = new List<CitySack.Rec>();
            foreach (var r in recs) if (!r.Undead) live.Add(r);
            if (men <= 0) { Dropped(d, "oboz bez zywych odbiorcow"); return; }
            // ---- zdjecie: najwyzej uzgodnione, najwyzej to, co lezy teraz
            var k = CitySack.Read(st);
            long[] took = new long[5];
            long w0 = Math.Min(j.Want[0], k.Kasa);
            int had = town.Gold;
            if (w0 > 0) town.ChangeGold(-(int)Math.Min(int.MaxValue, w0));
            took[0] = Math.Max(0, had - town.Gold);
            if (took[0] > 0) MoneyLedger.Note(MoneyLedger.NRansom, st, -(int)Math.Min(int.MaxValue, took[0]), -1);
            for (int c = 0; c < 4; c++) took[c + 1] = j.Want[c + 1] > 0 ? TownFolk.SackTake(st, c, Math.Min(j.Want[c + 1], k.Folk[c])) : 0;
            long pool = 0;
            for (int i = 0; i < 5; i++) pool += took[i];
            // ---- podzial: ten sam kod co lup L2
            var sp = CitySack.SharePool(st, pool, live, men, took, MoneyLedger.NRansom);
            if (sp.Err != null) Stumble("Execute.Split", sp.Err);
            long diff = sp.Given - pool;
            // ---- rozejm: frakcje druzyn, ktore wziely okup
            int until = Today + TruceDays;
            var facs = new List<string>(2);
            foreach (var r in live)
            {
                var f = r.Mp != null ? r.Mp.MapFaction : null;
                if (f == null || f.StringId == null || facs.Contains(f.StringId)) continue;
                facs.Add(f.StringId);
                AddTruce(st, f.StringId, until);
            }
            if (d.Fac != null && d.Fac.StringId != null && !facs.Contains(d.Fac.StringId)) { facs.Add(d.Fac.StringId); AddTruce(st, d.Fac.StringId, until); }
            // ---- BK ArmySiegePersistence: tylko pomiar (wpis armii wodza z celem tej osady w 24 h)
            string bk = BkStick(camp.LeaderParty, st);
            // ---- koniec oblezenia droga gry (P5)
            var leaderHero = camp.LeaderParty != null ? camp.LeaderParty.LeaderHero : null;
            var playerRec = (CitySack.Rec)null;
            foreach (var r in live) if (r.Mp == MobileParty.MainParty) playerRec = r;
            bool playerInCamp = MobileParty.MainParty != null && contrib.ContainsKey(MobileParty.MainParty);
            try { camp.RemoveAllSiegeParties(); }
            catch (Exception e) { Stumble("Execute.Lift", e); }          // zaplacone i rozejm zapisany - oboz, ktory by zostal, traci cel (wynik 0) i odchodzi z mysli AI
            int left = 0;
            try { if (st.SiegeEvent == d.Ev && d.Ev.BesiegerCamp != null) foreach (var pb in d.Ev.BesiegerCamp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege)) left++; } catch { left = -1; }
            bool ended = st.SiegeEvent != d.Ev;
            int turned = TurnBack(st);
            // ---- liczniki doby
            _dPaid[d.Kind]++;
            _dPool += pool; _dMerch += took[0]; _dFolk += took[1] + took[2] + took[3] + took[4];
            _dToMen += sp.ToMen; _dToLead += sp.ToLead; _dToParty += sp.ToParty; _dCrownBase += sp.CrownBase;
            _dDiff += Math.Abs(diff); _dTruceNew += facs.Count; _dTurned += turned;
            if (left != 0 || !ended) _dCampLeft++;
            if (bk.StartsWith("tak", StringComparison.Ordinal)) _dBk++;
            // ---- linia O1 (zawsze przy kluczu logu - prog O-2 czyta kazdy okup)
            long tm = sp.ToMen, tl = sp.ToLead, tp = sp.ToParty, pl = playerRec != null ? playerRec.ToMen + playerRec.ToLead : 0;
            var s = Settings.Current;
            if (s != null && s.LogEnabled)
            {
                var sb = new StringBuilder(1100);
                sb.Append("Okup oblezenia (O1): dzien ").Append(Today).Append(" | osada ").Append(st.StringId).Append(" (").Append(Nm(st)).Append(", ").Append(st.IsTown ? "miasto" : "zamek").Append(')')
                  .Append(" | rodzaj ").Append(KindName[d.Kind])
                  .Append(" | placi: krolestwo ").Append(st.MapFaction != null ? st.MapFaction.StringId : "-").Append(", rod ").Append(st.OwnerClan != null ? st.OwnerClan.StringId : "-")
                  .Append(" | bierze: frakcja ").Append(d.Fac != null ? d.Fac.StringId : "-").Append(", wodz ").Append(leaderHero != null ? leaderHero.Name.ToString() : "-")
                  .Append(" | powod: ").Append(d.Kind == KBes ? "wybor gracza" : j.AccWhy)
                  .Append(" | przy decyzji: okup ").Append(j.Ransom).Append(", doba zoldu ").Append(j.Wage)
                  .Append(" | zdjete: kupcy ").Append(took[0]).Append(" z ").Append(k.Kasa).Append(", W ").Append(took[1]).Append(" z ").Append(k.Folk[0]).Append(", R ").Append(took[2]).Append(" z ").Append(k.Folk[1])
                  .Append(", P ").Append(took[3]).Append(" z ").Append(k.Folk[2]).Append(", gmina ").Append(took[4]).Append(" z ").Append(k.Folk[3]).Append(" (kiesa ").Append(k.FolkLive ? "czynna" : "nieczynna").Append(')')
                  .Append(" | razem ").Append(pool)
                  .Append(" | odbiorcy ").Append(live.Count).Append(" druzyn (ludzie ").Append(men).Append("; Inni ").Append(uMen).Append(" - bez udzialu)")
                  .Append(" | zolnierze ").Append(tm).Append(", wodzowie ").Append(tl).Append(" (gracz razem ").Append(pl).Append("), kiesy druzyn bez wodza ").Append(tp)
                  .Append(", reszta zaokraglen do ").Append(sp.Big != null && sp.Big.Mp != null ? sp.Big.Mp.StringId : "-")
                  .Append(" | korona: trzecie wodzow w podstawie prawa trzecich ").Append(sp.CrownBase)
                  .Append(" | dopisane ").Append(sp.Given).Append(" - zdjete ").Append(pool).Append(" = ").Append(diff)
                  .Append(" | rozejm: ").Append(string.Join(", ", facs.ToArray())).Append(" do dnia ").Append(until).Append(" (").Append(TruceDays).Append(" dob)")
                  .Append(" | oboz po okupie: druzyn ").Append(left).Append(", oblezenie zakonczone ").Append(ended ? "tak" : "nie")
                  .Append(" | zawrocone w drodze ").Append(turned)
                  .Append(" | BK SIEGE-STICK: ").Append(bk)
                  .Append(" | stolica ").Append(j.Capital ? "tak" : "nie").Append(", fabula ").Append(j.Story ? "tak" : "nie").Append(", Inni ").Append(j.Undead ? "tak" : "nie")
                  .Append(" | gracz: ").Append(playerInCamp ? "w obozie" : j.Player).Append('.');
                Log.Info(sb.ToString());
            }
            Messages(d, st, leaderHero, pool, took, playerRec, playerInCamp);
        }

        // ------------------------------------------------------------ wiadomosci gracza (po angielsku)
        private static void Messages(Deal d, Settlement st, Hero leader, long pool, long[] took, CitySack.Rec pr, bool playerInCamp)
        {
            try
            {
                if (pool <= 0) return;
                string town = Nm(st);
                string lead = leader != null && leader.Name != null ? leader.Name.ToString() : "The besiegers";
                if (d.Kind == KBes) { BesiegerDone(d, st, pool, took, pr); return; }   // O3: wlasny napis
                if (playerInCamp)
                {
                    long mine = pr != null ? pr.ToMen + pr.ToLead : 0;
                    long crown = pr != null && pr.Lead != null ? CitySack.CrownPart(pr.Lead, pr.ToLead) : 0;
                    Log.Player(lead + " took a ransom of " + N(pool) + " denars from " + town + " and lifted the siege."
                               + (mine > 0 ? " Your party's part: " + N(mine) + " denars" + (pr.ToMen > 0 ? " (your men " + N(pr.ToMen) + ", you " + N(pr.ToLead) + ")" : "")
                                             + (crown > 0 ? "; the crown will claim " + N(crown) + " of your part" : "") + "." : ""));
                    return;
                }
                bool own = st.OwnerClan != null && st.OwnerClan == Clan.PlayerClan;
                bool realm = !own && Clan.PlayerClan != null && Clan.PlayerClan.Kingdom != null && ReferenceEquals(Clan.PlayerClan.Kingdom, st.MapFaction);
                if (own || realm || (MobileParty.MainParty != null && MobileParty.MainParty.CurrentSettlement == st))
                {
                    string head = d.Kind == KDefLate ? town + " could not wait for your word: it" : town;
                    Log.Player(head + " paid a ransom of " + N(pool) + " denars" + (st.IsTown ? " from its merchants and townsfolk" : " from its treasury and castle folk")
                               + "; the enemy has lifted the siege and swears not to return for " + TruceDays + " days.", own);
                }
            }
            catch (Exception e) { Stumble("Messages", e); }
        }

        // ------------------------------------------------------------ rozejm (P6)
        private static readonly Dictionary<string, Dictionary<string, int>> _truce = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

        private static void AddTruce(Settlement st, string fac, int until)
        {
            if (st == null || st.StringId == null || string.IsNullOrEmpty(fac)) return;
            Dictionary<string, int> m;
            if (!_truce.TryGetValue(st.StringId, out m)) { m = new Dictionary<string, int>(StringComparer.Ordinal); _truce[st.StringId] = m; }
            int old;
            if (!m.TryGetValue(fac, out old) || old < until) m[fac] = until;
        }

        /// <summary>Frakcja f ma rozejm z osada st (okup w ostatnich TruceDays dobach). Tylko odczyt.</summary>
        internal static bool IsTruce(Settlement st, IFaction f)
        {
            if (_truce.Count == 0 || st == null || f == null || st.StringId == null || f.StringId == null) return false;
            Dictionary<string, int> m;
            int until;
            return _truce.TryGetValue(st.StringId, out m) && m.TryGetValue(f.StringId, out until) && Today < until;
        }

        internal static int TruceUntil(Settlement st, IFaction f)
        {
            Dictionary<string, int> m;
            int until;
            return st != null && f != null && st.StringId != null && f.StringId != null && _truce.TryGetValue(st.StringId, out m) && m.TryGetValue(f.StringId, out until) ? until : 0;
        }

        /// <summary>Postfiks GetTargetScoreForFaction (Last): rozejm - oblezenie tej osady przez te frakcje ma wynik 0 (AI wybiera inny cel). Nie rzuca.</summary>
        public static void ScorePost(Settlement __0, Army.ArmyTypes __1, MobileParty __2, ref float __result)
        {
            if (__result <= 0f || _truce.Count == 0 || __1 != Army.ArmyTypes.Besieger) return;
            try
            {
                if (!ActOn || __0 == null || __2 == null) return;
                if (IsTruce(__0, __2.MapFaction)) { __result = 0f; _dScoreZero++; }
            }
            catch (Exception e) { Stumble("ScorePost", e); }
        }

        /// <summary>Po okupie: druzyny frakcji z rozejmem juz w drodze na oblezenie tej osady - SetMoveModeHold (AI mysli od nowa; wynik celu 0). Zwraca ile.</summary>
        private static int TurnBack(Settlement st)
        {
            int n = 0;
            try
            {
                foreach (var mp in MobileParty.AllLordParties)
                {
                    if (mp == null || !mp.IsActive || mp.IsMainParty || mp.AttachedTo != null || mp.BesiegerCamp != null) continue;
                    if (mp.DefaultBehavior != AiBehavior.BesiegeSettlement || mp.TargetSettlement != st || !IsTruce(st, mp.MapFaction)) continue;
                    mp.SetMoveModeHold();
                    n++;
                }
            }
            catch (Exception e) { Stumble("TurnBack", e); }
            return n;
        }

        /// <summary>Nasluch OnSiegeEventStarted: oblezenie wbrew rozejmowi - linia i licznik (prog O-3 = 0). Nie zmienia gry. Nie rzuca.</summary>
        internal static void OnSiegeStarted(SiegeEvent ev)
        {
            try
            {
                if (_truce.Count == 0 || ev == null || ev.BesiegedSettlement == null || ev.BesiegerCamp == null) return;
                var lp = ev.BesiegerCamp.LeaderParty;
                var f = lp != null ? lp.MapFaction : ev.BesiegerCamp.MapFaction;
                var st = ev.BesiegedSettlement;
                if (!IsTruce(st, f)) return;
                bool player = lp != null && lp.IsMainParty;
                if (player) _dBrokenPlayer++; else _dBroken++;
                Log.Info("Okup oblezenia (rozejm zlamany): dzien " + Today + " | osada " + st.StringId + " | frakcja " + (f != null ? f.StringId : "-") + " | wodz "
                         + (lp != null && lp.LeaderHero != null ? lp.LeaderHero.Name.ToString() : (lp != null ? lp.StringId : "-")) + " | gracz " + (player ? "tak" : "nie")
                         + " | rozejm do dnia " + TruceUntil(st, f) + ".");
            }
            catch (Exception e) { Stumble("OnSiegeStarted", e); }
        }

        // ------------------------------------------------------------ BK ArmySiegePersistence - pomiar (refleksja, tylko odczyt)
        private static bool _bkTried;
        private static IDictionary _bkState;

        private static string BkStick(MobileParty leader, Settlement st)
        {
            try
            {
                if (!_bkTried)
                {
                    _bkTried = true;
                    var t = QuartermasterLaw.FindType("BannerKings.Patches.ArmySiegePersistencePatches");
                    var f = t != null ? t.GetField("State", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) : null;
                    _bkState = f != null ? f.GetValue(null) as IDictionary : null;
                }
                if (_bkState == null) return "brak (BK bez tej latki)";
                if (leader == null || leader.Army == null || leader.StringId == null || !_bkState.Contains(leader.StringId)) return "nie (brak wpisu)";
                var o = _bkState[leader.StringId];
                if (o == null) return "nie (pusty wpis)";
                var tt = o.GetType();
                var target = tt.GetField("Target") != null ? tt.GetField("Target").GetValue(o) as Settlement : null;
                var lastF = tt.GetField("LastBesiegeHour");
                float last = lastF != null ? (float)lastF.GetValue(o) : 0f;
                double age = CampaignTime.Now.ToHours - last;
                if (target == st && age <= 24.0) return "tak (marsz na oblezenie " + F(age, "0") + " h temu - BK moze trzymac armie do " + F(24.0 - age, "0") + " h)";
                return "nie (wpis " + (target == st ? "tej osady sprzed " + F(age, "0") + " h" : "innego celu") + ")";
            }
            catch (Exception e) { Stumble("BkStick", e); return "blad odczytu"; }
        }

        // ------------------------------------------------------------ zapis "arm_siege_ransom" (wersja 1)
        private sealed class FailRec { public string St; public double Start; public int Failed; public string Fac; }
        private static readonly List<FailRec> _pendingFailed = new List<FailRec>();

        /// <summary>Szturmy bez zdobycia z zapisu dla tego samego oblezenia (osada i chwila poczatku oblezenia) - przy pierwszym uzyciu stanu oblezenia.</summary>
        private static void RestoreFailed(Siege s)
        {
            if (_pendingFailed.Count == 0 || s == null || s.St == null || s.Ev == null) return;
            double start = s.Ev.SiegeStartTime.ToHours;
            for (int i = _pendingFailed.Count - 1; i >= 0; i--)
            {
                var r = _pendingFailed[i];
                if (r.St != s.St.StringId || Math.Abs(r.Start - start) > 0.05) continue;
                s.Failed = Math.Max(s.Failed, r.Failed);
                s.FailFaction = r.Fac;
                _pendingFailed.RemoveAt(i);
            }
        }

        internal static string Export()
        {
            var sb = new StringBuilder(64);
            sb.Append('1');
            int today = Today;
            foreach (var kv in _truce)
                foreach (var f in kv.Value)
                    if (f.Value > today) sb.Append(";T|").Append(kv.Key).Append('|').Append(f.Key).Append('|').Append(f.Value.ToString(Inv));
            foreach (var kv in _sieges)
            {
                var s = kv.Value;
                if (s == null || s.Failed <= 0 || s.St == null || kv.Key == null) continue;
                sb.Append(";F|").Append(s.St.StringId).Append('|').Append(kv.Key.SiegeStartTime.ToHours.ToString("R", Inv)).Append('|').Append(s.Failed.ToString(Inv)).Append('|').Append(s.FailFaction ?? "");
            }
            foreach (var r in _pendingFailed)
            {
                var st = Settlement.Find(r.St);
                if (st == null || st.SiegeEvent == null || Math.Abs(st.SiegeEvent.SiegeStartTime.ToHours - r.Start) > 0.05) continue;   // oblezenie z zapisu juz sie skonczylo
                sb.Append(";F|").Append(r.St).Append('|').Append(r.Start.ToString("R", Inv)).Append('|').Append(r.Failed.ToString(Inv)).Append('|').Append(r.Fac ?? "");
            }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _truce.Clear(); _pendingFailed.Clear();
            if (string.IsNullOrEmpty(s)) return;
            int bad = 0;
            var parts = s.Split(';');
            if (parts.Length == 0 || parts[0] != "1") { Log.Info("Okup oblezenia: zapis arm_siege_ransom w nieznanej wersji - pominiety (rozejmy od nowa)."); return; }
            for (int i = 1; i < parts.Length; i++)
            {
                try
                {
                    var a = parts[i].Split('|');
                    if (a.Length >= 4 && a[0] == "T")
                    {
                        Dictionary<string, int> m;
                        if (!_truce.TryGetValue(a[1], out m)) { m = new Dictionary<string, int>(StringComparer.Ordinal); _truce[a[1]] = m; }
                        m[a[2]] = int.Parse(a[3], Inv);
                    }
                    else if (a.Length >= 5 && a[0] == "F")
                        _pendingFailed.Add(new FailRec { St = a[1], Start = double.Parse(a[2], NumberStyles.Float, Inv), Failed = int.Parse(a[3], Inv), Fac = a[4] });
                    else bad++;
                }
                catch { bad++; }
            }
            if (bad > 0) Log.Info("Okup oblezenia: zapis arm_siege_ransom - pominieto " + bad + " uszkodzonych wpisow.");
        }

        // ------------------------------------------------------------ doba
        private static readonly int[] _dDeals = new int[4], _dPaid = new int[4];
        private static int _dHeld, _dDropped, _dWaitBattle, _dTruceNew, _dTurned, _dCampLeft, _dBk, _dBroken, _dBrokenPlayer, _dScoreZero;
        private static long _dPool, _dMerch, _dFolk, _dToMen, _dToLead, _dToParty, _dCrownBase, _dDiff;

        private static void DailyMore()
        {
            if (_truce.Count == 0) return;
            int today = Today;
            List<string> emptySt = null;
            foreach (var kv in _truce)
            {
                List<string> gone = null;
                foreach (var f in kv.Value) if (f.Value <= today) { if (gone == null) gone = new List<string>(); gone.Add(f.Key); }
                if (gone != null) foreach (var g in gone) kv.Value.Remove(g);
                if (kv.Value.Count == 0) { if (emptySt == null) emptySt = new List<string>(); emptySt.Add(kv.Key); }
            }
            if (emptySt != null) foreach (var k in emptySt) _truce.Remove(k);
        }

        private static bool DayAny
        {
            get
            {
                return _dDeals[0] + _dDeals[1] + _dDeals[2] + _dDeals[3] + _dPaid[0] + _dPaid[1] + _dPaid[2] + _dPaid[3] > 0 || _dDropped > 0 || _dBroken > 0 || _dBrokenPlayer > 0
                       || _dScoreZero > 0 || DayAnyPlayer;
            }
        }

        private static void DayPart(StringBuilder sb)
        {
            int truces = 0;
            foreach (var kv in _truce) truces += kv.Value.Count;
            sb.Append(" | umowy: AI z AI ").Append(_dDeals[KAi]).Append(", gracz-obronca ").Append(_dDeals[KDefPaid] + _dDeals[KDefLate]).Append(", gracz-oblegajacy ").Append(_dDeals[KBes])
              .Append("; szturm wstrzymany (decyzje gry przy umowie) ").Append(_dHeld).Append(", czekanie na koniec bitwy ").Append(_dWaitBattle).Append(", przepadle ").Append(_dDropped)
              .Append(" | okupy zaplacone: AI z AI ").Append(_dPaid[KAi]).Append(", gracz-obronca ").Append(_dPaid[KDefPaid]).Append(" (bez odpowiedzi 24 h ").Append(_dPaid[KDefLate]).Append(')')
              .Append(", gracz-oblegajacy ").Append(_dPaid[KBes])
              .Append(" | zdjete ").Append(_dPool).Append(" (kupcy ").Append(_dMerch).Append(", lud ").Append(_dFolk).Append(") -> zolnierze ").Append(_dToMen).Append(", wodzowie ").Append(_dToLead)
              .Append(", druzyny bez wodza ").Append(_dToParty).Append("; korona: podstawa prawa trzecich ").Append(_dCrownBase).Append(" (jutro 1/3 w wojnie)")
              .Append("; kontrola dopisane - zdjete (prog 0) ").Append(_dDiff)
              .Append(" | rozejmy czynne ").Append(truces).Append(" (nowe ").Append(_dTruceNew).Append("), cele AI wyzerowane ").Append(_dScoreZero).Append(", zawrocone w drodze ").Append(_dTurned)
              .Append(", oblezenia wbrew rozejmowi: AI ").Append(_dBroken).Append(", gracz ").Append(_dBrokenPlayer)
              .Append(" | oboz niepusty po okupie ").Append(_dCampLeft).Append(" | BK moze trzymac armie po okupie ").Append(_dBk)
              .Append(" | latka rozejmu: ").Append(_hookScore);
            DayPartPlayer(sb);
        }

        private static void ZeroDayMore()
        {
            Array.Clear(_dDeals, 0, 4); Array.Clear(_dPaid, 0, 4);
            _dHeld = _dDropped = _dWaitBattle = _dTruceNew = _dTurned = _dCampLeft = _dBk = _dBroken = _dBrokenPlayer = _dScoreZero = 0;
            _dPool = _dMerch = _dFolk = _dToMen = _dToLead = _dToParty = _dCrownBase = _dDiff = 0;
            ZeroDayPlayer();
        }

        private static void ResetMore()
        {
            _deals.Clear();
            _truce.Clear(); _pendingFailed.Clear();
            ResetPlayer();
        }

        // ------------------------------------------------------------ latki rozejmu (w kampanii)
        private static string _hookScore = "nie zakladana";

        private static void EnsureHooksMore()
        {
            try
            {
                var wired = new List<string>(2);
                var mb = AccessTools.Method(typeof(DefaultTargetScoreCalculatingModel), "GetTargetScoreForFaction");
                if (mb != null)
                {
                    Seal31.Patch("okup oblezenia: rozejm (DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction)", mb, typeof(SiegeRansom), null, nameof(ScorePost), null,
                                 Priority.Normal, Priority.Last);
                    wired.Add("Default");
                }
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.TargetScoreCalculatingModel : null;
                if (model != null && model.GetType() != typeof(DefaultTargetScoreCalculatingModel))
                {
                    var mm = AccessTools.Method(model.GetType(), "GetTargetScoreForFaction");
                    if (mm != null && mm.DeclaringType == model.GetType())
                    {
                        Seal31.Patch("okup oblezenia: rozejm (" + model.GetType().FullName + ".GetTargetScoreForFaction)", mm, typeof(SiegeRansom), null, nameof(ScorePost), null,
                                     Priority.Normal, Priority.Last);
                        wired.Add(model.GetType().Name);
                    }
                }
                _hookScore = wired.Count > 0 ? "wpieta (" + string.Join(", ", wired.ToArray()) + ")" : "BRAK";
            }
            catch (Exception e) { _hookScore = "BRAK (blad: " + e.Message + ")"; Stumble("EnsureHooksMore", e); }
            EnsureHooksPlayer();
        }
    }
}
