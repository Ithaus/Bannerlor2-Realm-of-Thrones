using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Siege;

namespace Armoury
{
    /// <summary>
    /// 467 SZ5 "bije oddzial szturmowy, nie cala armia" (Jeff 12.11: "trzeba zrobic ze oblezenia trwaja dluzej i sa trudniejsze, np. mamy juz jak jest
    /// oblezenie to nie atakuja calym wojskiem tylko po stratach wycofuja sie i odczekuja ... Czy to tez dziala dla AI?").
    /// Dowod (logi 12.11): armia Polnocy ok. 13,5 tys. na zamek ok. 500 - 10 z 10 zamkow padlo w pierwszym szturmie po 6,0-6,6 doby, straty atakujacych
    /// 3-5% armii (14-21% oddzialu K), ponizej progu 371 (ok. 22% K). Przyczyna w SzTicks: ciosy czesci polowej (taran i wieza / wyrwa) = Math.Pow(A, 0.6),
    /// A = CALA armia (13 577 -> 302 ciosy pola, razem ok. 45 na runde zamiast 16) - oddzial szturmowy 371 zmienial tylko mianownik progu, nie sile ciosow.
    /// Zmiana: w szturmie AI kontra AI (bitwa Siege bez gracza, z rekordem szturmu OB2/371) do wzoru tur idzie Ak = min(A, K), K - oddzial szturmowy z
    /// rekordu szturmu (ten sam, wobec ktorego 371 liczy prog zalamania). A maleje z rundami, wiec oddzial jest uzupelniany z obozu (fale), az straty K
    /// przekrocza prog - wtedy obecny OB2/371 robi odwrot i przerwe 1-5 / 7-14 dob.
    /// Limit (zamrozenie wojen): po SiegeAssaultPartyStrikesRepels odparciach w tym samym oblezeniu (domyslnie 2) idzie cala armia - jak przed 467.
    /// Tylko symulacja kampanii: misje gracza i autobitwa gracza (IsPlayerMapEvent) bez zmian; bitwa z murow z odsiecza (SiegeOutside, OB3) bez zmian.
    /// Klucz SiegeAssaultPartyStrikes NIE (albo SiegeAssaultBreak NIE) = Ak = A, wzor co do bitu jak przed 467 (zostaja tylko linie logu).
    /// Wyjatek per bitwa = licznik potkniec (Stumble) i cala armia w tej bitwie, bez globalnego wylacznika.
    /// </summary>
    internal static partial class SiegeWalls
    {
        private static bool Sz5On { get { var s = Settings.Current; return s != null && s.SiegeAssaultPartyStrikes && s.SiegeAssaultBreak; } }
        private static int Sz5Limit { get { var s = Settings.Current; return s == null ? 2 : Math.Max(0, Math.Min(10, s.SiegeAssaultPartyStrikesRepels)); } }

        // liczniki doby 467
        private static int _d5Party, _d5Limit, _d5Player, _d5NoRec, _d5Off, _d5Rounds, _d5Cut, _d5Won, _d5Rep, _d5Other;

        /// <summary>467: przebieg jednego oblezenia (tylko log - linia "Oblezenie (467)" przy koncu oblezenia).</summary>
        private sealed class Siege5 { public string Fac = "-"; public int Assaults, Party, Full, Repelled, Won, Other, Rounds, Cut; public long CasA, CasD; }
        private static readonly Dictionary<SiegeEvent, Siege5> _sz5 = new Dictionary<SiegeEvent, Siege5>();

        /// <summary>467: liczba atakujacych do wzoru tur rundy (AssaultTicks). Decyzja raz na bitwe (pierwsza runda), potem Ak = min(A, K).</summary>
        private static int Sz5Party(MapEvent me, Bat b, int A)
        {
            if (b.Sz5 < 0) Sz5Decide(me, b);
            if (b.Sz5 != 1) return A;
            b.Sz5Rounds++; _d5Rounds++;
            if (A > b.K5) { b.Sz5Cut++; _d5Cut++; return b.K5; }
            return A;
        }

        private static void Sz5Decide(MapEvent me, Bat b)
        {
            b.Sz5 = 0;
            try
            {
                var st = b.St;
                b.Ev5 = st != null ? st.SiegeEvent : null;
                if (b.Player) { b.Sz5Why = "bitwa gracza - bez zmian"; _d5Player++; return; }
                Assault a;
                if (!_assaults.TryGetValue(me, out a) || a == null) { b.Sz5Why = "bez rekordu szturmu OB2"; _d5NoRec++; return; }
                b.Fac5 = a.Fac;
                if (!Sz5On) { b.Sz5Why = "klucz NIE"; _d5Off++; return; }
                if (a.Off || a.K <= 0 || RepelLoss <= 0f || a.Thr <= 0f) { b.Sz5Why = "bez progu zalamania (OB2/371 nieczynne)"; _d5NoRec++; return; }
                int lim = Sz5Limit;
                if (lim > 0 && b.Repels >= lim) { b.Sz5Why = "limit: odparc w tym oblezeniu " + b.Repels + " >= " + lim + " - idzie cala armia"; _d5Limit++; return; }
                b.K5 = a.K; b.Sz5 = 1; b.Sz5Why = null; _d5Party++;
            }
            catch (Exception e) { Stumble("Sz5Decide", e); b.Sz5 = 0; b.Sz5Why = "potkniecie - cala armia"; }
        }

