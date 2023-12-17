using System;
using System.Text;
using HarmonyLib;
using Sandbox.Game.Entities;
using Sandbox.Game.GUI.HudViewers;
using Sandbox.Game.World;
using SeamlessClient.Utilities;
using Shared.Plugin;
using VRageMath;

namespace SeamlessClient.Components
{
    public class MyHudTimeTracker : ComponentBase
    {
        private static bool GPSEtaEnabled => Common.Config.GPSEtaEnabled;
        public override void Patch(Harmony patcher)
        {
            var AppendDistance = PatchUtils.GetMethod(typeof(MyHudMarkerRender), "AppendDistance");

            patcher.Patch(AppendDistance, postfix: new HarmonyMethod(Get(typeof(MyHudTimeTracker), nameof(ApplyTimeToTarget))));
            base.Patch(patcher);
        }

        private static void ApplyTimeToTarget(MyHudMarkerRender __instance, StringBuilder stringBuilder, double distance)
        {
            // feature switch
            if (!GPSEtaEnabled) return;
            
            if (distance < 500 || MySession.Static.LocalHumanPlayer == null || MySession.Static.LocalHumanPlayer.Character == null)
                return;

            var velocity = new Vector3(0, 0, 0);
            if (MySession.Static.LocalHumanPlayer.Character.Parent is MyCockpit cockpit)
            {
                velocity = cockpit.CubeGrid.LinearVelocity;
            }
            else
            {
                velocity = MySession.Static.LocalHumanPlayer.Character.Physics.LinearVelocity;
            }

            double v0 = velocity.Length();
            if (v0 <= 2)
                return;


            var t = Math.Round(CalculateTimeToTarget(v0, distance), 0);


            if (t <= 0)
                return;

            stringBuilder.AppendLine($" [T-{FormatDuration(t)}]");
        }

        private static string FormatDuration(double durationInSeconds)
        {
            if (durationInSeconds < 60)
            {
                return $"{durationInSeconds}s";
            }
            var minutes = (int)(durationInSeconds / 60);
            var remainingSeconds = (int)(durationInSeconds % 60);


            if (remainingSeconds > 0)
            {
                return $"{minutes}m {remainingSeconds}s";
            }
            return $"{minutes}m";
        }

        private static double CalculateTimeToTarget(double velocity, double distance)
        {
            // Check for zero velocity to avoid division by zero
            if (Math.Abs(velocity) < double.Epsilon)
            {
                throw new ArgumentException("Velocity must be non-zero.");
            }

            return distance / velocity;
        }
    }
}