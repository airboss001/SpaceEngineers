using System;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using Sandbox.Game.Weapons;
using VRage.Game.ModAPI;
using VRageMath;
using VRage.Game.Entity;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI.Interfaces;
using Sandbox.Definitions;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;

namespace thinman.AutoShutoffOreDetectors
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), false)]
    public class AutoShutoffOreDetector : MyGameLogicComponent
    {
        private IMyOreDetector detector;
        private DateTime onTime = DateTime.Now;
        private bool isEnabled = false;
        private static int shutoffAfterMinutes = 10;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
                detector = (IMyOreDetector)Entity;

                if (detector.Enabled)
                {
                    onTime = DateTime.Now;
                    isEnabled = true;
                }

                MyAPIGateway.Utilities.ShowMessage("Auto-Shutoff Ore Detectors", "Loaded...");
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine($"AutoShutoffOreDetector | ERROR   | {e}");
            }
        }

        public override void UpdateBeforeSimulation100()
        {
            if (detector.Enabled)
            {
                if (!isEnabled)
                {
                    isEnabled = true;
                    onTime = DateTime.Now;
                    return;
                }
                else
                {
                    var tm = DateTime.Now.Subtract(onTime).TotalMinutes;
                    if (tm > shutoffAfterMinutes)
                    {
                        detector.Enabled = false;
                        isEnabled = false;
                    }
                }
            }
            else
            {
                if (isEnabled)
                {
                    isEnabled = false;
                }
            }
        }
    }
}