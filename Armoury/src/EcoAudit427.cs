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
    /// 427 (audyt ekonomii 11.11, poprawki A1 + A2) - SAME POMIARY, niczego nie zmienia w grze. Klucz Economy Audit Lines (NIE = linie i plik co do znaku jak dotad,
    /// liczniki nie licza). Kazda pozycja to dopisek do istniejacej linii doby albo wlasna linia; zadna nie rusza zlota, ludzi ani towaru:
    ///  1. "Straznik obiegu (obieg2)": stawka OBOWIAZUJACA danin od skarbca ludu (przy firmach DS straznik jest wylaczony - jego mnoznik to cien) i gracz osobno
    ///     od lordow AI (TownFolk.A427GuardNote);
    ///  2. "Pompy - sztuczne przelewy (nekon)": brakujace w grupie B danina od skarbca wsi DS i czesc firmy pana z oszczednosci ludu - POZA dotychczasowym "razem"
    ///     (szereg "razem" i "% zoldu" zostaje porownywalny), obok suma z nimi (MoneyLedger.A427PumpsTail);
    ///  3. "Warsztaty: dzien": "brak zlota/kupca" na trzy powody - kapital warsztatu ponizej kosztu surowca (start sztuki), gotowa sztuka czeka na kase miasta
    ///     (kasa - zapas kupcow &lt; cena), kapital warsztatu ponizej plac (WsStart / WsReady / WsText);
    ///  4. "Zajecia wiosek (ZW)" i dopisek reguly A7 (KS): powod kazdej doby postoju przebudowy (RbStall / RbText) i wiek przebudow po terminie (RbAge);
    ///  5. "Budzet rodow (166)": ile rodow jest ponad progiem wydawania skarbu w pokoju (Reserve Cap Days x D + 50 000) i ile z tego idzie na dwor i budowy;
    ///  6. "Dlugi miast (m96-dlugi)": dlug rad w podziale na krolestwa (DebtRealms; suma = dlug razem);
    ///  7. wlasna linia "Kto rozbija bandy (427)": kto byl "niszczycielem" bandy, karawany i taboru wsi (zdarzenie MobilePartyDestroyed - sam licznik);
    ///  8. ks-miasta.csv: dwie kolumny NA KONCU - oblezone (0/1) i wsie w rabunku albo spladrowane (StartCalibration.TownsCsv);
    ///  9. wlasna linia "Kiesy ludu - rozbicie zmiany (427)": kiesy ludu miast / podzamczy / gmin / wsi - zmiana doby i od wczytania, nazwane odplywy (daniny od
    ///     skarbca, czesc firmy pana z oszczednosci), wierzytelnosci ludu wobec rad miast (pozyczka to nie strata: kiesa -> kwit) - pomiar spadku kies ludu z audytu;
    ///  A2: etykieta "dochod z aktywow" w linii "Obieg: notable i karawany" (MoneyLedger.Obieg.cs) - to PRZELEW z kapitalu warsztatow i kas karawan, nie kran.
    /// Stan tylko sesyjny (bez zapisu): po wczytaniu i przy nowej kampanii liczniki od zera (Reset z konstruktora ArmouryBehavior). Wyjatek w liczniku - potkniecie
    /// liczone w linii, bez globalnego wylacznika (CLAUDE.md 7). Watek glowny (ticki dobowe i zdarzenia kampanii).
    /// </summary>
    internal static class EcoAudit427
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal static bool On { get { var s = Settings.Current; return s != null && s.EconomyAuditLines; } }

        private static int _st, _stSess;
        private static readonly HashSet<string> _err = new HashSet<string>();
        internal static void Stumble(string where, Exception e)
        {
            _st++; _stSess++;
            try { if (_err.Add(where)) Log.Error("EcoAudit427." + where, e); } catch { }
        }
        private static string StText() { return _stSess > 0 ? ", potkniecia pomiaru 427 od wczytania " + _stSess : ""; }
        private static string N0(double v) { return ((long)Math.Round(v)).ToString(Inv); }
        private static string Sg(long v) { return (v >= 0 ? "+" : "") + v.ToString(Inv); }
        private static string Pc(double part, double whole) { return whole > 0 ? (100.0 * part / whole).ToString("0.0", Inv) + "%" : "-"; }

        /// <summary>Nowa kampania / przed wczytaniem: wszystkie liczniki i pamiec sesji od zera.</summary>
        internal static void Reset()
        {
            _st = _stSess = 0; _err.Clear();
            WsZero();
            Array.Clear(_rb, 0, _rb.Length); Array.Clear(_rbAge, 0, _rbAge.Length); Array.Clear(_rbMaxOver, 0, _rbMaxOver.Length); RbWhy = 0;
            ReserveZero();
            Array.Clear(_kDay, 0, _kDay.Length); Array.Clear(_kSess, 0, _kSess.Length); _kDays = 0;
            _fkBase = null; _fkDays = 0; Array.Clear(_fkSum, 0, _fkSum.Length); _fkClaim0 = double.NaN; _fkClaimPrev = double.NaN;
        }

        // ============================================================ 3. warsztaty: "brak zlota/kupca" na trzy powody
        private static int _wsStart, _wsTown, _wsWage, _wsBoth;
        private static long _wsRev, _wsWageDue;
        private static readonly HashSet<Town> _wsTowns = new HashSet<Town>();

        /// <summary>WorkshopLaw: sztuka nie zaczeta, bo kapital warsztatu nie starcza na surowiec (TryStart = 4). Sam licznik.</summary>
        internal static void WsStart() { if (On) _wsStart++; }

        /// <summary>WorkshopLaw: gotowa sztuka czeka - `townShort`: wolna kasa miasta (kasa - zapas kupcow) ponizej ceny `rev`; `wageShort`: kapital warsztatu ponizej plac.</summary>
        internal static void WsReady(Town town, bool townShort, bool wageShort, int rev, int wages)
        {
            if (!On) return;
            if (townShort && wageShort) _wsBoth++; else if (townShort) _wsTown++; else if (wageShort) _wsWage++;
            if (townShort) { _wsRev += Math.Max(0, rev); if (town != null) _wsTowns.Add(town); }
            if (wageShort) _wsWageDue += Math.Max(0, wages);
        }

        /// <summary>Dopisek linii "Warsztaty: dzien" (`total` = licznik "brak zlota/kupca" tej linii). Klucz wylaczony - pusty napis.</summary>
        internal static string WsText(int total)
        {
            if (!On) return "";
            int sum = _wsStart + _wsTown + _wsWage + _wsBoth;
            return " | 427 brak zlota/kupca wedlug powodu: sztuka nie zaczeta - kapital warsztatu ponizej kosztu surowca " + _wsStart
                   + ", gotowa czeka na kase miasta (kasa - zapas kupcow < cena sztuki) " + _wsTown + ", gotowa czeka na place (kapital warsztatu < place cyklu) " + _wsWage
                   + ", gotowa czeka na jedno i drugie " + _wsBoth + "; wartosc sztuk czekajacych na kase miasta " + _wsRev + " zl w " + _wsTowns.Count
                   + " miastach (sztuka liczona w kazdym cyklu czekania), place, na ktore nie starczylo kapitalu " + _wsWageDue
                   + " zl; kontrola: powody - 'brak zlota/kupca' = " + (sum - total) + StText();
        }

        internal static void WsZero() { _wsStart = _wsTown = _wsWage = _wsBoth = 0; _wsRev = _wsWageDue = 0; _wsTowns.Clear(); }

        // ============================================================ 4. przebudowy wsi: powod doby postoju
        internal const int SysZw = 0, SysKs = 1;
        // powody doby postoju (RbStall) - kolejnosc = kolejnosc w linii
        private const int RNoDue = 0, RNotPayDay = 1, RNoTown = 2, RPurseEmpty = 3, RPayNoFolk = 4, RPayChest = 5, RPaySmall = 6, RPayNoShelf = 7, RPayPriceCap = 8,
                          RPayOldEmpty = 9, RPartial = 10, RQuarter = 11, RArrears = 12, RKinds = 13;
        private static readonly string[] RName =
        {
            "rata jeszcze nienalezna (ponizej 0.5 zl - rece czekaja na czas)", "nie dzien raty (KS: rata co 7 dob)", "brak miasta targowego",
            "kiesa wsi pusta (cwierc kiesy = 0)", "targ bez czynnej kiesy ludu", "kiesa wsi nie ponad skrzynie DS", "rata ponizej ceny sztuki materialu (budzet < 2 zl)",
            "brak materialu na polce targu", "material tylko ponad sufit ceny", "droga dawna: wies bez zlota", "zaplacone czesciowo (materialu albo kiesy ponad skrzynie mniej niz rata)",
            "rata przycieta do cwierci kiesy (zaplacona w calosci)", "rata dnia zaplacona, zalega wczesniejsza"
        };
        // kody CraftsRebuildPay (ostatnie wywolanie; 0 = nie ustawione / zaplacone)
        internal const int WhyNone = 0, WhyOld = 1, WhySmall = 2, WhyNoFolk = 3, WhyChest = 4, WhyNoShelf = 5, WhyPriceCap = 6;
        /// <summary>TownFolk.CraftsRebuildPay: powod ostatniego zwrotu 0 / -1 (watek glowny; RbStall czyta zaraz po racie).</summary>
        internal static int RbWhy;
        private static readonly int[] _rb = new int[2 * RKinds];
        private static readonly int[] _rbAge = new int[2 * 4];       // w toku: w terminie, po terminie do 28 dob, 29-182, ponad 182
        private static readonly int[] _rbMaxOver = new int[2];

        /// <summary>
        /// Doba postoju przebudowy (postep &lt; 1 w Progress): klasyfikacja jednym powodem. `owe` - rata nalezna wedlug czasu, `ask` - o ile poproszono (po cwierci
        /// kiesy / dniu raty), `got` - ile zeszlo z kiesy wsi, `payDay` - false tylko w KS z rata co 7 dob poza dniem raty. Sam licznik.
        /// </summary>
        internal static void RbStall(int sys, double owe, int ask, long got, int vilGold, bool hasTown, bool payDay)
        {
            if (!On || sys < 0 || sys > 1) return;
            try
            {
                int r;
                if (owe < 0.5) r = RNoDue;
                else if (!payDay) r = RNotPayDay;
                else if (ask <= 0) r = RPurseEmpty;
                else if (!hasTown) r = RNoTown;
                else if (got <= 0)
                {
                    switch (RbWhy)
                    {
                        case WhyNoFolk: r = RPayNoFolk; break;
                        case WhyChest: r = RPayChest; break;
                        case WhySmall: r = RPaySmall; break;
                        case WhyPriceCap: r = RPayPriceCap; break;
                        case WhyOld: r = RPayOldEmpty; break;
                        default: r = RPayNoShelf; break;
                    }
                }
                else if (got < ask) r = RPartial;
                else if (ask < Math.Ceiling(owe) - 0.5) r = RQuarter;
                else r = RArrears;
                _rb[sys * RKinds + r]++;
            }
            catch (Exception e) { Stumble("RbStall", e); }
        }

        /// <summary>Przebudowa w toku tej doby: `over` = doby po terminie (today - TEnd; &lt;= 0 w terminie). Licznik stanu - zerowany razem z linia.</summary>
        internal static void RbAge(int sys, int over)
        {
            if (!On || sys < 0 || sys > 1) return;
            int b = over <= 0 ? 0 : over <= 28 ? 1 : over <= 182 ? 2 : 3;
            _rbAge[sys * 4 + b]++;
            if (over > _rbMaxOver[sys]) _rbMaxOver[sys] = over;
        }

        /// <summary>Dopisek linii przebudow (`stall` = licznik dob bez pelnej zaplaty tej linii, `lines` - ile dob stanu zebrala linia). Zeruje liczniki systemu.</summary>
        internal static string RbText(int sys, int stall)
        {
            if (sys < 0 || sys > 1) return "";
            if (!On) { RbZero(sys); return ""; }
            var sb = new StringBuilder(700);
            try
            {
                int sum = 0;
                sb.Append(" | 427 powod doby postoju przebudowy:");
                bool f = true;
                for (int r = 0; r < RKinds; r++)
                {
                    int n = _rb[sys * RKinds + r]; sum += n;
                    if (n == 0) continue;
                    sb.Append(f ? " " : ", ").Append(RName[r]).Append(' ').Append(n); f = false;
                }
                if (f) sb.Append(" -");
                sb.Append("; kontrola: powody - doby bez pelnej zaplaty = ").Append(sum - stall);
                int a0 = _rbAge[sys * 4], a1 = _rbAge[sys * 4 + 1], a2 = _rbAge[sys * 4 + 2], a3 = _rbAge[sys * 4 + 3];
                sb.Append("; przebudowo-doby w toku wedlug terminu (stan sumowany po dobach tej linii): w terminie ").Append(a0).Append(", po terminie do 28 dob ").Append(a1)
                  .Append(", 29-182 doby ").Append(a2).Append(", ponad 182 doby ").Append(a3).Append(" (najdluzej ").Append(_rbMaxOver[sys]).Append(" dob po terminie; porzucenie po 364)")
                  .Append(StText());
            }
            catch (Exception e) { Stumble("RbText", e); }
            RbZero(sys);
            return sb.ToString();
        }

        private static void RbZero(int sys)
        {
            Array.Clear(_rb, sys * RKinds, RKinds); Array.Clear(_rbAge, sys * 4, 4); _rbMaxOver[sys] = 0;
        }

        // ============================================================ 5. rody ponad progiem wydawania skarbu (pokoj)
        private static int _rsPeace, _rsPeaceOver, _rsWar, _rsWarOver;
        private static double _rsPeaceG, _rsExcess, _rsOverG, _rsWarExcess, _rsMaxExcess;
        private static string _rsMaxName;

        /// <summary>ClanBudget (budzet doby rodu AI, bez najemnikow): G bez lupu okna, prog = Reserve Cap Days x D + 50 000. Sam licznik.</summary>
        internal static void NoteReserve(Clan c, bool war, double g, double cap)
        {
            if (!On) return;
            double ex = g - cap;
            if (war) { _rsWar++; if (ex > 0) { _rsWarOver++; _rsWarExcess += ex; } return; }
            _rsPeace++; _rsPeaceG += Math.Max(0, g);
            if (!(ex > 0)) return;
            _rsPeaceOver++; _rsExcess += ex; _rsOverG += g;
            if (ex > _rsMaxExcess) { _rsMaxExcess = ex; try { _rsMaxName = c != null && c.Name != null ? c.Name.ToString() : "?"; } catch { _rsMaxName = "?"; } }
        }

        /// <summary>Dopisek linii "Budzet rodow (166)". Klucz wylaczony - pusty napis.</summary>
        internal static string ReserveText(Settings s)
        {
            if (!On || s == null) return "";
            return " | 427 prog wydawania skarbu (pokoj: G ponad " + Math.Max(0f, s.ReserveCapDays).ToString("0", Inv) + " x D + 50000, 1/180 nadwyzki na dobe, pol na dwor / pol na budowy): rody w pokoju "
                   + _rsPeace + " (zloto " + N0(_rsPeaceG) + "), ponad progiem " + _rsPeaceOver + " (zloto " + N0(_rsOverG) + ", nadwyzka ponad prog " + N0(_rsExcess)
                   + ", z niej dzis na dwor i budowy " + N0(_rsExcess / 180.0) + " zl; najwieksza nadwyzka " + (_rsMaxName ?? "-") + " " + N0(_rsMaxExcess)
                   + ") | rody w wojnie " + _rsWar + " - regula nie dziala; bylyby ponad progiem " + _rsWarOver + " (nadwyzka " + N0(_rsWarExcess) + ")"
                   + " [rod dluznik: dwor x0.5 i budowy 0 po tej regule - w linii wyzej]" + StText();
        }

        internal static void ReserveZero()
        {
            _rsPeace = _rsPeaceOver = _rsWar = _rsWarOver = 0; _rsPeaceG = _rsExcess = _rsOverG = _rsWarExcess = _rsMaxExcess = 0; _rsMaxName = null;
        }

        // ============================================================ 6. dlugi rad miast na krolestwa
        /// <summary>TownDebt.Line: dopisek z dlugiem rad w podziale na krolestwa (`agg`: krolestwo -> [miasta z dlugiem, dlug, Bank, lud P+R, notable]).</summary>
        internal static string DebtRealms(Dictionary<string, double[]> agg, double debtTotal)
        {
            if (!On || agg == null) return "";
            try
            {
                var l = new List<KeyValuePair<string, double[]>>(agg);
                l.Sort((x, y) => y.Value[1].CompareTo(x.Value[1]));
                var sb = new StringBuilder(400);
                sb.Append(" | 427 dlug rad na krolestwa (miasta z dlugiem / dlug: Bank, lud P+R, notable):");
                double sum = 0; bool f = true;
                foreach (var kv in l)
                {
                    sum += kv.Value[1];
                    if (kv.Value[1] < 1) continue;
                    sb.Append(f ? " " : "; ").Append(kv.Key).Append(' ').Append((int)kv.Value[0]).Append(" / ").Append(N0(kv.Value[1])).Append(": ").Append(N0(kv.Value[2]))
                      .Append(", ").Append(N0(kv.Value[3])).Append(", ").Append(N0(kv.Value[4]));
                    f = false;
                }
                if (f) sb.Append(" -");
                sb.Append("; kontrola: suma krolestw - dlug razem = ").Append(N0(sum - debtTotal)).Append(StText());
                return sb.ToString();
            }
            catch (Exception e) { Stumble("DebtRealms", e); return ""; }
        }

        /// <summary>Nazwa krolestwa osady do podzialu (bez krolestwa - rod wlasciciela w nawiasie pominiety: jedna pozycja "bez krolestwa").</summary>
        internal static string RealmOf(Settlement st)
        {
            try
            {
                if (st == null) return "bez osady";
                var k = st.OwnerClan != null ? st.OwnerClan.Kingdom : null;
                return k != null && k.Name != null ? k.Name.ToString() : "bez krolestwa";
            }
            catch { return "?"; }
        }

        // ============================================================ 7. kto rozbija bandy, karawany i tabory wsi
        private const int VBand = 0, VCaravan = 1, VVillager = 2, VKinds = 3;
        private const int DNone = 0, DLord = 1, DWatch = 2, DPlayer = 3, DPlayerClan = 4, DMerc = 5, DPatrol = 6, DGarrison = 7, DMilitia = 8, DBandit = 9, DUndead = 10,
                          DCaravan = 11, DVillager = 12, DSettlement = 13, DOther = 14, DKinds = 15;
        private static readonly string[] DName =
        {
            "bez niszczyciela (rozwiazana: powrot do domu, glod, wchlonieta, koniec zycia partii)", "lordowie AI", "Nocna Straz", "gracz", "partie rodu gracza",
            "najemnicy i rody pomniejsze", "patrole", "zalogi", "milicja", "bandy", "umarli", "karawany", "tabory wsi", "osada", "inne"
        };
        private static readonly string[] VName = { "bandy", "karawany", "tabory wsi" };
        private static readonly int[] _kDay = new int[VKinds * DKinds];
        private static readonly int[] _kSess = new int[VKinds * DKinds];
        private static int _kDays;

        /// <summary>CampaignEvents.MobilePartyDestroyed: licznik "kto" dla band (bez umarlych), karawan i taborow wsi. Niczego nie zmienia.</summary>
        internal static void OnPartyDestroyed(MobileParty party, PartyBase destroyer)
        {
            if (!On || party == null) return;
            try
            {
                int v;
                if (party.IsCaravan) v = VCaravan;
                else if (party.IsVillager) v = VVillager;
                else if (party.IsBandit) { if (Undead.Party(party)) return; v = VBand; }
                else return;
                int d = Who(destroyer);
                _kDay[v * DKinds + d]++; _kSess[v * DKinds + d]++;
            }
            catch (Exception e) { Stumble("OnPartyDestroyed", e); }
        }

        private static int Who(PartyBase destroyer)
        {
            if (destroyer == null) return DNone;
            var mp = destroyer.MobileParty;
            if (mp == null) return destroyer.Settlement != null ? DSettlement : DOther;
            if (mp.IsMainParty) return DPlayer;
            if (Undead.Party(mp)) return DUndead;
            if (mp.IsBandit) return DBandit;
            if (mp.IsLordParty)
            {
                if (CrownGifts.IsWatch(mp.MapFaction)) return DWatch;
                var c = mp.ActualClan;
                if (c != null && c == Clan.PlayerClan) return DPlayerClan;
                if (c != null && (c.IsMinorFaction || c.IsUnderMercenaryService)) return DMerc;
                return DLord;
            }
            if (mp.IsPatrolParty) return DPatrol;
            if (mp.IsGarrison) return DGarrison;
            if (mp.IsMilitia) return DMilitia;
            if (mp.IsCaravan) return DCaravan;
            if (mp.IsVillager) return DVillager;
            return DOther;
        }

        private static void KillPart(StringBuilder sb, int[] a, int v)
        {
            int tot = 0; for (int d = 0; d < DKinds; d++) tot += a[v * DKinds + d];
            sb.Append(tot);
            if (tot == 0) return;
            sb.Append(" (");
            bool f = true;
            for (int d = 1; d <= DKinds; d++)
            {
                int k = d % DKinds;                                  // "bez niszczyciela" na koncu
                int n = a[v * DKinds + k];
                if (n == 0) continue;
                sb.Append(f ? "" : ", ").Append(DName[k]).Append(' ').Append(n); f = false;
            }
            sb.Append(')');
        }

        /// <summary>ArmouryBehavior.OnDailyTick (po OutlawLaw.Daily i BanditLimit.Daily): linia "Kto rozbija bandy (427)" i zerowanie licznikow doby.</summary>
        internal static void DailyKills(int day)
        {
            try
            {
                if (!On) { Array.Clear(_kDay, 0, _kDay.Length); return; }
                _kDays++;
                var s = Settings.Current;
                if (s != null && s.LogEnabled && LogGate.Lines)
                {
                    var sb = new StringBuilder(900);
                    sb.Append("Kto rozbija bandy (427): dzien ").Append(day).Append(" | sam pomiar (zdarzenie znikniecia partii; 'niszczyciel' = wodz strony zwyciezcow bitwy wedlug gry, "
                              + "nie kazda partia, ktora walczyla; umarli poza)");
                    for (int v = 0; v < VKinds; v++)
                    {
                        sb.Append(" | ").Append(VName[v]).Append(" znikle dzis ");
                        KillPart(sb, _kDay, v);
                        sb.Append(", od wczytania (").Append(_kDays).Append(" dob) ");
                        KillPart(sb, _kSess, v);
                    }
                    sb.Append(" | potkniecia ").Append(_st).Append(" (sesja ").Append(_stSess).Append(").");
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("DailyKills", e); }
            Array.Clear(_kDay, 0, _kDay.Length); _st = 0;
        }

        // ============================================================ 9. kiesy ludu - rozbicie zmiany
        // posiadacze ksiegi pieniadza (MoneyLedger.A427Folk): lud miast, podzamcza, gminy, wsie
        private const int FTown = 0, FCastle = 1, FGmina = 2, FVill = 3, FKinds = 4;
        private static readonly string[] FName = { "lud miast (W/R/P)", "podzamcza", "gminy", "wsie" };
        // nazwane przeplywy (sumy od wczytania): danina od skarbca ludu miast i podzamczy, danina od skarbca wsi, czesc firmy pana z oszczednosci ludu, zakupy ludu,
        // dochod ludu z kas (robocizna + obrot P + doplata)
        private const int SHoard = 0, SHoardVill = 1, SFarmFolk = 2, SFromCoffers = 3, SKinds = 4;
        private static long[] _fkBase;
        private static int _fkDays;
        private static readonly long[] _fkSum = new long[SKinds];
        private static double _fkClaim0 = double.NaN, _fkClaimPrev = double.NaN;

        /// <summary>
        /// MoneyLedger.Daily (zaraz po linii "Pompy"): linia "Kiesy ludu - rozbicie zmiany (427)". `now` / `last` - [lud miast, podzamcza, gminy, wsie, swiat] z ksiegi
        /// pieniadza na koniec tej i poprzedniej doby (`last` null - pierwsza doba ksiegi po wczytaniu). Tylko odczyt licznikow innych modulow z tej samej doby.
        /// </summary>
        internal static void FolkLine(int day, long[] now, long[] last)
        {
            if (!On || now == null || now.Length < FKinds + 1) return;
            try
            {
                if (_fkBase == null) { _fkBase = (long[])(last ?? now).Clone(); _fkDays = 0; }
                if (last != null) _fkDays++;
                long hoard = 0, hoardVill = 0, farmFolk = 0, coffers = 0;
                try { hoard = TownFolk.A427HoardTake; hoardVill = TownFolk.A427VillHoardTake; } catch (Exception e) { Stumble("FolkLine(daniny)", e); }
                try { farmFolk = DsTowns.A427PaidFolk; } catch (Exception e) { Stumble("FolkLine(firma)", e); }
                try { coffers = TownFolk.LastLabourTowns + TownFolk.LastTurnTowns + TownFolk.LastTopUpTowns + TownFolk.LastLabourCastles; } catch (Exception e) { Stumble("FolkLine(kasy)", e); }
                if (last != null) { _fkSum[SHoard] += hoard; _fkSum[SHoardVill] += hoardVill; _fkSum[SFarmFolk] += farmFolk; _fkSum[SFromCoffers] += coffers; }
                double cP = 0, cR = 0; bool claimOk = false;
                try { claimOk = TownDebt.A427FolkClaims(out cP, out cR); } catch (Exception e) { Stumble("FolkLine(kwity)", e); }
                double claim = cP + cR;
                if (claimOk && double.IsNaN(_fkClaim0)) _fkClaim0 = claim;
                var s = Settings.Current;
                if (s != null && s.LogEnabled && LogGate.Lines)
                {
                    long tot = 0, tot0 = 0, totL = 0;
                    var sb = new StringBuilder(1100);
                    sb.Append("Kiesy ludu - rozbicie zmiany (427): dzien ").Append(day).Append(" | sam pomiar | kiesy (stan, zmiana doby, zmiana od wczytania - ").Append(_fkDays).Append(" dob):");
                    for (int i = 0; i < FKinds; i++)
                    {
                        tot += now[i]; tot0 += _fkBase[i]; if (last != null) totL += last[i];
                        sb.Append(i == 0 ? " " : "; ").Append(FName[i]).Append(' ').Append(now[i]).Append(" (").Append(last != null ? Sg(now[i] - last[i]) : "-").Append(", ")
                          .Append(Sg(now[i] - _fkBase[i])).Append(')');
                    }
                    sb.Append("; razem ").Append(tot).Append(" (").Append(last != null ? Sg(tot - totL) : "-").Append(", ").Append(Sg(tot - tot0)).Append(") = ")
                      .Append(Pc(tot, now[FKinds])).Append(" swiata")
                      .Append(" | nazwane odplywy z kies do panow, kas osad i koron (dzis / od wczytania): danina od skarbca ludu miast i podzamczy ").Append(hoard).Append(" / ").Append(_fkSum[SHoard])
                      .Append(", danina od skarbca wsi ").Append(hoardVill).Append(" / ").Append(_fkSum[SHoardVill])
                      .Append(", czesc firmy pana z oszczednosci ludu (okno linii Firmy DS) ").Append(farmFolk).Append(" / ").Append(_fkSum[SFarmFolk])
                      .Append("; razem ").Append(hoard + hoardVill + farmFolk).Append(" / ").Append(_fkSum[SHoard] + _fkSum[SHoardVill] + _fkSum[SFarmFolk])
                      .Append(" | zwrot z kas osad do ludu (robocizna, obrot P, doplata do 35%, robocizna zamkow) ").Append(coffers).Append(" / ").Append(_fkSum[SFromCoffers])
                      .Append(" | kwity ludu wobec rad miast (pozyczka to kiesa -> kwit, nie strata): P ").Append(claimOk ? N0(cP) : "-").Append(", R ").Append(claimOk ? N0(cR) : "-")
                      .Append(", razem ").Append(claimOk ? N0(claim) : "-").Append(" (").Append(claimOk && !double.IsNaN(_fkClaimPrev) ? Sg((long)Math.Round(claim - _fkClaimPrev)) : "-")
                      .Append(", ").Append(claimOk && !double.IsNaN(_fkClaim0) ? Sg((long)Math.Round(claim - _fkClaim0)) : "-").Append(")")
                      .Append(" | majatek ludu miast = kiesy + kwity ").Append(claimOk ? N0(now[FTown] + claim) : "-").Append(" (od wczytania ")
                      .Append(claimOk && !double.IsNaN(_fkClaim0) ? Sg((long)Math.Round(now[FTown] - _fkBase[FTown] + claim - _fkClaim0)) : "-").Append(")")
                      .Append(" | reszta zmiany kies (zakupy, dochod z zewnatrz, podatki stale, pozyczki rodom i koronom) - linie 'Kiesa ludu', 'Dlugi miast', 'Pozyczki miejskie (f8)'")
                      .Append(" | potkniecia od wczytania ").Append(_stSess).Append('.');
                    Log.Info(sb.ToString());
                }
                if (claimOk) _fkClaimPrev = claim;
            }
            catch (Exception e) { Stumble("FolkLine", e); }
        }
    }
}
