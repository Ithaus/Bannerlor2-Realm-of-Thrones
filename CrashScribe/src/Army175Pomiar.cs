using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace CrashScribe
{
    /// <summary>
    /// PACZKA 175 - POMIARY CS (poprawki po recenzji 09.10; tylko log, bez zapisu, pod wylacznikiem Armoury Army175Measure):
    /// (1) STARE ZBROJOWNIE (projekt 6.1 pkt 9, bieg T2): raz w sesji, po SkillSinew/NorthHardy - ludzie x sloty (bron, tarcza,
    ///     amunicja z 1. zestawu bojowego) bez sztuki w zbrojowni DTE, ktora udzwigna (ta sama regula co SkillLawWard: grupa
    ///     typ+klasa, najsilniejszy pierwszy, najtrudniejsza uzyteczna pierwsza, siodlo), oraz sztuki w zbrojowni, ktorych nikt
    ///     z zolnierzy partii nie udzwignie - gracz, lordowie AI i zalogi wedlug krolestw.
    /// (2) KONNI Z SZABLONU PRZY ODRODZENIU (2.1 droga c): partia lorda AI tworzona z szablonu rodu (LordPartyComponent,
    ///     MobilePartyCreated) - ludzie i konni wedlug krolestwa; taka partia rodzi sie bez taboru i zbrojowni, wiec jej konni
    ///     sa "konnymi bez konia" z tego zrodla (do rozdzielenia D5 od zamian ROT i awansow w linii Konie AI Armoury).
    /// </summary>
    internal static partial class Army175
    {
        private static bool _oldArmDone;
        private static Dictionary<string, int[]> _spawnStart, _spawnLater;   // krolestwo -> [partie, ludzie, konni]
        private static bool _spawnFirstDay;

        private static void ResetMeasure()
        {
            _oldArmDone = false;
            _spawnStart = null; _spawnLater = null; _spawnFirstDay = false;
        }

        private static bool MeasureOn() { return Wanted("Army175Measure", 1f); }

        // ---------------- (2) konni z szablonu przy odrodzeniu ----------------

        /// <summary>CampaignEvents.MobilePartyCreated: roster jest juz wypelniony z szablonu (CreateParty -> Initialize ->
        /// InitializeLordPartyProperties przed zdarzeniem). Partie rodu gracza pomijane (gra nie daje im szablonu).</summary>
        internal static void OnPartyCreated(MobileParty mp)
        {
            try
            {
                if (mp == null || !mp.IsLordParty || !MeasureOn()) return;
                var clan = mp.ActualClan ?? (mp.LeaderHero != null ? mp.LeaderHero.Clan : null);
                if (clan == null || clan == Clan.PlayerClan) return;
                int men = 0, mounted = 0;
                var r = mp.MemberRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero || el.Number <= 0) continue;
                    men += el.Number;
                    if (el.Character.IsMounted) mounted += el.Number;
                }
                string k = clan.Kingdom != null ? clan.Kingdom.StringId : "bez_krolestwa";
                var map = _spawnFirstDay ? (_spawnLater ?? (_spawnLater = new Dictionary<string, int[]>(StringComparer.Ordinal)))
                                         : (_spawnStart ?? (_spawnStart = new Dictionary<string, int[]>(StringComparer.Ordinal)));
                int[] v;
                if (!map.TryGetValue(k, out v)) map[k] = v = new int[3];
                v[0]++; v[1] += men; v[2] += mounted;
            }
            catch { }
        }

        private static string SpawnText(Dictionary<string, int[]> map)
        {
            int p = 0, m = 0, k = 0;
            var rows = new List<KeyValuePair<string, int[]>>(map);
            foreach (var kv in rows) { p += kv.Value[0]; m += kv.Value[1]; k += kv.Value[2]; }
            rows.Sort((a, b) => b.Value[2] != a.Value[2] ? b.Value[2].CompareTo(a.Value[2]) : string.CompareOrdinal(a.Key, b.Key));
            var sb = new System.Text.StringBuilder();
            sb.Append("partii ").Append(p).Append(", ludzi ").Append(m).Append(", konnych ").Append(k);
            int shown = 0;
            foreach (var kv in rows)
            {
                if (shown++ >= 25) { sb.Append(" | ..."); break; }
                sb.Append(" | ").Append(kv.Key).Append(' ').Append(kv.Value[0]).Append('/').Append(kv.Value[1]).Append('/').Append(kv.Value[2]);
            }
            return sb.ToString();
        }

        /// <summary>DailyTick: linia partii lordow AI z szablonu od poprzedniej linii (krolestwo partie/ludzie/konni).</summary>
        internal static void RespawnDaily()
        {
            try
            {
                if (!_spawnFirstDay)
                {
                    _spawnFirstDay = true;
                    if (_spawnStart != null && _spawnStart.Count > 0)
                        Scribe.Line("Mends: sklad 175 - partie lordow AI z szablonu przed pierwsza doba (nowa gra: partie startowe; konni bez konia - partia bez taboru i zbrojowni): "
                                    + SpawnText(_spawnStart) + ".");
                    _spawnStart = null;
                }
                if (_spawnLater == null || _spawnLater.Count == 0) return;
                int day = 0; try { day = (int)CampaignTime.Now.ToDays; } catch { }
                Scribe.Line("Mends: sklad 175 - odrodzone partie lordow AI z szablonu (doba " + day + ", od poprzedniej linii; konni bez konia - partia bez taboru i zbrojowni): "
                            + SpawnText(_spawnLater) + ".");
                _spawnLater = null;
            }
            catch { }
        }

        // ---------------- (1) stare zbrojownie ----------------

        private sealed class ArmDemand { public CharacterObject Co; public int Need; public int Skill; public bool Mounted; }
        private sealed class ArmSup { public ItemObject Item; public int Left; }

        private static string ArmGroup(ItemObject it)
        {
            var wc = it.PrimaryWeapon != null ? it.PrimaryWeapon.WeaponClass : WeaponClass.Undefined;
            return (int)it.ItemType + "/" + (int)wc;
        }

        private static bool ArmSkip(ItemObject it)
        {
            return it == null || Mends.IsUniqueGear(it) || Mends.IsLoreBlade(it) || Mends.IsDeadGear(it);
        }

        /// <summary>Jedna partia: [ludzie x sloty, bez sztuki, sztuk (bez koni), sztuk nieuzytecznych].</summary>
        private static long[] MeasureParty(MobileParty mp, List<KeyValuePair<ItemObject, int>> supply)
        {
            var res = new long[4];
            var troops = new List<CharacterObject>();
            var demands = new Dictionary<string, List<ArmDemand>>(StringComparer.Ordinal);
            var r = mp.MemberRoster;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var co = el.Character;
                if (co == null || co.IsHero || el.Number <= 0) continue;
                troops.Add(co);
                Equipment first = null;
                foreach (var eq in co.BattleEquipments) { if (eq != null) { first = eq; break; } }
                if (first == null) continue;
                for (int s = 0; s <= 3; s++)
                {
                    var it = first[s].Item;
                    if (ArmSkip(it)) continue;
                    var rs = Mends.ReqSkill(it);
                    if (rs == null) continue;
                    string g = ArmGroup(it);
                    List<ArmDemand> l;
                    if (!demands.TryGetValue(g, out l)) demands[g] = l = new List<ArmDemand>();
                    l.Add(new ArmDemand { Co = co, Need = el.Number, Skill = co.GetSkillValue(rs), Mounted = co.IsMounted });
                    res[0] += el.Number;
                }
            }
            var sup = new Dictionary<string, List<ArmSup>>(StringComparer.Ordinal);
            foreach (var kv in supply)
            {
                var it = kv.Key;
                if (ArmSkip(it) || kv.Value <= 0) continue;
                var ty = it.ItemType;
                if (ty == ItemObject.ItemTypeEnum.Horse || ty == ItemObject.ItemTypeEnum.HorseHarness) continue;   // konie - Stajnia
                res[2] += kv.Value;
                bool anyone = false;
                foreach (var co in troops) if (Mends.CanUse(co, it)) { anyone = true; break; }
                if (!anyone) res[3] += kv.Value;
                string g = ArmGroup(it);
                if (!demands.ContainsKey(g)) continue;
                List<ArmSup> l;
                if (!sup.TryGetValue(g, out l)) sup[g] = l = new List<ArmSup>();
                l.Add(new ArmSup { Item = it, Left = kv.Value });
            }
            foreach (var kv in demands)
            {
                var ds = kv.Value;
                ds.Sort((a, b) => b.Skill.CompareTo(a.Skill));
                List<ArmSup> ss;
                if (!sup.TryGetValue(kv.Key, out ss)) ss = new List<ArmSup>();
                ss.Sort((a, b) => b.Item.Difficulty != a.Item.Difficulty ? b.Item.Difficulty.CompareTo(a.Item.Difficulty) : string.CompareOrdinal(a.Item.StringId, b.Item.StringId));
                foreach (var d in ds)
                {
                    int need = d.Need;
                    foreach (var s in ss)
                    {
                        if (need <= 0) break;
                        if (s.Left <= 0 || !Mends.CanUse(d.Co, s.Item)) continue;
                        if (d.Mounted && !Mends.MountOk(s.Item)) continue;
                        int take = Math.Min(need, s.Left);
                        s.Left -= take; need -= take;
                    }
                    res[1] += need;
                }
            }
            return res;
        }

        private static void Add(Dictionary<string, long[]> into, string key, long[] v)
        {
            long[] a;
            if (!into.TryGetValue(key, out a)) into[key] = a = new long[5];
            for (int i = 0; i < 4; i++) a[i] += v[i];
            a[4]++;
        }

        private static string ArmText(long[] a)
        {
            if (a == null) return "brak";
            return "bez sztuki " + a[1] + " z " + a[0] + ", nieuzytecznych " + a[3] + " z " + a[2] + " szt. (" + a[4] + " partii)";
        }

        /// <summary>Raz w sesji, po SkillSinew i NorthHardy (umiejetnosci juz po 175). Tylko log.</summary>
        internal static void OldArmouries()
        {
            if (_oldArmDone || !Mends.SinewApplied) return;
            _oldArmDone = true;
            try
            {
                if (!MeasureOn()) return;
                var tDte = AccessTools.TypeByName("DynamicTroopEquipmentReupload.EveryoneCampaignBehavior");
                var fMap = tDte != null ? AccessTools.Field(tDte, "PartyArmories") : null;
                var map = fMap != null ? fMap.GetValue(null) as IDictionary : null;
                var tArm = AccessTools.TypeByName("DynamicTroopEquipmentReupload.ArmyArmory");
                var fArm = tArm != null ? AccessTools.Field(tArm, "Armory") : null;
                var player = fArm != null ? fArm.GetValue(null) as ItemRoster : null;
                if (map == null && player == null) { Scribe.Line("Mends: stare zbrojownie (175) - brak zbrojowni DTE, pomiar pominiety."); return; }
                var lords = new Dictionary<string, long[]>(StringComparer.Ordinal);
                var garr = new Dictionary<string, long[]>(StringComparer.Ordinal);
                var mine = new Dictionary<string, long[]>(StringComparer.Ordinal);
                int stumbles = 0;
                foreach (var mp in MobileParty.All)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive) continue;
                        var supply = new List<KeyValuePair<ItemObject, int>>();
                        if (mp == MobileParty.MainParty)
                        {
                            if (player == null) continue;
                            for (int i = 0; i < player.Count; i++)
                            {
                                var el = player.GetElementCopyAtIndex(i);
                                if (el.EquipmentElement.Item != null && el.Amount > 0) supply.Add(new KeyValuePair<ItemObject, int>(el.EquipmentElement.Item, el.Amount));
                            }
                            Add(mine, "gracz", MeasureParty(mp, supply));
                            continue;
                        }
                        if (!mp.IsLordParty && !mp.IsGarrison) continue;
                        bool ofPlayer = mp.ActualClan == Clan.PlayerClan
                                        || (mp.IsGarrison && mp.CurrentSettlement != null && mp.CurrentSettlement.OwnerClan == Clan.PlayerClan);
                        if (map != null && map.Contains(mp.Id))
                        {
                            var inner = map[mp.Id] as IDictionary;
                            if (inner != null)
                                foreach (DictionaryEntry e in inner)
                                {
                                    var it = e.Key as ItemObject;
                                    int n = 0; try { n = Convert.ToInt32(e.Value); } catch { }
                                    if (it != null && n > 0) supply.Add(new KeyValuePair<ItemObject, int>(it, n));
                                }
                        }
                        var v = MeasureParty(mp, supply);
                        if (ofPlayer) { Add(mine, mp.IsGarrison ? "gracz-zalogi" : "gracz-inne", v); continue; }
                        string k = mp.MapFaction != null ? mp.MapFaction.StringId : "-";
                        Add(mp.IsGarrison ? garr : lords, k, v);
                    }
                    catch { stumbles++; }
                }
                long[] lt = null, gt = null;
                foreach (var a in lords.Values) { if (lt == null) lt = new long[5]; for (int i = 0; i < 5; i++) lt[i] += a[i]; }
                foreach (var a in garr.Values) { if (gt == null) gt = new long[5]; for (int i = 0; i < 5; i++) gt[i] += a[i]; }
                var keys = new List<string>(lords.Keys);
                foreach (var k in garr.Keys) if (!lords.ContainsKey(k)) keys.Add(k);
                keys.Sort((x, y) =>
                {
                    long[] a, b; long ux = 0, uy = 0;
                    if (lords.TryGetValue(x, out a)) ux += a[1]; if (garr.TryGetValue(x, out a)) ux += a[1];
                    if (lords.TryGetValue(y, out b)) uy += b[1]; if (garr.TryGetValue(y, out b)) uy += b[1];
                    return uy != ux ? uy.CompareTo(ux) : string.CompareOrdinal(x, y);
                });
                var sb = new System.Text.StringBuilder();
                sb.Append("Mends: stare zbrojownie (175, po SkillSinew; sprzet wedlug tieru ").Append(TierGearApplied ? "dziala" : "NIE dziala")
                  .Append(", ").Append(IsSavedCampaign() ? "zapis" : "nowa gra - zbrojownie startowe moga byc jeszcze puste")
                  .Append("; ludzie x sloty bron/tarcza/amunicja 1. zestawu bez sztuki do wymogu - regula SkillLawWard; sztuki bez koni, ktorych nikt z zolnierzy partii nie udzwignie) - ");
                long[] g0; mine.TryGetValue("gracz", out g0);
                sb.Append("gracz: ").Append(ArmText(g0));
                long[] g1; if (mine.TryGetValue("gracz-inne", out g1)) sb.Append("; inne partie gracza: ").Append(ArmText(g1));
                long[] g2; if (mine.TryGetValue("gracz-zalogi", out g2)) sb.Append("; zalogi gracza: ").Append(ArmText(g2));
                sb.Append("; lordowie AI: ").Append(ArmText(lt)).Append("; zalogi AI: ").Append(ArmText(gt)).Append("; wedlug krolestw (lordowie | zalogi):");
                foreach (var k in keys)
                {
                    long[] a, b;
                    lords.TryGetValue(k, out a); garr.TryGetValue(k, out b);
                    sb.Append(' ').Append(k).Append(' ')
                      .Append(a != null ? a[1] + "/" + a[0] + " n" + a[3] + "/" + a[2] : "-").Append(" | ")
                      .Append(b != null ? b[1] + "/" + b[0] + " n" + b[3] + "/" + b[2] : "-").Append(';');
                }
                sb.Append(" potkniecia ").Append(stumbles).Append('.');
                Scribe.Line(sb.ToString());
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.OldArmouries", null); } catch { } }
        }
    }
}
