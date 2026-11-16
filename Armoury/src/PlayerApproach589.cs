using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// 589 - LOG PODEJSCIA WROGIEGO LORDA DO GRACZA (tylko log, zero zmiany zachowania). Jeff 16.11: wrogi lord z zadaniem (odsiecz, obrona) "jedzie na slepo",
    /// choc sily gracza sa wieksze. Audyt (wf_826d0f68): przyczyna A - czlonek armii niedoczepiony pomija wroga slabszego od Army.EstimatedStrength
    /// (DefaultMobilePartyAIModel.GetBestInitiativeBehavior ok. :205), przyczyna B - gra szuka wroga tylko w 3R = 9 jedn., z daleka ucieka tylko przed miazdzaca
    /// przewaga. Log bitwy (159 P0) nie mial sil, odleglosci ani stanu armii - Sary Cox i Jasona Mallistera nie dalo sie rozstrzygnac.
    ///
    /// Pomiar: co 0.1 h czasu gry (watek glowny, TickEvent) wrogie partie lordow w promieniu SearchR od gracza. Dla kazdej zapamietane: pierwszy pomiar,
    /// gdy gracz widzi lorda (SeeingRange gracza), gdy lord widzi gracza (SeeingRange lorda), gdy wejda w 3R (promien szukania inicjatywy gry), i ostatni pomiar.
    /// Przy "lord widzi" i "3R": sily obu stron (wlasna i ze swoimi w 2R od GRACZA - przyblizenie num4/num7 gry), stan armii lorda, krotki rozkaz AI (wynik
    /// inicjatywy gry - modelu NIE wolamy: GetBestInitiativeBehavior czysci statyczna liste MobilePartyAi.DangerousPartiesAndTheirVecs wspolna z watkami AI).
    /// Wpis znika, gdy lord wyjdzie poza SearchR (nowe podejscie = nowy wpis). NewLordKit.Describe (strona "wrog") dopisuje to do linii "Bitwa gracza (159 P0)".
    /// </summary>
    internal static class PlayerApproach589
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const float SearchR = 40f;      // promien pomiaru wokol gracza (jedn. mapy); SeeingRange lordow jest mniejszy - wpis "lord widzi" przed SearchR
        private const double StepH = 0.1;       // co ile godzin gry pomiar
        private const float RDef = 3f;          // DefaultEncounterModel.GetEncounterJoiningRadius (1.4.8) - gdy modelu nie ma

        /// <summary>R jak w GetBestInitiativeBehavior: Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius (recenzja 589: nie na stale).</summary>
        private static float R
        {
            get { try { var m = Campaign.Current?.Models?.EncounterModel; float r = m != null ? m.GetEncounterJoiningRadius : RDef; return r > 0f ? r : RDef; } catch { return RDef; } }
        }

        private sealed class Snap
        {
            internal double H; internal float D;
            internal string Beh;     // krotki rozkaz i cel krotki
            internal string Str;     // sily (tylko "lord widzi", "3R", start)
        }
        private sealed class Rec
        {
            internal int Stamp;
            internal Snap PSee, LSee, In3R, Last;
            internal float LSeeR = -1f;   // SeeingRange lorda przy ostatnim liczeniu
        }

        private static readonly Dictionary<MobileParty, Rec> _rec = new Dictionary<MobileParty, Rec>();
        private static readonly List<MobileParty> _drop = new List<MobileParty>();
        private static double _nextH = -1;
        private static int _stamp, _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where) || _stumbles % 200 == 0) Log.Error("PlayerApproach589." + where + " (potkniecie " + _stumbles + ")", e); } catch { }
        }

        internal static void Reset() { _rec.Clear(); _drop.Clear(); _nextH = -1; _stamp = 0; _stumbles = 0; _err.Clear(); }

        internal static void RegisterEvents(CampaignBehaviorBase owner)
        {
            CampaignEvents.TickEvent.AddNonSerializedListener(owner, delegate (float dt) { Tick(); });
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(owner, delegate (MobileParty mp, PartyBase pb) { try { if (mp != null) _rec.Remove(mp); } catch { } });
        }

        private static void Tick()
        {
            try
            {
                if (Campaign.Current == null) return;
                double now = CampaignTime.Now.ToHours;
                if (now < _nextH) return;
                _nextH = now + StepH;
                var main = MobileParty.MainParty;
                if (main == null || !main.IsActive || main.MapEvent != null) return;
                var mf = main.MapFaction;
                if (mf == null) return;
                _stamp++;
                float pSee = -1f;
                var mpos = main.Position;
                var data = MobileParty.StartFindingLocatablesAroundPosition(mpos.ToVec2(), SearchR);
                for (var mp = MobileParty.FindNextLocatable(ref data); mp != null; mp = MobileParty.FindNextLocatable(ref data))
                {
                    try
                    {
                        if (mp == main || !mp.IsActive || !mp.IsLordParty || mp.LeaderHero == null || mp.MapFaction == null) continue;
                        if (!FactionManager.IsAtWarAgainstFaction(mp.MapFaction, mf)) continue;
                        float d = mp.Position.Distance(mpos);
                        if (d > SearchR) continue;
                        Rec r;
                        if (!_rec.TryGetValue(mp, out r)) { r = new Rec(); _rec[mp] = r; }
                        r.Stamp = _stamp;
                        r.Last = Take(mp, d, now, false);
                        if (r.PSee == null)
                        {
                            if (pSee < 0f) pSee = main.SeeingRange;
                            if (d <= pSee) r.PSee = Take(mp, d, now, false);
                        }
                        if (r.LSee == null)
                        {
                            r.LSeeR = mp.SeeingRange;
                            if (d <= r.LSeeR) r.LSee = Take(mp, d, now, true);
                        }
                        if (r.In3R == null && d <= 3f * R) r.In3R = Take(mp, d, now, true);
                    }
                    catch (Exception e) { Stumble("Tick(partia)", e); }
                }
                _drop.Clear();
                foreach (var kv in _rec) if (kv.Value.Stamp != _stamp || kv.Key == null || !kv.Key.IsActive) _drop.Add(kv.Key);
                foreach (var k in _drop) _rec.Remove(k);
                _drop.Clear();
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        private static Snap Take(MobileParty mp, float d, double now, bool full)
        {
            var s = new Snap { H = now, D = d, Beh = Beh(mp) };
            if (full) s.Str = Forces(mp);
            return s;
        }

        private static string Nm(MobileParty mp) { try { return mp != null && mp.Name != null ? mp.Name.ToString() : "?"; } catch { return "?"; } }
        private static string F(float v) { return v.ToString("0.0", Inv); }

        private static string Beh(MobileParty mp)
        {
            var t = mp.ShortTermTargetParty;
            return mp.ShortTermBehavior + (t != null ? (t.IsMainParty ? " -> GRACZ" : " -> " + Nm(t)) : "");
        }

        /// <summary>Sila partii tak, jak liczy ja gra w inicjatywie: wodz armii albo doczepiony - Army.EstimatedStrength, inaczej Party.EstimatedStrength.</summary>
        private static float Own(MobileParty p)
        {
            return p.Army != null && (p.AttachedTo != null || p.Army.LeaderParty == p) ? p.Army.EstimatedStrength : p.Party.EstimatedStrength;
        }

        /// <summary>Sila strony "ze swoimi w 2R" - przyblizenie num4 (lord) / num7 (gracz) z DefaultMobilePartyAIModel.GetBestInitiativeBehavior (1.4.8, ok. :260-345).
        /// Recenzja 589: gra liczy wsparcie OBU stron wokol pozycji celu (gracza): num8 = odleglosc partii od gracza, brane gdy num8 &lt;= 2R (num = 2).
        /// Lord: Own(lord) + partie frakcji lorda (nie gracz) z agresja &gt; 0.01 albo w tym samym miescie co gracz, waniliowe ShouldConsiderAttacking(partia, gracz) (Considers), nie w kryjowce
        /// - albo w tej samej bitwie co gracz. Gracz: Own(gracz) + partie frakcji gracza z agresja &gt; 0.01 albo w tej samej bitwie. Jak w grze: bez doczepionych,
        /// garnizonu, milicji, innej bitwy, innego zywiolu (morze/lad) niz lord, tratwy, partii w oblezonym miescie, nieprowadzacych obozu oblezniczego; wodz armii
        /// liczony Army.EstimatedStrength (takze gdy lord jest doczepiony do tej armii - gra wtedy liczy armie drugi raz). Przyblizenie: bez wagi odleglosci (b),
        /// bez obozow oblezniczych, bez odciecia promieniem szukania wokol lorda (3R) - przy progu "lord widzi" lord moze byc dalej, niz gra w ogole szuka.
        /// W linii misji ("teraz") partie juz w bitwie gracza sa liczone (flag4 gry) - to sklad bitwy, nie stan sprzed spotkania. Watek glowny.</summary>
        private static float Side(MobileParty lord, bool lordSide)
        {
            var main = MobileParty.MainParty;
            if (main == null) return -1f;
            var self = lordSide ? lord : main;
            var f = self.MapFaction;
            float r2 = 2f * R;
            float sum = Own(self);
            var pos = main.Position;
            var me = main.MapEvent;
            var data = MobileParty.StartFindingLocatablesAroundPosition(pos.ToVec2(), r2);
            for (var p = MobileParty.FindNextLocatable(ref data); p != null; p = MobileParty.FindNextLocatable(ref data))
            {
                if (p == lord || p == main || !p.IsActive || p.AttachedTo != null || p.IsGarrison || p.IsMilitia || p.MapFaction != f) continue;
                if (p.MapEvent != null && p.MapEvent != me) continue;
                if (p.BesiegedSettlement != null || p.IsInRaftState) continue;
                if (p.CurrentSettlement != null && p.CurrentSettlement.SiegeEvent != null) continue;
                if (p.BesiegerCamp != null) continue;   // oboz obleznicy - gra liczy inaczej (sklad obozu); pomijamy
                if (p.IsCurrentlyAtSea != lord.IsCurrentlyAtSea) continue;
                if (p.Position.Distance(pos) > r2) continue;
                bool sameBattle = me != null && p.MapEvent == me;
                if (lordSide)
                {
                    bool agg = p.Aggressiveness > 0.01f || (p.CurrentSettlement != null && p.CurrentSettlement == main.CurrentSettlement);
                    bool hide = p.CurrentSettlement != null && p.CurrentSettlement.IsHideout;
                    if (!sameBattle && !(agg && !hide && Considers(p, main))) continue;
                }
                else if (!sameBattle && p.Aggressiveness <= 0.01f) continue;
                sum += Own(p);
            }
            return sum;
        }

        /// <summary>Waniliowy DefaultMobilePartyAIModel.ShouldConsiderAttacking(p, gracz) przepisany na odczyt (1.4.8, :38-48). Modelu NIE wolamy: nasze postfiksy
        /// (ArmsFirst/LordEscort/FreeFolkRaids AttackPostfix) licza weta w statystykach dob - log nie moze ich nabijac. Przyblizenie: bez tych wet.</summary>
        private static bool Considers(MobileParty p, MobileParty main)
        {
            if (main.ShouldBeIgnored || !p.Ai.DoNotAttackMainPartyUntil.IsPast) return false;
            bool sea = p.IsCurrentlyAtSea == main.IsCurrentlyAtSea;
            bool port = p.CurrentSettlement != null && p.CurrentSettlement.IsFortification && p.CurrentSettlement.HasPort && p.HasNavalNavigationCapability;
            return (sea || port) && p.Morale > 0f;
        }

        /// <summary>Stan armii lorda wobec gracza: brak / wodz / doczepiony / NIEDOCZEPIONY (odleglosc do wodza, sila armii, slepa plamka gry - :205).</summary>
        private static string ArmyOf(MobileParty mp)
        {
            var a = mp.Army;
            if (a == null) return "NIE";
            if (a.LeaderParty == mp) return "wodz armii (sila armii " + F(a.EstimatedStrength) + ")";
            if (mp.AttachedTo != null) return "doczepiony do " + Nm(a.LeaderParty) + " (sila armii " + F(a.EstimatedStrength) + ")";
            var main = MobileParty.MainParty;
            var pm = main != null ? (main.AttachedTo ?? main) : null;
            float g = pm != null ? (pm.Army != null ? pm.Army.EstimatedStrength : pm.Party.EstimatedStrength) : -1f;
            float dl = a.LeaderParty != null ? mp.Position.Distance(a.LeaderParty.Position) : -1f;
            return "NIEDOCZEPIONY (" + Nm(a.LeaderParty) + ", do wodza " + F(dl) + " jedn., sila armii " + F(a.EstimatedStrength)
                   + ", slepa plamka gry: gracz w ocenie gry " + F(g) + (g >= 0f && g < a.EstimatedStrength ? " < armia - gra POMIJA gracza (ani atak, ani unikanie)" : " >= armia - gra ocenia normalnie") + ")";
        }

        private static string Forces(MobileParty mp)
        {
            var main = MobileParty.MainParty;
            string pl = main != null ? "gracz " + F(Own(main)) + " (ze swoimi w 2R od gracza " + F(Side(mp, false)) + ")" : "gracz ?";
            return pl + " / lord " + F(mp.Party.EstimatedStrength) + " (jak liczy gra " + F(Own(mp)) + ", ze swoimi w 2R od gracza " + F(Side(mp, true)) + "); armia lorda: " + ArmyOf(mp);
        }

        private static string Snp(string label, Snap s, double now)
        {
            if (s == null) return label + " -";
            var sb = new StringBuilder();
            sb.Append(label).Append(" z ").Append(F(s.D)).Append(" jedn. (").Append(((now - s.H)).ToString("0.0", Inv)).Append(" h przed), krotko ").Append(s.Beh);
            if (s.Str != null) sb.Append(", sily: ").Append(s.Str);
            return sb.ToString();
        }

        /// <summary>NewLordKit.Describe (strona "wrog"): podejscie tego lorda do gracza i stan teraz. Watek glowny, tylko odczyt; nie rzuca.</summary>
        internal static string Describe(MobileParty mp)
        {
            try
            {
                if (mp == null) return "";
                double now = CampaignTime.Now.ToHours;
                var main = MobileParty.MainParty;
                var sb = new StringBuilder(" | 589 podejscie: ");
                Rec r;
                if (_rec.TryGetValue(mp, out r))
                {
                    sb.Append(Snp("gracz widzi lorda", r.PSee, now)).Append("; ")
                      .Append(Snp("lord widzi gracza (zasieg lorda " + F(r.LSeeR) + ")", r.LSee, now)).Append("; ")
                      .Append(Snp("w 3R (" + F(3f * R) + ", promien szukania gry - przyblizenie)", r.In3R, now)).Append("; ")
                      .Append(Snp("ostatni pomiar", r.Last, now));
                }
                else sb.Append("brak pomiaru (lord poza ").Append(F(SearchR)).Append(" jedn. przy ostatnim pomiarze albo spotkanie przed pierwszym pomiarem co ").Append(StepH.ToString("0.0", Inv)).Append(" h)");
                sb.Append(" | 589 teraz: odleglosc ").Append(main != null ? F(mp.Position.Distance(main.Position)) : "?")
                  .Append(", zadanie ").Append(mp.DefaultBehavior)
                  .Append(mp.TargetSettlement != null ? " -> " + mp.TargetSettlement.Name : mp.TargetParty != null ? " -> " + Nm(mp.TargetParty) : "")
                  .Append(", krotko ").Append(Beh(mp)).Append(", sily: ").Append(Forces(mp));
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Describe", e); return " | 589: blad"; }
        }
    }
}
