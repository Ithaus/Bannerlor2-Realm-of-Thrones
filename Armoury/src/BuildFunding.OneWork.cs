using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// R3-11a (docs/POMYSLY-35-2026-10-20.md nr 34; Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak") - JEDNA BUDOWA NA ROD NARAZ. Klucz BuildOneWorkPerHouse
    /// (wylaczony = stan sprzed zmiany co do monety: przydzial budow rodu dzielony po rowno na osady z budowa).
    /// Dotad (BuildFunding.Daily): pan dawal na budowy przydzial dzienny (ClanBudget.BuildShare, bez budzetu 166 - BuildIncomeShare dochodu) i dzielil go po rowno
    /// na wszystkie swoje lenna z budowa w rozliczeniu doby - kazda szla w zolwim tempie. Historycznie tempo dyktowaly pieniadze, a pan stawial jedna rzecz naraz
    /// (zamki Edwarda I w Walii: kolejno, Harlech 8 190 funtow w 7 lat).
    /// Regula (tylko rody AI z co najmniej 2 budowami w rozliczeniu doby; rod z jedna budowa i rod gracza - jak dotad, bez zmian):
    ///  - caly dzienny przydzial rodu idzie na PIERWSZA budowe kolejki rodu, najwyzej tyle, ile brakuje do konca biezacego poziomu (koszt poziomu - postep,
    ///    przeliczone na pensy ta sama funkcja co punkty: PointsFor, udzial materialu BuildMaterialShare, place wedle miasta targowego; zapas 5% + 10 pensow
    ///    na ceny sztuk - nadwyzka punktow zostaje w budowli jak w grze: Building.LevelUp odejmuje koszt);
    ///  - czego pierwsza budowa nie wziela (poziom zaplacony do konca, brak materialu na targu - wtedy place najwyzej 3 x material, jak dotad), idzie na nastepna
    ///    budowe kolejki; reszta budow czeka (osady "w kolejce rodu" - licznik doby i dopisek w dzienniku budow);
    ///  - KOLEJNOSC: rod w wojnie (krolestwo w wojnie z krolestwem - ta sama definicja co wstrzymanie budowli cywilnych) z budowla wojskowa: najpierw budowla
    ///    wojskowa (mury, wieze, koszary) w warowni NAJBLIZEJ najblizszej wrogiej warowni (miasto albo zamek frakcji, z ktora frakcja rodu jest w wojnie;
    ///    bez wrogiej warowni - jak w pokoju); w pokoju (i w wojnie przy samych budowlach cywilnych dopuszczonych z dala od frontu) budowa NAJBLIZSZA KONCA
    ///    poziomu (najmniej brakujacych punktow). Remis - kolejnosc osad gry.
    /// Zachowane bez zmian: budowle cywilne w wojnie stoja przy froncie i u rodu z budowla wojskowa (WarFront, N5-E), roboty ludu (P2) placa mieszczanie po
    /// budowach pana na swoja budowle, zgliszcza na czele kolejki osady (CitySack.Burn), sufit ceny materialu, drewno ponad zapas rzemiosla (K1).
    /// Zamknieta ekonomia bez zmian: platnik - kiesa pana (lord.ChangeHeroGold), odbiorcy - kasa targu (material, sztuki zdjete z polki) i kasa osady albo kiesa
    /// ludu (place); suma wydana przez rod nie wieksza niz przydzial doby (jak dotad), czego budowy nie wziely - zostaje w kiesie pana.
    /// Stan: tylko doba (bez pol zapisu). Wczytanie w srodku doby: punkty doby odtwarza DayHeld jak dotad, dopisek "w kolejce" w dzienniku wraca od nastepnej doby.
    /// Koszt: raz na dobe; odleglosci liczone tylko dla rodow w wojnie z kilkoma budowami wojskowymi (budowy x 227 warowni).
    /// </summary>
    internal static partial class BuildFunding
    {
        internal static bool OneOn { get { var s = Settings.Current; return s != null && s.BuildOneWorkPerHouse && s.PaidConstruction; } }

        private static readonly Dictionary<Clan, float> _oneLeft = new Dictionary<Clan, float>();          // reszta przydzialu budow rodu tej doby
        private static readonly HashSet<Clan> _oneMulti = new HashSet<Clan>();                            // rody AI z co najmniej 2 budowami w rozliczeniu doby
        private static readonly Dictionary<Settlement, int> _onePos = new Dictionary<Settlement, int>();  // miejsce osady w kolejce rodu (0 - pierwsza)
        private static readonly HashSet<Settlement> _oneWaitSet = new HashSet<Settlement>();              // osady czekajace w kolejce rodu tej doby (dziennik budow)
        private static readonly List<Settlement> _oneForts = new List<Settlement>();
        private static readonly HashSet<string> _oneErr = new HashSet<string>();
        private static int _oneClans, _oneJobs, _oneWarC, _onePeaceC, _oneWait, _oneFirst, _oneNext, _oneLevel, _oneDone, _oneStumbles;
        private static double _onePts;
        private static bool _oneCapNow; private static float _oneNeedNow;

        private static void OneReset()
        {
            _oneLeft.Clear(); _oneMulti.Clear(); _onePos.Clear(); _oneWaitSet.Clear(); _oneForts.Clear(); _oneErr.Clear();
            _oneClans = _oneJobs = _oneWarC = _onePeaceC = _oneWait = _oneFirst = _oneNext = _oneLevel = _oneDone = _oneStumbles = 0; _onePts = 0;
            _oneCapNow = false; _oneNeedNow = 0f;
        }

        private static void OneStumble(string where, Exception e)
        {
            _oneStumbles++;
            try { if (_oneErr.Add(where)) Log.Error("BuildFunding.OneWork." + where, e); } catch { }   // licznik potkniec, blad raz na miejsce (CLAUDE.md 7)
        }

        /// <summary>Dziennik budow (BuildDiary): osada czeka dzis w kolejce rodu (przydzial poszedl na inna budowe rodu).</summary>
        internal static bool OneWaiting(Settlement st) { return st != null && _oneWaitSet.Contains(st); }
        internal static int OneWaitingCount { get { return _oneWaitSet.Count; } }

        /// <summary>
        /// BuildFunding.Daily, po zebraniu budow doby (z dopuszczonymi cywilnymi w wojnie): zeruje stan doby, wybiera rody AI z kilkoma budowami i ustawia ich
        /// budowy w liscie jobs w kolejnosci rodu (pozostale wpisy na swoich miejscach). Klucz wylaczony - tylko zerowanie (lista bez zmian).
        /// </summary>
        internal static void OnePlan(List<KeyValuePair<Settlement, Building>> jobs, Dictionary<Clan, int> count)
        {
            _oneLeft.Clear(); _oneMulti.Clear(); _onePos.Clear(); _oneWaitSet.Clear();
            _oneClans = _oneJobs = _oneWarC = _onePeaceC = _oneWait = _oneFirst = _oneNext = _oneLevel = _oneDone = 0; _onePts = 0;
            if (!OneOn || jobs == null || count == null) return;
            try
            {
                foreach (var kv in count)
                    if (kv.Key != null && kv.Key != Clan.PlayerClan && kv.Value >= 2) _oneMulti.Add(kv.Key);
                _oneClans = _oneMulti.Count;
                if (_oneMulti.Count == 0) return;
                var byClan = new Dictionary<Clan, List<KeyValuePair<double, int>>>();
                var war = new Dictionary<Clan, bool>();
                for (int i = 0; i < jobs.Count; i++)
                {
                    var c = jobs[i].Key.OwnerClan;
                    if (!_oneMulti.Contains(c)) continue;
                    List<KeyValuePair<double, int>> l;
                    if (!byClan.TryGetValue(c, out l)) { l = new List<KeyValuePair<double, int>>(); byClan[c] = l; war[c] = OneAtWar(c); }
                    l.Add(new KeyValuePair<double, int>(0.0, i));
                }
                foreach (var e in byClan)
                {
                    var c = e.Key; var l = e.Value;
                    bool warRule = false;
                    if (war[c]) foreach (var p in l) if (jobs[p.Value].Value.BuildingType.IsMilitaryProject) { warRule = true; break; }
                    if (warRule) _oneWarC++; else _onePeaceC++;
                    for (int k = 0; k < l.Count; k++)
                    {
                        var st = jobs[l[k].Value].Key; var b = jobs[l[k].Value].Value;
                        double key;
                        try
                        {
                            double rem = Math.Max(0.0, b.GetConstructionCost() - b.BuildingProgress);
                            if (warRule && b.BuildingType.IsMilitaryProject)
                            {
                                double d2 = OneEnemyDist2(c, st);
                                key = d2 >= 0 ? d2 : 1e12 + rem;   // warownia bez wrogiej warowni na mapie - za tymi przy wrogu, wedlug konca poziomu
                            }
                            else key = (warRule ? 2e12 : 0.0) + rem;   // w wojnie cywilna (gdyby byla) za wojskowymi; w pokoju - najblizsza konca
                        }
                        catch (Exception ex) { OneStumble("OnePlan(klucz)", ex); key = 3e12; }
                        l[k] = new KeyValuePair<double, int>(key, l[k].Value);
                    }
                    l.Sort((x, y) => { int r = x.Key.CompareTo(y.Key); return r != 0 ? r : x.Value.CompareTo(y.Value); });
                }
                var outL = new List<KeyValuePair<Settlement, Building>>(jobs.Count);
                var done = new HashSet<Clan>();
                for (int i = 0; i < jobs.Count; i++)
                {
                    var c = jobs[i].Key.OwnerClan;
                    if (!_oneMulti.Contains(c)) { outL.Add(jobs[i]); continue; }
                    if (!done.Add(c)) continue;
                    var l = byClan[c];
                    for (int k = 0; k < l.Count; k++) { var kv = jobs[l[k].Value]; _onePos[kv.Key] = k; outL.Add(kv); _oneJobs++; }
                }
                jobs.Clear(); jobs.AddRange(outL);
            }
            catch (Exception e) { OneStumble("OnePlan", e); }
        }

        /// <summary>Krolestwo rodu w wojnie z jakims krolestwem - ta sama definicja co wstrzymanie budowli cywilnych w BuildFunding.Daily.</summary>
        private static bool OneAtWar(Clan c)
        {
            var k = c != null ? c.Kingdom : null;
            if (k == null) return false;
            try { foreach (var o in Kingdom.All) if (o != k && !o.IsEliminated && k.IsAtWarWith(o)) return true; }
            catch (Exception e) { OneStumble("OneAtWar", e); }
            return false;
        }

        /// <summary>Kwadrat odleglosci warowni do najblizszej warowni (miasto, zamek) frakcji w wojnie z frakcja rodu; -1 - brak takiej warowni.</summary>
        private static double OneEnemyDist2(Clan c, Settlement st)
        {
            var mf = c.MapFaction;
            if (mf == null || st == null) return -1;
            if (_oneForts.Count == 0)
                foreach (var f in Settlement.All) if (f != null && f.Town != null && (f.IsTown || f.IsCastle)) _oneForts.Add(f);
            var p = st.GetPosition2D;
            double best = -1;
            for (int i = 0; i < _oneForts.Count; i++)
            {
                var f = _oneForts[i];
                var ff = f.MapFaction;
                if (f == st || ff == null || ff == mf || !mf.IsAtWarWith(ff)) continue;
                double d = p.DistanceSquared(f.GetPosition2D);
                if (best < 0 || d < best) best = d;
            }
            return best;
        }

        /// <summary>
        /// BuildFunding.Daily, przed ograniczeniem kiesa pana: budzet tej budowy. Rod spoza regul (jedna budowa, gracz) - equal (podzial po rowno, jak dotad).
        /// Rod z kolejka: reszta przydzialu doby (all na starcie), najwyzej brak do konca poziomu; reszta ponizej 1 - osada czeka w kolejce (0).
        /// </summary>
        internal static float OneBudget(Settlement st, Building b, Clan clan, float all, float equal, float ppp, Settlement market)
        {
            _oneCapNow = false; _oneNeedNow = 0f;
            if (clan == null || !_oneMulti.Contains(clan)) return equal;
            try
            {
                float left;
                if (!_oneLeft.TryGetValue(clan, out left)) { left = Math.Max(0f, all); _oneLeft[clan] = left; }
                if (left < 1f) { _oneWait++; _oneWaitSet.Add(st); return 0f; }
                float need = OneNeed(st, b, ppp, market);
                if (need <= 0f) { _oneDone++; return 0f; }   // poziom juz zaplacony (czeka na tick osady) - przydzial idzie dalej
                if (need <= left) { _oneCapNow = true; _oneNeedNow = need; return need; }
                return left;
            }
            catch (Exception e) { OneStumble("OneBudget", e); return equal; }
        }

        /// <summary>Pensy potrzebne do konca biezacego poziomu przy pelnym budzecie (material BuildMaterialShare, place wedle miasta targowego - PointsFor) z zapasem
        /// 5% + 10 pensow. Koszt poziomu 0 (poziom 3 - BuildingType.GetProductionCost zwraca 0; budowla czeka na zdjecie z kolejki w ticku osady) - 0, nic do zaplaty.</summary>
        private static float OneNeed(Settlement st, Building b, float ppp, Settlement market)
        {
            int cost = b.GetConstructionCost();
            if (cost <= 0) return 0f;
            double rem = cost - b.BuildingProgress - Boost(st.Town);
            if (rem <= 0) return 0f;
            var s = Settings.Current;
            double ms = MBMath.ClampFloat(s.BuildMaterialShare, 0f, 1f);
            double per = (ms + (1.0 - ms) / WageIdx(market)) / Math.Max(1f, ppp);   // punkty na pens
            if (!(per > 0)) return float.MaxValue;
            double need = rem / per * 1.05 + 10.0;
            return need > float.MaxValue / 2 ? float.MaxValue : (float)need;
        }

        /// <summary>BuildFunding.Daily, po zaplacie: odejmuje wydane od reszty przydzialu rodu i liczy oplacone budowy (pierwsza z kolejki / dalej z reszty).</summary>
        internal static void OnePaid(Settlement st, Clan clan, int spent, float pts)
        {
            if (clan == null || !_oneMulti.Contains(clan)) return;
            try
            {
                float left; _oneLeft.TryGetValue(clan, out left);
                _oneLeft[clan] = left - Math.Max(0, spent);
                int pos; _onePos.TryGetValue(st, out pos);
                if (pos == 0) _oneFirst++; else _oneNext++;
                _onePts += pts;
                if (_oneCapNow && spent >= _oneNeedNow * 0.95f) _oneLevel++;
            }
            catch (Exception e) { OneStumble("OnePaid", e); }
            _oneCapNow = false;
        }

        /// <summary>Dopisek linii "Budowy oplacone" (log_grep: "R3-11a jedna budowa na rod").</summary>
        internal static string OneTail()
        {
            try
            {
                if (!OneOn) return " | R3-11a jedna budowa na rod naraz: WYLACZONE (Build One Work Per House) - przydzial dzielony po rowno na osady z budowa, jak dotad.";
                var inv = CultureInfo.InvariantCulture;
                int funded = _oneFirst + _oneNext;
                var sb = new StringBuilder(400);
                sb.Append(" | R3-11a jedna budowa na rod naraz (Build One Work Per House tak): rody AI z kilkoma budowami ").Append(_oneClans)
                  .Append(" (budow ").Append(_oneJobs).Append("; kolejnosc - w wojnie mur najblizej wroga ").Append(_oneWarC).Append(" rodow, najblizsza konca ").Append(_onePeaceC)
                  .Append("); oplacone budowy tych rodow ").Append(funded).Append(" (pierwsza z kolejki ").Append(_oneFirst).Append(", dalej z reszty przydzialu ").Append(_oneNext)
                  .Append("), punktow ").Append(((long)_onePts).ToString(inv)).Append(" (srednio ").Append(funded > 0 ? (_onePts / funded).ToString("0.0", inv) : "-")
                  .Append(" na budowe), poziom zaplacony do konca ").Append(_oneLevel).Append(", poziom juz zaplacony ").Append(_oneDone)
                  .Append("; czeka w kolejce rodu ").Append(_oneWait).Append(" osad; potkniecia ").Append(_oneStumbles).Append('.');
                return sb.ToString();
            }
            catch (Exception e) { OneStumble("OneTail", e); return " | R3-11a jedna budowa na rod naraz: blad linii."; }
        }
    }
}
