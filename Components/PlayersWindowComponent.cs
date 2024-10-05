using HarmonyLib;
using Sandbox.Game;
using Sandbox.Game.Gui;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using SeamlessClient.Messages;
using SeamlessClient.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeamlessClient.OnlinePlayersWindow
{

    public class PlayersWindowComponent : ComponentBase
    {
        //public static List<OnlineClientServer> allServers = new List<OnlineClientServer>();
        //public static OnlineClientServer onlineServer;


        public override void Patch(Harmony patcher)
        {
            patcher.CreateClassProcessor(typeof(OnlineNexusPlayersWindow)).Patch();
        }


        public override void Update()
        {
            if (Sync.IsServer)
            {
                MyPerGameSettings.GUI.PlayersScreen = typeof(MyGuiScreenPlayers);
            }
            else
            {
                MyPerGameSettings.GUI.PlayersScreen = typeof(OnlineNexusPlayersWindow);
            }
        }

        /*
        public static void ApplyRecievedPlayers(List<OnlineClientServer> servers, int CurrentServer)
        {
            //Seamless.TryShow($"Recieved {CurrentServer} - {servers.Count}");


            allServers.Clear();

            foreach (OnlineClientServer server in servers)
            {
                if(server.ServerID == CurrentServer)
                {
                    onlineServer = server;
                    continue;
                }

                allServers.Add(server);
            }
        }
        */






    }
}
