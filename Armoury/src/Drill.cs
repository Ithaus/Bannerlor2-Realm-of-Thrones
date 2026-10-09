using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// MUSZTRA (docs/PROJEKT-MUSZTRA-2026-10-09.md, wersja 2 po krytyce; audyt 13 Z14a, Z14b; decyzje Jeffa 09.10 03:45 pkt 1-2, 04:00 pkt 1-2).
    /// Doswiadczenie oddzialu partii lorda na czlowieka na dobe: XP = (B x L x D x S + P) x A, a przy glodzie albo dlugu snu XP = 0 (takze perki - "wcale").
    ///  B - baza gry (10 + 2 x tier; glowa rodu AI 15 + 3 x tier; gracz 10 + 2 x tier - tabela Jeffa); w bitwie 0 jak w grze.
    ///  L - dowodca: Przywodztwo / 170 w granicach 0.5-1.5 (bez dowodcy 0.5).
    ///  D - dzien: 1.5 postoj (mniej niz 4 godziny ruchu z 24), 0.9 marsz. Godzina postoju = definicja odpoczynku ksiegi snu (RestHour: osada, oboz
    ///      obleznikow, ruch < 0.35 jedn./h - ta sama co NightRest.OnHourly i T10 R2); godzina niezaobserwowana = ruch (wczytanie nie daje postoju).
    ///  S - zapas do cwiczen: 1 + 0.10 x uB x kB + 0.10 x uZ x kZ; u = zapas / pelny (pelny = sztuka na 3 ludzi), k = 1.5 z perkiem kwatermistrza
    ///      (Giving Hands - bron, Paid in Promise - zbroja).
    ///  P - perki gry i BK w treningu (wynik modelu ponad baze); A - udzial uzbrojonych z 171 (ArmsDrill).
    /// Z14a (DrillLaw): partie, ktorym gra nie daje bazy (gracz, rod gracza, lordowie w armii gracza) - cala regula. AI poza Z14a: B x [L x D przy
    /// DrillLawAi] x [S przy DrillStockAi] + P; zero przy glodzie/snie tylko przy DrillLawAi. Zapas gracza: sprzet wyrzucony na zwyklym ekranie
    /// ekwipunku, zostawiony na ekranie lupu gry i trofea Spoils zostawione przy "Leave" (do 2 x pelny na grupe) + nadwyzka ludzi w zbrojowni DTE;
    /// AI: nadwyzka zbrojowni ponad komplet + uzbrojenie w taborze. Zapas zuzywa sie (sztuka w uzyciu sluzy 200 dni cwiczen), zlom (polowa rudy
    /// sztuki) odkupuja kowale miasta przy wizycie, zaplata dla wlasciciela (ludzie / lord); zapasu nie da sie wyjac i nie wraca do sakw.
    /// Liczniki tylko w ticku treningu partii (MobilePartyTrainingBehavior.OnDailyTickParty), linie dnia o polnocy.
    /// </summary>
    internal static class Drill
    {
        // ------------------------------------------------------------ stale (projekt rozdz. 2)
        private const float RestDay = 1.5f, MarchDay = 0.9f, LeadNorm = 170f, LeadMin = 0.5f, LeadMax = 1.5f;
        private const float StockBonus = 0.10f, PerkMult = 1.5f, WearDays = 200f;
        internal const float RestStep = 0.35f;            // ten sam prog co ksiega snu (NightRest.OnHourly) i T10 R2
        private const int RestBelowHours = 4, MenPerPiece = 3, IntakeSets = 2, AllHours = 0xFFFFFF, FeedPieces = 8;
        private const int GW = 0, GA = 1;                 // grupy zapasu: bron (z tarczami), zbroje
        internal const int SrcDiscard = 0, SrcLoot = 1, SrcTrophies = 2, SrcAutotest = 3;
        private static readonly string[] SrcName = { "wyrzucone", "lup", "trofea", "autotest" };

        /// <summary>Hak T10: dlug snu partii AI (0..3). Domyslnie 0; wpina go ten, kto scala drugi (PROJEKT-MUSZTRA rozdz. 8, PROJEKT-T10 dopisek).
        /// Gracz i partie doczepione do jego armii biora NightRest.Debt bez haka.</summary>
        internal static Func<MobileParty, int> SleepDebtOf = mp => 0;

        internal static bool StockOn { get { var s = Settings.Current; return s != null && s.DrillStock; } }

        // ------------------------------------------------------------ stan
        private sealed class Track { internal Vec2 Pos; internal long Stamp = -1; internal int Mask = AllHours; }
        private static readonly Dictionary<MobileParty, Track> _tr = new Dictionary<MobileParty, Track>();
        private static int _pendingMainMask = -1;

        private sealed class Acc { internal float WW, WA, OreMen, OreLord; }   // liczniki zuzycia (bron, zbroje) i zlom czekajacy na kowali (sztuki ludzi / lorda)
        private static readonly Dictionary<string, Acc> _acc = new Dictionary<string, Acc>();
        private static readonly ItemRoster _stock = new ItemRoster();         // zapas od gracza (wlasnosc ludzi)

        internal sealed class Ctx
        {
            internal MobileParty Party; internal double At;
            internal bool Main, NoBase, Rest, Hungry, Sleepless, Zero, StockOn, ArmsGate, PerkW, PerkA, Counted;
            internal int Men, Lead = -1, Moved, Debt, Full, GivenW, GivenA, SurW, SurA, BagW, BagA;
            internal float L = LeadMin, D = MarchDay, S = 1f;
            internal int StockW { get { return GivenW + SurW + BagW; } }
            internal int StockA { get { return GivenA + SurA + BagA; } }
        }
        private static Ctx _ctx;                           // kontekst partii w biezacym ticku (L, D, S liczone raz na partie)

        // element w toku (Shape -> Done; watek glowny)
        private static bool _e; private static Ctx _eC; private static float _eB, _eP, _ePre; private static int _eN; private static CharacterObject _eCh;
        internal static bool ElemArmsGate { get { return _e && _eC != null && _eC.ArmsGate; } }

        // tick treningu (latka MobilePartyTrainingBehavior.OnDailyTickParty)
        private static bool _tickHooked;
        private static MobileParty _tick;
        private static readonly Dictionary<CharacterObject, int> _room = new Dictionary<CharacterObject, int>();
        private static long _xpBefore;

        // ekrany zapasu
        private static int _opening;                       // 0 - nic, 1 + Src - otwierany ekran z bialej listy
        private static InventoryLogic _screen; private static int _screenKind;
        private static bool _hookDiscard, _hookLoot, _hookInit;

        // autotest i zapis
        private static int _autotest = -1, _dailyN;
        private static bool _fed;
        private static string _pendingStock, _importNote;
        private static int _importRejected;

        // ------------------------------------------------------------ liczniki doby
        private sealed class PlayerRec
        {
            internal Ctx C; internal int Day = -1;
            internal double B, P, Pre, Fin; internal long Computed, Accepted = -1, Cut;
        }
        private static PlayerRec _pr, _prLast;
        private static readonly int[] _in = new int[4];
        private static int _noRoom, _pWornW, _pWornA, _pWornGiven, _pNoMetal, _pSoldU, _pSoldGold, _givenYday = -1;
        private static float _pOreAdd;
        private static int _cParties, _cMen, _cRest; private static double _cXp, _cL;
        private static int _aParties, _aMen, _aNoLead, _aFullStock, _aNoStock, _aWornW, _aWornA, _aNoMetal, _aSoldU, _aSoldGold, _aSoldLord, _aSoldPurse;
        private static float _aOreAdd, _oreLost;
        private static double _aW, _aWL, _aWD, _aWS, _aWLD, _aWLDS, _aWRest, _aWMarch, _aWHungry, _aWSleep, _aGame, _aRule;
        private static readonly double[] _aWT = new double[3];   // razem przy progu postoju 4 / 8 / 12 h
        private static readonly int[] Thresholds = { 4, 8, 12 };
        private static readonly int[] _hb = new int[4];          // godziny ruchu w dobie: 0 / 1-3 / 4-11 / 12+
        private static readonly List<int> _leads = new List<int>();
        private sealed class KAcc { internal string Name; internal int PartyDays; internal double W, WLD, WS; internal readonly int[] Hb = new int[4]; }
        private static readonly Dictionary<string, KAcc> _k = new Dictionary<string, KAcc>();
        private static int _stumbles, _errDay = -1;
        private static readonly HashSet<string> _errWhere = new HashSet<string>();
        private static long _ticks;

        internal static void Reset()
        {
            _tr.Clear(); _pendingMainMask = -1; _acc.Clear(); _stock.Clear(); _ctx = null; _e = false; _eC = null; _tick = null; _room.Clear();
            _screen = null; _opening = 0; _dailyN = 0; _fed = false; _pendingStock = null; _importNote = null; _importRejected = 0;
            _pr = null; _prLast = null; _givenYday = -1; ClearDay(); _k.Clear(); _stumbles = 0; _errDay = -1; _errWhere.Clear();
            _ore = null;   // przedmioty gry sa tworzone na nowo przy kazdej grze - nie trzymac obiektu z poprzedniej kampanii
        }

        private static void ClearDay()
        {
            Array.Clear(_in, 0, _in.Length); _noRoom = _pWornW = _pWornA = _pWornGiven = _pNoMetal = _pSoldU = _pSoldGold = 0; _pOreAdd = 0f;
            _cParties = _cMen = _cRest = 0; _cXp = _cL = 0;
            _aParties = _aMen = _aNoLead = _aFullStock = _aNoStock = _aWornW = _aWornA = _aNoMetal = _aSoldU = _aSoldGold = _aSoldLord = _aSoldPurse = 0;
            _aOreAdd = 0f; _oreLost = 0f;
            _aW = _aWL = _aWD = _aWS = _aWLD = _aWLDS = _aWRest = _aWMarch = _aWHungry = _aWSleep = _aGame = _aRule = 0;
            Array.Clear(_aWT, 0, _aWT.Length); Array.Clear(_hb, 0, _hb.Length); _leads.Clear(); _ticks = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try
            {
                int d = (int)CampaignTime.Now.ToDays;
                if (d != _errDay) { _errDay = d; _errWhere.Clear(); }
                if (_errWhere.Add(where)) Log.Error("Drill." + where, e);
            }
            catch { }
        }

        private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
        private static string F2(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        private static string F3(double v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }
        private static string F1(double v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }
        private static int Pop(int m) { int n = 0; while (m != 0) { m &= m - 1; n++; } return n; }

        // ------------------------------------------------------------ grupy i sztuki zapasu
        private static int GroupOf(ItemObject it)
        {
            if (it == null) return -1;
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm:
                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow:
                case ItemObject.ItemTypeEnum.Thrown:
                case ItemObject.ItemTypeEnum.Shield:
                    return GW;
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.HeadArmor:
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                    return GA;
            }
            return -1;
        }

        private static readonly ItemObject.ItemTypeEnum[] StockTypes =
        {
            ItemObject.ItemTypeEnum.OneHandedWeapon, ItemObject.ItemTypeEnum.TwoHandedWeapon, ItemObject.ItemTypeEnum.Polearm, ItemObject.ItemTypeEnum.Bow,
            ItemObject.ItemTypeEnum.Crossbow, ItemObject.ItemTypeEnum.Thrown, ItemObject.ItemTypeEnum.Shield,
            ItemObject.ItemTypeEnum.BodyArmor, ItemObject.ItemTypeEnum.HeadArmor, ItemObject.ItemTypeEnum.LegArmor, ItemObject.ItemTypeEnum.HandArmor
        };

        /// <summary>Sztuka do zapasu: bron/zbroja z grup, bez unikatow i sztuk zakazanych w bitwie (ArmsPricing.IsUnique - z QuartermasterLaw.BarredInBattle,
        /// pamiec na przedmiot), bez wyrobow gracza (osobne obiekty zapisu).</summary>
        private static bool Eligible(ItemObject it)
        {
            if (GroupOf(it) < 0) return false;
            try { return !it.IsCraftedByPlayer && !ArmsPricing.IsUnique(it); }
            catch { return false; }
        }

        private static int Men(MobileParty mp)
        {
            try { var r = mp != null ? mp.MemberRoster : null; return r != null ? Math.Max(0, r.TotalManCount - r.TotalHeroes) : 0; }
            catch { return 0; }
        }

        private static int Full(int men) { return men > 0 ? (men + MenPerPiece - 1) / MenPerPiece : 0; }

        private static void GivenCounts(out int w, out int a)
        {
            w = 0; a = 0;
            for (int i = 0; i < _stock.Count; i++)
            {
                var el = _stock.GetElementCopyAtIndex(i);
                if (el.Amount <= 0) continue;
                int g = GroupOf(el.EquipmentElement.Item);
                if (g == GW) w += el.Amount; else if (g == GA) a += el.Amount;
            }
        }

        private static Acc AccOf(MobileParty mp, bool create)
        {
            if (mp == null || mp.StringId == null) return null;
            Acc a;
            if (!_acc.TryGetValue(mp.StringId, out a) && create) { a = new Acc(); _acc[mp.StringId] = a; }
            return a;
        }

        // ------------------------------------------------------------ godziny ruchu (maska 24 bitow: 1 = godzina ruchu)
        /// <summary>Godzina odpoczynku - ta sama definicja co ksiega snu gracza (NightRest.OnHourly) i T10 R2: osada, oboz obleznikow albo ruch
        /// ponizej 0.35 jedn. od poprzedniej godziny.</summary>
        internal static bool RestHour(MobileParty mp, float step)
        {
            return mp == null || mp.CurrentSettlement != null || mp.BesiegerCamp != null || step < RestStep;
        }

        internal static void Hourly()
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (Campaign.Current == null) return;
                long now = (long)Math.Floor(CampaignTime.Now.ToHours);
                var main = MobileParty.MainParty;
                bool mainSeen = false;
                var all = MobileParty.AllLordParties;
                if (all != null)
                    for (int i = 0; i < all.Count; i++) { var mp = all[i]; if (mp == main) mainSeen = true; Observe(mp, now); }
                if (!mainSeen && main != null) Observe(main, now);
                if (now % 24 == 0)
                {
                    var dead = new List<MobileParty>();
                    foreach (var kv in _tr) if (kv.Key == null || now - kv.Value.Stamp > 48) dead.Add(kv.Key);
                    foreach (var mp in dead) _tr.Remove(mp);
                }
            }
            catch (Exception e) { Stumble("Hourly", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        private static void Observe(MobileParty mp, long now)
        {
            try
            {
                if (mp == null || !mp.IsActive) return;
                var pos = mp.GetPosition2D;
                Track t;
                if (!_tr.TryGetValue(mp, out t))
                {
                    t = new Track();   // pierwsza obserwacja: cala doba jako ruch (bez darmowego postoju)
                    if (mp == MobileParty.MainParty && _pendingMainMask >= 0) { t.Mask = _pendingMainMask & AllHours; _pendingMainMask = -1; }
                    t.Pos = pos; t.Stamp = now; _tr[mp] = t;
                    return;
                }
                long gap = now - t.Stamp;
                if (gap <= 0) return;
                if (gap > 1) { int k = (int)Math.Min(24, gap - 1); t.Mask = ((t.Mask << k) | ((1 << k) - 1)) & AllHours; }   // przegapione godziny = ruch
                bool rest = RestHour(mp, pos.Distance(t.Pos));
                t.Mask = ((t.Mask << 1) | (rest ? 0 : 1)) & AllHours;
                t.Pos = pos; t.Stamp = now;
            }
            catch (Exception e) { Stumble("Observe", e); }
        }

        internal static int MovedHours(MobileParty mp)
        {
            Track t;
            return mp != null && _tr.TryGetValue(mp, out t) ? Pop(t.Mask) : 24;
        }

        // ------------------------------------------------------------ kontekst partii (raz na tick)
        /// <summary>Partie, ktorym gra nie daje bazy treningu (DefaultPartyTrainingModel.cs:21): armia gracza albo wlasciciel z rodu gracza.</summary>
        internal static bool NoGameBase(MobileParty mp)
        {
            try
            {
                if (mp == null) return false;
                if (mp.Army != null && mp.Army.LeaderParty == MobileParty.MainParty) return true;
                var o = mp.Party != null ? mp.Party.Owner : null;
                return o != null && o.Clan == Clan.PlayerClan;
            }
            catch { return false; }
        }

        private static float BaseOf(MobileParty mp, CharacterObject ch)
        {
            int t = ch.Tier;
            if (mp != MobileParty.MainParty && mp.LeaderHero != null && mp.ActualClan != null && mp.LeaderHero == mp.ActualClan.Leader) return 15f + 3f * t;
            return 10f + 2f * t;
        }

        private static int SleepDebt(MobileParty mp, Settings s)
        {
            try
            {
                if (mp == MobileParty.MainParty || mp.AttachedTo == MobileParty.MainParty) return s.NightRestEnabled ? NightRest.Debt : 0;   // krytyka 5: armia gracza idzie z nim noca
                var f = SleepDebtOf;
                return f != null ? f(mp) : 0;
            }
            catch { return 0; }
        }

        private static Ctx CtxOf(MobileParty mp, bool noBase, Settings s)
        {
            double now = CampaignTime.Now.ToHours;
            if (_ctx != null && _ctx.Party == mp && Math.Abs(_ctx.At - now) < 1e-6) return _ctx;
            long t0 = Stopwatch.GetTimestamp();
            var c = new Ctx { Party = mp, At = now, Main = mp == MobileParty.MainParty, NoBase = noBase };
            try
            {
                c.Men = Men(mp);
                c.Full = Full(c.Men);
                var h = mp.LeaderHero;
                if (h != null) { c.Lead = h.GetSkillValue(DefaultSkills.Leadership); c.L = Clamp(c.Lead / LeadNorm, LeadMin, LeadMax); }
                c.Moved = MovedHours(mp);
                c.Rest = c.Moved < RestBelowHours;
                c.Hungry = mp.Party != null && mp.Party.IsStarving;
                c.Debt = SleepDebt(mp, s);
                c.Sleepless = c.Debt >= 1;
                c.Zero = c.Hungry || c.Sleepless;
                c.D = c.Zero ? 0f : (c.Rest ? RestDay : MarchDay);
                c.StockOn = c.Main ? s.DrillStock : s.DrillStockAi;
                bool measure = c.StockOn || (!c.Main && !c.NoBase && s.DrillLog);   // AI przy wylaczonym zapasie: S tylko do linii pomiaru
                if (measure && c.Men > 0)
                {
                    if (c.Main) CountPlayerStock(c); else CountAiStock(c, mp);
                    c.PerkW = mp.HasPerk(DefaultPerks.Steward.GivingHands);
                    c.PerkA = mp.HasPerk(DefaultPerks.Steward.PaidInPromise, true);
                    float uB = Math.Min(1f, c.StockW / (float)c.Full), uZ = Math.Min(1f, c.StockA / (float)c.Full);
                    c.S = 1f + StockBonus * uB * (c.PerkW ? PerkMult : 1f) + StockBonus * uZ * (c.PerkA ? PerkMult : 1f);
                }
                c.ArmsGate = c.Main ? s.DrillNeedsArmsPlayer : (AiGear.On && s.PartyDrillNeedsArms);
            }
            catch (Exception e) { Stumble("CtxOf", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
            _ctx = c;
            return c;
        }

        // ------------------------------------------------------------ zapas: liczenie
        private static void CountPlayerStock(Ctx c)
        {
            int w, a; GivenCounts(out w, out a); c.GivenW = w; c.GivenA = a;
            var sur = PlayerSurplus();
            foreach (var kv in sur) { int g = TypeGroup((int)kv.Key); if (g == GW) c.SurW += kv.Value.Count; else if (g == GA) c.SurA += kv.Value.Count; }
        }

        private sealed class Sur { internal int Count; internal readonly List<ItemRosterElement> Cand = new List<ItemRosterElement>(); }

        /// <summary>Nadwyzka ludzi w zbrojowni DTE gracza wedlug typu (jak MenPurse.SellPlayerSurplus, bez progu SurplusKeepPercent): sztuki typu
        /// minus Twoje wklady minus potrzeba ludzi; kandydaci - sztuki ludzi z zapasu (bez unikatow, bez Twoich wkladow).</summary>
        private static Dictionary<ItemObject.ItemTypeEnum, Sur> PlayerSurplus()
        {
            var res = new Dictionary<ItemObject.ItemTypeEnum, Sur>();
            var armory = QuartermasterLaw.DteArmory();
            if (armory == null) return res;
            var needs = QuartermasterLaw.CountNeeds();
            foreach (var type in StockTypes)
            {
                int need = QuartermasterLaw.WornFor(type, needs);
                int have = 0;
                var ownIds = new Dictionary<string, int>();
                var cand = new List<ItemRosterElement>();
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (el.Amount <= 0 || !QuartermasterLaw.CountsAsKit(it, type)) continue;
                    have += el.Amount;
                    int c0; ownIds.TryGetValue(it.StringId, out c0); ownIds[it.StringId] = c0 + el.Amount;
                    if (ArmouryBehavior.StockOf(it.StringId) <= 0 && Eligible(it)) cand.Add(el);
                }
                foreach (var kv in ownIds) have -= Math.Min(kv.Value, Math.Max(0, ArmouryBehavior.StockOf(kv.Key)));
                int extra = have - need;
                if (extra <= 0 || cand.Count == 0) continue;
                int canTake = 0; foreach (var el in cand) canTake += el.Amount;
                var s = new Sur { Count = Math.Min(extra, canTake) };
                s.Cand.AddRange(cand);
                res[type] = s;
            }
            return res;
        }

        private sealed class AiSur { internal readonly Dictionary<int, int> ByType = new Dictionary<int, int>(); internal int Melee = -1; }

        /// <summary>Nadwyzka zbrojowni AI wedlug typu ponad komplet (jak MenPurse.SellAiSurplus, bez progu SurplusKeepPercent; bron biala grupa przy
        /// AiAnyMeleeWhenShort). Liczone tylko sztuki zdatne do zapasu.</summary>
        private static AiSur AiSurplus(MobileParty mp, Dictionary<ItemObject, int> arm)
        {
            var res = new AiSur();
            if (arm == null || arm.Count == 0) return res;
            var needT = new Dictionary<int, int>();
            foreach (var nk in AiGear.NeedBuckets(mp)) { int ty = nk.Key / 10; int v; needT.TryGetValue(ty, out v); needT[ty] = v + nk.Value; }
            var have = new Dictionary<int, int>();
            foreach (var kv in arm) { if (kv.Key == null || kv.Value <= 0 || !Eligible(kv.Key)) continue; int k = (int)kv.Key.ItemType; int n; have.TryGetValue(k, out n); have[k] = n + kv.Value; }
            bool group = AiGear.SubstituteMeleeOn;
            if (group) res.Melee = AiGear.MeleeGroupExtra(have, needT, 0f);
            foreach (var kv in have)
            {
                if (group && AiGear.Melee(kv.Key)) continue;
                int nd; needT.TryGetValue(kv.Key, out nd);
                int ex = kv.Value - nd;
                if (ex > 0) res.ByType[kv.Key] = ex;
            }
            return res;
        }

        private static Dictionary<ItemObject, int> ArmoryOf(MobileParty mp)
        {
            var d = AiGear.Armories(); Dictionary<ItemObject, int> a;
            return d != null && mp != null && d.TryGetValue(mp.Id, out a) ? a : null;
        }

        private static void CountAiStock(Ctx c, MobileParty mp)
        {
            var sur = AiSurplus(mp, ArmoryOf(mp));
            if (sur.Melee > 0) c.SurW += sur.Melee;
            foreach (var kv in sur.ByType)
            {
                int g = TypeGroup(kv.Key);
                if (g == GW) c.SurW += kv.Value; else if (g == GA) c.SurA += kv.Value;
            }
            var bag = mp.ItemRoster;
            if (bag != null)
                for (int i = 0; i < bag.Count; i++)
                {
                    var el = bag.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0 || !Eligible(el.EquipmentElement.Item)) continue;
                    if (GroupOf(el.EquipmentElement.Item) == GW) c.BagW += el.Amount; else c.BagA += el.Amount;
                }
        }

        private static int TypeGroup(int type)
        {
            switch ((ItemObject.ItemTypeEnum)type)
            {
                case ItemObject.ItemTypeEnum.OneHandedWeapon: case ItemObject.ItemTypeEnum.TwoHandedWeapon: case ItemObject.ItemTypeEnum.Polearm:
                case ItemObject.ItemTypeEnum.Bow: case ItemObject.ItemTypeEnum.Crossbow: case ItemObject.ItemTypeEnum.Thrown: case ItemObject.ItemTypeEnum.Shield:
                    return GW;
                case ItemObject.ItemTypeEnum.BodyArmor: case ItemObject.ItemTypeEnum.HeadArmor: case ItemObject.ItemTypeEnum.LegArmor: case ItemObject.ItemTypeEnum.HandArmor:
                    return GA;
            }
            return -1;
        }

        // ------------------------------------------------------------ wynik modelu (ArmsDrill.TrainingPostfix)
        /// <summary>
        /// Nowy wynik przed udzialem uzbrojonych: 0 - nie dotyczy (171 jak dotad), 1 - AI z baza gry (171 Gated dalej), 2 - Z14a (udzial wedlug
        /// ElemArmsGate). Wolane z zewnetrznego modelu (ArmsDrill pilnuje _tDepth).
        /// </summary>
        internal static int Shape(MobileParty mp, TroopRosterElement el, ref ExplainedNumber res)
        {
            _e = false;
            try
            {
                var s = Settings.Current;
                var ch = el.Character;
                if (s == null || mp == null || ch == null || ch.IsHero || ch.Culture == null || el.Number <= 0 || !mp.IsLordParty) return 0;
                bool noBase = NoGameBase(mp);
                if (noBase) { if (!s.DrillLaw || !mp.IsActive) return 0; }
                else if (!s.DrillStockAi && !s.DrillLawAi && !s.DrillLog) return 0;
                if (Undead.Party(mp)) return 0;
                var c = CtxOf(mp, noBase, s);
                float game = res.ResultNumber;
                float B = mp.MapEvent == null ? BaseOf(mp, ch) : 0f;   // w bitwie gra nie daje bazy
                float P, pre, sk = c.StockOn ? c.S : 1f;              // S tylko przy czynnym zapasie (gracz DrillStock, AI DrillStockAi)
                if (noBase)
                {
                    P = game;                                          // gra nie dala bazy - caly wynik to perki
                    pre = c.Zero ? 0f : B * c.L * c.D * sk + P;
                }
                else
                {
                    if (game < B - 0.01f) { B = Math.Max(0f, game); P = 0f; }   // model nie dal bazy (wyjatek BK) - nic nie dokladamy
                    else P = game - B;
                    float f = (s.DrillLawAi ? c.L * c.D : 1f) * sk;
                    pre = (s.DrillLawAi && c.Zero) ? 0f : B * f + P;
                }
                if (pre < 0f) pre = 0f;
                if (Math.Abs(pre - game) > 0.0001f) res = new ExplainedNumber(pre);   // opisy gubimy swiadomie (jak 171) - treningu nikt nie oglada
                _e = true; _eC = c; _eB = B; _eP = P; _ePre = pre; _eN = el.Number; _eCh = ch;
                return noBase ? 2 : 1;
            }
            catch (Exception e) { Stumble("Shape", e); _e = false; return 0; }
        }

        /// <summary>Po udziale uzbrojonych: liczniki doby (tylko w ticku treningu partii).</summary>
        internal static void Done(MobileParty mp, float final, float share)
        {
            if (!_e) return;
            _e = false;
            try
            {
                var c = _eC;
                if (c == null || c.Party != mp || (_tickHooked && _tick != mp)) return;
                int n = _eN;
                if (!c.Counted) { c.Counted = true; CountParty(c); }
                if (c.Main)
                {
                    var r = _pr;
                    if (r == null || r.C != c) return;
                    r.B += _eB * n; r.P += _eP * n; r.Pre += _ePre * n; r.Fin += final * n;
                    int add = TaleWorlds.Library.MathF.Round(final * n);   // ta sama liczba, ktora gra doda do rosteru (MobilePartyTrainingBehavior.cs:49)
                    r.Computed += add;
                    int room;
                    // gra: Xp = min(Xp + dodane, limit) - przyjete = min(dodane, limit - Xp); ponad limitem juz przed dodaniem (np. po stratach) wychodzi ujemne
                    if (_eCh != null && _room.TryGetValue(_eCh, out room)) { int acc = Math.Min(room, add); r.Cut += add - acc; _room[_eCh] = room - acc; }
                }
                else if (c.NoBase) _cXp += final * n;
                else
                {
                    _aGame += _eB * n; _aRule += (_ePre - _eP) * n;
                    if (_eB > 0f)
                    {
                        double w = _eB * n;
                        _aW += w; _aWL += w * c.L; _aWS += w * c.S;
                        float d = c.Zero ? 0f : (c.Rest ? RestDay : MarchDay);
                        _aWD += w * d; _aWLD += w * c.L * d; _aWLDS += w * c.L * d * c.S;
                        for (int i = 0; i < Thresholds.Length; i++) { float dt = c.Zero ? 0f : (c.Moved < Thresholds[i] ? RestDay : MarchDay); _aWT[i] += w * c.L * dt * c.S; }
                        if (c.Hungry) _aWHungry += w; else if (c.Sleepless) _aWSleep += w; else if (c.Rest) _aWRest += w; else _aWMarch += w;
                        var k = KOf(mp);
                        if (k != null) { k.W += w; k.WLD += w * c.L * d; k.WS += w * c.S; }
                    }
                }
            }
            catch (Exception e) { Stumble("Done", e); }
        }

        private static int Bucket(int moved) { return moved <= 0 ? 0 : (moved < 4 ? 1 : (moved < 12 ? 2 : 3)); }

        private static KAcc KOf(MobileParty mp)
        {
            try
            {
                var kd = mp.MapFaction as Kingdom;
                string id = kd != null ? kd.StringId : "-";
                KAcc k;
                if (!_k.TryGetValue(id, out k)) { k = new KAcc { Name = kd != null ? kd.Name.ToString() : "bez krolestwa" }; _k[id] = k; }
                return k;
            }
            catch { return null; }
        }

        private static void CountParty(Ctx c)
        {
            var mp = c.Party;
            if (c.Main) return;
            if (c.NoBase) { _cParties++; _cMen += c.Men; _cL += c.L; if (c.Rest && !c.Zero) _cRest++; return; }
            _aParties++; _aMen += c.Men;
            if (c.Lead >= 0) _leads.Add(c.Lead); else _aNoLead++;
            int b = Bucket(c.Moved); _hb[b]++;
            if (c.Full > 0) { if (c.StockW >= c.Full && c.StockA >= c.Full) _aFullStock++; else if (c.StockW + c.StockA == 0) _aNoStock++; }
            var k = KOf(mp);
            if (k != null) { k.PartyDays++; k.Hb[b]++; }
        }

        // ------------------------------------------------------------ tick treningu partii (latka gry)
        public static void TickPrefix(MobileParty mobileParty)
        {
            _tick = mobileParty;
            try
            {
                if (mobileParty == null || mobileParty != MobileParty.MainParty) return;
                // krytyka 6: limit XP oddzialu (PartyBase.OnXpChanged: Number x najwyzszy koszt awansu; t6 - 0) liczony PRZED dodaniem
                _room.Clear(); _xpBefore = 0;
                var r = mobileParty.MemberRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero) continue;
                    _xpBefore += el.Xp;
                    int max = 0;
                    var ups = el.Character.UpgradeTargets;
                    if (ups != null) for (int u = 0; u < ups.Length; u++) { int cost = el.Character.GetUpgradeXpCost(mobileParty.Party, u); if (cost > max) max = cost; }
                    _room[el.Character] = el.Number * max - el.Xp;   // moze byc ujemne (Xp ponad limit sprzed treningu - gra przytnie przy dodaniu)
                }
                var s = Settings.Current;
                if (s == null || !s.DrillLaw || !mobileParty.IsLordParty || Undead.Party(mobileParty)) { _pr = null; return; }
                _pr = new PlayerRec { Day = (int)CampaignTime.Now.ToDays };
                _ctx = null;
                _pr.C = CtxOf(mobileParty, NoGameBase(mobileParty), s);
            }
            catch (Exception e) { Stumble("TickPrefix", e); }
        }

        public static void TickPostfix(MobileParty mobileParty)
        {
            try
            {
                var s = Settings.Current;
                if (mobileParty == null || s == null) return;
                var c = _ctx;
                bool mine = c != null && c.Party == mobileParty && Math.Abs(c.At - CampaignTime.Now.ToHours) < 1e-6;
                if (mobileParty == MobileParty.MainParty && _pr != null)
                {
                    long after = 0;
                    var r = mobileParty.MemberRoster;
                    for (int i = 0; i < r.Count; i++) { var el = r.GetElementCopyAtIndex(i); if (el.Character != null && !el.Character.IsHero) after += el.Xp; }
                    _pr.Accepted = after - _xpBefore;
                    if (_pr.C != null && _pr.C.Counted) _prLast = _pr;   // trening gracza wedlug musztry odbyl sie
                    _pr = null;
                }
                if (mine && c.Counted) Wear(c, s);
                var st = mobileParty.CurrentSettlement;
                if (st != null && st.IsTown) SellScrap(mobileParty, st);
            }
            catch (Exception e) { Stumble("TickPostfix", e); }
        }

        public static Exception TickFinalizer(Exception __exception) { _tick = null; _room.Clear(); return __exception; }

        // ------------------------------------------------------------ zuzycie i zlom
        private static void Wear(Ctx c, Settings s)
        {
            if (!c.StockOn || c.Men <= 0 || c.Full <= 0) return;
            var mp = c.Party;
            bool law = c.NoBase || s.DrillLawAi;
            float dw = law ? c.D : 1f;                     // AI bez Z14b cwiczy plasko jak w grze
            if (dw <= 0f) return;
            long t0 = Stopwatch.GetTimestamp();
            var a = AccOf(mp, true);
            if (a == null) return;
            int inW = Math.Min(c.StockW, c.Full), inA = Math.Min(c.StockA, c.Full);
            a.WW += inW * dw / WearDays; a.WA += inA * dw / WearDays;
            int nW = (int)a.WW, nA = (int)a.WA;
            a.WW -= nW; a.WA -= nA;
            if (nW + nA <= 0) { _ticks += Stopwatch.GetTimestamp() - t0; return; }
            var gf = GoodsLedger.Begin(GoodsLedger.FDrill, mp);
            try
            {
                for (int g = 0; g < 2; g++)
                {
                    int n = g == GW ? nW : nA;
                    for (int k = 0; k < n; k++)
                    {
                        if (!(c.Main ? WearPlayerOne(g, a) : WearAiOne(mp, g, a))) break;
                        if (c.Main) { if (g == GW) _pWornW++; else _pWornA++; }
                        else { if (g == GW) _aWornW++; else _aWornA++; }
                    }
                }
            }
            catch (Exception e) { Stumble("Wear", e); }
            finally { GoodsLedger.End(gf); _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        private static float Yield() { var s = Settings.Current; return s != null ? MBMath.ClampFloat(s.OldStockScrapYield, 0f, 1f) : 0.5f; }

        /// <summary>Metal zuzytej sztuki -> zlom czekajacy na kowali (ruda = ruda sztuki x OldStockScrapYield, jak zlom 174).</summary>
        private static float Scrap(ItemObject it, Acc a, bool lords, bool player)
        {
            float ore = 0f;
            try { float d; var need = WorkshopLaw.Needs(it, out d); if (need != null && need.Length > 0 && need[0] > 0f) ore = need[0] * Yield(); }
            catch { ore = 0f; }
            if (ore <= 0f) { if (player) _pNoMetal++; else _aNoMetal++; return 0f; }
            if (lords) a.OreLord += ore; else a.OreMen += ore;
            if (player) _pOreAdd += ore; else _aOreAdd += ore;
            return ore;
        }

        /// <summary>Gracz: najgorsza sztuka grupy - najpierw zapas od gracza, potem nadwyzka ludzi w zbrojowni DTE (nigdy ponizej kompletu, nigdy wklady gracza).</summary>
        private static bool WearPlayerOne(int g, Acc a)
        {
            int best = -1; int bv = int.MaxValue;
            for (int i = 0; i < _stock.Count; i++)
            {
                var el = _stock.GetElementCopyAtIndex(i);
                if (el.Amount <= 0 || GroupOf(el.EquipmentElement.Item) != g) continue;
                int v = el.EquipmentElement.ItemValue;
                if (v < bv) { bv = v; best = i; }
            }
            if (best >= 0)
            {
                var el = _stock.GetElementCopyAtIndex(best);
                _stock.AddToCounts(el.EquipmentElement, -1);
                _pWornGiven++;
                Scrap(el.EquipmentElement.Item, a, false, true);
                return true;
            }
            var armory = QuartermasterLaw.DteArmory();
            if (armory == null) return false;
            ItemRosterElement pick = default(ItemRosterElement); bool found = false; bv = int.MaxValue;
            foreach (var kv in PlayerSurplus())
            {
                if (kv.Value.Count <= 0 || TypeGroup((int)kv.Key) != g) continue;
                foreach (var el in kv.Value.Cand)
                {
                    int v = el.EquipmentElement.ItemValue;
                    if (v < bv) { bv = v; pick = el; found = true; }
                }
            }
            if (!found) return false;
            armory.AddToCounts(pick.EquipmentElement, -1);
            Scrap(pick.EquipmentElement.Item, a, false, true);
            return true;
        }

        /// <summary>AI: najgorsza sztuka grupy - najpierw tabor (lup lorda), potem nadwyzka zbrojowni (sztuki ludzi; stan z AiWear).</summary>
        private static bool WearAiOne(MobileParty mp, int g, Acc a)
        {
            var bag = mp.ItemRoster;
            if (bag != null)
            {
                int best = -1; int bv = int.MaxValue;
                for (int i = 0; i < bag.Count; i++)
                {
                    var el = bag.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0 || GroupOf(el.EquipmentElement.Item) != g || !Eligible(el.EquipmentElement.Item)) continue;
                    int v = el.EquipmentElement.ItemValue;
                    if (v < bv) { bv = v; best = i; }
                }
                if (best >= 0)
                {
                    var el = bag.GetElementCopyAtIndex(best);
                    bag.AddToCounts(el.EquipmentElement, -1);
                    Scrap(el.EquipmentElement.Item, a, true, false);
                    return true;
                }
            }
            var arm = ArmoryOf(mp);
            if (arm == null || arm.Count == 0) return false;
            var sur = AiSurplus(mp, arm);
            ItemObject pick = null; int pv = int.MaxValue;
            foreach (var kv in arm)
            {
                var it = kv.Key;
                if (it == null || kv.Value <= 0 || GroupOf(it) != g || !Eligible(it)) continue;
                int ty = (int)it.ItemType;
                bool ok;
                if (sur.Melee >= 0 && AiGear.Melee(ty)) ok = sur.Melee > 0;
                else { int ex; ok = sur.ByType.TryGetValue(ty, out ex) && ex > 0; }
                if (!ok) continue;
                if (it.Value < pv) { pv = it.Value; pick = it; }
            }
            if (pick == null) return false;
            int cnt = arm[pick] - 1;
            if (cnt > 0) arm[pick] = cnt; else arm.Remove(pick);
            try { AiWear.TakeCondition(mp, pick); } catch { }   // zapis stanu sztuk AI zgodny ze zbrojownia
            Scrap(pick, a, false, false);
            return true;
        }

        private static ItemObject _ore;
        private static ItemObject Ore()
        {
            if (_ore == null) { try { _ore = MBObjectManager.Instance.GetObject<ItemObject>("iron"); } catch { } }
            return _ore;
        }

        internal static void OnEntered(MobileParty mp, Settlement st, Hero h)
        {
            try { if (mp != null && st != null && st.IsTown) SellScrap(mp, st); }
            catch (Exception e) { Stumble("OnEntered", e); }
        }

        /// <summary>Kowale miasta odkupuja zlom z cwiczen: cale ladunki rudy na polke (ramka FDrill, ksiega rudy), kasa miasta placi po cenie skupu,
        /// najwyzej tyle, ile ma. Czesc ludzi: gracz - sakiewka ludzi; AI - trzecia lordowi, reszta sakiewce; czesc lorda (tabor AI) - lordowi.</summary>
        private static void SellScrap(MobileParty mp, Settlement st)
        {
            var a = AccOf(mp, false);
            if (a == null || st == null || st.Town == null || st.ItemRoster == null) return;
            bool player = mp == MobileParty.MainParty;
            int menU = (int)a.OreMen, lordU = player ? 0 : (int)a.OreLord;
            if (menU + lordU <= 0) return;
            var ore = Ore();
            if (ore == null) return;
            Hero lord = player ? Hero.MainHero : mp.LeaderHero;
            if (lord == null || !lord.IsAlive) return;
            var s = Settings.Current;
            int unit = Math.Max(1, MenPurse.SellPrice(new EquipmentElement(ore), st, mp));
            int can = Math.Max(0, st.Town.Gold) / unit;
            lordU = Math.Min(lordU, can); menU = Math.Min(menU, can - lordU);
            int n = lordU + menU;
            if (n <= 0) return;
            var gf = GoodsLedger.Begin(GoodsLedger.FDrill, st.Town);
            try { st.ItemRoster.AddToCounts(ore, n); }
            finally { GoodsLedger.End(gf); }
            OreLedger.NoteDrillScrap(ore, n);
            int pay = unit * n, menPay = unit * menU, lordPay = unit * lordU;
            st.Town.ChangeGold(-pay);
            MoneyLedger.Note169(MoneyLedger.N169Surplus, st, -pay);   // paczka 169: linia kas (tylko licznik)
            if (player)
            {
                if (MenPurse.On) MenPurse.Add(mp, menPay); else Hero.MainHero.ChangeHeroGold(menPay);
                _pSoldU += n; _pSoldGold += pay;
                Log.Player("The smiths of " + st.Name + " bought " + n + " loads of scrap iron from your men's worn drill kit for " + pay + " denars - "
                           + (MenPurse.On ? "the coin went to the men's purse." : "the coin is yours."));
            }
            else
            {
                int third = MenPurse.On ? (int)Math.Round(menPay * MBMath.ClampFloat(s.LordLootThirdPercent, 0f, 100f) / 100f) : menPay;
                int toLord = lordPay + third;
                if (toLord > 0) lord.ChangeHeroGold(toLord);
                if (third > 0) ClanIncomeBook.NoteInflow(lord, third, ClanIncomeBook.KThird);   // paczka 169: D rodu (tylko licznik)
                if (menPay - third > 0) MenPurse.Add(mp, menPay - third);
                _aSoldU += n; _aSoldGold += pay; _aSoldLord += toLord; _aSoldPurse += Math.Max(0, menPay - third);
            }
            a.OreMen -= menU; a.OreLord -= lordU;
        }

        internal static void OnPartyDestroyed(MobileParty mp, PartyBase destroyer)
        {
            try
            {
                if (mp == null) return;
                _tr.Remove(mp);
                if (mp == MobileParty.MainParty || mp.StringId == null) return;
                Acc a;
                if (_acc.TryGetValue(mp.StringId, out a)) { _oreLost += a.OreMen + a.OreLord; _acc.Remove(mp.StringId); }
            }
            catch (Exception e) { Stumble("OnPartyDestroyed", e); }
        }

        // ------------------------------------------------------------ przyjecie do zapasu (ekrany 1-3)
        /// <summary>Przyjecie do zapasu gracza: bron i zbroje z listy, najtansze najpierw, do 2 x pelny na grupe; przyjete schodza z listy.</summary>
        internal static int Accept(ItemRoster from, int src)
        {
            try
            {
                var s = Settings.Current;
                var main = MobileParty.MainParty;
                if (s == null || !s.DrillStock || from == null || from.Count == 0 || main == null) return 0;
                var cand = new List<ItemRosterElement>();
                for (int i = 0; i < from.Count; i++)
                {
                    var el = from.GetElementCopyAtIndex(i);
                    if (el.Amount > 0 && Eligible(el.EquipmentElement.Item)) cand.Add(el);
                }
                if (cand.Count == 0) return 0;
                int men = Men(main), full = Full(men), cap = IntakeSets * full;
                int w, a; GivenCounts(out w, out a);
                var cur = new[] { w, a };
                cand.Sort((x, y) => x.EquipmentElement.ItemValue.CompareTo(y.EquipmentElement.ItemValue));
                int taken = 0, noRoom = 0;
                foreach (var el in cand)
                {
                    int g = GroupOf(el.EquipmentElement.Item);
                    int t = Math.Min(Math.Max(0, cap - cur[g]), el.Amount);
                    if (t > 0) { from.AddToCounts(el.EquipmentElement, -t); _stock.AddToCounts(el.EquipmentElement, t); cur[g] += t; taken += t; }
                    noRoom += el.Amount - t;
                }
                if (src >= 0 && src < _in.Length) _in[src] += taken;
                _noRoom += noRoom;
                string rest = src == SrcTrophies ? "stay on the field" : (src == SrcAutotest ? "go back to the baggage" : "are lost");
                if (men <= 0)
                    Log.Player("Your men took nothing into their drill stock - you have no soldiers to drill. " + noRoom + " pieces " + rest + ".", true);
                else
                    Log.Player("Your men took " + taken + " pieces into their drill stock (weapons " + cur[GW] + "/" + full + ", armour " + cur[GA] + "/" + full
                               + " - a full stock is one of each per three men, room for two)." + (noRoom > 0 ? " " + noRoom + " pieces " + rest + " - no room." : ""), noRoom > 0 && taken == 0);
                Log.Info("Musztra: zapas - przyjeto " + taken + " szt. (" + SrcName[Math.Max(0, Math.Min(SrcName.Length - 1, src))] + "), bez miejsca " + noRoom
                         + "; zapas od gracza: bron " + cur[GW] + ", zbroje " + cur[GA] + " (pelny " + full + ", limit " + cap + " na grupe).");
                return taken;
            }
            catch (Exception e) { Stumble("Accept", e); return 0; }
        }

        /// <summary>Spoils "Leave" (SpoilsSeal): trofea zostawione na polu - bron i zbroje do zapasu.</summary>
        internal static void AcceptTrophies(ItemRoster r) { Accept(r, SrcTrophies); }

        /// <summary>Podpowiedz Spoils "Leave" przy DrillStock: ile sztuk ludzie jeszcze przyjma.</summary>
        internal static string TrophyTip()
        {
            int room = 0;
            try
            {
                int full = Full(Men(MobileParty.MainParty)), w, a; GivenCounts(out w, out a);
                room = Math.Max(0, IntakeSets * full - w) + Math.Max(0, IntakeSets * full - a);
            }
            catch { }
            return "Remaining {COUNT} items: your men keep up to " + room + " pieces of arms and armour for their drill stock, the rest stay on the field.";
        }

        // ekran 1 (zwykly ekwipunek "Discard") i 2 (lup gry): prefiks + finalizer na metodzie otwierajacej, postfiks na InventoryLogic.Initialize
        public static void OpenDiscardPrefix() { _opening = 1 + SrcDiscard; }
        public static void OpenLootPrefix() { _opening = 1 + SrcLoot; }
        public static Exception OpenFinalizer(Exception __exception) { _opening = 0; return __exception; }
        public static void InitPostfix(InventoryLogic __instance) { if (_opening > 0) { _screen = __instance; _screenKind = _opening - 1; } }

        /// <summary>Zdarzenie gry OnItemsDiscardedByPlayer (InventoryLogic.DoneLogic): przyjecie tylko z ekranu z bialej listy - lewa lista tego ekranu.</summary>
        internal static void OnDiscarded(ItemRoster roster)
        {
            try
            {
                var logic = _screen; int kind = _screenKind;
                _screen = null;
                if (logic == null || roster == null) return;
                var left = logic.GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory) as ItemRoster;
                if (!ReferenceEquals(left, roster)) return;
                if (kind == SrcDiscard && Game.Current != null && Game.Current.CheatMode) return;   // tryb oszustw: lewa strona = 10 sztuk wszystkiego
                Accept(roster, kind);
            }
            catch (Exception e) { Stumble("OnDiscarded", e); }
        }

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            if (h == null) return;
            try
            {
                var m = AccessTools.Method(typeof(MobilePartyTrainingBehavior), "OnDailyTickParty", new[] { typeof(MobileParty) });
                if (m != null)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(Drill), nameof(TickPrefix)), postfix: new HarmonyMethod(typeof(Drill), nameof(TickPostfix)),
                            finalizer: new HarmonyMethod(typeof(Drill), nameof(TickFinalizer)));
                    _tickHooked = true;
                }
            }
            catch (Exception e) { Log.Error("Drill.ApplyAll(tick)", e); }
            try
            {
                MethodInfo init = null;
                foreach (var mi in typeof(InventoryLogic).GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (mi.Name != "Initialize") continue;
                    var ps = mi.GetParameters();
                    if (ps.Length > 3 && ps[1].ParameterType == typeof(ItemRoster) && ps[3].ParameterType == typeof(bool) && ps[3].Name == "isTrading") { init = mi; break; }
                }
                if (init != null) { h.Patch(init, postfix: new HarmonyMethod(typeof(Drill), nameof(InitPostfix))); _hookInit = true; }
                var fin = new HarmonyMethod(typeof(Drill), nameof(OpenFinalizer));
                var od = AccessTools.Method(typeof(Helpers.InventoryScreenHelper), "OpenInventoryPresentation");
                if (od != null && _hookInit) { h.Patch(od, prefix: new HarmonyMethod(typeof(Drill), nameof(OpenDiscardPrefix)), finalizer: fin); _hookDiscard = true; }
                var ol = AccessTools.Method(typeof(Helpers.InventoryScreenHelper), "OpenScreenAsLoot", new[] { typeof(Dictionary<PartyBase, ItemRoster>) });
                if (ol != null && _hookInit) { h.Patch(ol, prefix: new HarmonyMethod(typeof(Drill), nameof(OpenLootPrefix)), finalizer: fin); _hookLoot = true; }
            }
            catch (Exception e) { Log.Error("Drill.ApplyAll(ekrany)", e); }
            Log.Info("Drill: musztra - trening gry (tick partii) " + (_tickHooked ? "wpiety" : "NIE WPIETY (bez zuzycia zapasu i kontroli gracza)") + ", ekrany zapasu "
                     + ((_hookDiscard ? 1 : 0) + (_hookLoot ? 1 : 0)) + "/2 (zwykly ekwipunek " + (_hookDiscard ? "tak" : "NIE") + ", lup gry " + (_hookLoot ? "tak" : "NIE") + ").");
        }

        // ------------------------------------------------------------ start sesji: zapis, opisy perkow, linia startowa
        internal static void SessionStart()
        {
            var s = Settings.Current;
            if (s == null) return;
            string imp = ResolveImport();
            try { if (s.DrillStock) PerkTexts(); } catch (Exception e) { Stumble("PerkTexts", e); }
            try
            {
                // maska gracza z zapisu od razu (pierwszy trening po wczytaniu moze przyjsc przed pierwsza pelna godzina)
                var main = MobileParty.MainParty;
                if (main != null && _pendingMainMask >= 0 && !_tr.ContainsKey(main))
                {
                    _tr[main] = new Track { Pos = main.GetPosition2D, Stamp = (long)Math.Floor(CampaignTime.Now.ToHours), Mask = _pendingMainMask & AllHours };
                    _pendingMainMask = -1;
                }
            }
            catch (Exception e) { Stumble("SessionStart(maska)", e); }
            int w, a; GivenCounts(out w, out a);
            string spoils = SpoilsSeal.Present ? (SpoilsSeal.DrillLeaveWired ? "Spoils 1/1 (trofea przy Leave)" : "Spoils 0/1 - NIE WPIETE (trofea jak dotad)") : "Spoils nieobecny";
            Log.Info("Musztra: start - ekrany zapasu wpiete " + ((_hookDiscard ? 1 : 0) + (_hookLoot ? 1 : 0)) + "/2 + " + spoils + "; trening gry " + (_tickHooked ? "wpiety" : "NIE WPIETY")
                     + "; Z14a (Drill Law) " + On(s.DrillLaw) + ", bron gracza " + On(s.DrillNeedsArmsPlayer) + ", zapas gracza " + On(s.DrillStock) + ", zapas AI " + On(s.DrillStockAi)
                     + ", Z14b (Drill Law Ai) " + On(s.DrillLawAi) + "; stale: postoj x" + F2(RestDay) + " (ruch < " + RestBelowHours + " h z 24, godzina postoju: osada, oboz, < "
                     + F2(RestStep) + " jedn./h), marsz x" + F2(MarchDay) + ", dowodca Przywodztwo/" + (int)LeadNorm + " [" + F2(LeadMin) + "-" + F2(LeadMax) + "], zapas +"
                     + (int)(StockBonus * 100) + "% za grupe (perk x" + F2(PerkMult) + "), sztuka sluzy " + (int)WearDays + " dni cwiczen, zlom x" + F2(Yield())
                     + "; zapas od gracza: bron " + w + ", zbroje " + a + (imp != null ? "; " + imp : "") + ".");
        }

        private static string On(bool b) { return b ? "TAK" : "nie"; }

        private const string GivingHandsText = "Weapons and shields in your men's drill stock count half again for their daily drill.";
        private const string PaidInPromiseText = "Armour in your men's drill stock counts half again for their daily drill.";

        /// <summary>Nowe opisy czesci kwatermistrza obu perkow (druga polowa bez zmian): PrimaryDescription / SecondaryDescription i zmienne STR1/STR2 opisu.</summary>
        private static void PerkTexts()
        {
            int n = 0;
            var gh = DefaultPerks.Steward.GivingHands;
            var pp = DefaultPerks.Steward.PaidInPromise;
            var pPrim = AccessTools.Property(typeof(PerkObject), "PrimaryDescription");
            var pSec = AccessTools.Property(typeof(PerkObject), "SecondaryDescription");
            if (gh != null && pPrim != null)
            {
                var t = new TextObject(GivingHandsText);
                pPrim.SetValue(gh, t, null);
                if (gh.Description != null) gh.Description.SetTextVariable("STR1", t);
                n++;
            }
            if (pp != null && pSec != null)
            {
                var t = new TextObject(PaidInPromiseText);
                pSec.SetValue(pp, t, null);
                if (pp.Description != null) pp.Description.SetTextVariable("STR2", t);
                n++;
            }
            Log.Info("Musztra: opisy perkow kwatermistrza podmienione " + n + "/2 (Giving Hands - bron, Paid in Promise - zbroja w zapasie cwiczebnym).");
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            try
            {
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder("v1");
                int mask = -1; Track t;
                var main = MobileParty.MainParty;
                if (main != null && _tr.TryGetValue(main, out t)) mask = t.Mask;
                else if (_pendingMainMask >= 0) mask = _pendingMainMask;
                sb.Append("|M=").Append(mask).Append("|F=").Append(_fed ? 1 : 0).Append("|S=");
                int pcs = 0; bool first = true;
                for (int i = 0; i < _stock.Count; i++)
                {
                    var el = _stock.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (el.Amount <= 0 || it == null || !Safe(it.StringId)) continue;
                    var mod = el.EquipmentElement.ItemModifier;
                    string mid = mod != null && Safe(mod.StringId) ? mod.StringId : "";
                    if (!first) sb.Append(';');
                    first = false;
                    sb.Append(it.StringId).Append(',').Append(mid).Append(',').Append(el.Amount);
                    pcs += el.Amount;
                }
                sb.Append("|A=");
                first = true; float oreMain = 0f; int parties = 0;
                foreach (var kv in _acc)
                {
                    var a = kv.Value;
                    if (!Safe(kv.Key) || (a.WW <= 0f && a.WA <= 0f && a.OreMen <= 0f && a.OreLord <= 0f)) continue;
                    if (!first) sb.Append(';');
                    first = false;
                    sb.Append(kv.Key).Append(',').Append(a.WW.ToString("0.###", inv)).Append(',').Append(a.WA.ToString("0.###", inv)).Append(',')
                      .Append(a.OreMen.ToString("0.###", inv)).Append(',').Append(a.OreLord.ToString("0.###", inv));
                    parties++;
                    if (main != null && kv.Key == main.StringId) oreMain = a.OreMen;
                }
                int w, ar; GivenCounts(out w, out ar);
                Log.Info("Musztra: zapis - zapas od gracza " + pcs + " szt. (bron " + w + ", zbroje " + ar + "), godzin ruchu gracza " + (mask >= 0 ? Pop(mask).ToString() : "-")
                         + " z 24, zlom gracza czeka " + F2(oreMain) + " rudy, partii z licznikami " + parties + ", zasilenie autotestu " + (_fed ? "tak" : "nie") + ".");
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return null; }
        }

        private static bool Safe(string id) { return !string.IsNullOrEmpty(id) && id.IndexOfAny(new[] { ',', ';', '|', '=' }) < 0; }

        internal static void Import(string v)
        {
            try
            {
                _pendingStock = null; _importNote = null; _importRejected = 0;
                if (string.IsNullOrEmpty(v)) { _importNote = "z zapisu: brak klucza (stary zapis) - zapas pusty, godziny ruchu nieznane (ruch)"; return; }
                var inv = CultureInfo.InvariantCulture;
                foreach (var part in v.Split('|'))
                {
                    if (part.StartsWith("M=")) { int m; if (int.TryParse(part.Substring(2), NumberStyles.Integer, inv, out m) && m >= 0) _pendingMainMask = m & AllHours; }
                    else if (part.StartsWith("F=")) _fed = part.Substring(2) == "1";
                    else if (part.StartsWith("S=")) _pendingStock = part.Substring(2);
                    else if (part.StartsWith("A="))
                    {
                        foreach (var e in part.Substring(2).Split(';'))
                        {
                            var f = e.Split(',');
                            if (f.Length != 5 || f[0].Length == 0) continue;
                            float ww, wa, om, ol;
                            if (!float.TryParse(f[1], NumberStyles.Float, inv, out ww) || !float.TryParse(f[2], NumberStyles.Float, inv, out wa)
                                || !float.TryParse(f[3], NumberStyles.Float, inv, out om) || !float.TryParse(f[4], NumberStyles.Float, inv, out ol)) continue;
                            _acc[f[0]] = new Acc { WW = ww, WA = wa, OreMen = om, OreLord = ol };
                        }
                    }
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        /// <summary>Zapas z zapisu dopiero przy starcie sesji (przedmioty i modyfikatory pewnie wczytane); nieznane id pominiete i policzone.</summary>
        private static string ResolveImport()
        {
            string note = _importNote;
            _importNote = null;
            var raw = _pendingStock;
            _pendingStock = null;
            if (raw == null) return note;
            int pcs = 0;
            try
            {
                foreach (var e in raw.Split(';'))
                {
                    if (e.Length == 0) continue;
                    var f = e.Split(',');
                    int n;
                    if (f.Length != 3 || !int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n <= 0) { _importRejected++; continue; }
                    ItemObject it = null; ItemModifier mod = null;
                    try { it = MBObjectManager.Instance.GetObject<ItemObject>(f[0]); } catch { }
                    if (f[1].Length > 0) { try { mod = MBObjectManager.Instance.GetObject<ItemModifier>(f[1]); } catch { } }
                    if (it == null || !Eligible(it) || (f[1].Length > 0 && mod == null)) { _importRejected += n; continue; }
                    _stock.AddToCounts(new EquipmentElement(it, mod), n);
                    pcs += n;
                }
            }
            catch (Exception ex) { Stumble("ResolveImport", ex); }
            int w, a; GivenCounts(out w, out a);
            float oreMain = 0f; Acc am;
            var main = MobileParty.MainParty;
            if (main != null && _acc.TryGetValue(main.StringId ?? "", out am)) oreMain = am.OreMen;
            return "z zapisu: zapas " + pcs + " szt. (bron " + w + ", zbroje " + a + "), godzin ruchu gracza " + (_pendingMainMask >= 0 ? Pop(_pendingMainMask).ToString() : "-")
                   + " z 24, zlom gracza czeka " + F2(oreMain) + " rudy, partii z licznikami " + _acc.Count + ", zasilenie autotestu " + (_fed ? "tak" : "nie")
                   + ", odrzucono " + _importRejected + " szt.";
        }

        // ------------------------------------------------------------ autotest: zasilenie zapasu (krytyka 7)
        private static bool AutotestActive()
        {
            if (_autotest >= 0) return _autotest == 1;
            _autotest = 0;
            try
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "CrashScribe") continue;
                    Type t = asm.GetType("CrashScribe.Autotest", false);
                    FieldInfo f = t != null ? t.GetField("Active", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) : null;
                    if (f != null && f.FieldType == typeof(bool) && (bool)f.GetValue(null)) _autotest = 1;
                    break;
                }
            }
            catch { _autotest = 0; }
            return _autotest == 1;
        }

        /// <summary>Tylko w autotescie, raz na kampanie (flaga w zapisie): do 8 sztuk broni i 8 zbroi z taboru gracza, brakujace kupione najtaniej z polki
        /// miasta za zloto gracza (handel - kasa miasta dostaje zaplate); ta sama funkcja przyjecia co ekrany; czego zapas nie przyjmie - wraca do taboru.</summary>
        private static void AutotestFeed()
        {
            var s = Settings.Current;
            var main = MobileParty.MainParty;
            if (s == null || !s.DrillStock || main == null || _fed || !AutotestActive()) return;
            _fed = true;
            var tmp = new ItemRoster();
            int[] got = new int[2];
            int fromBag = 0, bought = 0, gold = 0;
            var bag = main.ItemRoster;
            var bagEls = new List<ItemRosterElement>();
            for (int i = 0; i < bag.Count; i++) { var el = bag.GetElementCopyAtIndex(i); if (el.Amount > 0 && Eligible(el.EquipmentElement.Item)) bagEls.Add(el); }
            foreach (var el in bagEls)
            {
                int g = GroupOf(el.EquipmentElement.Item);
                int t = Math.Min(FeedPieces - got[g], el.Amount);
                if (t <= 0) continue;
                bag.AddToCounts(el.EquipmentElement, -t); tmp.AddToCounts(el.EquipmentElement, t); got[g] += t; fromBag += t;
            }
            var st = main.CurrentSettlement;
            if (st != null && st.IsTown && st.Town != null && st.ItemRoster != null)
            {
                for (int g = 0; g < 2; g++)
                {
                    while (got[g] < FeedPieces)
                    {
                        int best = -1, bp = int.MaxValue;
                        var shelf = st.ItemRoster;
                        for (int i = 0; i < shelf.Count; i++)
                        {
                            var el = shelf.GetElementCopyAtIndex(i);
                            if (el.Amount <= 0 || GroupOf(el.EquipmentElement.Item) != g || !Eligible(el.EquipmentElement.Item)) continue;
                            int p = st.Town.MarketData.GetPrice(el.EquipmentElement, main, false, st.Party);
                            if (p > 0 && p < bp) { bp = p; best = i; }
                        }
                        if (best < 0 || Hero.MainHero.Gold < bp) break;
                        var pick = shelf.GetElementCopyAtIndex(best).EquipmentElement;
                        shelf.AddToCounts(pick, -1);
                        GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, st, bp, true);
                        tmp.AddToCounts(pick, 1); got[g]++; bought++; gold += bp;
                    }
                }
            }
            int taken = Accept(tmp, SrcAutotest), back = 0;
            for (int i = 0; i < tmp.Count; i++) { var el = tmp.GetElementCopyAtIndex(i); if (el.Amount > 0) { bag.AddToCounts(el.EquipmentElement, el.Amount); back += el.Amount; } }
            Log.Info("Musztra (autotest): zasilono zapas - z taboru " + fromBag + " szt., kupione " + bought + " szt. za " + gold + " d w " + (st != null ? st.Name.ToString() : "-")
                     + "; przyjeto " + taken + ", wraca do taboru " + back + ".");
        }

        // ------------------------------------------------------------ linie dnia
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            long t0 = Stopwatch.GetTimestamp();
            int day = (int)CampaignTime.Now.ToDays;
            try
            {
                _dailyN++;
                if (_dailyN == 2) { try { AutotestFeed(); } catch (Exception e) { Stumble("AutotestFeed", e); } }
                int w, a; GivenCounts(out w, out a);
                int given = w + a;
                int inToday = _in[0] + _in[1] + _in[2] + _in[3];
                string bal;
                if (_givenYday < 0) bal = "bilans zapasu od Ciebie od jutra (dzis " + given + ")";
                else
                {
                    int expect = _givenYday + inToday - _pWornGiven;
                    bal = "bilans zapasu od Ciebie " + (expect == given ? "ZGODNY" : "NIEZGODNY") + " (wczoraj " + _givenYday + " + przyjete " + inToday + " - zuzyte " + _pWornGiven + " = dzis " + given
                          + (expect == given ? "" : ", oczekiwane " + expect) + ")";
                }
                _givenYday = given;
                if (s.DrillLog)
                {
                    PlayerLine(day, s, w, a, bal);
                    AiLine(day, s);
                    if (day % 5 == 0) KingdomLine(day);
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); _stumbles = 0; }
        }

        private static void PlayerLine(int day, Settings s, int w, int a, string bal)
        {
            var sb = new StringBuilder("Musztra (gracz): dzien ").Append(day).Append(" - ");
            var r = _prLast;
            var c = r != null ? r.C : null;
            if (r == null || c == null || r.Day < day - 1)
                sb.Append(s.DrillLaw ? "brak treningu wedlug musztry w tej dobie" : "musztra gracza wylaczona (Drill Law)");
            else
            {
                double perHead = c.Men > 0 ? r.Computed / (double)c.Men : 0;
                double prod = r.B * c.L * c.D * c.S;
                double aEff = r.Pre > 0 ? r.Fin / r.Pre : 1;
                sb.Append("ludzi ").Append(c.Men).Append("; B ").Append(F1(r.B)).Append(" x dowodca ").Append(F3(c.L)).Append(" (Przywodztwo ").Append(c.Lead).Append(") x dzien ")
                  .Append(F2(c.D)).Append(" (").Append(c.Hungry ? "GLOD - bez cwiczen" : (c.Sleepless ? "DLUG SNU " + c.Debt + " - bez cwiczen" : (c.Rest ? "postoj" : "marsz")))
                  .Append(", ruch ").Append(c.Moved).Append(" h z 24) x zapas ").Append(F3(c.S)).Append(" = ").Append(F1(prod)).Append("; + perki P ").Append(F1(r.P))
                  .Append(" = ").Append(F1(r.Pre)).Append("; x bron A ").Append(F3(aEff)).Append(c.ArmsGate ? "" : " (wylaczone)").Append(" = XP wyliczone ").Append(r.Computed)
                  .Append(" (").Append(F1(perHead)).Append(" na glowe); przyjete przez roster ").Append(r.Accepted).Append(", uciete limitem awansu ").Append(r.Cut)
                  .Append(" -> ").Append(r.Accepted == r.Computed - r.Cut ? "ZGODNE" : "NIEZGODNE (roznica " + (r.Computed - r.Cut - r.Accepted) + ")");
                sb.Append("; zapas: bron ").Append(c.StockW).Append('/').Append(c.Full).Append(" (od Ciebie ").Append(c.GivenW).Append(", nadwyzka ludzi ").Append(c.SurW)
                  .Append("), zbroje ").Append(c.StockA).Append('/').Append(c.Full).Append(" (od Ciebie ").Append(c.GivenA).Append(", nadwyzka ").Append(c.SurA)
                  .Append("), Giving Hands ").Append(c.PerkW ? "tak" : "nie").Append(", Paid in Promise ").Append(c.PerkA ? "tak" : "nie")
                  .Append(c.StockOn ? "" : " (zapas wylaczony)");
            }
            float ore = 0f; Acc am;
            var main = MobileParty.MainParty;
            if (main != null && main.StringId != null && _acc.TryGetValue(main.StringId, out am)) ore = am.OreMen;
            sb.Append("; przyjeto ").Append(_in[0] + _in[1] + _in[2] + _in[3]).Append(" (wyrzucone ").Append(_in[SrcDiscard]).Append(", lup ").Append(_in[SrcLoot])
              .Append(", trofea ").Append(_in[SrcTrophies]).Append(", autotest ").Append(_in[SrcAutotest]).Append("), bez miejsca ").Append(_noRoom)
              .Append("; zuzyto ").Append(_pWornW + _pWornA).Append(" (bron ").Append(_pWornW).Append(", zbroje ").Append(_pWornA).Append(", w tym od Ciebie ").Append(_pWornGiven)
              .Append("; bez metalu ").Append(_pNoMetal).Append("), zlom +").Append(F2(_pOreAdd)).Append(" rudy, czeka ").Append(F2(ore)).Append(", sprzedano ").Append(_pSoldU)
              .Append(" ladunkow za ").Append(_pSoldGold).Append(" d; ").Append(bal).Append("; zapas od Ciebie: bron ").Append(w).Append(", zbroje ").Append(a);
            sb.Append("; partie rodu i armii: ").Append(_cParties);
            if (_cParties > 0) sb.Append(" (ludzi ").Append(_cMen).Append(", XP ").Append((long)_cXp).Append(", dowodca sr. x").Append(F2(_cL / _cParties)).Append(", na postoju ").Append(_cRest).Append(')');
            sb.Append("; potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
        }

        private static string Pct(double part, double all) { return all > 0 ? ((int)Math.Round(100 * part / all)).ToString() + "%" : "-"; }
        private static string X(double num, double den) { return den > 0 ? "x" + F2(num / den) : "-"; }

        private static void AiLine(int day, Settings s)
        {
            var inv = CultureInfo.InvariantCulture;
            string lead = "-";
            if (_leads.Count > 0)
            {
                _leads.Sort();
                int n = _leads.Count;
                Func<double, int> P = q => _leads[Math.Max(0, Math.Min(n - 1, (int)Math.Ceiling(q * n) - 1))];
                lead = "mediana " + _leads[n / 2] + ", p10 " + P(0.1) + ", p90 " + P(0.9);
            }
            float ore = 0f;
            var main = MobileParty.MainParty;
            foreach (var kv in _acc) if (main == null || kv.Key != main.StringId) ore += kv.Value.OreMen + kv.Value.OreLord;
            var sb = new StringBuilder("Musztra AI: dzien ").Append(day).Append(" - partii ").Append(_aParties).Append(", ludzi ").Append(_aMen)
              .Append("; wazone baza gry (").Append(((long)_aW).ToString(inv)).Append(" XP): dowodca ").Append(X(_aWL, _aW)).Append(" (Przywodztwo ").Append(lead)
              .Append(", bez dowodcy ").Append(_aNoLead).Append("), dzien ").Append(X(_aWD, _aW)).Append(" (postoj ").Append(Pct(_aWRest, _aW)).Append(", marsz ").Append(Pct(_aWMarch, _aW))
              .Append(", glod ").Append(Pct(_aWHungry, _aW)).Append(", sen ").Append(Pct(_aWSleep, _aW)).Append("), dowodca x dzien ").Append(X(_aWLD, _aW))
              .Append(s.DrillLawAi ? " (Z14b CZYNNA)" : " (Z14b WYLACZONA - pomiar)").Append("; zapas ").Append(X(_aWS, _aW)).Append(s.DrillStockAi ? " (CZYNNY" : " (wylaczony - pomiar")
              .Append("; pelny u ").Append(_aFullStock).Append(" partii, pusty u ").Append(_aNoStock).Append("); razem ").Append(X(_aWLDS, _aW))
              .Append("; razem przy progu postoju 4/8/12 h: ").Append(X(_aWT[0], _aW)).Append('/').Append(X(_aWT[1], _aW)).Append('/').Append(X(_aWT[2], _aW))
              .Append("; godziny ruchu w dobie (partie): 0 h ").Append(_hb[0]).Append(", 1-3 h ").Append(_hb[1]).Append(", 4-11 h ").Append(_hb[2]).Append(", 12+ h ").Append(_hb[3])
              .Append("; XP: baza gry ").Append(((long)_aGame).ToString(inv)).Append(", po czynnej regule ").Append(((long)_aRule).ToString(inv))
              .Append("; zuzyto ").Append(_aWornW + _aWornA).Append(" szt. (bron ").Append(_aWornW).Append(", zbroje ").Append(_aWornA).Append("; bez metalu ").Append(_aNoMetal)
              .Append("), zlom +").Append(F1(_aOreAdd)).Append(" rudy, czeka razem ").Append(F1(ore)).Append(", sprzedano ").Append(_aSoldU).Append(" ladunkow za ").Append(_aSoldGold)
              .Append(" d (lordowie ").Append(_aSoldLord).Append(", sakiewki ").Append(_aSoldPurse).Append("), przepadlo z rozbitymi ").Append(F1(_oreLost))
              .Append("; potkniecia ").Append(_stumbles).Append("; koszt ").Append((_ticks * 1000.0 / Stopwatch.Frequency).ToString("0.0", inv)).Append(" ms.");
            Log.Info(sb.ToString());
        }

        /// <summary>Co 5 dob: wedlug krolestw - "dowodca x dzien" i zapas z ostatnich dob, godziny ruchu, oraz stan armii AI dzis (sredni tier, t3+, konni).</summary>
        private static void KingdomLine(int day)
        {
            var men = new Dictionary<string, long[]>();   // id -> [ludzi, suma tierow, t3+, konni, partii]
            var names = new Dictionary<string, string>();
            foreach (var mp in MobileParty.AllLordParties)
            {
                try
                {
                    if (mp == null || !mp.IsActive || mp.LeaderHero == null || mp == MobileParty.MainParty || NoGameBase(mp) || Undead.Party(mp) || mp.MemberRoster == null) continue;
                    var kd = mp.MapFaction as Kingdom;
                    string id = kd != null ? kd.StringId : "-";
                    if (!names.ContainsKey(id)) names[id] = kd != null ? kd.Name.ToString() : "bez krolestwa";
                    long[] v;
                    if (!men.TryGetValue(id, out v)) { v = new long[5]; men[id] = v; }
                    v[4]++;
                    var r = mp.MemberRoster;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        var ch = el.Character;
                        if (ch == null || ch.IsHero || el.Number <= 0) continue;
                        v[0] += el.Number; v[1] += (long)el.Number * ch.Tier; if (ch.Tier >= 3) v[2] += el.Number; if (ch.IsMounted) v[3] += el.Number;
                    }
                }
                catch (Exception e) { Stumble("KingdomLine(partia)", e); }
            }
            var keys = new List<string>(men.Keys);
            keys.Sort((x, y) => men[y][0].CompareTo(men[x][0]));
            var sb = new StringBuilder("Musztra AI wedlug krolestw: dzien ").Append(day).Append(" (czynniki z dob od ostatniej linii) - ");
            bool first = true;
            foreach (var id in keys)
            {
                var v = men[id];
                KAcc k; _k.TryGetValue(id, out k);
                if (!first) sb.Append(" | ");
                first = false;
                sb.Append(names[id]).Append(": partii ").Append(v[4]).Append(", ludzi ").Append(v[0])
                  .Append(", dowodca x dzien ").Append(k != null ? X(k.WLD, k.W) : "-").Append(", zapas ").Append(k != null ? X(k.WS, k.W) : "-");
                if (k != null) sb.Append(", godziny ruchu 0/1-3/4-11/12+: ").Append(k.Hb[0]).Append('/').Append(k.Hb[1]).Append('/').Append(k.Hb[2]).Append('/').Append(k.Hb[3]);
                sb.Append(", sredni tier ").Append(v[0] > 0 ? F2(v[1] / (double)v[0]) : "-").Append(", t3+ ").Append(Pct(v[2], v[0])).Append(", konni ").Append(Pct(v[3], v[0]));
            }
            Log.Info(sb.ToString());
            _k.Clear();
        }
    }
}
