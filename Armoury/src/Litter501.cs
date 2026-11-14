using System;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 501 NOSZE (Jeff 14.11: "jak mam malo zdrowia ponizej np 10hp to jestem w zasadzie bliski smierci wiec leze i nie ma mowy o jezdzie dopoki sie
    /// nie wydobrzeje"; po pytaniu nosze czy postoj: "tak zrob nosze"). Tylko GRACZ (AI bez zmian - HeroWounds.Ai wiezie rannego lorda do twierdzy).
    ///
    /// Stan "na noszach": Hero.MainHero.HitPoints <= LitterHpThreshold (10) ALBO loze HeroWounds stopnia Grave (HeroWounds.GradeOf == Grave).
    /// Wyjscie z histereza: HP > LitterHpRelease (15) i brak loza Grave (HP waha sie wokol progu - bez migania komunikatow).
    /// Stan liczony na watku glownym: tik godzinowy, koniec bitwy gracza (OnPlayerBattleEnd - HP po misji) i odczyt z RC (HorseFlight / MountedGetaway,
    /// menu spotkania). NIGDY co klatke; nic nie rusza wizerunku partii na mapie ani menu (pulapki CLAUDE.md: SwitchToMenu, RemoveAllChildren).
    ///
    /// Skutki:
    ///  (a) predkosc partii gracza x LitterSpeedPercent (50), a gdy chirurg partii (rola Surgeon, bez chirurga przywodca - EffectiveSurgeon, jak perk
    ///      w modelu gry) ma Medicine.Sledges - x LitterSpeedPercentSledges (75). Gra: Sledges "-50% party speed penalty from the wounded"
    ///      (DefaultPartySpeedCalculatingModel.GetWoundedModifier) - ta sama regula, polowa kary noszy (50% -> 25% ciecia). Kara gry za rannych dziala
    ///      dopiero przy > 1/4 skladu rannych i liczy tylko szeregowych - wodz na noszach sie z nia nie dubluje. Wpis "Carrying the wounded lord on
    ///      a litter" w rozpisce predkosci. Wolane z NightRest.SpeedPostfix (Priority.Last, PO MarchPace), na KONCU (po premii karczmy / karze snu),
    ///      raz na lancuch modeli (SpeedDepth.OutermostFinal), wzorem CampCart.Speed: procent WYNIKU przez Add (dzielone przez 1 + suma czynnikow),
    ///      a gdy wynik trzyma sam sufit LimitMax (kolumna MarchPace - recenzja 16.10 TownInns: Add ginal pod sufitem) - sufit obnizony do celu.
    ///      Na morzu i w przejsciu lad/morze bez kary (wodz lezy na pokladzie, okret idzie swoim tempem).
    ///  (b) komunikat (po angielsku) tylko przy zmianie stanu.
    ///  (c) RealisticCaptivity: HorseFlight (konny odjazd ze spotkania) i MountedGetaway (CleanBreak) pytaja PlayerCarried() odbiciem (ArmouryBridge) -
    ///      lezacy gracz nie odjezdza galopem; zostaja zasady gry (straz tylna, tabor).
    ///  (d) gracz doczepiony do CUDZEJ armii - nosze nie spowalniaja armii (niesiony w taborze przywodcy): bez kary, tylko godziny w linii doby.
    ///      Gracz PROWADZACY armie - kara na jego partii (armia idzie tempem wodza).
    ///  (e) DLL autotestu / profilu (LogGate.Mode 1/2) - bez noszy, ten sam wyjatek co loze gracza (HeroWounds.cs:436); bitwa z Nocnym Krolem zdejmuje
    ///      loze i przywraca HP (HeroWounds.OnMapEventStarted) - koniec tej bitwy zdejmuje nosze (OnPlayerBattleEnd). Niewola / partia nieczynna -
    ///      stan zamrozony (bez komunikatow), liczony po uwolnieniu.
    ///  (f) linia doby w logu Armoury: godziny na noszach (w ruchu / postoj / w cudzej armii / morze), HP, predkosc przed / po, potkniecia.
    /// Bledy liczone (potkniecia), bez globalnego wylacznika.
    /// </summary>
    internal static class Litter501
    {
        // stan (watek glowny pisze, predkosc - rownolegle - tylko czyta)
        private static volatile bool _carried;
        private static volatile bool _sledges;
        private static volatile bool _onKey;

        // linia doby
        private static int _day = -1, _hMove, _hRest, _hArmy, _hSea, _hp0 = -1, _hpMin = -1, _ups, _downs, _stumbles;
        private static int _parStumbles;
        private static float _spdBefore, _spdAfter;   // ostatnie wywolanie z cieciem (rownolegle - zapis float atomowy, tylko log)
        private static string _why = "";

        private static TextObject _txt;   // tworzony leniwie (w kampanii), nie w konstruktorze statycznym
        private static TextObject Txt { get { return _txt ?? (_txt = new TextObject("{=!}Carrying the wounded lord on a litter")); } }

        internal static void Reset()
        {
            _carried = false; _sledges = false; _onKey = false;
            _day = -1; _hMove = 0; _hRest = 0; _hArmy = 0; _hSea = 0; _hp0 = -1; _hpMin = -1; _ups = 0; _downs = 0; _stumbles = 0; _parStumbles = 0;
            _spdBefore = 0f; _spdAfter = 0f; _why = "";
        }

        /// <summary>Gracz lezy na noszach (stan po ostatnim przeliczeniu). Dla RC (ArmouryBridge, odbiciem) - przelicza stan przed odpowiedzia
        /// (menu spotkania, watek glowny); komunikat tylko przy zmianie.</summary>
        internal static bool PlayerCarried()
        {
            try { Evaluate("spotkanie"); } catch (Exception e) { Stumble("PlayerCarried", e); }
            return _carried;
        }

        // ------------------------------------------------------------ stan
        /// <summary>Przeliczenie stanu (watek glowny). Zmiana stanu = komunikat i linia logu.</summary>
        internal static void Evaluate(string src)
        {
            var s = Settings.Current;
            var h = Hero.MainHero;
            var mp = MobileParty.MainParty;
            if (s == null || h == null || mp == null) return;
            bool key = s.LitterEnabled && LogGate.Mode != 1 && LogGate.Mode != 2;   // (e) autotest / profil - jak loze gracza (HeroWounds.cs:436)
            _onKey = key;
            if (!key)
            {
                if (_carried) { _carried = false; _downs++; Log.Info("Nosze (501): stan zdjety - klucz LitterEnabled wylaczony albo DLL autotestu/profilu (" + src + ")."); }
                return;
            }
            if (!h.IsAlive || h.IsPrisoner || !mp.IsActive) return;   // niewola / sluzba bez partii - stan zamrozony, po uwolnieniu liczony od nowa
            int hp = h.HitPoints;
            bool grave = HeroWounds.GradeOf(h) == HeroWounds.Grave;
            int on = Math.Max(1, s.LitterHpThreshold);
            int off = Math.Max(on, s.LitterHpRelease);
            bool now = _carried ? (hp <= off || grave) : (hp <= on || grave);
            try { _sledges = mp.HasPerk(DefaultPerks.Medicine.Sledges); } catch { _sledges = false; }
            if (now == _carried) return;
            _carried = now;
            if (now)
            {
                _ups++;
                _why = grave ? "rana Grave" : "HP " + hp;
                Log.Player("You are too badly wounded to ride - your men carry you on a litter.", true);
                Log.Info("Nosze (501): gracz na noszach (" + src + ") - HP " + hp + "/" + h.MaxHitPoints + (grave ? ", loze Grave (" + HeroWounds.DaysLeft(h) + " dni)" : "")
                         + " | predkosc x" + Pct(s) + "%" + (_sledges ? " (Sledges chirurga)" : "") + (InForeignArmy(mp) ? " | w cudzej armii - bez kary" : "")
                         + " | prog " + on + " / wyjscie > " + off + ".");
            }
            else
            {
                _downs++;
                Log.Player("You are well enough to ride again.");
                Log.Info("Nosze (501): gracz zszedl z noszy (" + src + ") - HP " + hp + "/" + h.MaxHitPoints + ", wyjscie > " + off + " i bez loza Grave.");
            }
        }

        private static int Pct(Settings s)
        {
            int p = _sledges ? s.LitterSpeedPercentSledges : s.LitterSpeedPercent;
            return Math.Max(10, Math.Min(100, p));
        }

        private static bool InForeignArmy(MobileParty mp)
        {
            var a = mp.Army;
            return a != null && a.LeaderParty != null && a.LeaderParty != mp && mp.AttachedTo != null;
        }

        /// <summary>CampaignEvents.OnPlayerBattleEndEvent - HP gracza po misji (i po leczeniu perkami bitwy).</summary>
        internal static void OnPlayerBattleEnd(TaleWorlds.CampaignSystem.MapEvents.MapEvent me)
        {
            try { Evaluate("koniec bitwy"); } catch (Exception e) { Stumble("OnPlayerBattleEnd", e); }
        }

        /// <summary>Tik godzinowy (ArmouryBehavior): przeliczenie stanu, godziny na noszach, raz na dobe linia logu.</summary>
        internal static void Hourly()
        {
            try
            {
                var s = Settings.Current;
                var mp = MobileParty.MainParty;
                var h = Hero.MainHero;
                if (s == null || mp == null || h == null) return;
                int day = (int)CampaignTime.Now.ToDays;
                if (_day < 0) { _day = day; _hp0 = h.HitPoints; _hpMin = h.HitPoints; }
                else if (day != _day)
                {
                    int hours = _hMove + _hRest + _hArmy + _hSea;
                    if (hours > 0 || _ups > 0 || _downs > 0 || _stumbles > 0 || _parStumbles > 0)
                        Log.Info("Nosze (501): doba " + _day + " - na noszach " + hours + " h (w ruchu " + _hMove + ", postoj/osada " + _hRest + ", w cudzej armii bez kary " + _hArmy
                                 + ", morze bez kary " + _hSea + ") | wejscia " + _ups + ", wyjscia " + _downs + (_why.Length > 0 ? " (ostatnie: " + _why + ")" : "")
                                 + " | HP " + _hp0 + " -> " + h.HitPoints + "/" + h.MaxHitPoints + ", najnizej " + _hpMin
                                 + " | predkosc " + _spdBefore.ToString("0.00") + " -> " + _spdAfter.ToString("0.00") + " (x" + Pct(s) + "%" + (_sledges ? ", Sledges" : "") + ")"
                                 + " | klucz " + (s.LitterEnabled ? "TAK" : "NIE") + (_onKey ? "" : " (nieczynny)") + " | potkniecia " + _stumbles + ", rownolegle " + _parStumbles);
                    _day = day; _hp0 = h.HitPoints; _hpMin = h.HitPoints;
                    _hMove = 0; _hRest = 0; _hArmy = 0; _hSea = 0; _ups = 0; _downs = 0; _stumbles = 0; _parStumbles = 0; _why = "";
                }
                if (h.HitPoints < _hpMin) _hpMin = h.HitPoints;
                Evaluate("godzina");
                if (!_carried || !mp.IsActive) return;   // niewola / sluzba ROT (partia nieczynna) - godzin nie liczymy
                if (InForeignArmy(mp)) _hArmy++;
                else if (mp.IsCurrentlyAtSea) _hSea++;
                else if (SlowHealing.PlayerMarching(mp)) _hMove++;
                else _hRest++;
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        // ------------------------------------------------------------ predkosc (NightRest.SpeedPostfix - rownolegle, tylko odczyt stanu)
        internal static void Speed(MobileParty mp, ref ExplainedNumber r)
        {
            try
            {
                if (!_carried || mp == null || mp != MobileParty.MainParty) return;
                if (!SpeedDepth.OutermostFinal) return;                       // lancuch modeli (ROT->RB->vanilla) - raz
                var s = Settings.Current;
                if (s == null || !s.LitterEnabled) return;
                if (mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) return;  // na pokladzie - bez kary
                if (InForeignArmy(mp)) return;                                  // (d) niesiony w taborze cudzej armii
                float pct = Pct(s);
                if (pct >= 100f) return;
                float res = r.ResultNumber;
                if (res <= 0f) return;
                float f = 1f + r.SumOfFactors;
                float cut = res * (1f - pct / 100f);
                float target = res - cut;
                r.Add(f > 0.01f ? -cut / f : -cut, Txt);
                // jak CampCart.Speed: wynik trzymany samym sufitem (kolumna MarchPace) - Add schodzi tylko pod wartoscia przed docieciem; sufit do celu
                float lim = r.LimitMaxValue;
                if (lim > 0f && lim < float.MaxValue * 0.5f && res >= lim - 0.0005f && r.ResultNumber > target + 0.001f)
                {
                    float lo = r.LimitMinValue;
                    r.LimitMax(target > lo ? target : lo);
                }
                _spdBefore = res; _spdAfter = r.ResultNumber;
            }
            catch { Interlocked.Increment(ref _parStumbles); }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3) Log.Error("Litter501." + where, e);
        }

        /// <summary>Linia startu sesji (SessionLaunched).</summary>
        internal static void SessionStart()
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                Log.Info("Nosze (501): " + (s.LitterEnabled ? "TAK" : "NIE") + " - gracz na noszach przy HP <= " + s.LitterHpThreshold + " albo lozu Grave, zejscie przy HP > "
                         + s.LitterHpRelease + " | predkosc x" + s.LitterSpeedPercent + "% (Sledges chirurga x" + s.LitterSpeedPercentSledges + "%) - w NightRest.SpeedPostfix po suficie MarchPace"
                         + " | morze i cudza armia bez kary | AI bez zmian | tryb LogGate " + LogGate.Mode + (LogGate.Mode == 1 || LogGate.Mode == 2 ? " (autotest/profil - nosze nieczynne)" : "") + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }
    }
}
