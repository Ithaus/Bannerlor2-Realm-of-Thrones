using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// E4/10 (167, PLAN 4.14; plan kodu docs/etap4/PLAN-KODU-E410.md): KONTO WETERANI ksiegi ludzi - pula karczm. Zolnierz, ktory odchodzi zywy ze sluzby do domu
    /// (zwolniony 166, przepelnienie W4c, rycerz 179, rozbity H3, uwolniony albo wypuszczony jeniec, rozwiazana partia - R16), od tieru VeteranMinTier, w dokladnie
    /// VeteranShareToTavern (1/3) przypadkow (akumulator ulamka na strumien - bez losowania) staje w karczmie najblizszego miasta; reszta wraca do domu jak dotad.
    /// Pula = miasto x klasa (konny tak/nie, tier): ludzie, udzialy pochodzenia (region -> ludzie, "" = bez pochodzenia) i kolejka FIFO (doba wejscia, ludzi).
    /// Karczma pokazuje tylko weteranow (Veterans.cs: gra nie dosypuje najemnikow z niczego); najem zdejmuje z kolejki pokazanej klasy (FIFO) i niesie udzialy
    /// do skladu partii (Give FVetHire). Niewynajety po VeteranWaitDays (120) wraca do DOM regionu pochodzenia (HomeKnown, MBackVeteran - z hearth jak MBack*).
    /// Start: to, co stoi w karczmach w dniu siewu (po siewie E4/7), zdjete ze stanu ludzi miasta (E4/4, MVetStart). Stan tylko w "arm_people" (vs: vt: vh:).
    /// Kazde cialo we wlasnym try, potkniecia liczone (U24); dopisanie do puli jest ostatnim krokiem wejscia. Zloto - bez zmian (P 5.2 pkt 4).
    /// </summary>
    internal static partial class PeopleBook
    {
        // ================================================================ strumienie wejscia (indeksy akumulatorow i licznikow)
        internal const int V166 = 0, VW4c = 1, V179 = 2, VH3 = 3, VFreed = 4, VLaw = 5, VLootFree = 6, VDisband = 7, VSrcs = 8;
        private static readonly string[] VSrcName = { "zwolnieni 166", "przepelnienie W4c", "rycerze 179", "rozbici H3", "uwolnieni jency", "prawo jenca I1",
                                                      "zostawieni po bitwie gracza", "rozwiazane partie" };
        /// <summary>Strumien, w ktorym zolnierz odchodzi z koniem (jeniec konia nie ma - zabral go zwyciezca, RecruitKit Z4).</summary>
        private static bool VSrcHorse(int v) { return v == V166 || v == VW4c || v == V179 || v == VH3 || v == VDisband; }

        private const int VKeyHorse = 100;      // klucz klasy: (konny ? 100 : 0) + tier
        private static int VKey(bool horse, int tier) { return (horse ? VKeyHorse : 0) + Math.Max(0, Math.Min(VKeyHorse - 1, tier)); }
        private static bool VKeyIsHorse(int key) { return key >= VKeyHorse; }
        private static int VKeyTier(int key) { return key % VKeyHorse; }

        private sealed class VetClass                       // jedna klasa w jednym miescie
        {
            public int Men;                                 // ludzie (calkowite - czlowiek w karczmie jest jeden)
            public readonly Dictionary<string, double> Org = new Dictionary<string, double>(4);   // region pochodzenia -> udzial ("" = bez pochodzenia); suma = Men
            public readonly List<int> Day = new List<int>(), N = new List<int>();                 // kolejka FIFO: doba wejscia, ludzi (suma N = Men)
        }

        // ================================================================ stan w zapisie
        private static readonly Dictionary<string, Dictionary<int, VetClass>> _vet = new Dictionary<string, Dictionary<int, VetClass>>();   // id miasta -> klasa
        private static readonly Dictionary<string, int> _vetShown = new Dictionary<string, int>();     // id miasta -> klucz klasy pokazanej w karczmie
        private static bool _vSown;
        private static int _vSowDay = -1;
        private static readonly double[] _vAcc = new double[VSrcs];                                     // akumulator udzialu do karczmy wedlug strumienia

        // ================================================================ sesja i liczniki doby (bez zapisu)
        /// <summary>Latki karczmy wpiete (Veterans.ApplyAll: L1 bez dosypki i L2 okno najmu AI) - bez nich paczka nieczynna (karczma jak w grze).</summary>
        internal static bool VetWired;
        private static bool _vLoaded;                       // wczytano vs: - linia wczytania przy pierwszej dobie sesji
        private static string _vLoadNote, _vSowNote;
        private static double _vPool0;                      // pula z konca poprzedniej doby (albo z zapisu) - kontrola
        private static readonly int[] _vIn = new int[VSrcs], _vElig = new int[VSrcs], _vBelow = new int[VSrcs], _vNoTown = new int[VSrcs];
        private static int _vInHorse, _vSeedMen, _vSeedTowns, _vBkMiss;
        private static int _vHired, _vHireLord, _vHireMercClan, _vHireCaravan, _vHirePlayer, _vHireOther, _vHireHorse, _vTierUp, _vTierDown, _vNoWin, _vOver, _vForeign;
        private static int _vBackMen, _vBackOthers, _vBackAband, _vBackDue;
        private static double _vBackKnown, _vBackNone, _vBackTown, _vBackNoVillage;
        private static int _vSiegeMen, _vSiegeTowns, _vSiegeOldest;
        internal static int VShownTowns, VShownHorse, VNoType, VFootForHorse, VClearTowns;   // synchronizacja karczm (Veterans.Sync)
        private static int _vStumbles, _vStumblesDay, _vBadIds;
        private static double _vMs;

        /// <summary>Paczka czynna: po siewie, latki wpiete, VeteransEnabled. Inaczej wejscia ida do domu, karczma jak w grze, najem nie zdejmuje z puli
        /// (powroty trwaja, az pula sie oprozni).</summary>
        internal static bool VetLive { get { var s = Settings.Current; return _vSown && VetWired && s != null && s.VeteransEnabled; } }
        internal static bool VetSown { get { return _vSown; } }

        private static void VStumble(string where, Exception e) { _vStumbles++; _vStumblesDay++; Stumble("E4/10." + where, e); }
        internal static void VetStumbleExt(string where, Exception e) { VStumble(where, e); }     // Veterans.cs (latki, synchronizacja, typy)

        private static bool VBad(string id)
        {
            if (string.IsNullOrEmpty(id)) return true;
            foreach (char ch in id) if (ch == '|' || ch == ',' || ch == ';' || ch == '=' || ch == ':' || ch == '#' || ch == '@' || ch == '~') return true;
            return false;
        }

        // ================================================================ reset / zapis
        private static void ResetVeterans()
        {
            _vet.Clear(); _vetShown.Clear(); _vSown = false; _vSowDay = -1; Array.Clear(_vAcc, 0, VSrcs);
            _vLoaded = false; _vLoadNote = null; _vSowNote = null; _vPool0 = 0; _vStumbles = 0; _vBadIds = 0;
            VetNewDay();
            try { Veterans.ResetSession(); } catch (Exception e) { VStumble("ResetVeterans(Veterans)", e); }
        }

        private static void VetNewDay()
        {
            Array.Clear(_vIn, 0, VSrcs); Array.Clear(_vElig, 0, VSrcs); Array.Clear(_vBelow, 0, VSrcs); Array.Clear(_vNoTown, 0, VSrcs);
            _vInHorse = _vSeedMen = _vSeedTowns = _vBkMiss = 0;
            _vHired = _vHireLord = _vHireMercClan = _vHireCaravan = _vHirePlayer = _vHireOther = _vHireHorse = _vTierUp = _vTierDown = _vNoWin = _vOver = _vForeign = 0;
            _vBackMen = _vBackOthers = _vBackAband = _vBackDue = 0; _vBackKnown = _vBackNone = _vBackTown = _vBackNoVillage = 0;
            _vSiegeMen = _vSiegeTowns = 0; _vSiegeOldest = 0;
            VShownTowns = VShownHorse = VNoType = VFootForHorse = VClearTowns = 0;
            _vStumblesDay = 0; _vMs = 0; _vSowNote = null;
        }

        private static void ExportVeterans(StringBuilder sb)
        {
            if (!_vSown) return;
            int len0 = sb.Length;
            sb.Append("|vs:").Append(_vSowDay.ToString(Inv));
            for (int i = 0; i < VSrcs; i++) sb.Append(';').Append(_vAcc[i].ToString("0.######", Inv));
            bool first = true; int classes = 0, entries = 0;
            foreach (var tw in _vet)
            {
                if (VBad(tw.Key)) { _vBadIds++; continue; }
                foreach (var kc in tw.Value)
                {
                    var c = kc.Value;
                    if (c.Men <= 0 || c.Day.Count == 0) continue;
                    sb.Append(first ? "|vt:" : ",").Append(tw.Key).Append('#').Append(kc.Key.ToString(Inv)).Append('=');
                    first = false; classes++;
                    bool f2 = true;
                    foreach (var o in c.Org)
                    {
                        if (!(o.Value > Eps)) continue;
                        string k = o.Key.Length == 0 ? "~" : o.Key;
                        if (k != "~" && VBad(k)) { _vBadIds++; continue; }      // udzial bez zapisu - po wczytaniu jako "" (normalizacja do Men)
                        sb.Append(f2 ? "" : ";").Append(k).Append(':').Append(o.Value.ToString("0.###", Inv));
                        f2 = false;
                    }
                    sb.Append('@');
                    for (int i = 0; i < c.Day.Count; i++) { sb.Append(i > 0 ? ";" : "").Append(c.Day[i].ToString(Inv)).Append(':').Append(c.N[i].ToString(Inv)); entries++; }
                }
            }
            first = true;
            foreach (var kv in _vetShown)
            {
                if (VBad(kv.Key)) continue;
                sb.Append(first ? "|vh:" : ",").Append(kv.Key).Append('=').Append(kv.Value.ToString(Inv));
                first = false;
            }
            try
            {
                Log.Info("Weterani (E4/10): zapis arm_people - pula " + VetWorld + " ludzi w " + _vet.Count + " miastach (klas " + classes + ", wpisow kolejek " + entries + ", pokazanych "
                         + _vetShown.Count + "); sekcje E4/10 " + (sb.Length - len0) + " znakow; nie zapisane (znaki w id) " + _vBadIds + ".");
            }
            catch { }
        }

        /// <summary>Import: sekcja E4/10 ("vs:", "vt:", "vh:"); false = nie nasza.</summary>
        private static bool ImportVeteransPart(string part)
        {
            string p2 = part.Substring(0, 3);
            if (p2 != "vs:" && p2 != "vt:" && p2 != "vh:") return false;
            try
            {
                string body = part.Substring(3);
                if (p2 == "vs:")
                {
                    var a = body.Split(';');
                    int d;
                    if (a.Length > 0 && int.TryParse(a[0], NumberStyles.Integer, Inv, out d)) { _vSown = true; _vSowDay = d; _vLoaded = true; }
                    for (int i = 0; i < VSrcs; i++) _vAcc[i] = Math.Max(0.0, Math.Min(0.999999, Num(a, i + 1)));
                }
                else if (p2 == "vt:")
                {
                    foreach (var item in body.Split(','))
                    {
                        try
                        {
                            int hash = item.IndexOf('#'), eq = item.IndexOf('=');
                            if (hash <= 0 || eq <= hash + 1) continue;
                            string town = item.Substring(0, hash);
                            int key; if (!int.TryParse(item.Substring(hash + 1, eq - hash - 1), NumberStyles.Integer, Inv, out key) || key < 0) continue;
                            string rest = item.Substring(eq + 1);
                            int at = rest.IndexOf('@');
                            string orgS = at >= 0 ? rest.Substring(0, at) : rest, qS = at >= 0 ? rest.Substring(at + 1) : "";
                            var q = new List<KeyValuePair<int, int>>();
                            foreach (var e in qS.Split(';'))
                            {
                                int c = e.IndexOf(':');
                                if (c <= 0) continue;
                                int dd, nn;
                                if (!int.TryParse(e.Substring(0, c), NumberStyles.Integer, Inv, out dd) || !int.TryParse(e.Substring(c + 1), NumberStyles.Integer, Inv, out nn) || nn <= 0) continue;
                                q.Add(new KeyValuePair<int, int>(dd, nn));
                            }
                            if (q.Count == 0) continue;
                            q.Sort((x, y) => x.Key.CompareTo(y.Key));
                            Dictionary<int, VetClass> cl;
                            if (!_vet.TryGetValue(town, out cl)) { cl = new Dictionary<int, VetClass>(); _vet[town] = cl; }
                            VetClass vc;
                            if (!cl.TryGetValue(key, out vc)) { vc = new VetClass(); cl[key] = vc; }
                            int men = 0;
                            foreach (var kv in q) { VQueueAdd(vc, kv.Key, kv.Value); men += kv.Value; }
                            vc.Men += men;
                            var org = new Dictionary<string, double>();
                            foreach (var o in orgS.Split(';'))
                            {
                                int c = o.LastIndexOf(':');
                                if (c <= 0) continue;
                                double v; if (!double.TryParse(o.Substring(c + 1), NumberStyles.Float, Inv, out v) || !(v > 0.0) || double.IsInfinity(v)) continue;
                                string k = o.Substring(0, c); if (k == "~") k = "";
                                double old; org.TryGetValue(k, out old); org[k] = old + v;
                            }
                            VNormalize(org, men);
                            foreach (var kv in org) { double old; vc.Org.TryGetValue(kv.Key, out old); vc.Org[kv.Key] = old + kv.Value; }
                        }
                        catch (Exception e) { VStumble("Import(vt: wpis)", e); }
                    }
                }
                else
                {
                    foreach (var item in body.Split(','))
                    {
                        int eq = item.IndexOf('=');
                        if (eq <= 0) continue;
                        int key; if (int.TryParse(item.Substring(eq + 1), NumberStyles.Integer, Inv, out key)) _vetShown[item.Substring(0, eq)] = key;
                    }
                }
            }
            catch (Exception e) { VStumble("ImportVeteransPart(" + p2 + ")", e); }
            return true;
        }

        /// <summary>Udzialy z zapisu do liczby ludzi klasy: ponad - proporcjonalnie w dol; brakuje (zaokraglenie, udzial bez zapisu) - roznica jako "".</summary>
        private static void VNormalize(Dictionary<string, double> org, int men)
        {
            double sum = 0; foreach (var v in org.Values) sum += v;
            if (sum > men + 1e-6 && sum > 0)
            {
                double f = men / sum;
                foreach (var k in org.Keys.ToList()) org[k] = org[k] * f;
            }
            else if (sum < men - 1e-6)
            {
                double old; org.TryGetValue("", out old); org[""] = old + (men - sum);
            }
        }

        /// <summary>Import: pula z zapisu - stan do kontroli pierwszej doby i linia wczytania.</summary>
        private static void VetAfterImport()
        {
            _vPool0 = VetWorld;
            if (!_vSown) return;
            int classes = 0, entries = 0;
            foreach (var cl in _vet.Values) foreach (var c in cl.Values) { classes++; entries += c.Day.Count; }
            _vLoadNote = "z zapisu " + VetWorld + " ludzi w " + _vet.Count + " miastach (klas " + classes + ", wpisow kolejek " + entries + "), pokazanych " + _vetShown.Count + ", siew dnia " + _vSowDay;
        }

        // ================================================================ pomocnicze klasy
        private static void VQueueAdd(VetClass c, int day, int n)
        {
            int last = c.Day.Count - 1;
            if (last >= 0 && c.Day[last] == day) c.N[last] += n;
            else if (last >= 0 && c.Day[last] > day)
            {
                // wejscie ze starsza doba niz ostatni wpis (tylko import) - wstaw na miejsce (FIFO po dobie)
                int i = 0; while (i < c.Day.Count && c.Day[i] < day) i++;
                if (i < c.Day.Count && c.Day[i] == day) c.N[i] += n; else { c.Day.Insert(i, day); c.N.Insert(i, n); }
            }
            else { c.Day.Add(day); c.N.Add(n); }
        }

        /// <summary>Zdejmuje z kolejki najstarszych (FIFO), najwyzej n; zmniejsza Men. Zwraca, ilu zdjeto.</summary>
        private static int VPopOldest(VetClass c, int n)
        {
            int got = 0;
            while (got < n && c.Day.Count > 0)
            {
                int k = Math.Min(c.N[0], n - got);
                c.N[0] -= k; got += k;
                if (c.N[0] <= 0) { c.N.RemoveAt(0); c.Day.RemoveAt(0); }
            }
            c.Men = Math.Max(0, c.Men - got);
            return got;
        }

        /// <summary>Wycina n ludzi proporcjonalnie z udzialow klasy (zdejmuje z Org); brak - reszta jako "".</summary>
        private static Shares VCut(VetClass c, int n)
        {
            var r = new Shares();
            if (n <= 0) return r;
            double sum = 0; foreach (var v in c.Org.Values) sum += v;
            if (sum <= Eps) { r.Add("", n); c.Org.Clear(); return r; }
            double take = Math.Min(n, sum), f = take / sum;
            if (f >= 1.0 - 1e-12) { foreach (var kv in c.Org) r.Add(kv.Key, kv.Value); c.Org.Clear(); }
            else
            {
                foreach (var k in c.Org.Keys.ToList())
                {
                    double m = c.Org[k] * f;
                    r.Add(k, m);
                    double left = c.Org[k] - m;
                    if (left > Eps) c.Org[k] = left; else c.Org.Remove(k);
                }
            }
            if (n - take > Eps) r.Add("", n - take);
            return r;
        }

        /// <summary>Dopisanie t ludzi do klasy miasta (udzialy part, suma = t) z doba wejscia. Ostatni krok wejscia (U24).</summary>
        private static void VAdd(string townId, int key, Shares part, int t, int today)
        {
            Dictionary<int, VetClass> cl;
            if (!_vet.TryGetValue(townId, out cl)) { cl = new Dictionary<int, VetClass>(); _vet[townId] = cl; }
            VetClass c;
            if (!cl.TryGetValue(key, out c)) { c = new VetClass(); cl[key] = c; }
            double added = 0;
            if (part != null) foreach (var kv in part.M) { if (!(kv.Value > 0.0)) continue; double old; c.Org.TryGetValue(kv.Key, out old); c.Org[kv.Key] = old + kv.Value; added += kv.Value; }
            if (t - added > Eps) { double old; c.Org.TryGetValue("", out old); c.Org[""] = old + (t - added); }
            VQueueAdd(c, today, t);
            c.Men += t;
        }

        private static void VDropEmpty(string townId)
        {
            Dictionary<int, VetClass> cl;
            if (!_vet.TryGetValue(townId, out cl)) return;
            foreach (var k in cl.Keys.ToList()) { var c = cl[k]; if (c.Men <= 0 || c.Day.Count == 0) cl.Remove(k); }
            if (cl.Count == 0) _vet.Remove(townId);
        }

        // ================================================================ wejscie do puli (R16) - jedna regula
        internal const int VROk = 0, VRBelow = 1, VRNoTown = 2;
        /// <summary>Plan wejscia (bez zapisu stanu): miasto, klasa, t z akumulatora. Commit zapisuje (W1 - po BK wsi, wszystko-albo-nic).</summary>
        internal sealed class VetTicket { internal int Reason, N, T, Src, Key; internal Town Town; internal CharacterObject Troop; internal bool Horse; internal double Acc; }

        /// <summary>Uprawniony do karczmy (krok 2 planu 3.1): szeregowy zolnierz albo najemnik (bez wightow i Strazy), partia nie milicja/tabor/karawana/Straz/Inni.</summary>
        private static bool VetEligible(MobileParty party, CharacterObject troop)
        {
            if (troop == null || troop.IsHero) return false;
            var occ = troop.Occupation;
            if (occ != Occupation.Soldier && occ != Occupation.Mercenary) return false;
            if (IsWight(troop)) return false;
            if (troop.Culture != null && troop.Culture.StringId == "nightswatch") return false;      // Straz dozywotnio (R14)
            if (party != null)
            {
                if (party.IsMilitia || party.IsVillager || party.IsCaravan) return false;
                if (CrownGifts.IsWatchParty(party) || Undead.Party(party)) return false;
            }
            return true;
        }

        /// <summary>Plan (kroki 1-5): null = nieuprawniony albo paczka nieczynna (bez licznika i bez akumulatora); Reason - ponizej tieru / bez miasta (bez akumulatora).</summary>
        internal static VetTicket VetPlan(MobileParty party, CharacterObject troop, int n, Vec2 from, int vsrc)
        {
            if (n <= 0 || vsrc < 0 || vsrc >= VSrcs || !VetLive) return null;
            try
            {
                if (!VetEligible(party, troop)) return null;
                var s = Settings.Current;
                int tier = Veterans.TierOf(troop);
                bool horse = troop.IsMounted && VSrcHorse(vsrc);
                if (tier < s.VeteranMinTier) return new VetTicket { Reason = VRBelow, N = n, Src = vsrc };
                var town = Veterans.TownFor(from, horse, tier);
                if (town == null) return new VetTicket { Reason = VRNoTown, N = n, Src = vsrc };
                double share = Math.Max(0.0, Math.Min(1.0, (double)s.VeteranShareToTavern));
                double x = _vAcc[vsrc] + n * share;
                int t = (int)Math.Floor(x + 1e-9);
                if (t < 0) t = 0;
                if (t > n) t = n;
                double acc = x - t;
                if (acc < 0) acc = 0;
                if (acc > 0.999999) acc = 0.999999;
                return new VetTicket { Reason = VROk, N = n, T = t, Src = vsrc, Key = VKey(horse, tier), Town = town, Troop = troop, Horse = horse, Acc = acc };
            }
            catch (Exception e) { VStumble("VetPlan", e); return null; }
        }

        /// <summary>
        /// Zapis planu (kroki 5-6): akumulator, BK miasta (sklad - R0; brak danych BK - licznik), udzial `org` pomniejszony o t (null = "" x t), dopisanie do klasy jako ostatni
        /// krok. Zwraca t (0 = wszyscy dalej do domu); przy wyjatku 0 (wolajacy wysyla wszystkich do domu). Nigdy nie rzuca.
        /// </summary>
        internal static int VetCommit(VetTicket tk, Shares org)
        {
            if (tk == null) return 0;
            try
            {
                if (tk.Reason == VRBelow) { _vBelow[tk.Src] += tk.N; return 0; }
                if (tk.Reason == VRNoTown) { _vNoTown[tk.Src] += tk.N; return 0; }
                _vAcc[tk.Src] = tk.Acc;
                _vElig[tk.Src] += tk.N;
                if (tk.T <= 0 || tk.Town == null || tk.Town.Settlement == null || tk.Town.Settlement.StringId == null) return 0;
                int t = tk.T;
                // ludnosc BK miasta zamiast wsi (sklad, nie ludzie - R0); najem z karczmy AI BK zdejmuje sam (DeduceManpower w ApplyInternal)
                bool bk = false;
                try { bk = ClanBudget.BkPopAdd(tk.Town.Settlement, tk.Troop, t); } catch { bk = false; }
                if (!bk) _vBkMiss += t;
                Shares part = org != null ? org.Cut(t) : Shares.None(t);
                VAdd(tk.Town.Settlement.StringId, tk.Key, part, t, (int)CampaignTime.Now.ToDays);
                try
                {
                    _vIn[tk.Src] += t;
                    if (tk.Horse) _vInHorse += t;
                    if (org != null && OriginLive) _vDestTake += t;          // E4/7 "dokad": wyjscie ze SLUZBY (Take wolajacego) do WETERANI
                }
                catch { }
                return t;
            }
            catch (Exception e) { VStumble("VetCommit", e); return 0; }
        }

        /// <summary>Jedna droga wejscia (plan + zapis) dla H3, uwolnionych, I1 i rozwiazanych partii. Zwraca t - wolajacy wysyla n - t dalej jak dotad (z pomniejszonym org).</summary>
        internal static int VetDivert(MobileParty party, CharacterObject troop, int n, Vec2 from, int vsrc, Shares org)
        {
            if (n <= 0 || !VetLive) return 0;
            return VetCommit(VetPlan(party, troop, n, from, vsrc), org);
        }

        /// <summary>DisbandPre (E4/7): sklad rozwiazywanej partii wedlug typu (szeregowi bez wightow) - tylko przy czynnej paczce.</summary>
        private static List<KeyValuePair<CharacterObject, int>> VetTypes(TroopRoster r)
        {
            if (r == null || !VetLive) return null;
            try
            {
                var l = new List<KeyValuePair<CharacterObject, int>>();
                for (int i = 0; i < r.Count; i++)
                {
                    var c = r.GetCharacterAtIndex(i);
                    if (c == null || c.IsHero || IsWight(c)) continue;
                    int k = r.GetElementNumber(i);
                    if (k > 0) l.Add(new KeyValuePair<CharacterObject, int>(c, k));
                }
                return l;
            }
            catch (Exception e) { VStumble("VetTypes", e); return null; }
        }

        /// <summary>W5: rozwiazana partia (DisbandPost E4/7) - `gone` z `men` odchodzi (normalnie wszyscy albo nikt); dzieli proporcjonalnie na typy i wola VetDivert
        /// dla kazdego typu. Zwraca, ilu poszlo do karczm (org pomniejszony o nich).</summary>
        private static double VetDisband(MobileParty party, List<KeyValuePair<CharacterObject, int>> types, int men, double gone, Shares org)
        {
            if (types == null || types.Count == 0 || men <= 0 || !(gone > Eps) || !VetLive) return 0;
            int want = (int)Math.Floor(gone + 1e-6);
            if (want <= 0) return 0;
            var part = new int[types.Count];
            int sum = 0, big = 0;
            for (int i = 0; i < types.Count; i++)
            {
                part[i] = want >= men ? types[i].Value : (int)Math.Floor((double)types[i].Value * want / men);
                sum += part[i];
                if (types[i].Value > types[big].Value) big = i;
            }
            if (sum < want) part[big] += Math.Min(want - sum, types[big].Value - part[big]);
            Vec2 pos;
            try { pos = party.GetPosition2D; } catch { return 0; }
            double t = 0;
            for (int i = 0; i < types.Count; i++)
                if (part[i] > 0) t += VetDivert(party, types[i].Key, part[i], pos, VDisband, org);
            return t;
        }

        // ================================================================ najem (Veterans.OnTavernNumber)
        /// <summary>Zdejmuje n z kolejki klasy pokazanej (FIFO, od najstarszych), przy braku - z innych klas tego miasta (od najstarszych), dalej "" (licznik
        /// "najem ponad pule", oczekiwane 0). Zwraca udzialy; classKey - klucz klasy pokazanej (-1 = brak).</summary>
        internal static Shares VetHire(Town town, int n, out int classKey)
        {
            classKey = -1;
            var sh = new Shares();
            if (n <= 0) return sh;
            int left = n;
            try
            {
                string id = town != null && town.Settlement != null ? town.Settlement.StringId : null;
                Dictionary<int, VetClass> cl;
                if (id != null && _vet.TryGetValue(id, out cl))
                {
                    int key; VetClass c;
                    if (_vetShown.TryGetValue(id, out key) && cl.TryGetValue(key, out c))
                    {
                        classKey = key;
                        int got = Math.Min(left, c.Men);
                        if (got > 0) { sh.AddAll(VCut(c, got)); VPopOldest(c, got); left -= got; }
                    }
                    while (left > 0)
                    {
                        VetClass best = null; int bd = int.MaxValue;
                        foreach (var x in cl.Values) if (x.Men > 0 && x.Day.Count > 0 && x.Day[0] < bd) { bd = x.Day[0]; best = x; }
                        if (best == null) break;
                        int got = Math.Min(left, best.Men);
                        sh.AddAll(VCut(best, got)); VPopOldest(best, got); left -= got;
                    }
                    VDropEmpty(id);
                }
            }
            catch (Exception e) { VStumble("VetHire", e); }
            if (left > 0) { sh.Add("", left); _vOver += left; }
            _vHired += n - left;
            return sh;
        }

        /// <summary>Liczniki najmu (Veterans.OnTavernNumber): rodzaj partii, konni, tier karczmy wobec weterana.</summary>
        internal static void VetNoteHire(MobileParty party, int n, bool horseType, int typeTier, int classKey)
        {
            try
            {
                if (party == null) { _vNoWin += n; return; }
                if (party.IsMainParty) _vHirePlayer += n;
                else if (party.IsCaravan) _vHireCaravan += n;
                else if (party.IsLordParty)
                {
                    _vHireLord += n;
                    var c = party.ActualClan;
                    if (c != null && (c.IsUnderMercenaryService || c.IsClanTypeMercenary)) _vHireMercClan += n;
                }
                else _vHireOther += n;
                if (horseType) _vHireHorse += n;
                if (classKey >= 0 && typeTier >= 0)
                {
                    int ct = VKeyTier(classKey);
                    if (typeTier > ct) _vTierUp += n; else if (typeTier < ct) _vTierDown += n;
                }
            }
            catch (Exception e) { VStumble("VetNoteHire", e); }
        }

        internal static void VetNoteForeign(int n) { if (n > 0) _vForeign += n; }

        /// <summary>Synchronizacja (Veterans.Sync): klasy miasta do pokazania - id miasta -> lista (klucz, ludzie, najstarsza doba). null = brak klas.</summary>
        internal static List<int[]> VetClassesOf(string townId)
        {
            Dictionary<int, VetClass> cl;
            if (townId == null || !_vet.TryGetValue(townId, out cl) || cl.Count == 0) return null;
            var l = new List<int[]>(cl.Count);
            foreach (var kv in cl) if (kv.Value.Men > 0 && kv.Value.Day.Count > 0) l.Add(new[] { kv.Key, kv.Value.Men, kv.Value.Day[0] });
            return l.Count > 0 ? l : null;
        }

        internal static void VetSetShown(string townId, int key) { if (townId == null) return; if (key < 0) _vetShown.Remove(townId); else _vetShown[townId] = key; }

        // ================================================================ odczyt
        /// <summary>WETERANI wedlug regionu pochodzenia (rownanie regionu Z10, menu); "" pominiete.</summary>
        internal static void VetByRegion(Dictionary<string, double> dst)
        {
            if (dst == null) return;
            foreach (var cl in _vet.Values)
                foreach (var c in cl.Values)
                    foreach (var kv in c.Org)
                    {
                        if (kv.Key.Length == 0 || !(kv.Value > 0.0)) continue;
                        double v; dst.TryGetValue(kv.Key, out v); dst[kv.Key] = v + kv.Value;
                    }
        }

        /// <summary>Menu miasta: weterani w karczmie, konni, najdluzej czekajacy (doby).</summary>
        internal static int VetInTown(Settlement town, out int mounted, out int oldestDays)
        {
            mounted = 0; oldestDays = 0;
            try
            {
                Dictionary<int, VetClass> cl;
                if (town == null || town.StringId == null || !_vet.TryGetValue(town.StringId, out cl)) return 0;
                int men = 0, oldest = int.MaxValue, today = (int)CampaignTime.Now.ToDays;
                foreach (var kv in cl)
                {
                    men += kv.Value.Men;
                    if (VKeyIsHorse(kv.Key)) mounted += kv.Value.Men;
                    if (kv.Value.Day.Count > 0 && kv.Value.Day[0] < oldest) oldest = kv.Value.Day[0];
                }
                if (oldest != int.MaxValue) oldestDays = Math.Max(0, today - oldest);
                return men;
            }
            catch (Exception e) { VStumble("VetInTown", e); return 0; }
        }

        /// <summary>Suma puli (ludzie).</summary>
        internal static int VetWorld { get { int n = 0; foreach (var cl in _vet.Values) foreach (var c in cl.Values) n += c.Men; return n; } }

        // ================================================================ siew (raz na kampanie)
        /// <summary>
        /// Pula starych zolnierzy (Jeff 08.10, SP:711) = to, co stoi w karczmach w dniu siewu (nowa kampania: poczatkowe karczmy gry; stary zapis: obecne). Zdjeci ze stanu
        /// ludzi miasta (E4/4, MVetStart, podloga 100 jak pobor E4/7; dlug miasta TownOut - powrot czesci do stanu miasta przez BackToTown), udzial = region miasta; przy
        /// !LevyLive albo bez stanu miasta - koszyk "". Karczmy NIE sa zmieniane (pokazuja juz tych ludzi). Gdy PeopleOriginEnabled - po siewie E4/7.
        /// </summary>
        private static void VetSow(int today)
        {
            var s = Settings.Current;
            if (_vSown || !VetWired || s == null || !s.VeteransEnabled || Campaign.Current == null) return;
            if (s.PeopleOriginEnabled && !_sown) return;          // pochodzenie czynne, ale jeszcze bez siewu E4/7 - start dostalby "" (Z5)
            var rcb = Campaign.Current.GetCampaignBehavior<RecruitmentCampaignBehavior>();
            if (rcb == null) return;
            _vet.Clear(); _vetShown.Clear(); Array.Clear(_vAcc, 0, VSrcs);
            int towns = 0, men = 0, horse = 0, fromTown = 0, none = 0;
            var tiers = new SortedDictionary<int, int>();
            bool levy = LevyLive;
            foreach (var town in Town.AllTowns)
            {
                try
                {
                    if (town == null || town.Settlement == null || town.Settlement.StringId == null) continue;
                    var md = rcb.GetMercenaryData(town);
                    if (md == null || md.TroopType == null || md.Number <= 0) continue;
                    int n = md.Number;
                    bool h = md.TroopType.IsMounted;
                    int tier = Veterans.TierOf(md.TroopType);
                    int key = VKey(h, tier);
                    var sh = new Shares();
                    double took = 0;
                    var reg = OutlawLaw.RegionFor(town.Settlement);
                    if (levy && reg != null && reg.StringId != null) took = VetTakeTown(town.Settlement, reg, n);
                    if (took > Eps) sh.Add(reg.StringId, took);
                    if (n - took > Eps) sh.Add("", n - took);
                    VAdd(town.Settlement.StringId, key, sh, n, today);
                    _vetShown[town.Settlement.StringId] = key;
                    towns++; men += n; if (h) horse += n;
                    int tv; tiers.TryGetValue(tier, out tv); tiers[tier] = tv + n;
                    fromTown += (int)Math.Round(took); none += n - (int)Math.Round(took);
                }
                catch (Exception e) { VStumble("VetSow(miasto)", e); }
            }
            _vSown = true; _vSowDay = today;
            _vSeedMen = men; _vSeedTowns = towns;
            var sb = new StringBuilder(400);
            sb.Append("siew dnia ").Append(today).Append(" - starzy zolnierze z karczm ").Append(men).Append(" w ").Append(towns).Append(" miastach (konni ").Append(horse).Append("; tiery");
            bool f = true; foreach (var kv in tiers) { sb.Append(f ? " " : ", ").Append('t').Append(kv.Key).Append(' ').Append(kv.Value); f = false; }
            if (f) sb.Append(" -");
            sb.Append("), ze stanu ludzi miast (E4/4) ").Append(fromTown).Append(", bez zdjecia (brak stanu miasta albo pobor z ludzi wylaczony) ").Append(none);
            _vSowNote = sb.ToString();
            Log.Info("Weterani (E4/10): " + _vSowNote + " | typy najemnikow: " + Veterans.TypesNote() + ".");
        }

        /// <summary>Siew: n ze stanu ludzi miasta (E4/4) - podloga 100 ludzi (jak pobor E4/7 TakeLevy); ruch MVetStart (stan miasta); dlug miasta TownOut (powrot - BackToTown).</summary>
        private static double VetTakeTown(Settlement st, Settlement region, double n)
        {
            float p;
            if (st == null || !TryTown(st, out p)) return 0.0;
            double t = Math.Min(n, Math.Max(0.0, p - 100.0));
            if (!(t > Eps)) return 0.0;
            lock (Gate) _town[st.StringId] = (float)(p - t);
            Count(t, MVetStart, 0.0);
            try { if (_sown) Rg(region.StringId).TownOut += t; } catch (Exception e) { VStumble("VetTakeTown(dlug miasta)", e); }
            return t;
        }

        // ================================================================ doba
        /// <summary>PeopleBook.Daily (po OriginDaily): siew, linia wczytania, powroty po czasie czekania, synchronizacja karczm, przyciecie kluczy udzialow.</summary>
        private static void VetDaily(int day)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int today = (int)CampaignTime.Now.ToDays;
            bool sowedNow = false;
            if (!_vSown)
            {
                try { VetSow(today); sowedNow = _vSown; } catch (Exception e) { VStumble("VetSow", e); }
            }
            if (_vSown)
            {
                if (_vLoaded)
                {
                    _vLoaded = false;
                    try { Log.Info("Weterani (E4/10): " + (_vLoadNote ?? "z zapisu") + "; nieznane miasta " + UnknownTowns() + " (do domu); typy najemnikow: " + Veterans.TypesNote() + "."); }
                    catch (Exception e) { VStumble("VetDaily(linia wczytania)", e); }
                }
                try { VetReturns(today); } catch (Exception e) { VStumble("VetReturns", e); }
                try { if (VetLive && !sowedNow) Veterans.Sync(); } catch (Exception e) { VStumble("Veterans.Sync", e); }
                try
                {
                    // recenzja E4/7 R2-3: ta sama regula przyciecia kluczy co slowniki skladu (suma bez zmian, Drift w rg: - rownanie regionu je odejmuje)
                    if (OriginLive) foreach (var cl in _vet.Values) foreach (var c in cl.Values) if (c.Org.Count > 1) Fold(c.Org);
                }
                catch (Exception e) { VStumble("VetDaily(przyciecie)", e); }
            }
            _vMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        private static int UnknownTowns()
        {
            int n = 0;
            foreach (var id in _vet.Keys) { Settlement st = null; try { st = Settlement.Find(id); } catch { } if (st == null || st.Town == null) n++; }
            return n;
        }

        /// <summary>
        /// Powroty (R16, U15c): wpisy kolejki z doba <= dzis - VeteranWaitDays wracaja do DOM regionu pochodzenia (HomeKnown MBackVeteran - najslabsza wies regionu, z hearth;
        /// czesc do stanu miasta wedlug dlugu miasta; "" - Note bez hearth). Miasto oblezone - czekaja (nie przechodza przez linie oblezenia); miasto Innych - wszyscy od razu;
        /// nieznane miasto z zapisu - wszyscy od razu. Dziala takze przy wylaczonym VeteransEnabled (pula sie oproznia - nikt nie utyka).
        /// </summary>
        private static void VetReturns(int today)
        {
            var s = Settings.Current;
            int wait = Math.Max(1, s != null ? s.VeteranWaitDays : 120);
            int due = today - wait;
            _vSiegeMen = 0; _vSiegeTowns = 0; _vSiegeOldest = 0;
            foreach (var id in _vet.Keys.ToList())
            {
                try
                {
                    Settlement st = null;
                    try { st = Settlement.Find(id); } catch { st = null; }
                    bool aband = st == null || st.Town == null;
                    bool others = !aband && Veterans.IsOthersTown(st);
                    bool siege = !aband && !others && st.IsUnderSiege;
                    var cl = _vet[id];
                    if (siege)
                    {
                        int m = 0;
                        foreach (var c in cl.Values) { m += c.Men; if (c.Day.Count > 0) _vSiegeOldest = Math.Max(_vSiegeOldest, today - c.Day[0]); }
                        if (m > 0) { _vSiegeMen += m; _vSiegeTowns++; }
                        continue;
                    }
                    foreach (var c in cl.Values)
                    {
                        int n = 0;
                        if (aband || others) n = c.Men;
                        else { int k = 0; for (int i = 0; i < c.Day.Count && c.Day[i] <= due; i++) k += c.N[i]; n = k; }
                        if (n <= 0) continue;
                        var sh = VCut(c, n);
                        VPopOldest(c, n);
                        VetHome(sh, n);
                        if (aband) _vBackAband += n; else if (others) _vBackOthers += n; else _vBackDue += n;
                    }
                    VDropEmpty(id);
                    if (aband) _vetShown.Remove(id);
                }
                catch (Exception e) { VStumble("VetReturns(miasto)", e); }
            }
        }

        /// <summary>WETERANI -> DOM: czesc znana - HomeKnown (MBackVeteran, z hearth, czesc do stanu miasta wedlug dlugu miasta), reszta - Note (bez hearth). To nie wyjscie
        /// ze SLUZBY (Take nie bylo) - liczniki "dokad" E4/7 (_dest, _homeTown, _homeNoVillage) przywrocone, ruch liczony tutaj.</summary>
        private static void VetHome(Shares sh, int n)
        {
            if (n <= 0) return;
            double placed = 0;
            double d0 = _dest[OHomeKnown], tw0 = _homeTown, nv0 = _homeNoVillage;
            try { placed = HomeKnown(sh, MBackVeteran); }
            catch (Exception e) { VStumble("VetHome", e); placed = 0; }
            double dTown = _homeTown - tw0, dNoV = _homeNoVillage - nv0;
            _dest[OHomeKnown] = d0; _homeTown = tw0; _homeNoVillage = nv0;
            double rest = Math.Max(0.0, n - placed);
            if (rest > Eps) Note(rest, MBackVeteran, 0.0);
            _vBackMen += n; _vBackKnown += placed; _vBackNone += rest; _vBackTown += dTown; _vBackNoVillage += dNoV;
        }

        // ================================================================ linie
        /// <summary>Odcinek linii "Ludzie (ksiega)".</summary>
        private static string VetSegment()
        {
            if (!_vSown) return " | E4/10: konto WETERANI - przed siewem" + (Settings.Current != null && Settings.Current.VeteransEnabled ? (VetWired ? " (czeka na siew pochodzenia E4/7)" : " (latki karczmy niewpiete)") : " (Veterans Enabled wylaczone)");
            double known = 0, none = 0;
            foreach (var cl in _vet.Values) foreach (var c in cl.Values) foreach (var kv in c.Org) { if (kv.Key.Length == 0) none += kv.Value; else known += kv.Value; }
            int w = 0; for (int i = 0; i < VSrcs; i++) w += _vIn[i];
            return " | E4/10: konto WETERANI " + VetWorld + " (z pochodzeniem " + N0(known) + ", bez " + N0(none) + ") - dzis +" + w + (_vSeedMen > 0 ? " (+siew " + _vSeedMen + ")" : "")
                   + " / -" + _vHired + " / -" + _vBackMen;
        }

        /// <summary>Linia "Weterani (E4/10)" (zaraz po "Pochodzenie (E4/7)").</summary>
        private static void VetLine(int day)
        {
            var s = Settings.Current;
            var sb = new StringBuilder(2400);
            sb.Append("Weterani (E4/10): dzien ").Append(day);
            if (!_vSown)
            {
                sb.Append(" | przed siewem: ").Append(s == null || !s.VeteransEnabled ? "Veterans Enabled wylaczone (karczmy jak w grze)" : !VetWired ? "latki karczmy niewpiete (karczmy jak w grze)"
                          : "czeka na siew pochodzenia E4/7 (karczmy jak w grze)");
                if (_vStumblesDay > 0) sb.Append(" | potkniecia ").Append(_vStumblesDay);
                sb.Append('.');
                Log.Info(sb.ToString());
                return;
            }
            int pool = 0, horse = 0, towns = 0, oldest = 0, today = (int)CampaignTime.Now.ToDays;
            double known = 0, none = 0;
            var tiers = new SortedDictionary<int, int>();
            var regs = new HashSet<string>();
            foreach (var tw in _vet)
            {
                int m = 0;
                bool siege = false;
                try { var st = Settlement.Find(tw.Key); siege = st != null && st.IsUnderSiege; } catch { }
                foreach (var kc in tw.Value)
                {
                    var c = kc.Value;
                    m += c.Men;
                    if (VKeyIsHorse(kc.Key)) horse += c.Men;
                    int tv; tiers.TryGetValue(VKeyTier(kc.Key), out tv); tiers[VKeyTier(kc.Key)] = tv + c.Men;
                    if (!siege && c.Day.Count > 0) oldest = Math.Max(oldest, today - c.Day[0]);
                    foreach (var o in c.Org) { if (o.Key.Length == 0) none += o.Value; else { known += o.Value; regs.Add(o.Key); } }
                }
                pool += m;
                if (m > 0) towns++;
            }
            int allTowns = 0; try { allTowns = Town.AllTowns.Count; } catch { }
            sb.Append(VetLive ? "" : (s == null || !s.VeteransEnabled ? " - Veterans Enabled WYLACZONE (wejscia do domu, karczmy jak w grze, powroty trwaja)" : " - NIECZYNNE (latki karczmy)"));
            sb.Append(" | pula ").Append(pool).Append(" w ").Append(towns).Append(" z ").Append(allTowns).Append(" miast (konni ").Append(horse).Append(", piesi ").Append(pool - horse).Append("; tiery");
            bool f = true; foreach (var kv in tiers) { if (kv.Value <= 0) continue; sb.Append(f ? " " : ", ").Append('t').Append(kv.Key).Append(' ').Append(kv.Value); f = false; }
            if (f) sb.Append(" -");
            sb.Append("; z pochodzeniem ").Append(N0(known)).Append(" w ").Append(regs.Count).Append(" regionach, bez pochodzenia ").Append(N0(none)).Append("), najdluzej czeka ").Append(oldest).Append(" dob");
            if (_vSowNote != null) sb.Append(" [").Append(_vSowNote).Append(']');
            int w = 0, u = 0, below = 0, notown = 0;
            for (int i = 0; i < VSrcs; i++) { w += _vIn[i]; u += _vElig[i]; below += _vBelow[i]; notown += _vNoTown[i]; }
            sb.Append(" | wejscia ").Append(w).Append(':');
            for (int i = 0; i < VSrcs; i++) sb.Append(i == 0 ? " " : ", ").Append(VSrcName[i]).Append(' ').Append(_vIn[i]);
            sb.Append(" (konni ").Append(_vInHorse).Append(')');
            double share = s != null ? Math.Max(0.0, Math.Min(1.0, (double)s.VeteranShareToTavern)) : 0.0;
            sb.Append(" | uprawnieni ").Append(u).Append(", do karczmy ").Append(w).Append(" = ").Append(u > 0 ? (100.0 * w / u).ToString("0.0", Inv) : "-").Append("% (cel ").Append((100.0 * share).ToString("0.#", Inv))
              .Append("%), ponizej tieru ").Append(s != null ? s.VeteranMinTier : 3).Append(" do domu ").Append(below).Append(", bez miasta ").Append(notown);
            sb.Append(" | najeci ").Append(_vHired + _vOver).Append(": lordowie ").Append(_vHireLord).Append(" (w tym rody najemne ").Append(_vHireMercClan).Append("), karawany ").Append(_vHireCaravan)
              .Append(", gracz ").Append(_vHirePlayer).Append(", inne partie ").Append(_vHireOther).Append("; konni ").Append(_vHireHorse).Append("; tier karczmy wobec weterana: awans +").Append(_vTierUp)
              .Append(", spadek -").Append(_vTierDown);
            sb.Append(" | powroty ").Append(_vBackMen).Append(" po ").Append(s != null ? s.VeteranWaitDays : 120).Append(" dobach: do DOM z pochodzeniem ").Append(M0(_vBackKnown)).Append(" ludzi (+")
              .Append(H3dV(VHearthBack())).Append(" hearth; w tym do stanu miast ").Append(M0(_vBackTown)).Append("), bez pochodzenia ").Append(M0(_vBackNone))
              .Append(" (w tym znani bez wsi w regionie ").Append(M0(_vBackNoVillage)).Append("), z miast Innych ").Append(_vBackOthers).Append(", porzucone miasta ").Append(_vBackAband)
              .Append("; czekaja w oblezonych miastach ").Append(_vSiegeMen).Append(" (miast ").Append(_vSiegeTowns).Append(", najdluzej ").Append(_vSiegeOldest).Append(" dob)");
            sb.Append(" | karczmy: pokazane ").Append(VShownTowns).Append(" miast (konny typ ").Append(VShownHorse).Append("), oproznione ").Append(VClearTowns).Append(", klasy bez typu ").Append(VNoType)
              .Append(", konni jako piesi ").Append(VFootForHorse).Append(", najem bez okna ").Append(_vNoWin).Append(", najem ponad pule ").Append(_vOver).Append(", przyrost obcy ").Append(_vForeign);
            if (_vBkMiss > 0) sb.Append(" | ludnosc BK miasta nie przyjela (sklad, nie ludzie) ").Append(_vBkMiss);
            int diff = pool - (int)Math.Round(_vPool0) - _vSeedMen - w + _vHired + _vBackMen;
            sb.Append(" | kontrola: pula wczoraj ").Append(N0(_vPool0)).Append(" + siew ").Append(_vSeedMen).Append(" + wejscia ").Append(w).Append(" - najeci ").Append(_vHired)
              .Append(" - powroty ").Append(_vBackMen).Append(" = ").Append(pool).Append(" (roznica ").Append(diff).Append(')');
            sb.Append(" | potkniecia ").Append(_vStumblesDay).Append(" (razem ").Append(_vStumbles).Append(')');
            sb.Append(" | czas ").Append(_vMs.ToString("0.00", Inv)).Append(" ms.");
            Log.Info(sb.ToString());
        }

        private static double VHearthBack() { lock (Gate) return _mHearth[MBackVeteran]; }
        private static string H3dV(double v) { return v.ToString("0.###", Inv); }

        /// <summary>Koniec doby (takze bez logu): pula do kontroli jutra, liczniki od zera.</summary>
        private static void VetEndDay()
        {
            _vPool0 = VetWorld;
            VetNewDay();
        }
    }
}
