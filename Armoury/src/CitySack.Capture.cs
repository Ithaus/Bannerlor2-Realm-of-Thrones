using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// L1 - BEZ ZDOBYCIA NIE MA GRABIEZY i KIESA ROZBITEJ ZALOGI DO ZWYCIEZCOW (projekt lupu miast rozdz. 1.7 D2, D3; rozdz. 4 "Wygrana bitwa pod murami bez zdobycia").
    ///  - D2 (klucz NoSackWithoutCapture): gra naklada nastepstwa (kara dobrobytu, budowle, lojalnosc, sila notabli, lup, a BK zabija mieszkancow) takze po wygranej
    ///    oblegajacych w bitwie POD MURAMI (SiegeOutside) - osada przy tym nie pada (KingdomManager.SiegeCompleted zmienia pana tylko po szturmie i wypadach;
    ///    G SiegeAftermathCampaignBehavior.cs:79-80, KingdomManager.cs:241). Prefiks OnMapEventEnded zachowania gry: bitwa pod murami wygrana przez atakujacych -
    ///    metoda gry pominieta (zadnych nastepstw, nikt nie dostaje lupu, nikt nie traci). Szturm, wypady i wypady z blokady - jak w grze (zdobycie).
    ///  - D3 (klucz BeatenGarrisonPurseToVictor): zaloga rozbita w bitwie (pod murami, w wypadzie albo w szturmie, zanim gra zmieni pana) znikala z mapy z kiesa
    ///    (ksiega: "kiesy partii bez wodza, ktore zniknely z mapy - garnizony"). Teraz regula kies rozbitych partii (BeatenPurse 3.2 pkt 3): niszczyciel wrogiej
    ///    frakcji - jego wodz (GiveGoldAction zaloga -> bohater; korona 1/9 od wodza-wasala - Ransom178.NoteBeatenGold), partia bez wodza z kiesa - jej kiesa;
    ///    kto nie moze wziac (Inni, milicja) - kasa osady tej zalogi (zloto zostaje w miescie). Zaloga niszczona przy zmianie pana (ChangeOwnerOfSettlementAction,
    ///    niszczyciel tej samej frakcji co nowy pan) - poza regula: kiese przejmuje nowa zaloga (SmallGold.CapturePre). Nasluch MobilePartyDestroyed dopisany po
    ///    ksiedze obiegu - biegnie przed nia (ksiega widzi kiese juz oddana).
    /// </summary>
    internal static partial class CitySack
    {
        internal static bool NoSackWithoutCaptureOn { get { var s = Settings.Current; return s != null && s.NoSackWithoutCapture; } }
        internal static bool GarrisonPurseOn { get { var s = Settings.Current; return s != null && s.BeatenGarrisonPurseToVictor; } }

        private static int _dSkip;
        private static readonly List<string> _dSkipIds = new List<string>();
        private static long _dGLord, _dGParty, _dGHome, _dGOdd, _dGLost, _dGPlayer;
        private static int _dGN, _dGOddN;

        /// <summary>D2: bitwa pod murami wygrana przez atakujacych - true = pominac metode gry (bez nastepstw). Wolane z prefiksu OnMapEventEnded (MapEndPre).</summary>
        private static bool SkipOutside(MapEvent me)
        {
            try
            {
                if (!NoSackWithoutCaptureOn || !Live || me == null || !me.IsSiegeOutside || me.MapEventSettlement == null) return false;
                if (me.WinningSide != BattleSideEnum.Attacker) return false;     // gra i tak nic nie robi (G :80)
                var st = me.MapEventSettlement;
                _dSkip++;
                _dSkipIds.Add(st.StringId);
                if (MeasureOn)
                {
                    var lp = me.GetLeaderParty(BattleSideEnum.Attacker);
                    Log.Info("Lup miast (L1): dzien " + (int)CampaignTime.Now.ToDays + " | osada " + st.StringId + " (" + st.Name + ") | bitwa pod-murami wygrana przez "
                             + (lp != null ? lp.Name.ToString() : "-") + " - nastepstwa pominiete (osada nie padla: bez kary dobrobytu, budowli, lojalnosci, lupu i zabitych BK).");
                }
                return true;
            }
            catch (Exception e) { Stumble("SkipOutside", e); return false; }
        }

        /// <summary>D3: nasluch MobilePartyDestroyed (ArmouryBehavior.RegisterEvents) - kiesa zalogi rozbitej przez wroga do zwyciezcy. Nie rzuca.</summary>
        internal static void OnPartyDestroyed(MobileParty mp, PartyBase destroyer)
        {
            try
            {
                if (mp == null || destroyer == null || !mp.IsGarrison || !GarrisonPurseOn || !Live) return;
                int g = mp.PartyTradeGold;
                if (g <= 0) return;
                IFaction fa = mp.MapFaction, fb = destroyer.MapFaction;
                if (fa == null || fb == null || fa == fb || !FactionManager.IsAtWarAgainstFaction(fa, fb)) { _dGOdd += g; _dGOddN++; return; }   // nie wrog (zmiana pana) - jak dotad
                _dGN++;
                var win = destroyer.MobileParty;
                if (win != null && win.IsActive && !Undead.Party(win))
                {
                    var hero = win.LeaderHero;
                    if (hero != null && hero.IsAlive)
                    {
                        GiveGoldAction.ApplyForPartyToCharacter(mp.Party, hero, g, true);   // zdarzenie gry: ksiega widzi przelew zaloga -> bohater
                        int moved = g - mp.PartyTradeGold;
                        _dGLord += moved;
                        Ransom178.NoteBeatenGold(hero, moved);                              // korona 1/9 od wodza-wasala (jak BeatenPurse pkt 4)
                        if (hero == Hero.MainHero && moved > 0) { _dGPlayer += moved; Log.Player("The broken garrison's chest is yours: " + moved + " denars."); }
                    }
                    else if (hero == null && win.IsPartyTradeActive)
                    {
                        GiveGoldAction.ApplyForPartyToParty(mp.Party, win.Party, g, true);
                        _dGParty += g - mp.PartyTradeGold;
                    }
                    g = mp.PartyTradeGold;
                    if (g <= 0) return;
                }
                // kto nie moze wziac (Inni - trup monety nie bierze, milicja, partia bez kiesy): zloto zostaje w osadzie zalogi
                var home = mp.CurrentSettlement ?? mp.HomeSettlement;
                if (home != null && home.SettlementComponent != null)
                {
                    GiveGoldAction.ApplyForPartyToSettlement(mp.Party, home, g, true);
                    _dGHome += g - mp.PartyTradeGold;
                }
                _dGLost += Math.Max(0, mp.PartyTradeGold);
            }
            catch (Exception e) { Stumble("OnPartyDestroyed", e); }
        }

        private static void L1Part(StringBuilder sb)
        {
            sb.Append(" | L1: nastepstwa pominiete po bitwie pod murami ").Append(_dSkip);
            if (_dSkipIds.Count > 0) sb.Append(" (").Append(string.Join(", ", _dSkipIds.ToArray())).Append(')');
            if (!NoSackWithoutCaptureOn) sb.Append(" [WYLACZONE No Sack Without Capture]");
            sb.Append(", kiesy zalog rozbitych przez wroga ").Append(_dGN).Append(": wodzom ").Append(_dGLord).Append(" (gracz ").Append(_dGPlayer).Append("), druzynom bez wodza ")
              .Append(_dGParty).Append(", do kasy osady zalogi ").Append(_dGHome).Append(", bez odbiorcy ").Append(_dGLost)
              .Append("; poza regula (niszczyciel nie wrog - zmiana pana) ").Append(_dGOdd).Append(" w ").Append(_dGOddN);
            if (!GarrisonPurseOn) sb.Append(" [WYLACZONE Beaten Garrison Purse To Victor]");
        }

        private static void L1Clear()
        {
            _dSkip = 0; _dSkipIds.Clear();
            _dGLord = _dGParty = _dGHome = _dGOdd = _dGLost = _dGPlayer = 0; _dGN = _dGOddN = 0;
        }

        private static bool L1Any { get { return _dSkip > 0 || _dGN > 0 || _dGOddN > 0; } }
    }
}