        /// <summary>467: z SzEnd - wynik bitwy do licznikow doby i przebiegu oblezenia. res: 0 zdobyte, 1 odparty OB2, inne - reszta.</summary>
        private static void Sz5End(Bat b, int res, int casA, int casD)
        {
            try
            {
                if (b.Sz5 < 0 || b.Player) return;                                 // bitwa bez rund SZ albo gracza
                if (b.Sz5 == 1) { if (res == 0) _d5Won++; else if (res == 1) _d5Rep++; else _d5Other++; }
                var ev = b.Ev5;
                if (ev == null) return;
                Siege5 s;
                if (!_sz5.TryGetValue(ev, out s)) { s = new Siege5(); _sz5[ev] = s; }
                if (b.Fac5 != null) s.Fac = b.Fac5;
                s.Assaults++;
                if (b.Sz5 == 1) s.Party++; else s.Full++;
                if (res == 0) s.Won++; else if (res == 1) s.Repelled++; else s.Other++;
                s.Rounds += b.Sz5Rounds; s.Cut += b.Sz5Cut; s.CasA += casA; s.CasD += casD;
            }
            catch (Exception e) { Stumble("Sz5End", e); }
        }

        /// <summary>467: fragment linii "Bitwa szturmu (SZ)".</summary>
        private static string Sz5Text(Bat b)
        {
            if (b.Sz5 == 1) return " | SZ5 (467) oddzial " + b.K5 + " / " + b.A0 + " (ciosy z oddzialu, nie z calej armii), rund z oddzialem " + b.Sz5Rounds + ", przycietych " + b.Sz5Cut;
            return " | SZ5 (467) cala armia (" + (b.Sz5 < 0 ? "bitwa z murow z odsiecza - bez SZ5" : b.Sz5Why ?? "-") + ")";
        }

        /// <summary>467: fragment linii doby "Mury oblezen (OB0)".</summary>
        private static string Sz5DayText()
        {
            return " | SZ5 (467): klucz " + (Sz5On ? "TAK" : "NIE") + ", limit odparc " + (Sz5Limit > 0 ? Sz5Limit.ToString() : "brak") + " | szturmy AI z oddzialem " + _d5Party
                 + " (zdobyte " + _d5Won + ", odparte " + _d5Rep + ", inne " + _d5Other + "), cala armia: po limicie " + _d5Limit + ", klucz NIE " + _d5Off
                 + ", bez rekordu " + _d5NoRec + ", gracz " + _d5Player + " | rund z oddzialem " + _d5Rounds + ", przycietych " + _d5Cut + " | oblezenia w pamieci " + _sz5.Count;
        }

        private static void ZeroDay467() { _d5Party = _d5Limit = _d5Player = _d5NoRec = _d5Off = _d5Rounds = _d5Cut = _d5Won = _d5Rep = _d5Other = 0; }

        /// <summary>467: z OnSiegeEnded - linia przebiegu oblezenia (tylko gdy byl szturm AI z rundami SZ). Nie rzuca.</summary>
        private static void Sz5SiegeEnded(SiegeEvent ev)
        {
            Siege5 s;
            if (ev == null || !_sz5.TryGetValue(ev, out s)) return;
            _sz5.Remove(ev);
            try
            {
                if (!LogOn) return;
                var st = ev.BesiegedSettlement;
                var sb = new StringBuilder(500);
                sb.Append("Oblezenie (467): dzien ").Append(Today).Append(" | osada ").Append(st != null ? st.StringId : "-").Append(" (").Append(Nm(st)).Append(", ")
                  .Append(st != null && st.IsTown ? "miasto" : "zamek").Append(") | oblegajacy ").Append(s.Fac)
                  .Append(" | czas oblezenia ").Append(F(ev.SiegeStartTime.ElapsedDaysUntilNow, "0.0")).Append(" dob")
                  .Append(" | szturmow ").Append(s.Assaults).Append(" (z oddzialem ").Append(s.Party).Append(", cala armia ").Append(s.Full).Append(')')
                  .Append(" | zdobyte ").Append(s.Won).Append(", odparte (odstapienie po stratach) ").Append(s.Repelled).Append(", inne ").Append(s.Other)
                  .Append(" | rund z oddzialem ").Append(s.Rounds).Append(", przycietych ").Append(s.Cut)
                  .Append(" | straty w szturmach: atakujacy ").Append(s.CasA).Append(", obroncy ").Append(s.CasD)
                  .Append(", wymiana ").Append(F(s.CasA / (double)Math.Max(1L, s.CasD), "0.00"))
                  .Append(" | klucz ").Append(Sz5On ? "TAK" : "NIE").Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Sz5SiegeEnded", e); }
        }

        /// <summary>467: z Daily - przebiegi oblezen, ktore skonczyly sie bez OnSiegeEventEnded.</summary>
        private static void Sz5Clean()
        {
            if (_sz5.Count == 0) return;
            List<SiegeEvent> dead = null;
            foreach (var kv in _sz5) if (kv.Key == null || kv.Key.BesiegedSettlement == null || kv.Key.BesiegedSettlement.SiegeEvent != kv.Key) { if (dead == null) dead = new List<SiegeEvent>(); dead.Add(kv.Key); }
            if (dead != null) foreach (var ev in dead) _sz5.Remove(ev);
        }
    }
}
