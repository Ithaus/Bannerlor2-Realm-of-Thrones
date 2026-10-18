using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// A16 (14.10, docs/reszta-2026-10-14/ROZPOZNANIE-A16.md) - sygnaly z biegow 120 dob i dwuletniego:
    ///  poz. 1 dobrobyt: (a) RblPeaceNeedsQuiet - prefiks na RBL RealisticProsperityModel.GetDaysSinceLastAttack: doby od Settlement.LastThreatTime warowni i jej
    ///     wsi (oblezenie = 0) zamiast "999, gdy LastAttackerParty == null" (gra zeruje ja po 1 dobie - SettlementVariablesBehavior); (b) BkProsperityLayerOnce
    ///     (domyslnie false) - dubel postfiksu warstwy BK na modelu, ktory wola base (wzor BkPenaltyOnce); (c) linia "A16 Dobrobyt (skladniki)" - suma linii
    ///     ExplainedNumber czynnego modelu po miastach i zamkach (dodatki przed mnoznikiem, mnozniki jako baza x czynnik - suma = wynik modelu);
    ///  poz. 8 linia "A16 Bohaterowie" - zgony wedlug przyczyny, porody, dorastanie, nowi dorosli lordowie AI wedlug zrodla z kontrola zbiorow;
    ///  poz. 9 linia "A16 Ludnosc BK" - kazda zmiana ludnosci BK (PopulationData.UpdatePopType) wedlug rodzaju osady i kontekstu (pobor BK DeduceManpower,
    ///     powroty AddManpowerFromSoldiers naszej reki i BK, jency BK UpdatePopulation, inne), reszta = zmiana sumy - przeplywy (wzrost klas z BetterEconomy); lustro powrotow
    ///     (BkReturnsToOriginVillage - LosersFlee.BkAddPlaces / ClanBudget.ToVillage) liczone wedlug rodzaju osady docelowej.
    /// Latki pomiaru wpinane zawsze (tylko licza), linie przy LogEnabled + A16SignalsLog. Potkniecia liczone, nic sie nie wylacza.
    /// </summary>
    internal static class A16Signals
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal static bool LogOn { get { var s = Settings.Current; return s != null && s.LogEnabled && s.A16SignalsLog; } }
        internal static bool ReturnsToOrigin { get { var s = Settings.Current; return s != null && s.BkReturnsToOriginVillage; } }
        private static bool PeaceOn { get { var s = Settings.Current; return s != null && s.RblPeaceNeedsQuiet; } }

        /// <summary>Glebokosc wywolan BK z naszej reki (LosersFlee.AddBk/BkAddPlaces/AddSlaves, ClanBudget.ToVillage/BkPopAdd) - pomiar "nasze" w linii BK.</summary>
        [ThreadStatic] internal static int Ours;

        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { if (_err.Add(where)) Log.Error("A16Signals." + where, e); } catch { }
        }

        internal static void Reset()
        {
            _stumbles = _stumblesDay = 0; _err.Clear();
            _peaceCalls = _peaceQuiet = 0;
            _lordsY = null; _deathBy.Clear(); _deadIds.Clear(); _agedIds.Clear(); _createdIds.Clear();
            _births = _stillborn = _birthsLord = _deadAll = _deadLord = _deadLordChild = 0;
            Array.Clear(_flow, 0, _flow.Length); Array.Clear(_mir, 0, _mir.Length); _mirLost = 0;
            _bkTotY = null; _w5Y = null;
            BkFlow.Clear();
        }

        // ================================================================ wpiecie
        private static string _wire = "nie wpiete";
        private static MethodInfo _rblDays;

        /// <summary>Menu glowne (OnBeforeInitialModuleScreenSetAsRoot - BK i RBL wpiete): prefiks RBL, dubel warstwy BK, okna pomiaru ludnosci BK.</summary>
        internal static void ApplyAll(Harmony h)
        {
            var parts = new List<string>();
            try
            {
                var t = AccessTools.TypeByName("RealisticBannerlord.Systems.Demographics.RealisticProsperityModel");
                _rblDays = t != null ? AccessTools.Method(t, "GetDaysSinceLastAttack", new[] { typeof(Town) }) : null;
                if (_rblDays != null)
                {
                    h.Patch(_rblDays, prefix: new HarmonyMethod(typeof(A16Signals), nameof(PeacePrefix)) { priority = Priority.First });
                    parts.Add("RBL GetDaysSinceLastAttack: wpiety");
                }
                else parts.Add("RBL GetDaysSinceLastAttack: BRAK (" + (t == null ? "brak RealisticProsperityModel" : "brak metody") + ") - premia pokoju RBL jak dotad");
            }
            catch (Exception e) { parts.Add("RBL: blad"); Stumble("ApplyAll(RBL)", e); }
            try { parts.Add(LayerOnce.ApplyAll(h)); } catch (Exception e) { parts.Add("warstwa BK: blad"); Stumble("ApplyAll(warstwa BK)", e); }
            try { parts.Add(BkFlow.ApplyAll(h)); } catch (Exception e) { parts.Add("pomiar BK: blad"); Stumble("ApplyAll(pomiar BK)", e); }
            _wire = string.Join("; ", parts.ToArray());
            Log.Info("A16 sygnaly: " + _wire + ".");
        }

        /// <summary>Start sesji (po McmSettings.Apply): wylacznik warstwy BK z MCM, linia startowa.</summary>
        internal static void OnSessionLaunched()
        {
            try
            {
                string lay = LayerOnce.Sync();
                var s = Settings.Current;
                Log.Info("A16 sygnaly (start sesji): premia pokoju RBL po 30 dobach spokoju warowni i jej wsi (RblPeaceNeedsQuiet) " + (s != null && s.RblPeaceNeedsQuiet ? "TAK" : "NIE (jak RBL)")
                         + "; warstwa dobrobytu BK: " + lay + "; powrot do ludnosci BK wsi pochodzenia (BkReturnsToOriginVillage) " + (s != null && s.BkReturnsToOriginVillage ? "TAK" : "NIE (jak dotad)")
                         + "; linie A16 " + (LogOn ? "TAK" : "NIE") + " | wpiecia: " + _wire + ".");
            }
            catch (Exception e) { Stumble("OnSessionLaunched", e); }
            _lordsY = null; _bkTotY = null; _w5Y = null;
        }

        // ================================================================ poz. 1 (a): premia pokoju RBL
        private static long _peaceCalls, _peaceQuiet;

        /// <summary>Prefiks First na RBL GetDaysSinceLastAttack(Town town): przy kluczu - doby od ostatniego zagrozenia warowni albo ktorejkolwiek jej wsi.</summary>
        public static bool PeacePrefix(Town town, ref float __result)
        {
            try
            {
                if (!PeaceOn || town == null || town.Settlement == null) return true;
                __result = QuietDays(town);
                _peaceCalls++;
                if (__result >= 30f) _peaceQuiet++;
                return false;
            }
            catch (Exception e) { Stumble("PeacePrefix", e); return true; }
        }

        /// <summary>Doby spokoju warowni: oblezenie = 0; inaczej najmniej dob od LastThreatTime warowni i jej wsi (nigdy - 999).</summary>
        internal static float QuietDays(Town town)
        {
            var st = town.Settlement;
            if (st.SiegeEvent != null) return 0f;
            double d = Elapsed(st);
            var vs = town.Villages;
            if (vs != null) foreach (var v in vs) if (v != null && v.Settlement != null) d = Math.Min(d, Elapsed(v.Settlement));
            return (float)Math.Max(0.0, Math.Min(999.0, d));
        }

        private static double Elapsed(Settlement st)
        {
            var t = st.LastThreatTime;
            if (t == CampaignTime.Zero) return 999.0;
            double e = t.ElapsedDaysUntilNow;
            return e < 0.0 || double.IsNaN(e) ? 999.0 : e;
        }

        /// <summary>Warunek RBL bez latki (do porownania w linii): poza oblezeniem i bez zapamietanego napastnika = 999 dob.</summary>
        private static bool RblPeaceVanilla(Town town)
        {
            var st = town.Settlement;
            return st.SiegeEvent == null && st.LastAttackerParty == null;
        }

        // ================================================================ poz. 1 (b): warstwa BK raz
        /// <summary>Dubel postfiksu BK "BetterEconomyLayering" na CalculateProsperityChange: podklasa z postfiksem BK, ktora w IL wola base z postfiksem BK -
        /// postfiks zdejmowany z podklasy (zostaje na bazie, dziala raz wewnatrz base - jak BkPenaltyOnce). Nic na sztywno: brak dubla - nic do zdjecia.</summary>
        private static class LayerOnce
        {
            private const string BkInstaller = "BannerKings.Patches.BetterEconomy.BKEconomyLayerInstaller";
            private const string BkPostfix = "CalculateProsperityChangePostfix";
            private sealed class Dub { internal MethodBase Target; internal Patch Bk; internal bool Lifted; }
            private static readonly List<Dub> _dubs = new List<Dub>();
            private static Harmony _h;

            private static Patch BkOn(MethodBase m)
            {
                if (m == null) return null;
                var pi = Harmony.GetPatchInfo(m);
                if (pi == null) return null;
                foreach (var p in pi.Postfixes)
                    if (p.PatchMethod != null && p.PatchMethod.Name == BkPostfix && p.PatchMethod.DeclaringType != null && p.PatchMethod.DeclaringType.FullName == BkInstaller) return p;
                return null;
            }

            private static MethodInfo BaseCall(Type t, MethodInfo m)
            {
                foreach (var ci in PatchProcessor.GetOriginalInstructions(m))
                {
                    if (ci.opcode != OpCodes.Call) continue;
                    var callee = ci.operand as MethodInfo;
                    if (callee == null || callee.Name != "CalculateProsperityChange" || callee.DeclaringType == null || callee.DeclaringType == t) continue;
                    if (!callee.DeclaringType.IsAssignableFrom(t)) continue;
                    return callee;
                }
                return null;
            }

            internal static string ApplyAll(Harmony h)
            {
                _h = h;
                var baseT = typeof(DefaultSettlementProsperityModel);
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                    catch { continue; }
                    foreach (var t in types)
                    {
                        try
                        {
                            if (t == null || t.IsAbstract || t == baseT || !baseT.IsAssignableFrom(t)) continue;
                            var m = AccessTools.DeclaredMethod(t, "CalculateProsperityChange", new[] { typeof(Town), typeof(bool) });
                            if (m == null) continue;
                            var bk = BkOn(m);
                            if (bk == null) continue;
                            var callee = BaseCall(t, m);
                            if (callee == null || BkOn(callee) == null) continue;
                            _dubs.Add(new Dub { Target = m, Bk = bk });
                        }
                        catch (Exception e) { Stumble("LayerOnce.Scan", e); }
                    }
                }
                Set(Settings.Current != null && Settings.Current.BkProsperityLayerOnce);
                return "warstwa dobrobytu BK: " + (_dubs.Count == 0 ? "bez dubla" : "dubel w " + Names());
            }

            private static string Names()
            {
                if (_dubs.Count == 0) return "-";
                return string.Join(", ", _dubs.Select(d => d.Target.DeclaringType != null ? d.Target.DeclaringType.FullName : d.Target.Name).ToArray());
            }

            internal static void Set(bool once)
            {
                foreach (var d in _dubs)
                {
                    try
                    {
                        if (once && !d.Lifted) { (_h ?? new Harmony("com.jeff.armoury")).Unpatch(d.Target, d.Bk.PatchMethod); d.Lifted = true; }
                        else if (!once && d.Lifted)
                        {
                            new Harmony(d.Bk.owner).Patch(d.Target, postfix: new HarmonyMethod(d.Bk.PatchMethod, d.Bk.priority, d.Bk.before, d.Bk.after));
                            d.Lifted = false;
                        }
                    }
                    catch (Exception e) { Stumble("LayerOnce.Set(" + once + ")", e); }
                }
            }

            internal static string Sync()
            {
                bool once = Settings.Current != null && Settings.Current.BkProsperityLayerOnce;
                Set(once);
                int k = 0;
                try
                {
                    var pro = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SettlementProsperityModel : null;
                    for (var t = pro != null ? pro.GetType() : null; t != null && t != typeof(object); t = t.BaseType)
                    {
                        var m = AccessTools.DeclaredMethod(t, "CalculateProsperityChange", new[] { typeof(Town), typeof(bool) });
                        if (m != null && BkOn(m) != null) k++;
                    }
                }
                catch (Exception e) { Stumble("LayerOnce.Sync", e); }
                return (_dubs.Count == 0 ? "dubla nie ma" : "dubel w " + Names() + (once ? " - ZDJETY (BkProsperityLayerOnce)" : " - zostaje (BkProsperityLayerOnce = false)"))
                       + ", postfiksow BK na lancuchu czynnego modelu teraz " + k;
            }
        }

        // ================================================================ poz. 1 (c): linia skladnikow dobrobytu
        private static void ProsperityLine(int day)
        {
            var sw = Stopwatch.StartNew();
            var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SettlementProsperityModel : null;
            if (model == null) return;
            var sb = new StringBuilder(2400);
            sb.Append("A16 Dobrobyt (skladniki): dzien ").Append(day).Append(" | model ").Append(model.GetType().Name);
            for (int kind = 0; kind < 2; kind++)
            {
                var list = kind == 0 ? Town.AllTowns : Town.AllCastles;
                var sum = new Dictionary<string, double>();
                var cnt = new Dictionary<string, int>();
                int n = 0, quiet = 0, vanilla = 0, siege = 0;
                double res = 0.0, prosp = 0.0, unexpl = 0.0;
                if (list != null)
                {
                    foreach (var t in list)
                    {
                        try
                        {
                            if (t == null || t.Settlement == null) continue;
                            n++;
                            prosp += t.Prosperity;
                            var en = model.CalculateProsperityChange(t, true);
                            double r = en.ResultNumber, lines = 0.0;
                            res += r;
                            foreach (var ln in en.GetLines())
                            {
                                string name = Clean(ln.name);
                                double v; sum.TryGetValue(name, out v); sum[name] = v + ln.number;
                                int c; cnt.TryGetValue(name, out c); cnt[name] = c + 1;
                                lines += ln.number;
                            }
                            unexpl += r - lines;
                            if (t.Settlement.SiegeEvent != null) siege++;
                            if (QuietDays(t) >= 30f) quiet++;
                            if (RblPeaceVanilla(t)) vanilla++;
                        }
                        catch (Exception e) { Stumble("ProsperityLine(osada)", e); }
                    }
                }
                sb.Append(" | ").Append(kind == 0 ? "miasta " : "zamki ").Append(n).Append(": dobrobyt ").Append(prosp.ToString("0", Inv))
                  .Append(", wynik modelu ").Append(S1(res)).Append("/d = ");
                var ord = sum.OrderByDescending(kv => Math.Abs(kv.Value)).ToList();
                int shown = 0; double rest = 0.0;
                foreach (var kv in ord)
                {
                    if (shown < 16) { sb.Append(kv.Key).Append(' ').Append(S1(kv.Value)).Append(" (").Append(cnt[kv.Key]).Append("), "); shown++; }
                    else rest += kv.Value;
                }
                sb.Append("inne ").Append(S1(rest)).Append(", bez opisu i granice ").Append(S1(unexpl))
                  .Append(" | premia pokoju RBL - warunek spelniony: z latka A16 (30 dob spokoju warowni i wsi) ").Append(quiet)
                  .Append(", bez latki (RBL: brak zapamietanego napastnika) ").Append(vanilla).Append(", oblezone ").Append(siege);
            }
            sb.Append(" | latka RblPeaceNeedsQuiet ").Append(PeaceOn ? "TAK" : "NIE").Append(" (wywolan od wczoraj ").Append(_peaceCalls).Append(", 30+ dob ").Append(_peaceQuiet).Append(')')
              .Append(" | warstwa BK raz (BkProsperityLayerOnce) ").Append(Settings.Current != null && Settings.Current.BkProsperityLayerOnce ? "TAK" : "NIE")
              .Append(" | uwaga: dodatki przed mnoznikiem, mnoznik jako baza x czynnik - suma = wynik modelu | czas ").Append(sw.Elapsed.TotalMilliseconds.ToString("0.0", Inv)).Append(" ms");
            _peaceCalls = _peaceQuiet = 0;
            Log.Info(sb.ToString());
        }

        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "?";
            return s.Replace('|', '/').Replace(';', ',').Replace(':', ' ').Replace('\n', ' ').Replace('\r', ' ').Trim();
        }

        private static string S1(double v) { return (v >= 0 ? "+" : "") + v.ToString("0.0", Inv); }

        // ================================================================ poz. 8: bohaterowie
        private static HashSet<string> _lordsY;            // dorosli zywi lordowie AI wczoraj (StringId)
        private static readonly Dictionary<string, int> _deathBy = new Dictionary<string, int>();
        private static readonly HashSet<string> _deadIds = new HashSet<string>(), _agedIds = new HashSet<string>(), _createdIds = new HashSet<string>();
        private static int _births, _stillborn, _birthsLord, _deadAll, _deadLord, _deadLordChild;

        internal static void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool notify)
        {
            try
            {
                if (victim == null) return;
                _deadAll++;
                if (!victim.IsLord) return;
                if (victim.IsChild) { _deadLordChild++; return; }
                _deadLord++;
                string k = detail.ToString();
                int v; _deathBy.TryGetValue(k, out v); _deathBy[k] = v + 1;
                // recenzja A16: tylko lordowie AI (zbior dnia jest bez rodu gracza) - kontrola zgonow porownuje wydarzenie ze zbiorem
                if (victim.StringId != null && victim.Clan != Clan.PlayerClan) _deadIds.Add(victim.StringId);
            }
            catch (Exception e) { Stumble("OnHeroKilled", e); }
        }

        internal static void OnGivenBirth(Hero mother, List<Hero> children, int stillborn)
        {
            try
            {
                int n = children != null ? children.Count : 0;
                _births += n; _stillborn += stillborn;
                if (mother != null && mother.IsLord) _birthsLord += n;
            }
            catch (Exception e) { Stumble("OnGivenBirth", e); }
        }

        internal static void OnComesOfAge(Hero h) { try { if (h != null && h.StringId != null) _agedIds.Add(h.StringId); } catch (Exception e) { Stumble("OnComesOfAge", e); } }

        internal static void OnHeroCreated(Hero h, bool born) { try { if (h != null && !born && h.StringId != null) _createdIds.Add(h.StringId); } catch (Exception e) { Stumble("OnHeroCreated", e); } }

        private static bool AdultAiLord(Hero h, Clan pc)
        {
            return h != null && h.IsLord && !h.IsChild && h.Clan != null && h.Clan != pc;
        }

        private static void HeroesLine(int day)
        {
            var pc = Clan.PlayerClan;
            var now = new HashSet<string>();
            int alive = 0, lordsAll = 0, lordChildren = 0;
            foreach (var h in Hero.AllAliveHeroes)
            {
                if (h == null) continue;
                alive++;
                if (h.IsLord) { lordsAll++; if (h.IsChild) lordChildren++; }
                if (AdultAiLord(h, pc) && h.StringId != null) now.Add(h.StringId);
            }
            var sb = new StringBuilder(900);
            sb.Append("A16 Bohaterowie: dzien ").Append(day).Append(" | dorosli zywi lordowie AI (bez rodu gracza) ").Append(now.Count);
            if (_lordsY == null)
            {
                sb.Append(" - pierwsza doba sesji (zbior od jutra)");
            }
            else
            {
                int nAged = 0, nCreated = 0, nOther = 0, gDead = 0, gOther = 0;
                foreach (var id in now)
                {
                    if (_lordsY.Contains(id)) continue;
                    if (_agedIds.Contains(id)) nAged++; else if (_createdIds.Contains(id)) nCreated++; else nOther++;
                }
                foreach (var id in _lordsY)
                {
                    if (now.Contains(id)) continue;
                    if (_deadIds.Contains(id)) gDead++; else gOther++;
                }
                int nNew = nAged + nCreated + nOther, nGone = gDead + gOther;
                // recenzja A16: "wczoraj + nowi - ubyli - dzis" bylo zawsze 0 z budowy zbiorow (tozsamosc, nie kontrola). Kontrola zgonow: zgony lordow AI z wydarzenia
                // HeroKilledEvent, ktorych nie bylo we wczorajszym zbiorze (doroslosc albo stworzenie i smierc tej samej doby) - gdy duzo, "ubyli przez zgon" zaniza zgony
                int deadNotY = 0;
                foreach (var id in _deadIds) if (!_lordsY.Contains(id)) deadNotY++;
                sb.Append(" = wczoraj ").Append(_lordsY.Count).Append(" + nowi ").Append(nNew).Append(" (dorosli z dzieci ").Append(nAged).Append(", stworzeni doroslymi ").Append(nCreated)
                  .Append(", inna droga - zawod, rod, z rodu gracza ").Append(nOther).Append(") - ubyli ").Append(nGone).Append(" (zgon ").Append(gDead)
                  .Append(", inna droga - do rodu gracza, utrata zawodu ").Append(gOther).Append(")")
                  .Append(" | kontrola zgonow: z wydarzenia lordow AI ").Append(_deadIds.Count).Append(" = ubyli przez zgon ").Append(gDead)
                  .Append(" + bez wczorajszego wpisu ").Append(deadNotY);
            }
            sb.Append(" | zgony lordow doroslych ").Append(_deadLord).Append(" (");
            bool first = true;
            foreach (var kv in _deathBy.OrderByDescending(x => x.Value)) { if (!first) sb.Append(", "); sb.Append(kv.Key).Append(' ').Append(kv.Value); first = false; }
            sb.Append("), dzieci lordow ").Append(_deadLordChild).Append(", wszystkich bohaterow ").Append(_deadAll)
              .Append(" | porody: dzieci ").Append(_births).Append(" (matka z rodu lordow ").Append(_birthsLord).Append("), martwe ").Append(_stillborn)
              .Append(" | zywi bohaterowie ").Append(alive).Append(", lordowie ").Append(lordsAll).Append(" (dzieci ").Append(lordChildren).Append(")");
            Log.Info(sb.ToString());
            _lordsY = now;
            _deathBy.Clear(); _deadIds.Clear(); _agedIds.Clear(); _createdIds.Clear();
            _births = _stillborn = _birthsLord = _deadAll = _deadLord = _deadLordChild = 0;
        }

        // ================================================================ poz. 9: ludnosc BK wedlug rodzaju osady i kontekstu
        internal const int KVil = 0, KCas = 1, KTown = 2, KOther = 3, Kinds = 4;
        // konteksty: pobor BK, powrot naszej reki, powrot BK (wlasny), wzrost BK, nasze inne (niewolnicy, zastepczo chlopi), inne BK
        internal const int CLevy = 0, COurRet = 1, CBkRet = 2, CGrowth = 3, COurOther = 4, COther = 5, Ctx = 6;
        private static readonly long[] _flow = new long[Kinds * Ctx * 2];     // [rodzaj][kontekst][0 = plus, 1 = minus]
        private static readonly long[] _mir = new long[Kinds * 2];            // lustro A16: [rodzaj][0 = BK przyjal, 1 = nie przyjal]
        private static long[] _bkTotY;

        internal static int KindOf(Settlement st)
        {
            if (st == null) return KOther;
            if (st.IsVillage) return KVil;
            if (st.IsCastle) return KCas;
            if (st.IsTown) return KTown;
            return KOther;
        }

        /// <summary>A16: lustro powrotow - ok ludzi BK przyjal w osadzie st, miss nie (wolajacy daje ich BK jak dotad).</summary>
        internal static void NoteMirror(Settlement st, int ok, int miss)
        {
            int k = KindOf(st);
            if (ok > 0) _mir[k * 2] += ok;
            if (miss > 0) _mir[k * 2 + 1] += miss;
        }

        // recenzja A16: ludzie z lustra, ktorych ksiega juz oddala do DOM, a ludnosc BK nie dostala nigdzie (osada pochodzenia bez danych BK i brak osady
        // zastepczej FindHome / wies rodu bez danych BK albo BK nie przyjal) - rozjazd obu ksiag; oczekiwane 0, prog A16-9c
        private static long _mirLost;
        internal static void NoteMirrorLost(int n) { if (n > 0) _mirLost += n; }

        private static class BkFlow
        {
            [ThreadStatic] private static int _levy, _ret, _growth;
            private static MethodInfo _settlementOf;
            private static readonly Dictionary<object, int> _kindOf = new Dictionary<object, int>();   // PopulationData -> rodzaj osady (klucz po referencji)
            internal static void Clear() { _kindOf.Clear(); }

            internal static string ApplyAll(Harmony h)
            {
                var pdT = AccessTools.TypeByName("BannerKings.Managers.Populations.PopulationData");
                var mdT = AccessTools.TypeByName("BannerKings.Managers.Populations.MilitaryData");
                if (pdT == null) return "pomiar ludnosci BK: BRAK PopulationData";
                _settlementOf = AccessTools.PropertyGetter(pdT, "Settlement");
                var miss = new List<string>();
                var upd = AccessTools.Method(pdT, "UpdatePopType");
                if (upd != null && _settlementOf != null) h.Patch(upd, postfix: new HarmonyMethod(typeof(BkFlow), nameof(TypePost)) { priority = Priority.Last });
                else miss.Add("UpdatePopType");
                var grow = AccessTools.Method(pdT, "UpdatePopulation");
                if (grow != null) h.Patch(grow, prefix: new HarmonyMethod(typeof(BkFlow), nameof(GrowPre)) { priority = Priority.First }, finalizer: new HarmonyMethod(typeof(BkFlow), nameof(GrowFin)));
                else miss.Add("UpdatePopulation");
                int levy = 0;
                if (mdT != null)
                {
                    foreach (var m in AccessTools.GetDeclaredMethods(mdT))
                    {
                        if (m.Name == "DeduceManpower")
                        {
                            h.Patch(m, prefix: new HarmonyMethod(typeof(BkFlow), nameof(LevyPre)) { priority = Priority.First }, finalizer: new HarmonyMethod(typeof(BkFlow), nameof(LevyFin)));
                            levy++;
                        }
                        else if (m.Name == "AddManpowerFromSoldiers")
                            h.Patch(m, prefix: new HarmonyMethod(typeof(BkFlow), nameof(RetPre)) { priority = Priority.First }, finalizer: new HarmonyMethod(typeof(BkFlow), nameof(RetFin)));
                    }
                }
                if (levy == 0) miss.Add("DeduceManpower");
                return "pomiar ludnosci BK: " + (miss.Count == 0 ? "wpiety" : "BRAK " + string.Join(", ", miss.ToArray()));
            }

            public static void LevyPre(out bool __state) { _levy++; __state = true; }
            public static Exception LevyFin(Exception __exception, bool __state) { if (__state) _levy--; return __exception; }
            public static void RetPre(out bool __state) { _ret++; __state = true; }
            public static Exception RetFin(Exception __exception, bool __state) { if (__state) _ret--; return __exception; }
            public static void GrowPre(out bool __state) { _growth++; __state = true; }
            public static Exception GrowFin(Exception __exception, bool __state) { if (__state) _growth--; return __exception; }

            public static void TypePost(object __instance, int count)
            {
                if (count == 0) return;
                try
                {
                    int k;
                    if (!_kindOf.TryGetValue(__instance, out k))
                    {
                        var st = _settlementOf.Invoke(__instance, null) as Settlement;
                        k = KindOf(st);
                        if (st != null) _kindOf[__instance] = k;        // osada danych BK sie nie zmienia - rodzaj raz na obiekt
                    }
                    int c = Ours > 0 ? (_ret > 0 ? COurRet : COurOther)
                          : _levy > 0 ? CLevy : _ret > 0 ? CBkRet : _growth > 0 ? CGrowth : COther;
                    int i = (k * Ctx + c) * 2 + (count > 0 ? 0 : 1);
                    _flow[i] += Math.Abs((long)count);
                }
                catch (Exception e) { Stumble("BkFlow.TypePost", e); }
            }
        }

        // suma ludnosci BK wedlug rodzaju (refleksja jak LosersFlee.BkResolve)
        private static object _popMgr;
        private static MethodInfo _getPopData;
        private static PropertyInfo _totalPop;
        private static bool _bkTried;

        private static bool BkResolve()
        {
            if (_getPopData != null && _popMgr != null && _totalPop != null) return true;
            if (_bkTried) return false;
            _bkTried = true;
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var inst = cfgT != null ? AccessTools.Property(cfgT, "Instance")?.GetValue(null, null) : null;
                _popMgr = inst != null ? AccessTools.Property(cfgT, "PopulationManager")?.GetValue(inst, null) : null;
                if (_popMgr == null) return false;
                _getPopData = AccessTools.Method(_popMgr.GetType(), "GetPopData", new[] { typeof(Settlement) });
                var pdT = _getPopData != null ? _getPopData.ReturnType : null;
                _totalPop = pdT != null ? AccessTools.Property(pdT, "TotalPop") : null;
                return _totalPop != null;
            }
            catch { return false; }
        }

        private static readonly string[] KName = { "wsie", "zamki", "miasta", "inne" };
        // recenzja A16: BK w tej wersji nie rosnie przez UpdatePopulation (jedyne wywolanie - BKPartyBehavior: jency partii BK do osady jako niewolni); wzrost
        // i spadek klas idzie z BetterEconomy (PopulationData.Update -> SyncClassesFromBetterEconomy -> MirrorClassCount ustawia liczby) - to jest w "reszcie"
        private static readonly string[] CName = { "pobor BK", "powroty naszej reki", "powroty BK", "jency BK do osady (UpdatePopulation)", "nasze inne (niewolni, chlopi zastepczo)", "inne BK" };

        private static void BkLine(int day)
        {
            var tot = new long[Kinds];
            var cnt = new int[Kinds];
            bool ok = BkResolve();
            if (ok)
            {
                foreach (var st in Settlement.All)
                {
                    try
                    {
                        if (st == null || !(st.IsVillage || st.IsCastle || st.IsTown)) continue;
                        var pd = _getPopData.Invoke(_popMgr, new object[] { st });
                        if (pd == null) continue;
                        int k = KindOf(st);
                        tot[k] += Convert.ToInt64(_totalPop.GetValue(pd, null)); cnt[k]++;
                    }
                    catch (Exception e) { Stumble("BkLine(osada)", e); }
                }
            }
            var sb = new StringBuilder(1600);
            sb.Append("A16 Ludnosc BK: dzien ").Append(day);
            if (!ok) sb.Append(" | BK nieobecny (brak PopulationManager)");
            for (int k = 0; k < 3; k++)
            {
                sb.Append(" | ").Append(KName[k]).Append(' ').Append(cnt[k]).Append(": ").Append(tot[k]);
                long net = 0;
                var parts = new StringBuilder();
                for (int c = 0; c < Ctx; c++)
                {
                    long p = _flow[(k * Ctx + c) * 2], m = _flow[(k * Ctx + c) * 2 + 1];
                    net += p - m;
                    parts.Append(", ").Append(CName[c]).Append(" +").Append(p).Append('/').Append('-').Append(m);
                }
                if (_bkTotY != null)
                {
                    long d = tot[k] - _bkTotY[k];
                    sb.Append(" (zmiana ").Append(d >= 0 ? "+" : "").Append(d).Append(" = przeplywy UpdatePopType ").Append(net >= 0 ? "+" : "").Append(net)
                      .Append(" + reszta ").Append(d - net >= 0 ? "+" : "").Append(d - net).Append(')');
                }
                else sb.Append(" (pierwsza doba sesji - zmiana od jutra)");
                sb.Append(parts.ToString());
                sb.Append("; lustro A16 do tej osady: BK przyjal ").Append(_mir[k * 2]).Append(", nie przyjal ").Append(_mir[k * 2 + 1]);
            }
            sb.Append(" | powrot do ludnosci BK osady pochodzenia (BkReturnsToOriginVillage) ").Append(ReturnsToOrigin ? "TAK" : "NIE")
              .Append(" | lustro A16 - BK nie dostal nigdzie (ksiega liczy ich jako powrot) ").Append(_mirLost)
              .Append(" | reszta = zmiany poza UpdatePopType (wzrost i spadek klas z BetterEconomy - SyncClassesFromBetterEconomy, przyciecie do 0) | potkniecia A16 dzis ").Append(_stumblesDay).Append(" (razem ").Append(_stumbles).Append(')');
            Log.Info(sb.ToString());
            _bkTotY = ok ? tot : null;
            Array.Clear(_flow, 0, _flow.Length); Array.Clear(_mir, 0, _mir.Length); _mirLost = 0;
        }

        // ================================================================ poz. 10: W5 - wykorzystanie miejsca na werbunek
        private static Dictionary<Clan, int> _w5Y;            // rody, ktorym W5 wczoraj dal miejsce ponad gotowke -> ludzie w druzynach wojennych wtedy

        private static void ClanMen(Clan c, out int men, out int lim, out int parties, out int inArmy)
        {
            men = lim = parties = inArmy = 0;
            var wp = c.WarPartyComponents;
            if (wp == null) return;
            foreach (var w in wp)
            {
                var mp = w != null ? w.MobileParty : null;
                if (mp == null || !mp.IsActive || mp.MemberRoster == null) continue;
                parties++;
                men += mp.MemberRoster.TotalManCount;
                if (mp.Party != null) lim += mp.Party.PartySizeLimit;
                if (mp.Army != null && mp.Army.LeaderParty != mp) inArmy++;
            }
        }

        /// <summary>Linia "A16 W5 miejsce": rody z miejscem W5 (przydzial ponad gotowke - miejsce na krok werbunku) wczoraj - ilu z nich ma dzis wiecej ludzi;
        /// mozliwe przyczyny braku werbunku z odczytu (wodz w niewoli, bez partii wojennej, partie w cudzej armii). Brak ochotnikow w zasiegu - poza pomiarem.</summary>
        private static void W5Line(int day)
        {
            var now = new Dictionary<Clan, int>();
            int nRoom = 0; long freeNow = 0;
            var pc = Clan.PlayerClan;
            foreach (var c in Clan.All)
            {
                try
                {
                    if (c == null || c.IsEliminated || c == pc) continue;
                    // recenzja f15: miara W5 bez czesci W5c (skarbiec dla srednich pisze do tych samych slownikow przydzialu i gotowki); klucz wylaczony - 0, linia jak dotad
                    double b = KingdomTreasury.NeedAidBoostOf(c) - KingdomTreasury.MidAidBoostOf(c);
                    long cash = KingdomTreasury.NeedAidCashOf(c) - KingdomTreasury.MidAidCashOf(c);
                    if (!(b > cash + 0.5)) continue;
                    int men, lim, parties, arm;
                    ClanMen(c, out men, out lim, out parties, out arm);
                    now[c] = men; nRoom++; freeNow += Math.Max(0, lim - men);
                }
                catch (Exception e) { Stumble("W5Line(rod)", e); }
            }
            var sb = new StringBuilder(500);
            sb.Append("A16 W5 miejsce: dzien ").Append(day).Append(" | dzis rodow z miejscem ponad gotowke ").Append(nRoom).Append(" (wolne miejsca w druzynach ").Append(freeNow).Append(')');
            if (_w5Y != null)
            {
                int up = 0, same = 0, down = 0, pris = 0, noParty = 0, army = 0, gone = 0; long gain = 0;
                foreach (var kv in _w5Y)
                {
                    try
                    {
                        var c = kv.Key;
                        if (c == null || c.IsEliminated) { gone++; continue; }
                        int men, lim, parties, arm;
                        ClanMen(c, out men, out lim, out parties, out arm);
                        int d = men - kv.Value;
                        if (d > 0) { up++; gain += d; } else if (d == 0) same++; else down++;
                        if (d <= 0)
                        {
                            if (c.Leader != null && c.Leader.IsPrisoner) pris++;
                            if (parties == 0) noParty++;
                            if (arm > 0) army++;
                        }
                    }
                    catch (Exception e) { Stumble("W5Line(wczoraj)", e); }
                }
                sb.Append(" | wczoraj ").Append(_w5Y.Count).Append(": dzis wiecej ludzi ").Append(up).Append(" (+").Append(gain).Append("), bez zmiany ").Append(same).Append(", mniej ").Append(down)
                  .Append(", rod zniknal ").Append(gone).Append(" | bez werbunku (bez zmiany albo mniej): wodz w niewoli ").Append(pris).Append(", bez partii wojennej ").Append(noParty)
                  .Append(", partie w cudzej armii ").Append(army).Append(" (reszta - brak ochotnikow w zasiegu albo straty wieksze niz werbunek - poza pomiarem)");
            }
            else sb.Append(" | pierwsza doba sesji (porownanie od jutra)");
            Log.Info(sb.ToString());
            _w5Y = now;
        }

        // ================================================================ doba
        internal static void Daily()
        {
            if (!LogOn || Campaign.Current == null) { Array.Clear(_flow, 0, _flow.Length); Array.Clear(_mir, 0, _mir.Length); _mirLost = 0; _lordsY = null; _bkTotY = null; _w5Y = null; return; }
            int day = (int)CampaignTime.Now.ToDays;
            try { ProsperityLine(day); } catch (Exception e) { Stumble("ProsperityLine", e); }
            try { HeroesLine(day); } catch (Exception e) { Stumble("HeroesLine", e); }
            try { BkLine(day); } catch (Exception e) { Stumble("BkLine", e); }
            try { W5Line(day); } catch (Exception e) { Stumble("W5Line", e); }
            _stumblesDay = 0;
        }
    }

    /// <summary>A16: nasluchy kampanii (doba, bohaterowie, start sesji) - bez zapisu (liczniki sesji).</summary>
    internal sealed class A16Behavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            A16Signals.Reset();
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { A16Signals.OnSessionLaunched(); });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, A16Signals.Daily);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, A16Signals.OnHeroKilled);
            CampaignEvents.OnGivenBirthEvent.AddNonSerializedListener(this, A16Signals.OnGivenBirth);
            CampaignEvents.HeroComesOfAgeEvent.AddNonSerializedListener(this, A16Signals.OnComesOfAge);
            CampaignEvents.HeroCreated.AddNonSerializedListener(this, A16Signals.OnHeroCreated);
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
