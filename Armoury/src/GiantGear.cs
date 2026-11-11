using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// SPRZET OLBRZYMOW JEST DLA OLBRZYMOW (Jeff 14.09: "maczugi gigantow maja
    /// miec tylko giganci, ludzie nimi nie walcza - to debilnie wyglada; usun
    /// je wszystkim armiom, ze skladow, z magazynow, wszedzie"). ROT daje
    /// giant_club / giant_bow / giant_arrows czterem oddzialom rasy giant
    /// (ROT-Troops.xml) - a DTE rozdaje je z magazynu kazdemu, kto ma skill,
    /// i kazda bitwa wysypuje je do lupu. Regula patrzy na NOSZACEGO, nie na
    /// przedmiot: wolno go miec temu, kto nosi go z WLASNEGO szablonu (olbrzymy,
    /// olbrzymi notabl i wedrowiec ROT). Czlowiek - nigdy, gracz tez.
    /// Bron, amunicja i pancerze (giant_club, giant_bow, giant_arrows,
    /// giant_garb, giant_boots, giant_handwraps) - Jeff 14.09: "pancerze tez".
    ///
    /// 424 (Jeff 11.11: "maczuga olbrzyma moga uzywac tylko olbrzymy, nie moga w niej latac lordowie, zolnierze - tylko olbrzym moze tego uzywac";
    /// klucz GiantGearGiantsOnly, czytany na zywo). Regula szablonu byla dziurawa u BOHATEROW: CharacterObject.BattleEquipments bohatera to jego
    /// BIEZACY zestaw (cs 1.4.8: return new List { HeroObject.BattleEquipment }), wiec bohater z maczuga sam sobie dawal do niej prawo, a czlowiek
    /// w giant_garb robil z partii "partie z olbrzymami" (czystki lupu i sakw stawaly). Teraz kryterium jest RASA - jedyne, po czym ROT sam rozpoznaje
    /// olbrzyma: nosi postac rasy "giant" (424-p: numer z rasy oddzialu "giant" w danych ROT; FaceGen.GetRaceOrDefault tylko kontrolnie - ROT ma
    /// postfiks na FaceGen.GetRaceNames, ktory zmienia pierwsza litere na wielka i usuwa puste wpisy, wiec indeks z tej tablicy bywa przesuniety) albo taka, ktorej szablon
    /// (OriginalCharacter) jest tej rasy - drugi czlon, bo ROT odtwarza rase bohatera dopiero w OnSessionLaunched (ROTRaceMap), a olbrzym
    /// przemieniony w upiora ma rase 2. Dla czterech oddzialow olbrzymow i Maga wynik jak dotad (rasa = szablon); zmienia sie tylko bohater.
    /// Dopiecia 424: ekran ekwipunku (HeroGear.CanUsePostfix), sito bohaterow (ItemReq.MeetsHero), zestaw zaciagu ROT (DragonUnmount),
    /// porzadek na stanie (Sweep: bohaterowie, polki osad, sakwy partii AI), kuznia (PatternGate.RareStuff), nagrody turniejow (PrizeSift).
    /// Klucz NIE albo rasa nieznaleziona: stara regula szablonu (przy kluczu NIE dokladnie jak przed 424).
    /// 424-p (po recenzji): bron olbrzyma lezaca na polu bitwy (PickupPostfix na SpawnedItemEntity.IsDisabledForAgent - gracz i AI; RallyRouted359.FindArms),
    /// "Mag's Secret" ROT nie do kupienia przy czynnej regule (MagSecretOff - warunek linii rozmowy), nagroda turnieju juz wylosowana (ValyrianBlades.PrizePostfix
    /// + przeglad turniejow po wczytaniu), trzeci zestaw bohatera (ukrycia), zestaw cywilny z szablonu, czystka po wczytaniu PO ostatecznych statystykach.
    /// </summary>
    internal static class GiantGear
    {
        private static readonly Dictionary<CharacterObject, bool> _mayWear = new Dictionary<CharacterObject, bool>();

        // 424: numer rasy giant (-2 nieustalony, -1 brak w danych; 0 = human, nigdy olbrzym) i sztuki giant_* z danych (tylko watek glowny: Sweep)
        private static int _giantRace = -2;
        private static readonly HashSet<ItemObject> _items = new HashSet<ItemObject>();
        // 424: liczniki sesji (linia czystki) - odmowy ekranu, odmowy sita bohaterow, sztuki zdjete bohaterom przy spawnie, sztuki odsiane z nagrod turniejow
        internal static int ScreenRefused, SieveRefused, SpawnHero, PrizeSifted;
        // 424-p: odmowy podniesienia z ziemi (pytania gry - licznik zapytan, nie sztuk; Interlocked, bo gra pyta tez z watkow roboczych) i ukrycia linii "Mag's Secret"
        internal static int PickupRefused, MagHidden;
        private static bool _pickupHooked;
        private static string _magState = "nie szukano";
        private static int _lastScreen, _lastSieve, _lastSpawn, _lastPrize, _lastPickup, _lastMag, _errs, _toldIn;
        private static long _sumPieces, _sumValue;

        internal static bool On { get { var s = Settings.Current; return s != null && s.GiantGearGiantsOnly; } }

        internal static bool Is(ItemObject it)
        {
            // bron, amunicja I PANCERZ olbrzymow (Jeff 14.09: "pancerze tez zrob")
            if (it == null || it.StringId == null || (!it.HasWeaponComponent && !it.HasArmorComponent)) return false;
            return it.StringId.StartsWith("giant_", StringComparison.Ordinal);
        }

        /// <summary>424: postac rasy giant albo z szablonu tej rasy. Same odczyty pol (bez slownika) - bezpieczne z kazdego watku. Rasa nieustalona - false.</summary>
        internal static bool IsGiant(CharacterObject co)
        {
            int g = _giantRace;
            if (co == null || g <= 0) return false;
            try
            {
                if (co.Race == g) return true;
                var o = co.OriginalCharacter;
                return o != null && o.Race == g;
            }
            catch { return false; }
        }

        /// <summary>Wolno nosic: 424 - tylko rasa giant (klucz i rasa ustalona); inaczej stara regula - WLASNY szablon jednostki ma sprzet olbrzymow.</summary>
        internal static bool MayWear(CharacterObject co)
        {
            if (co == null) return false;
            bool on = On;
            if (on && _giantRace > 0) return IsGiant(co);
            // 424: klucz TAK, ale rasy brak (albo pytanie przed startem sesji) - bohater wedlug szablonu, z ktorego powstal, nigdy wedlug biezacego zestawu
            if (on && co.IsHero) { var o = co.OriginalCharacter; return o != null && TemplateHas(o); }
            return TemplateHas(co);
        }

        private static bool TemplateHas(CharacterObject co)
        {
            bool ok;
            if (_mayWear.TryGetValue(co, out ok)) return ok;
            ok = false;
            try
            {
                foreach (var eq in co.BattleEquipments)
                {
                    if (eq == null) continue;
                    for (int s = 0; s <= 9; s++)
                        if (Is(eq[(EquipmentIndex)s].Item)) { ok = true; break; }
                    if (ok) break;
                }
            }
            catch { }
            _mayWear[co] = ok;
            return ok;
        }

        /// <summary>Czy w partii sluzy ktos, komu sprzet olbrzymow sie nalezy.</summary>
        internal static bool PartyHasGiants(MobileParty mp)
        {
            try
            {
                var roster = mp != null ? mp.MemberRoster : null;
                if (roster == null) return false;
                for (int i = 0; i < roster.Count; i++)
                    if (MayWear(roster.GetCharacterAtIndex(i))) return true;
            }
            catch { }
            return false;
        }

        // ------------------------------------------------------------ 424: teksty
        private static TextObject _whyWield, _whyWear;
        internal const string SieveWhy = "Only giants can use this.";

        /// <summary>Powod odmowy na ekranie ekwipunku (jeden gotowy TextObject na rodzaj - ekran pyta przy kazdej karcie).</summary>
        internal static TextObject WhyText(ItemObject it)
        {
            if (it != null && it.HasArmorComponent)
                return _whyWear ?? (_whyWear = new TextObject("Only a giant can wear this."));
            return _whyWield ?? (_whyWield = new TextObject("Only a giant can wield this."));
        }

        /// <summary>Kuznia: sprzet olbrzymow nie jest wzorem ludzkiego kowala (PatternGate.RareStuff - nienauczalny, niekuty).</summary>
        internal static bool ForgeBarred(ItemObject it) { return Is(it) && On; }

        /// <summary>Zestaw zaciagu ROT ("Mag's Secret") zdjety z gracza przy spawnie - komunikat raz na misje.</summary>
        internal static void TellPlayerSpawn()
        {
            try
            {
                var m = Mission.Current;
                int h = m != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(m) : 0;
                if (h == 0 || h == _toldIn) return;
                _toldIn = h;
                Log.Player("Giant's gear is for giants alone - you take the field with arms of your own measure.", true);
            }
            catch { }
        }

        /// <summary>Nagrody turniejow gry (HistoricalPrices.PrizeListPostfix): sprzet olbrzymow nie jest nagroda. Zwraca liczbe odsianych sztuk.</summary>
        internal static int PrizeSift(TaleWorlds.Library.MBList<ItemObject> list)
        {
            if (list == null || !On) return 0;
            int n = 0;
            for (int i = list.Count - 1; i >= 0; i--)
                if (Is(list[i])) { list.RemoveAt(i); n++; }
            PrizeSifted += n;
            return n;
        }

        // ------------------------------------------------------------ 424: sesja
        /// <summary>OnSessionLaunched (watek glowny): numer rasy i lista sztuk z danych; linia startu.</summary>
        internal static void SessionStart()
        {
            int byName = -1, byTroop = -1;
            // 424-p: zrodlem jest rasa oddzialu "giant" (race="giant" w ROT-Troops.xml - ten numer gra faktycznie nadaje postaciom). FaceGen.GetRaceNames
            // odpada: postfiks ROT zwraca "Giant" i tablice bez pustych wpisow (indeks przesuniety). GetRaceOrDefault (nielatany) tylko do porownania w logu
            // i jako zapas, gdyby oddzialu "giant" nie bylo (0 = human / brak - nigdy olbrzym).
            try { var g = MBObjectManager.Instance.GetObject<CharacterObject>("giant"); if (g != null) byTroop = g.Race; }
            catch (Exception e) { Log.Error("GiantGear.SessionStart (oddzial giant)", e); }
            try { byName = TaleWorlds.Core.FaceGen.GetRaceOrDefault("giant"); }
            catch (Exception e) { Log.Error("GiantGear.SessionStart (FaceGen.GetRaceOrDefault)", e); }
            _giantRace = byTroop > 0 ? byTroop : (byName > 0 ? byName : -1);
            _items.Clear();
            var ids = new StringBuilder();
            try
            {
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                    if (Is(it) && _items.Add(it)) ids.Append(ids.Length > 0 ? ", " : "").Append(it.StringId);
            }
            catch (Exception e) { Log.Error("GiantGear.SessionStart (lista sztuk)", e); }
            bool on = On;
            Log.Info("GiantGear (424): rasa giant " + (_giantRace > 0 ? "nr " + _giantRace : "NIEZNALEZIONA - stara regula szablonu (bohater wedlug szablonu, czystki stanu nie ma)")
                     + " (oddzial 'giant' " + byTroop + ", kontrolnie FaceGen.GetRaceOrDefault " + byName + (byName > 0 && byTroop > 0 && byName != byTroop ? " - ROZNE, biore oddzial" : "") + ")"
                     + " | regula rasy " + (on && _giantRace > 0 ? "CZYNNA" : (on ? "NIECZYNNA (brak rasy)" : "WYLACZONA (klucz Giant Gear Giants Only) - jak przed 424"))
                     + " | sprzet olbrzymow w danych: " + _items.Count + " [" + ids + "]"
                     + " | ekran: " + (HeroGear.CanUseHooked ? "CanUseItem wpiete" : "CanUseItem NIEWPIETE - ekran nie odmawia")
                     + " | sito bohaterow: ItemReq.MeetsHero | zaciag ROT: DragonUnmount | kuznia: PatternGate.RareStuff"
                     + " | nagrody turniejow: listy modelu (PrizeListPostfix) i gotowa nagroda (ValyrianBlades.PrizePostfix)"
                     + " | podniesienie z ziemi: " + (_pickupHooked ? "SpawnedItemEntity.IsDisabledForAgent wpiete" : "NIEWPIETE - czlowiek podniesie bron olbrzyma")
                     + " | czystka stanu: po wczytaniu (po ostatecznych statystykach) i co dobe (bohaterowie - 3 zestawy, polki osad, sakwy partii AI).");
        }

        // ------------------------------------------------------------ 424-p: bron olbrzyma lezaca na polu bitwy
        /// <summary>Latka misji (HeroGear.ApplyAll, raz na proces): SpawnedItemEntity.IsDisabledForAgent - metoda ZADEKLAROWANA w SpawnedItemEntity (override),
        /// wiec DeclaredMethod. Pyta o nia Agent.CanUseObject (gracz: podpowiedz i klawisz uzycia; AI: DefaultItemPickupModel.IsItemAvailableForAgent ->
        /// CanReachAndUseObject) - jedno miejsce dla obu drog.</summary>
        internal static void ApplyPickup(HarmonyLib.Harmony h)
        {
            try
            {
                var m = HarmonyLib.AccessTools.DeclaredMethod(typeof(SpawnedItemEntity), "IsDisabledForAgent", new[] { typeof(Agent) });
                if (m != null) { h.Patch(m, postfix: new HarmonyLib.HarmonyMethod(typeof(GiantGear), nameof(PickupPostfix))); _pickupHooked = true; }
                Log.Info("GiantGear (424-p): podniesienie broni olbrzyma z ziemi - " + (_pickupHooked ? "SpawnedItemEntity.IsDisabledForAgent wpiete." : "SpawnedItemEntity.IsDisabledForAgent(Agent) NIEZNALEZIONE - bez blokady."));
            }
            catch (Exception e) { Log.Error("GiantGear.ApplyPickup", e); }
        }

        /// <summary>Lezaca sztuka giant_* jest nieczynna dla kazdego, kto nie jest olbrzymem (gracz tez). Tylko odczyty pol - gra pyta takze z watkow roboczych.
        /// Poza kampania (postac nie jest CharacterObject) albo bez ustalonej rasy - bez zmian. Nigdy "nieczynna" -> "czynna".</summary>
        public static void PickupPostfix(SpawnedItemEntity __instance, Agent agent, ref bool __result)
        {
            try
            {
                if (__result || __instance == null || agent == null || _giantRace <= 0) return;
                var it = __instance.WeaponCopy.Item;
                if (it == null || !Is(it) || !On) return;
                var co = agent.Character as CharacterObject;
                if (co == null || IsGiant(co)) return;
                __result = true;
                System.Threading.Interlocked.Increment(ref PickupRefused);
            }
            catch { }
        }

        /// <summary>Nasz wlasny zbieracz broni (RallyRouted359.FindArms): czy ten agent ma ominac lezaca sztuke.</summary>
        internal static bool PickupBarred(Agent a, ItemObject it)
        {
            if (a == null || it == null || _giantRace <= 0 || !Is(it) || !On) return false;
            var co = a.Character as CharacterObject;
            return co != null && !IsGiant(co);
        }

        // ------------------------------------------------------------ 424-p: "Mag's Secret" ROT
        /// <summary>
        /// ROT sprzedaje zacieznemu u Wolnych Ludzi zestaw oddzialu giant za 10 000 (linia rozmowy "mag_secret_ask", rot ROTEnlistment OnSessionLaunched).
        /// Przy czynnej regule zestaw i tak schodzi przy kazdym spawnie (DragonUnmount) - zloto za nic. Warunek linii dostaje dopisek: regula czynna = linii
        /// nie ma (czytane na zywo - klucz NIE przywraca oferte bez wczytywania). Zdanie rozmowy jest obiektem tej kampanii (ConversationManager nowy na
        /// sesje), wiec bez Harmony i bez sprzatania; drugi raz nie owijamy (delegat juz nasz). Wolane w OnAfterSessionLaunched (po OnSessionLaunched ROT).
        /// Kto kupil sekret PRZED 424, ma go nadal na liscie zestawow ROT - zestaw schodzi przy spawnie z komunikatem (zwrot 10 000 = decyzja Jeffa).
        /// </summary>
        internal static void MagSecretOff()
        {
            _magState = "linia mag_secret_ask NIEZNALEZIONA (oferta ROT bez zmian)";
            try
            {
                var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
                var f = HarmonyLib.AccessTools.Field(typeof(TaleWorlds.CampaignSystem.Conversation.ConversationManager), "_sentences");
                var list = cm != null && f != null ? f.GetValue(cm) as List<TaleWorlds.CampaignSystem.Conversation.ConversationSentence> : null;
                if (list == null) { _magState = "lista zdan rozmowy niedostepna (oferta ROT bez zmian)"; }
                else
                {
                    int n = 0;
                    foreach (var sn in list)
                    {
                        if (sn == null || sn.Id != "mag_secret_ask") continue;
                        var old = sn.OnCondition;
                        if (old != null && old.Method != null && old.Method.DeclaringType != null && old.Method.DeclaringType.FullName != null
                            && old.Method.DeclaringType.FullName.StartsWith("Armoury.GiantGear", StringComparison.Ordinal)) { n++; continue; }
                        sn.OnCondition = () =>
                        {
                            if (On && _giantRace > 0) { MagHidden++; return false; }
                            return old == null || old();
                        };
                        n++;
                    }
                    if (n > 0) _magState = "linia mag_secret_ask pod regula (zdan " + n + ") - przy czynnej regule oferty nie ma";
                }
            }
            catch (Exception e) { _magState = "wyjatek przy linii mag_secret_ask (oferta ROT bez zmian)"; Log.Error("GiantGear.MagSecretOff", e); }
            Log.Info("GiantGear (424-p): Mag's Secret (ROT, 10 000 za zestaw olbrzyma) - " + _magState + ".");
        }

        /// <summary>424-p: nagroda giant_* juz wylosowana w trwajacym turnieju (zapis sprzed 424) - ponowne losowanie (listy modelu sa odsiane, gotowa nagrode
        /// zamienia ValyrianBlades.PrizePostfix). Zwraca liczbe turniejow z wymieniona nagroda.</summary>
        private static int RerollPrizes(ref int stumbles)
        {
            int n = 0;
            try
            {
                var tm = Campaign.Current != null ? Campaign.Current.TournamentManager : null;
                if (tm == null) return 0;
                foreach (var t in Town.AllTowns)
                {
                    try
                    {
                        var g = t != null ? tm.GetTournamentGame(t) : null;
                        if (g == null || !Is(g.Prize)) continue;
                        string was = g.Prize.StringId;
                        g.UpdateTournamentPrize(false, true);
                        n++;
                        Log.Info("GiantGear (424-p): turniej w " + t.Name + " - nagroda " + was + " losowana od nowa -> " + (g.Prize != null ? g.Prize.StringId : "brak") + ".");
                    }
                    catch (Exception e) { Stumble("GiantGear.RerollPrizes (turniej)", e, ref stumbles); }
                }
            }
            catch (Exception e) { Stumble("GiantGear.RerollPrizes", e, ref stumbles); }
            return n;
        }

        private static Equipment SetOf(Hero h, int k) { return k == 0 ? h.BattleEquipment : (k == 1 ? h.CivilianEquipment : h.StealthEquipment); }
        private static string SetName(int k) { return k == 1 ? " (cywilny)" : (k == 2 ? " (ukrycia)" : ""); }

        /// <summary>Wyjscie z kampanii: pamiec reguly szablonu, rasa, lista sztuk i liczniki od zera.</summary>
        internal static void GameEnd()
        {
            _mayWear.Clear(); _items.Clear(); _giantRace = -2; _toldIn = 0; _errs = 0;
            ScreenRefused = SieveRefused = SpawnHero = PrizeSifted = PickupRefused = MagHidden = 0;
            _lastScreen = _lastSieve = _lastSpawn = _lastPrize = _lastPickup = _lastMag = 0;
            _magState = "nie szukano";
            _sumPieces = 0; _sumValue = 0;
        }

        // ------------------------------------------------------------ 424: porzadek na stanie
        private static void Stumble(string where, Exception e, ref int stumbles)
        {
            stumbles++;
            if (_errs++ < 3) Log.Error(where, e);
        }

        /// <summary>Zdejmuje sztuki giant_* z rostera (od konca - zdjecie wiersza przesuwa indeksy). Zwraca liczbe sztuk, dolicza wartosc.</summary>
        private static int Purge(ItemRoster r, ref long value)
        {
            int n = 0;
            for (int i = r.Count - 1; i >= 0; i--)
            {
                var it = r.GetItemAtIndex(i);
                if (it == null || !_items.Contains(it)) continue;
                var el = r.GetElementCopyAtIndex(i);
                if (el.Amount <= 0) continue;
                value += (long)it.Value * el.Amount;
                n += el.Amount;
                r.AddToCounts(el.EquipmentElement, -el.Amount);
            }
            return n;
        }

        private static bool HasAny(ItemRoster r)
        {
            for (int i = r.Count - 1; i >= 0; i--)
            {
                var it = r.GetItemAtIndex(i);
                if (it != null && _items.Contains(it)) return true;
            }
            return false;
        }

        /// <summary>
        /// 424 R4 - PORZADEK NA STANIE (OnAfterSessionLaunched - po odtworzeniu ras przez ROT - i raz na dobe; watek glowny). Przy spawnie zwyklej bitwy
        /// bohater idzie w Hero.BattleEquipment bez nadpisu (DragonUnmount wraca przy AgentOverridenEquipment == null), wiec maczuge z jego wlasnego
        /// zestawu zdjac mozna tylko tutaj.
        ///  1. Bohaterowie zywi, nie-olbrzymy, trzy zestawy (bojowy, cywilny, 424-p: ukrycia), sloty 0-9: sztuka giant_* schodzi. Zamiennik - TA SAMA regula
        ///     co przy spawnie (DragonUnmount): bron / amunicja SkillsDecide.PatternFor tego samego typu w granicy umiejetnosci, pancerz SkillsDecide.TopArmor
        ///     w granicy Atletyki. 424-p: zestaw cywilny - najpierw sztuka z tego samego slotu cywilnego SZABLONU bohatera (OriginalCharacter; wzorce
        ///     bojowe prawie nigdy nie sa cywilne i AI zostawal z golym korpusem w miescie), potem wzorzec, o ile cywilny; zestaw ukrycia - pusty slot.
        ///     Brak - pusty slot. GRACZ zamiennika nie dostaje (pusty slot i komunikat - nic z niczego w rekach gracza; decyzja P2 czeka na Jeffa).
        ///     Bohater w trakcie bitwy czeka do nastepnej doby.
        ///     Sztuka zdjeta bohaterowi rodu gracza idacemu w partii gracza trafia do sakw gracza, gdy partia prowadzi olbrzymow (jest komu ja dac) -
        ///     424-p: wtedy BEZ zamiennika (pusty slot jak u gracza; zamiennik + oryginal w sakwach = sztuka z niczego).
        ///  2. Polki osad (Settlement.ItemRoster): giant_* zdjete - czlowiek tego nie kupi i nie zalozy, towarem nie jest.
        ///  3. Sakwy partii AI i karawan (bez partii gracza - te czysci CleanseTrashInBags przy menu): giant_* zdjete, chyba ze partia ma olbrzymow.
        /// KSIEGOWANIE: korekta blednego stanu, bez przelewu zlota. Sztuka u bohatera wymieniona 1:1 na zwykla tego samego typu (zestaw bohatera nie jest
        /// towarem - tak samo jak zamiana przy spawnie od 14.09); sztuki z polek i sakw to ujscie - kazda liczona w linii logu (sztuki, wartosc,
        /// suma sesji), jak dotychczasowe czystki sakw gracza i zbrojowni DTE; do LootTally nie wchodza (to nie lup).
        /// Wyjatek na jednym bohaterze / rosterze = potkniecie (log 3 pierwszych), bez globalnego wylacznika.
        /// </summary>
        internal static void AfterLoad()
        {
            try { MagSecretOff(); } catch (Exception e) { Log.Error("GiantGear.AfterLoad (Mag's Secret)", e); }
            try { Sweep(true); } catch (Exception e) { Log.Error("GiantGear.AfterLoad (czystka po wczytaniu)", e); }
        }


        internal static void Sweep(bool afterLoad)
        {
            if (!On || _giantRace <= 0 || _items.Count == 0) return;
            int heroes = 0, pieces = 0, toBags = 0, mine = 0, shelfN = 0, shelfAt = 0, bagN = 0, bagAt = 0, waiting = 0, stumbles = 0, rerolled = 0;
            long value = 0;
            var names = new StringBuilder();
            MobileParty main = null; bool mainGiants = false;
            try { main = MobileParty.MainParty; mainGiants = main != null && PartyHasGiants(main); } catch { }
            // 1. bohaterowie
            try
            {
                foreach (var h in Hero.AllAliveHeroes)
                {
                    try
                    {
                        var co = h != null ? h.CharacterObject : null;
                        if (co == null || IsGiant(co)) continue;
                        bool has = false;
                        for (int k = 0; k < 3 && !has; k++)
                        {
                            var eq0 = SetOf(h, k);   // puste pole = wspolny zestaw domyslny gry (bez giant_*), wiec nigdy do niego nie piszemy
                            if (eq0 == null) continue;
                            for (int s = 0; s <= 9; s++) if (_items.Contains(eq0[(EquipmentIndex)s].Item)) { has = true; break; }   // Contains(null) = false
                        }
                        if (!has) continue;
                        var mp = h.PartyBelongedTo;
                        if (mp != null && mp.MapEvent != null) { waiting++; continue; }
                        bool player = h == Hero.MainHero;
                        bool ours = player || (h.Clan != null && h.Clan == Clan.PlayerClan);
                        bool bag = ours && mainGiants && mp != null && mp == main;
                        int took = 0;
                        Equipment tplCiv = null;
                        try { var o = co.OriginalCharacter; tplCiv = o != null && o != co ? o.FirstCivilianEquipment : null; } catch { tplCiv = null; }
                        for (int k = 0; k < 3; k++)
                        {
                            var eq = SetOf(h, k);
                            if (eq == null) continue;
                            for (int s = 0; s <= 9; s++)
                            {
                                var el = eq[(EquipmentIndex)s];
                                var it = el.Item;
                                if (it == null || !_items.Contains(it)) continue;
                                ItemObject repl = null;
                                if (!player && !bag && k < 2)   // 424-p: sztuka idaca do sakw gracza nie ma zamiennika (nic z niczego); zestaw ukrycia - pusty slot
                                {
                                    if (k == 1 && tplCiv != null)
                                    {
                                        var tc = tplCiv[(EquipmentIndex)s].Item;
                                        if (tc != null && !Is(tc) && tc.ItemType == it.ItemType && ItemReq.MeetsHero(co, tc)) repl = tc;
                                    }
                                    if (repl == null)
                                    {
                                        if (s >= 5) repl = SkillsDecide.TopArmor(it.ItemType, co.GetSkillValue(DefaultSkills.Athletics), co.Culture);
                                        else if (s < 4)
                                        {
                                            var sk = ItemReq.SkillFor(it);
                                            repl = SkillsDecide.PatternFor(it.ItemType, sk != null ? co.GetSkillValue(sk) : 0, co);
                                        }
                                        if (repl != null && (Is(repl) || !ItemReq.MeetsHero(co, repl)
                                                             || (k == 1 && (repl.ItemFlags & ItemFlags.Civilian) == 0))) repl = null;
                                    }
                                }
                                eq[(EquipmentIndex)s] = repl != null ? new EquipmentElement(repl) : new EquipmentElement(null);
                                if (bag) { main.ItemRoster.AddToCounts(el, 1); toBags++; }
                                value += it.Value;
                                took++;
                                if (names.Length < 600)
                                    names.Append(names.Length > 0 ? "; " : "").Append(h.StringId).Append(SetName(k)).Append(": ").Append(it.StringId)
                                         .Append(" -> ").Append(repl != null ? repl.StringId : "pusty slot").Append(bag ? " [do sakw gracza]" : "");
                            }
                        }
                        if (took > 0) { heroes++; pieces += took; if (ours) mine += took; }
                    }
                    catch (Exception e) { Stumble("GiantGear.Sweep (bohater)", e, ref stumbles); }
                }
            }
            catch (Exception e) { Stumble("GiantGear.Sweep (lista bohaterow)", e, ref stumbles); }
            // 2. polki osad
            long shelfValue = 0;
            try
            {
                foreach (var st in Settlement.All)
                {
                    try
                    {
                        var r = st != null ? st.ItemRoster : null;
                        if (r == null || r.Count == 0) continue;
                        int n = Purge(r, ref shelfValue);
                        if (n > 0) { shelfN += n; shelfAt++; }
                    }
                    catch (Exception e) { Stumble("GiantGear.Sweep (polka osady)", e, ref stumbles); }
                }
            }
            catch (Exception e) { Stumble("GiantGear.Sweep (lista osad)", e, ref stumbles); }
            // 3. sakwy partii AI i karawan
            long bagValue = 0;
            try
            {
                foreach (var mp in MobileParty.All)
                {
                    try
                    {
                        if (mp == null || mp == main || mp.MapEvent != null) continue;
                        var r = mp.ItemRoster;
                        if (r == null || r.Count == 0 || !HasAny(r) || PartyHasGiants(mp)) continue;
                        int n = Purge(r, ref bagValue);
                        if (n > 0) { bagN += n; bagAt++; }
                    }
                    catch (Exception e) { Stumble("GiantGear.Sweep (sakwy partii)", e, ref stumbles); }
                }
            }
            catch (Exception e) { Stumble("GiantGear.Sweep (lista partii)", e, ref stumbles); }

            // 4. 424-p: nagroda giant_* juz wylosowana w trwajacym turnieju (tylko po wczytaniu - nowe losowania ida przez odsiane listy i PrizePostfix)
            if (afterLoad) rerolled = RerollPrizes(ref stumbles);

            int removed = pieces + shelfN + bagN;
            _sumPieces += removed; _sumValue += value + shelfValue + bagValue;
            bool counters = ScreenRefused != _lastScreen || SieveRefused != _lastSieve || SpawnHero != _lastSpawn || PrizeSifted != _lastPrize
                            || PickupRefused != _lastPickup || MagHidden != _lastMag;
            if (afterLoad || removed > 0 || stumbles > 0 || counters)
            {
                _lastScreen = ScreenRefused; _lastSieve = SieveRefused; _lastSpawn = SpawnHero; _lastPrize = PrizeSifted; _lastPickup = PickupRefused; _lastMag = MagHidden;
                Log.Info("GiantGear (424): " + (afterLoad ? "po wczytaniu" : "doba") + " | rasa giant nr " + _giantRace
                         + " | bohaterowie " + heroes + ", sztuk " + pieces + " (rod gracza " + mine + ", do sakw gracza " + toBags + ", wartosc " + value + ")"
                         + (names.Length > 0 ? " [" + names + "]" : "")
                         + " | w bitwie - czekaja " + waiting
                         + " | polki " + shelfN + " szt. w " + shelfAt + " osadach (wartosc " + shelfValue + ")"
                         + " | sakwy partii AI " + bagN + " szt. w " + bagAt + " partiach (wartosc " + bagValue + ")"
                         + " | od startu sesji zdjeto " + _sumPieces + " szt. (wartosc " + _sumValue + ")"
                         + " | odmowy: ekran " + ScreenRefused + ", sito bohaterow " + SieveRefused + ", spawn bohatera " + SpawnHero + ", nagrody turniejow " + PrizeSifted
                         + ", podniesienie z ziemi (zapytania) " + PickupRefused + ", Mag's Secret ukryty (zapytania) " + MagHidden
                         + (afterLoad ? " | nagrody turniejow losowane od nowa " + rerolled : "")
                         + " | potkniecia " + stumbles + ".");
            }
            if (mine > 0)
                Log.Player("Giant's gear is for giants alone - " + mine + (mine == 1 ? " piece was" : " pieces were") + " taken off your people."
                           + (toBags > 0 ? " It lies in your baggage for your giants." : ""), true);
        }
    }

    /// <summary>424: nasluchy kampanii reguly "sprzet olbrzymow tylko dla olbrzymow" (GiantGear) - numer rasy przy starcie sesji i porzadek na stanie raz
    /// na dobe. 424-p: porzadek PO WCZYTANIU wola ArmouryBehavior (GiantGear.AfterLoad w nasluchu OnAfterSessionLaunched po ArmsPricing.RefreshAfterFinalStats -
    /// po ROTRaceMap, po drabinie 423 i lukach 420): MbEvent wola od ostatnio dopisanego, wiec wlasny nasluch tej klasy biegl PRZED 423 i grzal pamiec wzorcow
    /// SkillsDecide (TopArmorBandCache, Pattern) na tierach sprzed drabiny - na cala sesje, takze dla spawnu. Bez stanu w zapisie.</summary>
    internal sealed class GiantLaw424 : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => { try { GiantGear.SessionStart(); } catch (Exception e) { Log.Error("GiantLaw424.SessionStart", e); } });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, () => { try { GiantGear.Sweep(false); } catch (Exception e) { Log.Error("GiantLaw424.Sweep (doba)", e); } });
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
