using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 514 (perki 70-98 audytu 14.11, Jeff 14.11 "78-105 ok, rob po kolei", "ale rob 1-7 od razu na raz"; docs/audyty-2026-11-14/PERKI.md 3.4-3.5).
    /// BK - DOPISANIE BRAKUJACYCH POLOWEK PERKOW wedlug ich opisow (BK ma opis, a kodu brak albo kod robi co innego). Wszystko przez refleksje
    /// (Armoury nie odwoluje sie do BannerKings.dll): perki umiejetnosci BK (Lordship / Scholarship / Theology) przez Hero.GetPerkValue, perki stylow
    /// zycia przez EducationData.HasPerk (jak BK). Maska perkow stylow bohatera liczona raz na dobe (ConcurrentDictionary - modele bywaja wolane z watkow AI).
    /// Trzy klucze (czytane na zywo): BkPerkHalvesCourt (70, 71, 73, 75, 77, 79), BkPerkHalvesLifestyle (82 mapa, 85, 89, 90 HonorGuard, 97 mapa, 98),
    /// BkPerkHalvesBattle (82 walka, 86, 90 ShieldBrother, 97 BoardingFury - Perk514.Battle.cs, latki misji wpinane w SubModuleMain).
    /// Latki kampanii wpinane raz na proces w OnSessionLaunched (typy BK istnieja juz w procesie, ale obiekty perkow dopiero w kampanii), cele przez
    /// AccessTools.DeclaredMethod na klasie deklarujacej. Bledy liczone (potkniecia), bez globalnego wylacznika. Stan tylko liczniki doby, bez zapisu.
    /// Linie: "Perki BK (514): start sesji ..." i "Perki BK (514): dzien N ..." (gdy cokolwiek zadzialalo albo byly potkniecia).
    /// </summary>
    internal static partial class Perk514
    {
        internal const string P = "Perki BK (514): ";
        private const string Owner = "com.jeff.armoury.perk514";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool CourtOn { get { var s = Settings.Current; return s != null && s.BkPerkHalvesCourt; } }
        internal static bool LifeOn { get { var s = Settings.Current; return s != null && s.BkPerkHalvesLifestyle; } }
        internal static bool BattleOn { get { var s = Settings.Current; return s != null && s.BkPerkHalvesBattle; } }

        // ------------------------------------------------------------ refleksja BK
        private static bool _resolved, _resolveTried;
        private static string _bk = "nie szukane";
        private static PropertyInfo _cfgInst, _eduMgr, _eduLifestyle, _councilOwner, _relHero, _bookSkill, _bookUse;
        private static MethodInfo _getEdu, _hasPerk;
        private static ConstructorInfo _relModCtor;

        // perki umiejetnosci BK (Hero.GetPerkValue)
        internal static PerkObject PAristo, PRogue, PTrad, PMilAdm, PDipTies, PPeer, PPoly, PLith;

        // perki stylow zycia (EducationData.HasPerk) - bity maski
        internal const int BShieldwall = 1 << 0, BSeaTough = 1 << 1, BSeaCmd = 1 << 2, BCoastal = 1 << 3, BInspirer = 1 << 4, BRanger = 1 << 5,
            BGhazw = 1 << 6, BIron = 1 << 7, BShieldBro = 1 << 8, BHonor = 1 << 9, BFury = 1 << 10, BHelms = 1 << 11, BPath = 1 << 12,
            BSeaEyes = 1 << 13, BTour = 1 << 14, BCrowds = 1 << 15, BKheshigLs = 1 << 16;
        private static readonly string[] LsIds =
        {
            "LifestyleJomsvikingShieldwall", "LifestyleJomsvikingSeaToughness", "LifestyleDrakkarSeaCommander", "LifestyleSjofarandiCoastalHunter",
            "LifestyleCommanderInspirer", "LifestyleFianRanger", "LifestyleJawwalGhazw", "LifestyleRitterIronHorses", "LifestyleVaryagShieldBrother",
            "LifestyleKheshigHonorGuard", "LifestyleJomsvikingBoardingFury", "LifestyleDrakkarHelmsman", "LifestyleSjofarandiPathfinder",
            "LifestyleSjofarandiSeaEyes", "LifestyleGladiatorTourDeCalradia", "LifestyleGladiatorCrowdsFavorite"
        };
        private static readonly PerkObject[] _ls = new PerkObject[16];
        internal static TextObject NameOf(int bit)
        {
            for (int i = 0; i < _ls.Length; i++) if ((1 << i) == bit && _ls[i] != null) return _ls[i].Name;
            return new TextObject("{=!}Lifestyle");
        }

        private static readonly ConcurrentDictionary<Hero, int> _mask = new ConcurrentDictionary<Hero, int>();
        private static int _maskDay = int.MinValue;

        // ------------------------------------------------------------ latki
        private static Harmony _h;
        private static bool _hooksTried;
        private static readonly List<string> _hookOk = new List<string>(), _hookMiss = new List<string>();
        private static bool _bkBetPatched, _bkRenownPatched, _bkSeaEyesPatched;

        // ------------------------------------------------------------ liczniki (Interlocked - modele z watkow AI)
        private static int _stumbles, _dStumbles;
        internal static int dGrace, dVassal, dMercJoin, dMercLeave, dMercDown, dMilit, dRelMod, dPeer, dPoly, dLith, dSeaMor, dSeaFood, dSeaSize, dSeaRen,
            dInspirer, dHonor, dHelms, dPath, dSeaEyesDay, dBet, dCrowds, dGhazw, dHorseRider, dHorseNoRider;
        private static long _dPeerXp;

        private static string N(int v) { return v.ToString(Inv); }

        internal static void Stumble(string where, Exception e)
        {
            int n = Interlocked.Increment(ref _stumbles);
            Interlocked.Increment(ref _dStumbles);
            if (n <= 3 || n % 100 == 0) Log.Error("Perk514." + where + " (potkniecie " + n + ")", e);
        }

        // ------------------------------------------------------------ BK: rozwiazanie i maska
        internal static bool Resolve()
        {
            if (_resolved) return true;
            if (_resolveTried) return false;
            _resolveTried = true;
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var mgrT = AccessTools.TypeByName("BannerKings.Managers.EducationManager");
                var eduT = AccessTools.TypeByName("BannerKings.Managers.Education.EducationData");
                if (cfgT == null || mgrT == null || eduT == null) { _bk = "BRAK typow BK"; return false; }
                _cfgInst = AccessTools.Property(cfgT, "Instance");
                _eduMgr = AccessTools.Property(cfgT, "EducationManager");
                _getEdu = AccessTools.Method(mgrT, "GetHeroEducation", new[] { typeof(Hero) });
                _hasPerk = AccessTools.Method(eduT, "HasPerk", new[] { typeof(PerkObject) });
                _eduLifestyle = AccessTools.Property(eduT, "Lifestyle");
                var om = MBObjectManager.Instance;
                if (om == null) { _bk = "BRAK MBObjectManager"; _resolveTried = false; return false; }
                PAristo = om.GetObject<PerkObject>("LordshipAristocraticRites");
                PRogue = om.GetObject<PerkObject>("LordshipRogueConnections");
                PTrad = om.GetObject<PerkObject>("LordshipTraditionalist");
                PMilAdm = om.GetObject<PerkObject>("LordshipMilitaryAdministration");
                PDipTies = om.GetObject<PerkObject>("LordshipDiplomaticTies");
                PPeer = om.GetObject<PerkObject>("ScholarshipPeerReview");
                PPoly = om.GetObject<PerkObject>("ScholarshipPolyglot");
                PLith = om.GetObject<PerkObject>("TheologyLithurgy");
                int found = 0;
                for (int i = 0; i < LsIds.Length; i++) { _ls[i] = om.GetObject<PerkObject>(LsIds[i]); if (_ls[i] != null) found++; }
                int skill = (PAristo != null ? 1 : 0) + (PRogue != null ? 1 : 0) + (PTrad != null ? 1 : 0) + (PMilAdm != null ? 1 : 0)
                            + (PDipTies != null ? 1 : 0) + (PPeer != null ? 1 : 0) + (PPoly != null ? 1 : 0) + (PLith != null ? 1 : 0);
                _resolved = _cfgInst != null && _eduMgr != null && _getEdu != null && _hasPerk != null;
                _bk = (_resolved ? "TAK" : "NIE (edukacja BK)") + ", perki umiejetnosci " + skill + "/8, perki stylow " + found + "/" + LsIds.Length;
                return _resolved;
            }
            catch (Exception e) { _bk = "wyjatek " + e.GetType().Name; Stumble("Resolve", e); return false; }
        }

        private static object Edu(Hero h)
        {
            var cfg = _cfgInst.GetValue(null, null);
            var mgr = cfg != null ? _eduMgr.GetValue(cfg, null) : null;
            return mgr != null ? _getEdu.Invoke(mgr, new object[] { h }) : null;
        }

        /// <summary>Maska perkow stylow zycia bohatera (bity B*), liczona raz na dobe gry. 0 = brak BK / brak perkow / blad.</summary>
        internal static int Mask(Hero h)
        {
            if (h == null || !Resolve()) return 0;
            int day;
            try { day = (int)CampaignTime.Now.ToDays; } catch { day = 0; }
            if (day != _maskDay) { _mask.Clear(); _maskDay = day; }
            int m;
            if (_mask.TryGetValue(h, out m)) return m;
            m = 0;
            try
            {
                var edu = Edu(h);
                if (edu != null)
                {
                    var arg = new object[1];
                    for (int i = 0; i < _ls.Length; i++)
                    {
                        if (_ls[i] == null) continue;
                        arg[0] = _ls[i];
                        if ((bool)_hasPerk.Invoke(edu, arg)) m |= 1 << i;
                    }
                    if (_eduLifestyle != null)
                    {
                        var ls = _eduLifestyle.GetValue(edu, null) as MBObjectBase;
                        if (ls != null && ls.StringId == "lifestyle_kheshig") m |= BKheshigLs;
                    }
                }
            }
            catch (Exception e) { Stumble("Mask", e); }
            _mask[h] = m;
            return m;
        }

        internal static bool Has(Hero h, int bit) { return h != null && (Mask(h) & bit) != 0; }

        private static bool SkillPerk(Hero h, PerkObject p)
        {
            try { return h != null && p != null && h.GetPerkValue(p); } catch { return false; }
        }

        private static bool IsRuler(Hero h)
        {
            var k = h != null && h.Clan != null ? h.Clan.Kingdom : null;
            return k != null && k.Leader == h;
        }

        // ------------------------------------------------------------ wpiecie (OnSessionLaunched)
        private static void Hook(Type t, string name, Type[] args, string post, string pre = null, string fin = null, int prio = Priority.Normal)
        {
            string label = (t != null ? t.Name : "?") + "." + name;
            try
            {
                if (t == null) { _hookMiss.Add(label + " (brak typu)"); return; }
                var m = args != null ? AccessTools.DeclaredMethod(t, name, args) : AccessTools.DeclaredMethod(t, name);
                if (m == null) { _hookMiss.Add(label + " (brak metody)"); return; }
                _h.Patch(m,
                    prefix: pre != null ? new HarmonyMethod(typeof(Perk514), pre) { priority = prio } : null,
                    postfix: post != null ? new HarmonyMethod(typeof(Perk514), post) { priority = prio } : null,
                    finalizer: fin != null ? new HarmonyMethod(typeof(Perk514), fin) { priority = prio } : null);
                bool ok = false;
                var info = Harmony.GetPatchInfo(m);
                if (info != null)
                {
                    foreach (var p in info.Postfixes) if (p.owner == Owner) ok = true;
                    foreach (var p in info.Prefixes) if (p.owner == Owner) ok = true;
                    foreach (var p in info.Finalizers) if (p.owner == Owner) ok = true;
                }
                if (ok) _hookOk.Add(label); else _hookMiss.Add(label + " (nie widac w GetPatchInfo)");
            }
            catch (Exception e) { _hookMiss.Add(label + " (wyjatek " + e.GetType().Name + ")"); Stumble("Hook " + label, e); }
        }

        private static bool BkPostfixOn(MethodBase m)
        {
            try
            {
                var info = m != null ? Harmony.GetPatchInfo(m) : null;
                if (info == null) return false;
                foreach (var p in info.Postfixes) if (p != null && p.owner == "BannerKings") return true;
            }
            catch { }
            return false;
        }

        /// <summary>OnSessionLaunched: latki kampanii (raz na proces), zerowanie licznikow doby, linia startu.</summary>
        internal static void EnsureHooks()
        {
            ResetDay();
            _mask.Clear(); _maskDay = int.MinValue;
            _resolved = false; _resolveTried = false;   // nowa kampania / wczytanie: obiekty perkow moga byc nowe - ponowne szukanie
            Resolve();
            _bkHealthPatched = null;
            if (!_hooksTried && AccessTools.TypeByName("BannerKings.BannerKingsConfig") != null)
            {
                _hooksTried = true;
                try
                {
                    if (_h == null) _h = new Harmony(Owner);
                    var tCouncil = AccessTools.TypeByName("BannerKings.Models.BKModels.BKCouncilModel");
                    var tStab = AccessTools.TypeByName("BannerKings.Models.BKModels.BKStabilityModel");
                    var tDip = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKDiplomacyModel");
                    var tVol = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKVolunteerModel");
                    var tRel = AccessTools.TypeByName("BannerKings.Models.BKModels.BKRelationsModel");
                    var tBook = AccessTools.TypeByName("BannerKings.Managers.Education.Books.BookType");
                    var tEduM = AccessTools.TypeByName("BannerKings.Models.BKModels.BKEducationModel");
                    var tCons = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKPartyConsumptionModel");
                    var tCouncilData = AccessTools.TypeByName("BannerKings.Managers.Court.CouncilData");
                    var tHeroRel = AccessTools.TypeByName("BannerKings.Behaviours.Relations.HeroRelations");
                    var tRelMod = AccessTools.TypeByName("BannerKings.Behaviours.Relations.RelationsModifier");
                    _councilOwner = tCouncilData != null ? AccessTools.Property(tCouncilData, "Owner") : null;
                    _relHero = tHeroRel != null ? AccessTools.Property(tHeroRel, "Hero") : null;
                    _relModCtor = tRelMod != null ? AccessTools.Constructor(tRelMod, new[] { typeof(int), typeof(TextObject) }) : null;
                    _bookSkill = tBook != null ? AccessTools.Property(tBook, "Skill") : null;
                    _bookUse = tBook != null ? AccessTools.Property(tBook, "Use") : null;

                    // 70 AristocraticRites
                    Hook(tCouncil, "CalculateGraceTarget", null, nameof(GracePost));
                    Hook(tStab, "CalculateVassalLimit", new[] { typeof(Hero) }, nameof(VassalPost));
                    // 71 RogueConnections
                    Hook(tDip, "MercenaryJoinScore", null, nameof(MercJoinPost));
                    Hook(tDip, "MercenaryLeaveScore", null, nameof(MercLeavePost));
                    Hook(tDip, "GetMercenaryDownPayment", null, nameof(MercDownPost));
                    // 73 Traditionalist / MilitaryAdministration
                    Hook(tVol, "GetMilitarism", null, nameof(MilitarismPost));
                    // 75 DiplomaticTies
                    Hook(tRel, "CalculateModifiers", null, nameof(RelModPost));
                    // 77 PeerReview, Polyglot
                    Hook(tBook, "FinishBook", new[] { typeof(Hero) }, nameof(BookPost), nameof(BookPre));
                    Hook(tEduM, "CalculateLanguageLearningRate", null, nameof(LangPost));
                    // 82 SeaToughness (jedzenie), 85 Inspirer + 82 SeaToughness (morale), 82 SeaCommander (liczebnosc, slawa)
                    Hook(tCons, "CalculateDailyFoodConsumptionf", null, nameof(FoodPost));
                    Hook(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultPartyMoraleModel), "GetEffectivePartyMorale", null, nameof(MoralePost), prio: Priority.Low);
                    Hook(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel), "GetPartyMemberSizeLimit", null, nameof(SizePost));
                    Hook(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel), "CalculateRenownGain", null, nameof(RenownPost));
                    // 90 HonorGuard
                    Hook(tVol, "CalculateMaximumRecruitmentIndex", null, nameof(RecruitIndexPost));
                    // 97 Pathfinder (predkosc), SeaEyes (tylko noca)
                    Hook(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel), "CalculateFinalSpeed", null, nameof(SpeedPost));
                    var tNavVis = AccessTools.TypeByName("NavalDLC.GameComponents.NavalDLCMapVisibilityModel");
                    var mNavVis = tNavVis != null ? AccessTools.DeclaredMethod(tNavVis, "GetPartySpottingRange") : null;
                    _bkSeaEyesPatched = BkPostfixOn(mNavVis);
                    if (_bkSeaEyesPatched) Hook(tNavVis, "GetPartySpottingRange", null, nameof(SpotPost), prio: Priority.Low);
                    else _hookMiss.Add("NavalDLCMapVisibilityModel.GetPartySpottingRange (bez postfiksu BK - SeaEyes nie dodaje nic, nie ma czego zdejmowac za dnia)");
                    // 98 Gladiator
                    var mBet = AccessTools.DeclaredMethod(typeof(SandBox.Tournaments.MissionLogics.TournamentBehavior), "GetMaximumBet");
                    _bkBetPatched = BkPostfixOn(mBet);
                    Hook(typeof(SandBox.Tournaments.MissionLogics.TournamentBehavior), "GetMaximumBet", null, null, fin: nameof(BetFin));
                    var mRen = AccessTools.DeclaredMethod(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultTournamentModel), "GetRenownReward");
                    _bkRenownPatched = BkPostfixOn(mRen);
                    Hook(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultTournamentModel), "GetRenownReward", null, nameof(TourRenownPost), prio: Priority.Low);
                }
                catch (Exception e) { _hookMiss.Add("wyjatek " + e.GetType().Name); Stumble("EnsureHooks", e); }
            }
            Log.Info(P + "start sesji | klucze: dwor i nauka (BkPerkHalvesCourt) " + (CourtOn ? "TAK" : "NIE") + ", style zycia na mapie (BkPerkHalvesLifestyle) "
                     + (LifeOn ? "TAK" : "NIE") + ", style zycia w bitwie (BkPerkHalvesBattle) " + (BattleOn ? "TAK" : "NIE")
                     + " | BK " + _bk + " | latki kampanii " + _hookOk.Count + " wpiete" + (_hookMiss.Count > 0 ? ", NIE: " + string.Join("; ", _hookMiss) : "")
                     + " | latki misji (SubModuleMain) " + BattleHookState()
                     + " | BK ma postfiks: zaklad turnieju " + (_bkBetPatched ? "TAK" : "NIE") + ", slawa turnieju " + (_bkRenownPatched ? "TAK" : "NIE")
                     + ", wzrok morski SeaEyes " + (_bkSeaEyesPatched ? "TAK" : "NIE")
                     + " | pominiete (bez systemu w grze): RogueConnections odprawa (BK jej nie wyplaca), SeaEyes zasadzki, RaidMaster lup, CivilOverseer limit infrastruktury,"
                     + " ArtisanEntrepeneur podatek (zywy przez Seal31B) | potkniecia " + _stumbles + ".");
        }

        private static void ResetDay()
        {
            dGrace = dVassal = dMercJoin = dMercLeave = dMercDown = dMilit = dRelMod = dPeer = dPoly = dLith = dSeaMor = dSeaFood = dSeaSize = dSeaRen = 0;
            dInspirer = dHonor = dHelms = dPath = dSeaEyesDay = dBet = dCrowds = dGhazw = dHorseRider = dHorseNoRider = 0;
            _dPeerXp = 0; _dStumbles = 0;
            ResetBattleDay();
        }

        // ============================================================ 70 AristocraticRites
        /// <summary>BKCouncilModel.CalculateGraceTarget: wlasciciel dworu z perkiem - laska (Grace) x1.04, wladca krolestwa x1.08.</summary>
        public static void GracePost(object __0, ref ExplainedNumber __result)
        {
            if (!CourtOn || PAristo == null || __0 == null || _councilOwner == null) return;
            try
            {
                var owner = _councilOwner.GetValue(__0, null) as Hero;
                if (!SkillPerk(owner, PAristo)) return;
                __result.AddFactor(IsRuler(owner) ? 0.08f : 0.04f, PAristo.Name);
                Interlocked.Increment(ref dGrace);
            }
            catch (Exception e) { Stumble("GracePost", e); }
        }

        /// <summary>BKStabilityModel.CalculateVassalLimit: perk - limit wasali +1.</summary>
        public static void VassalPost(Hero hero, ref ExplainedNumber __result)
        {
            if (!CourtOn || PAristo == null || !SkillPerk(hero, PAristo)) return;
            try { __result.Add(1f, PAristo.Name); Interlocked.Increment(ref dVassal); }
            catch (Exception e) { Stumble("VassalPost", e); }
        }

        // ============================================================ 71 RogueConnections (perk wladcy krolestwa-pracodawcy)
        private static Hero RulerOf(Kingdom k) { return k != null ? (k.Leader ?? (k.RulingClan != null ? k.RulingClan.Leader : null)) : null; }

        /// <summary>BKDiplomacyModel.MercenaryJoinScore: chec kompanii do sluzby u krolestwa z wladca-perkiem +10% (wartosci bezwzglednej wyniku).</summary>
        public static void MercJoinPost(Kingdom __1, ref ExplainedNumber __result)
        {
            if (!CourtOn || PRogue == null || !SkillPerk(RulerOf(__1), PRogue)) return;
            try
            {
                float v = __result.ResultNumber;
                if (v == 0f) return;
                __result.Add(Math.Abs(v) * 0.1f, PRogue.Name);
                Interlocked.Increment(ref dMercJoin);
            }
            catch (Exception e) { Stumble("MercJoinPost", e); }
        }

        /// <summary>BKDiplomacyModel.MercenaryLeaveScore: chec odejscia ze sluzby -10% (wartosci bezwzglednej wyniku).</summary>
        public static void MercLeavePost(Kingdom __1, ref ExplainedNumber __result)
        {
            if (!CourtOn || PRogue == null || !SkillPerk(RulerOf(__1), PRogue)) return;
            try
            {
                float v = __result.ResultNumber;
                if (v == 0f) return;
                __result.Add(-Math.Abs(v) * 0.1f, PRogue.Name);
                Interlocked.Increment(ref dMercLeave);
            }
            catch (Exception e) { Stumble("MercLeavePost", e); }
        }

        /// <summary>BKDiplomacyModel.GetMercenaryDownPayment: zaliczka placona przez wladce z perkiem -5% (placi mniej - nic z niczego).</summary>
        public static void MercDownPost(Kingdom __1, ref ExplainedNumber __result)
        {
            if (!CourtOn || PRogue == null || !SkillPerk(RulerOf(__1), PRogue)) return;
            try { __result.AddFactor(-0.05f, PRogue.Name); Interlocked.Increment(ref dMercDown); }
            catch (Exception e) { Stumble("MercDownPost", e); }
        }

        // ============================================================ 73 Traditionalist / MilitaryAdministration
        /// <summary>BKVolunteerModel.GetMilitarism: BK dodaje perki jako czynnik (+1% / +2% WZGLEDNIE od bazy 0.1, czyli +0.001 / +0.002), opis mowi
        /// o punktach (flat). Te same warunki co BK (pan osady, ta sama kultura): czynnik zdjety, punkt dodany (+0.01 / +0.02).</summary>
        public static void MilitarismPost(TaleWorlds.CampaignSystem.Settlements.Settlement __0, ref ExplainedNumber __result)
        {
            if (!CourtOn || __0 == null) return;
            try
            {
                var lead = __0.OwnerClan != null ? __0.OwnerClan.Leader : null;
                if (lead == null || __0.Culture != lead.Culture) return;
                bool hit = false;
                if (PTrad != null && SkillPerk(lead, PTrad)) { __result.AddFactor(-0.01f, PTrad.Name); __result.Add(0.01f, PTrad.Name); hit = true; }
                if (PMilAdm != null && SkillPerk(lead, PMilAdm)) { __result.AddFactor(-0.02f, PMilAdm.Name); __result.Add(0.02f, PMilAdm.Name); hit = true; }
                if (hit) Interlocked.Increment(ref dMilit);
            }
            catch (Exception e) { Stumble("MilitarismPost", e); }
        }

        // ============================================================ 75 DiplomaticTies
        /// <summary>BKRelationsModel.CalculateModifiers(relacje wladcy, cel): wladca krolestwa wobec obcego (spoza swojego krolestwa) z perkiem -
        /// cel relacji +3 (BK przesuwa relacje do celu o 1 tygodniowo; BK nie przesuwa relacji z GRACZEM - u gracza perk nie zmienia nic).</summary>
        public static void RelModPost(object __0, Hero __1, object __result)
        {
            if (!CourtOn || PDipTies == null || __0 == null || __1 == null || _relHero == null || _relModCtor == null) return;
            try
            {
                var hero = _relHero.GetValue(__0, null) as Hero;
                if (hero == null || hero == __1 || !IsRuler(hero) || !SkillPerk(__1, PDipTies)) return;
                var k = hero.Clan.Kingdom;
                if (__1.Clan != null && __1.Clan.Kingdom == k) return;
                var list = __result as IList;
                if (list == null) return;
                list.Add(_relModCtor.Invoke(new object[] { 3, PDipTies.Name }));
                Interlocked.Increment(ref dRelMod);
            }
            catch (Exception e) { Stumble("RelModPost", e); }
        }

        // ============================================================ 77 PeerReview, Polyglot
        /// <summary>Prefiks BookType.FinishBook: czy skupienie umiejetnosci ksiazki jest juz pelne (wtedy BK daje 1000 XP zamiast punktu).</summary>
        public static void BookPre(object __instance, Hero hero, out bool __state)
        {
            __state = false;
            try
            {
                if (!CourtOn || PPeer == null || hero == null || _bookSkill == null) return;
                var sk = _bookSkill.GetValue(__instance, null) as SkillObject;
                if (sk == null || hero.HeroDeveloper == null) return;
                __state = hero.HeroDeveloper.GetFocus(sk) >= Campaign.Current.Models.CharacterDevelopmentModel.MaxFocusPerSkill;
            }
            catch (Exception e) { Stumble("BookPre", e); }
        }

        /// <summary>Postfiks BookType.FinishBook: czytelnik z perkiem PeerReview - drugi raz to samo XP umiejetnosci (podrecznik 1500; inna ksiazka 300,
        /// a przy pelnym skupieniu jeszcze 1000). Punkt skupienia nie jest podwajany (to nie XP).</summary>
        public static void BookPost(object __instance, Hero hero, bool __state)
        {
            if (!CourtOn || PPeer == null || hero == null || _bookSkill == null || !SkillPerk(hero, PPeer)) return;
            try
            {
                var sk = _bookSkill.GetValue(__instance, null) as SkillObject;
                if (sk == null) return;
                var use = _bookUse != null ? _bookUse.GetValue(__instance, null) : null;
                float xp = use != null && use.ToString() == "Skillbook" ? 1500f : (300f + (__state ? 1000f : 0f));
                hero.AddSkillXp(sk, xp);
                Interlocked.Increment(ref dPeer);
                Interlocked.Add(ref _dPeerXp, (long)xp);
            }
            catch (Exception e) { Stumble("BookPost", e); }
        }

        /// <summary>BKEducationModel.CalculateLanguageLearningRate: uczen z perkiem Polyglot (przy nauczycielu) - nauka jezyka +50%.</summary>
        public static void LangPost(Hero __0, Hero __1, ref ExplainedNumber __result)
        {
            if (!CourtOn || PPoly == null || __1 == null || !SkillPerk(__0, PPoly)) return;
            try { __result.AddFactor(0.5f, PPoly.Name); Interlocked.Increment(ref dPoly); }
            catch (Exception e) { Stumble("LangPost", e); }
        }

        // ============================================================ 82 / 85 morale, jedzenie, liczebnosc, slawa
        /// <summary>DefaultPartyMoraleModel.GetEffectivePartyMorale (po BK): SeaToughness +5% morale; Inspirer przy stylu Kheshig - polowa kary BK
        /// za ludzi spoza kultury khuzait wraca (jedyna kara morale za roznice kultur w grze z modami Jeffa).</summary>
        public static void MoralePost(TaleWorlds.CampaignSystem.Party.MobileParty mobileParty, ref ExplainedNumber __result)
        {
            if (!LifeOn || mobileParty == null) return;
            var h = mobileParty.LeaderHero;
            if (h == null) return;
            try
            {
                int m = Mask(h);
                if (m == 0) return;
                if ((m & BSeaTough) != 0) { __result.AddFactor(0.05f, NameOf(BSeaTough)); Interlocked.Increment(ref dSeaMor); }
                if ((m & BInspirer) != 0 && (m & BKheshigLs) != 0)
                {
                    float n = 0f;
                    foreach (var el in mobileParty.MemberRoster.GetTroopRoster())
                        if (el.Character != null && el.Character.Culture != null && el.Character.Culture.StringId != "khuzait") n += el.Number;
                    if (n > 0f) { __result.Add(n * 0.025f, NameOf(BInspirer)); Interlocked.Increment(ref dInspirer); }
                }
            }
            catch (Exception e) { Stumble("MoralePost", e); }
        }

        /// <summary>BKPartyConsumptionModel.CalculateDailyFoodConsumptionf: SeaToughness - jedzenie partii -5% (dziala, gdy lancuch FoodChain biegnie przez BK).</summary>
        public static void FoodPost(TaleWorlds.CampaignSystem.Party.MobileParty __0, ref ExplainedNumber __result)
        {
            if (!LifeOn || __0 == null || !Has(__0.LeaderHero, BSeaTough)) return;
            try { __result.AddFactor(-0.05f, NameOf(BSeaTough)); Interlocked.Increment(ref dSeaFood); }
            catch (Exception e) { Stumble("FoodPost", e); }
        }

        /// <summary>DefaultPartySizeLimitModel.GetPartyMemberSizeLimit: SeaCommander - liczebnosc partii +8.</summary>
        public static void SizePost(TaleWorlds.CampaignSystem.Party.PartyBase __0, ref ExplainedNumber __result)
        {
            if (!LifeOn || __0 == null || __0.MobileParty == null || !Has(__0.MobileParty.LeaderHero, BSeaCmd)) return;
            try { __result.Add(8f, NameOf(BSeaCmd)); Interlocked.Increment(ref dSeaSize); }
            catch (Exception e) { Stumble("SizePost", e); }
        }

        /// <summary>DefaultBattleRewardModel.CalculateRenownGain (__0 = zwycieska partia): SeaCommander - slawa ze zwyciestw +10%.</summary>
        public static void RenownPost(TaleWorlds.CampaignSystem.Party.PartyBase __0, ref ExplainedNumber __result)
        {
            if (!LifeOn || __0 == null || !Has(__0.LeaderHero, BSeaCmd)) return;
            try { __result.AddFactor(0.1f, NameOf(BSeaCmd)); Interlocked.Increment(ref dSeaRen); }
            catch (Exception e) { Stumble("RenownPost", e); }
        }

        // ============================================================ 90 HonorGuard
        /// <summary>BKVolunteerModel.CalculateMaximumRecruitmentIndex (__0 = kupujacy): HonorGuard - poziom werbunku u notabli +1 (sufit BK zostaje).</summary>
        public static void RecruitIndexPost(Hero __0, ref ExplainedNumber __result)
        {
            if (!LifeOn || !Has(__0, BHonor)) return;
            try { __result.Add(1f, NameOf(BHonor)); Interlocked.Increment(ref dHonor); }
            catch (Exception e) { Stumble("RecruitIndexPost", e); }
        }

        // ============================================================ 97 Pathfinder, SeaEyes
        /// <summary>DefaultPartySpeedCalculatingModel.CalculateFinalSpeed: Pathfinder - partia +3% w lesie i na plazy (wybrzezu).</summary>
        public static void SpeedPost(TaleWorlds.CampaignSystem.Party.MobileParty __0, ref ExplainedNumber __result)
        {
            if (!LifeOn || __0 == null) return;
            var h = __0.LeaderHero;
            if (h == null || !Has(h, BPath)) return;
            try
            {
                var tt = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(__0.CurrentNavigationFace);
                if (tt != TerrainType.Forest && tt != TerrainType.Beach) return;
                __result.AddFactor(0.03f, NameOf(BPath));
                Interlocked.Increment(ref dPath);
            }
            catch (Exception e) { Stumble("SpeedPost", e); }
        }

        /// <summary>NavalDLCMapVisibilityModel.GetPartySpottingRange (po BK): BK dodaje SeaEyes +8% zawsze, opis mowi "noca" - za dnia zdjete.</summary>
        public static void SpotPost(TaleWorlds.CampaignSystem.Party.MobileParty __0, ref ExplainedNumber __result)
        {
            if (!LifeOn || __0 == null) return;
            var h = __0.LeaderHero ?? __0.Owner;
            if (h == null || !Has(h, BSeaEyes)) return;
            try
            {
                if (Campaign.Current.IsNight) return;
                __result.AddFactor(-0.08f, NameOf(BSeaEyes));
                Interlocked.Increment(ref dSeaEyesDay);
            }
            catch (Exception e) { Stumble("SpotPost", e); }
        }

        // ============================================================ 98 Gladiator
        /// <summary>Finalizer TournamentBehavior.GetMaximumBet (po wszystkich postfiksach, takze TXP o priorytecie 0): TourDeCalradia - podwojny zaklad
        /// zamiast +150 BK. Baza = to, co daloby bez BK (gra 150 x DeepPockets, albo wiecej z TXP); z TXP najwyzej zloto gracza (jak TXP).</summary>
        public static Exception BetFin(Exception __exception, ref int __result)
        {
            if (__exception != null || !LifeOn) return __exception;
            try
            {
                var me = Hero.MainHero;
                if (!Has(me, BTour)) return null;
                int vanilla = 150;
                try { if (me.GetPerkValue(DefaultPerks.Roguery.DeepPockets)) vanilla *= (int)DefaultPerks.Roguery.DeepPockets.PrimaryBonus; } catch { }
                int b = __result;
                if (_bkBetPatched && b == vanilla + 150) b = vanilla;   // tylko +150 BK - zdjete
                int doubled = b * 2;
                if (b > vanilla + 150) doubled = Math.Min(doubled, Math.Max(b, me.Gold));   // TXP: najwyzej zloto gracza
                __result = Math.Max(__result, doubled);
                Interlocked.Increment(ref dBet);
            }
            catch (Exception e) { Stumble("BetFin", e); }
            return null;
        }

        /// <summary>DefaultTournamentModel.GetRenownReward (po BK): CrowdsFavorite - podwojna slawa za wygrany turniej zamiast +3 BK.</summary>
        public static void TourRenownPost(Hero winner, ref int __result)
        {
            if (!LifeOn || !Has(winner, BCrowds)) return;
            try
            {
                int b = _bkRenownPatched ? Math.Max(0, __result - 3) : __result;
                __result = Math.Max(__result, b * 2);
                Interlocked.Increment(ref dCrowds);
            }
            catch (Exception e) { Stumble("TourRenownPost", e); }
        }

        // ============================================================ doba: 79 Lithurgy, 97 Helmsman, linia
        /// <summary>ArmouryBehavior.OnDailyTickBodyInner: losowe relacje z kaplanami (Lithurgy), szybsze odrabianie morale (Helmsman), linia doby.</summary>
        internal static void Daily(int day)
        {
            if (CourtOn && PLith != null)
            {
                try
                {
                    foreach (var clan in Clan.All)
                    {
                        if (clan == null || clan.IsEliminated || clan.IsBanditFaction || clan.Heroes == null) continue;
                        foreach (var h in clan.Heroes)
                        {
                            if (h == null || !h.IsAlive || !SkillPerk(h, PLith)) continue;
                            if (MBRandom.RandomFloat >= 0.2f / 7f) continue;   // BK: 0.2 (opis "losowo") - ok. raz na 5 tygodni
                            var pool = new List<Hero>();
                            foreach (var st in clan.Settlements)
                                if (st != null && st.Notables != null)
                                    foreach (var n in st.Notables) if (n != null && n.IsAlive && n.IsPreacher) pool.Add(n);
                            if (pool.Count == 0) continue;
                            var who = pool[MBRandom.RandomInt(pool.Count)];
                            TaleWorlds.CampaignSystem.Actions.ChangeRelationAction.ApplyRelationChangeBetweenHeroes(h, who, 1, h == Hero.MainHero);
                            dLith++;
                        }
                    }
                }
                catch (Exception e) { Stumble("Daily(Lithurgy)", e); }
            }
            if (LifeOn)
            {
                try
                {
                    foreach (var mp in TaleWorlds.CampaignSystem.Party.MobileParty.AllLordParties)
                    {
                        if (mp == null || !mp.IsActive || mp.RecentEventsMorale >= 0f || !Has(mp.LeaderHero, BHelms)) continue;
                        mp.RecentEventsMorale -= mp.RecentEventsMorale * 0.01f;   // gra odrabia 10% na dobe - perk +10% tego
                        dHelms++;
                    }
                }
                catch (Exception e) { Stumble("Daily(Helmsman)", e); }
            }
            try
            {
                string battle = BattleDayText();
                int any = dGrace + dVassal + dMercJoin + dMercLeave + dMercDown + dMilit + dRelMod + dPeer + dPoly + dLith + dSeaMor + dSeaFood + dSeaSize
                          + dSeaRen + dInspirer + dHonor + dHelms + dPath + dSeaEyesDay + dBet + dCrowds + dGhazw;
                if (any > 0 || _dStumbles > 0 || battle != null)
                {
                    Log.Info(P + "dzien " + day + " | dwor: laska " + N(dGrace) + ", wasale " + N(dVassal) + ", najemnicy chec / odejscie / zaliczka " + N(dMercJoin)
                             + " / " + N(dMercLeave) + " / " + N(dMercDown) + ", militaryzm (punkty) " + N(dMilit) + ", cel relacji z obcym wladca " + N(dRelMod)
                             + " | nauka: ksiazki x2 " + N(dPeer) + " (+" + _dPeerXp.ToString(Inv) + " XP), jezyk " + N(dPoly) + ", kaplani +1 " + N(dLith)
                             + " | style: morale Sea-Toughness " + N(dSeaMor) + ", jedzenie " + N(dSeaFood) + ", liczebnosc Sea Commander " + N(dSeaSize)
                             + ", slawa " + N(dSeaRen) + ", Inspirer " + N(dInspirer) + ", Honor Guard werbunek " + N(dHonor) + ", Helmsman morale " + N(dHelms)
                             + ", Pathfinder las/plaza " + N(dPath) + ", Sea-Eyes zdjete za dnia " + N(dSeaEyesDay) + ", zaklad x2 " + N(dBet) + ", slawa turnieju x2 "
                             + N(dCrowds) + ", konie Ghazw 12% " + N(dGhazw) + " (konie z jezdzcem / bez przy liczeniu HP " + N(dHorseRider) + " / " + N(dHorseNoRider) + ")"
                             + (battle != null ? " | bitwa: " + battle : "")
                             + " | potkniecia doby " + _dStumbles + " (razem " + _stumbles + ").");
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            ResetDay();
        }
    }
}
