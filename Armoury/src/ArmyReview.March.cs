using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// Paczka 133d (Jeff 25.10 ok. 12:57: "... przeniesmy logi do zakladki armii, aby tam wszystko bylo czytelne ile im place jaki zold, jakie konie jaka predkosc
    /// ile koni i ile zbroi potrzeba aby wojsko jechalo na koniach, ile brakuje ile za duzo koni i jest herding"; projekt 2.3 / 2.4 z krytyka K8, K9).
    /// HORSES AND HARNESS (przebudowa): gotowosc jezdzcow z podgladu nastepnej bitwy (KitPreview - ten sam rozdzielacz DTE i te same straze co bitwa: Jazda,
    /// kraj wierzchowca, uprzaz E8/H), nowy czytnik rodzajow koni po zbrojowni DTE gracza i sakwach (Stables.IsPlainMount, Stables.Level; juczne i bydlo -
    /// liczniki gry), ile koni i uprzezy kupic albo przeniesc z sakw (KitPreview.MountsOf - te same predykaty co przydzial), ile stoi bezczynnie.
    /// "Ile zbroi" = uprzaz konia (siodlo albo ladry - regula jezdzca E8/H). Bez podgladu (TroopsFightWithOwnKitOnly wylaczone, brak DTE, otwarta zbrojownia) -
    /// stara strona z SaddleLaw.Pairs. SaddleLaw.Pairs (arytmetyka z rannymi, bez Jazdy) zostaje w linii logu jako kontrola.
    /// ON THE MARCH -> trzy strony: SPEED - rozpiska modelu predkosci gry wprost (jak dymek gry: CalculateFinalSpeed(CalculateBaseSpeed z opisami), bez zapisu
    /// predkosci i stanu drogi do pamieci partii - MapRoads.Explained), dla gracza doczepionego do armii - predkosc wodza (K8: dymek gry i MobileParty.CalculateSpeed),
    /// linie mnoznikow glownie w procentach (linia / BaseNumber - K9), reszta bez nazwy jako "Other"; porownanie z predkoscia gry (rozjazd > 2% - log).
    /// HERD - regula gry (DefaultPartySpeedCalculatingModel: stado = juczne + bydlo + luzaki ponad pieszych, po partii i doczepionych - K8; kara tylko ponad
    /// liczbe ludzi), procent z linii "Herding" (nazwa ze statycznego _textHerd gry), "co gdyby" przez prywatne GetHerdingModifier gry na instancji Default
    /// z listy modeli (K9 - bez przepisywania wzoru); obok nasza regula taboru kolumny (decyzja (c) - tylko pokazac, nie scalac). COLUMN - MarchText bez zmian
    /// logiki + zdanie o regule jezdzca na mapie (decyzja (b) - tylko pokazac). Tylko odczyt; potkniecia liczone per miejsce (ArmyReview.Stumble).
    /// </summary>
    internal static partial class ArmyReview
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int LinesPerPara = 8, FootTypesPerPara = 4, FootTypesShown = 8;

        private static string F2(float f) { return f.ToString("0.00", Inv); }
        private static string S2(float f) { return f.ToString("+0.00;-0.00;0.00", Inv); }
        private static string Pc(float p) { return (p * 100f).ToString("+0.#;-0.#;0", Inv) + "%"; }
        private static string Pl(int n, string one, string many) { return n + " " + (n == 1 ? one : many); }

        // ------------------------------------------------------------ czytnik rodzajow koni (zbrojownia DTE gracza, sakwy, przydzial podgladu)
        /// <summary>Rodzaje zwierzat i uprzezy: zwykle / bojowe / szlachetne wierzchowce pod siodlo (Stables.IsPlainMount + Stables.Level), inne wierzchowce
        /// (wielblady, slonie...), juczne i bydlo (kolejnosc jak licznik gry ItemRoster: juczne, wierzchowiec, bydlo), uprzaz rodziny konia (SaddleLaw.Fits)
        /// i inna (wielblady, siodla juczne).</summary>
        internal sealed class Herdbook
        {
            public int Plain, War, Noble, Other, Pack, Live, Harness, HarnessOther;
            public int Riding { get { return Plain + War + Noble; } }
        }

        private static void Classify(Herdbook h, ItemObject it, int n)
        {
            if (it == null || n <= 0) return;
            if (it.ItemType == ItemObject.ItemTypeEnum.Horse)
            {
                var hc = it.HorseComponent;
                if (hc == null) return;
                if (hc.IsPackAnimal) { h.Pack += n; return; }
                if (hc.IsMount)
                {
                    if (Stables.IsPlainMount(it))
                    {
                        int l = Stables.Level(it.ItemCategory);
                        if (l == 2) h.Noble += n; else if (l == 1) h.War += n; else h.Plain += n;
                    }
                    else h.Other += n;
                    return;
                }
                if (hc.IsLiveStock) h.Live += n;
            }
            else if (it.ItemType == ItemObject.ItemTypeEnum.HorseHarness)
            {
                if (SaddleLaw.Fits(it)) h.Harness += n; else h.HarnessOther += n;
            }
        }

        internal static Herdbook ReadHerd(ItemRoster r)
        {
            var h = new Herdbook();
            if (r == null) return h;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                Classify(h, el.EquipmentElement.Item, el.Amount);
            }
            return h;
        }

        private static Herdbook ReadHerd(Dictionary<ItemObject, int> d)
        {
            var h = new Herdbook();
            if (d != null) foreach (var kv in d) Classify(h, kv.Key, kv.Value);
            return h;
        }

        // ------------------------------------------------------------ Horses and harness
        /// <summary>Liczby strony "Horses and harness" (te same do okna i linii logu).</summary>
        internal sealed class HorseView
        {
            public bool Ok;                                   // podglad nastepnej bitwy policzony (inaczej strona z SaddleLaw.Pairs)
            public KitPreview.Result R;
            public KitPreview.Mounts M;
            public Herdbook Arm, Bag, Rode;
            public bool ArmRead;
            public int Riders, Ready, Foot, NoHorse, NoSaddle;  // podglad: zdrowi z koniem we wzorcu, gotowi, pieszo (bez konia / kon bez uprzezy)
            public int WRiders, WHarness, MapRiders;           // ranni jezdzcy (wzorzec z koniem pod siodlo; z nich objeci regula uprzezy), zdrowi z typu konnego
            public int SadFit, LeftHorse, Reserve, Idle, LeftHarness, IdleHarness;
            public int GameMounts, Packs, Live, BagHarness;    // liczniki gry w sakwach, uprzaz rodziny konia w sakwach
        }

        internal static HorseView HorsesOf(MobileParty mp, KitPreview.Result r, int[] fit)
        {
            var v = new HorseView { R = r };
            if (mp == null || r == null || !r.Ok) return v;
            v.Ok = true;
            v.M = KitPreview.MountsOf(r, mp);
            v.Riders = r.Riders; v.NoHorse = r.NoHorse; v.NoSaddle = r.NoSaddle; v.Foot = r.Foot; v.Ready = Math.Max(0, r.Riders - r.Foot);
            var arm = QuartermasterLaw.DteArmory();
            v.ArmRead = arm != null;
            v.Arm = ReadHerd(arm);
            v.Bag = ReadHerd(mp.ItemRoster);
            v.Rode = ReadHerd(r.Rode);
            foreach (var kv in r.Saddles) if (kv.Key != null && SaddleLaw.Fits(kv.Key)) v.SadFit += kv.Value;
            var mr = mp.MemberRoster;
            if (mr != null)
                for (int i = 0; i < mr.Count; i++)
                {
                    var el = mr.GetElementCopyAtIndex(i);
                    var ch = el.Character;
                    if (ch == null || ch.IsHero || el.WoundedNumber <= 0) continue;
                    var e = ch.Equipment;
                    if (e == null || !Stables.IsPlainMount(e[EquipmentIndex.Horse].Item)) continue;
                    int w = Math.Min(el.Number, el.WoundedNumber);
                    v.WRiders += w;
                    if (SaddleLaw.Needs(ch)) v.WHarness += w;
                }
            v.MapRiders = fit != null && fit.Length > 3 ? fit[2] + fit[3] : 0;
            v.LeftHorse = Math.Max(0, v.Arm.Riding - v.Rode.Riding);
            v.Reserve = Math.Min(v.LeftHorse, v.WRiders);
            v.Idle = Math.Max(0, v.LeftHorse - v.Reserve - v.M.RemHorseFit);
            v.LeftHarness = Math.Max(0, v.Arm.Harness - v.SadFit);
            v.IdleHarness = Math.Max(0, v.LeftHarness - v.M.UseHarness - v.WHarness);
            var bag = mp.ItemRoster;
            if (bag != null) { v.GameMounts = bag.NumberOfMounts; v.Packs = bag.NumberOfPackAnimals; v.Live = bag.NumberOfLivestockAnimals; }
            v.BagHarness = SaddleLaw.BagFit(bag);
            return v;
        }

        /// <summary>Strona koni okna: z podgladu, a bez niego - stara strona z SaddleLaw.Pairs.</summary>
        internal static string HorsePageText(MobileParty mp, SaddleLaw.PairState p, HorseView v, MarchPace.Column col)
        {
            if (v == null || !v.Ok) return HorseText(mp, p);
            var sb = new StringBuilder();
            bool harness = SaddleLaw.On;
            // A. bitwa (podglad)
            if (v.Riders <= 0)
                sb.Append("No fit man of yours has a horse in his kit - nobody to put in the saddle for the next battle")
                  .Append(v.WRiders > 0 ? " (" + Pl(v.WRiders, "wounded rider stays", "wounded riders stay") + " out)." : ".");
            else
            {
                sb.Append("Riders for the next battle: ").Append(Pl(v.Riders, "fit man", "fit men")).Append(" whose kit has a horse");
                if (v.WRiders > 0) sb.Append(" (").Append(Pl(v.WRiders, "wounded rider stays", "wounded riders stay")).Append(" out)");
                sb.Append(".\nReady to ride: ").Append(v.Ready).Append(" - on a horse").Append(harness ? " with harness" : "").Append('.');
                sb.Append("\nOn foot: ").Append(v.Foot);
                var why = new List<string>();
                if (v.NoHorse > 0) why.Add(v.NoHorse + " with no horse for them in the troops' armoury");
                if (v.NoSaddle > 0) why.Add(v.NoSaddle + " with a horse but no harness for it (the horse stays in the armoury)");
                if (why.Count > 0) sb.Append(" - ").Append(string.Join(", ", why.ToArray()));
                sb.Append('.');
            }
            // B. typy z jezdzcami pieszo - pierwsza przyczyna (ta sama co strona typu w "Kit for the next battle")
            var foot = new List<KitPreview.TypeRow>();
            foreach (var t in v.R.Rows) if (t.Foot > 0) foot.Add(t);
            int shown = Math.Min(foot.Count, FootTypesShown);
            for (int i = 0; i < shown; i += FootTypesPerPara)
            {
                sb.Append("\n\n");
                if (i == 0) sb.Append("By troop:");
                for (int k = i; k < shown && k < i + FootTypesPerPara; k++)
                {
                    var t = foot[k];
                    if (k > i || i == 0) sb.Append('\n');   // pierwszy akapit: po "By troop:"; nastepne bez pustej linii na poczatku
                    sb.Append(t.Name).Append(" x").Append(t.Riders).Append(": ");
                    var bits = new List<string>();
                    if (t.NoHorse > 0) bits.Add(t.NoHorse + " no horse");
                    if (t.NoSaddle > 0) bits.Add(t.NoSaddle + " no harness");
                    sb.Append(string.Join(", ", bits.ToArray()));
                    string reason = t.NoHorse > 0 && t.HorseWhy.Count > 0 ? t.HorseWhy[0].Text : t.NoSaddle > 0 && t.SaddleWhy.Count > 0 ? t.SaddleWhy[0].Text : null;
                    if (!string.IsNullOrEmpty(reason)) sb.Append(" - ").Append(reason);
                }
            }
            if (foot.Count > shown) sb.Append("\nAnd ").Append(Pl(foot.Count - shown, "more kind", "more kinds")).Append(" - see Kit for the next battle.");
            // C. zbrojownia
            sb.Append("\n\n");
            if (!v.ArmRead) sb.Append("The troops' armoury (Dynamic Troop Equipment) cannot be read now.");
            else
            {
                var a = v.Arm;
                sb.Append("Troops' armoury: ").Append(Pl(a.Riding, "riding horse", "riding horses")).Append(" (").Append(a.Plain).Append(" plain, ").Append(a.War).Append(" war, ")
                  .Append(a.Noble).Append(" noble) - ").Append(v.Rode.Riding).Append(" ridden in the next battle, ").Append(v.LeftHorse).Append(" left in the armoury");
                if (a.Other > 0 || v.Rode.Other > 0) sb.Append("; ").Append(Pl(a.Other, "other mount", "other mounts")).Append(" (camels, elephants and the like), ").Append(v.Rode.Other).Append(" ridden");
                sb.Append('.');
                sb.Append("\nHarness for horses (a saddle or a barding): ").Append(a.Harness).Append(" - ").Append(v.SadFit).Append(" on the horses ridden, ").Append(v.LeftHarness).Append(" left");
                if (a.HarnessOther > 0) sb.Append("; and ").Append(a.HarnessOther).Append(" for camels and other beasts or pack saddles");
                sb.Append('.');
            }
            // D. sakwy
            var b = v.Bag;
            sb.Append("\n\nBaggage: ").Append(Pl(b.Riding, "riding horse", "riding horses")).Append(" (").Append(b.Plain).Append(" plain, ").Append(b.War).Append(" war, ").Append(b.Noble).Append(" noble)");
            if (b.Other > 0) sb.Append(", ").Append(Pl(b.Other, "other mount", "other mounts"));
            sb.Append(", ").Append(Pl(v.Packs, "pack animal", "pack animals")).Append(", ").Append(v.Live).Append(" livestock, ").Append(v.BagHarness).Append(" harness for horses.");
            if (v.GameMounts != b.Riding + b.Other) sb.Append(" The game counts ").Append(v.GameMounts).Append(" of the mounts as spare horses (it leaves out horses with a flaw).");
            // E. co kupic / przeniesc
            var m = v.M;
            sb.Append("\n\n");
            if (m.NeedHorse <= 0 && m.NeedHarness <= 0) sb.Append("To put every fit rider in the saddle: nothing to buy.");
            else
            {
                var buy = new List<string>();
                if (m.BuyHorse > 0) buy.Add(Pl(m.BuyHorse, "horse", "horses") + (m.HardTypes > 0 ? " their riders can ride" : ""));
                if (m.BuyHarness > 0) buy.Add(m.BuyHarness + " harness");
                var move = new List<string>();
                if (m.BagHorseFit > 0) move.Add(Pl(m.BagHorseFit, "horse", "horses"));
                if (m.MoveHarness > 0) move.Add(m.MoveHarness + " harness");
                sb.Append("To put every fit rider in the saddle (").Append(Pl(m.NeedHorse, "horse", "horses")).Append(" and ").Append(m.NeedHarness).Append(" harness short): ")
                  .Append(buy.Count > 0 ? "buy " + string.Join(" and ", buy.ToArray()) : "nothing to buy");
                if (move.Count > 0) sb.Append(buy.Count > 0 ? "; " : " - ").Append(string.Join(" and ", move.ToArray())).Append(" already in your baggage - put them in the troops' armoury");
                sb.Append('.');
                if (m.UseHarness > 0) sb.Append('\n').Append(m.UseHarness).Append(" harness left in the troops' armoury count towards it.");
                if (m.RemHorseFit > 0) sb.Append('\n').Append(Pl(m.RemHorseFit, "horse", "horses")).Append(" left in the troops' armoury would suit them, but the hand-out did not give them out - see the troop's page under Kit for the next battle.");
                if (m.HardTypes > 0) sb.Append("\nSome riders lack a horse only because the ones in the troops' armoury are too hard for them to ride (Requires Riding) - they need easier horses.");
                if (m.BagHorseFit > 0 && col != null && col.RidingFoot > 0)
                    sb.Append("\nA horse moved out of the baggage no longer carries a footman on the march (now ").Append(Pl(col.RidingFoot, "footman rides", "footmen ride")).Append(" spare horses).");
            }
            // F. bezczynne
            if (v.ArmRead)
            {
                var idle = new List<string>();
                if (v.Idle > 0) idle.Add(Pl(v.Idle, "riding horse", "riding horses"));
                if (v.IdleHarness > 0) idle.Add(v.IdleHarness + " harness");
                sb.Append("\n\n");
                if (idle.Count > 0)
                    sb.Append("Idle in the troops' armoury: ").Append(string.Join(" and ", idle.ToArray())).Append(" - more than every rider needs, the wounded included")
                      .Append(v.Reserve > 0 ? " (" + Pl(v.Reserve, "more horse waits", "more horses wait") + " for the wounded)" : "")
                      .Append(". Sell them, or move horses to your baggage: there a spare horse carries a footman on the march, or counts in the herd when there is no footman to carry.");
                else
                    sb.Append("Nothing stands idle in the troops' armoury").Append(v.Reserve > 0 ? " - the " + Pl(v.Reserve, "horse left waits", "horses left wait") + " for your wounded riders." : ".");
            }
            // G. dwie reguly jezdzca (decyzja (b) - tylko pokazac)
            sb.Append("\n\nOn the map a man rides by his troop: ").Append(Pl(v.MapRiders, "fit man of a mounted troop counts", "fit men of mounted troops count"))
              .Append(" as riders on the march (the game's Cavalry bonus and our marching column), even one who would fight on foot. In battle only a horse")
              .Append(harness ? " and a harness" : "").Append(" from the troops' armoury puts a man in the saddle.");
            if (col != null && col.Kind == 0 && col.Walkers > 0)
                sb.Append("\nOn the march ").Append(Pl(col.Walkers, "man walks", "men walk")).Append(": ").Append(col.Walkers).Append(" more spare horses in the baggage would put the whole column on horseback (see On the march).");
            sb.Append("\nIn sieges and hideouts every rider fights on foot.");
            return sb.ToString();
        }

        /// <summary>Wiersz glownego okna "Horses and harness" (bez podgladu - z SaddleLaw.Pairs jak dotad).</summary>
        private static InquiryElement HorseRow(SaddleLaw.PairState p, HorseView v)
        {
            if (v == null || !v.Ok)
            {
                int riders = p.Riders + p.Exempt;
                int foot = FootInBattle(p);
                return new InquiryElement("horses", riders == 0 ? "Horses and harness: no riders in your party" : "Horses and harness: " + (riders - foot) + " of " + riders + " riders ready to ride",
                                          null, true, foot > 0 ? foot + " riders will fight on foot - the page says why." : "Horses and harness in the troops' armoury and in your baggage.");
            }
            string title;
            if (v.Riders <= 0) title = "Horses and harness: no rider among your fit men";
            else
            {
                title = "Horses and harness: " + v.Ready + " of " + v.Riders + " riders ready";
                var buy = new List<string>();
                if (v.M.BuyHorse > 0) buy.Add(Pl(v.M.BuyHorse, "horse", "horses"));
                if (v.M.BuyHarness > 0) buy.Add(v.M.BuyHarness + " harness");
                if (buy.Count > 0) title += " - buy " + string.Join(", ", buy.ToArray());
                else if (v.Idle > 0) title += " - " + Pl(v.Idle, "horse", "horses") + " idle";
            }
            string hint = v.Foot > 0 ? Pl(v.Foot, "rider", "riders") + " would fight on foot - the page says why, what to buy and what is already in your baggage."
                                     : "The horses and harness in the troops' armoury and in your baggage: what is ridden and what stands idle (the herd is under On the march).";
            return new InquiryElement("horses", title, null, true, hint);
        }

        private static InquiryElement HorseRowSafe(SaddleLaw.PairState p, HorseView v)
        {
            try { return HorseRow(p, v); }
            catch (Exception e) { Stumble("HorseRow", e); return new InquiryElement("horses", "Horses and harness (not available)", null, false, "The horses cannot be counted now."); }
        }

        /// <summary>Segment linii raportu: konie (133d) - te same liczby co strona, plus SaddleLaw.Pairs jako kontrola.</summary>
        private static string HorseBrief(HorseView v, SaddleLaw.PairState p)
        {
            string pairs = "Pairs (z rannymi, bez Jazdy): jezdzcy " + (p.Riders + p.Exempt) + ", bez konia " + p.NoHorse + ", kon bez uprzezy " + p.NoSaddle;
            if (v == null || !v.Ok) return "podglad niedostepny - strona z SaddleLaw.Pairs | " + pairs;
            var a = v.Arm; var b = v.Bag; var m = v.M;
            bool id = v.Ready + v.Foot == v.Riders && (!v.ArmRead || v.Rode.Riding <= a.Riding)
                      && m.BuyHorse + m.RemHorseFit + m.BagHorseFit == m.NeedHorse && m.UseHarness + m.MoveHarness + m.BuyHarness == m.NeedHarness;
            return "podglad: jezdzcow " + v.Riders + ", gotowych " + v.Ready + ", bez konia " + v.NoHorse + ", bez uprzezy " + v.NoSaddle + ", rannych jezdzcow " + v.WRiders
                   + ", z typu konnego (mapa) " + v.MapRiders
                   + " | zbrojownia " + (v.ArmRead ? "konie " + a.Riding + " (" + a.Plain + "/" + a.War + "/" + a.Noble + ", inne " + a.Other + "), w bitwie " + v.Rode.Riding + " (inne " + v.Rode.Other
                                                     + "), zostaje " + v.LeftHorse + " (dla rannych " + v.Reserve + ", bezczynne " + v.Idle + "), uprzaz " + a.Harness + " (inna " + a.HarnessOther
                                                     + "), na koniach " + v.SadFit + ", zostaje " + v.LeftHarness + ", bezczynna " + v.IdleHarness
                                                   : "NIECZYTELNA")
                   + " | sakwy: konie " + b.Riding + " (" + b.Plain + "/" + b.War + "/" + b.Noble + ", inne " + b.Other + "; licznik gry " + v.GameMounts + "), juczne " + v.Packs + ", bydlo " + v.Live
                   + ", uprzaz " + v.BagHarness
                   + " | brak koni " + m.NeedHorse + " (resztka zbrojowni " + m.RemHorseFit + ", sakwy " + m.BagHorseFit + ", kupic " + m.BuyHorse + "; typy 'za trudny kon' " + m.HardTypes
                   + "), uprzezy " + m.NeedHarness + " (resztka " + m.UseHarness + ", sakwy " + m.MoveHarness + ", kupic " + m.BuyHarness + ")"
                   + " | " + pairs + " | tozsamosc " + (id ? "zgodna" : "ROZJAZD");
        }

        // ------------------------------------------------------------ On the march: Speed i Herd
        internal sealed class SpeedLine { public string Name; public float Number, Pct = float.NaN; public int Op = -1; public bool Shown; }   // Op: 0 baza, 1 dodatek, 2 mnoznik, 3 granica, -1 nieznany

        /// <summary>Liczby stron Speed i Herd (te same do okna, linii logu i testu).</summary>
        internal sealed class MarchView
        {
            public bool Ok; public string Why;
            public MobileParty Who; public bool Army; public string Leader;
            public float Result, BaseNumber, Sum, Other, Game = -1f, Own = -1f;
            public bool OpsKnown;
            public readonly List<SpeedLine> Lines = new List<SpeedLine>();
            // stado gry - wejscia jak AddCargoStats gry, po partii predkosci i jej doczepionych (K8)
            public int Men, Foot, Mounts, Pack, Live, RidingFoot, Spare, Herd, Excess, Parties;
            public bool HerdLine;                                                          // jest linia "Herding" w rozpisce
            public float HerdPct = float.NaN, HerdUnits, ShepPct = float.NaN, ShepUnits;   // z linii rozpiski: procent = linia mnoznika / BaseNumber (K9); NaN - brak albo linia nie-mnoznik
            public float HerdGame = float.NaN, HerdNoSpare = float.NaN;                    // GetHerdingModifier gry (K9)
            public string HerdWhy;
            public long Ms;
        }

        private static bool _spLooked;
        private static FieldInfo _fExplainer, _fLnOp, _fHerdText;
        private static PropertyInfo _pLines, _pBaseLine;
        private static MethodInfo _herdM;
        private static string _spWiring = "nie sprawdzane";

        /// <summary>Refleksja raz (pulapka 15.10 - Declared*): rodzaj linii rozpiski (ExplainedNumber._explainer: Lines, BaseLine, OperationType), nazwa
        /// "Herding" gry (_textHerd), GetHerdingModifier(int, int). Brak - procenty i "co gdyby" znikaja, liczby rozpiski zostaja.</summary>
        private static void SpeedWire()
        {
            if (_spLooked) return;
            _spLooked = true;
            var miss = new List<string>();
            try
            {
                _fExplainer = AccessTools.DeclaredField(typeof(ExplainedNumber), "_explainer");
                var tEx = _fExplainer != null ? _fExplainer.FieldType : null;
                _pLines = tEx != null ? AccessTools.DeclaredProperty(tEx, "Lines") : null;
                _pBaseLine = tEx != null ? AccessTools.DeclaredProperty(tEx, "BaseLine") : null;
                var tLn = tEx != null ? tEx.GetNestedType("ExplanationLine", BindingFlags.Public | BindingFlags.NonPublic) : null;
                _fLnOp = tLn != null ? AccessTools.DeclaredField(tLn, "OperationType") : null;
            }
            catch (Exception e) { Stumble("SpeedWire", e); }
            if (_fExplainer == null || _pLines == null || _pBaseLine == null || _fLnOp == null) { miss.Add("rodzaje linii"); _fExplainer = null; }
            try
            {
                _herdM = AccessTools.DeclaredMethod(typeof(DefaultPartySpeedCalculatingModel), "GetHerdingModifier", new[] { typeof(int), typeof(int) });
                if (_herdM != null && _herdM.ReturnType != typeof(float)) _herdM = null;
                _fHerdText = AccessTools.DeclaredField(typeof(DefaultPartySpeedCalculatingModel), "_textHerd");
                if (_fHerdText != null && (!_fHerdText.IsStatic || _fHerdText.FieldType != typeof(TextObject))) _fHerdText = null;
            }
            catch (Exception e) { Stumble("SpeedWire.Herd", e); }
            if (_herdM == null) miss.Add("GetHerdingModifier");
            if (_fHerdText == null) miss.Add("_textHerd");
            _spWiring = miss.Count == 0 ? "wpiecia 3/3" : "BRAK " + string.Join(", ", miss.ToArray());
        }

        /// <summary>Stan wpiec stron marszu do linii startu sesji.</summary>
        internal static string MarchWiring { get { SpeedWire(); return _spWiring; } }

        /// <summary>Rodzaj kazdej linii GetLines (ta sama kolejnosc co w grze: baza, linie Add/Multiply w kolejnosci dopisania, granice). null - nieznane.</summary>
        private static int[] OpsOf(ExplainedNumber en, int lines)
        {
            if (_fExplainer == null) return null;
            try
            {
                object ex = _fExplainer.GetValue(en);
                if (ex == null) return null;
                var l = _pLines.GetValue(ex, null) as IList;
                if (l == null) return null;
                bool hasBase = _pBaseLine.GetValue(ex, null) != null;
                int off = hasBase ? 1 : 0;
                if (lines < l.Count + off || lines > l.Count + off + 2) return null;
                var ops = new int[lines];
                for (int i = 0; i < lines; i++)
                {
                    if (hasBase && i == 0) { ops[i] = 0; continue; }
                    int j = i - off;
                    if (j >= l.Count) { ops[i] = 3; continue; }
                    int op = Convert.ToInt32(_fLnOp.GetValue(l[j]));
                    ops[i] = op == 1 ? 1 : op == 2 ? 2 : -1;   // gra: Base 0, Add 1, Multiply 2, LimitMin 3, LimitMax 4 (w Lines tylko Add i Multiply)
                }
                return ops;
            }
            catch (Exception e) { Stumble("OpsOf", e); return null; }
        }

        /// <summary>Instancja DefaultPartySpeedCalculatingModel z listy modeli kampanii (K9: GetHerdingModifier to prywatna metoda instancji Default - bez stanu,
        /// niewirtualna; najpierw sam Default, potem pierwszy model po nim dziedziczacy, np. czynny BKROT).</summary>
        private static object HerdModel()
        {
            var c = Campaign.Current;
            if (c == null || c.Models == null) return null;
            object sub = null;
            try
            {
                foreach (var gm in c.Models.GetGameModels())
                {
                    if (gm == null) continue;
                    if (gm.GetType() == typeof(DefaultPartySpeedCalculatingModel)) return gm;
                    if (sub == null && gm is DefaultPartySpeedCalculatingModel) sub = gm;
                }
            }
            catch (Exception e) { Stumble("HerdModel", e); }
            if (sub == null && c.Models.PartySpeedCalculatingModel is DefaultPartySpeedCalculatingModel) sub = c.Models.PartySpeedCalculatingModel;
            return sub;
        }

        private static float HerdMod(object model, int men, int herd)
        {
            if (model == null || _herdM == null) return float.NaN;
            try { return (float)_herdM.Invoke(model, new object[] { men, herd }); }
            catch (Exception e) { Stumble("HerdMod", e); return float.NaN; }
        }

        /// <summary>Rozpiska predkosci i stado tak, jak liczy je gra dla partii gracza (doczepionej do armii - dla wodza, jak dymek gry). Nigdy null.</summary>
        internal static MarchView MarchOf(MobileParty mp)
        {
            var v = new MarchView();
            if (mp == null || Campaign.Current == null) { v.Why = "No campaign is running."; return v; }
            SpeedWire();
            var sw = Stopwatch.StartNew();
            var who = mp;
            var army = mp.Army;
            if (army != null && army.LeaderParty != null && army.LeaderParty != mp && mp.AttachedTo == army.LeaderParty)
            {
                who = army.LeaderParty;
                v.Army = true;
                v.Leader = who.LeaderHero != null ? who.LeaderHero.Name.ToString() : who.Name != null ? who.Name.ToString() : "the army's leader";
            }
            v.Who = who;
            ExplainedNumber en;
            if (!MapRoads.Explained(who, out en)) { v.Why = "The game's speed model gave no number for your party now."; return v; }   // recenzja 133: Why - sama przyczyna, pelne zdanie
            v.Result = en.ResultNumber;
            v.BaseNumber = en.BaseNumber;
            var gl = en.GetLines();
            var ops = OpsOf(en, gl.Count);
            v.OpsKnown = ops != null;
            string herdName = null, shepName = null;
            try { var to = _fHerdText != null ? _fHerdText.GetValue(null) as TextObject : null; herdName = to != null ? to.ToString() : null; } catch { herdName = null; }
            try { shepName = DefaultPerks.Riding.Shepherd != null ? DefaultPerks.Riding.Shepherd.Name.ToString() : null; } catch { shepName = null; }
            bool shepLine = false, herdPlain = false, shepPlain = false;
            for (int i = 0; i < gl.Count; i++)
            {
                var l = new SpeedLine { Name = gl[i].Item1 ?? "", Number = gl[i].Item2, Op = ops != null ? ops[i] : -1 };
                if (l.Op == 2 && Math.Abs(v.BaseNumber) > 1e-4f) l.Pct = l.Number / v.BaseNumber;
                l.Shown = l.Op == 0 || Math.Abs(l.Number) >= 0.005f || (!float.IsNaN(l.Pct) && Math.Abs(l.Pct) >= 0.0005f);
                v.Sum += l.Number;
                v.Lines.Add(l);
                // stado: procent z linii mnoznika gry (nazwa ze statycznego tekstu gry, nie z napisu - K9); jednostki mapy "teraz" plywaja z dodatkami (droga,
                // kolumna); linia scalona jako zwykla (AddFromExplainedNumber innego modelu) albo rodzaj nieznany - bez procentu
                if (herdName != null && l.Name == herdName && l.Op != 0) { v.HerdLine = true; v.HerdUnits += l.Number; if (l.Op != 2) herdPlain = true; }
                else if (shepName != null && l.Name == shepName && l.Op != 0) { shepLine = true; v.ShepUnits += l.Number; if (l.Op != 2) shepPlain = true; }
            }
            v.Other = v.Result - v.Sum;
            if (Math.Abs(v.BaseNumber) > 1e-4f)
            {
                if (v.HerdLine && !herdPlain) v.HerdPct = v.HerdUnits / v.BaseNumber;
                if (shepLine && !shepPlain) v.ShepPct = v.ShepUnits / v.BaseNumber;
            }
            try { if (who.IsActive) v.Game = who.Speed; } catch { v.Game = -1f; }
            try { if (who != mp && mp.IsActive) v.Own = mp.Speed; } catch { v.Own = -1f; }
            // wejscia stada jak AddCargoStats gry: partia predkosci i jej doczepione (K8)
            Cargo(v, who);
            var att = who.AttachedParties;
            if (att != null) for (int i = 0; i < att.Count; i++) if (att[i] != null) Cargo(v, att[i]);
            v.RidingFoot = Math.Min(v.Foot, v.Mounts);
            v.Spare = v.Mounts - v.RidingFoot;
            v.Herd = v.Pack + v.Live + v.Spare;
            v.Excess = Math.Max(0, v.Herd - v.Men);
            var hm = HerdModel();
            if (hm == null) v.HerdWhy = "brak instancji Default w liscie modeli";
            else if (_herdM == null) v.HerdWhy = "brak metody GetHerdingModifier";
            v.HerdGame = HerdMod(hm, v.Men, v.Herd);
            v.HerdNoSpare = HerdMod(hm, v.Men, v.Pack + v.Live);
            if (float.IsNaN(v.HerdGame) && v.HerdWhy == null) v.HerdWhy = "blad wywolania GetHerdingModifier";
            v.Ok = true;
            v.Ms = sw.ElapsedMilliseconds;
            return v;
        }

        private static void Cargo(MarchView v, MobileParty p)
        {
            var r = p.ItemRoster;
            if (r != null) { v.Pack += r.NumberOfPackAnimals; v.Live += r.NumberOfLivestockAnimals; v.Mounts += r.NumberOfMounts; }
            if (p.MemberRoster != null) v.Men += p.MemberRoster.TotalManCount;
            if (p.Party != null) v.Foot += p.Party.NumberOfMenWithoutHorse;
            v.Parties++;
        }

        /// <summary>Zgodnosc stada: procent linii "Herding" wobec GetHerdingModifier gry z tych samych wejsc (K9: |roznica| <= 0.0005 - linia trzyma Round(x, 3));
        /// bez linii - kara gry musi byc 0. null - nieznane (brak instancji / metody albo linia bez procentu).</summary>
        internal static bool? HerdAgrees(MarchView v)
        {
            if (v == null || !v.Ok || float.IsNaN(v.HerdGame)) return null;
            if (!v.HerdLine) return Math.Abs(v.HerdGame) < 1e-6f;
            if (float.IsNaN(v.HerdPct)) return null;
            return Math.Abs(v.HerdPct - v.HerdGame) <= 0.0006f;
        }

        private static bool SpeedAgrees(MarchView v)
        {
            return v == null || !v.Ok || v.Game <= 0f || Math.Abs(v.Result - v.Game) <= 0.02f * v.Game;
        }

        private static string LineText(SpeedLine l)
        {
            if (l.Op == 0) return l.Name + " " + F2(l.Number);
            if (l.Op == 2 && !float.IsNaN(l.Pct)) return l.Name + " " + Pc(l.Pct) + " (" + S2(l.Number) + ")";
            return l.Name + " " + S2(l.Number);
        }

        /// <summary>Strona "Speed".</summary>
        internal static string SpeedText(MarchView v)
        {
            if (v == null || !v.Ok) return v != null && v.Why != null ? v.Why : "The speed cannot be worked out now.";
            var sb = new StringBuilder();
            sb.Append("Speed now: ").Append(F2(v.Result)).Append(" on the map");
            if (v.Game >= 0f) sb.Append(" (the game shows ").Append(F2(v.Game)).Append(')');
            sb.Append(". Worked out where you camp: the ground, the road, the weather and the hour all count, so it changes as you march.");
            if (v.Army)
            {
                sb.Append("\nYour party marches attached to ").Append(v.Leader).Append("'s army - this is the army's speed: its leader's party with every party attached.");
                if (v.Own >= 0f && v.Game > 0f)
                    sb.Append(" Right now your party moves at ").Append(F2(v.Own)).Append(" (x").Append(F2(v.Own / v.Game)).Append(") to keep its place in the column.");
            }
            var shown = new List<SpeedLine>();
            foreach (var l in v.Lines) if (l.Shown) shown.Add(l);
            for (int i = 0; i < shown.Count; i += LinesPerPara)
            {
                sb.Append("\n\n");
                for (int k = i; k < shown.Count && k < i + LinesPerPara; k++)
                {
                    if (k > i) sb.Append('\n');
                    sb.Append(LineText(shown[k]));
                }
            }
            // reszta: czynniki bez opisu (AddFactor bez nazwy) i linie ponizej 0.005
            float rest = v.Result;
            foreach (var l in shown) rest -= l.Number;
            sb.Append(shown.Count > 0 ? "\n" : "\n\n");
            if (Math.Abs(rest) >= 0.005f) sb.Append("Other (no name given) ").Append(S2(rest)).Append('\n');
            sb.Append("= ").Append(F2(v.Result));
            sb.Append("\n\n").Append(v.OpsKnown ? "A line in % is that share of " + F2(v.BaseNumber) + " - the base with the plain lines added - so each line counts once and together they make the speed."
                                                : "Together the lines make the speed.");
            if (Math.Abs(rest) >= 0.005f) sb.Append(" \"Other\" is what a mod changed without giving it a name.");
            return sb.ToString();
        }

        /// <summary>Strona "Herd": regula stada gry i obok nasza regula taboru kolumny (decyzja (c) - tylko pokazac).</summary>
        internal static string HerdText(MarchView v, MarchPace.Column col)
        {
            if (v == null || !v.Ok) return v != null && v.Why != null ? v.Why : "The herd cannot be worked out now.";
            var sb = new StringBuilder();
            sb.Append("The game's herd: pack animals, livestock and spare horses with no footman to ride them. It slows the party only when it outnumbers the men");
            if (v.Parties > 1) sb.Append(" - counted over the whole column: ").Append(v.Army ? "the army's leader's party with every party attached" : "your party with the parties of your army");
            sb.Append('.');
            sb.Append("\n\nHerd: ").Append(Pl(v.Pack, "pack animal", "pack animals")).Append(" + ").Append(v.Live).Append(" livestock + ").Append(Pl(v.Spare, "spare horse", "spare horses"))
              .Append(" with no footman to carry = ").Append(v.Herd).Append(", for ").Append(Pl(v.Men, "man", "men"));
            if (v.Excess <= 0) sb.Append(" - within the limit, no penalty.");
            else
            {
                sb.Append(" - ").Append(v.Excess).Append(" too many");
                if (v.HerdLine)
                {
                    sb.Append(": Herding ");
                    if (!float.IsNaN(v.HerdPct)) sb.Append(Pc(v.HerdPct)).Append(" (").Append(S2(v.HerdUnits)).Append(" on the map now)");
                    else sb.Append(S2(v.HerdUnits)).Append(" on the map now");
                    if (Math.Abs(v.ShepUnits) >= 0.005f)
                        sb.Append(", Shepherd ").Append(!float.IsNaN(v.ShepPct) ? Pc(v.ShepPct) + " (" + S2(v.ShepUnits) + ")" : S2(v.ShepUnits));
                }
                sb.Append('.');
                if (!v.HerdLine) sb.Append(" The game's speed shows no Herding line - a mod counts the herd its own way.");
                sb.Append("\nTo lose the penalty: ").Append(v.Excess).Append(" fewer animals - sell or slaughter livestock, sell pack animals, or put spare horses into the troops' armoury"
                                                                             + " (a horse there is not in the herd).");
                if (v.Spare > 0 && !float.IsNaN(v.HerdNoSpare))
                    sb.Append("\nWithout the ").Append(Pl(v.Spare, "spare horse", "spare horses")).Append(" carrying nobody: ")
                      .Append(Math.Abs(v.HerdNoSpare) < 1e-6f ? "no penalty." : "Herding " + Pc(v.HerdNoSpare) + ".");
            }
            sb.Append("\n\n");
            if (v.Mounts <= 0) sb.Append("No spare horses in the baggage.");
            else if (v.Spare > 0)
                sb.Append("Spare horses carrying nobody: ").Append(v.Spare).Append(" - every footman already rides one, so they count in the herd. A horse in the troops' armoury is not in the herd,"
                                                                                 + " but carries nobody on the march.");
            else sb.Append("Every spare horse in the baggage carries a footman (").Append(Pl(v.RidingFoot, "footman rides", "footmen ride")).Append(") - none counts in the herd.");
            sb.Append("\n\n");
            if (col == null) sb.Append("Our marching column rule is off - only the game's herd counts.");
            else
            {
                int prisRide = Math.Min(col.Prisoners, Math.Max(0, col.Horses - col.RidingFoot));
                if (prisRide > 0) sb.Append("Our marching column seats ").Append(Pl(prisRide, "prisoner", "prisoners")).Append(" on spare horses, but the game still counts those horses in its herd.\n");
                sb.Append("Our baggage-train rule (marching column): ").Append(col.Train).Append(" pack animals and livestock, ").Append(col.Allowance).Append(" go free with ")
                  .Append(Pl(col.Men, "man", "men"));
                if (col.Train > col.Allowance) sb.Append(" - ").Append(col.Train - col.Allowance).Append(" over");
                sb.Append("; it sets the pace only when nobody walks")
                  .Append(col.Kind == 3 ? " - and now it does." : col.Kind == 0 ? " - now men walk, so the walking pace rules." : ".");
            }
            int cart = 0;
            try { cart = CampCart.Pulled(); } catch { cart = 0; }
            if (cart > 0) sb.Append("\nThe ").Append(Pl(cart, "pack animal pulling", "pack animals pulling")).Append(" your supply cart count in both.");
            return sb.ToString();
        }

        /// <summary>Strona "Column": MarchText + regula jezdzca na mapie (decyzja (b) - tylko pokazac). Recenzja 133: hv - wolne konie zbrojowni z podgladu;
        /// zdanie o jezdzcach pieszo mowi o koniu ALBO uprzezy (r.Foot = bez konia + bez uprzezy).</summary>
        internal static string ColumnText(MobileParty mp, MarchPace.Column col, SaddleLaw.PairState p, KitPreview.Result r, HorseView hv)
        {
            var sb = new StringBuilder(MarchText(mp, col, p, hv));
            if (GarrisonKit.OwnKitOn)
            {
                sb.Append("\n\nA man rides on the march by his troop: a man of a mounted troop counts as a rider here even with no horse").Append(SaddleLaw.On ? " or no harness" : "").Append(" for him in the troops' armoury");
                if (r != null && r.Ok && r.Foot > 0) sb.Append(" - now ").Append(Pl(r.Foot, "such rider marches as a horseman", "such riders march as horsemen")).Append(" and would fight on foot (see Horses and harness).");
                else sb.Append(" - in battle he would fight on foot (see Horses and harness).");
            }
            return sb.ToString();
        }

        /// <summary>Wiersz glownego okna "On the march".</summary>
        private static InquiryElement MarchMainRow(MarchPace.Column col, MarchView v)
        {
            try
            {
                if (v == null || !v.Ok) return new InquiryElement("march", MarchRow(col), null, true, "Why your column walks or rides on the map, and what would change it.");
                string what = col == null ? "column rule off" : col.Kind == 0 ? col.N + " afoot, the column walks" : col.Kind == 2 ? col.N + " footmen on spare horses"
                            : col.Kind == 3 ? "the baggage train sets the pace" : "all riders";
                if (v.Excess > 0) what += "; herd " + v.Excess + " too many";
                return new InquiryElement("march", "On the march: speed " + F2(v.Result) + " - " + what, null, true,
                                          "Why your party moves at the speed it does on the map: every line of the game's speed, the herd, and our marching column.");
            }
            catch (Exception e) { Stumble("MarchRow", e); return new InquiryElement("march", MarchRow(col), null, true, "Why your column walks or rides on the map."); }
        }

        /// <summary>Okno "On the march": Speed / Herd / Column; strony wracaja tutaj, "Back" - do przegladu.</summary>
        private static void OpenMarch(MarchView v, MarchPace.Column col, List<string> pSpeed, List<string> pHerd, List<string> pCol)
        {
            bool ok = v != null && v.Ok;
            var rows = new List<InquiryElement>
            {
                new InquiryElement("speed", ok ? "Speed: " + F2(v.Result) + " on the map" + (v.Army ? " (the army's)" : "") : "Speed (not available)", null, ok,
                                   ok ? "Every line of the game's speed for " + (v.Army ? "the army" : "your party") + " where you camp now: the base, men and horses, the herd, the column,"
                                        + " the road, the weather and the hour." : (v != null && v.Why != null ? v.Why : "Not available now.")),
                new InquiryElement("herd", ok ? "Herd: " + v.Herd + " animals for " + v.Men + " men - " + (v.Excess > 0 ? v.Excess + " too many" + (!float.IsNaN(v.HerdPct) ? " (Herding " + Pc(v.HerdPct) + ")" : "")
                                                                                                    : "no penalty")
                                              : "Herd (not available)", null, ok,
                                   "The game's herd rule (pack animals, livestock and spare horses with no footman to ride them), and our baggage-train rule of the marching column."),
                new InquiryElement("column", MarchRow(col), null, true, "Our marching column rule: who walks, who rides, and what would put everyone on horseback."),
            };
            bool photo = WindowPhotos.Active;   // paczka 132: okno proby zdjec - przyciski bez skutku
            Action back = delegate { OpenMarch(v, col, pSpeed, pHerd, pCol); };
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("On the march", "Why your party moves at the speed it does on the map. Pick what to look at.",
                rows, true, 1, 1, "Look", "Back",
                delegate (List<InquiryElement> picked)
                {
                    if (Inert(photo)) return;
                    try
                    {
                        string id = picked != null && picked.Count > 0 ? picked[0].Identifier as string : null;
                        _dReports++;
                        if (id == "speed") ShowPages("Speed", pSpeed, 0, back);
                        else if (id == "herd") ShowPages("Herd", pHerd, 0, back);
                        else if (id == "column") ShowPages("Column", pCol, 0, back);
                        else back();
                    }
                    catch (Exception e) { Stumble("March.Pick", e); }
                },
                delegate (List<InquiryElement> _) { if (Inert(photo)) return; try { OpenMain(false); } catch (Exception e) { Stumble("March.Back", e); } }), true);
        }

        /// <summary>Segment linii raportu: predkosc (133d).</summary>
        private static string SpeedBrief(MarchView v)
        {
            if (v == null) return "-";
            if (!v.Ok) return "niedostepna (" + v.Why + ")";
            int shown = 0; foreach (var l in v.Lines) if (l.Shown) shown++;
            return "kto " + (v.Army ? "armia (wodz " + (v.Who != null ? v.Who.StringId : "?") + ")" : "gracz") + ", wynik " + F2(v.Result) + ", gra " + (v.Game >= 0f ? F2(v.Game) : "-")
                   + " (" + (SpeedAgrees(v) ? "zgodna" : "ROZJAZD " + ((v.Result / v.Game - 1f) * 100f).ToString("+0.0;-0.0", Inv) + "%") + ")"
                   + (v.Army ? ", druzyna gracza " + (v.Own >= 0f ? F2(v.Own) : "-") : "") + ", baza po dodatkach " + F2(v.BaseNumber) + ", linii " + v.Lines.Count + " (pokazanych " + shown
                   + "), Other " + S2(v.Other) + ", rodzaje linii " + (v.OpsKnown ? "TAK" : "BRAK") + ", " + v.Ms + " ms";
        }

        /// <summary>Segment linii raportu: stado (133d).</summary>
        private static string HerdBrief(MarchView v)
        {
            if (v == null || !v.Ok) return "-";
            var ag = HerdAgrees(v);
            return "ludzi " + v.Men + " (partii " + v.Parties + "), piesi " + v.Foot + ", luzaki " + v.Mounts + " (pod piechota " + v.RidingFoot + ", wolne " + v.Spare + "), juczne " + v.Pack
                   + ", bydlo " + v.Live + ", stado " + v.Herd + ", nadmiar " + v.Excess + ", Herding z linii "
                   + (!v.HerdLine ? "brak" : float.IsNaN(v.HerdPct) ? "bez procentu (" + S2(v.HerdUnits) + " jedn.)" : v.HerdPct.ToString("0.000", Inv) + " (" + S2(v.HerdUnits) + " jedn.)")
                   + ", GetHerdingModifier " + (float.IsNaN(v.HerdGame) ? "BRAK (" + (v.HerdWhy ?? "?") + ")" : v.HerdGame.ToString("0.000", Inv))
                   + " - " + (ag == null ? "bez porownania" : ag.Value ? "zgodne" : "ROZJAZD") + ", Shepherd "
                   + (Math.Abs(v.ShepUnits) < 0.005f ? "brak" : float.IsNaN(v.ShepPct) ? S2(v.ShepUnits) + " jedn." : v.ShepPct.ToString("0.000", Inv))
                   + ", bez wolnych luzakow " + (float.IsNaN(v.HerdNoSpare) ? "-" : v.HerdNoSpare.ToString("0.000", Inv));
        }
    }
}
