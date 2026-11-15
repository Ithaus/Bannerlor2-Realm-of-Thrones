using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 555 - JEDNORAZOWY DAR ZALOGI DLA BANEFORTU (Jeff, czat 15.11: "Banefort - tak, daj mu zaloge taka jak mial przed szturmem i powieksz o 50%").
    /// DOWOD: 544 oddal Banefort (ROT_castle20) House Banefort bez obsady (Armoury-2026-11-15_15-14-12.log l.864: "zaloga Banefort 311 -> [...], zostalo 0",
    /// "po zmianie Banefort: pan Banefort, zaloga 0"); Polnoc oblega go znowu od dnia 109116 przy 11.8:1 (l.8978: obroncy z milicja 411). Zaloga sprzed oblezenia
    /// (oblezenie od dnia 108995, Armoury-2026-11-15_01-07-36.log l.56285: obroncy z milicja 678; linie OB1 dni 109003-109012: "obrona: zaloga 336, milicja 344-347")
    /// = 336 ludzi; x 1.5 = 504. Skladu zalogi log nie zapisywal - sklad z drzewa oddzialow kultury pana (BasicTroop i awanse) wedlug realnej mieszanki tierow.
    /// ZAMKNIETA EKONOMIA: wyjatek z rozkazu Jeffa - ludzie z niczego (bez zdjecia z ludnosci, bez zlota), linia logu "dar 555"; ksiega ludzi: koszyk bez pochodzenia.
    /// Dalej zaloga zyje wedlug zwyklych regul (zold pana, limit zalogi i przepelnienie W4c, zbrojownia zalogi dokupi sprzet).
    /// KIEDY: raz na kampanie, flaga w zapisie "arm_555_banefort" ("" czeka, "new" nowa kampania - nic, "done:dzien:wynik"). Proba co godzine gry (HourlyTickEvent).
    /// RECENZJA 555: dar tylko gdy 544 w tej kampanii naprawde oddal zamek (stan 544 "done:dzien:ok"; 544 czeka - czekamy; inny wynik - bez daru), gracz na mapie
    /// (BookHoldings.PlayerOnMapFor jak 544), tansza mieszanka tierow (bez T6), ksiega ludzi - przyczyna FGift555. Limit zalogi gry i zold - DECYZJA JEFFA (CHANGELOG).
    /// WARUNKI: pan Banefortu = House Banefort (ROTclan_59) albo rod panujacy korony "vlandia" (odbiorca zapasowy 544), nie rod gracza. Pan z Polnocy, gdy 544
    /// jeszcze czeka - czekamy na 544; inny pan - koniec bez zmian. OBLEZENIE: gra sama dopisuje ludzi do zalogi oblezonej twierdzy (GarrisonTroopsCampaignBehavior.
    /// OnSettlementEntered - lord wchodzacy do oblezonej osady zostawia ludzi, bez warunku oblezenia; dekompilacja 1.4.8), a wlasny werbunek zalogi wstrzymuje
    /// tylko przy MapEvent / SiegeEvent (regula gry, nie bezpieczenstwo). Dlatego dar idzie tez w oblezeniu, ale NIE w trakcie bitwy (MapEvent osady albo zalogi)
    /// i nie gdy gracz stoi w Banefort (ekran zalogi moze byc otwarty) - wtedy czekamy (linia raz na dobe). Potkniecia liczone, bez wylacznika.
    /// </summary>
    internal static class Banefort555
    {
        internal const string Tag = "Banefort (555): ";
        private const string StId = "ROT_castle20", ClanId = "ROTclan_59", CrownId = "vlandia";
        internal const int PreSiegeGarrison = 336;                       // linie OB1 dni 109003-109012 (oblezenie od 108995)
        internal const int Target = PreSiegeGarrison * 3 / 2;            // +50% = 504
        // mieszanka tierow zalogi (T1..T6) - wiekszosc szeregowcy T1-T3, garstka weteranow; brak tieru w drzewie - udzial dzielony na pozostale.
        // Recenzja 555 #3: dawne { 0.10, 0.25, 0.30, 0.20, 0.10, 0.05 } = ok. 6.1 zoldu na czlowieka (GetCharacterWage: T1 2, T2 3, T3 5, T4 8, T5 12, T6 17),
        // House Banefort nie udzwignie; ta mieszanka ok. 4.05 na czlowieka (-34%), bez T6 (zaloga to nie gwardia)
        private static readonly double[] TierShare = { 0.30, 0.35, 0.20, 0.10, 0.05, 0.00 };
        private const int MaxErrors = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static string _state = "";
        private static int _errors, _waitDay = -1;
        private static string _waitText;
        private static bool _errLogged;

        internal static void Reset() { _state = ""; _errors = 0; _waitDay = -1; _waitText = null; _errLogged = false; }
        internal static string Export() { return "b1|" + _state; }

        internal static void Import(string v)
        {
            Reset();
            if (string.IsNullOrEmpty(v)) return;   // zapis sprzed 555 - dar czeka
            try
            {
                var p = v.Split('|');
                if (p.Length < 2 || p[0] != "b1") { _state = "done:?:nieznany-format"; return; }
                _state = p[1];
            }
            catch { _state = "done:?:blad-odczytu"; }
        }

        internal static void OnNewGame() { _state = "new"; }

        internal static void SessionStart()
        {
            try
            {
                Log.Info(Tag + "start sesji | stan zapisu: " + (_state.Length == 0 ? "czeka (dar zalogi " + Target + " = " + PreSiegeGarrison + " sprzed oblezenia x 1.5 w pierwszej godzinie)"
                         : _state == "new" ? "nowa kampania - nic" : "zrobione (" + _state + ")") + ".");
            }
            catch { }
        }

        private static Kingdom FindKingdom(string id) { foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k; return null; }
        private static string Nm(object o) { try { return o == null ? "-" : (o is Clan ? ((Clan)o).Name.ToString() : o is Kingdom ? ((Kingdom)o).Name.ToString() : o is CharacterObject ? ((CharacterObject)o).Name.ToString() : o.ToString()); } catch { return "?"; } }

        private static void Done(int day, string res, string text)
        {
            _state = "done:" + day.ToString(Inv) + ":" + res;
            Log.Info(Tag + "dzien " + day + " | " + text + " | flaga w zapisie: zrobione (nie powtorzy sie).");
        }

        private static void Wait(int day, string why)
        {
            _errors = 0;
            if (day == _waitDay && why == _waitText) return;
            if (day == _waitDay && why != null && _waitText != null && why.StartsWith("gracz", StringComparison.Ordinal) && _waitText.StartsWith("gracz", StringComparison.Ordinal)) return;   // jak 544
            _waitDay = day; _waitText = why;
            Log.Info(Tag + "czeka - dzien " + day + " | " + why + " (proba co godzine, tez po nastepnym wczytaniu).");
        }

        /// <summary>Drzewo oddzialow kultury od BasicTroop (bez bohaterow), po tierach 1..6.</summary>
        private static List<CharacterObject>[] Tree(CultureObject cult)
        {
            var res = new List<CharacterObject>[6];
            for (int i = 0; i < 6; i++) res[i] = new List<CharacterObject>();
            if (cult == null || cult.BasicTroop == null) return res;
            var seen = new HashSet<CharacterObject>();
            var stack = new Stack<CharacterObject>();
            stack.Push(cult.BasicTroop);
            while (stack.Count > 0)
            {
                var ch = stack.Pop();
                if (ch == null || !seen.Add(ch) || ch.IsHero) continue;
                int t = Math.Max(1, Math.Min(6, ch.Tier));
                res[t - 1].Add(ch);
                if (ch.UpgradeTargets != null) foreach (var u in ch.UpgradeTargets) stack.Push(u);
            }
            return res;
        }

        private static int Count(List<CharacterObject>[] tree) { int n = 0; foreach (var l in tree) n += l.Count; return n; }

        /// <summary>Plan `n` ludzi: udzialy tierow (brakujace tiery - na pozostale), w tierze po rowno, reszta po najwiekszych ulamkach.</summary>
        private static List<KeyValuePair<CharacterObject, int>> Plan(List<CharacterObject>[] tree, int n)
        {
            var chars = new List<CharacterObject>();
            var want = new List<double>();
            double sw = 0;
            for (int t = 0; t < 6; t++) if (tree[t].Count > 0) sw += TierShare[t];
            if (!(sw > 0)) return new List<KeyValuePair<CharacterObject, int>>();
            for (int t = 0; t < 6; t++)
            {
                if (tree[t].Count == 0) continue;
                double per = n * TierShare[t] / sw / tree[t].Count;
                foreach (var c in tree[t]) { chars.Add(c); want.Add(per); }
            }
            var take = new int[chars.Count];
            int sum = 0;
            for (int j = 0; j < chars.Count; j++) { take[j] = (int)Math.Floor(want[j]); sum += take[j]; }
            for (int rest = n - sum; rest > 0; rest--)
            {
                int best = 0; double bf = -1;
                for (int j = 0; j < chars.Count; j++) { double f = want[j] - take[j]; if (f > bf) { bf = f; best = j; } }
                take[best]++; want[best] = take[best];   // ten juz dostal reszte
            }
            var res = new List<KeyValuePair<CharacterObject, int>>();
            for (int j = 0; j < chars.Count; j++) if (take[j] > 0) res.Add(new KeyValuePair<CharacterObject, int>(chars[j], take[j]));
            return res;
        }

        /// <summary>HourlyTickEvent. Nie rzuca.</summary>
        internal static void Hourly()
        {
            if (_state.Length > 0) return;
            int day = -1;
            try
            {
                var camp = Campaign.Current;
                if (camp == null || !camp.GameStarted) return;
                day = (int)CampaignTime.Now.ToDays;
                var st = Settlement.Find(StId);
                if (st == null || !st.IsFortification || st.Town == null) { Done(day, "brak-osady", "osady " + StId + " nie ma w swiecie - bez daru"); return; }
                // recenzja 555 #1: dar tylko po rzeczywistym zwrocie 544 (zamek oddany bez obsady). 544 czeka - czekamy; inny wynik 544 (juz w koronie,
                // inny pan, gracz, nowa kampania, blad) - zaloga nie byla oprozniona przez 544 (np. starszy zapis sprzed oblezenia, zapis autotestu) - bez daru.
                if (Banefort544.Pending) { Wait(day, "czekam na zwrot 544"); return; }
                if (!Banefort544.ReturnedOk) { Done(day, "bez-zwrotu-544", "544 nie oddal Banefortu w tej kampanii (stan 544: " + Banefort544.StateText + ") - zaloga nie oprozniona przez 544, bez daru"); return; }
                var owner = st.OwnerClan;
                if (owner == null) { Wait(day, "Banefort bez pana"); return; }
                if (owner == Clan.PlayerClan) { Done(day, "gracz", "Banefort nalezy do rodu gracza - bez daru (zaloga gracza to jego decyzja)"); return; }
                var crown = FindKingdom(CrownId);
                bool rightful = owner.StringId == ClanId;
                bool crownRuler = crown != null && !crown.IsEliminated && crown.RulingClan == owner;
                if (!rightful && !crownRuler)
                {
                    Done(day, "inny-pan", "Banefort u " + Nm(owner) + " (" + (owner.Kingdom != null ? Nm(owner.Kingdom) : "-") + "), nie u House Banefort ani rodu panujacego korony - bez daru");
                    return;
                }
                string pwhy;   // recenzja 555 (drobne): ten sam warunek gotowosci co 544 (nie w osadzie, menu, spotkaniu, bitwie, oblezeniu, armii, rozmowie, niewoli)
                if (!BookHoldings.PlayerOnMapFor(out pwhy)) { Wait(day, pwhy ?? "gracz nie na mapie"); return; }
                if (st.Party != null && st.Party.MapEvent != null) { Wait(day, "bitwa w osadzie (MapEvent) - dar po bitwie"); return; }
                var g = st.Town.GarrisonParty;
                if (g != null && g.Party != null && g.Party.MapEvent != null) { Wait(day, "zaloga w bitwie (MapEvent) - dar po bitwie"); return; }

                // kultura: pana, osady, korony - pierwsza z drzewem oddzialow
                CultureObject cult = null; List<CharacterObject>[] tree = null;
                foreach (var c in new[] { owner.Culture, st.Culture, crown != null ? crown.Culture : null })
                {
                    if (c == null || c.IsBandit) continue;
                    var tr = Tree(c);
                    if (Count(tr) > 0) { cult = c; tree = tr; break; }
                }
                if (tree == null) { Done(day, "brak-drzewa", "brak drzewa oddzialow (kultura pana, osady i korony bez BasicTroop) - bez daru"); return; }

                if (g == null) { st.AddGarrisonParty(); g = st.Town.GarrisonParty; }
                if (g == null || g.MemberRoster == null || g.Party == null) { Wait(day, "brak partii zalogi (AddGarrisonParty bez skutku)"); return; }

                int g0 = g.MemberRoster.TotalRegulars;
                int add = Math.Max(0, Target - g0);
                _errors = 0;
                if (add <= 0)
                {
                    Done(day, "pelna", "zaloga " + g0 + " >= cel " + Target + " (" + PreSiegeGarrison + " sprzed oblezenia x 1.5) - bez daru");
                    return;
                }
                var plan = Plan(tree, add);
                var byTier = new int[6];
                var sb = new StringBuilder(600);
                int added = 0, wageAdd = 0;
                foreach (var kv in plan)
                {
                    g.MemberRoster.AddToCounts(kv.Key, kv.Value);
                    added += kv.Value;
                    try { wageAdd += Campaign.Current.Models.PartyWageModel.GetCharacterWage(kv.Key) * kv.Value; } catch { }
                    byTier[Math.Max(1, Math.Min(6, kv.Key.Tier)) - 1] += kv.Value;
                    sb.Append(sb.Length > 0 ? ", " : "").Append(Nm(kv.Key)).Append(" T").Append(kv.Key.Tier).Append(' ').Append(kv.Value);
                }
                try { PeopleBook.GiveNone(g.MemberRoster, added, PeopleBook.FGift555); }   // ksiega ludzi: koszyk bez pochodzenia, nazwana przyczyna "dar zalogi Banefort (555)" (recenzja 555)
                catch (Exception e) { try { Log.Error("Banefort555.PeopleBook", e); } catch { } }
                int g1 = g.MemberRoster.TotalRegulars;

                string limit = "?", wage = "?";
                try { limit = g.Party.PartySizeLimit.ToString(Inv); } catch { }
                try { wage = g.GetAvailableWageBudget().ToString(Inv); } catch { }
                string siege = "nie";
                try { if (st.SiegeEvent != null) siege = "TAK (oblegajacy " + (st.SiegeEvent.BesiegerCamp != null && st.SiegeEvent.BesiegerCamp.LeaderParty != null ? st.SiegeEvent.BesiegerCamp.LeaderParty.Name.ToString() : "?") + ")"; } catch { siege = "TAK"; }
                var tiers = new StringBuilder();
                for (int t = 0; t < 6; t++) tiers.Append(t > 0 ? " " : "").Append('T').Append(t + 1).Append(' ').Append(byTier[t]);

                Done(day, "ok", "DAR 555 zalogi Banefort [" + StId + "] | pan " + Nm(owner) + (rightful ? "" : " (rod panujacy korony)") + " | cel " + Target + " = zaloga sprzed oblezenia "
                     + PreSiegeGarrison + " (OB1 dni 109003-109012) x 1.5 | zaloga " + g0 + " -> " + g1 + " (dodano " + added + ", kultura " + (cult.StringId ?? "?") + ": " + tiers + ") [" + sb
                     + "] | oblezenie " + siege + ", milicja " + (int)st.Town.Militia + " | limit zalogi gry " + limit + ", budzet zoldu zalogi " + wage
                     + ", zold dodanych ok. " + wageAdd + " na dobe (przed perkami; brak zoldu - gra: +5 dezercji zalogi na dobe)"
                     + " (ponad limit: przepelnienie W4c - 25% nadwyzki na dobe do domu, ludnosc BK wsi) | dar 555 - jednorazowy wyjatek od zamknietej ekonomii z rozkazu Jeffa 15.11: ludzie z niczego, bez zlota, "
                     + "bez zdjecia z ludnosci; ksiega ludzi - bez pochodzenia; sprzet - zbrojownia zalogi dokupi braki");
                try { Log.Player("Banefort's garrison is restored: " + added + " fresh troops join the defenders (" + g1 + " men in the castle)."); } catch { }
            }
            catch (Exception e)
            {
                _errors++;
                if (!_errLogged) { _errLogged = true; try { Log.Error("Banefort555.Hourly", e); } catch { } }
                if (_errors >= MaxErrors) { try { Done(day, "blad", MaxErrors + " wyjatki z rzedu - dar porzucony (blad w logu wyzej)"); } catch { _state = "done:?:blad"; } }
            }
        }
    }

    /// <summary>555: jednorazowy dar zalogi dla Banefortu (flaga arm_555_banefort w zapisie).</summary>
    internal sealed class Banefort555Behavior : CampaignBehaviorBase
    {
        public Banefort555Behavior() { Banefort555.Reset(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => Banefort555.OnNewGame());
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => Banefort555.SessionStart());
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, Banefort555.Hourly);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                string v = dataStore.IsSaving ? Banefort555.Export() : null;
                SaveText.Sync(dataStore, "arm_555_banefort", ref v);
                if (dataStore.IsLoading) Banefort555.Import(v);
            }
            catch (Exception e) { Log.Error("SyncData.arm_555_banefort", e); }
        }
    }
}
