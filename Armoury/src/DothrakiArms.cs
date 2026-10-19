using System;
using System.Collections.Generic;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// BRON DOTHRAKOW TYLKO U DOTHRAKOW (n20-drobne; Jeff 19.10: "masa zolnierzy biega z arakhami - to bron khalow, a nie Westeros, to jakas bzdura";
    /// PRZEKAZANIE-2026-10-19 p. 3, decyzja Claude z pelnej zgody Jeffa na noc 19/20.10).
    ///
    /// Regula (GearFit.DothBar, klucz Gear Fit Dothraki Only): bron kultury Dothrakow (khuzait, steppe_bandits "Rogue Dothraki"), ktorej w zywych wzorcach ROT
    /// nie nosi ZADEN zolnierz innej kultury (spis przy starcie sesji w migawce GearFit.KinSnap; dane XML 19.10: arakh i kilka dothrackich szabel i tarcz -
    /// ROT daje Essos i Dorne wiele "wschodnich" lukow, szabel i wloczni kultury khuzait, te zostaja wolne jak dotad), jest tylko dla zolnierzy Dothrakow.
    /// Siedzi w GearFit.CultureOk PRZED regula "ta sama strona morza" - wiec dziala w kazdym miejscu, ktore pyta o kulture: zakupy notabli/AI/zalog/ludzi
    /// gracza, nadwyzki (MeetsFitOf - arakh u lorda Braavos staje sie nadwyzka i idzie do kupca), komplety i straz Waskiego Morza w bitwie (CrashScribe
    /// Mends.RealmWard przez refleksje - zdejmuje i daje zamiennik z polki). Podloga broni (czlowiek bez broni po strazach) bierze bron Dothrakow dopiero,
    /// gdy w polce nie ma zadnej innej broni, ktora on udzwignie (lepsza niz piesci - zasada GearFitFloorForeign). Bohaterowie, unikaty i wlasny wzorzec
    /// (TemplateHas) bez zmian.
    ///
    /// Bandy (klucz Outlaw Fence Dothraki Arms): banda bierze lup do zbrojowni DTE bez kontroli kultury i nigdy nie sprzedaje nadwyzek (MenPurse - tylko
    /// lordowie i gracz). Bron Dothrakow, ktorej nikt w bandzie nie moze nosic, przechodzi przy paserze (OutlawLaw, krok 4 doby) ze zbrojowni do jukow
    /// razem ze stanem (AiWear.TakeWornShare), a paser skupuje ja jak ladunek (FenceSell -> SellStack: miasto placi bandzie, sztuka na polke). Nic z niczego.
    /// Tylko przy zakupach AI (AiGear.On): bez nich DTE MoveRosterToArmory co tydzien wciaga bron z jukow z powrotem i placi za nia zlotem z niczego.
    ///
    /// Bez zapisu (stan sesji). Linie: "Bron Dothrakow (start sesji)" (GearFit.DothCensus) i "Bron Dothrakow: dzien N" (Daily, z GearFit.Daily).
    /// </summary>
    internal static class DothrakiArms
    {
        internal static bool BandOn { get { var s = Settings.Current; return s != null && s.OutlawFenceDothrakiArms && s.GearFitDothrakiOnly; } }

        // liczniki od ostatniej linii doby; misje i podloga z watku DTE (DoAssignAsync) - Interlocked
        private static int _mis, _men, _left, _floor;
        private static int _bands, _toPack, _sold, _soldGold, _packLeft, _packBands, _stum, _stumAll;
        private static bool _err;

        internal static void Reset()
        {
            _mis = _men = _left = _floor = 0;
            _bands = _toPack = _sold = _soldGold = _packLeft = _packBands = _stum = _stumAll = 0;
            _err = false;
        }

        internal static void NoteFloorLastResort() { Interlocked.Increment(ref _floor); }

        internal static void NoteMission(int men, int left)
        {
            Interlocked.Increment(ref _mis);
            Interlocked.Add(ref _men, men);
            Interlocked.Add(ref _left, left);
        }

        private static void Stumble(string where, Exception e)
        {
            _stum++; _stumAll++;
            if (!_err) { _err = true; try { Log.Error("DothrakiArms." + where, e); } catch { } }
        }

        /// <summary>Nikt z ludzi partii (bez bohaterow) nie moze nosic tej sztuki wedlug reguly kultury (GearFit.CultureOk - ta sama, co straz w bitwie).</summary>
        private static bool NobodyMay(TroopRoster roster, ItemObject it)
        {
            if (roster == null || it == null) return false;
            bool any = false;
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var c = el.Character;
                if (c == null || c.IsHero || el.Number <= 0) continue;
                any = true;
                if (GearFit.CultureOk(c, it)) return false;
            }
            return any;
        }

        /// <summary>OutlawLaw (krok 4 doby, banda w handlu - tuz przed FenceSell): bron Dothrakow ze zbrojowni DTE bandy, ktorej nikt w bandzie nie nosi,
        /// do jukow - ze stanem z zapisu obitych (AiWear.TakeWornShare przed zdjeciem ze slownika). Liczba sztuk bez zmian.</summary>
        internal static void BandToPack(MobileParty p)
        {
            if (!BandOn || p == null || !p.IsBandit || !AiGear.On || p.ItemRoster == null) return;
            try
            {
                var all = AiGear.Armories();
                Dictionary<ItemObject, int> arm;
                if (all == null || !all.TryGetValue(p.Id, out arm) || arm == null || arm.Count == 0) return;
                List<ItemObject> mv = null;
                foreach (var kv in arm)
                {
                    if (kv.Value <= 0 || !GearFit.DothOnly(kv.Key)) continue;
                    if (!NobodyMay(p.MemberRoster, kv.Key)) continue;
                    if (mv == null) mv = new List<ItemObject>();
                    mv.Add(kv.Key);
                }
                if (mv == null) return;
                int moved = 0;
                foreach (var it in mv)
                {
                    int n;
                    if (!arm.TryGetValue(it, out n) || n <= 0) continue;
                    var worn = AiWear.TakeWornShare(p, it, n, n);
                    arm.Remove(it);
                    int left = n;
                    foreach (var w in worn)
                    {
                        int k = Math.Min(left, w.Value);
                        if (k <= 0) continue;
                        p.ItemRoster.AddToCounts(new EquipmentElement(it, w.Key), k);
                        left -= k;
                    }
                    if (left > 0) p.ItemRoster.AddToCounts(it, left);
                    moved += n;
                }
                if (moved > 0) { _bands++; _toPack += moved; }
            }
            catch (Exception e) { Stumble("BandToPack", e); }
        }

        /// <summary>OutlawLaw.FenceSell: czy paser bierze ten stos z jukow jak ladunek - bron Dothrakow (sztuka zwykla, nie z zadania, towar), ktorej nikt
        /// w bandzie nie moze nosic.</summary>
        internal static bool FenceTakes(MobileParty p, ItemRosterElement el)
        {
            if (!BandOn || p == null || el.Amount <= 0) return false;
            try
            {
                var it = el.EquipmentElement.Item;
                if (it == null || el.EquipmentElement.IsQuestItem || it.NotMerchandise || !GearFit.DothOnly(it)) return false;
                return NobodyMay(p.MemberRoster, it);
            }
            catch (Exception e) { Stumble("FenceTakes", e); return false; }
        }

        internal static void NoteFenceSold(int pcs, int gold) { if (pcs > 0) { _sold += pcs; _soldGold += Math.Max(0, gold); } }

        /// <summary>Po FenceSell: ile broni Dothrakow (nie do noszenia w tej bandzie) czeka jeszcze w jukach - zaden paser w zasiegu jej nie kupil.</summary>
        internal static void AfterFence(MobileParty p)
        {
            if (!BandOn || p == null || p.ItemRoster == null) return;
            try
            {
                int n = 0;
                var r = p.ItemRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Amount > 0 && el.EquipmentElement.Item != null && GearFit.DothOnly(el.EquipmentElement.Item) && FenceTakes(p, el)) n += el.Amount;
                }
                if (n > 0) { _packLeft += n; _packBands++; }
            }
            catch (Exception e) { Stumble("AfterFence", e); }
        }

        private static string Yes(bool b) { return b ? "tak" : "nie"; }

        /// <summary>Linia doby (GearFit.Daily): liczniki od ostatniej linii, potem zero.</summary>
        internal static void Daily(int day, int deny)
        {
            try
            {
                var s = Settings.Current;
                bool on = s != null && s.GearFitDothrakiOnly;
                if (!on && !BandOn) return;
                int mis = Interlocked.Exchange(ref _mis, 0), men = Interlocked.Exchange(ref _men, 0), left = Interlocked.Exchange(ref _left, 0), floor = Interlocked.Exchange(ref _floor, 0);
                Log.Info("Bron Dothrakow: dzien " + day + " | Gear Fit Dothraki Only " + Yes(on) + ", Outlaw Fence Dothraki Arms " + Yes(BandOn)
                         + " | odmowy reguly (sprawdzenia kultury: zakupy, nadwyzki, straz w bitwie, zamienniki) " + deny
                         + " | bitwy (przydzialow DTE w misjach " + mis + ", ludzi " + men + "): po strazach i podlodze bron Dothrakow u nie-Dothrakow " + left
                         + " (z ostatniej deski podlogi - nic innego w polce " + floor + "; reszta " + Math.Max(0, left - floor) + " ma byc 0)"
                         + " | bandy: ze zbrojowni do jukow " + _toPack + " szt. (band " + _bands + "), sprzedane paserowi " + _sold + " szt. za " + _soldGold
                         + " zl, czeka w jukach po paserze " + _packLeft + " szt. (band " + _packBands + ")"
                         + (AiGear.On ? "" : " - zakupy AI wylaczone: bandy bez zmian (DTE wciaga juki do zbrojowni)")
                         + "; potkniecia " + _stum + " (sesja " + _stumAll + ").");
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { _bands = _toPack = _sold = _soldGold = _packLeft = _packBands = _stum = 0; }
        }
    }
}
