using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace RealisticCaptivity
{
    /// <summary>
    /// Czysty odskok. Vanilla potrafi "dogonic" konnego uciekiniera piechota:
    /// po ucieczce z bitwy gracz dostaje kare dezorganizacji i stoi w miejscu,
    /// a piesza banda wchodzi w niego drugi raz i bierze w niewole. Realizm:
    /// jesli CALA druzyna gracza jest konna, a POSCIG nie ma ani jednego konia,
    /// ucieczka z pola bitwy jest czysta - kara dezorganizacji znika, a ta
    /// konkretna banda nie moze wszczac starcia przez kilka godzin.
    /// Konny poscig lapie normalnie - kon przed koniem nie ucieknie za darmo.
    /// </summary>
    internal static class CleanBreak
    {
        private static string _pursuerId;
        private static CampaignTime _until = CampaignTime.Zero;

        // KIEDY gracz FAKTYCZNIE uciekl z pola - nacisnal odwrot w misji albo
        // odjechal z menu spotkania. To JEDYNY moment, w ktorym odskok ma sens.
        private static CampaignTime _fledAt = CampaignTime.Zero;

        /// <summary>Gracz wlasnie zwial z pola - zapamietaj chwile.</summary>
        internal static void MarkFled()
        {
            try
            {
                _fledAt = CampaignTime.Now;
                Log.Info("CleanBreak: gracz opuscil pole (odwrot) - odskok przysluguje.");
            }
            catch { }
        }

        /// <summary>Czy ucieczka wydarzyla sie przed chwila (misja konczy sie w sekundach czasu mapy).</summary>
        private static bool JustFled()
        {
            try { return _fledAt != CampaignTime.Zero && (float)(CampaignTime.Now - _fledAt).ToHours < 2f; }
            catch { return false; }
        }

        internal static void OnMapEventEnded(MapEvent ev)
        {
            try
            {
                var c = Settings.Current;
                if (!c.CleanBreakEnabled || ev == null || !ev.IsPlayerMapEvent) return;
                if (PlayerCaptivity.IsCaptive) return;                    // juz pojmany - za pozno
                if (ev.WinningSide == ev.PlayerSide) return;              // wygrana - nie dotyczy

                MobileParty pursuer = null;
                var side = ev.GetMapEventSide(ev.PlayerSide.GetOppositeSide());
                if (side != null)
                    foreach (var p in side.Parties)
                    {
                        var mp = p.Party != null ? p.Party.MobileParty : null;
                        if (mp != null && mp.IsActive && mp != MobileParty.MainParty) { pursuer = mp; break; }
                    }
                if (pursuer == null) return;
                if (!EveryoneMounted(MobileParty.MainParty)) return;      // ktos u nas piechota - nie ma odskoku
                if (AnyoneMounted(pursuer)) return;                       // maja konie - moga gonic

                _pursuerId = pursuer.StringId;
                _until = CampaignTime.HoursFromNow(c.CleanBreakHours);
                MobileParty.MainParty.SetDisorganized(false);
                Log.Player("You spur away. No man on foot catches a rider. (" + c.CleanBreakHours + "h clean break)");
                Log.Info("CleanBreak: odskok od " + pursuer.StringId + " na " + c.CleanBreakHours + "h.");
            }
            catch (Exception e) { Log.Error("CleanBreak.OnMapEventEnded", e); }
        }

        private static bool EveryoneMounted(MobileParty p)
        {
            try
            {
                var r = p.MemberRoster;
                if (r == null || r.TotalManCount == 0) return false;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Number <= 0) continue;
                    if (!el.Character.IsMounted) return false;
                }
                return true;
            }
            catch { return false; }
        }

        private static bool AnyoneMounted(MobileParty p)
        {
            try
            {
                var r = p.MemberRoster;
                if (r == null) return true;   // nie wiemy - zakladamy ostroznie, ze moga gonic
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Character != null && el.Number > 0 && el.Character.IsMounted) return true;
                }
                return false;
            }
            catch { return true; }
        }

        /// <summary>Publiczne wejscia dla MountedGetaway - te same testy koni.</summary>
        internal static bool PartyAllMounted(MobileParty p) { return EveryoneMounted(p); }
        internal static bool PartyAnyMounted(MobileParty p) { return AnyoneMounted(p); }

        /// <summary>Zarejestruj blokade tej bandy na CleanBreakHours od teraz.</summary>
        internal static void RegisterBlock(MobileParty pursuer)
        {
            try
            {
                if (pursuer == null) return;
                _pursuerId = pursuer.StringId;
                _until = CampaignTime.HoursFromNow(Settings.Current.CleanBreakHours);
                Log.Info("CleanBreak: blokada " + pursuer.StringId + " na " + Settings.Current.CleanBreakHours + "h (odjazd z menu).");
            }
            catch { }
        }

        internal static bool ShouldBlock(PartyBase attackerParty, PartyBase defenderParty)
        {
            try
            {
                if (_pursuerId == null || CampaignTime.Now > _until) return false;
                if (defenderParty != PartyBase.MainParty) return false;
                var mp = attackerParty != null ? attackerParty.MobileParty : null;
                return mp != null && mp.StringId == _pursuerId;
            }
            catch { return false; }
        }

        /// <summary>
        /// Ostatnia zapora - sama NIEWOLA. Pierwsza wersja pilnowala tylko NOWYCH
        /// starc, a vanilla lapie uciekiniera w ramach TEGO SAMEGO trwajacego
        /// spotkania (odwrot z misji -> spotkanie konczy sie "pojmaniem").
        /// Tu przechwytujemy sam akt wziecia do niewoli: jesli caly oddzial gracza
        /// jest konny, a lapacze nie maja ANI JEDNEGO konia i nie prowadza
        /// oblezenia - piechota nie bierze jezdzca. Odskok + blokada na kilka godzin.
        /// Kon zabity w bitwie = brak konia w ekwipunku = niewola JEST mozliwa.
        /// </summary>
        internal static bool InterceptCapture(PartyBase capturerParty, Hero prisonerCharacter)
        {
            try
            {
                var c = Settings.Current;
                if (!c.CleanBreakEnabled) return true;
                if (prisonerCharacter == null || prisonerCharacter != Hero.MainHero) return true;
                if (PlayerCaptivity.IsCaptive) return true;
                // ODSKOK NALEZY SIE TEMU, KTO UCIEKL - i nikomu innemu.
                // Pierwsza wersja pytala "czy jestes ranny", a to bez sensu:
                // uciekinier tez bywa poobijany, wiec konny, ktory dal drapaka,
                // ladowal w lochu ("ucieklem konno, a mnie zlapali" - Jeff).
                // Liczy sie JEDYNA rzecz, ktora te dwa przypadki rozdziela:
                // czy odwrot w ogole nastapil. Padles na polu - lapia. Zwiales - nie.
                if (c.CleanBreakNeedsStanding && !JustFled())
                {
                    Log.Info("CleanBreak: gracz nie uciekal (padl na polu) - niewola zgodnie z prawem.");
                    return true;
                }
                var mp = capturerParty != null ? capturerParty.MobileParty : null;
                if (mp == null) return true;                              // garnizon/osada - nie nasz przypadek
                if (mp.BesiegedSettlement != null) return true;           // oblezenie - lapia normalnie
                if (!EveryoneMounted(MobileParty.MainParty)) return true; // ktos u nas pieszo (albo kon polegl)
                if (AnyoneMounted(mp)) return true;                       // maja konie - dogonili uczciwie

                _pursuerId = mp.StringId;
                _until = CampaignTime.HoursFromNow(c.CleanBreakHours);
                try { MobileParty.MainParty.SetDisorganized(false); } catch { }
                Log.Player("They grasp at empty air - no man on foot takes a rider. You spur away.");
                Log.Info("CleanBreak: udaremniona niewola od " + mp.StringId + " (piesi vs konny), blokada " + c.CleanBreakHours + "h.");
                return false;                                             // niewola NIE nastepuje
            }
            catch (Exception e) { Log.Error("CleanBreak.InterceptCapture", e); return true; }
        }
    }

    /// <summary>
    /// ODWROT Z POLA. Gracz nacisnal "Retreat" w bitwie - to i tylko to znaczy,
    /// ze UCIEKL. Zapamietujemy chwile, bo od niej zalezy, czy piesza banda
    /// ma prawo go zlapac.
    /// </summary>
    [HarmonyPatch(typeof(TaleWorlds.MountAndBlade.Mission), "RetreatMission")]
    internal static class RetreatMarkPatch
    {
        private static void Postfix() { CleanBreak.MarkFled(); }
    }

    /// <summary>Piesza banda nie bierze konnego do niewoli - przechwycenie samego aktu pojmania.</summary>
    [HarmonyPatch(typeof(TaleWorlds.CampaignSystem.Actions.TakePrisonerAction), "ApplyInternal")]
    internal static class CleanBreakCapturePatch
    {
        private static bool Prefix(PartyBase capturerParty, Hero prisonerCharacter)
        {
            return CleanBreak.InterceptCapture(capturerParty, prisonerCharacter);
        }
    }

    /// <summary>
    /// "Try to get away" dla KONNEGO oddzialu przed PIESZYM pociagiem to nie
    /// hazard, tylko formalnosc. Vanilla kaze poswiecac ludzi i czesc taboru
    /// wedle modelu ofiar - a samotny jezdziec bywa wrecz ZABLOKOWANY (nie ma
    /// kogo poswiecic) i menu wpycha go z powrotem w bitwe 8 na 1. Realizm:
    /// gdy caly oddzial gracza jest konny, poscig nie ma ANI JEDNEGO konia,
    /// nie ma oblezenia ani morza - odjazd jest gwarantowany, DARMOWY
    /// (zero strat w ludziach, zero taboru) i konczy sie kilkugodzinna blokada
    /// tej bandy, zeby nie wchodzila w gracza drugi raz za rogiem.
    /// </summary>
    internal static class MountedGetaway
    {
        /// <summary>Warunki czystego odskoku wobec WROGA z BIEZACEGO spotkania.</summary>
        internal static bool Conditions()
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.CleanBreakEnabled) return false;
                if (PlayerEncounter.Current == null) return false;
                var enc = PlayerEncounter.EncounteredParty;
                var mp = enc != null ? enc.MobileParty : null;
                if (mp == null) return false;                                  // osada/garnizon - nie nasz przypadek
                if (mp.BesiegedSettlement != null) return false;               // oblezenie - normalne zasady
                try { if (MobileParty.MainParty.IsCurrentlyAtSea) return false; } catch { }
                if (!CleanBreak.PartyAllMounted(MobileParty.MainParty)) return false;
                if (CleanBreak.PartyAnyMounted(mp)) return false;              // maja konie - gonia uczciwie
                return true;
            }
            catch { return false; }
        }

        /// <summary>Konny zawsze MOZE odjechac pieszym - opcja nigdy nie jest zablokowana.</summary>
        internal static void CanGetAwayPostfix(ref bool __result)
        {
            if (!__result && Conditions()) __result = true;
        }

        /// <summary>Nikt nie zostaje w tyle - konie niosa wszystkich.</summary>
        internal static void SacrificePostfix(ref int __result)
        {
            if (__result > 0 && Conditions()) __result = 0;
        }

        /// <summary>Tabor tez zostaje przy nas - piechota nie dogania jucznych koni.</summary>
        internal static bool SkipBaggageLoss()
        {
            return !Conditions();
        }

        /// <summary>Po udanym odjezdzie: blokada tej bandy + zdjecie dezorganizacji.</summary>
        internal static void AfterGetaway()
        {
            try
            {
                if (!Conditions()) return;
                var enc = PlayerEncounter.EncounteredParty;
                var mp = enc != null ? enc.MobileParty : null;
                if (mp != null) CleanBreak.RegisterBlock(mp);
                CleanBreak.MarkFled();
                try { MobileParty.MainParty.SetDisorganized(false); } catch { }
                Log.Player("You ride clear without losing a man or a bag - no man on foot catches a rider.");
            }
            catch (Exception e) { Log.Error("MountedGetaway.AfterGetaway", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                int done = 0;
                var baseT = typeof(TaleWorlds.CampaignSystem.ComponentInterfaces.TroopSacrificeModel);
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (System.Reflection.ReflectionTypeLoadException r) { types = r.Types; }
                    catch { continue; }
                    foreach (var t in types)
                    {
                        if (t == null || t.IsAbstract || !baseT.IsAssignableFrom(t)) continue;
                        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly;
                        var m1 = t.GetMethod("CanPlayerGetAwayFromEncounter", flags);
                        if (m1 != null && !m1.IsAbstract)
                        { h.Patch(m1, postfix: new HarmonyMethod(typeof(MountedGetaway), "CanGetAwayPostfix")); done++; }
                        var m2 = t.GetMethod("GetNumberOfTroopsSacrificedForTryingToGetAway", flags);
                        if (m2 != null && !m2.IsAbstract)
                        { h.Patch(m2, postfix: new HarmonyMethod(typeof(MountedGetaway), "SacrificePostfix")); done++; }
                    }
                }
                var tMenu = typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior);
                var mi = AccessTools.Method(tMenu, "CalculateAndRemoveItemsForTryToGetAway");
                if (mi != null) { h.Patch(mi, prefix: new HarmonyMethod(typeof(MountedGetaway), "SkipBaggageLoss")); done++; }
                var ma = AccessTools.Method(tMenu, "game_menu_encounter_leave_your_soldiers_behind_accept_on_consequence");
                if (ma != null) { h.Patch(ma, prefix: new HarmonyMethod(typeof(MountedGetaway), "AfterGetaway")); done++; }
                Log.Info("MountedGetaway: konny odjazd przed piechota gwarantowany i darmowy (" + done + " latek).");
            }
            catch (Exception e) { Log.Error("MountedGetaway.ApplyAll", e); }
        }
    }

    /// <summary>W oknie czystego odskoku ta sama piesza banda nie wszczyna starcia z graczem.</summary>
    [HarmonyPatch(typeof(EncounterManager), "StartPartyEncounter")]
    internal static class CleanBreakEncounterPatch
    {
        private static bool Prefix(PartyBase attackerParty, PartyBase defenderParty)
        {
            return !CleanBreak.ShouldBlock(attackerParty, defenderParty);
        }
    }
}
