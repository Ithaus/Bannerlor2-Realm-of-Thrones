using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// POWOLNE GOJENIE. Jeff 28.08: "regeneracja zdrowia na mapie za szybka,
    /// ma byc dwa razy dluzsza - bonusy z perkow niech dalej dzialaja".
    /// Postfix na dzienne leczenie szeregowych i bohaterow. Po audycie 30.08
    /// dwie korekty:
    /// 1) OSOBNY procent dla AI (domyslnie 100 = vanilla). Wspolne -50%
    ///    sprawialo, ze partie AI po glodzie/bitwie jezdzily tygodniami
    ///    samymi rannymi (AI leczy 2.5-5.5 ludzi/dzien przy bitwach co
    ///    kilka dni) - obraz "Ramsay zdrowy, wszyscy inni ranni".
    /// 2) Czynnik tnie WYLACZNIE dodatni wynik. Przy glodzie model zwraca
    ///    liczbe UJEMNA (Starving: -25% skladu/dzien zamienianego w rannych)
    ///    i AddFactor lagodzilby te kare o polowe - tniemy leczenie,
    ///    nie kary.
    /// Uwaga do AddFactor: czynniki sa ADDYTYWNE z bonusem medycyny
    /// (wynik = baza * (1 + medycyna + nasz czynnik)), a nie "cieciem
    /// wyniku na koncu" - dlatego najmocniej zwalnia partie bez medyka.
    /// </summary>
    internal static class SlowHealing
    {
        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                var s = Settings.Current;
                var t = typeof(DefaultPartyHealingModel);
                var m1 = AccessTools.Method(t, "GetDailyHealingForRegulars");
                var m2 = AccessTools.Method(t, "GetDailyHealingHpForHeroes");
                // GLOD RANI POWOLI (Jeff 14.09: "czemu wszyscy maja rannych?").
                // Vanilla: partia glodujaca W POLU dostaje leczenie -25% skladu
                // DZIENNIE (DefaultPartyHealingModel, galaz IsStarving) - czyli
                // cala armia ranna w cztery dni. Zima (+50% jedzenia), drozyzna
                // i cap zapasow AI 4 dni robia z tego kaskade: lordowie jezdza
                // z 0 zdrowych/200 rannych, a infekcje RB dobijaja rannych.
                // Kara zostaje, ale w ludzkim tempie (suwak, dom. 5%/dzien) -
                // gracz i AI rowno; garnizony (-10%) bez zmian.
                if (m1 != null && s.StarvationWoundPercent != 25)
                {
                    harmony.Patch(m1, postfix: new HarmonyMethod(typeof(SlowHealing), "StarvePostfix") { priority = Priority.Last });
                    Log.Info("SlowHealing: glod rani " + s.StarvationWoundPercent + "% skladu dziennie (vanilla 25%).");
                }
                // 500: leczenie w spoczynku - wlasny postfiks, wpinany ZAWSZE (klucz HealOnlyAtRest czytany na zywo), takze przy 100/100 ponizej
                int rest = 0;
                if (m1 != null && harmony.Patch(m1, postfix: new HarmonyMethod(typeof(SlowHealing), nameof(RestPostfixRegulars)) { priority = Priority.Low - 1 }) != null) rest++;
                if (m2 != null && harmony.Patch(m2, postfix: new HarmonyMethod(typeof(SlowHealing), nameof(RestPostfixHeroes)) { priority = Priority.Low - 1 }) != null) rest++;
                Log.Info("Leczenie w spoczynku (500): " + (s.HealOnlyAtRest ? "TAK" : "NIE") + " - druzyna gracza w marszu " + Math.Max(0, Math.Min(100, s.MovingHealPercent))
                         + "% stawki, w spoczynku (postoj / oboz / sen / osada) pelna | AI bez zmian | jency pomijani | " + rest + "/2 metod.");
                if (s.HealingRegenPercent == 100 && s.AiHealingRegenPercent == 100)
                { Log.Info("SlowHealing: 100%/100% - vanilla tempo gojenia, patch spi (jency pomijani TAK, 318b)."); return; }
                int patched = 0;
                // poprawka po audycie TOWARY 3: Priority.Low (200) - PO postfiksie BK (400, wpinany w OnGameStart, czyli po nas) i przed StarvePostfix
                // (Last): warunek "kara - nie lagodzic" widzi juz kare BK za glod w oblezonej osadzie (dotad AddFactor gracza mnozyl ja przez pol)
                if (m1 != null) { harmony.Patch(m1, postfix: new HarmonyMethod(typeof(SlowHealing), "SlowPostfix") { priority = Priority.Low }); patched++; }
                if (m2 != null) { harmony.Patch(m2, postfix: new HarmonyMethod(typeof(SlowHealing), "SlowPostfix") { priority = Priority.Low }); patched++; }
                Log.Info("SlowHealing: gojenie na mapie - gracz " + s.HealingRegenPercent
                         + "%, AI " + s.AiHealingRegenPercent + "% (" + patched + "/2 metod) | jency pomijani TAK (318b).");
            }
            catch (Exception e) { Log.Error("SlowHealing.ApplyAll", e); }
        }

        // ---------------- 500: leczenie tylko w spoczynku ----------------
        // Jeff 14.11: "leczenie po bitwie jest mozliwe tylko w obozie, nie moge jechac na koniu i leczyc sie w trakcie jazdy". Druzyna GRACZA
        // (bohaterowie i ranni szeregowi, nie jency) w marszu dostaje MovingHealPercent dodatniej stawki; spoczynek = partia stoi (IsMoving gracza =
        // !IsMainPartyWaiting: oboz NightRest/BK, menu oczekiwania, postoj), jest w osadzie albo spi w menu snu. Morze w ruchu = marsz.
        // Stan czytany w chwili wywolania modelu: gra leczy gracza co godzine (PartyHealCampaignBehavior.OnHourlyTick, 1/24 stawki), wiec 24 probki
        // na dobe - udzial godzin marszu w dobie; tooltip leczenia w UI pokazuje stawke biezaca (w ruchu - z cieciem, na postoju - pelna).
        // Ciecie: Add(-baza x (1 - p)) do bazy ExplainedNumber - wynik = baza x (1 + suma czynnikow) mnozy sie dokladnie przez p, niezaleznie od
        // kolejnosci AddFactor (SlowHealing, perki, Naval Resilience); rozpiska pokazuje ubytek w punktach (wzor TownInns.HealPostfix).
        private static readonly TextObject _marchText = new TextObject("{=armMarchHeal}Marching (no rest)", null);
        private static int _stumbles, _cutsReg, _cutsHero;
        private static int _hMove, _hRest, _day = -1, _hp0 = -1, _w0 = -1;
        private static float _dueHero;

        internal static void Reset() { _stumbles = 0; _cutsReg = 0; _cutsHero = 0; _hMove = 0; _hRest = 0; _day = -1; _hp0 = -1; _w0 = -1; _dueHero = 0f; }

        /// <summary>500: partia gracza jest W MARSZU (nie odpoczywa): rusza sie po mapie (ladem albo morzem), poza osada i poza menu snu.</summary>
        internal static bool PlayerMarching(MobileParty mp)
        {
            if (mp == null || mp.CurrentSettlement != null || NightRest.SleepingNow) return false;
            return mp.IsMoving;
        }

        public static void RestPostfixRegulars(PartyBase __0, bool __1, ref TaleWorlds.CampaignSystem.ExplainedNumber __result) { if (RestCut(__0, __1, ref __result)) _cutsReg++; }
        public static void RestPostfixHeroes(PartyBase __0, bool __1, ref TaleWorlds.CampaignSystem.ExplainedNumber __result) { if (RestCut(__0, __1, ref __result)) _cutsHero++; }

        private static bool RestCut(PartyBase party, bool prisoners, ref TaleWorlds.CampaignSystem.ExplainedNumber r)
        {
            try
            {
                if (prisoners || party == null || party != PartyBase.MainParty) return false;   // jency - ryczalt gry (jak 318b); AI bez zmian
                var s = Settings.Current;
                if (s == null || !s.HealOnlyAtRest) return false;
                int pct = Math.Max(0, Math.Min(100, s.MovingHealPercent));
                if (pct >= 100) return false;
                if (r.ResultNumber <= 0f) return false;                                     // kara (glod, tratwa) - bez zmian
                if (!PlayerMarching(party.MobileParty)) return false;
                float b = r.BaseNumber;
                if (b <= 0f) return false;
                r.Add(-b * (1f - pct / 100f), _marchText);
                return true;
            }
            catch (Exception e) { if (++_stumbles <= 3) Log.Error("SlowHealing.RestCut", e); return false; }
        }

        /// <summary>500: tik godzinowy (ArmouryBehavior) - godzina marszu / spoczynku gracza, nalezne HP bohaterow (stawka modelu / 24) i raz na dobe linia logu.
        /// Jedno wywolanie modelu na godzine, tylko partia gracza.</summary>
        internal static void Hourly()
        {
            try
            {
                var s = Settings.Current;
                var mp = MobileParty.MainParty;
                var h = Hero.MainHero;
                if (s == null || mp == null || h == null) return;
                int day = (int)CampaignTime.Now.ToDays;
                if (_day < 0) { _day = day; _hp0 = h.HitPoints; _w0 = mp.MemberRoster.TotalWoundedRegulars; }
                else if (day != _day)
                {
                    if (s.HealOnlyAtRest)
                        Log.Info("Leczenie w spoczynku (500): doba " + _day + " - gracz marsz " + _hMove + " h / spoczynek " + _hRest + " h | HP " + _hp0 + " -> " + h.HitPoints
                                 + "/" + h.MaxHitPoints + (h.IsWounded ? " (ranny)" : "") + " | nalezne bohaterom " + _dueHero.ToString("0.0") + " HP | ranni szeregowi " + _w0
                                 + " -> " + mp.MemberRoster.TotalWoundedRegulars + " | wywolania modelu z cieciem marszu (gra, UI, pomiar): szeregowi " + _cutsReg + ", bohaterowie " + _cutsHero
                                 + " | marsz " + Math.Max(0, Math.Min(100, s.MovingHealPercent)) + "% | potkniecia " + _stumbles);
                    _day = day; _hp0 = h.HitPoints; _w0 = mp.MemberRoster.TotalWoundedRegulars;
                    _hMove = 0; _hRest = 0; _cutsReg = 0; _cutsHero = 0; _dueHero = 0f;
                }
                bool march = PlayerMarching(mp);
                if (march) _hMove++; else _hRest++;
                if (mp.Party != null && mp.MapEvent == null)
                {
                    float rate = mp.Party.HealingRateForMemberHeroes;   // czynny model z calym lancuchem (to samo co gra w tej godzinie)
                    _dueHero += rate / 24f;
                }
            }
            catch (Exception e) { if (++_stumbles <= 3) Log.Error("SlowHealing.Hourly", e); }
        }

        private static readonly TextObject _starveText = new TextObject("{=armStarve}Starving", null);

        /// <summary>Priority.Last: po BK (dodaje wlasna kare za glod w oblezeniu)
        /// - skalujemy CALA ujemna wartosc z 25%/dzien do suwaka.</summary>
        public static void StarvePostfix(PartyBase party, bool includeDescriptions, ref TaleWorlds.CampaignSystem.ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || party == null || !party.IsMobile) return;
                var mp = party.MobileParty;
                if (mp == null || mp.IsGarrison || !party.IsStarving) return;   // garnizon: vanilla -10%
                float v = __result.ResultNumber;
                if (v >= 0f) return;
                int pct = Math.Max(0, Math.Min(25, s.StarvationWoundPercent));
                float scaled = v * (pct / 25f);
                int rounded = pct == 0 ? 0 : -Math.Max(1, (int)Math.Round(-scaled));   // glod bez kary tylko przy 0
                __result = new TaleWorlds.CampaignSystem.ExplainedNumber(rounded, includeDescriptions, _starveText);
            }
            catch { }
        }

        /// <summary>318b: __1 = isPrisoners (obie metody Default: (PartyBase, bool isPrisoners, bool includeDescriptions)) - jency poza cieciem. Dotad
        /// przy 50% jency gracza zdrowieli 0.5/dobe zamiast ryczaltu gry 1 (bohaterowie-jency 10 HP zamiast 20) - niezamierzone; TownInns pomija ich tak samo.</summary>
        public static void SlowPostfix(PartyBase party, bool __1, ref TaleWorlds.CampaignSystem.ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || __1) return;              // 318b: jency - ryczalt gry bez ciecia
                bool player = party != null && party == PartyBase.MainParty;
                int pct = player ? s.HealingRegenPercent : s.AiHealingRegenPercent;
                if (pct == 100 || pct < 0) return;         // >100 = szybsze gojenie, tez dozwolone
                if (__result.ResultNumber <= 0f) return;   // kara (glod itp.) - nie lagodzic
                __result.AddFactor(pct / 100f - 1f, new TextObject("{=armSlowMend}Wounds knit slowly", null));
            }
            catch { }
        }
    }
}
