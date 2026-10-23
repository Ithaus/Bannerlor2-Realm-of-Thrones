using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// m98-plotki (Jeff 20.10 15:20: "w gossip tez powinny byc turnieje, gdzie jest turniej ... ze ktos wydaje zone albo szuka dla meza dla swojej corki jakis lord ...
    /// jak biore gossip, to musze kupic piwo lub ale"; 15:25: "za kazde wypite piwo moge dostac tyle plotek, ile moja charyzma"). Czesc Rumours:
    ///  - TURNIEJE (GossipTournaments, kategoria 7): turnieje gry w toku (TournamentManager.GetTournamentGame - te same, ktore liczy linia "Turnieje (E7/0)"
    ///    i ktore przejmuje GrandTourney), do 450 jedn. od karczmy, najblizsze 2; daleki turniej dopiero, gdy moglby doleciec kruk (Ravens.NotYet, jak wiesci wojenne).
    ///    Tresc: miasto, odleglosc i kierunek, kiedy ogloszony (TournamentGame.CreationTime), nagroda (TournamentGame.Prize) i jej cena na targu tego miasta.
    ///  - SWATY (GossipMatchmaking, kategoria 8): dorosle niezamezne dzieci rodow (Clan.All bez rodu gracza i band), ktore czynny model malzenstw gry/BK dopuszcza
    ///    do slubu (MarriageModel.IsSuitableForMarriage - u Jeffa ROT > BK: zywy, nie w niewoli, wiek, nie w bitwie/armii) i ktore spelniaja warunki domyslnego
    ///    modelu gry, ktorych BK nie sprawdza (recenzja gra: na mapie - IsActive, pan - IsLord, nie z rodu pomniejszego, nie notabl, nie wzor, nie zareczony
    ///    przez oferte slubu gry - IMarriageOfferCampaignBehavior.IsHeroEngaged), z zywym rodzicem (ojciec, inaczej
    ///    matka - to on/ona "szuka"), najwyzej 40 lat, bez glowy rodu; siedziba rodu (Clan.HomeSettlement) do 200 jedn. od karczmy - wiesci z bliska; najwyzej 2,
    ///    wazone tierem rodu i bliskoscia.
    ///  - Dawne kategorie bez zmian (najwyzej 6 jak dotad); nowe maja wlasne miejsca (po 2), wiec przy wylaczonych kluczach pula jest ta sama co przed galezia.
    ///  - KUFEL (InnGossipNeedsDrink): opcja "Buy a tankard of ale and listen to the gossip (N d)" w karczmie miasta i w zajezdzie. Cena = 1/10 ceny beczki piwa
    ///    ("beer", beczka 10 kg ok. 20 kufli, karczmarz x2) w miescie (zajazd - miasto handlowe wsi okregu, inaczej najblizsze); bez piwa w grze - 2 d. Zaplata ta
    ///    sama droga co nocleg gracza (TownInns.DrinkPayTown / DrinkPayRoad) z kontrola obiegu; komunikat z powodem (PayNotes). Opcja szara z podpowiedzia: brak
    ///    pieniedzy, karczmarz nie naleje (InnDrink.MaxCups), wszystko juz uslyszane tutaj. Plotek na kufel - InnDrink.PerDrink (Urok), kolejnosc jak dotad
    ///    (od najwazniejszej); uslyszane w tym miejscu nie wracaja (InnDrink.MarkHeard - skrot TOZSAMOSCI wiesci Item.Id, recenzja gra: dotad skrot tekstu, a tekst
    ///    zmienia sie z doba, przesada i pozycja - ten sam turniej wracal co dzien, banda co godzine); 5 XP Uroku za kazda. Zajazd wroga albo rabowany - nie nalewa
    ///    (TownInns.RoadDrinkBarred, te same warunki co nocleg).
    ///  - Pula dla miejsca liczona raz na godzine gry (warunek opcji i zaplata widza te sama pule).
    /// Linia "Kufle (m98): kufel w ..." przy kazdym kuflu: cena, beczka, komu, kontrola obiegu, Urok -> plotek, rodzaje, turnieje i swaty sprawdzone w chwili
    /// sluchania (turniej dalej w grze, kandydat zyje i jest wolny).
    /// </summary>
    internal static partial class Rumours
    {
        private const int CatTourn = 7, CatMatch = 8, NewCats = 9;
        private const float TournR = 450f, MatchR = 200f;
        private const int MatchMaxAge = 40, CharmXpPerItem = 5;
        private static bool TournOn { get { var s = Settings.Current; return s != null && s.InnGossip && s.GossipTournaments; } }
        private static bool MatchOn { get { var s = Settings.Current; return s != null && s.InnGossip && s.GossipMatchmaking; } }

        // pula miejsca - raz na godzine gry
        private static string _poolKey;
        private static long _poolHour = -1;
        private static List<Item> _pool;
        private static ItemObject _beer;
        private static bool _beerLooked;

        private static void DrinkReset() { _pool = null; _poolKey = null; _poolHour = -1; _beer = null; _beerLooked = false; }

        // ------------------------------------------------------------ turnieje
        private static void Tournaments(List<Item> all, Vec2 at, uint seed)
        {
            var tm = Campaign.Current != null ? Campaign.Current.TournamentManager : null;
            if (tm == null) return;
            var found = new List<KeyValuePair<float, TournamentGame>>();
            foreach (var t in Town.AllTowns)
            {
                if (t == null || t.Settlement == null) continue;
                var g = tm.GetTournamentGame(t);
                if (g == null) continue;
                float d = t.Settlement.GetPosition2D.Distance(at);
                if (d > TournR) continue;
                if (Ravens.NotYet(d, (int)g.CreationTime.ToDays)) continue;   // daleki turniej - wiesc dopiero z krukiem (jak wiesci wojenne)
                found.Add(new KeyValuePair<float, TournamentGame>(d, g));
            }
            found.Sort((x1, x2) => x1.Key.CompareTo(x2.Key));
            for (int i = 0; i < found.Count && i < 2; i++)
            {
                var g = found[i].Value;
                var t = g.Town;
                float d = found[i].Key;
                int age = Math.Max(0, (int)g.CreationTime.ElapsedDaysUntilNow);
                string when = age < 1 ? "only today" : age < 2 ? "yesterday" : age < 7 ? "a few days ago" : "some weeks ago";
                // recenzja (gra, technika): przyimek w galezi - "held here in X" / "held at X, a day's ride to the north" (dotad "held at here in X")
                string where = d < 6f ? "here in " + t.Name : "at " + t.Name + ", " + Days(d) + " to the " + Dir(at, t.Settlement.GetPosition2D);
                var sb = new StringBuilder();
                sb.Append(i == 0 ? "A tourney is being held " : "There is talk of another tourney ").Append(where).Append(" - it was proclaimed ").Append(when).Append('.');
                var pr = g.Prize;
                if (pr != null)
                {
                    int worth = 0;
                    try { worth = t.GetItemPrice(pr, null, false); } catch { worth = 0; }
                    sb.Append(" The prize is ").Append(AOrAn(pr.Name != null ? pr.Name.ToString() : "a fine prize"));
                    if (worth > 0) sb.Append(", worth some ").Append(Round(worth).ToString("N0", Inv)).Append(" denars");
                    sb.Append('.');
                }
                all.Add(new Item { Cat = CatTourn, Score = 2.6f + (1f - d / TournR), Text = sb.ToString(), Ref = t.Settlement.StringId,
                                   Id = "t|" + t.Settlement.StringId + "|" + ((int)g.CreationTime.ToDays).ToString(Inv) });   // ten sam turniej = ta sama plotka (wiek i cena nagrody bez znaczenia)
            }
        }

        private static string AOrAn(string n)
        {
            if (string.IsNullOrEmpty(n)) return "a prize";
            char c = char.ToLowerInvariant(n[0]);
            return ("aeiou".IndexOf(c) >= 0 ? "an " : "a ") + n;
        }

        // ------------------------------------------------------------ swaty
        private static Hero Parent(Hero h)
        {
            var me = Hero.MainHero;
            var f = h.Father;
            if (f != null && f.IsAlive && f != me) return f;
            var m = h.Mother;
            if (m != null && m.IsAlive && m != me) return m;
            return null;
        }

        /// <summary>Kandydat do swatow: zywy, wolny, dorosly do MatchMaxAge, nie glowa rodu, z zywym rodzicem (nie graczem), dopuszczony przez czynny model malzenstw.</summary>
        private static bool Candidate(Hero h, MarriageModel mm)
        {
            if (h == null || !h.IsAlive || h.Spouse != null || h == Hero.MainHero || h.IsChild || h.IsPrisoner || h.Age > MatchMaxAge) return false;
            var c = h.Clan;
            if (c == null || c == Clan.PlayerClan || c.IsBanditFaction || c.Leader == h) return false;
            if (Parent(h) == null) return false;
            // recenzja (gra): warunki domyslnego modelu gry, ktorych BK (czynny pod ROT) nie sprawdza - bohater na mapie, pan, nie z rodu pomniejszego, nie notabl,
            // nie wzor, nie zareczony przez oferte slubu gry
            if (!h.IsActive || !h.IsLord || h.IsMinorFactionHero || h.IsNotable || h.IsTemplate) return false;
            try
            {
                var offers = Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<TaleWorlds.CampaignSystem.CampaignBehaviors.IMarriageOfferCampaignBehavior>() : null;
                if (offers != null && offers.IsHeroEngaged(h)) return false;
            }
            catch { return false; }
            try { return mm.IsSuitableForMarriage(h); } catch { return false; }
        }

        private static MarriageModel Marriage() { return Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.MarriageModel : null; }

        private static void Matches(List<Item> all, Vec2 at, uint seed)
        {
            var mm = Marriage();
            if (mm == null) return;
            var pc = Clan.PlayerClan;
            var found = new List<KeyValuePair<float, Hero>>();
            foreach (var c in Clan.All)
            {
                if (c == null || c.IsEliminated || c.IsBanditFaction || c == pc || c.Heroes == null) continue;
                var seat = c.HomeSettlement;
                if (seat == null) continue;
                float d = seat.GetPosition2D.Distance(at);
                if (d > MatchR) continue;
                foreach (var h in c.Heroes)
                {
                    if (!Candidate(h, mm)) continue;
                    found.Add(new KeyValuePair<float, Hero>((Math.Max(0, c.Tier) + 2f) / (1f + d / 50f), h));
                }
            }
            found.Sort((x1, x2) => x2.Key.CompareTo(x1.Key));
            for (int i = 0; i < found.Count && i < 2; i++)
            {
                var h = found[i].Value;
                var p = Parent(h);
                var c = h.Clan;
                var seat = c.HomeSettlement;
                float d = seat.GetPosition2D.Distance(at);
                bool girl = h.IsFemale;
                string rel = (p.IsFemale ? "her " : "his ") + (girl ? "daughter" : "son");
                string first = h.FirstName != null ? h.FirstName.ToString() : h.Name.ToString();
                if (string.IsNullOrEmpty(first) || first.IndexOf('{') >= 0) first = h.Name.ToString();
                string house = c.Name != null ? c.Name.ToString() : "";
                var sb = new StringBuilder();
                sb.Append(i == 0 ? "They say " : "And ").Append(p.Name);
                if (house.Length > 0 && p.Name.ToString().IndexOf(house, StringComparison.Ordinal) < 0) sb.Append(" of ").Append(house);
                sb.Append(" is seeking ").Append(girl ? "a husband" : "a wife").Append(" for ").Append(rel).Append(' ').Append(first)
                  .Append(", ").Append(((int)h.Age).ToString(Inv)).Append(" years old");
                sb.Append(d < 6f ? ", here in " + seat.Name : " - their seat is " + seat.Name + ", " + Days(d) + " to the " + Dir(at, seat.GetPosition2D));
                sb.Append(c.Tier >= 4 ? ". A great house - a match worth having." : ".");
                all.Add(new Item { Cat = CatMatch, Score = 2.0f + Math.Min(1.5f, found[i].Key / 4f), Text = sb.ToString(), Ref = h.StringId, Id = "m|" + h.StringId });
            }
        }

        /// <summary>Linia doby: turnieje w toku w swiecie i kandydaci do swatow w swiecie (pula, z ktorej moga plynac plotki).</summary>
        internal static string WorldPoolText()
        {
            int tn = 0, mn = 0;
            try
            {
                var tm = Campaign.Current != null ? Campaign.Current.TournamentManager : null;
                if (tm != null) foreach (var t in Town.AllTowns) if (t != null && tm.GetTournamentGame(t) != null) tn++;
                var mm = Marriage();
                if (mm != null && MatchOn)
                    foreach (var c in Clan.All)
                    {
                        if (c == null || c.IsEliminated || c.IsBanditFaction || c == Clan.PlayerClan || c.Heroes == null || c.HomeSettlement == null) continue;
                        foreach (var h in c.Heroes) if (Candidate(h, mm)) mn++;
                    }
            }
            catch (Exception e) { Stumble("WorldPool", e); }
            return "turniejow w toku w swiecie " + tn + ", kandydatow do swatow w swiecie " + (MatchOn ? mn.ToString(Inv) : "- (klucz wylaczony)");
        }

        // ------------------------------------------------------------ cena kufla
        private static ItemObject Beer()
        {
            if (!_beerLooked)
            {
                _beerLooked = true;
                try { _beer = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<ItemObject>("beer") : null; } catch { _beer = null; }
            }
            return _beer;
        }

        /// <summary>Cena beczki piwa dla gracza w miescie (ta sama co w ekranie handlu); 0 - brak.</summary>
        private static int CaskPrice(Settlement town)
        {
            var b = Beer();
            if (b == null || town == null || town.Town == null) return 0;
            try { return Math.Max(0, town.Town.GetItemPrice(b, MobileParty.MainParty, false)); } catch { return 0; }
        }

        /// <summary>Kufel = 1/10 ceny beczki (beczka ok. 20 kufli, karczmarz bierze x2), najmniej 1 d; bez ceny piwa - 2 d.</summary>
        private static int TankardPrice(Settlement town)
        {
            int c = CaskPrice(town);
            return c > 0 ? Math.Max(1, (int)Math.Round(c / 10.0)) : 2;
        }

        private static Settlement RoadTown(RoadsideInns.Inn inn, Vec2 at)
        {
            try
            {
                var v = inn != null && inn.Village != null ? inn.Village.Village : null;
                var tb = v != null ? v.TradeBound : null;
                if (tb != null && tb.Town != null) return tb;
            }
            catch { }
            return NearestTown(at);
        }

        // ------------------------------------------------------------ pula i nowe plotki
        private static List<Item> Pool(Vec2 at, string key)
        {
            long hr = (long)CampaignTime.Now.ToHours;
            if (_pool != null && _poolKey == key && _poolHour == hr) return _pool;
            long t0 = Stopwatch.GetTimestamp();
            List<Item> items;
            try { items = Build(at, key); }
            catch (Exception e) { Stumble("Build", e); items = new List<Item>(); }
            _dBuildTicks += Stopwatch.GetTimestamp() - t0; _dBuilds++;   // recenzja (gra): dzielnik "budowa srednio" - budowy, nie sluchania (warunek opcji tez buduje)
            _pool = items; _poolKey = key; _poolHour = hr;
            return items;
        }

        /// <summary>Skrot do ksiegi uslyszanych: tozsamosc wiesci (Item.Id), bez niej - tekst.</summary>
        private static uint HeardHash(Item it) { return MapVillageData.Fnv(it.Id ?? it.Text ?? ""); }

        private static List<Item> Fresh(List<Item> pool, string key)
        {
            var res = new List<Item>();
            foreach (var it in pool) if (!InnDrink.Heard(key, HeardHash(it))) res.Add(it);
            return res;
        }

        // ------------------------------------------------------------ opcja menu (kufel)
        /// <summary>Warunek opcji przy kluczu InnGossipNeedsDrink: etykieta z cena, opcja szara z powodem (bez pieniedzy, karczmarz nie naleje, nic nowego, brak karczmarza).</summary>
        private static bool DrinkCondition(MenuCallbackArgs args, string var, Vec2 at, string key, Settlement priceTown, string barred)
        {
            int price = TankardPrice(priceTown);
            MBTextManager.SetTextVariable(var, ShortLabels.Pick("Buy a tankard and listen (" + price + " d)", "Buy a tankard of ale and listen to the gossip (" + price + " d)"));
            var me = Hero.MainHero;
            if (barred != null || me == null) { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}" + (barred ?? "Nobody pours ale here now.")); return true; }   // recenzja (gra): zajazd wroga / rabowany - jak nocleg
            var fresh = Fresh(Pool(at, key), key);
            Persuasion.Parts pp;
            int cups = InnDrink.Cups, max = InnDrink.MaxCups(), ch = InnDrink.Talk(priceTown != null ? priceTown.Culture : null, out pp);   // m109-przesluch: miara perswazji (klucz off - Urok)
            if (fresh.Count == 0)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=!}You have heard all the talk here. Come back when there is fresh news.");
                return true;
            }
            if (cups >= max)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=!}The innkeeper will pour you no more - sleep it off first.");
                return true;
            }
            if (me.Gold < price)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=!}A tankard of ale costs " + price + " d and you cannot pay it - nobody talks to a man with a dry tongue.");
                return true;
            }
            int per = InnDrink.PerDrink(ch, fresh.Count);
            var t = new StringBuilder("Nobody talks to a man with a dry tongue: a tankard of ale costs " + price + " d, and the coin goes to the innkeeper. ");
            t.Append("Over it you hear ").Append(per == fresh.Count ? "all " + fresh.Count : per + " of the " + fresh.Count).Append(fresh.Count == 1 ? " piece" : " pieces")
             .Append(" of news you have not heard here yet");
            if (InnDrink.ByPersuasion) t.Append(" (your persuasion ").Append(ch).Append(" - ").Append(Persuasion.Words(pp)).Append(": one piece below 50, two from 50, three from 110, four from 170, everything from 230)");
            else if (InnDrink.ByCharm) t.Append(" (your Charm ").Append(ch).Append(": one piece below 50, two from 50, three from 110, four from 170, everything from 230)");
            t.Append('.');
            if (InnDrink.DrunkOn)
            {
                int th = InnDrink.Threshold();
                t.Append("\nTankards since you last slept: ").Append(cups).Append(" - you get drunk after ").Append(th).Append('.');
                if (InnDrink.Drunk) t.Append(" You are drunk already.");
            }
            args.Tooltip = new TextObject("{=!}" + t.ToString());
            return true;
        }

        /// <summary>Kufel: zaplata (ta sama droga co nocleg), stan pijanstwa, plotki wedlug Uroku z puli bez juz uslyszanych tutaj, okno, linia logu.</summary>
        private static void DrinkConsequence(Vec2 at, string key, string place, bool town, Settlement st, RoadsideInns.Inn inn)
        {
            var me = Hero.MainHero;
            if (me == null) return;
            if (!town)
            {
                string barred = TownInns.RoadDrinkBarred(inn);   // recenzja (gra): te same warunki wojny i rabunku co nocleg w zajezdzie
                if (barred != null) { Log.Player(barred, true); return; }
            }
            Settlement priceTown = town ? st : RoadTown(inn, at);
            int cask = CaskPrice(priceTown), price = TankardPrice(priceTown);
            var pool = Pool(at, key);
            var fresh = Fresh(pool, key);
            if (fresh.Count == 0) { InnDrink.DNothingNew++; Log.Player("You have heard all the talk here - come back when there is fresh news."); return; }
            if (InnDrink.Cups >= InnDrink.MaxCups()) { InnDrink.DRefused++; Log.Player("The innkeeper will pour you no more - sleep it off first.", true); return; }
            if (me.Gold < price) { InnDrink.DNoMoney++; Log.Player("A tankard of ale costs " + price + " d and you cannot pay it.", true); return; }
            int g0 = me.Gold, paid;
            long diff;
            string recip;
            if (town)
            {
                long w0 = TownInns.DrinkWorldTown(st);
                int folk;
                paid = TownInns.DrinkPayTown(st, price, out folk);
                diff = TownInns.DrinkWorldTown(st) - w0;
                InnDrink.DFolk += folk; InnDrink.DCoffer += paid - folk;
                recip = "kiesa ludu " + st.StringId + " " + folk + ", kasa miasta " + (paid - folk);
            }
            else
            {
                var vs = inn != null ? inn.Village : null;
                if (vs == null || vs.Village == null) { Log.Player("Nobody pours ale here now.", true); return; }
                long w0 = TownInns.DrinkWorldRoad(vs);
                int tax, crown;
                paid = TownInns.DrinkPayRoad(vs, price, out tax, out crown);
                diff = TownInns.DrinkWorldRoad(vs) + crown - w0;   // korona DS poza suma posiadaczy - dodana z powrotem (jak nocleg w zajezdzie)
                InnDrink.DVillage += paid - tax - crown; InnDrink.DTax += tax; InnDrink.DCrown += crown;
                recip = "kiesa wsi " + vs.StringId + " " + (paid - tax - crown) + ", podatek pana " + tax + ", korona (DS) " + crown;
            }
            InnDrink.DDiff += diff;
            if (paid <= 0)
            {
                Log.Info(InnDrink.P + "kufel w " + place + " (" + key + ") - zaplata nie przeszla (cena " + price + ", zloto gracza " + g0 + ") - bez plotek.");
                Log.Player("The innkeeper does not take your coin.", true);
                return;
            }
            InnDrink.DCups++; InnDrink.DPaid += paid;
            if (PayNotes.On) PayNotes.Paid("tankard", paid, " for a tankard of ale at " + place + ".");   // m96-wies: komunikat z powodem (klucz wylaczony - komunikat gry)
            string state = InnDrink.OnTankard();
            Persuasion.Parts pp;
            int ch = InnDrink.Talk(priceTown != null ? priceTown.Culture : null, out pp);   // m109-przesluch: miara perswazji wobec ludzi miasta ceny kufla (klucz off - Urok)
            int n = InnDrink.PerDrink(ch, fresh.Count);
            var take = fresh.GetRange(0, n);
            int left = fresh.Count - n;
            // prawdziwosc turniejow i swatow w chwili sluchania (prog sprawdz_logi: nieprawdziwe 0)
            var tm = Campaign.Current != null ? Campaign.Current.TournamentManager : null;
            var mm = Marriage();
            int tn = 0, tTrue = 0, mn = 0, mTrue = 0;
            var refs = new StringBuilder();
            var cats = new int[NewCats];
            foreach (var it in take)
            {
                InnDrink.MarkHeard(key, HeardHash(it));
                if (it.Cat >= 0 && it.Cat < NewCats) cats[it.Cat]++;
                if (it.Cat == CatTourn)
                {
                    tn++;
                    var ts = it.Ref != null ? Settlement.Find(it.Ref) : null;
                    bool ok = ts != null && ts.Town != null && tm != null && tm.GetTournamentGame(ts.Town) != null;
                    if (ok) tTrue++;
                    refs.Append(refs.Length > 0 ? ", " : "").Append("turniej ").Append(it.Ref).Append(ok ? " w toku" : " NIE MA");
                }
                else if (it.Cat == CatMatch)
                {
                    mn++;
                    var h = it.Ref != null ? Hero.Find(it.Ref) : null;
                    bool ok = h != null && h.IsAlive && h.Spouse == null;   // zyje i wolny (model moze chwilowo wykluczyc kogos w armii - to nie falsz plotki)
                    if (ok) mTrue++;
                    refs.Append(refs.Length > 0 ? ", " : "").Append("swaty ").Append(it.Ref).Append(ok ? " wolny" : " NIEWOLNY");
                }
            }
            InnDrink.DItems += n; InnDrink.DTourn += tn; InnDrink.DMatch += mn; InnDrink.DTournFalse += tn - tTrue; InnDrink.DMatchFalse += mn - mTrue;
            try { if (n > 0) me.AddSkillXp(DefaultSkills.Charm, CharmXpPerItem * n); } catch (Exception e) { Stumble("CharmXp", e); }
            if (town) _dHeardTown++; else _dHeardRoad++;
            string head = (town ? "You buy a tankard of ale (" + paid + " d), and the innkeeper leans on his counter. " : "You buy a tankard of ale (" + paid + " d) and sit by the hearth. ")
                          + (n == 1 ? "Over it you hear one piece of news" : "Over it you hear " + n + " pieces of news")
                          + (left > 0 ? " - another round would loosen more tongues (" + left + " more):" : ":");
            Log.Info(InnDrink.P + "kufel w " + place + " (" + (town ? "karczma miasta" : "zajazd") + ", " + key + ") dzien " + Today + " - cena " + price
                     + " (beczka piwa w " + (priceTown != null ? priceTown.StringId : "-") + " " + cask + "), zaplacil " + paid + " (gracz " + g0 + " -> " + me.Gold + ") -> " + recip
                     + " | kontrola obiegu: roznica " + diff
                     + " | Urok " + ch + " -> plotek " + n + " z " + fresh.Count + " nowych (pula " + pool.Count + ", zostalo " + left + ")"
                     + (InnDrink.ByPersuasion ? " (perswazja m109: " + Persuasion.LogText(pp) + ")" : "")
                     + " [bandy " + cats[0] + ", kryjowki " + cats[1] + ", wojska " + cats[2] + ", karawany " + cats[3] + ", ceny " + cats[4] + ", wojny " + cats[5]
                     + ", turnieje " + cats[CatTourn] + ", swaty " + cats[CatMatch] + "]"
                     + " | turnieje " + tn + " (prawdziwe " + tTrue + "), swaty " + mn + " (prawdziwe " + mTrue + ")" + (refs.Length > 0 ? ": " + refs : "")
                     + " | XP Uroku +" + (CharmXpPerItem * n) + " | " + state + ".");
            Show(at, key, place, town, take, head);
        }
    }
}
