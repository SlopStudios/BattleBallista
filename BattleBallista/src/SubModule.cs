using System;
using Bannerlord.UIExtenderEx;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BattleBallista
{
    public class SubModule : MBSubModuleBase
    {
        private readonly UIExtender _extender = UIExtender.Create("BattleBallista");

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                _extender.Register(typeof(SubModule).Assembly);
                _extender.Enable();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Battle Ballista: UIExtender register error — " + ex.Message, Colors.Red));
            }
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            // Attach to every mission. The behavior gates internally on whether
            // a player agent is active, so attaching to lobby/cutscene missions
            // is harmless.
            mission.AddMissionBehavior(new BattleBallistaMissionBehavior());
        }
    }
}
