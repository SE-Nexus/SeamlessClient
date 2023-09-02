﻿using HarmonyLib;
using NLog.Fluent;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using SeamlessClient.Messages;
using SeamlessClient.OnlinePlayersWindow;
using SeamlessClient.ServerSwitching;
using SeamlessClient.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VRage.GameServices;
using VRage.Plugins;
using VRage.Sync;
using VRage.Utils;

namespace SeamlessClient
{
    public class Seamless : IPlugin
    {
        public static Version SeamlessVersion => typeof(Seamless).Assembly.GetName().Version;
        public static Version NexusVersion = new Version(1, 0, 0);
        private static Harmony SeamlessPatcher;
        public static ushort SeamlessClientNetId = 2936;

        private List<ComponentBase> allComps = new List<ComponentBase>();
        private Assembly thisAssembly => typeof(Seamless).Assembly;
        private bool Initilized = false;
        public static bool isSeamlessServer = false;

        public static bool isDebug = true;



        public void Init(object gameInstance)
        {
            TryShow($"Running Seamless Client Plugin v[{SeamlessVersion}]");
            SeamlessPatcher = new Harmony("SeamlessClientPatcher");
            GetComponents();
            

            PatchComponents(SeamlessPatcher);
        }


        private void GetComponents()
        {
            int failedCount = 0;
            foreach (Type type in thisAssembly.GetTypes())
            {

                if (type.BaseType != typeof(ComponentBase))
                    continue;

                try
                {
                    ComponentBase s = (ComponentBase)Activator.CreateInstance(type);
                    allComps.Add(s);

                }
                catch (Exception ex)
                {
                    failedCount++;

                    TryShow(ex, $"{type.FullName} failed to load!");
                }
            }
        }

        private void PatchComponents(Harmony patcher)
        {
            foreach (ComponentBase component in allComps)
            {
                try
                {
                    patcher.CreateClassProcessor(component.GetType()).Patch();
                    component.Patch(patcher);
                    TryShow($"Patched {component.GetType()}");

                }
                catch (Exception ex)
                {
                    TryShow(ex, $"Failed to Patch {component.GetType()}");
                }
            }
        }

        private void InitilizeComponents()
        {
            foreach(ComponentBase component in allComps)
            {
                try
                {
                    component.Initilized();
                    TryShow($"Initilized {component.GetType()}");

                }catch(Exception ex)
                {
                    TryShow(ex, $"Failed to initialize {component.GetType()}");
                }
            }
        }


        private static void MessageHandler(ushort packetID, byte[] data, ulong sender, bool fromServer)
        {
            //Ignore anything except dedicated server
            Seamless.TryShow("1!");
            if (!fromServer || sender == 0)
                return;

            Seamless.TryShow("2!");
            ClientMessage msg = MessageUtils.Deserialize<ClientMessage>(data);
            if (msg == null)
                return;

            Seamless.TryShow("3!");
            //Get Nexus Version
            if (!string.IsNullOrEmpty(msg.NexusVersion))
                NexusVersion = Version.Parse(msg.NexusVersion);

            Seamless.TryShow("4!");

            switch (msg.MessageType)
            {
                case ClientMessageType.FirstJoin:
                    Seamless.TryShow("Sending First Join!");
                    ClientMessage response = new ClientMessage(SeamlessVersion.ToString());
                    MyAPIGateway.Multiplayer?.SendMessageToServer(SeamlessClientNetId, MessageUtils.Serialize(response));
                    break;

                case ClientMessageType.TransferServer:
                    StartSwitch(msg.GetTransferData());
                    break;

                case ClientMessageType.OnlinePlayers:
                    //Not implemented yet
                    var playerData = msg.GetOnlinePlayers();
                    PlayersWindowComponent.ApplyRecievedPlayers(playerData.OnlineServers, playerData.currentServerID);
                    break;
            }
        }



        public void Dispose()
        {
            
        }



        public void Update()
        {
            if (MyAPIGateway.Multiplayer == null) 
                return;

            if (!Initilized)
            {
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(SeamlessClientNetId, MessageHandler);
                InitilizeComponents();

                Initilized = true;
            }
        }



        public static void StartSwitch(TransferData targetServer)
        {
            if (targetServer.TargetServerId == 0)
            {
                Seamless.TryShow("This is not a valid server!");
                return;
            }

            var server = new MyGameServerItem
            {
                ConnectionString = targetServer.IpAddress,
                SteamID = targetServer.TargetServerId,
                Name = targetServer.ServerName
            };


            Seamless.TryShow($"Beginning Redirect to server: {targetServer.TargetServerId}");
            var world = targetServer.WorldRequest.DeserializeWorldData();
            ServerSwitcherComponent.Instance.StartBackendSwitch(server, world);
        }








        public static void TryShow(string message)
        {
            if (MySession.Static?.LocalHumanPlayer != null && isDebug)
                MyAPIGateway.Utilities?.ShowMessage("Seamless", message);

            MyLog.Default?.WriteLineAndConsole($"SeamlessClient: {message}");
        }

        public static void TryShow(Exception ex, string message)
        {
            if (MySession.Static?.LocalHumanPlayer != null && isDebug)
                MyAPIGateway.Utilities?.ShowMessage("Seamless", message + $"\n {ex.ToString()}");

            MyLog.Default?.WriteLineAndConsole($"SeamlessClient: {message} \n {ex.ToString()}");
        }
    }
}
