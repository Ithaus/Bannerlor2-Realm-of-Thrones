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
    /// 433 (audyt ekonomii 11.11: C4 + C1 + C2; decyzja Jeffa 6 "tak wiecej zbojcow jak bieda") - ZBOJCY I RYZYKO.
    ///  C4. BIEDA RODZI WIECEJ ZBOJCOW (klucz Outlaw Poverty Rises Bands; kod reguly w OutlawLaw: MiseryCore / Extra433 / Daily). Audyt: kanal biedy to 10.8
    ///      ludzi na dobe na caly swiat (6% naplywu puli), prawie staly, i czyta tylko dobrobyt i bezpieczenstwo warowni z gry. Teraz jedna miara nedzy w OBU
    ///      miejscach wzoru (OutlawLaw.MiseryOf - ochotnicy Levy, i naplyw doby OutlawLaw.Daily): bieda liczona Outlaw Poverty Scale razy ciezej + glod wsi
    ///      (Famine) + uchodzcy spustoszenia poza domem (PeopleBook) + lud warowni bez monety na jedzenie (TownFolk.MoneyHungry). Ludzie jak dotad schodza
    ///      z domow wsi regionu do puli regionu (TakeCommoners -> PeopleBook.Move, przyczyna "bieda i wojna") - nikt z niczego. Hamulce: lad (bezpieczenstwo
    ///      zdejmuje czesc dodatku), sufit dodatku na region, pelna pula regionu (prostych w lesie >= Outlaw Poverty Pool Cap Per Hearth x hearth - tylko stary
    ///      naplyw); dalej dzialaja stare: wies nie schodzi ponizej 50 hearth, powrot do domu z pokojem i dobrobytem, glodna banda nie werbuje.
    ///  C1. KARAWANA WYCENIA RYZYKO TRASY (klucz Caravans Price Road Risk; CaravanWait139.Route): do kosztu drogi 139 dochodzi koszt ryzyka = (kiesa + towar po
    ///      koszcie) x min(1, stawka dobowa x dni) x (banda - sila) / banda; banda = najsilniejsza banda w dniu drogi wozu od odcinka (CaravanGuards285 - miara
    ///      strazy 286), sila = EstimatedStrength karawany. Karawana nie slabsza od bandy - 0. Tylko wybor celu (nic nie plynie); oceny BK bez zmian.
    ///  C2. KUPIEC CZEKA PO ROZBICIU KARAWANY (klucz Caravan Loss Wait Days, 0 = jak dotad): karawana AI zniszczona Z NISZCZYCIELEM (bitwa; nasze rozwiazania
    ///      ida bez niszczyciela) - wlasciciel (kupiec albo lord) przez N dob nie wystawia nowej (CaravanFlow.NotableCapPostfix / LordBuyPostfix). Rod gracza,
    ///      konwoje DTE i karawany bez wlasciciela poza regula. Pamiec w zapisie (arm_karawany433: id bohatera -> doba straty); brak klucza = stan pusty.
    /// Linia doby "Zbojcy i ryzyko (433)" z kontrolami: C4 wzieci z domow - dopisani do puli = 0 i wzieci - zdjeci z konta DOM w ksiedze ludzi = 0 (433-p:
    /// hearth zwrocony przez PeopleBook.Move / stawka wsi - niezalezny od licznika puli); C2 wpisy pamieci = wczoraj + nowe - wygasle.
    /// 433-p (po recenzjach): glod wsi liczy sie dopiero ponad prog glodu (racjonowanie planu zimy to nie nedza); Outlaw Poverty Also Recruits domyslnie NIE
    /// (decyzja Jeffa 6 mowi o zbojcach, nie o poborze); pelna pula (0.06 / hearth) to bezpiecznik awaryjny, nie hamulec dnia; karawana z zadania poza C2.
    /// Znane i zostawione: C1 liczy sile karawany SPRZED najmu strazy 286 (koszt ryzyka zawyzony dla tras, na ktore i tak dokupi straz) i nie dziala przy
    /// zmianie celu w polu (brak osady startu - licznik "bez osady startu").
    /// Wyjatek = potkniecie liczone w linii, bez globalnego wylacznika (CLAUDE.md 7). Watek glowny (takt doby i zdarzenia kampanii); Extra433 moze isc z modelu
    /// ochotnikow - tam tylko czyta i w razie wyjatku liczy potkniecie.
    /// </summary>
    internal static class Risk433
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly char[] Bad = { '|', ':' };

        /// <summary>C4: skladniki dodatku nedzy regionu po hamulcach ladu i sufitu (suma = dodatek) oraz to, co hamulce zdjely.</summary>
        internal struct Parts { public float Pov, Hunger, Ref, Purse, Cut; public bool Capped; }

        private static int _st, _stSess;
        private static readonly HashSet<string> _err = new HashSet<string>();
        internal static void Stumble(string where, Exception e)
        {
            System.Threading.Interlocked.Increment(ref _st); System.Threading.Interlocked.Increment(ref _stSess);
            try { lock (_err) { if (!_err.Add(where)) return; } Log.Error("Risk433." + where, e); } catch { }
        }

        // ------------------------------------------------------------ C4: liczniki doby (pisze OutlawLaw.Daily)
        private static int _c4Regions, _c4Capped, _c4Full, _c4Flow;
        private static double _c4Old, _c4Want, _c4Took, _c4Pool, _c4Hearth, _c4Pov, _c4Hunger, _c4Ref, _c4Purse, _c4Cut, _c4FullLost;
        private static double _c4Book, _c4Common, _c4CapSum;     // 433-p: zdjeci z DOM w ksiedze; prosci w puli przed naplywem; prog pelnej puli (suma regionow)
        private static readonly string[] _topName = new string[3];
        private static readonly float[] _topTook = new float[3];
        private static readonly float[] _topMis = new float[3];

        /// <summary>C4 (OutlawLaw.Daily, raz na region na dobe): stary naplyw, dodatek rozpisany na skladniki, hamulce, wzieci z domow, przyrost prostych w puli
        /// i hearth zdjety z wsi (kontrola linii). 433-p: bookMen - ludzie realnie zdjeci z konta DOM (hearth z PeopleBook.Move / stawka wsi), commoners -
        /// prosci regionu w puli przed naplywem, capMen - prog pelnej puli regionu (0 = hamulec wylaczony).</summary>
        internal static void NoteRegion(Settlement r, float inflowOld, float unit, float extra, ref Parts p, bool full, float want, float took, float poolDelta, double hearth, float misery,
                                         double bookMen, float commoners, float capMen)
        {
            try
            {
                _c4Regions++;
                _c4Book += bookMen; _c4Common += commoners; _c4CapSum += capMen;
                _c4Old += inflowOld; _c4Want += want; _c4Took += took; _c4Pool += poolDelta; _c4Hearth += hearth;
                _c4Cut += unit * p.Cut;
                if (p.Capped) _c4Capped++;
                if (full) { _c4Full++; _c4FullLost += unit * extra; }
                else { _c4Pov += unit * p.Pov; _c4Hunger += unit * p.Hunger; _c4Ref += unit * p.Ref; _c4Purse += unit * p.Purse; }
                if (took > 0f)
                {
                    _c4Flow++;
                    for (int i = 0; i < 3; i++)
                    {
                        if (!(took > _topTook[i])) continue;
                        for (int j = 2; j > i; j--) { _topTook[j] = _topTook[j - 1]; _topMis[j] = _topMis[j - 1]; _topName[j] = _topName[j - 1]; }
                        _topTook[i] = took; _topMis[i] = misery; _topName[i] = r != null && r.Name != null ? r.Name.ToString() : "?";
                        break;
                    }
                }
            }
            catch (Exception e) { Stumble("NoteRegion", e); }
        }

        // ------------------------------------------------------------ C1: ryzyko trasy
        private static int _c1Eval, _c1NoFrom, _c1Risky, _c1Dec, _c1Changed;
        private static double _c1Cost, _c1P, _c1Stake;

        internal static bool RouteOn(Settings s) { return s != null && s.CaravanRouteRisk && s.CaravanRouteRiskPerDayPercent > 0f; }

        /// <summary>C1: koszt ryzyka drogi from - to w zlocie (0, gdy karawana nie jest slabsza od najsilniejszej bandy drogi; 0 bez osady startu). Tylko odczyt.</summary>
        internal static double RouteCost(MobileParty mp, Settlement from, Settlement to, double days, double stake, float str, Settings s)
        {
            try
            {
                _c1Eval++;
                if (mp == null || from == null || to == null || from == to) { _c1NoFrom++; return 0.0; }
                float threat = CaravanGuards285.RoadThreat433(from, to, mp.HasNavalNavigationCapability, s);
                if (!(threat > str) || !(stake > 0.0)) return 0.0;
                double p = Math.Min(1.0, s.CaravanRouteRiskPerDayPercent / 100.0 * Math.Max(0.0, days)) * (threat - Math.Max(0f, str)) / threat;
                if (!(p > 0.0)) return 0.0;
                double k = stake * p;
                _c1Risky++; _c1Cost += k; _c1P += p; _c1Stake += stake;
                return k;
            }
            catch (Exception e) { Stumble("RouteCost", e); return 0.0; }
        }

        /// <summary>C1 (CaravanWait139.Route): decyzja celu z towarem; changed - wybor z ryzykiem inny niz bez ryzyka.</summary>
        internal static void NoteDecision(bool changed) { _c1Dec++; if (changed) _c1Changed++; }

        // ------------------------------------------------------------ C2: wlasciciel rozbitej karawany czeka
        private static readonly Dictionary<string, int> _lost = new Dictionary<string, int>();   // id bohatera-wlasciciela -> doba straty (zapis arm_karawany433)
        private static readonly HashSet<string> _refWho = new HashSet<string>();
        private static int _c2Lost, _c2LostBand, _c2LostSess, _c2Player, _c2Dte, _c2NoOwner, _c2Added, _c2Renewed, _c2RefNotable, _c2RefLord, _c2New, _c2Quest;
        private static int _c2Last = -1, _c2Cars = -1, _importN, _importBad;

        /// <summary>C2: zdarzenie MobilePartyDestroyed - karawana AI zniszczona z niszczycielem: doba straty wlasciciela do pamieci. Partii nie rusza.</summary>
        internal static void OnPartyDestroyed(MobileParty party, PartyBase destroyer)
        {
            if (party == null || destroyer == null) return;
            try
            {
                if (!party.IsCaravan) return;
                var s = Settings.Current;
                if (s == null || s.CaravanLossWaitDays <= 0 || Campaign.Current == null) return;
                // 433-p (rec. ksiegi 6): karawana z zadania (gra niszczy ja sama po zasadzce: niszczyciel = ta sama partia) to nie rozboj - zleceniodawca nie czeka
                if (destroyer.MobileParty == party || party.IsCurrentlyUsedByAQuest) { _c2Quest++; return; }
                if (Measure31.IsDteConvoy(party)) { _c2Dte++; return; }
                Hero owner = null;
                try { owner = party.CaravanPartyComponent != null ? party.CaravanPartyComponent.Owner : party.Owner; } catch { owner = null; }
                if (owner == null || owner.StringId == null || owner.StringId.IndexOfAny(Bad) >= 0) { _c2NoOwner++; return; }
                if (owner == Hero.MainHero || (owner.Clan != null && owner.Clan == Clan.PlayerClan)) { _c2Player++; return; }
                _c2Lost++; _c2LostSess++;
                if (destroyer.MobileParty != null && destroyer.MobileParty.IsBandit) _c2LostBand++;
                if (_lost.ContainsKey(owner.StringId)) _c2Renewed++; else _c2Added++;
                _lost[owner.StringId] = (int)CampaignTime.Now.ToDays;
            }
            catch (Exception e) { Stumble("OnPartyDestroyed", e); }
        }

        /// <summary>C2: zdarzenie MobilePartyCreated - nowe karawany doby (sam licznik).</summary>
        internal static void OnPartyCreated(MobileParty party)
        {
            try { if (party != null && party.IsCaravan) _c2New++; } catch (Exception e) { Stumble("OnPartyCreated", e); }
        }

        /// <summary>C2: wlasciciel stracil karawane mniej niz Caravan Loss Wait Days dob temu - nowej nie wystawia (true). Klucz 0 albo pusta pamiec - false.</summary>
        internal static bool Waits(Hero h, bool lord)
        {
            if (h == null || _lost.Count == 0) return false;
            try
            {
                var s = Settings.Current;
                if (s == null || s.CaravanLossWaitDays <= 0 || Campaign.Current == null) return false;
                string id = h.StringId; int d;
                if (id == null || !_lost.TryGetValue(id, out d)) return false;
                if ((int)CampaignTime.Now.ToDays - d >= s.CaravanLossWaitDays) return false;
                if (lord) _c2RefLord++; else _c2RefNotable++;
                _refWho.Add(id);
                return true;
            }
            catch (Exception e) { Stumble("Waits", e); return false; }
        }

        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(_lost.Count * 24 + 4);
                foreach (var kv in _lost)
                {
                    if (kv.Key.IndexOfAny(Bad) >= 0) continue;
                    if (sb.Length > 0) sb.Append('|');
                    sb.Append(kv.Key).Append(':').Append(kv.Value.ToString(Inv));
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _lost.Clear(); _importN = 0; _importBad = 0; _c2Last = -1;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                foreach (var part in data.Split('|'))
                {
                    if (part.Length == 0) continue;
                    var a = part.Split(':');
                    int d;
                    if (a.Length == 2 && a[0].Length > 0 && int.TryParse(a[1], NumberStyles.Integer, Inv, out d)) { _lost[a[0]] = d; _importN++; }
                    else _importBad++;
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        /// <summary>Nowa kampania / przed wczytaniem (konstruktor ArmouryBehavior): pamiec i liczniki od zera.</summary>
        internal static void Reset()
        {
            _lost.Clear(); _c2Last = -1; _c2Cars = -1; _c2LostSess = 0; _importN = _importBad = 0;
            _st = _stSess = 0; lock (_err) _err.Clear();
            ZeroDay();
        }

        private static void ZeroDay()
        {
            _st = 0;
            _c4Regions = _c4Capped = _c4Full = _c4Flow = 0;
            _c4Old = _c4Want = _c4Took = _c4Pool = _c4Hearth = _c4Pov = _c4Hunger = _c4Ref = _c4Purse = _c4Cut = _c4FullLost = 0;
            _c4Book = _c4Common = _c4CapSum = 0;
            for (int i = 0; i < 3; i++) { _topName[i] = null; _topTook[i] = 0f; _topMis[i] = 0f; }
            _c1Eval = _c1NoFrom = _c1Risky = _c1Dec = _c1Changed = 0; _c1Cost = _c1P = _c1Stake = 0;
            _c2Lost = _c2LostBand = _c2Player = _c2Dte = _c2NoOwner = _c2Added = _c2Renewed = _c2RefNotable = _c2RefLord = _c2New = _c2Quest = 0;
            _refWho.Clear();
        }

        private static string F(double v, string f) { return v.ToString(f, Inv); }

        /// <summary>Takt doby (po OutlawLaw.Daily i BanditLimit.Daily): wygasle wpisy C2, linia "Zbojcy i ryzyko (433)", zerowanie licznikow doby.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int today = (int)CampaignTime.Now.ToDays;
            var sb = new StringBuilder(1400);
            try
            {
                // C2: wygasle wpisy (takze przy kluczu 0 - pamiec nie rosnie bez granic)
                int wait = Math.Max(0, s.CaravanLossWaitDays), expired = 0;
                List<string> gone = null;
                foreach (var kv in _lost)
                    if (wait <= 0 || today - kv.Value >= wait) { if (gone == null) gone = new List<string>(); gone.Add(kv.Key); }
                if (gone != null) { foreach (var id in gone) _lost.Remove(id); expired = gone.Count; }
                int cars = 0, bands = 0, bandMen = 0;
                foreach (var mp in MobileParty.AllCaravanParties) if (mp != null && mp.IsActive) cars++;
                foreach (var mp in MobileParty.AllBanditParties) { if (mp == null || !mp.IsActive) continue; bands++; bandMen += mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0; }

                sb.Append("Zbojcy i ryzyko (433): dzien ").Append(today);
                // ---- C4
                sb.Append(" | C4 bieda: klucz ").Append(s.OutlawPovertyRisesBands ? "TAK" : "NIE");
                if (s.OutlawPovertyRisesBands)
                {
                    double add = _c4Pov + _c4Hunger + _c4Ref + _c4Purse;
                    sb.Append(" (bieda x").Append(F(s.OutlawPovertyScale, "0.00")).Append(", takze ochotnicy ").Append(s.OutlawPovertyAlsoRecruits ? "TAK" : "NIE")
                      .Append(") | regionow ").Append(_c4Regions).Append(", naplyw wedlug wzoru sprzed 433 ").Append(F(_c4Old, "0.0")).Append("/d, wedlug 433 ").Append(F(_c4Want, "0.0"))
                      .Append("/d (x").Append(_c4Old > 0 ? F(_c4Want / _c4Old, "0.00") : "-").Append(") = stary ").Append(F(_c4Old, "0.0")).Append(" + bieda ").Append(F(_c4Pov, "0.0"))
                      .Append(" + glod wsi ").Append(F(_c4Hunger, "0.0")).Append(" + uchodzcy ").Append(F(_c4Ref, "0.0")).Append(" + lud bez monety ").Append(F(_c4Purse, "0.0"))
                      .Append(" (rachunek: ").Append(F(_c4Want - _c4Old - add, "0.00")).Append(")")
                      .Append("; hamulce: lad i sufit zdjely ").Append(F(_c4Cut, "0.0")).Append(" (sufit w ").Append(_c4Capped).Append(" regionach), pelna pula w ").Append(_c4Full)
                      .Append(" regionach (bez dodatku ").Append(F(_c4FullLost, "0.0")).Append("; bezpiecznik awaryjny: prostych w puli ").Append(F(_c4Common, "0"))
                      .Append(", prog swiata ").Append(_c4CapSum > 0 ? F(_c4CapSum, "0") : "-").Append(")")
                      .Append(" | wzieci z domow ").Append(F(_c4Took, "0.0")).Append(" w ").Append(_c4Flow).Append(" regionach (chciano ").Append(F(_c4Want, "0.0"))
                      .Append("; wies nie schodzi ponizej 50 hearth), kontrola: wzieci - dopisani do puli = ").Append(F(_c4Took - _c4Pool, "0.00"))
                      .Append(", wzieci - zdjeci z domow w ksiedze ludzi (hearth z Move / stawka wsi) = ").Append(F(_c4Took - _c4Book, "0.00"))
                      .Append(", hearth wsi -").Append(F(_c4Hearth, "0.###")).Append(" | najwiecej: ");
                    bool any = false;
                    for (int i = 0; i < 3; i++)
                    {
                        if (_topName[i] == null) continue;
                        sb.Append(any ? ", " : "").Append(_topName[i].Replace("|", "/")).Append(' ').Append(F(_topTook[i], "0.00")).Append(" (nedza ").Append(F(_topMis[i], "0.00")).Append(')');
                        any = true;
                    }
                    if (!any) sb.Append('-');
                }
                sb.Append(" | band ").Append(bands).Append(", ludzi ").Append(bandMen);
                // ---- C1
                sb.Append(" | C1 ryzyko trasy: klucz ").Append(RouteOn(s) ? "TAK" : "NIE");
                if (RouteOn(s))
                    sb.Append(" (").Append(F(s.CaravanRouteRiskPerDayPercent, "0.00")).Append("%/dobe drogi) | wycen tras ").Append(_c1Eval).Append(", w tym banda silniejsza od karawany ")
                      .Append(_c1Risky).Append(" (sredni koszt ryzyka ").Append(_c1Risky > 0 ? F(_c1Cost / _c1Risky, "0") : "-").Append(" zl, stawka ")
                      .Append(_c1Risky > 0 ? F(_c1Stake / _c1Risky, "0") : "-").Append(" zl, szansa straty ").Append(_c1Risky > 0 ? F(100.0 * _c1P / _c1Risky, "0.00") : "-")
                      .Append("%), bez osady startu ").Append(_c1NoFrom).Append(" | decyzji z towarem ").Append(_c1Dec).Append(", wybor zmieniony przez ryzyko ").Append(_c1Changed);
                // ---- C2
                sb.Append(" | C2 czekanie po rozbiciu: klucz ").Append(wait > 0 ? wait.ToString(Inv) + " dob" : "0 (NIE)");
                if (wait > 0)
                {
                    sb.Append(" | rozbite karawany AI ").Append(_c2Lost).Append(" (przez bandy ").Append(_c2LostBand).Append(", inni ").Append(_c2Lost - _c2LostBand)
                      .Append("; poza regula: rodu gracza ").Append(_c2Player).Append(", konwoje DTE ").Append(_c2Dte).Append(", bez wlasciciela ").Append(_c2NoOwner).Append(", z zadania ").Append(_c2Quest)
                      .Append("), od wczytania ").Append(_c2LostSess)
                      .Append(" | czeka wlascicieli ").Append(_lost.Count).Append(", odmowy nowej karawany: kupcy ").Append(_c2RefNotable).Append(", lordowie ").Append(_c2RefLord)
                      .Append(" (osob ").Append(_refWho.Count).Append(")");
                    sb.Append(" | kontrola pamieci: ");
                    if (_c2Last < 0) sb.Append("pierwsza doba sesji (z zapisu ").Append(_importN).Append(", zle wpisy ").Append(_importBad).Append(")");
                    else sb.Append("wpisy ").Append(_lost.Count).Append(" = wczoraj ").Append(_c2Last).Append(" + nowe ").Append(_c2Added).Append(" - wygasle ").Append(expired)
                           .Append(" (ponowna strata ").Append(_c2Renewed).Append("), roznica ").Append(_lost.Count - (_c2Last + _c2Added - expired));
                }
                sb.Append(" | karawan ").Append(cars).Append(_c2Cars >= 0 ? " (wczoraj " + _c2Cars.ToString(Inv) + ")" : "").Append(", nowe dzis ").Append(_c2New);
                sb.Append(" | potkniecia ").Append(_st).Append(" (sesja ").Append(_stSess).Append(").");
                _c2Last = _lost.Count; _c2Cars = cars;
            }
            catch (Exception e)
            {
                Stumble("Daily", e);
                sb.Clear();
                sb.Append("Zbojcy i ryzyko (433): dzien ").Append(today).Append(" | blad linii | potkniecia ").Append(_st).Append(" (sesja ").Append(_stSess).Append(").");
            }
            Log.Info(sb.ToString());
            ZeroDay();
        }
    }
}
