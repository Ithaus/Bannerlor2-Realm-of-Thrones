using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// POZIOM PLAC MIASTA (Jeff 07.10: "stawka robocizny powinna byc zalezna od dobrobytu miasta"; "wszelkie koszty w danym miescie
    /// powinny byc zalezne od dobrobytu i stawek historycznych"; "wszystko, co dotyczy pieniadza, placenia musi byc spojne").
    /// Jedna regula dla kazdej zaplaty za prace ludzi w miescie: dni roboty x dniowka z cen historycznych (HistMasterWageT1 /
    /// HistMasterWagePerTier, WorkshopWagePerDay) x Index(miasto). Index = dobrobyt / TownWageRefProsperity (4800 = mediana 97 miast
    /// w tescie Jeffa 07.10), w granicach 0.5 - 1.5 (Londyn placil rzemieslnikom ok. 1.5 x prowincji). Poza miastem i przy 0 - 1.
    /// Wskaznik dziala tylko na STAWKE dniowki - nigdy na ilosc (liczba rak i popyt juz rosna z dobrobytem).
    /// Uzytkownicy: naprawy u kowali miasta (MendMaterial.LocalWage), place i utrzymanie warsztatow (WorkshopLaw, WorkshopTrade).
    /// </summary>
    internal static class TownWage
    {
        internal const float Min = 0.5f, Max = 1.5f;

        internal static float Index(Town town)
        {
            var s = Settings.Current;
            float rf = s != null ? s.TownWageRefProsperity : 0f;
            if (rf <= 0f || town == null) return 1f;
            return Math.Max(Min, Math.Min(Max, town.Prosperity / rf));
        }

        internal static float Index(Settlement st) { return Index(st != null ? st.Town : null); }
    }
}
