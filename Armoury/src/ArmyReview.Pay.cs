using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// Paczka 133e (Jeff 25.10 ok. 12:57: "... przeniesmy logi do zakladki armii, aby tam wszystko bylo czytelne ile im place jaki zold ..."; projekt 2.5 z krytyka
    /// K10, K11, K12). Wiersz "Pay" okna "Review the army" -> okno z trzema stronami (Look/Back, strony wracaja do niego):
    /// PAY - naglowek i linie z JEDNEGO wywolania modelu zoldu gry (PartyWageModel.GetTotalWage(druzyna, roster, true); wynik (int) = to, co gra pobiera jako
    /// TotalWage; K10: BK GetHeroWage losuje zaokraglenie bohaterow spoza CW, wiec osobne TotalWage i TotalWageExplained moglyby sie roznic), linie gry jak sa
    /// (mnozniki w procentach - rodzaj linii z ExplainedNumber._explainer, ta sama refleksja co Speed 133d), reszta bez nazwy jako "Other"; ostatnie pobranie
    /// z SoldierPay.WagePostfix (K12: kwota, ktora gra wziela, i odsetek niezaplacony z tego samego wywolania gry), bez zapisu - HasUnpaidWages gry; na ile dni
    /// starczy zlota (zold + rycerze).
    /// WHO GETS WHAT - towarzysze z regula CW: wyplata z tabeli x czynniki partii (CompanionWage.ShareOf/FactorOf - ta sama regula co PayFromWage przy pelnej
    /// wyplacie, czynnik z TEGO SAMEGO wywolania); reszta wyniku rozdzielona METODA NAJWIEKSZYCH RESZT proporcjonalnie do wag z podstawy BK (K10): szeregowi
    /// (int)max(stawka w zoldzie partii x BaseWage BK, 1) x placeni (AidCorps - bez rannych), bohaterowie bez CW - czesc (1) BK (nie glowy rodow) + ich linia
    /// z rozpiski; suma wierszy = wynik. Samosprawdzenie wag szeregowych + czesci (1) wszystkich bohaterow wobec linii "Base" BK - tylko do logu (ROZJAZD).
    /// Rycerze (K11): linia BK z tej samej rozpiski i osobno zold rycerza GentryService (GentryKnightWage) - bez tezy "poza zoldem powyzej" (mozliwe podwojne
    /// pobranie - osobny temat). Straz i Wolni Ludzie (CrownGifts.IsUnpaidParty): stawka 0, BK liczy najmniej 1 na czlowieka - pokazane wprost.
    /// WHERE IT GOES - podzial jak SoldierPay.Route: towarzysze CW do swoich kies, reszta (takze czesc rycerzy i innych bohaterow) do sakiewki ludzi (MenPurse)
    /// albo nikomu (klucze), rycerze osobnym przelewem; sakiewka teraz (w tym odlozone na lepszy sprzet), zalegle naprawy (TroopSelfMend.OutstandingCost),
    /// jedzenie w jednostkach (bez wyceny w zlocie - gra jej nie ma; druga regula).
    /// Tylko odczyt; GetTotalWage zuzywa MBRandom (BK GetHeroWage) - nieszkodliwe. Potkniecia liczone per miejsce (ArmyReview.Stumble).
    /// </summary>
    internal static partial class ArmyReview
    {
        private const int HeroRowsPerPara = 3, TypeRowsPerPara = 4;

        /// <summary>Linia rozpiski zoldu gry.</summary>
        internal sealed class PayLine
        {
            public string Name;
            public float Number, Pct = float.NaN;   // Pct - linia mnoznika / BaseNumber (jak Speed 133d)
            public int Op = -1;                     // 0 baza, 1 dodatek, 2 mnoznik, 3 granica, -1 nieznany
            public bool Shown;
        }

        /// <summary>Wiersz "Who gets what": typ szeregowych albo bohater.</summary>
        internal sealed class PayRow
        {
            public CharacterObject Troop;
            public Hero Hero;
            public string Name;
            public int Men, Paid, Tier, Listed, Rate, PerMan;   // szeregowi: ludzie, placeni, tier, stawka z ekranu druzyny, stawka w zoldzie partii, na czlowieka w podstawie BK
            public int Part1, Level, Due, KnightWage;           // bohaterowie: czesc (1) BK, poziom, nalezny z tabeli CW, zold rycerza (GentryService)
            public float Line;                                  // linia z imieniem bohatera w rozpisce (BK GetHeroWage, u CW - reszta do tabeli)
            public bool HasLine, Comp, Kin, Knight, Rider, Sellsword, NoCulture;
            public long Weight;                                 // waga rozdzialu (K10); towarzysz CW - 0 (wyplata z tabeli)
            public int Share;                                   // dzienna czesc wyniku
        }

        /// <summary>Zold druzyny gracza: rozpiska gry, rozdzial i dokad idzie. Nigdy null z PayOf.</summary>
        internal sealed class PayView
        {
            public bool Ok;
            public string Why;
            public float Result, BaseNumber, Sum, Other, BaseLine = float.NaN;
            public int Total;                                   // (int)wynik - jak MobileParty.TotalWage
            public double Factor = 1.0;                         // czynniki partii (CompanionWage.FactorOf)
            public float Bw = 1f;                               // BaseWage BK
            public bool OpsKnown, AidCorps, PickedShots, NoCoin, Watch, Cw, Routes, ToPurse, Undead;
            public readonly List<PayLine> Lines = new List<PayLine>();
            public readonly List<PayRow> Rows = new List<PayRow>();
            public long BaseWeights, Weights;                   // samosprawdzenie (szeregowi + czesc (1)); suma wag rozdzialu
            public int CompPaid, Shared, Unshared, Types, Heroes, DupNames;
            public int Knights, KnightPay, Gold, Days = -1;
            public bool LastKnown;
            public CampaignTime LastAt;
            public int LastPaid, LastFull = -1;
            public float LastUnpaid;
            public long Ms;

            /// <summary>Suma wierszy "Who gets what" + nierozdzielone - tozsamosc: = Total.</summary>
            public int RowsSum { get { int s = Unshared; foreach (var r in Rows) s += r.Share; return s; } }
        }

        private static string W1(float f) { return f.ToString("0.#", Inv); }
        private static string SW1(float f) { return f.ToString("+0.#;-0.#;0", Inv); }

        // ------------------------------------------------------------ liczenie
        /// <summary>Zold druzyny gracza tak, jak liczy go gra (jedno wywolanie modelu zoldu), z rozdzialem na typy i bohaterow i ostatnim pobraniem.</summary>
        internal static PayView PayOf(MobileParty mp)
        {
            var v = new PayView();
            var c = Campaign.Current;
            if (mp == null || c == null || c.Models == null || c.Models.PartyWageModel == null || mp.MemberRoster == null) { v.Why = "No campaign is running."; return v; }
            SpeedWire();   // rodzaj linii rozpiski (ExplainedNumber._explainer) - ta sama refleksja co strona Speed (133d)
            var sw = Stopwatch.StartNew();
            // K10: naglowek i linie z JEDNEGO wywolania (BK GetHeroWage losuje zaokraglenie bohaterow spoza CW - osobne TotalWage daloby inna liczbe)
            var en = c.Models.PartyWageModel.GetTotalWage(mp, mp.MemberRoster, true);
            v.Result = en.ResultNumber;
            if (float.IsNaN(v.Result) || float.IsInfinity(v.Result)) { v.Why = "The game's pay model gave no number for your party now."; return v; }   // recenzja 133: Why - sama przyczyna, pelne zdanie
            v.BaseNumber = en.BaseNumber;
            v.Total = (int)v.Result;
            v.Factor = CompanionWage.FactorOf(en);
            v.Bw = CompanionWage.BaseWageNow();
            var gl = en.GetLines();
            var ops = OpsOf(en, gl.Count);
            v.OpsKnown = ops != null;
            var byName = new Dictionary<string, float>(StringComparer.Ordinal);
            for (int i = 0; i < gl.Count; i++)
            {
                var l = new PayLine { Name = gl[i].Item1 ?? "", Number = gl[i].Item2, Op = ops != null ? ops[i] : -1 };
                if (l.Op == 2 && Math.Abs(v.BaseNumber) > 1e-4f) l.Pct = l.Number / v.BaseNumber;
                l.Shown = l.Op == 0 || Math.Abs(l.Number) >= 0.05f;
                if (l.Op == 0) v.BaseLine = l.Number;
                // linie z imieniem bohatera to dodatki (BK Add(GetHeroWage, imie)); bez rodzajow linii - kazda linia poza pierwsza
                if (l.Op == 1 || (!v.OpsKnown && i > 0)) { float x; byName.TryGetValue(l.Name, out x); byName[l.Name] = x + l.Number; }
                v.Sum += l.Number;
                v.Lines.Add(l);
            }
            if (v.OpsKnown && float.IsNaN(v.BaseLine)) v.BaseLine = 0f;   // podstawa BK 0 - gra nie pisze linii bazy
            v.Other = v.Result - v.Sum;
            try { v.AidCorps = mp.HasPerk(DefaultPerks.Steward.AidCorps); } catch { v.AidCorps = false; }
            try { v.PickedShots = mp.HasPerk(DefaultPerks.Crossbow.PickedShots); } catch { v.PickedShots = false; }
            try { v.NoCoin = CrownGifts.IsUnpaidParty(mp); v.Watch = v.NoCoin && CrownGifts.IsWatchParty(mp); } catch { v.NoCoin = false; v.Watch = false; }
            try { v.Undead = Undead.Party(mp); } catch { v.Undead = false; }
            var s = Settings.Current;
            v.Cw = CompanionWage.On;
            v.Routes = SoldierPay.RoutesWage;
            v.ToPurse = v.Routes && s != null && s.SoldierPayToPurse && MenPurse.On && !v.Undead;   // jak SoldierPay.Route (partia lorda)
            var lead = mp.LeaderHero;
            var usedName = new HashSet<string>(StringComparer.Ordinal);
            var r = mp.MemberRoster;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || el.Number <= 0) continue;
                if (ch.IsHero)
                {
                    var h = ch.HeroObject;
                    if (h == null) continue;
                    // czesc (1) BK: kazdy bohater rosteru poza glowa swojego rodu (takze wodz partii, gdy nie jest glowa rodu) - do samosprawdzenia linii "Base"
                    var cl = h.Clan;
                    int p1 = cl == null || cl.Leader != h ? CompanionWage.Part1(ch.TroopWage, mp, v.Bw) : 0;
                    v.BaseWeights += p1;
                    if (h == lead || h == Hero.MainHero) continue;   // wodz zoldu nie bierze (gracz w cudzej partii - umowa BK bez nazwy, w "Other")
                    var hr = new PayRow { Hero = h, Name = ch.Name != null ? ch.Name.ToString() : "?", Men = 1, Paid = 1, Level = h.Level, Part1 = p1 };
                    float ln;
                    if (byName.TryGetValue(hr.Name, out ln))
                    {
                        if (usedName.Add(hr.Name)) { hr.Line = ln; hr.HasLine = true; }
                        else v.DupNames++;   // linie o tym samym imieniu gra scala - cala linia przy pierwszym
                    }
                    hr.Comp = CompanionWage.Applies(h);
                    if (hr.Comp) { hr.Kin = !h.IsPlayerCompanion; hr.Due = CompanionWage.DueOf(h); }
                    else
                    {
                        hr.Knight = GentryService.IsKnightIn(h, mp);
                        if (hr.Knight) { hr.KnightWage = GentryService.KnightWageOf(h, mp); v.Knights++; v.KnightPay += hr.KnightWage; }
                        hr.Weight = Math.Max(0L, (long)Math.Round(p1 + (hr.HasLine ? hr.Line : 0f)));
                    }
                    v.Heroes++;
                    v.Rows.Add(hr);
                    continue;
                }
                // szeregowi: stawka w zoldzie partii (Straz/WL 0, premia konnego wedlug partii) i podstawa BK (int)max(stawka x BaseWage, 1) x placeni (AidCorps - bez rannych)
                var t = new PayRow { Troop = ch, Name = ch.Name != null ? ch.Name.ToString() : "?", Men = el.Number };
                t.Paid = v.AidCorps ? Math.Max(0, el.Number - el.WoundedNumber) : el.Number;
                try { t.Tier = ch.Tier; } catch { t.Tier = 0; }
                t.Listed = ch.TroopWage;
                t.Rate = MountedWage.InParty(ch, mp);
                t.Rider = t.Rate > 0 && MountedWage.PremiumFor(ch, mp);
                t.Sellsword = ch.Occupation == Occupation.Mercenary;
                t.NoCulture = ch.Culture == null;   // BK takich nie liczy
                if (!t.NoCulture)
                {
                    t.PerMan = CompanionWage.BkPerMan(t.Rate, v.Bw);
                    t.Weight = (long)t.PerMan * t.Paid;
                    v.BaseWeights += t.Weight;
                }
                v.Types++;
                v.Rows.Add(t);
            }
            Share(v);
            try { v.Gold = Hero.MainHero != null ? Hero.MainHero.Gold : 0; } catch { v.Gold = 0; }
            int per = v.Total + v.KnightPay;
            v.Days = per > 0 ? Math.Max(0, v.Gold) / per : -1;
            // K12: ostatnie pobranie zapisane przy naliczeniu gry (SoldierPay.WagePostfix); bez zapisu - stan gry (HasUnpaidWages)
            v.LastKnown = SoldierPay.LastPlayerWageKnown;
            if (v.LastKnown)
            {
                v.LastAt = SoldierPay.LastPlayerWageAt;
                v.LastPaid = SoldierPay.LastPlayerWagePaid;
                v.LastUnpaid = SoldierPay.LastPlayerWageUnpaid;
                if (v.LastUnpaid <= 0.0001f) v.LastFull = v.LastPaid;
                else if (v.LastUnpaid < 0.9999f) v.LastFull = (int)Math.Round(v.LastPaid / (1.0 - v.LastUnpaid));   // gra: niezaplacone = 1 - wziete / zold
            }
            else { try { v.LastUnpaid = mp.HasUnpaidWages; } catch { v.LastUnpaid = 0f; } }
            v.Ok = true;
            v.Ms = sw.ElapsedMilliseconds;
            return v;
        }

        /// <summary>Rozdzial wyniku (K10): towarzysze CW - wyplata z tabeli x czynniki (ta sama regula co PayFromWage, w kolejnosci rosteru, najwyzej wynik);
        /// reszta metoda najwiekszych reszt proporcjonalnie do wag (calkowite, bez zaokraglen zmiennoprzecinkowych). Suma wierszy + nierozdzielone = Total.</summary>
        private static void Share(PayView v)
        {
            int left = Math.Max(0, v.Total);
            foreach (var row in v.Rows)
            {
                if (!row.Comp) continue;
                int pay = Math.Max(0, Math.Min(left, CompanionWage.ShareOf(row.Due, v.Factor, 1.0)));
                row.Share = pay; left -= pay; v.CompPaid += pay;
            }
            long w = 0;
            foreach (var row in v.Rows) if (!row.Comp && row.Weight > 0) w += row.Weight;
            v.Weights = w;
            if (w > 0 && left > 0)
            {
                long given = 0;
                var rem = new List<KeyValuePair<long, int>>();   // (reszta, indeks wiersza)
                for (int i = 0; i < v.Rows.Count; i++)
                {
                    var row = v.Rows[i];
                    if (row.Comp || row.Weight <= 0) continue;
                    long num = (long)left * row.Weight;
                    row.Share = (int)(num / w);
                    given += row.Share;
                    rem.Add(new KeyValuePair<long, int>(num % w, i));
                }
                long rest = left - given;   // mniej niz liczba wierszy
                rem.Sort((a, b) =>
                {
                    if (a.Key != b.Key) return b.Key.CompareTo(a.Key);
                    long wa = v.Rows[a.Value].Weight, wb = v.Rows[b.Value].Weight;
                    return wa != wb ? wb.CompareTo(wa) : a.Value.CompareTo(b.Value);
                });
                for (int k = 0; k < rem.Count && rest > 0; k++, rest--) v.Rows[rem[k].Value].Share++;
                v.Shared = left;
            }
            else v.Unshared = left;
            if (v.Total < 0) v.Unshared += v.Total;   // wynik ujemny (lancuch stawia LimitMin 0 - nie powinno sie zdarzyc): bez rozdzialu, tozsamosc trzyma
        }

        // ------------------------------------------------------------ teksty
        private static string PayLineText(PayLine l)
        {
            if (l.Op == 0) return l.Name + " " + W1(l.Number);
            if (l.Op == 2 && !float.IsNaN(l.Pct)) return l.Name + " " + Pc(l.Pct) + " (" + SW1(l.Number) + ")";
            return l.Name + " " + SW1(l.Number);
        }

        private static string Pct0(float f) { return ((int)Math.Round(f * 100f)).ToString(Inv) + "%"; }

        /// <summary>Ostatni dzien wyplaty (K12): zapis z naliczenia gry albo stan gry.</summary>
        private static string LastPayText(PayView v)
        {
            if (v.LastKnown)
            {
                string when = BattleDate(v.LastAt);
                if (v.LastUnpaid <= 0.0001f) return "Last pay day " + when + ": paid in full, " + Math.Max(0, v.LastPaid) + " denars.";
                if (v.LastFull > 0) return "Last pay day " + when + ": " + Math.Max(0, v.LastPaid) + " of " + v.LastFull + " paid - the men lost morale for the rest.";
                return "Last pay day " + when + ": nothing paid - the men lost morale.";
            }
            if (v.LastUnpaid > 0.0001f) return "At the last pay day " + Pct0(v.LastUnpaid) + " of the pay went unpaid - the men lost morale. The sums show here from the next pay day.";
            return "No pay day seen since the game was loaded; the game's own record says the last one was paid in full.";
        }

        private static string LastPayShort(PayView v)
        {
            return v.LastUnpaid > 0.0001f ? "last pay short " + Pct0(v.LastUnpaid) : "last paid in full";
        }

        /// <summary>Strona "Pay": naglowek, ostatnia wyplata, linie gry, "Other", wynik.</summary>
        internal static string PayText(PayView v)
        {
            if (v == null || !v.Ok) return v != null && v.Why != null ? v.Why : "The pay cannot be worked out now.";
            var sb = new StringBuilder();
            sb.Append("Your party's pay: ").Append(v.Total).Append(v.Total == 1 ? " denar" : " denars").Append(" a day, taken from your purse once a day");
            if (v.KnightPay > 0) sb.Append(", and ").Append(v.KnightPay).Append(" a day more to your knights (see Who gets what)");
            sb.Append('.');
            if (v.Days >= 0) sb.Append("\nYour gold: ").Append(v.Gold).Append(" - enough for ").Append(Pl(v.Days, "day", "days")).Append('.');
            sb.Append('\n').Append(LastPayText(v));
            var shown = new List<PayLine>();
            foreach (var l in v.Lines) if (l.Shown) shown.Add(l);
            for (int i = 0; i < shown.Count; i += LinesPerPara)
            {
                sb.Append("\n\n");
                for (int k = i; k < shown.Count && k < i + LinesPerPara; k++)
                {
                    if (k > i) sb.Append('\n');
                    sb.Append(PayLineText(shown[k]));
                }
            }
            float rest = v.Result;
            foreach (var l in shown) rest -= l.Number;
            sb.Append(shown.Count > 0 ? "\n" : "\n\n");
            if (Math.Abs(rest) >= 0.05f) sb.Append("Other (no name given) ").Append(SW1(rest)).Append('\n');
            sb.Append("= ").Append(W1(v.Result));
            if (Math.Abs(v.Result - v.Total) >= 0.05f) sb.Append(" - the game takes whole denars: ").Append(v.Total);
            sb.Append('.');
            sb.Append("\n\n");
            if (v.OpsKnown)
                sb.Append("Base is Banner Kings' count of your men: each one's wage x ").Append(F2(v.Bw)).Append(" (its Base Wage setting), at least 1 a man. A line in % is that share of ")
                  .Append(W1(v.BaseNumber)).Append(" - Base with the plain lines added.");
            else sb.Append("Together the lines make the pay.");
            bool below = false;
            foreach (var row in v.Rows) if (row.Comp && row.HasLine && row.Line < 0f) below = true;
            if (below) sb.Append(" A companion's line can be below zero: Banner Kings counts part of his pay in Base - his full pay is on Who gets what.");
            if (Math.Abs(rest) >= 0.05f) sb.Append(" \"Other\" is what a mod changed without giving it a name.");
            return sb.ToString();
        }

        private static string HeroRowText(PayView v, PayRow row)
        {
            var h = row.Hero;
            string his = h != null && h.IsFemale ? "her" : "his";
            if (row.Comp)
                return row.Name + " (" + (row.Kin ? "kin" : "companion") + ", level " + row.Level + "): " + row.Share + " a day" + (v.Routes ? " - to " + his + " own purse." : ".");
            if (row.Knight)
                return row.Name + " (knight of your banner): " + row.Share + " a day of the pay above (" + his + " Banner Kings line " + W1(row.HasLine ? row.Line : 0f) + ")"
                       + (row.KnightWage > 0 ? ", and " + row.KnightWage + " a day knight's wage from your purse." : ", and no knight's wage (unpaid service).");
            return row.Name + " (level " + row.Level + "): " + row.Share + " a day.";
        }

        private static string TypeRowText(PayView v, PayRow row)
        {
            var sb = new StringBuilder();
            sb.Append(row.Name).Append(" x").Append(row.Men).Append(", tier ").Append(row.Tier);
            if (row.Rider) sb.Append(", rider");
            if (row.Sellsword) sb.Append(", sell-sword");
            if (row.NoCulture) return sb.Append(": not counted by Banner Kings (no culture).").ToString();
            sb.Append(": ").Append(row.Share).Append(" a day (").Append(row.PerMan).Append(" a man; party screen ").Append(row.Listed);
            if (v.AidCorps && row.Paid < row.Men) sb.Append("; ").Append(row.Paid).Append(" paid");
            sb.Append(')');
            if (v.NoCoin && row.Rate <= 0 && row.Listed > 0) sb.Append(" - no coin by ").Append(v.Watch ? "the Watch's" : "the Free Folk's").Append(" rule, Banner Kings still counts 1 a man");
            return sb.Append('.').ToString();
        }

        /// <summary>Strona "Who gets what": towarzysze, rycerze i inni bohaterowie, typy szeregowych (od najwiekszej czesci), legenda.</summary>
        internal static string WhoText(PayView v)
        {
            if (v == null || !v.Ok) return v != null && v.Why != null ? v.Why : "The pay cannot be worked out now.";
            var sb = new StringBuilder();
            sb.Append("Who gets the ").Append(v.Total).Append(" a day");
            if (v.CompPaid > 0) sb.Append(": your companions take their pay by their level first (Companion Wage By Level); the rest falls");
            else sb.Append(": it falls");
            sb.Append(" on each man by his part of Banner Kings' count, so culture, perks and the like weigh on all alike.");
            var heroes = new List<PayRow>();
            var types = new List<PayRow>();
            foreach (var row in v.Rows) (row.Hero != null ? heroes : types).Add(row);
            heroes.Sort((a, b) =>
            {
                int ka = a.Comp ? 0 : a.Knight ? 1 : 2, kb = b.Comp ? 0 : b.Knight ? 1 : 2;
                return ka != kb ? ka.CompareTo(kb) : b.Share.CompareTo(a.Share);
            });
            types.Sort((a, b) => a.Share != b.Share ? b.Share.CompareTo(a.Share) : b.Men.CompareTo(a.Men));
            for (int i = 0; i < heroes.Count; i += HeroRowsPerPara)
            {
                sb.Append("\n\n");
                for (int k = i; k < heroes.Count && k < i + HeroRowsPerPara; k++) { if (k > i) sb.Append('\n'); sb.Append(HeroRowText(v, heroes[k])); }
            }
            for (int i = 0; i < types.Count; i += TypeRowsPerPara)
            {
                sb.Append("\n\n");
                for (int k = i; k < types.Count && k < i + TypeRowsPerPara; k++) { if (k > i) sb.Append('\n'); sb.Append(TypeRowText(v, types[k])); }
            }
            if (types.Count == 0 && heroes.Count == 0) sb.Append("\n\nNobody in your party draws pay.");
            sb.Append("\n\n\"A man\" is Banner Kings' count: the troop's wage x ").Append(F2(v.Bw)).Append(" (its Base Wage setting), at least 1")
              .Append(v.AidCorps ? ", the wounded left out (Aid Corps)" : ", the wounded too").Append(". The party screen shows the troop's wage alone.");
            bool rider = false;
            foreach (var t in types) if (t.Rider) rider = true;
            if (rider)
            {
                var st = Settings.Current;
                sb.Append(" A rider is paid x").Append(MountedWage.Factor(st).ToString("0.##", Inv)).Append(" by his troop, with or without a horse in the troops' armoury.");
            }
            if (v.Unshared != 0) sb.Append(' ').Append(v.Unshared).Append(" a day the game counts for nobody listed here.");
            return sb.ToString();
        }

        private static int MenPart(PayView v) { return v.Total - (v.Cw ? v.CompPaid : 0); }

        /// <summary>Strona "Where it goes": podzial jak SoldierPay.Route, rycerze, sakiewka ludzi teraz, zalegle naprawy, jedzenie.</summary>
        internal static string WhereText(PayView v, MobileParty mp)
        {
            if (v == null || !v.Ok) return v != null && v.Why != null ? v.Why : "The pay cannot be worked out now.";
            var sb = new StringBuilder();
            if (!v.Routes) sb.Append("The pay leaves your purse and nobody gets it - Soldier Pay To Purse, Garrison Pay To Coffers and Crown Wage Refund Enabled are all off.");
            else
            {
                sb.Append("Of the ").Append(v.Total).Append(" a day:");
                if (v.Cw && v.CompPaid > 0) sb.Append("\n- ").Append(v.CompPaid).Append(" to your companions' own purses (Companion Wage By Level);");
                int men = MenPart(v);
                bool others = false;
                foreach (var row in v.Rows) if (row.Hero != null && !row.Comp && row.Share > 0) others = true;
                if (v.ToPurse)
                {
                    sb.Append("\n- ").Append(men).Append(" to the men's purse").Append(others ? " (the knights' and other heroes' part too)" : "")
                      .Append(". The men spend it themselves: mending and missing kit in the towns, clothing, better kit from what they put aside, the rest on life in town")
                      .Append(Sutlers.On ? " and with the sutlers in the field." : ".");
                }
                else sb.Append("\n- ").Append(men).Append(" goes to nobody: ").Append(v.Undead ? "the dead spend nothing." : "Soldier Pay To Purse or Men Purse Enabled is off.");
            }
            if (v.KnightPay > 0)
            {
                var st = Settings.Current;
                sb.Append("\n\nKnights of your banner: ").Append(v.KnightPay).Append(" a day more, straight from your purse to theirs (").Append(Pl(v.Knights, "knight", "knights"))
                  .Append(" x ").Append(st != null ? Math.Max(0, st.GentryKnightWage) : 0).Append(").");
            }
            if (MenPurse.On)
            {
                int purse = 0, saved = 0, mend = 0;
                try { purse = MenPurse.Get(mp); saved = Math.Max(0, Math.Min(purse, MenPurse.SavedOf(mp))); } catch (Exception e) { Stumble("Where.Purse", e); }
                try { mend = TroopSelfMend.OutstandingCost(null); } catch (Exception e) { Stumble("Where.Mend", e); }
                sb.Append("\n\nThe men's purse now: ").Append(purse).Append(purse == 1 ? " denar" : " denars");
                if (saved > 0) sb.Append(", ").Append(saved).Append(" of it put aside for better kit");
                sb.Append('.');
                if (mend > 0) sb.Append(" Mending owed on their kit: about ").Append(mend).Append(" denars at a smith's.");
            }
            float fc = 0f;
            int food = 0, days = -1;
            try { fc = mp.FoodChange; food = mp.TotalFoodAtInventory; if (fc < -0.001f) days = mp.GetNumDaysForFoodToLast(); } catch (Exception e) { Stumble("Where.Food", e); }
            sb.Append("\n\nFood is not bought from the pay: ");
            if (fc < -0.001f) sb.Append("your party eats ").Append(W1(-fc)).Append(" a day, ").Append(food).Append(" in the baggage - enough for ").Append(Pl(Math.Max(0, days), "day", "days")).Append('.');
            else sb.Append(food).Append(" in the baggage, and none is eaten from it now.");
            return sb.ToString();
        }

        private static string WhereShort(PayView v)
        {
            if (!v.Routes) return "nobody gets it";
            int men = MenPart(v);
            string s = v.Cw && v.CompPaid > 0 ? v.CompPaid + " to companions, " : "";
            return s + men + (v.ToPurse ? " to the men's purse" : " to nobody");
        }

        // ------------------------------------------------------------ okno
        /// <summary>Wiersz glownego okna "Pay" z zapasem: niedostepny - wyszarzony z powodem (reszta okna dziala).</summary>
        private static InquiryElement PayMainRow(PayView v)
        {
            try
            {
                if (v == null || !v.Ok)
                    return new InquiryElement("pay", "Pay (not available)", null, false, v != null && v.Why != null ? v.Why : "The pay cannot be worked out now.");
                string t = "Pay: " + v.Total + " denars a day" + (v.KnightPay > 0 ? " + " + v.KnightPay + " to knights" : "") + " (" + LastPayShort(v) + ")";
                return new InquiryElement("pay", t, null, true, "What your men cost a day: every line of the game's pay, the last pay day and how long your gold lasts; who gets what;"
                                                               + " where the pay goes.");
            }
            catch (Exception e) { Stumble("PayRow", e); return new InquiryElement("pay", "Pay (not available)", null, false, "The pay cannot be worked out now."); }
        }

        /// <summary>Okno "Pay": Pay / Who gets what / Where it goes; strony wracaja tutaj, "Back" - do przegladu.</summary>
        private static void OpenPay(PayView v, List<string> pPay, List<string> pWho, List<string> pWhere)
        {
            if (v == null || !v.Ok) { OpenMain(false); return; }
            int types = 0, comps = 0, knights = 0, others = 0;
            foreach (var row in v.Rows)
            {
                if (row.Hero == null) types++;
                else if (row.Comp) comps++;
                else if (row.Knight) knights++;
                else others++;
            }
            var who = new List<string> { Pl(types, "troop type", "troop types") };
            if (comps > 0) who.Add(Pl(comps, "companion", "companions"));
            if (knights > 0) who.Add(Pl(knights, "knight", "knights"));
            if (others > 0) who.Add(Pl(others, "other hero", "other heroes"));
            var rows = new List<InquiryElement>
            {
                new InquiryElement("lines", "Pay: " + v.Total + " a day - the game's lines", null, true,
                                   "Every line of the game's pay for your party, the last pay day and how long your gold lasts."),
                new InquiryElement("who", "Who gets what: " + string.Join(", ", who.ToArray()), null, true,
                                   "Each troop type's, companion's and knight's part of the pay, worked out from the same count as the game's."),
                new InquiryElement("where", "Where it goes: " + WhereShort(v), null, true,
                                   "Where the pay goes once it leaves your purse, the men's purse now, the mending they owe and your food."),
            };
            bool photo = WindowPhotos.Active;   // paczka 132: okno proby zdjec - przyciski bez skutku
            Action back = delegate { OpenPay(v, pPay, pWho, pWhere); };
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Pay", "What your men cost a day, who gets it and where it goes. Pick what to look at.",
                rows, true, 1, 1, "Look", "Back",
                delegate (List<InquiryElement> picked)
                {
                    if (Inert(photo)) return;
                    try
                    {
                        string id = picked != null && picked.Count > 0 ? picked[0].Identifier as string : null;
                        _dReports++;
                        if (id == "lines") ShowPages("Pay", pPay, 0, back);
                        else if (id == "who") ShowPages("Who gets what", pWho, 0, back);
                        else if (id == "where") ShowPages("Where it goes", pWhere, 0, back);
                        else back();
                    }
                    catch (Exception e) { Stumble("Pay.Pick", e); }
                },
                delegate (List<InquiryElement> _) { if (Inert(photo)) return; try { OpenMain(false); } catch (Exception e) { Stumble("Pay.Back", e); } }), true);
        }

        // ------------------------------------------------------------ log
        /// <summary>Samosprawdzenie wag (K10, tylko log): linia "Base" BK wobec wag szeregowych + czesci (1) bohaterow.</summary>
        internal static string PayBaseCheck(PayView v)
        {
            if (v == null || !v.Ok) return "-";
            if (float.IsNaN(v.BaseLine)) return "bez porownania (rodzaje linii BRAK)";
            if (v.PickedShots) return "bez porownania (Picked Shots w podstawie BK)";
            float d = v.BaseWeights - v.BaseLine;
            return Math.Abs(d) < 0.5f ? "ZGODNA" : "ROZJAZD " + d.ToString("+0;-0", Inv) + " (najemnicy BK x3, BaseWage ogniwa?)";
        }

        /// <summary>Segment linii raportu: zold (133e).</summary>
        private static string PayBrief(PayView v)
        {
            if (v == null) return "-";
            if (!v.Ok) return "niedostepny (" + v.Why + ")";
            int shown = 0; foreach (var l in v.Lines) if (l.Shown) shown++;
            int comps = 0; foreach (var row in v.Rows) if (row.Comp) comps++;
            int rs = v.RowsSum;
            string last = v.LastKnown
                ? "dzien " + ((int)v.LastAt.ToDays).ToString(Inv) + ", wzieto " + v.LastPaid + (v.LastUnpaid > 0.0001f ? " z " + v.LastFull + " (niezaplacone " + v.LastUnpaid.ToString("0.00", Inv) + ")" : " (w calosci)")
                : "brak zapisu (HasUnpaidWages " + v.LastUnpaid.ToString("0.00", Inv) + ")";
            return "wynik " + v.Result.ToString("0.##", Inv) + " (gra bierze " + v.Total + "), linii " + v.Lines.Count + " (pokazanych " + shown + "), Other " + v.Other.ToString("+0.##;-0.##;0", Inv)
                   + ", rodzaje linii " + (v.OpsKnown ? "TAK" : "BRAK") + ", czynnik " + v.Factor.ToString("0.000", Inv) + ", BaseWage " + v.Bw.ToString("0.00", Inv)
                   + ", podstawa: linia Base " + (float.IsNaN(v.BaseLine) ? "-" : v.BaseLine.ToString("0", Inv)) + " wobec wag (szeregowi + czesc (1) bohaterow) " + v.BaseWeights
                   + " - " + PayBaseCheck(v) + ", BaseNumber " + v.BaseNumber.ToString("0.##", Inv)
                   + "; rozdzial: towarzysze CW " + comps + " = " + v.CompPaid + ", reszta " + v.Shared + " na wagi " + v.Weights + " (typow " + v.Types + ", bohaterow " + v.Heroes
                   + "), nierozdzielone " + v.Unshared + ", suma wierszy " + rs + " = wynik " + (rs == v.Total ? "TAK" : "NIE") + (v.DupNames > 0 ? ", wspolne imiona " + v.DupNames : "")
                   + "; rycerze " + v.Knights + " (zold osobno " + v.KnightPay + "), Straz/WL " + (v.NoCoin ? (v.Watch ? "Straz" : "WL") : "NIE") + ", AidCorps " + Yes(v.AidCorps)
                   + "; dalej: obieg " + Yes(v.Routes) + ", sakiewka ludzi " + Yes(v.ToPurse) + ", CW " + Yes(v.Cw) + "; zloto " + v.Gold + " (dni " + v.Days + ")"
                   + "; ostatnie pobranie: " + last + ", " + v.Ms + " ms";
        }
    }
}
