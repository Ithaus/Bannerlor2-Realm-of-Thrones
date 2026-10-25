using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// Paczka 133b (Jeff 25.10 ok. 12:57: "w widoku armii tez ma pokazywac: jakiego sprzetu brakuje zolnierzom ... de facto przeniesmy logi do zakladki armii";
    /// projekt + krytyka K1-K16 w raport-armii-133.txt, K2). ROZBICIE "SLOTOW WZORCA BEZ SZTUKI" NA RODZAJE - ta sama regula co GarrisonKit.Missing (linia
    /// "Bitwa gracza (sklad7b ...): sloty wzorca bez sztuki - gracz N"), tylko z podzialem:
    ///  - pancerz po slotach 5-9 (helm, zbroja, nogi, rekawice, plaszcz) - jak Missing;
    ///  - bron 0-3 po grupach (ItemType + klasa broni - jak CrashScribe Mends.WardGroup): kazda sztuka wzorca (sloty po kolei, glowna pierwsza) szuka u czlowieka
    ///    sztuki tej samej grupy; bez pary = brak jej rodzaju. Rodzaje: bron biala, bron boczna (biala) - K2: biala bron wzorca po pierwszej broni glownej
    ///    (miecz wlocznika, lucznika, oszczepnika), tarcza, luk, kusza, strzaly, belty, rzucana, inna. Sztuki czlowieka bez pary (DTE dal inny rodzaj albo dodatek
    ///    w wolny slot) = nadmiar; zastapione innym rodzajem = min(braki broni, nadmiar).
    /// TOZSAMOSC (sprawdzana na kazdym czlowieku, rozjazd do logu): pancerz + braki broni - zastapione = Missing, bo Missing = pancerz + max(0, bron wzorca - bron
    /// czlowieka), a (bron wzorca - bron czlowieka) = braki - nadmiar. Missing bez zmian (jedna zasada - ta sama liczba co linia bitwy).
    /// "OSTATNIA BITWA": zbierane w GarrisonKit.OwnKitSpawn (tylko druzyna gracza, Kind 0 - po wszystkich strazach, w tej samej chwili co Missing, przed ubraniem
    /// cywilnym i kijem z domu), zamykane w GarrisonKit.BattleLine (gdy wyszedl choc jeden Twoj czlowiek - inaczej zostaje poprzednia). Okno: ArmyReview.Kit.cs.
    /// Tylko odczyt - zadnej sztuki ani zlota. Stan: biezaca bitwa (ClearBattle - z GarrisonKit.ClearBattle) i ostatnia bitwa (Reset - z ArmyReview.Reset,
    /// konstruktor ArmouryBehavior: nowa kampania i kazde wczytanie). Potkniecia liczone, bez wylacznika.
    /// </summary>
    internal static class KitGaps
    {
        internal const int Helmet = 0, BodyArmour = 1, LegArmour = 2, Gloves = 3, Cape = 4, Melee = 5, SideArm = 6, Shield = 7, Bow = 8, Crossbow = 9,
                           Arrows = 10, Bolts = 11, Thrown = 12, OtherWeapon = 13, Cats = 14;
        // kolejnosc w oknie i w logu: bron, potem pancerz
        internal static readonly int[] Order = { Melee, SideArm, Shield, Bow, Crossbow, Arrows, Bolts, Thrown, OtherWeapon, Helmet, BodyArmour, LegArmour, Gloves, Cape };
        private static readonly string[] En = { "helmet", "body armour", "leg armour", "gloves", "cape", "melee weapon", "side-arm (melee)", "shield", "bow", "crossbow",
                                                "arrows", "bolts", "throwing weapons", "other weapon" };
        private static readonly string[] Pl = { "helm", "zbroja", "nogi", "rece", "plaszcz", "bron biala", "bron boczna (biala)", "tarcza", "luk", "kusza",
                                                "strzaly", "belty", "rzucana", "inna bron" };

        internal static string NameEn(int cat) { return cat >= 0 && cat < Cats ? En[cat] : "?"; }

        /// <summary>Braki jednego czlowieka wobec wzorca. Missing - ta sama liczba co GarrisonKit.Missing.</summary>
        internal sealed class Gaps
        {
            internal readonly int[] Short = new int[Cats];   // braki wedlug rodzaju (bron - przed zastapieniem innym rodzajem)
            internal readonly int[] Need = new int[Cats];    // 133c: sztuki wzorca wedlug rodzaju (ile ich wzorzec ma)
            internal int Armour, Weapons, Extra;             // pancerz 5-9 bez sztuki, sztuki broni wzorca bez pary, sztuki broni czlowieka bez pary
            internal List<KeyValuePair<int, ItemObject>> Lost;   // 133c (detail): sztuki wzorca bez pary - rodzaj i sztuka wzorca (do przyczyn w oknie)
            internal List<ItemObject> Spare;                     // 133c (detail): sztuki broni czlowieka bez pary (dodatki DTE, inny rodzaj)
            internal int Replaced { get { return Math.Min(Weapons, Extra); } }
            internal int Missing { get { return Armour + Weapons - Replaced; } }
        }

        /// <summary>Wzorzec (Assignment.ReferenceEquipment) wobec ekwipunku czlowieka - czysta funkcja, bez stanu.</summary>
        internal static Gaps Compare(Equipment rf, Equipment eq) { return Compare(rf, eq, false); }

        /// <summary>To samo; detail (133c, KitPreview) - takze listy sztuk bez pary (Lost, Spare). Liczby te same w obu trybach.</summary>
        internal static Gaps Compare(Equipment rf, Equipment eq, bool detail)
        {
            var g = new Gaps();
            if (rf == null || eq == null) return g;
            if (detail) { g.Lost = new List<KeyValuePair<int, ItemObject>>(); g.Spare = new List<ItemObject>(); }
            for (int i = 5; i <= 9; i++)
            {
                var r = rf[(EquipmentIndex)i]; var e = eq[(EquipmentIndex)i];
                if (r.IsEmpty || r.Item == null) continue;
                g.Need[i - 5]++;
                if (e.IsEmpty || e.Item == null) { g.Short[i - 5]++; g.Armour++; if (detail) g.Lost.Add(new KeyValuePair<int, ItemObject>(i - 5, r.Item)); }
            }
            var held = new List<int>(4);
            var heldIt = detail ? new List<ItemObject>(4) : null;
            for (int i = 0; i < 4; i++)
            {
                var e = eq[(EquipmentIndex)i];
                if (e.IsEmpty || e.Item == null) continue;
                held.Add(Group(e.Item));
                if (detail) heldIt.Add(e.Item);
            }
            bool primary = false;
            for (int i = 0; i < 4; i++)
            {
                var r = rf[(EquipmentIndex)i]; var it = r.Item;
                if (r.IsEmpty || it == null) continue;
                int cat = WeaponCat(it, ref primary);
                g.Need[cat]++;
                int k = held.IndexOf(Group(it));
                if (k >= 0) { held.RemoveAt(k); if (detail) heldIt.RemoveAt(k); continue; }
                g.Short[cat]++; g.Weapons++;
                if (detail) g.Lost.Add(new KeyValuePair<int, ItemObject>(cat, it));
            }
            g.Extra = held.Count;
            if (detail) g.Spare.AddRange(heldIt);
            return g;
        }

        /// <summary>133c (K15): kopia biezacej bitwy wedlug typu (porownanie z podgladem w misji - KitPreview.BattleSegment, przed Close).</summary>
        internal static List<Row> CurrentTypes() { return new List<Row>(_cur.Values); }

        /// <summary>133c: najwieksze braki wiersza (najwyzej n rodzajow, wedlug liczby): "arrows 6, shield 1". Recenzja 133 (tytuly wierszy okna): andMore - dopisek
        /// "and more" przy dalszych rodzajach; counts - liczby przy rodzajach (bez nich same nazwy - gdy czesc brakow zastapiono innym rodzajem, liczby rodzajow
        /// nie sumuja sie do pustych slotow).</summary>
        internal static string TopKinds(int[] shortBy, int n, bool en, bool andMore = true, bool counts = true)
        {
            var l = new List<int>();
            foreach (int c in Order) if (shortBy[c] > 0) l.Add(c);
            l.Sort((a, b) => { int x = shortBy[b].CompareTo(shortBy[a]); return x != 0 ? x : Array.IndexOf(Order, a).CompareTo(Array.IndexOf(Order, b)); });
            var sb = new StringBuilder();
            for (int i = 0; i < l.Count && i < n; i++)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(en ? En[l[i]] : Pl[l[i]]);
                if (counts) sb.Append(' ').Append(shortBy[l[i]]);
            }
            if (andMore && l.Count > n) sb.Append(en ? " and more" : " i inne");
            return sb.ToString();
        }

        /// <summary>133c: rodzaje brakow z tablicy (kolejnosc Order) - jak Kinds(Row).</summary>
        internal static string KindsOf(int[] shortBy, bool en)
        {
            var sb = new StringBuilder();
            foreach (int c in Order)
            {
                if (shortBy[c] <= 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(en ? En[c] : Pl[c]).Append(' ').Append(shortBy[c]);
            }
            return sb.ToString();
        }

        /// <summary>Grupa broni jak CrashScribe Mends.WardGroup: rodzaj przedmiotu i klasa broni.</summary>
        internal static int Group(ItemObject it)
        {
            var wc = it.PrimaryWeapon != null ? it.PrimaryWeapon.WeaponClass : WeaponClass.Undefined;
            return (int)it.ItemType * 1000 + (int)wc;
        }

        /// <summary>Rodzaj sztuki wzorca w slocie 0-3. primary - czy byla juz bron glowna (biala, luk, kusza, rzucana, proca, pistolet, muszkiet): biala po niej
        /// to bron boczna (K2). Amunicja i tarcza nie sa bronia glowna.</summary>
        private static int WeaponCat(ItemObject it, ref bool primary)
        {
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm:
                    { int c = primary ? SideArm : Melee; primary = true; return c; }
                case ItemObject.ItemTypeEnum.Shield: return Shield;
                case ItemObject.ItemTypeEnum.Arrows: return Arrows;
                case ItemObject.ItemTypeEnum.Bolts: return Bolts;
                case ItemObject.ItemTypeEnum.Bow: primary = true; return Bow;
                case ItemObject.ItemTypeEnum.Crossbow: primary = true; return Crossbow;
                case ItemObject.ItemTypeEnum.Thrown: primary = true; return Thrown;
                case ItemObject.ItemTypeEnum.Sling:
                case ItemObject.ItemTypeEnum.Pistol:
                case ItemObject.ItemTypeEnum.Musket: primary = true; return OtherWeapon;
                default: return OtherWeapon;   // pociski, sztandar i inne w slotach 0-3
            }
        }

        // ------------------------------------------------------------ ostatnia bitwa druzyny gracza
        /// <summary>Jeden typ wojska (albo cala druzyna) w jednej bitwie. Missing - suma GarrisonKit.Missing (liczba z linii bitwy).</summary>
        internal sealed class Row
        {
            internal string Id, Name;
            internal int Men, Missing, Armour, Weapons, Extra, Replaced, Foot, NoSaddle, Bare, Home, Civ, Hero;
            internal readonly int[] Short = new int[Cats];
        }

        internal sealed class Battle
        {
            internal CampaignTime When;
            internal bool Field;                                    // konie wpuszczone (nie oblezenie, kryjowka ani morze - GarrisonKit.MountsOk)
            internal bool Floor;                                    // liczniki podlogi broni tej misji odczytane (koniec misji)
            internal int FloorBare, FloorGot, FloorOver, FloorNone; // podloga broni przy rozdaniu (GearFit, wszyscy zdrowi druzyny gracza)
            internal int Bad;                                       // ludzi z rozjazdem tozsamosci (rozbicie bez Missing)
            internal Row All;
            internal readonly List<Row> Types = new List<Row>();
        }

        private static readonly Dictionary<string, Row> _cur = new Dictionary<string, Row>(StringComparer.Ordinal);
        private static Row _curAll = new Row();
        private static int _curBad, _badLog, _stumbles;
        private static Battle _last;

        /// <summary>Ostatnia bitwa druzyny gracza (null - zadnej w tej kampanii od wczytania).</summary>
        internal static Battle Last { get { return _last; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3 || _stumbles % 50 == 0) { try { Log.Error("KitGaps." + where + " (potkniecie " + _stumbles + ")", e); } catch { } }
        }

        /// <summary>Nowa kampania i kazde wczytanie (ArmyReview.Reset).</summary>
        internal static void Reset() { ClearBattle(); _last = null; _stumbles = 0; }

        /// <summary>Nowa bitwa (GarrisonKit.ClearBattle).</summary>
        internal static void ClearBattle() { _cur.Clear(); _curAll = new Row(); _curBad = 0; _badLog = 0; }

        /// <summary>GarrisonKit.OwnKitSpawn, druzyna gracza (Kind 0), po wszystkich strazach: braki (g - null, gdy Compare sie potknal), Missing tego czlowieka
        /// (miss - z GarrisonKit.Missing, liczba z linii bitwy), zejscie z konia z braku uprzezy, jezdziec bez konia, bez broni po strazach (przed kijem z domu),
        /// kij z domu, sloty z ubraniem cywilnym.</summary>
        internal static void Note(CharacterObject co, Gaps g, int miss, bool noSaddle, bool foot, bool bare, bool home, int civ)
        {
            try
            {
                string id = co != null ? co.StringId ?? "?" : "?";
                Row r;
                if (!_cur.TryGetValue(id, out r))
                {
                    r = new Row { Id = id, Name = co != null && co.Name != null ? co.Name.ToString() : id };
                    _cur[id] = r;
                }
                bool hero = co != null && co.IsHero;
                Add(r, g, miss, noSaddle, foot, bare, home, civ, hero);
                Add(_curAll, g, miss, noSaddle, foot, bare, home, civ, hero);
                if (g == null || g.Missing != miss)
                {
                    _curBad++;
                    if (_badLog < 3)
                    {
                        _badLog++;
                        Log.Info("Sprzet wedlug rodzaju (133b): ROZJAZD z GarrisonKit.Missing - " + id + ": Missing " + miss
                                 + (g == null ? ", rozbicie niepoliczone (potkniecie)" : ", rozbicie " + g.Missing + " (pancerz " + g.Armour + ", bron " + g.Weapons + ", nadmiar " + g.Extra + ")") + ".");
                    }
                }
            }
            catch (Exception e) { Stumble("Note", e); }
        }

        private static void Add(Row r, Gaps g, int miss, bool noSaddle, bool foot, bool bare, bool home, int civ, bool hero)
        {
            r.Men++;
            r.Missing += Math.Max(0, miss);
            if (g != null)
            {
                for (int c = 0; c < Cats; c++) r.Short[c] += g.Short[c];
                r.Armour += g.Armour; r.Weapons += g.Weapons; r.Extra += g.Extra; r.Replaced += g.Replaced;
            }
            if (noSaddle) r.NoSaddle++;
            if (foot) r.Foot++;
            if (bare) r.Bare++;
            if (home) r.Home++;
            if (civ > 0) r.Civ += civ;
            if (hero) r.Hero++;
        }

        /// <summary>GarrisonKit.BattleLine: zamkniecie bitwy - gdy wyszedl choc jeden Twoj czlowiek, to jest teraz "ostatnia bitwa" (okno). field - konie wpuszczone
        /// (GarrisonKit._mountsOk tej misji); atEnd - koniec misji (liczniki podlogi broni GearFit sa jeszcze tej misji; "poprzednia misja bez konca" - juz nastepnej).
        /// playerMen/playerMiss - liczniki linii bitwy (kontrola: te same ludzie i ta sama suma Missing). Zwraca segment linii bitwy (pusty bez Twoich ludzi).</summary>
        internal static string Close(bool field, bool atEnd, int playerMen, int playerMiss)
        {
            try
            {
                if (_curAll.Men <= 0) return "";
                var b = new Battle { When = CampaignTime.Now, Field = field, Bad = _curBad, All = _curAll };
                foreach (var r in _cur.Values) b.Types.Add(r);
                b.Types.Sort(ByGaps);
                if (atEnd)
                {
                    try { GearFit.FloorCounts(out b.FloorBare, out b.FloorGot, out b.FloorOver, out b.FloorNone); b.Floor = true; }
                    catch (Exception e) { Stumble("Close.Floor", e); }
                }
                _last = b;
                return Segment(b, playerMen, playerMiss);
            }
            catch (Exception e) { Stumble("Close", e); return " | sprzet gracza wedlug rodzaju (133b): blad (potkniecie)"; }
            finally { ClearBattle(); }
        }

        /// <summary>Najpierw typy z najwiekszymi brakami, potem z jezdzcami pieszo, potem najliczniejsze.</summary>
        internal static int ByGaps(Row a, Row b)
        {
            int c = b.Missing.CompareTo(a.Missing);
            if (c != 0) return c;
            c = (b.Foot + b.NoSaddle).CompareTo(a.Foot + a.NoSaddle);
            if (c != 0) return c;
            c = b.Men.CompareTo(a.Men);
            return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name);
        }

        /// <summary>Rodzaje brakow wiersza: "strzaly 6, tarcza 1" (pl) albo "arrows 6, shield 1" (en); pusty - brak brakow.</summary>
        internal static string Kinds(Row r, bool en) { return KindsOf(r.Short, en); }

        /// <summary>Suma brakow wedlug rodzaju (przed zastapieniem): pancerz + bron wzorca bez pary.</summary>
        internal static int ShortSum(Row r) { return r.Armour + r.Weapons; }

        private static string Segment(Battle b, int playerMen, int playerMiss)
        {
            var a = b.All;
            var sb = new StringBuilder(" | sprzet gracza wedlug rodzaju (133b): ludzi ").Append(a.Men).Append(", puste ").Append(a.Missing)
                .Append(" = brakow ").Append(ShortSum(a));
            string k = Kinds(a, false);
            if (k.Length > 0) sb.Append(" (").Append(k).Append(')');
            sb.Append(" - bron zastapiona innym rodzajem ").Append(a.Replaced).Append("; nadmiar broni ponad wzorzec (dodatki) ").Append(a.Extra - a.Replaced)
              .Append("; pieszo: bez konia ").Append(a.Foot).Append(", bez uprzezy ").Append(a.NoSaddle)
              .Append("; bez broni po strazach ").Append(a.Bare).Append(" (kij z domu ").Append(a.Home).Append("); ubranie cywilne ").Append(a.Civ).Append(" szt.")
              .Append("; ").Append(b.Field ? "pole (konie)" : "oblezenie/kryjowka/morze (bez koni)");
            bool same = a.Men == playerMen && a.Missing == playerMiss;
            sb.Append("; tozsamosc z Missing ").Append(b.Bad == 0 ? "zgodna" : "ROZJAZD u " + b.Bad + " ludzi")
              .Append(", z linia ").Append(same ? "zgodna" : "ROZJAZD (linia: ludzi " + playerMen + ", puste " + playerMiss + ")");
            int n = 0, more = 0;
            var ex = new StringBuilder();
            foreach (var r in b.Types)
            {
                if (r.Missing <= 0 && ShortSum(r) <= 0 && r.Foot + r.NoSaddle <= 0) continue;
                if (n >= 10) { more++; continue; }
                n++;
                if (ex.Length > 0) ex.Append("; ");
                ex.Append(r.Id).Append(" x").Append(r.Men).Append(" - puste ").Append(r.Missing);
                string rk = Kinds(r, false);
                if (rk.Length > 0) ex.Append(" (").Append(rk).Append(')');
                if (r.Replaced > 0) ex.Append(", zastapione ").Append(r.Replaced);
                if (r.Foot + r.NoSaddle > 0) ex.Append(", pieszo ").Append(r.Foot + r.NoSaddle);
            }
            sb.Append(" | typy z brakami: ").Append(n == 0 ? "brak" : ex.ToString()).Append(more > 0 ? "; i " + more + " innych" : "");
            if (_stumbles > 0) sb.Append(" | potkniecia rozbicia ").Append(_stumbles);
            return sb.ToString();
        }
    }
}
