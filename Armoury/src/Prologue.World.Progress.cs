using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// m104-e5 pkt 1 - ORSZAK KROLA ROBERTA (klucz PrologueRoyalProgress; projekt pkt 2: dzien 0 wyjazd, 35 Winterfell, 55 powrot z Nedem, Sansa i Arya, 95 Krolewska
    /// Przystan; dzien 55 Tyrion na Mur, okolo 100 przez rozstaje). Bez armii, bez nowych partii i bez ludzi z niczego (rozpoznanie e5:A): krol jedzie swoja druzyna
    /// lorda, Jaime i Tyrion (a od Winterfell Ned) eskortuja go swoimi druzynami (SetMoveEscortParty), dorosli z rodu krola bez wlasnej druzyny (krolowa Cersei, Ogar,
    /// Joffrey bez druzyny) jada W DRUZYNIE KROLA (TeleportHeroAction do partii - ten sam rod, jak malzonek w druzynie gracza; namiestnik osady zostaje), dzieci
    /// (ponizej AgeModel.HeroComesOfAge - nie moga byc w druzynie) przenoszone do celu przy przyjezdzie orszaku (Myrcella i Tommen do Winterfell i z powrotem,
    /// Sansa i Arya do Krolewskiej Przystani), tylko gdy czynne, wolne i bez druzyny.
    /// TRASA (postoje tylko w miastach i wsiach - gra kupuje tam jedzenie co godzine: PartiesBuyFoodCampaignBehavior.HourlyTickParty): Krolewska Przystan (d0) ->
    /// gospoda na rozstajach (od d1) -> Blizniaki (od d10) -> Winterfell (od d28, przyjazd ok. d31-34) ... postoj do d55 -> Blizniaki -> gospoda (od d70) ->
    /// Krolewska Przystan (od d88, przyjazd ok. d90-93) = koniec, wszyscy wracaja do swojego AI. Odcinek bez dojazdu LegLateDays dni po terminie nastepnego - dalej.
    /// Partie orszaku ukryte przed AI (bandy i lordowie ich nie zaczepiaja; gracz moze - trasa i tak idzie dalej, krol nie ginie: CanHeroDie). Krol jencem / bez
    /// druzyny - orszak przerwany (linia), dosiadajacy zostaja tam, gdzie gra ich zostawi.
    /// NED (od d50): jego druzyna jedzie do Winterfell, od wyjazdu orszaku (d55) eskortuje krola do konca trasy. TYRION (d55): odlacza sie na Mur (Czarny Zamek),
    /// postoj do d96, potem do gospody na rozstajach i czeka tam do d109 (#p01 w dniu 110 bierze go stamtad do Orlego Gniazda). Bez druzyny - bez drogi (linia).
    /// Zapis sprzed E5 (wczytany w polowie prologu) - orszak i drogi, ktorych czas minal, pominiete (bez nadrabiania).
    /// </summary>
    internal static partial class Prologue
    {
        private const string PScene = "orszak", NScene = "Ned", TScene = "Tyrion";
        private const string WinterfellId = "town_B2", TwinsId = "ROT_town3", InnId = "village_EN3_1", CastleBlackId = "town_B5";
        private const string CerseiId = "lord_4_2", MyrcellaId = "lord_4_10", TommenId = "lord_4_7", SansaId = "lord_5_13_1", AryaId = "lord_5_1_1", SandorTpl = "ROTwanderer3";
        private static readonly string[] LegTo = { KingsLandingId, InnId, TwinsId, WinterfellId, TwinsId, InnId, KingsLandingId };
        private static readonly int[] LegFrom = { 0, 1, 10, 28, 55, 70, 88 };     // dzien prologu, od ktorego orszak rusza do celu odcinka
        private const int LegWinterfell = 3, LegHome = 4, LegCount = 7;           // indeksy odcinkow (od 0)
        private const int LegLateDays = 15;
        private const int ProgressStartLate = 3;     // krol bez druzyny do tego dnia prologu - orszak pominiety
        private const int NedRideFrom = 50;
        private const int TyrionWallFrom = 55, TyrionWallLeave = 96, TyrionInnUntil = 109;

        // stan (zapis): orszak -1 pominiety/przerwany, 0 przed, 1..7 odcinek (indeks + 1), 8 koniec; Ned -1 pominiety, 0 przed, 1 do Winterfell, 2 eskorta, 3 koniec;
        // Tyrion -1 pominiety, 0 w orszaku, 1 na Mur, 2 w Czarnym Zamku, 3 do gospody, 4 w gospodzie, 5 koniec
        private static int _rpPhase, _nedPhase, _tyPhase, _rpLegSince;
        private static double _rpArrW = -1, _rpArrKL = -1, _tyArrCB = -1, _tyArrInn = -1;
        private static readonly List<string> _rpRiders = new List<string>();
        private static string _rpWhy = "", _nedWhy = "", _tyWhy = "";

        private static void ResetProgress()
        {
            _rpPhase = _nedPhase = _tyPhase = _rpLegSince = 0;
            _rpArrW = _rpArrKL = _tyArrCB = _tyArrInn = -1;
            _rpRiders.Clear(); _rpWhy = _nedWhy = _tyWhy = "";
        }

        private static string D(double x) { return x.ToString("0.0", Inv); }

        private static string PackProgress()
        {
            return "wrp=" + _rpPhase.ToString(Inv) + "," + _rpLegSince.ToString(Inv) + "," + D(_rpArrW) + "," + D(_rpArrKL) + ";wrn=" + _nedPhase.ToString(Inv)
                   + ";wrt=" + _tyPhase.ToString(Inv) + "," + D(_tyArrCB) + "," + D(_tyArrInn) + ";wrr=" + Ids(_rpRiders) + ";";
        }

        private static double Dbl(string[] a, int i, double def)
        {
            double r;
            return a != null && i < a.Length && double.TryParse(a[i], NumberStyles.Float, Inv, out r) ? r : def;
        }

        private static int Int(string[] a, int i, int def)
        {
            int r;
            return a != null && i < a.Length && int.TryParse(a[i], NumberStyles.Integer, Inv, out r) ? r : def;
        }

        private static void UnpackProgress(Dictionary<string, string> map)
        {
            ResetProgress();
            string v;
            if (map.TryGetValue("wrp", out v)) { var a = v.Split(','); _rpPhase = Int(a, 0, 0); _rpLegSince = Int(a, 1, 0); _rpArrW = Dbl(a, 2, -1); _rpArrKL = Dbl(a, 3, -1); }
            if (map.TryGetValue("wrn", out v)) _nedPhase = Int(v.Split(','), 0, 0);
            if (map.TryGetValue("wrt", out v)) { var a = v.Split(','); _tyPhase = Int(a, 0, 0); _tyArrCB = Dbl(a, 1, -1); _tyArrInn = Dbl(a, 2, -1); }
            _rpRiders.AddRange(IdList(map, "wrr"));
        }

        // ---------------------------------------------------------------- krok (co godzine)

        private static void ProgressStep(double day, bool daily)
        {
            var s = S;
            if (s == null || !s.PrologueRoyalProgress) return;   // klucz wylaczony - partie orszaku puszczone (WorldStep), stan zostaje
            if (day >= KingFallsDay)
            {
                // e5:A: blokady zdjete przed smiercia krola i aresztem Neda - orszak, Ned i Tyrion, ktore jeszcze trwaja (zatrzymane w drodze), koncza sie
                if (_rpPhase >= 1 && _rpPhase <= LegCount) ColumnSkip("koniec prologu (dzien " + KingFallsDay + ") - orszak nie dojechal", day);
                if (_nedPhase == 1 || _nedPhase == 2) { _nedPhase = 3; Log.Info(WTag + "Ned: koniec eskorty - koniec prologu (dzien prologu " + D(day) + ")."); }
                if (_tyPhase >= 1 && _tyPhase <= 4) TyrionEnd(5, "koniec prologu", day);
                return;
            }
            ColumnStep(day);
            NedStep(day);
            TyrionStep(day);
        }

        private static void ColumnSkip(string why, double day)
        {
            _rpPhase = -1; _rpWhy = why;
            Log.Info(WTag + "orszak POMINIETY albo PRZERWANY - " + why + " (dzien prologu " + D(day) + ").");
        }

        private static void ColumnStep(double day)
        {
            if (_rpPhase < 0 || _rpPhase > LegCount) return;
            var robert = H(RobertId);
            if (_rpPhase == 0)
            {
                if (_wLegacy && day > 1.0) { ColumnSkip("zapis sprzed E5 - orszak nie rusza w polowie drogi", day); return; }
                if (!_kingsAlive || robert == null || !robert.IsAlive) { ColumnSkip("krol Robert nie ozywiony albo martwy", day); return; }
                var r0 = Led(robert);
                if (r0 == null) { if (day >= ProgressStartLate) ColumnSkip("krol bez wlasnej druzyny do dnia " + ProgressStartLate, day); return; }
                _rpPhase = 1; _rpLegSince = (int)day;
                string riders = Mount(robert, r0);
                Log.Info(WTag + "orszak RUSZA (dzien prologu " + D(day) + ") | krol " + Who(robert) + " druzyna " + r0.StringId + " (" + r0.MemberRoster.TotalManCount + " ludzi), "
                         + Pos(r0, null) + " | cel " + LegTo[0] + " | eskorta: " + EscortText(r0) + " | w druzynie krola: " + riders + ".");
            }
            var rob = Led(robert);
            if (rob == null)
            {
                ColumnSkip("krol " + (robert == null || !robert.IsAlive ? "martwy" : robert.IsPrisoner ? "jencem" : "bez druzyny") + " w odcinku " + _rpPhase + "/" + LegCount, day);
                _rpRiders.Clear();
                return;
            }
            int leg = _rpPhase - 1;
            var target = Settlement.Find(LegTo[leg]);
            if (target == null) { ColumnSkip("brak osady " + LegTo[leg], day); return; }
            if (At(rob, target))
            {
                if (leg == LegCount - 1) { ColumnEnd(robert, rob, day); return; }
                if (leg == LegWinterfell && _rpArrW < 0)
                {
                    _rpArrW = day;
                    string kids = MoveKids(new[] { MyrcellaId, TommenId }, target);
                    Log.Info(WTag + "orszak w WINTERFELL (dzien prologu " + D(day) + ") | eskorta: " + EscortText(rob) + " | w druzynie krola: " + RidersText(rob)
                             + " | dzieci krola: " + kids + ".");
                }
                if (day >= LegFrom[leg + 1]) NextLeg(rob, day, "z " + target.StringId);
            }
            else if (leg + 1 < LegCount && day >= LegFrom[leg + 1] + LegLateDays) NextLeg(rob, day, "SPOZNIONY - bez dojazdu do " + target.StringId + " (" + Pos(rob, target) + ")");
            leg = _rpPhase - 1;
            target = Settlement.Find(LegTo[leg]);
            Control(PScene, rob, KGo, target, null, true);
            foreach (var h in EscortHeroes(day))
            {
                var ep = Led(h);
                if (ep != null && ep != rob) Escort(PScene, ep, rob);
            }
        }

        /// <summary>Eskorta krola: w drodze - SetMoveEscortParty; krol w osadzie (postoj) - eskorta wjezdza do tej osady (eskorta gry czeka pod brama
        /// - MobileParty.ComputeIsWaiting - i nie kupuje jedzenia; w miescie i wsi gra kupuje je co godzine).</summary>
        private static void Escort(string scene, MobileParty ep, MobileParty rob)
        {
            var at = rob.CurrentSettlement;
            if (at != null && (at.IsTown || at.IsVillage || at.IsCastle)) Control(scene, ep, KGo, at, null, true);
            else Control(scene, ep, KEscort, null, rob, true);
        }

        private static void NextLeg(MobileParty rob, double day, string why)
        {
            _rpPhase++; _rpLegSince = (int)day;
            Log.Info(WTag + "orszak: odcinek " + _rpPhase + "/" + LegCount + " -> " + LegTo[_rpPhase - 1] + " (" + why + ", dzien prologu " + D(day) + ") | krol " + Pos(rob, null)
                     + " | eskorta: " + EscortText(rob) + ".");
        }

        /// <summary>Kto eskortuje krola swoja druzyna: Jaime, Tyrion (do dnia 55), Ned (od wyjazdu z Winterfell), krolowa i Joffrey, gdy maja druzyne.</summary>
        private static List<Hero> EscortHeroes(double day)
        {
            var l = new List<Hero>();
            var j = Tpl(JaimeTpl); if (j != null) l.Add(j);
            if (_tyPhase == 0 && day < TyrionWallFrom) { var t = Tpl(TyrionTpl); if (t != null) l.Add(t); }
            if (_nedPhase == 2) { var n = H(NedId); if (n != null) l.Add(n); }
            foreach (var id in new[] { CerseiId, JoffreyId }) { var h = H(id); if (h != null && Led(h) != null) l.Add(h); }
            return l;
        }

        private static string EscortText(MobileParty rob)
        {
            var sb = new StringBuilder();
            foreach (var h in EscortHeroes(PrologueDay()))
            {
                var ep = Led(h);
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(WhoS(h)).Append(' ').Append(ep == null ? "(bez druzyny: " + WhereS(h) + ")" : ep == rob ? "(w druzynie krola)" : ep.CurrentSettlement != null && ep.CurrentSettlement == rob.CurrentSettlement
                          ? "(z krolem w " + ep.CurrentSettlement.StringId + ")" : "(" + Dist(ep, rob).ToString("0", Inv) + " jedn. od krola)");
            }
            return sb.Length > 0 ? sb.ToString() : "brak";
        }

        private static string RidersText(MobileParty rob)
        {
            var names = new List<string>();
            foreach (var id in _rpRiders) { var h = H(id); if (h != null && h.PartyBelongedTo == rob) names.Add(WhoS(h)); }
            return names.Count > 0 ? string.Join(", ", names.ToArray()) : "nikt";
        }

        /// <summary>Start orszaku: dorosli z rodu krola bez wlasnej druzyny wsiadaja do druzyny krola (Cersei, Joffrey bez druzyny, Ogar); reszta - linia.</summary>
        private static string Mount(Hero robert, MobileParty rob)
        {
            var notes = new List<string>();
            var list = new List<Hero> { H(CerseiId), H(JoffreyId), Tpl(SandorTpl) };
            foreach (var h in list)
            {
                if (h == null) continue;
                string n = WhoS(h);
                try
                {
                    if (!h.IsAlive || h.IsPrisoner) { notes.Add(n + " (" + WhereS(h) + ")"); continue; }
                    if (Led(h) != null) { notes.Add(n + " (wlasna druzyna - eskorta)"); continue; }
                    if (h.PartyBelongedTo == rob) { if (!_rpRiders.Contains(h.StringId)) _rpRiders.Add(h.StringId); notes.Add(n + " (juz w druzynie krola)"); continue; }
                    if (h.PartyBelongedTo != null) { notes.Add(n + " (w druzynie " + h.PartyBelongedTo.Name + " - zostaje)"); continue; }
                    if (h.IsChild) { notes.Add(n + " (dziecko - nie jedzie w druzynie)"); continue; }
                    if (h.Clan != robert.Clan) { notes.Add(n + " (inny rod - zostaje)"); continue; }
                    if (h.GovernorOf != null) { notes.Add(n + " (namiestnik " + h.GovernorOf.Name + " - zostaje)"); continue; }
                    if (!h.IsActive) { notes.Add(n + " (nieczynny - zostaje)"); continue; }
                    if (h.CurrentSettlement != null) LeaveSettlementAction.ApplyForCharacterOnly(h);
                    TeleportHeroAction.ApplyImmediateTeleportToParty(h, rob);
                    if (h.PartyBelongedTo == rob) { _rpRiders.Add(h.StringId); notes.Add(n + " (wsiada)"); }
                    else notes.Add(n + " (NIE wsiadl: " + WhereS(h) + ")");
                }
                catch (Exception e) { WStumble("Mount(" + n + ")", e); notes.Add(n + " (BLAD)"); }
            }
            return notes.Count > 0 ? string.Join(", ", notes.ToArray()) : "nikt";
        }

        /// <summary>Dzieci (i inni bez druzyny) do osady celu orszaku: tylko czynne, wolne, bez druzyny i bez urzedu namiestnika.</summary>
        private static string MoveKids(string[] ids, Settlement to)
        {
            var notes = new List<string>();
            foreach (var id in ids)
            {
                var h = H(id);
                if (h == null) continue;
                string n = WhoS(h);
                try
                {
                    if (!h.IsAlive || h.IsPrisoner) { notes.Add(n + " (" + WhereS(h) + ")"); continue; }
                    if (h.CurrentSettlement == to) { notes.Add(n + " (juz w " + to.StringId + ")"); continue; }
                    if (h.PartyBelongedTo != null) { notes.Add(n + " (w druzynie " + h.PartyBelongedTo.Name + ")"); continue; }
                    if (h.GovernorOf != null) { notes.Add(n + " (namiestnik - zostaje)"); continue; }
                    if (!h.IsActive) { notes.Add(n + " (nieczynny - zostaje)"); continue; }
                    TeleportHeroAction.ApplyImmediateTeleportToSettlement(h, to);
                    notes.Add(n + (h.CurrentSettlement == to ? " (przyjezdza do " + to.StringId + ")" : " (NIE: " + WhereS(h) + ")"));
                }
                catch (Exception e) { WStumble("MoveKids(" + n + ")", e); notes.Add(n + " (BLAD)"); }
            }
            return notes.Count > 0 ? string.Join(", ", notes.ToArray()) : "brak";
        }

        private static void ColumnEnd(Hero robert, MobileParty rob, double day)
        {
            _rpPhase = LegCount + 1; _rpArrKL = day;
            var kl = KingsLanding();
            var off = new List<string>();
            foreach (var id in _rpRiders)
            {
                var h = H(id);
                try
                {
                    if (h == null || !h.IsAlive || h.IsPrisoner || h.PartyBelongedTo != rob || rob.MapEvent != null) continue;
                    var to = rob.CurrentSettlement ?? kl;
                    if (to == null) continue;
                    TeleportHeroAction.ApplyImmediateTeleportToSettlement(h, to);
                    off.Add(WhoS(h) + (h.CurrentSettlement == to ? " -> " + to.StringId : " (NIE: " + WhereS(h) + ")"));
                }
                catch (Exception e) { WStumble("ColumnEnd(" + id + ")", e); }
            }
            _rpRiders.Clear();
            string kids = kl != null ? MoveKids(new[] { MyrcellaId, TommenId, SansaId, AryaId }, kl) : "brak osady " + KingsLandingId;
            string ned = "-";
            var nh = H(NedId);
            if (nh != null && Led(nh) == null && kl != null) ned = MoveKids(new[] { NedId }, kl);
            else if (nh != null) ned = WhoS(nh) + " " + WhereS(nh) + " (wlasna druzyna - wraca do swojego AI)";
            Log.Info(WTag + "orszak w KROLEWSKIEJ PRZYSTANI - KONIEC (dzien prologu " + D(day) + "; Winterfell " + (_rpArrW >= 0 ? "d" + D(_rpArrW) : "-") + ") | z druzyny krola: "
                     + (off.Count > 0 ? string.Join(", ", off.ToArray()) : "nikt") + " | Sansa, Arya, dzieci krola: " + kids + " | Ned: " + ned + " | wszyscy wracaja do swojego AI.");
        }

        private static void NedStep(double day)
        {
            if (_nedPhase < 0 || _nedPhase > 2) return;
            var ned = H(NedId);
            if (_nedPhase == 0)
            {
                if (day < NedRideFrom) return;
                if (_rpPhase < 0 || (_wLegacy && day > NedRideFrom + 1)) { _nedPhase = -1; _nedWhy = _rpPhase < 0 ? "orszak pominiety" : "zapis sprzed E5"; return; }
                if (!_kingsAlive || ned == null || !ned.IsAlive) { _nedPhase = -1; _nedWhy = "Ned nie ozywiony albo martwy"; return; }
                if (Led(ned) == null) { _nedPhase = -1; _nedWhy = "Ned bez druzyny (" + WhereS(ned) + ") - do Krolewskiej Przystani na koniec orszaku"; Log.Info(WTag + "Ned: " + _nedWhy + "."); return; }
                _nedPhase = 1;
                Log.Info(WTag + "Ned: druzyna jedzie do Winterfell na wyjazd orszaku (dzien prologu " + D(day) + ", " + Pos(Led(ned), Settlement.Find(WinterfellId)) + ").");
            }
            var np = Led(ned);
            if (np == null) { _nedPhase = -1; _nedWhy = "Ned stracil druzyne (" + WhereS(ned) + ")"; Log.Info(WTag + "Ned: " + _nedWhy + " (dzien prologu " + D(day) + ")."); return; }
            if (_nedPhase == 1)
            {
                if (_rpPhase > LegHome || _rpPhase < 0 || _rpPhase > LegCount)
                {
                    _nedPhase = 2;
                    Log.Info(WTag + "Ned: eskortuje krola na poludnie (dzien prologu " + D(day) + ", " + Pos(np, null) + ").");
                }
                else { Control(NScene, np, KGo, Settlement.Find(WinterfellId), null, true); return; }
            }
            if (_rpPhase < 0 || _rpPhase > LegCount)
            {
                _nedPhase = 3;
                Log.Info(WTag + "Ned: koniec eskorty (dzien prologu " + D(day) + ", " + Pos(np, null) + ") - wraca do swojego AI.");
                return;
            }
            var rob = Led(H(RobertId));
            if (rob != null && rob != np) Escort(NScene, np, rob);
        }

        private static void TyrionEnd(int phase, string why, double day)
        {
            _tyPhase = phase; _tyWhy = why;
            Log.Info(WTag + "Tyrion: " + why + " (dzien prologu " + D(day) + ").");
        }

        private static void TyrionStep(double day)
        {
            if (_tyPhase < 0 || _tyPhase > 4) return;
            var ty = Tpl(TyrionTpl);
            var tp = Led(ty);
            if (_tyPhase == 0)
            {
                if (day < TyrionWallFrom) return;
                if (_wLegacy && day > TyrionWallFrom + 1) { TyrionEnd(-1, "zapis sprzed E5 - bez drogi na Mur", day); return; }
                if (tp == null) { TyrionEnd(-1, "bez wlasnej druzyny (" + WhereS(ty) + ") - bez drogi na Mur", day); return; }
                _tyPhase = 1;
                Log.Info(WTag + "Tyrion: odlacza sie od orszaku i jedzie na Mur (dzien prologu " + D(day) + ", " + Pos(tp, Settlement.Find(CastleBlackId)) + ").");
            }
            if (tp == null) { TyrionEnd(5, "koniec drogi - " + WhereS(ty), day); return; }
            var cb = Settlement.Find(CastleBlackId); var inn = Settlement.Find(InnId);
            if (cb == null || inn == null) { TyrionEnd(-1, "brak osady " + (cb == null ? CastleBlackId : InnId), day); return; }
            if (_tyPhase == 1 && At(tp, cb)) { _tyPhase = 2; _tyArrCB = day; Log.Info(WTag + "Tyrion: w CZARNYM ZAMKU (dzien prologu " + D(day) + ")."); }
            if ((_tyPhase == 1 || _tyPhase == 2) && day >= TyrionWallLeave)
            {
                _tyPhase = 3;
                Log.Info(WTag + "Tyrion: wraca z Muru na poludnie, do gospody na rozstajach (dzien prologu " + D(day) + ", " + Pos(tp, inn) + ").");
            }
            if (_tyPhase == 3 && At(tp, inn)) { _tyPhase = 4; _tyArrInn = day; Log.Info(WTag + "Tyrion: w GOSPODZIE NA ROZSTAJACH (dzien prologu " + D(day) + ")."); }
            if (_tyPhase >= 3 && day >= TyrionInnUntil)
            {
                TyrionEnd(5, (_tyPhase == 4 ? "w gospodzie" : "NIE dojechal do gospody (" + Pos(tp, inn) + ")") + " - wraca do swojego AI przed #p01", day);
                return;
            }
            Control(TScene, tp, KGo, _tyPhase <= 2 ? cb : inn, null, true);
        }

        // ---------------------------------------------------------------- linie

        private static string ProgressLine()
        {
            var sb = new StringBuilder(400);
            sb.Append("orszak: ");
            if (_rpPhase < 0) sb.Append("pominiety/przerwany (").Append(_rpWhy).Append(')');
            else if (_rpPhase == 0) sb.Append("przed wyjazdem");
            else if (_rpPhase > LegCount) sb.Append("KONIEC w ").Append(KingsLandingId).Append(" d").Append(D(_rpArrKL));
            else
            {
                var rob = Led(H(RobertId));
                var target = Settlement.Find(LegTo[_rpPhase - 1]);
                sb.Append("odcinek ").Append(_rpPhase).Append('/').Append(LegCount).Append(" -> ").Append(LegTo[_rpPhase - 1]).Append(" (od d").Append(LegFrom[_rpPhase - 1]).Append(")")
                  .Append(", krol ").Append(Pos(rob, target));
                if (rob != null) sb.Append(", eskorta: ").Append(EscortText(rob)).Append(", w druzynie krola: ").Append(RidersText(rob));
            }
            sb.Append(", Winterfell ").Append(_rpArrW >= 0 ? "d" + D(_rpArrW) : "-");
            sb.Append(" | Ned: ").Append(_nedPhase < 0 ? "pominiety (" + _nedWhy + ")" : _nedPhase == 0 ? "przed" : _nedPhase == 1 ? "do Winterfell" : _nedPhase == 2 ? "eskorta krola" : "koniec");
            if (_nedPhase == 1 || _nedPhase == 2) sb.Append(" (").Append(Pos(Led(H(NedId)), null)).Append(')');
            sb.Append(" | Tyrion (Mur): ").Append(_tyPhase < 0 ? "pominiety (" + _tyWhy + ")" : _tyPhase == 0 ? "w orszaku" : _tyPhase == 1 ? "na Mur" : _tyPhase == 2 ? "w Czarnym Zamku"
                       : _tyPhase == 3 ? "do gospody" : _tyPhase == 4 ? "w gospodzie" : "koniec (" + _tyWhy + ")");
            if (_tyPhase >= 1 && _tyPhase <= 4) sb.Append(" (").Append(Pos(Led(Tpl(TyrionTpl)), null)).Append(')');
            sb.Append(", Czarny Zamek ").Append(_tyArrCB >= 0 ? "d" + D(_tyArrCB) : "-").Append(", gospoda ").Append(_tyArrInn >= 0 ? "d" + D(_tyArrInn) : "-");
            return sb.ToString();
        }

        private static string ProgressStart()
        {
            return "orszak faza " + _rpPhase + "/" + LegCount + " (Winterfell " + (_rpArrW >= 0 ? "d" + D(_rpArrW) : "-") + ", KP " + (_rpArrKL >= 0 ? "d" + D(_rpArrKL) : "-")
                   + "), Ned " + _nedPhase + ", Tyrion " + _tyPhase + ", w druzynie krola " + _rpRiders.Count;
        }
    }
}
