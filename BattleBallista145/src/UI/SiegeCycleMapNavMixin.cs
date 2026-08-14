using System;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace BattleBallista145.UI
{
    // Adds a small cycle-toggle button on the campaign-map MapBar that flips
    // SiegeCycle.Current. The button is injected via SiegeCycleMapBarExtension
    // as a sibling of any other MapBar additions (e.g. ROT AI's World Events
    // button). Position is right-anchored with MarginRight=170 so it sits to
    // the left of ROT AI's World Events (MarginRight=12, Width=150) without
    // overlapping when both mods are loaded.
    [ViewModelMixin]
    internal sealed class SiegeCycleMapNavMixin : BaseViewModelMixin<MapNavigationVM>
    {
        private string _siegeCycleLabel;

        public SiegeCycleMapNavMixin(MapNavigationVM vm) : base(vm)
        {
            SiegeCycleLabel = SiegeCycle.CurrentLabel;
        }

        [DataSourceProperty]
        public string SiegeCycleLabel
        {
            get => _siegeCycleLabel;
            set
            {
                if (_siegeCycleLabel == value) return;
                _siegeCycleLabel = value;
                OnPropertyChangedWithValue(value, nameof(SiegeCycleLabel));
            }
        }

        [DataSourceMethod]
        public void ExecuteCycleSiege()
        {
            try
            {
                SiegeCycleLabel = SiegeCycle.Next();
            }
            catch (Exception ex)
            {
                try
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Battle Ballista cycle error: " + ex.Message, Colors.Red));
                }
                catch { }
            }
        }
    }
}
