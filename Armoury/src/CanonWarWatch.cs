using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 264 (4) - DIAGNOSTYKA DOBY WLADCOW WOJNY KANONU, TYLKO LOG (Jeff 27.10: "czemu dorzecze i polnoc jest slabsza od lannisterow i krolewskiej przystani";
    /// audyt: "linia w logu, co planuje Robb"). Niczego nie zmienia: odczyt stanu partii, ocen AI gry i StrategicCampaignAI145.
    /// Obserwowani: Robb (lord_5_1), wladca korony (vlandia - Joffrey lord_7_1), wladca Dorzecza (riverlands), glowa rodu clan_vlandia_1 (Tywin lord_4_1). Partie
    /// odswiezane co godzine (bramka 4 porownan referencji w latce mysli AI).
    /// Zrodla:
    ///  - postfiks Last CampaignEventDispatcher.AiHourlyTick (wzor CrossingLaw: po wszystkich sluchaczach i po naszych latkach celu - ArmsFirst VeryLow, CrossingLaw,
    ///    BesiegedRelief Low - przed wyborem gry): mysli doby, 3 najlepsze (zachowanie, cel, ocena, zbiorka armii), CurrentObjectiveValue;
    ///  - prog gry z AiPartyThinkBehavior.PartyHourlyAiTick: 0.03 dla PatrolAroundPoint / GoToSettlement, 0.1 inne; x2 przy zbiorce armii, x0.33 wodz armii;
    ///    Hold, RethinkAtNextHourlyTick albo CurrentObjectiveValue &lt; 0.05 - gra bierze najlepsza zawsze (ocena = 1) -> "najlepsza ponizej progu gry - zostaje
    ///    przy starym rozkazie";
    ///  - probka godziny (MapEvent, SiegeEvent / BesiegedSettlement, w osadzie) i sluchacz MapEventEnded (bitwy polowe, oblezenia, najazdy partii wladcy albo
    ///    jego armii; krolestwa - strona bitwy);
    ///  - stan partii: osada, ludzie / limit, dni jedzenia, Default / ShortTerm z celem, Ai.IsDisabled, DoNotMakeNewDecisions, armia (partie, ludzie, spojnosc);
    ///  - nasze wstrzymania: ArmsFirst.Unready / CapTripActive (p9), ArmyRestDay.Resting / NightHolds (m118, noc), LordEscort.HeldBack (223), GentryService (179);
    ///  - StrategicCampaignAI145 (odbiciem, StrategicAiState internal static): cel wojny, cel operacji, stan frakcji (OwnedStrength, EnemyStrength, fortece,
    ///    zagrozone, spladrowane wioski, IsExhausted, WantsPeace), zakaz nowej armii (IsArmyCreationOnCooldown). Model celow SCA mnozy cele oblezen
    ///    x0.55 przy wyczerpaniu, x0.35 przy ForcePeace / WantsPeace, x0.35 przy wodzu w niewoli, x0.25 gdy wrog w poblizu silniejszy (tylko cytat w tekscie).
    ///    Hipoteza do sprawdzenia: SCA sumuje CurrentTotalStrength WSZYSTKICH frakcji z FactionsAtWarWith (z bandami i Innymi) - linia krolestwa podaje
    ///    rozbicie wroga na krolestwa i reszte oraz wyczerpanie bez band.
    /// Linie: "Wojna kanonu - wladca (264): dzien D | ..." (4 na dobe) i "Wojna kanonu - krolestwo (264): dzien D | ..." (3 na dobe). Koszt: postfiks mysli -
    /// 4 porownania na mysl partii; godzina - 4 partie (DayProfile.Hr); doba - ok. 7 linii i kilka wywolan odbiciem.
    /// Zapis: brak (pamiec doby).
    /// </summary>
    internal static class CanonWarWatch
    {
        internal const string TagR = "Wojna kanonu - wladca (264): ", TagK = "Wojna kanonu - krolestwo (264): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string TywinClanId = "clan_vlandia_1";
        private static readonly string[] KingdomIds = { Prologue.NorthId, Prologue.Crown, Prologue.RiverId };

        private sealed class W
        {
            public string Label;
            public Hero Hero;
            public MobileParty Party;
            public int Thinks, Below, Hours, InTown, SiegeH, BattleH, Field, Siege, Raid, Other;
            public string Top = "-", LastThink = "-";
            public float Best = -1f, Thr, Obj;
            public void ZeroDay() { Thinks = Below = Hours = InTown = SiegeH = BattleH = Field = Siege = Raid = Other = 0; Top = "-"; LastThink = "-"; Best = -1f; Thr = 0f; Obj = 0f; }
        }

        private sealed class KD { public int Field, Siege, Raid; public void Zero() { Field = Siege = Raid = 0; } }

        private static readonly W[] _w = { new W { Label = "Robb" }, new W { Label = "wladca korony" }, new W { Label = "wladca Dorzecza" }, new W { Label = "glowa rodu " + TywinClanId } };
        private static readonly KD[] _k = { new KD(), new KD(), new KD() };
        private static readonly Kingdom[] _kRef = new Kingdom[3];
        // bramka latki mysli (odswiezana co godzine)
        private static MobileParty _p0, _p1, _p2, _p3;
        private static Hero _robb;
        private static Clan _tywinClan;
        private static int _heroDay = int.MinValue;
        private static int _stumbles, _dStumbles;
        private static bool _hooked;

        internal static void Reset()
        {
            foreach (var w in _w) { w.ZeroDay(); w.Hero = null; w.Party = null; }
            foreach (var k in _k) k.Zero();
            for (int i = 0; i < _kRef.Length; i++) _kRef[i] = null;
            _p0 = _p1 = _p2 = _p3 = null; _robb = null; _tywinClan = null; _heroDay = int.MinValue;
            _stumbles = _dStumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            if (_stumbles <= 5) { try { Log.Error("CanonWarWatch." + where + " (potkniecie " + _stumbles + ")", e); } catch { } }
        }

        private static bool On { get { var s = Settings.Current; return s != null && s.LogEnabled && Prologue.CanonHere; } }

        /// <summary>Latka mysli AI (raz na proces, z CanonPeaceLock.EnsureHooks): postfiks Last CampaignEventDispatcher.AiHourlyTick. Zwraca TAK / BRAK.</summary>
        internal static string Install(Harmony h)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                if (m == null) return "BRAK (CampaignEventDispatcher.AiHourlyTick)";
                h.Patch(m, postfix: new HarmonyMethod(typeof(CanonWarWatch), nameof(ThinkPostfix)) { priority = Priority.Last });
                _hooked = true;
                return "TAK";
            }
            catch (Exception e) { Stumble("Install", e); return "BLAD (" + e.GetType().Name + ")"; }
        }

        // ------------------------------------------------------------ mysl AI

        /// <summary>Prog gry z AiPartyThinkBehavior.PartyHourlyAiTick (opis klasy).</summary>
        internal static float GameThreshold(AiBehavior b, bool gather, bool armyLeader)
        {
            double t = b == AiBehavior.PatrolAroundPoint || b == AiBehavior.GoToSettlement ? 0.03 : 0.1;
            t *= gather ? 2f : armyLeader ? 0.33f : 1f;
            return (float)t;
        }

        private static string PointName(IMapPoint p)
        {
            try { return p != null && p.Name != null ? p.Name.ToString() : "punkt"; } catch { return "?"; }
        }

        /// <summary>Postfiks Last CampaignEventDispatcher.AiHourlyTick: obserwowana partia - 3 najlepsze oceny i prog gry (tylko odczyt). Nie rzuca.</summary>
        public static void ThinkPostfix(MobileParty __0, PartyThinkParams __1)
        {
            if (__0 == null || (__0 != _p0 && __0 != _p1 && __0 != _p2 && __0 != _p3)) return;
            try
            {
                W w = null;
                for (int i = 0; i < _w.Length; i++) if (_w[i].Party == __0) { w = _w[i]; break; }
                if (w == null || __1 == null) return;
                var list = __1.AIBehaviorScores;
                int n = list != null ? list.Count : 0;
                int i1 = -1, i2 = -1, i3 = -1;
                for (int i = 0; i < n; i++)
                {
                    float v = list[i].Item2;
                    if (i1 < 0 || v > list[i1].Item2) { i3 = i2; i2 = i1; i1 = i; }
                    else if (i2 < 0 || v > list[i2].Item2) { i3 = i2; i2 = i; }
                    else if (i3 < 0 || v > list[i3].Item2) i3 = i;
                }
                w.Thinks++;
                w.Obj = __1.CurrentObjectiveValue;
                var sb = new StringBuilder(160);
                int k = 0;
                foreach (int idx in new[] { i1, i2, i3 })
                {
                    if (idx < 0) continue;
                    var d = list[idx].Item1;
                    if (k > 0) sb.Append(", ");
                    sb.Append(++k).Append(". ").Append(d.AiBehavior).Append(' ').Append(d.Party != null ? PointName(d.Party) : "punkt").Append(' ')
                      .Append(list[idx].Item2.ToString("0.000", Inv)).Append(d.WillGatherArmy ? " zbiorka armii" : "");
                }
                if (i1 >= 0)
                {
                    var d1 = list[i1].Item1;
                    bool lead = __0.Army != null && __0.Army.LeaderParty == __0;
                    float thr = GameThreshold(d1.AiBehavior, d1.WillGatherArmy, lead);
                    bool always = __0.DefaultBehavior == AiBehavior.Hold || __0.Ai.RethinkAtNextHourlyTick || __1.CurrentObjectiveValue < 0.05f;
                    w.Best = list[i1].Item2; w.Thr = thr;
                    if (!always && list[i1].Item2 <= thr) w.Below++;
                }
                w.Top = k > 0 ? sb.ToString() : "brak ocen";
                w.LastThink = (int)CampaignTime.Now.ToHours % 24 + ":00";
            }
            catch (Exception e) { Stumble("ThinkPostfix", e); }
        }

        // ------------------------------------------------------------ godzina

        /// <summary>Co godzine (DayProfile.Hr): bohaterowie raz na dobe, partie i probka godziny.</summary>
        internal static void Hourly()
        {
            if (Campaign.Current == null || !On) { _p0 = _p1 = _p2 = _p3 = null; return; }
            try
            {
                int day = (int)CampaignTime.Now.ToDays;
                if (day != _heroDay)
                {
                    _heroDay = day;
                    _robb = Prologue.HeroById(Prologue.RobbId);
                    _tywinClan = Prologue.C(TywinClanId);
                    for (int i = 0; i < KingdomIds.Length; i++) _kRef[i] = Prologue.KingdomById(KingdomIds[i]);
                }
                _w[0].Hero = _robb;
                _w[1].Hero = _kRef[1] != null ? _kRef[1].Leader : null;
                _w[2].Hero = _kRef[2] != null ? _kRef[2].Leader : null;
                _w[3].Hero = _tywinClan != null ? _tywinClan.Leader : null;
                for (int i = 0; i < _w.Length; i++)
                {
                    var w = _w[i];
                    var h = w.Hero;
                    var mp = h != null && h.IsAlive && !h.IsPrisoner && h.PartyBelongedTo != null && h.PartyBelongedTo.LeaderHero == h ? h.PartyBelongedTo : null;
                    w.Party = mp;
                    if (mp == null) continue;
                    w.Hours++;
                    if (mp.CurrentSettlement != null) w.InTown++;
                    if (mp.MapEvent != null) w.BattleH++;
                    var lead = mp.Army != null ? mp.Army.LeaderParty : null;
                    if (mp.SiegeEvent != null || mp.BesiegedSettlement != null || (lead != null && lead.BesiegedSettlement != null)) w.SiegeH++;
                }
                _p0 = _w[0].Party; _p1 = _w[1].Party; _p2 = _w[2].Party; _p3 = _w[3].Party;
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        /// <summary>Sluchacz MapEventEnded: bitwy partii wladcow (albo ich armii) i krolestw wojny kanonu. Nie rzuca.</summary>
        internal static void OnMapEventEnded(MapEvent me)
        {
            if (me == null || (_kRef[0] == null && _kRef[1] == null && _kRef[2] == null)) return;
            try
            {
                IFaction fa = me.AttackerSide != null ? me.AttackerSide.MapFaction : null, fd = me.DefenderSide != null ? me.DefenderSide.MapFaction : null;
                int ka = KIndex(fa), kd = KIndex(fd);
                if (ka < 0 && kd < 0) return;
                int kind = Kind(me);
                if (ka >= 0) Bump(_k[ka], kind);
                if (kd >= 0 && kd != ka) Bump(_k[kd], kind);
                if (_p0 == null && _p1 == null && _p2 == null && _p3 == null) return;
                foreach (var w in _w)
                {
                    var p = w.Party;
                    if (p == null) continue;
                    bool hit = false;
                    foreach (var pb in me.InvolvedParties)
                    {
                        var mp = pb != null ? pb.MobileParty : null;
                        if (mp == null) continue;
                        if (mp == p || (mp.Army != null && mp.Army.LeaderParty == p)) { hit = true; break; }
                    }
                    if (!hit) continue;
                    if (kind == 0) w.Field++; else if (kind == 1) w.Siege++; else if (kind == 2) w.Raid++; else w.Other++;
                }
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        private static int KIndex(IFaction f)
        {
            if (f == null) return -1;
            for (int i = 0; i < _kRef.Length; i++) if (_kRef[i] != null && f == _kRef[i]) return i;
            return -1;
        }

        /// <summary>0 polowa, 1 oblezenie (szturm, wypad, przed murami, zasadzka), 2 najazd, 3 inne.</summary>
        private static int Kind(MapEvent me)
        {
            if (me.IsFieldBattle) return 0;
            if (me.IsSiegeAssault || me.IsSiegeOutside || me.IsSallyOut || me.IsSiegeAmbush) return 1;
            if (me.IsRaid) return 2;
            return 3;
        }

        private static void Bump(KD k, int kind) { if (kind == 0) k.Field++; else if (kind == 1) k.Siege++; else if (kind == 2) k.Raid++; }

        // ------------------------------------------------------------ StrategicCampaignAI145 (odbiciem)
        private static bool _scaTried;
        private static MethodInfo _mGoal, _mOp, _mStatus, _mCool;
        private static PropertyInfo _sOwn, _sEnemy, _sForts, _sThreat, _sWars, _sRaided, _sExh, _sPeace;

        private static bool ScaReady()
        {
            if (!_scaTried)
            {
                _scaTried = true;
                try
                {
                    var t = AccessTools.TypeByName("StrategicCampaignAI145.StrategicAiState");
                    if (t != null)
                    {
                        _mGoal = AccessTools.Method(t, "GetWarGoal", new[] { typeof(Kingdom) });
                        _mOp = AccessTools.Method(t, "GetOperationTarget", new[] { typeof(Kingdom) });
                        _mStatus = AccessTools.Method(t, "GetFactionStatus", new[] { typeof(IFaction) });
                        _mCool = AccessTools.Method(t, "IsArmyCreationOnCooldown", new[] { typeof(Hero) });
                        var st = _mStatus != null ? _mStatus.ReturnType : null;
                        if (st != null)
                        {
                            _sOwn = AccessTools.Property(st, "OwnedStrength"); _sEnemy = AccessTools.Property(st, "EnemyStrength");
                            _sForts = AccessTools.Property(st, "OwnedFortifications"); _sThreat = AccessTools.Property(st, "ThreatenedFortifications");
                            _sWars = AccessTools.Property(st, "ActiveWars"); _sRaided = AccessTools.Property(st, "RaidedVillages");
                            _sExh = AccessTools.Property(st, "IsExhausted"); _sPeace = AccessTools.Property(st, "WantsPeace");
                        }
                    }
                }
                catch (Exception e) { Stumble("ScaReady", e); }
            }
            return _mGoal != null && _mStatus != null && _sEnemy != null && _sOwn != null && _sExh != null;
        }

        private static object Get(PropertyInfo p, object o) { try { return p != null && o != null ? p.GetValue(o, null) : null; } catch { return null; } }

        private static string F0(object o) { try { return o == null ? "?" : Convert.ToDouble(o, Inv).ToString("0", Inv); } catch { return "?"; } }

        /// <summary>SCA: cel wojny, operacja, stan frakcji (tekst) i flagi do "dlaczego stoi".</summary>
        private static string Sca(Kingdom k, Hero h, out bool exhausted, out bool wantsPeace, out bool forcePeace, out bool armyCool)
        {
            exhausted = wantsPeace = forcePeace = armyCool = false;
            if (k == null) return "brak krolestwa";
            if (!ScaReady()) return "BRAK (StrategicCampaignAI145.StrategicAiState)";
            try
            {
                var goal = _mGoal.Invoke(null, new object[] { k });
                string g = goal != null ? goal.ToString() : "?";
                forcePeace = g == "ForcePeace";
                string op = "-";
                if (_mOp != null) { var s = _mOp.Invoke(null, new object[] { k }) as Settlement; op = s != null ? s.Name.ToString() : "brak"; }
                var st = _mStatus.Invoke(null, new object[] { k });
                exhausted = Get(_sExh, st) is bool b1 && b1;
                wantsPeace = Get(_sPeace, st) is bool b2 && b2;
                armyCool = h != null && _mCool != null && _mCool.Invoke(null, new object[] { h }) is bool b3 && b3;
                return "cel " + g + ", operacja " + op + ", wyczerpane " + (exhausted ? "tak" : "nie") + " (wrog " + F0(Get(_sEnemy, st)) + " / swoi " + F0(Get(_sOwn, st))
                       + ", fortece " + F0(Get(_sForts, st)) + " zagrozone " + F0(Get(_sThreat, st)) + ", wojen " + F0(Get(_sWars, st)) + ", wioski spladrowane " + F0(Get(_sRaided, st))
                       + "), chce pokoju " + (wantsPeace ? "tak" : "nie") + ", zakaz nowej armii " + (armyCool ? "tak" : "nie");
            }
            catch (Exception e) { Stumble("Sca", e); return "BLAD (" + e.GetType().Name + ")"; }
        }

        /// <summary>Hipoteza SCA (opis klasy): wrog jako suma CurrentTotalStrength wszystkich FactionsAtWarWith, z rozbiciem na krolestwa i reszte; wyczerpanie
        /// wzorem SCA (wrog &gt; swoi x 1.35) z wszystkimi i bez band.</summary>
        private static string Enemy(Kingdom k)
        {
            try
            {
                double all = 0, kings = 0, own = k.CurrentTotalStrength;
                int nAll = 0, nK = 0;
                foreach (var f in k.FactionsAtWarWith)
                {
                    if (f == null) continue;
                    double v = f.CurrentTotalStrength;
                    all += v; nAll++;
                    if (f.IsKingdomFaction) { kings += v; nK++; }
                }
                return "wrog wedlug SCA " + all.ToString("0", Inv) + " (" + nAll + " frakcji; krolestwa " + kings.ToString("0", Inv) + " z " + nK + ", bandy i inni " + (all - kings).ToString("0", Inv)
                       + "), swoi " + own.ToString("0", Inv) + " -> wyczerpane wzorem SCA (wrog > swoi x 1.35): z bandami " + (all > own * 1.35 ? "tak" : "nie") + ", bez band " + (kings > own * 1.35 ? "tak" : "nie");
            }
            catch (Exception e) { Stumble("Enemy", e); return "wrog ?"; }
        }

        // ------------------------------------------------------------ doba

        internal static void Daily()
        {
            if (Campaign.Current == null) return;
            if (!On) { foreach (var w in _w) w.ZeroDay(); foreach (var k in _k) k.Zero(); return; }
            int day = (int)CampaignTime.Now.ToDays;
            for (int i = 0; i < _w.Length; i++)
            {
                try { Log.Info(RulerLine(_w[i], day)); }
                catch (Exception e) { Stumble("RulerLine", e); }
            }
            for (int i = 0; i < KingdomIds.Length; i++)
            {
                try { Log.Info(KingdomLine(i, day)); }
                catch (Exception e) { Stumble("KingdomLine", e); }
            }
            foreach (var w in _w) w.ZeroDay();
            foreach (var k in _k) k.Zero();
            _dStumbles = 0;
        }

        private static string HeroText(Hero h)
        {
            if (h == null) return "brak bohatera";
            string k = h.MapFaction != null && h.MapFaction.Name != null ? h.MapFaction.Name.ToString() : "-";
            return h.Name + " (" + h.StringId + ", " + k + ")";
        }

        /// <summary>Linia wladcy (format w opisie klasy; autotest T7 liczy pola).</summary>
        private static string RulerLine(W w, int day)
        {
            var sb = new StringBuilder(700);
            var h = w.Hero;
            sb.Append(TagR).Append("dzien ").Append(day).Append(" | ").Append(w.Label).Append(": ").Append(HeroText(h));
            var why = new List<string>();
            var mp = w.Party;
            Kingdom k = h != null ? h.Clan != null ? h.Clan.Kingdom : null : null;
            // partia
            sb.Append(" | partia: ");
            if (h == null) { sb.Append("-"); why.Add("brak bohatera"); }
            else if (!h.IsAlive) { sb.Append("nie zyje"); why.Add("nie zyje"); }
            else if (h.IsPrisoner)
            {
                var cp = h.PartyBelongedToAsPrisoner;
                bool player = cp != null && (cp == PartyBase.MainParty || (cp.MobileParty != null && cp.MobileParty.IsMainParty) || cp.Owner == Hero.MainHero);
                sb.Append("w niewoli u ").Append(cp != null && cp.Name != null ? cp.Name.ToString() : "?").Append(player ? " (gracza)" : "");
                why.Add(player ? "w niewoli u gracza" : "w niewoli");
            }
            else if (mp == null)
            {
                var st = h.CurrentSettlement;
                sb.Append("bez wlasnej partii").Append(st != null ? " (w " + st.Name + ")" : "");
                why.Add("bez wlasnej partii");
            }
            else
            {
                int men = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
                int lim = mp.Party != null ? mp.Party.PartySizeLimit : 0;
                float ch = mp.FoodChange;
                string food = ch < -0.01f ? (mp.Food / -ch).ToString("0.0", Inv) + " dni" : "bez ubytku";
                sb.Append(mp.CurrentSettlement != null ? "w " + mp.CurrentSettlement.Name : "w polu").Append(", ludzi ").Append(men).Append('/').Append(lim).Append(", jedzenie ").Append(food);
                if (ch < -0.01f && mp.Food / -ch < 1f) why.Add("glod (jedzenie < 1 dnia)");
                if (lim > 0 && men < lim * 0.5f) why.Add("slaba partia (" + men + "/" + lim + ")");
            }
            // AI
            sb.Append(" | AI: ");
            if (mp == null) sb.Append("-");
            else
            {
                sb.Append(mp.DefaultBehavior).Append(" -> ").Append(mp.TargetSettlement != null ? mp.TargetSettlement.Name.ToString() : mp.TargetParty != null ? mp.TargetParty.Name.ToString() : "-")
                  .Append(", krotko ").Append(mp.ShortTermBehavior).Append(" -> ")
                  .Append(mp.ShortTermTargetSettlement != null ? mp.ShortTermTargetSettlement.Name.ToString() : mp.ShortTermTargetParty != null ? mp.ShortTermTargetParty.Name.ToString() : "-")
                  .Append(", AI wylaczone ").Append(mp.Ai.IsDisabled ? "tak" : "nie").Append(", bez nowych decyzji ").Append(mp.Ai.DoNotMakeNewDecisions ? "tak" : "nie");
                if (mp.Ai.IsDisabled) why.Add("AI wylaczone");
                if (mp.Ai.DoNotMakeNewDecisions) why.Add("bez nowych decyzji (gra)");
            }
            // armia
            sb.Append(" | armia: ");
            var army = mp != null ? mp.Army : null;
            if (army == null) sb.Append("brak");
            else
            {
                int am = 0;
                foreach (var p in army.Parties) if (p != null && p.MemberRoster != null) am += p.MemberRoster.TotalManCount;
                bool lead = army.LeaderParty == mp;
                sb.Append(lead ? "wodz" : "w armii " + (army.LeaderParty != null && army.LeaderParty.LeaderHero != null ? army.LeaderParty.LeaderHero.Name.ToString() : "?"))
                  .Append(", partii ").Append(army.Parties.Count).Append(", ludzi ").Append(am).Append(", spojnosc ").Append(army.Cohesion.ToString("0", Inv));
                try { if (ArmyRestDay.Resting(army)) why.Add("armia na dniu odpoczynku (m118)"); } catch { }
            }
            // dzis
            sb.Append(" | dzis: bitwy polowe ").Append(w.Field).Append(", oblezenia ").Append(w.Siege).Append(", najazdy ").Append(w.Raid)
              .Append(" (godzin oblezenia ").Append(w.SiegeH).Append(", bitwy ").Append(w.BattleH).Append(", w osadzie ").Append(w.InTown).Append('/').Append(w.Hours).Append(')');
            // mysl AI
            sb.Append(" | mysl AI (mysli ").Append(w.Thinks).Append(", latka ").Append(_hooked ? "TAK" : "BRAK").Append("): ").Append(w.Top);
            if (w.Best >= 0f)
                sb.Append(" | najlepsza ").Append(w.Best.ToString("0.000", Inv)).Append(" / prog gry ").Append(w.Thr.ToString("0.000", Inv)).Append(" (cel obecny ").Append(w.Obj.ToString("0.00", Inv))
                  .Append(", ponizej progu ").Append(w.Below).Append(" z ").Append(w.Thinks).Append(')');
            if (mp != null)
            {
                if (w.Thinks == 0) why.Add("AI nie myslalo dzis");
                else if (w.Below > 0) why.Add("najlepsza ponizej progu gry - zostaje przy starym rozkazie (" + w.Below + " z " + w.Thinks + " mysli)");
                try
                {
                    if (ArmsFirst.Unready(mp)) why.Add("niegotowy do bitwy (p9 ArmsFirst)");
                    if (ArmsFirst.CapTripActive(mp)) why.Add("wyjazd po sprzet (p9)");
                    if (NightRest.NightHolds(mp)) why.Add("noc w obozie (NightRest)");
                    if (LordEscort.HeldBack(mp)) why.Add("postoj 223 (slaby wobec zagrozenia)");
                    if (GentryService.IsHeldParty(mp)) why.Add("rod rycerza (179)");
                }
                catch (Exception e) { Stumble("Holds", e); }
            }
            // SCA
            bool exh, wp, fp, cool;
            string sca = Sca(k, h, out exh, out wp, out fp, out cool);
            if (exh) why.Add("SCA: wyczerpane - cele oblezen x0.55");
            if (wp || fp) why.Add("SCA: " + (fp ? "ForcePeace" : "chce pokoju") + " - cele oblezen x0.35");
            if (cool) why.Add("SCA: zakaz nowej armii");
            if (w.Field + w.Siege + w.Raid > 0 || w.SiegeH > 0) why.Clear();
            sb.Append(" | dlaczego stoi: ").Append(why.Count == 0 ? (w.Field + w.Siege + w.Raid > 0 || w.SiegeH > 0 ? "nie stoi (walczyl)" : "-") : string.Join("; ", why.ToArray()));
            sb.Append(" | SCA: ").Append(sca).Append('.');
            return sb.ToString();
        }

        /// <summary>Linia krolestwa: partie lordow, armie, oblezenia, bitwy doby, wojny z data poczatku, SCA i hipoteza wroga.</summary>
        private static string KingdomLine(int i, int day)
        {
            var k = _kRef[i] ?? Prologue.KingdomById(KingdomIds[i]);
            var sb = new StringBuilder(600);
            sb.Append(TagK).Append("dzien ").Append(day).Append(" | ").Append(k != null ? k.Name.ToString() : "-").Append(" (").Append(KingdomIds[i]).Append(')');
            if (k == null || k.IsEliminated) { sb.Append(" | krolestwa brak."); return sb.ToString(); }
            sb.Append(" | wladca ").Append(k.Leader != null ? k.Leader.Name.ToString() : "-");
            int parties = 0, inTown = 0, sieging = 0, men = 0;
            var besieging = new List<string>();
            foreach (var c in k.Clans)
            {
                if (c == null) continue;
                foreach (var wpc in c.WarPartyComponents)
                {
                    var mp = wpc != null ? wpc.MobileParty : null;
                    if (mp == null || !mp.IsActive || !mp.IsLordParty) continue;
                    parties++;
                    if (mp.MemberRoster != null) men += mp.MemberRoster.TotalManCount;
                    if (mp.CurrentSettlement != null) inTown++;
                    if (mp.BesiegedSettlement != null) { sieging++; if (besieging.Count < 4 && !besieging.Contains(mp.BesiegedSettlement.Name.ToString())) besieging.Add(mp.BesiegedSettlement.Name.ToString()); }
                }
            }
            int armies = 0, armyParties = 0;
            foreach (var a in k.Armies) { if (a == null) continue; armies++; armyParties += a.Parties.Count; }
            int under = 0;
            foreach (var s in k.Settlements) if (s != null && s.IsUnderSiege) under++;
            sb.Append(" | partie lordow ").Append(parties).Append(" (ludzi ").Append(men).Append(", w osadach ").Append(inTown).Append(", w polu ").Append(parties - inTown).Append(')')
              .Append(", armie ").Append(armies).Append(" (partii ").Append(armyParties).Append(')')
              .Append(" | oblega teraz ").Append(sieging).Append(besieging.Count > 0 ? " [" + string.Join(", ", besieging.ToArray()) + "]" : "")
              .Append(", oblegane osady ").Append(under)
              .Append(" | dzis: bitwy polowe ").Append(_k[i].Field).Append(", oblezenia ").Append(_k[i].Siege).Append(", najazdy ").Append(_k[i].Raid);
            var wars = new List<string>();
            foreach (var f in k.FactionsAtWarWith)
            {
                var o = f as Kingdom;
                if (o == null || o.IsEliminated || wars.Count >= 6) continue;
                wars.Add(o.StringId + " (" + Prologue.WarSinceText(k, o, (k.StringId == Prologue.RiverId && o.StringId == Prologue.Crown) || (o.StringId == Prologue.RiverId && k.StringId == Prologue.Crown)
                    ? Prologue.RiverFirstDay : Prologue.NoDayValue) + ")");
            }
            sb.Append(" | wojny: ").Append(wars.Count > 0 ? string.Join(", ", wars.ToArray()) : "brak");
            bool exh, wp, fp, cool;
            sb.Append(" | SCA: ").Append(Sca(k, null, out exh, out wp, out fp, out cool));
            sb.Append(" | ").Append(Enemy(k));
            sb.Append(" | potkniecia ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(").");
            return sb.ToString();
        }

        /// <summary>Autotest T7: linie wladcow tej chwili (bez zerowania doby).</summary>
        internal static List<string> TestRulerLines()
        {
            var l = new List<string>();
            int day = (int)CampaignTime.Now.ToDays;
            foreach (var w in _w) l.Add(RulerLine(w, day));
            return l;
        }
    }
}
