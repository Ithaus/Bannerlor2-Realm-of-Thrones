using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Generic;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>401 (Jeff 11.11 "rob 4" - rany i blizny bohaterow; PLAN-396 commit 401) - KSIEGA RAN I LOZE. Od 403 przesilenie: smierc od ran i okaleczenia
    /// (HeroFate / LordToll). Od 408 AI: ranny wodz wiezie sie do twierdzy (HeroWounds.Ai.cs).
    ///
    /// Wyzwalacz: CampaignEvents.HeroWounded (gra wola go w seterze Hero.HitPoints przy przejsciu ponad prog rany -> pod prog). Liczy sie tylko rana W BITWIE
    /// (partia bohatera w MapEvent albo gracz w PlayerEncounter.Battle), lord / gracz / towarzysz i rod gracza, dorosly, zywy, bez znaku smierci, nie
    /// Occupation.Special, nie Bialy Wedrowiec, bez czynnej rany Kroniki (Chronicle.Actions - kanon ma pierwszenstwo), bez wpisu w ksiedze.
    /// Rzut: Grave (Hero Wounds Grave Percent + 0.4 pp za rok ponad 50, sufit 20%), Serious (Hero Wounds Serious Percent), reszta Light (tylko licznik).
    /// Bohater potrzebny fabule ROT (StoryGuard.Needed): Grave -> Serious, a loze konczy sie najpozniej 7 dni przed jego wydarzeniem (minelo -> Light).
    ///
    /// Loze = HP nie przekracza progu rany (Hero.WoundedHealthLimit - model, nie stala 20) do konca terminu; bohater zostaje w rosterze "ranny" (nie staje w misji
    /// ani w symulacji, pobity idzie do niewoli z szansa 1.0 - zasady gry). Dwie latki: prefiks Hero.HealByAmountInternal (przycina leczenie do progu; przy zerze
    /// pomija metode - chirurg nie bierze XP Medycyny za leczenie bez skutku) i prefiks setera Hero.HitPoints (sciezki bez Heal: bohater bez partii, cudze mody).
    /// Seter NIE przenosi przez prog: rusza tylko wartosc ponad prog przy biezacym HP pod progiem, wiec zadnego HeroWounded i zadnego przeskoku rostera.
    /// Doba: pozostalo -1; -2 w nieoblezonej twierdzy / miescie (Hero Wounds Fortress Rest Double); jeniec zawsze -1. Koniec terminu: wpis znika, gra leczy dalej.
    /// Przesilenie rany Grave (dzien rany + 3..10) jest w zapisie od razu.
    ///
    /// 403 - PRZESILENIE: w dniu przesilenia (doba; nigdy w seterze HP ani w misji) rzut smierci z warunkami Z TEJ CHWILI: p = Hero Wounds Death Base Percent
    /// + Hero Wounds Death Per Year Over40 x lata ponad 40 (sufit 40%); wolny bohater w nieoblezonej twierdzy x0.6; chirurg partii Medicine >= 100 x0.75,
    /// >= 200 x0.5; jeniec x1.5. Trafiony idzie do HeroFate.TryKill (straz fabuly, dziecko, wladca, ostatni z rodu, sufit roku LordToll, akcja gry
    /// ApplyByWounds - nigdy znak smierci). Bohater zajety (bitwa, partia oblega, misja, spotkanie polowe gracza, rozmowa - HeroFate.Busy) -> przesilenie czeka
    /// do nastepnej doby, loze nie konczy sie przed nim; 403-p: najdluzej do dnia rany + 24, potem mija bez rzutu, a galezie bez smierci (klucze NIE, gracz bez
    /// rzutu, opcja kampanii bez smierci w bitwie) nie czekaja wcale. Odmowa -> wpis zostaje jako Serious. Gracz bez rzutu przy Hero Wounds Player Can Die NIE. Kto przezyl przesilenie, ma rzut
    /// okaleczenia Hero Wounds Maim Percent (HeroFate.TryMaim; w dobie, nie w chwili rany - HeroWounded pada w seterze HP w srodku misji).
    ///
    /// Rana krytyczna RealisticBannerlord (15% przy kazdym HeroWounded, -4 HP/dobe) - wylaczona prefiksem przy kluczu Hero Wounds Replace Rb Critical (jedna zasada).
    /// Zapis arm_herowounds_v1: wiersz "id|stopien|dzienRany|pozostalo|dzienPrzesilenia|miejsce|zrodlo" (stary zapis = pusta ksiega). Statyki: Reset w konstruktorze
    /// ArmouryBehavior i w OnGameEnd; bohaterowie rozwiazywani w SessionStart. Potkniecia liczone per miejsce - nic sie nie gasi.</summary>
    internal static partial class HeroWounds
    {
        private const string Tag = "Rany (HW): ";
        private const int CrisisMaxWaitDays = 24;   // 403-p: przesilenie pada 3..10 dni po ranie; odroczenia (bitwa, oblezenie) najdluzej do rana + 24, potem bez rzutu smierci
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const int Serious = 1, Grave = 2;

        private sealed class Row
        {
            public string Id = "", Place = "", Src = "";
            public int Grade, Left, Crisis;      // Crisis: dzien kampanii przesilenia (Grave); 0 = brak; -1 = minelo
            public double Day;                   // dzien kampanii rany (z ulamkiem)
            public Hero H;
            public bool DeferSaid;               // 403: linia "przesilenie odroczone" juz napisana (poza zapisem)
            public bool Fresh, Guard;            // 401-p: wpis z trwajacej bitwy - komunikat i liczniki dopiero, gdy bohater ja przezyl (poza zapisem)
            public MapEvent Me;                  // 401-p: bitwa rany (poza zapisem)
            public int Days0;                    // 401-p: dni loza z rzutu - do odlozonego komunikatu (poza zapisem)
        }

        // ---------------------------------------------------------------- stan (zapis: _rows)
        private static readonly Dictionary<string, Row> _rows = new Dictionary<string, Row>(StringComparer.Ordinal);
        private static readonly Dictionary<Hero, Row> _bed = new Dictionary<Hero, Row>();   // lezacy (rozwiazani bohaterowie) - czytane przez latki
        private static int _n;                                                              // _bed.Count - szybkie wyjscie latek

        // ---------------------------------------------------------------- latki (raz na proces)
        private static Harmony _harmony;
        private static bool _tried;
        private static MethodInfo _mHeal, _mSet, _mRb, _mEnc, _mTip;
        private static bool _rbPresent;
        private static AccessTools.FieldRef<EncyclopediaHeroPageVM, Hero> _encHero;

        // ---------------------------------------------------------------- liczniki (poza zapisem)
        private static int _dEv, _dBattle, _dMission, _dSim, _dLight, _dSerious, _dGrave, _dGuard, _dNoBattle, _dOut, _dCanon, _dHas, _dRose, _dCrisis, _dGone, _dRb;
        private static int _sBattle, _sSerious, _sGrave, _sDays;
        private static int _healCuts, _setCuts, _dStumbles, _stumbles;
        private static int _dDied, _dLived, _dDeferred, _dSpared, _dMaimed, _sDied, _sMaimed;   // 403: przesilenia
        private static int _freshN, _dFell, _sFell, _dDropped, _dNk, _dTest;                    // 401-p: wpisy przed potwierdzeniem, polegli w bitwie rany, zdjete kluczem / HP, Nocny Krol, gracz autotestu
        private static readonly List<Row> _crisisBuf = new List<Row>();
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);

        internal static void SetHarmony(Harmony h) { _harmony = h; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_errSites.Add(where)) Log.Error("HeroWounds." + where, e); } catch { }
        }

        private static string YN(bool b) { return b ? "TAK" : "NIE"; }
        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "?"; } catch { return "?"; } }
        private static bool KeyOn { get { var s = Settings.Current; return s != null && s.HeroWoundsEnabled; } }
        private static string Clean(string t) { return string.IsNullOrEmpty(t) ? "" : t.Replace('|', '/').Replace('\n', ' ').Replace('\r', ' '); }
        private static string GradeName(int g) { return g == Grave ? "GRAVE" : g == Serious ? "SERIOUS" : "LIGHT"; }

        // ---------------------------------------------------------------- cykl zycia
        /// <summary>Konstruktor ArmouryBehavior (nowa kampania i wczytanie - przed SyncData) oraz OnGameEnd: ksiega i liczniki od zera.</summary>
        internal static void Reset()
        {
            _rows.Clear(); _bed.Clear(); _n = 0; _errSites.Clear();
            _dEv = _dBattle = _dMission = _dSim = _dLight = _dSerious = _dGrave = _dGuard = _dNoBattle = _dOut = _dCanon = _dHas = _dRose = _dCrisis = _dGone = _dRb = 0;
            _sBattle = _sSerious = _sGrave = _sDays = 0;
            _healCuts = _setCuts = _dStumbles = _stumbles = 0;
            _dDied = _dLived = _dDeferred = _dSpared = _dMaimed = _sDied = _sMaimed = 0;
            _freshN = _dFell = _sFell = _dDropped = _dNk = _dTest = 0;
            _crisisBuf.Clear();
            AiReset();
        }

        internal static string Export()
        {
            if (_rows.Count == 0) return "";
            var sb = new StringBuilder(64 * _rows.Count);
            foreach (var r in _rows.Values)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(r.Id).Append('|').Append(r.Grade).Append('|').Append(r.Day.ToString("R", Inv)).Append('|').Append(r.Left).Append('|').Append(r.Crisis)
                  .Append('|').Append(Clean(r.Place)).Append('|').Append(Clean(r.Src));
            }
            return sb.ToString();
        }

        /// <summary>Wczytanie: same wiersze (bohaterow rozwiazuje SessionStart - w SyncData swiat nie musi byc gotowy). Zly wiersz pominiety z potknieciem.</summary>
        internal static void Import(string text)
        {
            _rows.Clear(); _bed.Clear(); _n = 0; _freshN = 0;
            if (string.IsNullOrEmpty(text)) return;
            foreach (var line in text.Split('\n'))
            {
                try
                {
                    var p = line.Split('|');
                    if (p.Length < 5 || p[0].Length == 0) continue;
                    var r = new Row { Id = p[0], Grade = int.Parse(p[1], Inv), Day = double.Parse(p[2], Inv), Left = int.Parse(p[3], Inv), Crisis = int.Parse(p[4], Inv),
                                      Place = p.Length > 5 ? p[5] : "", Src = p.Length > 6 ? p[6] : "" };
                    if (r.Grade != Serious && r.Grade != Grave) continue;
                    _rows[r.Id] = r;
                }
                catch (Exception e) { Stumble("Import", e); }
            }
        }

        /// <summary>OnSessionLaunched (po StoryGuard): bohaterowie ksiegi rozwiazani, latki raz na proces, linia startu.</summary>
        internal static void SessionStart()
        {
            try
            {
                _bed.Clear();
                List<string> gone = null;
                foreach (var r in _rows.Values)
                {
                    Hero h = null;
                    try { string id = r.Id; h = Hero.FindFirst(x => x != null && x.StringId == id); } catch (Exception e) { Stumble("SessionStart.Find", e); }
                    if (h == null || !h.IsAlive) { (gone ?? (gone = new List<string>())).Add(r.Id); continue; }
                    r.H = h; _bed[h] = r;
                }
                if (gone != null) foreach (var id in gone) _rows.Remove(id);
                _n = _bed.Count;
                Wire();
                var s = Settings.Current;
                int sr = 0, gr = 0;
                foreach (var r in _rows.Values) { if (r.Grade == Grave) gr++; else sr++; }
                Log.Info("Rany bohaterow (HW): start | klucze: wlaczone " + YN(s != null && s.HeroWoundsEnabled)
                         + (s != null ? ", serious " + s.HeroWoundsSeriousPercent.ToString("0.#", Inv) + "% / " + s.HeroWoundsSeriousDays + " dni, grave "
                                        + s.HeroWoundsGravePercent.ToString("0.#", Inv) + "% / " + s.HeroWoundsGraveDays + " dni, twierdza x2 " + YN(s.HeroWoundsFortressRestDouble)
                                        + ", gracz " + YN(s.HeroWoundsPlayer) + " (loze " + s.HeroWoundsPlayerBedPercent + "%), towarzysze i rod gracza " + YN(s.HeroWoundsCompanions)
                                        + ", zamiast rany krytycznej RB " + YN(s.HeroWoundsReplaceRbCritical) : "")
                         + " | latki: leczenie (Hero.HealByAmountInternal) " + YN(_mHeal != null) + ", seter HP " + YN(_mSet != null)
                         + ", RB krytyczna " + (_mRb != null ? "TAK" : _rbPresent ? "BLAD" : "BRAK (nie ma RealisticBannerlord)")
                         + ", encyklopedia " + YN(_mEnc != null) + ", dymek " + YN(_mTip != null)
                         + " | ksiega " + _rows.Count + " (serious " + sr + ", grave " + gr + (gone != null ? "; usuniete bez bohatera " + gone.Count : "") + ")"
                         + " | 403 przesilenie: smierc od ran " + YN(s != null && s.HeroWoundsDeathEnabled)
                         + (s != null ? " (p " + s.HeroWoundsDeathBasePercent.ToString("0.#", Inv) + "% + " + s.HeroWoundsDeathPerYearOver40.ToString("0.##", Inv)
                                        + " pp/rok ponad 40, gracz " + YN(s.HeroWoundsPlayerCanDie) + ", wladcy " + YN(s.HeroFateRulersCanDie) + ", ostatni z rodu oszczedzony "
                                        + YN(s.HeroFateSpareLastOfHouse) + "), okaleczenie " + s.HeroWoundsMaimPercent.ToString("0.#", Inv) + "%" : "")
                         + " | okaleczen w ksiedze " + HeroFate.MaimRows + " | LordToll rok " + LordToll.Year + ": rany " + LordToll.Count(LordToll.Wounds) + "/" + LordToll.MaxWounds
                         + ", wspolny " + LordToll.Total + "/" + LordToll.MaxAll
                         + " | 408 ranny wodz do twierdzy: klucz " + YN(s != null && s.HeroWoundsLordsRest) + ", latka CampaignEventDispatcher.AiHourlyTick (postfiks 60) " + _aiHook + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        private static void Wire()
        {
            if (_tried || _harmony == null || Campaign.Current == null) return;
            _tried = true;
            // wszystkie cele zadeklarowane w podanych klasach (DeclaredMethod / DeclaredPropertySetter - pulapka 15.10 nie dotyczy)
            _mHeal = Patch("Hero.HealByAmountInternal", () => AccessTools.DeclaredMethod(typeof(Hero), "HealByAmountInternal", new[] { typeof(int), typeof(bool) }),
                           nameof(HealPrefix), null);
            _mSet = Patch("Hero.set_HitPoints", () => AccessTools.DeclaredPropertySetter(typeof(Hero), "HitPoints"), nameof(HpSetPrefix), null);
            Type rb = null;
            try { rb = AccessTools.TypeByName("RealisticBannerlord.Systems.Medicine.CriticalWoundBehavior"); } catch { rb = null; }
            _rbPresent = rb != null;
            if (rb != null) _mRb = Patch("RealisticBannerlord CriticalWoundBehavior.OnHeroWounded", () => AccessTools.DeclaredMethod(rb, "OnHeroWounded", new[] { typeof(Hero) }),
                                         nameof(RbCriticalPrefix), null);
            try { _encHero = AccessTools.FieldRefAccess<EncyclopediaHeroPageVM, Hero>("_hero"); } catch (Exception e) { _encHero = null; Log.Error("HeroWounds.Wire _hero", e); }
            if (_encHero != null)
                _mEnc = Patch("EncyclopediaHeroPageVM.Refresh", () => AccessTools.DeclaredMethod(typeof(EncyclopediaHeroPageVM), "Refresh", Type.EmptyTypes), null, nameof(EncPostfix));
            _mTip = Patch("TooltipRefresherCollection.RefreshHeroTooltip",
                          () => AccessTools.DeclaredMethod(typeof(TooltipRefresherCollection), "RefreshHeroTooltip", new[] { typeof(PropertyBasedTooltipVM), typeof(object[]) }),
                          null, nameof(TipPostfix));
            AiWire();   // 408
        }

        /// <summary>Zwraca MethodInfo tylko, gdy latka weszla (null = brak celu albo blad Harmony).</summary>
        private static MethodInfo Patch(string label, Func<MethodInfo> find, string pre, string post)
        {
            try
            {
                var m = find();
                if (m == null) { Log.Info(Tag + "BLAD latki 401: nie ma metody " + label + "."); return null; }
                _harmony.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(HeroWounds), pre) : null, postfix: post != null ? new HarmonyMethod(typeof(HeroWounds), post) : null);
                return m;
            }
            catch (Exception e) { Log.Error("HeroWounds.Patch " + label, e); return null; }
        }

        // ---------------------------------------------------------------- API (373, FieldDuel, pozniej 402/403/408)
        /// <summary>Bohater lezy (czynny wpis Serious albo Grave). 401-p: przy kluczu Hero Wounds Enabled NIE cale API mowi "nie lezy" (wpisy dalej odliczaja,
        /// ale HP nie jest trzymane - 373, loch 402, encyklopedia i dymek maja wtedy stan sprzed 401).</summary>
        internal static bool Abed(Hero h) { return _n != 0 && h != null && _bed.ContainsKey(h) && KeyOn; }

        /// <summary>0 = bez wpisu, 1 = Serious, 2 = Grave.</summary>
        internal static int GradeOf(Hero h) { Row r; return _n != 0 && h != null && _bed.TryGetValue(h, out r) && KeyOn ? r.Grade : 0; }

        /// <summary>Dni spoczynku do konca loza (0 = bez wpisu).</summary>
        internal static int DaysLeft(Hero h) { Row r; return _n != 0 && h != null && _bed.TryGetValue(h, out r) && KeyOn ? Math.Max(1, r.Left) : 0; }

        /// <summary>Dzien kampanii (z ulamkiem), od ktorego bohater lezy; double.MaxValue gdy nie lezy (porownanie "lezal przed bitwa" daje wtedy NIE).</summary>
        internal static double BedriddenSince(Hero h) { Row r; return _n != 0 && h != null && _bed.TryGetValue(h, out r) && KeyOn ? r.Day : double.MaxValue; }

        /// <summary>Bohater lezal juz PRZED ta bitwa (rana sprzed jej poczatku) - nie jest "rannym z tej bitwy" (SiegeWounded373).</summary>
        internal static bool AbedBefore(Hero h, MapEvent me)
        {
            try
            {
                if (_n == 0 || h == null || me == null) return false;
                Row r;
                return _bed.TryGetValue(h, out r) && KeyOn && r.Day < me.BattleStartTime.ToDays;
            }
            catch (Exception e) { Stumble("AbedBefore", e); return false; }
        }

        /// <summary>Rana Grave, ktorej przesilenie jeszcze nie minelo (403: rzut smierci; 402: goraczka lochu pomija).</summary>
        internal static bool GraveCrisisAhead(Hero h) { Row r; return _n != 0 && h != null && _bed.TryGetValue(h, out r) && r.Grade == Grave && r.Crisis > 0 && KeyOn; }

        // ---------------------------------------------------------------- latki
        /// <summary>Hero.HealByAmountInternal(int healingAmount, bool addXp): lezacemu leczenie przyciete do progu rany; nic do wyleczenia = metoda pominieta
        /// (bez XP Medycyny dla chirurga). Bohater ponad progiem (nie powinno sie zdarzyc) - bez zmian.</summary>
        private static bool HealPrefix(Hero __instance, ref int healingAmount)
        {
            if (_n == 0) return true;
            try
            {
                if (!_bed.ContainsKey(__instance) || !KeyOn) return true;
                int hp = __instance.HitPoints, lim = __instance.WoundedHealthLimit;
                if (hp > lim) return true;
                int room = lim - hp;
                if (room <= 0) { _healCuts++; return false; }
                if (healingAmount > room) { healingAmount = room; _healCuts++; }
                return true;
            }
            catch (Exception e) { Stumble("HealPrefix", e); return true; }
        }

        /// <summary>Seter Hero.HitPoints: lezacemu wartosc ponad prog rany przycieta do progu - tylko gdy biezace HP jest pod progiem albo na nim (nigdy nie
        /// przenosi przez prog, wiec gra nie wola HeroWounded ani nie przestawia rosterow).</summary>
        private static void HpSetPrefix(Hero __instance, ref int value)
        {
            if (_n == 0) return;
            try
            {
                if (!_bed.ContainsKey(__instance) || !KeyOn) return;
                int lim = __instance.WoundedHealthLimit;
                if (value <= lim || __instance.HitPoints > lim) return;
                value = lim; _setCuts++;
            }
            catch (Exception e) { Stumble("HpSetPrefix", e); }
        }

        /// <summary>RealisticBannerlord CriticalWoundBehavior.OnHeroWounded: przy naszych kluczach nie losuje swojej rany krytycznej (stare wpisy RB wygasaja same).</summary>
        private static bool RbCriticalPrefix()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HeroWoundsEnabled || !s.HeroWoundsReplaceRbCritical || Campaign.Current == null) return true;
                _dRb++;
                return false;
            }
            catch { return true; }
        }

        private static string StateText(Row r)
        {
            var s = Settings.Current;   // 403-p: przy Hero Wounds Death Enabled NIE przesilenie nikogo nie zabija - bez grozby w napisie
            return r.Grade == Grave && r.Crisis > 0 && s != null && s.HeroWoundsDeathEnabled ? "Gravely wounded - the crisis has not passed" : "Bedridden (" + Math.Max(1, r.Left) + " days of rest left)";
        }

        /// <summary>Encyklopedia bohatera: wiersz "Wounds" w statystykach lezacego (nie przy ukrytych informacjach o bohaterze).</summary>
        private static void EncPostfix(EncyclopediaHeroPageVM __instance)
        {
            if (_n == 0 && HeroFate.MaimRows == 0) return;
            try
            {
                if (_encHero == null || __instance == null || __instance.IsInformationHidden || __instance.Stats == null) return;
                var h = _encHero(__instance);
                if (h == null) return;
                Row r;
                string bed = _bed.TryGetValue(h, out r) && KeyOn ? StateText(r) : null;
                string scars = HeroFate.Labels(h);   // 403: trwale okaleczenia (takze po wstaniu z loza i po smierci)
                if (bed == null && scars == null) return;
                __instance.Stats.Add(new StringPairItemVM("Wounds:", bed != null && scars != null ? bed + "; " + scars : bed ?? scars));
            }
            catch (Exception e) { Stumble("EncPostfix", e); }
        }

        /// <summary>Dymek bohatera: jedna linia o lozu.</summary>
        private static void TipPostfix(PropertyBasedTooltipVM propertyBasedTooltipVM, object[] args)
        {
            if (_n == 0 && HeroFate.MaimRows == 0) return;
            try
            {
                var h = args != null && args.Length > 0 ? args[0] as Hero : null;
                if (h == null || propertyBasedTooltipVM == null) return;
                bool hidden = false;   // 401-p: jak encyklopedia - nic o bohaterze, ktorego gracz nie zna (gra liczy to samo w RefreshHeroTooltip)
                try { hidden = CampaignUIHelper.IsHeroInformationHidden(h, out _); } catch { hidden = false; }
                if (hidden) return;
                Row r;
                if (_bed.TryGetValue(h, out r) && KeyOn) propertyBasedTooltipVM.AddProperty("", StateText(r));
                string scars = HeroFate.Labels(h);   // 403
                if (scars != null) propertyBasedTooltipVM.AddProperty("", scars);
            }
            catch (Exception e) { Stumble("TipPostfix", e); }
        }

        // ---------------------------------------------------------------- rana
        /// <summary>Bialy Wedrowiec (ROT Race 1) albo upior (Race 2, 401-p) - nie lezy na lozu i nie umiera od ran.</summary>
        private static bool Walker(Hero h) { var c = h.CharacterObject; return c != null && (c.Race == 1 || c.Race == 2); }

        /// <summary>401-p: partia Nocnego Krola (jak ROTDuelsBehavior.IsNightKingParty: wodz - Bialy Wedrowiec, glowa swojego rodu).</summary>
        private static bool NkParty(PartyBase p)
        {
            try
            {
                var lh = p != null ? p.LeaderHero : null;
                var c = lh != null ? lh.CharacterObject : null;
                return c != null && c.Race == 1 && lh.Clan != null && lh.Clan.Leader == lh;
            }
            catch { return false; }
        }

        private static bool NkBattle(MapEvent me)
        {
            try { return me != null && (NkParty(me.AttackerSide != null ? me.AttackerSide.LeaderParty : null) || NkParty(me.DefenderSide != null ? me.DefenderSide.LeaderParty : null)); }
            catch { return false; }
        }

        /// <summary>Komunikat tylko, gdy rzecz dotyczy gracza: on sam, jego rod i towarzysze, jego jeniec, a przy ranie Grave takze lord jego krolestwa.</summary>
        private static bool Concerns(Hero h, bool grave)
        {
            try
            {
                if (h == Hero.MainHero || h.Clan == Clan.PlayerClan || h.IsPlayerCompanion) return true;
                var cap = h.PartyBelongedToAsPrisoner;
                if (cap != null && (cap == PartyBase.MainParty || (cap.IsSettlement && cap.Settlement != null && cap.Settlement.OwnerClan == Clan.PlayerClan))) return true;
                var mf = Hero.MainHero != null ? Hero.MainHero.MapFaction : null;
                return grave && mf != null && mf.IsKingdomFaction && h.MapFaction == mf;
            }
            catch { return false; }
        }

        private static string PlaceOf(Hero h, MapEvent me)
        {
            try
            {
                var st = me.MapEventSettlement;
                if (st != null && st.Name != null) return "at " + st.Name;
                var mp = h.PartyBelongedTo ?? MobileParty.MainParty;
                if (mp == null) return "in the field";
                Vec2 pos = mp.GetPosition2D;
                Settlement best = null;
                float bd = float.MaxValue;
                foreach (var s in Settlement.All)
                {
                    if (s == null || !(s.IsTown || s.IsCastle || s.IsVillage)) continue;
                    float d = s.GetPosition2D.DistanceSquared(pos);
                    if (d < bd) { bd = d; best = s; }
                }
                return best != null && best.Name != null ? "near " + best.Name : "in the field";
            }
            catch { return "in the field"; }
        }

        /// <summary>CampaignEvents.HeroWounded: rzut stopnia rany i wpis do ksiegi. Wolane z wnetrza setera HP - bez zmian HP, bez akcji gry.</summary>
        internal static void OnHeroWounded(Hero h)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HeroWoundsEnabled || h == null || Campaign.Current == null) return;
                _dEv++;
                bool player = h == Hero.MainHero;
                bool mine = !player && (h.IsPlayerCompanion || h.Clan == Clan.PlayerClan);
                if (!h.IsAlive || h.IsChild || h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None || h.Occupation == Occupation.Special || Walker(h)
                    || (player ? !s.HeroWoundsPlayer : mine ? !s.HeroWoundsCompanions : !h.IsLord))
                { _dOut++; return; }
                var hp = h.PartyBelongedTo;
                MapEvent me = hp != null ? hp.MapEvent : null;
                if (me == null && player) { try { me = PlayerEncounter.Battle; } catch { me = null; } }
                if (me == null) { _dNoBattle++; return; }
                if (player && (LogGate.Mode == 1 || LogGate.Mode == 2)) { _dTest++; _dOut++; return; }   // 401-p: DLL autotestu / profilu - gracz testu bitew nie trafia na loze (kolejne bitwy sesji musza otwierac misje)
                string id = h.StringId;
                if (string.IsNullOrEmpty(id)) { _dOut++; return; }
                if (_rows.ContainsKey(id)) { _dHas++; return; }
                if (Chronicle.Actions.HasWound(id)) { _dCanon++; return; }

                bool mission = false;
                try { mission = TaleWorlds.MountAndBlade.Mission.Current != null; } catch { mission = false; }
                _dBattle++; _sBattle++;
                if (mission) _dMission++; else _dSim++;

                // rzut stopnia
                float age = h.Age;
                float gp = Math.Max(0f, Math.Min(100f, s.HeroWoundsGravePercent));
                if (age > 50f && gp > 0f) gp = Math.Min(Math.Max(20f, gp), gp + 0.4f * (age - 50f));
                float sp = Math.Max(0f, Math.Min(100f, s.HeroWoundsSeriousPercent));
                float roll = MBRandom.RandomFloat * 100f;
                int grade = roll < gp ? Grave : roll < gp + sp ? Serious : 0;
                string guard = "nie";
                bool needed = false;
                if (grade != 0 && player && NkBattle(me))
                {
                    // 401-p: bitwa z Nocnym Krolem konczy sie WYMUSZONYM pojedynkiem gracza (ROT: przegrana = smierc postaci) - loze nie moze go trzymac pod progiem rany
                    grade = 0; guard = "gracz w bitwie z Nocnym Krolem -> Light"; _dNk++;
                }
                if (grade != 0)
                {
                    needed = StoryGuard.Needed(h);
                    // 401-p: postac z tabeli obsady przy niewidocznej liscie wydarzen ROT albo nieoceniona (StoryGuard.Shielded) - bez Grave, jak chroniona
                    bool shielded = needed || StoryGuard.Shielded(h);
                    if (shielded && grade == Grave) { grade = Serious; guard = needed ? "Grave -> Serious" : "Grave -> Serious (lista wydarzen niewidoczna / stan nieoceniony)"; }
                    else if (needed) guard = "chroniony";
                }
                int days = 0;
                if (grade != 0)
                {
                    int baseDays = Math.Max(1, grade == Grave ? s.HeroWoundsGraveDays : s.HeroWoundsSeriousDays);
                    days = Math.Max(1, (int)Math.Round(baseDays * (0.67f + 0.66f * MBRandom.RandomFloat)));
                    if (player) days = (int)Math.Round(days * Math.Max(0, Math.Min(100, s.HeroWoundsPlayerBedPercent)) / 100.0);
                    if (needed)
                    {
                        double need = StoryGuard.NextNeedDay(h);
                        if (need >= 0)
                        {
                            int cap = (int)Math.Floor(need - 7.0 - CanonCompanions.StoryDay());
                            if (cap < days) { days = cap; guard += ", loze do 7 dni przed wydarzeniem (plan " + need.ToString("0", Inv) + ")"; }
                        }
                    }
                    if (days <= 0) { grade = 0; guard += ", loze 0 -> Light"; }
                }
                string where = PlaceOf(h, me);
                if (grade == 0)
                {
                    _dLight++;
                    if (s.HeroWoundsLog)
                        Log.Info(Tag + Nm(h) + " (" + id + ", wiek " + ((int)age) + ") ranny " + where + " [" + (mission ? "misja" : "symulacja") + "] - rzut "
                                 + (roll / 100f).ToString("0.00", Inv) + " (grave < " + (gp / 100f).ToString("0.00", Inv) + ", serious < " + ((gp + sp) / 100f).ToString("0.00", Inv)
                                 + ") -> LIGHT | straz fabuly: " + guard + ".");
                    return;
                }
                double now = CampaignTime.Now.ToDays;
                // 401-p: gra najpierw zadaje rane (ten nasluch), a dopiero potem rzuca smierc w tej samej bitwie - komunikat gracza i liczniki serious / grave /
                // dni loza czekaja na koniec bitwy (OnMapEventEnded; zapas: doba). HP trzymamy od razu.
                var r = new Row { Id = id, Grade = grade, Day = now, Left = days, Place = where, Src = mission ? "misja" : "symulacja", H = h,
                                  Crisis = grade == Grave ? (int)now + 3 + MBRandom.RandomInt(8) : 0, Fresh = true, Guard = needed, Me = me, Days0 = days };
                _rows[id] = r; _bed[h] = r; _n = _bed.Count; _freshN++;
                if (s.HeroWoundsLog)
                    Log.Info(Tag + Nm(h) + " (" + id + ", wiek " + ((int)age) + (h.Clan != null && h.Clan.Name != null ? ", " + h.Clan.Name : "") + ") ranny " + where
                             + " [" + r.Src + "] - rzut " + (roll / 100f).ToString("0.00", Inv) + " (grave < " + (gp / 100f).ToString("0.00", Inv) + ", serious < "
                             + ((gp + sp) / 100f).ToString("0.00", Inv) + ") -> " + GradeName(grade) + ", loze " + days + " dni spoczynku"
                             + (grade == Grave ? ", przesilenie dnia " + r.Crisis : "") + " | HP " + h.HitPoints + ", prog " + h.WoundedHealthLimit
                             + " | straz fabuly: " + guard + " | wpis wstepny - komunikat i liczniki po bitwie, jesli przezyje.");
            }
            catch (Exception e) { Stumble("OnHeroWounded", e); }
        }

        /// <summary>401-p: bohater przezyl bitwe swojej rany - wpis liczony (serious / grave / dni loza) i komunikat dla gracza.</summary>
        private static void Confirm(Row r)
        {
            if (!r.Fresh) return;
            r.Fresh = false; r.Me = null; if (_freshN > 0) _freshN--;
            var h = r.H;
            int grade = r.Grade, days = r.Days0;
            if (grade == Grave) { _dGrave++; _sGrave++; } else { _dSerious++; _sSerious++; }
            if (r.Guard) _dGuard++;
            _sDays += days;
            try
            {
                var s = Settings.Current;
                if (h == null || s == null || !s.HeroWoundsEnabled) return;
                if (h == Hero.MainHero)
                    Log.Player((grade == Grave ? "You are gravely wounded." : "You have taken a serious wound.") + " You can ride, but you cannot fight for about " + days + " days."
                               + (s.HeroWoundsFortressRestDouble ? " Rest within castle or town walls mends you twice as fast." : ""), true);
                else if (Concerns(h, grade == Grave))
                    Log.Player(grade == Grave
                        ? Nm(h) + " lies gravely wounded. The healers cannot say whether " + (h.IsFemale ? "she" : "he") + " will live."
                        : Nm(h) + " has taken a serious wound and will be abed for about " + days + " days.", h.MapFaction == Hero.MainHero.MapFaction);
            }
            catch (Exception e) { Stumble("Confirm.Player", e); }
        }

        private static void Drop(Row r)
        {
            if (r.Fresh) { r.Fresh = false; r.Me = null; if (_freshN > 0) _freshN--; }
            _rows.Remove(r.Id);
            if (r.H != null) _bed.Remove(r.H);
            _n = _bed.Count;
        }

        private static void Fell(Row r)
        {
            _dFell++; _sFell++;
            Log.Info(Tag + (r.H != null ? Nm(r.H) : r.Id) + " (" + r.Id + ") polegl w tej samej bitwie - wpis wstepny " + GradeName(r.Grade) + " usuniety bez komunikatu i bez licznikow.");
            Drop(r);
        }

        /// <summary>401-p CampaignEvents.MapEventEnded: wpisy wstepne tej bitwy - polegly (nie zyje albo znak smierci) znika bez komunikatu i bez licznikow,
        /// zywy jest potwierdzany. Bez wpisow wstepnych jedno porownanie.</summary>
        internal static void OnMapEventEnded(MapEvent me)
        {
            if (_freshN == 0) return;
            try
            {
                List<Row> fresh = null;
                foreach (var r in _rows.Values) if (r.Fresh && (r.Me == me || r.Me == null)) (fresh ?? (fresh = new List<Row>())).Add(r);
                if (fresh == null) return;
                foreach (var r in fresh)
                {
                    try
                    {
                        var h = r.H;
                        if (h == null || !h.IsAlive || h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None) Fell(r);
                        else Confirm(r);
                    }
                    catch (Exception e) { Stumble("OnMapEventEnded(" + r.Id + ")", e); }
                }
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        /// <summary>401-p CampaignEvents.MapEventStarted: Nocny Krol staje do bitwy z druzyna gracza, a gracz lezy - loze zdjete i HP przywrocone (ROT konczy
        /// te bitwe wymuszonym pojedynkiem gracza, przegrana = smierc postaci; bez 401 gra dawno by go wyleczyla). Tylko ta jedna bitwa fabuly.</summary>
        internal static void OnMapEventStarted(MapEvent me, PartyBase attacker, PartyBase defender)
        {
            if (_n == 0) return;
            try
            {
                var h = Hero.MainHero;
                Row r;
                if (h == null || !_bed.TryGetValue(h, out r)) return;
                var main = PartyBase.MainParty;
                if (main == null || !((attacker == main && NkParty(defender)) || (defender == main && NkParty(attacker)))) return;
                bool on = KeyOn;
                int hp0 = h.HitPoints;
                Drop(r); _dNk++;
                if (on && h.IsAlive && h.HitPoints < h.MaxHitPoints) h.HitPoints = h.MaxHitPoints;
                Log.Info(Tag + "gracz wstaje z loza przed bitwa z Nocnym Krolem (rana " + GradeName(r.Grade) + " z dnia " + r.Day.ToString("0", Inv) + ", zostalo " + Math.Max(1, r.Left)
                         + " dni) - wpis usuniety, HP " + hp0 + " -> " + h.HitPoints + " (ROT: wymuszony pojedynek po bitwie).");
                if (on) Log.Player("The Night King is upon you. You rise from the sickbed to face him.", true);
            }
            catch (Exception e) { Stumble("OnMapEventStarted", e); }
        }

        // ---------------------------------------------------------------- doba
        /// <summary>Doba ran (po StoryGuard.Daily, przed zaraza): termin loza, przesilenie (403: rzut smierci i okaleczenia), wstajacy, sprzatanie, linia doby.</summary>
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                var s = Settings.Current;
                int day = (int)CampaignTime.Now.ToDays;
                int sr = 0, gr = 0, pris = 0, fort = 0;
                List<Row> done = null;
                foreach (var r in _rows.Values)
                {
                    try
                    {
                        var h = r.H;
                        if (r.Fresh && h != null && (!h.IsAlive || h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None))
                        {
                            // 401-p: zapas dla OnMapEventEnded - polegl w bitwie swojej rany
                            (done ?? (done = new List<Row>())).Add(r); _dFell++; _sFell++;
                            Log.Info(Tag + Nm(h) + " (" + r.Id + ") polegl w tej samej bitwie - wpis wstepny " + GradeName(r.Grade) + " usuniety bez komunikatu i bez licznikow.");
                            continue;
                        }
                        if (h == null || !h.IsAlive)
                        {
                            (done ?? (done = new List<Row>())).Add(r); _dGone++;
                            Log.Info(Tag + (h != null ? Nm(h) : r.Id) + " - wpis usuniety: " + (h == null ? "brak bohatera w grze" : "nie zyje") + " (rana " + GradeName(r.Grade) + " " + r.Place + ").");
                            continue;
                        }
                        if (r.Fresh)
                        {
                            var fp = h.PartyBelongedTo;
                            if (fp == null || fp.MapEvent == null) Confirm(r);   // 401-p: bitwa rany juz sie skonczyla (zapas dla OnMapEventEnded)
                        }
                        if (s != null && s.HeroWoundsEnabled)
                        {
                            // 401-p: klucz zakresu przestawiony na NIE zdejmuje istniejace loze; HP ponad progiem = klucz glowny byl wylaczony i gra bohatera wyleczyla
                            bool pl = h == Hero.MainHero;
                            bool mineH = !pl && (h.IsPlayerCompanion || h.Clan == Clan.PlayerClan);
                            string drop = pl && !s.HeroWoundsPlayer ? "klucz Hero Wounds Player NIE" : mineH && !s.HeroWoundsCompanions ? "klucz Hero Wounds Companions NIE"
                                        : h.HitPoints > h.WoundedHealthLimit ? "HP " + h.HitPoints + " ponad progiem rany (wyleczony przy wylaczonym kluczu)" : null;
                            if (drop != null)
                            {
                                (done ?? (done = new List<Row>())).Add(r); _dDropped++;
                                Log.Info(Tag + Nm(h) + " (" + r.Id + ") - wpis usuniety: " + drop + " (rana " + GradeName(r.Grade) + " " + r.Place + ", zostalo " + Math.Max(1, r.Left) + " dni).");
                                continue;
                            }
                        }
                        int step = 1;
                        if (h.IsPrisoner) pris++;
                        else
                        {
                            var cs = h.CurrentSettlement;
                            if (cs != null && cs.IsFortification && !cs.IsUnderSiege) { fort++; if (s == null || s.HeroWoundsFortressRestDouble) step = 2; }
                        }
                        r.Left -= step;
                        if (r.Grade == Grave && r.Crisis > 0)
                        {
                            // 403: przesilenie przed nami - loze nie konczy sie przed nim; rzut PO petli (smierc bohatera nie moze isc w srodku przegladu ksiegi)
                            if (day >= r.Crisis) _crisisBuf.Add(r);
                            if (r.Left < 1) r.Left = 1;
                        }
                        if (r.Left <= 0)
                        {
                            (done ?? (done = new List<Row>())).Add(r); _dRose++;
                            if (s == null || s.HeroWoundsLog)
                                Log.Info(Tag + Nm(h) + " (" + r.Id + ") wstal z loza - rana " + GradeName(r.Grade) + " z dnia " + r.Day.ToString("0", Inv) + " " + r.Place
                                         + ", HP " + h.HitPoints + " (gra leczy dalej).");
                            try
                            {
                                if (KeyOn)
                                {
                                    if (h == Hero.MainHero) Log.Player("You have risen from the sickbed.");
                                    else if (Concerns(h, r.Grade == Grave)) Log.Player(Nm(h) + " has risen from the sickbed.");
                                }
                            }
                            catch (Exception e) { Stumble("Daily.Player", e); }
                            continue;
                        }
                        if (r.Grade == Grave) gr++; else sr++;
                    }
                    catch (Exception e) { Stumble("Daily(" + r.Id + ")", e); }
                }
                // 403: przesilenia dnia (po petli - TryKill wola akcje gry i jej zdarzenia)
                if (_crisisBuf.Count > 0)
                {
                    for (int i = 0; i < _crisisBuf.Count; i++)
                    {
                        var r = _crisisBuf[i];
                        try
                        {
                            int was = r.Grade;
                            bool died = Crisis(r, s, day);
                            if (died) (done ?? (done = new List<Row>())).Add(r);
                            if (died || r.Grade != was) { if (was == Grave) gr--; else sr--; }
                            if (!died && r.Grade != was) { if (r.Grade == Grave) gr++; else sr++; }
                        }
                        catch (Exception e) { Stumble("Crisis(" + r.Id + ")", e); }
                    }
                    _crisisBuf.Clear();
                }
                if (done != null)
                {
                    foreach (var r in done) Drop(r);
                }
                if (_dEv + _dRose + _dCrisis + _dDeferred + _dGone + _dStumbles + _dRb + _rows.Count + _healCuts + _setCuts + _aiOrders + _aiOff + _dFell + _dDropped + _dNk > 0)
                    Log.Info(Tag + "dzien " + day + " | rany w bitwie dzis " + _dBattle + " (misja " + _dMission + " / symulacja " + _dSim + "): light " + _dLight + ", serious "
                             + _dSerious + ", grave " + _dGrave + ", chronieni fabula " + _dGuard + " | zdarzen HeroWounded " + _dEv + " (poza bitwa " + _dNoBattle + ", poza zakresem "
                             + _dOut + ", rana Kroniki " + _dCanon + ", juz na lozu " + _dHas + ") | na lozu " + sr + " serious / " + gr + " grave (jency " + pris + ", w twierdzy " + fort
                             + ") | wstali " + _dRose + ", przesilenia " + _dCrisis + " (przezyli " + _dLived + ", oszczedzeni / bez rzutu " + _dSpared + ", odroczone " + _dDeferred
                             + "), zmarli " + _dDied + " (rok " + LordToll.Count(LordToll.Wounds) + "/" + LordToll.MaxWounds + ", wspolny " + LordToll.Total + "/" + LordToll.MaxAll
                             + "), okaleczeni " + _dMaimed + ", wpisy bez bohatera " + _dGone + ", polegli w bitwie rany " + _dFell + " (sesja " + _sFell + "), czeka na koniec bitwy " + _freshN
                             + ", zdjete kluczem / HP " + _dDropped + ", Nocny Krol (gracz bez loza) " + _dNk + (_dTest > 0 ? ", gracz autotestu pominiety " + _dTest : "")
                             + " | przyciecia: leczenie " + _healCuts + ", seter "
                             + _setCuts + " | RB rana krytyczna pominieta " + _dRb + " | od startu sesji: ran w bitwie " + _sBattle + ", serious " + _sSerious + ", grave " + _sGrave
                             + ", dni loza " + _sDays + ", zmarli od ran " + _sDied + ", okaleczeni " + _sMaimed + " | potkniecia " + _dStumbles + " (sesja " + _stumbles
                             + ", HeroFate " + HeroFate.Stumbles + ")" + AiDayText() + ".");
                _dEv = _dBattle = _dMission = _dSim = _dLight = _dSerious = _dGrave = _dGuard = _dNoBattle = _dOut = _dCanon = _dHas = _dRose = _dCrisis = _dGone = _dRb = 0;
                _healCuts = _setCuts = _dStumbles = 0;
                _dDied = _dLived = _dDeferred = _dSpared = _dMaimed = 0;
                _dFell = _dDropped = _dNk = _dTest = 0;
                AiDayEnd();   // 408
            }
            catch (Exception e) { Stumble("Daily", e); _crisisBuf.Clear(); }
        }

        // ---------------------------------------------------------------- 403: przesilenie
        /// <summary>Przesilenie rany Grave w dobie. true = bohater zmarl (wpis do usuniecia). Odroczenie zostawia r.Crisis (ponowna proba jutro).</summary>
        private static bool Crisis(Row r, Settings s, int day)
        {
            var h = r.H;
            if (h == null || !h.IsAlive || s == null) return false;
            bool log = s.HeroWoundsLog;
            string head = Tag + Nm(h) + " (" + r.Id + ", wiek " + ((int)h.Age) + ") przesilenie rany z dnia " + r.Day.ToString("0", Inv) + " " + r.Place + ": ";
            string what;
            bool player = h == Hero.MainHero;
            // 403-p: najpierw galezie BEZ smierci - nie potrzebuja odroczenia (klucz NIE = stan sprzed 403; gracz bez rzutu nie czeka na wyjazd z miasta)
            string noRoll = null;
            bool keyOff = !s.HeroWoundsEnabled || !s.HeroWoundsDeathEnabled;
            if (keyOff) noRoll = "klucz Hero Wounds Enabled / Hero Wounds Death Enabled NIE";
            else if (player && !s.HeroWoundsPlayerCanDie) noRoll = "gracz - klucz Hero Wounds Player Can Die NIE";
            else
            {
                // opcja kampanii gry: przy najlatwiejszej bohaterowie nie gina w bitwie (DefaultPartyHealingModel.GetSurvivalChance), przy latwej - gracz
                CampaignOptions.Difficulty bd = CampaignOptions.Difficulty.Realistic;
                try { bd = CampaignOptions.BattleDeath; } catch { }
                if (bd == CampaignOptions.Difficulty.VeryEasy) noRoll = "opcja kampanii: bohaterowie nie gina w bitwie";
                else if (player && bd == CampaignOptions.Difficulty.Easy) noRoll = "opcja kampanii: gracz nie ginie w bitwie";
            }
            if (noRoll == null && HeroFate.Busy(h, out what))
            {
                // 403-p: sufit odroczen - po CrisisMaxWaitDays od rany przesilenie mija bez rzutu (lord oblegajacy przez miesiace nie lezy bez konca)
                if (day >= (int)r.Day + CrisisMaxWaitDays) noRoll = "odroczenia ponad " + CrisisMaxWaitDays + " dob od rany (" + what + ")";
                else
                {
                    _dDeferred++;
                    if (!r.DeferSaid) { r.DeferSaid = true; if (log) Log.Info(head + "odroczone (" + what + ") - ponowna proba co dobe, najdluzej do dnia " + ((int)r.Day + CrisisMaxWaitDays) + "."); }
                    return false;
                }
            }
            r.Crisis = -1; _dCrisis++;
            bool dead = false;
            string outcome;
            if (noRoll != null) { outcome = "bez rzutu smierci (" + noRoll + ")"; _dSpared++; }
            else
            {
                float age = h.Age;
                float p = Math.Max(0f, s.HeroWoundsDeathBasePercent) + Math.Max(0f, s.HeroWoundsDeathPerYearOver40) * Math.Max(0f, age - 40f);
                if (p > 40f) p = 40f;
                p /= 100f;
                string mods = "wiek " + ((int)age);
                if (h.IsPrisoner) { p *= 1.5f; mods += ", jeniec x1.5"; }
                else
                {
                    var cs = h.CurrentSettlement;
                    if (cs != null && cs.IsFortification && !cs.IsUnderSiege) { p *= 0.6f; mods += ", twierdza x0.6"; }
                    var mp = h.PartyBelongedTo;
                    Hero surgeon = mp != null ? mp.EffectiveSurgeon : null;
                    if (surgeon != null && surgeon != h)
                    {
                        int med = surgeon.GetSkillValue(DefaultSkills.Medicine);
                        if (med >= 200) { p *= 0.5f; mods += ", chirurg " + med + " x0.5"; }
                        else if (med >= 100) { p *= 0.75f; mods += ", chirurg " + med + " x0.75"; }
                    }
                }
                if (p > 1f) p = 1f;
                float roll = MBRandom.RandomFloat;
                outcome = "p " + p.ToString("0.00", Inv) + " (" + mods + ") rzut " + roll.ToString("0.00", Inv) + " -> ";
                if (roll >= p) { outcome += "PRZEZYL"; _dLived++; }
                else
                {
                    string why;
                    bool tell = false, mine = false;
                    try { tell = !player && Concerns(h, true); mine = h.MapFaction == Hero.MainHero.MapFaction; } catch { }
                    string name = Nm(h);
                    var fate = HeroFate.TryKill(h, KillCharacterAction.KillCharacterActionDetail.WoundedInBattle, LordToll.Wounds, null, false, out why);
                    if (fate == HeroFate.Fate.Killed)
                    {
                        dead = true; _dDied++; _sDied++;
                        outcome += "ZMARL (" + why + ") | rok " + LordToll.Year + ": rany " + LordToll.Count(LordToll.Wounds) + "/" + LordToll.MaxWounds + ", wspolny "
                                   + LordToll.Total + "/" + LordToll.MaxAll;
                        try { if (tell) Log.Player(name + " has died of wounds taken " + (string.IsNullOrEmpty(r.Place) ? "in battle" : r.Place) + ".", mine); }
                        catch (Exception e) { Stumble("Crisis.Player", e); }
                    }
                    else if (fate == HeroFate.Fate.Deferred && day < (int)r.Day + CrisisMaxWaitDays)
                    {
                        r.Crisis = day + 1; _dCrisis--; _dDeferred++;
                        if (log) Log.Info(head + outcome + why + " - ponowna proba jutro.");
                        return false;
                    }
                    else
                    {
                        _dSpared++;
                        r.Grade = Serious;   // odmowa: rana zostaje jako Serious
                        outcome += (why ?? "odmowa") + " - wpis zostaje jako SERIOUS";
                    }
                }
            }
            // okaleczenie: tylko kto przezyl przesilenie
            string maim = "";
            string maimType = null;
            if (!dead && s.HeroWoundsEnabled && s.HeroWoundsMaimPercent > 0f)
            {
                float mr = MBRandom.RandomFloat * 100f;
                if (mr < s.HeroWoundsMaimPercent)
                {
                    string det;
                    if (HeroFate.TryMaim(h, LordToll.Wounds, out maimType, out det)) { _dMaimed++; _sMaimed++; maim = " | okaleczenie: " + det; }
                    else maim = " | okaleczenie: nie (" + det + ")";
                }
            }
            if (log || dead || maimType != null) Log.Info(head + outcome + maim + ".");
            if (!dead)
            {
                try
                {
                    // 403-p: "przezyl najgorsze" tylko gdy przesilenie bylo grozba (klucze smierci TAK); przy kluczu NIE sam komunikat okaleczenia
                    if (player) { if (!keyOff) Log.Player("You have passed the worst and will live."); if (maimType != null) Log.Player(HeroFate.News(h, maimType), true); }
                    else if (Concerns(h, true))
                    {
                        if (!keyOff) Log.Player(Nm(h) + " has passed the worst and will live.");
                        if (maimType != null) Log.Player(HeroFate.News(h, maimType), h.MapFaction == Hero.MainHero.MapFaction);
                    }
                }
                catch (Exception e) { Stumble("Crisis.Player", e); }
            }
            return dead;
        }
    }
}
