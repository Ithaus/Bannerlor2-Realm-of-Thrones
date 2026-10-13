using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// STRAZ KONIA PRZY ZAMIANACH ROT U DOTHRAKOW - paczka 175.1b (decyzja Jeffa 09.10 pkt 3, projekt rozdz. 2.1; wylacznik
    /// Army175DothrakiHorseGuard). ROT (ROTTroopRecruiter.ExchangeClanTroops) zamienia kazdego zolnierza spoza puli rodu na czlowieka
    /// z puli tego samego tieru i BRAKUJACEJ formacji. Po 2.1 Dothrakom brakuje tylko jazdy i konnych lucznikow, wiec obcy w partii
    /// Dothrakow (jeniec, najemnik, ochotnik innej kultury) stawalby sie konnym bez konia - kon z niczego. Zasada z 30.08 ("na konnego
    /// tylko z koniem") zastosowana do tych zamian: dla kazdego konnego, ktory przybyl z pieszego, kon z taboru (Stables.Consume ->
    /// zbrojownia, jak przy awansie), a z AiFreeArmoryHorsesFirst najpierw WOLNY tej kategorii ze zbrojowni (kon zostaje na miejscu);
    /// dla brakujacych konny wraca na PIESZEGO TEGO SAMEGO TIERU z dopisanej puli (CS Army175.RotPoolKeepFoot): t1 khuzait_nomad,
    /// t2 khuzait_footman, t3 spearman / hunter, t4 spear_infantry / archer, t5 darkhan / marksman (jazda -> linia wloczni, konny
    /// lucznik -> linia lukow). Ten sam tier, wiec ROT nie przelicza zlota. t6 nie ma pieszego - zostaje konny i idzie do licznika.
    /// Warunek: pula rodu (ROT Settings) zawiera khuzait_nomad (korzen drzewa wsi Dothrakow, tylko w szablonie kultury) - nie
    /// "tribal_warrior" (ten jest tez w pulach Daenerys i Joraha). Inne krolestwa - bez zmian (najpierw pomiar rot_plus).
    /// PRZEGLAD 175: (1) straz dziala wedlug ZNACZNIKA CrashScribe (Army175.DothrakiPoolActive - CS ustawia go przy wczytaniu, gdy 2.1
    /// weszlo), nie wedlug biezacych kluczy Army175DothrakiRide / Army175Composition - CS stosuje sklad tylko przy wczytaniu, wiec
    /// wylaczenie w trakcie gry gasilo straz od razu, a konne drzewo i pula zostawaly do konca sesji (obcy - konni bez konia); bez CS
    /// 175 (bieg B0) straz spi. (2) Pieszy musi byc w puli rodu: bez dopisku CS (postfiks puli niewpiety) ROT nastepnej doby zamienilby
    /// go z powrotem na konnego bez konia - wtedy konny zostaje (licznik "pieszego brak w puli", ostrzezenie raz w logu).
    /// Styk z 171: w tej bazie echo werbunku ROT daje zastepcy przez RecruitKit pelny komplet wzorca z koniem (sciezka "bez
    /// zapisu") - dlatego wolne konie (175.1-wolne) sa domyslnie wylaczone; 171 to zamyka (po scaleniu straz widzi tylko prawdziwe konie).
    /// E6/K1 (211, TroopSwapNeedsHorse, AllOn): ta sama straz w KAZDEJ krainie - konny z zamiany ROT dostaje konia jak przy awansie (wolny w zbrojowni przy
    /// AiFreeArmoryHorsesFirst, potem tabor), inaczej staje sie pieszym tego samego tieru z puli rodu (ten sam rodzaj i kultura najpierw - pieszy z puli,
    /// wiec ROT go nie zamienia z powrotem; ten sam tier - ROT nie przelicza zlota); bez pieszego tego tieru w puli zostaje konny bez konia (licznik K1).
    /// </summary>
    internal static class RotHorseGuard
    {
        private static MethodInfo _settings;
        private static bool _bound;
        private static CharacterObject _nomad;
        private static Dictionary<int, CharacterObject[]> _foot;
        private static HashSet<CharacterObject> _elite;
        private static bool _footBuilt, _poolWarned;
        private static int _stumbles;

        internal static bool Bound { get { return _bound; } }

        internal static bool On
        {
            get
            {
                var s = Settings.Current;
                return _bound && s != null && s.Army175DothrakiHorseGuard && s.CavalryNeedsMounts && CsMarks.DothrakiPoolActive;
            }
        }

        /// <summary>E6/K1 (211, TroopSwapNeedsHorse): straz konia przy zamianach ROT w KAZDEJ krainie (pieszy tego samego tieru z puli rodu); Dothrakowie - jak dotad (On).</summary>
        internal static bool AllOn
        {
            get
            {
                var s = Settings.Current;
                return _bound && s != null && s.TroopSwapNeedsHorse && s.CavalryNeedsMounts;
            }
        }

        internal static void Reset() { _nomad = null; _foot = null; _elite = null; _footBuilt = false; _poolWarned = false; _stumbles = 0; }

        internal static void Bind(Type rot)
        {
            try
            {
                _settings = null;
                foreach (var m in AccessTools.GetDeclaredMethods(rot))
                {
                    if (m.Name != "Settings") continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 2 && ps[0].ParameterType == typeof(Hero) && ps[1].ParameterType == typeof(Settlement)) { _settings = m; break; }
                }
                _bound = _settings != null;
            }
            catch { _bound = false; }
        }

        /// <summary>Pula rodu z ROT (ta sama, z ktorej ROT wybral zastepce): Settings(owner) albo Settings(null, osada).</summary>
        private static IList<CharacterObject> Pool(object rot, Hero owner, Settlement settlement)
        {
            if (rot == null || _settings == null) return null;
            object ps = settlement != null ? _settings.Invoke(rot, new object[] { null, settlement }) : _settings.Invoke(rot, new object[] { owner, null });
            if (ps == null) return null;
            var t = Traverse.Create(ps);
            object pt = null;
            try { pt = t.Property("PartyTemplate").GetValue(); } catch { }
            if (pt == null) try { pt = t.Field("PartyTemplate").GetValue(); } catch { }
            return pt as IList<CharacterObject>;
        }

        private static CharacterObject Ch(string id)
        {
            try { return MBObjectManager.Instance.GetObject<CharacterObject>(id); } catch { return null; }
        }

        private static void BuildFoot()
        {
            _footBuilt = true;
            _nomad = Ch("khuzait_nomad");
            _foot = new Dictionary<int, CharacterObject[]>
            {
                { 1, new[] { _nomad, _nomad } },
                { 2, new[] { Ch("khuzait_footman"), Ch("khuzait_footman") } },
                { 3, new[] { Ch("khuzait_spearman"), Ch("khuzait_hunter") } },
                { 4, new[] { Ch("khuzait_spear_infantry"), Ch("khuzait_archer") } },
                { 5, new[] { Ch("khuzait_darkhan"), Ch("khuzait_marksman") } },
            };
            // linia szlachty Dothrakow (drzewo EliteBasicTroop kultury) - straz zamienia ja na pieszego linii wsi; licznik osobno (przeglad 175)
            _elite = new HashSet<CharacterObject>();
            try
            {
                var cu = MBObjectManager.Instance.GetObject<CultureObject>("khuzait");
                var st = new Stack<CharacterObject>();
                if (cu != null && cu.EliteBasicTroop != null) st.Push(cu.EliteBasicTroop);
                while (st.Count > 0)
                {
                    var c = st.Pop();
                    if (c == null || !_elite.Add(c)) continue;
                    var ups = c.UpgradeTargets;
                    if (ups != null) foreach (var u in ups) if (u != null) st.Push(u);
                }
            }
            catch { }
        }

        /// <summary>Pieszy Dothrak tego samego tieru: jazda -> linia wloczni, konny lucznik -> linia lukow (null: brak, np. t6).</summary>
        private static CharacterObject FootFor(CharacterObject rider)
        {
            CharacterObject[] pair;
            if (_foot == null || !_foot.TryGetValue(rider.Tier, out pair) || pair == null) return null;
            var f = rider.IsRanged ? pair[1] : pair[0];
            if (f == null) f = rider.IsRanged ? pair[0] : pair[1];
            return f != null && f.Tier == rider.Tier ? f : null;
        }

        /// <summary>Postfiks ExchangeClanTroops (z HorseCensus): added = kto przybyl (bez zamienianego), plus = konnych z pieszego.</summary>
        internal static void Apply(object rot, Hero owner, TroopRoster roster, CharacterObject troop, Settlement settlement, MobileParty mp,
                                   List<KeyValuePair<CharacterObject, int>> added, int plus)
        {
            try
            {
                if (troop == null || troop.IsMounted || mp == null || roster == null || added == null || plus <= 0) return;
                if (!_footBuilt) BuildFoot();
                var pool = Pool(rot, owner, settlement);
                if (pool == null) return;
                // Dothrakowie (175.1b, On + khuzait_nomad w puli) - mapa pieszych jak dotad; E6/K1 (AllOn) - kazda kraina: pieszy z puli rodu
                bool dothraki = On && _nomad != null && pool.Contains(_nomad);
                if (!dothraki && !AllOn) return;
                var party = mp.Party;
                string key = HorseCensus.KeyOfParty(party);
                bool fix = Stables.AiFix(party), ff = Stables.FreeFirst(party);
                // konni z tej zamiany, ktorzy jeszcze nie maja konia - nie zajmuja wolnych (ujemna poprawka do Stables.Balance)
                var pending = new Dictionary<CharacterObject, int>();
                foreach (var a in added)
                {
                    if (a.Key == null || !a.Key.IsMounted || a.Value <= 0) continue;
                    int p0; pending.TryGetValue(a.Key, out p0); pending[a.Key] = p0 - a.Value;
                }
                foreach (var a in added)
                {
                    var val = a.Key; int n = a.Value;
                    if (val == null || !val.IsMounted || n <= 0) continue;
                    var cat = Stables.RequiredMountFor(party, val);
                    int fromFree = ff ? Math.Min(n, Stables.FreeArmory(mp, cat, pending)) : 0;
                    int left = n - fromFree;
                    int fromRoster = left > 0 ? Math.Min(left, Stables.CountInRoster(party, cat)) : 0;
                    // E8/H (271): jezdziec = kon + uprzaz - z tych, dla ktorych jest kon, tylu nie ma pary (wolna uprzaz zbrojowni + tabor); ich kon zostaje,
                    // gdzie byl (najpierw tabor, potem wolny w zbrojowni), a oni ida droga konnych bez konia: pieszy tego samego tieru z puli rodu
                    int noPair = SaddleLaw.RotNoPair(party, val, fromFree + fromRoster, pending);
                    if (noPair > 0) { int cr = Math.Min(noPair, fromRoster); fromRoster -= cr; fromFree -= noPair - cr; }
                    int lost = 0;
                    if (fromRoster > 0)
                    {
                        // jak przy awansie (175.1): kon z taboru do zbrojowni nowego jezdzca; bez AiUpgradeHorseToArmory - przepada jak przy awansie
                        var taken = new List<KeyValuePair<EquipmentElement, int>>();
                        Stables.Consume(party, cat, fromRoster, taken);
                        int mn, mpos;
                        Stables.BankToArmory(mp, taken, fix, false, out lost, out mn, out mpos);   // sklad9-p: licznik straznika ROT, nie "konie za awans"
                        HorseCensus.Add(key, HorseCensus.CKonModUjemny, mn);
                        HorseCensus.Add(key, HorseCensus.CKonModDodatni, mpos);
                    }
                    int s = n - fromFree - fromRoster;   // E8/H: left - fromRoster jak dotad, plus konni bez pary (noPair)
                    int toFoot = 0, keep = 0, noPool = 0;
                    if (s > 0)
                    {
                        var foot = dothraki ? FootFor(val) : PoolFoot(pool, val);
                        if (foot != null && !pool.Contains(foot))
                        {
                            // bez dopisku CS ROT jutro zamienilby pieszego z powrotem na konnego bez konia - konny zostaje
                            noPool = s; foot = null;
                            if (!_poolWarned)
                            {
                                _poolWarned = true;
                                Log.Info("RotHorseGuard (175.1b): OSTRZEZENIE - pieszego " + FootId(val) + " nie ma w puli ROT rodu (dopisek CS RotPoolKeepFoot nie zadzialal?) - jezdzcy z zamian zostaja konni bez konia (licznik 'pieszego brak w puli').");
                            }
                        }
                        int idx = roster.FindIndexOfTroop(val);
                        int healthy = 0;
                        if (idx >= 0) { var el = roster.GetElementCopyAtIndex(idx); healthy = el.Number - el.WoundedNumber; }
                        toFoot = foot != null ? Math.Min(s, Math.Max(0, healthy)) : 0;
                        if (toFoot > 0)
                        {
                            roster.AddToCounts(val, -toFoot);
                            roster.AddToCounts(foot, toFoot);
                            if (foot.StringId == "khuzait_footman") HorseCensus.Add(key, HorseCensus.CStrazPieszyFootman, toFoot);   // footman rosnie przez straz, nie przez blad 2.1
                            if (_elite != null && _elite.Contains(val)) HorseCensus.Add(key, HorseCensus.CStrazPieszyElita, toFoot);   // jezdziec linii szlachty -> pieszy linii wsi
                        }
                        keep = s - toFoot - noPool;   // t6 (brak pieszego) albo brak zdrowych w stosie - zostaje konny bez konia
                        if (keep < 0) keep = 0;
                    }
                    int p1; pending.TryGetValue(val, out p1); pending[val] = p1 + n;   // ci juz rozliczeni
                    SaddleLaw.RotSeat(party, val, fromFree + fromRoster, pending);   // E8/H (271): uprzaz z taboru do zbrojowni pod nowych jezdzcow z koniem
                    HorseCensus.Add(key, HorseCensus.CStrazZbrojownia, fromFree);
                    HorseCensus.Add(key, HorseCensus.CStrazTabor, fromRoster);
                    HorseCensus.Add(key, HorseCensus.CStrazPrzepadlo, lost);
                    HorseCensus.Add(key, HorseCensus.CStrazPieszy, toFoot);
                    HorseCensus.Add(key, HorseCensus.CStrazT6, keep);
                    HorseCensus.Add(key, HorseCensus.CStrazBrakPuli, noPool);
                    if (!dothraki) HorseFate.NoteRot(fromFree + fromRoster, toFoot, keep + noPool);   // E6/K1: zamiany ROT poza Dothrakami (linia K1)
                }
            }
            catch (Exception e) { _stumbles++; if (_stumbles <= 3) Log.Error("RotHorseGuard.Apply", e); }
        }

        /// <summary>E6/K1 (211): pieszy z puli rodu tego samego tieru - ten sam rodzaj (strzelec/wojownik) i kultura najpierw (null: brak).</summary>
        private static CharacterObject PoolFoot(IList<CharacterObject> pool, CharacterObject rider)
        {
            CharacterObject best = null; int bs = -1;
            foreach (var c in pool)
            {
                if (c == null || c.IsHero || c.IsMounted || c.Tier != rider.Tier) continue;
                int sc = (c.IsRanged == rider.IsRanged ? 2 : 0) + (c.Culture == rider.Culture ? 1 : 0);
                if (sc > bs) { bs = sc; best = c; }
            }
            return best;
        }

        private static string FootId(CharacterObject rider)
        {
            var f = FootFor(rider);
            return f != null ? f.StringId : "?";
        }
    }
}
