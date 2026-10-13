using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// M6a-2 (raport roczny 13.10, tabela 6.2 wiersz 18; DECYZJA z pelnymi uprawnieniami): OKUP POWTORNY TANSZY. Klucz RansomRepeatCheaper (dziala z okupami
    /// wedlug majatku 178 - LordRansomByIncome i drabina dlugu).
    /// Bieg roczny: 1 260 okupow za 52.3 mln; 546 osob, 53 osoby 5+ razy (329 okupow); ta sama osoba wykupiona ponownie po medianie 47 dob (kwartyle 23 / 94),
    /// 17% okupow w 28 dob od poprzedniego okupu tej osoby, 33% w 56 dobach. Glowa rodu Kasgos 11 razy (doby 52, 58, 73, 85, 100, 115, 137, 149...), rod Kasgos
    /// 31 okupow za 787 103 zl = 57% rocznych wplywow (zloto 222 570 -> 14 295). Para Kasgos (Norvos) - Ostyrion (Qohor) wykupuje sie nawzajem: 279 bitew
    /// lordow, zero zmian na mapie.
    /// Przyczyna (kod): Ransom178.PriceOf liczy cene z dochodu rodu (glowa RansomHeadYears x 364 x D, lord RansomLordDays x D, krol RansomKingDays x wplywy
    /// korony) - bez pamieci, ze rodzina zaplacila wlasnie okup za te sama osobe. Gra tez jej nie ma (EndCaptivityLogEntry tylko w dzienniku).
    /// Wybor (zamiast "przerwy w domu po wykupie"): przerwa trzymalaby wykupionego lorda bez druzyny - przy 1 260 okupach rocznie i przerwie 14 dob to ok.
    /// 48 lordow naraz poza polem (rzad 2-5 tys. ludzi mniej w druzynach) - obniza wojsko w polu. Tanszy okup nie zabiera nikogo z pola.
    /// Regula: okup tej samej osoby w RansomRepeatDays (56) dob od jej poprzedniego okupu kosztuje czesc ceny, rosnaca liniowo od RansomRepeatFloor (1/4)
    /// tuz po poprzednim okupie do pelnej ceny po 56 dobach: cena x (podloga + (1 - podloga) x dni / 56). Wszystkie drogi 178 (AI-AI, kurier, posrednik,
    /// ekran druzyny, nagroda za wielkiego jenca) - przez PriceOf. Okup samego gracza (PlayerPrice) bez zmian.
    /// Zloto: ta sama droga (rodzina jenca / skarbiec krola -> porywacz / korona), mniejsza kwota - nic nie powstaje i nic nie znika. Rachunek na biegu
    /// rocznym: 5.25 mln w roku (14.4 tys. dziennie, 10% okupow) zostaje u rodzin jencow; Kasgos zatrzymuje 324 tys. z 787 tys.
    /// Pamiec: doba ostatniego okupu osoby (zdarzenie gry HeroPrisonerReleased z EndCaptivityDetail.Ransom - kazda droga okupu) - zapis: segment 2 klucza
    /// "arm_m6a"; pamiec jest prowadzona zawsze, cena zmienia sie tylko przy kluczu. Klucz wylaczony = stan 4d33042 (mnoznik 1).
    /// Linia "Okupy powtorne (M6a-2)" (liczniki doby i sesji).
    /// </summary>
    internal static class RansomRepeat
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.RansomRepeatCheaper && Ransom178.On; } }
        private static int Window { get { var s = Settings.Current; return s != null ? Math.Max(1, Math.Min(364, s.RansomRepeatDays)) : 56; } }
        private static double Floor { get { var s = Settings.Current; return s != null ? Math.Max(0.0, Math.Min(1.0, s.RansomRepeatFloor)) : 0.25; } }

        private static readonly Dictionary<string, int> _last = new Dictionary<string, int>();   // id bohatera -> doba ostatniego okupu

        // liczniki doby i sesji
        private static int _dRec, _dN, _dRep, _sN, _sRep;
        private static long _dFull, _dPaid, _sFull, _sPaid;
        private static int _stumbles, _importN = -1, _importBad;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static void Reset()
        {
            _last.Clear(); _dRec = _dN = _dRep = _sN = _sRep = 0; _dFull = _dPaid = _sFull = _sPaid = 0;
            _stumbles = 0; _err.Clear(); _importN = -1; _importBad = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("RansomRepeat." + where, e); } catch { }
        }

        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }

        /// <summary>Mnoznik ceny okupu tej osoby: 1 bez klucza, bez wpisu albo po Window dobach; inaczej podloga + (1 - podloga) x dni / Window.</summary>
        internal static double Factor(Hero h)
        {
            try
            {
                int d;
                if (!On || h == null || h.StringId == null || !_last.TryGetValue(h.StringId, out d)) return 1.0;
                int ago = Math.Max(0, Today - d), w = Window;
                if (ago >= w) return 1.0;
                double f = Floor;
                return f + (1.0 - f) * ago / w;
            }
            catch (Exception e) { Stumble("Factor", e); return 1.0; }
        }

        /// <summary>Ransom178.Execute (AI-AI i kurier): okup wykonany - cena pelna (bez mnoznika) i zaplacona (gotowka + dlug).</summary>
        internal static void Note(long full, long paid, bool repeat)
        {
            _dN++; _sN++;
            if (!repeat) return;
            _dRep++; _sRep++; _dFull += full; _dPaid += paid; _sFull += full; _sPaid += paid;
        }

        /// <summary>Zdarzenie gry HeroPrisonerReleased: kazdy okup (EndCaptivityDetail.Ransom) zapisuje dobe - kazda droga (AI-AI, kurier, posrednik, gracz).</summary>
        internal static void OnReleased(Hero h, PartyBase party, IFaction f, EndCaptivityDetail detail, bool show)
        {
            try
            {
                if (detail != EndCaptivityDetail.Ransom || h == null || h.StringId == null || h.StringId.IndexOfAny(Bad) >= 0) return;
                _last[h.StringId] = Today; _dRec++;
            }
            catch (Exception e) { Stumble("OnReleased", e); }
        }

        /// <summary>Linia doby (po linii "Okupy (178)"); wpisy starsze niz Window - usuwane.</summary>
        internal static void Report()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                int today = Today, w = Window;
                var old = new List<string>();
                foreach (var kv in _last) if (today - kv.Value >= w) old.Add(kv.Key);
                foreach (var id in old) _last.Remove(id);
                if (s.LogEnabled)
                    Log.Info("Okupy powtorne (M6a-2): dzien " + today + " | klucz Ransom Repeat Cheaper " + (On ? "tak" : "nie") + " (okno " + w + " dob, podloga "
                             + Floor.ToString("0.00", Inv) + " ceny; okupy 178 " + (Ransom178.On ? "tak" : "nie") + ")"
                             + " | okupy dzis (AI-AI i kurier) " + _dN + ", w tym powtorne w oknie " + _dRep + ": cena pelna " + _dFull + ", zaplacona " + _dPaid
                             + ", zostalo u rodzin jencow " + (_dFull - _dPaid)
                             + " | sesja: okupy " + _sN + ", powtorne " + _sRep + ", cena pelna " + _sFull + ", zaplacona " + _sPaid + ", zostalo u rodzin " + (_sFull - _sPaid)
                             + " | pamiec okupow: zapisane dzis " + _dRec + ", osob w oknie " + _last.Count
                             + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + (_importN >= 0 ? " | wczytano wpisow " + _importN + " (bledne " + _importBad + ")" : "") + ".");
                _importN = -1;
            }
            catch (Exception e) { Stumble("Report", e); }
            _dRec = _dN = _dRep = 0; _dFull = _dPaid = 0;
        }

        // ------------------------------------------------------------ zapis (segment 2 klucza "arm_m6a"): "idBohatera:doba;..."
        private static readonly char[] Bad = { '|', ';', ':' };

        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(_last.Count * 24 + 4);
                bool first = true;
                foreach (var kv in _last)
                {
                    if (kv.Key.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(kv.Key).Append(':').Append(kv.Value.ToString(Inv));
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _last.Clear(); _importN = 0; _importBad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                foreach (var p in data.Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); int d;
                    if (x.Length == 2 && x[0].Length > 0 && int.TryParse(x[1], NumberStyles.Integer, Inv, out d)) { _last[x[0]] = d; _importN++; }
                    else _importBad++;
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
