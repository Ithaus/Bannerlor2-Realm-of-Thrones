using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// Paczka 133b (Jeff 25.10 ok. 12:57: "w widoku armii tez ma pokazywac: jakiego sprzetu brakuje zolnierzom, bo teraz ja nie wiem, w tym logu tak szybko to
    /// leci ze ja nie wiem ... przeniesmy logi do zakladki armii"): wiersz "Last battle" okna "Review the army" i jego strony - FAKTYCZNY stan ostatniej bitwy
    /// druzyny gracza (KitGaps.Last: zebrany w GarrisonKit.OwnKitSpawn po wszystkich strazach, ta sama liczba co "gracz N" linii bitwy z podzialem na rodzaje).
    /// Tylko odczyt zapamietanych liczb (zero liczenia na mapie).
    /// 133c: wiersz "Kit for the next battle" (KitPreview - ten sam rozdzielacz DTE i te same straze co bitwa, z pamiecia po wersjach i dniu), lista typow
    /// ("Look" / "Back"), strona typu z przyczynami brakow i strona "Unused in the troops' armoury". Okno nadal tylko z obozu (decyzja (d)).
    /// </summary>
    internal static partial class ArmyReview
    {
        private const int TypesPerPara = 6;

        /// <summary>Stan okna "Last battle" i podgladu nastepnej bitwy (133c) - ArmyReview.Reset (nowa kampania i wczytanie).</summary>
        private static void KitReset() { KitGaps.Reset(); KitPreview.Reset(); }

        // ------------------------------------------------------------ 133c: "Kit for the next battle"
        /// <summary>Wiersz glownego okna z zapasem: niedostepny podglad - wiersz wyszarzony z powodem (reszta okna dziala).</summary>
        private static InquiryElement NextRowSafe(KitPreview.Result pv)
        {
            try
            {
                bool ok = pv != null && pv.Ok && pv.Men > 0;
                return new InquiryElement("next", KitPreview.MainTitle(pv), null, ok, KitPreview.MainHint(pv));
            }
            catch (Exception e) { Stumble("NextRow", e); return new InquiryElement("next", "Kit for the next battle (not available)", null, false, "The hand-out cannot be worked out now."); }
        }

        /// <summary>Okno listy typow: najpierw typy z brakami, na koncu "Unused in the troops' armoury". Strony typu i resztki wracaja tutaj.</summary>
        private static void OpenKitTypes()
        {
            var pv = KitPreview.Get(false);
            if (pv == null || !pv.Ok || pv.Rows.Count == 0) { OpenMain(false); return; }
            var rows = new List<InquiryElement>();
            foreach (var t in pv.Rows)
                rows.Add(new InquiryElement(t.Id, KitPreview.RowTitle(t), TroopPortrait(t.Troop), t.Men > 0, KitPreview.RowHint(t)));
            rows.Add(new InquiryElement("~unused", KitPreview.UnusedTitle(pv), null, true, "What is left in the troops' armoury after the hand-out, and why no man took it."));
            bool photo = WindowPhotos.Active;   // paczka 132: okno proby zdjec - przyciski bez skutku
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Kit for the next battle", KitPreview.ListDesc(pv), rows, true, 1, 1, "Look", "Back",
                delegate (List<InquiryElement> picked)
                {
                    if (Inert(photo)) return;
                    try
                    {
                        string id = picked != null && picked.Count > 0 ? picked[0].Identifier as string : null;
                        var cur = KitPreview.Get(false);
                        if (id == null || cur == null || !cur.Ok) { OpenKitTypes(); return; }
                        _dReports++;
                        if (id == "~unused") { ShowPages("Unused in the troops' armoury", Paginate(KitPreview.UnusedText(cur)), 0, OpenKitTypes); return; }
                        var t = KitPreview.Find(cur, id);
                        if (t == null) OpenKitTypes();
                        else ShowPages(t.Name, Paginate(KitPreview.TypeText(cur, t)), 0, OpenKitTypes);
                    }
                    catch (Exception e) { Stumble("Kit.Pick", e); }
                },
                delegate (List<InquiryElement> _) { if (Inert(photo)) return; try { OpenMain(false); } catch (Exception e) { Stumble("Kit.Back", e); } }), true);
        }

        /// <summary>Wszystkie strony podgladu (typy i resztka) - linia raportu i test T1 (strony najwyzej Rumours.PageLines linii).</summary>
        internal static List<string> NextPages(KitPreview.Result pv)
        {
            var l = new List<string>();
            if (pv == null || !pv.Ok) return l;
            foreach (var t in pv.Rows) l.AddRange(Paginate(KitPreview.TypeText(pv, t)));
            l.AddRange(Paginate(KitPreview.UnusedText(pv)));
            return l;
        }

        /// <summary>Data bitwy: data gry i ile dni temu (K16 - bez surowego numeru doby).</summary>
        private static string BattleDate(CampaignTime t)
        {
            string d;
            try { d = CalendarText.Date(t, false); } catch { d = "?"; }
            int ago = 0;
            try { ago = Math.Max(0, (int)Math.Floor(t.ElapsedDaysUntilNow)); } catch { ago = 0; }
            return d + (ago == 0 ? " (today)" : ago == 1 ? " (yesterday)" : " (" + ago + " days ago)");
        }

        /// <summary>Wiersz glownego okna z zapasem: potkniecie - wiersz wyszarzony (reszta okna dziala).</summary>
        private static InquiryElement LastRowSafe()
        {
            try { return LastBattleRow(); }
            catch (Exception e) { Stumble("LastBattleRow", e); return new InquiryElement("last", "Last battle (not available)", null, false, "The record of the last battle cannot be read now."); }
        }

        /// <summary>Wiersz glownego okna; bez zapisu - wyszarzony z powodem.</summary>
        private static InquiryElement LastBattleRow()
        {
            var b = KitGaps.Last;
            if (b == null || b.All == null)
                return new InquiryElement("last", "Last battle: nothing recorded yet", null, false,
                                          GarrisonKit.OwnKitOn ? "Recorded as your men take the field in a battle - what each of them went without. No battle of your men since this campaign was loaded."
                                                               : "Recorded only when Troops Fight With Own Kit Only is on - without it your men fight in their troop's full kit.");
            var a = b.All;
            int foot = a.Foot + a.NoSaddle;
            string title;
            if (a.Missing <= 0 && foot <= 0) title = "Last battle: full kit (" + a.Men + (a.Men == 1 ? " man)" : " men)");
            else
            {
                var parts = new List<string>();
                if (a.Missing > 0) parts.Add(a.Missing + (a.Missing == 1 ? " kit slot empty" : " kit slots empty"));
                if (foot > 0) parts.Add(foot + (foot == 1 ? " rider on foot" : " riders on foot"));
                title = "Last battle: " + string.Join(", ", parts.ToArray()) + " (" + a.Men + (a.Men == 1 ? " man)" : " men)");
            }
            string hint = BattleDate(b.When) + ": what your men went without when they took the field, counted after every check. The gaps gather in the men"
                          + " handed out last - if not all your men fought, fewer show here than the hand-out had.";
            return new InquiryElement("last", title, null, true, hint);
        }

        /// <summary>Strony "Last battle" (akapity rozdzielone pusta linia - Paginate; zaden akapit nie dluzszy niz strona).</summary>
        internal static string LastBattleText()
        {
            var b = KitGaps.Last;
            if (b == null || b.All == null)
                return GarrisonKit.OwnKitOn
                    ? "No battle of your men recorded since this campaign was loaded. After your next battle this page shows what each kind of your men went without."
                    : "Nothing is recorded: Troops Fight With Own Kit Only is off, so your men fight in their troop's full kit and nothing goes missing.";
            var a = b.All;
            var sb = new StringBuilder();
            sb.Append("Your last battle: ").Append(BattleDate(b.When)).Append(" - ")
              .Append(b.Field ? "a battle in the field" : "a siege, a hideout or a fight at sea").Append(". ")
              .Append(a.Men).Append(" of your men took the field");
            if (a.Hero > 0) sb.Append(" (").Append(a.Hero).Append(a.Hero == 1 ? " of them you or a companion)" : " of them you and your companions)");
            sb.Append('.');

            // braki wedlug rodzaju (te same sloty co linia bitwy "gracz N")
            int sum = KitGaps.ShortSum(a);
            sb.Append("\n\n");
            if (sum <= 0 && a.Missing <= 0) sb.Append("Every man had each piece of the kit his skill and rank call for.");
            else
            {
                sb.Append(a.Missing).Append(a.Missing == 1 ? " kit slot went empty" : " kit slots went empty")
                  .Append(" - pieces of the kit their skill and rank call for that they fought without.");
                string kinds = KitGaps.Kinds(a, true);
                if (kinds.Length > 0) sb.Append("\nBy kind: ").Append(kinds).Append('.');
                if (a.Replaced > 0)
                    sb.Append('\n').Append(a.Replaced).Append(a.Replaced == 1 ? " of these weapon gaps was" : " of these weapon gaps were")
                      .Append(" filled with another kind of weapon from the troops' armoury, so ").Append(a.Replaced == 1 ? "it is" : "they are").Append(" not counted as empty.");
                if (a.Short[KitGaps.SideArm] > 0) sb.Append("\nA side-arm is the second weapon of the kit - the sword of a spearman or an archer.");
            }
            int extra = a.Extra - a.Replaced;
            if (extra > 0) sb.Append('\n').Append(extra).Append(extra == 1 ? " weapon was" : " weapons were").Append(" handed out on top of the kit (spare kit in free slots).");
            if (b.Bad > 0) sb.Append("\nFor ").Append(b.Bad).Append(b.Bad == 1 ? " man" : " men").Append(" the kinds could not be counted - the log has the details.");

            // typy z brakami (akapit najwyzej TypesPerPara wierszy), potem typy w pelnym sprzecie
            var gaps = new List<KitGaps.Row>();
            var full = new List<KitGaps.Row>();
            foreach (var r in b.Types)
            {
                if (r == null) continue;
                if (r.Missing > 0 || KitGaps.ShortSum(r) > 0 || r.Foot + r.NoSaddle > 0) gaps.Add(r); else full.Add(r);
            }
            for (int i = 0; i < gaps.Count; i += TypesPerPara)
            {
                sb.Append("\n\n");
                if (i == 0) sb.Append("By troop:\n");
                for (int k = i; k < gaps.Count && k < i + TypesPerPara; k++)
                {
                    var r = gaps[k];
                    if (k > i) sb.Append('\n');
                    sb.Append(r.Name).Append(" x").Append(r.Men).Append(": ");
                    var bits = new List<string>();
                    if (r.Missing > 0 || KitGaps.ShortSum(r) > 0)
                    {
                        string kinds = KitGaps.Kinds(r, true);
                        bits.Add(r.Missing + " empty" + (kinds.Length > 0 ? " - " + kinds : "") + (r.Replaced > 0 ? " (" + r.Replaced + " filled with another kind)" : ""));
                    }
                    if (r.Foot > 0) bits.Add(r.Foot + (r.Foot == 1 ? " rider with no horse" : " riders with no horse"));
                    if (r.NoSaddle > 0) bits.Add(r.NoSaddle + (r.NoSaddle == 1 ? " rider with no harness" : " riders with no harness"));
                    sb.Append(string.Join("; ", bits.ToArray())).Append('.');
                }
            }
            if (full.Count > 0)
            {
                sb.Append("\n\nFull kit: ");
                int shown = Math.Min(full.Count, 10);
                for (int k = 0; k < shown; k++) { if (k > 0) sb.Append(", "); sb.Append(full[k].Name).Append(" x").Append(full[k].Men); }
                if (full.Count > shown) sb.Append(" and ").Append(full.Count - shown).Append(" more kinds");
                sb.Append('.');
            }

            // jezdzcy
            sb.Append("\n\n");
            if (!b.Field) sb.Append("Riders: in a siege, a hideout or at sea no horses are handed out - every rider fought on foot.");
            else
            {
                int foot = a.Foot + a.NoSaddle;
                if (foot <= 0) sb.Append("No rider went on foot.");
                else
                {
                    sb.Append(foot).Append(foot == 1 ? " rider went on foot: " : " riders went on foot: ");
                    var why = new List<string>();
                    if (a.Foot > 0) why.Add(a.Foot + " with no horse in the troops' armoury");
                    if (a.NoSaddle > 0) why.Add(a.NoSaddle + " with a horse but no harness for it (the horse stayed in the armoury)");
                    sb.Append(string.Join(", ", why.ToArray())).Append('.');
                }
            }

            // bron: podloga przy rozdaniu (wszyscy zdrowi) i bez broni przy wystawieniu (po strazach)
            var arms = new List<string>();
            if (b.Floor && b.FloorBare > 0)
            {
                string s = "At the hand-out (all your fit men, before they took the field) " + b.FloorBare + (b.FloorBare == 1 ? " man was" : " men were")
                           + " left with no weapon after the checks: " + b.FloorGot + " took one left in the troops' armoury";
                if (b.FloorOver > 0) s += " (" + b.FloorOver + " of them one too hard for them)";
                if (b.FloorNone > 0) s += ", " + b.FloorNone + " found none";
                arms.Add(s + ".");
            }
            if (a.Bare > 0)
                arms.Add("Took the field with no weapon from the troops' armoury: " + a.Bare
                         + (a.Home > 0 ? " - " + a.Home + (a.Home == 1 ? " of them took a club or farm tool from home." : " of them took clubs and farm tools from home.") : "."));
            if (a.Civ > 0) arms.Add("Civilian tunics and boots worn where armour was missing: " + a.Civ + " - still counted as empty above.");
            if (arms.Count > 0) sb.Append("\n\n").Append(string.Join("\n", arms.ToArray()));

            sb.Append("\n\nCounted as each man took the field, after every check of the hand-out (skill, culture, saddle, no kit from nowhere). The battle hands kit out"
                      + " in order, so the gaps gather in the men handed out last: if not all your men took the field, fewer gaps show here than the hand-out had.");
            return sb.ToString();
        }

        /// <summary>Segment linii raportu okna (log): ostatnia bitwa w skrocie.</summary>
        private static string LastBattleBrief()
        {
            var b = KitGaps.Last;
            if (b == null || b.All == null) return "brak zapisu (tylko to, co ma " + Yes(GarrisonKit.OwnKitOn) + ")";
            var a = b.All;
            string k = KitGaps.Kinds(a, false);
            return "dzien " + (int)b.When.ToDays + ", ludzi " + a.Men + ", puste " + a.Missing + (k.Length > 0 ? " (" + k + ")" : "") + ", zastapione " + a.Replaced
                   + ", pieszo " + (a.Foot + a.NoSaddle) + (b.Field ? "" : ", bez koni") + (b.Bad > 0 ? ", ROZJAZD rozbicia " + b.Bad : "");
        }
    }
}
