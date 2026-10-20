using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

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
    /// (TemplateHas) bez zmian. Zolnierz kultury Dothrakow nosi swoj arakh wszedzie (takze w bandzie w Westeros) - to jego wlasna bron.
    ///
    /// Bandy (klucz Outlaw Fence Dothraki Arms): zbrojownie DTE maja tylko bandy z hersztem-bohaterem (DTE GarbageCollectParties kasuje co dobe zbrojownie
    /// partii bez wodza-bohatera), a DTE bandytow nigdy z niej nie ubiera (SpawnAgentPatch.IsPartyValidForProcessing: IsBandit -> false). Taka zbrojownia
    /// jest jednak lupem: po bitwie DTE (OnMapEventEnded -> DistributeLootRandomly -> GetAllLootItems) kopiuje zbrojownie pokonanych zwyciezcy - takze
    /// graczowi (ArmyArmory.AddItemToArmory), ktory po rozbiciu bandy z hersztem dostaje jej arakhy. Bron Dothrakow, ktorej nikt w bandzie nie moze nosic,
    /// przechodzi wiec przy paserze (OutlawLaw, krok 4 doby) ze zbrojowni do jukow razem ze stanem (AiWear.TakeWornShare), a paser skupuje ja jak ladunek
    /// (FenceSell -> SellStack: miasto placi bandzie, sztuka na polke). Nic z niczego. Tylko przy zakupach AI (AiGear.On): bez nich DTE MoveRosterToArmory
    /// co tydzien wciaga bron z jukow z powrotem i placi za nia zlotem z niczego.
    ///
    /// POMIAR (recenzja n20-drobne, klucz Dothraki Arms Census Log): skad arakhy u Jeffa (log 19.10 11:40: 30 arakhow po bitwach z bandami 11:55-12:07,
    /// wrogowie we wzorcach gry) - dwie drogi bez rozstrzygniecia w logu: (a) ciala zolnierzy KULTURY Dothrakow w bandach Westeros (arakh we wlasnym
    /// wzorcu - regula ich nie rusza), (b) kopia zbrojowni DTE bandy z hersztem. Linie: "Bron Dothrakow w bitwie (pomiar)" (kazda bitwa gracza: kto nosi
    /// bron Dothrakow - kultura, rodzaj partii, strona morza; zbrojownie DTE partii wroga) i "Dothracy w Westeros (pomiar, co 7 dob)" (ludzie kultury
    /// Dothrakow w partiach, jency, pule wyrzutkow i zbrojownie DTE w Westeros). Tylko odczyt.
    ///
    /// Bez zapisu (stan sesji). Linie: "Bron Dothrakow (start sesji)" (GearFit.DothCensus) i "Bron Dothrakow: dzien N" (Daily, z GearFit.Daily).
    /// </summary>
    internal static class DothrakiArms
    {
        internal static bool BandOn { get { var s = Settings.Current; return s != null && s.OutlawFenceDothrakiArms && s.GearFitDothrakiOnly; } }
        internal static bool CensusOn { get { var s = Settings.Current; return s != null && s.DothrakiArmsCensusLog; } }

        // liczniki od ostatniej linii doby; misje i podloga z watku DTE (DoAssignAsync) - Interlocked
        private static int _mis, _men, _left, _floor;
        private static int _bands, _toPack, _sold, _soldGold, _packLeft, _packBands, _stum, _stumAll;
        private static bool _err;
        private static int _cenDay = int.MinValue;
        private const int CensusEveryDays = 7;

        internal static void Reset()
        {
            _mis = _men = _left = _floor = 0;
            _bands = _toPack = _sold = _soldGold = _packLeft = _packBands = _stum = _stumAll = 0;
            _err = false;
            _cenDay = int.MinValue;
        }

        internal static void NoteFloorLastResort() { Interlocked.Increment(ref _floor); }

        internal static void NoteMission(int men, int left)
        {
            Interlocked.Increment(ref _mis);
            Interlocked.Add(ref _men, men);
            Interlocked.Add(ref _left, left);
        }

        internal static void Stumble(string where, Exception e)
        {
            _stum++; _stumAll++;
            if (!_err) { _err = true; try { Log.Error("DothrakiArms." + where, e); } catch { } }
        }

        /// <summary>Nikt z ludzi partii (bez bohaterow) nie moze nosic tej sztuki wedlug reguly kultury (GearFit.CultureOk - ta sama, co straz w bitwie).
        /// Pytanie pomiarowe - GearFit.Quiet: bez licznika odmow linii doby (recenzja: odmowy maja liczyc zakupy, nadwyzki i straz, nie te petle).</summary>
        private static bool NobodyMay(TroopRoster roster, ItemObject it)
        {
            if (roster == null || it == null) return false;
            bool any = false;
            bool was = GearFit.Quiet;
            GearFit.Quiet = true;
            try
            {
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var c = el.Character;
                    if (c == null || c.IsHero || el.Number <= 0) continue;
                    any = true;
                    if (GearFit.CultureOk(c, it)) return false;
                }
            }
            finally { GearFit.Quiet = was; }
            return any;
        }

        /// <summary>OutlawLaw (krok 4 doby, banda w handlu - tuz przed FenceSell): bron Dothrakow ze zbrojowni DTE bandy, ktorej nikt w bandzie nie nosi,
        /// do jukow - ze stanem z zapisu obitych (AiWear.TakeWornShare przed zdjeciem ze slownika). Liczba sztuk bez zmian. Zbrojownie maja tylko bandy
        /// z hersztem-bohaterem (reszte DTE kasuje co dobe) - licznik "ze zbrojowni do jukow" bywa maly.</summary>
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

        /// <summary>Linia doby (GearFit.Daily): liczniki od ostatniej linii, potem zero. Przedtem (co 7 dob) spis "Dothracy w Westeros (pomiar)".</summary>
        internal static void Daily(int day, int deny)
        {
            try { Census(day); } catch (Exception e) { Stumble("Census", e); }
            try
            {
                var s = Settings.Current;
                bool on = s != null && s.GearFitDothrakiOnly;
                if (!on && !BandOn) return;
                int mis = Interlocked.Exchange(ref _mis, 0), men = Interlocked.Exchange(ref _men, 0), left = Interlocked.Exchange(ref _left, 0), floor = Interlocked.Exchange(ref _floor, 0);
                Log.Info("Bron Dothrakow: dzien " + day + " | Gear Fit Dothraki Only " + Yes(on) + ", Outlaw Fence Dothraki Arms " + Yes(BandOn)
                         + " | odmowy reguly (sprawdzenia kultury: zakupy, nadwyzki, straz w bitwie, zamienniki; bez pytan pomiarowych band) " + deny
                         + " | bitwy (przydzialow DTE w misjach " + mis + ", ludzi " + men + "): po strazach i podlodze bron Dothrakow u nie-Dothrakow " + left
                         + " (z ostatniej deski podlogi - nic innego w polce " + floor + "; reszta " + Math.Max(0, left - floor) + " ma byc 0)"
                         + " | bandy (zbrojownie maja tylko bandy z hersztem): ze zbrojowni do jukow " + _toPack + " szt. (band " + _bands + "), sprzedane paserowi " + _sold + " szt. za " + _soldGold
                         + " zl, czeka w jukach po paserze " + _packLeft + " szt. (band " + _packBands + ")"
                         + (AiGear.On ? "" : " - zakupy AI wylaczone: bandy bez zmian (DTE wciaga juki do zbrojowni)")
                         + "; potkniecia " + _stum + " (sesja " + _stumAll + ").");
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { _bands = _toPack = _sold = _soldGold = _packLeft = _packBands = _stum = 0; }
        }

        // ================================================================ POMIAR (recenzja n20-drobne): kto nosi bron Dothrakow i ilu Dothrakow jest w Westeros
        internal static readonly string[] KindName = { "bandy", "lordowie", "zalogi i milicje", "karawany", "inne" };

        internal static int Kind(MobileParty p)
        {
            if (p == null) return 4;
            if (p.IsBandit) return 0;
            if (p.IsLordParty) return 1;
            if (p.IsGarrison || p.IsMilitia) return 2;
            if (p.IsCaravan) return 3;
            return 4;
        }

        /// <summary>Kultura partii: kultura klanu (bandy - klan bandy), inaczej kultura partii gry.</summary>
        internal static string PartyCulture(MobileParty p)
        {
            try
            {
                var c = p != null && p.ActualClan != null ? p.ActualClan.Culture : null;
                if (c == null && p != null && p.Party != null) c = p.Party.Culture;
                return c != null ? c.StringId : "-";
            }
            catch { return "-"; }
        }

        /// <summary>Strona morza osady: kultura z kalibracji (PeopleBook.CultureAtCalibration - ta sama, co kraina StartCalibration.TownLandOf; tylko odczyt,
        /// bez pamieci TownLandOf). Watek glowny.</summary>
        internal static bool EssosOf(Settlement s) { return s != null && GearFit.IsEssos(PeopleBook.CultureAtCalibration(s)); }

        /// <summary>Strona morza miejsca na mapie: kraina najblizszej twierdzy (OutlawLaw.PoolRegionAt). Watek glowny.</summary>
        internal static bool EssosAt(Vec2 p) { return EssosOf(OutlawLaw.PoolRegionAt(p)); }

        private static bool SideOf(MobileParty p, Dictionary<MobileParty, bool> memo)
        {
            bool b;
            if (memo.TryGetValue(p, out b)) return b;
            try { b = EssosAt(p.Position.ToVec2()); } catch { b = false; }
            memo[p] = b;
            return b;
        }

        private static string Clean(string s) { return s == null ? "-" : s.Replace('|', '/').Replace(';', ','); }

        private static string NameOf(MobileParty p)
        {
            try { return Clean(p.Name != null ? p.Name.ToString() : p.StringId); } catch { return "?"; }
        }

        /// <summary>Czy wzorzec zolnierza (zestawy bojowe, sloty 0-3) ma bron Dothrakow (DothOnly). Pamiec na jeden spis.</summary>
        private static bool TemplateCarries(CharacterObject c, Dictionary<CharacterObject, bool> memo)
        {
            bool b;
            if (memo.TryGetValue(c, out b)) return b;
            b = false;
            try
            {
                foreach (var eq in c.BattleEquipments)
                {
                    if (eq == null) continue;
                    for (int i = 0; i < 4 && !b; i++) { var it = eq[(EquipmentIndex)i].Item; if (it != null && GearFit.DothOnly(it)) b = true; }
                    if (b) break;
                }
            }
            catch { }
            memo[c] = b;
            return b;
        }

        private static void CountDoth(TroopRoster r, Dictionary<CharacterObject, bool> memo, ref int men, ref int tmpl)
        {
            if (r == null) return;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var c = el.Character;
                if (c == null || c.IsHero || el.Number <= 0 || c.Culture == null || !GearFit.IsDoth(c.Culture.StringId)) continue;
                men += el.Number;
                if (TemplateCarries(c, memo)) tmpl += el.Number;
            }
        }

        /// <summary>Spis co 7 dob (pierwsza doba sesji, potem co 7): ludzie kultury Dothrakow w Westeros - w partiach innej kultury (wedlug rodzaju), w partiach
        /// Dothrakow, jency, pule wyrzutkow regionow; zbrojownie DTE partii w Westeros z bronia Dothrakow. Tylko odczyt, watek glowny (takt doby).</summary>
        private static void Census(int day)
        {
            if (!CensusOn) return;
            if (_cenDay != int.MinValue && day >= _cenDay && day - _cenDay < CensusEveryDays) return;
            _cenDay = day;
            var memo = new Dictionary<CharacterObject, bool>();
            var side = new Dictionary<MobileParty, bool>();
            var byId = new Dictionary<MBGUID, MobileParty>();
            var top = new List<KeyValuePair<int, string>>();
            int[] wk = new int[5];
            int wMen = 0, wParties = 0, wTmpl = 0, wBand = 0, wBandN = 0, wDp = 0, wDpN = 0, wPris = 0, eMen = 0, eParties = 0;
            var all = MobileParty.All;
            for (int k = 0; k < all.Count; k++)
            {
                var p = all[k];
                if (p == null || !p.IsActive) continue;
                byId[p.Id] = p;
                int n = 0, nt = 0, np = 0, npt = 0;
                CountDoth(p.MemberRoster, memo, ref n, ref nt);
                CountDoth(p.PrisonRoster, memo, ref np, ref npt);
                if (n == 0 && np == 0) continue;
                bool es = SideOf(p, side);
                string pc = PartyCulture(p);
                bool dp = GearFit.IsDoth(pc);
                if (es) { if (!dp && n > 0) { eMen += n; eParties++; } continue; }
                wPris += np;
                if (n == 0) continue;
                if (dp) { wDp += n; wDpN++; continue; }
                int kd = Kind(p);
                wMen += n; wParties++; wTmpl += nt; wk[kd] += n;
                if (kd == 0) { wBand += n; wBandN++; }
                top.Add(new KeyValuePair<int, string>(n, NameOf(p) + " (" + KindName[kd] + ", " + pc + ")"));
            }
            float poolW, poolE; int poolR;
            OutlawLaw.DothPool(out poolW, out poolE, out poolR);
            int armP = 0, armPcs = 0, armBP = 0, armBPcs = 0;
            var arms = AiGear.Armories();
            if (arms != null)
            {
                foreach (var kv in arms)
                {
                    MobileParty p;
                    if (kv.Value == null || !byId.TryGetValue(kv.Key, out p)) continue;
                    int pcs = 0;
                    foreach (var e in kv.Value) if (e.Value > 0 && GearFit.DothOnly(e.Key)) pcs += e.Value;
                    if (pcs == 0 || SideOf(p, side) || GearFit.IsDoth(PartyCulture(p))) continue;
                    armP++; armPcs += pcs;
                    if (p.IsBandit) { armBP++; armBPcs += pcs; }
                }
            }
            top.Sort((a, b) => b.Key.CompareTo(a.Key));
            var sb = new StringBuilder(600);
            sb.Append("Dothracy w Westeros (pomiar, co ").Append(CensusEveryDays).Append(" dob): dzien ").Append(day)
              .Append(" | ludzie kultury Dothrakow (khuzait, steppe_bandits; bez bohaterow) w partiach innej kultury w Westeros ").Append(wMen).Append(" w ").Append(wParties)
              .Append(" partiach (bandy ").Append(wBand).Append(" w ").Append(wBandN).Append(" bandach, lordowie ").Append(wk[1]).Append(", zalogi i milicje ").Append(wk[2])
              .Append(", karawany ").Append(wk[3]).Append(", inne ").Append(wk[4]).Append("), z bronia Dothrakow we wzorcu ").Append(wTmpl)
              .Append(" | w partiach Dothrakow w Westeros ").Append(wDp).Append(" (partii ").Append(wDpN).Append(")")
              .Append(" | jency kultury Dothrakow w partiach w Westeros ").Append(wPris)
              .Append(" | w pulach wyrzutkow Westeros ").Append(poolW.ToString("0")).Append(" (regionow ").Append(poolR).Append("; Essos ").Append(poolE.ToString("0")).Append(")")
              .Append(" | zbrojownie DTE w Westeros (partie nie-Dothrakow) z bronia Dothrakow: partii ").Append(armP).Append(", szt. ").Append(armPcs)
              .Append(" (bandy ").Append(armBP).Append(", szt. ").Append(armBPcs).Append(" - po bitwie DTE kopiuje zbrojownie pokonanego zwyciezcy)")
              .Append(" | Essos dla porownania: Dothrakow w partiach innej kultury ").Append(eMen).Append(" (partii ").Append(eParties).Append(")")
              .Append(" | najwiecej w Westeros: ");
            if (top.Count == 0) sb.Append("-");
            for (int i = 0; i < top.Count && i < 5; i++) { if (i > 0) sb.Append("; "); sb.Append(top[i].Value).Append(" x").Append(top[i].Key); }
            sb.Append(" | potkniecia (sesja) ").Append(_stumAll).Append('.');
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ pomiar w bitwie gracza
        internal static void OnMissionInit(Mission m)
        {
            if (m == null || !CensusOn || Campaign.Current == null || MapEvent.PlayerMapEvent == null) return;
            try { m.AddMissionBehavior(new DothrakiArmsMission()); } catch (Exception e) { Stumble("OnMissionInit", e); }
        }
    }

    /// <summary>Pomiar (klucz Dothraki Arms Census Log): w bitwie gracza kazdy czlowiek przy spawnie (sprzet juz po DTE i strazach) - czy ma w slotach 0-3
    /// bron Dothrakow (DothOnly); wedlug kultury zolnierza, strony, rodzaju partii, strony morza partii; plus zbrojownie DTE partii wroga. Linia na koniec
    /// misji "Bron Dothrakow w bitwie (pomiar)". Tylko odczyt, watek glowny.</summary>
    internal sealed class DothrakiArmsMission : MissionLogic
    {
        private int _men, _carry, _doth, _other, _dothWestBand, _heroCarry, _west, _essos, _stum;
        private readonly int[] _side = new int[3];
        private readonly int[] _kind = new int[5];
        private readonly Dictionary<string, int> _top = new Dictionary<string, int>();
        private readonly Dictionary<MobileParty, bool> _sea = new Dictionary<MobileParty, bool>();
        private readonly HashSet<MobileParty> _foes = new HashSet<MobileParty>();
        private bool _done;

        private static MobileParty PartyOf(Agent a)
        {
            try
            {
                var pb = a.Origin != null ? a.Origin.BattleCombatant as PartyBase : null;
                return pb != null ? pb.MobileParty : null;
            }
            catch { return null; }
        }

        private static int SideIx(Agent a)
        {
            var t = a.Team;
            if (t == null) return 2;
            if (t.IsPlayerTeam) return 0;
            if (t.IsPlayerAlly) return 1;
            return 2;
        }

        private bool Essos(MobileParty p)
        {
            bool b;
            if (p == null) return false;
            if (_sea.TryGetValue(p, out b)) return b;
            try { b = DothrakiArms.EssosAt(p.Position.ToVec2()); } catch { b = false; }
            _sea[p] = b;
            return b;
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            try
            {
                if (_done || agent == null || !agent.IsHuman) return;
                var co = agent.Character as CharacterObject;
                if (co == null) return;
                var mp = PartyOf(agent);
                int si = SideIx(agent);
                if (si == 2 && mp != null) _foes.Add(mp);
                var eq = agent.SpawnEquipment;
                if (eq == null) return;
                bool has = false;
                for (int i = 0; i < 4 && !has; i++) { var it = eq[(EquipmentIndex)i].Item; if (it != null && GearFit.DothOnly(it)) has = true; }
                if (co.IsHero) { if (has) _heroCarry++; return; }
                _men++;
                if (!has) return;
                _carry++;
                _side[si]++;
                int kd = DothrakiArms.Kind(mp);
                _kind[kd]++;
                bool es = Essos(mp);
                if (es) _essos++; else _west++;
                bool doth = co.Culture != null && GearFit.IsDoth(co.Culture.StringId);
                if (doth) { _doth++; if (kd == 0 && !es) _dothWestBand++; }
                else _other++;
                string key = co.StringId + " (" + (co.Culture != null ? co.Culture.StringId : "-") + ", " + DothrakiArms.KindName[kd] + " " + DothrakiArms.PartyCulture(mp)
                             + ", " + (es ? "Essos" : "Westeros") + ", " + (si == 0 ? "Twoi" : (si == 1 ? "sojusznik" : "wrog")) + ")";
                int n;
                _top.TryGetValue(key, out n);
                _top[key] = n + 1;
            }
            catch (Exception e) { _stum++; DothrakiArms.Stumble("Mission.OnAgentBuild", e); }
        }

        protected override void OnEndMission()
        {
            try { Report(); } catch (Exception e) { DothrakiArms.Stumble("Mission.Report", e); }
            base.OnEndMission();
        }

        private void Report()
        {
            if (_done) return;
            _done = true;
            if (_men == 0 && _heroCarry == 0) return;
            int armP = 0, armPcs = 0, armBP = 0, armBPcs = 0;
            var arms = AiGear.Armories();
            if (arms != null)
            {
                foreach (var p in _foes)
                {
                    Dictionary<ItemObject, int> arm;
                    if (p == null || !arms.TryGetValue(p.Id, out arm) || arm == null) continue;
                    int pcs = 0;
                    foreach (var e in arm) if (e.Value > 0 && GearFit.DothOnly(e.Key)) pcs += e.Value;
                    if (pcs == 0) continue;
                    armP++; armPcs += pcs;
                    if (p.IsBandit) { armBP++; armBPcs += pcs; }
                }
            }
            var top = new List<KeyValuePair<string, int>>(_top);
            top.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new StringBuilder(500);
            sb.Append("Bron Dothrakow w bitwie (pomiar): dzien ").Append((int)CampaignTime.Now.ToDays)
              .Append(" | ludzi w bitwie (bez bohaterow) ").Append(_men).Append(", z bronia Dothrakow ").Append(_carry)
              .Append(" (bohaterowie z nia ").Append(_heroCarry).Append(" - bez zmian)")
              .Append(" | kultura zolnierza: Dothrakowie ").Append(_doth).Append(" (w bandach w Westeros ").Append(_dothWestBand).Append("), inni ").Append(_other)
              .Append(" (ma byc 0 poza ostatnia deska podlogi)")
              .Append(" | strona: Twoi ").Append(_side[0]).Append(", sojusznicy ").Append(_side[1]).Append(", wrog ").Append(_side[2])
              .Append(" | partie: bandy ").Append(_kind[0]).Append(", lordowie ").Append(_kind[1]).Append(", zalogi i milicje ").Append(_kind[2])
              .Append(", karawany ").Append(_kind[3]).Append(", inne ").Append(_kind[4])
              .Append(" | strona morza partii: Westeros ").Append(_west).Append(", Essos ").Append(_essos)
              .Append(" | najczesciej: ");
            if (top.Count == 0) sb.Append("-");
            for (int i = 0; i < top.Count && i < 6; i++) { if (i > 0) sb.Append("; "); sb.Append(top[i].Key).Append(" x").Append(top[i].Value); }
            sb.Append(" | zbrojownie DTE partii wroga (po bitwie DTE kopiuje je zwyciezcy): partii z bronia Dothrakow ").Append(armP).Append(" z ").Append(_foes.Count)
              .Append(", szt. ").Append(armPcs).Append(" (bandy ").Append(armBP).Append(", szt. ").Append(armBPcs).Append(")")
              .Append(" | potkniecia ").Append(_stum).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
