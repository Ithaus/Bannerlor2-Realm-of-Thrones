using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// DOROBEK STULECI (wpis 79; Jeff 05.10: "nie zaczynamy gry w prozni - minely setki lat produkcji, sprzet juz powinien byc
    /// wyprodukowany i na targach, a wyglada jakby wszystko bylo puste"; "A + B (14 dni)"). Raz na kampanie, tylko w nowej grze.
    ///
    /// A. Zbrojownie: DTE daje partii sprzet startowy przy jej utworzeniu (OnMobilePartyCreated), ale w nowej grze jego
    ///    OnNewGameCreated czysci PartyArmories PO utworzeniu partii startowych - wszyscy lordowie i garnizony zaczynali z pusta
    ///    zbrojownia, AiGear liczyl cale wojsko jako gole i pierwszego dnia wykupowal 5 296 sztuk z targow (sesja 02:55).
    ///    Teraz: kazda partia lorda AI i garnizon (sklad7: takze garnizon gracza) dostaje do zbrojowni brakujace sztuki kompletu swoich ludzi (to, co juz nosza).
    /// B. Zapas kupiecki: kazde miasto dostaje na polki ColdStartMarketDays dni pracy swoich rzemieslnikow (rece wedle dobrobytu
    ///    x udzialy cechow), sztuki kultury miasta, glownie nizszych tierow; roboczodni na sztuke z Needs (jak w warsztatach).
    /// </summary>
    internal static class ColdStart
    {
        private static bool _done;
        internal static void Reset() { _done = false; ResetLate(); }
        internal static string Export() { return _done ? "done" : ""; }
        internal static void Import(string s) { _done = s == "done"; }

        internal static void Run()
        {
            // E8/T (263): sesja wystartowala (nowa kampania i kazde wczytanie; Run wola OnSessionLaunched) - dopiero od teraz nowa partia lorda to prawdziwe
            // utworzenie, a nie echo wczytania DTE (OnGameLoaded wola OnMobilePartyCreated dla partii bez zbrojowni PRZED startem sesji)
            _ran = true;
            try { SessionLine(); } catch { }
            if (_done) return;
            var s = Settings.Current;
            if (s == null || !s.ColdStartEnabled) return;
            _done = true;
            try
            {
                double age = (CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays;
                if (age > 3.0) { Log.Info("ColdStart: kampania ma juz " + (int)age + " dni - dorobek stuleci pominiety (tylko nowa gra)."); return; }
            }
            catch { }
            try { Armories(); } catch (Exception e) { Log.Error("ColdStart.Armories", e); }
            try { Markets(); } catch (Exception e) { Log.Error("ColdStart.Markets", e); }
            try { int k = RecruitKit.SeedCampaignStart(); Log.Info("ColdStart: ochotnicy tieru 2+ w pulach notabli maja komplety (dorobek stuleci): " + k + "; zapisy sprzed startu zamienione na dorobek " + RecruitKit.SeedConverted + "."); } catch (Exception e) { Log.Error("ColdStart.RecruitKit", e); }
        }

        // ------------------------------------------------------------ A. zbrojownie
        private static void Armories()
        {
            var dict = AiGear.Armories();
            if (dict == null) { Log.Info("ColdStart: zbrojownie DTE niedostepne - pominiete."); return; }
            int parties = 0, pieces = 0, garrisons = 0;
            foreach (var mp in MobileParty.All)
            {
                if (mp == null || !mp.IsActive || mp.IsMainParty || mp.MemberRoster == null) continue;
                bool garrison = mp.IsGarrison;
                if (!mp.IsLordParty && !garrison) continue;
                // sklad7 (scalenie K1, jedna regula gracz/AI): zaloga gracza tez dostaje dorobek - od K1 jest w systemie (sakiewka z zoldu) i walczy tylko tym,
                // co ma w zbrojowni (GarrisonFightsWithArmoryOnly); dotad pomijana, bo poza systemem DTE kasowal jej zbrojownie co dobe. Nowa gra zwykle
                // nie daje graczowi twierdzy - dotyczy startow z lennem.
                int added = FillToTemplate(mp, dict);
                if (!garrison) NoteLateLeader(mp);   // E8/T (263): wodz partii sprzed startu ma juz dorobek - jego partia odtworzona po rozbiciu nie dostaje drugiego
                if (added > 0) { parties++; pieces += added; if (garrison) garrisons++; }
            }
            Log.Info("ColdStart: zbrojownie - " + parties + " partii (w tym garnizonow " + garrisons + ") dostalo " + pieces + " szt. kompletu swoich ludzi (dorobek stuleci, nie z targu).");
        }

        /// <summary>171 C9a: brakujace sztuki kompletu ludzi partii (wzorce, koszyki typ x tier jak AiGear, bez koni - sklad7b-p: z konmi i rzedami przy
        /// TroopsFightWithOwnKitOnly) do jej zbrojowni DTE;
        /// zwraca dolozone sztuki. Wydzielone z Armories() bez zmian - ColdStart i odtworzenie zalog po starym zapisie (GarrisonArmory).</summary>
        internal static int FillToTemplate(MobileParty mp)
        {
            var dict = AiGear.Armories();
            return dict != null && mp != null && mp.MemberRoster != null ? FillToTemplate(mp, dict) : 0;
        }

        private static int FillToTemplate(MobileParty mp, Dictionary<MBGUID, Dictionary<ItemObject, int>> dict)
        {
                // braki w koszykach typ x tier (jak AiGear)
                // sklad7b-p (uwagi 3 i 11): przy TroopsFightWithOwnKitOnly kon jest sztuka zbrojowni (jezdziec bez konia w zbrojowni idzie pieszo) -
                // dorobek obejmuje takze konia (slot 10) i rzad (11, tylko z koniem) jezdzcow; wylaczone - jak dotad, bez koni (Stajnia)
                int last = GarrisonKit.OwnKitOn ? 12 : 10;
                var need = new Dictionary<int, int>();
                var roster = mp.MemberRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var ch = el.Character;
                    if (ch == null || ch.IsHero || el.Number <= 0) continue;
                    // PLAN 1.6 (recenzja 14.10, GoldenKits): dorobek wedlug tej samej potrzeby co AiGear - kusznicy ZK przy Army175GoldenBows ze wszystkich
                    // zestawow, ludzie po rowno (1/3 kusze, 2/3 luki); inni - 1. zestaw (kn = 1, jak dotad). Bez tego nowa partia ZK dostawala kusze dla
                    // wszystkich, a AI sprzedawalo 2/3 z nich i kupowalo luki (fala kup-sprzedaj w nowej kampanii i u nowych wodzow jej pierwszych dob)
                    int kn = GoldenKits.Count(ch);
                    for (int q = 0; q < kn; q++)
                    {
                        Equipment eq = GoldenKits.Kit(ch, q, kn);
                        int num = GoldenKits.Men(el.Number, q, kn);
                        if (eq == null || num <= 0) continue;
                        for (int sl = 0; sl < last; sl++)
                        {
                            var it = eq[(EquipmentIndex)sl].Item;
                            if (it == null || !SupplyDemand.Equipmentish(it)) continue;
                            if (sl == 11 && eq[EquipmentIndex.Horse].Item == null) continue;
                            int k = AiGear.Bucket(it); int n; need.TryGetValue(k, out n); need[k] = n + num;
                        }
                    }
                }
                Dictionary<ItemObject, int> arm;
                if (dict.TryGetValue(mp.Id, out arm) && arm != null)
                    foreach (var kv in arm)
                    {
                        if (kv.Key == null || kv.Value <= 0) continue;
                        int k = AiGear.Bucket(kv.Key); int n; if (need.TryGetValue(k, out n)) need[k] = n - kv.Value;
                    }
                int added = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var ch = el.Character;
                    if (ch == null || ch.IsHero || el.Number <= 0) continue;
                    int kn = GoldenKits.Count(ch);   // PLAN 1.6 (recenzja 14.10): jak wyzej - zestawy kusznikow ZK po rowno, inni 1. zestaw
                    for (int q = 0; q < kn; q++)
                    {
                        Equipment eq = GoldenKits.Kit(ch, q, kn);
                        int num = GoldenKits.Men(el.Number, q, kn);
                        if (eq == null || num <= 0) continue;
                        for (int sl = 0; sl < last; sl++)
                        {
                            var it = eq[(EquipmentIndex)sl].Item;
                            if (it == null || !SupplyDemand.Equipmentish(it)) continue;
                            if (sl == 11 && eq[EquipmentIndex.Horse].Item == null) continue;
                            int k = AiGear.Bucket(it); int d;
                            if (!need.TryGetValue(k, out d) || d <= 0) continue;
                            int n = Math.Min(d, num);
                            if (AiGear.AddToArmory(mp, it, n)) { need[k] = d - n; added += n; }
                        }
                    }
                }
                return added;
        }

        // ------------------------------------------------------------ B. zapas kupiecki
        private static string GuildOfCategory(ItemObject it)
        {
            string id = it.ItemCategory != null ? it.ItemCategory.StringId : "";
            if (id == "garment") return "krawiec";
            if (id.EndsWith("_armor")) return "platnerz";
            if (id.StartsWith("melee_weapons")) return "miecznik";
            if (id == "arrows" || id.StartsWith("ranged_weapons")) return "lucznik";
            if (id.StartsWith("shield")) return "tarczownik";
            if (id.StartsWith("horse_equipment")) return "siodlarz";
            return null;
        }

        private static void Markets()
        {
            var s = Settings.Current;
            float days = Math.Max(0f, s.ColdStartMarketDays);
            if (days <= 0f) return;
            // pula wyrobow cechow: kultura -> cech -> tier -> sztuki
            var pool = new Dictionary<string, Dictionary<string, List<ItemObject>[]>>();
            // E6/M1 (207): dorobek startowy wedlug lancucha krainy miasta (pula "@kraina", bez neutralnej doklejki - neutralne sa w lancuchu na poziomie 4)
            bool m1 = CraftRealm.Active;
            var chainAll = m1 ? new List<KeyValuePair<ItemObject, string>>() : null;
            foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                if (it == null || it.NotMerchandise || !SupplyDemand.Equipmentish(it)) continue;
                if (ArmsPricing.IsUnique(it) || LegendaryLaw.IsLegend(it) || WorkshopLaw.Forbidden(it)) continue;
                if (WorkshopLaw.ChooseByShortage && WorkshopLaw.CivilianOnly(it)) continue;   // 174.1: stroj mieszczan bez zolnierza (ladys_shoe) nie jest dorobkiem cechow zbrojnych
                string g = GuildOfCategory(it); if (g == null) continue;
                if (m1) { chainAll.Add(new KeyValuePair<ItemObject, string>(it, g)); continue; }
                string c = it.Culture == null || it.Culture.StringId == "neutral_culture" ? "" : it.Culture.StringId;
                Dictionary<string, List<ItemObject>[]> byG;
                if (!pool.TryGetValue(c, out byG)) pool[c] = byG = new Dictionary<string, List<ItemObject>[]>();
                List<ItemObject>[] byT;
                if (!byG.TryGetValue(g, out byT)) { byT = new List<ItemObject>[6]; for (int i = 0; i < 6; i++) byT[i] = new List<ItemObject>(); byG[g] = byT; }
                byT[Math.Max(0, Math.Min(5, (int)it.Tier))].Add(it);
            }
            float[] tierW = { 0.35f, 0.30f, 0.20f, 0.10f, 0.04f, 0.01f };   // tier 1..6: targ to glownie sprzet zwykly
            string[] guilds = { "krawiec", "platnerz", "miecznik", "siodlarz", "lucznik", "tarczownik" };
            float wsum = 0f; foreach (var g in guilds) wsum += WorkshopLaw.GuildWeight(g);
            if (wsum <= 0f) return;
            int towns = 0, pieces = 0; long worth = 0;
            // E8/T (263, PLAN 8.10a): budzet cechow - dany / przepadly (zuzyty = dany - przepadly) w roboczodniach, sztuki i sztuki znalezione wyzej niz wylosowany tier
            bool tierUp = s.ColdStartTierUp;
            var bGiven = new double[guilds.Length]; var bLost = new double[guilds.Length]; var bPieces = new int[guilds.Length]; var bUp = new int[guilds.Length];
            double lostDraw = 0, lostNone = 0, lostGuard = 0; int cellsDraw = 0, cellsNone = 0, cellsGuard = 0;
            var byType = new Dictionary<ItemObject.ItemTypeEnum, int>();
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsTown || st.Town == null || st.ItemRoster == null) continue;
                float hands = Settings.Current.ColdStartLegacyHands ? WorkshopLaw.LegacyTownHands(st.Town) : WorkshopLaw.TownHands(st.Town);   // 174.3: wiecej rzemieslnikow to nie wiecej zapasu z niczego
                string cul = st.Culture != null ? st.Culture.StringId : "";
                if (m1)
                {
                    string realm = CraftRealm.RealmOfTown(st.Town);
                    cul = "@" + realm;
                    if (!pool.ContainsKey(cul))
                    {
                        var byG0 = new Dictionary<string, List<ItemObject>[]>();
                        foreach (var kv in chainAll)
                        {
                            if (!CraftRealm.Allowed(realm, kv.Key)) continue;
                            List<ItemObject>[] byT0;
                            if (!byG0.TryGetValue(kv.Value, out byT0)) { byT0 = new List<ItemObject>[6]; for (int i = 0; i < 6; i++) byT0[i] = new List<ItemObject>(); byG0[kv.Value] = byT0; }
                            byT0[Math.Max(0, Math.Min(5, (int)kv.Key.Tier))].Add(kv.Key);
                        }
                        pool[cul] = byG0;
                    }
                }
                int made = 0;
                int gi = -1;
                foreach (var g in guilds)
                {
                    gi++;
                    float budget = days * hands * WorkshopLaw.GuildWeight(g) / wsum;   // roboczodni cechu
                    List<ItemObject>[] byT = null;
                    Dictionary<string, List<ItemObject>[]> byG;
                    if (pool.TryGetValue(cul, out byG)) byG.TryGetValue(g, out byT);
                    List<ItemObject>[] neutral = null;
                    if (pool.TryGetValue("", out byG)) byG.TryGetValue(g, out neutral);
                    if (budget > 0f) bGiven[gi] += budget;
                    bool anyPiece = false;   // E8/T: cech ma w tym miescie jakakolwiek sztuke (kultury / krainy miasta albo neutralna)
                    for (int tt = 0; tt < 6 && !anyPiece; tt++) anyPiece = (byT != null && byT[tt].Count > 0) || (neutral != null && neutral[tt].Count > 0);
                    int guard = 0;
                    while (budget > 0f && guard++ < 2000)
                    {
                        // tier wedle wag; sztuka kultury miasta, inaczej neutralna
                        float r = MBRandom.RandomFloat, acc = 0f; int t = 0;
                        for (; t < 5; t++) { acc += tierW[t]; if (r < acc) break; }
                        List<ItemObject> l = null;
                        for (int tt = t; tt >= 0 && (l == null || l.Count == 0); tt--)
                            l = byT != null && byT[tt].Count > 0 ? byT[tt] : neutral != null && neutral[tt].Count > 0 ? neutral[tt] : null;
                        // E8/T (263, PLAN 8.10a): los tieru, ponizej ktorego cech nic nie robi (platnerz, tarczownik bez sztuk tieru 1) - dotad koniec petli i reszta
                        // budzetu cechu przepadala; teraz po tierach w dol szukamy w gore (t+1..5), koniec dopiero, gdy cech nie ma zadnej sztuki
                        if ((l == null || l.Count == 0) && tierUp)
                        {
                            for (int tt = t + 1; tt <= 5 && (l == null || l.Count == 0); tt++)
                                l = byT != null && byT[tt].Count > 0 ? byT[tt] : neutral != null && neutral[tt].Count > 0 ? neutral[tt] : null;
                            if (l != null && l.Count > 0) bUp[gi]++;
                        }
                        if (l == null || l.Count == 0) break;
                        var it = l[MBRandom.RandomInt(l.Count)];
                        float d; var nd = WorkshopLaw.Needs(it, out d);
                        if (nd == null) d = 1f;
                        budget -= Math.Max(0.05f, d);
                        st.ItemRoster.AddToCounts(it, 1);
                        made++; worth += it.Value;
                        int n; byType.TryGetValue(it.ItemType, out n); byType[it.ItemType] = n + 1;
                        bPieces[gi]++;
                    }
                    if (budget > 0f)
                    {
                        // E8/T: reszta budzetu cechu, ktorej nie zamieniono na sztuki - wedlug przyczyny (cech bez zadnej sztuki / los tieru - stara regula / straznik petli)
                        bLost[gi] += budget;
                        if (!anyPiece) { lostNone += budget; cellsNone++; }
                        else if (guard > 2000) { lostGuard += budget; cellsGuard++; }
                        else { lostDraw += budget; cellsDraw++; }
                    }
                }
                if (made > 0) { towns++; pieces += made; }
            }
            var parts = new List<string>(); foreach (var kv in byType) parts.Add(kv.Key + " " + kv.Value);
            Log.Info("ColdStart: zapas kupiecki - " + towns + " miast dostalo " + pieces + " szt. (" + days + " dni pracy rzemieslnikow, wartosc " + worth + " d) [" + string.Join(", ", parts.ToArray()) + "].");
            // E8/T (263): linia budzetu cechow (prog T-2 narzedzia: przepadly 0)
            double given = 0, lost = 0; int up = 0;
            var gp = new List<string>();
            for (int i = 0; i < guilds.Length; i++)
            {
                given += bGiven[i]; lost += bLost[i]; up += bUp[i];
                gp.Add(guilds[i] + " dany " + R0(bGiven[i]) + " / zuzyty " + R0(bGiven[i] - bLost[i]) + " / przepadly " + R0(bLost[i]) + " / szt. " + bPieces[i] + " / z wyzszego tieru " + bUp[i]);
            }
            Log.Info("ColdStart: budzet cechow (E8/T) - dany " + R0(given) + " roboczodni, zuzyty " + R0(given - lost) + ", przepadly " + R0(lost)
                     + " (los tieru bez sztuki nizej - stara regula " + R0(lostDraw) + " w " + cellsDraw + " miasto-cechach; cech bez zadnej sztuki " + R0(lostNone) + " w " + cellsNone
                     + "; straznik petli " + R0(lostGuard) + " w " + cellsGuard + "); szukanie wyzszego tieru (Cold Start Tier Up " + (tierUp ? "CZYNNE" : "WYLACZONE") + "): " + up
                     + " szt.; cechy [" + string.Join("; ", gp.ToArray()) + "].");
        }

        private static string R0(double v) { return ((long)Math.Round(v)).ToString(); }

        // ------------------------------------------------------------ E8/T (263, PLAN 8.10c): dorobek partii lordow z pierwszych dob kampanii
        // Armories() widzi tylko partie stojace na mapie przy starcie sesji (Z33: 34 partie lordow). Pozostale gra tworzy po starcie z ludzmi z szablonu klanu
        // (Z33: 276 partii w dobie 1, potem 23 i 38), a LevyGold.PartyCreatedPrefix blokuje im komplet DTE: ludzie sa, ich dorobku stuleci nie ma i pierwsze doby
        // to fala zakupow (2 451 / 6 123 / 5 037 szt.). Teraz nowa partia lorda AI w oknie nowej gry (te same 3 doby co Run) dostaje ten sam dorobek co partie
        // sprzed startu - RAZ na wodza: partia odtworzona po rozbiciu w tych dobach nie dostaje drugiego (wodzowie w zapisie arm_e8_coldlate do konca okna).
        private const double LateDays = 3.0;
        private static bool _ran;
        private static readonly HashSet<string> _lateLeaders = new HashSet<string>();
        private static int _dLateParties, _dLatePieces, _dLateEmpty, _dLateAgain, _dLateNoLeader, _dLateOff, _lateStumbles, _lateErrDay = -1;
        private static int _dLateUndead, _dLatePlayer;   // recenzja E8/T: partie lordow celowo bez dorobku w oknie nowej gry (umarli, rod gracza) - linia doby i prog T-3
        private static long _tLateParties, _tLatePieces;

        internal static bool LateOn { get { var s = Settings.Current; return s != null && s.ColdStartEnabled && s.ColdStartLateLordParties; } }

        private static void ResetLate()
        {
            _ran = false; _lateLeaders.Clear();
            _dLateParties = _dLatePieces = _dLateEmpty = _dLateAgain = _dLateNoLeader = _dLateOff = 0; _lateStumbles = 0; _lateErrDay = -1;
            _dLateUndead = _dLatePlayer = 0;
            _tLateParties = _tLatePieces = 0;
        }

        /// <summary>Wyjatek przy jednej partii: liczony w linii "Dorobek startowy (E8/T)", Log.Error najwyzej raz na dobe; nic nie gasnie.</summary>
        private static void LateStumble(string where, Exception e)
        {
            _lateStumbles++;
            try { int d = (int)CampaignTime.Now.ToDays; if (d != _lateErrDay) { _lateErrDay = d; Log.Error("ColdStart." + where, e); } } catch { }
        }

        private static string LeaderKey(MobileParty mp)
        {
            Hero h = null;
            try { h = mp.LeaderHero ?? mp.Owner; } catch { }
            return h != null ? h.StringId : null;
        }

        private static void NoteLateLeader(MobileParty mp)
        {
            try { var k = LeaderKey(mp); if (!string.IsNullOrEmpty(k)) _lateLeaders.Add(k); } catch { }
        }

        /// <summary>
        /// Z LevyGold.PartyCreatedPrefix (prefiks DTE OnMobilePartyCreated, po starcie gry; sklad z szablonu klanu jest juz w partii - gra wypelnia go w
        /// LordPartyComponent przed zdarzeniem): nowa partia lorda AI w pierwszych dobach nowej kampanii dostaje dorobek (FillToTemplate - regula Armories).
        /// true = dostala sztuki (nie liczy sie jako "bez kompletu"). Nie rzuca.
        /// </summary>
        internal static bool LateLord(MobileParty mp)
        {
            try
            {
                if (mp == null || !mp.IsLordParty || mp.IsMainParty || mp.MemberRoster == null) return false;
                if (!_ran || !_done) return false;                       // przed startem sesji (echo wczytania DTE) albo dorobek stuleci nie biegl w tej kampanii
                if (Hunger.CampaignAge() > LateDays) return false;       // po oknie nowej gry - lord kupuje na targu jak dotad
                // recenzja E8/T: dwie odmowy celowe - liczone (linia doby), zeby prog T-3 nie bral ich za blad
                if (mp.ActualClan == Clan.PlayerClan) { _dLatePlayer++; return false; }   // druzyna rodu gracza - nie "partia lorda AI"
                // umarli (Nocny Krol): sprzet umarlych jest poza gospodarka - DTE dopelnia im wzorzec w bitwie i tak (GarrisonKit.DteDresses, PartyTakesKit;
                // ta sama metoda FillToTemplate w GarrisonArmory C9a tez ich pomija). Dorobek w zbrojowni DTE bylby sprzetem z niczego do wziecia po rozbiciu partii.
                if (Undead.Party(mp)) { _dLateUndead++; return false; }
                if (!LateOn) { _dLateOff++; return false; }
                var key = LeaderKey(mp);
                if (string.IsNullOrEmpty(key)) { _dLateNoLeader++; return false; }
                if (!_lateLeaders.Add(key)) { _dLateAgain++; return false; }
                int added = FillToTemplate(mp);
                if (added <= 0) { _dLateEmpty++; return false; }
                _dLateParties++; _dLatePieces += added;
                _tLateParties++; _tLatePieces += added;
                return true;
            }
            catch (Exception e) { LateStumble("LateLord", e); return false; }
        }

        /// <summary>Z LevyGold.Tick przy zmianie doby (przed zerowaniem licznikow werbunku): linia doby w oknie nowej gry i dwie doby po nim albo gdy cos policzono.</summary>
        internal static void LateFlush(int day, int skippedAll, int skippedLords)
        {
            try
            {
                double age = Hunger.CampaignAge();
                int any = _dLateParties + _dLateEmpty + _dLateAgain + _dLateNoLeader + _dLateOff + _dLateUndead + _dLatePlayer;
                if (_done && (any > 0 || age <= LateDays + 2.0))
                    Log.Info("Dorobek startowy (E8/T): dzien " + day + " - nowe partie lordow AI z dorobkiem " + _dLateParties + " (szt. " + _dLatePieces + ")"
                             + ", bez dorobku: bez ludzi albo bez brakow " + _dLateEmpty + ", drugi raz ten sam wodz " + _dLateAgain + ", bez wodza " + _dLateNoLeader + ", klucz wylaczony " + _dLateOff
                             + ", umarli (sprzet poza gospodarka) " + _dLateUndead + ", rod gracza " + _dLatePlayer
                             + "; nowe partie AI bez kompletu DTE i bez dorobku " + skippedAll + " (w tym partie lordow " + skippedLords + ")"
                             + "; od startu sesji: partii z dorobkiem " + _tLateParties + ", szt. " + _tLatePieces + "; wodzow z dorobkiem w pamieci " + _lateLeaders.Count
                             + "; potkniecia " + _lateStumbles + " (Cold Start Late Lord Parties " + (LateOn ? "CZYNNE" : "WYLACZONE") + ").");
                _dLateParties = _dLatePieces = _dLateEmpty = _dLateAgain = _dLateNoLeader = _dLateOff = 0; _dLateUndead = _dLatePlayer = 0;
                if (age > LateDays && _lateLeaders.Count > 0) _lateLeaders.Clear();   // po oknie pamiec wodzow niepotrzebna (sekcja zapisu pusta)
            }
            catch (Exception e) { LateStumble("LateFlush", e); }
        }

        /// <summary>Linia sesji (nowa kampania i kazde wczytanie): stan trzech kluczy paczki E8/T i latek rekruta gracza - dla narzedzia (T-K).</summary>
        private static void SessionLine()
        {
            var s = Settings.Current;
            if (s == null) return;
            Log.Info("Start kampanii (E8/T): sesja - Cold Start Tier Up " + (s.ColdStartTierUp ? "CZYNNE" : "WYLACZONE") + ", Cold Start Late Lord Parties " + (s.ColdStartLateLordParties ? "CZYNNE" : "WYLACZONE")
                     + ", Player Recruit No Free Kit " + (s.PlayerRecruitNoFreeKit ? "CZYNNE" : "WYLACZONE") + " (" + PlayerRecruit.State() + ")"
                     + "; dorobek stuleci " + (!s.ColdStartEnabled ? "wylaczony" : _done ? "zrobiony wczesniej w tej kampanii (zapis)" : "przed pierwszym biegiem (nowa gra albo kampania starsza niz 3 doby)")
                     + "; wiek kampanii " + R0(Hunger.CampaignAge()) + " dob, wodzow z dorobkiem z zapisu " + _lateLeaders.Count + ".");
        }

        /// <summary>Zapis arm_e8_coldlate: "1|id,id,..." - wodzowie partii z dorobkiem w oknie nowej gry (po oknie pusty). Stary zapis bez sekcji = pusty zbior.</summary>
        internal static string ExportLate()
        {
            if (_lateLeaders.Count == 0) return "";
            return "1|" + string.Join(",", new List<string>(_lateLeaders).ToArray());
        }

        internal static void ImportLate(string s)
        {
            _lateLeaders.Clear();
            if (string.IsNullOrEmpty(s) || !s.StartsWith("1|")) return;
            foreach (var id in s.Substring(2).Split(',')) if (id.Length > 0) _lateLeaders.Add(id);
        }
    }
}
