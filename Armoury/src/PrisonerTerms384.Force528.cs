using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 528 (Jeff 14.11 "wynegocjowalem ze przez 60 dni nie bedzie najezdzal ziemi za uwolnienie zakladnika ale: Ja mam 40 ludzi on 200 dopadl mnie nie musi
    /// negocjowac pokona mnie i zabierze zakladnikow, nie ma sensu aby sie zgadzac w takiej sytuacji"). Dowod: Armoury-2026-11-14_19-28-01.log 22:46:20
    /// Kevan Lannister (228 ludzi) przyjal pakt w polu za samego zakladnika (V 2,818 / C 1,000 -> ZGODA), a 4 s pozniej sam uderzyl na gracza (ok. 40 ludzi)
    /// - "umowa PRZEPADLA ... (atak na gracza)".
    /// (1) SILA W OCENIE (klucz PrisonerTermsForceRatio, 2.0; 0 = wylaczone): tylko rozmowa W POLU twarza w twarz (nie oblezenie - tam sile liczy juz PTake,
    ///   nie poslaniec - wodz stoi daleko), zadania A (pakt) i M (moi ludzie); T (pokoj krolestw) nie - to nie sprawa jednej bitwy.
    ///   Sila jak reszta Armoury: EstimatedStrength (z mnoznikiem uzbrojenia ArmedStrength) - partia wodza albo jego armia (gdy wodz ja prowadzi albo stoi w jej
    ///   szyku), druzyna gracza albo jego armia. R = sila wodza / sila gracza. K = clamp((R - 1) / (X - 1) x sklonnosc, 0, 1), sklonnosc = 1 + 0.25 Valor
    ///   - 0.25 Mercy (0.5..1.5). R <= 1 (slabszy albo rowny) - K = 0, ocena jak dotad. X = 2: gra sama przestaje nagradzac przewage ponad 2 przy wyborze celu
    ///   (DefaultTargetScoreCalculatingModel - opis BesiegedRelief), a przy 2:1 autobitwa konczy sie pewnym zwyciestwem silniejszego.
    ///   Skutek: (a) to, co wodz moze WZIAC SILA - zakladnik (stoi w szeregu druzyny gracza) i jency-lordowie w druzynie gracza (nie w lochach) - traci K swojej
    ///   wartosci (V); zloto, ziemia, jency z lochow i przysiega bez zmian; (b) pakt A kosztuje go zwyciestwo, z ktorego rezygnuje: C += K x (okup gracza +
    ///   0.3 x wartosc wojskowa druzyny gracza) - ta sama miara co swobodne przejscie P pod murami. Odmowa z powodem (Why 'F') przy K >= 0.5 - linia wodza po
    ///   angielsku, kontroferta tez mowi o sile. Calculating dalej tylko w progu (jak dotad), Honor dalej tylko w mnozniku V.
    /// (2) OBRONCY DOTRZYMUJA SLOWA (klucz PrisonerTermsDefendersKeepWord): MobilePartyAi.GetDefendSettlementBehavior (DefaultBehavior DefendSettlement) wysyla
    ///   EngageParty na Settlement.LastAttackerParty BEZ pytania ShouldConsiderAttacking, wiec bez DoNotAttackMainPartyUntil (gra czyta to pole tylko tam i w
    ///   rozmowie). Kevan mial "AI DefendSettlement -> Duskendale, krotko EngageParty" - zgoda dala mu SetDoNotAttackMainParty(3), a obrona osady i tak poslala
    ///   go na gracza. Postfiks: gdy wynik to EngageParty na druzyne gracza, a partia ma DoNotAttackMainPartyUntil w przyszlosci - zachowanie jak przy braku
    ///   napastnika (GoToPoint do bramy / portu, cel null - ta sama galaz gry). Najazd i oblezenie osady (MapEvent / SiegeEvent) ida inna galezia - bez zmian.
    ///   Do tego: przy zgodzie partia rozmowcy scigajaca gracza dostaje ForceDefaultBehaviorUpdate (nowa mysl w najblizszym takcie, nie po 0.25 h), a w takcie
    ///   kampanii, dopoki umowa z pola czeka w kolejce, SetDoNotAttackMainParty(3) odswiezane (dotad 3 h od zgody bez odswiezania).
    /// Watki AI: postfiks czyta tylko pola partii i klucz; liczniki przez Interlocked. Potkniecia liczone, nic sie nie gasi.
    /// </summary>
    internal static partial class PrisonerTerms384
    {
        private static double ForceX { get { var s = Settings.Current; return s != null ? s.PrisonerTermsForceRatio : 2.0; } }
        private static bool KKeep { get { var s = Settings.Current; return s != null && s.PrisonerTermsDefendersKeepWord; } }

        private static string _defHook = "nie wpinana";
        private static AccessTools.FieldRef<MobilePartyAi, MobileParty> _defParty;
        private static MethodInfo _mRethink, _mUntilSet;
        private static int _fDef, _fDefStumbles, _fRethink, _fRefresh, _fBroken;
        private const double FieldWordDays = 1.0;   // Recenzja 528: slowo z pola odswiezane najwyzej dobe od zgody (gracz w miescie nie trzyma wodza w nietykalnosci w nieskonczonosc)

        /// <summary>528: stosunek sil wodza do gracza w polu twarza w twarz; wypelnia e.K, e.R, e.Es, e.Ps, e.MenE, e.MenP, e.Incl. Bez rzutow.</summary>
        private static void Force(Hero lord, Ev e, int valor, int mercy)
        {
            try
            {
                double x = ForceX;
                if (x <= 0 || lord == null) return;
                x = Math.Max(1.1, x);
                var lp = lord.PartyBelongedTo;
                var me = MobileParty.MainParty;
                if (lp == null || me == null || !lp.IsActive || lp.Party == null || me.Party == null) return;
                var la = lp.Army != null && (lp.Army.LeaderParty == lp || lp.AttachedTo != null) ? lp.Army : null;   // armia tylko, gdy stoi w jej szyku
                var ma = me.Army != null && (me.Army.LeaderParty == me || me.AttachedTo != null) ? me.Army : null;
                e.Es = la != null ? la.EstimatedStrength : lp.Party.EstimatedStrength;
                e.Ps = ma != null ? ma.EstimatedStrength : me.Party.EstimatedStrength;
                e.MenE = la != null ? la.TotalHealthyMembers : lp.Party.NumberOfHealthyMembers;
                e.MenP = ma != null ? ma.TotalHealthyMembers : me.Party.NumberOfHealthyMembers;
                e.R = e.Ps > 0.01 ? e.Es / e.Ps : 99;
                e.Incl = Math.Max(0.5, Math.Min(1.5, 1.0 + 0.25 * valor - 0.25 * mercy));
                e.X = x;
                e.K = Math.Max(0, Math.Min(1.0, (e.R - 1.0) / (x - 1.0) * e.Incl));
                e.FOn = true;
            }
            catch (Exception ex) { Stumble("Force", ex); e.K = 0; }
        }

        /// <summary>528: czesc linii logu oceny - sila obu stron i mnoznik.</summary>
        private static string ForceParts(Ev e)
        {
            if (!e.FOn) return "";
            return " | sila (528): wodz " + N(e.Es) + " (" + e.MenE + " ludzi) vs gracz " + N(e.Ps) + " (" + e.MenP + " ludzi) -> R " + e.R.ToString("0.00", Inv)
                   + ", prog X " + e.X.ToString("0.0", Inv) + ", sklonnosc " + e.Incl.ToString("0.00", Inv) + " -> K " + e.K.ToString("0.00", Inv)
                   + " | do wziecia sila odjete z V " + N(e.Grab) + (e.Prize > 0 ? ", wygrana nad graczem dodana do C " + N(e.Prize) : "");
        }

        /// <summary>528: jeniec w szeregu jencow druzyny gracza - do wziecia sila.</summary>
        private static bool Grabbable(Hero h)
        {
            try { return h != null && h.IsPrisoner && h.PartyBelongedToAsPrisoner == PartyBase.MainParty; } catch { return false; }
        }

        /// <summary>528: partia, ktora naprawde prowadzi druzyne wodza - on sam albo wodz armii, w ktorej szyku stoi (partia w szyku nie mysli sama -
        /// MobilePartyAi.TickInternal pomija dolaczonych; Recenzja 528).</summary>
        private static MobileParty Mover(MobileParty lp)
        {
            if (lp == null) return null;
            if (lp.AttachedTo != null && lp.Army != null && lp.Army.LeaderParty != null && lp.Army.LeaderParty != MobileParty.MainParty) return lp.Army.LeaderParty;
            return lp;
        }

        /// <summary>528: slowo z pola - partia wodza (i wodz jego armii, gdy stoi w szyku) nie atakuje gracza przez h godzin.</summary>
        private static void Bind(MobileParty lp, int hours)
        {
            if (lp == null) return;
            if (lp.Ai != null) lp.Ai.SetDoNotAttackMainParty(hours);
            var m = Mover(lp);
            if (m != lp && m != null && m.Ai != null) m.Ai.SetDoNotAttackMainParty(hours);
        }

        /// <summary>528: przy zgodzie w polu - partia rozmowcy scigajaca gracza mysli od nowa w najblizszym takcie (MobilePartyAi.ForceDefaultBehaviorUpdate, internal).
        /// Recenzja 528: dlugi rozkaz EngageParty na gracza (DefaultBehavior) mysl odtworzylaby bez pytania o slowo - jak ClearOrders paktu: Hold + przemyslenie.</summary>
        private static void Rethink(MobileParty lp)
        {
            try
            {
                lp = Mover(lp);
                if (lp == null || lp.Ai == null || !KKeep || lp.MapEvent != null) return;
                if (lp.DefaultBehavior == AiBehavior.EngageParty && lp.TargetParty == MobileParty.MainParty) { lp.SetMoveModeHold(); lp.Ai.RethinkAtNextHourlyTick = true; _fRethink++; return; }
                if (lp.ShortTermBehavior != AiBehavior.EngageParty || lp.ShortTermTargetParty != MobileParty.MainParty) return;
                if (_mRethink == null) _mRethink = AccessTools.DeclaredMethod(typeof(MobilePartyAi), "ForceDefaultBehaviorUpdate");
                if (_mRethink == null) return;
                _mRethink.Invoke(lp.Ai, null);
                _fRethink++;
            }
            catch (Exception e) { Stumble("Rethink", e); }
        }

        /// <summary>Recenzja 528: gracz pierwszy dobyl miecza na frakcje rozmowcy (FieldFight, atak gracza) - slowo wodza zdjete od razu, nie po 3 h
        /// (DoNotAttackMainPartyUntil = teraz; setter internal - odbiciem). Takze wodz armii, w ktorej szyku stoi.</summary>
        private static void Unbind(Hero lord)
        {
            try
            {
                var lp = lord != null ? lord.PartyBelongedTo : null;
                if (lp == null) return;
                if (_mUntilSet == null) _mUntilSet = AccessTools.PropertySetter(typeof(MobilePartyAi), "DoNotAttackMainPartyUntil");
                if (_mUntilSet == null) return;
                var now = new object[] { CampaignTime.Now };
                if (lp.Ai != null && lp.Ai.DoNotAttackMainPartyUntil.IsFuture) { _mUntilSet.Invoke(lp.Ai, now); _fBroken++; }
                var m = Mover(lp);
                if (m != lp && m != null && m.Ai != null && m.Ai.DoNotAttackMainPartyUntil.IsFuture) _mUntilSet.Invoke(m.Ai, now);
            }
            catch (Exception e) { Stumble("Unbind", e); }
        }

        /// <summary>528: takt kampanii - dopoki umowa z pola (nie przez poslanca) czeka w kolejce, partia rozmowcy nie atakuje gracza (3 h, odswiezane;
        /// Recenzja 528: najwyzej FieldWordDays od zgody, takze wodz armii, w ktorej szyku stoi; przy odswiezeniu nowa mysl, gdy nadal sciga gracza).</summary>
        private static void KeepFieldWord()
        {
            try
            {
                if (Campaign.Current == null) return;
                double today = Today;
                for (int i = 0; i < _queue.Count; i++)
                {
                    var d = _queue[i];
                    if (!d.Field || d.Envoy || today - d.Day > FieldWordDays) continue;
                    var h = Hero.Find(d.Lord);
                    var lp = h != null ? h.PartyBelongedTo : null;
                    if (lp == null || lp.Ai == null || !lp.IsActive) continue;
                    var m = Mover(lp);
                    if (lp.Ai.DoNotAttackMainPartyUntil.RemainingHoursFromNow < 2f || (m != null && m.Ai != null && m.Ai.DoNotAttackMainPartyUntil.RemainingHoursFromNow < 2f))
                    {
                        Bind(lp, 3); _fRefresh++;
                        Rethink(lp);
                    }
                }
            }
            catch (Exception e) { Stumble("KeepFieldWord", e); }
        }

        /// <summary>Z ApplyPact (menu glowne, raz na proces): postfiks prywatnej MobilePartyAi.GetDefendSettlementBehavior (zadeklarowana w tej klasie - DeclaredMethod).</summary>
        private static void ApplyForce528(Harmony h)
        {
            try
            {
                _defParty = AccessTools.FieldRefAccess<MobilePartyAi, MobileParty>("_mobileParty");
                var m = AccessTools.DeclaredMethod(typeof(MobilePartyAi), "GetDefendSettlementBehavior");
                if (m != null && _defParty != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(PrisonerTerms384), nameof(DefendKeepWord))); _defHook = "wpieta"; }
                else _defHook = m == null ? "BRAK GetDefendSettlementBehavior" : "BRAK _mobileParty";
            }
            catch (Exception e) { _defHook = "blad " + e.GetType().Name; Log.Error("PrisonerTerms384.ApplyForce528", e); }
            try { Log.Info(Tag + ": latka obroncow (528) - MobilePartyAi.GetDefendSettlementBehavior: " + _defHook + " (obronca osady z DoNotAttackMainPartyUntil w przyszlosci nie dostaje EngageParty na druzyne gracza; klucz Prisoner Terms Defenders Keep Word)."); } catch { }
        }

        /// <summary>Postfiks GetDefendSettlementBehavior (watki AI): EngageParty na druzyne gracza mimo DoNotAttackMainPartyUntil -> galaz gry "bez napastnika"
        /// (GoToPoint do bramy, przy partii na morzu i porcie - do portu; cel null). Tylko pola partii. Nie rzuca.</summary>
        public static void DefendKeepWord(MobilePartyAi __instance, Settlement __0, ref AiBehavior __1, ref CampaignVec2 __2, ref MobileParty __3)
        {
            try
            {
                if (__1 != AiBehavior.EngageParty || __3 == null || __3 != MobileParty.MainParty || __0 == null || !KKeep) return;
                // Recenzja 528: oblezenie albo bitwa / najazd na te osade (galaz SiegeEvent / MapEvent tez daje EngageParty na LastAttackerParty) oraz gracz w bitwie,
                // najezdzie albo oblezeniu (galaz "IsRaid z druzyna gracza") - to gracz lamie spokoj; obroncy ida na niego jak w grze
                if (__0.SiegeEvent != null || (__0.Party != null && __0.Party.MapEvent != null) || __3.MapEvent != null || __3.SiegeEvent != null) return;
                var mp = _defParty != null ? _defParty(__instance) : null;
                if (mp == null || mp.Ai == null || !mp.Ai.DoNotAttackMainPartyUntil.IsFuture) return;
                __1 = AiBehavior.GoToPoint;
                __2 = mp.IsCurrentlyAtSea && __0.HasPort ? __0.PortPosition : __0.GatePosition;
                __3 = null;
                Interlocked.Increment(ref _fDef);
            }
            catch { Interlocked.Increment(ref _fDefStumbles); }
        }

        /// <summary>528: czesc linii doby.</summary>
        private static string ForceLine()
        {
            return " | 528 sila w polu (prog X " + ForceX.ToString("0.0", Inv) + ", 0 = wyl.), obroncy (klucz " + (KKeep ? "TAK" : "NIE") + ", latka " + _defHook + "): ataki obrony osady na gracza zdjete "
                   + _fDef + " (potkniecia watkow " + _fDefStumbles + "), nowa mysl przy zgodzie " + _fRethink + ", odswiezenia slowa w kolejce " + _fRefresh + ", slowo zdjete (gracz zaatakowal) " + _fBroken;
        }
    }
}
