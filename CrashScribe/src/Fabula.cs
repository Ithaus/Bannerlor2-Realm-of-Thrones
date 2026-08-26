using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

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
                return (CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays;
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
                if (_pTotal == null || _pActive == null)
                {
                    // z KLASY BAZOWEJ, nie z pierwszego napotkanego wydarzenia
                    var tBase = Type.GetType("ROT.Events.EventBase, ROT") ?? ev.GetType();
                    _pTotal = AccessTools.Property(tBase, "TotalEvents");
                    _pActive = AccessTools.Property(tBase, "IsActive");
                    if (_pTotal == null || _pActive == null) return false;
                }
                int total = _pTotal != null ? Convert.ToInt32(_pTotal.GetValue(ev, null)) : 0;
                bool active = _pActive != null && Convert.ToBoolean(_pActive.GetValue(ev, null));
                return total == 0 && !active;
            }
            catch { return false; }
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
        /// </summary>
        private static void Diag(double day)
        {
            try
            {
                if (day - _lastDiagDay < 1.0) return;
                _lastDiagDay = day;
                var list = Events();
                string head = null; float headDay = float.MaxValue; int pend = 0;
                if (list != null)
                    foreach (var ev in list)
                    {
                        if (ev == null) continue;
                        var nm = ev.GetType().Name;
                        float dd;
                        if (!Timeline.TryGetValue(nm, out dd)) continue;
                        if (!Pending(ev)) continue;
                        pend++;
                        dd = Scaled(dd);
                        if (dd < headDay) { headDay = dd; head = nm; }
                    }
                Scribe.Line("FABULA: dzien " + ((int)day)
                            + " | zaleglych na osi: " + pend
                            + " | czolo kolejki: " + (head ?? "brak")
                            + (head != null ? " (na dzien " + ((int)headDay) + ")" : "")
                            + " | ostatni start: " + (_lastStartDay < -9000 ? "zaden" : ((int)_lastStartDay).ToString())
                            + " | pominietych: " + Stuck.Count
                            + (list == null ? "  [UWAGA: nie widze listy wydarzen ROT]" : ""));
            }
            catch { }
        }

        internal static void Install(Harmony h)
        {
            try
            {
                if (!Config.FabulaPacerEnabled) { Scribe.Line("Fabula: rozrusznik wylaczony."); return; }
                var tBase = Type.GetType("ROT.Events.EventBase, ROT");
                if (tBase == null) { Scribe.Line("Fabula: ROT nieobecny - rozrusznik spi."); return; }

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
