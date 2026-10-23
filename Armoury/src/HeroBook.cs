using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// m110-stan krok 2 (Jeff 23.10 ok. 15:50 "tak": sprzet, ktory nosisz ty i towarzysze, chroni wedlug dokladnego HP). KSIEGA DOKLADNEJ WYTRZYMALOSCI
    /// SPRZETU TOWARZYSZY (bohaterow w druzynie gracza) i ich ZUZYCIE Z TRAFIEN - te same reguly co u gracza: zbroja tam, gdzie pada cios (surowy cios przez pule
    /// ArmorPool), bron od celnych uderzen i strzalow, tarcza od blokow, uprzaz gdy obrywa kon (FieldCraft pisze do ksiegi misji agenta - HeroHp); po wygranej
    /// bitwie ArmouryBehavior.WearAfter (ta sama arytmetyka co ApplyWearPerSlot gracza), stan wedlug progow 80/40/10 z d = ksiega - (100 - srodek stanu
    /// poczatku), zuzycie nigdy nie poprawia. Kleska albo poczatek bitwy - ksiegi ciosow przepadaja (jak u gracza, ArenaNoWear).
    /// Wpis: slot|ksiega|stan poczatku|przedmiot|stan teraz - jak piecio-polowy wpis gracza; tozsamosc sztuki = przedmiot + obecny modyfikator: kazda zmiana
    /// sztuki w gniezdzie (inny przedmiot albo inny stan) zaczyna ksiege od stanu (dokladne HP zdjetej sztuki przepada - stos w sakwach jest wedlug stanu).
    /// Zapis: SaveText "arm_condition_heroes" (klucz CompanionGearWear; NIE albo brak ksiag - klucza w zapisie nie ma); ksiega zostaje przy bohaterze takze poza
    /// druzyna (recenzja U5: dotad przyciecie do druzyny przy zapisie - wynik zalezal od tego, czy po drodze byl zapis), przy zapisie usuwane tylko ksiegi
    /// bohaterow, ktorzy nie zyja; zuzycie i ochrona z ksiegi - tylko w druzynie gracza. Stary zapis - ksiag brak (start od stanu wedlug modyfikatora).
    /// Ciosy bitwy ida tylko w sztuke noszona w bitwie (recenzja R1: tozsamosc z wejscia agenta; inna sztuka w gniezdzie po bitwie - ciosy przepadaja, liczone).
    /// Bledy liczone per sztuka i wiersz, bez globalnego wylacznika.
    /// </summary>
    internal static class HeroBook
    {
        private const string P = "Zuzycie towarzysza (m110): ";
        private const string PS = "Zuzycie towarzyszy (m110): ";
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        private static readonly Dictionary<string, List<string>> _books = new Dictionary<string, List<string>>();   // id bohatera -> wpisy
        private static readonly Dictionary<Hero, float[]> _hits = new Dictionary<Hero, float[]>();                // ksiega ciosow do najblizszej wygranej
        private static readonly Dictionary<Hero, string[]> _worn = new Dictionary<Hero, string[]>();              // recenzja R1: sztuka (przedmiot|modyfikator), do ktorej naleza ciosy gniazda
        private static float _lostPts;     // recenzja R1: ciosy sztuki zmienionej miedzy misjami tego samego zdarzenia (do linii sumy po wygranej)
        private static int _lostN;
        private static int _bad, _stumbles;

        /// <summary>Zuzycie sprzetu towarzyszy: klucz CompanionGearWear, stany (GearFourStates) i zuzycie (WearEnabled).</summary>
        internal static bool On
        {
            get { var s = Settings.Current; return s != null && s.CompanionGearWear && s.WearEnabled && GearState.On; }
        }

        internal static void Reset()
        {
            _books.Clear(); _hits.Clear(); _worn.Clear(); _lostPts = 0f; _lostN = 0; _bad = 0;
        }

        private static string ModId(EquipmentElement el) { return el.ItemModifier != null ? el.ItemModifier.StringId : ""; }

        /// <summary>Tozsamosc sztuki w gniezdzie: przedmiot|modyfikator (jak pola 3 i 4 wpisu ksiegi) - pusty gniazdo "|".</summary>
        internal static string Ident(EquipmentElement el) { return (el.Item != null ? el.Item.StringId : "") + "|" + ModId(el); }

        private static int Find(List<string> list, int slot)
        {
            if (list == null) return -1;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                int bar = c.IndexOf('|');
                int sl;
                if (bar > 0 && int.TryParse(c.Substring(0, bar), out sl) && sl == slot) return i;
            }
            return -1;
        }

        /// <summary>Dokladna wytrzymalosc d (procent puli) sztuki towarzysza w gniezdzie: wpis TEJ sztuki (przedmiot i obecny modyfikator) bity ponizej 100;
        /// false - brak wpisu, wpis innej sztuki albo swiezy (ochrona wedlug stanu). Klucz CompanionGearWear NIE - false (jak 6f3e0314). Tylko odczyt.</summary>
        internal static bool ExactD(Hero hero, int slot, EquipmentElement el, out float d)
        {
            d = 100f;
            var s = Settings.Current;
            if (hero == null || el.Item == null || s == null || !s.CompanionGearWear || !GearState.On) return false;
            List<string> list;
            if (!_books.TryGetValue(hero.StringId ?? "", out list)) return false;
            int i = Find(list, slot);
            if (i < 0) return false;
            var p = list[i].Split('|');
            float cond;
            if (p.Length < 5 || p[3] != el.Item.StringId || p[4] != ModId(el)) return false;
            if (!float.TryParse(p[1], NumberStyles.Float, CI, out cond) || cond >= 100f) return false;
            d = ArmouryBehavior.EffectiveCondition(cond, p[2]);
            return true;
        }

        /// <summary>Koniec misji (HeroHp): ciosy agenta towarzysza do ksiegi ciosow bohatera - tylko gniazda zgodne z jego BattleEquipment; ids - sztuka noszona w
        /// bitwie (recenzja R1): inna sztuka w gniezdzie niz w poprzedniej misji tego samego zdarzenia - ciosy poprzedniej przepadaja (liczone).</summary>
        internal static void AddHits(Hero h, float[] hits, bool[] match, string[] ids)
        {
            if (h == null || hits == null) return;
            float[] a;
            if (!_hits.TryGetValue(h, out a)) { a = new float[12]; _hits[h] = a; }
            string[] w;
            if (!_worn.TryGetValue(h, out w)) { w = new string[12]; _worn[h] = w; }
            for (int i = 0; i < 12 && i < hits.Length; i++)
            {
                if (match != null && (i >= match.Length || !match[i])) continue;
                string id = ids != null && i < ids.Length ? ids[i] : null;
                if (hits[i] > 0.01f && id != null)
                {
                    if (a[i] > 0.01f && w[i] != null && w[i] != id) { _lostPts += a[i]; _lostN++; a[i] = 0f; }
                    w[i] = id;
                }
                a[i] += hits[i];
            }
        }

        /// <summary>Ksiegi ciosow przepadaja (poczatek bitwy gracza, bitwa przegrana albo opuszczona) - jak FieldCraft.DropLedger gracza.</summary>
        internal static void DropLedgers(string why)
        {
            try
            {
                if (_hits.Count == 0) return;
                float sum = 0f;
                int n = 0;
                foreach (var kv in _hits)
                {
                    float s0 = 0f;
                    foreach (var v in kv.Value) s0 += v;
                    if (s0 > 0.01f) { sum += s0; n++; }
                }
                _hits.Clear(); _worn.Clear(); _lostPts = 0f; _lostN = 0;
                if (sum > 0.01f) Log.Info(PS + why + " - " + sum.ToString("0.0", CI) + " pkt z ksiag ciosow przepada (bohaterow " + n + ").");
            }
            catch (Exception e) { Stumble("HeroBook.DropLedgers", e); }
        }

        /// <summary>Po wygranej bitwie (ArmouryBehavior.OnMapEventEnded, po zuzyciu gracza): ksiega ciosow kazdego towarzysza na jego sztuki - ta sama arytmetyka i te
        /// same progi co gracz; nowy stan - modyfikator stanu grupy (GearState.ModForGroup), komunikat dla gracza i linia w logu.</summary>
        internal static void ApplyBattle()
        {
            if (_hits.Count == 0) return;
            int heroes = 0, skipped = 0, newState = 0, errors = 0;
            float total = 0f;
            int changed = _lostN;          // recenzja R1: sztuka zmieniona po bitwie (ekran lupu gry przed koncem zdarzenia) albo miedzy misjami - ciosy przepadaja
            float changedPts = _lostPts;
            try
            {
                var cfg = Settings.Current;
                var mp = MobileParty.MainParty;
                var dead = Campaign.Current != null ? Campaign.Current.DeadBattleEquipment : null;
                foreach (var kv in _hits)
                {
                    var h = kv.Key;
                    var a = kv.Value;
                    float hs = 0f;
                    foreach (var v in a) hs += v;
                    if (hs <= 0.01f) continue;
                    total += hs;
                    var eq = h != null ? h.BattleEquipment : null;
                    if (h == null || !h.IsAlive || h == Hero.MainHero || h.PartyBelongedTo != mp || eq == null || ReferenceEquals(eq, dead)) { skipped++; continue; }
                    heroes++;
                    string hid = h.StringId ?? "";
                    List<string> list;
                    if (!_books.TryGetValue(hid, out list)) { list = new List<string>(); _books[hid] = list; }
                    var segs = new List<string>();
                    string[] worn;
                    _worn.TryGetValue(h, out worn);
                    for (int slot = 0; slot < 12; slot++)
                    {
                        float amount = a[slot];
                        if (amount <= 0.01f) continue;
                        try
                        {
                            var el = eq[slot];
                            // recenzja R1: ciosy tylko w sztuke noszona w bitwie - inna sztuka w gniezdzie (zalozona na ekranie lupu przed koncem zdarzenia, zdjeta) nie
                            // zuzywa sie cudzymi ciosami; ciosy przepadaja, liczone w linii sumy
                            if (worn != null && worn[slot] != null && worn[slot] != Ident(el)) { changed++; changedPts += amount; continue; }
                            if (el.Item == null || ArmouryBehavior.NoWear(el.Item) || !GearState.Handles(el.Item)) continue;
                            var group = el.Item.ItemComponent.ItemModifierGroup;
                            string id = el.Item.StringId, mod = ModId(el);
                            int idx = Find(list, slot);
                            var p = idx >= 0 ? list[idx].Split('|') : null;
                            float a0 = 100f;
                            string orig = mod;
                            if (p != null && p.Length >= 5 && p[3] == id && p[4] == mod)
                            {
                                if (!float.TryParse(p[1], NumberStyles.Float, CI, out a0)) a0 = 100f;
                                orig = p[2];
                            }
                            float b = ArmouryBehavior.WearAfter(el.Item, a0, amount);
                            int so = GearState.OfId(orig);
                            if (so < 0) so = GearState.Sound;
                            int was = GearState.Of(el.ItemModifier);
                            if (was < 0) was = GearState.Sound;
                            string how = el.Item.HasArmorComponent ? "pula " + ArmouryBehavior.ArmorPool(el.Item)
                                                                    : "solidnosc " + ArmouryBehavior.Sturdiness(el.Item).ToString("0.000", CI);
                            if (b <= 0f && cfg.BreakAtZeroCondition)
                            {
                                // jak u gracza (BreakAtZeroCondition, u Jeffa wylaczone): sztuka pekla i przepada
                                eq[slot] = new EquipmentElement(null);
                                if (idx >= 0) list.RemoveAt(idx);
                                Log.Player(h.Name + "'s " + el.Item.Name + " gave out in the fight and is beyond saving.", true);
                                segs.Add("slot " + slot + " " + id + " ksiega " + a0.ToString("0.00", CI) + " -> 0.00 (trafienia " + amount.ToString("0.00", CI) + " pkt, " + how
                                         + "; pekla)");
                                continue;
                            }
                            float d = ArmouryBehavior.EffectiveCondition(b, orig);
                            int st = GearState.OfCondition(d);
                            int now = was;
                            string last = mod;
                            if (st > was)
                            {
                                var gs = GearState.ModForGroup(group, st);
                                if (gs != null && gs != el.ItemModifier)
                                {
                                    var e2 = el;
                                    e2.SetModifier(gs);
                                    eq[slot] = e2;
                                    last = gs.StringId;
                                    now = st;
                                    newState++;
                                    Log.Info(P + h.Name + " slot " + slot + " " + id + " -> " + gs.StringId + " | ksiega " + b.ToString("0.00", CI) + "%, poczatek " + GearState.Pl[so]
                                             + ", liczony " + d.ToString("0.00", CI) + "% -> " + GearState.Pl[st] + " (byl " + GearState.Pl[was] + ").");
                                    Log.Player(h.Name + "'s " + el.Item.Name + " is now " + GearState.En[st] + ".", true);
                                }
                            }
                            string line = slot + "|" + b.ToString("0.##", CI) + "|" + orig + "|" + id + "|" + last;
                            if (idx >= 0) list[idx] = line; else list.Add(line);
                            segs.Add("slot " + slot + " " + id + " ksiega " + a0.ToString("0.00", CI) + " -> " + b.ToString("0.00", CI) + " (trafienia " + amount.ToString("0.00", CI)
                                     + " pkt, " + how + "; poczatek " + GearState.Pl[so] + ", liczony " + d.ToString("0.00", CI) + " -> " + GearState.Pl[now] + ", byl "
                                     + GearState.Pl[was] + ")");
                        }
                        catch (Exception e) { errors++; Stumble("HeroBook.ApplyBattle(" + slot + ")", e); }
                    }
                    if (list.Count == 0) _books.Remove(hid);
                    if (segs.Count > 0) Log.Info(P + h.Name + " | " + string.Join(" ; ", segs.ToArray()) + ".");
                }
            }
            catch (Exception e) { errors++; Stumble("HeroBook.ApplyBattle", e); }
            _hits.Clear(); _worn.Clear(); _lostPts = 0f; _lostN = 0;
            Log.Info(PS + "bitwa | bohaterow z trafieniami " + heroes + " (pominieci " + skipped + "), zuzycie celowane " + total.ToString("0.0", CI) + " pkt, sztuk w ksiegach "
                     + Entries() + ", nowy stan " + newState + ", zmienione sztuki " + changed + " (pkt " + changedPts.ToString("0.0", CI) + "), bledy " + errors + ".");
        }

        private static int Entries()
        {
            int n = 0;
            foreach (var kv in _books) n += kv.Value.Count;
            return n;
        }

        /// <summary>Linia startu (HeroHp.StartLine): bohaterowie z ksiega, z nich w druzynie gracza, wpisy, bledne wiersze wczytania.</summary>
        internal static void Stats(out int heroes, out int inParty, out int entries, out int bad)
        {
            heroes = _books.Count; inParty = 0; entries = Entries(); bad = _bad;
            try
            {
                var ids = PartyHeroIds();
                if (ids == null) return;
                foreach (var k in _books.Keys) if (ids.Contains(k)) inParty++;
            }
            catch { }
        }

        /// <summary>Id zywych bohaterow (bez gracza) w druzynie gracza; null - brak druzyny.</summary>
        private static HashSet<string> PartyHeroIds()
        {
            var mp = MobileParty.MainParty;
            var r = mp != null ? mp.MemberRoster : null;
            if (r == null) return null;
            var ids = new HashSet<string>();
            for (int i = 0; i < r.Count; i++)
            {
                var c = r.GetElementCopyAtIndex(i).Character;
                var h = c != null && c.IsHero ? c.HeroObject : null;
                if (h != null && h.IsAlive && h != Hero.MainHero && h.StringId != null) ids.Add(h.StringId);
            }
            return ids;
        }

        /// <summary>Id zywych bohaterow (przy zapisie); null - brak kampanii.</summary>
        private static HashSet<string> AliveHeroIds()
        {
            if (Campaign.Current == null) return null;
            var ids = new HashSet<string>();
            foreach (var h in Hero.AllAliveHeroes) if (h != null && h.StringId != null) ids.Add(h.StringId);
            return ids;
        }

        /// <summary>Zapis (SyncData): ksiegi zywych bohaterow (recenzja U5: takze poza druzyna gracza - zapis nie zmienia gry); null - klucz CompanionGearWear NIE
        /// albo brak ksiag (klucza w zapisie nie ma).</summary>
        internal static string Export()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CompanionGearWear) return null;
                int pruned = 0;
                var ids = AliveHeroIds();
                if (ids != null)
                    foreach (var k in new List<string>(_books.Keys))
                        if (!ids.Contains(k) || _books[k].Count == 0) { _books.Remove(k); pruned++; }
                string r = null;
                if (_books.Count > 0)
                {
                    var sb = new StringBuilder();
                    foreach (var kv in _books)
                    {
                        if (sb.Length > 0) sb.Append('\n');
                        sb.Append(kv.Key).Append('=').Append(string.Join(";", kv.Value.ToArray()));
                    }
                    r = sb.ToString();
                }
                if (HeroHp.TestWanted())
                    Log.Info("HP sprzetu (m110): zapis | ksiegi towarzyszy: bohaterow " + _books.Count + ", sztuk " + Entries() + ", znakow " + (r != null ? r.Length : 0)
                             + " | usunieci (nie zyja) " + pruned + ".");
                return r;
            }
            catch (Exception e) { Stumble("HeroBook.Export", e); return null; }
        }

        /// <summary>Wczytanie (SyncData): wiersze "id=wpis;wpis" - bledne wiersze i wpisy liczone (bez wywrotki), jeden wpis na gniazdo.</summary>
        internal static void Import(string s)
        {
            _books.Clear(); _hits.Clear(); _worn.Clear(); _lostPts = 0f; _lostN = 0; _bad = 0;
            if (string.IsNullOrEmpty(s)) return;
            try
            {
                foreach (var row in s.Split('\n'))
                {
                    if (row.Length == 0) continue;
                    int eqi = row.IndexOf('=');
                    if (eqi <= 0) { _bad++; continue; }
                    var list = new List<string>();
                    foreach (var e in row.Substring(eqi + 1).Split(';'))
                    {
                        if (e.Length == 0) continue;
                        var p = e.Split('|');
                        int sl;
                        float c;
                        if (p.Length != 5 || !int.TryParse(p[0], out sl) || sl < 0 || sl > 11 || !float.TryParse(p[1], NumberStyles.Float, CI, out c) || Find(list, sl) >= 0)
                        { _bad++; continue; }
                        list.Add(e);
                    }
                    if (list.Count > 0) _books[row.Substring(0, eqi)] = list;
                }
            }
            catch (Exception e) { Stumble("HeroBook.Import", e); }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3 || _stumbles % 50 == 0) Log.Error(where, e);
        }
    }
}
