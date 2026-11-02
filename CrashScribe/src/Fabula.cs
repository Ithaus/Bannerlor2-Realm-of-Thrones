using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace CrashScribe
{
    /// <summary>
    /// ROZRUSZNIK FABULY ROT, wedle zyczen Jeffa ("sprawdz daty wzgledem
    /// serialu" + kaskada po wlaczeniu fabuly w trwajacej kampanii):
    ///
    /// 1. KOLEJNOSC SERIALU. Dwa wydarzenia ROT stoja w zlym miejscu osi:
    ///    egzekucja Karstarka (dzien 100) i bunt u Crasterow (150) odpalaly
    ///    PRZED Czarnym Nurtem (177) - w serialu i ksiazkach oba sa PO nim
    ///    (S3 kontra S2E9). Przesuwamy: Karstark 190, Craster 200. Reszta osi
    ///    ROT trzyma sie serialu zaskakujaco wiernie (rok Bannerlorda = 84 dni,
    ///    wiec Ned~0, Renly~1.4 roku, Krwawe Gody~2.6, Dany~5.4 - serial:
    ///    0 / ~1 / ~2 / ~6 lat).
    ///
    /// 2. TEMPO. Po wlaczeniu fabuly w kampanii, ktora dawno minela wszystkie
    ///    bramki dat, WSZYSTKIE wydarzenia chca odpalic w jeden-dwa dni
    ///    (lawina popupow, wojny i przewroty naraz). Rozrusznik przepuszcza
    ///    JEDNO wydarzenie na FabulaPaceDays dni, W KOLEJNOSCI osi czasu -
    ///    opowiesc nadrabia zaleglosci po kolei, jak sezony, nie jak lawina.
    ///
    /// 3. ZATOR. Wydarzenie, ktoremu swiat odjechal (aktor nie zyje, zamek
    ///    w cudzych rekach), potrafi wisiec "pending" wiecznie - po kilku
    ///    dniach na czele kolejki zostaje ominiete, zeby nie tamowac reszty.
    ///
    /// Wydarzenia BEZ daty (smierc Stannisa, Piesc, Krolewska Przystan...)
    /// sa lancuchowe - zadnego rozrusznika, odpalaja po swojemu.
    /// </summary>
    internal static class Fabula
    {
        // os czasu wedle SERIALU (dzien kampanii ROT; 84 dni = rok Bannerlorda)
        private static readonly Dictionary<string, float> Timeline = new Dictionary<string, float>
        {
            { "NedEvent", 2f },
            { "HarrenhalSiegeNotificationEvent", 18f },
            { "RiverlandsDeclareWarEvent", 25f },
            { "HarrenhalSiegeEvent", 27f },
            { "RenlyDeathEvent", 120f },
            { "BlackwaterSiegeNotificationEvent", 170f },
            { "BlackwaterSiegeEvent", 177f },
            { "RickardKarstarkExecutionEvent", 190f },   // ROT mial 100 - PRZED Czarnym Nurtem (S3E5 jest po)
            { "JeorMutinyEvent", 200f },                 // ROT mial 150 - bunt u Crasterow to S3, po Czarnym Nurcie
            { "RedWeddingEvent", 220f },
            { "JoffreyEvent", 250f },
            { "WallSiegeNotificationEvent", 260f },
            { "WallSiegeEvent", 267f },
            { "EastwatchEvent", 280f },                  // Stannis rusza pod Mur zaraz po szturmie Mance'a - jak w serialu
            { "RamsayEvent", 300f },
            { "JonEvent", 325f },
            { "RooseDeathEvent", 336f },
            { "BastardsSiegeNotificationEvent", 350f },
            { "BastardsSiegeEvent", 357f },
            { "AegonInvasionEvent", 400f },              // watek ksiazkowy - przed powrotem Dany, jak w ADWD
            { "DanyInvasionEvent", 450f },
        };

        private static readonly HashSet<string> Stuck = new HashSet<string>();
        private static double _lastStartDay = -9999.0;
        private static double _lastDiagDay = -9999.0;
        private static object _lastStarted;
        private static double _lastStartedFor = -1.0;
        private static string _headName;
        private static double _headSinceDay;
        private const double StuckDays = 6.0;

        private static System.Reflection.FieldInfo _fEvents;
        private static System.Reflection.PropertyInfo _pTotal, _pActive;
        private static object _behavior;
        private static Campaign _behCampaign;

        /// <summary>
        /// Data z osi po rozciagnieciu. Przy dluzszym roku (Armoury potrafi dac
        /// rok 168-dniowy) daty w DNIACH zostalyby te same, ale w LATACH GRY
        /// fabula pedzilaby dwa razy szybciej niz w serialu - mnoznik to prostuje.
        /// </summary>
        private static float Scaled(float day)
        {
            float k = Config.FabulaTimeScale;
            if (k < 0.25f) k = 0.25f;
            if (k > 6f) k = 6f;
            return day * k;
        }

        private static double DayNow()
        {
            try
            {
                return StoryClock.Days();   // m101-prolog: od startu opowiesci (w prologu 298 ujemne - bramki zamkniete)
            }
            catch { return 0.0; }
        }

        private static System.Collections.IEnumerable Events()
        {
            try
            {
                if (_behavior == null || _behCampaign != Campaign.Current)
                {
                    _behCampaign = Campaign.Current;
                    _behavior = null;
                    var tBeh = Type.GetType("ROT.CampaignBehaviors.ROTEventBehavior, ROT");
                    if (tBeh == null || Campaign.Current == null) return null;
                    var mi = typeof(Campaign).GetMethod("GetCampaignBehavior");
                    if (mi != null) _behavior = mi.MakeGenericMethod(tBeh).Invoke(Campaign.Current, null);
                    if (_fEvents == null) _fEvents = AccessTools.Field(tBeh, "_events");
                }
                return _behavior != null && _fEvents != null
                    ? _fEvents.GetValue(_behavior) as System.Collections.IEnumerable : null;
            }
            catch { return null; }
        }

        private static bool Pending(object ev)
        {
            try
            {
                if (ev == null) return false;
                if (!Props(ev)) return false;
                int total = _pTotal != null ? Convert.ToInt32(_pTotal.GetValue(ev, null)) : 0;
                bool active = _pActive != null && Convert.ToBoolean(_pActive.GetValue(ev, null));
                return total == 0 && !active;
            }
            catch { return false; }
        }

        /// <summary>Wlasciwosci TotalEvents / IsActive z klasy bazowej wydarzen ROT (leniwie, raz).</summary>
        private static bool Props(object ev)
        {
            if (_pTotal == null || _pActive == null)
            {
                // z KLASY BAZOWEJ, nie z pierwszego napotkanego wydarzenia
                var tBase = Type.GetType("ROT.Events.EventBase, ROT") ?? ev.GetType();
                _pTotal = AccessTools.Property(tBase, "TotalEvents");
                _pActive = AccessTools.Property(tBase, "IsActive");
                if (_pTotal == null || _pActive == null) return false;
            }
            return true;
        }

        /// <summary>f19-kanon-a: stan wydarzenia ROT po nazwie typu (np. "RenlyDeathEvent"): -1 = nie widac listy wydarzen ROT
        /// albo tego wydarzenia, 0 = czeka (nie wystartowalo), 1 = trwa (IsActive), 2 = zakonczone (TotalEvents > 0, nie trwa).
        /// Tylko odczyt - ta sama lista i te same wlasciwosci, z ktorych korzysta rozrusznik.</summary>
        internal static int EventState(string typeName)
        {
            try
            {
                var list = Events();
                if (list == null) return -1;
                foreach (var ev in list)
                {
                    if (ev == null || ev.GetType().Name != typeName) continue;
                    if (!Props(ev)) return -1;
                    int total = Convert.ToInt32(_pTotal.GetValue(ev, null));
                    bool active = Convert.ToBoolean(_pActive.GetValue(ev, null));
                    return active ? 1 : (total > 0 ? 2 : 0);
                }
                return -1;
            }
            catch { return -1; }
        }

        /// <summary>
        /// Prefix na ConditionsMet KAZDEGO datowanego wydarzenia ROT:
        /// wlasna bramka daty (korekta serialowa), kolejnosc osi i tempo.
        /// </summary>
        public static bool GatePrefix(object __instance, ref bool __result)
        {
            try
            {
                if (!Config.FabulaPacerEnabled || __instance == null) return true;
                string my = __instance.GetType().Name;
                float myDay;
                if (!Timeline.TryGetValue(my, out myDay)) return true;
                myDay = Scaled(myDay);
                double day = DayNow();
                Diag(day);

                // 1. bramka daty (serialowa korekta - nigdy wczesniej niz na osi)
                if (day < myDay) { __result = false; return false; }

                // 2. kolejnosc: najwczesniejsze ZALEGLE wydarzenie idzie pierwsze
                var list = Events();
                if (list != null)
                {
                    string head = null; float headDay = float.MaxValue;
                    foreach (var ev in list)
                    {
                        if (ev == null) continue;
                        var nm = ev.GetType().Name;
                        float d;
                        if (!Timeline.TryGetValue(nm, out d)) continue;
                        d = Scaled(d);
                        if (d > day || Stuck.Contains(nm)) continue;
                        if (!Pending(ev)) continue;
                        if (d < headDay) { headDay = d; head = nm; }
                    }
                    if (_headName != head) { _headName = head; _headSinceDay = day; }
                    else if (head != null && day - _headSinceDay > StuckDays)
                    {
                        // czolo kolejki stoi od tygodnia - swiat mu odjechal; omijamy
                        Stuck.Add(head);
                        Scribe.Line("FABULA: " + head + " nie moze odpalic (warunki swiata ROT) - kolejka idzie dalej (dzien " + ((int)day) + ").");
                        _headName = null;
                        __result = false; return false;
                    }
                    if (head != null && head != my) { __result = false; return false; }
                }

                // 3. tempo: jedno wydarzenie na FabulaPaceDays dni
                if (day - _lastStartDay < Math.Max(0, Config.FabulaPaceDays)) { __result = false; return false; }
                return true;   // oryginalne warunki ROT decyduja dalej
            }
            catch { return true; }
        }

        /// <summary>Postfix na EventBase.StartEvent: znacznik tempa + slad w logu.</summary>
        public static void StartPostfix(object __instance)
        {
            try
            {
                double d = DayNow();
                // wydarzenia nadpisuja StartEvent i wolaja base - lapiemy oba, meldujemy raz
                if (ReferenceEquals(__instance, _lastStarted) && Math.Abs(d - _lastStartedFor) < 0.01) return;
                _lastStarted = __instance; _lastStartedFor = d;
                _lastStartDay = d;
                if (__instance != null)
                    Scribe.Line("FABULA: wydarzenie " + __instance.GetType().Name + " WYSTARTOWALO (dzien "
                                + ((int)d) + ").");
            }
            catch { }
        }

        /// <summary>
        /// Raz na dzien kampanii: gdzie stoi kolejka fabuly. Bez tego nie widac,
        /// czy bramka trzyma wydarzenie, czy to ROT nie chce go odpalic.
        /// 320a: najpierw Resolve (martwe wydarzenia ROT); "zaleglych" liczy tylko wydarzenia, ktorych data juz minela,
        /// "czolo kolejki" pomija ominiete (Stuck) - jak bramka; osobno liczba wydarzen z data w przyszlosci.
        /// </summary>
        private static void Diag(double day)
        {
            try
            {
                if (day - _lastDiagDay < 1.0) return;
                _lastDiagDay = day;
                Resolve(day);
                var list = Events();
                string head = null; float headDay = float.MaxValue; int pend = 0, future = 0;
                if (list != null)
                    foreach (var ev in list)
                    {
                        if (ev == null) continue;
                        var nm = ev.GetType().Name;
                        float dd;
                        if (!Timeline.TryGetValue(nm, out dd)) continue;
                        if (!Pending(ev)) continue;
                        dd = Scaled(dd);
                        if (dd > day) { future++; continue; }
                        if (Stuck.Contains(nm)) continue;
                        pend++;
                        if (dd < headDay) { headDay = dd; head = nm; }
                    }
                Scribe.Line("FABULA: dzien " + ((int)day)
                            + " | zaleglych na osi: " + pend
                            + " | czolo kolejki: " + (head ?? "brak")
                            + (head != null ? " (na dzien " + ((int)headDay) + ")" : "")
                            + " | przyszlych: " + future
                            + " | ostatni start: " + (_lastStartDay < -9000 ? "zaden" : ((int)_lastStartDay).ToString())
                            + " | pominietych: " + Stuck.Count + (Stuck.Count > 0 ? " [" + string.Join(", ", new List<string>(Stuck).ToArray()) + "]" : "")
                            + (_resolveStumbles > 0 ? " | potkniecia 320a " + _resolveStumbles : "")
                            + (list == null ? "  [UWAGA: nie widze listy wydarzen ROT]" : ""));
            }
            catch { }
        }

        // ---------------------------------------------------------- 320a: martwe wydarzenia ROT (Jeff 02.11)
        // Lancuch Harrenhal (Notification -> Siege -> HarrenhalEvent) rusza tylko, gdy Harrenhal trzyma ROTClans.Clegane
        // (HarrenhalSiegeEvent.SiegeLeaderReadyToAttackTarget), a hold Dorzecza (RiverlandsDeclareWarEvent.MainCheck) tylko przy zywym
        // krolestwie Dorzecza. Gdy swiat odjechal: ROTEventBehavior.SkipEvent(Type) (droga komendy rot.skip_event: TotalEvents++,
        // bez StartEvent, w zapisie ROT) - bez 6 dni zatoru po kazdym wczytaniu. HarrenhalEvent pierwsze: jego MainCheck to
        // "Siege.TotalEvents > 0 && !IsActive" - samo pominiecie oblezenia oddaloby Harrenhal Roose'owi (HarrenhalEvent.cs:72).
        private static System.Reflection.MethodInfo _mSkip;
        private static bool _skipTried;
        private static int _resolveStumbles;
        private static readonly HashSet<string> _resolveSaid = new HashSet<string>();

        private static void ResolveStumble(string where, Exception e)
        {
            _resolveStumbles++;
            if (_resolveStumbles <= 5) Scribe.Line("Fabula (320a): " + where + " NIE (" + (e != null ? e.GetType().Name + ": " + e.Message : "?") + ", potkniecie " + _resolveStumbles + ").");
        }

        private static object FindEvent(System.Collections.IEnumerable list, string name)
        {
            foreach (var ev in list) if (ev != null && ev.GetType().Name == name) return ev;
            return null;
        }

        private static bool Active(object ev)
        {
            return ev != null && Props(ev) && Convert.ToBoolean(_pActive.GetValue(ev, null));
        }

        private static object RotStatic(string type, string prop)
        {
            var t = Type.GetType("ROT.Misc." + type + ", ROT");
            var pi = t != null ? AccessTools.Property(t, prop) : null;
            return pi != null ? pi.GetValue(null, null) : null;
        }

        /// <summary>ROT SkipEvent dla wydarzenia (tylko gdy czeka). Zwraca "TAK" albo "NIE (powod)".</summary>
        private static string Skip(object ev)
        {
            if (ev == null) return "brak";
            if (!Pending(ev)) return "juz nie czeka";
            if (!_skipTried)
            {
                _skipTried = true;
                var tBeh = Type.GetType("ROT.CampaignBehaviors.ROTEventBehavior, ROT");
                _mSkip = tBeh != null ? AccessTools.DeclaredMethod(tBeh, "SkipEvent", new[] { typeof(Type) }) : null;
            }
            if (_mSkip == null || _behavior == null) return "NIE (brak ROTEventBehavior.SkipEvent)";
            object r = _mSkip.Invoke(_behavior, new object[] { ev.GetType() });
            return r is bool && (bool)r && !Pending(ev) ? "TAK" : "NIE (SkipEvent false)";
        }

        private static void Resolve(double day)
        {
            if (!Config.FabulaHarrenhalResolve) return;
            System.Collections.IEnumerable list;
            try { list = Events(); } catch (Exception e) { ResolveStumble("lista wydarzen", e); return; }
            if (list == null) return;
            try
            {
                float dN;
                if (Timeline.TryGetValue("HarrenhalSiegeNotificationEvent", out dN) && day >= Scaled(dN))
                {
                    var evH = FindEvent(list, "HarrenhalEvent");
                    var evS = FindEvent(list, "HarrenhalSiegeEvent");
                    var evN = FindEvent(list, "HarrenhalSiegeNotificationEvent");
                    // tylko gdy ani oblezenie, ani koniec nie ruszyly (oblezenie skonczone -> HarrenhalEvent ROT odpala sam tej doby)
                    if (evH != null && evS != null && Pending(evH) && Pending(evS) && !Active(evN))
                    {
                        var harrenhal = RotStatic("ROTSettlements", "Harrenhal") as Settlement;
                        var clegane = RotStatic("ROTClans", "Clegane") as Clan;
                        var bolton = RotStatic("ROTClans", "Bolton") as Clan;
                        Clan owner = harrenhal != null ? harrenhal.OwnerClan : null;
                        string why = null;
                        if (harrenhal == null || clegane == null)
                        {
                            if (_resolveSaid.Add("hh-brak")) Scribe.Line("Fabula (320a): lancuch Harrenhal NIE (brak ROTSettlements.Harrenhal albo ROTClans.Clegane) | dzien " + (int)day + ".");
                        }
                        else if (owner != clegane)
                            why = "Harrenhal trzyma " + (owner != null ? owner.Name + " (" + owner.StringId + ", " + (owner.Kingdom != null ? owner.Kingdom.Name + " / " + owner.Kingdom.StringId : "bez krolestwa") + ")" : "nikt") + ", nie Clegane";
                        else if (bolton == null || bolton.IsEliminated)
                            why = "rod Boltonow wyeliminowany - Roose nie poprowadzi oblezenia";
                        if (why != null)
                        {
                            string rH = Skip(evH), rS = Skip(evS);
                            string rN = evN == null ? "brak" : (Pending(evN) ? Skip(evN) : "juz bylo");
                            Scribe.Line("Fabula (320a): lancuch Harrenhal zamkniety - " + why + ": pominiete HarrenhalEvent " + rH + ", HarrenhalSiegeEvent " + rS
                                        + ", HarrenhalSiegeNotificationEvent " + rN + " (ROT SkipEvent, w zapisie ROT; Harrenhal bez zmiany pana) | dzien " + (int)day + ".");
                            if (rH != "TAK" || rS != "TAK") _resolveStumbles++;
                        }
                    }
                }
            }
            catch (Exception e) { ResolveStumble("lancuch Harrenhal", e); }
            try
            {
                float dR;
                if (Timeline.TryGetValue("RiverlandsDeclareWarEvent", out dR) && day >= Scaled(dR))
                {
                    var evR = FindEvent(list, "RiverlandsDeclareWarEvent");
                    if (evR != null && Pending(evR))
                    {
                        var river = RotStatic("ROTKingdoms", "Riverlands") as Kingdom;
                        if (river == null || river.IsEliminated)
                        {
                            string r = Skip(evR);
                            Scribe.Line("Fabula (320a): RiverlandsDeclareWarEvent pominiete " + r + " - krolestwa Dorzecza nie ma (" + (river == null ? "brak riverlands" : "wyeliminowane - unia 272 albo podboj")
                                        + "); po Krwawych Godach nie odpali z tekstem holdu | dzien " + (int)day + ".");
                            if (r != "TAK") _resolveStumbles++;
                        }
                    }
                }
            }
            catch (Exception e) { ResolveStumble("hold Dorzecza", e); }
        }


        // ---------------------------------------------------------- wojny fabularne i progi Innych (Jeff 04.10)
        // ROTStorylineWar.Enforced porownuje dni od startu ze StartDay/EndDay (np. Zelazni Ludzie
        // na Polnoc 100-235) - na czas wywolania podstawiamy daty x skala osi, potem przywracamy.
        private static System.Reflection.PropertyInfo _pWarStart, _pWarEnd;

        public static void WarPrefix(object __instance, out int[] __state)
        {
            __state = null;
            try
            {
                float k = ScaleK();
                if (Math.Abs(k - 1f) < 0.001f || _pWarStart == null || _pWarEnd == null) return;
                int s = (int)_pWarStart.GetValue(__instance, null);
                int e = (int)_pWarEnd.GetValue(__instance, null);
                if (s <= 0 && e <= 0) return;
                __state = new[] { s, e };
                if (s > 0) _pWarStart.SetValue(__instance, (int)Math.Round(s * k), null);
                if (e > 0) _pWarEnd.SetValue(__instance, (int)Math.Round(e * k), null);
            }
            catch { __state = null; }
        }

        public static Exception WarFinalizer(object __instance, int[] __state, Exception __exception)
        {
            try
            {
                if (__state != null)
                {
                    _pWarStart.SetValue(__instance, __state[0], null);
                    _pWarEnd.SetValue(__instance, __state[1], null);
                }
            }
            catch { }
            return __exception;
        }

        private static float ScaleK()
        {
            float k = Config.FabulaTimeScale;
            if (k < 0.25f) k = 0.25f;
            if (k > 6f) k = 6f;
            return k;
        }

        // Inni: ROT wypuszcza ich na Mrozny Brzeg/Rogowa Stope po 300 dniach, Thenn/Kly Mrozu po 400,
        // Hardhome po 500 (OnAiHourlyTick). Mnozymy stala, ktora stoi zaraz po ElapsedDaysUntilNow.
        public static System.Collections.Generic.IEnumerable<CodeInstruction> OthersTranspiler(System.Collections.Generic.IEnumerable<CodeInstruction> instructions)
        {
            float k = ScaleK();
            var elapsed = AccessTools.PropertyGetter(typeof(CampaignTime), "ElapsedDaysUntilNow");
            CodeInstruction prev = null;
            foreach (var ci in instructions)
            {
                if (prev != null && ci.opcode == System.Reflection.Emit.OpCodes.Ldc_R4 && ci.operand is float
                    && (prev.opcode == System.Reflection.Emit.OpCodes.Call || prev.opcode == System.Reflection.Emit.OpCodes.Callvirt)
                    && Equals(prev.operand, elapsed))
                {
                    float v = (float)ci.operand;
                    if (v == 300f || v == 400f || v == 500f) { ci.operand = v * k; _othersSwapped++; }
                }
                prev = ci;
                yield return ci;
            }
        }
        private static int _othersSwapped;

        private static void InstallWars(Harmony h)
        {
            try
            {
                var tWar = Type.GetType("ROT.CampaignBehaviors.ROTStorylineWar, ROT");
                var mEnf = tWar != null ? AccessTools.Method(tWar, "Enforced") : null;
                if (mEnf != null)
                {
                    _pWarStart = AccessTools.Property(tWar, "StartDay");
                    _pWarEnd = AccessTools.Property(tWar, "EndDay");
                    h.Patch(mEnf, prefix: new HarmonyMethod(typeof(Fabula), "WarPrefix"), finalizer: new HarmonyMethod(typeof(Fabula), "WarFinalizer"));
                }
                int others = 0;
                var tOth = Type.GetType("ROT.CampaignBehaviors.ROTOthersCampaignBehavior, ROT");
                if (tOth != null)
                {
                    var elapsed = AccessTools.PropertyGetter(typeof(CampaignTime), "ElapsedDaysUntilNow");
                    var types = new System.Collections.Generic.List<Type> { tOth };
                    try { types.AddRange(tOth.GetNestedTypes(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)); } catch { }
                    foreach (var t in types)
                    {
                        if (t.ContainsGenericParameters) continue;
                        foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
                        {
                            try
                            {
                                if (m.IsAbstract || m.ContainsGenericParameters || m.GetMethodBody() == null) continue;
                                bool hit = false; object last = null;
                                foreach (var kv in PatchProcessor.ReadMethodBody(m))
                                {
                                    if (kv.Value is float && Equals(last, elapsed))
                                    {
                                        float v = (float)kv.Value;
                                        if (v == 300f || v == 400f || v == 500f) { hit = true; break; }
                                    }
                                    last = kv.Value;
                                }
                                if (!hit) continue;
                                h.Patch(m, transpiler: new HarmonyMethod(typeof(Fabula), "OthersTranspiler"));
                                others++;
                            }
                            catch { }
                        }
                    }
                }
                Scribe.Line("Fabula: wojny fabularne " + (mEnf != null ? "x" + ScaleK() + " (StartDay/EndDay)" : "BRAK ROTStorylineWar.Enforced")
                            + ", progi Innych x" + ScaleK() + " w " + others + " metodach (podmienionych stalych " + _othersSwapped + ").");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Fabula.InstallWars", null); } catch { } }
        }
        internal static void Install(Harmony h)
        {
            try
            {
                if (!Config.FabulaPacerEnabled) { Scribe.Line("Fabula: rozrusznik wylaczony."); return; }
                var tBase = Type.GetType("ROT.Events.EventBase, ROT");
                if (tBase == null) { Scribe.Line("Fabula: ROT nieobecny - rozrusznik spi."); return; }
                InstallWars(h);

                int done = 0;
                foreach (var name in Timeline.Keys)
                {
                    var t = Type.GetType("ROT.Events." + name + ", ROT");
                    var m = t != null ? AccessTools.Method(t, "ConditionsMet") : null;
                    if (m == null) continue;
                    h.Patch(m, prefix: new HarmonyMethod(typeof(Fabula), "GatePrefix"));
                    done++;
                }
                var mStart = AccessTools.Method(tBase, "StartEvent");
                if (mStart != null) h.Patch(mStart, postfix: new HarmonyMethod(typeof(Fabula), "StartPostfix"));
                int starts = 0;
                foreach (var name in Timeline.Keys)
                {
                    var t = Type.GetType("ROT.Events." + name + ", ROT");
                    // wlasne StartEvent wydarzenia - nadpisane, wiec latka na bazie sama go nie zlapie
                    var ms = t != null ? AccessTools.DeclaredMethod(t, "StartEvent") : null;
                    if (ms == null) continue;
                    h.Patch(ms, postfix: new HarmonyMethod(typeof(Fabula), "StartPostfix"));
                    starts++;
                }

                Scribe.Line("Fabula: rozrusznik czynny (" + done + " bramek, " + starts + " znacznikow startu, tempo 1/"
                            + Config.FabulaPaceDays + " dni, os x" + Config.FabulaTimeScale + "; Karstark 100->190, Craster 150->200).");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Fabula.Install", null); } catch { } }
        }
    }
}
