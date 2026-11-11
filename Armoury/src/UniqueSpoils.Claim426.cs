using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 426a (Jeff 11.11: "w lupach nawet jak lorda zabije wojownik mojej armii to takie rzeczy jak bronie z valyrianskiej stali czy inne unikaty
    /// ida do mnie"). Druga droga NIE powstaje - zmienia sie tylko ODBIORCA istniejacego zwyczaju wojennego (UniqueSpoils.Take: slot ofiary dostaje
    /// zamiennik, sztuka idzie do taboru gracza; rejestr stali valyrianskiej bez zmian, drugi Take na tym samym bohaterze nic nie znajdzie).
    ///  1. Pojmanie przez sojusznika w bitwie gracza (gra losuje odbiorce jenca wedlug wkladu - MapEvent.CaptureDefeatedPartyMembers): jeniec zostaje
    ///     u sojusznika, unikat idzie do gracza (ClaimLive w OnPrisonerTaken).
    ///  2. Polegly ze strony pokonanej: gra zabija poleglych dopiero w MapEventSide.HandleMapEventEndForPartyInternal z "zabojca"
    ///     OtherSide.LeaderParty.LeaderHero (bywa sojusznik AI albo null - wtedy Take w ogole nie biegl). MapEventEnded idzie PRZED tym krokiem
    ///     (MapEvent.FinalizeEvent), wiec tu robimy migawke bohaterow strony pokonanej; OnHeroKilled pyta o nia (Claimed).
    ///  3. Wypuszczony w rozmowie po bitwie ("You are free to go" - LordConversationsCampaignBehavior.
    ///     conversation_talk_lord_defeat_to_lord_release_on_consequence, bez zdarzenia pojmania): postfiks Harmony, klucz UniqueSpoilsOnRelease.
    ///  4. Egzekucja reka gracza: Take przed Inherit (w UniqueSpoils.OnHeroKilled).
    /// "Moja bitwa" (PlayerClaims) wedlug klucza UniqueSpoilsClaim: 0 - nigdy (stan sprzed 426), 1 - gracz dowodzi (na jego stronie nie ma wodza cudzej
    /// armii), 2 - kazda wygrana bitwa. Zaciezny ROT i gracz-jeniec - nigdy. Bez stanu w zapisie: migawka to pamiec sesji (Reset, kazda bitwa gracza).
    /// </summary>
    internal static partial class UniqueSpoils
    {
        private static Harmony _harmony;
        private static bool _relTried, _relWired;
        private static HashSet<string> _claimIds;          // StringId POLEGLYCH (znak smierci DiedInBattle) strony pokonanej w ostatniej wygranej bitwie gracza z roszczeniem
        private static HashSet<MobileParty> _claimParties; // 426-p: partie strony pokonanej tej bitwy (tabor rozbitej partii - OnPartyDestroyed)
        private static double _claimAt;                    // godzina gry migawki
        private static int _cAlly, _cFallen, _cRelease, _cExec, _cGiant, _cGiantDead, _cGiantBag, _cBaggage, _cStumble;   // liczniki doby (linia "Unikaty (177)")

        internal static int ClaimMode { get { var s = Settings.Current; return s == null ? 0 : Math.Max(0, Math.Min(2, s.UniqueSpoilsClaim)); } }

        private static void ClaimReset() { _claimIds = null; _claimParties = null; _claimAt = 0; ClaimClearDay(); _cStumble = 0; UniqueGold.Reset(); }
        private static void ClaimClearDay() { _cAlly = _cFallen = _cRelease = _cExec = _cGiant = _cGiantDead = _cGiantBag = _cBaggage = 0; }
        private static int ClaimDaySum() { return _cAlly + _cFallen + _cRelease + _cExec + _cGiant + _cGiantDead + _cGiantBag + _cBaggage; }
        private static string ClaimDayText()
        {
            return "; do gracza (426): po jencach sojusznikow " + _cAlly + ", po poleglych w bitwie gracza " + _cFallen + ", po wypuszczonych " + _cRelease
                   + ", po straconych " + _cExec + ", z taborow partii rozbitych w bitwie gracza " + _cBaggage
                   + "; sprzet olbrzyma: zostawiony pojmanemu olbrzymowi " + _cGiant + ", przepadl z poleglym olbrzymem (ujscie) " + _cGiantDead
                   + ", po poleglym olbrzymie do taboru gracza z olbrzymami " + _cGiantBag + ", potkniecia 426 " + _cStumble;
        }

        /// <summary>426-p: sprzet olbrzyma w Take, gdy odbiorca nie jest olbrzymem (424). 0 = sztuka zostaje w slocie (pojmany olbrzym ja trzyma; polegly -
        /// przepada z nim, liczone jako ujscie - dawniej liczone razem z "zostawionymi"), 1 = idzie zwykla droga do taboru gracza: tylko polegly olbrzym
        /// i tylko gdy gracz prowadzi olbrzymow (jest komu ja dac - ta sama regula co sakwy gracza w 424; inaczej sakwy by ja skasowaly przy menu).</summary>
        private static int GiantPiece(bool death, bool toPlayer)
        {
            if (!death) { _cGiant++; return 0; }
            bool keep = false;
            try { keep = toPlayer && GiantGear.PartyHasGiants(MobileParty.MainParty); } catch { keep = false; }
            if (!keep) { _cGiantDead++; return 0; }
            _cGiantBag++;
            return 1;
        }

        /// <summary>W kampanii (OnSessionLaunched): latka rozmowy raz na proces (cel zadeklarowany w LordConversationsCampaignBehavior - DeclaredMethod)
        /// i linia startu.</summary>
        private static void ClaimSessionLaunched()
        {
            if (!_relTried && _harmony != null)
            {
                _relTried = true;
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(LordConversationsCampaignBehavior), "conversation_talk_lord_defeat_to_lord_release_on_consequence");
                    if (m != null)
                    {
                        _harmony.Patch(m, postfix: new HarmonyMethod(typeof(UniqueSpoils), nameof(ReleasePostfix)));
                        _relWired = true;
                    }
                }
                catch (Exception e) { Log.Error("UniqueSpoils.ClaimSessionLaunched(latka rozmowy)", e); }
            }
            var s = Settings.Current;
            int mode = ClaimMode;
            Log.Info("Unikaty (426): zwyczaj wojenny - tryb " + mode
                     + (mode == 0 ? " (tylko wlasna druzyna gracza - jak przed 426)" : mode == 1 ? " (wygrana bitwa gracza bez wodza cudzej armii po jego stronie; jency partii WLASNEJ armii gracza zawsze)" : " (kazda wygrana bitwa gracza, takze w cudzej armii)")
                     + ", wypuszczony oddaje " + (s != null && s.UniqueSpoilsOnRelease ? "TAK" : "NIE") + ", latka rozmowy " + (_relWired ? "wpieta" : "BRAK")
                     + ", sprzet olbrzyma poza zdobycza " + (GiantGear.On ? "TAK" : "NIE") + ".");
        }

        /// <summary>Czy unikaty pokonanych w tej bitwie naleza sie graczowi. why - powod do logu.</summary>
        private static bool PlayerClaims(MapEvent me, out string why)
        {
            why = "";
            int mode = ClaimMode;
            if (mode <= 0) { why = "tryb 0"; return false; }
            var main = MobileParty.MainParty;
            if (me == null || main == null || main.MapEvent != me) { why = "gracz poza bitwa"; return false; }
            if (Hero.MainHero == null || Hero.MainHero.IsPrisoner) { why = "gracz jencem"; return false; }
            if (BattlefieldLaw.RotEnlisted()) { why = "zaciezny - lup zostaje przy armii"; return false; }
            if (mode >= 2) { why = "tryb 2"; return true; }
            if (main.Army != null && main.Army.LeaderParty != main) { why = "gracz w cudzej armii (" + main.Army.LeaderParty.Name + ")"; return false; }
            var side = PartyBase.MainParty.Side;
            if (side == BattleSideEnum.None) { why = "strona gracza nieznana"; return false; }
            foreach (var p in me.PartiesOnSide(side))
            {
                var mp = p != null && p.Party != null ? p.Party.MobileParty : null;
                if (mp == null || mp == main || mp.Army == null) continue;
                if (mp.Army.LeaderParty == mp) { why = "po stronie gracza dowodzi wodz armii " + mp.Name; return false; }
            }
            why = "po stronie gracza nie ma wodza cudzej armii";
            return true;
        }

        /// <summary>Pojmanie w trakcie rozliczenia bitwy: partia pojmujaca stoi w bitwie gracza po jego stronie i gracz ma roszczenie.</summary>
        private static bool ClaimLive(PartyBase capturer)
        {
            try
            {
                if (capturer == null || ClaimMode <= 0 || PartyBase.MainParty == null) return false;
                var me = capturer.MapEvent;
                if (me == null || me != MapEvent.PlayerMapEvent) return false;
                var side = PartyBase.MainParty.Side;
                if (side == BattleSideEnum.None || capturer.Side != side) return false;
                // 426-p (recenzja, sredni 2): partia z armii, ktora prowadzi GRACZ, pojmuje dla gracza takze wtedy, gdy po jego stronie stoi druga armia AI
                // (PlayerClaims odmawia calej stronie z powodu wodza tamtej armii - a to wprost "wojownik mojej armii" z polecenia Jeffa)
                var cmp = capturer.MobileParty; var main = MobileParty.MainParty;
                if (cmp != null && main != null && main.Army != null && main.Army.LeaderParty == main && cmp.Army == main.Army
                    && Hero.MainHero != null && !Hero.MainHero.IsPrisoner && !BattlefieldLaw.RotEnlisted()) return true;
                string why;
                return PlayerClaims(me, out why);
            }
            catch (Exception e) { if (_cStumble++ < 3) Log.Error("UniqueSpoils.ClaimLive", e); return false; }
        }

        /// <summary>Koniec bitwy (CampaignEvents.MapEventEnded - przed zabiciem poleglych): migawka bohaterow strony pokonanej. Tylko odczyt.</summary>
        internal static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                // 426-p: koniec kazdej INNEJ bitwy gasi migawke - gra zabija poleglych i rozbija partie w tym samym kroku co koniec bitwy gracza
                // (MapEvent.FinalizeEvent: MapEventEnded -> HandleMapEventEnd), wiec po nim migawka nie ma juz czego obslugiwac
                if (me == null) return;
                _claimIds = null; _claimParties = null;
                if (!me.IsPlayerMapEvent) return;
                if (PartyBase.MainParty == null) return;
                var side = PartyBase.MainParty.Side;
                if (side == BattleSideEnum.None || !me.HasWinner || me.WinningSide != side) return;
                var other = side == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker;
                var ids = new HashSet<string>();
                var parties = new HashSet<MobileParty>();
                int heroes = 0;
                foreach (var p in me.PartiesOnSide(other))
                {
                    var pb = p != null ? p.Party : null;
                    if (pb == null) continue;
                    if (pb.MobileParty != null) parties.Add(pb.MobileParty);
                    var roster = pb.MemberRoster;
                    if (roster == null) continue;
                    for (int i = 0; i < roster.Count; i++)
                    {
                        var co = roster.GetCharacterAtIndex(i);
                        if (co == null || !co.IsHero || co.HeroObject == null || co.HeroObject.StringId == null) continue;
                        heroes++;
                        // 426-p: tylko ze znakiem smierci - dokladnie tych gra zabije za chwile (MapEventSide.HandleMapEventEndForPartyInternal bierze
                        // z rostera bohaterow z DeathMark == DiedInBattle); zbieg z tej bitwy nie jest w migawce
                        if (co.HeroObject.DeathMark == TaleWorlds.CampaignSystem.Actions.KillCharacterAction.KillCharacterActionDetail.DiedInBattle) ids.Add(co.HeroObject.StringId);
                    }
                }
                if (heroes == 0 && parties.Count == 0) return;
                string why;
                bool claim = PlayerClaims(me, out why);
                if (claim) { _claimIds = ids; _claimParties = parties; _claimAt = CampaignTime.Now.ToHours; }
                if (heroes > 0)   // banda bez bohaterow - bez linii
                    Log.Info("Unikaty (426): bitwa gracza - roszczenie " + (claim ? "TAK" : "NIE") + " (" + why + "), bohaterow strony pokonanej " + heroes
                             + " (poleglych " + ids.Count + "), partii " + parties.Count + ".");
            }
            catch (Exception e) { _claimIds = null; _claimParties = null; if (_cStumble++ < 3) Log.Error("UniqueSpoils.OnMapEventEnded", e); }
        }

        /// <summary>Polegly z migawki ostatniej wygranej bitwy gracza (nie starszej niz godzina gry - gra zabija poleglych w tym samym kroku).</summary>
        private static bool Claimed(Hero victim)
        {
            if (_claimIds == null || victim == null || victim == Hero.MainHero || victim.StringId == null) return false;
            double age = CampaignTime.Now.ToHours - _claimAt;
            if (age < 0 || age > 1.0) { _claimIds = null; _claimParties = null; return false; }
            return _claimIds.Remove(victim.StringId);   // 426-p: migawka zuzywana - jeden polegly, jedno roszczenie
        }

        /// <summary>426-p (recenzja, sredni 1): partia strony pokonanej z ostatniej wygranej bitwy gracza z roszczeniem (migawka nie starsza niz godzina gry).
        /// Gra rozbija ja przez DestroyPartyAction.Apply(GetLeaderParty(strona zwyciezcow), ...) - "niszczycielem" bywa sojusznik AI, ktory zaczal bitwe,
        /// i unikat z TABORU pokonanego lorda szedl do jego taboru, choc unikat z biodra tego samego lorda szedl do gracza.</summary>
        private static bool ClaimedParty(MobileParty mp)
        {
            if (_claimParties == null || mp == null) return false;
            double age = CampaignTime.Now.ToHours - _claimAt;
            if (age < 0 || age > 1.0) { _claimIds = null; _claimParties = null; return false; }
            return _claimParties.Remove(mp);
        }

        /// <summary>Postfiks rozmowy po bitwie: lord puszczony wolno zostawia unikaty graczowi (dostaje zamiennik - StandIn). Rozmowa dotyczy tylko
        /// bohaterow z doli gracza (PlayerEncounter.DoCaptureHeroes), wiec to odpowiednik pojmania przez wlasna druzyne - bez pytania o PlayerClaims.</summary>
        public static void ReleasePostfix()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.UniqueSpoilsOnRelease || Campaign.Current == null) return;
                var h = Hero.OneToOneConversationHero;
                if (h == null || h == Hero.MainHero || Hero.MainHero == null || Hero.MainHero.IsPrisoner) return;
                if (BattlefieldLaw.RotEnlisted()) return;
                int n = Take(h, Hero.MainHero, true, "wypuszczony po bitwie", false, 1);
                if (n > 0) _cRelease += n;
            }
            catch (Exception e) { if (_cStumble++ < 3) Log.Error("UniqueSpoils.ReleasePostfix (potkniecie " + _cStumble + ")", e); }
        }
    }
}
