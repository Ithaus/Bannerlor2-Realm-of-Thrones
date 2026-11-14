using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 513 PERKI LECZENIA (audyt perkow 14.11, docs/audyty-2026-11-14/PERKI.md rozdz. 2; decyzje PERKI-DECYZJE.md: 36/37 "ok", 60-68 "ok", 69 "ok").
    /// Linia leczenia 500 / 500-a / 501 (SlowHealing.RestCut, Litter501) - perki dopasowane do "leczenie tylko w spoczynku".
    ///
    /// 37 Athletics.WalkItOff (wodz partii) i 60 Medicine.WalkItOff (chirurg): w marszu ladem LEKKO ranni bohaterowie (HP powyzej progu rany gry
    ///    WoundedHealthLimit = 20, nie pelne HP) lecza sie WalkItOffLightWoundPercent (50) % stawki zamiast MovingHealPercent (10); CIEZKO ranni (HP <= 20)
    ///    i ranni szeregowi (w grze ranny szeregowy = niezdolny do walki, czyli ciezko) - bez zmian (MovingHealPercent). Obie wersje perku daja TO SAMO
    ///    (jedna zasada, bez sumowania): Medicine.WalkItOff = chirurg opatruje w drodze. Druga polowa Medicine.WalkItOff - "mniejsze pogorszenie ran
    ///    w marszu" - czeka na pogorszenie ran (jeszcze go nie ma): stala WalkItOffWorseningCut i MarchWorseningFactor (haczyk, dzis nieuzywany).
    ///    Mechanika: stawka gry jest na ROSTER (jedna liczba dla wszystkich bohaterow partii), wiec RestCut (SlowHealing) podnosi stawke marszu
    ///    do 50%, gdy partia ma perk i w rosterze jest lekko ranny bohater; ciezko rannym w tym samym ticku leczenie skalowane z powrotem
    ///    (x MovingHealPercent / 50, zaokraglenie losowe - ta sama wartosc oczekiwana co bez perku): okno na PartyHealCampaignBehavior.HealMemberHeroes
    ///    (prefiks / finalizer) + prefiks Hero.Heal tylko w oknie. Okno = ta sama partia i ta sama chwila kampanii, w ktorej RestCut podniosl stawke
    ///    (TryToHealOrWoundMembers liczy stawke tuz przed HealMemberHeroes). Gracz i AI - ta sama regula (500-a). Jency bez perku.
    ///    Premie gry "w ruchu" (+10% / +15% w modelu) zostaja w stawce i sa mnozone przez procent marszu (przy 10% to ok. 1-1.5 pkt).
    /// 61 TriageTent - bez zmian (Jeff: zostaje).
    /// 62 OPATRUNEK PO WYGRANEJ (Jeff 14.11: "jak wygralismy bitwe po prostu mnie lecza moi ludzie, medycyna wpada tez do mnie"): postfiks
    ///    DefaultPartyHealingModel.GetBattleEndHealingAmount (DeclaredMethod; BKROT dziedziczy te metode, NavalDLC poza lancuchem - BkrotChains) -
    ///    dla partii strony WYGRANEJ wynik gry zastapiony opatrunkiem: utracone HP x (Medicine CHIRURGA partii
    ///    (EffectiveSurgeon, bez chirurga wodz - jak gra) / 100 x BattleDressingPer100Medicine %, sufit Medicine 300) + PreventiveMedicine CHIRURGA
    ///    (30%, wartosc perku gry; wlasne PreventiveMedicine bohatera zostaje, ale z chirurgowym liczy sie raz) ; wlasne Medicine.WalkItOff +10 po bitwie zaczepnej - jak gra (perk osobisty). Strona PRZEGRANA - wynik gry bez
    ///    zmian (decyzja dla Jeffa). Leczy gra (Hero.Heal w OnBattleEndCheckPerkEffects) - loze HeroWounds dalej przycina do progu, 318a dalej
    ///    pomija rannych lordow pokonanych. XP chirurga - SkillLevelingManager.OnHeroHealedWhileWaiting z REALNIE przybylych HP, tylko czesc chirurga (finalizer okna
    ///    OnBattleEndCheckPerkEffects; ta sama droga XP co leczenie gry). AI - klucz BattleDressingAi.
    /// 69 NavalDLC Boatswain.Resilience (osobisty): po wygranej bitwie MORSKIEJ (MapEvent.IsNavalMapEvent) opatrunek bohatera z perkiem x (1 + 30%).
    ///    Wlasne 30% DLC zostaje martwe (BkrotChains - metoda dziedziczona); gdyby lancuch NavalDLC wszedl na te metode (515), NavalGuardPostfix zdejmuje dodatek DLC u opatrzonego.
    /// 63 BushDoctor (chirurg): takze w OBOZIE (NightRest / BK: gracz PlayerCamped, AI IsCamping) przy dowolnej wsi (ten sam promien co gra:
    ///    odleglosc^2 < 2, wies nie pladrowana) - gra liczy tylko ostatnio odwiedzona wies; gdy warunek gry jest spelniony, nie dokladamy drugi raz.
    /// 68 AGoodDaysRest (wodz partii): takze w obozie (gra: tylko w osadzie). Oba (63, 68): postfiks dziennego leczenia bohaterow i szeregowych, tylko
    ///    dodatni wynik, poza osada (w osadzie liczy gra), bez jencow.
    /// 66 CheatDeath CHIRURGA w rzucie smierci przesilenia HeroWounds (p x (1 + premia perku gry -50%)) - wolane z HeroWounds.Crisis.
    /// 36 FormFittingArmor (osobisty, -15% wagi zbroi w grze): nasze kary biegu (FieldCraft.MeasureWind) i zadyszki (BattleWind.MakeBreath) licza
    ///    zbroje x 0.85 - ta sama liczba co GetEffectiveArmorEncumbrance gry.
    ///
    /// Linia startu "Perki leczenia (513): start sesji" i linia doby "Perki leczenia (513): doba N" w logu Armoury. Bledy liczone (potkniecia, pierwszy
    /// blad miejsca do logu), bez globalnego wylacznika. Nic co klatke (model leczenia: gracz co godzine, AI 4 x na dobe; agent - raz przy starcie).
    /// </summary>
    internal static class Perk513
    {
        private const string HarmonyId = "com.jeff.armoury.perk513";
        private const string P = "Perki leczenia (513): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>60: Medicine.WalkItOff chirurga - o ile mniejsze pogorszenie ran w marszu (gdy pogorszenie wejdzie). Dzis nieuzywane.</summary>
        internal const float WalkItOffWorseningCut = 0.5f;

        private static bool _hookTried;
        private static string _hookNote = "-";
        private static int _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);

        // 37/60: stawka podniesiona przez RestCut (partia, chwila kampanii, procent marszu i procent perku)
        private static PartyBase _raiseParty; private static double _raiseAt = -1; private static int _raiseLo, _raiseHi;
        [ThreadStatic] private static PartyBase _win;     // okno HealMemberHeroes z podniesiona stawka
        [ThreadStatic] private static float _winScale;
        // 62: okno OnBattleEndCheckPerkEffects
        private sealed class Dress { public Hero H; public PartyBase Party; public Hero Surgeon; public int Hp0; public int Asked; public int SurgHp; public int WalkHp; public bool Player; public bool Naval; }
        [ThreadStatic] private static List<Dress> _dress;
        [ThreadStatic] private static bool _inEnd;
        [ThreadStatic] private static Hero _dressedHero;   // bohater, ktoremu DressPostfix dal opatrunek w biezacym wywolaniu modelu (straz 69 / NavalDLC)

        // linia doby
        private static int _day = -1;
        private static int _dRaise, _dRaisePlayer, _dLight, _dHeavy, _dHeavyCut, _dWin;
        private static int _dDressBattles, _dDressHeroes, _dDressHp, _dDressXpCalls, _dDressAi, _dDressNaval, _dDressPlayerHp = -1, _dNavalCut;
        private static int _dGoodRest, _dBush, _dCheat, _dFfa;
        private static int _sDressHp, _sCheat, _sFfa;
        private static PerkObject _resilience; private static bool _resTried;
        private static TextObject _tDress, _tCampRest, _tBush;

        internal static void Reset()
        {
            _raiseParty = null; _raiseAt = -1; _raiseLo = 0; _raiseHi = 0; _win = null; _winScale = 1f; _dress = null; _inEnd = false; _dressedHero = null;
            _resTried = false; _resilience = null;   // recenzja: PerkObject NavalDLC zyje w MBObjectManager danej gry (NavalPerks.Create) - stara referencja po wczytaniu innego zapisu = GetPerkValue false
            _stumbles = 0; _errSites.Clear(); _day = -1; DayReset(); _sDressHp = 0; _sCheat = 0; _sFfa = 0;
        }

        private static void DayReset()
        {
            _dRaise = _dRaisePlayer = _dLight = _dHeavy = _dHeavyCut = _dWin = 0;
            _dDressBattles = _dDressHeroes = _dDressHp = _dDressXpCalls = _dDressAi = _dDressNaval = _dNavalCut = 0; _dDressPlayerHp = -1;
            _dGoodRest = _dBush = _dCheat = _dFfa = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error("Perk513." + where, e); } catch { }
        }

        private static PerkObject Resilience
        {
            get
            {
                if (!_resTried) { _resTried = true; try { _resilience = MBObjectManager.Instance?.GetObject<PerkObject>("Resilience"); } catch { _resilience = null; } }
                return _resilience;
            }
        }

        // ------------------------------------------------------------ latki (raz na proces, w kampanii - OnSessionLaunched)
        internal static void EnsureHooks()
        {
            if (!_hookTried)
            {
                _hookTried = true;
                var h = new Harmony(HarmonyId);
                var notes = new List<string>(6);
                notes.Add("HealMemberHeroes " + Wire(h, AccessTools.DeclaredMethod(typeof(PartyHealCampaignBehavior), "HealMemberHeroes"), nameof(WinPrefix), null, nameof(WinFinalizer)));
                notes.Add("Hero.Heal " + Wire(h, AccessTools.DeclaredMethod(typeof(Hero), "Heal", new[] { typeof(int), typeof(bool) }), nameof(HealPrefix), null, null));
                notes.Add("OnBattleEndCheckPerkEffects " + Wire(h, AccessTools.DeclaredMethod(typeof(PartyHealCampaignBehavior), "OnBattleEndCheckPerkEffects", new[] { typeof(MapEvent) }),
                                                                 nameof(EndPrefix), null, nameof(EndFinalizer)));
                notes.Add("GetBattleEndHealingAmount " + Wire(h, AccessTools.DeclaredMethod(typeof(DefaultPartyHealingModel), "GetBattleEndHealingAmount"), null, nameof(DressPostfix), null));
                notes.Add("GetDailyHealingHpForHeroes " + Wire(h, AccessTools.DeclaredMethod(typeof(DefaultPartyHealingModel), "GetDailyHealingHpForHeroes"), null, nameof(CampPostfix), null));
                notes.Add("GetDailyHealingForRegulars " + Wire(h, AccessTools.DeclaredMethod(typeof(DefaultPartyHealingModel), "GetDailyHealingForRegulars"), null, nameof(CampPostfix), null));
                var navalModel = AccessTools.TypeByName("NavalDLC.GameComponents.NavalDLCPartyHealingModel");
                notes.Add("NavalDLC GetBattleEndHealingAmount " + (navalModel == null ? "brak DLC"
                          : Wire(h, AccessTools.DeclaredMethod(navalModel, "GetBattleEndHealingAmount"), null, nameof(NavalGuardPostfix), null)));
                _hookNote = string.Join("; ", notes);
            }
            var s = Settings.Current;
            if (s == null) return;
            Log.Info(P + "start sesji - latki " + _hookNote + " | 37/60 WalkItOff lekko ranni (HP > progu) w marszu " + Math.Max(0, Math.Min(100, s.WalkItOffLightWoundPercent))
                     + "% stawki (marsz " + s.MovingHealPercent + "%, Heal Only At Rest " + (s.HealOnlyAtRest ? "TAK" : "NIE") + ") | 62 opatrunek po wygranej "
                     + (s.BattleDressingEnabled ? "TAK" : "NIE") + " (Medicine chirurga " + s.BattleDressingPer100Medicine + "% na 100 pkt + Preventive Medicine chirurga albo wlasne 30% raz, AI "
                     + (s.BattleDressingAi ? "TAK" : "NIE") + ", 69 Resilience na morzu +" + s.BattleDressingNavalResilience + "%) | 63/68 Bush Doctor i A Good Days Rest w obozie "
                     + (s.CampHealingPerks ? "TAK" : "NIE") + " | 66 Cheat Death chirurga w przesileniu " + (s.CheatDeathAtCrisis ? "TAK" : "NIE") + " | 36 Form Fitting Armor w biegu i zadyszce "
                     + (s.FormFittingArmorInBreath ? "TAK" : "NIE") + " | Resilience (NavalDLC) " + (Resilience != null ? "jest" : "BRAK") + ".");
        }

        private static string Wire(Harmony h, System.Reflection.MethodInfo m, string prefix, string postfix, string finalizer)
        {
            try
            {
                if (m == null) return "BRAK (metody nie ma)";
                h.Patch(m, prefix: prefix == null ? null : new HarmonyMethod(typeof(Perk513), prefix),
                           postfix: postfix == null ? null : new HarmonyMethod(typeof(Perk513), postfix) { priority = Priority.Low - 2 },
                           finalizer: finalizer == null ? null : new HarmonyMethod(typeof(Perk513), finalizer));
                var info = Harmony.GetPatchInfo(m);
                bool ok = false;
                if (info != null)
                {
                    foreach (var p in info.Prefixes) if (p != null && p.owner == HarmonyId) ok = true;
                    foreach (var p in info.Postfixes) if (p != null && p.owner == HarmonyId) ok = true;
                }
                return ok ? "wpieta" : "BRAK (latka nie weszla)";
            }
            catch (Exception e) { Stumble("Wire", e); return "BRAK (" + e.GetType().Name + ")"; }
        }

        // ------------------------------------------------------------ 37 / 60: WalkItOff w marszu
        private static bool HasWalkItOff(MobileParty mp)
        {
            return mp.HasPerk(DefaultPerks.Athletics.WalkItOff) || mp.HasPerk(DefaultPerks.Medicine.WalkItOff);
        }

        /// <summary>Wolane z SlowHealing.RestCut (bohaterowie-czlonkowie, partia w marszu, przed cieciem). Zwraca procent marszu: pct albo
        /// WalkItOffLightWoundPercent, gdy partia ma WalkItOff (Athletics wodza albo Medicine chirurga) i w rosterze jest lekko ranny bohater.</summary>
        internal static int MarchPct(PartyBase party, MobileParty mp, int pct)
        {
            try
            {
                var s = Settings.Current;
                int hi = s != null ? Math.Min(100, s.WalkItOffLightWoundPercent) : 0;
                if (hi <= pct || party == null || mp == null) return pct;
                var ro = party.MemberRoster;
                if (ro == null || ro.TotalHeroes <= 0 || !HasWalkItOff(mp)) return pct;
                bool light = false;
                for (int i = 0; i < ro.Count && !light; i++)
                {
                    var hh = ro.GetCharacterAtIndex(i).HeroObject;
                    if (hh != null && hh.HitPoints > hh.WoundedHealthLimit && !hh.IsHealthFull()) light = true;
                }
                if (!light) return pct;
                _raiseParty = party; _raiseAt = CampaignTime.Now.ToHours; _raiseLo = pct; _raiseHi = hi;
                return hi;
            }
            catch (Exception e) { Stumble("MarchPct", e); return pct; }
        }

        /// <summary>60 (haczyk na pogorszenie ran w marszu - jeszcze nie istnieje): mnoznik pogorszenia dla partii; chirurg z Medicine.WalkItOff - x (1 - WalkItOffWorseningCut).</summary>
        internal static float MarchWorseningFactor(MobileParty mp)
        {
            try { return mp != null && mp.HasPerk(DefaultPerks.Medicine.WalkItOff) ? 1f - WalkItOffWorseningCut : 1f; }
            catch (Exception e) { Stumble("MarchWorseningFactor", e); return 1f; }
        }

        public static void WinPrefix(PartyBase __0)
        {
            _win = null;
            try
            {
                if (__0 == null || !ReferenceEquals(__0, _raiseParty) || _raiseHi <= 0 || Math.Abs(CampaignTime.Now.ToHours - _raiseAt) > 1e-6) return;
                _win = __0; _winScale = _raiseLo / (float)_raiseHi;
                _raiseParty = null;
                _dWin++; _dRaise++; if (__0 == PartyBase.MainParty) _dRaisePlayer++;
            }
            catch (Exception e) { _win = null; Stumble("WinPrefix", e); }
        }

        public static void WinFinalizer() { _win = null; }

        /// <summary>Hero.Heal w oknie podniesionej stawki: ciezko ranny (HP <= progu) - leczenie x MovingHealPercent / WalkItOff (losowe zaokraglenie).</summary>
        public static bool HealPrefix(Hero __instance, ref int healAmount)
        {
            var w = _win;
            if (w == null) return true;
            try
            {
                if (__instance == null || healAmount <= 0) return true;
                if (__instance.HitPoints > __instance.WoundedHealthLimit) { _dLight++; return true; }
                _dHeavy++;
                float f = healAmount * _winScale;
                int n = (int)f;
                if (f - n > MBRandom.RandomFloat) n++;
                _dHeavyCut += healAmount - n;
                if (n <= 0) return false;
                healAmount = n;
                return true;
            }
            catch (Exception e) { Stumble("HealPrefix", e); return true; }
        }

        // ------------------------------------------------------------ 62 / 69: opatrunek po wygranej
        public static void EndPrefix() { _inEnd = true; if (_dress != null) _dress.Clear(); }

        /// <summary>Po leczeniu gry: XP chirurga z realnie przybylych HP (loze HeroWounds i 318a moga przyciac), linia logu bitwy gracza, komunikat.</summary>
        public static void EndFinalizer(MapEvent __0)
        {
            _inEnd = false;
            var l = _dress;
            if (l == null || l.Count == 0) return;
            try
            {
                int heroes = 0, hp = 0, playerHp = -1;
                bool player = false, naval = false;
                Hero playerSurgeon = null; bool playerDressed = false;
                var sb = new System.Text.StringBuilder(64);
                foreach (var d in l)
                {
                    try
                    {
                        int got = d.H.HitPoints - d.Hp0;
                        if (got <= 0) continue;
                        heroes++; hp += got;
                        if (d.Player) player = true;
                        if (d.Naval) naval = true;
                        if (d.H == Hero.MainHero) { playerHp = got; playerSurgeon = d.SurgHp > 0 ? d.Surgeon : null; playerDressed = d.Asked > d.WalkHp; }
                        int surgGot = Math.Min(got, d.SurgHp);   // recenzja: XP chirurga tylko z jego opatrunku (bez wlasnego WalkItOff +10 i wlasnego Preventive Medicine)
                        if (d.Surgeon != null && surgGot > 0 && d.H.PartyBelongedTo != null) { SkillLevelingManager.OnHeroHealedWhileWaiting(d.H, surgGot); _dDressXpCalls++; }
                        if (d.Player && sb.Length < 600) sb.Append(" [").Append(d.H.Name).Append(" +").Append(got).Append(" (prosba ").Append(d.Asked).Append(", chirurga ").Append(d.SurgHp).Append(") HP ").Append(d.H.HitPoints).Append('/').Append(d.H.MaxHitPoints)
                                                    .Append(", chirurg ").Append(d.Surgeon != null ? d.Surgeon.Name.ToString() + " Medicine " + d.Surgeon.GetSkillValue(DefaultSkills.Medicine) : "brak").Append(']');
                    }
                    catch (Exception e) { Stumble("EndFinalizer(bohater)", e); }
                }
                if (heroes == 0) return;
                _dDressBattles++; _dDressHeroes += heroes; _dDressHp += hp; _sDressHp += hp;
                if (naval) _dDressNaval++;
                if (!player) _dDressAi++;
                if (playerHp > 0 && playerDressed)   // sam WalkItOff +10 (bez opatrunku) - bez komunikatu
                {
                    _dDressPlayerHp = Math.Max(0, _dDressPlayerHp) + playerHp;
                    try
                    {
                        if (playerSurgeon == null || playerSurgeon == Hero.MainHero) Log.Player("You dress your own wounds after the victory (+" + playerHp + " hit points).");
                        else Log.Player(playerSurgeon.Name + " dresses your wounds after the victory (+" + playerHp + " hit points).");
                    }
                    catch (Exception e) { Stumble("EndFinalizer(komunikat)", e); }
                }
                if (player) Log.Info(P + "opatrunek po wygranej bitwie" + (naval ? " MORSKIEJ" : "") + " - bohaterowie " + heroes + ", +" + hp + " HP:" + sb + ".");
            }
            catch (Exception e) { Stumble("EndFinalizer", e); }
            finally { l.Clear(); }
        }

        /// <summary>Postfiks GetBattleEndHealingAmount (Default; BKROT dziedziczy). Strona wygrana - opatrunek chirurga; wlasne Preventive Medicine
        /// bohatera zostaje (recenzja: perk osobisty), ale z Preventive Medicine chirurga liczy sie RAZ (30%, nie 60%) - wygrany nigdy gorzej niz przegrany.</summary>
        public static void DressPostfix(PartyBase __0, Hero __1, ref ExplainedNumber __result)
        {
            _dressedHero = null;
            try
            {
                var s = Settings.Current;
                var party = __0; var hero = __1;
                if (s == null || !s.BattleDressingEnabled || party == null || hero == null) return;
                var me = party.MapEvent;
                if (me == null || !me.HasWinner || party.Side == BattleSideEnum.None || party.Side != me.WinningSide) return;   // przegrani - wynik gry
                bool player = party == PartyBase.MainParty || hero == Hero.MainHero;
                if (!player && !s.BattleDressingAi) return;
                int lost = hero.MaxHitPoints - hero.HitPoints;
                var mp = party.MobileParty;
                Hero surgeon = mp != null ? mp.EffectiveSurgeon : null;
                var pm = DefaultPerks.Medicine.PreventiveMedicine;
                float surgPct = 0f, ownPct = 0f;
                if (surgeon != null)
                {
                    int med = Math.Min(300, Math.Max(0, surgeon.GetSkillValue(DefaultSkills.Medicine)));
                    surgPct = med / 100f * Math.Max(0, s.BattleDressingPer100Medicine) / 100f;
                    if (surgeon.GetPerkValue(pm)) surgPct += pm.SecondaryBonus;
                }
                // wlasne Preventive Medicine (jak gra) - tylko gdy nie liczyl go juz chirurg (sam bohater-chirurg albo chirurg z tym perkiem)
                if (hero.GetPerkValue(pm) && !(surgeon != null && surgeon.GetPerkValue(pm))) ownPct = pm.SecondaryBonus;
                float pct = surgPct + ownPct;
                bool naval = false;
                try { naval = me.IsNavalMapEvent; } catch { naval = false; }
                var res = Resilience;
                if (naval && res != null && pct > 0f && hero.GetPerkValue(res)) pct *= 1f + Math.Max(0, s.BattleDressingNavalResilience) / 100f;
                if (pct > 0.9f) pct = 0.9f;
                if (_tDress == null) _tDress = new TextObject("{=!}Wounds dressed after the victory");
                var r = new ExplainedNumber(0f, false, null);
                if (lost > 0 && pct > 0f) r.Add(pct * lost, _tDress);
                // czesc chirurga (do XP i komunikatu): jego udzial w opatrunku, po mnozniku Resilience i suficie
                float surgShare = (surgPct + ownPct) > 0f ? surgPct / (surgPct + ownPct) : 0f;
                int surgHp = lost > 0 ? (int)Math.Round(pct * lost * surgShare) : 0;
                // wlasne Medicine.WalkItOff (+10 HP po bitwie zaczepnej, perk osobisty) - jak gra
                int walkHp = 0;
                if (party.MapEventSide == me.AttackerSide && hero.GetPerkValue(DefaultPerks.Medicine.WalkItOff))
                {
                    r.Add(DefaultPerks.Medicine.WalkItOff.SecondaryBonus, DefaultPerks.Medicine.WalkItOff.Name);
                    walkHp = (int)Math.Round(DefaultPerks.Medicine.WalkItOff.SecondaryBonus);
                }
                __result = r;
                _dressedHero = hero;
                if (_inEnd && lost > 0)
                {
                    if (_dress == null) _dress = new List<Dress>(8);
                    _dress.Add(new Dress { H = hero, Party = party, Surgeon = surgeon, Hp0 = hero.HitPoints, Asked = r.RoundedResultNumber, SurgHp = surgHp, WalkHp = walkHp, Player = player, Naval = naval });
                }
            }
            catch (Exception e) { _dressedHero = null; Stumble("DressPostfix", e); }
        }

        /// <summary>69 straz (recenzja; scalanie z 515 / punkt 102): gdyby NavalDLCPartyHealingModel wszedl do lancucha modeli, dolozylby swoje 30%
        /// utraconego HP (Resilience) na KAZDEJ bitwie do wyniku bazy, w ktorym jest juz nasz opatrunek z mnoznikiem Resilience. Gdy opatrunek byl dany
        /// temu bohaterowi w tym wywolaniu (_dressedHero) - dodatek DLC zdjety. Dzis metoda jest poza lancuchem (BkrotChains) - latka bez skutku.</summary>
        public static void NavalGuardPostfix(Hero __1, ref ExplainedNumber __result)
        {
            var dh = _dressedHero;
            _dressedHero = null;
            try
            {
                if (dh == null || __1 == null || !ReferenceEquals(dh, __1)) return;
                var res = Resilience;
                if (res == null || !__1.GetPerkValue(res)) return;
                float add = res.PrimaryBonus * (__1.MaxHitPoints - __1.HitPoints);   // dokladnie to, co dodal NavalDLC (NavalDLCPartyHealingModel.GetBattleEndHealingAmount)
                if (add <= 0f) return;
                __result.Add(-add, res.Name);
                _dNavalCut++;
            }
            catch (Exception e) { Stumble("NavalGuardPostfix", e); }
        }

        // ------------------------------------------------------------ 63 / 68: perki w obozie
        /// <summary>Partia stoi obozem w polu: gracz - namiot NightRest / BK (PlayerCamped) i nie w marszu; AI - nocny oboz NightRest (IsCamping).</summary>
        private static bool Camped(MobileParty mp)
        {
            if (mp == null || mp.CurrentSettlement != null || mp.IsCurrentlyAtSea || mp.MapEvent != null || mp.BesiegedSettlement != null) return false;
            if (mp == MobileParty.MainParty) return NightRest.PlayerCamped && !SlowHealing.Marching(mp);
            return NightRest.IsCamping(mp);
        }

        /// <summary>Warunek gry dla BushDoctor (DefaultPartyHealingModel): postoj przy ostatnio odwiedzonej wsi - wtedy gra juz doliczyla perk.</summary>
        private static bool GameBush(MobileParty mp, PartyBase party)
        {
            var lv = mp.LastVisitedSettlement;
            return !mp.IsMoving && lv != null && lv.IsVillage && lv.Position.DistanceSquared(party.Position) < 2f && !lv.IsUnderRaid && !lv.IsRaided;
        }

        private static bool VillageNear(PartyBase party)
        {
            var pos = party.Position;
            var data = Settlement.StartFindingLocatablesAroundPosition(pos.ToVec2(), 1.5f);
            for (var st = Settlement.FindNextLocatable(ref data); st != null; st = Settlement.FindNextLocatable(ref data))
                if (st.IsVillage && !st.IsUnderRaid && !st.IsRaided && st.Position.DistanceSquared(pos) < 2f) return true;
            return false;
        }

        public static void CampPostfix(PartyBase __0, bool __1, ref ExplainedNumber __result)
        {
            try
            {
                if (__1 || __0 == null || __result.ResultNumber <= 0f) return;   // jency, kary (glod, tratwa) - bez zmian
                var s = Settings.Current;
                if (s == null || !s.CampHealingPerks) return;
                var mp = __0.MobileParty;
                if (mp == null || mp.IsGarrison || mp.IsMilitia || !Camped(mp)) return;
                bool player = mp == MobileParty.MainParty;
                if (mp.HasPerk(DefaultPerks.Athletics.AGoodDaysRest))
                {
                    if (_tCampRest == null) _tCampRest = new TextObject("{=!}A Good Day's Rest (camp)");
                    __result.AddFactor(DefaultPerks.Athletics.AGoodDaysRest.PrimaryBonus, _tCampRest);
                    _dGoodRest++;
                }
                if (mp.HasPerk(DefaultPerks.Medicine.BushDoctor, true) && !GameBush(mp, __0) && VillageNear(__0))
                {
                    if (_tBush == null) _tBush = new TextObject("{=!}Bush Doctor (camp by a village)");
                    __result.AddFactor(DefaultPerks.Medicine.BushDoctor.SecondaryBonus, _tBush);
                    _dBush++;
                }
            }
            catch (Exception e) { Stumble("CampPostfix", e); }
        }

        // ------------------------------------------------------------ 66: Cheat Death chirurga w przesileniu
        /// <summary>Wolane z HeroWounds.Crisis (chirurg partii, nie sam ranny): p x (1 + premia perku gry, -50%).</summary>
        internal static void CheatDeath(Hero surgeon, ref float p, ref string mods)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CheatDeathAtCrisis || surgeon == null || !surgeon.GetPerkValue(DefaultPerks.Medicine.CheatDeath)) return;
                float k = 1f + DefaultPerks.Medicine.CheatDeath.SecondaryBonus;
                if (k < 0f) k = 0f;
                p *= k;
                mods += ", Cheat Death chirurga x" + k.ToString("0.##", Inv);
                _dCheat++; _sCheat++;
            }
            catch (Exception e) { Stumble("CheatDeath", e); }
        }

        // ------------------------------------------------------------ 36: Form Fitting Armor w biegu i zadyszce
        /// <summary>Waga zbroi do naszych kar biegu (FieldCraft) i zadyszki (BattleWind): bohater z perkiem - x (1 + premia perku gry, -15%).</summary>
        internal static float ArmorKg(Agent agent, float kg)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.FormFittingArmorInBreath || kg <= 0f || agent == null) return kg;
                var co = agent.Character as CharacterObject;
                if (co == null || !co.IsHero || !co.GetPerkValue(DefaultPerks.Athletics.FormFittingArmor)) return kg;
                _dFfa++; _sFfa++;
                return Math.Max(0f, kg * (1f + DefaultPerks.Athletics.FormFittingArmor.PrimaryBonus));
            }
            catch (Exception e) { Stumble("ArmorKg", e); return kg; }
        }

        // ------------------------------------------------------------ linia doby
        internal static void Hourly()
        {
            try
            {
                int day = (int)CampaignTime.Now.ToDays;
                if (_day < 0) { _day = day; return; }
                if (day == _day) return;
                Log.Info(P + "doba " + _day + " - 37/60 WalkItOff: okna podniesionej stawki " + _dWin + " (gracz " + _dRaisePlayer + "), leczenia lekko rannych " + _dLight
                         + ", ciezko rannych przycietych " + _dHeavy + " (-" + _dHeavyCut + " HP) | 62 opatrunek: bitwy " + _dDressBattles + " (AI " + _dDressAi + ", morskie " + _dDressNaval
                         + ", zdjete Resilience NavalDLC " + _dNavalCut + "), bohaterowie " + _dDressHeroes + ", +" + _dDressHp + " HP, XP chirurga " + _dDressXpCalls + ", gracz " + (_dDressPlayerHp < 0 ? "-" : "+" + _dDressPlayerHp + " HP")
                         + " | 68 A Good Days Rest w obozie " + _dGoodRest + ", 63 Bush Doctor w obozie " + _dBush + " (wywolania modelu) | 66 Cheat Death w przesileniu " + _dCheat
                         + " | 36 Form Fitting Armor agenci " + _dFfa + " | sesja: opatrunek +" + _sDressHp + " HP, Cheat Death " + _sCheat + ", Form Fitting Armor " + _sFfa
                         + " | potkniecia " + _stumbles);
                _day = day;
                DayReset();
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }
    }
}
