using System;
using System.Diagnostics;
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
                // recenzja 500: wlasny try - Harmony przy bledzie rzuca, a wywrotka latki 500 nie moze odciac SlowPostfix ponizej (HealingRegenPercent)
                int rest = 0;
                try
                {
                    if (m1 != null && harmony.Patch(m1, postfix: new HarmonyMethod(typeof(SlowHealing), nameof(RestPostfixRegulars)) { priority = Priority.Low - 1 }) != null) rest++;
                    if (m2 != null && harmony.Patch(m2, postfix: new HarmonyMethod(typeof(SlowHealing), nameof(RestPostfixHeroes)) { priority = Priority.Low - 1 }) != null) rest++;
                }
                catch (Exception e) { Log.Error("SlowHealing.Rest", e); }
                Log.Info("Leczenie w spoczynku (500/500-a): " + (s.HealOnlyAtRest ? "TAK" : "NIE") + " - w marszu ladem " + Math.Max(0, Math.Min(100, s.MovingHealPercent))
                         + "% stawki, w spoczynku (postoj / oboz / sen / osada / morze i przejscie lad-morze) pelna | druzyna gracza i jej jency | AI z jencami "
                         + (s.HealOnlyAtRest && s.AiHealOnlyAtRest ? "TAK" : "NIE") + " (klucz Ai Heal Only At Rest) | " + rest + "/2 metod.");
                if (s.HealingRegenPercent == 100 && s.AiHealingRegenPercent == 100)
                { Log.Info("SlowHealing: 100%/100% - vanilla tempo gojenia, patch spi (jency bez Healing Regen TAK, 318b; marsz 500-a osobno)."); return; }
                int patched = 0;
                // poprawka po audycie TOWARY 3: Priority.Low (200) - PO postfiksie BK (400, wpinany w OnGameStart, czyli po nas) i przed StarvePostfix
                // (Last): warunek "kara - nie lagodzic" widzi juz kare BK za glod w oblezonej osadzie (dotad AddFactor gracza mnozyl ja przez pol)
                if (m1 != null) { harmony.Patch(m1, postfix: new HarmonyMethod(typeof(SlowHealing), "SlowPostfix") { priority = Priority.Low }); patched++; }
                if (m2 != null) { harmony.Patch(m2, postfix: new HarmonyMethod(typeof(SlowHealing), "SlowPostfix") { priority = Priority.Low }); patched++; }
                Log.Info("SlowHealing: gojenie na mapie - gracz " + s.HealingRegenPercent
                         + "%, AI " + s.AiHealingRegenPercent + "% (" + patched + "/2 metod) | jency bez Healing Regen TAK (318b; marsz 500-a osobno).");
            }
            catch (Exception e) { Log.Error("SlowHealing.ApplyAll", e); }
        }

        // ---------------- 500: leczenie tylko w spoczynku (500-a: takze AI, jency, morze = spoczynek) ----------------
        // 500-a (Jeff 14.11): "plywanie statkiem to nie marsz, przeciez statek moze plynac a ja moge odpoczywac w kajucie", "jency i partie Ai tez maja miec
        // tn sam efekt", "to dobrze ze wojna sie spowolni". Morze i przejscie lad/morze (IsMoving gry i tak false w przejsciu) = spoczynek - gracz, cudza
        // armia (stan przywodcy) i AI; tratwa (wynik ujemny szeregowych) bez zmian. Jency (isPrisoners) tez ciete - w druzynie gracza i AI; loch osady
        // (partia osady, MobileParty == null) i garnizon (CurrentSettlement) = spoczynek. Partie AI (wszystkie MobileParty: lordowie, bandyci, karawany,
        // wiesniacy, milicja, garnizony) - gra leczy je w OnQuarterDailyPartyTick (4 x na dobe, healFrequencyPerDay 4), wiec 4 probki stanu na dobe.
        // Ta sama stawka MovingHealPercent (jedna zasada na jedno zjawisko); osobny wylacznik AiHealOnlyAtRest. AI bez rannych / bez bohaterow w danym
        // rosterze - wyjscie po odczycie licznika rostera (pole), bez testu ruchu. Zero alokacji (liczniki statyczne, Stopwatch.GetTimestamp).
        // Jeff 14.11: "leczenie po bitwie jest mozliwe tylko w obozie, nie moge jechac na koniu i leczyc sie w trakcie jazdy". Druzyna GRACZA
        // (bohaterowie, ranni szeregowi i jency - 500-a) w marszu dostaje MovingHealPercent dodatniej stawki; spoczynek = partia stoi (IsMoving gracza =
        // !IsMainPartyWaiting: oboz NightRest/BK, menu oczekiwania, postoj), jest w osadzie albo spi w menu snu. Morze = spoczynek (500-a; w 500 bylo marszem).
        // Recenzja 500-a: wyjatek - jency-bohaterowie partii AI, w ktorej siedzi GRACZ jako jeniec: bez ciecia (glod RC 4-7 HP/dobe liczony przy
        // leczeniu gry 20/dobe; przy 2/dobe HP gracza w ruchomej niewoli spadaloby do 1). Decyzja do potwierdzenia przez Jeffa.
        // Stan czytany w chwili wywolania modelu: gra leczy gracza co godzine (PartyHealCampaignBehavior.OnHourlyTick, 1/24 stawki), wiec 24 probki
        // na dobe - udzial godzin marszu w dobie; tooltip leczenia w UI pokazuje stawke biezaca (w ruchu - z cieciem, na postoju - pelna).
        // Ciecie: Add(-baza x (1 - p)) do bazy ExplainedNumber - wynik = baza x (1 + suma czynnikow) mnozy sie dokladnie przez p, niezaleznie od
        // kolejnosci AddFactor (SlowHealing, perki, Naval Resilience); rozpiska pokazuje ubytek w punktach (wzor TownInns.HealPostfix).
        private static readonly TextObject _marchText = new TextObject("{=armMarchHeal}Marching (no rest)", null);
        private static int _stumbles, _cutsReg, _cutsHero;
        private static int _hMove, _hRest, _hSea, _day = -1, _hp0 = -1, _w0 = -1, _cutsPris;
        private static float _dueHero;
        // 500-a doba AI (watek glowny: PartyHealCampaignBehavior, UI): wywolania, pominiete (nic do leczenia), ciete (marsz), spoczynek (w tym morze)
        // recenzja 500-a: _aiCutHp = stawka nominalna (takze rostery ze zdrowymi bohaterami - gra ich nie leczy); realny ubytek _aiCutHpReal = ciecie/4
        // x bohaterowie bez pelnego HP (czlonkowie: !IsHealthFull jak HealMemberHeroes; jency: HP < WoundedHealthLimit jak ManageHealingOfPrisonerHeroes)
        private static int _aiCalls, _aiSkip, _aiMarch, _aiRest, _aiSea, _aiPrisCuts, _aiHurtSamples, _aiHurtHeroes, _aiWndMarch, _aiWndRest, _aiCaptiveSkip;
        private static float _aiCutReg, _aiCutHp, _aiCutHpReal;
        private static long _aiTicks;

        internal static void Reset()
        {
            _stumbles = 0; _cutsReg = 0; _cutsHero = 0; _cutsPris = 0; _hMove = 0; _hRest = 0; _hSea = 0; _day = -1; _hp0 = -1; _w0 = -1; _dueHero = 0f;
            ResetAi();
        }

        private static void ResetAi() { _aiCalls = 0; _aiSkip = 0; _aiMarch = 0; _aiRest = 0; _aiSea = 0; _aiPrisCuts = 0; _aiHurtSamples = 0; _aiHurtHeroes = 0; _aiWndMarch = 0; _aiWndRest = 0;
                                          _aiCaptiveSkip = 0; _aiCutReg = 0f; _aiCutHp = 0f; _aiCutHpReal = 0f; _aiTicks = 0; }

        /// <summary>500: partia gracza jest W MARSZU (nie odpoczywa): rusza sie po mapie (ladem albo morzem), poza osada. Sen w menu = SetMoveModeHold,
        /// czyli IsMainPartyWaiting (recenzja 500: bez flagi NightRest.SleepingNow - bywa nieaktualna i dawala pelna stawke w jezdzie).
        /// Gracz doczepiony do CUDZEJ armii: wlasne IsMoving (ComputeIsWaiting z wlasnych celow, nieaktualnych u doczepionego - DoAiPathMode
        /// wychodzi od razu) nic nie mowi - stan przywodcy, jak sama gra (MobileParty: IsMoving || Army.LeaderParty.IsMoving).</summary>
        internal static bool PlayerMarching(MobileParty mp) { return Marching(mp); }

        /// <summary>500-a: partia (gracz albo AI) W MARSZU LADEM. Spoczynek: osada, oblezenie, bitwa, postoj, przejscie lad/morze (IsMoving gry = false),
        /// MORZE (IsCurrentlyAtSea - statek plynie, ludzie odpoczywaja), oboz nocny AI (NightRest.IsCamping). Czlonek armii doczepiony (AttachedTo) - stan
        /// przywodcy (wlasne cele doczepionego sa nieaktualne; gra: IsMoving || Army.LeaderParty.IsMoving). IsMoving AI = IsActive, poza przejsciem, osada,
        /// bitwa i oblezeniem, Position != MoveTargetPoint; gracza = !IsMainPartyWaiting (poza przejsciem). Bez alokacji.</summary>
        internal static bool Marching(MobileParty mp)
        {
            if (mp == null || mp.CurrentSettlement != null) return false;
            var army = mp.Army;
            if (army != null && mp.AttachedTo != null && army.LeaderParty != null && army.LeaderParty != mp)
            {
                var L = army.LeaderParty;
                // IsMoving AI = Position != MoveTargetPoint i samo wyklucza osade, oblezenie, bitwe, przejscie; morze i oboz nocny AI (NightRest) = spoczynek
                return L.CurrentSettlement == null && L.BesiegedSettlement == null && L.MapEvent == null && L.IsMoving && !L.IsCurrentlyAtSea && !NightRest.IsCamping(L);
            }
            if (!mp.IsMoving || mp.IsCurrentlyAtSea) return false;
            return mp == MobileParty.MainParty || !NightRest.IsCamping(mp);   // gracz w obozie stoi (IsMainPartyWaiting) - IsCamping tylko dla AI
        }

        public static void RestPostfixRegulars(PartyBase __0, bool __1, ref TaleWorlds.CampaignSystem.ExplainedNumber __result) { if (RestCut(__0, __1, false, ref __result) && __0 == PartyBase.MainParty) _cutsReg++; }
        public static void RestPostfixHeroes(PartyBase __0, bool __1, ref TaleWorlds.CampaignSystem.ExplainedNumber __result) { if (RestCut(__0, __1, true, ref __result) && __0 == PartyBase.MainParty) _cutsHero++; }

        /// <summary>500 / 500-a: ciecie marszu. Gracz: zawsze (czlonkowie i jency - UI pokazuje stawke, PrisonerCare 319a liczy roznice swoi - jency z obu
        /// pocietych wartosci, wiec dodatek jencow tez x p). AI: tylko gdy w tym rosterze jest co leczyc (ranni szeregowi / bohaterowie) - dla wyniku gry
        /// bez roznicy (gra zeruje akumulator szeregowych bez rannych, zdrowych bohaterow nie leczy), a test ruchu tylko tam, gdzie ma sens.</summary>
        private static bool RestCut(PartyBase party, bool prisoners, bool heroes, ref TaleWorlds.CampaignSystem.ExplainedNumber r)
        {
            bool ai = false;
            long t0 = 0;
            try
            {
                if (party == null) return false;
                var s = Settings.Current;
                if (s == null || !s.HealOnlyAtRest) return false;
                var mp = party.MobileParty;
                if (mp == null) return false;                                               // osada (loch, partia osady) - spoczynek
                if (party != PartyBase.MainParty)
                {
                    if (!s.AiHealOnlyAtRest) return false;
                    ai = true; t0 = Stopwatch.GetTimestamp(); _aiCalls++;
                    var ro = prisoners ? party.PrisonRoster : party.MemberRoster;
                    if (ro == null || (heroes ? ro.TotalHeroes <= 0 : ro.TotalWoundedRegulars <= 0)) { _aiSkip++; return false; }
                    if (prisoners && heroes)
                    {
                        // recenzja 500-a: gracz jeniec tej partii - bez ciecia (glod RC liczony przy leczeniu gry 20 HP/dobe); stawka jest per roster,
                        // wiec dotyczy wszystkich jencow-bohaterow tej partii
                        var mh = Hero.MainHero;
                        if (mh != null && mh.PartyBelongedToAsPrisoner == party) { _aiCaptiveSkip++; return false; }
                    }
                }
                int pct = Math.Max(0, Math.Min(100, s.MovingHealPercent));
                if (pct >= 100) return false;
                if (r.ResultNumber <= 0f) return false;                                     // kara (glod, tratwa) - bez zmian
                if (!Marching(mp))
                {
                    if (ai)
                    {
                        _aiRest++;
                        if (mp.IsCurrentlyAtSea && mp.CurrentSettlement == null) _aiSea++;   // recenzja 500-a: statek w porcie (IsCurrentlyAtSea zostaje) to osada, nie morze
                        if (heroes && !prisoners) _aiWndRest += party.MemberRoster.TotalWoundedHeroes;
                    }
                    return false;
                }
                float b = r.BaseNumber;
                if (b <= 0f) return false;
                if (heroes && !prisoners) pct = Perk513.MarchPct(party, mp, pct);   // 513 (37/60): WalkItOff - lekko ranni bohaterowie w marszu WalkItOffLightWoundPercent (Perk513Healing.cs)
                float before = r.ResultNumber;
                r.Add(-b * (1f - pct / 100f), _marchText);
                if (prisoners) { if (ai) _aiPrisCuts++; else _cutsPris++; }
                if (ai)
                {
                    _aiMarch++;
                    float cut = before - r.ResultNumber;
                    if (!heroes) _aiCutReg += cut;
                    else
                    {
                        _aiCutHp += cut;
                        var ro = prisoners ? party.PrisonRoster : party.MemberRoster;
                        if (!prisoners) _aiWndMarch += ro.TotalWoundedHeroes;
                        // bohaterowie, ktorych gra w tym ticku leczy - ta sama petla i ten sam test co gra (koszt jak HealMemberHeroes), tylko partie AI w marszu
                        int hurt = 0;
                        for (int i = 0; i < ro.Count; i++)
                        {
                            var hh = ro.GetCharacterAtIndex(i).HeroObject;
                            if (hh == null) continue;
                            if (prisoners ? hh.HitPoints < hh.WoundedHealthLimit : !hh.IsHealthFull()) hurt++;
                        }
                        if (hurt > 0) { _aiHurtSamples++; _aiHurtHeroes += hurt; _aiCutHpReal += cut / 4f * hurt; }
                    }
                }
                return true;
            }
            catch (Exception e) { if (++_stumbles <= 3) Log.Error("SlowHealing.RestCut", e); return false; }
            finally { if (ai) _aiTicks += Stopwatch.GetTimestamp() - t0; }
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
                    {
                        Log.Info("Leczenie w spoczynku (500): doba " + _day + " - gracz marsz " + _hMove + " h / spoczynek " + _hRest + " h (w tym morze " + _hSea + " h) | HP " + _hp0 + " -> " + h.HitPoints
                                 + "/" + h.MaxHitPoints + (h.IsWounded ? " (ranny)" : "") + " | nalezne bohaterom " + _dueHero.ToString("0.0") + " HP | ranni szeregowi " + _w0
                                 + " -> " + mp.MemberRoster.TotalWoundedRegulars + " | wywolania modelu z cieciem marszu (gra, UI, pomiar): szeregowi " + _cutsReg + ", bohaterowie " + _cutsHero
                                 + " (w tym jency " + _cutsPris + ") | marsz " + Math.Max(0, Math.Min(100, s.MovingHealPercent)) + "% | potkniecia " + _stumbles);
                        if (s.AiHealOnlyAtRest)
                            Log.Info("Leczenie w spoczynku (500-a): doba " + _day + " - AI: wywolania modelu " + _aiCalls + " (nic do leczenia " + _aiSkip + ") | probki (wywolania) z rannymi/bohaterami: marsz (ciete) "
                                     + _aiMarch + ", spoczynek " + _aiRest + " (w tym morze " + _aiSea + ") - nie liczba partii (do 4 wywolan na partie na tick, 4 ticki na dobe) | sciete: szeregowi "
                                     + _aiCutReg.ToString("0.0") + " stawki dobowej (ok. " + (_aiCutReg / 4f).ToString("0.0") + " rannych mniej wyleczonych), bohaterowie stawka nominalna "
                                     + _aiCutHp.ToString("0") + " HP (takze zdrowi), realnie ok. " + _aiCutHpReal.ToString("0") + " HP (" + _aiHurtSamples + " probek, bohaterow bez pelnego HP " + _aiHurtHeroes
                                     + ") | ranni bohaterowie (HP<=20) w druzynach AI, srednio w dobie: w marszu " + (_aiWndMarch / 4f).ToString("0.0") + ", w spoczynku " + (_aiWndRest / 4f).ToString("0.0")
                                     + " | jency AI ciec " + _aiPrisCuts + ", gracz jencem bez ciecia " + _aiCaptiveSkip + " | czas latki AI " + (_aiTicks * 1000.0 / Stopwatch.Frequency).ToString("0.00")
                                     + " ms | marsz " + Math.Max(0, Math.Min(100, s.MovingHealPercent)) + "% | potkniecia " + _stumbles);
                    }
                    _day = day; _hp0 = h.HitPoints; _w0 = mp.MemberRoster.TotalWoundedRegulars;
                    _hMove = 0; _hRest = 0; _hSea = 0; _cutsReg = 0; _cutsHero = 0; _cutsPris = 0; _dueHero = 0f;
                    ResetAi();
                }
                bool march = PlayerMarching(mp);
                if (march) _hMove++; else { _hRest++; if (mp.CurrentSettlement == null && mp.IsCurrentlyAtSea && mp.IsActive) _hSea++; }   // recenzja 500-a: port = osada
                if (mp.Party != null && mp.MapEvent == null && mp.IsActive)   // recenzja 500: gra leczy tylko partie czynna (TryHealOrWoundParty) - niewola, sluzba ROT bez naleznych
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
