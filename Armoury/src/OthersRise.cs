using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PACZKA E4/2 (190, PLAN 4.18) - INNI TYLKO Z POLEGLYCH ZA MUREM I PRZY MURZE (projekt etapu 4 wersja 2, rozdz. 3 "E4/2", regula R13;
    /// Jeff 07.10 pyt. 1-2 i H2: "pojmany i odbity jeniec to polegly swojego regionu i cialo dla Innych, tylko za Murem i przy Murze",
    /// "0% cial niespalonych"; RK 4.2). Po T2b (CrashScribe OthersGrowth blokuje +100, +2/d i ochotnikow z mapy - bez zmian).
    ///  (a) nekromancja: prefiks bool na ROT ROTOthersCampaignBehavior.OnMapEventEnded - wzor ROT (zdrowi przegranych NA STARCIE x udzial, po kazdej
    ///      bitwie, takze przegranej, bez miejsca) zastapiony: ciala = polegli (DiedInBattle, bez bohaterow i wightow) strony pokonanej x udzial bandy
    ///      w walce (tylko banda zwyciezcow - przegrany nie podnosi trupow, zwyciezca pali poleglych; RK 4.2 B pkt 9) + jency-niebohaterowie, ktorych ROT
    ///      kasuje (kazdy = polegly R3); wstaja tylko w bitwie za Murem / przy Murze (Measure169c.PlaceAt - ta sama definicja co pomiar W8), x mnoznik ROT
    ///      najwyzej 1, do miejsca w bandzie, nadwyzka do puli CIALA (PeopleBook); reszta spalona. Jedyna inna czynnosc metody ROT - Clear lochu bandy
    ///      (razem z bohaterami, jak ROT) - powtorzona wiernie. Okno pomiaru 169c W8 (void First) biegnie mimo naszego false.
    ///  (b) rajd: prefiks bool na ROT OnRaidCompleted - Hearth/2 trupow z niczego nie wchodzi do bandy; od E4/5 (K7) wstaja zabici przy spustoszeniu calego
    ///      rabunku (Devastation.RaidDead - rozliczeni w EndPrefix przed RaidCompleted), ta sama regula ciala (Raise: tylko za Murem / przy Murze), bez R3 drugi raz;
    ///      wlacznik OthersRaidWights (domyslnie false = zablokowane).
    ///  (c) nowa banda: postfiks na ROT OnMobilePartyCreated (biegnie po ROT i po prefiksie CrashScribe, takze gdy ten pominal +100) - szeregowi
    ///      szablonu (53, w bilansie otwarcia +100) tylko z puli CIALA, przycieci proporcjonalnie; pusta pula = sam wodz.
    ///  (d) przejecie osady przez Innych: prefiks bool na ROT ChangeMilitiaCulture - zywa milicja = polegli (R3); wstaje (zamiana ROT na milicje Innych)
    ///      tylko za Murem / przy Murze, gdzie indziej ginie. Odbicie osady (wighty milicji -> zywi, ROT) - bez zmian, tylko licznik "z niczego".
    ///  (e) druga droga wightow do puli wyrzutkow: OutlawLaw.OnPartyDestroyed pomija Undead.Character (jak W4b dla dezercji) - licznik tu.
    ///  (f) odbici jency (U6): prefiks + finalizer na MapEvent.LootDefeatedPartyPrisoners (ten sam cel co okno E4/0 W10) - gdy zwyciezca to banda Innych:
    ///      przybyli do skladu zywi sa zdejmowani; kazdy = polegly (R3, region miejsca do E4/7); wight tylko za Murem / przy Murze (do miejsca, nadwyzka do
    ///      puli), reszta spalona. Finalizer High biegnie PRZED finalizerem W10 (Normal), wiec W10 widzi juz sklad bez zywych; i przed OnMapEventEnded.
    /// Latki wpinane ZAWSZE (jak E4/0, U26) - wylaczniki czytane na zywo: OthersRiseOnlyFromFallen (a, d, e; przy false wszystko jak w ROT),
    /// OthersRaidWights (b), OthersBandFromCorpsePool (c), OthersRescuedAreFallen (f). Kazde cialo w try, potkniecia liczone (bez globalnych wylacznikow).
    /// Linia raz na dobe "Inni z poleglych (E4/2)" + "Ludzie (ksiega)" (PeopleBook). Stan w zapisie - tylko pula CIALA (PeopleBook, arm_people).
    /// </summary>
    internal static class OthersRise
    {
        private static bool FallenOn { get { var s = Settings.Current; return s != null && s.OthersRiseOnlyFromFallen; } }
        private static bool RaidBlocked { get { var s = Settings.Current; return s != null && s.OthersRiseOnlyFromFallen && !s.OthersRaidWights; } }
        private static bool PoolOn { get { var s = Settings.Current; return s != null && s.OthersBandFromCorpsePool; } }
        private static bool RescueOn { get { var s = Settings.Current; return s != null && s.OthersRescuedAreFallen; } }

        private const string OthersClanId = "ROTclan_126";     // ROT ROTClans.WhiteWalkers (ten sam warunek co ROT OnMobilePartyCreated)
        private const string WightId = "angry_wight";          // ROT _wight (ROTOthersCampaignBehavior, OnSessionLaunched)

        private static bool _applied;
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static PropertyInfo _pMult; private static bool _multTried;
        private static CharacterObject _wight; private static bool _wightTried;
        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { if (_errSites.Add(where)) Log.Error("OthersRise." + where, e); } catch { }
        }

        // ---------------------------------------------------------------- liczniki doby (bez zapisu)
        private const int PKinds = 5;   // 0 za Murem, 1 przy Murze, 2 Westeros, 3 Essos, 4 miejsce nieznane
        private static readonly string[] PlaceName = { "za Murem", "przy Murze", "Westeros", "Essos", "miejsce nieznane" };
        private static int _nBattles, _nBattlesR13, _nParties;
        private static long _nFallen, _nPris, _nRisen, _nPooled, _nBurntMult, _nRotWould;
        private static readonly long[] _nBurntPlace = new long[PKinds];
        private static int _rBattles;
        private static long _rLiving, _rRisen, _rPooled, _rBurntMult;
        private static readonly long[] _rPlace = new long[PKinds];
        private static int _raidN; private static long _raidWould;
        private static long _raidDead, _raidRisen, _raidPooled, _raidBurnt;     // E4/5 (K7): zabici przy spustoszeniu rajdow Innych i co z nich
        private static long _raidRisenAll;     // wstali z rajdu od startu procesu (bez zerowania) - okno W9 Measure169c odejmuje ich od "Hearth/2 z niczego"

        /// <summary>Okno W9 (Measure169c, rajd ROT): ilu zabitych przy spustoszeniu wstalo do band Innych od startu procesu (licznik rosnacy, roznica w oknie).</summary>
        internal static long RaidRisenAll { get { return _raidRisenAll; } }
        private static int _milN, _milRevertN; private static long _milDead, _milRisen, _milBurnt, _milRevertLiving;
        private static int _bN, _bLeaderOnly, _bOtherN; private static long _bTemplate, _bFromPool, _bOtherMen;

        internal static void Reset()
        {
            ZeroDay();
            _wight = null; _wightTried = false;
            _stumbles = 0; _errSites.Clear();
        }

        private static void ZeroDay()
        {
            _nBattles = _nBattlesR13 = _nParties = 0; _nFallen = _nPris = _nRisen = _nPooled = _nBurntMult = _nRotWould = 0; Array.Clear(_nBurntPlace, 0, PKinds);
            _rBattles = 0; _rLiving = _rRisen = _rPooled = _rBurntMult = 0; Array.Clear(_rPlace, 0, PKinds);
            _raidN = 0; _raidWould = 0; _raidDead = _raidRisen = _raidPooled = _raidBurnt = 0;
            _milN = _milRevertN = 0; _milDead = _milRisen = _milBurnt = _milRevertLiving = 0;
            _bN = _bLeaderOnly = _bOtherN = 0; _bTemplate = _bFromPool = _bOtherMen = 0;
            _stumblesDay = 0;
        }

        // ---------------------------------------------------------------- wpiecie (SubModuleMain, raz na proces)
        private static void Wire(Harmony h, string name, MethodBase m, string pre, string post, string fin, int finPriority = Priority.Normal)
        {
            try
            {
                if (m == null) { _missing.Add(name); return; }
                h.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(OthersRise), pre) : null,
                           postfix: post != null ? new HarmonyMethod(typeof(OthersRise), post) { priority = Priority.Last } : null,
                           finalizer: fin != null ? new HarmonyMethod(typeof(OthersRise), fin) { priority = finPriority } : null);
                _wired.Add(name);
            }
            catch (Exception e) { _missing.Add(name + " (blad)"); Stumble("Wire(" + name + ")", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            if (_applied || h == null) return;
            _applied = true;
            var rot = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTOthersCampaignBehavior");
            Wire(h, "(a) nekromancja ROT", rot != null ? AccessTools.Method(rot, "OnMapEventEnded", new[] { typeof(MapEvent) }) : null, nameof(NecroPrefix), null, null);
            Wire(h, "(b) rajd ROT", rot != null ? AccessTools.Method(rot, "OnRaidCompleted", new[] { typeof(BattleSideEnum), typeof(RaidEventComponent) }) : null, nameof(RaidPrefix), null, null);
            Wire(h, "(c) narodziny bandy", rot != null ? AccessTools.Method(rot, "OnMobilePartyCreated", new[] { typeof(MobileParty) }) : null, null, nameof(BirthPostfix), null);
            Wire(h, "(d) milicja przejetej osady", rot != null ? AccessTools.Method(rot, "ChangeMilitiaCulture", new[] { typeof(TroopRoster), typeof(CultureObject) }) : null, nameof(MilitiaPrefix), nameof(MilitiaPostfix), null);
            Wire(h, "(f) odbici jency", AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"), nameof(RescuePre), null, nameof(RescueFin), Priority.High);
            var s = Settings.Current;
            Log.Info("Inni z poleglych (E4/2): latki wpiete " + _wired.Count + " z 5 (" + string.Join(", ", _wired.ToArray()) + "); BRAK: "
                     + (_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-") + "; (e) wighty rozwiazanych band - w OutlawLaw.OnPartyDestroyed"
                     + "; wylaczniki na zywo: Others Rise Only From Fallen " + YN(s != null && s.OthersRiseOnlyFromFallen) + ", Others Raid Wights " + YN(s != null && s.OthersRaidWights)
                     + ", Others Band From Corpse Pool " + YN(s != null && s.OthersBandFromCorpsePool) + ", Others Rescued Are Fallen " + YN(s != null && s.OthersRescuedAreFallen) + ".");
        }

        private static string YN(bool b) { return b ? "TAK" : "NIE"; }

        // ---------------------------------------------------------------- pomocnicze
        internal static bool IsWhiteWalkerClan(Clan c) { return c != null && c.Culture != null && c.Culture.StringId == "whitewalker"; }   // ROT Extensions.IsWhiteWalker(Clan)
        private static bool MainHeroIsWight() { try { var h = Hero.MainHero; return h != null && h.CharacterObject != null && h.CharacterObject.Race == 2; } catch { return false; } }   // ROT IsWight

        /// <summary>Szeregowi zywi (bez bohaterow i wightow) w rosterze - E4/2 i okno W10 (Measure40) dla band Innych.</summary>
        internal static int LivingRegulars(TroopRoster r)
        {
            if (r == null) return 0;
            int n = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var e = r.GetElementCopyAtIndex(i);
                if (e.Character == null || e.Character.IsHero || e.Number <= 0 || Undead.Character(e.Character)) continue;
                n += e.Number;
            }
            return n;
        }

        private static float NecroMult()
        {
            try
            {
                if (!_multTried)
                {
                    _multTried = true;
                    var t = AccessTools.TypeByName("ROT.ROTSettings");
                    _pMult = t != null ? AccessTools.Property(t, "OthersNecromancyMultiplier") : null;
                }
                if (_pMult == null) return 1f;
                float m = Convert.ToSingle(_pMult.GetValue(null, null));
                return m < 0f ? 0f : (m > 1f ? 1f : m);    // mnoznik ROT najwyzej 1 (projekt E4/2 a)
            }
            catch (Exception e) { Stumble("NecroMult", e); return 1f; }
        }

        private static CharacterObject Wight()
        {
            if (_wightTried && _wight != null) return _wight;
            _wightTried = true;
            try { _wight = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<CharacterObject>(WightId) : null; }
            catch (Exception e) { _wight = null; Stumble("Wight", e); }
            return _wight;
        }

        private static int Place(Vec2 pos) { int p = Measure169c.PlaceAt(pos); return p >= 0 && p < 4 ? p : 4; }
        private static bool IsR13(int place) { return place == 0 || place == 1; }

        /// <summary>Jedna regula ciala -> wight dla (a) i (f): tylko w miejscu R13; x mnoznik ROT (najwyzej 1); do miejsca w bandzie; nadwyzka do puli CIALA
        /// regionu miejsca; reszta spalona (poza R13 cale ciala, w R13 czesc ponad mnoznik).</summary>
        private static void Raise(MobileParty mp, long bodies, Settlement region, int place, int cause, out long risen, out long pooled, out long burnt)
        {
            risen = pooled = 0; burnt = 0;
            if (bodies <= 0) return;
            if (!IsR13(place)) { burnt = bodies; return; }
            long raise = (long)(bodies * NecroMult());
            burnt = bodies - raise;
            if (raise <= 0) return;
            var w = Wight();
            int space = 0;
            try { if (mp != null && mp.Party != null) space = (int)((1f - mp.PartySizeRatio) * (float)mp.Party.PartySizeLimit); } catch { space = 0; }   // to samo co ROT
            long up = w != null && mp != null && mp.MemberRoster != null ? Math.Max(0L, Math.Min(raise, (long)space)) : 0L;
            if (up > 0) mp.MemberRoster.AddToCounts(w, (int)up);
            risen = up;
            pooled = raise - up;
            if (pooled > 0) PeopleBook.ToBodies(region, pooled, cause);
        }

        // ================================================================ (a) NEKROMANCJA
        private sealed class NecroPlan
        {
            public MobileParty P; public long Fallen; public long Pris; public TroopRoster Clear; public long RotWould;
        }

        /// <summary>Prefiks ROTOthersCampaignBehavior.OnMapEventEnded(MapEvent): przy OthersRiseOnlyFromFallen cala obsluga tutaj, ROT nie biegnie.
        /// Najpierw plan (sam odczyt; wyjatek = ROT jak dotad), potem wykonanie.</summary>
        public static bool NecroPrefix(MapEvent __0)
        {
            if (!FallenOn) return true;
            List<NecroPlan> plan; int place; Settlement region;
            try
            {
                plan = PlanNecro(__0, out place, out region);
                if (plan == null) return false;          // ROT tez nic by nie zrobil (brak zwyciezcy)
            }
            catch (Exception e) { Stumble("Necro(plan)", e); return true; }
            try
            {
                if (plan.Count == 0) return false;
                _nBattles++; if (IsR13(place)) _nBattlesR13++;
                foreach (var p in plan)
                {
                    try
                    {
                        _nParties++; _nFallen += p.Fallen; _nPris += p.Pris; _nRotWould += p.RotWould;
                        long risen, pooled, burnt;
                        Raise(p.P, p.Fallen + p.Pris, region, place, PeopleBook.CNecro, out risen, out pooled, out burnt);
                        _nRisen += risen; _nPooled += pooled;
                        if (IsR13(place)) _nBurntMult += burnt; else _nBurntPlace[place] += burnt;
                        if (p.Pris > 0) PeopleBook.Die(region, p.Pris, PeopleBook.DCaptives, place);
                    }
                    catch (Exception e) { Stumble("Necro(banda)", e); }
                    try { if (p.Clear != null) p.Clear.Clear(); } catch (Exception e) { Stumble("Necro(Clear)", e); }   // jak ROT: loch bandy (nie gracza) po bitwie pusty
                }
            }
            catch (Exception e) { Stumble("Necro(apply)", e); }
            return false;
        }

        private static List<NecroPlan> PlanNecro(MapEvent me, out int place, out Settlement region)
        {
            place = 4; region = null;
            if (me == null || me.Winner == null || me.Winner.Parties == null) return null;
            var win = me.WinningSide;
            var lose = win == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker;
            var plan = new List<NecroPlan>(2);
            var items = new List<MapEventParty>(2); var sides = new List<BattleSideEnum>(2);
            // najpierw (tanio): bandy Innych w starciu - ten sam warunek co ROT; bitwa bez Innych konczy sie tutaj
            foreach (var side in new[] { BattleSideEnum.Attacker, BattleSideEnum.Defender })
            {
                var ps = me.PartiesOnSide(side);
                if (ps == null) continue;
                for (int i = 0; i < ps.Count; i++)
                {
                    var item = ps[i];
                    if (item == null || item.Party == null) continue;
                    if (!IsWhiteWalkerClan(item.Party.MapFaction as Clan)) continue;
                    var mp = item.Party.MobileParty;
                    if (mp == null) continue;
                    if (mp.IsMainParty && MainHeroIsWight()) continue;                 // ROT: gracz-wight bez nekromancji (i bez Clear)
                    if (item.Party.MemberRoster == null || item.Party.MemberRoster.Count == 0) continue;
                    items.Add(item); sides.Add(side);
                }
            }
            if (items.Count == 0) return plan;
            long fallen = 0, healthyAtStart = 0; int winContrib = 0;
            var lp = me.PartiesOnSide(lose);
            if (lp != null) for (int i = 0; i < lp.Count; i++) { var x = lp[i]; if (x == null) continue; fallen += LivingRegulars(x.DiedInBattle); healthyAtStart += x.HealthyManCountAtStart; }
            var wp = me.PartiesOnSide(win);
            if (wp != null) for (int i = 0; i < wp.Count; i++) { var x = wp[i]; if (x != null) winContrib += x.ContributionToBattle; }
            float mult = NecroMult();
            // loch liczony raz (recenzja E4/2 pkt 1): zaloga i milicja osady dostaja od gry ten sam loch osady (MapEventParty.RosterToReceiveLootPrisoners:
            // IsMilitia/IsGarrison -> HomeSettlement.Party.PrisonRoster) - druga partia z tym samym rosterem: Pris 0, bez Clear (jedno Clear wystarczy, jak u ROT);
            // ten sam test ReferenceEquals co okno W8 (Measure169c.OthersPris), wiec linia E4/2 i W8 licza tych samych jencow
            var seenPris = new List<TroopRoster>(2);
            for (int k = 0; k < items.Count; k++)
            {
                var item = items[k]; var mp = item.Party.MobileParty;
                var p = new NecroPlan { P = mp };
                if (winContrib > 0)
                {
                    float share = (float)item.ContributionToBattle / (float)winContrib;    // udzial jak w ROT (wklad bandy / wklad strony zwyciezcow)
                    if (sides[k] == win) p.Fallen = (long)(fallen * share);              // tylko zwyciezcy (przegrany nie podnosi trupow - RK 4.2 B pkt 9)
                    // ile dalby wzor ROT (zdrowi przegranych na starcie x udzial x mnoznik, do miejsca) - tylko do linii
                    long rv = (long)(healthyAtStart * share * mult);
                    int space = 0; try { space = (int)((1f - mp.PartySizeRatio) * (float)item.Party.PartySizeLimit); } catch { }
                    p.RotWould = Math.Max(0L, Math.Min(rv, (long)space));
                }
                if (!mp.IsMainParty)
                {
                    var pr = item.RosterToReceiveLootPrisoners;
                    bool had = false;
                    if (pr != null) foreach (var x in seenPris) if (ReferenceEquals(x, pr)) { had = true; break; }
                    if (pr != null && !had) { seenPris.Add(pr); p.Clear = pr; p.Pris = LivingRegulars(pr); }
                }
                plan.Add(p);
            }
            {
                var pos = me.Position.ToVec2();
                place = Place(pos);
                region = OutlawLaw.RegionAt(pos);
            }
            return plan;
        }

        // ================================================================ (b) RAJD
        public static bool RaidPrefix(RaidEventComponent __1)
        {
            try
            {
                if (!RaidBlocked || __1 == null || __1.AttackerSide == null || __1.AttackerSide.LeaderParty == null) return true;
                var mp = __1.AttackerSide.LeaderParty.MobileParty;
                if (mp == null || !IsWhiteWalkerClan(mp.ActualClan)) return true;
                if (mp.IsMainParty && MainHeroIsWight()) return true;                     // ROT: gracz-wight bez wightow z rajdu - nic do blokowania
                var st = __1.MapEventSettlement;
                if (st != null && st.Village != null) _raidWould += (long)(st.Village.Hearth / 2f * NecroMult());
                _raidN++;
                // E4/5 (K7, P E4/2 (b)): wstaja tylko wiesniacy zabici przy spustoszeniu calego rabunku (0 przy wylaczonym spustoszeniu - nikt), ta sama regula ciala
                // co nekromancja: tylko za Murem / przy Murze, do miejsca w bandzie, nadwyzka do puli CIALA, reszta spalona. BEZ PeopleBook.Die - ci zmarli sa juz
                // w koncie Devastation (zabici wsi), inaczej R3 liczylby ich drugi raz
                long dead = 0;
                try { dead = (long)Math.Round(Devastation.RaidDead(__1)); } catch (Exception e) { dead = 0; Stumble("Raid(zabici)", e); }
                if (dead > 0 && st != null)
                {
                    int place = Place(st.GetPosition2D);
                    var region = OutlawLaw.RegionFor(st);
                    long risen, pooled, burnt;
                    Raise(mp, dead, region, place, PeopleBook.CRaid, out risen, out pooled, out burnt);
                    _raidDead += dead; _raidRisen += risen; _raidPooled += pooled; _raidBurnt += burnt; _raidRisenAll += risen;
                }
                return false;                                                              // metoda ROT robi tylko AddToCounts(_wight, Hearth/2)
            }
            catch (Exception e) { Stumble("Raid", e); return true; }
        }

        // ================================================================ (c) NARODZINY BANDY
        /// <summary>Postfiks ROTOthersCampaignBehavior.OnMobilePartyCreated: sklad zrodzonej bandy (szablon klanu, w bilansie otwarcia +100) tylko z puli CIALA.</summary>
        public static void BirthPostfix(MobileParty __0)
        {
            try
            {
                if (!PoolOn || __0 == null || __0.ActualClan == null || __0.ActualClan.StringId != OthersClanId) return;
                var r = __0.MemberRoster;
                if (r == null) return;
                if (!__0.IsLordParty || __0.IsMainParty) { _bOtherN++; _bOtherMen += r.TotalRegulars; return; }   // inne partie klanu - tylko licznik
                int regs = r.TotalRegulars;
                long take = PeopleBook.FromBodies(__0.Position.ToVec2(), regs, PeopleBook.CBirth);
                Trim(r, regs - (int)take);
                _bN++; _bTemplate += regs; _bFromPool += take;
                try { PeopleLedger.FixCreatedMen(__0); } catch { }   // linia "Partie (E4/0)": ludzie przy utworzeniu po przycieciu
                if (take <= 0) _bLeaderOnly++;
            }
            catch (Exception e) { Stumble("Birth", e); }
        }

        /// <summary>Przyciecie szeregowych proporcjonalnie do skladu (kazdy oddzial zostawia floor(n x zostaje / razem), reszta miejsc po kolei).</summary>
        private static void Trim(TroopRoster r, int remove)
        {
            if (remove <= 0) return;
            var ch = new List<CharacterObject>(); var num = new List<int>();
            for (int i = 0; i < r.Count; i++)
            {
                var e = r.GetElementCopyAtIndex(i);
                if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                ch.Add(e.Character); num.Add(e.Number);
            }
            int total = 0; foreach (var n in num) total += n;
            if (total <= 0) return;
            int keep = Math.Max(0, total - remove);
            var k = new int[num.Count]; int kept = 0;
            for (int i = 0; i < num.Count; i++) { k[i] = (int)((long)num[i] * keep / total); kept += k[i]; }
            for (int i = 0; kept < keep && i < num.Count * 2; i++) { int j = i % num.Count; if (k[j] < num[j]) { k[j]++; kept++; } }
            for (int i = 0; i < num.Count; i++) { int d = num[i] - k[i]; if (d > 0) r.AddToCounts(ch[i], -d); }
        }

        // ================================================================ (d) MILICJA PRZEJETEJ OSADY
        internal sealed class MilState { public TroopRoster R; public int Living, Undead; public bool ToOthers; public int Place; public Settlement Region; }

        /// <summary>Prefiks ROT ChangeMilitiaCulture(roster, kultura): przejecie przez Innych - zywa milicja to polegli; wstaje (zamiana ROT) tylko za Murem
        /// / przy Murze, gdzie indziej ginie (bez zamiany). Odbicie osady - ROT bez zmian (licznik w postfiksie).</summary>
        public static bool MilitiaPrefix(TroopRoster __0, CultureObject __1, out MilState __state)
        {
            __state = null;
            try
            {
                if (!FallenOn || __0 == null || __1 == null) return true;
                var st = new MilState { R = __0, ToOthers = __1.StringId == "whitewalker" };
                int und = 0, liv = 0;
                for (int i = 0; i < __0.Count; i++)
                {
                    var e = __0.GetElementCopyAtIndex(i);
                    if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                    if (Undead.Character(e.Character)) und += e.Number; else liv += e.Number;
                }
                st.Living = liv; st.Undead = und;
                if (!st.ToOthers) { __state = st; return true; }                         // odbicie osady: ROT bez zmian
                var owner = OwnerOf(__0);
                var mp = owner != null ? owner.MobileParty : null;
                Settlement home = mp != null ? (mp.HomeSettlement ?? mp.CurrentSettlement) : null;
                Vec2 pos = home != null ? home.GetPosition2D : (mp != null ? mp.Position.ToVec2() : Vec2.Zero);
                st.Place = Place(pos);
                st.Region = home != null ? OutlawLaw.RegionFor(home) : OutlawLaw.RegionAt(pos);
                _milN++; _milDead += liv;
                if (liv > 0) PeopleBook.Die(st.Region, liv, PeopleBook.DMilitia, st.Place);
                if (IsR13(st.Place)) { __state = st; return true; }                       // za Murem / przy Murze: wstaja (zamiana ROT), liczone w postfiksie
                // gdzie indziej: zywi gina, nikt nie wstaje (ROT i tak kasuje oddzialy spoza milicji - tu wszystkich zywych)
                for (int i = __0.Count - 1; i >= 0; i--)
                {
                    var e = __0.GetElementCopyAtIndex(i);
                    if (e.Character == null || e.Character.IsHero || e.Number <= 0 || Undead.Character(e.Character)) continue;
                    __0.AddToCounts(e.Character, -e.Number);
                }
                __0.RemoveZeroCounts();
                _milBurnt += liv;
                return false;
            }
            catch (Exception e) { __state = null; Stumble("Militia(pre)", e); return true; }
        }

        private static MethodInfo _mOwner; private static bool _ownerTried;
        /// <summary>TroopRoster.OwnerParty jest internal - getter przez refleksje (raz na proces); bez niego: osada, ktorej milicja ma ten roster.</summary>
        private static PartyBase OwnerOf(TroopRoster r)
        {
            try
            {
                if (!_ownerTried) { _ownerTried = true; _mOwner = AccessTools.PropertyGetter(typeof(TroopRoster), "OwnerParty"); }
                if (_mOwner != null) { var pb = _mOwner.Invoke(r, null) as PartyBase; if (pb != null) return pb; }
                foreach (var st in Settlement.All)
                {
                    var mc = st != null ? st.MilitiaPartyComponent : null;
                    var mmp = mc != null ? mc.MobileParty : null;
                    if (mmp != null && ReferenceEquals(mmp.MemberRoster, r)) return mmp.Party;
                }
            }
            catch (Exception e) { Stumble("OwnerOf", e); }
            return null;
        }

        public static void MilitiaPostfix(MilState __state)
        {
            try
            {
                if (__state == null || __state.R == null) return;
                int und = 0, liv = 0;
                for (int i = 0; i < __state.R.Count; i++)
                {
                    var e = __state.R.GetElementCopyAtIndex(i);
                    if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                    if (Undead.Character(e.Character)) und += e.Number; else liv += e.Number;
                }
                if (__state.ToOthers) { int up = Math.Max(0, und - __state.Undead); _milRisen += up; _milBurnt += Math.Max(0, __state.Living - up); }
                else { _milRevertN++; _milRevertLiving += Math.Max(0, liv - __state.Living); }
            }
            catch (Exception e) { Stumble("Militia(post)", e); }
        }

        // ================================================================ (f) ODBICI JENCY
        internal sealed class RescueState { public MapEvent Me; public readonly List<MobileParty> P = new List<MobileParty>(2); public readonly List<Dictionary<CharacterObject, int>> Before = new List<Dictionary<CharacterObject, int>>(2); }

        private static bool IsOthersWinner(MapEventParty mep)
        {
            if (mep == null || mep.Party == null || !mep.IsNpcParty) return false;            // gracz: rostery PlayerEncounter (ekran lupu) - poza regula, jak ROT dla gracza-wighta
            var mp = mep.Party.MobileParty;
            return mp != null && mp.MemberRoster != null && IsWhiteWalkerClan(mep.Party.MapFaction as Clan);
        }

        public static void RescuePre(MapEvent __instance, MBReadOnlyList<MapEventParty> __0, MBReadOnlyList<MapEventParty> __1, out RescueState __state)
        {
            __state = null;
            try
            {
                if (!RescueOn || __0 == null || __1 == null) return;
                bool any = false;
                for (int i = 0; i < __0.Count; i++) if (IsOthersWinner(__0[i])) { any = true; break; }   // pierwszy, tani warunek: zwyciezca to banda Innych
                if (!any) return;
                any = false;
                for (int i = 0; i < __1.Count; i++)
                {
                    var pr = __1[i] != null && __1[i].Party != null ? __1[i].Party.PrisonRoster : null;
                    if (pr != null && pr.TotalRegulars > 0) { any = true; break; }
                }
                if (!any) return;                                                              // pokonani bez szeregowych jencow - nic do odbicia
                var w = new RescueState { Me = __instance };
                for (int i = 0; i < __0.Count; i++)
                {
                    if (!IsOthersWinner(__0[i])) continue;
                    var mp = __0[i].Party.MobileParty;
                    if (w.P.Contains(mp)) continue;
                    var d = new Dictionary<CharacterObject, int>();
                    var r = mp.MemberRoster;
                    for (int j = 0; j < r.Count; j++)
                    {
                        var e = r.GetElementCopyAtIndex(j);
                        if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                        int v; d.TryGetValue(e.Character, out v); d[e.Character] = v + e.Number;
                    }
                    w.P.Add(mp); w.Before.Add(d);
                }
                if (w.P.Count > 0) __state = w;
            }
            catch (Exception e) { __state = null; Stumble("RescuePre", e); }
        }

        public static Exception RescueFin(Exception __exception, RescueState __state)
        {
            if (__state == null) return __exception;
            try
            {
                int place = 4; Settlement region = null; bool where = false;
                for (int k = 0; k < __state.P.Count; k++)
                {
                    try
                    {
                        var mp = __state.P[k]; var r = mp.MemberRoster; var before = __state.Before[k];
                        if (r == null) continue;
                        var add = new List<KeyValuePair<CharacterObject, int>>();
                        for (int i = 0; i < r.Count; i++)
                        {
                            var e = r.GetElementCopyAtIndex(i);
                            if (e.Character == null || e.Character.IsHero || e.Number <= 0 || Undead.Character(e.Character)) continue;
                            int b; before.TryGetValue(e.Character, out b);
                            if (e.Number > b) add.Add(new KeyValuePair<CharacterObject, int>(e.Character, e.Number - b));
                        }
                        if (add.Count == 0) continue;
                        if (!where)
                        {
                            where = true;
                            var pos = __state.Me != null ? __state.Me.Position.ToVec2() : mp.Position.ToVec2();
                            place = Place(pos); region = OutlawLaw.RegionAt(pos);
                            _rBattles++;
                        }
                        long living = 0;
                        foreach (var a in add) { r.AddToCounts(a.Key, -a.Value); living += a.Value; }   // zywi nie wchodza do bandy Innych
                        _rLiving += living; _rPlace[place] += living;
                        PeopleBook.Die(region, living, PeopleBook.DRescued, place);
                        try { Measure40.NoteRescuedFallen((int)living); } catch { }
                        long risen, pooled, burnt;
                        Raise(mp, living, region, place, PeopleBook.CRescued, out risen, out pooled, out burnt);
                        _rRisen += risen; _rPooled += pooled; if (IsR13(place)) _rBurntMult += burnt;
                    }
                    catch (Exception e) { Stumble("RescueFin(banda)", e); }
                }
            }
            catch (Exception e) { Stumble("RescueFin", e); }
            return __exception;
        }

        // ================================================================ linia doby
        private static string S(long v) { return (v >= 0 ? "+" : "") + v; }

        /// <summary>ArmouryBehavior.OnDailyTick: linia "Inni z poleglych (E4/2)" i "Ludzie (ksiega)" (PeopleBook); zerowanie licznikow doby.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            bool log = s != null && s.LogEnabled;
            int day = 0;
            try { day = (int)CampaignTime.Now.ToDays - 1; } catch { }
            try
            {
                if (log)
                {
                    long disb = OutlawLaw.TakeDisbandUndead();
                    var sb = new StringBuilder(1400);
                    sb.Append("Inni z poleglych (E4/2): dzien ").Append(day)
                      .Append(" | wylaczniki: z poleglych ").Append(YN(s.OthersRiseOnlyFromFallen)).Append(", wighty z rajdu ").Append(YN(s.OthersRaidWights))
                      .Append(", banda z puli cial ").Append(YN(s.OthersBandFromCorpsePool)).Append(", odbici to polegli ").Append(YN(s.OthersRescuedAreFallen));
                    long bodies = _nFallen + _nPris, burntOut = 0; for (int i = 0; i < PKinds; i++) burntOut += _nBurntPlace[i];
                    sb.Append(" | (a) nekromancja: bitew z bandami Innych ").Append(_nBattles).Append(" (za Murem / przy Murze ").Append(_nBattlesR13).Append("), band ").Append(_nParties)
                      .Append(", ciala ").Append(bodies).Append(" = polegli strony pokonanej x udzial ").Append(_nFallen).Append(" + jency Innych (skasowani, polegli R3) ").Append(_nPris)
                      .Append(" -> wstalo ").Append(_nRisen).Append(", do puli ").Append(_nPooled).Append(", spalone ponad mnoznik ROT ").Append(_nBurntMult)
                      .Append(", spalone poza Murem ").Append(burntOut).Append(" (");
                    for (int i = 0; i < PKinds; i++) sb.Append(i > 0 ? ", " : "").Append(PlaceName[i]).Append(' ').Append(_nBurntPlace[i]);
                    sb.Append("), kontrola ciala - wstalo - pula - spalone = ").Append(bodies - _nRisen - _nPooled - _nBurntMult - burntOut)
                      .Append("; wzor ROT dalby ").Append(_nRotWould);
                    long rb = 0; for (int i = 0; i < PKinds; i++) rb += _rPlace[i];
                    sb.Append(" | (f) odbici jency: zywi zdjeci ze skladu band Innych ").Append(_rLiving).Append(" w ").Append(_rBattles).Append(" bitwach (polegli R3: ");
                    for (int i = 0; i < PKinds; i++) sb.Append(i > 0 ? ", " : "").Append(PlaceName[i]).Append(' ').Append(_rPlace[i]);
                    sb.Append(") -> wstalo ").Append(_rRisen).Append(", do puli ").Append(_rPooled).Append(", spalone ").Append(_rLiving - _rRisen - _rPooled)
                      .Append(" (w tym ponad mnoznik ROT ").Append(_rBurntMult).Append(')');
                    sb.Append(" | (b) rajd: zablokowane ").Append(_raidN).Append(" (ROT dalby ").Append(_raidWould).Append(" wightow z niczego)")
                      .Append(" -> zabici przy spustoszeniu ").Append(_raidDead).Append(", wstalo ").Append(_raidRisen).Append(", do puli ").Append(_raidPooled)
                      .Append(", spalone ").Append(_raidBurnt).Append(" (kontrola ").Append(_raidDead - _raidRisen - _raidPooled - _raidBurnt).Append(')');
                    sb.Append(" | (c) narodziny band: ").Append(_bN).Append(", szablon ").Append(_bTemplate).Append(" -> z puli cial ").Append(_bFromPool)
                      .Append(", przycieto ").Append(_bTemplate - _bFromPool).Append(", sam wodz ").Append(_bLeaderOnly)
                      .Append("; inne nowe partie klanu Innych (bez zmian, z niczego) ").Append(_bOtherN).Append(" (").Append(_bOtherMen).Append(" ludzi)");
                    sb.Append(" | (d) przejecie osady: ").Append(_milN).Append(" milicji, zywi polegli ").Append(_milDead).Append(" -> wstalo ").Append(_milRisen)
                      .Append(", spalone ").Append(_milBurnt).Append("; odbicie osady (ROT: wighty milicji -> zywi, z niczego) ").Append(_milRevertN).Append(" (").Append(S(_milRevertLiving)).Append(" zywych)");
                    sb.Append(" | (e) wighty rozwiazanych band - do niczego, nie do puli wyrzutkow ").Append(disb);
                    sb.Append(" | pula CIALA ").Append(PeopleBook.Bodies).Append(" (linia \"Ludzie (ksiega)\")");
                    if (_stumblesDay > 0) sb.Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (razem ").Append(_stumbles).Append(", pierwszy blad kazdego miejsca w logu)");
                    sb.Append('.');
                    Log.Info(sb.ToString());
                }
                else OutlawLaw.TakeDisbandUndead();
            }
            catch (Exception e) { Stumble("Daily", e); }
            try { PeopleBook.Daily(day, log); } catch (Exception e) { Stumble("PeopleBook.Daily", e); }
            ZeroDay();
        }
    }
}
