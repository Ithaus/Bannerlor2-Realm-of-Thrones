using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 486 (Jeff 13.11: "i w miastach nie nalicza mi noclegu, przeciez nie spie na ulicy, powinno co noc naliczac mi nocleg, a jak wchodze do karczmy, to pokazuje mi
    /// jakie sa noclegi dostepne"). Klucz TownLodgingNightly ("Charge Town Lodging Every Night", TAK); NIE = jak dotad (sen w miescie darmowy, opcje menu karczmy
    /// placa i klada spac od razu).
    /// N1 Liczenie nocy (bez Harmony): NightRest.OnHourly woła NightHour w kazdej godzinie nocy ksiegi (LedgerNight - ta sama noc co ksiega snu gracza); partia gracza
    ///    w miescie = +1 godzina dla tego miasta. Kazda godzina obecnosci (kuznia, menu oczekiwania gry, scena) - gdzies trzeba spac. Godzina nadrabiana (NightClock
    ///    NomH ta sama co ostatnio liczona) nie liczy sie drugi raz; przerwa > 14 h bez godziny nocy w miescie - liczniki od zera (swit pominiety nie skleja dwoch nocy).
    /// N2 Zaplata o swicie gracza (NightRest, blok switu raz na 12 h, PO SettleNight): miasto z najwieksza liczba godzin tej nocy, gdy >= polowa bazy snu
    ///    (SleepHoursNeeded / 2, w gore; 6 -> 3 h). Bez oplaty (linia logu z powodem): klucz, noc juz oplacona od poprzedniego switu (Commit z menu - _lastTownPaidMin,
    ///    zajazd / wioska - _rest.CommitMin), miasto rodu gracza (spi w zamku pana), oblezenie, sluzba ROT, cudza armia, przebranie, niewola, bitwa. Bramka wlasna,
    ///    nie Allowed() - o swicie gracz bywa w menu snu, ktore Allowed odrzuca. Dom RealisticCaptivity (Homes.Vault, internal innego DLL) - NIE rozrozniany (Jeff ma
    ///    dom tylko we wsi); krok nastepny: publiczne API w RC.
    /// N3 Jakosc: pokoj zarezerwowany w menu karczmy tego miasta (_stayQ), inaczej wspolna izba. Rachunek Plan() jak w menu (szeregowi i towarzysze sami schodza nizej
    ///    albo do stajni, okno lupu darowane); gracza nie stac - cala druzyna o jakosc nizej; nie stac nawet na wspolna izbe - gracz spi w stajni (darmo, jak oboz),
    ///    ludzie i towarzysze placa swoje. Zaplata PayCore (ta sama ksiega co Commit: InnPay, HeroPays, kontrola obiegu World()).
    /// N4 Premia: po pelnej nocy (NightRest.FullNight z odpoczynku doby przed switem) stan "wypoczeci" do zmierzchu (faza 2, Bonus z rachunku) - ta sama regula co
    ///    Wake(full). Bez mnoznika snu i gojenia (te naleza do snu w menu w oplaconym lozku - "Go to bed now"). Noc niepelna - zaplacone bez premii.
    /// N5 Menu karczmy (arm_inn): przy kluczu opcje jakosci REZERWUJA pokoj na pobyt (bez zaplaty i snu), nowa opcja "Go to bed now" = dotychczasowy Commit (zaplata
    ///    z gory i sen; swit tej doby nie placi drugi raz). Rezerwacja tylko w pamieci, zdjeta po wyjezdzie (gdy ta noc nie ma godzin w tym miescie).
    /// N6 Karczmarz 3D miasta (tavernkeeper_talk): "Have you beds for the night?" - ceny z tych samych tablic i rezerwacja (BedsLine), bez zaplaty w rozmowie.
    /// Wyjatki: licznik potkniec (Stumble), nic nie gasimy.
    /// </summary>
    internal static partial class TownInns
    {
        private const string P486 = "Nocleg w miescie (486): ";
        private const int DefaultQ = 0;          // bez rezerwacji - wspolna izba
        private const long GapResetH = 14;       // przerwa w godzinach nocy w miescie, po ktorej liczniki od zera
        private const long SinceDawnMin = 1440 - 120;   // oplata "od poprzedniego switu" (zapas 2 h na tick godzinowy)

        internal static bool NightlyOn { get { var s = Settings.Current; return On && s != null && s.TownLodgingNightly; } }

        private static readonly Dictionary<string, int> _nightH = new Dictionary<string, int>(StringComparer.Ordinal);
        private static long _nightLastH = long.MinValue;
        private static long _lastTownPaidMin = -1;
        private static string _stayTown;
        private static int _stayQ = -1;
        // liczniki doby (linia "Karczmy (KA): dzien") i sesji
        private static int _dAuto, _dAutoSkip, _dAutoStable, _dAutoDown, _dBooked, _sAuto;
        private static long _dAutoPaid;

        /// <summary>Reset (nowa kampania / wczytanie): liczniki nocy, rezerwacja, ostatnia oplata.</summary>
        private static void NightlyReset()
        {
            _nightH.Clear(); _nightLastH = long.MinValue; _lastTownPaidMin = -1; _stayTown = null; _stayQ = -1; _sAuto = 0;
            NightlyDayReset();
        }

        private static void NightlyDayReset() { _dAuto = _dAutoSkip = _dAutoStable = _dAutoDown = _dBooked = 0; _dAutoPaid = 0; }

        private static string NightlyDayText()
        {
            return " | 486 nocleg z urzedu (klucz " + (NightlyOn ? "TAK" : "NIE") + "): oplacone noce " + _dAuto + " (od wczytania " + _sAuto + "), zaplacone " + _dAutoPaid
                   + ", pominiete " + _dAutoSkip + ", jakosc obnizona (gracza nie stac) " + _dAutoDown + ", gracz w stajni " + _dAutoStable + ", rezerwacje " + _dBooked
                   + ", rezerwacja teraz " + (_stayTown != null ? _stayTown + " " + QLog[Math.Max(0, Math.Min(2, _stayQ))] : "brak");
        }

        /// <summary>Jakosc nocy w tym miescie: rezerwacja albo wspolna izba.</summary>
        private static int CurQ(Settlement st) { return st != null && _stayTown == st.StringId && _stayQ >= 0 && _stayQ <= 2 ? _stayQ : DefaultQ; }

        private static bool Booked(Settlement st) { return st != null && _stayTown == st.StringId && _stayQ >= 0; }

        /// <summary>Miasto rodu gracza - spi w zamku pana, bez oplaty.</summary>
        private static bool OwnTown(Settlement st) { return st != null && Clan.PlayerClan != null && st.OwnerClan == Clan.PlayerClan; }

        // ------------------------------------------------------------ N1 liczenie nocy
        /// <summary>NightRest.OnHourly (po ksiedze godziny): godzina nocy ksiegi w miescie = +1 dla tego miasta. Rezerwacja zdjeta po wyjezdzie.</summary>
        internal static void NightHour(MobileParty mp, bool night)
        {
            try
            {
                if (mp == null) return;
                var cur = mp.CurrentSettlement;
                if (_stayTown != null && (cur == null || cur.StringId != _stayTown) && !_nightH.ContainsKey(_stayTown))
                {
                    Log.Info(P486 + "rezerwacja (" + QLog[Math.Max(0, Math.Min(2, _stayQ))] + ") w " + _stayTown + " zdjeta - gracz wyjechal z miasta.");
                    _stayTown = null; _stayQ = -1;
                }
                if (!night || !NightlyOn || cur == null || !cur.IsTown || PlayerCaptivity.IsCaptive) return;
                double nom = NightClock.NomH >= 0 ? NightClock.NomH : CampaignTime.Now.ToHours;
                long hk = (long)Math.Round(nom);
                if (hk == _nightLastH) return;   // godzina nadrabiana - juz policzona
                if (_nightLastH != long.MinValue && hk - _nightLastH > GapResetH && _nightH.Count > 0) _nightH.Clear();   // swit pominiety - nie sklejac nocy
                _nightLastH = hk;
                int n;
                _nightH.TryGetValue(cur.StringId, out n);
                _nightH[cur.StringId] = n + 1;
            }
            catch (Exception e) { Stumble("NightHour", e); }
        }

        // ------------------------------------------------------------ N2-N4 zaplata o swicie
        /// <summary>Powod braku oplaty (po polsku do logu); null = placi.</summary>
        private static string SkipWhy(Settlement st, MobileParty mp, long now)
        {
            if (!NightlyOn) return "klucz Charge Town Lodging Every Night albo Town Inns / Night Rest wylaczone";
            if (st == null || !st.IsTown || st.Town == null) return "brak miasta";
            // "od poprzedniego switu" z zapasem 2 h (swit liczony tickiem godzinowym - dwa swity nie sa co do minuty 1440 od siebie)
            if (_lastTownPaidMin >= 0 && _lastTownPaidMin > now - SinceDawnMin) return "juz oplacone w karczmie od poprzedniego switu (" + When(_lastTownPaidMin) + ")";
            var r = _rest;
            if (r != null && !r.Auto && r.CommitMin > now - SinceDawnMin) return "juz oplacone (" + (r.RoadUid != null ? "zajazd" : r.HostUid != null ? "wlasna wioska" : "karczma") + " " + When(r.CommitMin) + ")";
            if (OwnTown(st)) return "miasto rodu gracza - sen w zamku pana";
            if (st.IsUnderSiege) return "miasto oblezone";
            if (PlayerCaptivity.IsCaptive) return "gracz w niewoli";
            if (NightRest.EnlistedNow) return "sluzba ROT - o noclegu decyduje lord";
            if (mp.Army != null && mp.Army.LeaderParty != mp) return "cudza armia - o noclegu decyduje lord";
            if (Campaign.Current != null && Campaign.Current.IsMainHeroDisguised) return "przebranie";
            if (mp.MapEvent != null) return "bitwa";
            return null;
        }

        /// <summary>NightRest, blok switu gracza (raz na 12 h), PO SettleNight: zaplata nocy w miescie. restBefore - odpoczynek doby przed switem (premia N4).</summary>
        internal static void DawnCharge(float restBefore)
        {
            try
            {
                if (_nightH.Count == 0) return;
                string best = null; int bestH = 0;
                foreach (var kv in _nightH) if (kv.Value > bestH) { best = kv.Key; bestH = kv.Value; }
                _nightH.Clear(); _nightLastH = long.MinValue;
                int need = Math.Max(1, (int)Math.Ceiling(NightRest.BaseHours() / 2f));
                if (best == null || bestH < need)
                {
                    Log.Info(P486 + "swit - w miescie " + (best ?? "?") + " tylko " + bestH + " h nocy (prog " + need + " h) - noc nie w miescie, bez oplaty.");
                    return;
                }
                var mp = MobileParty.MainParty;
                var me = Hero.MainHero;
                if (mp == null || me == null) return;
                Settlement st = null;
                try { st = Settlement.Find(best); } catch { st = null; }
                long now = NowMin();
                string why = SkipWhy(st, mp, now);
                string name = st != null ? st.Name.ToString() : best;
                if (why != null)
                {
                    _dAutoSkip++;
                    Log.Info(P486 + "miasto " + name + " (" + best + "), godzin nocy " + bestH + " - pominiete: " + why + ".");
                    if (_stayTown == best && (mp.CurrentSettlement == null || mp.CurrentSettlement.StringId != best)) { _stayTown = null; _stayQ = -1; }
                    return;
                }
                int booked = CurQ(st), q = booked;
                bool sacked = SackedHere(st, mp);
                var b = Plan(st, q, true, sacked);
                while (!b.PlayerCan && q > 0) { q--; b = Plan(st, q, true, sacked); }
                bool stable = !b.PlayerCan;
                if (stable)
                {
                    // gracza nie stac nawet na wspolna izbe - spi w stajni (darmo, jak oboz); ludzie i towarzysze placa swoje
                    b.PlayerCost = 0; b.People[q + 1]--; b.People[0]++;
                    _dAutoStable++;
                }
                if (q < booked) _dAutoDown++;
                int menPaid, compPaid, compPursePaid, playerPaid, folk;
                long paidBefore = _lastTownPaidMin;
                PayCore(st, q, b, "nocleg z urzedu (486, godzin nocy w miescie " + bestH + (stable ? ", gracz w stajni" : "") + ") w ", out menPaid, out compPaid, out compPursePaid, out playerPaid, out folk);
                _lastTownPaidMin = paidBefore;   // PayCore zapisuje chwile oplaty dla noclegu z menu; oplata z urzedu nie blokuje nastepnej nocy
                int paidAll = menPaid + compPaid + playerPaid;
                _dAuto++; _sAuto++; _dAutoPaid += paidAll;
                // konie oplacone: konni szeregowi w lozkach, towarzysze z koniem poza stajnia, konie gracza (bez stajni gracza)
                int horses = 0;
                for (int l = 0; l < 3; l++) horses += b.MountAt[l];
                for (int i = 0; i < b.Comp.Count; i++) if (b.CompQ[i] >= 0 && HasMount(b.Comp[i])) horses++;
                if (!stable) horses += (b.OwnHorse ? 1 : 0) + b.Spare + b.ArmorySpare;
                int compStable = 0;
                for (int i = 0; i < b.CompQ.Count; i++) if (b.CompQ[i] < 0) compStable++;
                var lack = new List<string>();
                if (q < booked) lack.Add("gracza nie stac na " + QLog[booked] + " - cala druzyna " + QLog[q]);
                if (stable) lack.Add("gracz w stajni (nie stac na wspolna izbe, ma " + me.Gold + ")");
                if (b.MenStable > 0) lack.Add("szeregowi w stajni " + b.MenStable);
                if (compStable > 0) lack.Add("towarzysze w stajni " + compStable);
                Log.Info(P486 + "miasto " + name + " (" + best + "), pokoj " + QLog[q] + (Booked(st) ? " (rezerwacja)" : " (domyslny)") + ", ludzi " + b.Persons + ", koni " + horses
                         + ", zaplacono " + paidAll + " zl -> karczmy miasta (kiesa ludu " + folk + ", kasa miasta " + (paidAll - folk) + "; ludzie " + menPaid + ", towarzysze " + compPaid
                         + ", gracz " + playerPaid + ")" + (b.Sacked ? ", okno lupu - ludzie bez zaplaty" : "") + ", brak pieniedzy: " + (lack.Count > 0 ? string.Join(", ", lack.ToArray()) : "nikt")
                         + " | godzin nocy w miescie " + bestH + ", odpoczynek doby " + restBefore.ToString("0.0", Inv) + " h, dlug snu " + NightRest.Debt + ".");
                // N4 premia: pelna noc - wypoczeci do zmierzchu (ta sama regula co Wake(full))
                bool full = NightRest.FullNight(restBefore, NightRest.BaseHours());
                float sp, mo, sl, he; Bonus(b, out sp, out mo, out sl, out he);
                if (full && sp + mo > 0f)
                {
                    _rest = new Rest { Phase = 2, Q = q, PlayerQ = stable ? -1 : q, TownId = st.StringId, TownName = name, CommitMin = now, StartMin = now, UntilMin = RestedUntil(now),
                                       Speed = sp, Morale = mo, Sleep = 1f, Heal = 0f, Price = q == 2 && !stable, Auto = true };
                    Log.Info(P486 + "pelna noc - wypoczeci do " + When(_rest.UntilMin) + " (predkosc +" + F1(sp) + "%, morale +" + F1(mo) + ", cena " + (q == 2 && !stable && PriceKey ? "tak" : "nie")
                             + (NightRest.Debt > 0 ? ", dlug snu " + NightRest.Debt + " - premia czeka na splate" : "") + ").");
                }
                else Log.Info(P486 + "noc niepelna (odpoczynek " + restBefore.ToString("0.0", Inv) + " h z " + NightRest.BaseHours().ToString("0.0", Inv) + " h) - zaplacone bez premii.");
                // komunikat gracza raz na noc
                string men = b.Foot + b.Mounted == 0 ? "" : b.Sacked ? " Your men took their beds by right of the sack and paid nothing." : " Your men paid " + menPaid + (b.MenStable > 0 ? ", " + b.MenStable + " of them slept in the stable." : ".");
                string comps = compPaid > 0 ? " Your companions paid " + compPaid + "." : "";
                string rested = full && sp + mo > 0f ? " Rested until " + Clock(RestedUntil(now)) + ": +" + F1(sp) + "% speed, +" + F1(mo) + " morale." : "";
                if (stable)
                    Log.Player("You could not pay for a bed in " + name + " - you slept in the stable with the horses." + men + comps + rested, true);
                else
                    InnNote("inn", q, name, b, playerPaid, men + comps + rested,
                            "The night in " + name + " is paid at a " + QualityOf(q) + " - " + paidAll + " denars in all, " + playerPaid + " of them yours." + men + comps + rested);
                if (_stayTown == best && (mp.CurrentSettlement == null || mp.CurrentSettlement.StringId != best)) { _stayTown = null; _stayQ = -1; }
            }
            catch (Exception e) { Stumble("DawnCharge", e); }
        }

        // ------------------------------------------------------------ N5 menu
        /// <summary>Napis wejscia do karczmy: "Lodgings (tonight: Common room, 34 d)"; klucz NIE - jak dotad.</summary>
        private static void EnterLabel(Settlement st, bool allowed)
        {
            string t = "Take lodgings at an inn";
            try
            {
                if (NightlyOn && st != null)
                {
                    if (OwnTown(st)) t = "Lodgings (you sleep in the keep)";
                    else if (!allowed) t = "Lodgings";
                    else
                    {
                        int q = CurQ(st);
                        var b = Plan(st, q, false, SackedHere(st, MobileParty.MainParty));
                        t = "Lodgings (tonight: " + QName[q] + ", " + b.Total + " d)";
                    }
                }
            }
            catch (Exception e) { Stumble("EnterLabel", e); }
            MBTextManager.SetTextVariable("ARM_INN_ENTER", t);
        }

        /// <summary>Akapit tekstu menu karczmy przy kluczu: noc placona o swicie, rezerwacja.</summary>
        private static void NightlyMenuText(StringBuilder sb, Settlement st)
        {
            if (!NightlyOn || st == null) return;
            sb.Append("\n\n");
            if (OwnTown(st)) { sb.Append("Your clan holds this town - you sleep in the keep for free."); return; }
            int q = CurQ(st);
            sb.Append("Each night you spend in town is paid at dawn at the inn you have booked - you do not sleep in the street. Booked for this stay: ")
              .Append(QName[q]).Append(Booked(st) ? "." : " (nothing booked yet).")
              .Append(" Choose a kind of night below to book it; \"Go to bed now\" pays at once and puts you to bed - that night is not paid again at dawn.")
              .Append(" Who cannot pay sleeps in the stable.");
        }

        /// <summary>Opcja jakosci przy kluczu: rezerwacja na pobyt (bez zaplaty i snu). SwitchToMenu z opcji zwyklego menu (arm_inn nie jest menu oczekiwania).</summary>
        private static void Book(int q)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                if (st == null || !st.IsTown) return;
                _stayTown = st.StringId; _stayQ = q; _dBooked++;
                var b = Plan(st, q, false, SackedHere(st, MobileParty.MainParty));
                Log.Info(P486 + "rezerwacja w " + st.Name + " (" + st.StringId + "): " + QLog[q] + ", dzis rachunek " + b.Total + " (gracz " + b.PlayerCost + ").");
                Log.Player("You book a " + QualityOf(q) + " in " + st.Name + " for your stay - each night here is paid at dawn (" + b.Total + " d tonight, " + b.PlayerCost + " of it yours).");
                GameMenu.SwitchToMenu(Menu);   // odswiezenie napisow (zwykle menu)
            }
            catch (Exception e) { Stumble("Book", e); }
        }

        private static bool SleepNowCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Wait;
                var st = Settlement.CurrentSettlement;
                string why;
                if (!NightlyOn || !Allowed(st, out why)) return false;
                int q = CurQ(st);
                var b = Plan(st, q, false, SackedHere(st, MobileParty.MainParty));
                MBTextManager.SetTextVariable("ARM_INN_SLEEP", "Go to bed now (" + QName[q] + " - " + b.Total + " d)");
                var t = new StringBuilder("Pay for tonight at once and go to bed at the inn you have booked - the same sleep and rest as before; this night is not paid again at dawn.");
                float sp, mo, sl, he; Bonus(b, out sp, out mo, out sl, out he);
                WakeLine(t, sl, sp, mo, he);
                t.Append(".");
                if (!b.PlayerCan) { args.IsEnabled = false; t.Append("\nYou cannot pay your own part (").Append(b.PlayerCost).Append(" d)."); }
                args.Tooltip = new TextObject("{=!}" + t.ToString());
                return true;
            }
            catch (Exception e) { Stumble("SleepNowCondition", e); return false; }
        }

        // ------------------------------------------------------------ N6 karczmarz 3D
        /// <summary>Odpowiedz karczmarza miasta na "Have you beds for the night?" - ceny z tablic menu i rezerwacja.</summary>
        internal static string BedsLine(Settlement st)
        {
            var t = new StringBuilder();
            t.Append("A man and his horse pay ").Append(PMan[0]).Append(" and ").Append(PHorse[0]).Append(" in the common room, ").Append(RoomWord(0)).Append(" ").Append(RoomOf(0)).Append(".")
             .Append(" Decent beds ").Append(PMan[1]).Append(" and ").Append(PHorse[1]).Append(", a room ").Append(RoomOf(1)).Append(".")
             .Append(" The good house ").Append(PMan[2]).Append(" and ").Append(PHorse[2]).Append(", the best room ").Append(RoomOf(2)).Append(".");
            if (NightlyOn && st != null)
            {
                if (OwnTown(st)) t.Append(" But you'll be sleeping up at the keep, my lord - it's your clan's town.");
                else
                {
                    int q = CurQ(st);
                    var b = Plan(st, q, false, SackedHere(st, MobileParty.MainParty));
                    t.Append(Booked(st) ? " You've booked the " + QName[q].ToLowerInvariant() + "; I'll have it made up - " : " Nothing booked yet, so it's the common room for you - ")
                     .Append(b.Total).Append(" for your whole company tonight, settled at dawn.");
                }
            }
            else t.Append(" Tell me when you want a bed and I'll have it made up.");
            return t.ToString();
        }
    }
}
