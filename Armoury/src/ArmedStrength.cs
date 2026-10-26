using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// p9 - SILA Z UZBROJENIEM (Jeff 25.10, bitwa 108972: 55 ludzi Lady Darlessy Marbrand w tunikach z domu i z kijem zaatakowalo 46 ludzi gracza - zabici 24,
    /// ranni 86, gracz 0; "zolnierz taki nie nadaje sie do bitwy, przeciez wiadomo, ze przegra"). Klucz AiStrengthCountsGear (MCM "AI Strength Counts Gear").
    ///
    /// DLACZEGO AI NIE WIDZI SPRZETU (dekompilacja 1.4.8, sprawdzone): kazda ocena sily na mapie idzie przez MilitaryPowerModel.GetPowerOfParty - PartyBase.
    /// EstimatedStrength (pamiec wedlug MemberRoster.VersionNo), CalculateCurrentStrength, GetCustomStrength, MobileParty.GetTotalLandStrengthWithFollowers, Army.
    /// EstimatedStrength - a ta liczy tylko tier ludzi: P(t) = (2+t)(10+t) x 0.02 (DefaultMilitaryPowerModel.GetDefaultTroopPower; ROT i NavalDLC przekazuja dalej).
    /// StrategicCampaignAI145 (progi 35/80/260), BK (BKArmyManagementModel), ROT i nasze ArmyRestDay/BesiegedRelief/NightMarch czytaja te same liczby. Partia Darlessy
    /// (nowa po okupie, bez wpisu w zbrojowniach DTE - 201 pustych slotow) miala w grze sile 142 wobec 66 gracza (2.16) i zaatakowala.
    ///
    /// REGULA (jedna dla gracza i AI): postfiks Priority.Last na GetPowerOfParty CZYNNEGO modelu (PatchTarget.OfActive - klasa deklarujaca, dzis ROTMilitaryPowerModel;
    /// tylko najbardziej zewnetrzny model, wiec mnozenie jest raz), wpinany w kampanii (OnSessionLaunched; pola statyczne DefaultMilitaryPowerModel - pulapka
    /// konstruktora statycznego). Wynik x F(partia), gdy kontekst Estimated albo partia poza bitwa (MapEvent == null). Rozliczen w toku bitwy (GetCustomStrength
    /// z SimulationContext: slawa i wplywy, udzial, morale strony, poswiecenie ludzi) NIE ruszamy. Lup DTE AI-AI (EveryoneCampaignBehavior.OnMapEventEnded dzieli
    /// sztuki wedlug EstimatedStrength) - bez mnoznika: prefiks/finalizer z flaga [ThreadStatic] i wyzerowana pamiecia sily uczestnikow (przed i po).
    /// Partie w regule: lordowie (GarrisonKit.LordRule - takze towarzysze i partie Twojego rodu) i druzyna gracza (zbrojownia QuartermasterLaw.DteArmory, ten sam
    /// algorytm) przy TroopsFightWithOwnKitOnly, zalogi przy GarrisonKit.BareOn (inaczej walcza we wzorcu - F = 1). Reszta F = 1: bandy, karawany, wozy, milicja,
    /// umarli Nocnego Krola; bohaterowie bez kary (sprzet wlasny). Zbrojownie DTE nieczytelne - F = 1 (nie karzemy za blad odczytu), licznik w linii.
    ///
    /// GOTOWOSC R = GarrisonKit.FillPercent(partia, zbrojownia, battle: true) - % slotow wzorca (bez koni) obsadzonych ze zbrojowni, tylko zdrowi; ta sama miara
    /// co progi 75 (GarrisonArmoryMinFillPercent) i 60 (ClanFillOnce). Brak wpisu DTE = 0.
    /// MNOZNIK F: g = min(1, R / Knee), Knee = AiStrengthFullKitPercent (75). Zwykly zolnierz: g x P(t) + (1-g) x P0, P0 = 0.40 (tier 0 - wiesniak w tunice z kijem,
    /// dokladnie to, co m117/m119 daja mu z domu). F = (Pstale + suma zwyklych z kara) / (Pstale + suma zwyklych), Pstale = bohaterowie (P x 1.5 z modelu) i dodatki
    /// czynnego modelu ponad tier (ROT: smok +100). Przyklad Jeffa: sredni tier 2.3-2.5 -> F 0.36-0.38, sila AI 142 x 0.38 = 54 wobec 66 gracza - gra atakuje tylko
    /// przy przewadze > 1 (DefaultMobilePartyAIModel.CalculateInitiativeScoresForEnemy), a avoidScore rosnie z 1/przewaga - ucieka.
    ///
    /// WYDAJNOSC: postfiks to odczyt z niezmiennego slownika MBGUID -> Info (publikacja co godzine, copy-on-write - bezpieczne przy watkach roboczych).
    /// R i F liczone w DailyTickParty (gra rozklada na dobe, watek glowny) i co godzine dla "brudnych" (zakup AiGear, koniec bitwy, nowa partia, zmiana skladu
    /// o >= max(3, 10%) zdrowych, zmiana zbrojowni gracza), najwyzej HourCap na godzine; po wczytaniu pelny przeglad kolejka (lordowie i gracz najpierw).
    /// Przy zmianie F zerujemy pamiec gry PartyBase._lastEstimatedStrengthVersionNo (inaczej EstimatedStrength stare do zmiany skladu); brak pola - "BRAK".
    /// Bez zapisu: po wczytaniu do pierwszego przeliczenia F = 1. Potkniecia liczone per partia - bez globalnych wylacznikow.
    ///
    /// AUTOBITWA - TA SAMA REGULA (recenzja p9, uwaga 1): wynik autorozstrzygniecia liczy cios po cios DefaultCombatSimulationModel.SimulateHit = 40 x (moc bijacego /
    /// moc trafianego)^0.7 z GetTroopPower (sam tier; ROT dokleja smoki, BK perki) - bez mnoznika ocena i wynik by sie rozjechaly (uzbrojeni widza gola, liczna partie
    /// jako slaba, atakuja i przegrywaja symulacje po tierze). Postfiks Priority.VeryLow na bazowym SimulateHit (po NorthHomeEdge Low i BK Normal, przed CS
    /// ValyrianWardSim Last; ROT-owy model deleguje do bazowego, smok i tak nadpisany przez ROT): moc zwyklego zolnierza partii w regule x k = 1 - (1-g) x s, s = (P - min(P0,P))
    /// / (P + dodatek modelu) - DOKLADNIE ten sam wzor na czlowieka co F (g z publikacji partii, bez nowego liczenia), wiec cios x (k bijacego / k trafianego)^0.7.
    /// Bohaterowie k = 1. Dotyczy kazdej autobitwy (AI-AI, szturm AI, "wyslij wojsko" gracza - jedna regula), przy kluczu AiStrengthCountsGear I osobnym
    /// AiAutobattleCountsGear (druga recenzja p9: wynik autobitew zmienia WPO balansu krolestw i kalibracje NorthHomeEdge - osobny klucz do proby A/B na tym samym zapisie).
    /// NIE ZMIENIA: linii B175 "moc" (KingdomBalance) - ale jej "wygrane ponad oczekiwane" i "wyrownane" licza oczekiwany wynik z samego tieru, a autobitwa (z kluczem)
    /// rozstrzyga sie takze wedlug zbrojowni: po p9 WPO nie jest porownywalne z baza sprzed p9 (CHANGELOG 122).
    ///
    /// ZALOGI - DECYZJE "ILU ZOSTAWIC / ILU WZIAC" (druga recenzja p9): BKGarrisonModel.FindNumberOfTroopsToLeaveToGarrison porownuje EstimatedStrength zalogi
    /// z idealem, gra FindNumberOfTroopsToTakeFromGarrison - CalculateCurrentStrength; z F < 1 (zaloga przy BareOn) niedozbrojona zaloga wygladalaby na slaba,
    /// lord zostawialby wiecej ludzi -> wiecej pustych slotow -> nizsze R i F -> petla wzrostu zalog i zoldu. Prefiks/finalizer czynnego modelu zalog (ten sam
    /// wzor co lup DTE: flaga [ThreadStatic] i wyzerowana pamiec sily zalogi i lorda przed i po) - te dwie decyzje licza ludzi bez mnoznika, jak przed p9.
    ///
    /// ZAPIS (druga recenzja p9): zegar czekania (od kiedy niegotowa, od kiedy zwolniona, zwolniona) partii niegotowych idzie do zapisu (klucz arm_p9_wait,
    /// StringId partii) - inaczej kazde wczytanie zaczynaloby AiArmsWaitMaxDays od nowa i lord bez pieniedzy przy krotkich sesjach nie bylby nigdy zwolniony.
    /// </summary>
    internal static class ArmedStrength
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const int KNone = 0, KLord = 1, KPlayer = 2, KGarrison = 3;
        private const float P0 = 0.40f;          // (2+0)(10+0) x 0.02 - DefaultMilitaryPowerModel.GetDefaultTroopPower tieru 0
        private const int DirtyCap = 200;        // brudne na godzine
        private const int FullCap = 400;         // pelny przeglad po wczytaniu - na godzine

        /// <summary>Opublikowany stan partii (niezmienny): mnoznik sily, gotowosc %, "niegotowa" (ArmsFirst, histereza), partia - do zerowania pamieci gry.
        /// Recenzja p9: G - udzial pelnej sily zwyklego zolnierza (min(1, R / Knee) - autobitwa), Free - niegotowa zwolniona z czekania po AiArmsWaitMaxDays dobach
        /// (walczy tym, co ma; ArmsFirst jej nie wstrzymuje), Short - sztuk brakujacych do progu gotowosci (AiArmsReadyPercent).</summary>
        internal sealed class Info
        {
            internal readonly float F, G; internal readonly int R, Short; internal readonly bool Low, Free; internal readonly MobileParty P;
            internal Info(float f, float g, int r, int sh, bool low, bool free, MobileParty p) { F = f; G = g; R = r; Short = sh; Low = low; Free = free; P = p; }
        }

        private sealed class Ent
        {
            internal MobileParty P; internal int Kind, R = 100, Ver, Healthy, ArmVer, Day, Short; internal float F = 1f, G = 1f; internal bool Low, Known, Free;
            internal double LowSinceH = -1, FreeFromH = -1;   // recenzja p9 (uwaga 3): od kiedy niegotowa (zegar czekania), od kiedy zwolniona
        }

        private static readonly Dictionary<MBGUID, Ent> _ent = new Dictionary<MBGUID, Ent>();
        private static readonly HashSet<MobileParty> _dirty = new HashSet<MobileParty>();
        private static readonly Queue<MobileParty> _fullQ = new Queue<MobileParty>();
        private static readonly Dictionary<MBGUID, Info> Empty = new Dictionary<MBGUID, Info>();
        private static volatile Dictionary<MBGUID, Info> _pub = Empty;
        private static volatile bool _strOn, _simOn;   // _simOn - autobitwa (AiStrengthCountsGear i AiAutobattleCountsGear)
        private static bool _pubDirty, _invAll, _needFull, _anyOn;
        private static int _knee = -1;
        [ThreadStatic] private static int _hold;

        // latki (raz na proces)
        private static Harmony _h;
        private static readonly HashSet<MethodBase> _patched = new HashSet<MethodBase>();
        private static string _hPower = "nie wpieta", _hDte = "nie wpieta", _hMem = "BRAK", _hSim = "nie wpieta", _hGar = "nie wpieta";
        // druga recenzja p9: zegary czekania z zapisu (StringId -> stan), zuzywane przy pierwszym czytelnym przeliczeniu partii
        private struct SavedWait { internal double LowH, FreeH; internal bool Free; }
        private static readonly Dictionary<string, SavedWait> _savedWait = new Dictionary<string, SavedWait>(StringComparer.Ordinal);
        private static int _waitLoaded, _dRestored, _dGarHold;
        private static AccessTools.FieldRef<PartyBase, int> _verRef;
        private static bool _verTried;
        // recenzja p9 (uwaga 1): udzial "nagiej" mocy zwyklego zolnierza s = (P - min(P0,P)) / (P + dodatek modelu) - stale na kampanie (tier i wzorce), pamiec na postac
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<CharacterObject, float> _share = new System.Collections.Concurrent.ConcurrentDictionary<CharacterObject, float>();

        // liczniki
        private static int _stumbles, _dComputed, _dUnknown, _dFull, _fullTotal, _dInvalid, _dPublish, _dHold, _dFreed, _dUnfree, _simStumbles;
        private static long _dTicks, _dSimHits, _dSimOut, _dSimIn, _dSimMilli;   // autobitwa: ciosy z poprawka, zadane / otrzymane przez slabo uzbrojonych, suma mnoznikow x 1000

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5 || _stumbles % 100 == 0) Log.Error("ArmedStrength." + where + " (potkniecie " + _stumbles + ")", e);
        }

        internal static void Reset()
        {
            _ent.Clear(); _dirty.Clear(); _fullQ.Clear(); _pub = Empty; _strOn = false; _simOn = false; _savedWait.Clear(); _waitLoaded = 0; _pubDirty = _invAll = false; _needFull = true; _anyOn = false; _knee = -1;
            _stumbles = 0; _simStumbles = 0; ClearDay(); _fullTotal = 0; _share.Clear();
            ArmsFirst.Reset();
            NewLordKit.Reset();   // 159: plany nowych druzyn, zegary "nie moze sie dozbroic", liczniki - od zera przy nowej kampanii / przed wczytaniem
        }

        private static void ClearDay()
        {
            _dComputed = _dUnknown = _dFull = _dInvalid = _dPublish = _dHold = _dFreed = _dUnfree = _dRestored = _dGarHold = 0; _dTicks = 0;
            Interlocked.Exchange(ref _dSimHits, 0); Interlocked.Exchange(ref _dSimOut, 0); Interlocked.Exchange(ref _dSimIn, 0); Interlocked.Exchange(ref _dSimMilli, 0);
        }

        // ------------------------------------------------------------ odczyt (dowolny watek)
        /// <summary>Opublikowany stan partii albo null (poza regula, F = 1 i gotowa, albo jeszcze nie policzona).</summary>
        internal static Info InfoOf(MobileParty mp)
        {
            if (mp == null) return null;
            Info i;
            return _pub.TryGetValue(mp.Id, out i) ? i : null;
        }

        /// <summary>ArmsFirst: partia lorda AI "niegotowa" (R ponizej AiArmsBelowPercent, histereza do AiArmsReadyPercent) i NIE zwolniona z czekania (recenzja p9,
        /// uwaga 3: po AiArmsWaitMaxDays dobach walczy tym, co ma) wedlug ostatniej publikacji.</summary>
        internal static bool IsLow(MobileParty mp) { var i = InfoOf(mp); return i != null && i.Low && !i.Free; }

        /// <summary>159: czy partia byla juz przeliczona w tej sesji (po wczytaniu pelny przeglad idzie kolejka) - brak publikacji przy policzonej = gotowa.</summary>
        internal static bool Computed(MobileParty mp) { return mp != null && _ent.ContainsKey(mp.Id); }

        /// <summary>159 P0: ile dob partia czeka jako niegotowa (zegar ArmedStrength.Wait; -1 - nie czeka albo nie policzona). Watek glowny, tylko odczyt.</summary>
        internal static double WaitDaysOf(MobileParty mp)
        {
            Ent e;
            if (mp == null || !_ent.TryGetValue(mp.Id, out e) || e.LowSinceH < 0) return -1;
            return (CampaignTime.Now.ToHours - e.LowSinceH) / 24.0;
        }

        /// <summary>Postfiks Priority.Last na GetPowerOfParty czynnego modelu: wynik x F, gdy kontekst Estimated albo partia poza bitwa. Tylko odczyt niezmiennego
        /// slownika (watki robocze ruchu); zadnych zapisow. Flaga _hold (lup DTE) - bez mnoznika. Nie rzuca.</summary>
        public static void PowerPostfix(PartyBase __0, MapEvent.PowerCalculationContext __2, ref float __result)
        {
            if (!_strOn || _hold > 0 || __0 == null) return;
            try
            {
                var pub = _pub;
                if (pub.Count == 0 || !__0.IsMobile) return;
                if (__2 != MapEvent.PowerCalculationContext.Estimated && __0.MapEvent != null) return;
                var mp = __0.MobileParty;
                Info i;
                if (mp != null && pub.TryGetValue(mp.Id, out i) && i.F < 1f) __result *= i.F;
            }
            catch { }
        }

        /// <summary>Recenzja p9 (uwaga 1): udzial mocy zwyklego zolnierza, ktory znika bez sprzetu - s = (P - min(P0,P)) / (P + dodatek modelu ponad tier), jak w Factor.
        /// Bohater 0 (sprzet wlasny). Pamiec na postac (tier i wzorce stale w kampanii).</summary>
        private static float ShareOf(CharacterObject ch)
        {
            if (ch == null || ch.IsHero) return 0f;
            float v;
            if (_share.TryGetValue(ch, out v)) return v;
            var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.MilitaryPowerModel : null;
            if (model == null) return 0f;
            float full = model.GetTroopPower(ch, BattleSideEnum.Defender, MapEvent.PowerCalculationContext.Estimated, 0f);
            float b = model.GetDefaultTroopPower(ch);
            float tot = b + Math.Max(0f, full - b);
            v = tot > 0f ? Math.Max(0f, Math.Min(1f, (b - Math.Min(P0, b)) / tot)) : 0f;
            _share[ch] = v;
            return v;
        }

        /// <summary>k zwyklego zolnierza partii w autobitwie: 1 - (1 - g) x s (g z publikacji partii; poza regula albo pelna sila - 1).</summary>
        private static float SimK(CharacterObject ch, PartyBase pb, Dictionary<MBGUID, Info> pub)
        {
            if (pb == null || !pb.IsMobile || ch == null || ch.IsHero) return 1f;
            var mp = pb.MobileParty;
            Info i;
            if (mp == null || !pub.TryGetValue(mp.Id, out i) || !(i.G < 1f)) return 1f;
            float k = 1f - (1f - Math.Max(0f, i.G)) * ShareOf(ch);
            return Math.Max(0.05f, Math.Min(1f, k));
        }

        /// <summary>Recenzja p9 (uwaga 1): postfiks VeryLow bazowego DefaultCombatSimulationModel.SimulateHit (cios w autobitwie) - moc bijacego i trafianego
        /// z gotowoscia zbrojowni ich partii, ten sam wzor co F w ocenie sily: cios x (k bijacego / k trafianego)^0.7 (wzor gry 40 x (moc / moc)^0.7). Tylko odczyt
        /// niezmiennej publikacji. Nie rzuca.</summary>
        public static void SimPostfix(CharacterObject __0, CharacterObject __1, PartyBase __2, PartyBase __3, ref ExplainedNumber __result)
        {
            if (!_simOn) return;
            try
            {
                var pub = _pub;
                if (pub.Count == 0) return;
                float pre = __result.ResultNumber;
                if (!(pre > 0f)) return;
                float ka = SimK(__0, __2, pub), kd = SimK(__1, __3, pub);
                if (ka >= 1f && kd >= 1f) return;
                double m = Math.Pow(ka / (double)kd, 0.7);
                __result = new ExplainedNumber((float)(pre * m));
                Interlocked.Increment(ref _dSimHits);
                if (ka < 1f) Interlocked.Increment(ref _dSimOut);
                if (kd < 1f) Interlocked.Increment(ref _dSimIn);
                Interlocked.Add(ref _dSimMilli, (long)(m * 1000.0));
            }
            catch { Interlocked.Increment(ref _simStumbles); }
        }

        /// <summary>Prefiks First DTE EveryoneCampaignBehavior.OnMapEventEnded: podzial lupu AI-AI wedlug EstimatedStrength bez mnoznika (jak przed p9) - flaga
        /// i wyzerowana pamiec sily uczestnikow (DTE policzy i zapamieta sile bez mnoznika).</summary>
        public static void HoldPrefix(MapEvent __0)
        {
            _hold++;
            if (_strOn) { _dHold++; InvalidateEvent(__0); }
        }

        /// <summary>Finalizer: koniec flagi i znowu wyzerowana pamiec uczestnikow - nastepny odczyt policzy sile z mnoznikiem. Wyjatek DTE idzie dalej bez zmian.</summary>
        public static Exception HoldFinalizer(MapEvent __0, Exception __exception)
        {
            if (_hold > 0) _hold--;
            if (_strOn) InvalidateEvent(__0);
            return __exception;
        }

        /// <summary>Druga recenzja p9: prefiks First czynnego modelu zalog (FindNumberOfTroopsToLeaveToGarrison / FindNumberOfTroopsToTakeFromGarrison) - ile ludzi
        /// zostawic albo wziac liczone bez mnoznika (jak przed p9): flaga i wyzerowana pamiec sily zalogi i lorda. Nie rzuca.</summary>
        public static void GarPrefix(MobileParty __0, Settlement __1)
        {
            _hold++;
            if (!_strOn) return;
            _dGarHold++;
            InvalidateGar(__0, __1);
        }

        /// <summary>Finalizer: koniec flagi i znowu wyzerowana pamiec - nastepny odczyt policzy sile z mnoznikiem. Wyjatek gry idzie dalej bez zmian.</summary>
        public static Exception GarFinalizer(MobileParty __0, Settlement __1, Exception __exception)
        {
            if (_hold > 0) _hold--;
            if (_strOn) InvalidateGar(__0, __1);
            return __exception;
        }

        private static void InvalidateGar(MobileParty mp, Settlement st)
        {
            try
            {
                if (_verRef == null) return;
                if (mp != null && mp.Party != null) _verRef(mp.Party) = -1;
                var g = st != null && st.Town != null ? st.Town.GarrisonParty : null;
                if (g != null && g.Party != null) _verRef(g.Party) = -1;
            }
            catch (Exception e) { Stumble("InvalidateGar", e); }
        }

        // ------------------------------------------------------------ zapis zegarow czekania (druga recenzja p9)
        private static readonly char[] IdBad = { ',', ';', '|' };

        private static void AppendWait(StringBuilder sb, ref bool first, string id, double lowH, double freeH, bool free)
        {
            if (!first) sb.Append(';');
            first = false;
            sb.Append(id).Append(',').Append(lowH.ToString("0.##", Inv)).Append(',').Append(freeH.ToString("0.##", Inv)).Append(',').Append(free ? '1' : '0');
        }

        /// <summary>"1|StringId,odH,zwolnionaOdH,0/1;..." - partie lordow niegotowe (z zegarem) plus wpisy z zapisu, ktorych jeszcze nie przeliczono po wczytaniu.</summary>
        internal static string ExportWait()
        {
            var sb = new StringBuilder("1|");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool first = true;
            foreach (var e in _ent.Values)
            {
                if (e.P == null || e.Kind != KLord || !e.Low || e.LowSinceH < 0) continue;
                string id = e.P.StringId;
                if (string.IsNullOrEmpty(id) || id.IndexOfAny(IdBad) >= 0 || !seen.Add(id)) continue;
                AppendWait(sb, ref first, id, e.LowSinceH, e.FreeFromH, e.Free);
            }
            foreach (var kv in _savedWait)
                if (seen.Add(kv.Key)) AppendWait(sb, ref first, kv.Key, kv.Value.LowH, kv.Value.FreeH, kv.Value.Free);
            return sb.ToString();
        }

        /// <summary>Wczytanie (po Reset z konstruktora zachowania): brak klucza albo zly format - pusto (zegary od nowa, jak przed poprawka).</summary>
        internal static void ImportWait(string v)
        {
            _savedWait.Clear(); _waitLoaded = 0;
            if (string.IsNullOrEmpty(v) || !v.StartsWith("1|", StringComparison.Ordinal)) return;
            foreach (var row in v.Substring(2).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var c = row.Split(',');
                double lo, fr;
                if (c.Length < 4 || c[0].Length == 0 || !double.TryParse(c[1], NumberStyles.Float, Inv, out lo) || !double.TryParse(c[2], NumberStyles.Float, Inv, out fr) || lo < 0) continue;
                _savedWait[c[0]] = new SavedWait { LowH = lo, FreeH = fr, Free = c[3] == "1" && fr >= 0 };
                _waitLoaded++;
            }
        }

        private static void InvalidateEvent(MapEvent me)
        {
            try
            {
                if (me == null || _verRef == null) return;
                foreach (var pb in me.InvolvedParties) if (pb != null && pb.IsMobile) _verRef(pb) = -1;
            }
            catch (Exception e) { Stumble("InvalidateEvent", e); }
        }

        private static void Invalidate(MobileParty mp)
        {
            try { if (_verRef != null && mp != null && mp.Party != null) { _verRef(mp.Party) = -1; _dInvalid++; } }
            catch (Exception e) { Stumble("Invalidate", e); }
        }

        // ------------------------------------------------------------ regula partii
        /// <summary>Rodzaj partii w regule: lord (LordRule) i gracz przy TroopsFightWithOwnKitOnly, zaloga przy BareOn (bez umarlych); reszta KNone (F = 1).</summary>
        internal static int Kind(MobileParty mp)
        {
            if (mp == null || !mp.IsActive || mp.MemberRoster == null) return KNone;
            if (mp.IsMainParty) return GarrisonKit.OwnKitOn ? KPlayer : KNone;
            if (mp.IsGarrison) return GarrisonKit.BareOn && !Undead.Party(mp) ? KGarrison : KNone;
            return GarrisonKit.OwnKitOn && GarrisonKit.LordRule(mp) ? KLord : KNone;
        }

        private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
        internal static int Below(Settings s) { return Clamp(s.AiArmsBelowPercent, 0, 100); }
        internal static int Ready(Settings s) { return Math.Max(Below(s), Clamp(s.AiArmsReadyPercent, 0, 100)); }
        private static int Knee(Settings s) { return Clamp(s.AiStrengthFullKitPercent, 1, 100); }

        private static Dictionary<ItemObject, int> ToDict(ItemRoster pr)
        {
            var arm = new Dictionary<ItemObject, int>();
            for (int i = 0; i < pr.Count; i++)
            {
                var e = pr.GetElementCopyAtIndex(i);
                var it = e.EquipmentElement.Item;
                if (it == null || e.Amount <= 0) continue;
                int c0; arm.TryGetValue(it, out c0); arm[it] = c0 + e.Amount;
            }
            return arm;
        }

        /// <summary>Mnoznik sily partii z gotowosci R (opis klasy). Zdrowi jak w GetPowerOfParty; moc z czynnego modelu (kontekst Estimated, bez premii dowodcy -
        /// ta mnozy wszystkich jednakowo).</summary>
        private static float Factor(MobileParty mp, int r, Settings s)
        {
            double g = Math.Min(1.0, Math.Max(0, r) / (double)Knee(s));
            if (g >= 1.0) return 1f;
            var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.MilitaryPowerModel : null;
            if (model == null) return 1f;
            var roster = mp.MemberRoster;
            double fix = 0, reg = 0, naked = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var ch = el.Character;
                int n = el.Number - el.WoundedNumber;
                if (ch == null || n <= 0) continue;
                float full = model.GetTroopPower(ch, BattleSideEnum.Defender, MapEvent.PowerCalculationContext.Estimated, 0f);
                if (ch.IsHero) { fix += n * (double)full; continue; }
                float b = model.GetDefaultTroopPower(ch);
                fix += n * (double)Math.Max(0f, full - b);
                reg += n * (double)b;
                naked += n * (double)Math.Min(P0, b);
            }
            double tot = fix + reg;
            if (tot <= 0) return 1f;
            double f = (fix + g * reg + (1.0 - g) * naked) / tot;
            return (float)Math.Max(0.05, Math.Min(1.0, f));
        }

        /// <summary>Przeliczenie R, F i stanu "niegotowa" jednej partii (watek glowny). Zmiana opublikowanego stanu (F o >= 0.01 albo Low) - publikacja w godzinie.</summary>
        private static void Compute(MobileParty mp, int kind, Settings s)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                Ent e;
                if (!_ent.TryGetValue(mp.Id, out e)) { e = new Ent(); _ent[mp.Id] = e; }
                e.P = mp; e.Kind = kind;
                var roster = mp.MemberRoster;
                e.Ver = roster.VersionNo; e.Healthy = roster.TotalHealthyCount; e.Day = (int)CampaignTime.Now.ToDays;
                Dictionary<ItemObject, int> arm = null;
                bool known;
                if (kind == KPlayer)
                {
                    var pr = QuartermasterLaw.DteArmory();
                    known = pr != null;
                    if (known) { e.ArmVer = pr.VersionNo; arm = ToDict(pr); }
                }
                else
                {
                    var all = AiGear.Armories();
                    known = all != null;
                    if (known) all.TryGetValue(mp.Id, out arm);
                }
                if (!known) { e.Known = false; e.R = -1; e.F = 1f; e.G = 1f; e.Short = 0; e.Low = false; e.Free = false; e.LowSinceH = -1; _dUnknown++; }
                else
                {
                    e.Known = true;
                    int slots, filled;
                    int r = GarrisonKit.FillPercent(mp, arm, null, true, out slots, out filled);
                    e.R = r;
                    e.F = Factor(mp, r, s);
                    e.G = (float)Math.Min(1.0, Math.Max(0, r) / (double)Knee(s));
                    e.Short = Math.Max(0, (int)Math.Ceiling(Ready(s) * (double)slots / 100.0) - filled);   // sztuk do progu gotowosci (ArmsFirst.CanAfford)
                    // druga recenzja p9: stan z zapisu (niegotowa - histereza - i zegar czekania), raz, przy pierwszym czytelnym przeliczeniu po wczytaniu
                    SavedWait sw;
                    bool restored = false;
                    if (_savedWait.Count > 0 && mp.StringId != null && _savedWait.TryGetValue(mp.StringId, out sw))
                    {
                        _savedWait.Remove(mp.StringId);
                        if (kind == KLord) { e.Low = true; e.LowSinceH = sw.LowH; e.FreeFromH = sw.FreeH; e.Free = sw.Free; restored = true; }
                    }
                    e.Low = kind == KLord && (e.Low ? r < Ready(s) : r < Below(s));
                    if (restored && e.Low) _dRestored++;
                    Wait(e, mp, s);
                }
                _dComputed++;
                var pi = InfoOf(mp);
                float pf = pi != null ? pi.F : 1f;
                bool pl = pi != null && pi.Low, pfr = pi != null && pi.Free;
                int psh = pi != null ? pi.Short : 0;
                if (Math.Abs(pf - e.F) >= 0.01f || pl != e.Low || pfr != e.Free || (e.Low && psh != e.Short)) _pubDirty = true;
            }
            catch (Exception ex) { Stumble("Compute", ex); }
            finally { _dTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Recenzja p9 (uwaga 3 - regula Jeffa 12.10 "jak ich nie stac na wojne, to pozyczka albo mniejsze wojsko", nie bezczynnosc): zegar czekania niegotowej
        /// partii. Po AiArmsWaitMaxDays dobach bez dozbrojenia (brak zlota, brak miasta, puste polki) - ZWOLNIONA: ArmsFirst jej nie wstrzymuje, walczy tym, co ma
        /// (sila dalej z uzbrojeniem - nie zaczepi silniejszych). Koniec zwolnienia: gotowa (R >= progu) albo najwczesniej po tylu samych dobach, gdy stac ja znowu
        /// na sprzet (CanAfford) - nowa proba dozbrojenia, zegar od nowa. Partia, ktora w miescie dalej dokupuje z postepem (ArmsFirst.Progressing), nie jest zwalniana
        /// (postep wymaga pieniedzy, R ma sufit - to sie konczy). Watek glowny (doba partii albo brudna w godzinie).</summary>
        private static void Wait(Ent e, MobileParty mp, Settings s)
        {
            if (!e.Low) { e.LowSinceH = -1; e.FreeFromH = -1; e.Free = false; return; }
            double now = CampaignTime.Now.ToHours, wait = ArmsFirst.WaitDays(s) * 24.0;
            if (e.LowSinceH < 0) e.LowSinceH = now;
            if (mp.Army != null && !e.Free) { e.LowSinceH = now; return; }   // w armii nie jest wstrzymywana (Unready - false) - zegar liczy od wyjscia z armii
            if (e.Free)
            {
                int b, need;
                if (now - e.FreeFromH >= wait && ArmsFirst.CanAfford(mp, e.Short, null, out b, out need)) { e.Free = false; e.FreeFromH = -1; e.LowSinceH = now; _dUnfree++; }
            }
            else if (now - e.LowSinceH >= wait && !ArmsFirst.Progressing(mp))   // w miescie z postepem (postoj przedluzony) - dokupuje dalej, zwolnienie pozniej
            {
                e.Free = true; e.FreeFromH = now; _dFreed++;
                ArmsFirst.Forget(mp);   // wyjazd i seria wizyt bez zakupu - koniec
            }
        }

        private static void Drop(MobileParty mp)
        {
            if (mp == null) return;
            if (_ent.Remove(mp.Id) && InfoOf(mp) != null) _pubDirty = true;
        }

        // ------------------------------------------------------------ zdarzenia
        internal static void RegisterEvents(CampaignBehaviorBase owner)
        {
            CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(owner, DayProfile.PartyDay("ArmedStrength.OnDailyTickParty", OnDailyTickParty));
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(owner, DayProfile.Hr("ArmedStrength.Hourly", Hourly));
            CampaignEvents.MapEventEnded.AddNonSerializedListener(owner, DayProfile.MapEnd("ArmedStrength.OnMapEventEnded", OnMapEventEnded));
            CampaignEvents.MobilePartyCreated.AddNonSerializedListener(owner, MarkDirty);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(owner, DayProfile.Destroyed("ArmedStrength.OnPartyDestroyed", OnPartyDestroyed));
            NewLordKit.RegisterEvents(owner);   // 159: doba powstania partii, koniec planu przy zniszczeniu, P0 przy bitwie gracza
        }

        /// <summary>Partia do przeliczenia w nastepnej godzinie (zakup AiGear, nowa partia, koniec bitwy). Watek glowny.</summary>
        internal static void MarkDirty(MobileParty mp)
        {
            try { if (mp != null && _anyOn) _dirty.Add(mp); } catch { }
        }

        private static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                if (me == null || !_anyOn) return;
                foreach (var pb in me.InvolvedParties) if (pb != null && pb.MobileParty != null) _dirty.Add(pb.MobileParty);
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        private static void OnPartyDestroyed(MobileParty mp, PartyBase pb)
        {
            try { if (mp != null) { _dirty.Remove(mp); Drop(mp); ArmsFirst.Forget(mp); } } catch (Exception e) { Stumble("OnPartyDestroyed", e); }
        }

        /// <summary>DailyTickParty (watek glowny, gra rozklada partie na dobe): przeliczenie partii w regule; poza regula - wypada z pamieci.</summary>
        internal static void OnDailyTickParty(MobileParty mp)
        {
            var s = Settings.Current;
            if (s == null || mp == null || !(s.AiStrengthCountsGear || s.AiArmsBeforeBattle)) return;
            try
            {
                int k = Kind(mp);
                if (k == KNone) { Drop(mp); return; }
                _dirty.Remove(mp);
                Compute(mp, k, s);
            }
            catch (Exception e) { Stumble("OnDailyTickParty", e); }
        }

        /// <summary>Godzina: klucze z MCM, pelny przeglad po wczytaniu (kolejka), wersje skladow i zbrojowni gracza, brudne (limit), publikacja i pamiec gry.</summary>
        internal static void Hourly()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                bool str = s.AiStrengthCountsGear, any = str || s.AiArmsBeforeBattle;
                if (!any)
                {
                    if (_anyOn || _pub.Count > 0)
                    {
                        var old = _pub; _pub = Empty; _strOn = false; _simOn = false;
                        foreach (var kv in old) if (kv.Value.F < 1f) Invalidate(kv.Value.P);
                        _ent.Clear(); _dirty.Clear(); _fullQ.Clear(); _needFull = true;
                    }
                    _anyOn = false;
                    return;
                }
                _anyOn = true;
                if (str != _strOn) { _strOn = str; _invAll = true; _pubDirty = true; }
                _simOn = str && s.AiAutobattleCountsGear;
                int knee = Knee(s);
                if (knee != _knee) { if (_knee >= 0) foreach (var e in _ent.Values) if (e.P != null) _dirty.Add(e.P); _knee = knee; }
                if (_needFull)
                {
                    _needFull = false; _fullQ.Clear();
                    if (MobileParty.MainParty != null) _fullQ.Enqueue(MobileParty.MainParty);
                    foreach (var mp in MobileParty.AllLordParties) if (mp != null && !mp.IsMainParty) _fullQ.Enqueue(mp);
                    foreach (var mp in MobileParty.AllGarrisonParties) if (mp != null) _fullQ.Enqueue(mp);
                    _fullTotal = _fullQ.Count;
                }
                // wersje: sklad (zdrowi o >= max(3, 10%)), zbrojownia gracza; nieczynne - z pamieci
                List<MBGUID> gone = null;
                ItemRoster pr = null; bool prRead = false;
                foreach (var kv in _ent)
                {
                    var e = kv.Value; var mp = e.P;
                    if (mp == null || !mp.IsActive || mp.MemberRoster == null) { if (gone == null) gone = new List<MBGUID>(); gone.Add(kv.Key); continue; }
                    if (e.Kind == KPlayer)
                    {
                        if (!prRead) { pr = QuartermasterLaw.DteArmory(); prRead = true; }
                        if (pr != null && pr.VersionNo != e.ArmVer) _dirty.Add(mp);
                    }
                    var ro = mp.MemberRoster;
                    if (ro.VersionNo != e.Ver)
                    {
                        int h = ro.TotalHealthyCount;
                        if (Math.Abs(h - e.Healthy) >= Math.Max(3, e.Healthy / 10)) _dirty.Add(mp);
                    }
                }
                if (gone != null)
                {
                    var pub0 = _pub;
                    foreach (var id in gone)
                    {
                        Ent e;
                        if (_ent.TryGetValue(id, out e) && e.P != null) _dirty.Remove(e.P);
                        _ent.Remove(id);
                        if (pub0.ContainsKey(id)) _pubDirty = true;
                    }
                }
                // brudne, potem pelny przeglad
                int n = 0;
                if (_dirty.Count > 0)
                {
                    var list = new List<MobileParty>(_dirty);
                    foreach (var mp in list)
                    {
                        if (n >= DirtyCap) break;
                        _dirty.Remove(mp);
                        int k = Kind(mp);
                        if (k == KNone) { Drop(mp); continue; }
                        Compute(mp, k, s); n++;
                    }
                }
                int f = 0;
                while (_fullQ.Count > 0 && f < FullCap)
                {
                    var mp = _fullQ.Dequeue();
                    int k = Kind(mp);
                    if (k == KNone) continue;
                    Ent e;
                    if (_ent.TryGetValue(mp.Id, out e) && e.Day == (int)CampaignTime.Now.ToDays) continue;   // juz policzona dzis (doba albo brudna)
                    Compute(mp, k, s); f++; _dFull++;
                }
                if (_pubDirty) Publish();
            }
            catch (Exception e) { Stumble("Hourly", e); }
            try { ArmsFirst.Hourly(); } catch (Exception e) { Stumble("ArmsFirst.Hourly", e); }
        }

        /// <summary>Nowy niezmienny slownik (tylko partie z F &lt; 1 albo niegotowe) i wyzerowana pamiec EstimatedStrength kazdej partii, ktorej mnoznik sie zmienil
        /// (albo wszystkich przy przelaczeniu klucza).</summary>
        private static void Publish()
        {
            _pubDirty = false; _dPublish++;
            var old = _pub;
            var nu = new Dictionary<MBGUID, Info>(Math.Max(16, old.Count + 8));
            foreach (var kv in _ent)
            {
                var e = kv.Value;
                if (e.P == null) continue;
                if (e.F < 0.9995f || e.Low) nu[kv.Key] = new Info(e.F, e.G, e.R, e.Short, e.Low, e.Free, e.P);
            }
            _pub = nu;
            bool all = _invAll; _invAll = false;
            foreach (var kv in nu)
            {
                Info o;
                float of = old.TryGetValue(kv.Key, out o) ? o.F : 1f;
                if (all ? (of < 1f || kv.Value.F < 1f) : of != kv.Value.F) Invalidate(kv.Value.P);
            }
            foreach (var kv in old)
                if (!nu.ContainsKey(kv.Key) && kv.Value.F < 1f && kv.Value.P != null && kv.Value.P.IsActive) Invalidate(kv.Value.P);
        }

        // ------------------------------------------------------------ start sesji
        /// <summary>OnSessionLaunched (po McmSettings.Apply): latki (raz na proces) - GetPowerOfParty czynnego modelu, lup DTE, pole pamieci sily; potem ArmsFirst;
        /// linia startu. Pelny przeglad w pierwszych godzinach (kolejka).</summary>
        internal static void SessionStart()
        {
            if (_h == null) _h = new Harmony("com.jeff.armoury.armedstrength");
            _needFull = true;
            try
            {
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.MilitaryPowerModel : null;
                if (model == null) _hPower = "BRAK (model sily nieznany)";
                else
                {
                    var m = PatchTarget.OfActive(model, "GetPowerOfParty", new[] { typeof(PartyBase), typeof(BattleSideEnum), typeof(MapEvent.PowerCalculationContext) }, "sila z uzbrojeniem (p9)");
                    if (m == null) _hPower = "BRAK (" + model.GetType().FullName + ".GetPowerOfParty)";
                    else if (_patched.Contains(m)) _hPower = "wpieta " + m.DeclaringType.FullName + ".GetPowerOfParty (juz wczesniej w tym procesie)";
                    else
                    {
                        _h.Patch(m, postfix: new HarmonyMethod(typeof(ArmedStrength), nameof(PowerPostfix)) { priority = Priority.Last });
                        _patched.Add(m);
                        _hPower = "wpieta " + m.DeclaringType.FullName + ".GetPowerOfParty (model czynny " + model.GetType().FullName + ")";
                    }
                }
            }
            catch (Exception e) { _hPower = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Power", e); }
            try
            {
                var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.EveryoneCampaignBehavior");
                var m = t != null ? AccessTools.DeclaredMethod(t, "OnMapEventEnded", new[] { typeof(MapEvent) }) : null;
                if (m == null) _hDte = "BRAK (DTE EveryoneCampaignBehavior.OnMapEventEnded)";
                else if (_patched.Contains(m)) _hDte = "wpiety (juz wczesniej w tym procesie)";
                else
                {
                    _h.Patch(m, prefix: new HarmonyMethod(typeof(ArmedStrength), nameof(HoldPrefix)) { priority = Priority.First },
                             finalizer: new HarmonyMethod(typeof(ArmedStrength), nameof(HoldFinalizer)));
                    _patched.Add(m);
                    _hDte = "wpiety";
                }
            }
            catch (Exception e) { _hDte = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Dte", e); }
            // recenzja p9 (uwaga 1): autobitwa - bazowy SimulateHit zolnierza (DeclaredMethod; ROT-owy i morski model deleguja do bazowego - latanie obu cieloby podwojnie)
            try
            {
                var tDef = typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel);
                var m = AccessTools.DeclaredMethod(tDef, "SimulateHit", new[] { typeof(CharacterObject), typeof(CharacterObject), typeof(PartyBase), typeof(PartyBase),
                                                                               typeof(float), typeof(MapEvent), typeof(float), typeof(float) });
                if (m == null) _hSim = "BRAK (DefaultCombatSimulationModel.SimulateHit)";
                else if (_patched.Contains(m)) _hSim = "wpieta (juz wczesniej w tym procesie)";
                else
                {
                    _h.Patch(m, postfix: new HarmonyMethod(typeof(ArmedStrength), nameof(SimPostfix)) { priority = Priority.VeryLow });
                    _patched.Add(m);
                    _hSim = "wpieta DefaultCombatSimulationModel.SimulateHit (VeryLow; czynny model " + TypeName(Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.CombatSimulationModel : null) + ")";
                }
            }
            catch (Exception e) { _hSim = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Sim", e); }
            // druga recenzja p9: czynny model zalog - ile ludzi zostawic / wziac bez mnoznika (prefiks First i finalizer; PatchTarget.OfActive - klasa deklarujaca,
            // dzis ROTSettlementGarrisonModel -> NavalDLC -> BKGarrisonModel; latamy najbardziej zewnetrzny, wiec flaga obejmuje caly lancuch)
            try
            {
                var gm = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SettlementGarrisonModel : null;
                if (gm == null) _hGar = "BRAK (model zalog nieznany)";
                else
                {
                    var mL = PatchTarget.OfActive(gm, "FindNumberOfTroopsToLeaveToGarrison", new[] { typeof(MobileParty), typeof(Settlement) }, "zalogi: ilu zostawic bez mnoznika (p9)");
                    var mT = PatchTarget.OfActive(gm, "FindNumberOfTroopsToTakeFromGarrison", new[] { typeof(MobileParty), typeof(Settlement), typeof(float) }, "zalogi: ilu wziac bez mnoznika (p9)");
                    var parts = new List<string>(2);
                    var names = new[] { "zostaw", "wez" };
                    var ms = new[] { mL, mT };
                    for (int i = 0; i < 2; i++)
                    {
                        var m = ms[i];
                        if (m == null) { parts.Add(names[i] + " BRAK"); continue; }
                        if (!_patched.Contains(m))
                        {
                            _h.Patch(m, prefix: new HarmonyMethod(typeof(ArmedStrength), nameof(GarPrefix)) { priority = Priority.First },
                                     finalizer: new HarmonyMethod(typeof(ArmedStrength), nameof(GarFinalizer)));
                            _patched.Add(m);
                        }
                        parts.Add(names[i] + " " + m.DeclaringType.Name);
                    }
                    _hGar = "wpiete (" + string.Join(", ", parts.ToArray()) + "; model czynny " + gm.GetType().Name + ")";
                }
            }
            catch (Exception e) { _hGar = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Gar", e); }
            if (!_verTried)
            {
                _verTried = true;
                try { _verRef = AccessTools.FieldRefAccess<PartyBase, int>("_lastEstimatedStrengthVersionNo"); _hMem = _verRef != null ? "TAK" : "BRAK"; }
                catch (Exception e) { _verRef = null; _hMem = "BRAK (" + e.GetType().Name + ")"; }
            }
            string af = "";
            try { af = ArmsFirst.SessionStart(); } catch (Exception e) { af = "wyjatek " + e.GetType().Name; Stumble("SessionStart.ArmsFirst", e); }
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                Log.Info("Gotowosc do bitwy (p9): start sesji | klucze: AI Strength Counts Gear " + (s.AiStrengthCountsGear ? "TAK" : "NIE") + " (pelna sila od " + Knee(s)
                         + "% slotow wzorca, ponizej liniowo do sily ludzi bez sprzetu P0 = 0.40), AI Arms Before Battle " + (s.AiArmsBeforeBattle ? "TAK" : "NIE")
                         + " (niegotowa ponizej " + Below(s) + "%, gotowa od " + Ready(s) + "%, miasto najwyzej " + ArmsFirst.TripDays(s) + " dob drogi, wytchnienie miasta "
                         + ArmsFirst.CoolDays(s) + " dob, bandy " + (s.AiArmsBlockBanditAttacks ? "tez zakazane" : "wolno") + ", czekanie najwyzej " + ArmsFirst.WaitDays(s)
                         + " dob - potem walczy tym, co ma, do gotowosci albo pieniedzy na sprzet)"
                         + " | regula: lordowie i druzyna gracza " + (GarrisonKit.OwnKitOn ? "TAK" : "NIE (TroopsFightWithOwnKitOnly wylaczone)") + ", zalogi " + (GarrisonKit.BareOn ? "TAK" : "NIE (walcza we wzorcu)")
                         + " | AI Autobattle Counts Gear " + (s.AiAutobattleCountsGear ? "TAK" : "NIE") + (s.AiAutobattleCountsGear && !s.AiStrengthCountsGear ? " (bez skutku - AI Strength Counts Gear NIE)" : "")
                         + " | latki: sila partii " + _hPower + "; autobitwa (ta sama regula na cios) " + _hSim + "; pamiec sily gry (PartyBase._lastEstimatedStrengthVersionNo) " + _hMem
                         + "; lup DTE AI-AI bez mnoznika " + _hDte + "; zalogi ilu zostawic / wziac bez mnoznika " + _hGar
                         + " | zegary czekania z zapisu (arm_p9_wait): " + _waitLoaded + " partii"
                         + " | " + af + " | pelny przeglad partii w pierwszych godzinach (najwyzej " + FullCap + " na godzine, lordowie najpierw); do przeliczenia F = 1.");
            }
            catch (Exception e) { Stumble("SessionStart.Line", e); }
        }

        // ------------------------------------------------------------ linia doby
        private static string F2(double v) { return v.ToString("0.00", Inv); }
        private static string TypeName(object o) { return o != null ? o.GetType().FullName : "?"; }
        private static string Nm(MobileParty mp) { try { return mp != null && mp.Name != null ? mp.Name.ToString() : "?"; } catch { return "?"; } }

        private static string KingdomR(SortedDictionary<string, long[]> byK)
        {
            if (byK.Count == 0) return "-";
            var sb = new StringBuilder();
            foreach (var kv in byK)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(kv.Key).Append(' ').Append(kv.Value[1] > 0 ? (kv.Value[0] / (double)kv.Value[1]).ToString("0", Inv) : "-").Append("% (").Append(kv.Value[1]).Append(", ").Append(kv.Value[2]).Append(')');
            }
            return sb.ToString();
        }

        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            try
            {
                if (!(s.AiStrengthCountsGear || s.AiArmsBeforeBattle)) { ClearDay(); ArmsFirst.ClearDay(); return; }
                int lords = 0, lordsKnown = 0, gars = 0, garsKnown = 0, garsLow = 0, low = 0, lowArmy = 0, poor = 0, freed = 0;
                long rSum = 0, gSum = 0; double fSum = 0, fMin = 2; string fMinName = null;
                var bucket = new int[4];
                var byK = new SortedDictionary<string, long[]>(StringComparer.Ordinal);   // druga recenzja p9: R lordow wedlug krolestw (HorseCensus.KeyOfParty) - [suma R, partii, niegotowych]
                Ent pl = null;
                var lows = new List<Ent>();
                foreach (var e in _ent.Values)
                {
                    if (e.P == null) continue;
                    if (e.Kind == KPlayer) { pl = e; continue; }
                    if (e.Kind == KGarrison) { gars++; if (e.Known) { garsKnown++; gSum += e.R; if (e.F < 0.995f) garsLow++; } continue; }
                    if (e.Kind != KLord) continue;
                    lords++;
                    if (!e.Known) continue;
                    lordsKnown++; rSum += e.R; fSum += e.F;
                    string kk = HorseCensus.KeyOfParty(e.P.Party) ?? "?";
                    long[] kr;
                    if (!byK.TryGetValue(kk, out kr)) { kr = new long[3]; byK[kk] = kr; }
                    kr[0] += e.R; kr[1]++; if (e.Low && !e.Free) kr[2]++;
                    bucket[Math.Min(3, Math.Max(0, e.R) / 25)]++;
                    if (e.F < fMin) { fMin = e.F; fMinName = Nm(e.P) + " (" + e.P.MemberRoster.TotalManCount + " ludzi, R " + e.R + "%)"; }
                    if (e.Low)
                    {
                        if (e.P.Army != null) { lowArmy++; continue; }
                        if (e.Free) { freed++; continue; }   // recenzja p9 (uwaga 3): zwolniona z czekania - walczy tym, co ma
                        low++; lows.Add(e);
                        int b, need;
                        if (!ArmsFirst.CanAfford(e.P, e.Short, null, out b, out need)) poor++;
                    }
                }
                lows.Sort((a, b) => a.R != b.R ? a.R.CompareTo(b.R) : b.P.MemberRoster.TotalManCount.CompareTo(a.P.MemberRoster.TotalManCount));
                var ex = new StringBuilder();
                for (int i = 0; i < lows.Count && i < 3; i++)
                {
                    var e = lows[i];
                    int b, need; bool can = ArmsFirst.CanAfford(e.P, e.Short, null, out b, out need);
                    double waited = e.LowSinceH >= 0 ? (CampaignTime.Now.ToHours - e.LowSinceH) / 24.0 : 0;
                    if (ex.Length > 0) ex.Append("; ");
                    ex.Append(Nm(e.P)).Append(" (").Append(e.P.MemberRoster.TotalManCount).Append(" ludzi, R ").Append(e.R).Append("%, F ").Append(F2(e.F))
                      .Append(", brak do progu ").Append(e.Short).Append(" szt., czeka ").Append(waited.ToString("0.0", Inv)).Append(" dob")
                      .Append(", budzet ").Append(b).Append(" z ").Append(need).Append(can ? "" : " - nie stac").Append(", cel ").Append(ArmsFirst.TargetName(e.P)).Append(')');
                }
                double ms = _dTicks * 1000.0 / Stopwatch.Frequency;
                var sb = new StringBuilder(1600);
                sb.Append("Gotowosc do bitwy (p9): dzien ").Append((int)CampaignTime.Now.ToDays)
                  .Append(" | sila z uzbrojeniem ").Append(_strOn ? "TAK" : "NIE")
                  .Append(" | partie lordow (bez gracza): ").Append(lords).Append(lordsKnown < lords ? " (zbrojownia nieczytelna " + (lords - lordsKnown) + ")" : "")
                  .Append(", R srednio ").Append(lordsKnown > 0 ? (rSum / (double)lordsKnown).ToString("0", Inv) + "%" : "-")
                  .Append(", R 0-25: ").Append(bucket[0]).Append(", 25-50: ").Append(bucket[1]).Append(", 50-75: ").Append(bucket[2]).Append(", 75-100: ").Append(bucket[3])
                  .Append(", F srednio ").Append(lordsKnown > 0 ? F2(fSum / lordsKnown) : "-").Append(", najmniejsze ").Append(fMinName != null ? F2(fMin) + " " + fMinName : "-")
                  .Append(" | R lordow wedlug krolestw (srednio, partii, niegotowych): ").Append(KingdomR(byK))
                  .Append(" | gracz: ").Append(pl != null ? (pl.Known ? "R " + pl.R + "%, F " + F2(pl.F) : "zbrojownia nieczytelna") : "poza regula")
                  .Append(" | zalogi: ").Append(gars).Append(garsKnown > 0 ? ", R srednio " + (gSum / (double)garsKnown).ToString("0", Inv) + "%, z F < 1: " + garsLow : "")
                  .Append(" | niegotowe (R < ").Append(Below(s)).Append("%, do ").Append(Ready(s)).Append("%): poza armia ").Append(low).Append(", w tym bez pieniedzy ").Append(poor)
                  .Append(", w armii ").Append(lowArmy)
                  .Append(", zwolnione z czekania (po ").Append(ArmsFirst.WaitDays(s)).Append(" dobach walcza tym, co maja) ").Append(freed)
                  .Append(" (dzis nowe ").Append(_dFreed).Append(", powrot do dozbrajania - stac na sprzet ").Append(_dUnfree).Append(')')
                  .Append(" | autobitwa (ta sama regula, AI Autobattle Counts Gear ").Append(_simOn ? "TAK" : "NIE").Append("): ciosy z poprawka ").Append(Interlocked.Read(ref _dSimHits))
                  .Append(" (zadane przez slabo uzbrojonych ").Append(Interlocked.Read(ref _dSimOut)).Append(", otrzymane ").Append(Interlocked.Read(ref _dSimIn))
                  .Append(", sredni mnoznik ciosu ").Append(Interlocked.Read(ref _dSimHits) > 0 ? (Interlocked.Read(ref _dSimMilli) / 1000.0 / Interlocked.Read(ref _dSimHits)).ToString("0.00", Inv) : "-")
                  .Append(", potkniecia ").Append(_simStumbles).Append(')')
                  .Append(" | ").Append(ArmsFirst.DayText())
                  .Append(" | przyklady: ").Append(ex.Length > 0 ? ex.ToString() : "brak")
                  .Append(" | przeliczen ").Append(_dComputed).Append(" (pelny przeglad ").Append(_dFull).Append(" z ").Append(_fullTotal).Append(", w kolejce ").Append(_fullQ.Count)
                  .Append(", brudnych czeka ").Append(_dirty.Count).Append("), koszt ").Append(ms.ToString("0.0", Inv)).Append(" ms")
                  .Append(", publikacji ").Append(_dPublish).Append(", wyzerowan pamieci sily ").Append(_dInvalid).Append(", lup DTE bez mnoznika ").Append(_dHold)
                  .Append(", zbrojownie nieczytelne ").Append(_dUnknown)
                  .Append(", zalogi ilu zostawic / wziac bez mnoznika ").Append(_dGarHold)
                  .Append(", zegary czekania z zapisu przywrocone dzis ").Append(_dRestored).Append(" (czeka na przeliczenie ").Append(_savedWait.Count).Append(')')
                  .Append(", potkniecia ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            ClearDay();
            ArmsFirst.ClearDay();
        }
    }
}
