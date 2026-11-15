using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 562 (Jeff 15.11: "pancerze i miecze same sie uzupelniaja - jesli leza w armoury, zolnierze sami sobie biora ... pelny sklad pancerza ... strzaly
    /// cwiczebne + luk zapasowy / kusza ... i samo sie uzupelnia i schodzi ze stanu Armoury"): PELNY ZESTAW DO MUSZTRY wedlug roli zolnierza.
    ///  - Rola z zestawu bitewnego typu (wiekszosc wzorcow BattleEquipments; bohater - Hero.BattleEquipment): lucznik (luk + strzaly), kusznik (kusza +
    ///    belty), jazda (bron 1H + tarcza + wlocznia), piechota dwureczna (bron 2H), reszta piechoty (bron 1H + tarcza); oszczepnik dodatkowo oszczepy;
    ///    kazdy: zbroja korpusu, helm, nogawice (z butami - jeden slot gry). Rekawice poza zestawem; oponcza (562c) - dodatek: poza zestawem, ale oponcze
    ///    juz lezace w zapasie zostaja (do jednej na czlowieka), sluza musztrze i zuzywaja sie, nowych ludzie nie biora i nie przyjmuja.
    ///  - Gracz: braki zestawu ludzie biora SAMI ze swojej nadwyzki w zbrojowni DTE (plan K1 z podgladem bitwy - KeepGiven: nigdy sprzet, ktory nastepna
    ///    bitwa wydaje); sztuka schodzi ze stanu zbrojowni do zapasu musztry (_stock). Nadmiar (smierc, awans, zmiana roli) wraca do zbrojowni, brak bitwy
    ///    tez (zestaw bitewny ma pierwszenstwo). Codziennie, po zmianie skladu, przy starcie bitwy (sam zwrot) i w miescie przed sprzedaza (sam zwrot).
    ///  - AI: ta sama regula, ale zapasem jest rezerwacja w slowniku zbrojowni DTE (sztuki zostaja w zbrojowni - DTE i tak wydaje cala w bitwie), liczona
    ///    od nowa raz na dobe; sprzedaz nadwyzek AI (MenPurse.SellArmorySurplus) omija zarezerwowane.
    ///  - S = 1 + 0.10 x uB x kB + 0.10 x uZ x kZ, u = pokrycie sztuk polowy (562c: suma min(Cov, Need) / suma Need rodzajow broni / zbroi - ta sama miara,
    ///    co zuzycie i pasek; dawniej ludzie z pelnym kompletem, przez co premia stala na zerze do pierwszego pelnego kompletu).
    ///  - Zuzycie wedlug rodzaju (zycie w dniach musztry = WearDays x LifeX).
    /// </summary>
    internal static partial class Drill
    {
        // ------------------------------------------------------------ rodzaje sztuk zestawu
        // 562 (Jeff 15.11 "pelny sklad"): rodzaje sztuk zestawu musztry
        internal const int K1H = 0, K2H = 1, KPole = 2, KShield = 3, KBow = 4, KArrows = 5, KXbow = 6, KBolts = 7, KThrown = 8,
                           KBody = 9, KHead = 10, KLeg = 11, KCape = 12, NK = 13;
        private const int MMelee = (1 << K1H) | (1 << K2H) | (1 << KPole) | (1 << KShield);
        private const int MRanged = (1 << KBow) | (1 << KArrows) | (1 << KXbow) | (1 << KBolts) | (1 << KThrown);
        private const int MWeap = MMelee | MRanged, MArm = (1 << KBody) | (1 << KHead) | (1 << KLeg);   // 562c (Jeff 15.11 "dodatek"): oponcza poza wymaganym zestawem
        private const int DefaultRole = MArm | (1 << K1H) | (1 << KShield);
        private static readonly ItemObject.ItemTypeEnum[] KindType = { ItemObject.ItemTypeEnum.OneHandedWeapon, ItemObject.ItemTypeEnum.TwoHandedWeapon,
            ItemObject.ItemTypeEnum.Polearm, ItemObject.ItemTypeEnum.Shield, ItemObject.ItemTypeEnum.Bow, ItemObject.ItemTypeEnum.Arrows,
            ItemObject.ItemTypeEnum.Crossbow, ItemObject.ItemTypeEnum.Bolts, ItemObject.ItemTypeEnum.Thrown, ItemObject.ItemTypeEnum.BodyArmor,
            ItemObject.ItemTypeEnum.HeadArmor, ItemObject.ItemTypeEnum.LegArmor, ItemObject.ItemTypeEnum.Cape };
        // zuzycie: zycie rodzaju w dniach musztry = WearDays (200) x LifeX
        private static readonly float[] LifeX = { 1.5f, 1.5f, 1.5f, 1f, 2f, 0.5f, 2.5f, 0.5f, 0.75f, 5f, 5f, 5f, 3f };
        private static readonly int[] TakeOrder = { KPole, K2H, K1H, KShield, KBow, KArrows, KXbow, KBolts, KThrown, KBody, KHead, KLeg, KCape };
        private static readonly string[] KindEn = { "one-handed weapons", "two-handed weapons", "spears", "shields", "bows", "quivers of arrows",
            "crossbows", "cases of bolts", "stacks of javelins", "body armour", "helmets", "leg armour", "capes" };
        private static readonly string[] KindEn1 = { "one-handed weapon", "two-handed weapon", "spear", "shield", "bow", "quiver of arrows",
            "crossbow", "case of bolts", "stack of javelins", "body armour", "helmet", "leg armour", "cape" };
        private static readonly string[] KindPl = { "1H", "2H", "wlocznie", "tarcze", "luki", "strzaly", "kusze", "belty", "oszczepy", "korpus", "helmy", "nogi", "oponcze" };
        // role (do linii i podpowiedzi): miecz i tarcza, dwureczni, jazda, lucznicy, kusznicy, z oszczepami (dodatkowo)
        private const int RSword = 0, RTwo = 1, RCav = 2, RBow = 3, RXbow = 4, RJav = 5, NR = 6;

        private const int F1H = 1 << 8, F2H = 1 << 9;   // sztuka ma uzycie biale jednoreczne / dwureczne (NotUsableWithOneHand)
        private static readonly Dictionary<ItemObject, int> _kindOf = new Dictionary<ItemObject, int>();
        /// <summary>Rodzaj podstawowy (0..12) | flagi uzycia bialego; -1 poza zestawem. Pamiec na przedmiot (Reset).</summary>
        private static int KindInfo(ItemObject it)
        {
            if (it == null) return -1;
            int v; if (_kindOf.TryGetValue(it, out v)) return v;
            int k = Array.IndexOf(KindType, it.ItemType);
            if (k == K1H || k == K2H || k == KPole)
            {
                var ws = it.Weapons;
                if (ws != null) for (int i = 0; i < ws.Count; i++)
                    { var d = ws[i]; if (d == null || !d.IsMeleeWeapon) continue; k |= (d.WeaponFlags & WeaponFlags.NotUsableWithOneHand) != 0 ? F2H : F1H; }
            }
            _kindOf[it] = k; return k;
        }
        private static int KindOf(ItemObject it) { int v = KindInfo(it); return v < 0 ? -1 : v & 0xFF; }

        private static readonly Dictionary<CharacterObject, int> _roleOf = new Dictionary<CharacterObject, int>();
        /// <summary>Maska rodzajow zestawu dla jednego wzorca (sloty broni 0-3, kon w slocie 10).</summary>
        private static int RoleOfEq(Equipment eq)
        {
            if (eq == null) return DefaultRole;
            bool bow = false, xbow = false, thrown = false, shield = false, two = false;
            for (int i = 0; i < 4; i++)
            {
                var it = eq[(EquipmentIndex)i].Item; if (it == null) continue;
                switch (it.ItemType)
                {
                    case ItemObject.ItemTypeEnum.Bow: bow = true; break;
                    case ItemObject.ItemTypeEnum.Crossbow: xbow = true; break;
                    case ItemObject.ItemTypeEnum.Thrown: thrown = true; break;
                    case ItemObject.ItemTypeEnum.Shield: shield = true; break;
                    case ItemObject.ItemTypeEnum.TwoHandedWeapon: two = true; break;
                    case ItemObject.ItemTypeEnum.Polearm: if ((KindInfo(it) & F1H) == 0) two = true; break;
                }
            }
            int m = MArm;
            if (bow) return m | (1 << KBow) | (1 << KArrows);
            if (xbow) return m | (1 << KXbow) | (1 << KBolts);
            if (!eq[EquipmentIndex.Horse].IsEmpty) m |= (1 << K1H) | (1 << KShield) | (1 << KPole);
            else if (two && !shield) m |= 1 << K2H;
            else m |= (1 << K1H) | (1 << KShield);
            if (thrown) m |= 1 << KThrown;
            return m;
        }

        /// <summary>Rola typu: wiekszosc wzorcow bitewnych (remis - pierwszy); pamiec na typ (Reset).</summary>
        private static int RoleOf(CharacterObject ch)
        {
            int m; if (_roleOf.TryGetValue(ch, out m)) return m;
            var cnt = new Dictionary<int, int>(); int best = -1, bn = 0;
            try
            {
                var eqs = ch.BattleEquipments;
                if (eqs != null)
                    foreach (var eq in eqs)
                    { if (eq == null) continue; int r = RoleOfEq(eq), n; cnt.TryGetValue(r, out n); cnt[r] = ++n; if (n > bn) { bn = n; best = r; } }
            }
            catch (Exception e) { Stumble("RoleOf", e); }
            m = best >= 0 ? best : DefaultRole;
            _roleOf[ch] = m; return m;
        }

        private static int RoleClass(int mask)
        {
            if ((mask & (1 << KBow)) != 0) return RBow;
            if ((mask & (1 << KXbow)) != 0) return RXbow;
            if ((mask & (1 << KPole)) != 0) return RCav;
            if ((mask & (1 << K2H)) != 0) return RTwo;
            return RSword;
        }

        /// <summary>Sztuka do zapasu: rodzaj zestawu, bez wyrobow gracza, bez unikatow i sztuk zakazanych w bitwie (ArmsPricing.IsUnique). Szczebel, kultura,
        /// umiejetnosc i modyfikator dowolne (musztra to nie bitwa).</summary>
        private static bool Eligible(ItemObject it)
        {
            if (KindOf(it) < 0) return false;
            try { return !it.IsCraftedByPlayer && !ArmsPricing.IsUnique(it); }
            catch (Exception e) { Stumble("Eligible", e); return false; }
        }

        private static int Units(int men) { return men > 0 ? (men + MenPerPiece - 1) / MenPerPiece : 0; }   // MenPerPiece: 1 sztuka rodzaju na N ludzi

        // ------------------------------------------------------------ miara zestawu
        internal sealed class KitM
        {
            internal readonly List<int> GMask = new List<int>(), GMen = new List<int>();   // grupy rol: maska -> ludzie (zolnierze z rannymi + bohaterowie musztry)
            internal readonly int[] Need = new int[NK], Have = new int[NK], Cov = new int[NK], Used = new int[NK];
            internal readonly int[] RoleMen = new int[NR];
            internal int T1;                     // w Have[K2H]: dwureczne z uzyciem 1H
            internal int Pc1, Pc2, Pcb;          // w Have[KPole]: drzewce tylko 1H, tylko 2H, z oboma (reszta - bez uzycia bialego)
            internal int Other;                  // sztuki spoza rodzajow (rekawice ze starego zapasu) - zawsze nadmiar
            internal int L2N, L21, LP0, LP2, LP1, LPb;   // po przydziale: wolne dwureczne (bez 1H / z 1H) i drzewce wedlug klasy
            internal int Men, Heroes, Ready, ReadyW, ReadyA, ReadyM, NeedM, ReadyR, NeedR, Sig;
            internal int CapeOpt;                // 562c: oponcza-dodatek - ile sztuk zapasu moze sluzyc (1 na MenPerPiece ludzi kazdej grupy); Need[KCape] = 0
            internal int Kit { get { return Men + Heroes; } }
            /// <summary>562c: pokrycie sztuk rodzajow lo..hi (suma min(Cov, Need) / suma Need), 0..1; bez popytu - 0.</summary>
            internal float Cover(int lo, int hi)
            {
                int n = 0, c = 0;
                for (int i = lo; i <= hi; i++) { n += Need[i]; c += Math.Min(Cov[i], Need[i]); }
                return n > 0 ? Math.Min(1f, c / (float)n) : 0f;
            }
            internal int Short(int k) { return Math.Max(0, Need[k] - Cov[k]); }
            internal int Pieces { get { int n = Other; for (int k = 0; k < NK; k++) n += Have[k]; return n; } }
        }

        /// <summary>Grupy rol partii (zolnierze z pamieci typu, bohaterowie musztry z zestawu bitewnego) i popyt. Jedna petla po rosterze, bez planow.</summary>
        private static KitM Groups(MobileParty mp, Settings s, bool noBase)
        {
            var m = new KitM();
            var r = mp != null ? mp.MemberRoster : null;
            if (r == null) return m;
            var by = new Dictionary<int, int>();
            bool heroes = r.TotalHeroes > 0 && HeroesOn(s, noBase);
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || el.Number <= 0) continue;
                int mask, n;
                if (ch.IsHero)
                {
                    if (!heroes || !DrillHero(mp, ch.HeroObject)) continue;
                    mask = RoleOfEq(ch.HeroObject.BattleEquipment); n = 1; m.Heroes++;
                }
                else { mask = RoleOf(ch); n = el.Number; m.Men += n; }
                int v; by.TryGetValue(mask, out v); by[mask] = v + n;
            }
            var keys = new List<int>(by.Keys);
            keys.Sort((a, b) => { int pa = Pop(a), pb = Pop(b); return pa != pb ? pa.CompareTo(pb) : a.CompareTo(b); });   // od najkrotszej maski
            int sig = 17;
            foreach (var mask in keys)
            {
                int men = by[mask];
                m.GMask.Add(mask); m.GMen.Add(men);
                sig = unchecked((sig * 31 + mask) * 31 + men);
                m.RoleMen[RoleClass(mask)] += men;
                if ((mask & (1 << KThrown)) != 0) m.RoleMen[RJav] += men;
                int u = Units(men);
                for (int k = 0; k < NK; k++) if ((mask & (1 << k)) != 0) m.Need[k] += u;
                m.CapeOpt += u;
            }
            m.Sig = unchecked(sig * 31 + MenPerPiece);
            return m;
        }

        private static void AddHave(KitM m, ItemObject it, int n)
        {
            if (n <= 0) return;
            int v = KindInfo(it);
            if (v < 0) { m.Other += n; return; }
            int b = v & 0xFF;
            m.Have[b] += n;
            if (b == K2H && (v & F1H) != 0) m.T1 += n;
            else if (b == KPole)
            {
                bool f1 = (v & F1H) != 0, f2 = (v & F2H) != 0;
                if (f1 && f2) m.Pcb += n; else if (f1) m.Pc1 += n; else if (f2) m.Pc2 += n;
            }
        }

        private static int Grab(ref int need, ref int pool) { int x = Math.Max(0, Math.Min(need, pool)); need -= x; pool -= x; return x; }

        /// <summary>Pokrycie rodzajow (Cov) i zuzycie sztuk typow (Used) z zastepstwami w stalej kolejnosci: drzewce do wloczni (najpierw bez uzycia bialego,
        /// potem zbedne dla 1H / 2H, potem reszta),
        /// bron 1H, dwureczne bez 1H do 2H; brak 1H - dwureczne z 1H, potem drzewce z 1H; brak 2H - reszta dwurecznych, potem drzewce z 2H; reszta 1:1.</summary>
        private static void Allocate(KitM m)
        {
            Array.Clear(m.Cov, 0, NK); Array.Clear(m.Used, 0, NK);
            for (int k = 0; k < NK; k++)
                if (k != K1H && k != K2H && k != KPole) { m.Cov[k] = Math.Min(m.Need[k], m.Have[k]); m.Used[k] = m.Cov[k]; }
            // 562c: oponcza-dodatek - bez popytu (Cov 0, poza premia i paskiem), ale sztuki w zapasie do CapeOpt sa w uzyciu: nie wracaja jako nadmiar
            // (ReturnExcess liczy od Used) i zuzywaja sie w musztrze (Wear); ponad CapeOpt (smierc, awans) - nadmiar jak kazdy rodzaj
            m.Used[KCape] = Math.Min(m.Have[KCape], m.CapeOpt);
            int p0 = Math.Max(0, m.Have[KPole] - m.Pc1 - m.Pc2 - m.Pcb), p2 = m.Pc2, p1 = m.Pc1, pb = m.Pcb;
            int need = m.Need[KPole];
            // recenzja 562 (4): wlocznia najpierw z drzewc bez uzycia bialego, potem z tych, ktorych 1H / 2H nie potrzebuja (reszta po wlasnym typie
            // i dwurecznych), dopiero na koncu z reszty
            int r1 = Math.Max(0, m.Need[K1H] - m.Have[K1H]);
            int t1Left = Math.Max(0, m.T1 - r1);
            r1 = Math.Max(0, r1 - m.T1);
            int r2 = Math.Max(0, m.Need[K2H] - Math.Max(0, m.Have[K2H] - m.T1) - t1Left);
            int s2 = Math.Max(0, p2 - r2), s1 = Math.Max(0, p1 - r1), sb = Math.Max(0, pb - Math.Max(0, r1 - p1) - Math.Max(0, r2 - p2));
            int c = Grab(ref need, ref p0);
            int g = Grab(ref need, ref s2); p2 -= g; c += g;
            g = Grab(ref need, ref s1); p1 -= g; c += g;
            g = Grab(ref need, ref sb); pb -= g; c += g;
            c += Grab(ref need, ref p2) + Grab(ref need, ref p1) + Grab(ref need, ref pb);
            m.Cov[KPole] = c; m.Used[KPole] = c;
            int one = m.Need[K1H], h1 = m.Have[K1H];
            c = Grab(ref one, ref h1); m.Cov[K1H] = c; m.Used[K1H] = c;
            int t1 = m.T1, tn = Math.Max(0, m.Have[K2H] - m.T1), two = m.Need[K2H];
            c = Grab(ref two, ref tn); m.Cov[K2H] = c; m.Used[K2H] = c;
            c = Grab(ref one, ref t1); m.Cov[K1H] += c; m.Used[K2H] += c;
            c = Grab(ref one, ref p1) + Grab(ref one, ref pb); m.Cov[K1H] += c; m.Used[KPole] += c;
            c = Grab(ref two, ref t1); m.Cov[K2H] += c; m.Used[K2H] += c;
            c = Grab(ref two, ref p2) + Grab(ref two, ref pb); m.Cov[K2H] += c; m.Used[KPole] += c;
            m.L2N = tn; m.L21 = t1; m.LP0 = p0; m.LP2 = p2; m.LP1 = p1; m.LPb = pb;
        }

        /// <summary>Ludzie z kompletem czesci sub swojej roli (tylko grupy, ktore te czesc maja). Najpierw po rowno (nigdy ponad pokrycie), potem reszta
        /// pokrycia grupom od najkrotszej maski.</summary>
        private static int ReadyOf(KitM m, int sub, out int needMen)
        {
            needMen = 0;
            int n = m.GMask.Count, men = 0;
            if (n == 0) return 0;
            var c = new int[n]; var u = new int[n];
            for (int g = 0; g < n; g++)
            {
                int mk = m.GMask[g] & sub;
                if (mk == 0) { u[g] = -1; continue; }
                u[g] = Units(m.GMen[g]); needMen += m.GMen[g];
                double f = 1.0;
                for (int k = 0; k < NK; k++) if ((mk & (1 << k)) != 0) f = Math.Min(f, m.Need[k] > 0 ? m.Cov[k] / (double)m.Need[k] : 1.0);
                c[g] = (int)Math.Floor(u[g] * f + 1e-9);
            }
            var L = new int[NK];
            Array.Copy(m.Cov, L, NK);
            for (int g = 0; g < n; g++)
            {
                if (u[g] < 0) continue;
                int mk = m.GMask[g] & sub;
                for (int k = 0; k < NK; k++) if ((mk & (1 << k)) != 0) L[k] -= c[g];
            }
            for (int g = 0; g < n; g++)
            {
                if (u[g] < 0) continue;
                int mk = m.GMask[g] & sub, extra = u[g] - c[g];
                for (int k = 0; k < NK && extra > 0; k++) if ((mk & (1 << k)) != 0) extra = Math.Min(extra, L[k]);
                if (extra <= 0) continue;
                c[g] += extra;
                for (int k = 0; k < NK; k++) if ((mk & (1 << k)) != 0) L[k] -= extra;
            }
            for (int g = 0; g < n; g++) if (u[g] >= 0) men += Math.Min(m.GMen[g], c[g] * MenPerPiece);
            return men;
        }

        private static void FillReady(KitM m)
        {
            int x;
            m.Ready = ReadyOf(m, MWeap | MArm, out x);
            m.ReadyW = ReadyOf(m, MWeap, out x);
            m.ReadyA = ReadyOf(m, MArm, out x);
            m.ReadyM = ReadyOf(m, MMelee, out m.NeedM);
            m.ReadyR = ReadyOf(m, MRanged, out m.NeedR);
        }

        /// <summary>Gracz: grupy rol + zapas musztry (_stock) -> Need, Have, Cov, Used, Ready.</summary>
        private static KitM MeasurePlayer()
        {
            var m = Groups(MobileParty.MainParty, Settings.Current, true);
            for (int i = 0; i < _stock.Count; i++)
            {
                var el = _stock.GetElementCopyAtIndex(i);
                if (el.Amount > 0) AddHave(m, el.EquipmentElement.Item, el.Amount);
            }
            Allocate(m); FillReady(m);
            return m;
        }

        /// <summary>Podpis rol druzyny gracza (maski i ludzie) - zmiana skladu, ktora zmienia zestaw (rekrut, smierc, awans, bohater). Bez planow.</summary>
        private static int PlayerSig()
        {
            try { return Groups(MobileParty.MainParty, Settings.Current, true).Sig; }
            catch (Exception e) { Stumble("PlayerSig", e); return _settleSig; }
        }

        private static int Pieces(ItemRoster r)
        {
            int n = 0;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++) { int a = r.GetElementCopyAtIndex(i).Amount; if (a > 0) n += a; }
            return n;
        }

        /// <summary>Zapas od gracza wedlug polowy: bron (rodzaje 0-8), zbroje (9-12 i stare sztuki spoza rodzajow).</summary>
        private static void GivenCounts(out int w, out int a)
        {
            w = 0; a = 0;
            for (int i = 0; i < _stock.Count; i++)
            {
                var el = _stock.GetElementCopyAtIndex(i);
                if (el.Amount <= 0) continue;
                int k = KindOf(el.EquipmentElement.Item);
                if (k >= 0 && k <= KThrown) w += el.Amount; else a += el.Amount;
            }
        }

        // ------------------------------------------------------------ gracz: rozliczenie zapasu ze zbrojownia
        internal const int WhyDaily = 0, WhyRoster = 1, WhyBattle = 2, WhyTown = 3;
        private static readonly string[] WhyPl = { "doba", "sklad", "bitwa", "miasto" };
        private static int _settleSig, _settleArmVer = int.MinValue;   // recenzja 562 (3): wersja zbrojowni DTE po ostatnim rozliczeniu
        private static bool _hudDirty = true, _migrPending;
        private static int _lastTook = -1;   // 562b: ile sztuk wzial ostatni pobor (doba / sklad); -1 - jeszcze nie bylo w tej sesji
        private static int _pTaken, _pBack, _pBackOther, _pBackBattle, _pBackOff, _pLost, _settleN, _settlePv, _settleBad, _pvNone, _pWornN;
        private static long _settleTicks;
        private static readonly int[] _settleBy = new int[4];
        private static readonly int[] _pWorn = new int[NK], _aWorn = new int[NK];
        private static int _aKitParties, _aKitPieces, _aiKept, _aiNoKitSale, _importLegacy;

        /// <summary>Ruch sztuk miedzy zbrojownia DTE (czesc ludzi) a zapasem musztry: a) nadmiar ponad przydzial + SurplusKeepPercent wraca do zbrojowni,
        /// b) sztuki zapasu, ktorych brakuje w podgladzie nastepnej bitwy, wracaja do zbrojowni (zestaw bitewny ma pierwszenstwo), c) (doba, sklad) braki
        /// zestawu ludzie biora z nadwyzki K1 (Sell > 0 - plan z podgladem, KeepGiven). Bez ruchu: zapas wylaczony, misja, otwarta zbrojownia, brak DTE.</summary>
        internal static void SettlePlayer(int why)
        {
            var main = MobileParty.MainParty;
            if (!StockOn) { ReleaseIfOff(); return; }   // recenzja 562 (2): zapas wylaczony - caly do zbrojowni ludzi (rozliczenie z Need = 0)
            if (main == null || QuartermasterEscrow.Active || TaleWorlds.MountAndBlade.Mission.Current != null) return;
            var arm = QuartermasterLaw.DteArmory();
            if (arm == null) return;
            int kept = MenPurse.KeptGiven;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                int before = Pieces(arm) + Pieces(_stock);
                int sw0 = 0, sa0 = 0, oth0 = 0;
                bool migr = _migrPending;
                if (migr) { GivenCounts(out sw0, out sa0); oth0 = OtherPieces(); }
                var m = MeasurePlayer();
                int back = ReturnExcess(m, arm);
                var pv = KitPreview.ForArmoury(); _settlePv++;
                if (pv == null) _pvNone++;
                int bb = ReturnForBattle(pv, arm);
                if (bb > 0) { pv = KitPreview.ForArmoury(); _settlePv++; }
                int took = 0;
                if (why == WhyDaily || why == WhyRoster) { took = TakePlayer(MeasurePlayer(), arm, pv); _lastTook = took; _hudDirty = true; }   // 562b: wynik ostatniego poboru do podpowiedzi paska
                _settleSig = m.Sig;
                _settleArmVer = arm.VersionNo;
                _settleN++; _settleBy[Math.Max(0, Math.Min(3, why))]++;
                int after = Pieces(arm) + Pieces(_stock);
                if (after != before) _settleBad++;
                if (back + bb + took > 0) { _hudDirty = true; try { MarchPairs.InvalidateWeight(main); } catch { } }
                if (migr)
                {
                    _migrPending = false;
                    int sw1, sa1; GivenCounts(out sw1, out sa1);
                    int oth1 = OtherPieces();
                    Log.Info("Musztra 562: migracja zapasu - bylo " + (sw0 + sa0) + " szt. (bron " + sw0 + ", zbroje " + sa0 + "), zostalo " + (sw1 + sa1) + ", do zbrojowni " + (back + bb)
                             + " (rekawice i inne spoza zestawu " + Math.Max(0, oth0 - oth1) + ", ponad komplet " + Math.Max(0, back - (oth0 - oth1)) + ", pod braki bitwy " + bb + "), wzieto z nadwyzki " + took
                             + ", powod " + WhyPl[Math.Max(0, Math.Min(3, why))] + ", kontrola sztuk zbrojownia + zapas " + (after == before ? "ZGODNE" : "NIEZGODNE (" + before + " -> " + after + ")") + ".");
                }
            }
            catch (Exception e) { Stumble("SettlePlayer", e); }
            finally { MenPurse.KeptGiven = kept; _settleTicks += Stopwatch.GetTimestamp() - t0; }
        }

        private static int OtherPieces()
        {
            int n = 0;
            for (int i = 0; i < _stock.Count; i++) { var el = _stock.GetElementCopyAtIndex(i); if (el.Amount > 0 && KindOf(el.EquipmentElement.Item) < 0) n += el.Amount; }
            return n;
        }

        /// <summary>Caly zapas do zbrojowni DTE (ta sama para AddToCounts co ReturnExcess). Zwraca liczbe sztuk.</summary>
        private static int MoveAll(ItemRoster arm)
        {
            var els = new List<ItemRosterElement>();
            for (int i = 0; i < _stock.Count; i++) { var el = _stock.GetElementCopyAtIndex(i); if (el.Amount > 0 && el.EquipmentElement.Item != null) els.Add(el); }
            int n = 0;
            foreach (var el in els)
            {
                _stock.AddToCounts(el.EquipmentElement, -el.Amount);
                arm.AddToCounts(el.EquipmentElement, el.Amount);
                n += el.Amount;
            }
            return n;
        }

        /// <summary>Recenzja 562 (2): zapas wylaczony (DrillStock, DrillLaw albo DonationXpOff) - nic go nie rozlicza, wiec caly wraca do zbrojowni ludzi, jak
        /// rozliczenie z Need = 0 (a waga - StockKg - przy wylaczonym zapasie 0). Bez DTE, w misji i przy otwartej zbrojowni - czeka do nastepnego wolania
        /// (Drill.Daily, wjazd do miasta). Nie rzuca.</summary>
        internal static void ReleaseIfOff()
        {
            try
            {
                if (StockOn || _stock.Count == 0) return;
                var main = MobileParty.MainParty;
                if (main == null || QuartermasterEscrow.Active || TaleWorlds.MountAndBlade.Mission.Current != null) return;
                var arm = QuartermasterLaw.DteArmory();
                if (arm == null) return;
                int before = Pieces(arm) + Pieces(_stock);
                int n = MoveAll(arm);
                if (n <= 0) return;
                _pBackOff += n; _hudDirty = true;
                try { MarchPairs.InvalidateWeight(main); } catch { }
                int after = Pieces(arm) + Pieces(_stock);
                if (after != before) _settleBad++;
                Log.Info("Musztra 562: zapas wylaczony - caly zapas (" + n + " szt.) wrocil do zbrojowni ludzi, kontrola sztuk " + (after == before ? "ZGODNE" : "NIEZGODNE (" + before + " -> " + after + ")") + ".");
            }
            catch (Exception e) { Stumble("ReleaseIfOff", e); }
        }

        /// <summary>Recenzja 562 (1): HeroPrisonerTaken gracza (sluchacz zarejestrowany PO CaptiveArmory - biegnie przed nim). Zapas ma los zbrojowni, niezaleznie
        /// od kolejnosci sluchacza DTE: C4 (PlayerArmoryLikeAiLord) - caly zapas do zbrojowni, CaptiveArmory oddaje go zdobywcy razem z nia; bez C4 - regula DTE
        /// ApplyCapturedArmoryLoss wprost na zapasie (4/5 kazdej pozycji przepada, ulamek losowo), bez przenoszenia (DTE nie liczy go drugi raz). Bez DTE - nic
        /// (zbrojownia tez nic nie traci). Nie rzuca.</summary>
        internal static void OnPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            try
            {
                if (prisoner == null || prisoner != Hero.MainHero || _stock.Count == 0) return;
                var arm = QuartermasterLaw.DteArmory();
                if (arm == null) return;
                int all = Pieces(_stock);
                if (CaptiveArmory.On)
                {
                    int n = MoveAll(arm);
                    _pBackOff += n;
                    Log.Info("Musztra 562: pojmanie gracza - zapas musztry (" + n + " szt.) do zbrojowni przed oddaniem jej zdobywcy (C4).");
                }
                else
                {
                    var els = new List<ItemRosterElement>();
                    for (int i = 0; i < _stock.Count; i++) { var el = _stock.GetElementCopyAtIndex(i); if (el.Amount > 0) els.Add(el); }
                    int lost = 0;
                    foreach (var el in els)
                    {
                        long four = (long)el.Amount * 4L;
                        int rem = (int)(four % 5);
                        int x = Math.Min(el.Amount, (int)(four / 5) + (rem > 0 && MBRandom.RandomInt(5) < rem ? 1 : 0));
                        if (x <= 0) continue;
                        _stock.AddToCounts(el.EquipmentElement, -x);
                        lost += x;
                    }
                    _pLost += lost;
                    Log.Info("Musztra 562: pojmanie gracza - zapas musztry traci " + lost + " z " + all + " szt. (regula DTE: 4/5 jak zbrojownia), zostaje " + Pieces(_stock) + ".");
                }
                _hudDirty = true;
                try { MarchPairs.InvalidateWeight(MobileParty.MainParty); } catch { }
            }
            catch (Exception e) { Stumble("OnPrisonerTaken", e); }
        }

        private static int Cls2H(int v) { return (v & F1H) != 0 ? 1 : 0; }                                            // 0 bez 1H, 1 z 1H
        private static int ClsPole(int v) { bool f1 = (v & F1H) != 0, f2 = (v & F2H) != 0; return f1 && f2 ? 3 : (f1 ? 2 : (f2 ? 1 : 0)); }   // 0 bez, 1 tylko 2H, 2 tylko 1H, 3 oba

        /// <summary>a) Nadmiar: dla rodzaju t Have - Used - ceil(Used x SurplusKeepPercent / 100) do zbrojowni, najlepsze najpierw, tylko z wolnych klas przydzialu
        /// (pokrycie sie nie zmienia); sztuki spoza rodzajow (stare rekawice) zawsze. Do czesci ludzi - K1 je potem sprzeda jak kazda nadwyzke.</summary>
        private static int ReturnExcess(KitM m, ItemRoster arm)
        {
            var s = Settings.Current;
            float pct = s != null ? Math.Max(0f, s.SurplusKeepPercent) : 0f;
            var ex = new int[NK];
            int any = m.Other;
            for (int t = 0; t < NK; t++)
            {
                int keep = (int)Math.Ceiling(m.Used[t] * pct / 100f - 1e-6);
                ex[t] = Math.Max(0, m.Have[t] - m.Used[t] - keep);
                any += ex[t];
            }
            if (any <= 0) return 0;
            int[] p2 = { m.L2N, m.L21 }, pp = { m.LP0, m.LP2, m.LP1, m.LPb };
            var els = new List<ItemRosterElement>();
            for (int i = 0; i < _stock.Count; i++) { var el = _stock.GetElementCopyAtIndex(i); if (el.Amount > 0) els.Add(el); }
            els.Sort((x, y) => y.EquipmentElement.ItemValue.CompareTo(x.EquipmentElement.ItemValue));   // najlepsze najpierw
            int n = 0;
            foreach (var el in els)
            {
                var it = el.EquipmentElement.Item;
                int v = KindInfo(it), give;
                if (v < 0) { give = el.Amount; _pBackOther += give; }
                else
                {
                    int b = v & 0xFF;
                    if (ex[b] <= 0) continue;
                    give = Math.Min(el.Amount, ex[b]);
                    if (b == K2H) { int c = Cls2H(v); give = Math.Min(give, p2[c]); p2[c] -= give; }
                    else if (b == KPole) { int c = ClsPole(v); give = Math.Min(give, pp[c]); pp[c] -= give; }
                    ex[b] -= give;
                    _pBack += give;
                }
                if (give <= 0) continue;
                _stock.AddToCounts(el.EquipmentElement, -give);
                arm.AddToCounts(el.EquipmentElement, give);
                n += give;
            }
            return n;
        }

        /// <summary>b) Braki podgladu nastepnej bitwy (bez broni bocznej - 535): sztuki zapasu tej samej grupy broni, ktorych uzyje ktos z brakujacych typow
        /// (GearFit.ForTroop - predykat MeetsFitOf), wracaja do zbrojowni, najlepsze najpierw. Sztuka, ktorej podglad by nie wydal, zostaje (bez przerzucania).</summary>
        private static int ReturnForBattle(KitPreview.Result pv, ItemRoster arm)
        {
            if (pv == null || pv.Wants.Count == 0 || _stock.Count == 0) return 0;
            int n = 0;
            foreach (var w in pv.Wants.Values)
            {
                if (w == null || w.Side || w.N <= 0 || w.Type == ItemObject.ItemTypeEnum.Horse || w.Type == ItemObject.ItemTypeEnum.HorseHarness) continue;
                try
                {
                    var cand = new List<ItemRosterElement>();
                    for (int i = 0; i < _stock.Count; i++)
                    {
                        var el = _stock.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (el.Amount <= 0 || it == null || it.ItemType != w.Type || KitGaps.Group(it) != w.Group || !FitsAny(w.By.Keys, it)) continue;
                        cand.Add(el);
                    }
                    if (cand.Count == 0) continue;
                    cand.Sort((x, y) => y.EquipmentElement.ItemValue.CompareTo(x.EquipmentElement.ItemValue));
                    int left = w.N;
                    foreach (var el in cand)
                    {
                        if (left <= 0) break;
                        int give = Math.Min(left, el.Amount);
                        _stock.AddToCounts(el.EquipmentElement, -give);
                        arm.AddToCounts(el.EquipmentElement, give);
                        left -= give; n += give;
                    }
                }
                catch (Exception e) { Stumble("ReturnForBattle", e); }
            }
            _pBackBattle += n;
            return n;
        }

        private static bool FitsAny(IEnumerable<CharacterObject> troops, ItemObject it)
        {
            bool kit = GearFit.On;
            foreach (var t in troops) if (t != null && GearFit.ForTroop(t, it, kit)) return true;
            return false;
        }

        private static readonly ItemObject.ItemTypeEnum[] Take2H = { ItemObject.ItemTypeEnum.TwoHandedWeapon, ItemObject.ItemTypeEnum.Polearm };
        /// <summary>Typy, z ktorych rodzaj bierze sztuki (bez zastepstw - zestaw kanoniczny; jedyny wyjatek: 2H takze z drzewc z uzyciem dwurecznym).</summary>
        private static ItemObject.ItemTypeEnum[] TakeTypes(int k) { return k == K2H ? Take2H : new[] { KindType[k] }; }

        /// <summary>c) Braki zestawu (rodzaje w TakeOrder) z nadwyzki K1 ludzi (Sell > 0, lista WorseFirst - do musztry najgorsze), plan tylko typu z brakiem.
        /// Sztuka schodzi ze zbrojowni do zapasu.</summary>
        private static int TakePlayer(KitM m, ItemRoster arm, KitPreview.Result pv)
        {
            var plans = new Dictionary<int, List<SwapMath.Piece>>();
            int took = 0;
            foreach (int k in TakeOrder)
            {
                int want = m.Short(k);
                if (want <= 0) continue;
                foreach (var t in TakeTypes(k))
                {
                    if (want <= 0) break;
                    List<SwapMath.Piece> pl;
                    if (!plans.TryGetValue((int)t, out pl)) plans[(int)t] = pl = MenPurse.PlayerSurplusPlan(arm, t, pv);
                    if (pl == null) continue;
                    foreach (var p in pl)
                    {
                        if (p.Sell <= 0) continue;
                        var el = QuartermasterLaw.ElOf(p);
                        var it = el.Item;
                        if (it == null || !Eligible(it) || (k == K2H && t == ItemObject.ItemTypeEnum.Polearm && (KindInfo(it) & F2H) == 0)) continue;
                        int idx = arm.FindIndexOfElement(el);
                        int inArm = idx >= 0 ? arm.GetElementCopyAtIndex(idx).Amount : 0;
                        int x = Math.Min(Math.Min(p.Sell, want), inArm);
                        if (x <= 0) continue;
                        arm.AddToCounts(el, -x);
                        _stock.AddToCounts(el, x);
                        p.Sell -= x; want -= x; took += x;
                        if (want <= 0) break;
                    }
                }
            }
            _pTaken += took;
            return took;
        }

        /// <summary>Start bitwy z graczem: gdy sklad (rekruci tej godziny) albo zbrojownia zmienily sie od ostatniego rozliczenia - sam zwrot (nadmiar, braki bitwy).</summary>
        internal static void OnMapEventStarted(MapEvent me)
        {
            try
            {
                var main = MobileParty.MainParty;
                if (me == null || main == null || !StockOn || _stock.Count == 0) return;
                bool mine = main.MapEvent == me;
                if (!mine && me.InvolvedParties != null) foreach (var p in me.InvolvedParties) if (p == main.Party) { mine = true; break; }
                if (!mine) return;
                var arm = QuartermasterLaw.DteArmory();
                // recenzja 562 (3): takze zmiana zbrojowni bez zmiany skladu (sztuki wyjete albo sprzedane, zlom DTE) - podglad bitwy moze miec nowe braki
                if (PlayerSig() != _settleSig || (arm != null && arm.VersionNo != _settleArmVer)) SettlePlayer(WhyBattle);
            }
            catch (Exception e) { Stumble("OnMapEventStarted", e); }
        }

        // ------------------------------------------------------------ AI: ta sama regula, rezerwacja w zbrojowni
        private static readonly Dictionary<MobileParty, Dictionary<ItemObject, int>> _aiKit = new Dictionary<MobileParty, Dictionary<ItemObject, int>>();

        /// <summary>Zestaw AI (zastepuje CountAiStock; stoper PCtxSur jak dotad): role, popyt, rezerwacja z nadwyzki K1 (Sell > 0, najgorsze), plany tylko typow
        /// z popytem; ten sam przydzial i Ready co u gracza. Rezerwacja tylko przy czynnym zapasie AI (pomiar przy wylaczonym - bez rezerwacji).</summary>
        private static void AiKit(Ctx c, MobileParty mp, Settings s)
        {
            var m = Groups(mp, s, c.NoBase);
            var arm = ArmoryOf(mp);
            var res = new Dictionary<ItemObject, int>();
            if (arm != null && arm.Count > 0 && m.Kit > 0)
            {
                var plans = new Dictionary<int, List<SwapMath.Piece>>();
                int meleeLeft = -1;   // -1 = jeszcze nie liczone; int.MaxValue = bez grupy
                foreach (int k in TakeOrder)
                {
                    int want = m.Need[k];
                    if (want <= 0) continue;
                    foreach (var t in TakeTypes(k))
                    {
                        if (want <= 0) break;
                        bool melee = AiGear.Melee((int)t);
                        if (melee && meleeLeft < 0) meleeLeft = MenPurse.AiMeleeLeft(arm, mp.MemberRoster);
                        List<SwapMath.Piece> pl;
                        if (!plans.TryGetValue((int)t, out pl)) plans[(int)t] = pl = MenPurse.AiSurplusPlan(arm, mp.MemberRoster, t);
                        if (pl == null) continue;
                        foreach (var p in pl)
                        {
                            var it = QuartermasterLaw.ElOf(p).Item;
                            if (it == null || p.Sell <= 0 || !Eligible(it) || (k == K2H && t == ItemObject.ItemTypeEnum.Polearm && (KindInfo(it) & F2H) == 0)) continue;
                            int have; res.TryGetValue(it, out have);
                            int inArm; arm.TryGetValue(it, out inArm);
                            int n = Math.Min(Math.Min(p.Sell, inArm) - have, want);
                            if (melee) n = Math.Min(n, meleeLeft);
                            if (n <= 0) continue;
                            res[it] = have + n; want -= n;
                            AddHave(m, it, n);
                            if (melee && meleeLeft != int.MaxValue) meleeLeft -= n;
                            if (want <= 0) break;
                        }
                    }
                }
            }
            Allocate(m); FillReady(m);
            if (c.StockOn)
            {
                _aiKit[mp] = res;
                if (res.Count > 0) { _aKitParties++; foreach (var v in res.Values) _aKitPieces += v; }
            }
            else _aiKit.Remove(mp);
            c.M = m;
        }

        /// <summary>Kopia rezerwacji musztry partii AI (MenPurse.SellArmorySurplus - pierwsze, najgorsze sztuki nadwyzki zostaja ludziom). null - brak
        /// (zapas AI wylaczony albo partia bez dziennego ticku od wczytania - licznik w linii AI).</summary>
        internal static Dictionary<ItemObject, int> AiKeepCopy(MobileParty mp)
        {
            try
            {
                if (mp == null) return null;
                Dictionary<ItemObject, int> r;
                if (_aiKit.TryGetValue(mp, out r)) return r.Count > 0 ? new Dictionary<ItemObject, int>(r) : null;
                var s = Settings.Current;
                if (s != null && s.DrillStockAi && mp.IsLordParty && !Undead.Party(mp)) _aiNoKitSale++;   // Inni nie cwicza - bez rezerwacji z zalozenia
            }
            catch (Exception e) { Stumble("AiKeepCopy", e); }
            return null;
        }

        /// <summary>Sztuki nadwyzki, ktorych kupiec nie dostal, bo leza w zestawie musztry (tylko licznik linii AI).</summary>
        internal static void NoteAiKept(int n) { if (n > 0) _aiKept += n; }

        // ------------------------------------------------------------ zuzycie wedlug rodzaju
        private static void Wear(Ctx c, Settings s)
        {
            var m = c.M;
            if (!c.StockOn || c.Kit <= 0 || m == null || c.Off) return;   // dzien kary (glod, sen) - nikt nie cwiczy, nic sie nie zuzywa
            var mp = c.Party;
            bool law = c.NoBase || s.DrillLawAi;
            float dw = law ? c.D : 1f;                     // AI bez Z14b cwiczy plasko jak w grze; Z14b - zuzycie x D
            if (dw <= 0f) return;
            var a = AccOf(mp, true);
            if (a == null) return;
            Dictionary<ItemObject, int> res = null, arm = null;
            if (!c.Main) { _aiKit.TryGetValue(mp, out res); arm = ArmoryOf(mp); }
            bool changed = false;
            var gf = GoodsLedger.Begin(GoodsLedger.FDrill, mp);
            try
            {
                for (int k = 0; k < NK; k++)
                {
                    int inUse = k == KCape ? m.Used[KCape] : Math.Min(m.Cov[k], m.Need[k]);   // 562c: oponcze-dodatek w uzyciu tez sie zuzywaja
                    if (inUse <= 0) continue;
                    a.WK[k] += inUse * dw / (WearDays * LifeX[k]);
                    int n = (int)a.WK[k];
                    if (n <= 0) continue;
                    a.WK[k] -= n;
                    for (int i = 0; i < n; i++)
                    {
                        if (!(c.Main ? WearPlayerOne(k, a) : WearAiOne(mp, k, a, arm, res))) break;
                        changed = true;
                        if (c.Main) { _pWorn[k]++; _pWornN++; } else _aWorn[k]++;
                    }
                }
            }
            catch (Exception e) { Stumble("Wear", e); }
            finally { GoodsLedger.End(gf); }
            if (changed && c.Main) { _hudDirty = true; try { MarchPairs.InvalidateWeight(mp); } catch { } }
        }

        /// <summary>Czy sztuka (KindInfo v) sluzy rodzajowi k: sub = false - rodzaj podstawowy; sub = true - zastepstwo (1H: dwureczne i drzewce z 1H; 2H: drzewce z 2H).</summary>
        private static bool Serves(int v, int k, bool sub)
        {
            if (v < 0) return false;
            int b = v & 0xFF;
            if (!sub) return b == k;
            if (k == K1H) return (b == K2H || b == KPole) && (v & F1H) != 0;
            if (k == K2H) return b == KPole && (v & F2H) != 0;
            return false;
        }

        /// <summary>Gracz: najtansza sztuka zapasu sluzaca rodzajowi (najpierw typ podstawowy, potem zastepstwa).</summary>
        private static bool WearPlayerOne(int k, Acc a)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                int best = -1, bv = int.MaxValue;
                for (int i = 0; i < _stock.Count; i++)
                {
                    var el = _stock.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0 || !Serves(KindInfo(el.EquipmentElement.Item), k, pass == 1)) continue;
                    int v = el.EquipmentElement.ItemValue;
                    if (v < bv) { bv = v; best = i; }
                }
                if (best < 0) continue;
                var pick = _stock.GetElementCopyAtIndex(best).EquipmentElement;
                _stock.AddToCounts(pick, -1);
                Scrap(pick.Item, a, true);
                return true;
            }
            return false;
        }

        /// <summary>AI: najtansza zarezerwowana sztuka sluzaca rodzajowi; stan sztuki przed zmiana zbrojowni (AiWear.TakeCondition), potem zbrojownia - 1
        /// i rezerwacja - 1 (bez liczenia nadwyzki od nowa).</summary>
        private static bool WearAiOne(MobileParty mp, int k, Acc a, Dictionary<ItemObject, int> arm, Dictionary<ItemObject, int> res)
        {
            if (arm == null || res == null || res.Count == 0) return false;
            for (int pass = 0; pass < 2; pass++)
            {
                ItemObject pick = null; int pv = int.MaxValue;
                foreach (var kv in res)
                {
                    var it = kv.Key; int cnt;
                    if (it == null || kv.Value <= 0 || !arm.TryGetValue(it, out cnt) || cnt <= 0 || !Serves(KindInfo(it), k, pass == 1)) continue;
                    if (it.Value < pv) { pv = it.Value; pick = it; }
                }
                if (pick == null) continue;
                try { AiWear.TakeCondition(mp, pick); } catch (Exception e) { Stumble("TakeCondition", e); }
                int left = arm[pick] - 1;
                if (left > 0) arm[pick] = left; else arm.Remove(pick);
                int r = res[pick] - 1;
                if (r > 0) res[pick] = r; else res.Remove(pick);
                Scrap(pick, a, false);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------ waga zapasu musztry (ArmouryHasWeight)
        private static volatile float _kgV;
        private static volatile bool _kgOk, _kgSea;
        private static int _kgVer = int.MinValue;

        /// <summary>Waga zapasu musztry gracza (suma GetItemEffectiveWeight x ilosc, jak StashWeight.Measure). live - watek glowny (liczy po zmianie wersji
        /// rostera albo flagi morza); inaczej ostatni wynik z ta sama flaga morza.</summary>
        internal static float StockKg(MobileParty mp, bool atSea, bool live)
        {
            try
            {
                if (_stock.Count == 0 || !StockOn) return 0f;   // recenzja 562 (2): wylaczony zapas nie wazy (jak przed 562; ReleaseIfOff oddaje go zbrojowni)
                if (!live) return _kgOk && _kgSea == atSea ? _kgV : 0f;
                int ver = _stock.VersionNo;
                if (_kgOk && ver == _kgVer && _kgSea == atSea) return _kgV;
                var cp = Campaign.Current;
                var model = cp != null && cp.Models != null ? cp.Models.InventoryCapacityModel : null;
                if (model == null) return 0f;
                float kg = 0f;
                for (int i = 0; i < _stock.Count; i++)
                {
                    var el = _stock.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0) continue;
                    TextObject d;
                    float w = model.GetItemEffectiveWeight(el.EquipmentElement, mp, atSea, out d);
                    if (w > 0f) kg += w * el.Amount;
                }
                _kgOk = false; _kgV = kg; _kgVer = ver; _kgSea = atSea; _kgOk = true;
                return kg;
            }
            catch (Exception e) { Stumble("StockKg", e); return 0f; }
        }

        // ------------------------------------------------------------ przyjecie do zapasu (ekrany 1-3)
        /// <summary>Rodzaje, ktorym sztuka moze sluzyc, w kolejnosci zastepstw (drzewce: wlocznia, 2H, 1H; dwureczna: 2H, 1H; reszta 1:1).</summary>
        private static List<int> KindsFor(ItemObject it)
        {
            var l = new List<int>(3);
            int v = KindInfo(it);
            if (v < 0) return l;
            int b = v & 0xFF;
            l.Add(b);
            if (b == KPole) { if ((v & F2H) != 0) l.Add(K2H); if ((v & F1H) != 0) l.Add(K1H); }
            else if (b == K2H && (v & F1H) != 0) l.Add(K1H);
            return l;
        }

        private static string ShortList(int[] sh, int max)
        {
            var idx = new List<int>();
            for (int k = 0; k < NK; k++) if (sh[k] > 0) idx.Add(k);
            idx.Sort((x, y) => sh[y].CompareTo(sh[x]));
            var sb = new StringBuilder();
            int rest = 0;
            for (int i = 0; i < idx.Count; i++)
            {
                int k = idx[i];
                if (i >= max) { rest += sh[k]; continue; }
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(sh[k]).Append(' ').Append(sh[k] == 1 ? KindEn1[k] : KindEn[k]);
            }
            if (rest > 0) sb.Append(", ").Append(rest).Append(" other pieces");
            return sb.ToString();
        }

        private static int[] Shorts(KitM m) { var sh = new int[NK]; for (int k = 0; k < NK; k++) sh[k] = m.Short(k); return sh; }
        private static int Sum(int[] a) { int n = 0; foreach (var v in a) n += v; return n; }

        /// <summary>Przyjecie do zapasu gracza: bron i zbroje z listy, najtansze najpierw, do brakow zestawu (rodzaje z zastepstwami); przyjete schodza z listy.
        /// 316a: z Discard nadmiar wraca do taboru (gdy zapas cos przyjal); lup zostaje na polu; trofea Spoils - reszta zostaje na polu; autotest - reszte zwraca wolajacy.</summary>
        internal static int Accept(ItemRoster from, int src)
        {
            Clk(PScreen);
            try
            {
                var main = MobileParty.MainParty;
                if (!StockOn || from == null || from.Count == 0 || main == null) return 0;   // bez musztry albo bez Z1 zapas nic nie przyjmuje (rzeczy jak dotad)
                var cand = new List<ItemRosterElement>();
                for (int i = 0; i < from.Count; i++)
                {
                    var el = from.GetElementCopyAtIndex(i);
                    if (el.Amount > 0 && Eligible(el.EquipmentElement.Item)) cand.Add(el);
                }
                if (cand.Count == 0) return 0;
                var m = MeasurePlayer();
                int men = m.Kit;
                var sh = Shorts(m);
                cand.Sort((x, y) => x.EquipmentElement.ItemValue.CompareTo(y.EquipmentElement.ItemValue));
                int taken = 0, noRoom = 0, back = 0;
                var left = new List<KeyValuePair<EquipmentElement, int>>();
                foreach (var el in cand)
                {
                    int t = 0;
                    foreach (int k in KindsFor(el.EquipmentElement.Item))
                    {
                        int x = Math.Min(el.Amount - t, sh[k]);
                        if (x <= 0) continue;
                        sh[k] -= x; t += x;
                    }
                    if (t > 0) { from.AddToCounts(el.EquipmentElement, -t); _stock.AddToCounts(el.EquipmentElement, t); taken += t; }
                    if (el.Amount - t > 0) left.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, el.Amount - t));
                }
                // 316a: nadmiar z Discard wraca do taboru, ale tylko gdy zapas cos przyjal; zapas pelny (nic nie przyjal) - Discard wyrzuca jak w grze
                bool toBag = src == SrcDiscard && taken > 0 && men > 0 && main.ItemRoster != null && !ReferenceEquals(from, main.ItemRoster);
                foreach (var kv in left)
                {
                    if (toBag) { from.AddToCounts(kv.Key, -kv.Value); main.ItemRoster.AddToCounts(kv.Key, kv.Value); back += kv.Value; }   // przeniesienie: z listy do taboru
                    else noRoom += kv.Value;
                }
                if (src >= 0 && src < _in.Length) _in[src] += taken;
                _noRoom += noRoom;
                _backBag += back;
                if (taken > 0) { _hudDirty = true; try { MarchPairs.InvalidateWeight(main); } catch { } }
                string rest = src == SrcTrophies ? "stay on the field" : (src == SrcAutotest ? "go back to the baggage" : src == SrcLoot ? "stay with the loot you leave behind" : "are thrown away");
                var m2 = MeasurePlayer();
                var sh2 = Shorts(m2);
                int still = Sum(sh2);
                if (men > 0)   // m110-sam: bez zolnierzy (sam / sami bohaterowie bez musztry) bez napisu - zapas nic nie przyjmuje, rzeczy jak w grze
                    Log.Player("Your men took " + taken + " pieces into their drill kit" + (back > 0 ? "; " + back + " pieces they do not need went back to your baggage" : "") + ". "
                               + m2.Ready + " of " + m2.Kit + " now have a full drill kit for their role"
                               + (still > 0 ? "; still short: " + ShortList(sh2, 4) + " - they take those from their spare kit in the troops' armoury, or from you." : ".")
                               + (noRoom > 0 ? " " + noRoom + " pieces " + rest + " - their kit needs none of them." : ""),
                               noRoom > 0 && taken == 0);
                Log.Info("Musztra: zapas - przyjeto " + taken + " szt. (" + SrcName[Math.Max(0, Math.Min(SrcName.Length - 1, src))] + "), bez miejsca " + noRoom + ", wrocone do taborow " + back
                         + "; z kompletem " + m2.Ready + "/" + m2.Kit + " (bron " + m2.ReadyW + ", zbroje " + m2.ReadyA + "), braki " + still + " [" + KindLog(m2) + "], zapas " + m2.Pieces + " szt.");
                return taken;
            }
            catch (Exception e) { Stumble("Accept", e); return 0; }
            finally { Unclk(); }
        }

        /// <summary>Spoils "Leave" (SpoilsSeal): trofea zostawione na polu - bron i zbroje do zapasu.</summary>
        internal static void AcceptTrophies(ItemRoster r) { Accept(r, SrcTrophies); }

        /// <summary>Podpowiedz Spoils "Leave" przy czynnym zapasie (LeaveOn): ile sztuk brakuje zestawom; reszta zostaje na polu bez XP (Z1).</summary>
        internal static string TrophyTip()
        {
            int room = 0, kit = 0;
            try { var m = MeasurePlayer(); kit = m.Kit; room = Sum(Shorts(m)); }
            catch (Exception e) { Stumble("TrophyTip", e); }
            if (PartyWords.NoTroops && kit <= 0) return "Remaining {COUNT} items will be left on the field.";   // m110-sam: bez zolnierzy zapas nic nie przyjmuje - jak LeftBehindTip
            return "Remaining {COUNT} items: your men keep up to " + room + " pieces their drill kits lack, the rest stay on the field.";
        }

        /// <summary>Need/Cov wedlug rodzajow (tylko rodzaje z popytem albo zapasem) - do linii logu.</summary>
        private static string KindLog(KitM m)
        {
            var sb = new StringBuilder();
            for (int k = 0; k < NK; k++)
            {
                if (m.Need[k] == 0 && m.Have[k] == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                if (k == KCape && m.Need[k] == 0)   // 562c: dodatek - w uzyciu / w zapasie
                { sb.Append(KindPl[k]).Append(" (dodatek) ").Append(m.Used[k]).Append('/').Append(m.Have[k]); continue; }
                sb.Append(KindPl[k]).Append(' ').Append(m.Cov[k]).Append('/').Append(m.Need[k]);
                if (m.Have[k] > m.Used[k]) sb.Append(" (+").Append(m.Have[k] - m.Used[k]).Append(')');
            }
            if (m.Other > 0) sb.Append(sb.Length > 0 ? ", " : "").Append("spoza zestawu ").Append(m.Other);
            return sb.ToString();
        }

        /// <summary>Pasek mapy (linia logu MarchHud): pokryte/potrzeba wedlug rodzajow z popytem.</summary>
        internal static string KitLog(Kit k)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < NK; i++) { if (k.Need[i] == 0) continue; if (sb.Length > 0) sb.Append(", "); sb.Append(KindPl[i]).Append(' ').Append(k.Cov[i]).Append('/').Append(k.Need[i]); }
            if (k.CapeUse > 0) sb.Append(sb.Length > 0 ? ", " : "").Append(KindPl[KCape]).Append(" (dodatek) ").Append(k.CapeUse);   // 562c
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        private static string RoleLog(KitM m)
        {
            return "miecz i tarcza " + m.RoleMen[RSword] + ", dwureczni " + m.RoleMen[RTwo] + ", jazda " + m.RoleMen[RCav] + ", lucznicy " + m.RoleMen[RBow]
                   + ", kusznicy " + m.RoleMen[RXbow] + ", z oszczepami " + m.RoleMen[RJav];
        }

        private static string IntLog(int[] a)
        {
            var sb = new StringBuilder();
            for (int k = 0; k < NK; k++) { if (a[k] == 0) continue; if (sb.Length > 0) sb.Append(", "); sb.Append(KindPl[k]).Append(' ').Append(a[k]); }
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        // ------------------------------------------------------------ pasek mapy (MarchHud - pozycja Drill)
        /// <summary>Stan zestawu musztry gracza dla paska mapy (z pamieci: tylko _stock i sklad, bez planow K1 i bez podgladu bitwy).</summary>
        internal sealed class Kit
        {
            internal bool Ok;                                  // false - zapas wylaczony albo blad liczenia
            internal int Men, Heroes, Ready, ReadyW, ReadyA, ReadyM, NeedM, ReadyR, NeedR, Stock, Other;
            internal int KitN { get { return Men + Heroes; } }
            internal readonly int[] Need = new int[NK], Cov = new int[NK], WornToday = new int[NK], RoleMen = new int[NR];
            internal readonly float[] Acc = new float[NK];     // ulamek zuzycia do nastepnej sztuki rodzaju (0..1)
            internal int Taken, Back, BackBattle, LastTook;
            internal int CapeUse;                              // 562c: oponcze-dodatek z zapasu w uzyciu (KitM.Used[KCape])
            internal int CovSum(int lo, int hi) { int n = 0; for (int i = lo; i <= hi; i++) n += Math.Min(Cov[i], Need[i]); return n; }   // 562b: pokryte sztuki rodzajow lo..hi
            internal int NeedSum(int lo, int hi) { int n = 0; for (int i = lo; i <= hi; i++) n += Need[i]; return n; }
            /// <summary>562b: pokrycie sztuk calego zestawu w procentach (postep widoczny przed pierwszym pelnym zestawem); 100 tylko przy zerowych brakach.</summary>
            internal float Cover(int lo, int hi) { int n = NeedSum(lo, hi); return n > 0 ? Math.Min(1f, CovSum(lo, hi) / (float)n) : 0f; }   // 562c: jak KitM.Cover
            internal int Pct { get { int n = NeedSum(0, NK - 1), c = CovSum(0, NK - 1); return n <= 0 ? 100 : (c >= n ? 100 : Math.Min(99, (int)(c * 100L / n))); } }
            internal bool PerkW, PerkA;
            internal int Short(int k) { return Math.Max(0, Need[k] - Cov[k]); }
            internal float Bonus(bool weapons)                 // premia S tej polowy (0.10 przy pelnej, x1.5 z perkiem); 562c: u = pokrycie sztuk polowy (jak Drill.CtxOf)
            {
                float u = Cover(weapons ? K1H : KBody, weapons ? KThrown : KLeg);
                return StockBonus * u * ((weapons ? PerkW : PerkA) ? PerkMult : 1f);
            }
            internal float WearPerFullDay(int k)               // sztuk rodzaju na pelny dzien cwiczen (8 h, CampDrill.DayFull)
            { return (k == KCape ? CapeUse : Math.Min(Cov[k], Need[k])) * CampDrill.DayFull / (WearDays * LifeX[k]); }   // 562c: oponcze-dodatek jak Wear
            internal string Shorts(int lo, int hi, int max)
            {
                var sh = new int[NK];
                for (int k = lo; k <= hi; k++) sh[k] = Short(k);
                return ShortList(sh, max);
            }
        }
        internal static float BonusPerSet { get { return StockBonus; } }
        internal static float BonusPerkMult { get { return PerkMult; } }
        internal static int OnePer { get { return MenPerPiece; } }
        internal static string KindName(int k) { return KindEn[k]; }
        internal static float LifeDays(int k) { return WearDays * LifeX[k]; }

        private static readonly Kit _hudKit = new Kit();
        private static int _hudStockVer = int.MinValue, _hudRosVer = int.MinValue, _hudHeroes = -1, _hudCfg = -1, _hudDay = int.MinValue, _hudAt = Environment.TickCount - 60000, _hudN;
        private static bool _hudHave;

        /// <summary>Odczyt dla paska mapy (watek glowny, co 0.2 s): miara zestawu liczona od nowa tylko po zmianie klucza (zapas, sklad, bohaterowie, dzien,
        /// rozliczenie) i najwyzej raz na 1 s - zero planow K1 i zero podgladu bitwy. Nie rzuca.</summary>
        internal static Kit HudKit()
        {
            var k = _hudKit;
            try
            {
                var main = MobileParty.MainParty;
                if (!StockOn || main == null) { k.Ok = false; return k; }
                var ros = main.MemberRoster;
                int sv = _stock.VersionNo, rv = ros != null ? ros.VersionNo : -1, hn = ros != null ? ros.TotalHeroes : 0, day = (int)CampaignTime.Now.ToDays;
                int now = Environment.TickCount;
                int cfg = MenPerPiece * 2 + (HeroesOn(Settings.Current, true) ? 1 : 0);   // recenzja 562 (6): MCM Drill Men Per Piece / Drill Heroes
                bool changed = _hudDirty || !_hudHave || sv != _hudStockVer || rv != _hudRosVer || hn != _hudHeroes || day != _hudDay || cfg != _hudCfg;
                if (changed && (!_hudHave || unchecked(now - _hudAt) >= 1000))
                {
                    _hudAt = now; _hudN++; _hudDirty = false;
                    var m = MeasurePlayer();
                    k.Men = m.Men; k.Heroes = m.Heroes; k.Ready = m.Ready; k.ReadyW = m.ReadyW; k.ReadyA = m.ReadyA; k.ReadyM = m.ReadyM; k.NeedM = m.NeedM;
                    k.ReadyR = m.ReadyR; k.NeedR = m.NeedR; k.Stock = m.Pieces; k.Other = m.Other; k.CapeUse = m.Used[KCape];
                    Array.Copy(m.Need, k.Need, NK); Array.Copy(m.Cov, k.Cov, NK); Array.Copy(m.RoleMen, k.RoleMen, NR);
                    _hudStockVer = sv; _hudRosVer = rv; _hudHeroes = hn; _hudDay = day; _hudCfg = cfg; _hudHave = true;
                }
                Array.Copy(_pWorn, k.WornToday, NK);
                var acc = AccOf(main, false);
                if (acc != null) Array.Copy(acc.WK, k.Acc, NK); else Array.Clear(k.Acc, 0, NK);
                k.LastTook = _lastTook;
                k.Taken = _pTaken; k.Back = _pBack + _pBackOther + _pBackOff; k.BackBattle = _pBackBattle;
                k.PerkW = main.HasPerk(DefaultPerks.Steward.GivingHands);
                k.PerkA = main.HasPerk(DefaultPerks.Steward.PaidInPromise, true);
                k.Ok = _hudHave;
            }
            catch (Exception e) { k.Ok = false; Stumble("HudKit", e); }
            return k;
        }

        /// <summary>Liczba przeliczen miary dla paska (do linii doby MarchHud.State).</summary>
        internal static int HudKitCounts { get { return _hudN; } }

        /// <summary>Nowa kampania - pamiec paska od zera.</summary>
        private static void HudKitReset()
        {
            _hudStockVer = _hudRosVer = _hudDay = int.MinValue; _hudHeroes = _hudCfg = -1; _hudAt = Environment.TickCount - 60000; _hudN = 0; _hudHave = false; _hudDirty = true; _hudKit.Ok = false; _lastTook = -1;
        }

        // ------------------------------------------------------------ autotest: zasilenie zapasu (krytyka 7)
        /// <summary>Tylko w autotescie (CrashScribe.Autotest.Active), co dobe od doby 2, dopoki zestawy nie sa pelne (flaga w zapisie): braki rodzajow z taboru
        /// gracza, reszta kupiona najtaniej z polki miasta za zloto gracza (handel), gdy druzyna stoi w miescie; ta sama funkcja przyjecia co ekrany; czego zapas
        /// nie przyjmie - wraca do taboru. Bez ludzi nie ma czego zasilac - proba nastepnej doby.</summary>
        private static void AutotestFeed()
        {
            var main = MobileParty.MainParty;
            if (!StockOn || main == null || _fed || !AutotestActive()) return;
            _feedTries++;
            var m = MeasurePlayer();
            int men = m.Kit;
            var want = Shorts(m);
            int total = Sum(want);
            if (men > 0 && total == 0) { _fed = true; Log.Info("Musztra (autotest): zestawy pelne (z kompletem " + m.Ready + "/" + men + ") - zasilanie zakonczone."); return; }
            var st = main.CurrentSettlement;
            bool town = st != null && st.IsTown && st.Town != null && st.ItemRoster != null;
            if (men <= 0)
            {
                if (_feedTries == 1 || _feedTries % 10 == 0)
                    Log.Info("Musztra (autotest): zasilenie zapasu - proba " + _feedTries + ": druzyna bez zolnierzy, nic do zasilenia; ponowie w nastepnej dobie.");
                return;
            }
            var tmp = new ItemRoster();
            int fromBag = 0, bought = 0, gold = 0;
            var bag = main.ItemRoster;
            var bagEls = new List<ItemRosterElement>();
            for (int i = 0; i < bag.Count; i++) { var el = bag.GetElementCopyAtIndex(i); if (el.Amount > 0 && Eligible(el.EquipmentElement.Item)) bagEls.Add(el); }
            foreach (var el in bagEls)
            {
                int t = 0;
                foreach (int k in KindsFor(el.EquipmentElement.Item)) { int x = Math.Min(el.Amount - t, want[k]); if (x > 0) { want[k] -= x; t += x; } }
                if (t <= 0) continue;
                bag.AddToCounts(el.EquipmentElement, -t); tmp.AddToCounts(el.EquipmentElement, t); fromBag += t;
            }
            if (town)
            {
                foreach (int k in TakeOrder)
                {
                    while (want[k] > 0)
                    {
                        int best = -1, bp = int.MaxValue;
                        var shelf = st.ItemRoster;
                        for (int i = 0; i < shelf.Count; i++)
                        {
                            var el = shelf.GetElementCopyAtIndex(i);
                            if (el.Amount <= 0 || KindOf(el.EquipmentElement.Item) != k || !Eligible(el.EquipmentElement.Item)) continue;
                            int p = st.Town.MarketData.GetPrice(el.EquipmentElement, main, false, st.Party);
                            if (p > 0 && p < bp) { bp = p; best = i; }
                        }
                        if (best < 0 || Hero.MainHero.Gold < bp) break;
                        var pick = shelf.GetElementCopyAtIndex(best).EquipmentElement;
                        shelf.AddToCounts(pick, -1);
                        TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, st, bp, true);
                        tmp.AddToCounts(pick, 1); want[k]--; bought++; gold += bp;
                    }
                }
            }
            int taken = fromBag + bought > 0 ? Accept(tmp, SrcAutotest) : 0, back = 0;
            for (int i = 0; i < tmp.Count; i++) { var el = tmp.GetElementCopyAtIndex(i); if (el.Amount > 0) { bag.AddToCounts(el.EquipmentElement, el.Amount); back += el.Amount; } }
            var m2 = MeasurePlayer();
            int still = Sum(Shorts(m2));
            if (still == 0) _fed = true;
            if (fromBag + bought > 0 || _feedTries == 1 || _feedTries % 10 == 0 || _fed)
                Log.Info("Musztra (autotest): zasilenie zapasu - proba " + _feedTries + ", ludzi " + men + ": z taboru " + fromBag + " szt., kupione " + bought
                         + " szt. za " + gold + " d " + (town ? "w " + st.Name : "(poza miastem - bez zakupu)") + ", zloto gracza " + Hero.MainHero.Gold + "; przyjeto " + taken
                         + ", wraca do taboru " + back + "; z kompletem " + m2.Ready + "/" + m2.Kit + ", braki " + still + (_fed ? " - PELNE, koniec zasilania." : " - ponowie w nastepnej dobie."));
        }
    }
}
