using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m102-tablica E2b (projekt pkt 2.3, 6.4) - LIST SOLTYSA: "BANDA NEKA WIES". Gdy banda zlupila wies miasta w ostatnich 30 dniach (RaidCompletedEvent), a soltys
    /// tej wsi nie ma zadania z gry (kazdy czlowiek najwyzej jedno ogloszenie), na tablicy miasta wisi jego list (rodzaj 1). Ta sama kwota co u kapitana (S = zold zalogi
    /// miasta tej wsi), ten sam dowod i odbiorca (gracz albo lord AI, nie wrog), ale PLACI KIESA WSI: list tylko, gdy kwota <= 1/4 wolnej kiesy (kiesa - skrzynia
    /// na czarna godzine DsVillage.Chest), wyplata najwyzej wolna kiesa (GiveGoldAction.ApplyForSettlementToCharacter z osady wsi), bez doplaty pana. Gracz dostaje
    /// tez relacje z soltysem +2. Rozmowa tylko we wsi (tablica stawia flage na wsi): linia "About your notice - the band harrying X..." w glownych opcjach rozmowy
    /// z soltysem (priorytet 111), dalej przyjecie / kto placi / nie teraz. Jedna banda - jeden list (kapitan albo soltys). Klucz HeadmanBounties.
    /// </summary>
    internal static partial class NoticeBoard
    {
        private const int HeadmanHarmDays = 30;
        private static int _hNew, _hPaid, _hDialog;

        /// <summary>Listy soltysow: klucz HeadmanBounties, tablica (NoticeBoard) i wsie na tablicy (NoticeBoardVillages) - soltys to czlowiek wsi (poprawka po recenzji).</summary>
        private static bool HeadmanOn { get { var s = Settings.Current; return s != null && s.NoticeBoard && s.NoticeBoardVillages && s.HeadmanBounties; } }

        private static string HeadmanKeysText() { var s = Settings.Current; return ", Headman Bounties " + YN(s != null && s.HeadmanBounties); }

        private static Hero HeadmanOf(Settlement vs)
        {
            if (vs == null || vs.Notables == null) return null;
            foreach (var h in vs.Notables) if (h != null && h.IsHeadman && h.IsAlive && !h.IsPrisoner) return h;
            return null;
        }

        /// <summary>Wolna kiesa wsi: kiesa - skrzynia na czarna godzine (DsVillage.Chest; wies nieskalibrowana - 0).</summary>
        private static double VillageFree(Village v)
        {
            if (v == null) return 0;
            double chest = 0;
            try { chest = DsVillage.Chest(v); } catch (Exception e) { WStumble("VillageFree", e); }
            return Math.Max(0, v.Gold - chest);
        }

        /// <summary>WatchDaily: wywieszenie listow soltysow (po listach kapitanow). posted - bandy z listem (jedna banda, jeden list).</summary>
        private static void HeadmanPost(int day, HashSet<string> posted)
        {
            if (!HeadmanOn || _harm.Count == 0) return;
            var givers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var n in _notices) if (n.Kind == 1) givers.Add(n.Giver);
            foreach (var kv in _harm)
            {
                if (posted.Contains(kv.Key)) continue;
                MobileParty mp = null;
                foreach (var h in kv.Value)
                {
                    try
                    {
                        if (h.Kind != 0 || h.Day < day - HeadmanHarmDays || string.IsNullOrEmpty(h.Vil)) continue;
                        if (mp == null) mp = FindParty(kv.Key);
                        if (mp == null || !mp.IsActive || mp.CurrentSettlement != null || mp.MapEvent != null || Undead.Party(mp) || mp.IsCurrentlyAtSea) break;
                        int men = mp.Party != null ? mp.Party.NumberOfHealthyMembers : 0;
                        if (men < 5) break;
                        var vs = Settlement.Find(h.Vil);
                        var v = vs != null ? vs.Village : null;
                        var town = v != null ? v.Bound : null;
                        if (v == null || town == null || town.Town == null || town.OwnerClan == null) continue;
                        if (vs.IsUnderSiege || v.VillageState == Village.VillageStates.Looted || v.VillageState == Village.VillageStates.BeingRaided) continue;
                        var head = HeadmanOf(vs);
                        if (head == null || head.Issue != null || givers.Contains(head.StringId)) continue;
                        var c = new Cand { Mp = mp, Men = men };
                        Score(c, town);
                        float rate = GarrisonRate(town);
                        int N = Math.Max(10, Math.Min(60, men));
                        int D = c.Mounted ? 4 : 3;
                        int sum = Math.Max(1, (int)Math.Round(N * D * rate * Premium * (1 + c.Bonus / 100.0)));
                        double free = VillageFree(v);
                        if (sum > free / 4) continue;
                        var n = new Notice
                        {
                            Town = town.StringId, Band = mp.StringId, Name = BandNameOf(mp), Owner = town.OwnerClan.StringId, Men = men, Days = D, BonusPct = c.Bonus, S = rate, Sum = sum,
                            Posted = day, Due = day + DaysUp, Kind = 1, Giver = head.StringId, Village = vs.StringId, St = town, Mp = mp
                        };
                        _notices.Add(n);
                        posted.Add(n.Band);
                        givers.Add(head.StringId);
                        _hNew++; _wNew++;
                        Log.Info(PW + "list soltysa - " + vs.StringId + " (" + town.StringId + ") " + n.Band + " (" + men + " ludzi, " + D + " d, S " + rate.ToString("0.00", Inv) + ", szkody +" + c.Bonus
                                 + "%) = " + sum + ", platnik wies, wolna kiesa " + ((long)free).ToString(Inv) + ", soltys " + head.StringId + " - " + n.Name);
                        break;
                    }
                    catch (Exception e) { WStumble("HeadmanPost", e); break; }
                }
            }
        }

        /// <summary>Pay (rodzaj 1): kiesa wsi -> odbiorca, najwyzej wolna kiesa; gracz - relacja z soltysem +2. Zwraca wyplacone z kiesy wsi.</summary>
        private static int HeadmanPay(Notice n, Hero payee)
        {
            var vs = Settlement.Find(n.Village);
            var v = vs != null ? vs.Village : null;
            if (v == null || payee == null) return 0;
            int x = (int)Math.Max(0, Math.Min(n.Sum, VillageFree(v)));
            if (x > 0) GiveGoldAction.ApplyForSettlementToCharacter(vs, payee, x, true);
            _hPaid++;
            if (payee == Hero.MainHero)
            {
                var head = Hero.Find(n.Giver);
                if (head != null && head.IsAlive) { try { ChangeRelationAction.ApplyPlayerRelation(head, 2); } catch (Exception e) { WStumble("HeadmanPay(relacja)", e); } }
            }
            return x;
        }

        private static string HeadmanCard(Notice n, Settlement st)
        {
            var vs = Settlement.Find(n.Village);
            var head = Hero.Find(n.Giver);
            var sb = new StringBuilder();
            sb.Append("WANTED: ").Append((n.Name ?? "a band").ToUpperInvariant()).Append('\n');
            sb.Append("Posted by ").Append(NameOf(head)).Append(", headman of ").Append(vs != null ? vs.Name.ToString() : "a village").Append('.');
            string near = n.Mp != null ? NearName(n.Mp.GetPosition2D) : null;
            sb.Append("\n\n\"They came and took what they pleased. ").Append(n.Men).Append(" of them - ").Append(near != null ? "still about, near " + near : "still about").Append(". Break them and the village will pay.\"");
            sb.Append("\n\nPay: ").Append(n.Sum.ToString("N0", Inv)).Append(" denars from the village purse, paid at once to ").Append(PaidTo()).Append(" - on top of whatever the band carries.");
            sb.Append("\nTime: the notice stays up ").Append(Math.Max(0, n.Due - Today)).Append(" more days.");
            if (vs != null) sb.Append("\n\nYou must ride to ").Append(vs.Name).Append(" to speak with the headman").Append(Away(st, vs.GetPosition2D)).Append('.');
            if (n.Taken) sb.Append("\n\nYou have told the headman you will hunt them.");
            return sb.ToString();
        }

        // ------------------------------------------------------------ rozmowa we wsi

        private static int AddHeadmanDialogs(CampaignGameStarter starter)
        {
            int n = 0;
            try
            {
                starter.AddPlayerLine("arm_head_notice", "hero_main_options", "arm_head_open", "{=!}About your notice - the band harrying {ARM_HEAD_VILLAGE}...", HeadmanCond, null, 111); n++;
                starter.AddDialogLine("arm_head_open_r", "arm_head_open", "arm_head_opts", "{=!}{ARM_HEAD_OPEN}", null, null, 100); n++;
                starter.AddPlayerLine("arm_head_take", "arm_head_opts", "arm_head_taken", "{=!}I'll hunt them down.", null, CaptainTakeCons, 100); n++;
                starter.AddDialogLine("arm_head_taken_r", "arm_head_taken", "hero_main_options", "{=!}{ARM_WATCH_TAKEN}", null, null, 100); n++;
                starter.AddPlayerLine("arm_head_who", "arm_head_opts", "arm_head_who_r", "{=!}Who pays, exactly?", null, null, 99); n++;
                starter.AddDialogLine("arm_head_who_r", "arm_head_who_r", "arm_head_opts", "{=!}The village purse - what we have put by, less what we need to see the winter out. It is little, but it is honest coin.", null, null, 100); n++;
                starter.AddPlayerLine("arm_head_no", "arm_head_opts", "arm_head_no_r", "{=!}Not now.", null, null, 98); n++;
                starter.AddDialogLine("arm_head_no_r", "arm_head_no_r", "hero_main_options", "{=!}The notice stays up in town. Whoever breaks them gets the coin.", null, null, 100); n++;
            }
            catch (Exception e) { WStumble("AddHeadmanDialogs", e); }
            _hDialog = n;
            return n;
        }

        private static bool HeadmanCond()
        {
            try
            {
                if (!HeadmanOn) return false;
                var h = Hero.OneToOneConversationHero;
                if (h == null || !h.IsHeadman || h.IsPrisoner) return false;
                Notice n = null;
                foreach (var x in _notices) if (x.Kind == 1 && x.Giver == h.StringId && !x.Taken && Live(x)) { n = x; break; }
                if (n == null) return false;
                var vs = Settlement.Find(n.Village);
                string vname = vs != null ? vs.Name.ToString() : "the village";
                string near = n.Mp != null ? NearName(n.Mp.GetPosition2D) : null;
                MBTextManager.SetTextVariable("ARM_HEAD_VILLAGE", vname);
                MBTextManager.SetTextVariable("ARM_HEAD_OPEN", n.Name + " - " + n.Men + " of them. They plundered us and they are still about" + (near != null ? ", near " + near : "") + ". The village pays "
                                                + n.Sum.ToString("N0", Inv) + " to " + PaidTo() + ", coin in hand. Whatever they carry is yours.");
                MBTextManager.SetTextVariable("ARM_WATCH_TAKEN", "Gods be good to you. " + (near != null ? "They were last seen near " + near + ". " : "") + "I'll mark them for you.");
                _talkNotice = n;
                return true;
            }
            catch (Exception e) { WStumble("HeadmanCond", e); return false; }
        }

        private static string HeadmanDayText() { return ", soltysi: nowe " + _hNew + ", wyplacone " + _hPaid + ", dialog " + _hDialog + "/8"; }

        private static void ZeroHeadmanDay() { _hNew = 0; _hPaid = 0; }
    }
}
