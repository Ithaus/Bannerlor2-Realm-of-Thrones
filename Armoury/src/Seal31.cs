using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using CW = Armoury.CirculationWindows;

namespace Armoury
{
    /// <summary>
    /// ETAP 3.1 - SZCZELNOSC PIENIADZA (projekt PROJEKT-ETAP3-SZCZELNOSC-STATKI 2026-10-10, wersja 2 po krytyce). Kawalki tego drzewa:
    /// E3/0 pomiar (Measure31), E3/1 prawdziwy platnik i kasy osad (RealPayer), E3/3 lup z cial z sakiewek pokonanych (CasualtyLoot),
    /// E3/5 awanse do sakiewki (UpgradePurse), E3/6 werbunek - dokladny zwrot (LevyGold), E3/7 kapital karawan i partie znikajace bez bitwy
    /// (CaravanCapital). Kazdy za wlasnym wylacznikiem (E3/0 - CirculationLedgerEnabled, E3/6 - RecruitGoldToSeller).
    ///
    /// Wspolne:
    ///  - WLASNE POZYCJE KSIEGI PIENIADZA (Sum): bez ruszania stalych CirculationWindows; kazda pozycja idzie do CW.NamedRun (probki swiata)
    ///    i w naszym ticku do CW.BlockNamed (RB) - ta sama droga co CW.AddWorld; w rozliczeniu rodu nic (D7 - siedzi w roznicy stanu).
    ///    Linia "Pieniadz swiata (bilans - przyczyny)" dopisuje je w ZRODLACH (K - korekta zrodel), UJSCIACH (U, M - minus oddane) i NETTO (N).
    ///  - NADWYZKA OSADY (rozdz. 2 projektu, jedna definicja): miasto - kasa ponad zapas kupcow (10 000 + 12 x dobrobyt, cel regulatora gry)
    ///    minus tarcze (zold i dwor w drodze do pana) minus to, co osada juz dzis wydala na nasze wyplaty; zamek - CastlePurse.Spendable minus
    ///    wydane dzis. Jeden licznik "wydane dzis" na osade, czyszczony co dobe.
    ///  - Linia dzienna "Szczelnosc (164a): dzien N | ..." - czesci kawalkow; potkniecia liczone, pierwszy wyjatek z miejsca do logu.
    /// </summary>
    internal static class Seal31
    {
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        // ------------------------------------------------------------ wlasne pozycje ksiegi (znak jak CW.AddWorld: +1 zrodlo/netto/minus-ujscie, -1 ujscie/korekta zrodel)
        internal const int UCtwLegacy = 0, UCtwBelow = 1, UPartyRemoved = 2, MUpgradeAi = 3, MUpgradePlayer = 4, MLordBuyPaid = 5, KCapFromPrice = 6, NCtwNet = 7,
                           IU8Disb = 8, Items = 9;
        private static readonly int[] Sign = { -1, -1, -1, +1, +1, +1, -1, +1, 0 };
        internal static readonly long[] Sum = new long[Items];

        internal static int Stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static void Stumble(string where, Exception e)
        {
            Stumbles++;
            try { if (_err.Add(where)) Log.Error("Seal31." + where, e); } catch { }
        }

        /// <summary>Jedyna droga zapisu wlasnych pozycji ksiegi. I* (znak 0) - tylko informacja. W rozliczeniu rodu nic (D7).</summary>
        internal static void AddWorld(int item, long value)
        {
            try
            {
                if (value == 0 || item < 0 || item >= Items) return;
                if (Sign[item] == 0) { Sum[item] += value; return; }
                if (MoneyLedger.InClanTick) { CW.InClanSkipped++; return; }
                Sum[item] += value;
                long v = Sign[item] * value;
                CW.NamedRun += v;
                if (MoneyLedger.InBlock) CW.BlockNamed += v;
            }
            catch (Exception e) { Stumble("AddWorld", e); }
        }

        /// <summary>Do sumy ZRODEL ksiegi (korekta: minus kapital nowych karawan z ceny kupna).</summary>
        internal static long SrcNow() { return -Sum[KCapFromPrice]; }
        /// <summary>Do sumy UJSC ksiegi (nowe ujscia minus zloto oddane odbiorcom przez 164a).</summary>
        internal static long SinkNow() { return Sum[UCtwLegacy] + Sum[UCtwBelow] + Sum[UPartyRemoved] - Sum[MUpgradeAi] - Sum[MUpgradePlayer] - Sum[MLordBuyPaid]; }
        /// <summary>Do NETTO ksiegi (zmiana pola netto portfeli wezwan bez platnika).</summary>
        internal static long NetNow() { return Sum[NCtwNet]; }

        private static string Sg(long v) { return (v >= 0 ? "+" : "") + v; }

        internal static string SrcText()
        {
            return ", minus kapital nowych karawan z ceny kupna (164a-6) " + Sum[KCapFromPrice]
                   + " (w tym statki zatopione w bitwach morskich - w \"bitwy\", informacja 164a-0: " + Measure31.DayShipBattleGold + ")";
        }

