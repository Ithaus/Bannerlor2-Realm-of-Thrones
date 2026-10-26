using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// Paczka 202 (Jeff 26.10 ok. 22:15, odpowiedz "B": "Prawdziwy sprzet na ekranie druzyny: Twoi ludzie pokazani w tym, co naprawde dostana do bitwy, zamiast
    /// wzorca. Zasady sie nie zmieniaja, znika tylko mylacy obrazek. Mala zmiana."). Wcieleni rycerze Lannisterow staly na ekranie druzyny w pelnym wzorcu ROT
    /// z koniem i kropierzem, choc przy "tylko to, co ma" (TroopsFightWithOwnKitOnly) wychodza z tym, co da wspolna zbrojownia druzyny (pula DTE).
    /// DUZY model 3D (PartyVM.SelectedCharacter, ten sam punkt co CaptiveRags - postfiks RefreshCurrentCharacterInformation, wolany tylko przy zmianie
    /// kliknietego wiersza): dla SZEREGOWEGO czlonka druzyny gracza (prawa strona, nie bohater, nie jeniec - jency dalej w lachmanach CaptiveRags) model dostaje
    /// zestaw z podgladu nastepnej bitwy (KitPreview.ForScreen - ten sam Compute co okno "Review the army": rozdzielacz DTE w trybie podgladu, straze wystawienia,
    /// siodlo, jezdziec bez konia) dla skladu z ekranu (z wcielonymi jencami przed zatwierdzeniem). Pokazany jest najlepiej obsluzony czlowiek typu (najmniej
    /// pustych slotow; remis - pierwszy w kolejnosci DTE); puste sloty puste, kon i uprzaz tylko, gdy zbrojownia je daje. Ikony listy nietkniete.
    /// Tylko obraz: zadna zasada, pula, zloto ani wynik bitwy sie nie zmienia (osobny rozdzielacz i osobna kopia puli, jak okno armii). Kazde niepowodzenie -
    /// wzorzec jak dotad i linia logu. Klucz MCM PartyScreenRealKit ("Party Screen Real Kit", TAK).
    /// Takze (202): linia "Werbunek jencow gracza (202)" przy CampaignEvents.OnMainPartyPrisonerRecruitedEvent (DoneLogic ekranu druzyny).
    /// </summary>
    internal static class PartyScreenKit
    {
        private const string P = "Ekran druzyny (202): ";
        private static object _loggedFor;                                   // podglad, dla ktorego pisane sa linie typow (nowy podglad - od nowa)
        private static readonly HashSet<string> _logged = new HashSet<string>(StringComparer.Ordinal);
        private static string _lastSkip;
        private static int _stumbles;

        internal static bool On { get { var s = Settings.Current; return s != null && s.PartyScreenRealKit; } }

        public static void Postfix(PartyVM __instance)
        {
            try
            {
                if (!On || __instance == null) return;
                var cur = __instance.CurrentCharacter;
                if (cur == null || cur.Type != PartyScreenLogic.TroopType.Member || cur.Side != PartyScreenLogic.PartyRosterSide.Right) return;
                var ch = cur.Character;
                if (ch == null || ch.IsHero) return;
                var model = __instance.SelectedCharacter;
                var logic = __instance.PartyScreenLogic;
                var main = MobileParty.MainParty;
                if (model == null || logic == null || main == null || logic.RightOwnerParty != main.Party) return;   // tylko druzyna gracza
                var rosters = logic.MemberRosters;
                var roster = rosters != null && rosters.Length > (int)PartyScreenLogic.PartyRosterSide.Right ? rosters[(int)PartyScreenLogic.PartyRosterSide.Right] : null;

                bool fresh;
                var r = KitPreview.ForScreen(roster, out fresh);
                if (fresh)
                {
                    _loggedFor = r; _logged.Clear();
                    if (r.Ok)
                        Log.Info(P + "podglad nastepnej bitwy dla skladu z ekranu - typow " + r.Rows.Count + ", zdrowych " + r.Men + " (rannych " + r.Wounded + "), pustych slotow "
                                 + r.Missing + ", pieszo " + r.Foot + ", zdjete przy wystawieniu " + r.Stripped + "; czas " + r.Ms + " ms" + (r.Ms > 50 ? " (PONAD 50 ms)" : "") + ".");
                }
                if (!r.Ok)
                {
                    // niedostepny (np. Troops Fight With Own Kit Only = off - wtedy wzorzec jest prawda): wzorzec zostaje, linia raz na powod
                    string why = r.Why ?? "?";
                    if (why != _lastSkip) { _lastSkip = why; Log.Info(P + "wzorzec zostaje - podglad niedostepny: \"" + why + "\"."); }
                    return;
                }
                _lastSkip = null;
                var row = KitPreview.Find(r, ch.StringId);
                if (!ReferenceEquals(_loggedFor, r)) { _loggedFor = r; _logged.Clear(); }
                if (row == null || row.Look == null)
                {
                    if (_logged.Add(ch.StringId ?? "?"))
                        Log.Info(P + (ch.StringId ?? "?") + " - wzorzec zostaje: " + (row != null && row.Wounded > 0 ? "wszyscy ranni (" + row.Wounded + ") - nie wyjda do nastepnej bitwy"
                                                                                       : "typu nie ma w podgladzie (brak wzorca bitwy)") + ".");
                    return;
                }
                var eq = row.Look.Clone(false);
                if (eq[EquipmentIndex.Horse].IsEmpty) eq[EquipmentIndex.HorseHarness] = default(EquipmentElement);   // uprzaz bez konia nie wyjedzie
                model.SetEquipment(eq);
                if (_logged.Add(ch.StringId ?? "?"))
                {
                    int empty = 0, tmpl = 0;
                    var rf = ch.Equipment;
                    for (int s = 0; s < 12; s++)
                    {
                        if (s == 4) continue;
                        bool inT = rf != null && !rf[(EquipmentIndex)s].IsEmpty;
                        if (inT) tmpl++;
                        if (inT && eq[(EquipmentIndex)s].IsEmpty) empty++;
                    }
                    bool tHorse = rf != null && !rf[EquipmentIndex.Horse].IsEmpty, tSad = rf != null && !rf[EquipmentIndex.HorseHarness].IsEmpty;
                    Log.Info(P + ch.StringId + " - model w sprzecie nastepnej bitwy (najlepiej obsluzony z " + row.Men + " zdrowych): puste sloty " + empty + " z " + tmpl
                             + " pierwszego wzorca, kon " + (eq[EquipmentIndex.Horse].IsEmpty ? "NIE" : "TAK") + (tHorse ? " (wzorzec TAK)" : " (wzorzec NIE)")
                             + ", uprzaz " + (eq[EquipmentIndex.HorseHarness].IsEmpty ? "NIE" : "TAK") + (tSad ? " (wzorzec TAK)" : " (wzorzec NIE)")
                             + "; typ: puste " + row.Missing + ", na koniu " + row.Rode + ", pieszo " + row.Foot + ".");
                }
            }
            catch (Exception e)
            {
                // wzorzec gry zostal juz ustawiony przez metode gry - nic do cofania
                _stumbles++;
                if (_stumbles <= 3 || _stumbles % 50 == 0) Log.Error("PartyScreenKit (potkniecie " + _stumbles + ", wzorzec zostaje)", e);
            }
        }

        /// <summary>CampaignEvents.OnMainPartyPrisonerRecruitedEvent (DoneLogic ekranu druzyny): wcieleni jency gracza - tylko log (typ i liczba).</summary>
        internal static void OnPrisonerRecruited(FlattenedTroopRoster roster)
        {
            try
            {
                if (roster == null) return;
                var n = new Dictionary<string, int>(StringComparer.Ordinal);
                var order = new List<string>();
                foreach (var el in roster)
                {
                    var t = el.Troop;
                    if (t == null || t.IsHero) continue;
                    string id = t.StringId ?? "?";
                    int v;
                    if (!n.TryGetValue(id, out v)) order.Add(id);
                    n[id] = v + 1;
                }
                int day = (int)CampaignTime.Now.ToDays;
                foreach (var id in order) Log.Info("Werbunek jencow gracza (202): " + id + " x" + n[id] + ", dzien " + day + ".");
            }
            catch (Exception e) { Log.Error("PartyScreenKit.OnPrisonerRecruited", e); }
        }

        internal static void Reset() { _loggedFor = null; _logged.Clear(); _lastSkip = null; _stumbles = 0; }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(PartyVM), "RefreshCurrentCharacterInformation");
                if (m == null) { Log.Info(P + "brak PartyVM.RefreshCurrentCharacterInformation - patch spi."); return; }
                h.Patch(m, postfix: new HarmonyMethod(typeof(PartyScreenKit), "Postfix"));
                Log.Info(P + "model 3D szeregowego czlonka druzyny gracza w sprzecie nastepnej bitwy (podglad 133c), klucz Party Screen Real Kit "
                         + (On ? "TAK" : "NIE") + "; jency - lachmany CaptiveRags, ikony listy nietkniete.");
            }
            catch (Exception e) { Log.Error("PartyScreenKit.ApplyAll", e); }
        }
    }
}
