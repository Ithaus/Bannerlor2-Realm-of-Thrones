using System;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using BannerKings.UI.Crafting;
using TaleWorlds.Library;

namespace ForgeView
{
    /// <summary>Dane, ktorych ItemTableauWidget potrzebuje do narysowania zbroi w 3D
    /// + stan wzoru (znany/nieznany) do kolorowego oznaczenia jak przy broniach.</summary>
    [ViewModelMixin]
    internal sealed class ArmourItemMixin : BaseViewModelMixin<ArmorItemVM>
    {
        public ArmourItemMixin(ArmorItemVM vm) : base(vm) { }

        [DataSourceProperty]
        public string FvItemId
        {
            get { try { return ViewModel != null && ViewModel.Item != null ? ViewModel.Item.StringId : ""; } catch { return ""; } }
        }

        [DataSourceProperty] public string FvModifierId { get { return ""; } }
        [DataSourceProperty] public string FvBannerCode { get { return ""; } }

        // ---- wzor znany czy nie (Armoury.RangedLore przez reflection, bez twardej zaleznosci) ----
        private static MethodInfo _knownOf;
        private static bool _knownLooked;

        private static bool KnownOf(TaleWorlds.Core.ItemObject item)
        {
            try
            {
                if (!_knownLooked)
                {
                    _knownLooked = true;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType("Armoury.RangedLore", false);
                        if (t != null) { _knownOf = t.GetMethod("KnownOf", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static); break; }
                    }
                    // E8/M (247): jedna linia przy pierwszym odczycie; fallback bez zmian
                    Log.Info("Armoury RangedLore (kafelek wzoru): " + (_knownOf != null ? "PODPIETY (Armoury.RangedLore.KnownOf)"
                             : "BRAK (Armoury.RangedLore.KnownOf) - zostaje: kazdy wzor odblokowany"));
                }
                if (_knownOf == null || item == null) return true;
                return _knownOf.Invoke(null, new object[] { item }) is bool b && b;
            }
            catch { return true; }
        }

        [DataSourceProperty]
        public bool FvLocked
        {
            get { try { return ViewModel != null && ViewModel.Item != null && !KnownOf(ViewModel.Item); } catch { return false; } }
        }

        // ---- m96-wzory: znany, ale kowadlo odmowi (Armoury.PatternGate.BlockText: prog umiejetnosci kowala z ekranu, jedyna legenda, rzadki material) ----
        private static MethodInfo _blockText;
        private static FieldInfo _fCrafting;
        private static bool _blockLooked;
        private bool _blockDone;
        private string _block;
        private TaleWorlds.CampaignSystem.Hero _blockHero;
        private int _blockSkill = -1;

        /// <summary>Napis blokady albo null. Po recenzji: pamiec na pare kowal + jego Smithing - przy innym kowalu w ekranie albo po przekroczeniu
        /// progu w tej samej wizycie liczone od nowa (lista BK NIE powstaje od nowa przy zmianie kowala: CraftingVM.UpdateCraftingHero odswieza tylko
        /// projektowanie, uszlachetnianie i przetop). Gauntlet czyta wlasciwosc przy wiazaniu - po przebudowie listy (filtr, odswiezenie BK)
        /// albo wyborze kafelka; bez alokacji przy trafieniu w pamiec.</summary>
        private string Block()
        {
            try
            {
                if (!_blockLooked)
                {
                    _blockLooked = true;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType("Armoury.PatternGate", false);
                        if (t != null) { _blockText = t.GetMethod("BlockText", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static); break; }
                    }
                    _fCrafting = typeof(ArmorItemVM).GetField("armorCrafting", BindingFlags.NonPublic | BindingFlags.Instance);
                    Log.Info("Armoury PatternGate (kafelek: znany, ale za trudny - m96-wzory): " + (_blockText != null ? "PODPIETY (Armoury.PatternGate.BlockText)"
                             : "BRAK (Armoury.PatternGate.BlockText) - zostaje: znany wzor zawsze PATTERN KNOWN")
                             + ", kowal z ekranu " + (_fCrafting != null ? "tak (ArmorItemVM.armorCrafting.Hero, pamiec na kowala i jego Smithing)" : "nie - gracz") + ".");
                }
                if (_blockText == null || ViewModel == null || ViewModel.Item == null) return null;
                TaleWorlds.CampaignSystem.Hero smith = null;
                try { var ac = _fCrafting != null ? _fCrafting.GetValue(ViewModel) as ArmorCraftingVM : null; if (ac != null) smith = ac.Hero; } catch { }
                int skill = -1;
                try
                {
                    var h = smith ?? TaleWorlds.CampaignSystem.Hero.MainHero;
                    if (h != null) skill = h.GetSkillValue(TaleWorlds.Core.DefaultSkills.Crafting);
                }
                catch { }
                if (_blockDone && ReferenceEquals(smith, _blockHero) && skill == _blockSkill) return _block;
                _blockDone = true;
                _blockHero = smith;
                _blockSkill = skill;
                _block = _blockText.Invoke(null, new object[] { ViewModel.Item, smith }) as string;
            }
            catch { _block = null; }
            return _block;
        }

        /// <summary>m96-wzory: znany wzor, ktorego kowal przy kowadle jeszcze nie moze zaczac - lzejsza plachta na wierszu listy.</summary>
        [DataSourceProperty]
        public bool FvBlocked
        {
            get { try { return !FvLocked && Block() != null; } catch { return false; } }
        }

        /// <summary>Bursztynowy napis w podgladzie: znany, ale za trudny - albo (po recenzjach) rzadki material, takze bez wzoru.</summary>
        [DataSourceProperty]
        public bool FvBlockShown
        {
            get { try { return Block() != null; } catch { return false; } }
        }

        [DataSourceProperty]
        public string FvBlockText
        {
            get { try { return Block() ?? ""; } catch { return ""; } }
        }

        /// <summary>Czerwony "PATTERN NOT LEARNED": wzor nieznany i nic innego go nie zamyka (rzadki material ma wlasny napis - nauka go nie otworzy).</summary>
        [DataSourceProperty]
        public bool FvNotLearned
        {
            get { try { return FvLocked && Block() == null; } catch { return false; } }
        }

        [DataSourceProperty]
        public bool FvKnown
        {
            get { return !FvLocked && !FvBlocked; }
        }
    }
}
