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
    /// Army175DothrakiHorseGuard, dziala tylko z Army175DothrakiRide i Army175Composition, bo to 2.1 robi z tego problem).
    /// ROT (ROTTroopRecruiter.ExchangeClanTroops) zamienia kazdego zolnierza spoza puli rodu na czlowieka z puli tego samego
    /// tieru i BRAKUJACEJ formacji. Po 2.1 Dothrakom brakuje tylko jazdy i konnych lucznikow, wiec obcy w partii Dothrakow
    /// (jeniec, najemnik, ochotnik innej kultury) stawalby sie konnym bez konia - kon z niczego. Zasada z 30.08 ("na konnego
    /// tylko z koniem") zastosowana do tych zamian: dla kazdego konnego, ktory przybyl z pieszego, kon najpierw WOLNY ze
    /// zbrojowni (kon zostaje na miejscu), potem z taboru (Stables.Consume -> zbrojownia, jak przy awansie); dla brakujacych
    /// konny wraca na PIESZEGO TEGO SAMEGO TIERU z dopisanej puli (CS Army175.RotPoolKeepFoot): t1 khuzait_nomad, t2 khuzait_footman,
    /// t3 spearman / hunter, t4 spear_infantry / archer, t5 darkhan / marksman (jazda -> linia wloczni, konny lucznik -> linia
    /// lukow). Ten sam tier, wiec ROT nie przelicza zlota. t6 nie ma pieszego - zostaje konny i idzie do licznika.
    /// Warunek: pula rodu (ROT Settings) zawiera khuzait_nomad (korzen drzewa wsi Dothrakow, tylko w szablonie kultury) - nie
    /// "tribal_warrior" (ten jest tez w pulach Daenerys i Joraha). Inne krolestwa - bez zmian (najpierw pomiar rot_plus).
    /// Styk z 171: w tej bazie echo werbunku ROT daje zastepcy przez RecruitKit pelny komplet wzorca z koniem (sciezka "bez
    /// zapisu") - straz uzna takiego konia za wolny; 171 to zamyka (po scaleniu straz widzi tylko prawdziwe konie).
    /// </summary>
    internal static class RotHorseGuard
    {
        private static MethodInfo _settings;
        private static bool _bound;
        private static CharacterObject _nomad;
        private static Dictionary<int, CharacterObject[]> _foot;
        private static bool _footBuilt;
        private static int _stumbles;

        internal static bool Bound { get { return _bound; } }

        internal static bool On
        {
            get
            {
                var s = Settings.Current;
                return _bound && s != null && s.Army175DothrakiHorseGuard && s.Army175DothrakiRide && s.Army175Composition && s.CavalryNeedsMounts;
            }
        }

        internal static void Reset() { _nomad = null; _foot = null; _footBuilt = false; _stumbles = 0; }

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
                if (_nomad == null) return;
                var pool = Pool(rot, owner, settlement);
                if (pool == null || !pool.Contains(_nomad)) return;
                var party = mp.Party;
                var fac = mp.MapFaction;
                int pending = plus;   // konni z tej zamiany, ktorzy jeszcze nie maja konia (nie zajmuja wolnych)
                foreach (var a in added)
                {
                    var val = a.Key; int n = a.Value;
                    if (val == null || !val.IsMounted || n <= 0) continue;
                    var cat = Stables.RequiredMountFor(party, val);
                    int fromFree = Stables.AiFix(party) ? Math.Min(n, Stables.FreeArmory(mp, cat, pending)) : 0;
                    int left = n - fromFree;
                    int fromRoster = left > 0 ? Math.Min(left, Stables.CountInRoster(party, cat)) : 0;
                    int lost = 0;
                    if (fromRoster > 0)
                    {
                        // jak przy awansie (175.1): kon z taboru do zbrojowni nowego jezdzca; bez AiUpgradeHorseToArmory - przepada jak przy awansie
                        var taken = new List<KeyValuePair<EquipmentElement, int>>();
                        Stables.Consume(party, cat, fromRoster, taken);
                        int mn, mpos;
                        Stables.BankToArmory(mp, taken, Stables.AiFix(party), out lost, out mn, out mpos);
                        HorseCensus.Add(fac, HorseCensus.CKonModUjemny, mn);
                        HorseCensus.Add(fac, HorseCensus.CKonModDodatni, mpos);
                    }
                    int s = left - fromRoster;
                    int toFoot = 0, keep = 0;
                    if (s > 0)
                    {
                        var foot = FootFor(val);
                        int idx = roster.FindIndexOfTroop(val);
                        int healthy = 0;
                        if (idx >= 0) { var el = roster.GetElementCopyAtIndex(idx); healthy = el.Number - el.WoundedNumber; }
                        toFoot = foot != null ? Math.Min(s, Math.Max(0, healthy)) : 0;
                        if (toFoot > 0)
                        {
                            roster.AddToCounts(val, -toFoot);
                            roster.AddToCounts(foot, toFoot);
                        }
                        keep = s - toFoot;   // t6 (brak pieszego) albo brak zdrowych w stosie - zostaje konny bez konia
                    }
                    pending -= n;
                    HorseCensus.Add(fac, HorseCensus.CStrazZbrojownia, fromFree);
                    HorseCensus.Add(fac, HorseCensus.CStrazTabor, fromRoster);
                    HorseCensus.Add(fac, HorseCensus.CStrazPrzepadlo, lost);
                    HorseCensus.Add(fac, HorseCensus.CStrazPieszy, toFoot);
                    HorseCensus.Add(fac, HorseCensus.CStrazT6, keep);
                }
            }
            catch (Exception e) { _stumbles++; if (_stumbles <= 3) Log.Error("RotHorseGuard.Apply", e); }
        }
    }
}
