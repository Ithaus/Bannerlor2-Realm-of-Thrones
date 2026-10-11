using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// E3/3 (paczka 164a-2) - LUP Z CIAL Z SAKIEWKI PRZEGRANYCH (projekt etapu 3, rozdz. 2 R5, rozdz. 3 E3/3). Wylacznik CasualtyLootFromPurse;
    /// czynny tylko razem z 3.2 (BeatenPurseToVictor - krytyka D4: bez 3.2 nie ma 1/9 korony) i z sakiewkami ludzi (MenPurseEnabled).
    /// Gra (MapEvent.LootCasualtyCharacter, galaz AI - wodz zwyciezcow nie jest graczem): za kazde cialo pokonanych (zabity, ranny) dopisuje
    /// zwyciezcy (int)(Round(oczekiwana wartosc) x aiTradePenalty) do PartyTradeGold - z niczego (Z1 "lup z cial dla AI": mediana 21 tys./d,
    /// skoki do 10 mln w jednej dobie).
    /// Teraz (prefiks bool, priorytet Low - po oknie ksiegi O16 First): ta sama kwota gry, najwyzej udzial ciala w sakiewce przegranej partii
    /// (sakiewka w chwili lupu tej bitwy / ludzie tej partii przed bitwa - migawka w prefiksie LootDefeatedPartyCasualties, czyszczona w jego
    /// finalizerze), zdjeta z MenPurse przegranych; 1/3 do PartyTradeGold zwyciezcy (to samo miejsce co gra: u lorda kiesa wodza, u karawany z
    /// bohaterem - jej kasa; u lorda tez ClanIncomeBook KThird i podstawa 1/9 Ransom178.NoteBeatenGold), 2/3 do sakiewki ludzi zwyciezcy;
    /// SkillLevelingManager.OnAIPartyLootCasualties jak gra (od kwoty zdjetej); pusta sakiewka (banda, garnizon bez sakiewki) - nic; zwyciezcy Inni
    /// - nic (trup monety nie bierze, jak 3.2). Galaz gracza (przedmioty) bez zmian. Gracz przegrany: AI zabiera z sakiewki jego ludzi, komunikat raz na lup bitwy.
    /// Ksiega: okno O16 liczy zmiane kiesy zwyciezcy - LootFin odejmuje 1/3 przekazana w tym wywolaniu (TakeThird) - przelew sakiewka -> kiesa/sakiewka.
    /// Kolejnosc w bitwie: lup z cial biegnie w CalculateAndCommitMapEventResults przed zniszczeniem partii, wiec MenPurse.OnPartyDestroyed oddaje
    /// potem tylko reszte sakiewki - bez podwojnej wyplaty.
    /// </summary>
    internal static class CasualtyLoot
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.CasualtyLootFromPurse && BeatenPurse.On && MenPurse.On; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        private sealed class Snap { public long Purse0; public long Men0; public long Bodies; public long Taken; }
        private static readonly Dictionary<MapEventParty, Snap> _snap = new Dictionary<MapEventParty, Snap>();
        private static long _lastThird;
        private static long _playerLostRun;

        private static long _bodies, _gameGold, _taken, _toLords, _toMen, _cut, _undeadSkip, _playerLost;
        private static int _empty, _playerMsgs;

        /// <summary>CirculationWindows.LootFin: 1/3 przekazana zwyciezcy w ostatnim wywolaniu (przelew, nie zloto z niczego) - odczyt zeruje.</summary>
        internal static long TakeThird() { long t = _lastThird; _lastThird = 0; return t; }

        /// <summary>Prefiks LootDefeatedPartyCasualties (void): migawka sakiewek i ludzi pokonanych na czas lupu tej bitwy.</summary>
        public static void RunPre(MBReadOnlyList<MapEventParty> __1)
        {
            try
            {
                _playerLostRun = 0;
                if (!On || !Live || __1 == null) return;
                foreach (var d in __1)
                {
                    if (d == null || d.Party == null || _snap.ContainsKey(d)) continue;
                    var mp = d.Party.MobileParty;
                    long purse = mp != null ? MenPurse.Get(mp) : 0;
                    long men = (d.Party.MemberRoster != null ? d.Party.MemberRoster.TotalManCount : 0) + (d.DiedInBattle != null ? d.DiedInBattle.TotalManCount : 0);
                    _snap[d] = new Snap { Purse0 = purse, Men0 = Math.Max(1L, men) };
                }
            }
            catch (Exception e) { Seal31.Stumble("CasualtyLoot.RunPre", e); }
        }

        public static void RunFin(MBReadOnlyList<MapEventParty> __1)
        {
            try
            {
                if (__1 != null) foreach (var d in __1) if (d != null) _snap.Remove(d);
                if (_playerLostRun > 0)
                {
                    Log.Player("The victors stripped your fallen men of " + _playerLostRun + " denars from their purse.", true);
                    _playerMsgs++;
                }
            }
            catch (Exception e) { Seal31.Stumble("CasualtyLoot.RunFin", e); }
            finally { _playerLostRun = 0; }
        }

        /// <summary>Prefiks bool MapEvent.LootCasualtyCharacter(cialo, zwyciezca, pokonany, aiTradePenalty, ...): galaz AI z sakiewki przegranych.</summary>
        public static bool LootPre(CharacterObject __0, MapEventParty __1, MapEventParty __2, float __3)
        {
            _lastThird = 0;
            int t = 0;
            try
            {
                if (!On || !Live || __0 == null || __1 == null || __2 == null || __1.Party == null) return true;
                var leader = __1.Party.LeaderHero;
                if (leader == null || leader == Hero.MainHero) return true;                // gra: bez wodza nic; gracz - przedmioty (bez zmian)
                var wmp = __1.Party.MobileParty;
                if (wmp == null) return true;
                float exp = Campaign.Current.Models.BattleRewardModel.GetExpectedLootedItemValueFromCasualty(leader, __0);
                if (Math.Abs(exp) < 1e-5f) return false;                                   // gra tez nic nie robi
                int num = (int)((float)MathF.Round(exp) * __3);                            // ten sam wzor co gra (MapEvent.cs:1864)
                if (num <= 0) return false;
                _bodies++; _gameGold += num;
                if (Undead.Party(wmp)) { _undeadSkip += num; return false; }                // trup monety nie bierze (jak 3.2)
                var dmp = __2.Party != null ? __2.Party.MobileParty : null;
                Snap sn = null;
                long limit = 0;
                if (dmp != null && _snap.TryGetValue(__2, out sn))
                {
                    // udzial na glowe narastajaco (bez gubienia ulamkow): po k cialach najwyzej sakiewka x k / ludzie przed bitwa
                    sn.Bodies++;
                    limit = Math.Max(0L, sn.Purse0 * Math.Min(sn.Bodies, sn.Men0) / sn.Men0 - sn.Taken);
                }
                int want = (int)Math.Min(num, limit);
                t = want > 0 ? MenPurse.Take(dmp, want) : 0;
                if (sn != null) sn.Taken += t;
                _cut += num - t;
                if (t <= 0) { _empty++; return false; }
                int third = t / 3;
                if (third > 0) { wmp.PartyTradeGold += third; _lastThird = third; }
                if (t - third > 0) MenPurse.Add(wmp, t - third);
                if (wmp.IsLordParty && third > 0)
                {
                    ClanIncomeBook.NoteInflow(leader, third, ClanIncomeBook.KThird);   // D rodu (tylko licznik), jak MenPurse.OnPartyDestroyed
                    Ransom178.NoteBeatenGold(leader, third);                           // podstawa 1/9 korony (3.2)
                }
                SkillLevelingManager.OnAIPartyLootCasualties(t, leader, __2.Party);
                _taken += t; _toLords += third; _toMen += t - third;
                if (dmp.IsMainParty) { _playerLost += t; _playerLostRun += t; }
                return false;
            }
            catch (Exception e) { Seal31.Stumble("CasualtyLoot.LootPre", e); return t <= 0; }   // nic nie zdjete - gra po swojemu; zdjete - bez drugiej wyplaty
        }

        internal static void ApplyAll()
        {
            Seal31.Patch("lup z cial - migawka sakiewek (LootDefeatedPartyCasualties)", AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties"), typeof(CasualtyLoot),
                         nameof(RunPre), null, nameof(RunFin), Priority.Normal);
            Seal31.Patch("lup z cial z sakiewki przegranych (LootCasualtyCharacter, Low)", AccessTools.Method(typeof(MapEvent), "LootCasualtyCharacter"), typeof(CasualtyLoot),
                         nameof(LootPre), null, null, Priority.Low);
        }

        internal static string Part()
        {
            var s = Settings.Current;
            string off = On ? "" : (s != null && !s.CasualtyLootFromPurse ? "WYLACZONE (Casualty Loot From Purse) - " : "NIECZYNNE (wymaga Beaten Purse To Victor 3.2 i Men Purse) - lup z niczego jak w grze; ");
            return off + "ciala z kwota gry " + _bodies + ", kwota gry " + _gameGold + ", zdjete z sakiewek przegranych " + _taken + " (do kies lordow i kas 1/3 " + _toLords
                   + ", do sakiewek zwyciezcow " + _toMen + "), uciete (pusta albo wyczerpana sakiewka) " + _cut + " (ciala bez zlota " + _empty + "), zwyciezcy Inni - bez lupu " + _undeadSkip
                   + "; gracz przegrany stracil " + _playerLost + " (" + _playerMsgs + " komunikatow)";
        }

        internal static void ClearDay() { _bodies = _gameGold = _taken = _toLords = _toMen = _cut = _undeadSkip = _playerLost = 0; _empty = _playerMsgs = 0; }

        internal static void Reset() { ClearDay(); _snap.Clear(); _lastThird = 0; _playerLostRun = 0; }
    }
}
