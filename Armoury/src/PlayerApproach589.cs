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
    /// Przy "lord widzi" i "3R": sily obu stron (wlasna i z sojusznikami tej samej frakcji w 2R - jak liczy gra), stan armii lorda, krotki rozkaz AI (wynik
    /// inicjatywy gry - modelu NIE wolamy: GetBestInitiativeBehavior czysci statyczna liste MobilePartyAi.DangerousPartiesAndTheirVecs wspolna z watkami AI).
    /// Wpis znika, gdy lord wyjdzie poza SearchR (nowe podejscie = nowy wpis). NewLordKit.Describe (strona "wrog") dopisuje to do linii "Bitwa gracza (159 P0)".
    /// </summary>
    internal static class PlayerApproach589
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const float SearchR = 40f;      // promien pomiaru wokol gracza (jedn. mapy); SeeingRange lordow jest mniejszy - wpis "lord widzi" przed SearchR
        private const double StepH = 0.1;       // co ile godzin gry pomiar
        private const float R = 3f;             // DefaultEncounterModel.GetEncounterJoiningRadius (1.4.8)

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

        /// <summary>Sila strony: partia "center" (Own) plus partie tej samej frakcji w 2R od niej (niedoczepione, bez garnizonu/milicji, bez innej bitwy) - przyblizenie
        /// num4/num7 gry (bez wagi odleglosci). Watek glowny.</summary>
        private static float Side(MobileParty center)
        {
            float sum = Own(center);
            var f = center.MapFaction;
            var data = MobileParty.StartFindingLocatablesAroundPosition(center.Position.ToVec2(), 2f * R);
            for (var p = MobileParty.FindNextLocatable(ref data); p != null; p = MobileParty.FindNextLocatable(ref data))
            {
                if (p == center || !p.IsActive || p.AttachedTo != null || p.IsGarrison || p.IsMilitia || p.MapFaction != f) continue;
                if (center.Army != null && p.Army == center.Army && (center.AttachedTo != null || center.Army.LeaderParty == center)) continue;   // juz w Army.EstimatedStrength
                if (p.MapEvent != null && p.MapEvent != center.MapEvent) continue;
                if (p.IsCurrentlyAtSea != center.IsCurrentlyAtSea) continue;
                if (p.Position.Distance(center.Position) > 2f * R) continue;
                sum += Own(p);
            }
            return sum;
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
            string pl = main != null ? "gracz " + F(Own(main)) + " (ze swoimi w 2R " + F(Side(main)) + ")" : "gracz ?";
            return pl + " / lord " + F(mp.Party.EstimatedStrength) + " (jak liczy gra " + F(Own(mp)) + ", ze swoimi w 2R " + F(Side(mp)) + "); armia lorda: " + ArmyOf(mp);
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
                      .Append(Snp("w 3R (" + F(3f * R) + ")", r.In3R, now)).Append("; ")
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
