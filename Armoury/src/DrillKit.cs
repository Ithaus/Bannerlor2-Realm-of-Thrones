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
    ///  - 571 (klucz DrillTakesPlayerStash): czego nadwyzka ludzi nie da, ludzie biora z NIENOSZONYCH sztuk gracza w zbrojowni (lista gracza), najgorsze najpierw,
    ///    nigdy tego, co wyda nastepna bitwa; zwrot takiej sztuki wraca na liste gracza (opis przy sekcji 571; 575 - pobor w Candidates/Move).
    ///  - AI: ta sama regula, ale zapasem jest rezerwacja w slowniku zbrojowni DTE (sztuki zostaja w zbrojowni - DTE i tak wydaje cala w bitwie), liczona
    ///    od nowa raz na dobe; sprzedaz nadwyzek AI (MenPurse.SellArmorySurplus) omija zarezerwowane.
    ///  - S = 1 + 0.10 x uB x kB + 0.10 x uZ x kZ, u = pokrycie sztuk polowy (562c: suma min(Cov, Need) / suma Need rodzajow broni / zbroi - ta sama miara,
    ///    co zuzycie i pasek; dawniej ludzie z pelnym kompletem, przez co premia stala na zerze do pierwszego pelnego kompletu).
    ///  - Zuzycie wedlug rodzaju (zycie w dniach musztry = WearDays x LifeX).
    ///  - 574 (Jeff 15.11 "jesli ma lucznictwo 30 a luk lezy 40 ... nie moze uzyc"; klucz DrillKitSkillCheck): do zestawu trafia tylko sztuka, ktora udzwignie
    ///    typ z brakiem jej rodzaju (GearFit.ForTroop - umiejetnosc, siodlo, kultura, jak kwatermistrz bitwy; bohater ItemReq.MeetsHero). Pokrycie per typ:
    ///    typ z n ludzmi pokrywaja najwyzej Units(n) sztuk, ktore sam udzwignie (FitMatch); sztuki, ktorych nikt w roli nie udzwignie, wracaja do zbrojowni.
    ///  - 575 (Jeff 15.11 "najtanszy ... jesli zabral luk tier 6 ... a pojawily sie luki tansze w armoury to oddaje luk tier 6 i zabiera najtanszy"; klucz
    ///    DrillKitCheapestSwap): pobor po CENIE (EquipmentElement.ItemValue rosnaco) z obu pul razem (nadwyzka ludzi i lista gracza; przy rownej cenie ludzi
    ///    najpierw) i z obu typow 2H razem; codziennie wymiana na tansze (SwapCheaper - tansza uzywalna sztuka tej samej klasy z puli, drozsza wraca do zbrojowni);
    ///    sztuka zapasu, ktora dalaby zolnierzowi w nastepnej bitwie lepsza bron/zbroje niz wydana, wraca przed bitwa (ReturnForUpgrade, z proba podgladu).
    ///    AI: rezerwacja po cenie (liczona od nowa co dobe, wiec wymiana dzieje sie sama). Zuzycie zostaje "najtansza pierwsza" (w zapasie i tak leza najtansze).
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

        /// <summary>Sztuka do zapasu: rodzaj zestawu, bez wyrobow gracza, bez unikatow i sztuk zakazanych w bitwie (ArmsPricing.IsUnique). Szczebel i modyfikator
        /// dowolne; umiejetnosc, siodlo i kultura - 574 (klucz DrillKitSkillCheck): osobno, per typ (Fits / Claim / UsableAny).</summary>
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
            // 574 (Jeff 15.11 "aby respektowane byly umiejetnosci"): typy oddzialow (i bohaterowie musztry) z rola i ludzmi; przy Skill sztuka liczy sie
            // do zestawu tylko, gdy typ, ktory jej potrzebuje, ja udzwignie (GearFit.ForTroop - ten sam predykat co kwatermistrz bitwy; bohater - ItemReq.MeetsHero)
            internal readonly List<KitT> Types = new List<KitT>();
            internal bool Skill;                 // 574: sprawdzanie umiejetnosci czynne przy tej mierze (klucz DrillKitSkillCheck)
            internal int Unfit;                  // 574: sztuki zapasu, ktorych nikt w potrzebujacej roli nie udzwignie - poza Have, zawsze nadmiar (ReturnExcess)
            internal readonly int[] Fit = new int[NK];   // 574: sztuki rodzaju dopasowane do typow (typ z n ludzmi - najwyzej Units(n) sztuk, ktore sam udzwignie)
            internal int[][] Resid;              // 574: [rodzaj][typ] wolne miejsca typu po dopasowaniu (Claim je zajmuje)
            internal Dictionary<ItemObject, int> FitItems;   // 574: dopasowane sztuki wedlug przedmiotu (kazdy rodzaj, wspolna pula - recenzja 574) - ReturnExcess oddaje najpierw reszte
            internal int Kit { get { return Men + Heroes; } }
            /// <summary>562c: pokrycie sztuk rodzajow lo..hi (suma min(Cov, Need) / suma Need), 0..1; bez popytu - 0.</summary>
            internal float Cover(int lo, int hi)
            {
                int n = 0, c = 0;
                for (int i = lo; i <= hi; i++) { n += Need[i]; c += Math.Min(Cov[i], Need[i]); }
                return n > 0 ? Math.Min(1f, c / (float)n) : 0f;
            }
            internal int Short(int k) { return Math.Max(0, Need[k] - Cov[k]); }
            internal int Pieces { get { int n = Other + Unfit; for (int k = 0; k < NK; k++) n += Have[k]; return n; } }
        }

        /// <summary>574: typ oddzialu w mierze zestawu (bohater musztry - osobny typ z 1 czlowiekiem).</summary>
        internal sealed class KitT { internal CharacterObject Ch; internal int Mask, Men, Cap; internal bool Hero; }

        private static bool SkillOn { get { var s = Settings.Current; return s != null && s.DrillKitSkillCheck; } }

        // 574: pamiec predykatu (typ, przedmiot) na dobe - umiejetnosci szeregowych sa stale, bohaterow rosna wolno; czyszczona tez przy zmianie bramek GearFit
        private static readonly Dictionary<CharacterObject, Dictionary<ItemObject, bool>> _fitMemo = new Dictionary<CharacterObject, Dictionary<ItemObject, bool>>();
        private static int _fitMemoDay = int.MinValue, _fitMemoCfg = -1, _fitMemoN;

        /// <summary>574: czy typ udzwignie sztuke: szeregowi GearFit.ForTroop (umiejetnosc, siodlo przy GearFitKits, kultura - jak kwatermistrz bitwy i FitsAny),
        /// bohaterowie ItemReq.MeetsHero (sito bohaterow). Bez licznikow GearFit (Quiet). Wyjatek - true (jak ItemReq), slad w potknieciach.</summary>
        private static bool Fits(KitT t, ItemObject it)
        {
            if (t == null || t.Ch == null || it == null) return true;
            var st = Settings.Current;
            int day = (int)CampaignTime.Now.ToDays, cfg = (GearFit.On ? 1 : 0) + (GearFit.CultureOn ? 2 : 0)
                + (GearFit.KinItemOn ? 4 : 0) + (GiantGear.On ? 8 : 0) + (st == null || st.HeroBardingNeedsRiding ? 16 : 0);   // recenzja 574 (3): wszystkie bramki predykatu
            if (day != _fitMemoDay || cfg != _fitMemoCfg || _fitMemoN > 200000) { _fitMemo.Clear(); _fitMemoN = 0; _fitMemoDay = day; _fitMemoCfg = cfg; }
            Dictionary<ItemObject, bool> d;
            if (!_fitMemo.TryGetValue(t.Ch, out d)) _fitMemo[t.Ch] = d = new Dictionary<ItemObject, bool>();
            bool v; if (d.TryGetValue(it, out v)) return v;
            bool q = GearFit.Quiet; GearFit.Quiet = true;
            try { v = t.Hero ? ItemReq.MeetsHero(t.Ch, it) : GearFit.ForTroop(t.Ch, it, GearFit.On); }
            catch (Exception e) { Stumble("Fits", e); v = true; }
            finally { GearFit.Quiet = q; }
            d[it] = v; _fitMemoN++;
            return v;
        }

        /// <summary>574: maska rodzajow, ktorym sztuka moze sluzyc (z zastepstwami, jak KindsFor).</summary>
        private static int KindsMask(ItemObject it) { int mk = 0; foreach (int k in KindsFor(it)) mk |= 1 << k; return mk; }

        /// <summary>574: czy ktos w roli potrzebujacej ktoregos z rodzajow sztuki ja udzwignie (oponcza-dodatek: ktokolwiek). Bez Skill albo bez ludzi - true.</summary>
        private static bool UsableAny(KitM m, ItemObject it)
        {
            if (m == null || !m.Skill || m.Types.Count == 0 || KindInfo(it) < 0) return true;
            int mk = KindsMask(it);
            bool cape = (mk & (1 << KCape)) != 0;
            foreach (var t in m.Types) if ((cape || (t.Mask & mk) != 0) && Fits(t, it)) return true;
            return false;
        }

        /// <summary>574: czy ktos w roli z rodzajem k udzwignie sztuke (bez patrzenia na wolne miejsca; oponcza-dodatek - ktokolwiek) - zuzycie i licznik
        /// "za trudne". Bez Skill albo bez ludzi - true.</summary>
        private static bool FitsKind(KitM m, int k, ItemObject it)
        {
            if (m == null || !m.Skill || m.Types.Count == 0) return true;
            bool cape = k == KCape;
            foreach (var t in m.Types) if ((cape || (t.Mask & (1 << k)) != 0) && Fits(t, it)) return true;
            return false;
        }

        /// <summary>574: zajmij do max miejsc rodzaju k u typow, ktore sztuke udzwigna (najpierw typ z najnizsza umiejetnoscia sztuki - mocniejsi zostaja
        /// dla trudniejszych sztuk). Bez Skill - max. Zwraca zajete.</summary>
        private static int Claim(KitM m, int k, ItemObject it, int max)
        {
            if (max <= 0) return 0;
            if (m == null || !m.Skill || m.Resid == null) return max;
            var r = m.Resid[k];
            var sk = ItemReq.SkillFor(it);
            int got = 0;
            while (got < max)
            {
                int best = -1, bs = int.MaxValue;
                for (int t = 0; t < r.Length; t++)
                {
                    if (r[t] <= 0 || !Fits(m.Types[t], it)) continue;
                    int sv = 0;
                    if (sk != null) { try { sv = m.Types[t].Ch.GetSkillValue(sk); } catch (Exception e) { Stumble("Claim", e); sv = 0; } }
                    if (sv < bs) { bs = sv; best = t; }
                }
                if (best < 0) break;
                int x = Math.Min(max - got, r[best]);
                r[best] -= x; got += x;
            }
            return got;
        }

        /// <summary>574: dopasowanie sztuk do typow (Fit, Resid, FitItems). Recenzja 574 (blad 1): JEDNA pula sztuk dla wszystkich rodzajow - sztuka zajmuje
        /// miejsce typu tylko w jednym rodzaju (wczesniej kopia z 1H zajmowala miejsca i wloczni, i 1H - Allocate dawal ja wloczni, a pobor mieczy stal na 0).
        /// Kolejnosc jak Allocate: rodzaje bez zastepstw, wlocznie, 1H, dwureczne bez 1H do 2H, zastepstwa 1H (dwureczne z 1H przed drzewcami), dwureczne z 1H do 2H, drzewce z 2H.
        /// W rodzaju najpierw sztuki, ktore udzwignie najmniej typow (przy progach umiejetnosci dopasowanie pelne), potem mniej wszechstronne (drzewce bez
        /// uzycia bialego, dwureczne bez 1H - zostaja dla zastepstw jak w Allocate), potem drozsze. pcs == null - same wolne miejsca.</summary>
        private static void FitMatch(KitM m, Dictionary<ItemObject, int> pcs)
        {
            int nt = m.Types.Count;
            m.Resid = new int[NK][];
            m.FitItems = new Dictionary<ItemObject, int>();
            for (int k = 0; k < NK; k++)
            {
                m.Fit[k] = 0;
                var r = new int[nt];
                for (int t = 0; t < nt; t++) r[t] = (m.Types[t].Mask & (1 << k)) != 0 ? m.Types[t].Cap : 0;
                m.Resid[k] = r;
            }
            if (!m.Skill || pcs == null || pcs.Count == 0) return;
            var rem = new Dictionary<ItemObject, int>();   // recenzja 574: wspolna pula - kazda dopasowana sztuka ja zmniejsza
            foreach (var kv in pcs) if (kv.Key != null && kv.Value > 0 && UsableAny(m, kv.Key)) rem[kv.Key] = kv.Value;
            if (rem.Count == 0) return;
            for (int k = 0; k < NK; k++) if (k != K1H && k != K2H && k != KPole) FitKind(m, k, rem, false, 0);
            // jak Allocate: wlocznie, 1H, dwureczne bez 1H do 2H, brak 1H - dwureczne z 1H i drzewce z 1H, brak 2H - reszta dwurecznych i drzewce z 2H
            FitKind(m, KPole, rem, false, 0); FitKind(m, K1H, rem, false, 0); FitKind(m, K2H, rem, false, 1);
            FitKind(m, K1H, rem, true, 0); FitKind(m, K2H, rem, false, 2); FitKind(m, K2H, rem, true, 0);
        }

        /// <summary>Recenzja 574: jeden przebieg FitMatch - rodzaj k ze sztukami podstawowymi (sub = false) albo zastepczymi (sub = true) z pozostalej puli; f1: 0 - wszystkie, 1 - tylko bez uzycia 1H, 2 - tylko z uzyciem 1H.</summary>
        private static void FitKind(KitM m, int k, Dictionary<ItemObject, int> rem, bool sub, int f1)
        {
            if (k == KCape || m.Need[k] <= 0) return;
            int nt = m.Types.Count;
            var l = new List<ItemObject>();
            var cnt = new Dictionary<ItemObject, int>();
            var ver = new Dictionary<ItemObject, int>();
            foreach (var kv in rem)
            {
                var it = kv.Key; int v = KindInfo(it);
                if (kv.Value <= 0 || !Serves(v, k, sub)) continue;
                if (f1 != 0 && ((v & F1H) != 0) != (f1 == 2)) continue;
                int c = 0;
                for (int t = 0; t < nt; t++) if ((m.Types[t].Mask & (1 << k)) != 0 && Fits(m.Types[t], it)) c++;
                if (c == 0) continue;
                l.Add(it); cnt[it] = c;
                int mk = KindsMask(it), p = 0; while (mk != 0) { p += mk & 1; mk >>= 1; }
                ver[it] = p;
            }
            if (l.Count == 0) return;
            l.Sort((a, b) =>
            {
                int c = 0;
                if (sub) c = (KindInfo(a) & 0xFF) == K2H ? ((KindInfo(b) & 0xFF) == K2H ? 0 : -1) : ((KindInfo(b) & 0xFF) == K2H ? 1 : 0);   // 1H: dwureczne przed drzewcami (Allocate)
                if (c != 0) return c;
                c = cnt[a].CompareTo(cnt[b]);
                if (c != 0) return c;
                c = ver[a].CompareTo(ver[b]);
                if (c != 0) return c;
                c = b.Value.CompareTo(a.Value);
                return c != 0 ? c : string.CompareOrdinal(a.StringId, b.StringId);
            });
            foreach (var it in l)
            {
                int x = Claim(m, k, it, rem[it]);
                if (x <= 0) continue;
                rem[it] -= x;
                m.Fit[k] += x;
                int f; m.FitItems.TryGetValue(it, out f); m.FitItems[it] = f + x;   // recenzja 574 (uwaga 1): kazde dopasowanie, takze jako zastepstwo
            }
        }

        /// <summary>574: po Allocate - pokrycie rodzaju najwyzej tyle, ile sztuk dopasowano do typow; rodzaje bez zastepstw - Used tez (reszta to nadmiar
        /// i wraca, najpierw niedopasowane). Bron biala (1H, 2H, drzewce): Used bez zmian (przydzial klas zastepstw), tylko pokrycie - wersja per rodzaj.</summary>
        private static void ClampFit(KitM m)
        {
            if (!m.Skill) return;
            for (int k = 0; k < NK; k++)
            {
                if (k == KCape || m.Cov[k] <= m.Fit[k]) continue;
                m.Cov[k] = m.Fit[k];
                if (k != K1H && k != K2H && k != KPole) m.Used[k] = m.Cov[k];
            }
        }

        /// <summary>Grupy rol partii (zolnierze z pamieci typu, bohaterowie musztry z zestawu bitewnego) i popyt. Jedna petla po rosterze, bez planow.</summary>
        private static KitM Groups(MobileParty mp, Settings s, bool noBase)
        {
            var m = new KitM();
            var r = mp != null ? mp.MemberRoster : null;
            if (r == null) return m;
            var by = new Dictionary<int, int>();
            m.Skill = SkillOn;   // 574
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
                m.Types.Add(new KitT { Ch = ch, Mask = mask, Men = n, Cap = Units(n), Hero = ch.IsHero });   // 574
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
            if (m.Skill && !UsableAny(m, it)) { m.Unfit += n; return; }   // 574: nikt w roli nie udzwignie - poza zestawem
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
            var pcs = new Dictionary<ItemObject, int>();   // 574: sztuki wedlug przedmiotu do dopasowania
            for (int i = 0; i < _stock.Count; i++)
            {
                var el = _stock.GetElementCopyAtIndex(i);
                if (el.Amount <= 0) continue;
                var it = el.EquipmentElement.Item;
                AddHave(m, it, el.Amount);
                if (it != null) { int v; pcs.TryGetValue(it, out v); pcs[it] = v + el.Amount; }
            }
            FitMatch(m, pcs);
            Allocate(m); ClampFit(m); FillReady(m);
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
                int ub = ReturnForUpgrade(pv, arm);   // 575: sztuki, ktore w bitwie dalyby komus lepsza bron/zbroje niz wydana (sprawdzone proba podgladu)
                if (ub > 0) { pv = KitPreview.ForArmoury(); _settlePv++; }   // recenzja 575: ub osobno (log migracji 562 'pod braki bitwy' bez ulepszen)
                int took = 0, sw = 0;
                if (why == WhyDaily || why == WhyRoster)
                {
                    var tc = NewTakeCtx(arm, pv);   // 575: plany wspolne dla poboru i wymiany (Sell zmniejszany przy kazdym ruchu)
                    took = TakePlayer(MeasurePlayer(), tc); _lastTook = took; _hudDirty = true;   // 562b: wynik ostatniego poboru do podpowiedzi paska
                    if (why == WhyDaily) sw = SwapCheaper(tc);   // 575: raz na dobe - tansze z puli za drozsze z zapasu, 1:1
                }
                _settleSig = m.Sig;
                _settleArmVer = arm.VersionNo;
                _settleN++; _settleBy[Math.Max(0, Math.Min(3, why))]++;
                int after = Pieces(arm) + Pieces(_stock);
                if (after != before) _settleBad++;
                if (back + bb + ub + took + sw > 0) { _hudDirty = true; try { MarchPairs.InvalidateWeight(main); } catch { } }
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
                BackToArmory(arm, el.EquipmentElement, el.Amount);   // 571: czesc gracza na liste gracza
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
                        _pBookGone += BookLose(el.EquipmentElement);   // 571 (recenzja): strata - czesc gracza traci sie OSTATNIA, jak ReconcileStock w zbrojowni
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
        /// (pokrycie sie nie zmienia); sztuki spoza rodzajow (stare rekawice) zawsze. Do czesci ludzi - K1 je potem sprzeda jak kazda nadwyzke; 571: sztuki
        /// wziete z listy gracza (_fromBook) wracaja na liste gracza (BackToArmory). 574: sztuki, ktorych nikt w roli nie udzwignie (Unfit), wracaja zawsze
        /// (sprzatanie zapasu sprzed 574 - przy pierwszym rozliczeniu); nadmiar najpierw ze sztuk niedopasowanych do typow, dopiero potem z dopasowanych.</summary>
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
            any += m.Unfit;   // 574: za trudne - zawsze do zbrojowni
            if (any <= 0) return 0;
            int[] p2 = { m.L2N, m.L21 }, pp = { m.LP0, m.LP2, m.LP1, m.LPb };
            var els = new List<ItemRosterElement>();
            for (int i = 0; i < _stock.Count; i++) { var el = _stock.GetElementCopyAtIndex(i); if (el.Amount > 0) els.Add(el); }
            els.Sort((x, y) => y.EquipmentElement.ItemValue.CompareTo(x.EquipmentElement.ItemValue));   // najlepsze najpierw
            // 574: sztuki uzywalne, ale niedopasowane do typow (FitItems - dopasowane wedlug przedmiotu) wracaja jako nadmiar PRZED dopasowanymi
            // (inaczej zwrot drogiej dopasowanej sztuki obnizalby pokrycie, a pobor bralby ja jutro z powrotem)
            Dictionary<ItemObject, int> unm = null;
            if (m.Skill && m.FitItems != null)
            {
                unm = new Dictionary<ItemObject, int>();
                foreach (var el in els)
                {
                    var it = el.EquipmentElement.Item;
                    if (it == null || KindInfo(it) < 0 || !UsableAny(m, it)) continue;
                    int u; unm.TryGetValue(it, out u); unm[it] = u + el.Amount;
                }
                foreach (var key in new List<ItemObject>(unm.Keys)) { int f; m.FitItems.TryGetValue(key, out f); unm[key] = Math.Max(0, unm[key] - f); }
            }
            var left = new int[els.Count];
            for (int i = 0; i < els.Count; i++) left[i] = els[i].Amount;
            int n = 0;
            for (int pass = 0; pass < 3; pass++)   // 0: spoza rodzajow i za trudne (cale), 1: nadmiar z niedopasowanych (574), 2: reszta nadmiaru
            {
                if (pass == 1 && unm == null) continue;
                for (int i = 0; i < els.Count; i++)
                {
                    var el = els[i];
                    if (left[i] <= 0) continue;
                    var it = el.EquipmentElement.Item;
                    int v = KindInfo(it), give;
                    if (v < 0) { if (pass != 0) continue; give = left[i]; _pBackOther += give; }
                    else if (m.Skill && !UsableAny(m, it)) { if (pass != 0) continue; give = left[i]; _pBackHard += give; T581HardNote(it, give, true); }   // 574; 583: autotest 581 - sztuki testu (poza testem nic)
                    else
                    {
                        if (pass == 0) continue;
                        int b = v & 0xFF;
                        if (ex[b] <= 0) continue;
                        give = Math.Min(left[i], ex[b]);
                        int u = 0;
                        if (pass == 1) { unm.TryGetValue(it, out u); give = Math.Min(give, u); }
                        if (b == K2H) give = Math.Min(give, p2[Cls2H(v)]);
                        else if (b == KPole) give = Math.Min(give, pp[ClsPole(v)]);
                        if (give <= 0) continue;
                        if (b == K2H) p2[Cls2H(v)] -= give;
                        else if (b == KPole) pp[ClsPole(v)] -= give;
                        if (pass == 1) unm[it] = u - give;
                        ex[b] -= give;
                        _pBack += give;
                    }
                    if (give <= 0) continue;
                    left[i] -= give;
                    BackToArmory(arm, el.EquipmentElement, give);   // 571: czesc gracza na liste gracza, reszta na stan wojska
                    n += give;
                }
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
                        BackToArmory(arm, el.EquipmentElement, give);   // 571: czesc gracza na liste gracza (bitwa i tak wyda ja z calej zbrojowni)
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

        private static bool CheapOn { get { var s = Settings.Current; return s != null && s.DrillKitCheapestSwap; } }

        /// <summary>575: plany jednego rozliczenia - pula ludzi (PlayerSurplusPlan z podgladem, KeepGiven) i lista gracza (PlayerBookPlan), budowane leniwie na typ;
        /// wspolne dla poboru (TakePlayer) i wymiany (SwapCheaper): Sell planu maleje przy kazdym ruchu, wiec ta sama sztuka nie idzie dwa razy. Keep - KeepFor(pv):
        /// na przedmiot zostaje w zbrojowni co najmniej tyle, ile wyda nastepna bitwa - lista gracza zawsze (571), pula ludzi przy 575 (KeepMen; dotad tylko
        /// przez KeepGiven przy KitPreview.SideOptional).</summary>
        private sealed class TakeCtx
        {
            internal ItemRoster Arm; internal KitPreview.Result Pv;
            internal Dictionary<ItemObject, int> Keep;
            internal bool Book, KeepMen, Cheap;
            private readonly Dictionary<int, List<SwapMath.Piece>> _men = new Dictionary<int, List<SwapMath.Piece>>(), _bk = new Dictionary<int, List<SwapMath.Piece>>();
            internal List<SwapMath.Piece> MenPlan(ItemObject.ItemTypeEnum t)
            { List<SwapMath.Piece> pl; if (!_men.TryGetValue((int)t, out pl)) _men[(int)t] = pl = MenPurse.PlayerSurplusPlan(Arm, t, Pv); return pl; }
            internal List<SwapMath.Piece> BookPlan(ItemObject.ItemTypeEnum t)
            { List<SwapMath.Piece> pl; if (!_bk.TryGetValue((int)t, out pl)) _bk[(int)t] = pl = MenPurse.PlayerBookPlan(Arm, t); return pl; }
        }

        private static TakeCtx NewTakeCtx(ItemRoster arm, KitPreview.Result pv)
        {
            var c = new TakeCtx { Arm = arm, Pv = pv, Cheap = CheapOn };
            bool book = BookOn;
            if (pv != null && (book || c.Cheap)) c.Keep = MenPurse.KeepFor(pv);   // 571: bez podgladu z listy gracza nic (nie wiadomo, czego bitwa nie wyda)
            c.Book = book && c.Keep != null;
            c.KeepMen = c.Cheap && c.Keep != null;
            return c;
        }

        /// <summary>575: kandydat poboru - sztuka planu (P), pula (Book - lista gracza), typ w TakeTypes (T), miejsce w planie (Ix - porzadek WorseFirst), cena.</summary>
        private sealed class TakeC { internal SwapMath.Piece P; internal EquipmentElement El; internal bool Book, Dead; internal int T, Ix, Price; }

        /// <summary>575: kandydaci rodzaju k z obu pul i wszystkich typow TakeTypes(k) (te same limity co dotad: Sell &gt; 0, Eligible, drzewce do 2H tylko z F2H,
        /// lista gracza bez id z rozkazem ksiegi musztry; przy 575 bez sztuk oddanych pod bitwe w ostatnich UpgBlockDays dniach). Kolejnosc: przy 575 cena rosnaco,
        /// przy rownej - pula ludzi przed lista gracza, potem typ i WorseFirst; bez 575 - jak dotad: pula ludzi (typ po typie, WorseFirst), potem lista gracza.</summary>
        private static List<TakeC> Candidates(int k, TakeCtx c)
        {
            var l = new List<TakeC>();
            var types = TakeTypes(k);
            for (int pool = 0; pool < 2; pool++)
            {
                if (pool == 1 && !c.Book) break;
                for (int ti = 0; ti < types.Length; ti++)
                {
                    var t = types[ti];
                    var pl = pool == 0 ? c.MenPlan(t) : c.BookPlan(t);
                    if (pl == null) continue;
                    for (int ix = 0; ix < pl.Count; ix++)
                    {
                        var p = pl[ix];
                        if (p.Sell <= 0) continue;
                        var el = QuartermasterLaw.ElOf(p);
                        var it = el.Item;
                        if (it == null || !Eligible(it) || (k == K2H && t == ItemObject.ItemTypeEnum.Polearm && (KindInfo(it) & F2H) == 0)) continue;
                        if (pool == 1 && MusterBook.IsPinnedItem(it.StringId)) continue;
                        if (c.Cheap && UpgBlocked(el)) continue;
                        l.Add(new TakeC { P = p, El = el, Book = pool == 1, T = ti, Ix = ix, Price = el.ItemValue });
                    }
                }
            }
            if (c.Cheap) l.Sort(ByPrice);
            else l.Sort((a, b) => { int d = a.Book.CompareTo(b.Book); if (d != 0) return d; d = a.T.CompareTo(b.T); return d != 0 ? d : a.Ix.CompareTo(b.Ix); });
            return l;
        }

        private static int ByPrice(TakeC a, TakeC b)
        {
            int d = a.Price.CompareTo(b.Price); if (d != 0) return d;
            d = a.Book.CompareTo(b.Book); if (d != 0) return d;
            d = a.T.CompareTo(b.T); return d != 0 ? d : a.Ix.CompareTo(b.Ix);
        }

        /// <summary>575: ile sztuk kandydata mozna wziac teraz - Sell planu, zywy stan egzemplarza w zbrojowni, ochrona nastepnej bitwy na przedmiot (KeepFor:
        /// lista gracza zawsze, pula ludzi przy 575).</summary>
        private static int Avail(TakeC x, TakeCtx c)
        {
            if (x.Dead || x.P.Sell <= 0) return 0;
            int idx = c.Arm.FindIndexOfElement(x.El);
            int n = Math.Min(x.P.Sell, idx >= 0 ? c.Arm.GetElementCopyAtIndex(idx).Amount : 0);
            if (n > 0 && (x.Book || c.KeepMen))
            {
                int kp; c.Keep.TryGetValue(x.El.Item, out kp);
                n = Math.Min(n, ArmCount(c.Arm, x.El.Item) - kp);   // nastepna bitwa ma pierwszenstwo: na przedmiot zostaje co najmniej KeepFor
            }
            return Math.Max(0, n);
        }

        /// <summary>575: n sztuk kandydata ze zbrojowni do zapasu (para AddToCounts); lista gracza - StockWithdrawKey i _fromBook (jak 571). Sell planu - n.</summary>
        private static void Move(TakeC x, int n, TakeCtx c)
        {
            c.Arm.AddToCounts(x.El, -n);
            _stock.AddToCounts(x.El, n);
            if (x.Book)
            {
                ArmouryBehavior.StockWithdrawKey(x.El, n);
                string key = QuartermasterLaw.KeyOf(x.El);
                int v; _fromBook.TryGetValue(key, out v); _fromBook[key] = v + n;
            }
            x.P.Sell -= n;
        }

        /// <summary>c) Braki zestawu (rodzaje w TakeOrder) z nadwyzki K1 ludzi (Sell &gt; 0) i (571) z nienoszonych sztuk gracza; 575: jedna lista kandydatow
        /// po cenie (Candidates), bez 575 - jak dotad (najgorsze najpierw, ludzie przed lista gracza). 574: tylko dla typu z brakiem, ktory sztuke udzwignie.
        /// Sztuka schodzi ze zbrojowni do zapasu.</summary>
        private static int TakePlayer(KitM m, TakeCtx c)
        {
            int took = 0, book = 0;
            foreach (int k in TakeOrder)
            {
                int want = m.Short(k);
                if (want <= 0) continue;
                foreach (var x in Candidates(k, c))
                {
                    int n = Math.Min(Avail(x, c), want);
                    if (n <= 0) continue;
                    int hs0 = _pHardSkip;
                    n = ClaimTake(m, k, x.El.Item, n, false);   // 574
                    if (x.Book && _pHardSkip != hs0) T581HardNote(x.El.Item, _pHardSkip - hs0, false);   // 583: autotest 581 - pominiete z listy gracza (poza testem nic)
                    if (n <= 0) continue;
                    Move(x, n, c);
                    want -= n; took += n;
                    if (x.Book) book += n;
                    if (want <= 0) break;
                }
            }
            _pTaken += took;
            _pTakenBook += book;
            return took;
        }

        // ------------------------------------------------------------ 575: najtansze i wymiana
        // 575 (Jeff 15.11 "jesli zabral luk tier 6 do treningu a pojawily sie luki tansze w armoury to oddaje luk tier 6 i zabiera najtanszy ... zeby stos do nauki
        // nie zabieral tier 6 sprzetu a jak zabiera to potem wymienia na tanszy i oddaje do armoury drozszy aby inne wojsko moglo go zalozyc do bitwy"; audyt A/B/C:
        // pobor szedl po WorseFirst - tier i sila, bez ceny - pula ludzi zawsze przed lista gracza; pelny zestaw nigdy sie nie wymienial; ReturnForBattle oddawal
        // tylko pod PUSTE sloty, nie pod ulepszenie).
        private const int SwapPerDay = 100;     // limit wymian na dobe (gracz)
        private const int UpgBlockDays = 7;     // sztuka oddana pod bitwe nie wraca do zapasu przez tyle dni (bez ping-ponga przy losowym pancerzu podgladu - K7)
        private static readonly Dictionary<string, int> _upgBlock = new Dictionary<string, int>(StringComparer.Ordinal), _upgNo = new Dictionary<string, int>(StringComparer.Ordinal);   // recenzja 575: egzemplarz (KeyOf - przedmiot + modyfikator) -> dzien
        private static int _pSwapN, _pSwapCovDrop, _pUpgBack, _pUpgNo, _pUpgTrial, _pSwapBookIn;   // recenzja 575: _pSwapBookIn - tansze wziete z listy gracza w wymianie
        private static long _pSwapOutV, _pSwapInV;
        private static readonly int[] _pSwapOutT = new int[7], _pSwapInT = new int[7];

        private static int TierIx(ItemObject it) { int t = 1; try { t = AiGear.TierOf(it); } catch (Exception e) { Stumble("TierOf", e); } return Math.Max(1, Math.Min(6, t)); }

        private static bool UpgBlocked(EquipmentElement el) { int d; return el.Item != null && _upgBlock.TryGetValue(QuartermasterLaw.KeyOf(el), out d) && (int)CampaignTime.Now.ToDays - d < UpgBlockDays; }

        /// <summary>575: tansza sztuka moze zastapic drozsza - kazdy typ z rola, ktorej drozsza sluzy (KindsMask), i ktory drozsza udzwignie, udzwignie tez tansza
        /// (pokrycie per typ - 574 - nie spada). Bez 574 - zawsze.</summary>
        private static bool FitsSuper(KitM m, ItemObject cheap, ItemObject dear)
        {
            if (m == null || !m.Skill || m.Types.Count == 0) return true;
            int mk = KindsMask(dear);
            foreach (var t in m.Types) if ((t.Mask & mk) != 0 && Fits(t, dear) && !Fits(t, cheap)) return false;
            return true;
        }

        /// <summary>575: codzienna wymiana na tansze (gracz, po poborze): dla kazdego rodzaju sztuki zapasu od najdrozszej; dopoki w pulach (te same limity co pobor:
        /// Sell, stan zbrojowni, KeepFor, Eligible, rozkaz ksiegi, blokada UpgBlockDays) lezy sztuka TANSZA tej samej klasy (KindInfo - rodzaj i uzycie 1H/2H, wiec
        /// przydzial Allocate sie nie zmienia), ktora udzwigna wszyscy, ktorzy udzwigna drozsza (FitsSuper) - tansza do zapasu (Move), drozsza do zbrojowni
        /// (BackToArmory - czesc gracza na liste gracza). Bilans 1:1; najwyzej SwapPerDay na dobe. Bez ping-ponga: kazda wymiana scisle obniza wartosc zapasu,
        /// a pobor bierze tylko na brak i najtansze najpierw - oddana drozsza wraca do zapasu tylko wtedy, gdy nie ma tanszej (i wtedy nie ma z czym jej wymienic).</summary>
        private static int SwapCheaper(TakeCtx c)
        {
            if (!c.Cheap || _stock.Count == 0) return 0;
            int n = 0;
            try
            {
                var m = MeasurePlayer();
                int cov0 = Sum(m.Cov);
                foreach (int k in TakeOrder)
                {
                    if (k == KCape || n >= SwapPerDay) continue;   // 562c: oponcze-dodatek - ludzie nowych nie biora
                    var dear = new List<ItemRosterElement>();
                    for (int i = 0; i < _stock.Count; i++)
                    {
                        var el = _stock.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (el.Amount <= 0 || it == null || KindOf(it) != k) continue;
                        if (m.Skill && !UsableAny(m, it)) continue;   // 574: za trudne wraca przez ReturnExcess
                        dear.Add(el);
                    }
                    if (dear.Count == 0) continue;
                    dear.Sort((a, b) => b.EquipmentElement.ItemValue.CompareTo(a.EquipmentElement.ItemValue));
                    List<TakeC> cands = null;
                    foreach (var del in dear)
                    {
                        var dIt = del.EquipmentElement.Item;
                        int dv = del.EquipmentElement.ItemValue, dInfo = KindInfo(dIt), left = del.Amount;
                        while (left > 0 && n < SwapPerDay)
                        {
                            if (cands == null) cands = Candidates(k, c);   // przy 575 - cena rosnaco
                            TakeC pick = null;
                            foreach (var x in cands)
                            {
                                if (x.Dead) continue;
                                if (x.Price >= dv) break;   // dalej juz nie tansze
                                var it = x.El.Item;
                                if (KindInfo(it) != dInfo || !FitsSuper(m, it, dIt)) continue;
                                if (Avail(x, c) <= 0) { x.Dead = true; continue; }
                                pick = x; break;
                            }
                            if (pick == null) break;
                            Move(pick, 1, c);
                            if (pick.Book) _pSwapBookIn++;   // recenzja 575: osobno od _pTakenBook (pobor), para do _pBookBack z BackToArmory
                            BackToArmory(c.Arm, del.EquipmentElement, 1);   // 571: czesc gracza na liste gracza, reszta na stan wojska
                            left--; n++;
                            _pSwapOutT[TierIx(dIt)]++; _pSwapInT[TierIx(pick.El.Item)]++; _pSwapOutV += dv; _pSwapInV += pick.Price;
                        }
                    }
                }
                if (n > 0) { int cov1 = Sum(MeasurePlayer().Cov); if (cov1 < cov0) _pSwapCovDrop += cov0 - cov1; }   // kontrola: wymiana nie moze obnizyc pokrycia
            }
            catch (Exception e) { Stumble("SwapCheaper", e); }
            _pSwapN += n;
            return n;
        }

        private sealed class Slot { internal CharacterObject Ch; internal SwapMath.Piece P; internal int Left; }

        /// <summary>575: wydane w podgladzie sztuki grupy (typ, sztuka, ilu ludzi) - najgorsze najpierw (WorseFirst).</summary>
        private static List<Slot> SlotsOf(Dictionary<CharacterObject, Dictionary<ItemObject, int>> by)
        {
            var l = new List<Slot>();
            foreach (var kv in by)
            {
                if (kv.Key == null) continue;
                foreach (var kv2 in kv.Value)
                    if (kv2.Key != null && kv2.Value > 0) l.Add(new Slot { Ch = kv.Key, P = QuartermasterLaw.PieceOf(new EquipmentElement(kv2.Key), 1), Left = kv2.Value });
            }
            l.Sort((a, b) => SwapMath.WorseFirst(a.P, b.P));
            return l;
        }

        /// <summary>575 (rozszerzenie ReturnForBattle): pierwszenstwo bitwy takze przy ULEPSZENIU - sztuka zapasu (najdrozsze najpierw), ktora w podgladzie nastepnej
        /// bitwy dalaby czlowiekowi lepsza sztuke tej samej grupy broni niz wydana (SwapMath.Better &gt; 0 - tier, sila, wrak, jakosc) i ktora ten typ udzwignie
        /// (GearFit.ForTroop, jak FitsAny), idzie do proby podgladu (KitPreview.Trial - ten sam rozdzielacz DTE z ta sztuka w zbrojowni); wraca tylko tyle, o ile
        /// proba wydaje jej wiecej (Given), reszta - "odrzucone" do konca doby. Oddana sztuka nie wraca do zapasu przez UpgBlockDays dni (UpgBlocked).
        /// Po zwrocie wolajacy liczy podglad od nowa (jak po ReturnForBattle).</summary>
        private static int ReturnForUpgrade(KitPreview.Result pv, ItemRoster arm)
        {
            if (!CheapOn || pv == null || pv.GivenBy == null || pv.GivenBy.Count == 0 || _stock.Count == 0) return 0;
            int n = 0;
            try
            {
                int day = (int)CampaignTime.Now.ToDays;
                var cand = new List<ItemRosterElement>();
                for (int i = 0; i < _stock.Count; i++)
                {
                    var el = _stock.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (el.Amount <= 0 || it == null || KindInfo(it) < 0) continue;
                    int nd; if (_upgNo.TryGetValue(QuartermasterLaw.KeyOf(el.EquipmentElement), out nd) && nd == day) continue;   // proba dzis juz odrzucila ten egzemplarz
                    if (!pv.GivenBy.ContainsKey(KitGaps.Group(it))) continue;
                    cand.Add(el);
                }
                if (cand.Count == 0) return 0;
                cand.Sort((a, b) => b.EquipmentElement.ItemValue.CompareTo(a.EquipmentElement.ItemValue));
                var slots = new Dictionary<int, List<Slot>>();
                var pending = new List<Tuple<EquipmentElement, int>>();
                bool q = GearFit.Quiet; GearFit.Quiet = true;
                try
                {
                    foreach (var el in cand)
                    {
                        var it = el.EquipmentElement.Item;
                        int g = KitGaps.Group(it);
                        List<Slot> sl;
                        if (!slots.TryGetValue(g, out sl)) slots[g] = sl = SlotsOf(pv.GivenBy[g]);
                        var pc = QuartermasterLaw.PieceOf(el.EquipmentElement, 1);
                        int got = 0;
                        foreach (var s in sl)
                        {
                            if (s.Left <= 0 || SwapMath.Better(pc, s.P) <= 0 || !GearFit.ForTroop(s.Ch, it, GearFit.On)) continue;
                            int x = Math.Min(el.Amount - got, s.Left);
                            s.Left -= x; got += x;
                            if (got >= el.Amount) break;
                        }
                        if (got > 0) pending.Add(Tuple.Create(el.EquipmentElement, got));
                    }
                }
                finally { GearFit.Quiet = q; }
                if (pending.Count == 0) return 0;
                _pUpgTrial++;
                var tr = KitPreview.Trial(pending);
                if (tr == null) return 0;
                var acc = new Dictionary<ItemObject, int>();
                foreach (var kv in tr.Given) { int b; pv.Given.TryGetValue(kv.Key, out b); if (kv.Value > b) acc[kv.Key] = kv.Value - b; }
                foreach (var t in pending)
                {
                    var it = t.Item1.Item;
                    int a; acc.TryGetValue(it, out a);
                    int x = Math.Min(t.Item2, a);
                    if (x > 0) { BackToArmory(arm, t.Item1, x); acc[it] = a - x; n += x; _upgBlock[QuartermasterLaw.KeyOf(t.Item1)] = day; }   // 571: czesc gracza na liste gracza
                    if (x < t.Item2) { _upgNo[QuartermasterLaw.KeyOf(t.Item1)] = day; _pUpgNo += t.Item2 - x; }
                }
            }
            catch (Exception e) { Stumble("ReturnForUpgrade", e); }
            _pUpgBack += n;
            return n;
        }

        /// <summary>575: rozklad zapasu musztry wedlug tierow T1..T6 (sztuki rodzajow zestawu) - do linii doby.</summary>
        private static string StockTiers()
        {
            var t = new int[7];
            for (int i = 0; i < _stock.Count; i++)
            {
                var el = _stock.GetElementCopyAtIndex(i);
                if (el.Amount > 0 && KindOf(el.EquipmentElement.Item) >= 0) t[TierIx(el.EquipmentElement.Item)] += el.Amount;
            }
            return t[1] + "/" + t[2] + "/" + t[3] + "/" + t[4] + "/" + t[5] + "/" + t[6];
        }

        private static string TierList(int[] t)
        {
            var sb = new StringBuilder();
            for (int i = 6; i >= 1; i--) { if (t[i] <= 0) continue; if (sb.Length > 0) sb.Append(", "); sb.Append('T').Append(i).Append(" x").Append(t[i]); }
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        /// <summary>575: fragment linii doby gracza (wymiana, oddane pod bitwe, tiery zapasu).</summary>
        private static string SwapLog()
        {
            var sb = new StringBuilder(", 575: wymieniono na tansze ").Append(_pSwapN);
            if (_pSwapN > 0)
                sb.Append(" (oddane: ").Append(TierList(_pSwapOutT)).Append(" / sr. cena ").Append(_pSwapOutV / _pSwapN).Append(" d, wziete: ").Append(TierList(_pSwapInT))
                  .Append(" / sr. cena ").Append(_pSwapInV / _pSwapN).Append(" d; z listy gracza ").Append(_pSwapBookIn).Append("; bilans 1:1)");
            if (_pSwapCovDrop > 0) sb.Append(", SPADEK pokrycia po wymianie ").Append(_pSwapCovDrop);
            sb.Append(", oddane pod bitwe (lepsze niz wydane) ").Append(_pUpgBack).Append(" (prob podgladu ").Append(_pUpgTrial).Append(", odrzucone proba ").Append(_pUpgNo).Append(')');
            sb.Append(", zapas wedlug tierow T1..T6 ").Append(StockTiers());
            if (!CheapOn) sb.Append(" (klucz Drill Kit Cheapest Swap wylaczony)");
            return sb.ToString();
        }

        // ------------------------------------------------------------ 571: musztra z listy gracza
        // 571 (Jeff 15.11, zbrojownia pelna helmow gracza, podpowiedz "Missing most ... 130 helmets"; log 15.11 20:53: HeadArmor "wklady nienoszone 371 zostaja gracza",
        // "zapas ludzi" 43 -> 18, papier potrzeba 168): TakePlayer bral tylko z nadwyzki LUDZI (PlayerSurplusPlan - Sell z MenFree ponad komplet + 10%), a sztuki
        // gracza (ksiega wkladow) lezaly nietkniete. Teraz (klucz DrillTakesPlayerStash) po nadwyzce ludzi brak bierze sie z NIENOSZONYCH sztuk gracza
        // (MenPurse.PlayerBookPlan), najgorsze najpierw (WorseFirst), bez sztuk wykluczonych z bitwy, bez wyrobow gracza i unikatow (Eligible), bez id z rozkazem
        // ksiegi musztry, i nigdy tego, co wyda nastepna bitwa: na przedmiot zostaje w zbrojowni co najmniej KeepFor (Given + sztuki zapelniajace braki podgladu)
        // - liczone na zywym stanie zbrojowni (po poborze z nadwyzki ludzi). Bez podgladu bitwy - z listy gracza nic.
        // Wlasnosc: sztuka schodzi z listy gracza (StockWithdrawKey - ksiega per id i rozbicie) do zapasu musztry, a _fromBook pamieta, ile sztuk tego egzemplarza
        // w zapasie to sztuki gracza. Zwrot do zbrojowni (nadmiar, braki bitwy, zapas wylaczony, pojmanie z C4): czesc gracza wychodzi PIERWSZA
        // i wraca na LISTE GRACZA (StockDepositKey), nie na stan wojska, wiec K1 jej nie sprzeda ludziom. Zuzycie i strata przy pojmaniu (bez C4): czesc gracza
        // traci sie OSTATNIA (BookLose - pamiec przycieta do reszty zapasu, jak ReconcileStock dla ksiegi w zbrojowni; recenzja 571). Zamknieta ekonomia: ani sztuka, ani moneta nie powstaje (sam ruch
        // zbrojownia <-> zapas i etykieta wlasnosci).
        private static readonly Dictionary<string, int> _fromBook = new Dictionary<string, int>(StringComparer.Ordinal);
        private static int _pTakenBook, _pBookBack, _pBookGone;

        private static bool BookOn { get { var s = Settings.Current; return s != null && s.DrillTakesPlayerStash; } }

        /// <summary>571: n sztuk egzemplarza el wychodzi z zapasu - ile z nich to czesc gracza (zdjeta z pamieci; czesc gracza wychodzi pierwsza).</summary>
        private static int BookOut(EquipmentElement el, int n)
        {
            if (n <= 0 || _fromBook.Count == 0 || el.Item == null) return 0;
            string key = QuartermasterLaw.KeyOf(el);
            int v; if (!_fromBook.TryGetValue(key, out v) || v <= 0) return 0;
            int x = Math.Min(v, n);
            if (v - x > 0) _fromBook[key] = v - x; else _fromBook.Remove(key);
            return x;
        }

        /// <summary>571 (recenzja): sztuki egzemplarza el ZNIKNELY z zapasu (strata przy pojmaniu bez C4, zuzycie) - czesc gracza traci sie OSTATNIA:
        /// pamiec przycieta do tego, co zostalo w zapasie (ta sama regula co ReconcileStock dla ksiegi gracza w zbrojowni). Wolac PO zdjeciu z _stock.
        /// Zwraca sztuki gracza, ktore przepadly.</summary>
        private static int BookLose(EquipmentElement el)
        {
            if (_fromBook.Count == 0 || el.Item == null) return 0;
            string key = QuartermasterLaw.KeyOf(el);
            int v; if (!_fromBook.TryGetValue(key, out v) || v <= 0) return 0;
            int idx = _stock.FindIndexOfElement(el);
            int h = idx >= 0 ? Math.Max(0, _stock.GetElementCopyAtIndex(idx).Amount) : 0;
            if (v <= h) return 0;
            if (h > 0) _fromBook[key] = h; else _fromBook.Remove(key);
            return v - h;
        }

        /// <summary>571: n sztuk egzemplarza el z zapasu do zbrojowni - czesc gracza wraca na liste gracza (ksiega), reszta na stan wojska (jak dotad).</summary>
        private static void BackToArmory(ItemRoster arm, EquipmentElement el, int n)
        {
            _stock.AddToCounts(el, -n);
            arm.AddToCounts(el, n);
            int own = BookOut(el, n);
            if (own > 0) { ArmouryBehavior.StockDepositKey(el, own); _pBookBack += own; }
        }

        private static int ArmCount(ItemRoster arm, ItemObject it)
        {
            int n = 0;
            for (int i = 0; i < arm.Count; i++) { var el = arm.GetElementCopyAtIndex(i); if (el.Amount > 0 && ReferenceEquals(el.EquipmentElement.Item, it)) n += el.Amount; }
            return n;
        }

        /// <summary>571: ile sztuk zapasu to sztuki gracza (pamiec _fromBook) - do linii doby.</summary>
        private static int BookPieces() { int n = 0; foreach (var v in _fromBook.Values) n += v; return n; }

        // 574: liczniki doby - za trudne oddane z zapasu gracza, pominiete przy braniu (gracz: TakePlayer z Candidates, Accept; AI: rezerwacja)
        private static int _pBackHard, _pHardSkip, _aHardSkip;

        /// <summary>574: Claim przy braniu sztuki do zestawu + licznik "za trudne" (nikt w roli z rodzajem k jej nie udzwignie).</summary>
        private static int ClaimTake(KitM m, int k, ItemObject it, int x, bool ai)
        {
            int got = Claim(m, k, it, x);
            if (got < x && !FitsKind(m, k, it)) { if (ai) _aHardSkip += x - got; else _pHardSkip += x - got; }
            return got;
        }

        /// <summary>571: pamiec czesci gracza przycieta do stanu zapasu (wczytanie, kontrola) - klucz bez sztuk w zapasie znika. Zwraca przyciete sztuki.</summary>
        private static int ClipBook()
        {
            if (_fromBook.Count == 0) return 0;
            var have = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < _stock.Count; i++)
            {
                var el = _stock.GetElementCopyAtIndex(i);
                if (el.Amount <= 0 || el.EquipmentElement.Item == null) continue;
                string key = QuartermasterLaw.KeyOf(el.EquipmentElement);
                int v; have.TryGetValue(key, out v); have[key] = v + el.Amount;
            }
            int cut = 0;
            foreach (var key in new List<string>(_fromBook.Keys))
            {
                int h; have.TryGetValue(key, out h);
                int v = _fromBook[key];
                if (v <= h) continue;
                cut += v - Math.Max(0, h);
                if (h > 0) _fromBook[key] = h; else _fromBook.Remove(key);
            }
            return cut;
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
            FitMatch(m, null);   // 574: wolne miejsca typow (rezerwacja bierze tylko sztuki, ktore typ z brakiem udzwignie)
            if (arm != null && arm.Count > 0 && m.Kit > 0)
            {
                var plans = new Dictionary<int, List<SwapMath.Piece>>();
                int meleeLeft = -1;   // -1 = jeszcze nie liczone; int.MaxValue = bez grupy
                bool cheap = CheapOn;   // 575: rezerwacja po cenie z obu typow 2H razem (liczona od nowa co dobe - wymiana na tansze dzieje sie sama)
                foreach (int k in TakeOrder)
                {
                    int want = m.Need[k];
                    if (want <= 0) continue;
                    var types = TakeTypes(k);
                    var cl = new List<TakeC>();
                    for (int ti = 0; ti < types.Length; ti++)
                    {
                        var t = types[ti];
                        List<SwapMath.Piece> pl;
                        if (!plans.TryGetValue((int)t, out pl)) plans[(int)t] = pl = MenPurse.AiSurplusPlan(arm, mp.MemberRoster, t);
                        if (pl == null) continue;
                        for (int ix = 0; ix < pl.Count; ix++)
                        {
                            var p = pl[ix];
                            var el = QuartermasterLaw.ElOf(p);
                            var it = el.Item;
                            if (it == null || p.Sell <= 0 || !Eligible(it) || (k == K2H && t == ItemObject.ItemTypeEnum.Polearm && (KindInfo(it) & F2H) == 0)) continue;
                            cl.Add(new TakeC { P = p, El = el, T = ti, Ix = ix, Price = el.ItemValue });
                        }
                    }
                    if (cheap) cl.Sort(ByPrice);
                    else cl.Sort((a, b) => { int d = a.T.CompareTo(b.T); return d != 0 ? d : a.Ix.CompareTo(b.Ix); });   // jak dotad: typ po typie, WorseFirst
                    foreach (var x in cl)
                    {
                        var it = x.El.Item;
                        bool melee = AiGear.Melee((int)types[x.T]);
                        if (melee && meleeLeft < 0) meleeLeft = MenPurse.AiMeleeLeft(arm, mp.MemberRoster);
                        int have; res.TryGetValue(it, out have);
                        int inArm; arm.TryGetValue(it, out inArm);
                        int n = Math.Min(Math.Min(x.P.Sell, inArm) - have, want);
                        if (melee) n = Math.Min(n, meleeLeft);
                        if (n <= 0) continue;
                        n = ClaimTake(m, k, it, n, true);   // 574
                        if (n <= 0) continue;
                        res[it] = have + n; want -= n;
                        AddHave(m, it, n);
                        if (melee && meleeLeft != int.MaxValue) meleeLeft -= n;
                        if (want <= 0) break;
                    }
                }
            }
            FitMatch(m, res);   // 574: pokrycie z rezerwacji per typ (jak u gracza z zapasu)
            Allocate(m); ClampFit(m); FillReady(m);
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
                        if (!(c.Main ? WearPlayerOne(m, k, a) : WearAiOne(mp, k, a, arm, res))) break;
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

        /// <summary>Gracz: najtansza sztuka zapasu sluzaca rodzajowi (najpierw typ podstawowy, potem zastepstwa); 574: tylko sztuka, ktora ktos w roli z rodzajem k
        /// udzwignie (za trudne nie cwicza, wiec sie nie zuzywaja - wracaja przy rozliczeniu).</summary>
        private static bool WearPlayerOne(KitM m, int k, Acc a)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                int best = -1, bv = int.MaxValue;
                for (int i = 0; i < _stock.Count; i++)
                {
                    var el = _stock.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0 || !Serves(KindInfo(el.EquipmentElement.Item), k, pass == 1)) continue;
                    if (m != null && m.Skill && !FitsKind(m, k, el.EquipmentElement.Item)) continue;
                    int v = el.EquipmentElement.ItemValue;
                    if (v < bv) { bv = v; best = i; }
                }
                if (best < 0) continue;
                var pick = _stock.GetElementCopyAtIndex(best).EquipmentElement;
                _stock.AddToCounts(pick, -1);
                _pBookGone += BookLose(pick);   // 571 (recenzja): zuzycie - czesc gracza ostatnia (jak strata i ReconcileStock)
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
                        x = ClaimTake(m, k, el.EquipmentElement.Item, x, false);   // 574: tylko sztuki, ktore typ z brakiem udzwignie
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
            if (m.Unfit > 0) sb.Append(sb.Length > 0 ? ", " : "").Append("za trudne (574) ").Append(m.Unfit);
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
            internal int TakenBook, BookBack, Swapped, SwapBook;                  // 571: z tego z listy gracza / wrocone na liste gracza
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
        internal static bool HudCheapOn { get { return CheapOn; } }   // recenzja 575: klucz Drill Kit Cheapest Swap (podpowiedz paska)
        internal static bool HudBookOn { get { return BookOn; } }   // 571: klucz Drill Takes Player Stash (podpowiedz paska)
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
                int cfg = MenPerPiece * 4 + (HeroesOn(Settings.Current, true) ? 1 : 0) + (SkillOn ? 2 : 0);   // recenzja 562 (6): MCM Drill Men Per Piece / Drill Heroes; 574: Drill Kit Skill Check
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
                k.Taken = _pTaken; k.Back = _pBack + _pBackOther + _pBackOff; k.BackBattle = _pBackBattle + _pUpgBack;   // 575: takze oddane pod bitwe jako lepsze
                k.TakenBook = _pTakenBook; k.BookBack = _pBookBack;   // 571
                k.Swapped = _pSwapN; k.SwapBook = _pSwapBookIn;   // recenzja 575
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
