using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// m109-lup (23.10, Jeff 22.10 pkt 8: "pokonalem robber squires, bylo dwoch konnych - nie dostalem za nich koni ani ich pancerzy koni, ani zbroi") -
    /// POMIAR LUPU BITWY GRACZA wedlug rodzaju (kon, uprzaz, tulow, glowa, rece, nogi, peleryna, bron, pociski, tarcza, inne). Tylko czyta - zachowanie
    /// zmieniaja klucze w BattlefieldLaw (FallenRiderHorsesLooted, RiderBlowSparesHarness, ExtraBrokenArmorWrecks) i HorseFate (TakeKill).
    /// Dowod (dekompilacja DTE v1.4.7 DynamicTroopMissionLogic.OnAgentRemoved :329-397): z ciala powalonego DTE dopisuje kazda sztuke (sloty 0-3, 5-11
    /// bez choragwi) do ItemsToRecover partii ofiary i (DropRate) do LootedItems partii zabojcy - bez czesci rozbitej ciosem (ArmorSelector), bez
    /// "dodatkowo peknietej" (extraBrokenArmor, wrogowie od tieru 3: 25/35/45/60%), bez sztuk zastepczych (Assignment.IsTemporarySlot), czarnej listy,
    /// pustych kolczanow i rozbitych tarcz. Prefiks i postfiks tej metody licza roznice licznikow obu rekordow (najwyzej 22 odczyty slownika na powalonego),
    /// reszta sztuk dostaje przyczyne: zastepcza (slot tymczasowy DTE), cios (wlasny odczyt wyniku ArmorSelector), czarna lista, pekniete (jedna brakujaca
    /// czesc zbroi ze slotu 5-9 u wroga od tieru 3; wiecej niz jedna - niejednoznaczne, bez wraka), inne.
    /// Bitwa: liczniki od pierwszego zdarzenia misji albo kolejki lupu (BattlefieldLaw.ShareQueue), zamkniecie przy liscie trofeow Spoils, sakwach,
    /// zwrocie do zbrojowni wojska, ekranie gry albo przy nastepnej bitwie (bez rozliczenia). Linia bitwy z tozsamosciami (sprawdza je grupa m109lup):
    /// A z cial: na cialach = lup + cios + pekniete + zastepcze + odrzut + inne; B kolejka: wczesniej + dzialka + bagaz + send - padle = lista + sakwy
    /// + zwrot + przepadlo + zostaje; C lista trofeow: na liscie + Spoils wyjal + rezerwa = z kolejki + wraki - przesiane, a przesiane = smoki + legendy
    /// + olbrzymy-egzotyki + zniszczone + wraki ponizej progu (osobny blok linii - recenzja L4).
    /// Hak autotestu (plik CrashScribe/armoury-lup.flag, tryb autotestu LogGate 1/2): wrog bitwy testu (autotest_bitwa_*) w planie mocna dostaje 6 jezdzcow
    /// z koniem, uprzeza i zbroja (sam roster - DTE ubiera bandytow wzorcem jednostki) - w grze Jeffa nic nie robi.
    /// Stan statyczny bez zapisu (Reset z BattlefieldLaw.LReset). Wyjatki liczone (pierwsze 3 i co 50 do Log.Error), bez globalnego wylacznika.
    /// </summary>
    internal static class LootTally
    {
        private const string P = "Lup bitwy (m109): ";
        internal const string AutoFlagName = "armoury-lup.flag";
        private const int RidersPerBattle = 8;     // szczegoly jezdzcow w linii bitwy
        private const int RiderLinesSession = 3;   // linia jezdzcow: pierwsze 3 bitwy sesji (pod flaga testu kazda)
        private const int TestRidersCount = 6;     // jezdzcy dokladani wrogowi bitwy testu

        // ------------------------------------------------------------ rodzaje
        internal const int Kon = 0, Uprzaz = 1, Tulow = 2, Glowa = 3, Rece = 4, Nogi = 5, Peleryna = 6, Bron = 7, Pociski = 8, Tarcza = 9, Inne = 10, Kinds = 11;
        private static readonly string[] KindName = { "kon", "uprzaz", "tulow", "glowa", "rece", "nogi", "peleryna", "bron", "pociski", "tarcza", "inne" };

        // blok A (z cial powalonych przez druzyne gracza)
        private const int ANa = 0, ALup = 1, ACios = 2, APek = 3, AZast = 4, AOdrz = 5, AInne = 6, ACols = 7;
        // blok B (kolejka lupu BattlefieldLaw.ShareQueue)
        internal const int BWcz = 0, Dzialka = 1, Bagaz = 2, Send = 3, BPadle = 4, Lista = 5, Sakwy = 6, Zwrot = 7, Przepadlo = 8, BZost = 9, BCols = 10;
        // blok C (lista trofeow Spoils)
        private const int CKol = 0, CWraki = 1, CPrzes = 2, CWyjal = 3, CTrzym = 4, CWroc = 5, CRez = 6, CNa = 7, CCols = 8;
        // przesiane z listy trofeow wedlug przyczyny (recenzja L4; suma = "przesiane" bloku C): smoki (CleanseDragons), legendy (LegendaryLaw), olbrzymy
        // i egzotyki (sprzet olbrzymow, wierzchowiec egzotyczny nie dla partii gracza, slonie - CleanseTrash), zniszczone (stan <= LootMinConditionPercent),
        // wraki ponizej progu zniszczenia (AppendWrecks)
        internal const int SSmok = 0, SLeg = 1, SEgz = 2, SZnisz = 3, SWrak = 4, SCols = 5;

        internal static int KindOf(ItemObject it)
        {
            if (it == null) return Inne;
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.Horse: return Kon;
                case ItemObject.ItemTypeEnum.HorseHarness: return Uprzaz;
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.ChestArmor: return Tulow;
                case ItemObject.ItemTypeEnum.HeadArmor: return Glowa;
                case ItemObject.ItemTypeEnum.HandArmor: return Rece;
                case ItemObject.ItemTypeEnum.LegArmor: return Nogi;
                case ItemObject.ItemTypeEnum.Cape: return Peleryna;
                case ItemObject.ItemTypeEnum.Shield: return Tarcza;
                case ItemObject.ItemTypeEnum.Arrows:
                case ItemObject.ItemTypeEnum.Bolts:
                case ItemObject.ItemTypeEnum.SlingStones:
                case ItemObject.ItemTypeEnum.Bullets: return Pociski;
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm:
                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow:
                case ItemObject.ItemTypeEnum.Sling:
                case ItemObject.ItemTypeEnum.Thrown:
                case ItemObject.ItemTypeEnum.Pistol:
                case ItemObject.ItemTypeEnum.Musket: return Bron;
                default: return Inne;
            }
        }

        // ------------------------------------------------------------ bitwa
        private sealed class Rider
        {
            public Agent Agent;
            public string Head;                     // "id (Ty, zabity)"
            public string HorsePart;                // "kon X lup"
            public readonly List<string> Parts = new List<string>();
            public int Dead = -1;                   // padly kon: 0 z kolejki, 1 ze zbrojowni, 2 poza lupem (nic do zdjecia), 3 brak
        }

        private sealed class Battle
        {
            public int N;
            public WeakReference MissionRef, EventRef;
            public bool Mission, Sim, HasC;
            public readonly long[,] A = new long[Kinds, ACols];
            public readonly long[,] B = new long[Kinds, BCols];
            public readonly long[,] C = new long[Kinds, CCols];
            public readonly long[,] S = new long[Kinds, SCols];  // przesiane wedlug przyczyny (recenzja L4)
            public readonly long[] SimGain = new long[Kinds];
            public readonly long[] CBefore = new long[Kinds];   // z kolejki + wraki przed przesianiem (do przesianych)
            public int FelledHero, FelledTroop, Riders, HeroesSkipped;
            public int DeadQ, DeadArm, DeadOut, DeadNone;
            public int WCios, WCiosHarness, PekOne, PekWreck, Ambig;
            public long Ticks;
            public int Calls;                       // recenzja R2: wywolania pomiaru w misji (prefiks i postfiks OnAgentRemoved) - prog czasu od liczby
            public int Stumbles;
            public readonly List<Rider> RiderList = new List<Rider>();
            public readonly Dictionary<Agent, bool> HorseLoot = new Dictionary<Agent, bool>();   // jezdziec -> czy jego kon wszedl do lupu druzyny gracza
        }

        private static Battle _cur;
        private static bool _sifting;          // recenzja L4: AfterGenerateLoot miedzy WrecksIn a Sifted - przyczyny przesiania listy trofeow liczone
        private static int _battleN;           // numer bitwy w sesji (proces)
        private static int _riderLines;        // linie jezdzcow w sesji

        // ------------------------------------------------------------ doba
        private static readonly long[,] DayA = new long[Kinds, ACols], DayB = new long[Kinds, BCols], DayC = new long[Kinds, CCols];
        private static int _dBattles, _dList, _dBag, _dBack, _dOther, _dDeadQ, _dDeadArm, _dDeadOut, _dDeadNone, _dPekOne, _dPekWreck, _dAmbig, _dStumbles;
        private static long _dTicks;
        private static int _stumbles;          // sesja

        // ------------------------------------------------------------ odczyty DTE (refleksja)
        private static FieldInfo _fRecords;    // DynamicTroopMissionLogic._partyBattleRecords (ConcurrentDictionary<MBGUID, PartyBattleRecord>)
        private static FieldInfo _fRecover;    // PartyBattleRecord.ItemsToRecover
        private static FieldInfo _fLooted;     // PartyBattleRecord.LootedItems
        private static FieldInfo _fAssign;     // DynamicTroopMissionLogic._assignmentByAgent (Dictionary<Agent, Assignment>)
        private static FieldInfo _fTemp;       // Assignment._temporaryItemIdBySlot (Dictionary<EquipmentIndex, string>)
        private static FieldInfo _fProcessed;  // DynamicTroopMissionLogic._processedAgents (HashSet<Agent>)
        private static bool _blFound;          // DTE ItemBlackList.Test (test robi AiGear.DteTakes)
        [ThreadStatic] private static ItemObject _pick;   // czesc rozbita ciosem (wynik ArmorSelector) w biegu OnAgentRemoved

        // autotest
        private static bool _atLooked;
        private static FieldInfo _fKind;       // CrashScribe.AutotestBattle._kind
        private static readonly HashSet<string> _testDone = new HashSet<string>();

        // ------------------------------------------------------------ latki
        internal static void ApplyAll(Harmony h)
        {
            string wRemoved = "BRAK", wPick = "BRAK", wTest = "BRAK";
            try
            {
                var tLogic = AccessTools.TypeByName("DynamicTroopEquipmentReupload.DynamicTroopMissionLogic");
                var tRec = AccessTools.TypeByName("DynamicTroopEquipmentReupload.PartyBattleRecord");
                var tAsg = AccessTools.TypeByName("DynamicTroopEquipmentReupload.Assignment");
                var tSel = AccessTools.TypeByName("DynamicTroopEquipmentReupload.ArmorSelector");
                var tBl = AccessTools.TypeByName("DynamicTroopEquipmentReupload.ItemBlackList");
                if (tLogic != null)
                {
                    _fRecords = AccessTools.Field(tLogic, "_partyBattleRecords");
                    _fAssign = AccessTools.Field(tLogic, "_assignmentByAgent");
                    _fProcessed = AccessTools.Field(tLogic, "_processedAgents");
                }
                if (tRec != null)
                {
                    _fRecover = AccessTools.Field(tRec, "ItemsToRecover");
                    _fLooted = AccessTools.Field(tRec, "LootedItems");
                }
                if (tAsg != null) _fTemp = AccessTools.Field(tAsg, "_temporaryItemIdBySlot");
                _blFound = tBl != null && AccessTools.Method(tBl, "Test", new[] { typeof(ItemObject) }) != null;
                try
                {
                    var rem = tLogic != null ? AccessTools.DeclaredMethod(tLogic, "OnAgentRemoved", new[] { typeof(Agent), typeof(Agent), typeof(AgentState), typeof(KillingBlow) }) : null;
                    if (rem != null && RecordsOk)
                    {
                        h.Patch(rem, prefix: new HarmonyMethod(typeof(LootTally), nameof(FelledPre)), postfix: new HarmonyMethod(typeof(LootTally), nameof(FelledPost)));
                        wRemoved = "TAK";
                    }
                }
                catch (Exception e) { wRemoved = "BLAD"; Log.Error("LootTally.ApplyAll(OnAgentRemoved)", e); }
                try
                {
                    var pick = tSel != null ? AccessTools.DeclaredMethod(tSel, "GetRandomArmorByBodyPart") : null;
                    if (pick != null)
                    {
                        h.Patch(pick, postfix: new HarmonyMethod(typeof(LootTally), nameof(PickPost)));
                        wPick = BattlefieldLaw.SpareHarnessWired ? "TAK" : "pomiar TAK, uprzaz BRAK";
                    }
                }
                catch (Exception e) { wPick = "BLAD"; Log.Error("LootTally.ApplyAll(GetRandomArmorByBodyPart)", e); }
                try
                {
                    var enc = AccessTools.DeclaredMethod(typeof(EncounterManager), "StartPartyEncounter", new[] { typeof(PartyBase), typeof(PartyBase) });
                    if (enc != null)
                    {
                        h.Patch(enc, prefix: new HarmonyMethod(typeof(LootTally), nameof(TestRiders)));
                        wTest = "TAK";
                    }
                }
                catch (Exception e) { wTest = "BLAD"; Log.Error("LootTally.ApplyAll(StartPartyEncounter)", e); }
            }
            catch (Exception e) { Log.Error("LootTally.ApplyAll", e); }
            try
            {
                var s = Settings.Current;
                string wRet = BattlefieldLaw.MountReturnWired ? "TAK" : "BRAK", wFin = BattlefieldLaw.MountFinalWired ? "TAK" : "BRAK";
                int ok = (wRet == "TAK" ? 1 : 0) + (wFin == "TAK" ? 1 : 0) + (wRemoved == "TAK" ? 1 : 0) + (wPick == "TAK" ? 1 : 0) + (wTest == "TAK" ? 1 : 0);
                Log.Info(P + "start | klucze FallenRiderHorsesLooted " + YN(s != null && s.FallenRiderHorsesLooted) + ", RiderBlowSparesHarness " + YN(s != null && s.RiderBlowSparesHarness)
                         + ", ExtraBrokenArmorWrecks " + YN(s != null && s.ExtraBrokenArmorWrecks) + " (WreckSalvageEnabled " + YN(s != null && s.WreckSalvageEnabled)
                         + ", HorsesDieOnlyKilled " + YN(s != null && s.HorsesDieOnlyKilled) + ") | latki " + ok + "/5: Spoils ApplyDamageToLoot " + wRet
                         + ", Spoils OnMapEventEnded (rezerwa) " + wFin + ", DTE OnAgentRemoved (pomiar) " + wRemoved + ", DTE GetRandomArmorByBodyPart (uprzaz, pomiar) " + wPick
                         + ", EncounterManager.StartPartyEncounter (test) " + wTest + " | odczyt: rekordy DTE " + YN(RecordsOk) + ", zastepcze sloty " + YN(_fAssign != null && _fTemp != null)
                         + ", czarna lista " + YN(_blFound) + ", Spoils RegisterLuckyElement " + YN(BattlefieldLaw.LuckyFound) + ".");
            }
            catch (Exception e) { Log.Error("LootTally.ApplyAll(linia)", e); }
        }

        private static bool RecordsOk { get { return _fRecords != null && _fRecover != null && _fLooted != null && _fProcessed != null; } }

        private static string YN(bool b) { return b ? "TAK" : "NIE"; }

        // ------------------------------------------------------------ potkniecia i czas
        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            if (_cur != null) _cur.Stumbles++;
            if (_stumbles <= 3 || _stumbles % 50 == 0) Log.Error("LootTally." + where, e);
        }

        private static void Lap(long t0)
        {
            long d = Stopwatch.GetTimestamp() - t0;
            if (_cur != null) _cur.Calls++;
            if (d <= 0) return;
            _dTicks += d;
            if (_cur != null) _cur.Ticks += d;
        }

        private static string Ms(long ticks)
        {
            return (ticks * 1000.0 / Stopwatch.Frequency).ToString("0.0", CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------ otwarcie i zamkniecie bitwy
        private static MapEvent EventNow()
        {
            try { return MapEvent.PlayerMapEvent ?? PlayerEncounter.Battle; } catch { return null; }
        }

        private static object Target(WeakReference w)
        {
            try { return w != null ? w.Target : null; } catch { return null; }
        }

        /// <summary>Biezaca bitwa - otwiera nowa przy pierwszym zdarzeniu; inna misja albo inna bitwa na mapie zamyka poprzednia (bez rozliczenia).</summary>
        private static Battle Begin(Mission m)
        {
            var me = EventNow();
            if (_cur != null)
            {
                object cm = Target(_cur.MissionRef), ce = Target(_cur.EventRef);
                bool otherMission = m != null && !ReferenceEquals(cm, m) && (cm != null || _cur.Mission);
                bool otherEvent = me != null && ce != null && !ReferenceEquals(ce, me);
                if (otherMission || otherEvent) Close("bez rozliczenia - nastepna bitwa");
            }
            if (_cur == null)
            {
                var b = new Battle { N = ++_battleN };
                if (m != null) { b.MissionRef = new WeakReference(m); b.Mission = true; }
                if (me != null) b.EventRef = new WeakReference(me);
                var q = BattlefieldLaw.QueueRoster;   // dzialka poprzedniej bitwy jeszcze w kolejce (sakwy po godzinie) - wejscie "wczesniej"
                if (q != null)
                    for (int i = 0; i < q.Count; i++)
                    {
                        var el = q.GetElementCopyAtIndex(i);
                        if (el.EquipmentElement.Item != null && el.Amount > 0) b.B[KindOf(el.EquipmentElement.Item), BWcz] += el.Amount;
                    }
                _cur = b;
            }
            else
            {
                if (m != null && _cur.MissionRef == null) { _cur.MissionRef = new WeakReference(m); _cur.Mission = true; }
                if (me != null && _cur.EventRef == null) _cur.EventRef = new WeakReference(me);
            }
            return _cur;
        }

        /// <summary>Zamyka biezaca bitwe: linia bitwy (zawsze), linia jezdzcow (pierwsze 3 bitwy sesji, pod flaga testu kazda), liczniki doby.</summary>
        internal static void Close(string how)
        {
            _sifting = false;
            var b = _cur;
            if (b == null) return;
            _cur = null;
            try
            {
                var q = BattlefieldLaw.QueueRoster;   // co zostalo w kolejce (odbierze ja nastepna bitwa albo sakwy)
                if (q != null)
                    for (int i = 0; i < q.Count; i++)
                    {
                        var el = q.GetElementCopyAtIndex(i);
                        if (el.EquipmentElement.Item != null && el.Amount > 0) b.B[KindOf(el.EquipmentElement.Item), BZost] += el.Amount;
                    }
                var s = Settings.Current;
                var sb = new StringBuilder(P);
                sb.Append("bitwa ").Append(b.N).Append(" | misja ").Append(YN(b.Mission)).Append(", Send troops ").Append(YN(b.Sim))
                  .Append(", klucze konie ").Append(YN(s != null && s.FallenRiderHorsesLooted)).Append("/uprzaz ").Append(YN(s != null && s.RiderBlowSparesHarness))
                  .Append("/pekniete ").Append(YN(s != null && s.ExtraBrokenArmorWrecks)).Append(" (wraki ").Append(YN(s != null && s.WreckSalvageEnabled))
                  .Append(", konie gina zabite ").Append(YN(s != null && s.HorsesDieOnlyKilled)).Append(')')
                  .Append(" | powaleni przez druzyne ").Append(b.FelledHero + b.FelledTroop).Append(" (Ty i towarzysze ").Append(b.FelledHero).Append(", zolnierze ").Append(b.FelledTroop)
                  .Append("), konnych ").Append(b.Riders).Append(", bohaterow pominietych ").Append(b.HeroesSkipped)
                  .Append(" | z cial [na cialach/lup/cios/pekniete/zastepcze/odrzut/inne]: ").Append(Block(b.A, ACols))
                  .Append(" | kolejka [wczesniej/dzialka/bagaz/send/padle/lista/sakwy/zwrot/przepadlo/zostaje]: ").Append(Block(b.B, BCols))
                  .Append(" | lista trofeow [z kolejki/wraki/przesiane/Spoils wyjal/przetrzymane/wrocone/rezerwa/na liscie]: ").Append(b.HasC ? Block(b.C, CCols) : "nie bylo")
                  .Append(" | przesiane [smoki/legendy/olbrzymy-egzotyki/zniszczone/wraki ponizej progu]: ").Append(Block(b.S, SCols));
                if (b.Sim) sb.Append(" | Send troops - lup DTE z poleglych do zbrojowni wojska: ").Append(Row(b.SimGain));
                sb.Append(" | konie padle pod powalonymi: z kolejki ").Append(b.DeadQ).Append(", ze zbrojowni ").Append(b.DeadArm).Append(", poza lupem ").Append(b.DeadOut).Append(", brak ").Append(b.DeadNone)
                  .Append(" | wraki: cios ").Append(b.WCios).Append(" (w uprzaz ").Append(b.WCiosHarness).Append("), pekniete dodatkowo ").Append(b.PekOne)
                  .Append(" -> wraki ").Append(b.PekWreck).Append(", niejednoznaczne ").Append(b.Ambig)
                  .Append(" | koniec: ").Append(how).Append(" | pomiar ").Append(Ms(b.Ticks)).Append(" ms (wywolan ").Append(b.Calls).Append(") | potkniecia ").Append(b.Stumbles).Append('.');
                Log.Info(sb.ToString());
                if (b.RiderList.Count > 0 && (_riderLines < RiderLinesSession || TestWanted()))
                {
                    _riderLines++;
                    var rb = new StringBuilder(P).Append("jezdzcy bitwy ").Append(b.N).Append(" - ");
                    for (int i = 0; i < b.RiderList.Count; i++)
                    {
                        var r = b.RiderList[i];
                        if (i > 0) rb.Append(" | ");
                        rb.Append(r.Head).Append(": ").Append(r.HorsePart ?? "kon -");
                        if (r.Dead >= 0) rb.Append(" (padl - ").Append(r.Dead == 0 ? "z kolejki" : r.Dead == 1 ? "ze zbrojowni" : r.Dead == 2 ? "nie w lupie" : "brak w kolejce i zbrojowni").Append(')');
                        foreach (var p in r.Parts) rb.Append(", ").Append(p);
                    }
                    if (b.Riders > b.RiderList.Count) rb.Append(" | pozostalych jezdzcow ").Append(b.Riders - b.RiderList.Count);
                    rb.Append('.');
                    Log.Info(rb.ToString());
                }
                // doba
                _dBattles++;
                if (how.StartsWith("lista trofeow")) { _dList++; _lastListN = b.N; _lastListW = b.C[Bron, CNa]; }   // m114-lup: do linii ekranu trofeow
                else if (how.StartsWith("sakwy")) _dBag++;
                else if (how.StartsWith("zwrot")) _dBack++;
                else _dOther++;
                Add(DayA, b.A, ACols); Add(DayB, b.B, BCols); Add(DayC, b.C, CCols);
                _dDeadQ += b.DeadQ; _dDeadArm += b.DeadArm; _dDeadOut += b.DeadOut; _dDeadNone += b.DeadNone;
                _dPekOne += b.PekOne; _dPekWreck += b.PekWreck; _dAmbig += b.Ambig;
            }
            catch (Exception e) { Stumble("Close", e); }
            foreach (var r in b.RiderList) r.Agent = null;
            b.HorseLoot.Clear();
        }

        /// <summary>HorseFate.TakeKill: czy kon jezdzca powalonego przez druzyne gracza wszedl do jej lupu (1), nie wszedl (0 - nie ma czego zdejmowac,
        /// zdjecie trafiloby w konie gracza), nie wiadomo (-1 - pomiar nie widzial jezdzca).</summary>
        internal static int HorseLootState(Agent rider)
        {
            try
            {
                bool v;
                var b = _cur;
                if (b == null || rider == null || !b.HorseLoot.TryGetValue(rider, out v)) return -1;
                return v ? 1 : 0;
            }
            catch { return -1; }
        }

        /// <summary>Recenzja R1 (HorseFate.TakeKill): bitwa otwarta PRZED zdjeciem padlego konia z kolejki - otwarcie liczy kolejke jako "wczesniej",
        /// wiec po zdjeciu bylaby liczona o 1 za malo (tozsamosc B -1).</summary>
        internal static void Touch()
        {
            try { if (_cur == null) Begin(Mission.Current); }
            catch (Exception e) { Stumble("Touch", e); }
        }

        /// <summary>Recenzja R4 (HorseFate.ApplyMission, finally): agenci skonczonej misji puszczeni - HorseLoot i jezdzcy sa potrzebni tylko w TakeKill;
        /// dotad trzymane do zamkniecia bitwy (wygrana bez listy trofeow i kolejki - az do nastepnej bitwy), a agent trzyma misje.</summary>
        internal static void ReleaseAgents()
        {
            try
            {
                var b = _cur;
                if (b == null) return;
                b.HorseLoot.Clear();
                foreach (var r in b.RiderList) r.Agent = null;
            }
            catch (Exception e) { Stumble("ReleaseAgents", e); }
        }

        private static void Add(long[,] dst, long[,] src, int cols)
        {
            for (int k = 0; k < Kinds; k++) for (int c = 0; c < cols; c++) dst[k, c] += src[k, c];
        }

        /// <summary>Blok linii: tylko niezerowe rodzaje w stalej kolejnosci ("kon 2/2/0/0/0/0/0, uprzaz ..."), bez nich "brak".</summary>
        private static string Block(long[,] a, int cols)
        {
            var sb = new StringBuilder();
            for (int k = 0; k < Kinds; k++)
            {
                bool any = false;
                for (int c = 0; c < cols; c++) if (a[k, c] != 0) { any = true; break; }
                if (!any) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(KindName[k]).Append(' ');
                for (int c = 0; c < cols; c++) { if (c > 0) sb.Append('/'); sb.Append(a[k, c]); }
            }
            return sb.Length > 0 ? sb.ToString() : "brak";
        }

        private static string Row(long[] a)
        {
            var sb = new StringBuilder();
            for (int k = 0; k < Kinds; k++)
            {
                if (a[k] == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(KindName[k]).Append(' ').Append(a[k]);
            }
            return sb.Length > 0 ? sb.ToString() : "brak";
        }

        // ------------------------------------------------------------ kolejka i lista trofeow (wolane z BattlefieldLaw i HorseFate)

        /// <summary>Sztuki weszly do kolejki lupu (Dzialka - AfterDtePartyItems, Bagaz - AfterCollectLoot, Send - QueueSimShare) albo z niej zeszly (BPadle).</summary>
        internal static void Queued(ItemObject it, int n, int col)
        {
            if (it == null || n <= 0 || col < 0 || col >= BCols) return;
            try { var b = Begin(Mission.Current); b.B[KindOf(it), col] += n; if (col == Send) b.Sim = true; }
            catch (Exception e) { Stumble("Queued", e); }
        }

        /// <summary>Kolejka oddana albo wyczyszczona (Lista, Sakwy, Zwrot, Przepadlo) - wolane PRZED oproznieniem; bez otwartej bitwy nic (sztuki z bitwy
        /// juz zamknietej sa w jej "zostaje").</summary>
        internal static void QueueOut(ItemRoster q, int col)
        {
            var b = _cur;
            if (b == null || q == null || col < 0 || col >= BCols) return;
            try
            {
                for (int i = 0; i < q.Count; i++)
                {
                    var el = q.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item != null && el.Amount > 0) b.B[KindOf(el.EquipmentElement.Item), col] += el.Amount;
                }
            }
            catch (Exception e) { Stumble("QueueOut", e); }
        }

        /// <summary>"Send troops": przybytek zbrojowni wojska z lupu DTE (przedmiot -> ile) - wedlug rodzaju do linii bitwy.</summary>
        internal static void SimBody(Dictionary<ItemObject, int> gained)
        {
            if (gained == null) return;
            try
            {
                var b = Begin(Mission.Current);
                b.Sim = true;
                foreach (var kv in gained) if (kv.Key != null && kv.Value > 0) b.SimGain[KindOf(kv.Key)] += kv.Value;
            }
            catch (Exception e) { Stumble("SimBody", e); }
        }

        /// <summary>AfterGenerateLoot: sztuka z kolejki na liste trofeow (B lista, C z kolejki).</summary>
        internal static void ToList(ItemObject it, int n)
        {
            if (it == null || n <= 0) return;
            try { var b = Begin(Mission.Current); int k = KindOf(it); b.B[k, Lista] += n; b.C[k, CKol] += n; b.CBefore[k] += n; b.HasC = true; }
            catch (Exception e) { Stumble("ToList", e); }
        }

        /// <summary>AfterGenerateLoot: wraki dosypywane do listy (przed przesianiem; wraki ponizej progu zniszczenia licza sie potem jako przesiane).
        /// Recenzja L4: od teraz do Sifted przyczyny przesiania tej listy liczone (SiftOut).</summary>
        internal static void WrecksIn(List<ItemObject> wrecks)
        {
            _sifting = true;
            if (wrecks == null || wrecks.Count == 0) return;
            try
            {
                var b = Begin(Mission.Current);
                b.HasC = true;
                foreach (var it in wrecks) { if (it == null) continue; int k = KindOf(it); b.C[k, CWraki]++; b.CBefore[k]++; }
            }
            catch (Exception e) { Stumble("WrecksIn", e); }
        }

        /// <summary>AfterGenerateLoot po przesianiu (smoki, smiec, legendy, wraki ponizej progu): przesiane = przed - jest; potem konie i uprzaz:
        /// holding - przetrzymane poza Spoils (BattlefieldLaw.HoldMounts, liczy Held), inaczej Spoils je wyjmie (Spoils wyjal); reszta na liscie.</summary>
        internal static void Sifted(ItemRoster list)
        {
            _sifting = false;
            var b = _cur;
            if (b == null || list == null) return;
            try
            {
                var now = new long[Kinds];
                for (int i = 0; i < list.Count; i++)
                {
                    var el = list.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item != null && el.Amount > 0) now[KindOf(el.EquipmentElement.Item)] += el.Amount;
                }
                for (int k = 0; k < Kinds; k++) b.C[k, CPrzes] += Math.Max(0, b.CBefore[k] - now[k]);
                Array.Clear(b.CBefore, 0, Kinds);
            }
            catch (Exception e) { Stumble("Sifted", e); }
        }

        /// <summary>Recenzja L4: sztuka zdjeta z listy trofeow przy przesiewaniu (AppendWrecks, CleanseDragons, CleanseTrash) z przyczyna (SSmok..SWrak) -
        /// tylko miedzy WrecksIn a Sifted w AfterGenerateLoot (te same funkcje na ekranie gry i w sakwach nie licza sie).</summary>
        internal static void SiftOut(ItemObject it, int n, int why)
        {
            if (!_sifting || it == null || n <= 0 || why < 0 || why >= SCols) return;
            var b = _cur;
            if (b == null) return;
            try { b.S[KindOf(it), why] += n; } catch (Exception e) { Stumble("SiftOut", e); }
        }

        /// <summary>AfterGenerateLoot na koncu: co zostaje na liscie (bez kluczy - konie i uprzaz liczone jako "Spoils wyjal": Spoils zabierze je do taboru,
        /// ktory u nas jest zamkniety).</summary>
        internal static void OnList(ItemRoster list, bool holding)
        {
            var b = _cur;
            if (b == null || list == null) return;
            try
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var el = list.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0) continue;
                    bool mount = it.IsMountable || it.ItemType == ItemObject.ItemTypeEnum.HorseHarness;
                    b.C[KindOf(it), !holding && mount ? CWyjal : CNa] += el.Amount;
                }
            }
            catch (Exception e) { Stumble("OnList", e); }
        }

        internal static void Held(ItemObject it, int n) { Col(it, n, CTrzym); }

        internal static void Returned(ItemObject it, int n) { Col(it, n, CWroc); Col(it, n, CNa); }

        internal static void Reserve(ItemObject it, int n) { Col(it, n, CRez); }

        private static void Col(ItemObject it, int n, int col)
        {
            var b = _cur;
            if (b == null || it == null || n <= 0) return;
            try { b.C[KindOf(it), col] += n; b.HasC = true; } catch (Exception e) { Stumble("Col", e); }
        }

        /// <summary>HorseFate.TakeKill: padly kon jezdzca powalonego przez druzyne gracza zdjety z kolejki (0), ze zbrojowni (1), nie wszedl do lupu - nic
        /// do zdjecia (2) albo wszedl, a nie ma go ani w kolejce, ani w zbrojowni (3).</summary>
        internal static void DeadHorse(Agent rider, int where)
        {
            try
            {
                var b = _cur;
                if (b == null) b = Begin(Mission.Current);
                if (where == 0) { b.DeadQ++; b.B[Kon, BPadle]++; }
                else if (where == 1) b.DeadArm++;
                else if (where == 2) b.DeadOut++;
                else b.DeadNone++;
                if (rider != null)
                    foreach (var r in b.RiderList) if (ReferenceEquals(r.Agent, rider)) { r.Dead = where; break; }
            }
            catch (Exception e) { Stumble("DeadHorse", e); }
        }

        // ------------------------------------------------------------ misja: powaleni przez druzyne gracza
        private sealed class Felled
        {
            public Battle B;
            public int Kind;
            public string Who, State;
            public int Tier;
            public bool Naval, HasIds, ProcessedBefore;
            public MBGUID VictimId, MainId;
            public ItemObject Horse;
            public string Troop;
            public Dictionary<EquipmentIndex, string> Temp;
            public readonly List<int> Slots = new List<int>();
            public readonly List<ItemObject> Items = new List<ItemObject>();
            public readonly Dictionary<ItemObject, int> RecBefore = new Dictionary<ItemObject, int>(), LootBefore = new Dictionary<ItemObject, int>();
        }

        private static readonly int[] BodySlots = { 0, 1, 2, 3, 5, 6, 7, 8, 9, 10, 11 };

        private static MobileParty PartyOf(IAgentOriginBase o)
        {
            PartyBase pb = null;
            var a = o as TaleWorlds.CampaignSystem.AgentOrigins.PartyAgentOrigin;
            if (a != null) pb = a.Party;
            else
            {
                var g = o as TaleWorlds.CampaignSystem.AgentOrigins.PartyGroupAgentOrigin;
                if (g != null) pb = g.Party;
                else { var s = o as TaleWorlds.CampaignSystem.AgentOrigins.SimpleAgentOrigin; if (s != null) pb = s.Party; }
            }
            return pb != null ? pb.MobileParty : null;
        }

        private static ConcurrentDictionary<ItemObject, int> RecDict(object logic, MBGUID id, FieldInfo f)
        {
            var recs = _fRecords.GetValue(logic) as IDictionary;
            if (recs == null) return null;
            var r = recs[id];
            return r != null ? f.GetValue(r) as ConcurrentDictionary<ItemObject, int> : null;
        }

        private static int Count(ConcurrentDictionary<ItemObject, int> d, ItemObject it)
        {
            int v;
            return d != null && it != null && d.TryGetValue(it, out v) ? v : 0;
        }

        private static bool Processed(object logic, Agent a)
        {
            var set = _fProcessed.GetValue(logic) as HashSet<Agent>;
            return set != null && set.Contains(a);
        }

        /// <summary>Postfiks DTE ArmorSelector.GetRandomArmorByBodyPart: czesc rozbita ciosem (wlasny odczyt - bez zaleznosci od kolejnosci z QueueWreck).</summary>
        public static void PickPost(ItemObject __result) { _pick = __result; }

        /// <summary>Prefiks DTE DynamicTroopMissionLogic.OnAgentRemoved: wrog (nie bohater) zabity albo ogluszony przez gracza, towarzysza albo zolnierza
        /// druzyny gracza - sztuki z ciala i liczniki rekordow DTE przed rozliczeniem. Tylko czyta; nie rzuca.</summary>
        public static void FelledPre(object __instance, Agent affectedAgent, Agent affectorAgent, AgentState agentState, out object __state)
        {
            __state = null;
            _pick = null;
            if (agentState != AgentState.Killed && agentState != AgentState.Unconscious) return;
            if (affectedAgent == null || !affectedAgent.IsHuman || __instance == null || !RecordsOk) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var m = Mission.Current;
                if (m == null || !MissionTeams.Enemies(affectedAgent.Team, m.PlayerTeam)) return;
                int kind = BattlefieldLaw.KillerKindOf(affectorAgent);
                if (kind != 1 && kind != 2) return;
                var b = Begin(m);
                var ch = affectedAgent.Character;
                if (ch == null) return;
                if (ch.IsHero) { b.HeroesSkipped++; return; }   // DTE nie rusza bohaterow (ich rynsztunek, okup, niewola)
                if (kind == 1) b.FelledHero++; else b.FelledTroop++;
                var st = new Felled { B = b, Kind = kind };
                st.State = agentState == AgentState.Killed ? "zabity" : "ogluszony";
                var a = affectorAgent;
                if (a != null && a.IsMount && a.RiderAgent != null) a = a.RiderAgent;
                st.Who = kind == 2 ? "zolnierz" : a == Agent.Main ? "Ty" : "towarzysz";
                var co = ch as CharacterObject;
                st.Tier = co != null ? co.Tier : 0;
                st.Troop = ch.StringId;
                try { st.Naval = m.IsNavalBattle || m.IsNavalRaidBattle; } catch { st.Naval = false; }
                var eq = affectedAgent.SpawnEquipment;
                foreach (int slot in BodySlots)
                {
                    var it = eq[(EquipmentIndex)slot].Item;
                    if (it == null || it.IsBannerItem || it.ItemType == ItemObject.ItemTypeEnum.Banner) continue;
                    st.Slots.Add(slot); st.Items.Add(it);
                    if (slot == (int)EquipmentIndex.Horse) st.Horse = it;
                }
                var vp = PartyOf(affectedAgent.Origin);
                var mp = MobileParty.MainParty;
                if (vp != null && mp != null)
                {
                    st.HasIds = true; st.VictimId = vp.Id; st.MainId = mp.Id;
                    var rec = RecDict(__instance, st.VictimId, _fRecover);
                    var loot = RecDict(__instance, st.MainId, _fLooted);
                    foreach (var it in st.Items)
                    {
                        if (st.RecBefore.ContainsKey(it)) continue;
                        st.RecBefore[it] = Count(rec, it);
                        st.LootBefore[it] = Count(loot, it);
                    }
                }
                st.ProcessedBefore = Processed(__instance, affectedAgent);
                if (_fAssign != null && _fTemp != null)
                {
                    var asg = _fAssign.GetValue(__instance) as IDictionary;
                    var one = asg != null ? asg[affectedAgent] : null;
                    st.Temp = one != null ? _fTemp.GetValue(one) as Dictionary<EquipmentIndex, string> : null;
                }
                __state = st;
            }
            catch (Exception e) { Stumble("FelledPre", e); }
            finally { Lap(t0); }
        }

        /// <summary>Postfiks DTE OnAgentRemoved: roznica licznikow rekordow = lup (LootedItems gracza) i odrzut DropRate (ItemsToRecover bez lupu); reszta
        /// sztuk z przyczyna. Z1 (ExtraBrokenArmorWrecks): dokladnie jedna brakujaca czesc zbroi ze slotu 5-9 u wroga od tieru 3 = "dodatkowo peknieta"
        /// DTE - do wrakow (BattlefieldLaw.AddWreck); wiecej niz jedna - niejednoznaczne, bez wraka (nic z niczego).</summary>
        public static void FelledPost(object __instance, Agent affectedAgent, object __state)
        {
            var st = __state as Felled;
            if (st == null) { _pick = null; return; }
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var b = st.B;
                var pick = _pick;
                ConcurrentDictionary<ItemObject, int> rec = null, loot = null;
                if (st.HasIds)
                {
                    rec = RecDict(__instance, st.VictimId, _fRecover);
                    loot = RecDict(__instance, st.MainId, _fLooted);
                }
                bool processed = st.HasIds && !st.ProcessedBefore && Processed(__instance, affectedAgent);
                int n = st.Items.Count;
                var cls = new int[n];
                for (int i = 0; i < n; i++) cls[i] = -1;
                // lup i odrzut DropRate wedlug przedmiotu (ten sam przedmiot w kilku slotach - po kolei)
                var done = new HashSet<ItemObject>();
                for (int i = 0; i < n; i++)
                {
                    var it = st.Items[i];
                    if (!st.HasIds || !done.Add(it)) continue;
                    int cnt = 0;
                    for (int j = i; j < n; j++) if (st.Items[j] == it) cnt++;
                    int rb, lb;
                    st.RecBefore.TryGetValue(it, out rb); st.LootBefore.TryGetValue(it, out lb);
                    int la = Math.Max(0, Math.Min(cnt, Count(loot, it) - lb));
                    int ra = Math.Max(0, Count(rec, it) - rb);
                    int od = Math.Max(0, Math.Min(cnt - la, ra - la));
                    int k = 0;
                    for (int j = i; j < n; j++)
                    {
                        if (st.Items[j] != it) continue;
                        if (k < la) cls[j] = ALup; else if (k < la + od) cls[j] = AOdrz;
                        k++;
                    }
                }
                var cand = new List<int>();
                for (int i = 0; i < n; i++)
                {
                    if (cls[i] >= 0) continue;
                    var it = st.Items[i];
                    int slot = st.Slots[i];
                    string tmp;
                    if (!processed) cls[i] = AInne;                                                                       // DTE nie rozliczyl ciala
                    else if (!st.Naval && st.Temp != null && st.Temp.TryGetValue((EquipmentIndex)slot, out tmp) && tmp == it.StringId) cls[i] = AZast;
                    // m117 (recenzja): zapas, gdy odczyt _temporaryItemIdBySlot sie nie zwiazal (st.Temp null) - wlasne ubranie cywilne agenta to sztuka
                    // tymczasowa, nie kandydat Z1 "dodatkowo peknieta" (inaczej jedyny kandydat -> AddWreck = wrak z niczego); ten sam wzor co BattlefieldLaw.QueueWreck
                    else if (!st.Naval && Civvies.IsWorn(affectedAgent, it)) cls[i] = AZast;
                    else if (pick != null && it.StringId == pick.StringId) cls[i] = ACios;
                    else if (!AiGear.DteTakes(it)) cls[i] = AOdrz;                                                        // czarna lista DTE
                    else if (!st.Naval && st.Tier >= 3 && slot >= (int)EquipmentIndex.Head && slot <= (int)EquipmentIndex.Cape
                             && it.HasArmorComponent && it.ItemType != ItemObject.ItemTypeEnum.HorseHarness) cand.Add(i);
                    else cls[i] = AInne;
                }
                bool wreck = false;
                if (cand.Count == 1)
                {
                    int i = cand[0];
                    cls[i] = APek;
                    b.PekOne++;
                    var s = Settings.Current;   // Z1 (ExtraBrokenArmorWrecks): jak czesc rozbita ciosem - wrak do lupu (regula Jeffa 04.10)
                    if (s != null && s.ExtraBrokenArmorWrecks && s.WreckSalvageEnabled)
                    {
                        // m110-stan (LootStateFromWounds): stan peknietej czesci z obrazen - nietrafiona wraca w swoim stanie sprzed bitwy (podzial w AfterDtePartyItems)
                        var pdw = LootWear.On ? LootWear.Piece(affectedAgent, st.Items[i], st.Slots[i], LootWear.CPek, st.Kind, st.Who) : null;
                        if (pdw == null && LootWear.On) LootWear.NoBook(true);
                        if (BattlefieldLaw.AddWreck(st.Items[i], pdw)) { b.PekWreck++; wreck = true; }
                        else LootWear.Route(pdw, LootWear.RPrzep);
                    }
                }
                else if (cand.Count > 1)
                {
                    foreach (int i in cand) cls[i] = AInne;
                    b.Ambig += cand.Count;
                }
                for (int i = 0; i < n; i++)
                {
                    var it = st.Items[i];
                    int k = KindOf(it);
                    b.A[k, ANa]++;
                    b.A[k, cls[i]]++;
                    if (cls[i] == ACios) { b.WCios++; if (it.ItemType == ItemObject.ItemTypeEnum.HorseHarness) b.WCiosHarness++; }
                }
                if (st.Horse != null)
                {
                    bool horseLoot = false;
                    for (int i = 0; i < n; i++) if (st.Slots[i] == (int)EquipmentIndex.Horse && cls[i] == ALup) horseLoot = true;
                    b.HorseLoot[affectedAgent] = horseLoot;
                    b.Riders++;
                    if (b.RiderList.Count < RidersPerBattle)
                    {
                        var r = new Rider { Agent = affectedAgent, Head = st.Troop + " (" + st.Who + ", " + st.State + ")" };
                        foreach (int want in new[] { 10, 11, 6, 5, 8, 7, 9 })
                            for (int i = 0; i < n; i++)
                            {
                                if (st.Slots[i] != want) continue;
                                string part = KindName[KindOf(st.Items[i])] + " " + st.Items[i].StringId + " " + ClsName(cls[i], wreck);
                                if (want == 10) r.HorsePart = part; else r.Parts.Add(part);
                            }
                        b.RiderList.Add(r);
                    }
                }
            }
            catch (Exception e) { Stumble("FelledPost", e); }
            finally { _pick = null; Lap(t0); }
        }

        private static string ClsName(int c, bool wreck)
        {
            switch (c)
            {
                case ALup: return "lup";
                case ACios: return "cios";
                case APek: return wreck ? "pekniete->wrak" : "pekniete (bez wraka)";
                case AZast: return "zastepcze";
                case AOdrz: return "odrzut";
                default: return "inne";
            }
        }

        // ------------------------------------------------------------ doba
        internal static void Daily()
        {
            try
            {
                if (_dBattles == 0 && _dStumbles == 0) { ClearDay(); return; }   // recenzja R2: czas pomiaru doby bez bitew nie przechodzi na nastepna dobe
                int day = (int)CampaignTime.Now.ToDays;
                var s = Settings.Current;
                long armNa = 0, armLup = 0, armCios = 0, armZast = 0, rez = 0;
                for (int k = Tulow; k <= Peleryna; k++) { armNa += DayA[k, ANa]; armLup += DayA[k, ALup]; armCios += DayA[k, ACios]; armZast += DayA[k, AZast]; }
                for (int k = 0; k < Kinds; k++) rez += DayC[k, CRez];
                var sb = new StringBuilder("Lup bitew (m109): doba ").Append(day)
                    .Append(" | klucz konie ").Append(YN(s != null && s.FallenRiderHorsesLooted))
                    .Append(" | bitew ").Append(_dBattles).Append(" (lista trofeow ").Append(_dList).Append(", sakwy ").Append(_dBag).Append(", zwrot ").Append(_dBack)
                    .Append(", inne ").Append(_dOther).Append(')')
                    .Append(" | konie: z cial ").Append(DayA[Kon, ANa]).Append(", lup ").Append(DayA[Kon, ALup]).Append(", dzialka ").Append(DayB[Kon, Dzialka])
                    .Append(", na liscie ").Append(DayC[Kon, CNa]).Append(", do sakw ").Append(DayB[Kon, Sakwy] + DayC[Kon, CRez]).Append(", Spoils wyjal ").Append(DayC[Kon, CWyjal])
                    .Append(", padle zdjete (z kolejki/ze zbrojowni/poza lupem/brak) ").Append(_dDeadQ).Append('/').Append(_dDeadArm).Append('/').Append(_dDeadOut).Append('/').Append(_dDeadNone)
                    .Append(" | uprzaz: z cial ").Append(DayA[Uprzaz, ANa]).Append(", lup ").Append(DayA[Uprzaz, ALup]).Append(", cios ").Append(DayA[Uprzaz, ACios])
                    .Append(", na liscie ").Append(DayC[Uprzaz, CNa]).Append(", Spoils wyjal ").Append(DayC[Uprzaz, CWyjal])
                    .Append(" | zbroje ludzi: z cial ").Append(armNa).Append(", lup ").Append(armLup).Append(", cios ").Append(armCios)
                    .Append(", pekniete dodatkowo ").Append(_dPekOne).Append(" -> wraki ").Append(_dPekWreck).Append(" (niejednoznaczne ").Append(_dAmbig).Append(')')
                    .Append(", zastepcze ").Append(armZast)
                    .Append(" | rezerwa do sakw ").Append(rez).Append(" | pomiar ").Append(Ms(_dTicks)).Append(" ms | potkniecia ").Append(_dStumbles)
                    .Append(" (sesja ").Append(_stumbles).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            ClearDay();
        }

        private static void ClearDay()
        {
            Array.Clear(DayA, 0, DayA.Length); Array.Clear(DayB, 0, DayB.Length); Array.Clear(DayC, 0, DayC.Length);
            _dBattles = _dList = _dBag = _dBack = _dOther = _dDeadQ = _dDeadArm = _dDeadOut = _dDeadNone = _dPekOne = _dPekWreck = _dAmbig = _dStumbles = 0;
            _dTicks = 0;
        }

        /// <summary>Nowa gra albo wczytanie (BattlefieldLaw.LReset, PRZED czyszczeniem kolejki): otwarta bitwa zamknieta bez rozliczenia, doba od zera.</summary>
        internal static void Reset()
        {
            try { Close("bez rozliczenia - nowa kampania albo wczytanie"); } catch { }
            _cur = null; _sifting = false;
            _lastListN = 0; _lastListW = 0;   // m114-lup
            ClearDay();
            _testDone.Clear();
        }

        // ------------------------------------------------------------ m114-lup: ekran trofeow Spoils
        // Jeff 24.10 17:40: "dostaje tylko zbroje i zero broni... wszystkich zabijam sam, wiec wszystko powinno byc moje"; 17:55: bron byla potem w ekwipunku,
        // nie na ekranie lupu. Pomiar m109 konczy sie na liscie trofeow, a Spoils budowal ekran pozniej (BuildLootScreenRoster po zbieraniu) i drugi raz dzielil:
        // 30% od najdrozszych na ekran, reszta do sakw. Linia 'Ekran trofeow (m114)' na kazde zbudowanie ekranu: lista -> ekran -> sakwy (podzial) -> niezebrane.
        // Tylko czyta (prefiks liczy sakwy gracza, postfiks pola Spoils); wylacznik podzialu to BattlefieldLaw.NoSoldierSplit (klucz SpoilsSoldierSplitOff).
        private const string PE = "Ekran trofeow (m114): ";
        private static int _lastListN;         // bitwa, ktorej pomiar zamknela lista trofeow Spoils (do linii ekranu; 0 - nie wiadomo)
        private static long _lastListW;        // bron na liscie wedlug m109 (blok C, na liscie)
        private static FieldInfo _sGen, _sScr, _sPct, _sSolo;   // LootCollectionBehavior._generatedLoot / _lootScreenRoster / _collectedPercentage / _isSoloParty

        internal static void ApplyScreen(Harmony h)
        {
            string wBuild = "BRAK";
            try
            {
                var t = Type.GetType("RealisticLoot.Behaviors.LootCollectionBehavior, RealisticLoot");
                if (t != null)
                {
                    _sGen = AccessTools.Field(t, "_generatedLoot");
                    _sScr = AccessTools.Field(t, "_lootScreenRoster");
                    _sPct = AccessTools.Field(t, "_collectedPercentage");
                    _sSolo = AccessTools.Field(t, "_isSoloParty");
                    var build = AccessTools.DeclaredMethod(t, "BuildLootScreenRoster");
                    if (build != null && _sGen != null && _sScr != null)
                    {
                        h.Patch(build, prefix: new HarmonyMethod(typeof(LootTally), nameof(ScreenPre)), postfix: new HarmonyMethod(typeof(LootTally), nameof(ScreenBuilt)));
                        wBuild = "TAK";
                    }
                }
            }
            catch (Exception e) { wBuild = "BLAD"; Log.Error("LootTally.ApplyScreen", e); }
            try
            {
                var s = Settings.Current;
                string wSplit = BattlefieldLaw.SoldierSplitWired ? "TAK" : "BRAK";
                int ok = (wSplit == "TAK" ? 1 : 0) + (wBuild == "TAK" ? 1 : 0);
                Log.Info(PE + "start | klucz SpoilsSoldierSplitOff " + YN(s != null && s.SpoilsSoldierSplitOff) + " (BattlefieldLawEnabled " + YN(s != null && s.BattlefieldLawEnabled)
                         + ") | latki " + ok + "/2: Spoils EnablePrioritySystem (drugi podzial) " + wSplit + ", Spoils BuildLootScreenRoster (pomiar) " + wBuild
                         + " | odczyt: zebrano " + YN(_sPct != null) + ", solo " + YN(_sSolo != null) + ".");
            }
            catch (Exception e) { Log.Error("LootTally.ApplyScreen(linia)", e); }
        }

        private static long[] KindsOf(ItemRoster r)
        {
            var k = new long[Kinds];
            if (r == null) return k;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.EquipmentElement.Item != null && el.Amount > 0) k[KindOf(el.EquipmentElement.Item)] += el.Amount;
            }
            return k;
        }

        private static long Sum(long[] k) { long n = 0; foreach (var v in k) n += v; return n; }

        /// <summary>Prefiks BuildLootScreenRoster: sakwy gracza przed budowa ekranu (podzial Spoils dokladal tam swoja czesc).</summary>
        public static void ScreenPre(out long[] __state)
        {
            __state = null;
            try { var mp = MobileParty.MainParty; if (mp != null) __state = KindsOf(mp.ItemRoster); }
            catch (Exception e) { Stumble("ScreenPre", e); }
        }

        /// <summary>Postfiks BuildLootScreenRoster: lista trofeow -> ekran, sakwy (podzial Spoils), niezebrane (zbieranie przerwane).</summary>
        public static void ScreenBuilt(object __instance, long[] __state)
        {
            try
            {
                if (__instance == null) return;
                var gen = KindsOf(_sGen.GetValue(__instance) as ItemRoster);
                var scr = KindsOf(_sScr.GetValue(__instance) as ItemRoster);
                var bag = new long[Kinds];
                var mp = MobileParty.MainParty;
                if (__state != null && mp != null)
                {
                    var after = KindsOf(mp.ItemRoster);
                    for (int i = 0; i < Kinds; i++) bag[i] = Math.Max(0, after[i] - __state[i]);
                }
                float pct = -1f;
                try { if (_sPct != null) pct = (float)_sPct.GetValue(__instance); } catch { }
                string solo = "?";
                try { if (_sSolo != null) solo = YN((bool)_sSolo.GetValue(__instance)); } catch { }
                var s = Settings.Current;
                bool off = BattlefieldLaw.SoldierSplitWired && s != null && s.BattlefieldLawEnabled && s.SpoilsSoldierSplitOff;
                long L = Sum(gen), E = Sum(scr), Y = Sum(bag), Z = Math.Max(0, L - E - Y);
                var sb = new StringBuilder(PE);
                sb.Append("bitwa ").Append(_lastListN).Append(" | lista ").Append(L).Append(" (bron ").Append(gen[Bron]).Append(") -> ekran ").Append(E)
                  .Append(" (bron ").Append(scr[Bron]).Append(") | zebrano ").Append(pct < 0 ? "?" : Math.Round(pct * 100f).ToString(CultureInfo.InvariantCulture)).Append('%')
                  .Append(" | solo ").Append(solo).Append(" | podzial Spoils ").Append(off ? "WYLACZONY" : "WLACZONY")
                  .Append(" | poza ekranem ").Append(L - E).Append(" (podzial ").Append(Y).Append(", niezebrane ").Append(Z).Append(')')
                  .Append(" | m109 bron na liscie ").Append(_lastListN > 0 ? _lastListW.ToString(CultureInfo.InvariantCulture) : "-")
                  .Append(" | rodzaje [lista/ekran/podzial]: ");
                bool any = false;
                for (int i = 0; i < Kinds; i++)
                {
                    if (gen[i] == 0 && scr[i] == 0 && bag[i] == 0) continue;
                    if (any) sb.Append(", ");
                    sb.Append(KindName[i]).Append(' ').Append(gen[i]).Append('/').Append(scr[i]).Append('/').Append(bag[i]);
                    any = true;
                }
                if (!any) sb.Append("brak");
                sb.Append('.');
                Log.Info(sb.ToString());
                _lastListN = 0; _lastListW = 0;
            }
            catch (Exception e) { Stumble("ScreenBuilt", e); }
        }

        // ------------------------------------------------------------ autotest
        internal static bool TestWanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return File.Exists(Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName));
            }
            catch { return false; }
        }

        private static string Plan()
        {
            if (!_atLooked)
            {
                _atLooked = true;
                try
                {
                    foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (asm.GetName().Name != "CrashScribe") continue;
                        var t = asm.GetType("CrashScribe.AutotestBattle", false);
                        _fKind = t != null ? t.GetField("_kind", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) : null;
                        break;
                    }
                }
                catch { _fKind = null; }
            }
            try { var v = _fKind != null ? _fKind.GetValue(null) : null; return v != null ? v.ToString() : "?"; }
            catch { return "?"; }
        }

        private static string PlanName(string k)
        {
            return k == "Strong" ? "mocna" : k == "Retreat" ? "odwrot" : k == "Weak" ? "slaba" : "nieznany";
        }

        /// <summary>Zestaw jezdzca testu: kon (bez smokow i egzotykow), uprzaz i zbroja tulowia.</summary>
        private static bool RiderKit(Equipment e)
        {
            if (e == null) return false;
            var h = e[EquipmentIndex.Horse].Item;
            var hh = e[EquipmentIndex.HorseHarness].Item;
            if (h == null || hh == null || e[EquipmentIndex.Body].Item == null || h.StringId == null) return false;
            if (h.StringId.StartsWith("dragon_") || MountLaw.IsExotic(h)) return false;
            return h.ItemType == ItemObject.ItemTypeEnum.Horse && hh.ItemType == ItemObject.ItemTypeEnum.HorseHarness;
        }

        /// <summary>Jezdziec testu: nie bohater od tieru 3 z zestawem bojowym jezdzca (RiderKit) - pierwszy taki zestaw.</summary>
        private static Equipment RiderSet(CharacterObject c)
        {
            if (c == null || c.IsHero || c.Tier < 3) return null;
            foreach (var e in c.BattleEquipments) if (RiderKit(e)) return e;
            return null;
        }

        private static CharacterObject PickRider(out string why)
        {
            why = null;
            var c = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<CharacterObject>("veteran_caravan_guard_battania") : null;
            if (RiderSet(c) != null) return c;
            CharacterObject best = null;
            foreach (var ch in CharacterObject.All)
            {
                if (ch == null || ch.StringId == null || RiderSet(ch) == null) continue;
                if (best == null || string.CompareOrdinal(ch.StringId, best.StringId) < 0) best = ch;
            }
            if (best == null) why = c == null ? "brak veteran_caravan_guard_battania i zadnej jednostki tieru 3+ z koniem, uprzeza i zbroja" : "veteran_caravan_guard_battania bez zestawu z koniem i uprzeza, zadnej innej";
            return best;
        }

        /// <summary>Prefiks EncounterManager.StartPartyEncounter - tylko autotest (plik armoury-lup.flag, LogGate 1/2) i tylko wrog bitwy testu CrashScribe
        /// (autotest_bitwa_*) w planie mocna: 6 jezdzcow z koniem, uprzeza i zbroja do bandy (bitwa testu jest z bandytami bez koni). Sam roster -
        /// recenzja L1: banda testu to partia bandytow (BanditPartyComponent), a DTE nie ubiera bandytow ze zbrojowni (SpawnAgentPatch
        /// IsPartyValidForProcessing - IsBandit), tylko wzorcem jednostki (Assignment.FillEmptySlots: sloty 10-11 z wzorca w bitwie polowej) - jezdzcy
        /// przychodza konno; zestaw dolozony do zbrojowni bandy bylby sprzetem z niczego (udzial gracza ze zbrojowni pokonanych - DTE
        /// ItemRosterForPlayerLootSharePatch - jako bagaz na liste trofeow). Przed MapEvent, wiec przed migawka Spoils i rozdzielaczami DTE.
        /// W grze Jeffa: brak pliku, inny tryb i brak takiej partii - koniec od razu.</summary>
        public static void TestRiders(PartyBase attackerParty, PartyBase defenderParty)
        {
            try
            {
                if (attackerParty == null || defenderParty == null || attackerParty != PartyBase.MainParty) return;
                var mp = defenderParty.MobileParty;
                if (mp == null || mp.StringId == null || !mp.StringId.StartsWith("autotest_bitwa_")) return;
                if (!TestWanted() || !_testDone.Add(mp.StringId)) return;
                string plan = Plan();
                if (plan == "Retreat" || plan == "Weak") { Log.Info(P + "test - plan " + PlanName(plan) + ": bez jezdzcow."); return; }
                string why;
                var troop = PickRider(out why);
                var set = RiderSet(troop);
                if (troop == null || set == null) { Log.Info(P + "test - brak jezdzca z koniem i uprzeza (" + (why ?? "?") + ")."); return; }
                mp.MemberRoster.AddToCounts(troop, TestRidersCount);
                Log.Info(P + "test - wrog " + mp.StringId + " (plan " + PlanName(plan) + "): dolozono " + TestRidersCount + " x " + troop.StringId + " (kon "
                         + set[EquipmentIndex.Horse].Item.StringId + ", uprzaz " + set[EquipmentIndex.HorseHarness].Item.StringId + ", tulow " + set[EquipmentIndex.Body].Item.StringId
                         + ") - bez zbrojowni (DTE ubiera bandytow wzorcem jednostki).");
            }
            catch (Exception e) { Stumble("TestRiders", e); }
        }
    }
}
