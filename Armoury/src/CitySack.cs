using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// LUP ZDOBYTEGO MIASTA - RDZEN (docs/lup-miast/LUP-ZDOBYTEGO-MIASTA-2026-10-13.md, rozdz. 3-6; decyzje Jeffa 13.10: 1 lup z kazdej monety, 2 podzial wedlug
    /// ludzi i prawo trzecich, 3 trzy doby bez zoldu w lupionym miescie, 4 laska = laska i AI lupi z potrzeby, 5a towar i konie - drugi etap).
    ///
    /// L0 (klucz SackMeasureLog) - POMIAR NA SUCHO, gra bez zmian: przy kazdych nastepstwach oblezenia (SiegeAftermathCampaignBehavior.OnSiegeAftermathApplied,
    ///    to samo wywolanie, ktore liczy okno O10 ksiegi) linia "Lup miast (L0)": osada, typ bitwy (migawka MapEvent z OnMapEventEnded), wybor gry, stan zbiornikow
    ///    (kasa kupcow i zapas, kiesy W / R / P / gmina albo podzamcze, polki: towar, zywnosc, konie), ile wzielaby regula przy kazdym z trzech wyborow, ludzie
    ///    druzyn zwycieskich, zold armii, "armia w potrzebie" i wybor wedlug rozdz. 3.4. Przez 30 dob po nastepstwach linia osad: kasa, kiesy, "stac na jedzenie",
    ///    pobor pana i korony, "zycie" i markietani do tej osady (w oknie lupu i po nim, od wojska zdobywcy i od innych), jency kupieni przez osade.
    ///    Przejecia warsztatow przez BK przy zmianie pana oblezeniem (migawka wlascicieli wokol ChangeOwnerOfSettlementAction.ApplyInternal). Pamiec sesji.
    /// Wyjatki per wywolanie - licznik potkniec (pierwszy z miejsca do logu), bez globalnych wylacznikow (CLAUDE.md 7).
    /// </summary>
    internal static partial class CitySack
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------ klucze
        internal static bool MeasureOn { get { var s = Settings.Current; return s != null && s.SackMeasureLog && s.LogEnabled; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        // rodzaje wyboru (nasze): 0 laska, 1 grabiez, 2 spustoszenie
        internal const int KMercy = 0, KPillage = 1, KDevastate = 2;
        private static readonly string[] KName = { "laska", "grabiez", "spustoszenie" };
        internal static int KindOf(SiegeAftermathAction.SiegeAftermath a)
        {
            return a == SiegeAftermathAction.SiegeAftermath.ShowMercy ? KMercy : a == SiegeAftermathAction.SiegeAftermath.Pillage ? KPillage : KDevastate;
        }

        /// <summary>Czesc monety brana przy wyborze (rozdz. 3.2): laska 0, grabiez SackSharePillage (1/3), spustoszenie SackShareDevastate (2/3); kod przycina do 0..1.</summary>
        internal static double ShareOf(int kind)
        {
            var s = Settings.Current;
            if (s == null || kind == KMercy) return 0.0;
            float v = kind == KPillage ? s.SackSharePillage : s.SackShareDevastate;
            if (float.IsNaN(v) || v < 0f) return 0.0;
            return v > 1f ? 1.0 : v;
        }

        // ------------------------------------------------------------ potkniecia
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("CitySack." + where, e); } catch { }
        }

        // ------------------------------------------------------------ stan zbiornikow osady
        internal sealed class Stock
        {
            public long Kasa, Reserve;
            public readonly long[] Folk = new long[4];      // W, R, P, gmina (zamek: [0] podzamcze)
            public bool FolkLive, Castle;
            public long FolkSum { get { return Folk[0] + Folk[1] + Folk[2] + Folk[3]; } }
        }

        internal static Stock Read(Settlement st)
        {
            var k = new Stock();
            if (st == null || st.Town == null) return k;
            var t = st.Town;
            k.Castle = !st.IsTown;
            k.Kasa = Math.Max(0, t.Gold);
            try
            {
                if (st.IsTown) k.Reserve = TownPurse.On ? TownPurse.Reserve(t) : Seal31.TownReserve(t);
                else k.Reserve = Math.Max(0L, k.Kasa - CastlePurse.Spendable(t));
            }
            catch (Exception e) { Stumble("Read.Reserve", e); }
            k.FolkLive = TownFolk.SackRead(st, k.Folk);
            return k;
        }

        /// <summary>Ile wzielaby regula ze zbiornikow przy czesci `share` - floor(czesc x stan) z kazdego (rozdz. 3.3 pkt 1).</summary>
        internal static void RuleTake(Stock k, double share, out long merchants, out long folk)
        {
            merchants = 0; folk = 0;
            if (k == null || share <= 0) return;
            merchants = (long)Math.Floor(share * k.Kasa);
            for (int c = 0; c < 4; c++) folk += (long)Math.Floor(share * k.Folk[c]);
        }

        // ------------------------------------------------------------ odbiorcy (rozdz. 3.3 pkt 2-3): druzyny ruchome strony zwycieskiej, wedlug ludzi
        internal sealed class Rec
        {
            public MobileParty Mp; public int Men; public long Share, ToMen, ToLead; public Hero Lead; public bool Undead;
        }

        /// <summary>Druzyny z listy udzialow gry (te same, ktorym placi gra); lista pusta ("Right of Pillage") - druzyna, ktora lupi. Inni - oznaczeni (bez udzialu).</summary>
        internal static List<Rec> Recipients(MobileParty attacker, Dictionary<MobileParty, float> contrib, out int men, out int undeadMen)
        {
            var l = new List<Rec>();
            men = 0; undeadMen = 0;
            var seen = new HashSet<MobileParty>();
            if (contrib != null) foreach (var kv in contrib) Add(l, seen, kv.Key, ref men, ref undeadMen);
            if (l.Count == 0 && attacker != null) Add(l, seen, attacker, ref men, ref undeadMen);
            return l;
        }

        private static void Add(List<Rec> l, HashSet<MobileParty> seen, MobileParty mp, ref int men, ref int undeadMen)
        {
            if (mp == null || !mp.IsActive || !seen.Add(mp)) return;
            int n = 0;
            try { n = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0; } catch { n = 0; }
            if (n <= 0) return;
            bool u = false;
            try { u = Undead.Party(mp); } catch { u = false; }
            if (u) { undeadMen += n; l.Add(new Rec { Mp = mp, Men = n, Undead = true }); return; }
            men += n;
            l.Add(new Rec { Mp = mp, Men = n, Lead = mp.LeaderHero != null && mp.LeaderHero.IsAlive ? mp.LeaderHero : null });
        }

        // ------------------------------------------------------------ armia: zold i potrzeba (rozdz. 3.4 pkt 3-4)
        internal sealed class Need
        {
            public long ArmyWage, LeadWage, LeadGold; public int Unpaid, Parties; public bool InNeed; public double LeadDays;
        }

        internal static Need NeedOf(MobileParty a)
        {
            var n = new Need();
            if (a == null) return n;
            var list = new List<MobileParty>();
            if (a.Army != null && a.Army.Parties != null) { foreach (var p in a.Army.Parties) if (p != null) list.Add(p); }
            if (!list.Contains(a)) list.Add(a);
            foreach (var p in list)
            {
                int w = 0;
                try { w = Math.Max(0, p.TotalWage); } catch { w = 0; }
                n.ArmyWage += w; n.Parties++;
                if (p == a) n.LeadWage = w;
                // ta sama regula co WarLedger (placi ten, kto ma czym): zalegly zold i kiesa rodu ponizej dziennego zoldu tej druzyny
                try
                {
                    if (p.HasUnpaidWages > 0f)
                    {
                        var payer = p.ActualClan ?? (p.LeaderHero != null ? p.LeaderHero.Clan : null);
                        if (payer == null || payer.Gold < Math.Max(1, w)) n.Unpaid++;
                    }
                }
                catch { }
            }
            n.LeadGold = a.LeaderHero != null ? Math.Max(0, a.LeaderHero.Gold) : 0;
            n.LeadDays = n.LeadWage > 0 ? (double)n.LeadGold / n.LeadWage : 999.0;
            n.InNeed = n.Unpaid > 0 || (a.LeaderHero != null && n.LeadDays < NeedDays);
            return n;
        }

        private const double NeedDays = 14.0;   // rozdz. 3.4 pkt 4: kiesa wodza ponizej 14 dob zoldu jego druzyny

        // ------------------------------------------------------------ wybor wedlug rozdz. 3.4 (L0 - pomiar; L4 - regula)
        private static MethodInfo _miInfl, _miAlive;

        private static float InfluenceCost(SiegeAftermathCampaignBehavior inst, MobileParty a, Settlement st, SiegeAftermathAction.SiegeAftermath t)
        {
            try
            {
                if (inst == null) return 0f;
                if (_miInfl == null) _miInfl = AccessTools.Method(typeof(SiegeAftermathCampaignBehavior), "GetSiegeAftermathInfluenceCost");
                return _miInfl != null ? (float)_miInfl.Invoke(inst, new object[] { a, st, t }) : 0f;
            }
            catch (Exception e) { Stumble("InfluenceCost", e); return 0f; }
        }

        private static bool LeaderStands(SiegeAftermathCampaignBehavior inst, MobileParty a)
        {
            try
            {
                if (inst != null)
                {
                    if (_miAlive == null) _miAlive = AccessTools.Method(typeof(SiegeAftermathCampaignBehavior), "IsMobilePartyLeaderAliveForSiegeAftermath");
                    if (_miAlive != null) return (bool)_miAlive.Invoke(inst, new object[] { a });
                }
            }
            catch (Exception e) { Stumble("LeaderStands", e); }
            var h = a != null ? a.LeaderHero : null;
            return h != null && h.IsAlive;
        }

        /// <summary>Wybor wedlug rozdz. 3.4: 0/1/2 albo -1 (wybor gry - cecha, wplywy, morale). why - powod do linii. Tylko odczyt.</summary>
        internal static int Rule34(SiegeAftermathCampaignBehavior inst, MobileParty a, Settlement st, Stock k, Need n, out string why)
        {
            why = "gra";
            if (a == null || st == null) return -1;
            var cul = a.MapFaction != null ? a.MapFaction.Culture : null;
            if (cul != null && cul == st.Culture) { why = "ta sama kultura"; return KMercy; }
            if (!LeaderStands(inst, a)) { why = "wodz poza bitwa - gra"; return -1; }
            long m, f;
            RuleTake(k, ShareOf(KPillage), out m, out f);
            if (m + f < Math.Max(1L, n.ArmyWage)) { why = "nie ma czego brac (" + (m + f) + " < doba zoldu " + n.ArmyWage + ")"; return KMercy; }
            if (!n.InNeed) return -1;
            int mercy = 0;
            try { mercy = a.LeaderHero != null ? a.LeaderHero.GetTraitLevel(DefaultTraits.Mercy) : 0; } catch { mercy = 0; }
            if (mercy < 0)
            {
                float morale = a.Army != null ? a.Army.Morale : a.Morale;
                float infl = a.ActualClan != null ? a.ActualClan.Influence : 0f;
                if (infl > 2f * InfluenceCost(inst, a, st, SiegeAftermathAction.SiegeAftermath.Devastate) && morale < 90f) { why = "potrzeba, wodz okrutny"; return KDevastate; }
            }
            why = "potrzeba";
            return KPillage;
        }

        // ------------------------------------------------------------ migawka bitwy (OnMapEventEnded gry)
        private static MapEvent _me;

        public static void MapEndPre(MapEvent __0)
        {
            _me = __0;
        }

        public static void MapEndFin()
        {
            _me = null;
        }

        private static string BattleOf(MapEvent me, Dictionary<MobileParty, float> contrib)
        {
            if (me != null)
            {
                if (me.IsSiegeAssault) return "szturm";
                if (me.IsSiegeOutside) return "pod-murami";
                if (me.IsSallyOut) return "wypad";
                if (me.IsBlockadeSallyOut) return "wypad-z-blokady";
                return "inna";
            }
            return contrib == null || contrib.Count == 0 ? "bez-bitwy" : "menu-gracza";
        }

        // ------------------------------------------------------------ pomiar 30 dob po nastepstwach (pamiec sesji)
        internal sealed class Track
        {
            public Settlement St; public float Day0; public string Faction; public int Kind; public bool Captured;
            public long Kasa0, Folk0, Merch, FolkTaken;
            public long InWinSacker, InWinOther, AfterSacker, AfterOther, Pris;
        }
        private static readonly Dictionary<Settlement, Track> _track = new Dictionary<Settlement, Track>();
        private const int TrackDays = 30;

        private static float Now { get { return (float)CampaignTime.Now.ToDays; } }

        /// <summary>Okno lupu (doby) - L3 SackNoSpendingDays; do pomiaru takze przy wylaczonym L3 (te same granice).</summary>
        internal static int WindowDays { get { var s = Settings.Current; return s != null ? Math.Max(0, Math.Min(30, s.SackNoSpendingDays)) : 3; } }

        /// <summary>MenPurse.OnLeft ("zycie" przy wyjezdzie z miasta) i Sutlers.One (markietani): zloto zostawione w osadzie - pomiar L0 dla osad po nastepstwach. Nie rzuca.</summary>
        internal static void NoteSpend(Settlement st, MobileParty mp, long amount)
        {
            try
            {
                if (st == null || amount <= 0 || _track.Count == 0) return;
                Track t;
                if (!_track.TryGetValue(st, out t)) return;
                bool sacker = mp != null && mp.MapFaction != null && mp.MapFaction.StringId == t.Faction;
                bool inWin = Now - t.Day0 < WindowDays;
                if (inWin) { if (sacker) t.InWinSacker += amount; else t.InWinOther += amount; }
                else { if (sacker) t.AfterSacker += amount; else t.AfterOther += amount; }
            }
            catch (Exception e) { Stumble("NoteSpend", e); }
        }

        /// <summary>RealPayer: osada zaplacila za jencow (pomiar L0 - kasa po lupie ma mniej na wykup). Nie rzuca.</summary>
        internal static void NotePrisoners(Settlement st, long pay)
        {
            try
            {
                if (st == null || pay <= 0 || _track.Count == 0) return;
                Track t;
                if (_track.TryGetValue(st, out t)) t.Pris += pay;
            }
            catch (Exception e) { Stumble("NotePrisoners", e); }
        }

        // ------------------------------------------------------------ warsztaty przy zmianie pana oblezeniem (pomiar przejec BK)
        private static Hero[] _wsOwners;
        private static Settlement _wsSt;
        private static int _dWsSeen, _dWsMoved;
        private static readonly List<string> _dWsTowns = new List<string>();

        public static void OwnerPre(Settlement __0, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail __3)
        {
            _wsSt = null; _wsOwners = null;
            try
            {
                if (!MeasureOn || !Live || __0 == null || __0.Town == null || __3 != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege) return;
                var ws = __0.Town.Workshops;
                if (ws == null) return;
                _wsOwners = new Hero[ws.Length];
                for (int i = 0; i < ws.Length; i++) _wsOwners[i] = ws[i] != null ? ws[i].Owner : null;
                _wsSt = __0;
            }
            catch (Exception e) { _wsSt = null; Stumble("OwnerPre", e); }
        }

        public static void OwnerFin()
        {
            try
            {
                var st = _wsSt; var before = _wsOwners;
                _wsSt = null; _wsOwners = null;
                if (st == null || before == null || st.Town == null || st.Town.Workshops == null) return;
                var ws = st.Town.Workshops;
                int moved = 0, seen = 0;
                for (int i = 0; i < ws.Length && i < before.Length; i++)
                {
                    if (ws[i] == null || before[i] == null) continue;
                    seen++;
                    if (!ReferenceEquals(ws[i].Owner, before[i])) moved++;
                }
                _dWsSeen += seen; _dWsMoved += moved;
                if (moved > 0) _dWsTowns.Add(st.StringId + " " + moved);
            }
            catch (Exception e) { Stumble("OwnerFin", e); }
        }

        // ------------------------------------------------------------ nastepstwa: prefiks OnSiegeAftermathApplied (L0)
        private static int _dCalls, _dLines;
        private static readonly int[] _dGame = new int[3];

        public static void AftermathPre(SiegeAftermathCampaignBehavior __instance, MobileParty __0, Settlement __1, SiegeAftermathAction.SiegeAftermath __2,
                                        Clan __3, Dictionary<MobileParty, float> __4)
        {
            if (!Live) return;
            _dCalls++;
            try
            {
                if (!MeasureOn || __1 == null || __1.Town == null) return;
                MeasureLine(__instance, __0, __1, KindOf(__2), __4);
            }
            catch (Exception e) { Stumble("AftermathPre", e); }
        }

        private static void MeasureLine(SiegeAftermathCampaignBehavior inst, MobileParty a, Settlement st, int kind, Dictionary<MobileParty, float> contrib)
        {
            var k = Read(st);
            int men, uMen;
            var recs = Recipients(a, contrib, out men, out uMen);
            var need = NeedOf(a);
            string why;
            int rule = Rule34(inst, a, st, k, need, out why);
            string battle = BattleOf(_me, contrib);
            bool captured = _me != null ? (_me.IsSiegeAssault || _me.IsSallyOut || _me.IsBlockadeSallyOut)
                                        : (a != null && st.MapFaction == a.MapFaction);
            _dGame[kind]++;
            // polki: towar bez zywnosci, zywnosc, konie (ceny bazowe)
            long gN = 0, gV = 0, fN = 0, hN = 0, hV = 0;
            try
            {
                var r = st.ItemRoster;
                if (r != null)
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it == null || el.Amount <= 0) continue;
                        if (it.IsFood) fN += el.Amount;
                        else if (it.ItemType == ItemObject.ItemTypeEnum.Horse) { hN += el.Amount; hV += (long)it.Value * el.Amount; }
                        else { gN += el.Amount; gV += (long)it.Value * el.Amount; }
                    }
            }
            catch (Exception e) { Stumble("MeasureLine.Shelf", e); }
            long pm, pf, dm, df;
            RuleTake(k, ShareOf(KPillage), out pm, out pf);
            RuleTake(k, ShareOf(KDevastate), out dm, out df);
            string player = a == MobileParty.MainParty ? "wodz" : (contrib != null && MobileParty.MainParty != null && contrib.ContainsKey(MobileParty.MainParty) ? "w armii" : "-");
            int mercyT = 0;
            try { mercyT = a != null && a.LeaderHero != null ? a.LeaderHero.GetTraitLevel(DefaultTraits.Mercy) : 0; } catch { }
            var sb = new StringBuilder(900);
            sb.Append("Lup miast (L0): dzien ").Append((int)CampaignTime.Now.ToDays).Append(" | osada ").Append(st.StringId).Append(" (").Append(st.Name).Append(", ")
              .Append(st.IsTown ? "miasto" : "zamek").Append(") | bitwa ").Append(battle).Append(" | zdobycie ").Append(captured ? "tak" : "nie")
              .Append(" | wybor gry ").Append(KName[kind]).Append(" | regula 3.4 ").Append(rule >= 0 ? KName[rule] : "gra").Append(" (").Append(why).Append(')')
              .Append(" | kasa kupcow ").Append(k.Kasa).Append(" (zapas ").Append(k.Reserve).Append(')')
              .Append(" | lud W ").Append(k.Folk[0]).Append(" R ").Append(k.Folk[1]).Append(" P ").Append(k.Folk[2]).Append(" gmina ").Append(k.Folk[3])
              .Append(" (kiesa ").Append(k.FolkLive ? "czynna" : "nieczynna").Append(')')
              .Append(" | polki: towar ").Append(gN).Append(" szt. za ").Append(gV).Append(", zywnosc ").Append(fN).Append(" szt., konie ").Append(hN).Append(" za ").Append(hV)
              .Append(" | lup wedlug reguly: laska 0, grabiez ").Append(pm + pf).Append(" (kupcy ").Append(pm).Append(", lud ").Append(pf).Append("), spustoszenie ").Append(dm + df)
              .Append(" (kupcy ").Append(dm).Append(", lud ").Append(df).Append(')')
              .Append(" | druzyny ").Append(recs.Count).Append(" (ludzie ").Append(men).Append(", Inni ").Append(uMen).Append("), zold armii ").Append(need.ArmyWage).Append("/d")
              .Append(", grabiez = ").Append(need.ArmyWage > 0 ? ((double)(pm + pf) / need.ArmyWage).ToString("0.0", Inv) : "-").Append(" dob zoldu")
              .Append(" | armia w potrzebie ").Append(need.InNeed ? "tak" : "nie").Append(" (niezaplacony zold ").Append(need.Unpaid).Append(" z ").Append(need.Parties)
              .Append(" druzyn, kiesa wodza ").Append(need.LeadGold).Append(" = ").Append(need.LeadDays >= 999 ? "-" : need.LeadDays.ToString("0.0", Inv)).Append(" dob zoldu jego druzyny)")
              .Append(" | wodz ").Append(a != null && a.LeaderHero != null ? a.LeaderHero.Name.ToString() : "-").Append(" (Mercy ").Append(mercyT).Append(')')
              .Append(" | gracz ").Append(player).Append('.');
            Log.Info(sb.ToString());
            _dLines++;
            // pomiar 30 dob: osada po nastepstwach (ostatnie nastepstwa nadpisuja)
            var t = new Track { St = st, Day0 = Now, Faction = a != null && a.MapFaction != null ? a.MapFaction.StringId : "", Kind = kind, Captured = captured, Kasa0 = k.Kasa, Folk0 = k.FolkSum };
            _track[st] = t;
        }

        // ------------------------------------------------------------ raz na dobe (ArmouryBehavior, po kronice bitew)
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                int today = (int)CampaignTime.Now.ToDays;
                if (MeasureOn && (_dCalls > 0 || _dWsSeen > 0 || _dLines > 0))
                {
                    var sb = new StringBuilder(600);
                    sb.Append("Lup miast: dzien ").Append(today).Append(" | L0: nastepstw ").Append(_dCalls).Append(" (linii ").Append(_dLines).Append("), wybor gry: laska ")
                      .Append(_dGame[KMercy]).Append(", grabiez ").Append(_dGame[KPillage]).Append(", spustoszenie ").Append(_dGame[KDevastate])
                      .Append(" | warsztaty przy zmianie pana oblezeniem: ").Append(_dWsSeen).Append(", zmienily wlasciciela (BK) ").Append(_dWsMoved)
                      .Append(_dWsTowns.Count > 0 ? " (" + string.Join(", ", _dWsTowns.ToArray()) + ")" : "")
                      .Append(" | potkniecia ").Append(_stumbles).Append('.');
                    Log.Info(sb.ToString());
                }
                TrackLine(today);
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }

        private static void TrackLine(int today)
        {
            if (_track.Count == 0) return;
            var drop = new List<Settlement>();
            var sb = MeasureOn ? new StringBuilder(300 + 260 * _track.Count) : null;
            if (sb != null) sb.Append("Lup miast (L0 po nastepstwach): dzien ").Append(today).Append(" | okno lupu ").Append(WindowDays).Append(" dob");
            foreach (var kv in _track)
            {
                var t = kv.Value; var st = kv.Key;
                int age = (int)Math.Floor(Now - t.Day0);
                if (st == null || st.Town == null || age > TrackDays) { drop.Add(st); continue; }
                if (sb == null) continue;
                var k = Read(st);
                double food, take;
                TownFolk.SackWatch(st, out food, out take);
                sb.Append(" | ").Append(st.StringId).Append(" d+").Append(age).Append(' ').Append(KName[t.Kind]).Append(t.Captured ? "" : " (bez zdobycia)")
                  .Append(": kasa ").Append(k.Kasa).Append(" (zapas ").Append(k.Reserve).Append(", przy nastepstwach ").Append(t.Kasa0).Append(')')
                  .Append(", lud ").Append(k.FolkSum).Append(" (przy nastepstwach ").Append(t.Folk0).Append(')')
                  .Append(", stac jedzenie ").Append(double.IsNaN(food) ? "-" : food.ToString("0.00", Inv))
                  .Append(", pobor pana i korony ").Append(double.IsNaN(take) ? "-" : ((long)take).ToString(Inv))
                  .Append(", zycie i markietani w oknie: zdobywca ").Append(t.InWinSacker).Append(" / inni ").Append(t.InWinOther)
                  .Append(", po oknie: zdobywca ").Append(t.AfterSacker).Append(" / inni ").Append(t.AfterOther)
                  .Append(", jency kupione ").Append(t.Pris);
            }
            foreach (var st in drop) _track.Remove(st);
            if (sb != null && _track.Count > 0) Log.Info(sb.Append('.').ToString());
        }

        private static void ClearDay()
        {
            _dCalls = _dLines = 0; Array.Clear(_dGame, 0, 3);
            _dWsSeen = _dWsMoved = 0; _dWsTowns.Clear();
        }

        /// <summary>Konstruktor ArmouryBehavior (nowa kampania i kazde wczytanie).</summary>
        internal static void Reset()
        {
            ClearDay();
            _track.Clear();
            _me = null; _wsSt = null; _wsOwners = null;
            _stumbles = 0; _err.Clear();
        }

        // ------------------------------------------------------------ wpiecia (Seal31.ApplyAll - po oknach ksiegi obiegu)
        internal static void ApplyAll()
        {
            var t = typeof(SiegeAftermathCampaignBehavior);
            Seal31.Patch("lup miast L0: migawka bitwy (SiegeAftermath OnMapEventEnded)", AccessTools.Method(t, "OnMapEventEnded"), typeof(CitySack),
                         nameof(MapEndPre), null, nameof(MapEndFin), Priority.High, Priority.Normal, Priority.Low);
            Seal31.Patch("lup miast L0: nastepstwa (OnSiegeAftermathApplied)", AccessTools.Method(t, "OnSiegeAftermathApplied"), typeof(CitySack),
                         nameof(AftermathPre), null, null, Priority.High);
            Seal31.Patch("lup miast L0: warsztaty przy zmianie pana (ChangeOwnerOfSettlementAction.ApplyInternal)", AccessTools.Method(typeof(ChangeOwnerOfSettlementAction), "ApplyInternal"),
                         typeof(CitySack), nameof(OwnerPre), null, nameof(OwnerFin), Priority.High, Priority.Normal, Priority.Low);
        }
    }
}
