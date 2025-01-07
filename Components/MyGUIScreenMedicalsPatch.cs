using HarmonyLib;
using Sandbox.Game.GUI.HudViewers;
using Sandbox.Game.Localization;
using Sandbox.Graphics.GUI;
using SeamlessClient.Utilities;
using SpaceEngineers.Game.GUI;
using SpaceEngineers.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VRage;
using VRage.Collections;

namespace SeamlessClient.Components
{
    public class MyGUIScreenMedicalsPatch : ComponentBase
    {


        public override void Patch(Harmony patcher)
        {
            //var addSuit = PatchUtils.GetMethod(typeof(MyGuiScreenMedicals), "<RefreshMedicalRooms>g__AddSuitRespawn|86_4");

            var spawninsuit = PatchUtils.GetMethod(typeof(MyGuiScreenMedicals), "RefreshMedicalRooms");
            patcher.Patch(spawninsuit, postfix: new HarmonyMethod(Get(typeof(MyGUIScreenMedicalsPatch), nameof(RefreshMedicals))));



            base.Patch(patcher);
        }

        private static void RefreshMedicals(MyGuiScreenMedicals __instance, ListReader<MySpaceRespawnComponent.MyRespawnPointInfo> medicalRooms, object planetInfos)
        {
            if (!Seamless.isSeamlessServer)
                return;

            MyGuiControlTable myGuiControlTable = (MyGuiControlTable)typeof(MyGuiScreenMedicals).GetField("m_respawnsTable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(__instance);
            if (myGuiControlTable == null)
                return;


            string s = MyTexts.GetString(MySpaceTexts.SpawnInSpaceSuit);
            foreach (var item in myGuiControlTable.Rows)
            {
                if (item.GetCell(0) == null)
                    continue;

                if (item.GetCell(0).Text.ToString().Contains(s))
                {
                    item.GetCell(0).Text.Clear();
                    item.GetCell(0).Text.Append("Nexus Lobby");
                }
            }
            
           



        }

    }
}
