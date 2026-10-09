using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// KSIEGA WOJNY (Jeff 31.08, "rob"): dwa prawa pieniadza i miecza.
    /// 1) ZALEGLY ZOLD = DEZERCJE. Vanilla juz karze morale za niewyplacony
    ///    zold (HasUnpaidWages) - my dokladamy druga polowe prawdy: po okresie
    ///    laski (2 dni) armia zaczyna sie ROZCHODZIC, dziennie 0.5% ludzi za
    ///    kazdy dzien zwloki, ELITY PIERWSZE (najwyzszy tier odchodzi
    ///    najszybciej - najemnik zna swoja cene). AI placi te sama cene, ale
    ///    o polowe lagodniej (biedni lordowie BK nie moga stopniec globalnie).
    ///    Garnizony i umarli poza prawem (osada placi; trup zoldu nie bierze).
    /// 2) SZTURM ZOSTAWIA KRATER. Miasto/zamek wziete obleczeniem traci
    ///    prosperity (dom. -15%) i lojalnosc (-15) - zdobycz jest zdobycza
    ///    ZRUJNOWANA, ktora trzeba odbudowac, nie darmowa nagroda.
    /// </summary>
    internal static class WarLedger
    {
        private static readonly Dictionary<MobileParty, int> _unpaidDays = new Dictionary<MobileParty, int>();
        private static int _leftStumbles;      // T4: ludzie zdjeci, ktorych nie dalo sie wpisac do rosteru odchodzacych (doba)
        private static bool _errLeft;          // T4: ten blad tylko raz do logu

        internal static void OnDaily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.WagesDueEnabled) return;

                var seen = new List<MobileParty>();
                // T4: liczniki doby (wszystkie dezercje WarLedger zachodza w tym jednym wywolaniu)
                int dayParties = 0, dayGone = 0, dayPeople = 0; float dayPool = 0f;
                int peopleStumbles0 = PeopleLedger.StumblesToday;
                _leftStumbles = 0;
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsActive || !mp.IsLordParty) continue;
                    if (Undead.Party(mp)) continue;
                    bool unpaid = false;
                    try { unpaid = mp.HasUnpaidWages > 0f; } catch { }
                    // PLACI TEN, KTO MA CZYM (Jeff 19.09: "czemu znowu ubylo mi 6 ludzi, mialem 320
                    // i mam 314" - przy ponad 500 tys. zlota w skarbcu). MobileParty.HasUnpaidWages
                    // to NIE flaga "dzis nie zaplacono", tylko KWOTA zalegosci zapisywana w save
                    // (public float, SaveableField 1006). Raz niespłacona zostaje niezerowa i przy
                    // Banner Kings, ktory przejmuje finanse klanu, potrafi wisiec tak na zawsze -
                    // log 19.09 pokazuje lordow AI na 19, 20, 21, 22 i 23 dniu zwloki z rzedu.
                    // Nasz licznik rosl bez konca, a stawka 0.5%/dzien x dni siegala 11% skladu
                    // DZIENNIE. Od teraz pytamy o PIENIADZE, nie o pole: klan, ktory ma w kasie na
                    // dzienny zold tej partii, dluznikiem nie jest.
                    if (unpaid)
                    {
                        try
                        {
                            var payer = mp.ActualClan ?? (mp.LeaderHero != null ? mp.LeaderHero.Clan : null);
                            int wage = 0;
                            try { wage = mp.TotalWage; } catch { }
                            if (payer != null && payer.Gold >= Math.Max(1, wage)) unpaid = false;
                        }
                        catch { }
                    }
                    if (!unpaid) { _unpaidDays.Remove(mp); continue; }

                    int d;
                    _unpaidDays.TryGetValue(mp, out d);
                    _unpaidDays[mp] = ++d;
                    seen.Add(mp);
                    int over = d - Math.Max(0, s.WagesGraceDays);
                    int overCap = Math.Max(1, s.WagesDesertMaxDays);
                    if (over > overCap) over = overCap;           // bez sufitu dlug sprzed tygodni wykrwawia armie w kilka dni
                    if (over <= 0)
                    {
                        if (mp == MobileParty.MainParty)
                            Log.Player("The war chest is empty - the men grumble. Pay them, or they will start walking.", true);
                        continue;
                    }

                    float pct = Math.Max(0f, s.WagesDesertPercentPerDay) / 100f * over;
                    if (mp != MobileParty.MainParty) pct *= 0.5f;      // AI lagodniej - swiat nie moze stopniec
                    int men = mp.MemberRoster.TotalManCount;
                    float exp = men * pct;
                    int leave = (int)exp;
                    if (MBRandom.RandomFloat < exp - leave) leave++;
                    if (leave <= 0) continue;

                    // T4 (noc 08/09.10): odchodzacy zbierani do rosteru, zeby nie znikali w nicosc
                    bool toOutlaws = s.WarLedgerToOutlaws;
                    var left = toOutlaws ? TroopRoster.CreateDummyTroopRoster() : null;
                    int gone = DesertElitesFirst(mp, leave, left);
                    if (gone <= 0) continue;
                    // T4: dezerterzy ida do puli wyrzutkow regionu i do ksiegi ludzi jak kazdy dezerter gry.
                    // Wolamy sluchaczy WPROST, bez CampaignEvents.OnTroopsDeserted - gra tych ludzi nie
                    // widziala (AddToCounts nie strzela zdarzeniem), wiec nic nie liczy sie dwa razy,
                    // a cudzych sluchaczy (BK, DTE i in.) nie budzimy.
                    // Dopisek w logu MIERZY (stan puli regionu i licznik ksiegi przed i po), nie powtarza "gone".
                    string where = "";
                    if (left != null)
                    {
                        float added = 0f; int toPeople = 0;
                        try
                        {
                            var reg = OutlawLaw.RegionAt(mp.Position.ToVec2());
                            float before = OutlawLaw.PoolIn(reg);
                            OutlawLaw.OnTroopsDeserted(mp, left);
                            added = OutlawLaw.PoolIn(reg) - before;
                            if (reg != null) where = reg.StringId;
                        }
                        catch (Exception e) { Log.Error("WarLedger.ToOutlaws", e); }
                        try
                        {
                            int p0 = PeopleLedger.DesertedLordToday;
                            PeopleLedger.OnTroopsDeserted(mp, left);
                            toPeople = PeopleLedger.DesertedLordToday - p0;
                        }
                        catch (Exception e) { Log.Error("WarLedger.ToPeopleLedger", e); }
                        dayPool += added; dayPeople += toPeople;
                        where = OutlawLaw.On
                            ? "; do puli wyrzutkow " + added.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " (region " + (where.Length > 0 ? where : "brak") + "), do ksiegi ludzi " + toPeople
                            : "; prawo wyrzutkow wylaczone - " + gone + " ludzi znika (tylko ksiega ludzi " + toPeople + ")";
                    }
                    dayParties++; dayGone += gone;
                    // KAZDY ubytek do PLIKU, takze u gracza. Do 19.09 strata gracza szla wylacznie
                    // przez Log.Player, ktory pokazuje komunikat w grze i NIC nie zapisuje - przez to
                    // w logu nie bylo po niej ani sladu i szukanie winnego trwalo dwa dni.
                    long purse = -1;
                    try { var pc = mp.ActualClan ?? (mp.LeaderHero != null ? mp.LeaderHero.Clan : null); if (pc != null) purse = pc.Gold; } catch { }
                    Log.Info("WarLedger: " + (mp == MobileParty.MainParty ? "PARTIA GRACZA" : mp.StringId)
                             + " traci " + gone + " ludzi (zold niewyplacony " + d + " dni, liczone jak " + over
                             + "; zalegosc " + mp.HasUnpaidWages.ToString("0.##") + ", dzienny zold " + mp.TotalWage
                             + ", kasa klanu " + purse + ")" + where + ".");
                    if (mp == MobileParty.MainParty)
                        Log.Player("Unpaid and unbound: " + gone + " men desert in the night - the best-paid first.", true);
                }
                // T4: jedna linia na dobe - autotest porownuje "zdjeto" z "do puli" bez mieszania z dezercja gry
                if (dayGone > 0 && s.WarLedgerToOutlaws)
                    Log.Info("WarLedger doba: zdjeto " + dayGone + " ludzi w " + dayParties + " partiach; do puli wyrzutkow "
                             + dayPool.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                             + (OutlawLaw.On ? "" : " (prawo wyrzutkow wylaczone)") + ", do ksiegi ludzi " + dayPeople
                             + ", potkniecia " + (_leftStumbles + Math.Max(0, PeopleLedger.StumblesToday - peopleStumbles0)) + ".");
                if (_unpaidDays.Count > seen.Count + 50)
                {
                    var drop = new List<MobileParty>();
                    foreach (var kv in _unpaidDays)
                        if (kv.Key == null || !kv.Key.IsActive || !seen.Contains(kv.Key)) drop.Add(kv.Key);
                    foreach (var k in drop) _unpaidDays.Remove(k);
                }
            }
            catch (Exception e) { Log.Error("WarLedger.OnDaily", e); }
        }

        /// <summary>Najemnik zna swoja cene: dezerteruja od najwyzszego tieru.</summary>
        private static int DesertElitesFirst(MobileParty mp, int count, TroopRoster left)
        {
            int gone = 0;
            try
            {
                var roster = mp.MemberRoster;
                while (count > 0)
                {
                    int best = -1, bestTier = -1;
                    for (int i = 0; i < roster.Count; i++)
                    {
                        var ch = roster.GetCharacterAtIndex(i);
                        if (ch == null || ch.IsHero) continue;
                        int n = roster.GetElementNumber(i);
                        if (n <= 0) continue;
                        if (ch.Tier > bestTier) { bestTier = ch.Tier; best = i; }
                    }
                    if (best < 0) break;
                    var c = roster.GetCharacterAtIndex(best);
                    int take = Math.Min(count, roster.GetElementNumber(best));
                    roster.AddToCounts(c, -take);
                    gone += take; count -= take;
                    if (left != null)
                    {
                        // T4: kto odszedl; blad tu = czlowiek zdjety, ale nie przekazany - liczymy potkniecie
                        try { left.AddToCounts(c, take); }
                        catch (Exception e) { _leftStumbles += take; if (!_errLeft) { _errLeft = true; Log.Error("WarLedger.LeftRoster", e); } }
                    }
                }
            }
            catch { }
            return gone;
        }

        /// <summary>Miasto wziete obleczeniem: prosperity -15%, lojalnosc -15.</summary>
        internal static void OnOwnerChanged(Settlement st, bool openToClaim, Hero newOwner, Hero oldOwner,
                                            Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.SackScarEnabled) return;
                if (detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege) return;
                var town = st != null ? st.Town : null;
                if (town == null) return;
                float cut = MBMath.ClampFloat(Math.Max(0, s.SackProsperityCutPercent) / 100f, 0f, 0.6f);
                float before = town.Prosperity;
                town.Prosperity = Math.Max(0f, town.Prosperity * (1f - cut));
                town.Loyalty = Math.Max(0f, town.Loyalty - Math.Max(0, s.SackLoyaltyHit));
                Log.Info("WarLedger: " + st.StringId + " wziete obleczeniem - prosperity " + (int)before
                         + " -> " + (int)town.Prosperity + ", lojalnosc -" + s.SackLoyaltyHit + ".");
                if (newOwner != null && newOwner.Clan == Clan.PlayerClan)
                    Log.Player(st.Name + " is yours - but the sack has left it bleeding: prosperity and loyalty are down. Rebuild what you broke.", true);
            }
            catch (Exception e) { Log.Error("WarLedger.OnOwnerChanged", e); }
        }
    }
}