        internal static string SinkText()
        {
            return ", portfele wezwan do wojny (168): dlug portfela sprzed paczki ze skarbcow " + Sum[UCtwLegacy] + ", wplaty do ujemnego portfela wezwanego " + Sum[UCtwBelow]
                   + ", kiesy partii usunietych bez zniszczenia (U8r) " + Sum[UPartyRemoved] + " (" + Measure31.RemovedText() + ")"
                   + "; w U8 rozwiazane (niszczyciel null) " + Sum[IU8Disb]
                   + ", minus zloto oddane odbiorcom przez 164a " + (Sum[MUpgradeAi] + Sum[MUpgradePlayer] + Sum[MLordBuyPaid])
                   + " (awanse AI do sakiewek i kas " + Sum[MUpgradeAi] + ", awanse gracza do sakiewki jego ludzi " + Sum[MUpgradePlayer]
                   + ", kupno karawan BK - kapital i kasa miasta " + Sum[MLordBuyPaid] + ")";
        }

        internal static string NetText() { return ", portfele wezwan - zmiana pola netto bez platnika (168) " + Sg(Sum[NCtwNet]); }

        internal static string Keys() { return " S31=" + SrcNow() + " U31=" + SinkNow() + " N31=" + NetNow(); }

        // ------------------------------------------------------------ nadwyzka osady (rozdz. 2) i licznik "wydane dzis"
        private static readonly Dictionary<Settlement, long> _spent = new Dictionary<Settlement, long>();

        /// <summary>Zapas kupcow miasta = cel regulatora gry (10 000 + 12 x dobrobyt).</summary>
        internal static long TownReserve(Town t) { return t == null ? 0 : 10000L + (long)(12f * t.Prosperity); }

        /// <summary>Nadwyzka osady na nasze wyplaty (miasto: kasa - zapas kupcow - tarcze - wydane dzis; zamek: CastlePurse.Spendable - wydane dzis). Wies i inne - 0.</summary>
        internal static long Spare(Settlement st)
        {
            try
            {
                if (st == null || st.Town == null) return 0;
                var t = st.Town;
                long free;
                if (st.IsTown) free = (long)t.Gold - TownReserve(t) - SoldierPay.HeldOf(st) - SoldierPay.CourtOf(st);
                else free = CastlePurse.Spendable(t);
                long sp; _spent.TryGetValue(st, out sp);
                return Math.Max(0L, free - sp);
            }
            catch (Exception e) { Stumble("Spare", e); return 0; }
        }

        internal static void NoteSpent(Settlement st, long x)
        {
            if (st == null || x <= 0) return;
            long v; _spent.TryGetValue(st, out v); _spent[st] = v + x;
        }

        // ------------------------------------------------------------ wspolni pomocnicy
        /// <summary>Najblizsze miasto krolestwa (faction) albo, gdy go nie ma, najblizsze miasto w ogole.</summary>
        internal static Settlement NearestTownOf(TaleWorlds.Library.Vec2 p, IFaction faction)
        {
            Settlement best = null, any = null; float bd = float.MaxValue, ad = float.MaxValue;
            foreach (var t in Settlement.All)
            {
                if (t == null || !t.IsTown || t.Town == null) continue;
                float d = p.DistanceSquared(t.GetPosition2D);
                if (d < ad) { ad = d; any = t; }
                if (faction != null && t.MapFaction == faction && d < bd) { bd = d; best = t; }
            }
            return best ?? any;
        }

        // ------------------------------------------------------------ wpiecia
        internal static Harmony H;
        internal static readonly List<string> Wired = new List<string>(), Missing = new List<string>();

        internal static void Patch(string label, System.Reflection.MethodBase m, Type owner, string pre, string post, string fin, int prePrio = Priority.Normal, int postPrio = Priority.Normal, int finPrio = Priority.Normal)
        {
            try
            {
                if (H == null) { Missing.Add(label + " (brak Harmony)"); return; }
                if (m == null) { Missing.Add(label); return; }
                H.Patch(m, prefix: pre != null ? new HarmonyMethod(owner, pre) { priority = prePrio } : null,
                           postfix: post != null ? new HarmonyMethod(owner, post) { priority = postPrio } : null,
                           finalizer: fin != null ? new HarmonyMethod(owner, fin) { priority = finPrio } : null);
                Wired.Add(label);
            }
            catch (Exception e) { Missing.Add(label + " (blad: " + e.Message + ")"); }
        }

        /// <summary>SubModuleMain - latki na typach gry i modow (po oknach ksiegi obiegu).</summary>
        internal static void ApplyAll(Harmony h)
        {
            H = h;
            try { Measure31.ApplyAll(); } catch (Exception e) { Stumble("Measure31.ApplyAll", e); }
            try { UpgradePurse.ApplyAll(); } catch (Exception e) { Stumble("UpgradePurse.ApplyAll", e); }
            try { CaravanCapital.ApplyAll(); } catch (Exception e) { Stumble("CaravanCapital.ApplyAll", e); }
            try { RealPayer.ApplyAll(); } catch (Exception e) { Stumble("RealPayer.ApplyAll", e); }
            Log.Info("Szczelnosc (164a): latki przy starcie gry - wpiete: " + (Wired.Count > 0 ? string.Join(", ", Wired.ToArray()) : "-")
                     + "; BRAK: " + (Missing.Count > 0 ? string.Join(", ", Missing.ToArray()) : "-") + ".");
            Wired.Clear(); Missing.Clear();
        }

