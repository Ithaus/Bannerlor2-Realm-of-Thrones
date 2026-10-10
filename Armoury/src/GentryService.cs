using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PACZKA 179 - RYCERZE BEZ LENNA BEZ ODDZIALOW (projekt etapu 2, krok C3; PLAN 2.7; [D] 03:45 nr 9 = Q1a, 10:20; Jeff 09.10 "rycerze bez lenna jezdza
    /// przy panu", "jesli nie ma sprzetu, to nie ma sprzetu - nic z kosmosu"). Wylacznik GentryNoParties.
    ///  - Rod rycerza = rod BK gentry ("gentryClan_", BK IsGentryClan: mniejsze parostwo) bez miasta i zamku. Nie wystawia wlasnej druzyny: latka nowej
    ///    partii (ClanBudget.SpawnPrefix -> BlocksSpawn) i prefiks BK SummonGentry (BK wystawial partie z ludzi majatku - "z kosmosu" zbrojnych).
    ///  - Na wezwanie choragwi glowa rodu rycerza JEDZIE W DRUZYNIE PANA (pan = wlasciciel wsi majatku) jako czlonek partii (AddHeroToPartyAction):
    ///    BK wezwanie (SummonGentry): do partii pana w tej armii, inaczej do wodza armii (wzywajacego). AI codziennie: krolestwo w wojnie i pan w armii
    ///    krolestwa - do pana; pan bez partii albo poza armia - do wodza najblizszej armii krolestwa. Druzyna gracza tylko na wezwanie gracza (BK);
    ///    rycerze, ktorych panem jest gracz, jada tylko na jego wezwanie.
    ///  - Po rozwiazaniu armii, w pokoju, przy zmianie strony albo rozwiazaniu partii rycerz wraca do majatku (TeleportHeroAction - jak BK, ktory sadza
    ///    rodzine rycerza w majatku co tydzien). Ludzie majatku zostaja w ludnosci BK - rycerz jedzie sam, bez zolnierzy i bez sprzetu z niczego.
    ///  - Zold rycerza GentryKnightWage (24 zl = 2 szylingi) dziennie: glowa rodu, ktorego druzyna go wiezie -> glowa rodu rycerza (GiveGoldAction w parze);
    ///    w pulapie zoldu partii 166 (KnightWage w ClanBudget) i w podstawie zwrotu 50% korony (SoldierPay.AddKnightPaid), u rycerza w D (czesc "kontrakt").
    ///    Straz (WatchUnpaid) - bez zoldu. Gracz placi tak samo (jedna regula), komunikat zbiorczo co 7 dob.
    ///  - Istniejace partie rycerzy: limit zoldu 0 (bez rekrutow, awansow i dosypki BK z majatku; dezercja z limitu zdjeta - ClanBudget.DesertPrefix),
    ///    BK odsyla je do majatku i rozwiazuje (FinishParty) - prefiks: zolnierze do ludnosci BK wsi majatku (nie w nicosc).
    ///  - GentryEstateSpendCap: zakupy majatkow BK (niewolnicy, zaopatrzenie wsi) z przydzialu sprzetu 166 rodu i nie ponizej podlogi rodziny w kiesie.
    /// Stan pochodny ze swiata (rycerz w partii innego rodu = w sluzbie) - bez zapisu. Wylaczone: rycerze w sluzbie wracaja do domu, BK jak dotad.
    /// </summary>
    internal static class GentryService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string GentryPrefix = "gentryClan_";

        internal static bool On { get { var s = Settings.Current; return s != null && s.GentryNoParties; } }

        // ------------------------------------------------------------ BK (refleksja, leniwie; Reset czysci - BK ma nowe obiekty po kazdym wczytaniu)
        private static bool _bkTried;
        private static object _beh;
        private static MethodInfo _isGentry;
        private static FieldInfo _t1, _t2;
        private static PropertyInfo _estData, _estSettlement, _estOwner;

        private static bool BkResolve()
        {
            if (_beh != null && _isGentry != null) return true;
            if (_bkTried || Campaign.Current == null) return false;
            _bkTried = true;
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Behaviours.BKGentryBehavior");
                if (t == null) return false;
                MethodInfo get = null;
                foreach (var m in typeof(Campaign).GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    if (m.Name == "GetCampaignBehavior" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0) { get = m; break; }
                _beh = get != null ? get.MakeGenericMethod(t).Invoke(Campaign.Current, null) : null;
                _isGentry = AccessTools.Method(t, "IsGentryClan", new[] { typeof(Clan) });
                return _beh != null && _isGentry != null;
            }
            catch { _beh = null; _isGentry = null; return false; }
        }

        /// <summary>Osada majatku BK (Estate.EstatesData.Settlement) albo null.</summary>
        internal static Settlement EstateSettlement(object estate)
        {
            try
            {
                if (estate == null) return null;
                if (_estData == null) _estData = AccessTools.Property(estate.GetType(), "EstatesData");
                var data = _estData != null ? _estData.GetValue(estate, null) : null;
                if (data == null) return null;
                if (_estSettlement == null) _estSettlement = AccessTools.Property(data.GetType(), "Settlement");
                return _estSettlement != null ? _estSettlement.GetValue(data, null) as Settlement : null;
            }
            catch { return null; }
        }

        private static Hero EstateOwner(object estate)
        {
            try
            {
                if (estate == null) return null;
                if (_estOwner == null) _estOwner = AccessTools.Property(estate.GetType(), "Owner");
                return _estOwner != null ? _estOwner.GetValue(estate, null) as Hero : null;
            }
            catch { return null; }
        }

        /// <summary>Rod rycerza teraz (bez pamieci doby): "gentryClan_", bez miasta i zamku, BK - mniejsze parostwo; majatek - osada majatku BK (moze byc null).</summary>
        internal static bool IsKnightClanNow(Clan c, out Settlement estate)
        {
            estate = null;
            if (c == null || c.StringId == null || !c.StringId.StartsWith(GentryPrefix, StringComparison.Ordinal)) return false;
            if (c == Clan.PlayerClan || c.IsEliminated || c.IsBanditFaction || c.Leader == null) return false;
            if (c.Fiefs != null && c.Fiefs.Count > 0) return false;   // rycerz z lennem to pan - prowadzi druzyne jak dotad
            if (ClanIncomeBook.IsUndeadClan(c)) return false;
            if (!BkResolve()) { var hs = c.HomeSettlement; estate = hs != null && hs.IsVillage ? hs : null; return true; }
            try
            {
                var tup = _isGentry.Invoke(_beh, new object[] { c });
                if (tup == null) return false;
                if (_t1 == null) { _t1 = tup.GetType().GetField("Item1"); _t2 = tup.GetType().GetField("Item2"); }
                bool lesser = _t1 != null && (bool)_t1.GetValue(tup);
                if (!lesser) return false;   // gracz nadal pelne parostwo - rod pana z prawem do druzyn
                estate = _t2 != null ? EstateSettlement(_t2.GetValue(tup)) : null;
                return true;
            }
            catch (Exception e) { Stumble("IsKnightClanNow", e); return false; }
        }

        // ------------------------------------------------------------ rody rycerzy na dzis (pamiec doby: rod -> osada majatku)
        private static readonly Dictionary<Clan, Settlement> _knights = new Dictionary<Clan, Settlement>();
        private static int _knightsDay = int.MinValue;

        private static void Refresh()
        {
            int today = (int)CampaignTime.Now.ToDays;
            if (today == _knightsDay) return;
            _knightsDay = today;
            _knights.Clear();
            foreach (var c in Clan.All)
            {
                try
                {
                    Settlement est;
                    if (c != null && c.StringId != null && c.StringId.StartsWith(GentryPrefix, StringComparison.Ordinal) && IsKnightClanNow(c, out est)) _knights[c] = est;
                }
                catch (Exception e) { Stumble("Refresh", e); }
            }
        }

        /// <summary>Rod rycerza (pamiec doby; wylaczone 179 - nie).</summary>
        internal static bool IsKnightClan(Clan c)
        {
            if (!On || c == null || c.StringId == null || !c.StringId.StartsWith(GentryPrefix, StringComparison.Ordinal)) return false;
            Refresh();
            return _knights.ContainsKey(c);
        }

        /// <summary>Rycerz w sluzbie w tej partii: glowa rodu rycerza w druzynie innego rodu.</summary>
        private static bool IsServingKnight(Hero h, MobileParty mp)
        {
            return h != null && mp != null && h != mp.LeaderHero && h.Clan != null && h == h.Clan.Leader && h.Clan != mp.ActualClan && _knights.ContainsKey(h.Clan);
        }

        /// <summary>166: zold rycerzy w sluzbie w tej partii (24 zl kazdy; Straz bez zoldu) - czesc zoldu partii w pulapie pana.</summary>
        internal static int KnightWage(MobileParty mp)
        {
            try
            {
                var s = Settings.Current;
                if (!On || s == null || mp == null || !mp.IsLordParty || mp.MemberRoster == null || mp.MemberRoster.TotalHeroes <= 1) return 0;
                if (CrownGifts.WatchUnpaidOn && CrownGifts.IsWatchParty(mp)) return 0;
                Refresh();
                int n = 0; var r = mp.MemberRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var ch = r.GetCharacterAtIndex(i);
                    if (ch != null && ch.IsHero && IsServingKnight(ch.HeroObject, mp)) n++;
                }
                return n * Math.Max(0, s.GentryKnightWage);
            }
            catch (Exception e) { Stumble("KnightWage", e); return 0; }
        }

        /// <summary>ClanBudget.SpawnPrefix: rod rycerza nie wystawia nowej partii (sprawdzenie bez pamieci doby - nowy rod BK tego samego dnia tez).</summary>
        internal static bool BlocksSpawn(Hero h)
        {
            try
            {
                Settlement est;
                if (!On || h == null || h.Clan == null || !IsKnightClanNow(h.Clan, out est)) return false;
                _dSpawnBlock++;
                return true;
            }
            catch (Exception e) { Stumble("BlocksSpawn", e); return false; }
        }

        /// <summary>Partia rodu rycerza (stara, sprzed 179) - limit zoldu 0; ClanBudget.DesertPrefix zdejmuje wtedy dezercje z limitu zoldu.</summary>
        internal static bool IsHeldParty(MobileParty mp)
        {
            try { return On && mp != null && mp.IsLordParty && IsKnightClan(mp.ActualClan); }
            catch { return false; }
        }

        /// <summary>Limit zoldu 0 dla partii rodu rycerza (bez rekrutow, awansow i dosypki BK z majatku). Zwraca true, gdy rod jest rodem rycerza.</summary>
        internal static bool HoldParties(Clan c)
        {
            if (!IsKnightClan(c)) return false;
            var wps = c.WarPartyComponents;
            if (wps != null)
                for (int i = 0; i < wps.Count; i++)
                {
                    var mp = wps[i] != null ? wps[i].MobileParty : null;
                    if (mp == null || !mp.IsActive || !mp.IsLordParty) continue;
                    if (mp.PaymentLimit != 0) mp.SetWagePaymentLimit(0);
                }
            return true;
        }

        // ------------------------------------------------------------ liczniki doby (linia "Rycerze (179)", "Obieg")
        internal static long LastPaid;
        private static long _dDue, _dPaid, _dPlayerPaid;
        private static int _dClans, _dServing, _dAtLord, _dAtArmy, _dAtPlayer, _dCalledLord, _dCalledArmy, _dHome, _dWait, _dUnpaidN, _dWatch, _dOwnParties, _dOwnMen;
        private static int _dSpawnBlock, _dSummon, _dSummonLord, _dSummonLead, _dSummonSkip, _dFinish, _dFinishMen, _dFinishLost, _dFinishPris, _dFinishShips;
        private static int _dEstateBlock; private static long _dEstateSpent;
        private static int _dArmies, _dArmiesWithKnight, _dFree, _dFreeNoArmy;
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static long _weekSum; private static int _weekStart = -1;

        internal static void Reset()
        {
            _bkTried = false; _beh = null; _isGentry = null; _t1 = _t2 = null; _estData = _estSettlement = _estOwner = null;
            _knights.Clear(); _knightsDay = int.MinValue; ZeroLast(); ClearDay(); _stumbles = 0; _err.Clear(); _weekSum = 0; _weekStart = -1;
        }

        internal static void ZeroLast() { LastPaid = 0; }

        private static void ClearDay()
        {
            _dDue = _dPaid = _dPlayerPaid = 0;
            _dClans = _dServing = _dAtLord = _dAtArmy = _dAtPlayer = _dCalledLord = _dCalledArmy = _dHome = _dWait = _dUnpaidN = _dWatch = _dOwnParties = _dOwnMen = 0;
            _dArmies = _dArmiesWithKnight = _dFree = _dFreeNoArmy = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("GentryService." + where, e); } catch { }
        }

        // ------------------------------------------------------------ rycerz wolny, cel wezwania, wyjazd, powrot
        private static bool Free(Hero h)
        {
            return h != null && h.IsAlive && h.IsActive && !h.IsChild && !h.IsPrisoner && h.PartyBelongedTo == null && h.PartyBelongedToAsPrisoner == null
                   && h.GovernorOf == null && h != Hero.MainHero && !h.IsNoncombatant;
        }

        /// <summary>Druzyna, do ktorej rycerz moze dolaczyc: czynna, AI (nie gracz), nie w bitwie, nie rozwiazywana, strona krolestwa rycerza, inny rod.</summary>
        private static bool Fit(MobileParty mp, Kingdom k, Clan knight)
        {
            return mp != null && mp.IsActive && mp.IsLordParty && !mp.IsMainParty && !mp.IsDisbanding && mp.MapEvent == null && mp.LeaderHero != null
                   && mp.ActualClan != null && mp.ActualClan != knight && mp.MapFaction == k && !Undead.Party(mp);
        }

        private static bool Join(Hero h, MobileParty target)
        {
            if (h.CurrentSettlement != null && h.PartyBelongedTo == null) LeaveSettlementAction.ApplyForCharacterOnly(h);
            AddHeroToPartyAction.Apply(h, target, false);
            return h.PartyBelongedTo == target;
        }

        /// <summary>Powrot do majatku (gdy wies w rekach wroga - najblizsza twierdza wlasnego krolestwa); TeleportHeroAction nie rusza rycerza w bitwie.</summary>
        private static void Home(Hero h, Settlement estate, MobileParty mp)
        {
            Settlement home = estate != null && estate.MapFaction == h.MapFaction ? estate : null;
            if (home == null)
            {
                var k = h.Clan != null ? h.Clan.Kingdom : null;
                Vec2 pos = mp != null ? mp.GetPosition2D : (estate != null ? estate.GetPosition2D : Vec2.Invalid);
                float bd = float.MaxValue;
                if (k != null && pos.IsValid)
                    foreach (var f in k.Fiefs)
                    {
                        var st = f != null ? f.Settlement : null;
                        if (st == null || st.IsUnderSiege) continue;
                        float d = pos.DistanceSquared(st.GetPosition2D);
                        if (d < bd) { bd = d; home = st; }
                    }
                if (home == null) home = estate;
            }
            if (home == null) { _dWait++; return; }
            TeleportHeroAction.ApplyImmediateTeleportToSettlement(h, home);
            if (h.PartyBelongedTo == null) _dHome++; else _dWait++;   // partia w bitwie - powrot jutro
        }

        /// <summary>Zold rycerza za dobe sluzby: glowa rodu druzyny -> glowa rodu rycerza; w podstawie zwrotu korony; u rycerza w D (czesc "kontrakt").</summary>
        private static void Pay(Hero h, MobileParty mp, Settings s)
        {
            int wage = Math.Max(0, s.GentryKnightWage);
            if (wage <= 0) return;
            if (CrownGifts.WatchUnpaidOn && CrownGifts.IsWatchParty(mp)) { _dWatch++; return; }   // Straz bez zoldu (182)
            var pc = mp.ActualClan;
            Hero payer = mp.IsMainParty ? Hero.MainHero : (pc != null ? pc.Leader : null);
            if (payer == null || !payer.IsAlive || payer == h) return;
            _dDue += wage;
            int x = Math.Min(wage, Math.Max(0, payer.Gold));
            if (x > 0)
            {
                int before = h.Gold;
                GiveGoldAction.ApplyBetweenCharacters(payer, h, x, true);
                x = Math.Max(0, h.Gold - before);
            }
            if (x <= 0) { _dUnpaidN++; return; }
            _dPaid += x;
            if (payer == Hero.MainHero) { _dPlayerPaid += x; _weekSum += x; }
            SoldierPay.AddKnightPaid(payer, x);                        // zwrot 50% korony jak zold partii (WageRefund: krolestwo w wojnie)
            ClanIncomeBook.NoteInflow(h, x, ClanIncomeBook.KContract);  // D rycerza: zold za sluzbe (czesc "kontrakt")
        }

        // ------------------------------------------------------------ raz na dobe (przed korona: zold rycerzy w podstawie dzisiejszego zwrotu)
        internal static void Daily()
        {
            ClearDay();
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                bool on = s.GentryNoParties;
                int today = (int)CampaignTime.Now.ToDays;
                Refresh();   // wylaczone = stan sprzed paczki: tylko rycerze w sluzbie wracaja do domu (bez zoldu i wezwan), BK jak dotad
                var war = new Dictionary<Kingdom, bool>();
                var free = new List<KeyValuePair<Clan, Settlement>>();
                foreach (var kv in _knights)
                {
                    try
                    {
                        var c = kv.Key; var estate = kv.Value; var h = c.Leader;
                        if (c.IsEliminated || h == null || !h.IsAlive) continue;
                        _dClans++;
                        var wps = c.WarPartyComponents;
                        if (wps != null)
                            for (int i = 0; i < wps.Count; i++)
                            {
                                var own = wps[i] != null ? wps[i].MobileParty : null;
                                if (own == null || !own.IsActive) continue;
                                _dOwnParties++; _dOwnMen += own.MemberRoster != null ? own.MemberRoster.TotalRegulars : 0;
                                if (on && own.IsLordParty && own.PaymentLimit != 0) own.SetWagePaymentLimit(0);   // bez rekrutow - BK rozwiaze ja w majatku
                            }
                        var mp = h.PartyBelongedTo;
                        if (mp != null && !h.IsPrisoner && mp.LeaderHero != h && mp.ActualClan != c)
                        {
                            // w sluzbie w druzynie innego rodu: zold za dobe, potem zostaje (armia krolestwa w wojnie) albo wraca do majatku
                            var k = c.Kingdom;
                            bool w = false;
                            if (k != null && !war.TryGetValue(k, out w)) { w = KingdomTreasury.AtWar(k); war[k] = w; }
                            bool stay = on && w && mp.IsActive && !mp.IsDisbanding && mp.Army != null && mp.MapFaction == h.MapFaction;
                            if (on) { try { Pay(h, mp, s); } catch (Exception e) { Stumble("Pay", e); } }
                            if (stay)
                            {
                                _dServing++;
                                var lc = estate != null ? estate.OwnerClan : null;
                                if (mp.IsMainParty) _dAtPlayer++; else if (lc != null && mp.ActualClan == lc) _dAtLord++; else _dAtArmy++;
                            }
                            else Home(h, estate, mp);
                            continue;
                        }
                        if (on && Free(h)) free.Add(kv);
                    }
                    catch (Exception e) { Stumble("Daily(rod)", e); }
                }
                // wezwania AI: krolestwo w wojnie; pan w armii krolestwa - do pana, inaczej do wodza najblizszej armii krolestwa (nie gracz)
                if (on)
                {
                    foreach (var kv in free)
                    {
                        try
                        {
                            var c = kv.Key; var estate = kv.Value; var h = c.Leader; var k = c.Kingdom;
                            if (k == null) continue;
                            bool w;
                            if (!war.TryGetValue(k, out w)) { w = KingdomTreasury.AtWar(k); war[k] = w; }
                            if (!w) continue;
                            _dFree++;
                            var lc = estate != null ? estate.OwnerClan : null;
                            if (lc == Clan.PlayerClan) continue;   // rycerz gracza jedzie tylko na wezwanie gracza (BK)
                            MobileParty target = null; bool atLord = false;
                            var lord = lc != null && lc != c && lc.Kingdom == k ? lc.Leader : null;
                            var lp = lord != null ? lord.PartyBelongedTo : null;
                            if (lp != null && lp.LeaderHero == lord && lp.Army != null && lp.Army.Kingdom == k && Fit(lp, k, c)) { target = lp; atLord = true; }
                            if (target == null)
                            {
                                Vec2 pos = estate != null ? estate.GetPosition2D : (h.CurrentSettlement != null ? h.CurrentSettlement.GetPosition2D : Vec2.Invalid);
                                float bd = float.MaxValue;
                                var armies = k.Armies;
                                if (armies != null)
                                    for (int i = 0; i < armies.Count; i++)
                                    {
                                        var lead = armies[i] != null ? armies[i].LeaderParty : null;
                                        if (!Fit(lead, k, c)) continue;
                                        float d = pos.IsValid ? pos.DistanceSquared(lead.GetPosition2D) : 0f;
                                        if (target == null || d < bd) { target = lead; bd = d; }
                                    }
                            }
                            if (target == null) { _dFreeNoArmy++; continue; }
                            if (!Join(h, target)) continue;
                            if (atLord) _dCalledLord++; else _dCalledArmy++;
                            _dServing++; if (atLord) _dAtLord++; else _dAtArmy++;
                        }
                        catch (Exception e) { Stumble("Daily(wezwanie)", e); }
                    }
                    // armie swiata z rycerzem w ktorejs druzynie (test: "rycerze w druzynach panow > 0 przy kazdej armii")
                    try
                    {
                        foreach (var k in Kingdom.All)
                        {
                            if (k == null || k.IsEliminated || k.Armies == null) continue;
                            foreach (var a in k.Armies)
                            {
                                if (a == null || a.LeaderParty == null) continue;
                                _dArmies++;
                                bool any = HasKnight(a.LeaderParty);
                                if (!any && a.Parties != null) foreach (var p in a.Parties) if (p != a.LeaderParty && HasKnight(p)) { any = true; break; }
                                if (any) _dArmiesWithKnight++;
                            }
                        }
                    }
                    catch (Exception e) { Stumble("Daily(armie)", e); }
                }
                // gracz: zold rycerzy zbiorczo co 7 dob (bez komunikatu codziennie)
                if (_weekStart < 0 || today < _weekStart) _weekStart = today;
                if (today - _weekStart >= 7)
                {
                    if (_weekSum > 0) Log.Player("Your knights' wages this past week: " + _weekSum + " denars (" + Math.Max(0, s.GentryKnightWage) + " a day for each knight riding in your party).");
                    _weekSum = 0; _weekStart = today;
                }
                Report(s, today, on);
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { LastPaid = _dPaid; }
        }

        private static bool HasKnight(MobileParty mp)
        {
            if (mp == null || mp.MemberRoster == null || mp.MemberRoster.TotalHeroes <= 1) return false;
            var r = mp.MemberRoster;
            for (int i = 0; i < r.Count; i++) { var ch = r.GetCharacterAtIndex(i); if (ch != null && ch.IsHero && IsServingKnight(ch.HeroObject, mp)) return true; }
            return false;
        }

        // ------------------------------------------------------------ latki BK (w kampanii, raz na proces)
        private static Harmony _harmony;
        private static bool _hooksTried;
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        internal static void SetHarmony(Harmony h) { _harmony = h; }

        internal static void EnsureHooks()
        {
            if (_hooksTried || _harmony == null || Campaign.Current == null) return;
            _hooksTried = true;
            var g = AccessTools.TypeByName("BannerKings.Behaviours.BKGentryBehavior");
            Wire("wezwanie rycerza (BK SummonGentry)", g != null ? AccessTools.Method(g, "SummonGentry") : null, nameof(SummonPrefix), null);
            Wire("rozwiazanie partii rycerza (BK FinishParty)", g != null ? AccessTools.Method(g, "FinishParty") : null, nameof(FinishPrefix), null);
            var sl = AccessTools.TypeByName("BannerKings.Behaviours.BKEstateAutoSlavePurchaseBehavior");
            Wire("majatek: niewolnicy (BK TryAutoBuyForEstate)", sl != null ? AccessTools.Method(sl, "TryAutoBuyForEstate") : null, nameof(SlavesPrefix), nameof(SlavesFinalizer));
            var sup = AccessTools.TypeByName("BannerKings.Behaviours.Estates.BKVillageSupplyAutoBehavior");
            Wire("majatek: zaopatrzenie wsi (BK RefillFromTownMarket)", sup != null ? AccessTools.Method(sup, "RefillFromTownMarket") : null, nameof(SupplyPrefix), nameof(SupplyFinalizer));
            Log.Info("Rycerze (179): latki BK - " + (_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-") + "; BRAK: " + (_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-")
                     + "; rozpoznanie rodow rycerzy: BK IsGentryClan " + (BkResolve() ? "TAK" : "BRAK (tylko id \"gentryClan_\" bez lenna)") + ".");
        }

        private static void Wire(string label, MethodBase m, string pre, string fin)
        {
            try
            {
                if (m == null) { _missing.Add(label); return; }
                _harmony.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(GentryService), pre) : null,
                                  finalizer: fin != null ? new HarmonyMethod(typeof(GentryService), fin) : null);
                _wired.Add(label);
            }
            catch (Exception e) { _missing.Add(label + " (blad: " + e.Message + ")"); }
        }

        /// <summary>BK wezwanie choragwi: rycerz zamiast wystawiac partie z ludzi majatku jedzie w druzynie pana (w tej armii) albo wzywajacego.</summary>
        public static bool SummonPrefix(Clan __0, Army __1)
        {
            try
            {
                if (!On || __0 == null || __1 == null) return true;
                Settlement estate;
                if (!IsKnightClanNow(__0, out estate)) return true;   // nie rod rycerza - BK jak dotad
                _dSummon++;
                var h = __0.Leader; var lead = __1.LeaderParty;
                if (!Free(h) || lead == null || !lead.IsActive || lead.MapEvent != null) { _dSummonSkip++; return false; }
                MobileParty target = null;
                var lc = estate != null ? estate.OwnerClan : null;
                var lord = lc != null && lc != __0 ? lc.Leader : null;
                var lp = lord != null ? lord.PartyBelongedTo : null;
                if (lp != null && lp.LeaderHero == lord && lp.IsActive && lp.Army == __1 && lp.MapEvent == null && !lp.IsDisbanding) { target = lp; _dSummonLord++; }
                else { target = lead; _dSummonLead++; }
                if (!Join(h, target)) { _dSummonSkip++; return false; }
                if (target.IsMainParty)
                    Log.Player(h.Name + " of " + __0.Name + " rides with you as a knight of your banner - " + Math.Max(0, Settings.Current.GentryKnightWage) + " denars a day while your army is in the field.");
                return false;
            }
            catch (Exception e) { Stumble("SummonPrefix", e); return true; }   // blad - BK jak dotad
        }

        /// <summary>BK rozwiazuje stara partie rycerza w majatku (DestroyPartyAction czysci liste ludzi) - zolnierze najpierw do ludnosci BK wsi majatku.</summary>
        public static void FinishPrefix(WarPartyComponent __0, object __1)
        {
            try
            {
                if (!On || __0 == null) return;
                var mp = __0.MobileParty;
                if (mp == null || !mp.IsActive) return;
                _dFinish++;
                var v = EstateSettlement(__1);
                if (v == null || !v.IsVillage) v = mp.CurrentSettlement != null && mp.CurrentSettlement.IsVillage ? mp.CurrentSettlement : null;
                var r = mp.MemberRoster;
                if (r != null)
                    for (int i = r.Count - 1; i >= 0; i--)
                    {
                        var el = r.GetElementCopyAtIndex(i); var ch = el.Character;
                        if (ch == null || ch.IsHero || el.Number <= 0) continue;
                        if (v != null && ClanBudget.ToVillagePop(v, ch, el.Number)) { r.AddToCounts(ch, -el.Number, false, -el.WoundedNumber); _dFinishMen += el.Number; }
                        else _dFinishLost += el.Number;
                    }
                _dFinishPris += mp.PrisonRoster != null ? mp.PrisonRoster.TotalRegulars : 0;
                try { _dFinishShips += mp.Ships != null ? mp.Ships.Count : 0; } catch { }
            }
            catch (Exception e) { Stumble("FinishPrefix", e); }
        }

        // ---- GentryEstateSpendCap: zakupy majatkow BK z przydzialu sprzetu 166, nie ponizej podlogi rodziny
        private static bool EstateGate(Hero owner)
        {
            var s = Settings.Current;
            if (!On || s == null || !s.GentryEstateSpendCap || owner == null || owner.Clan == null || owner.Clan == Clan.PlayerClan) return true;
            int cap;
            if (!ClanBudget.GearCap(owner.Clan, out cap)) return true;   // rod bez budzetu - jak dotad
            if (cap > 0 && owner.Gold > Math.Max(0, s.FamilyPurseFloor)) return true;
            _dEstateBlock++;
            return false;
        }

        private static void EstateSpent(Hero owner, int before)
        {
            if (owner == null || owner.Clan == null) return;
            int d = before - owner.Gold;
            if (d <= 0) return;
            ClanBudget.GearSpent(owner.Clan, d);
            _dEstateSpent += d;
        }

        public static bool SlavesPrefix(object __0, out int __state)
        {
            __state = int.MinValue;
            try
            {
                var o = EstateOwner(__0);
                if (!EstateGate(o)) return false;
                if (o != null) __state = o.Gold;
                return true;
            }
            catch (Exception e) { Stumble("SlavesPrefix", e); return true; }
        }

        public static Exception SlavesFinalizer(Exception __exception, object __0, int __state)
        {
            try { if (__state != int.MinValue) EstateSpent(EstateOwner(__0), __state); } catch (Exception e) { Stumble("SlavesFinalizer", e); }
            return __exception;
        }

        public static bool SupplyPrefix(Hero __0, out int __state)
        {
            __state = int.MinValue;
            try
            {
                if (!EstateGate(__0)) return false;
                if (__0 != null) __state = __0.Gold;
                return true;
            }
            catch (Exception e) { Stumble("SupplyPrefix", e); return true; }
        }

        public static Exception SupplyFinalizer(Exception __exception, Hero __0, int __state)
        {
            try { if (__state != int.MinValue) EstateSpent(__0, __state); } catch (Exception e) { Stumble("SupplyFinalizer", e); }
            return __exception;
        }

        // ------------------------------------------------------------ linia "Rycerze (179)"
        private static void Report(Settings s, int today, bool on)
        {
            if (!s.LogEnabled || (!on && _dHome == 0 && _dWait == 0)) return;   // wylaczone: linia tylko, gdy ktos wracal do domu
            var sb = new StringBuilder(900);
            sb.Append("Rycerze (179): dzien ").Append(today).Append(on ? "" : " (WYLACZONE - rycerze w sluzbie wracaja do domu)")
              .Append(" | rody rycerzy (BK gentry bez lenna) ").Append(_dClans)
              .Append(", w sluzbie ").Append(_dServing).Append(" (w druzynie pana ").Append(_dAtLord).Append(", u wodza armii ").Append(_dAtArmy).Append(", u gracza ").Append(_dAtPlayer).Append(')')
              .Append(", wolni w krolestwach w wojnie ").Append(_dFree).Append(" (bez armii w krolestwie ").Append(_dFreeNoArmy).Append(')')
              .Append(" | wezwani dzis: pan w armii ").Append(_dCalledLord).Append(", wodz armii ").Append(_dCalledArmy)
              .Append("; wezwania BK (od wczoraj) ").Append(_dSummon).Append(" (do pana ").Append(_dSummonLord).Append(", do wzywajacego ").Append(_dSummonLead).Append(", rycerz niedostepny ").Append(_dSummonSkip).Append(')')
              .Append(" | wrocili do majatku ").Append(_dHome).Append(", czeka (bitwa) ").Append(_dWait)
              .Append(" | armie ").Append(_dArmies).Append(", z rycerzem ").Append(_dArmiesWithKnight)
              .Append(" | zold rycerzy ").Append(Math.Max(0, s.GentryKnightWage)).Append(" zl: nalezny ").Append(_dDue).Append(", zaplacony ").Append(_dPaid)
              .Append(" (gracz ").Append(_dPlayerPaid).Append("), pan bez zlota ").Append(_dUnpaidN).Append(", Straz bez zoldu ").Append(_dWatch)
              .Append(" | wlasne partie rycerzy ").Append(_dOwnParties).Append(" (ludzi ").Append(_dOwnMen).Append(", limit zoldu 0), nowe partie zablokowane ").Append(_dSpawnBlock)
              .Append(", rozwiazane przez BK ").Append(_dFinish).Append(" (ludzie do wsi majatku ").Append(_dFinishMen).Append(", bez wsi z danymi BK ").Append(_dFinishLost)
              .Append(", jency w rozwiazanych ").Append(_dFinishPris).Append(", statki ").Append(_dFinishShips).Append(')')
              .Append(" | majatki BK (przydzial sprzetu 166").Append(s.GentryEstateSpendCap ? "" : " - WYLACZONE").Append("): zakupy wstrzymane ").Append(_dEstateBlock).Append(", wydane ").Append(_dEstateSpent)
              .Append(_stumbles > 0 ? " | potkniecia " + _stumbles : "").Append('.');
            Log.Info(sb.ToString());
            _dSpawnBlock = _dSummon = _dSummonLord = _dSummonLead = _dSummonSkip = 0;
            _dFinish = _dFinishMen = _dFinishLost = _dFinishPris = _dFinishShips = 0; _dEstateBlock = 0; _dEstateSpent = 0;
        }
    }
}
