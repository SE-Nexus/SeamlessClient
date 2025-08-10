using Sandbox.Game;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeamlessClient.ServerSwitching.Commands
{
    public class MyCharacterCommands : IDisposable
    {

        public MyCharacterCommands()
        {
            MyAPIGateway.Utilities.MessageEntered += Utilities_MessageEntered;
        }

        public void Dispose()
        {
            MyAPIGateway.Utilities.MessageEntered -= Utilities_MessageEntered;
        }

        private void Utilities_MessageEntered(string messageText, ref bool sendToOthers)
        {
            if (!messageText.StartsWith("/nexus"))
                return;

            string[] cmd = messageText.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (cmd[1] == "refreshcharacter")
            {
                if (MySession.Static.LocalHumanPlayer == null)
                {
                    MyAPIGateway.Utilities?.ShowMessage("Seamless", "LocalHumanPlayer Null!");
                    return;
                }

                if (MySession.Static.LocalHumanPlayer.Character == null)
                {
                    MyAPIGateway.Utilities?.ShowMessage("Seamless", "LocalHumanPlayerCharacter Null!");
                    return;
                }


                //None of this shit works.... 5/3/2025
                MySession.Static.LocalHumanPlayer.SpawnIntoCharacter(MySession.Static.LocalHumanPlayer.Character);
                MySession.Static.LocalHumanPlayer.Controller.TakeControl(MySession.Static.LocalHumanPlayer.Character);

                MySession.Static.LocalHumanPlayer.Character.GetOffLadder();
                MySession.Static.LocalHumanPlayer.Character.Stand();

                MySession.Static.LocalHumanPlayer.Character.ResetControls();
                MySession.Static.LocalHumanPlayer.Character.UpdateCharacterPhysics(true);

                MyAPIGateway.Utilities?.ShowMessage("Seamless", "Character Controls Reset!");

            }
        }
    }
}