        private static bool _hooksTried;

        /// <summary>ArmouryBehavior.OnSessionLaunched - latki zakladane w kampanii (modele, AllianceCampaignBehavior), raz na proces.</summary>
        internal static void EnsureHooks()
        {
            if (_hooksTried || H == null || Campaign.Current == null) return;
            _hooksTried = true;
            try { Measure31.EnsureHooks(); } catch (Exception e) { Stumble("Measure31.EnsureHooks", e); }
            try { CaravanCapital.EnsureHooks(); } catch (Exception e) { Stumble("CaravanCapital.EnsureHooks", e); }
            Log.Info("Szczelnosc (164a): latki w kampanii - wpiete: " + (Wired.Count > 0 ? string.Join(", ", Wired.ToArray()) : "-")
                     + "; BRAK: " + (Missing.Count > 0 ? string.Join(", ", Missing.ToArray()) : "-") + ".");
            Wired.Clear(); Missing.Clear();
        }

        /// <summary>ArmouryBehavior.RegisterEvents - zaraz po nasluchu ksiegi obiegu (MobilePartyDestroyed: nasz biegnie przedostatni, po 3.2 i 112).</summary>
        internal static void RegisterEvents(CampaignBehaviorBase owner)
        {
            Reset();                                        // nowa kampania / wczytanie - stan doby i kontekstu od zera (nic z tego nie idzie do zapisu)
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(owner, OnPartyDestroyed);
            CampaignEvents.OnPartyRemovedEvent.AddNonSerializedListener(owner, OnPartyRemoved);
            CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(owner, Measure31.OnKingdomDestroyed);
            CampaignEvents.HeroOrPartyTradedGold.AddNonSerializedListener(owner, OnGoldTraded);
        }

        private static void OnPartyDestroyed(MobileParty mp, PartyBase destroyer)
        {
            try { Measure31.MarkDestroyed(mp); } catch (Exception e) { Stumble("MarkDestroyed", e); }
            try { if (destroyer == null) CaravanCapital.RoutePurse(mp, 0); } catch (Exception e) { Stumble("CaravanCapital.RoutePurse", e); }   // E3/7: rozwiazana - kiesa do glowy rodu
        }

        private static void OnPartyRemoved(PartyBase party)
        {
            try { Measure31.OnPartyRemoved(party); } catch (Exception e) { Stumble("OnPartyRemoved", e); }
        }

        private static void OnGoldTraded((Hero, PartyBase) giver, (Hero, PartyBase) recipient, (int, string) amount, bool show)
        {
            try { Measure31.OnGold(giver.Item1, giver.Item2, recipient.Item1, recipient.Item2, amount.Item1); } catch (Exception e) { Stumble("Measure31.OnGold", e); }
            try { LevyGold.OnGold(giver.Item1, giver.Item2, recipient.Item1, recipient.Item2, amount.Item1); } catch (Exception e) { Stumble("LevyGold.OnGold", e); }   // E3/6
        }

        /// <summary>ArmouryBehavior - tuz przed MoneyLedger.BlockOpen (poczatek naszego rozliczenia doby).</summary>
        internal static void BlockOpen()
        {
            try { Measure31.BlockOpen(); } catch (Exception e) { Stumble("Measure31.BlockOpen", e); }
        }

        internal static void Reset()
        {
            try
            {
                Array.Clear(Sum, 0, Items); _spent.Clear(); Stumbles = 0; _err.Clear();
                Measure31.Reset();
                CaravanCapital.Reset();
                RealPayer.Reset();
            }
            catch { }
        }

        // ------------------------------------------------------------ raz na dobe - PO MoneyLedger.Daily (ksiega czyta pozycje tej doby)
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                var s = Settings.Current;
                if (s != null && s.LogEnabled)
                {
                    int day = (int)CampaignTime.Now.ToDays - 1;
                    var sb = new StringBuilder(2048);
                    sb.Append("Szczelnosc (164a): dzien ").Append(day);
                    sb.Append(" | platnik (164a-1): ").Append(RealPayer.Part());
                    sb.Append(" | awanse (164a-4): ").Append(UpgradePurse.Part());
                    sb.Append(" | kapital (164a-6): ").Append(CaravanCapital.Part());
                    sb.Append(" | pomiar (164a-0): ").Append(Measure31.Part());
                    if (Stumbles > 0) sb.Append(" | potkniecia ").Append(Stumbles).Append(" (pierwszy wyjatek z miejsca w logu jako ERROR)");
                    sb.Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }

        private static void ClearDay()
        {
            try { Array.Clear(Sum, 0, Items); _spent.Clear(); Stumbles = 0; } catch { }
            try { Measure31.ClearDay(); } catch { }
            try { UpgradePurse.ClearDay(); } catch { }
            try { CaravanCapital.ClearDay(); } catch { }
            try { RealPayer.ClearDay(); } catch { }
        }
    }
}
