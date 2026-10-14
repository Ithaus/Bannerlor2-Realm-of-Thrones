using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// L2 - LUP Z MONETY (projekt lupu miast rozdz. 3.1-3.3, 3.7; decyzje Jeffa 13.10 pkt 1, 2, 4). Klucze SackTakesRealCoin (regula), SackScarOnlyWhenSacked (blizna).
    ///  - Regula: w osadzie po nastepstwach zdobywcy biora czesc KAZDEJ monety, ktora tam lezy - z kasy kupcow (Town.Gold, cala, takze zapas kupcow) i z kies
    ///    ludu (W, R, P, gmina; zamek - podzamcze; cale, takze zapas na chleb): laska 0, grabiez SackSharePillage (1/3), spustoszenie SackShareDevastate (2/3);
    ///    z kazdego zbiornika floor(czesc x stan w tej chwili). Kwota gry "15 x utracony dobrobyt" wylaczona (prefiks GetSiegeAftermathArmyGoldGain = 0, takze
    ///    w podpowiedzi menu - napisy daje L5). "Right of Pillage" (IncidentsCampaignBehaviour, gracz) idzie ta sama metoda gry - ta sama regula.
    ///  - Kto dostaje: druzyny z listy udzialow gry (strona zwycieska; lista pusta - druzyna, ktora lupi), wedlug ludzi (zdrowi i ranni), reszta z zaokraglen
    ///    do najwiekszej - nic nie przepada. W druzynie prawo trzecich (MenPurse 2/3 / 1/3): 2/3 do sakiewki ludzi, 1/3 wodzowi (druzyna bez wodza - jej kiesa);
    ///    sakiewki wylaczone (MenPurseEnabled) - calosc wodzowi. Korona: trzecia wodza do podstawy prawa trzecich (ClanIncomeBook KThird -> Ransom178.NoteThird,
    ///    gracz i AI przy wylaczonej ksiedze - Ransom178.NoteThird wprost) - korona krolestwa w wojnie bierze jej 1/3 nastepnego ranka (1/9 calosci; bez najemnikow i krola-gracza).
    ///    Inni (Undead) nie biora monety - ich ludzie bez udzialu; same Inni - lupu nie ma, moneta zostaje.
    ///  - Obieg: kasa osady (ChangeGold) i kiesy ludu (TownFolk.SackTake - ksiega kies _ctlOut) -> pula -> sakiewki ludzi (MenPurse.Add), kiesy wodzow
    ///    (ChangeHeroGold), kiesy druzyn bez wodza (PartyTradeGold) - same posiadacze "Pieniadz swiata", suma swiata bez zmian. Bez GiveGoldAction - prefiks
    ///    RealPayer (kontekst "lup z oblezen" na tej samej metodzie) nie ma czego przycinac. Wyjatek przy podziale - niepodzielona reszta wraca do zbiornikow, z ktorych zeszla (kiesy ludu wedlug udzialu, reszta - kasa).
    ///  - Blizna (klucz SackScarOnlyWhenSacked, przy SackScarEnabled): -SackProsperityCutPercent dobrobytu reszty i -SackLoyaltyHit lojalnosci tylko przy grabiezy
    ///    i spustoszeniu, w chwili wyboru (postfiks - po karze gry, ta sama kolejnosc u AI i gracza; D5, D6). WarLedger.OnOwnerChanged wtedy blizny nie naklada.
    ///  - Wiadomosci (po angielsku): gracz-wodz (gdy L5 wylaczone albo "Right of Pillage" bez menu), gracz w cudzej armii, gracz-pan zlupionej osady.
    /// </summary>
    internal static partial class CitySack
    {
        internal static bool CoinOn { get { var s = Settings.Current; return s != null && s.SackTakesRealCoin; } }
        internal static bool ScarAtChoice { get { var s = Settings.Current; return s != null && s.SackScarOnlyWhenSacked && s.SackScarEnabled; } }

        /// <summary>Wynik ostatniego lupu (podsumowanie gracza L5).</summary>
        internal sealed class Result
        {
            public Settlement St; public int Kind; public double Share; public long Merch, Folk, Pool, PlayerMen, PlayerLead, PlayerCrown; public int Men, Parties;
        }
        internal static Result Last;

        // liczniki doby (linia "Lup miast")
        private static int _dSacks, _dMercies, _dNoTaker;
        private static long _dMerch, _dFolk, _dToMen, _dToLead, _dToParty, _dCrownBase, _dDiff;
        private static int _dScar;

        private static long Fl(double share, long v) { return v <= 0 || share <= 0 ? 0 : (long)Math.Floor(share * v); }

        private static void AftermathCoin(SiegeAftermathCampaignBehavior inst, MobileParty a, Settlement st, int kind, Clan prev, Dictionary<MobileParty, float> contrib, Stock k, Track t)
        {
            if (!CoinOn) return;
            try { Sack(a, st, kind, prev, contrib, t); }
            catch (Exception e) { Stumble("Sack", e); }
        }

        private static void Sack(MobileParty a, Settlement st, int kind, Clan prev, Dictionary<MobileParty, float> contrib, Track t)
        {
            var town = st.Town;
            double share = ShareOf(kind);
            int men, uMen;
            var recs = Recipients(a, contrib, out men, out uMen);
            var live = new List<Rec>();
            foreach (var r in recs) if (!r.Undead) live.Add(r);
            var k = Read(st);                                              // stan TERAZ (po migawce L0 nic sie nie zmienilo - prefiks)
            long[] want = new long[5];                                     // 0 kupcy, 1-4 lud W, R, P, gmina
            long[] took = new long[5];
            long pool = 0, given = 0, crownBase = 0;
            Rec big = null;
            if (share > 0 && men > 0)
            {
                want[0] = Fl(share, k.Kasa);
                for (int c = 0; c < 4; c++) want[c + 1] = Fl(share, k.Folk[c]);
                // ---- zdjecie: kasa kupcow, potem kiesy ludu (ksiega kies)
                int had = town.Gold;
                if (want[0] > 0) town.ChangeGold(-(int)Math.Min(int.MaxValue, want[0]));
                took[0] = Math.Max(0, had - town.Gold);
                for (int c = 0; c < 4; c++) took[c + 1] = want[c + 1] > 0 ? TownFolk.SackTake(st, c, want[c + 1]) : 0;
                for (int i = 0; i < 5; i++) pool += took[i];
                // ---- podzial wedlug ludzi; reszta z zaokraglen do najwiekszej druzyny
                try
                {
                    long split = 0;
                    foreach (var r in live)
                    {
                        r.Share = pool * r.Men / men;
                        split += r.Share;
                        if (big == null || r.Men > big.Men) big = r;
                    }
                    if (big != null) big.Share += pool - split;
                    foreach (var r in live)
                    {
                        if (r.Share <= 0) continue;
                        if (MenPurse.On) { r.ToLead = r.Share / 3; r.ToMen = r.Share - r.ToLead; }
                        else { r.ToLead = r.Share; r.ToMen = 0; }
                        if (r.ToMen > 0) { MenPurse.Add(r.Mp, (int)Math.Min(int.MaxValue, r.ToMen)); given += r.ToMen; _dToMen += r.ToMen; }
                        if (r.ToLead > 0)
                        {
                            int lead = (int)Math.Min(int.MaxValue, r.ToLead);
                            if (r.Lead != null)
                            {
                                r.Lead.ChangeHeroGold(lead);
                                given += lead; _dToLead += lead;
                                // prawo trzecich (decyzja 2): trzecia wodza w podstawie - korona w wojnie bierze jej 1/3 jutro. Recenzja (obieg 9): przy wylaczonej
                                // ksiedze dochodow rodow (ClanIncomeBook) AI tez wprost (dotad tylko gracz); licznik podstawy - tylko obciazeni (bez najemnikow, krola-gracza)
                                if (r.Lead == Hero.MainHero || !ClanIncomeBook.On) Ransom178.NoteThird(r.Lead, lead);
                                else ClanIncomeBook.NoteInflow(r.Lead, lead, ClanIncomeBook.KThird);
                                if (Ransom178.WouldTax(r.Lead)) { _dCrownBase += lead; crownBase += lead; }
                            }
                            else
                            {
                                r.Mp.PartyTradeGold += lead;                // druzyna bez wodza - jej kiesa (jak gra :169)
                                given += lead; _dToParty += lead;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Stumble("Sack.Split", e);
                    // czego nie podzielono - wraca tam, skad zeszlo (recenzja: kazda klasa ludu swoja czesc, wedlug udzialu w puli; reszta - kasa kupcow); nic nie znika
                    long back = pool - given;
                    if (back > 0)
                    {
                        long rest = back;
                        for (int c = 1; c <= 4 && rest > 0; c++)
                        {
                            long x = Math.Min(rest, Math.Min(took[c], pool > 0 ? (long)Math.Floor((double)back * took[c] / pool) : 0));
                            if (x > 0) { TownFolk.SackBack(st, c - 1, x); rest -= x; }
                        }
                        if (rest > 0) town.ChangeGold((int)Math.Min(int.MaxValue, rest));
                        given += back;
                    }
                }
            }
            else if (share > 0 && uMen > 0) _dNoTaker++;
            long diff = given - pool;
            _dDiff += Math.Abs(diff);
            if (kind == KMercy) _dMercies++; else if (pool > 0) _dSacks++;
            _dMerch += took[0]; _dFolk += took[1] + took[2] + took[3] + took[4];
            // ---- wynik dla napisow (L5) i okna lupu (L3)
            long folkTook = took[1] + took[2] + took[3] + took[4];
            if (t != null) { t.Merch = took[0]; t.FolkTaken = folkTook; }
            var res = new Result { St = st, Kind = kind, Share = share, Merch = took[0], Folk = folkTook, Pool = pool, Men = men, Parties = live.Count };
            foreach (var r in live)
                if (r.Mp == MobileParty.MainParty) { res.PlayerMen = r.ToMen; res.PlayerLead = r.ToLead; res.PlayerCrown = r.Lead != null ? CrownPart(r.Lead, r.ToLead) : 0; }
            Last = res;
            // ---- linia L2
            if (MeasureOn)
            {
                var sb = new StringBuilder(700);
                sb.Append("Lup miast (L2): dzien ").Append((int)CampaignTime.Now.ToDays).Append(" | osada ").Append(st.StringId).Append(" | wybor ").Append(KName[kind])
                  .Append(" | czesc ").Append(share.ToString("0.000", Inv))
                  .Append(" | zdjete: kupcy ").Append(took[0]).Append(" z ").Append(k.Kasa)
                  .Append(", W ").Append(took[1]).Append(" z ").Append(k.Folk[0]).Append(", R ").Append(took[2]).Append(" z ").Append(k.Folk[1])
                  .Append(", P ").Append(took[3]).Append(" z ").Append(k.Folk[2]).Append(", gmina ").Append(took[4]).Append(" z ").Append(k.Folk[3])
                  .Append(" (kiesa ").Append(k.FolkLive ? "czynna" : "nieczynna").Append(") | razem ").Append(pool)
                  .Append(" | odbiorcy ").Append(live.Count).Append(" druzyn (ludzie ").Append(men).Append("; Inni ").Append(uMen).Append(" - bez udzialu)");
                long tm = 0, tl = 0, tp = 0, pl = 0;
                foreach (var r in live) { tm += r.ToMen; if (r.Lead != null) tl += r.ToLead; else tp += r.ToLead; if (r.Mp == MobileParty.MainParty) pl = r.ToMen + r.ToLead; }
                sb.Append(" | zolnierze ").Append(tm).Append(", wodzowie ").Append(tl).Append(" (gracz razem ").Append(pl).Append("), kiesy druzyn bez wodza ").Append(tp)
                  .Append(", reszta zaokraglen do ").Append(big != null ? big.Mp.StringId : "-")
                  .Append(" | korona: trzecie wodzow w podstawie prawa trzecich ").Append(crownBase)
                  .Append(" | dopisane ").Append(given).Append(" - zdjete ").Append(pool).Append(" = ").Append(diff).Append('.');
                Log.Info(sb.ToString());
            }
            Messages(a, st, kind, prev, contrib, live, res);
        }

        /// <summary>Czesc trzeciej wodza, ktora korona wezmie jutro (tylko do napisu): prawo trzecich czynne i bohater obciazony (Ransom178.Taxed).</summary>
        internal static long CrownPart(Hero h, long third)
        {
            try
            {
                var s = Settings.Current;
                if (h == null || third <= 0 || s == null || !Ransom178.WouldTax(h)) return 0;
                return (long)(third * Math.Max(0f, Math.Min(1f, s.CrownThirdsShare)) * 3.0);
            }
            catch { return 0; }
        }

        private static string N(long v) { return v.ToString("N0", Inv); }

        private static void Messages(MobileParty a, Settlement st, int kind, Clan prev, Dictionary<MobileParty, float> contrib, List<Rec> live, Result res)
        {
            try
            {
                var main = MobileParty.MainParty;
                string town = st.Name != null ? st.Name.ToString() : st.StringId;
                bool castle = !st.IsTown;                   // poprawka po audycie (gracz 7, 8): zamek - skarbiec i ludzie podzamcza; pieniadz zawsze "denars"
                if (a == main && main != null)
                {
                    // gracz-wodz: podsumowanie w menu (L5); bez L5 albo bez menu ("Right of Pillage") - wiadomosc
                    bool menu = WindowOn && contrib != null && contrib.Count > 0;   // L5: podsumowanie w menu po wyborze (bez menu - "Right of Pillage")
                    if (!menu && kind != KMercy && res.Pool > 0)
                        Log.Player("Your army took " + N(res.Pool) + " denars from " + town + ": " + N(res.Merch) + (castle ? " from its treasury, " : " from its merchants, ") + N(res.Folk)
                                   + (castle ? " from its castle folk. " : " from its townsfolk. ")
                                   + "Your men's purses grew by " + N(res.PlayerMen) + "; your own part is " + N(res.PlayerLead) + ".");
                }
                else if (main != null && contrib != null && contrib.ContainsKey(main))
                {
                    string lead = a != null && a.LeaderHero != null ? a.LeaderHero.Name.ToString() : "The army's leader";
                    if (kind == KMercy) Log.Player(lead + " spared " + town + ". There is no plunder.");
                    else if (res.PlayerMen + res.PlayerLead > 0)
                        Log.Player(lead + " let the army sack " + town + ". Your party's part: " + N(res.PlayerMen + res.PlayerLead) + " denars (your men " + N(res.PlayerMen) + ", you " + N(res.PlayerLead) + ").");
                }
                if (prev != null && prev == Clan.PlayerClan && kind != KMercy && res.Pool > 0)
                    Log.Player(town + (castle ? " has been sacked: its treasury lost " : " has been sacked: its merchants lost ") + N(res.Merch)
                               + (castle ? " denars, its castle folk " : " denars, its townsfolk ") + N(res.Folk) + ".", true);
            }
            catch (Exception e) { Stumble("Messages", e); }
        }

        // ------------------------------------------------------------ kwota gry "15 x utracony dobrobyt" - wylaczona (prefiks GetSiegeAftermathArmyGoldGain)
        public static bool GoldGainPre(ref int __result)
        {
            if (!CoinOn) return true;
            __result = 0;
            return false;
        }

        // ------------------------------------------------------------ blizna w chwili wyboru (postfiks OnSiegeAftermathApplied - po karze gry)
        public static void AftermathPost(Settlement __1, SiegeAftermathAction.SiegeAftermath __2)
        {
            try
            {
                if (!Live || !ScarAtChoice || __1 == null || __1.Town == null) return;
                // recenzja (kod, drobne): osada niezdobyta (L1 wylaczone - nastepstwa po bitwie pod murami) - blizny nie ma, jak przed paczka (blizna szla przy zmianie pana)
                Track tr;
                if (_track.TryGetValue(__1, out tr) && tr != null && !tr.Captured && Now - tr.Day0 < 0.01f)
                {
                    if (MeasureOn) Log.Info("Lup miast (L2 blizna): dzien " + (int)CampaignTime.Now.ToDays + " | osada " + __1.StringId + " | bez zdobycia - bez blizny.");
                    return;
                }
                int kind = KindOf(__2);
                var town = __1.Town;
                var s = Settings.Current;
                if (kind == KMercy)
                {
                    if (MeasureOn) Log.Info("Lup miast (L2 blizna): dzien " + (int)CampaignTime.Now.ToDays + " | osada " + __1.StringId + " | laska - bez blizny (zostaje kara gry).");
                    return;
                }
                float cut = MBMath.ClampFloat(Math.Max(0, s.SackProsperityCutPercent) / 100f, 0f, 0.6f);
                float before = town.Prosperity;
                town.Prosperity = Math.Max(0f, town.Prosperity * (1f - cut));
                town.Loyalty = Math.Max(0f, town.Loyalty - Math.Max(0, s.SackLoyaltyHit));
                _dScar++;
                if (MeasureOn)
                    Log.Info("Lup miast (L2 blizna): dzien " + (int)CampaignTime.Now.ToDays + " | osada " + __1.StringId + " | " + KName[kind] + " - dobrobyt " + (int)before + " -> "
                             + (int)town.Prosperity + ", lojalnosc -" + s.SackLoyaltyHit + ".");
            }
            catch (Exception e) { Stumble("AftermathPost", e); }
        }

        /// <summary>WarLedger.OnOwnerChanged (BySiege) przy SackScarOnlyWhenSacked: blizna juz nalozona przy wyborze (AI) albo czeka na wybor gracza - linia
        /// i komunikat graczowi, gdy dostaje osade zlupiona dzis. Nie rzuca.</summary>
        internal static void OwnerChangedScarSkipped(Settlement st, Hero newOwner)
        {
            try
            {
                Ensure();
                Track t;
                bool sacked = st != null && _track.TryGetValue(st, out t) && t != null && t.Kind != KMercy && Now - t.Day0 < 1f;
                Log.Info("WarLedger: " + (st != null ? st.StringId : "?") + " wziete obleczeniem - blizna w chwili wyboru zdobywcy (Sack Scar Only When Sacked): "
                         + (sacked ? "zlupione dzis" : "laska albo wybor gracza jeszcze nie zapadl") + ".");
                if (sacked && newOwner != null && newOwner.Clan == Clan.PlayerClan)
                    Log.Player(st.Name + " is yours - but the sack has left it bleeding: prosperity and loyalty are down. Rebuild what you broke.", true);
            }
            catch (Exception e) { Stumble("OwnerChangedScarSkipped", e); }
        }

        private static void L2Part(StringBuilder sb)
        {
            sb.Append(" | L2: ");
            if (!CoinOn) sb.Append("WYLACZONE (Sack Takes Real Coin) - kwota gry 15 x dobrobyt przez RealPayer; ");
            sb.Append("lupow ").Append(_dSacks).Append(", lask ").Append(_dMercies).Append(", zwyciezcy Inni (moneta zostaje) ").Append(_dNoTaker)
              .Append(", zdjete kupcom ").Append(_dMerch).Append(", ludowi ").Append(_dFolk).Append(" -> zolnierze ").Append(_dToMen).Append(", wodzowie ").Append(_dToLead)
              .Append(", druzyny bez wodza ").Append(_dToParty).Append("; korona: podstawa prawa trzecich ").Append(_dCrownBase).Append(" (jutro 1/3 w wojnie)")
              .Append("; kontrola dopisane - zdjete (prog 0) ").Append(_dDiff)
              .Append("; blizna przy wyborze ").Append(_dScar).Append(ScarAtChoice ? "" : " [blizna przy zmianie pana - Sack Scar Only When Sacked wylaczone]");
        }

        private static void L2Clear()
        {
            _dSacks = _dMercies = _dNoTaker = 0; _dMerch = _dFolk = _dToMen = _dToLead = _dToParty = _dCrownBase = _dDiff = 0; _dScar = 0;
        }

        private static bool L2Any { get { return _dSacks > 0 || _dMercies > 0 || _dNoTaker > 0 || _dScar > 0; } }

        private static void L2Patch()
        {
            var t = typeof(SiegeAftermathCampaignBehavior);
            Seal31.Patch("lup miast L2: kwota gry 15 x dobrobyt wylaczona (GetSiegeAftermathArmyGoldGain)", AccessTools.Method(t, "GetSiegeAftermathArmyGoldGain"), typeof(CitySack),
                         nameof(GoldGainPre), null, null, Priority.Low);
            Seal31.Patch("lup miast L2: blizna w chwili wyboru (OnSiegeAftermathApplied, postfiks)", AccessTools.Method(t, "OnSiegeAftermathApplied"), typeof(CitySack),
                         null, nameof(AftermathPost), null, Priority.Normal, Priority.Low);
        }
    }
}
