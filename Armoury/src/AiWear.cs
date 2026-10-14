using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// ZUZYCIE I NAPRAWY SPRZETU AI (wpis 85; Jeff 05.10: "jak AI naprawia sprzet zepsuty i ile to trwa?" - nie naprawialo,
    /// bo nic sie nie zuzywalo). Zbrojownia DTE lorda AI to lista "przedmiot: ile" bez stanu - obok prowadzimy wlasny zapis:
    /// ile sztuk danego przedmiotu jest obitych i w jakim stanie (modyfikator gry). Reszta = sprawna.
    ///  - Po bitwie (wygranej albo przegranej, kto przezyl): BattleWear - TroopWearPercent sztuk w uzyciu dostaje zuzycie walki;
    ///    lup, ktory DTE dolozyl do zbrojowni (przybytek od ostatniego spisu po bitwie), wchodzi obity jak lup gracza.
    ///  - Zakupy AiGear i komplety rekrutow - sprawne (przybytek bez bitwy).
    ///  - W miescie, raz dziennie (wejscie + dzienny tick postoju): kowale miasta naprawiaja tyle, ile zdaza (rece rzemieslnikow
    ///    x udzial platnerzy i miecznikow x 18 h pracy / 0.6 h na sztuke), placi sakiewka ludzi do kasy miasta. Wraki zostaja.
    ///  - Sprzedaz nadwyzek (MenPurse) bierze najgorsze obite sztuki ze swoim stanem; sprawne ida jako sprawne.
    /// </summary>
    internal static class AiWear
    {
        private static bool On { get { var s = Settings.Current; return s != null && s.AiWearEnabled; } }

        // partia -> przedmiot -> modyfikator -> ile obitych
        private static readonly Dictionary<string, Dictionary<string, Dictionary<string, int>>> _worn = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>();
        // partia -> przedmiot -> ile bylo w zbrojowni przy ostatnim spisie
        private static readonly Dictionary<string, Dictionary<string, int>> _known = new Dictionary<string, Dictionary<string, int>>();
        private static readonly HashSet<string> _battleSince = new HashSet<string>();
        private static readonly Dictionary<string, int> _lastMend = new Dictionary<string, int>();
        private static int _dayLoot, _dayWorn, _dayMended, _dayPaid, _dayStamp = -1;
        // poprawka po audycie TOWARY 3 (krok 139 planu K13): material napraw AI z polek miast - liczniki doby (tylko log)
        private static int _dayMat, _dayWait, _dayWaitMask, _dayNoSmith, _dayTowns;
        private static int _dayByKind, _dayOldRule;    // T3 (noc 08/09.10): naprawione sztuki z metalem w recepturze - wedlug rodzaju / stara regula (kolczuga, nieznane, wylacznik)
        private static float _dayKgByKind, _dayKgOldRule;   // T3: kg metalu (surowki) tych sztuk - osobno, zeby sklad napraw nie mylil testu
        private static readonly float[] _dayKg = new float[MendMaterial.Kinds];
        private static readonly int[] _dayWaitBy = new int[MendMaterial.Kinds];
        // E6/N1 (217, R16): partia -> (doba, przedmiot -> ile sztuk nosza ludzie) - dopasowanie raz na dobe (naprawy liczone swiezo, rezerwa sakiewki z pamieci)
        private static readonly Dictionary<string, KeyValuePair<int, Dictionary<string, int>>> _menUsed = new Dictionary<string, KeyValuePair<int, Dictionary<string, int>>>();
        private static int _daySkipLoot, _dayFolk;   // E6/N1: obite sztuki pominiete (lup nienoszony) w zleceniach doby; robocizna do kiesy ludu (tylko log)
        private static int _usedErr;                 // E6/N1: potkniecia dopasowania (do logu najwyzej 3; partia wraca wtedy do starej reguly - wszystkie obite)
        // F6 pkt 2 (TRIAZ zb58 A3-7, sama wydajnosc): dopasowanie z _menUsed sluzy REZERWIE napraw (OutstandingCost) przez 5-8 dob zamiast do konca doby;
        // naprawa w miescie liczy swiezo jak dotad. Liczniki doby linii "Rezerwa napraw AI - pamiec dopasowania (F6)" (tylko log) i probka 1/64 (R9):
        // rezerwa z pamieci starszej niz doba obok rezerwy ze swiezego dopasowania - ile razy i o ile zlota sie roznia (wynik probki niczego nie zmienia)
        private static int _muToday, _muOld, _muCalc, _muFresh, _muDrop, _muSeq, _muProbes, _muProbeDiff, _muStumbles;
        private static long _muProbeGold, _muProbeAbs, _muProbeNet;

        private static void MuClear()
        {
            _muToday = _muOld = _muCalc = _muFresh = _muDrop = _muProbes = _muProbeDiff = 0; _muProbeGold = _muProbeAbs = _muProbeNet = 0;
        }

        /// <summary>F6 pkt 2: dopasowanie tej partii z pamieci jest nieaktualne (bitwa: polegli, lup, pociski; nowa pozycja ksiegi obitych) - nastepna rezerwa liczy swiezo.</summary>
        private static void DropUsed(string p) { if (p != null && _menUsed.Remove(p)) _muDrop++; }

        /// <summary>F6 pkt 2: ile dob dopasowanie z pamieci sluzy rezerwie - 5-8 wedlug nazwy partii (staly rozrzut terminow: pamieci nie ma w zapisie, wiec po
        /// wczytaniu wszystkie partie licza tej samej doby i bez rozrzutu wygasalyby razem).</summary>
        private static int UsedKeepDays(string p)
        {
            int h = 0;
            if (p != null) for (int i = 0; i < p.Length; i++) h = unchecked(h * 31 + p[i]);
            return 5 + (h & 3);
        }

        private static bool WornOnlyOn { get { var s = Settings.Current; return s != null && s.AiMendsWornOnly; } }

        // E8/P2 (244, PLAN 8.7a, 8.8c, 8.4a): pomiary doby - tylko liczniki i linia "Zuzycie AI - pomiary (E8/P2)" (Stage8MeasureLog); gra bez zmian
        private static bool P2On { get { var s = Settings.Current; return s != null && s.Stage8MeasureLog; } }
        private static int _p2LootG, _p2WornG, _p2MendG, _p2LootL, _p2WornL, _p2MendL;   // zalogi (G) i pozostale partie (L) liczone osobno - suma ma dac liczby linii "Zuzycie AI"
        private static int _p2Visits, _p2Jobs, _p2PurseP, _p2PurseN, _p2HoursP, _p2HoursN, _p2MatP, _p2OtherN;   // zlecenia napraw: partie i sztuki, ktore stanely - wedlug powodu
        private static long _p2PurseLeft;                                                  // reszta sakiewek partii, ktorych naprawy stanely na sakiewce
        private static readonly float[] _p2AmmoGone = new float[3];                       // "zeszloby": strzaly, belty, miotane (ulamki sumowane, bez losowania)
        private static readonly int[] _p2AmmoUse = new int[3];                            // sztuki pociskow w uzyciu u partii po bitwie
        private static float _p2AmmoWin, _p2AmmoLose, _p2IntSum;
        private static int _p2AmmoParties, _p2IntN, _p2Stumbles;

        private static void P2Clear()
        {
            _p2LootG = _p2WornG = _p2MendG = _p2LootL = _p2WornL = _p2MendL = 0;
            _p2Visits = _p2Jobs = _p2PurseP = _p2PurseN = _p2HoursP = _p2HoursN = _p2MatP = _p2OtherN = 0; _p2PurseLeft = 0;
            Array.Clear(_p2AmmoGone, 0, 3); Array.Clear(_p2AmmoUse, 0, 3); _p2AmmoWin = _p2AmmoLose = _p2IntSum = 0f; _p2AmmoParties = _p2IntN = 0;
        }

        // E8/W (249, PLAN 8.8c-d, 8.4a; LAWA 15; reguly R3-R5): zuzycie wedlug natezenia bitwy i sztuk noszonych (AiWearByIntensity), kolejka napraw
        // bez stania i rezerwa napraw nietknieta przez zakup (AiMendQueueContinue), amunicja AI (AiAmmoUse - W CIENIU). Liczniki doby linii "Zuzycie AI (E8/W)"
        private static bool ByIntensityOn { get { var s = Settings.Current; return s != null && s.AiWearByIntensity; } }
        private static bool QueueOn { get { var s = Settings.Current; return s != null && s.AiMendQueueContinue; } }
        private static bool AmmoOn { get { var s = Settings.Current; return s != null && s.AiAmmoUse; } }
        private static int _wParties, _wLost, _wHealthy, _wUsed, _wStep, _wWreck, _wBottom, _wFallback;   // bitwy: partie z zapisem, straty, noszone sztuki, obite o stopien nizej
        private static float _wIntSum, _wOldExp, _wNewExp;                                               // natezenie; oczekiwane zuzycie stara / nowa regula (bez losowania)
        private static int _wPoorP, _wPoorN, _wAfterPoor;                                                // naprawy: partie i sztuki czekajace na sakiewke, naprawione po pierwszej bez pieniedzy
        private static int _wBuys, _wBuyShiftN; private static long _wBuyShift;                          // zakupy brakow: zaplaty, ile kiesa pana zaplacila zamiast rezerwy napraw
        private static readonly int[] _wAmmo = new int[3]; private static int _wAmmoParties;             // amunicja zdjeta (strzaly, belty, miotane)
        private static int _wAmmoField;                                                                  // A9 (14.10): partie bitwy gracza z misja - pociski zuzyl DTE w misji, AmmoUse pominiete
        private static long _wTicks; private static int _wStumbles;                                      // czas dopasowania "co nosza ludzie" po bitwie; potkniecia sesji

        private static void WClear()
        {
            _wParties = _wLost = _wHealthy = _wUsed = _wStep = _wWreck = _wBottom = _wFallback = 0; _wIntSum = _wOldExp = _wNewExp = 0f;
            _wPoorP = _wPoorN = _wAfterPoor = 0; _wBuys = _wBuyShiftN = 0; _wBuyShift = 0;
            Array.Clear(_wAmmo, 0, 3); _wAmmoParties = 0; _wAmmoField = 0; _wTicks = 0;
        }

        private static void WStumble(string where, Exception e) { if (++_wStumbles <= 3) Log.Error("AiWear." + where + " (potkniecie " + _wStumbles + ")", e); }   // per partia, bez wylacznika (CLAUDE.md 7)

        internal static void Reset()
        {
            _worn.Clear(); _known.Clear(); _battleSince.Clear(); _lastMend.Clear(); _dayLoot = _dayWorn = _dayMended = _dayPaid = 0; _dayStamp = -1;
            _menUsed.Clear(); _daySkipLoot = _dayFolk = _usedErr = 0;
            MuClear(); _muSeq = 0; _muStumbles = 0;   // F6 pkt 2
            ClearMatDay();
            P2Clear(); _p2Stumbles = 0;   // E8/P2
            WClear(); _wStumbles = 0;     // E8/W
        }

        private static void ClearMatDay()
        {
            _dayMat = _dayWait = _dayWaitMask = _dayNoSmith = _dayTowns = 0; _dayByKind = _dayOldRule = 0; _dayKgByKind = _dayKgOldRule = 0f;
            Array.Clear(_dayKg, 0, _dayKg.Length); Array.Clear(_dayWaitBy, 0, _dayWaitBy.Length);
        }

        internal static string Export()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in _worn)
                foreach (var it in p.Value)
                    foreach (var m in it.Value)
                        if (m.Value > 0) sb.Append(p.Key).Append(',').Append(it.Key).Append(',').Append(m.Key).Append(',').Append(m.Value).Append(';');
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _worn.Clear(); _known.Clear(); _battleSince.Clear();
            if (string.IsNullOrEmpty(s)) return;
            foreach (var rec in s.Split(';'))
            {
                var a = rec.Split(','); int n;
                if (a.Length != 4 || !int.TryParse(a[3], out n) || n <= 0) continue;
                AddWorn(a[0], a[1], a[2], n);
            }
        }

        private static void AddWorn(string p, string item, string mod, int n)
        {
            Dictionary<string, Dictionary<string, int>> byItem;
            if (!_worn.TryGetValue(p, out byItem)) _worn[p] = byItem = new Dictionary<string, Dictionary<string, int>>();
            Dictionary<string, int> byMod;
            if (!byItem.TryGetValue(item, out byMod)) byItem[item] = byMod = new Dictionary<string, int>();
            int v; bool had = byMod.TryGetValue(mod, out v); v += n;
            // F6 pkt 2: do ksiegi partii WCHODZI nowa pozycja (przedmiot albo jego stan, ktorego partia nie miala) - dopasowanie z pamieci liczono tylko dla typow,
            // ktore mialy wtedy obite sztuki do naprawy, wiec moglo tego typu nie objac (przedmiot bez wpisu = "nikt nie nosi" = rezerwa 0). Pamiec tej partii precz
            if (!had && n > 0) DropUsed(p);
            if (v > 0) byMod[mod] = v; else byMod.Remove(mod);
            if (byMod.Count == 0) byItem.Remove(item);
            if (byItem.Count == 0) _worn.Remove(p);
        }

        private static int WornOf(string p, string item)
        {
            Dictionary<string, Dictionary<string, int>> byItem; Dictionary<string, int> byMod;
            if (!_worn.TryGetValue(p, out byItem) || !byItem.TryGetValue(item, out byMod)) return 0;
            int n = 0; foreach (var v in byMod.Values) n += v; return n;
        }

        private static ItemModifier Mod(string id) { try { return MBObjectManager.Instance.GetObject<ItemModifier>(id); } catch { return null; } }

        private static Dictionary<ItemObject, int> Armory(MobileParty mp)
        {
            var d = AiGear.Armories(); Dictionary<ItemObject, int> a;
            return d != null && mp != null && d.TryGetValue(mp.Id, out a) ? a : null;
        }

        /// <summary>Spis zbrojowni: przybytek po bitwie = lup (obity), bez bitwy = sprawny; ubytki przycinaja zapis obitych.</summary>
        private static void Sync(MobileParty mp)
        {
            var arm = Armory(mp);
            if (arm == null) return;
            string p = mp.StringId;
            Dictionary<string, int> known;
            bool first = !_known.TryGetValue(p, out known);
            if (first) _known[p] = known = new Dictionary<string, int>();
            bool battle = _battleSince.Remove(p);
            var seen = new HashSet<string>();
            foreach (var kv in arm)
            {
                if (kv.Key == null || !SupplyDemand.Equipmentish(kv.Key) || ArmouryBehavior.NoState(kv.Key) || MenPurse.HorseKind(kv.Key)) continue;   // recenzja E8/C: NoState - ksiega dalej prowadzi stan, ktory bron miotana juz ma (nowego nie dostaje: PickWornModifier -> NoWear)
                string id = kv.Key.StringId; seen.Add(id);
                int was; known.TryGetValue(id, out was);
                int now = Math.Max(0, kv.Value);
                if (!first && battle && now > was)
                    for (int k = 0; k < now - was; k++)
                    {
                        var m = ArmouryBehavior.PickWornModifier(kv.Key);
                        if (m != null) { AddWorn(p, id, m.StringId, 1); _dayLoot++; if (mp.IsGarrison) _p2LootG++; else _p2LootL++; }   // E8/P2: zalogi osobno (tylko licznik)
                    }
                // ubytek (polegli, sprzedaz): obitych nie wiecej niz sztuk
                int w = WornOf(p, id);
                if (w > now) TrimWorn(p, id, w - now);
                known[id] = now;
            }
            foreach (var id in known.Keys.ToList()) if (!seen.Contains(id)) { known.Remove(id); int w = WornOf(p, id); if (w > 0) TrimWorn(p, id, w); }
        }

        private static void TrimWorn(string p, string item, int n)
        {
            Dictionary<string, Dictionary<string, int>> byItem; Dictionary<string, int> byMod;
            if (!_worn.TryGetValue(p, out byItem) || !byItem.TryGetValue(item, out byMod)) return;
            foreach (var m in byMod.Keys.ToList()) { if (n <= 0) break; int c = Math.Min(n, byMod[m]); AddWorn(p, item, m, -c); n -= c; }
        }

        /// <summary>wpis 89: kupiona sztuka ze stanem z polki - obita zostaje obita (dotad AI kupowalo tanio zuzyte i mialo je jako nowe).</summary>
        internal static void NoteBought(MobileParty mp, EquipmentElement el, int n)
        {
            if (!On || mp == null || el.Item == null) return;
            NoteSound(mp, el.Item, n);
            var m = el.ItemModifier;
            if (m != null && m.PriceMultiplier < 1f && !MenPurse.HorseKind(el.Item) && !ArmouryBehavior.NoState(el.Item)) AddWorn(mp.StringId, el.Item.StringId, m.StringId, n);   // recenzja E8/C: NoState - obita bron miotana (zakup z polki, komplet od gracza, dar) zostaje obita
        }

        internal static void Forget(MobileParty mp)
        {
            if (mp == null) return;
            string p = mp.StringId; _worn.Remove(p); _known.Remove(p); _battleSince.Remove(p); _lastMend.Remove(p); _menUsed.Remove(p);
        }

        /// <summary>171 (recenzja): czy zapis zuzycia jest prowadzony - zalogi sprzedaja nadwyzke ze stanem z zapisu tylko wtedy.</summary>
        internal static bool BookOn { get { return On; } }

        /// <summary>
        /// 171 (recenzja, jedna regula zuzycia dla zalog): n sztuk przedmiotu przechodzi z from do to (partia lorda <-> zaloga, rozwiazana partia) - razem
        /// z nimi czesc zapisu obitych: udzial proporcjonalny (nie wiemy, ktorzy ludzie maja obite), ale nie mniej niz trzeba, zeby u dawcy obitych nie bylo
        /// wiecej niz sztuk, i nie wiecej niz n. Wolac PRZED zdjeciem sztuk ze zbrojowni dawcy (countBefore = ile mial). Dotad sztuki z zalogi wchodzily
        /// jako sprawne, a obite lorda w zalodze stawaly sie sprawne (naprawa bez kowala i materialu).
        /// </summary>
        internal static void MoveWorn(MobileParty from, MobileParty to, ItemObject it, int n, int countBefore, bool sync = true)
        {
            if (!On || from == null || to == null || it == null || n <= 0) return;
            if (sync) Sync(from);   // sync = false: spis dawcy zrobiony juz w tym samym przeniesieniu (Known trzyma go w zgodzie)
            NoteSound(to, it, n);   // przybytek u odbiorcy to nie lup (spis od razu, gdy odbiorca ma spis)
            if (ArmouryBehavior.NoState(it) || MenPurse.HorseKind(it)) return;   // recenzja E8/C: NoState
            string pf = from.StringId, id = it.StringId;
            int w = WornOf(pf, id);
            if (w > 0)
            {
                int c = Math.Max(1, countBefore);
                int k = (int)Math.Round(w * (double)n / c);
                k = Math.Max(k, w - Math.Max(0, countBefore - n));
                k = Math.Min(k, Math.Min(w, n));
                Dictionary<string, Dictionary<string, int>> byItem; Dictionary<string, int> byMod;
                if (k > 0 && _worn.TryGetValue(pf, out byItem) && byItem.TryGetValue(id, out byMod))
                    foreach (var m in byMod.Keys.ToList())
                    {
                        if (k <= 0) break;
                        int t = Math.Min(k, byMod[m]);
                        AddWorn(pf, id, m, -t); AddWorn(to.StringId, id, m, t);
                        k -= t;
                    }
            }
            Known(from, it, -n);
        }

        /// <summary>
        /// Poprawki sklad7 (ekran druzyny gracza): n sztuk przedmiotu wychodzi ze zbrojowni AI (zaloga) do zbrojowni druzyny gracza, ktora trzyma stan
        /// w modyfikatorze sztuki. Ten sam udzial obitych co MoveWorn (proporcjonalnie, nie mniej niz trzeba, nie wiecej niz n) - zdjety z zapisu dawcy
        /// i zwrocony (stan, ile); reszta sztuk jest sprawna. Wolac PRZED zdjeciem sztuk ze slownika dawcy (countBefore = ile mial).
        /// </summary>
        internal static List<KeyValuePair<ItemModifier, int>> TakeWornShare(MobileParty from, ItemObject it, int n, int countBefore, bool sync = true)
        {
            var list = new List<KeyValuePair<ItemModifier, int>>();
            if (!On || from == null || it == null || n <= 0) return list;
            if (sync) Sync(from);
            if (!ArmouryBehavior.NoState(it) && !MenPurse.HorseKind(it))   // recenzja E8/C: NoState - obita bron miotana wraca do druzyny gracza obita
            {
                string pf = from.StringId, id = it.StringId;
                int w = WornOf(pf, id);
                if (w > 0)
                {
                    int c = Math.Max(1, countBefore);
                    int k = (int)Math.Round(w * (double)n / c);
                    k = Math.Max(k, w - Math.Max(0, countBefore - n));
                    k = Math.Min(k, Math.Min(w, n));
                    Dictionary<string, Dictionary<string, int>> byItem; Dictionary<string, int> byMod;
                    if (k > 0 && _worn.TryGetValue(pf, out byItem) && byItem.TryGetValue(id, out byMod))
                        foreach (var m in byMod.Keys.ToList())
                        {
                            if (k <= 0) break;
                            int t = Math.Min(k, byMod[m]);
                            if (t <= 0) continue;
                            AddWorn(pf, id, m, -t);
                            var mod = Mod(m);
                            if (mod != null) list.Add(new KeyValuePair<ItemModifier, int>(mod, t));   // nieznany stan - sztuka idzie jako sprawna
                            k -= t;
                        }
                }
            }
            Known(from, it, -n);
            return list;
        }

        /// <summary>AiGear dolozyl kupione sztuki - sprawne, spis od razu.</summary>
        internal static void NoteSound(MobileParty mp, ItemObject it, int n)
        {
            if (!On || mp == null || it == null) return;
            Dictionary<string, int> known;
            if (!_known.TryGetValue(mp.StringId, out known)) return;   // brak spisu - pierwszy Sync uzna calosc za sprawna
            int was; known.TryGetValue(it.StringId, out was); known[it.StringId] = was + n;
        }

        // ------------------------------------------------------------ bitwa
        internal static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                if (!On || me == null) return;
                Day();
                var s = Settings.Current;
                float share = MBMath.ClampFloat(s.TroopWearPercent, 0f, 100f) / 100f;
                bool p2 = P2On;   // E8/P2 (244): pomiar "Amunicja AI: zeszloby" (tylko licznik)
                bool byInt = ByIntensityOn, ammo = AmmoOn;   // E8/W (249)
                // A9 (14.10, wlaczenie AiAmmoUse): w bitwie gracza z misja (walka w polu) pociski partii AI zuzyl juz DTE strzal po strzale - DTE wola
                // IsAmmoAndEmpty (AmmoRecovery, odzysk wedlug strony E8/A) dla agentow WSZYSTKICH partii bitwy gracza; drugi rachunek wedlug natezenia
                // zdejmowalby te kolczany drugi raz. "Wyslij wojsko" bez misji (symulacja) i bitwy samego AI - jak w E8/W. Bron miotana dalej sie nie obija (ammo).
                bool ammoUse = ammo && !LosersFlee.FieldFought(me);
                foreach (var side in new[] { me.AttackerSide, me.DefenderSide })
                {
                    if (side == null) continue;
                    foreach (var mep in side.Parties)
                    {
                        var mp = mep != null && mep.Party != null ? mep.Party.MobileParty : null;
                        // K1 (przeglad): takze zalogi - od K1-C dostaja w bitwie gracza lup DTE i walcza sprzetem ze zbrojowni (dotad lup zalogi
                        // wchodzil jako sprawny, a jej sprzet nie obijal sie w walce; taki "sprawny" lup szedl do kupca albo graczowi jako zwrot B6)
                        if (mp == null || mp.IsMainParty || !(mp.IsLordParty || mp.IsGarrison) || !mp.IsActive) continue;
                        _battleSince.Add(mp.StringId);   // przybytek do nastepnego spisu = lup (DTE doklada go w tym samym zdarzeniu)
                        DropUsed(mp.StringId);           // F6 pkt 2: po bitwie inni ludzie i inna zbrojownia (polegli, lup, pociski) - rezerwa napraw liczy dopasowanie od nowa
                        if (p2) P2Ammo(me, side, mep, mp);   // E8/P2 (244, PLAN 8.4a): ile pociskow zeszloby tej partii - nic nie zdejmuje, bez losowania
                        float intensity = WBattle(mep, mp, s);   // E8/W (249, R4): natezenie bitwy tej partii i straty do linii (tylko licznik)
                        if (ammoUse) AmmoUse(me, side, mp, intensity);   // E8/W (PLAN 8.4a): pociski schodza ze zbrojowni - tylko przy AiAmmoUse (A9 14.10: domyslnie wlaczone)
                        else if (ammo) _wAmmoField++;                    // A9: bitwa gracza z misja - zuzyl DTE w misji (tylko licznik)
                        if (share <= 0f) continue;
                        // zuzycie walki: czesc sztuk W UZYCIU (do liczby ludzi) - sprawne staja sie obite
                        var arm = Armory(mp); if (arm == null) continue;
                        // E8/W (249, R4, PLAN 8.8d): share x natezenie, obijane sztuki noszone (dopasowanie N1), obita schodzi o stopien; false = dopasowanie
                        // sie potknelo przed jakakolwiek zmiana - ta partia stara regula ponizej (jak UsedByMen w N1)
                        if (byInt && WearByIntensity(mp, arm, share, intensity, p2, ammo)) continue;
                        if (!byInt && p2) WShadow(mp, arm, share, intensity, ammo);   // E8/W (R10): klucz wylaczony - obie reguly policzone w linii (tylko licznik)
                        var need = AiGear.NeedBuckets(mp);
                        // wpis 95 (audyt zuzycia pkt 4): potrzeba koszyka ROZDZIELANA miedzy przedmioty (dotad liczona od nowa dla
                        // kazdego przedmiotu w koszyku - 10 roznych zbroi t3 dawalo ~5x za duzo sztuk "w uzyciu")
                        foreach (var kv in arm.ToList())
                        {
                            var it = kv.Key;
                            if (it == null || kv.Value <= 0 || !SupplyDemand.Equipmentish(it) || ArmouryBehavior.NoWear(it) || MenPurse.HorseKind(it)) continue;
                            if (ammo && it.ItemType == ItemObject.ItemTypeEnum.Thrown) continue;   // E8/W (R3): przy AiAmmoUse bron miotana schodzi jak pocisk - nie obija sie drugi raz
                            int bk = AiGear.Bucket(it);
                            int nd; need.TryGetValue(bk, out nd);
                            int inUse = Math.Min(nd, kv.Value);
                            need[bk] = Math.Max(0, nd - inUse);
                            int sound = kv.Value - WornOf(mp.StringId, it.StringId);
                            int hits = Math.Min(sound, (int)Math.Floor(inUse * share + MBRandom.RandomFloat));
                            for (int k = 0; k < hits; k++)
                            {
                                var m = ArmouryBehavior.MildWornModifier(it);   // wpis 95 (pkt 5): bitwa = jeden stopien, nie loteria lupu
                                if (m != null) { AddWorn(mp.StringId, it.StringId, m.StringId, 1); _dayWorn++; if (mp.IsGarrison) _p2WornG++; else _p2WornL++; }   // E8/P2: zalogi osobno (tylko licznik)
                            }
                        }
                    }
                }
            }
            catch (Exception e) { Log.Error("AiWear.OnMapEventEnded", e); }
        }

        /// <summary>
        /// E8/P2 (244, PLAN 8.4a; wzor dla E8/W "amunicja AI"): ile kolczanow, snopow beltow i wiazek broni miotanej zeszloby tej partii w tej bitwie, gdyby
        /// AI zuzywalo pociski. Sztuki pociskow W UZYCIU (koszyki wzorcow do liczby sztuk w zbrojowni - ta sama miara co zuzycie) x natezenie bitwy tej
        /// partii (R4, wzor gracza z autobitwy: (polegli + ranni) / zdrowi na starcie / TroopWearBaseCasualtyShare, 0.1-3) x udzial strony: pokonany 1.0,
        /// zwyciezca 1 - odzysk (AmmoRecoverPercent + AmmoRepairPercent - regula AmmoRecovery "zwyciezca zbiera pole", domyslnie 0.35); najwyzej tyle,
        /// ile w uzyciu. Ulamki sumowane - bez losowania (MBRandom nietkniety). Wlasny slownik potrzeb, zbrojownia tylko czytana - nic nie schodzi.
        /// </summary>
        private static void P2Ammo(MapEvent me, MapEventSide side, MapEventParty mep, MobileParty mp)
        {
            try
            {
                var arm = Armory(mp); if (arm == null) return;
                var s = Settings.Current;
                int lost = (mep.DiedInBattle != null ? mep.DiedInBattle.TotalManCount : 0) + (mep.WoundedInBattle != null ? mep.WoundedInBattle.TotalManCount : 0);
                float intensity = MBMath.ClampFloat((lost / (float)Math.Max(1, mep.HealthyManCountAtStart)) / Math.Max(0.01f, s.TroopWearBaseCasualtyShare), 0.1f, 3f);
                _p2IntSum += intensity; _p2IntN++;
                bool won = me.WinningSide != BattleSideEnum.None && side.MissionSide == me.WinningSide;
                float keep = MBMath.ClampFloat((s.AmmoRecoverPercent + s.AmmoRepairPercent) / 100f, 0f, 1f);
                float f = Math.Min(1f, intensity * (won ? 1f - keep : 1f));
                Dictionary<int, int> need = null;
                bool any = false;
                foreach (var kv in arm)
                {
                    var it = kv.Key;
                    if (it == null || kv.Value <= 0) continue;
                    int kind = AmmoKind(it);   // E8/C (R3): jeden predykat "pocisk"
                    if (kind < 0) continue;
                    if (need == null) need = AiGear.NeedBuckets(mp);
                    int bk = AiGear.Bucket(it), nd;
                    need.TryGetValue(bk, out nd);
                    int inUse = Math.Min(nd, kv.Value);
                    if (inUse <= 0) continue;
                    need[bk] = nd - inUse;
                    float gone = inUse * f;
                    _p2AmmoUse[kind] += inUse; _p2AmmoGone[kind] += gone;
                    if (won) _p2AmmoWin += gone; else _p2AmmoLose += gone;
                    any = true;
                }
                if (any) _p2AmmoParties++;
            }
            catch (Exception e) { if (++_p2Stumbles <= 3) Log.Error("AiWear.P2Ammo (potkniecie " + _p2Stumbles + ")", e); }   // per partia, bez wylacznika (CLAUDE.md 7)
        }

        // ------------------------------------------------------------ E8/W (249): zuzycie wedlug natezenia bitwy, sztuki noszone, amunicja AI
        /// <summary>
        /// E8/W (R4 - jedna regula zuzycia): natezenie bitwy tej partii = (polegli + ranni) / zdrowi na starcie / TroopWearBaseCasualtyShare, 0.1-3 (wzor
        /// gracza z autobitwy, ArmouryBehavior.WearTheTroops; ta sama liczba co w pomiarze P2Ammo). Partie z zapisem zbrojowni wchodza do linii doby.
        /// </summary>
        private static float WBattle(MapEventParty mep, MobileParty mp, Settings s)
        {
            float intensity = 1f;
            try
            {
                int lost = (mep.DiedInBattle != null ? mep.DiedInBattle.TotalManCount : 0) + (mep.WoundedInBattle != null ? mep.WoundedInBattle.TotalManCount : 0);
                int healthy = Math.Max(1, mep.HealthyManCountAtStart);
                intensity = MBMath.ClampFloat((lost / (float)healthy) / Math.Max(0.01f, s.TroopWearBaseCasualtyShare), 0.1f, 3f);
                if (Armory(mp) != null) { _wParties++; _wLost += lost; _wHealthy += healthy; _wIntSum += intensity; }
            }
            catch (Exception e) { WStumble("WBattle", e); }
            return intensity;
        }

        /// <summary>E8/W: sztuka zbrojowni, ktora sie obija (jak dotad: rynsztunek bez amunicji, zwierzat i towaru); przy AiAmmoUse bron miotana schodzi jak pocisk (R3).</summary>
        private static bool Wearable(ItemObject it, int n, bool ammo)
        {
            return it != null && n > 0 && SupplyDemand.Equipmentish(it) && !ArmouryBehavior.NoWear(it) && !MenPurse.HorseKind(it)
                   && !(ammo && it.ItemType == ItemObject.ItemTypeEnum.Thrown);
        }

        /// <summary>E8/W: ile sztuk kazdego przedmiotu nosza ludzie partii TERAZ (po bitwie) - to samo dopasowanie co naprawy N1 i nadwyzki (MenPurse.AiWornByMen,
        /// roster GarrisonArmory.FitRoster), dla wszystkich typow, ktore sie obijaja. Bez pamieci doby (_menUsed trzyma dopasowanie napraw - tylko typy z obitymi).</summary>
        private static Dictionary<ItemObject, int> BattleUsed(MobileParty mp, Dictionary<ItemObject, int> arm, bool ammo)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                var types = new HashSet<ItemObject.ItemTypeEnum>();
                foreach (var kv in arm) if (Wearable(kv.Key, kv.Value, ammo)) types.Add(kv.Key.ItemType);
                if (types.Count == 0) return new Dictionary<ItemObject, int>();
                return MenPurse.AiWornByMen(arm, GarrisonArmory.FitRoster(mp), types);
            }
            finally { _wTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>E8/W (R10): ile sztuk zuzylaby STARA regula (stale TroopWearPercent sztuk "w uzyciu" po koszykach wzorcow, tylko sprawne) - wartosc oczekiwana,
        /// bez losowania; zbrojownia i ksiega tylko czytane. Wolac PRZED zmiana ksiegi w tej bitwie.</summary>
        private static float OldRuleExpected(MobileParty mp, Dictionary<ItemObject, int> arm, float share)
        {
            float sum = 0f;
            var need = AiGear.NeedBuckets(mp);
            foreach (var kv in arm)
            {
                var it = kv.Key;
                if (it == null || kv.Value <= 0 || !SupplyDemand.Equipmentish(it) || ArmouryBehavior.NoWear(it) || MenPurse.HorseKind(it)) continue;
                int bk = AiGear.Bucket(it), nd;
                need.TryGetValue(bk, out nd);
                int inUse = Math.Min(nd, kv.Value);
                need[bk] = Math.Max(0, nd - inUse);
                int sound = kv.Value - WornOf(mp.StringId, it.StringId);
                if (sound > 0 && inUse > 0) sum += Math.Min(sound, inUse * share);
            }
            return sum;
        }

        /// <summary>E8/W (R10): klucz AiWearByIntensity wylaczony - stara regula dziala, a linia doby dostaje obie liczby oczekiwane (stara i nowa); tylko licznik.</summary>
        private static void WShadow(MobileParty mp, Dictionary<ItemObject, int> arm, float share, float intensity, bool ammo)
        {
            try
            {
                _wOldExp += OldRuleExpected(mp, arm, share);
                var used = BattleUsed(mp, arm, ammo);
                foreach (var kv in arm)
                {
                    int u;
                    if (!Wearable(kv.Key, kv.Value, ammo) || !used.TryGetValue(kv.Key, out u) || u <= 0) continue;
                    int inUse = Math.Min(u, kv.Value);
                    _wUsed += inUse; _wNewExp += Math.Min(inUse, inUse * share * intensity);
                }
            }
            catch (Exception e) { WStumble("WShadow", e); }
        }

        /// <summary>
        /// E8/W (249, PLAN 8.8d, R4): zuzycie po bitwie wedlug natezenia tej partii - share x natezenie sztuk NOSZONYCH przez ludzi (dopasowanie N1, nie
        /// koszyki wzorcow w kolejnosci zbrojowni); trafienie w sprawna sztuke = pierwszy stopien (jak dotad), w obita = stopien nizej (do wraku - jak
        /// u ludzi gracza w WearTheTroops; dotad obite nie schodzily, a trafienia ponad liczbe sprawnych przepadaly). Zwraca false, gdy dopasowanie sie
        /// potknelo PRZED jakakolwiek zmiana ksiegi (wolajacy obija te partie stara regula); potkniecie w trakcie - licznik, bez drugiego obijania.
        /// </summary>
        private static bool WearByIntensity(MobileParty mp, Dictionary<ItemObject, int> arm, float share, float intensity, bool shadow, bool ammo)
        {
            Dictionary<ItemObject, int> used;
            try
            {
                used = BattleUsed(mp, arm, ammo);
                if (shadow) _wOldExp += OldRuleExpected(mp, arm, share);   // R10: stara regula w tym samym biegu (przed zmiana ksiegi)
            }
            catch (Exception e) { WStumble("WearByIntensity (dopasowanie)", e); _wFallback++; return false; }
            try
            {
                string p = mp.StringId;
                foreach (var kv in arm.ToList())
                {
                    var it = kv.Key; int u;
                    if (!Wearable(it, kv.Value, ammo) || !used.TryGetValue(it, out u) || u <= 0) continue;
                    int inUse = Math.Min(u, kv.Value);
                    float x = inUse * share * intensity;
                    _wUsed += inUse; _wNewExp += Math.Min(inUse, x);
                    int hits = Math.Min(inUse, (int)Math.Floor(x + MBRandom.RandomFloat));
                    if (hits > 0) HitCarried(mp, p, it, kv.Value, inUse, hits);
                }
            }
            catch (Exception e) { WStumble("WearByIntensity", e); }
            return true;
        }

        /// <summary>
        /// E8/W: hits trafien w sztuki przedmiotu, ktore nosza ludzie (inUse z count). Noszone = najlepsze: wolne sztuki (count - inUse) to najgorsze obite
        /// (MenWornLots - ta sama kolejnosc co naprawy N1 i sprzedaz nadwyzek), reszta obitych i sprawne sa na ludziach. Kazda noszona sztuka ma rowna szanse
        /// (jak u gracza): sprawna -> pierwszy stopien (licznik "zuzyte w bitwach" jak dotad), obita -> stopien nizej; wrak albo dno drabinki - bez zmiany.
        /// </summary>
        private static void HitCarried(MobileParty mp, string p, ItemObject it, int count, int inUse, int hits)
        {
            string id = it.StringId;
            var mods = new List<string>(); var cnt = new List<int>();
            int cw = 0;
            Dictionary<string, Dictionary<string, int>> byItem; Dictionary<string, int> byMod;
            if (_worn.TryGetValue(p, out byItem) && byItem.TryGetValue(id, out byMod))
            {
                int skipped = 0;
                foreach (var lot in MenWornLots(byMod, Math.Max(0, count - inUse), ref skipped)) { mods.Add(lot.Key); cnt.Add(lot.Value); cw += lot.Value; }
            }
            int cs = Math.Max(0, inUse - cw);   // sprawne na ludziach
            ItemModifier mild = null; bool mildAsked = false;
            for (int h = 0; h < hits; h++)
            {
                if (cs + cw <= 0) break;
                int r = MBRandom.RandomInt(cs + cw);
                if (r < cs)
                {
                    if (!mildAsked) { mild = ArmouryBehavior.MildWornModifier(it); mildAsked = true; }   // wpis 95: bitwa = jeden stopien
                    if (mild == null) { cs = 0; continue; }                                               // sztuka bez drabinki stanow - nie obija sie (jak dotad)
                    AddWorn(p, id, mild.StringId, 1); _dayWorn++; if (mp.IsGarrison) _p2WornG++; else _p2WornL++;   // E8/P2: zalogi osobno (tylko licznik)
                    cs--; cw++; Bump(mods, cnt, mild.StringId);
                    continue;
                }
                r -= cs;
                int i = 0;
                while (i < cnt.Count - 1 && r >= cnt[i]) { r -= cnt[i]; i++; }
                var cur = Mod(mods[i]);
                var next = cur != null ? NextWorse(it, cur) : null;   // nieznany stan (usuniety mod) - bez zmiany
                if (next == null) { _wBottom++; continue; }
                AddWorn(p, id, mods[i], -1); AddWorn(p, id, next.StringId, 1);
                cnt[i]--; Bump(mods, cnt, next.StringId);
                _wStep++; if (LootPrices.IsWreck(next)) _wWreck++;
            }
        }

        private static void Bump(List<string> mods, List<int> cnt, string mod)
        {
            int i = mods.IndexOf(mod);
            if (i >= 0) cnt[i]++; else { mods.Add(mod); cnt.Add(1); }
        }

        /// <summary>E8/W: nastepny gorszy stan z drabinki przedmiotu (najblizszy nizszy mnoznik ceny - jak WearTheTroops gracza); wrak to dno (null).</summary>
        private static ItemModifier NextWorse(ItemObject it, ItemModifier cur)
        {
            try
            {
                if (LootPrices.IsWreck(cur) || it.ItemComponent == null || it.ItemComponent.ItemModifierGroup == null) return null;
                ItemModifier best = null;
                foreach (var m in it.ItemComponent.ItemModifierGroup.ItemModifiers)
                    if (m != null && m.PriceMultiplier < cur.PriceMultiplier && (best == null || m.PriceMultiplier > best.PriceMultiplier)) best = m;
                return best;
            }
            catch { return null; }
        }

        private static int AmmoKind(ItemObject it)
        {
            if (!ArmouryBehavior.IsMissile(it)) return -1;   // E8/C (R3): jeden predykat "pocisk" (ten sam zbior co dotad: strzaly 0, belty 1, bron miotana 2)
            return it.ItemType == ItemObject.ItemTypeEnum.Arrows ? 0 : it.ItemType == ItemObject.ItemTypeEnum.Bolts ? 1 : 2;
        }

        /// <summary>
        /// E8/W (249, PLAN 8.4a, R3) - TYLKO przy AiAmmoUse (W CIENIU: domyslnie wylaczone, "zeszloby" liczy P2Ammo): pociski partii AI schodza po bitwie ze
        /// zbrojowni. Ten sam wzor co pomiar P2Ammo: sztuki pociskow W UZYCIU (koszyki wzorcow do liczby sztuk) x natezenie bitwy tej partii x udzial strony
        /// (pokonany 1.0, zwyciezca 1 - odzysk: AmmoRecoverPercent + AmmoRepairPercent - regula AmmoRecovery), ulamek losowo, najwyzej tyle, ile w uzyciu.
        /// Zuzyty pocisk nie ma odbiorcy (wystrzelony i zlamany - jak u gracza); braki dokupi AiGear (Order ma Arrows / Bolts / Thrown). Bron miotana jest
        /// w ksiedze zuzycia - spis (Known) i zapis obitych zostaja w zgodzie ze zbrojownia.
        /// </summary>
        private static void AmmoUse(MapEvent me, MapEventSide side, MobileParty mp, float intensity)
        {
            try
            {
                var arm = Armory(mp); if (arm == null) return;
                var s = Settings.Current;
                bool won = me.WinningSide != BattleSideEnum.None && side.MissionSide == me.WinningSide;
                float keep = MBMath.ClampFloat((s.AmmoRecoverPercent + s.AmmoRepairPercent) / 100f, 0f, 1f);
                float f = Math.Min(1f, intensity * (won ? 1f - keep : 1f));
                if (f <= 0f) return;
                Dictionary<int, int> need = null;
                List<KeyValuePair<ItemObject, int>> gone = null;
                foreach (var kv in arm)
                {
                    var it = kv.Key;
                    if (it == null || kv.Value <= 0 || AmmoKind(it) < 0) continue;
                    if (need == null) need = AiGear.NeedBuckets(mp);
                    int bk = AiGear.Bucket(it), nd;
                    need.TryGetValue(bk, out nd);
                    int inUse = Math.Min(nd, kv.Value);
                    if (inUse <= 0) continue;
                    need[bk] = nd - inUse;
                    int k = Math.Min(inUse, (int)Math.Floor(inUse * f + MBRandom.RandomFloat));
                    if (k <= 0) continue;
                    if (gone == null) gone = new List<KeyValuePair<ItemObject, int>>();
                    gone.Add(new KeyValuePair<ItemObject, int>(it, k));
                }
                if (gone == null) return;
                string p = mp.StringId;
                foreach (var g in gone)
                {
                    int have;
                    if (!arm.TryGetValue(g.Key, out have) || have <= 0) continue;
                    int k = Math.Min(have, g.Value), left = have - k;
                    if (left > 0) arm[g.Key] = left; else arm.Remove(g.Key);
                    if (!ArmouryBehavior.NoState(g.Key))   // recenzja E8/C: NoState - bron miotana jest w spisie i w ksiedze obitych
                    {
                        Known(mp, g.Key, -k);
                        int w = WornOf(p, g.Key.StringId);
                        if (w > left) TrimWorn(p, g.Key.StringId, w - left);
                    }
                    _wAmmo[AmmoKind(g.Key)] += k;
                }
                _wAmmoParties++;
            }
            catch (Exception e) { WStumble("AmmoUse", e); }
        }

        /// <summary>
        /// E8/W (249, R5 "zakup nie zjada rezerwy napraw", PLAN 8.8c): ile z ceny zakupu brakow bierze sakiewka ludzi. Budzet zakupu (AiGear.TryBuyCore) to
        /// kiesa pana + sakiewka MINUS rezerwa na zalegle naprawy (hold - policzona raz na wizyte), ale zaplata brala dotad z sakiewki cala cene - rezerwa
        /// znikala tego samego dnia, a przydzial pana zostawal. Teraz sakiewka placi najwyzej "sakiewka - rezerwa", reszte kiesa pana (w budzecie, ktory juz
        /// mial; zloto nie powstaje - zmienia sie platnik). Pan nie placi (Twoja zaloga bez GarrisonBuysGearPlayer) albo klucz wylaczony - jak dotad.
        /// </summary>
        internal static int PurseShare(MobileParty mp, int cost, int hold, bool lordPays)
        {
            int have = MenPurse.Get(mp);
            int old = Math.Min(have, Math.Max(0, cost));
            if (!On || hold <= 0 || !lordPays) return old;
            int keep = Math.Min(old, Math.Max(0, have - hold));
            // recenzja E8/W: licznik i zmiana doby ksiegi (Day pisze linie logu) nie moga przerwac ZAPLATY - PurseShare biegnie w srodku zakupu
            // (sztuka juz zdjeta z polki i dopisana do zbrojowni, zloto jeszcze nie zaplacone); wyjatek = licznik potkniec, podzial zaplaty bez zmiany
            try
            {
                Day();
                _wBuys++;
                if (old > keep) { _wBuyShift += old - keep; _wBuyShiftN++; }
            }
            catch (Exception e) { WStumble("PurseShare", e); }
            return QueueOn ? keep : old;
        }

        // ------------------------------------------------------------ stan przy sprzedazy nadwyzek
        /// <summary>Sztuka idzie do kupca: najgorsza obita, jesli jest; inaczej sprawna (null).</summary>
        internal static ItemModifier TakeCondition(MobileParty mp, ItemObject it) { return TakeCondition(mp, it, true); }

        /// <summary>171: sync = false - spis zrobiony juz w tej samej sprzedazy (seria sztuk z jednej zbrojowni; Known trzyma spis w zgodzie).</summary>
        internal static ItemModifier TakeCondition(MobileParty mp, ItemObject it, bool sync)
        {
            if (!On || mp == null || it == null) return ArmouryBehavior.PickWornModifier(it);
            if (sync) Sync(mp);
            Dictionary<string, Dictionary<string, int>> byItem; Dictionary<string, int> byMod;
            if (!_worn.TryGetValue(mp.StringId, out byItem) || !byItem.TryGetValue(it.StringId, out byMod) || byMod.Count == 0) { Known(mp, it, -1); return null; }
            string worst = null; float wm = float.MaxValue;
            foreach (var id in byMod.Keys) { var m = Mod(id); float pm = m != null ? m.PriceMultiplier : 1f; if (pm < wm) { wm = pm; worst = id; } }
            AddWorn(mp.StringId, it.StringId, worst, -1);
            Known(mp, it, -1);
            return Mod(worst);
        }

        /// <summary>K1c (przeglad K1b, "Hand kit to the garrison"): ile sztuk przedmiotu w zbrojowni partii jest obitych i w jakim stanie - bez
        /// zdejmowania (TakeCondition zdejmuje). Po spisie (Sync), jak TakeCondition. Wylaczone zuzycie - pusto.</summary>
        internal static List<KeyValuePair<ItemModifier, int>> WornSplit(MobileParty mp, ItemObject it)
        {
            var list = new List<KeyValuePair<ItemModifier, int>>();
            if (!On || mp == null || it == null) return list;
            try
            {
                Sync(mp);
                Dictionary<string, Dictionary<string, int>> byItem; Dictionary<string, int> byMod;
                if (!_worn.TryGetValue(mp.StringId, out byItem) || !byItem.TryGetValue(it.StringId, out byMod)) return list;
                foreach (var kv in byMod)
                {
                    if (kv.Value <= 0) continue;
                    var m = Mod(kv.Key);
                    if (m != null) list.Add(new KeyValuePair<ItemModifier, int>(m, kv.Value));
                }
            }
            catch { list.Clear(); }
            return list;
        }

        /// <summary>K1: sztuka wzieta przez TakeCondition nie poszla do kupca (kasa miasta pusta) - wraca do zbrojowni ze swoim stanem.</summary>
        internal static void PutBack(MobileParty mp, ItemObject it, ItemModifier mod)
        {
            if (!On || mp == null || it == null) return;
            if (mod != null && mod.PriceMultiplier < 1f && !MenPurse.HorseKind(it) && !ArmouryBehavior.NoState(it)) AddWorn(mp.StringId, it.StringId, mod.StringId, 1);   // recenzja E8/C: NoState
            Known(mp, it, 1);
        }

        private static void Known(MobileParty mp, ItemObject it, int d)
        {
            Dictionary<string, int> known;
            if (!_known.TryGetValue(mp.StringId, out known)) return;
            int v; known.TryGetValue(it.StringId, out v); known[it.StringId] = Math.Max(0, v + d);
        }

        // ------------------------------------------------------------ naprawy w miescie
        private static int UnitCost(ItemObject it, ItemModifier m, Settlement st)
        {
            int old = Math.Max(1, (int)(it.Value * (1f - (m != null ? m.PriceMultiplier : 1f)) * 0.25f));
            // regula kowali miasta: ta sama robota co u gracza (MendMaterial.Labor); st null (zalegle naprawy w sakiewce) - zwykla dniowka
            return MendMaterial.RuleOn ? MendMaterial.Labor(it, MendMaterial.Share(new EquipmentElement(it, m)), st, old) : old;
        }
        private static bool Mendable(ItemModifier m) { return m != null && m.PriceMultiplier < 1f && !LootPrices.IsWreck(m); }   // wpis 97

        internal static int OutstandingCost(MobileParty mp) { return OutstandingCost(mp, null); }

        /// <summary>Rezerwa sakiewki na zalegle naprawy. Przy naprawach z materialem (MendMaterial.MenAndLordsOn) i podanym MIESCIE -
        /// robocizna + szacunek materialu z polki tego miasta (Bench.Estimate), sztuki bez receptury kowala pominiete; robocizna jak dotad.</summary>
        internal static int OutstandingCost(MobileParty mp, Settlement st)
        {
            if (!On || mp == null) return 0;
            int sum = 0;
            Dictionary<string, Dictionary<string, int>> byItem;
            if (!_worn.TryGetValue(mp.StringId, out byItem)) return 0;
            MendMaterial.Bench bench = MendMaterial.MenAndLordsOn && st != null && st.IsTown ? new MendMaterial.Bench(st) : null;
            if (bench != null && !bench.Ok) bench = null;
            // E6/N1 (217): rezerwa tylko na to, co kowale naprawia - przy AiMendsWornOnly obite sztuki, ktore nosza ludzie (dopasowanie z pamieci - F6 pkt 2:
            // do 5-8 dob, dotad do konca doby); dotad rezerwa obejmowala tez lup ponad potrzebe, ktorego nikt nie naprawi (sakiewka trzymana na zawsze)
            bool oldMem = false;
            var used = WornOnlyOn ? UsedByMen(mp, byItem, false, out oldMem) : null;
            var arm = used != null ? Armory(mp) : null;
            // E8/W (249, LAWA 15, R5): robocizna rezerwy przy placach miasta, w ktorym partia stoi - tyle zaplaci za naprawe (UnitCost(.., st) w MendInTown);
            // dotad zawsze poziom 1 (rezerwa za mala w bogatym miescie, za duza w biednym). Bez miasta (pole, zamek) - poziom 1 jak dotad
            Settlement wageAt = QueueOn && st != null && st.IsTown ? st : null;
            sum = ReserveSum(byItem, used, arm, bench, wageAt);
            // F6 pkt 2 (R9): co 64. rezerwa policzona z pamieci starszej niz doba liczona tez ze swiezego dopasowania - tylko licznik linii doby. Bez lawy
            // (szacunek materialu z polki ma wlasny licznik w linii E8/B - probka nie moze go ruszyc); to droga markietanow, zaopatrzenia BK i zamkow
            if (oldMem && used != null && bench == null && (++_muSeq & 63) == 0) ProbeReserve(mp, byItem, arm, wageAt, sum);
            return sum;
        }

        /// <summary>
        /// Suma rezerwy po ksiedze obitych partii (petla wydzielona z OutstandingCost w F6 pkt 2 - probka liczy ja drugi raz). used = null - stara regula:
        /// wszystkie obite. F6 pkt 2 (TRIAZ A3-7 podpunkt 3), TEN SAM WYNIK mniejsza praca: przedmiot bez wolnych sztuk (ludzie nosza wszystkie) - kazdy
        /// stan w calosci, bez MenWornLots (dwie listy, sortowanie i dwa dodatkowe odczyty stanu na przedmiot - oddalby dokladnie te stany i liczby);
        /// wolnych sztuk co najmniej tyle, ile obitych - MenWornLots oddalby pusta liste, przedmiot pominiety; MenWornLots tylko w przypadku mieszanym.
        /// </summary>
        private static int ReserveSum(Dictionary<string, Dictionary<string, int>> byItem, Dictionary<string, int> used, Dictionary<ItemObject, int> arm, MendMaterial.Bench bench, Settlement wageAt)
        {
            int sum = 0;
            foreach (var it in byItem)
            {
                var item = MBObjectManager.Instance.GetObject<ItemObject>(it.Key); if (item == null) continue;
                if (used != null)
                {
                    int free = FreeOf(arm, item, used);
                    if (free <= 0)
                    {
                        foreach (var m in it.Value) if (m.Value > 0) sum += LotCost(item, Mod(m.Key), bench, wageAt) * m.Value;
                        continue;
                    }
                    int worn = 0;
                    foreach (var m in it.Value) if (m.Value > 0) worn += m.Value;
                    if (free >= worn) continue;
                    int skipped = 0;
                    foreach (var lot in MenWornLots(it.Value, free, ref skipped))
                        sum += LotCost(item, Mod(lot.Key), bench, wageAt) * lot.Value;
                    continue;
                }
                foreach (var m in it.Value)
                    sum += LotCost(item, Mod(m.Key), bench, wageAt) * m.Value;
            }
            return sum;
        }

        /// <summary>F6 pkt 2 (R9, tylko licznik): rezerwa tej partii ze SWIEZEGO dopasowania obok rezerwy z pamieci (fromMem) - pamieci nie rusza, wyniku nie zmienia.</summary>
        private static void ProbeReserve(MobileParty mp, Dictionary<string, Dictionary<string, int>> byItem, Dictionary<ItemObject, int> arm, Settlement wageAt, int fromMem)
        {
            try
            {
                var now = UsedNow(mp, byItem);
                if (now == null) return;
                int fresh = ReserveSum(byItem, now, arm, null, wageAt);
                _muProbes++; _muProbeGold += fresh;
                if (fresh != fromMem) { _muProbeDiff++; _muProbeAbs += Math.Abs(fromMem - fresh); _muProbeNet += fromMem - fresh; }
            }
            catch (Exception e) { if (++_muStumbles <= 3) Log.Error("AiWear.ProbeReserve (potkniecie " + _muStumbles + ")", e); }   // per partia, bez wylacznika (CLAUDE.md 7)
        }

        /// <summary>Rezerwa na jedna sztuke (wydzielone z OutstandingCost w E6/N1 - bez zmiany): robocizna + szacunek materialu z polki; 0 - nie do naprawy.
        /// E8/W: wageAt - miasto, wedlug ktorego plac liczona jest robocizna (null = poziom 1 jak dotad).</summary>
        private static int LotCost(ItemObject item, ItemModifier mod, MendMaterial.Bench bench, Settlement wageAt)
        {
            if (!Mendable(mod)) return 0;
            int unit = UnitCost(item, mod, wageAt);
            if (bench != null)
            {
                var need = MendMaterial.Needs(new EquipmentElement(item, mod));
                if (need == null) return 0;
                unit += MendMaterial.Gold(bench.Estimate(need));
            }
            return unit;
        }

        // ------------------------------------------------------------ E6/N1 (217, PLAN 6.17, R16): AI naprawia tylko to, co nosza ludzie
        // Zbrojownia DTE to "przedmiot: ile"; ksiega _worn mowi, ile z nich jest obitych. Ile sztuk nosza ludzie - to samo dopasowanie co nadwyzki
        // (MenPurse.AiWornByMen = AiFitted z AiSurplusPlan: ludzie nosza najlepsze, co udzwigna). Wolne sztuki przedmiotu (ile - noszone) to najgorsze
        // obite (ta sama kolejnosc co sprzedaz nadwyzki - TakeCondition bierze najgorsza); obite ponad wolne nosza ludzie - tylko te ida do kowala.
        // Obity lup ponad potrzebe zostaje obity: sprzedaz nadwyzek (MenPurse.SellArmorySurplus) oddaje go kupcowi w jego stanie (wrak - zlom, paczka 158).

        /// <summary>Ile sztuk kazdego przedmiotu nosza ludzie partii (id -> sztuk). fresh = false (rezerwa napraw) - wynik z pamieci, jesli jest mlodszy niz
        /// UsedKeepDays partii (F6 pkt 2: 5-8 dob; dotad tylko z tej samej doby) i nie z przyszlosci; fresh = true (naprawa w miescie) - zawsze liczone i zapisane.
        /// old - wynik z pamieci starszej niz dzisiejsza (do probki). null - blad (jak dotad: wszystkie). Pamiec gasi bitwa i nowa pozycja ksiegi obitych (DropUsed).</summary>
        private static Dictionary<string, int> UsedByMen(MobileParty mp, Dictionary<string, Dictionary<string, int>> byItem, bool fresh, out bool old)
        {
            old = false;
            int today = (int)CampaignTime.Now.ToDays;
            KeyValuePair<int, Dictionary<string, int>> c;
            if (!fresh && _menUsed.TryGetValue(mp.StringId, out c) && c.Key <= today && today - c.Key < UsedKeepDays(mp.StringId))
            {
                if (c.Key == today) _muToday++; else { _muOld++; old = true; }
                return c.Value;
            }
            if (fresh) _muFresh++; else _muCalc++;
            var res = UsedNow(mp, byItem);
            if (res != null) _menUsed[mp.StringId] = new KeyValuePair<int, Dictionary<string, int>>(today, res);
            return res;
        }

        /// <summary>Dopasowanie "co nosza ludzie" policzone teraz, bez pamieci (cialo UsedByMen sprzed F6 pkt 2 - bez zmiany; wola je tez probka). null - blad.
        /// Dopasowanie tylko dla typow, w ktorych sa obite sztuki do naprawy (koszt).</summary>
        private static Dictionary<string, int> UsedNow(MobileParty mp, Dictionary<string, Dictionary<string, int>> byItem)
        {
            var res = new Dictionary<string, int>();
            try
            {
                var arm = Armory(mp);
                if (arm != null)
                {
                    var types = new HashSet<ItemObject.ItemTypeEnum>();
                    foreach (var it in byItem)
                    {
                        bool any = false;
                        foreach (var m in it.Value) if (m.Value > 0 && Mendable(Mod(m.Key))) { any = true; break; }
                        if (!any) continue;
                        var io = MBObjectManager.Instance.GetObject<ItemObject>(it.Key);
                        if (io != null) types.Add(io.ItemType);
                    }
                    if (types.Count > 0)
                        foreach (var kv in MenPurse.AiWornByMen(arm, GarrisonArmory.FitRoster(mp), types)) res[kv.Key.StringId] = kv.Value;
                }
            }
            catch (Exception e) { if (++_usedErr <= 3) Log.Error("AiWear.UsedByMen (potkniecie " + _usedErr + ")", e); return null; }   // per partia, bez wylacznika (CLAUDE.md 7)
            return res;
        }

        /// <summary>Wolne sztuki przedmiotu w zbrojowni: ile jest minus ile nosza ludzie (nie mniej niz 0).</summary>
        private static int FreeOf(Dictionary<ItemObject, int> arm, ItemObject item, Dictionary<string, int> used)
        {
            int cnt = 0, u;
            if (arm != null && item != null) arm.TryGetValue(item, out cnt);
            used.TryGetValue(item.StringId, out u);
            return Math.Max(0, cnt - u);
        }

        /// <summary>Obite sztuki przedmiotu, ktore nosza ludzie (stan, ile): najgorsze najpierw wypelniaja wolne sztuki (free) i zostaja; skipped - ile
        /// sztuk do naprawy (nie wrakow) tak pominieto.</summary>
        private static List<KeyValuePair<string, int>> MenWornLots(Dictionary<string, int> byMod, int free, ref int skipped)
        {
            var list = new List<KeyValuePair<string, int>>();
            var mods = new List<KeyValuePair<string, float>>();
            foreach (var m in byMod) { if (m.Value <= 0) continue; var md = Mod(m.Key); mods.Add(new KeyValuePair<string, float>(m.Key, md != null ? md.PriceMultiplier : 1f)); }
            mods.Sort((a, b) => a.Value.CompareTo(b.Value));   // najgorszy stan najpierw - jak TakeCondition
            foreach (var m in mods)
            {
                int n = byMod[m.Key];
                int k = Math.Min(free, n);
                free -= k; n -= k;
                if (k > 0 && Mendable(Mod(m.Key))) skipped += k;
                if (n > 0) list.Add(new KeyValuePair<string, int>(m.Key, n));
            }
            return list;
        }

        /// <summary>Dzien postoju w miescie: kowale naprawiaja, ile zdaza i na ile starcza sakiewki ludzi.</summary>
        internal static void MendInTown(MobileParty mp, Settlement st)
        {
            try
            {
                if (!On || mp == null || st == null || !st.IsTown || st.Town == null || mp.IsMainParty) return;
                int today = (int)CampaignTime.Now.ToDays, last;
                if (_lastMend.TryGetValue(mp.StringId, out last) && last == today) return;   // dzien pracy kowali raz na dobe
                _lastMend[mp.StringId] = today;
                Day();
                Sync(mp);
                Dictionary<string, Dictionary<string, int>> byItem;
                if (!_worn.TryGetValue(mp.StringId, out byItem)) return;
                var s = Settings.Current;
                // wpis 91: z WSPOLNEJ puli kowali miasta - lordowie i gracz dziela te same rece
                float perPiece = Math.Max(0.05f, s.MendLootHoursPerPiece);
                int cap = (int)(SmithHours.Available(st.Town) / perPiece);
                var jobs = new List<KeyValuePair<string, string>>();   // przedmiot, modyfikator - najgorsze najpierw
                // E6/N1 (217, R16): przy AiMendsWornOnly tylko obite sztuki, ktore nosza ludzie (dopasowanie swieze - raz na dobe na partie, pamiec dla rezerwy)
                bool oldMem;   // F6 pkt 2: naprawa liczy swiezo (fresh) - nigdy z pamieci
                var used = WornOnlyOn ? UsedByMen(mp, byItem, true, out oldMem) : null;
                if (used != null)
                {
                    var arm = Armory(mp);
                    int skipped = 0;
                    foreach (var it in byItem)
                    {
                        var io = MBObjectManager.Instance.GetObject<ItemObject>(it.Key); if (io == null) continue;
                        foreach (var lot in MenWornLots(it.Value, FreeOf(arm, io, used), ref skipped))
                            if (Mendable(Mod(lot.Key))) for (int k = 0; k < lot.Value; k++) jobs.Add(new KeyValuePair<string, string>(it.Key, lot.Key));
                    }
                    _daySkipLoot += skipped;
                }
                else
                    foreach (var it in byItem) foreach (var m in it.Value) if (Mendable(Mod(m.Key))) for (int k = 0; k < m.Value; k++) jobs.Add(new KeyValuePair<string, string>(it.Key, m.Key));
                jobs.Sort((a, b) => { var ma = Mod(a.Value); var mb = Mod(b.Value); return (ma != null ? ma.PriceMultiplier : 1f).CompareTo(mb != null ? mb.PriceMultiplier : 1f); });
                // E8/W (249, R5, PLAN 8.8c): kolejka od najtanszej robocizny w tym miescie (przy rownej - najgorszy stan najpierw, jak dotad; sortowanie
                // stabilne). Material (srednio ok. 1/7 zaplaty) wyceniany dopiero przy planie na lawie - nie zmienia kolejnosci
                bool q = QueueOn;
                if (q && jobs.Count > 1)
                {
                    var unitBy = new Dictionary<string, int>();
                    foreach (var j in jobs)
                    {
                        string key = j.Key + "|" + j.Value;
                        if (unitBy.ContainsKey(key)) continue;
                        var io = MBObjectManager.Instance.GetObject<ItemObject>(j.Key);
                        unitBy[key] = io != null ? UnitCost(io, Mod(j.Value), st) : int.MaxValue;
                    }
                    var cheapFirst = jobs.OrderBy(j => unitBy[j.Key + "|" + j.Value]).ToList();
                    jobs.Clear(); jobs.AddRange(cheapFirst);
                }
                if (MendMaterial.MenAndLordsOn) { MendWithMaterial(mp, st, jobs, cap, perPiece, q); return; }   // poprawka po audycie TOWARY 3: material jak na lawie
                int done = 0, paid = 0, poorN = 0, afterPoor = 0;
                foreach (var j in jobs)
                {
                    if (done >= cap) break;
                    var item = MBObjectManager.Instance.GetObject<ItemObject>(j.Key); if (item == null) continue;
                    int unit = UnitCost(item, Mod(j.Value), st);
                    if (MenPurse.Get(mp) < unit) { if (!q) break; poorN++; continue; }   // E8/W (R5): sztuka, na ktora nie stac, czeka - nastepna probuje (dotad koniec na dzis)
                    MenPurse.Take(mp, unit);
                    _dayFolk += MendMaterial.PaySmiths(st, unit, unit);   // E6/N1: sama robocizna - do kiesy ludu (B1), reszta kasa; linia kas 169 w PaySmiths
                    AddWorn(mp.StringId, j.Key, j.Value, -1);
                    done++; paid += unit;
                    if (poorN > 0) afterPoor++;
                }
                SmithHours.Use(st.Town, done * perPiece);
                _dayMended += done; _dayPaid += paid;
                if (q) WMend(poorN, afterPoor);   // E8/W: linia doby (tylko licznik)
                P2Mend(mp, jobs.Count, done, 0, 0, q ? poorN > 0 : done < cap, done >= cap, q ? poorN : -1);   // E8/P2 (244): zalogi osobno i powod stania (tylko licznik)
            }
            catch (Exception e) { Log.Error("AiWear.MendInTown", e); }
        }

        /// <summary>
        /// E8/P2 (244, PLAN 8.7a, 8.8c): zlecenie napraw partii w tej dobie - naprawione sztuki osobno dla zalog i pozostalych partii oraz powod, dla ktorego
        /// reszta sztuk do naprawy (jobs - done - czekajace na material - nie robota kowala) stanela: sakiewka (petla konczy sie na pierwszej sztuce, na
        /// ktora nie stac - poor) albo godziny kowali miasta (capHit). Tylko liczniki linii "Zuzycie AI - pomiary (E8/P2)".
        /// </summary>
        private static void P2Mend(MobileParty mp, int jobs, int done, int wait, int noSmith, bool poor, bool capHit, int poorN = -1)
        {
            if (mp.IsGarrison) _p2MendG += done; else _p2MendL += done;
            if (jobs <= 0) return;
            _p2Visits++; _p2Jobs += jobs;
            if (wait > 0) _p2MatP++;
            int rest = jobs - done - wait - noSmith;
            if (rest <= 0) return;
            // E8/W (249): kolejka "czeka i idzie dalej" (AiMendQueueContinue) zna liczbe sztuk, ktore stanely na sakiewce (poorN >= 0) - reszta to godziny
            // kowali albo inne; stara petla (poorN = -1) konczy sie na pierwszej takiej sztuce, wiec cala reszta idzie na jej powod (jak dotad)
            if (poorN >= 0)
            {
                int pn = Math.Min(rest, poorN);
                if (pn > 0) { _p2PurseP++; _p2PurseN += pn; _p2PurseLeft += Math.Max(0, MenPurse.Get(mp)); }
                rest -= pn;
                if (rest <= 0) return;
                if (capHit) { _p2HoursP++; _p2HoursN += rest; } else _p2OtherN += rest;
                return;
            }
            if (poor) { _p2PurseP++; _p2PurseN += rest; _p2PurseLeft += Math.Max(0, MenPurse.Get(mp)); }
            else if (capHit) { _p2HoursP++; _p2HoursN += rest; }
            else _p2OtherN += rest;
        }

        /// <summary>
        /// Poprawka po audycie TOWARY 3 (krok 139 planu K13): kowale miasta naprawiaja AI ta sama regula co lawe gracza - MendMaterial.Order:
        /// material wedle stanu z polki miasta albo zapasu kowali, po cenie targu; brak materialu - ta sztuka czeka (kolejne sztuki tego samego
        /// przedmiotu i stanu tez), inne ida dalej; sakiewka ludzi placi robote + material w calych pensach do kasy miasta. Najpierw plan na
        /// kopii (polka i zapas bez zmian), potem Commit, zaplata i zapis stanu - tylko dla zaplanowanych sztuk. Godziny kowali - za gotowe.
        /// </summary>
        private static void MendWithMaterial(MobileParty mp, Settlement st, List<KeyValuePair<string, string>> jobs, int cap, float perPiece, bool q)
        {
            var o = new MendMaterial.Order(st);
            int purse = MenPurse.Get(mp);
            // ta sama sztuka i ten sam stan - jeden stos (kolejnosc: najgorsze najpierw, jak lista jobs); brak materialu - caly stos czeka
            // E8/W (249, R5): przy AiMendQueueContinue (q) lista jobs jest od najtanszej robocizny, a stos, na ktory nie stac, czeka - nastepne probuja
            var order = new List<KeyValuePair<string, string>>();
            var count = new Dictionary<string, int>();
            foreach (var j in jobs) { string key = j.Key + "|" + j.Value; int c; if (!count.TryGetValue(key, out c)) order.Add(j); count[key] = c + 1; }
            int poorPre = 0, afterPoor = 0; bool sawPoor = false;   // E8/W: stosy odlozone bez planowania materialu; sztuki naprawione po pierwszej bez pieniedzy
            foreach (var j in order)
            {
                if (o.Pieces >= cap) break;
                var item = MBObjectManager.Instance.GetObject<ItemObject>(j.Key); if (item == null) continue;
                var mod = Mod(j.Value);
                var ee = new EquipmentElement(item, mod);
                var need = MendMaterial.Needs(ee);
                int unit = UnitCost(item, mod, st), n = count[j.Key + "|" + j.Value];
                // E8/W: sama robocizna ponad reszte sakiewki - stos czeka bez planowania materialu (AddLot odrzucilby kazda sztuke tak samo: robocizna
                // + material <= sakiewka); sztuka bez receptury kowala idzie do AddLot (licznik "nie robota kowala")
                if (q && need != null && (long)o.Labor + unit + o.MatGold > purse) { poorPre += n; sawPoor = true; continue; }
                int poor0 = o.Poor, pcs0 = o.Pieces; bool sawBefore = sawPoor;
                o.AddLot(ee, need, unit, n, purse, cap);
                if (sawBefore) afterPoor += o.Pieces - pcs0;
                if (o.Poor > poor0)
                {
                    if (!q) break;   // sakiewka nie starcza - jak dotad koniec na dzis
                    sawPoor = true;  // E8/W (R5): ta sztuka czeka, nastepny stos probuje
                }
            }
            int done = 0, paid = 0;
            if (o.Pieces > 0)
            {
                o.Bench.Commit();
                foreach (var job in o.Jobs) { AddWorn(mp.StringId, job.El.Item.StringId, job.El.ItemModifier.StringId, -job.N); done += job.N; MetalTally(job); }
                paid = MenPurse.Take(mp, o.Total);
                _dayFolk += MendMaterial.PaySmiths(st, paid, o.Labor);   // E6/N1: robocizna do kiesy ludu (B1), material do kasy miasta; linia kas 169 w PaySmiths
                _dayMat += o.MatGold; _dayTowns++;
                for (int k = 0; k < MendMaterial.Kinds; k++) _dayKg[k] += o.Bench.UsedKg[k];
            }
            SmithHours.Use(st.Town, done * perPiece);
            _dayMended += done; _dayPaid += paid;
            _dayWait += o.Wait; _dayWaitMask |= o.WaitMask; _dayNoSmith += o.NoSmith;
            for (int k = 0; k < MendMaterial.Kinds; k++) _dayWaitBy[k] += o.WaitBy[k];
            if (q) WMend(o.Poor + poorPre, afterPoor);   // E8/W: linia doby (tylko licznik)
            P2Mend(mp, jobs.Count, done, o.Wait, o.NoSmith, o.Poor + poorPre > 0, o.Pieces >= cap, q ? o.Poor + poorPre : -1);   // E8/P2 (244): zalogi osobno i powod stania (tylko licznik)
        }

        /// <summary>E8/W (249): zlecenie napraw z kolejka "czeka i idzie dalej" - ile sztuk czekalo na sakiewke i ile naprawiono PO pierwszej takiej sztuce
        /// (stara petla konczyla sie na niej); tylko liczniki linii "Zuzycie AI (E8/W)".</summary>
        private static void WMend(int poor, int afterPoor)
        {
            if (poor > 0) { _wPoorP++; _wPoorN += poor; }
            _wAfterPoor += afterPoor;
        }

        private static void Day()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (_dayStamp == d) return;
            if (_dayStamp >= 0 && (_dayLoot + _dayWorn + _dayMended + _dayWait) > 0)   // _dayWait: tylko przy naprawach z materialem (dzien, w ktorym wszystko czekalo, tez ma linie)
            {
                int parties = _worn.Count, pieces = 0; foreach (var p in _worn.Values) foreach (var it in p.Values) foreach (var v in it.Values) pieces += v;
                Log.Info("Zuzycie AI: dzien " + _dayStamp + " - lup obity " + _dayLoot + " szt., zuzyte w bitwach " + _dayWorn + ", naprawione w miastach " + _dayMended
                         + " za " + _dayPaid + " z sakiewek ludzi; obitych razem " + pieces + " szt. w " + parties + " partiach"
                         + "; pominiete (lup nienoszony) " + (WornOnlyOn ? _daySkipLoot + " szt." : "- (AiMendsWornOnly WYL)")   // E6/N1 (217)
                         + ", robocizna do kiesy ludu " + _dayFolk + "."
                         + (MendMaterial.MenAndLordsOn ? MatDay() : ""));
            }
            if (_dayStamp >= 0 && P2On) P2Line();   // E8/P2 (244): przed zerowaniem licznikow doby - te same doby i ta sama etykieta co linia "Zuzycie AI"
            if (_dayStamp >= 0) WLine();            // E8/W (249): jw. - stara i nowa regula w tym samym biegu
            if (_dayStamp >= 0) MuLine();           // F6 pkt 2: pamiec dopasowania rezerwy napraw (tylko log)
            _dayLoot = _dayWorn = _dayMended = _dayPaid = 0; _dayStamp = d;
            _daySkipLoot = _dayFolk = 0;
            ClearMatDay();
            P2Clear();
            WClear();
            MuClear();
        }

        /// <summary>
        /// F6 pkt 2 (TRIAZ zb58 A3-7): linia "Rezerwa napraw AI - pamiec dopasowania (F6)" - przy zmianie doby ksiegi (etykieta jak "Zuzycie AI"), gdy bylo co
        /// liczyc. Ile rezerw wzielo dopasowanie z pamieci tej doby (tak bylo i przed poprawka), ile z pamieci starszej (to jest oszczednosc), ile liczylo od
        /// nowa; ile pamieci zgasila bitwa albo nowa pozycja ksiegi; probka 1/64: ile razy rezerwa z pamieci starszej rozni sie od rezerwy ze swiezego
        /// dopasowania i o ile zlota (rezerwa to kwota trzymana w sakiewce na kowala - roznica nie tworzy ani nie gubi zlota, przesuwa je w czasie).
        /// </summary>
        private static void MuLine()
        {
            try
            {
                if (_muToday + _muOld + _muCalc + _muFresh + _muDrop <= 0) return;
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                Log.Info("Rezerwa napraw AI - pamiec dopasowania (F6): dzien " + _dayStamp
                         + " | rezerwy z dopasowaniem z pamieci tej doby " + _muToday + ", z pamieci starszej (wazna 5-8 dob wedlug partii) " + _muOld + ", liczone od nowa " + _muCalc
                         + " | swieze dopasowanie przy naprawie w miescie " + _muFresh
                         + " | pamiec zgaszona (bitwa, nowa pozycja ksiegi obitych) " + _muDrop + ", partii z pamiecia " + _menUsed.Count
                         + " | probka 1/64 rezerw z pamieci starszej (bez lawy): " + _muProbes + ", inna niz ze swiezego dopasowania w " + _muProbeDiff
                         + " (swieza razem " + _muProbeGold.ToString(ci) + " zl, roznica bezwzgledna " + _muProbeAbs.ToString(ci) + " zl, netto pamiec - swieza "
                         + _muProbeNet.ToString(ci) + " zl) | potkniecia (sesja) " + _muStumbles + ".");
            }
            catch (Exception e) { if (++_muStumbles <= 3) Log.Error("AiWear.MuLine (potkniecie " + _muStumbles + ")", e); }
        }

        /// <summary>
        /// E8/W (249): linia "Zuzycie AI (E8/W)" - przy kazdej zmianie doby ksiegi (ta sama etykieta co "Zuzycie AI"). Stan kazdego z trzech kluczy
        /// i obie drogi w tym samym biegu (R10): oczekiwane zuzycie stara regula / nowa regula (przy Stage8MeasureLog), naprawy po pierwszej sztuce bez
        /// pieniedzy, zloto przesuniete z rezerwy napraw na kiese pana (przy wylaczonym kluczu: "zaplacilaby"), amunicja zdjeta (W CIENIU: 0 - "zeszloby"
        /// liczy linia pomiarow E8/P2). Wraki w ksiedze: obita sztuka schodzi teraz do wraku, a wraku kowale nie naprawiaja (paczka 158).
        /// </summary>
        private static void WLine()
        {
            try
            {
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                var s = Settings.Current;
                bool byInt = ByIntensityOn, q = QueueOn, ammo = AmmoOn, p2 = P2On;
                string pct = MBMath.ClampFloat(s.TroopWearPercent, 0f, 100f).ToString("0.##", ci);
                int wrecks = 0;
                var wreckId = new Dictionary<string, bool>();   // stan -> wrak (raz na linie; ksiega ma dziesiatki tysiecy pozycji, stanow kilkadziesiat)
                foreach (var p in _worn.Values)
                    foreach (var it in p.Values)
                        foreach (var m in it)
                        {
                            bool w;
                            if (!wreckId.TryGetValue(m.Key, out w)) wreckId[m.Key] = w = LootPrices.IsWreck(Mod(m.Key));
                            if (w) wrecks += m.Value;
                        }
                double ms = _wTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                Log.Info("Zuzycie AI (E8/W): dzien " + _dayStamp
                         + " | zuzycie " + (byInt ? "[WL wedlug natezenia bitwy]" : "[WYL stale " + pct + "%]") + ": partii po bitwie " + _wParties + ", straty " + _wLost
                         + " ludzi (polegli i ranni) z " + _wHealthy + " zdrowych na starcie, natezenie srednio " + (_wParties > 0 ? (_wIntSum / _wParties).ToString("0.00", ci) : "-")
                         + "; noszone sztuki " + (byInt || p2 ? _wUsed.ToString() : "-") + ", sprawne -> obite " + _dayWorn + ", obite o stopien nizej " + _wStep + " (w tym do wraku " + _wWreck
                         + "), trafienia w dno drabinki " + _wBottom + ", partie na starej regule po potknieciu " + _wFallback
                         + "; stare zuzycie " + pct + "% zuzyloby " + (p2 ? _wOldExp.ToString("0.0", ci) : "-") + " szt., nowa regula " + (byInt || p2 ? _wNewExp.ToString("0.0", ci) : "-")
                         + " szt. (oczekiwane, bez losowania)"
                         + " | naprawy " + (q ? "[WL od najtanszej, sztuka bez pieniedzy czeka]" : "[WYL od najgorszego stanu, koniec na pierwszej bez pieniedzy]") + ": partie z brakiem w sakiewce "
                         + _wPoorP + ", sztuki czekajace na sakiewke " + _wPoorN + ", naprawione po pierwszej sztuce bez pieniedzy " + _wAfterPoor + " szt."
                         + " | zakupy brakow " + (q ? "[WL rezerwa napraw zostaje]" : "[WYL sakiewka placi cala cene]") + ": zaplat z rezerwa " + _wBuys + ", kiesa pana zamiast rezerwy napraw "
                         + (q ? "zaplacila " : "zaplacilaby ") + _wBuyShift.ToString(ci) + " zl w " + _wBuyShiftN + " zaplatach"
                         + " | amunicja AI " + (ammo ? "[WL]" : "[CIEN - wylaczone]") + ": zeszlo " + (_wAmmo[0] + _wAmmo[1] + _wAmmo[2]) + " szt. (strzaly " + _wAmmo[0] + ", belty " + _wAmmo[1]
                         + ", miotane " + _wAmmo[2] + ") w " + _wAmmoParties + " partiach"
                         + " | wrakow w ksiedze " + wrecks
                         + " | amunicja AI pominieta w bitwach gracza z misja (zuzyl DTE w misji) " + _wAmmoField + " partii"   // A9 (14.10)
                         + " | koszt dopasowania " + ms.ToString("0.0", ci) + " ms | potkniecia (sesja) " + _wStumbles + ".");
            }
            catch (Exception e) { WStumble("WLine", e); }
        }

        /// <summary>
        /// E8/P2 (244): linia "Zuzycie AI - pomiary (E8/P2)" - przy kazdej zmianie doby ksiegi (takze w dobie bez linii "Zuzycie AI"). Zalogi i pozostale
        /// partie liczone osobno (suma = liczby linii "Zuzycie AI" - prog P2-2); powody stania napraw (PLAN 8.8c); "Amunicja AI: zeszloby" (PLAN 8.4a).
        /// </summary>
        private static void P2Line()
        {
            try
            {
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                float gone = _p2AmmoGone[0] + _p2AmmoGone[1] + _p2AmmoGone[2];
                Log.Info("Zuzycie AI - pomiary (E8/P2): dzien " + _dayStamp
                         + " | w tym zalogi: lup obity " + _p2LootG + ", zuzyte " + _p2WornG + ", naprawione " + _p2MendG
                         + " | pozostale partie: lup obity " + _p2LootL + ", zuzyte " + _p2WornL + ", naprawione " + _p2MendL
                         + " | razem (linia Zuzycie AI): lup obity " + _dayLoot + ", zuzyte " + _dayWorn + ", naprawione " + _dayMended
                         + " | naprawy: zlecen " + _p2Visits + " (partia x doba z robota), sztuk do naprawy " + _p2Jobs + "; stoja - sakiewka " + _p2PurseP + " partii / " + _p2PurseN
                         + " szt. (reszta sakiewek tych partii " + _p2PurseLeft.ToString(ci) + " zl), godziny kowali " + _p2HoursP + " partii / " + _p2HoursN
                         + " szt., material " + _p2MatP + " partii / " + _dayWait + " szt., nie robota kowala " + _dayNoSmith + " szt., inne " + _p2OtherN + " szt."
                         + " | Amunicja AI: zeszloby " + gone.ToString("0.0", ci) + " szt. (strzaly " + _p2AmmoGone[0].ToString("0.0", ci) + ", belty " + _p2AmmoGone[1].ToString("0.0", ci)
                         + ", miotane " + _p2AmmoGone[2].ToString("0.0", ci) + "; zwyciezcy " + _p2AmmoWin.ToString("0.0", ci) + ", pokonani " + _p2AmmoLose.ToString("0.0", ci)
                         + ") z " + (_p2AmmoUse[0] + _p2AmmoUse[1] + _p2AmmoUse[2]) + " w uzyciu (strzaly " + _p2AmmoUse[0] + ", belty " + _p2AmmoUse[1] + ", miotane " + _p2AmmoUse[2]
                         + ") w " + _p2AmmoParties + " partiach; natezenie bitew srednio " + (_p2IntN > 0 ? (_p2IntSum / _p2IntN).ToString("0.00", ci) : "-") + " (" + _p2IntN
                         + " partii po bitwie) | potkniecia (sesja) " + _p2Stumbles + ".");
            }
            catch (Exception e) { if (++_p2Stumbles <= 3) Log.Error("AiWear.P2Line (potkniecie " + _p2Stumbles + ")", e); }
        }

        /// <summary>Dopisek linii dnia: material napraw AI (poprawka po audycie TOWARY 3).</summary>
        private static string MatDay()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return " Material napraw z targu: " + _dayMat + " zl w " + _dayTowns + " zleceniach, zuzyto kg: metal " + _dayKg[MendMaterial.Metal].ToString("0.0", ci)
                   + ", drewno " + _dayKg[MendMaterial.Wood].ToString("0.0", ci) + ", skora " + _dayKg[MendMaterial.Leather].ToString("0.0", ci)
                   + ", plotno " + _dayKg[MendMaterial.Cloth].ToString("0.0", ci) + "; czeka na material " + _dayWait + " szt. (metal " + _dayWaitBy[MendMaterial.Metal]
                   + ", drewno " + _dayWaitBy[MendMaterial.Wood] + ", skora " + _dayWaitBy[MendMaterial.Leather] + ", plotno " + _dayWaitBy[MendMaterial.Cloth]
                   + "), nie robota kowala " + _dayNoSmith + "; " + MetalPerPiece(ci) + "; " + MendMaterial.Describe() + ".";
        }

        /// <summary>T3 (noc 08/09.10): metal (kg surowki wedlug receptury) na naprawiona sztuke - do porownania w autotescie (odniesienie 0.18 kg, doby 1-40).</summary>
        private static string MetalPerPiece(System.Globalization.CultureInfo ci)
        {
            var s = Settings.Current;
            string per = _dayMended > 0 ? (_dayKg[MendMaterial.Metal] / _dayMended).ToString("0.000", ci) : "-";
            return "metal na naprawiona sztuke " + per + " kg (T3 metal wedlug rodzaju " + (s != null && s.MendMetalByKind ? "WL" : "WYL")
                   + ": wedlug rodzaju " + _dayKgByKind.ToString("0.00", ci) + " kg / " + _dayByKind + " szt., stara regula " + _dayKgOldRule.ToString("0.00", ci)
                   + " kg / " + _dayOldRule + " szt., bez metalu w recepturze " + Math.Max(0, _dayMended - _dayByKind - _dayOldRule) + " szt.)";
        }

        /// <summary>T3 (poprawka po recenzji): kg metalu i sztuki osobno dla nowej i starej reguly; tylko sztuki z metalem w recepturze. O(1) na stos.</summary>
        private static void MetalTally(MendMaterial.Job job)
        {
            try
            {
                var c = ArmsPricing.CostOf(job.El.Item);
                if (c == null || c.MetalKg <= 0.001f) return;
                if (MendMaterial.MetalByKind(job.El.Item)) { _dayByKind += job.N; _dayKgByKind += job.MetalKg; }
                else { _dayOldRule += job.N; _dayKgOldRule += job.MetalKg; }
            }
            catch (Exception e) { Log.Error("AiWear.MetalTally", e); }
        }
    }
}
