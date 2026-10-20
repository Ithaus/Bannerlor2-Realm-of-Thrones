using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// R3-10a (POMYSLY-35 nr 31, Jeff 20.10 "pozostale tak"; audyt docs/audyt-2026-10-09/13-UMIEJETNOSCI.md Z13, PLAN Z13 "NightScout") -
    /// ZWIAD NOCA I W LESIE. Dotad nasza kara nocy (TerrainEase: plaska -0.5 na ladzie), nasza kara lasu (TerrainEase) i nasz wzrok noca
    /// (SightRange: NightSightFactor 0.65) w ogole nie patrzyly na Zwiad - liczyly sie tylko perki gry (Night Runner, Forest Kin).
    /// Po naszemu (jedna regula dla gracza i AI, klucz NightScoutEnabled; wylaczony = jak dotad):
    ///  1) predkosc: zwiadowca partii (MobileParty.EffectiveScout - wyznaczony zwiadowca w druzynie, inaczej wodz, jak w grze) zdejmuje
    ///     ulamek min(0.5, Zwiad / 400) naszej kary nocy i naszej kary lasu - jedna pozycja "Scout" w rozpisce predkosci (dodatnia), liczona
    ///     w TerrainEase.SpeedPostfix przed sufitem kolumny (MarchPace ma Priority.Last). Zwiad 200: noc -0.5 -> -0.25. Snieg, pustynia,
    ///     bagno i brody bez zmian. Dziala tylko przy TerrainEaseEnabled (bez niego kary sa procentem gry, nie nasze).
    ///  2) wzrok noca: czynnik nocy NightSightFactor + (1 - NightSightFactor) x min(0.5, Zwiad / 400) - osobna pozycja "Scout (night)"
    ///     w SightRange.Postfix (wspolczynniki ExplainedNumber sie dodaja, wiec suma = wzor). Zwiad 200: 0.65 -> 0.825. Na morzu nie (jak
    ///     kara nocy predkosci i perk Night Runner gry). Dziala tylko przy SightCycleEnabled.
    ///  3) nauka: +5 XP Zwiadu za kazda godzine nocnego marszu (karawany polowa - jak marsz w grze i w XpFair), przez metode gry
    ///     DefaultSkillLevelingManager.OnPartySkillExercised (rola Scout; latka ROT dla gracza na sluzbie dziala dalej). Postfiks na
    ///     DefaultSkillLevelingManager.OnTraverseTerrain - gra wola ja co godzine dla kazdej idacej partii z wodzem i zwiadowca, nie na
    ///     morzu (MobilePartyTrainingBehavior.CheckScouting, zdarzenie HourlyTickParty - watek glowny). Postfiks biegnie takze, gdy prefiks
    ///     XpFair zdejmuje metode gry. Warunek "naprawde idzie" jak w grze: predkosc > 1 (MobileParty._lastCalculatedSpeed).
    ///     Priority.Normal - przed postfiksem MEnd ksiegi E8/P1 (Last), wiec ksiega widzi nasza wyplate jako "marsz".
    /// Wspolczynnik Cut wolany z watkow roboczych gry (CalculateFinalSpeed, GetPartySpottingRange): tylko odczyt, bez alokacji, bez licznikow.
    /// Liczniki linii doby zbiera postfiks XP (watek glowny, raz na godzine na partie). Linia doby "Zwiad noca (R3-10a): dzien N" z
    /// ArmouryBehavior.OnDailyTick; linia startu "Zwiad noca (R3-10a): start". Stan sesji bez zapisu; nowa kampania/wczytanie = liczniki od zera
    /// (rozpoznane po tozsamosci obiektu Campaign, bez trzymania referencji).
    /// </summary>
    internal static class NightScout
    {
        // ---- regula (liczby z projektu Z13, bez suwakow)
        internal const float PerPoint = 1f / 400f;      // ulamek kary zdjety za punkt Zwiadu
        internal const float MaxCut = 0.5f;             // najwyzej polowa kary
        internal const float NightXp = 5f;              // XP Zwiadu za godzine nocnego marszu
        internal const float CaravanShare = 0.5f;       // karawany polowa (gra i XpFair.ScoutCaravan)
        private const float ForestFull = 0.3f;          // kara lasu gry (jak TerrainEase.GameForestFull) - tylko do licznikow linii

        internal static readonly TextObject SpeedLabel = new TextObject("{=!}Scout");
        internal static readonly TextObject SightLabel = new TextObject("{=!}Scout (night)");

        /// <summary>Ulamek naszej kary nocy i lasu, ktory zdejmuje zwiadowca partii: min(0.5, Zwiad / 400); 0 przy wylaczonym kluczu
        /// albo bez zwiadowcy. Wolane z watkow roboczych gry - tylko odczyt.</summary>
        internal static float Cut(MobileParty mp)
        {
            var s = Settings.Current;
            if (s == null || !s.NightScoutEnabled || mp == null) return 0f;
            Hero h = mp.EffectiveScout;
            if (h == null) return 0f;
            int sc = h.GetSkillValue(DefaultSkills.Scouting);
            if (sc <= 0) return 0f;
            float c = sc * PerPoint;
            return c > MaxCut ? MaxCut : c;
        }

        /// <summary>TerrainEase.SpeedPostfix: ile z naszej plaskiej kary (pen, jedn.) zdejmuje zwiadowca. Kara ponizej progu Swap = 0.</summary>
        internal static float Ease(MobileParty mp, float pen)
        {
            if (!(pen > 0.0005f)) return 0f;
            return pen * Cut(mp);
        }

        /// <summary>SightRange.Postfix (tylko noca): dodatkowy wspolczynnik wzroku (1 - czynnik nocy) x Cut; na morzu 0.</summary>
        internal static float SightFactor(MobileParty party, float nightFactor)
        {
            if (party == null || !(nightFactor < 1f) || party.IsCurrentlyAtSea) return 0f;
            return (1f - nightFactor) * Cut(party);
        }

        /// <summary>Wayfinder (meldunek kursu gracza): kara nocy po ulgach zwiadowcy - ta sama, ktora naklada TerrainEase.</summary>
        internal static float NightPenalty(MobileParty mp, Settings s)
        {
            float pen = Math.Max(0f, s.NightSpeedPenalty);
            try { return pen - Ease(mp, pen); }
            catch { return pen; }
        }

        // ---- latka XP (raz na proces, z SubModuleMain)
        private delegate void PartyXpFn(MobileParty party, SkillObject skill, float skillXp, PartyRole partyRole);
        private static PartyXpFn _partyXp;                                             // DefaultSkillLevelingManager.OnPartySkillExercised
        private static AccessTools.FieldRef<MobileParty, float> _speed;                // MobileParty._lastCalculatedSpeed
        private static bool _wired;

        // ---- liczniki doby (watek glowny): wiersze gracz / lordowie AI / pozostali
        private const int RPlayer = 0, RLord = 1, RRest = 2, Rows = 3;
        private static readonly string[] RowNames = { "gracz", "lordowie AI", "pozostali" };
        private static readonly long[] _nH = new long[Rows], _nCut = new long[Rows], _nMax = new long[Rows], _fH = new long[Rows], _fCut = new long[Rows];
        private static readonly double[] _nCutSum = new double[Rows], _nEase = new double[Rows], _fEase = new double[Rows], _xp = new double[Rows];
        private static int _stumbles, _camp;
        private static bool _had;

        private static void Clear()
        {
            Array.Clear(_nH, 0, Rows); Array.Clear(_nCut, 0, Rows); Array.Clear(_nMax, 0, Rows); Array.Clear(_fH, 0, Rows); Array.Clear(_fCut, 0, Rows);
            Array.Clear(_nCutSum, 0, Rows); Array.Clear(_nEase, 0, Rows); Array.Clear(_fEase, 0, Rows); Array.Clear(_xp, 0, Rows);
        }

        private static int RowOf(MobileParty mp)
        {
            if (mp.IsMainParty) return RPlayer;
            return mp.IsLordParty ? RLord : RRest;
        }

        private static void Stumble(Exception e)
        {
            // potkniecia liczone per wywolanie, regula dziala dalej (CLAUDE.md 7 - bez globalnego wylacznika)
            _stumbles++;
            if (_stumbles <= 3) Log.Error("NightScout", e);
        }

        /// <summary>Postfiks DefaultSkillLevelingManager.OnTraverseTerrain (co godzine, idaca partia z wodzem i zwiadowca, na ladzie):
        /// noca +5 XP Zwiadu (karawany 2.5) i liczniki linii doby (noc i las). Bez alokacji.</summary>
        public static void TraversePostfix(MobileParty mobileParty, TerrainType currentTerrainType)
        {
            var s = Settings.Current;
            if (s == null || !s.NightScoutEnabled || mobileParty == null) return;
            try
            {
                var camp = Campaign.Current;
                if (camp == null) return;
                bool night = camp.IsNight, forest = currentTerrainType == TerrainType.Forest;
                if (!night && !forest) return;
                var speedOf = _speed;
                if (speedOf == null || !(speedOf(mobileParty) > 1f)) return;                    // stoi - jak w grze: nic
                int row = RowOf(mobileParty);
                float cut = Cut(mobileParty);
                if (night)
                {
                    _nH[row]++;
                    if (cut > 0f) _nCut[row]++;
                    if (cut >= MaxCut) _nMax[row]++;
                    _nCutSum[row] += cut;
                    if (s.TerrainEaseEnabled) _nEase[row] += Ease(mobileParty, Math.Max(0f, s.NightSpeedPenalty));
                    var pay = _partyXp;
                    if (pay != null)
                    {
                        float xp = mobileParty.IsCaravan ? NightXp * CaravanShare : NightXp;
                        pay(mobileParty, DefaultSkills.Scouting, xp, PartyRole.Scout);
                        _xp[row] += xp;
                    }
                }
                if (forest && s.TerrainEaseEnabled)
                {
                    float pen = s.ForestSpeedPenalty;
                    if (s.TerrainEaseExact)
                    {
                        bool kin, feat;
                        pen = s.ForestSpeedPenalty * Math.Max(0f, TerrainEase.GameForestNet(mobileParty, out kin, out feat)) / ForestFull;
                    }
                    _fH[row]++;
                    if (cut > 0f) _fCut[row]++;
                    _fEase[row] += Ease(mobileParty, pen);
                }
            }
            catch (Exception e) { Stumble(e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var s = Settings.Current;
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(DefaultSkillLevelingManager), "OnPartySkillExercised",
                                                       new[] { typeof(MobileParty), typeof(SkillObject), typeof(float), typeof(PartyRole) });
                    if (m != null && m.IsStatic) _partyXp = (PartyXpFn)Delegate.CreateDelegate(typeof(PartyXpFn), m);
                }
                catch { _partyXp = null; }
                try { _speed = AccessTools.FieldRefAccess<MobileParty, float>("_lastCalculatedSpeed"); }
                catch { _speed = null; }
                string miss = null;
                var t = AccessTools.DeclaredMethod(typeof(DefaultSkillLevelingManager), "OnTraverseTerrain", new[] { typeof(MobileParty), typeof(TerrainType) });
                if (t == null) miss = "brak DefaultSkillLevelingManager.OnTraverseTerrain";
                else if (_partyXp == null) miss = "brak DefaultSkillLevelingManager.OnPartySkillExercised";
                else if (_speed == null) miss = "brak MobileParty._lastCalculatedSpeed";
                else
                {
                    h.Patch(t, postfix: new HarmonyMethod(typeof(NightScout), nameof(TraversePostfix)) { priority = Priority.Normal });
                    _wired = true;
                }
                Log.Info("Zwiad noca (R3-10a): start - klucz NightScoutEnabled " + (s != null && s.NightScoutEnabled ? "on" : "off")
                         + " | kara nocy i lasu x (1 - min(0.5, Zwiad/400)) w TerrainEase (TerrainEaseEnabled " + (s != null && s.TerrainEaseEnabled ? "on" : "off") + ")"
                         + " | wzrok noca NightSightFactor + (1 - NightSightFactor) x min(0.5, Zwiad/400) w SightRange (SightCycleEnabled " + (s != null && s.SightCycleEnabled ? "on" : "off") + ")"
                         + " | +" + NightXp.ToString("0", CultureInfo.InvariantCulture) + " XP Zwiadu za godzine nocnego marszu (karawany polowa): "
                         + (_wired ? "latka wpieta (OnTraverseTerrain)" : "NIE wpieta - " + miss) + ".");
            }
            catch (Exception e) { Log.Error("NightScout.ApplyAll", e); }
        }

        private static string F(double v, string fmt) { return v.ToString(fmt, CultureInfo.InvariantCulture); }

        /// <summary>Z ArmouryBehavior.OnDailyTick: linia za dobe (dzien = doba, ktora sie skonczyla); pierwszy takt sesji bez linii.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            var camp = Campaign.Current;
            int id = camp != null ? RuntimeHelpers.GetHashCode(camp) : 0;
            if (id != _camp) { _camp = id; _had = false; _stumbles = 0; Clear(); }
            if (s == null || !s.NightScoutEnabled || camp == null) { _had = false; Clear(); return; }
            try { if (_had) Line(s); }
            catch (Exception e) { Stumble(e); }
            Clear();
            _had = true;
        }

        private static void Line(Settings s)
        {
            int day = (int)CampaignTime.Now.ToDays - 1;
            var sb = new StringBuilder(900);
            sb.Append("Zwiad noca (R3-10a): dzien ").Append(day)
              .Append(" (TerrainEaseEnabled ").Append(s.TerrainEaseEnabled ? "on" : "off")
              .Append(", SightCycleEnabled ").Append(s.SightCycleEnabled ? "on" : "off")
              .Append(", latka XP ").Append(_wired ? "wpieta" : "NIE wpieta").Append(')');
            sb.Append(" | noc w marszu (partio-godziny): ");
            for (int r = 0; r < Rows; r++)
            {
                long n = _nH[r];
                sb.Append(r == 0 ? "" : ", ").Append(RowNames[r]).Append(' ').Append(n)
                  .Append(" (ze zwiadowca z ulga ").Append(_nCut[r]).Append(", ulga pelna 50% ").Append(_nMax[r])
                  .Append(", sr. ulga ").Append(n > 0 ? F(100.0 * _nCutSum[r] / n, "0.0") : "-").Append("%")
                  .Append(", zdjete z kary nocy ").Append(F(_nEase[r], "0.00")).Append(" jedn.")
                  .Append(", XP Zwiadu +").Append(F(_xp[r], "0")).Append(')');
            }
            sb.Append(" | las w marszu (partio-godziny, dzien i noc): ");
            for (int r = 0; r < Rows; r++)
                sb.Append(r == 0 ? "" : ", ").Append(RowNames[r]).Append(' ').Append(_fH[r])
                  .Append(" (z ulga ").Append(_fCut[r]).Append(", zdjete z kary lasu ").Append(F(_fEase[r], "0.00")).Append(" jedn.)");
            // gracz teraz: Zwiad zwiadowcy, kara nocy i lasu po uldze, wzrok noca
            var main = MobileParty.MainParty;
            if (main != null)
            {
                Hero sh = main.EffectiveScout;
                int sc = sh != null ? sh.GetSkillValue(DefaultSkills.Scouting) : 0;
                float cut = Cut(main);
                float nsf = s.NightSightFactor;
                float sight = nsf < 1f ? nsf + (1f - nsf) * cut : nsf;
                sb.Append(" | gracz teraz: zwiadowca ").Append(sh != null ? sh.Name.ToString() : "-").Append(" Zwiad ").Append(sc)
                  .Append(", ulga ").Append(F(100.0 * cut, "0.0")).Append("%")
                  .Append(", kara nocy -").Append(F(NightPenalty(main, s), "0.00")).Append(" zamiast -").Append(F(Math.Max(0f, s.NightSpeedPenalty), "0.00"))
                  .Append(", kara lasu x").Append(F(1f - cut, "0.00"))
                  .Append(", wzrok noca x").Append(F(sight, "0.000")).Append(" zamiast x").Append(F(nsf, "0.000"));
            }
            sb.Append(" | potkniecia ").Append(_stumbles);
            Log.Info(sb.ToString());
        }
    }
}
