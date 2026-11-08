using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 362 (Jeff 08.11: "jak pladruje wies ... od rougery zalezy, im mam wieksze rougery tym lepiej szukam zlota i przymuszam chlopow aby wyjawili gdzie ukryli zloto")
    /// - SKRYTKI WSI WEDLUG ROGUERY WODZA RABUNKU. Klucz RaidCacheByRoguery (off = s0 i zelazna cena jak dotad).
    ///
    /// JEDNA FUNKCJA UDZIALU (Share) dla wyplaty i dla szacunkow AI:
    ///   S = min(Cap, (Base + k x Roguery + KnowHow 0.05 + RogueExtraordinaire 0.01 x max(0, Roguery - 200)) x Scarface 1.25 x SaltTheEarth 1.2) x u
    ///   Base 0.05, k 0.002/pkt, Cap 0.50 (MCM; Jeff: "5% + 0,2%/pkt, max 50%"): Roguery 0 -> 5%, 50 -> 15%, 100 -> 25%, 200 -> 45%, 225+ -> 50%.
    ///   Cap PO perkach: perki dobijaja do 50%, nie ponad (Jeff wybral "max 50%" - polowa kiesy to sufit niezaleznie od perkow; inaczej Scarface x Salt przy 200
    ///   dawalby 0.45 x 1.5 = 67% i rabunek bralby wiekszosc oszczednosci wsi w jednym kroku). Rogue Extraordinaire (tier 275) przy domyslnych liczbach nic nie
    ///   dodaje - Base + k x 225 = 0.50 juz jest na suficie; zostaje w regule dla Base/k ustawionych nizej w MCM.
    ///   Perki wodza sprawdzane Hero.GetPerkValue (wodz = bohater prowadzacy partie; Know-How i Salt the Earth sa PartyLeader, Scarface i Rogue Extraordinaire Personal -
    ///   w obu przypadkach to ten sam bohater). Dotychczasowe efekty perkow gry zostaja (lup z bitwy, wymuszenia, gubernator) - tu tylko dodatkowy skladnik.
    ///   u = postep sesji (1 po pelnym kroku, przerwany - zrobiona czesc), jak zelazna cena WW1. Funkcja tylko czyta (umiejetnosc, perki) - wolno z mysli AI.
    ///
    /// LUP (DistrictRaid.Loot, SideVillageVisit.Loot): RAZ NA SESJE (pierwszy krok, ss.Steps == 0), zloto = max(s0, S, zelazna cena) x kiesa - suma jest MAX, nie suma
    ///   (doplata skrytek = round(S x kiesa) - to, co juz poszlo z s0 i zelaznej ceny). Ten sam platnik (SmallGold.PayRaidLoot -> LootPayer: nie wiecej, niz w kiesie,
    ///   przelew wies -> wodz). Towar z magazynu bez zmian (s0 / zelazna cena) - skrytki to zloto ("wyjawili gdzie ukryli zloto"). Wioska poboczna: S x udzial wioski
    ///   w ludziach okregu (kiesa jest wspolna dla okregu - jak okup wioski).
    /// XP: wodz dostaje min(XpCap, XpPerGold x zloto ze skrytek), gdzie zloto ze skrytek = sama doplata skrytek (ponad max(s0, zelazna cena), do S x u x kiesa) - raz na sesje,
    ///   AI i gracz. 0.5 XP / zl = ta sama stawka co XP Roguery za towar z rabunku w grze (0.5 x wartosc towaru, DefaultSkillLevelingManager) - denar zlota uczy tyle,
    ///   co denar towaru. Sufit 2000 XP: typowa kiesa 5-13 tys. x S 25% = 1.2-3.3 tys. zl = 600-1600 XP (pod sufitem); najbogatsza wies (241 tys.) x 50% bez sufitu
    ///   dalaby 60 tys. XP naraz - petla dodatnia (wiecej Roguery -> wiecej zlota -> wiecej XP), sufit ja tnie.
    /// RELACJA (tortury): podstawa = S x u (intensywnosc razy zrobiona czesc, bez udzialu wioski - przy wiosce pobocznej tortury te same, tylko ludzi mniej);
    ///   ponad RelationOver (0.20) - dodatkowo -floor((podstawa - 0.20) / 0.10) relacji z panem wsi i kazdym notablem; tylko gdy rabus to bohater, a wies nie jest jego rodu.
    ///   Sumuje sie z kara gry (-6/-3 x RaidDamage) i wioski (-3/-1) - zamierzone (Jeff: "Kara relacji za tortury"). Recenzja 362: kara tylko, gdy doplata skrytek > 0
    ///   (bez wyciagnietego zlota - bez tortur), i raz na dobe na pare (wodz, wies gry) - wies gry i jej wioski poboczne maja tego samego pana i tych samych notabli.
    ///
    /// SZACUNKI AI (ta sama funkcja, u = 1): RaidTribute (E = max(s, S) x kiesa + s x magazyn), ReaverLaw.LootEstimate WW3 (max(Q, S) x kiesa + Q x magazyn).
    ///   Prologue.World.Raiders bez zmian - bierze ze StepEstimate tylko doby kroku (cel bandy = najszybsza wies), udzialu nie uzywa.
    ///   RABUNEK vs OKUP (RaidTribute: rabunek, gdy T &lt; E - C x D; okup T = 10-20% kiesy wedlug przewagi 1:1 - 5:1): bez magazynu i bez kosztu dob rabunek przebija
    ///   okup, gdy S &gt; stawka: okup 10% (przewaga 1:1) - od Roguery 25 (S = 0.05 + 0.05 = 0.10); 15% (3:1) - od 50; 20% (5:1) - od 75 (z Know-How przy 75: 25%).
    ///   Koszt dob (zold armii x doby kroku, zwykle 2-3 doby) przesuwa prog w gore - dla armii o wysokim zoldzie i ubogiej wsi okup dalej wygrywa. Stawek okupu nie ruszamy.
    /// Linia doby "Skrytki wsi (362): dzien N" (ArmouryBehavior po DistrictRaid.Daily). Wyjatek per wywolanie - licznik potkniec, bez wylacznika (CLAUDE.md 7).
    /// </summary>
    internal static class RaidCache
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double KnowHowAdd = 0.05;          // Know-How: +5 pkt proc.
        private const double ScarfaceMul = 1.25;         // Scarface: x1.25 ("villagers reveal their hiding places")
        private const double SaltMul = 1.2;              // Salt the Earth: x1.2
        private const double RogueExtraPerPoint = 0.01;  // Rogue Extraordinaire: +1 pkt proc. za kazdy punkt Roguery ponad 200
        private const int RogueExtraFrom = 200;
        private const double RelationStep = 0.10;        // -1 relacji za kazde 10 pkt ponad prog
        private const int MaxStumbles = 5;

        internal static bool On { get { var s = Settings.Current; return s != null && s.RaidCacheByRoguery; } }

        // liczniki doby (watek kampanii; NoteChoice przez Interlocked - na wszelki wypadek, wolajacy jest w watku kampanii)
        private static int _dSess, _dSessGold, _dPlayer, _dSide, _dRelHeroes, _dRelSum, _dStumbles;
        private static long _dGold, _dTopUp, _dXp;
        private static double _dSumS, _dMaxS;
        private static int _dAiRaid, _dAiTrib, _dAiRaidByCache;
        private static readonly HashSet<string> _err = new HashSet<string>();
        // recenzja 362 (1-2): kara tortur raz na dobe na pare (wodz, wies gry) - wies gry i jej wioski poboczne to ten sam pan i ci sami notable
        private static readonly HashSet<string> _relDone = new HashSet<string>();

        internal static void Reset()
        {
            NewDay();
            lock (_err) _err.Clear();
        }

        private static void NewDay()
        {
            _dSess = _dSessGold = _dPlayer = _dSide = _dRelHeroes = _dRelSum = _dStumbles = 0;
            _dGold = _dTopUp = _dXp = 0;
            _dSumS = _dMaxS = 0.0;
            _dAiRaid = _dAiTrib = _dAiRaidByCache = 0;
            _relDone.Clear();
        }

        internal static void Stumble(string where, Exception e)
        {
            _dStumbles++;
            bool first;
            lock (_err) first = _err.Count < MaxStumbles * 4 && _err.Add(where);
            if (first) Log.Error("RaidCache." + where, e);
        }

        private static double Clamp01(double x) { return double.IsNaN(x) ? 0.0 : (x < 0.0 ? 0.0 : (x > 1.0 ? 1.0 : x)); }

        /// <summary>Udzial kiesy wsi, ktory wodz wyciaga ze skrytek przy pelnym rabunku (bez u). 0 - klucz off albo brak wodza. Tylko odczyt (wolno z mysli AI).</summary>
        internal static double Raw(Hero leader, out int roguery)
        {
            roguery = 0;
            var s = Settings.Current;
            if (s == null || !s.RaidCacheByRoguery || leader == null) return 0.0;
            try
            {
                roguery = leader.GetSkillValue(DefaultSkills.Roguery);
                double add = Math.Max(0f, s.RaidCacheBase) + Math.Max(0f, s.RaidCachePerSkill) * roguery;
                double mul = 1.0;
                var kh = DefaultPerks.Roguery.KnowHow;
                var sf = DefaultPerks.Roguery.Scarface;
                var se = DefaultPerks.Roguery.SaltTheEarth;
                var re = DefaultPerks.Roguery.RogueExtraordinaire;
                if (kh != null && leader.GetPerkValue(kh)) add += KnowHowAdd;
                if (re != null && leader.GetPerkValue(re)) add += RogueExtraPerPoint * Math.Max(0, roguery - RogueExtraFrom);
                if (sf != null && leader.GetPerkValue(sf)) mul *= ScarfaceMul;
                if (se != null && leader.GetPerkValue(se)) mul *= SaltMul;
                double cap = Clamp01(s.RaidCacheCap);
                return Math.Min(cap, Math.Max(0.0, add * mul));   // Cap po perkach - perki dobijaja do sufitu, nie ponad
            }
            catch { return 0.0; }   // tylko odczyt, takze z mysli AI - bez Stumble (licznik nie jest bezpieczny dla watkow)
        }

        /// <summary>JEDNA FUNKCJA UDZIALU: S = min(Cap, (Base + k x Roguery + perki) x mnozniki perkow) x u.</summary>
        internal static double Share(Hero leader, double u)
        {
            int r;
            return Raw(leader, out r) * Clamp01(u);
        }

        /// <summary>RaidTribute (AI przy wsi): wynik decyzji z szacunkiem skrytek. raid - rabunek z rachunku zysku; byCache - bez skrytek bylby okup.</summary>
        internal static void NoteChoice(bool raid, bool byCache)
        {
            if (raid) { Interlocked.Increment(ref _dAiRaid); if (byCache) Interlocked.Increment(ref _dAiRaidByCache); }
            else Interlocked.Increment(ref _dAiTrib);
        }

        /// <summary>
        /// Po wyplacie skrytek (raz na sesje): liczniki, XP Roguery za zloto ze skrytek, kara relacji za tortury, komunikat gracza. sRaw - S bez u, sTaken - S x u
        /// (podstawa kary), sPurse - udzial kiesy wsi wziety naprawde w tej sesji (komunikat), cacheGold - zloto ponad s0 przypisane skrytkom (podstawa XP), topUp - sam przelew
        /// doplaty.
        /// </summary>
        internal static void After(Hero leader, Settlement st, int roguery, double sRaw, double sTaken, double sPurse, long cacheGold, long topUp, bool player, bool side)
        {
            if (leader == null || !(sRaw > 0.0)) return;
            var s = Settings.Current;
            if (s == null) return;
            try
            {
                _dSess++;
                if (player) _dPlayer++;
                if (side) _dSide++;
                _dSumS += sRaw; if (sRaw > _dMaxS) _dMaxS = sRaw;
                if (cacheGold > 0) { _dSessGold++; _dGold += cacheGold; }
                if (topUp > 0) _dTopUp += topUp;
                // XP Roguery za zloto ze skrytek (z sufitem na rabunek)
                int xp = 0;
                if (cacheGold > 0 && s.RaidCacheXpPerGold > 0f)
                {
                    double x = Math.Min(Math.Max(0, s.RaidCacheXpCap), Math.Max(0f, s.RaidCacheXpPerGold) * (double)cacheGold);
                    xp = (int)Math.Floor(x);
                    if (xp > 0) { leader.AddSkillXp(DefaultSkills.Roguery, xp); _dXp += xp; }
                }
                // kara relacji za tortury: -1 za kazde 10 pkt S x u ponad prog
                int rel = 0;
                double over = Clamp01(s.RaidCacheRelationOver);
                if (sTaken > over + 1e-9) rel = (int)Math.Floor((sTaken - over) / RelationStep + 1e-9);
                int hit = 0;
                // recenzja 362 (1-1): kara tylko, gdy tortury cos wyciagnely (doplata skrytek > 0) - pusta kiesa albo s0 >= S x u: nic nie ukryto, nie ma za co karac
                if (cacheGold <= 0) rel = 0;
                // recenzja 362 (1-2): raz na dobe na pare (wodz, wies gry) - kilka wiosek pobocznych tego samego okregu nie mnozy kary u tego samego pana i notabli
                if (rel > 0 && st != null && !_relDone.Add((leader.StringId ?? "?") + "|" + (st.StringId ?? "?"))) rel = 0;
                if (rel > 0 && st != null && !ReferenceEquals(st.OwnerClan, leader.Clan))
                {
                    var lord = st.OwnerClan != null ? st.OwnerClan.Leader : null;
                    if (lord != null && lord != leader && lord.IsAlive) { ChangeRelationAction.ApplyRelationChangeBetweenHeroes(leader, lord, -rel, player); hit++; }
                    var ns = st.Notables;
                    if (ns != null)
                        for (int i = 0; i < ns.Count; i++)
                        {
                            var n = ns[i];
                            if (n == null || n == leader || n == lord || !n.IsAlive) continue;
                            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(leader, n, -rel, player);
                            hit++;
                        }
                    _dRelHeroes += hit; _dRelSum += hit * rel;
                }
                if (player && cacheGold > 0)
                {
                    string vn = "the village";
                    try { if (st != null && st.Name != null) vn = st.Name.ToString(); } catch { }
                    double pct = 100.0 * Clamp01(sPurse);
                    Log.Player("Your men tortured the villagers of " + vn + " into revealing their hidden coin: " + cacheGold.ToString("N0", Inv) + " denars (Roguery " + roguery
                               + ": " + pct.ToString("0", Inv) + "% of the village purse)."
                               + (hit > 0 ? " Its lord and elders will not forget it (" + (-rel).ToString(Inv) + " relation each)." : ""));
                }
            }
            catch (Exception e) { Stumble("After", e); }
        }

        /// <summary>Linia doby (ArmouryBehavior po DistrictRaid.Daily) i zerowanie licznikow.</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s != null && s.LogEnabled && (On || _dSess > 0))
                {
                    int day = (int)CampaignTime.Now.ToDays - 1;
                    var sb = new StringBuilder(400);
                    sb.Append("Skrytki wsi (362): dzien ").Append(day.ToString(Inv))
                      .Append(" | ").Append(On ? "WLACZONE" : "WYLACZONE (Raid Cache By Roguery w MCM)")
                      .Append(" (S = min(").Append(s.RaidCacheCap.ToString("0.00", Inv)).Append(", ").Append(s.RaidCacheBase.ToString("0.000", Inv)).Append(" + ")
                      .Append(s.RaidCachePerSkill.ToString("0.0000", Inv)).Append(" x Roguery + perki) x u)")
                      .Append(" | sesji z S ").Append(_dSess).Append(" (gracz ").Append(_dPlayer).Append(", wioski poboczne ").Append(_dSide).Append(", ze zlotem skrytek ").Append(_dSessGold).Append(')')
                      .Append(", zloto ze skrytek ").Append(_dGold.ToString(Inv)).Append(" zl (ponad s0; sam przelew doplaty ").Append(_dTopUp.ToString(Inv)).Append(" zl)")
                      .Append(", sredni S ").Append(_dSess > 0 ? (_dSumS / _dSess).ToString("0.000", Inv) : "-").Append(", max S ").Append(_dSess > 0 ? _dMaxS.ToString("0.000", Inv) : "-")
                      .Append(", XP Roguery ").Append(_dXp.ToString(Inv))
                      .Append(", kara relacji ").Append(_dRelSum).Append(" pkt u ").Append(_dRelHeroes).Append(" bohaterow")
                      .Append(" | rabunek vs okup AI (z rachunku zysku R3-03a): wybrany rabunek ").Append(_dAiRaid).Append(" (przez skrytki ").Append(_dAiRaidByCache).Append(") / okup ").Append(_dAiTrib);
                    if (_dStumbles > 0) sb.Append(" | potkniecia ").Append(_dStumbles);
                    sb.Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            NewDay();
        }
    }
}
