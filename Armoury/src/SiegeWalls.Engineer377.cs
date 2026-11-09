using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 377 (Jeff 09.11: "dlaczego jak bronie zamku i budje machiny nie dostaje engenering?" -> "tak rob 377"). Dowod (dekompilacja 1.4.8, cs_full.cs):
    /// SiegeEventCampaignBehavior.OnSiegeEngineBuilt bierze aktywny model SiegeEventModel.GetEffectiveSiegePartyForSide; dla obroncow DefaultSiegeEventModel
    /// wybiera partie z bohaterem o NAJWYZSZYM Engineering EffectiveEngineer, warunek ostry (num2 &gt; num od 0) - gdy wszyscy maja 0, wynik null i gra
    /// wychodzi przed XP (30 + 2 x Difficulty, DefaultSkillLevelingManager.OnSiegeEngineBuilt -&gt; rola Engineer). Log Jeffa (Goldentooth): zero XP z
    /// "armia i oblezenie" przez cala obrone. BK modelu nie podmienia (ma tylko wlasna prywatna statyczna).
    /// Zasada: machina OBRONCOW zbudowana (takze przez druga ekipe SiegeWalls2 - CreateSiegeObject wola to samo zdarzenie), model zwrocil null, a w osadzie
    /// stoi partia gracza tej samej frakcji co osada -&gt; SkillLevelingManager.OnSiegeEngineBuilt(MainParty, typ): ten sam wzor co gra (rola Engineer,
    /// GetEffectiveRoleHolder), a ksiega XP (SkillLedger) zalicza to do "armia i oblezenie" po nazwie metody. Tylko XP - wybor strony (perki, tempo budowy,
    /// ostrzal, ofiary) bez zmian; Apprenticeship gra daje tylko przy wyniku nie-null - nie dokladamy. Gdy model zwroci partie (ktos ma Engineering &gt; 0),
    /// XP daje gra - my nic (bez podwojnego liczenia). Wyjatek = potkniecie, bez globalnego wylacznika.
    /// </summary>
    internal static partial class SiegeWalls
    {
        private static bool Eng377On { get { var s = Settings.Current; return s != null && s.SiegeDefenderEngineeringXp; } }

        // liczniki doby i kampanii 377 (kampanii: zerowane w Reset - nowa kampania i kazde wczytanie)
        private static int _dEng377Built, _dEng377Given, _dEng377Game, _sEng377Given;
        // 377 po recenzji: machiny czekajace na XP. Sluchacze MbEvent ida od OSTATNIO dopisanego (cs_full.cs:22805 - LIFO), a ArmouryBehavior rejestruje sie
        // po zachowaniach gry, wiec nasz OnEngineBuilt biegnie PRZED SiegeEventCampaignBehavior.OnSiegeEngineBuilt. XP dane od razu (>= 32, prog poziomu 1
        // = 30) podnosi inzyniera na Engineering 1 i gra zaraz potem wybralaby MainParty i dala XP drugi raz. Dlatego: decyzja tu (model pytany przed
        // jakimkolwiek XP - wynik prawdziwy), samo XP w najblizszym ticku kampanii (Tick377), gdy sluchacz gry juz skonczyl.
        private static readonly System.Collections.Generic.List<KeyValuePair377> _eng377Pending = new System.Collections.Generic.List<KeyValuePair377>();
        private sealed class KeyValuePair377 { public Settlement St; public SiegeEngineType T; }
        private static float _dEng377Xp, _sEng377Xp;
        private static string _dEng377Who = "-", _dEng377GameWho = "-";

        /// <summary>Z OnEngineBuilt (strona obrony). Nie rzuca.</summary>
        private static void OnDefenderEngineBuilt377(SiegeEvent ev, SiegeEngineType t)
        {
            try
            {
                if (!Eng377On || ev == null || t == null) return;
                var st = ev.BesiegedSettlement;
                if (st == null) return;
                // tylko obrona osady, w ktorej stoi gracz tej samej frakcji (jak Town.GetNextDefenderParty) - inne osady swiata bez zmian
                var mp = MobileParty.MainParty;
                if (mp == null || !mp.IsActive || mp.CurrentSettlement != st || !st.Parties.Contains(mp) || mp.MapFaction == null || mp.MapFaction != st.MapFaction
                    || (Hero.MainHero != null && Hero.MainHero.IsPrisoner)) return;
                _dEng377Built++;
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SiegeEventModel : null;
                if (model == null) return;
                var eff = model.GetEffectiveSiegePartyForSide(ev, BattleSideEnum.Defender);
                if (eff != null)
                {
                    // gra dala XP sama (ktos ma Engineering > 0) - tylko do logu
                    _dEng377Game++;
                    _dEng377GameWho = eff.LeaderHero != null ? eff.LeaderHero.Name.ToString() : (eff.Name != null ? eff.Name.ToString() : "-");
                    return;
                }
                if (_eng377Pending.Count < 16) _eng377Pending.Add(new KeyValuePair377 { St = st, T = t });   // XP w Tick377 - po sluchaczu gry
            }
            catch (Exception e) { Stumble("Engineer377", e); }
        }

        /// <summary>Z CampaignEvents.TickEvent: XP za machiny odlozone w OnDefenderEngineBuilt377 (sluchacz gry juz skonczyl). Pusta lista = jedno porownanie.</summary>
        internal static void Tick377()
        {
            if (_eng377Pending.Count == 0) return;
            var list = _eng377Pending.ToArray();
            _eng377Pending.Clear();
            foreach (var p in list)
            {
                try
                {
                    if (!Eng377On || p == null || p.St == null || p.T == null) continue;
                    // warunki jeszcze raz: gracz dalej aktywny, wolny, w tej osadzie i tej samej frakcji
                    var mp = MobileParty.MainParty;
                    if (mp == null || !mp.IsActive || mp.CurrentSettlement != p.St || !p.St.Parties.Contains(mp) || mp.MapFaction == null || mp.MapFaction != p.St.MapFaction
                        || (Hero.MainHero != null && Hero.MainHero.IsPrisoner)) continue;
                    Hero who = null;
                    try { who = mp.GetEffectiveRoleHolder(PartyRole.Engineer); } catch { }
                    float xp = 30f + 2f * p.T.Difficulty;                        // DefaultSkillLevelingManager.OnSiegeEngineBuilt - tylko do logu
                    SkillLevelingManager.OnSiegeEngineBuilt(mp, p.T);            // ten sam wzor i rola co gra; ksiega XP: "armia i oblezenie"
                    _dEng377Given++; _sEng377Given++;
                    _dEng377Xp += xp; _sEng377Xp += xp;
                    _dEng377Who = who != null ? who.Name.ToString() : "-";
                }
                catch (Exception e) { Stumble("Engineer377.Tick", e); }
            }
        }

        /// <summary>Linia doby "Inzynieria obroncow (377)" - tylko gdy w dobie obroncy osady z graczem zbudowali machine.</summary>
        private static void Eng377Daily()
        {
            try
            {
                if (_dEng377Built == 0) return;
                Log.Info("Inzynieria obroncow (377): dzien " + Today + " | klucz " + (Eng377On ? "tak" : "nie") + " | machin obroncow zbudowanych (osada z graczem) " + _dEng377Built
                         + " | XP od gry (ktos z Engineering > 0) " + _dEng377Game + (_dEng377Game > 0 ? " (ostatnio " + _dEng377GameWho + ")" : "")
                         + " | XP od nas (nikt > 0, gracz w osadzie) " + _dEng377Given + ", razem " + F(_dEng377Xp, "0") + " XP Engineering, komu " + _dEng377Who
                         + " | kampania (od wczytania): " + _sEng377Given + " machin, " + F(_sEng377Xp, "0") + " XP.");
            }
            catch (Exception e) { Stumble("Eng377Daily", e); }
        }

        /// <summary>Z SiegeWalls.Reset (nowa kampania i kazde wczytanie): liczniki kampanii i kolejka od zera.</summary>
        private static void Reset377() { _sEng377Given = 0; _sEng377Xp = 0f; _eng377Pending.Clear(); ZeroDay377(); }

        private static void ZeroDay377() { _dEng377Built = _dEng377Given = _dEng377Game = 0; _dEng377Xp = 0f; _dEng377Who = "-"; _dEng377GameWho = "-"; }
    }
}
