using HarmonyLib;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Networking;
using Sandbox.Engine.Utils;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Gui;
using Sandbox.Game.GUI;
using Sandbox.Game.GUI.HudViewers;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Replication;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Sandbox.Game.World.Generator;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using SeamlessClient.Components;
using SeamlessClient.OnlinePlayersWindow;
using SeamlessClient.Utilities;
using SpaceEngineers.Game.GUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.GameServices;
using VRage.Library.Utils;
using VRage.Network;
using VRage.Utils;
using VRageRender;
using VRageRender.Messages;

namespace SeamlessClient.ServerSwitching
{
    public class ServerSwitcherV2 : ComponentBase
    {
        public static ConstructorInfo ClientConstructor;
        public static ConstructorInfo SyncLayerConstructor;
        public static ConstructorInfo TransportLayerConstructor;

        public static PropertyInfo MySessionLayer;

        private static FieldInfo RemoteAdminSettings;
        private static FieldInfo AdminSettings;
        private static MethodInfo UnloadProceduralWorldGenerator;
        private static MethodInfo GpsRegisterChat;
        private static MethodInfo LoadMembersFromWorld;
        private static MethodInfo InitVirtualClients;
        private static MethodInfo CreateNewPlayerInternal;
        private static MethodInfo PauseClient;
        public static MethodInfo SendPlayerData;

        private static FieldInfo VirtualClients;


        public static ServerSwitcherV2 Instance { get; private set; }

        private MyGameServerItem TargetServer { get; set; }
        private MyObjectBuilder_World TargetWorld { get; set; }
        private string OldArmorSkin { get; set; } = string.Empty;

        private bool needsEntityUnload = true;

        public static event EventHandler<JoinResultMsg> OnJoinEvent;

        private MyCharacter originalLocalCharacter;
        private IMyControllableEntity originalControlledEntity;

        private static bool isSwitch = false;
        private MyObjectBuilder_Player player { get; set; }
        private static Timer pauseResetTimer = new Timer(1000);



        public ServerSwitcherV2()
        {
            pauseResetTimer.Elapsed += PauseResetTimer_Elapsed;
            Instance = this;
        }

        private void PauseResetTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if(MySandboxGame.IsPaused)
            {
                Seamless.TryShow("Game is still paused... Attempting to unpause!");
                MySandboxGame.PausePop();
            }
            else
            {
                pauseResetTimer.Stop();
            }
        }

        public override void Patch(Harmony patcher)
        {
            MySessionLayer = PatchUtils.GetProperty(typeof(MySession), "SyncLayer");

            PauseClient = PatchUtils.GetMethod(PatchUtils.MyMultiplayerClientBase, "PauseClient");
            ClientConstructor = PatchUtils.GetConstructor(PatchUtils.ClientType, new[] { typeof(MyGameServerItem), PatchUtils.SyncLayerType });
            SyncLayerConstructor = PatchUtils.GetConstructor(PatchUtils.SyncLayerType, new[] { PatchUtils.MyTransportLayerType });
            TransportLayerConstructor = PatchUtils.GetConstructor(PatchUtils.MyTransportLayerType, new[] { typeof(int) });


            RemoteAdminSettings = PatchUtils.GetField(typeof(MySession), "m_remoteAdminSettings");
            AdminSettings = PatchUtils.GetField(typeof(MySession), "m_adminSettings");
            VirtualClients = PatchUtils.GetField(typeof(MySession), "VirtualClients");

            UnloadProceduralWorldGenerator = PatchUtils.GetMethod(typeof(MyProceduralWorldGenerator), "UnloadData");
            GpsRegisterChat = PatchUtils.GetMethod(typeof(MyGpsCollection), "RegisterChat");
            LoadMembersFromWorld = PatchUtils.GetMethod(typeof(MySession), "LoadMembersFromWorld");
            InitVirtualClients = PatchUtils.GetMethod(PatchUtils.VirtualClientsType, "Init");
            SendPlayerData = PatchUtils.GetMethod(PatchUtils.ClientType, "SendPlayerData");

            CreateNewPlayerInternal = PatchUtils.GetMethod(typeof(MyPlayerCollection), "CreateNewPlayerInternal");


            MethodInfo onAllmembersData = PatchUtils.ClientType.GetMethod("OnAllMembersData", BindingFlags.Instance | BindingFlags.NonPublic);


            var onClientRemoved = PatchUtils.GetMethod(typeof(MyClientCollection), "RemoveClient");
            var onDisconnectedClient = PatchUtils.GetMethod(typeof(MyMultiplayerBase), "OnDisconnectedClient");
            var onClientConnected = PatchUtils.GetMethod(PatchUtils.ClientType, "OnClientConnected");
            var processAllMembersData = PatchUtils.GetMethod(typeof(MyMultiplayerBase), "ProcessAllMembersData");
            var RemovePlayer = PatchUtils.GetMethod(typeof(MyPlayerCollection), "RemovePlayerFromDictionary");
            var LoadClient = PatchUtils.GetMethod(PatchUtils.ClientType, "LoadMembersFromWorld");


            Seamless.TryShow("Patched!");


            patcher.Patch(LoadClient, prefix: new HarmonyMethod(Get(typeof(ServerSwitcherV2), nameof(LoadClientsFromWorld))));
            //patcher.Patch(RemovePlayer, prefix: new HarmonyMethod(Get(typeof(ServerSwitcherComponent), nameof(RemovePlayerFromDict))));
            patcher.Patch(processAllMembersData, prefix: new HarmonyMethod(Get(typeof(ServerSwitcherV2), nameof(ProcessAllMembersData))));
            patcher.Patch(onDisconnectedClient, prefix: new HarmonyMethod(Get(typeof(ServerSwitcherV2), nameof(OnDisconnectedClient))));
            patcher.Patch(onClientRemoved, prefix: new HarmonyMethod(Get(typeof(ServerSwitcherV2), nameof(RemoveClient))));
            patcher.Patch(onAllmembersData, prefix: new HarmonyMethod(Get(typeof(ServerSwitcherV2), nameof(OnAllMembersData))));



            patcher.Patch(onClientConnected, prefix: new HarmonyMethod(Get(typeof(ServerSwitcherV2), nameof(OnClientConnected))));


            base.Patch(patcher);
        }



        public static void OnUserJoined(JoinResult msg)
        {
            Seamless.TryShow($"OnUserJoin! Result: {msg}");
            if (msg == JoinResult.OK)
            {



               
                //SendPlayerData


                //Invoke the switch event
                if (isSwitch)
                    Instance.StartSwitch();

                if (MySandboxGame.IsPaused)
                {
                    pauseResetTimer.Start();
                    MyHud.Notifications.Remove(MyNotificationSingletons.ConnectionProblem);
                    MySandboxGame.PausePop();
                }

                ModAPI.ServerSwitched();
                SendPlayerData.Invoke(MyMultiplayer.Static, new object[] { MyGameService.OnlineName });
                isSwitch = false;
            }
        }

        public static bool LoadClientsFromWorld(ref List<MyObjectBuilder_Client> clients)
        {
            if(!isSwitch || clients == null || clients.Count == 0)
                return true;

            
            //Dictionary<ulong, MyConnectedClientData>

            IDictionary m_memberData = (IDictionary)PatchUtils.GetField(PatchUtils.ClientType, "m_memberData").GetValue(MyMultiplayer.Static);
 
            Seamless.TryShow($"{m_memberData.Count} members from clients");

            var keys = m_memberData.Keys.Cast<ulong>();

            for(int i = clients.Count - 1; i >= 0; i-- )
            {
                Seamless.TryShow($"Client {clients[i].SteamId}");
                if (keys.Contains(clients[i].SteamId))
                {
                    Seamless.TryShow($"Remove {clients[i].SteamId}");
                    clients.RemoveAt(i);
                }
                   
               
            }

            return false;
        }



        private static bool ProcessAllMembersData(ref AllMembersDataMsg msg)
        {
            if(!isSwitch) 
                return true;


            Sync.Players.ClearIdentities();
            if (msg.Identities != null)
            {
                Sync.Players.LoadIdentities(msg.Identities);
            }

            Seamless.TryShow($"Clearing Players! \n {Environment.StackTrace.ToString()} ");

            //Sync.Players.ClearPlayers();
            if (msg.Players != null)
            {
                Sync.Players.LoadPlayers(msg.Players);
            }

            //MySession.Static.Factions.LoadFactions(msg.Factions);



            return false;
        }


        public override void Initilized()
        {
            base.Initilized();
        }


        public static bool OnClientConnected(MyPacket packet)
        {
            Seamless.TryShow("OnClientConnected");


            return true;
        }



        public static void RemoveClient(ulong steamId)
        {
            if (steamId != Sync.MyId)
                return;
             
            //Seamless.TryShow(Environment.StackTrace.ToString());
        }


        public static void OnAllMembersData(ref AllMembersDataMsg msg)
        {
            Seamless.TryShow("Recieved all members data!");
        }

        public static bool OnDisconnectedClient(ref MyControlDisconnectedMsg data, ulong sender)
        {
            Seamless.TryShow($"OnDisconnectedClient {data.Client} - Sender {sender}");
            if (isSwitch && data.Client == Sync.MyId)
                return false;

            return true;
        }


        public void StartBackendSwitch(MyGameServerItem TargetServer, MyObjectBuilder_World TargetWorld)
        {
            this.TargetServer = TargetServer;
            this.TargetWorld = TargetWorld;
            OldArmorSkin = MySession.Static.LocalHumanPlayer.BuildArmorSkin;

            Seamless.TryShow($"1 NexusMajor: {Seamless.NexusVersion.Major} - ConrolledEntity {MySession.Static.ControlledEntity == null} - HumanPlayer {MySession.Static.LocalHumanPlayer == null} - Character {MySession.Static.LocalCharacter == null}");
            if (Seamless.NexusVersion.Major >= 2)
                needsEntityUnload = false;

            isSwitch = true;

            originalLocalCharacter = MySession.Static.LocalCharacter;
            //originalControlledEntity = MySession.Static.ControlledEntity;

      


            player = MySession.Static.LocalHumanPlayer.GetObjectBuilder();
            player.Connected = false;

            AsyncInvoke.InvokeAsync(() => 
            {
                Seamless.TryShow($"Needs entity Unload: {needsEntityUnload}");

                if (needsEntityUnload)
                    MySession.Static.SetCameraController(MyCameraControllerEnum.SpectatorFixed);

                try
                {

                    UnloadServer();
                    SetNewMultiplayerClient();

                }catch(Exception ex)
                {
                    Seamless.TryShow(ex.ToString());
                }
            });
        }


        public void ResetReplicationTime(bool ClientReady)
        {
            PatchUtils.GetField(typeof(MyReplicationClient), "m_lastServerTimestamp").SetValue(MyMultiplayer.Static.ReplicationLayer, MyTimeSpan.Zero);
            PatchUtils.GetField(typeof(MyReplicationClient), "m_lastServerTimeStampReceivedTime").SetValue(MyMultiplayer.Static.ReplicationLayer, MyTimeSpan.Zero);
            PatchUtils.GetField(typeof(MyReplicationClient), "m_clientStartTimeStamp").SetValue(MyMultiplayer.Static.ReplicationLayer, MyTimeSpan.Zero);
            PatchUtils.GetField(typeof(MyReplicationClient), "m_lastTime").SetValue(MyMultiplayer.Static.ReplicationLayer, MyTimeSpan.Zero);
            PatchUtils.GetField(typeof(MyReplicationClient), "m_lastClientTime").SetValue(MyMultiplayer.Static.ReplicationLayer, MyTimeSpan.Zero);
            PatchUtils.GetField(typeof(MyReplicationClient), "m_lastServerTime").SetValue(MyMultiplayer.Static.ReplicationLayer, MyTimeSpan.Zero);
            PatchUtils.GetField(typeof(MyReplicationClient), "m_lastClientTimestamp").SetValue(MyMultiplayer.Static.ReplicationLayer, MyTimeSpan.Zero);


            PatchUtils.GetField(typeof(MyReplicationClient), "m_clientReady").SetValue(MyMultiplayer.Static.ReplicationLayer, ClientReady);
        }


        private void UnloadServer()
        {
            if (MyMultiplayer.Static == null)
                throw new Exception("MyMultiplayer.Static is null on unloading? dafuq?");


            UnloadOldEntities();

            //Close and Cancel any screens (Medical or QuestLog)
            MySessionComponentIngameHelp component = MySession.Static.GetComponent<MySessionComponentIngameHelp>();
            component?.TryCancelObjective();
            MyGuiScreenMedicals.Close();


            //Clear all old players and clients.
            //Sync.Clients.Clear();
            //Sync.Players.ClearPlayers();


            //Unregister Chat
         
        

            MethodInfo removeClient = PatchUtils.GetMethod(PatchUtils.ClientType, "MyMultiplayerClient_ClientLeft");
            foreach (var connectedClient in Sync.Clients.GetClients())
            {
                if (connectedClient.SteamUserId == Sync.MyId || connectedClient.SteamUserId == Sync.ServerId)
                    continue;

                removeClient.Invoke(MyMultiplayer.Static, new object[] { connectedClient.SteamUserId, MyChatMemberStateChangeEnum.Left });
            }





            //Clear all local GPS Points
            MyReplicationClient client = (MyReplicationClient)MyMultiplayer.Static.ReplicationLayer;
            client.Dispose();
            //client.Disconnect();


         


            MyGameService.Peer2Peer.CloseSession(Sync.ServerId);
            MyGameService.DisconnectFromServer();


            MyControlDisconnectedMsg myControlDisconnectedMsg = default(MyControlDisconnectedMsg);
            myControlDisconnectedMsg.Client = Sync.MyId;

            typeof(MyMultiplayerBase).GetMethod("SendControlMessage", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(typeof(MyControlDisconnectedMsg)).Invoke(MyMultiplayer.Static, new object[] { Sync.ServerId, myControlDisconnectedMsg, true });

            //DisconnectReplication
            //MyMultiplayer.Static.ReplicationLayer.Disconnect();
            //MyMultiplayer.Static.ReplicationLayer.Dispose();
            //MyMultiplayer.Static.Dispose();
            //MyMultiplayer.Static = null;

            MyReplicationClient clienta = (MyReplicationClient)MyMultiplayer.Static.ReplicationLayer;

            ResetReplicationTime(false);

            //Reset Hud
            MyHud.Static.UnloadDataConditional();

            Seamless.TryShow($"2 Streaming: {clienta.HasPendingStreamingReplicables} - LastMessage: {clienta.LastMessageFromServer}");
            Seamless.TryShow($"2 NexusMajor: {Seamless.NexusVersion.Major} - ConrolledEntity {MySession.Static.ControlledEntity == null} - HumanPlayer {MySession.Static.LocalHumanPlayer == null} - Character {MySession.Static.LocalCharacter == null}");


            ModAPI.StartModSwitching();

            //MyMultiplayer.Static.ReplicationLayer.Disconnect();
            //MyMultiplayer.Static.ReplicationLayer.Dispose();
            //MyMultiplayer.Static.Dispose();
        }

        private void UnloadOldEntities()
        {


            foreach (var ent in MyEntities.GetEntities())
            {
                if (ent is MyPlanet)
                    continue;

                if (ent is MyCharacter)
                    continue;


                ent.Close();

                if (needsEntityUnload)
                {
                    ent.Close();
                }
                else
                {
                    //
                }


            }
        }

        private void SetNewMultiplayerClient()
        {

            MyReplicationClient clienta = (MyReplicationClient)MyMultiplayer.Static.ReplicationLayer;
            Seamless.TryShow($"3 Streaming: {clienta.HasPendingStreamingReplicables} - LastMessage: {clienta.LastMessageFromServer}");
            Seamless.TryShow($"3 NexusMajor: {Seamless.NexusVersion.Major} - ConrolledEntity {MySession.Static.ControlledEntity == null} - HumanPlayer {MySession.Static.LocalHumanPlayer == null} - Character {MySession.Static.LocalCharacter == null}");

           

            MySandboxGame.Static.SessionCompatHelper.FixSessionComponentObjectBuilders(TargetWorld.Checkpoint, TargetWorld.Sector);




            PatchUtils.ClientType.GetProperty("Server", BindingFlags.Public | BindingFlags.Instance).SetValue(MyMultiplayer.Static, TargetServer);
            typeof(MyMultiplayerBase).GetProperty("ServerId", BindingFlags.Public | BindingFlags.Instance).SetValue(MyMultiplayer.Static, TargetServer.SteamID);
            

            MyGameService.ConnectToServer(TargetServer, delegate (JoinResult joinResult)
            {
                MySandboxGame.Static.Invoke(delegate
                {
                    Seamless.TryShow("Connected to server!");
                    PatchUtils.ClientType.GetMethod("OnConnectToServer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(MyMultiplayer.Static, new object[] { joinResult });
                    OnUserJoined(joinResult);


                }, "OnConnectToServer");
            });



            Seamless.TryShow($"4 Streaming: {clienta.HasPendingStreamingReplicables} - LastMessage: {clienta.LastMessageFromServer}");
            Seamless.TryShow($"4 NexusMajor: {Seamless.NexusVersion.Major} - ConrolledEntity {MySession.Static.ControlledEntity == null} - HumanPlayer {MySession.Static.LocalHumanPlayer == null} - Character {MySession.Static.LocalCharacter == null}");


            return;
            // Create constructors
            var LayerInstance = TransportLayerConstructor.Invoke(new object[] { 2 });
            var SyncInstance = SyncLayerConstructor.Invoke(new object[] { LayerInstance });
            var instance = ClientConstructor.Invoke(new object[] { TargetServer, SyncInstance });


            MyMultiplayer.Static = UtilExtensions.CastToReflected(instance, PatchUtils.ClientType);
            MyMultiplayer.Static.ExperimentalMode = true;

            

            // Set the new SyncLayer to the MySession.Static.SyncLayer
            MySessionLayer.SetValue(MySession.Static, MyMultiplayer.Static.SyncLayer);
            Sync.Clients.SetLocalSteamId(Sync.MyId, false, MyGameService.UserName);
            return;
            Seamless.TryShow("Successfully set MyMultiplayer.Static");



            Sync.Clients.SetLocalSteamId(Sync.MyId, false, MyGameService.UserName);
            Sync.Players.RegisterEvents();

            return;
        }

        private void StartSwitch()
        {
     
            MyReplicationClient clienta = (MyReplicationClient)MyMultiplayer.Static.ReplicationLayer;
            Seamless.TryShow($"5 Streaming: {clienta.HasPendingStreamingReplicables} - LastMessage: {clienta.LastMessageFromServer}");
            Seamless.TryShow($"5 NexusMajor: {Seamless.NexusVersion.Major} - ConrolledEntity {MySession.Static.ControlledEntity == null} - HumanPlayer {MySession.Static.LocalHumanPlayer == null} - Character {MySession.Static.LocalCharacter == null}");
            Seamless.TryShow("Starting new MP Client!");

            /* On Server Successfull Join
             * 
             * 
             */



            List<ulong> clients = new List<ulong>();    
            foreach(var client in Sync.Clients.GetClients())
            {
                clients.Add(client.SteamUserId);
                Seamless.TryShow($"ADDING {client.SteamUserId} - {Sync.MyId}");
            }

            foreach(var client in clients)
            {
                if (client == TargetServer.SteamID || client == Sync.MyId)
                    continue;

                Seamless.TryShow($"REMOVING {client}");
                Sync.Clients.RemoveClient(client);
            }

            LoadConnectedClients();

            //Sync.Clients.SetLocalSteamId(Sync.MyId, !Sync.Clients.HasClient(Sync.MyId), MyGameService.UserName);


            Seamless.TryShow("Applying World Settings...");
            SetWorldSettings();

            Seamless.TryShow("Starting Components...");
            InitComponents();

            Seamless.TryShow("Starting Entity Sync...");
            StartEntitySync();

            //MyGuiSandbox.RemoveScreen(MyGuiScreenHudSpace.Static);





            typeof(MySandboxGame).GetField("m_pauseStackCount", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, 0);


     

            //GpsRegisterChat.Invoke(MySession.Static.Gpss, new object[] { MyMultiplayer.Static });



            //Recreate all controls... Will fix weird gui/paint/crap
            //MyGuiScreenHudSpace.Static?.RecreateControls(true);

           


            Seamless.TryShow($"6 NexusMajor: {Seamless.NexusVersion.Major} - ConrolledEntity {MySession.Static.ControlledEntity == null} - HumanPlayer {MySession.Static.LocalHumanPlayer == null} - Character {MySession.Static.LocalCharacter == null}");
            Seamless.TryShow($"6 Streaming: {clienta.HasPendingStreamingReplicables} - LastMessage: {clienta.LastMessageFromServer}");
            
            originalLocalCharacter?.Close();
            ResetReplicationTime(true);

            // Allow the game to start proccessing incoming messages in the buffer
            MyMultiplayer.Static.StartProcessingClientMessages();


            //Send Client Ready
            ClientReadyDataMsg clientReadyDataMsg = default(ClientReadyDataMsg);
            clientReadyDataMsg.ForcePlayoutDelayBuffer = MyFakes.ForcePlayoutDelayBuffer;
            clientReadyDataMsg.UsePlayoutDelayBufferForCharacter = true;
            clientReadyDataMsg.UsePlayoutDelayBufferForJetpack = true;
            clientReadyDataMsg.UsePlayoutDelayBufferForGrids = true;
            ClientReadyDataMsg msg = clientReadyDataMsg;
            clienta.SendClientReady(ref msg);

            Seamless.SendSeamlessVersion();

            FieldInfo hudPoints = typeof(MyHudMarkerRender).GetField("m_pointsOfInterest", BindingFlags.Instance | BindingFlags.NonPublic);
            IList points = (IList)hudPoints.GetValue(MyGuiScreenHudSpace.Static.MarkerRender);

            MySandboxGame.PausePop();
            //Sync.Players.RequestNewPlayer(Sync.MyId, 0, MyGameService.UserName, null, true, true);
            PauseClient.Invoke(MyMultiplayer.Static, new object[] { false });
            MySandboxGame.PausePop();
            MyHud.Notifications.Remove(MyNotificationSingletons.ConnectionProblem);
        }




        private void SetWorldSettings()
        {
            //Clear old list
            MySession.Static.PromotedUsers.Clear();
            MySession.Static.CreativeTools.Clear();
            Dictionary<ulong, AdminSettingsEnum> AdminSettingsList = (Dictionary<ulong, AdminSettingsEnum>)RemoteAdminSettings.GetValue(MySession.Static);
            AdminSettingsList.Clear();



            // Set new world settings
            MySession.Static.Name = MyStatControlText.SubstituteTexts(TargetWorld.Checkpoint.SessionName);
            MySession.Static.Description = TargetWorld.Checkpoint.Description;

            MySession.Static.Mods = TargetWorld.Checkpoint.Mods;
            MySession.Static.Settings = TargetWorld.Checkpoint.Settings;
            MySession.Static.CurrentPath = MyLocalCache.GetSessionSavesPath(MyUtils.StripInvalidChars(TargetWorld.Checkpoint.SessionName), contentFolder: false, createIfNotExists: false);
            MySession.Static.WorldBoundaries = TargetWorld.Checkpoint.WorldBoundaries;
            MySession.Static.InGameTime = MyObjectBuilder_Checkpoint.DEFAULT_DATE;
            MySession.Static.ElapsedGameTime = new TimeSpan(TargetWorld.Checkpoint.ElapsedGameTime);
            MySession.Static.Settings.EnableSpectator = false;

            MySession.Static.Password = TargetWorld.Checkpoint.Password;
            MySession.Static.PreviousEnvironmentHostility = TargetWorld.Checkpoint.PreviousEnvironmentHostility;
            MySession.Static.RequiresDX = TargetWorld.Checkpoint.RequiresDX;
            MySession.Static.CustomLoadingScreenImage = TargetWorld.Checkpoint.CustomLoadingScreenImage;
            MySession.Static.CustomLoadingScreenText = TargetWorld.Checkpoint.CustomLoadingScreenText;
            MySession.Static.CustomSkybox = TargetWorld.Checkpoint.CustomSkybox;

            try
            {
                MySession.Static.Gpss = new MyGpsCollection();

            }
            catch (Exception ex)
            {
                Seamless.TryShow($"An error occured while loading GPS points! You will have an empty gps list! \n {ex.ToString()}");
            }


            //MyRenderProxy.RebuildCullingStructure();
            //MySession.Static.Toolbars.LoadToolbars(checkpoint);

            Sync.Players.RespawnComponent.InitFromCheckpoint(TargetWorld.Checkpoint);


            // Set new admin settings
            if (TargetWorld.Checkpoint.PromotedUsers != null)
            {
                MySession.Static.PromotedUsers = TargetWorld.Checkpoint.PromotedUsers.Dictionary;
            }
            else
            {
                MySession.Static.PromotedUsers = new Dictionary<ulong, MyPromoteLevel>();
            }




            foreach (KeyValuePair<MyObjectBuilder_Checkpoint.PlayerId, MyObjectBuilder_Player> item in TargetWorld.Checkpoint.AllPlayersData.Dictionary)
            {
                ulong clientId = item.Key.GetClientId();
                AdminSettingsEnum adminSettingsEnum = (AdminSettingsEnum)item.Value.RemoteAdminSettings;
                if (TargetWorld.Checkpoint.RemoteAdminSettings != null && TargetWorld.Checkpoint.RemoteAdminSettings.Dictionary.TryGetValue(clientId, out var value))
                {
                    adminSettingsEnum = (AdminSettingsEnum)value;
                }
                if (!MyPlatformGameSettings.IsIgnorePcuAllowed)
                {
                    adminSettingsEnum &= ~AdminSettingsEnum.IgnorePcu;
                    adminSettingsEnum &= ~AdminSettingsEnum.KeepOriginalOwnershipOnPaste;
                }


                AdminSettingsList[clientId] = adminSettingsEnum;
                if (!Sync.IsDedicated && clientId == Sync.MyId)
                {
                    AdminSettings.SetValue(MySession.Static, adminSettingsEnum);
                }



                if (!MySession.Static.PromotedUsers.TryGetValue(clientId, out var value2))
                {
                    value2 = MyPromoteLevel.None;
                }
                if (item.Value.PromoteLevel > value2)
                {
                    MySession.Static.PromotedUsers[clientId] = item.Value.PromoteLevel;
                }
                if (!MySession.Static.CreativeTools.Contains(clientId) && item.Value.CreativeToolsEnabled)
                {
                    MySession.Static.CreativeTools.Add(clientId);
                }
            }


            MySector.InitEnvironmentSettings(TargetWorld.Sector.Environment);

            string text = ((!string.IsNullOrEmpty(TargetWorld.Checkpoint.CustomSkybox)) ? TargetWorld.Checkpoint.CustomSkybox : MySector.EnvironmentDefinition.EnvironmentTexture);
            MyRenderProxy.PreloadTextures(new string[1] { text }, TextureType.CubeMap);




        }

        private void InitComponents()
        {
            UpdateWorldGenerator();
        }
        private void UpdateWorldGenerator()
        {
            //This will re-init the MyProceduralWorldGenerator. (Not doing this will result in asteroids not rendering in properly)


            //This shoud never be null
            var Generator = MySession.Static.GetComponent<MyProceduralWorldGenerator>();

            //Force component to unload
            UnloadProceduralWorldGenerator.Invoke(Generator, null);

            //Re-call the generator init
            MyObjectBuilder_WorldGenerator GeneratorSettings = (MyObjectBuilder_WorldGenerator)TargetWorld.Checkpoint.SessionComponents.FirstOrDefault(x => x.GetType() == typeof(MyObjectBuilder_WorldGenerator));
            if (GeneratorSettings != null)
            {
                //Re-initilized this component (forces to update asteroid areas like not in planets etc)
                Generator.Init(GeneratorSettings);
            }

            //Force component to reload, re-syncing settings and seeds to the destination server
            Generator.LoadData();

            //We need to go in and force planets to be empty areas in the generator. This is originially done on planet init.
            FieldInfo PlanetInitArgs = typeof(MyPlanet).GetField("m_planetInitValues", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var Planet in MyEntities.GetEntities().OfType<MyPlanet>())
            {
                MyPlanetInitArguments args = (MyPlanetInitArguments)PlanetInitArgs.GetValue(Planet);

                float MaxRadius = args.MaxRadius;

                Generator.MarkEmptyArea(Planet.PositionComp.GetPosition(), MaxRadius);
            }
        }

        private void StartEntitySync() 
        {
            Seamless.TryShow("Requesting Player From Server");

            Sync.Players.RequestNewPlayer(Sync.MyId, 0, MyGameService.UserName, null, true, true);
            if (!Sandbox.Engine.Platform.Game.IsDedicated && MySession.Static.LocalHumanPlayer == null)
            {
                Seamless.TryShow("RequestNewPlayer");
               

            }
            else if (MySession.Static.ControlledEntity == null && Sync.IsServer && !Sandbox.Engine.Platform.Game.IsDedicated)
            {
                Seamless.TryShow("ControlledObject was null, respawning character");
                //m_cameraAwaitingEntity = true;
                MyPlayerCollection.RequestLocalRespawn();
            }

            

            //Request client state batch
            (MyMultiplayer.Static as MyMultiplayerClientBase).RequestBatchConfirmation();
            MyMultiplayer.Static.PendingReplicablesDone += Static_PendingReplicablesDone;
            //typeof(MyGuiScreenTerminal).GetMethod("CreateTabs")

            //MySession.Static.LocalHumanPlayer.Controller.TakeControl(originalControlledEntity);

            MyGuiSandbox.UnloadContent();
            MyGuiSandbox.LoadContent();

            //MyGuiSandbox.CreateScreen(MyPerGameSettings.GUI.HUDScreen);

            MyRenderProxy.RebuildCullingStructure();
            MyRenderProxy.CollectGarbage();


            Seamless.TryShow("OnlinePlayers: " + MySession.Static.Players.GetOnlinePlayers().Count);
            Seamless.TryShow("Loading Complete!");
        }

        private void Static_PendingReplicablesDone()
        {
            if (MySession.Static.VoxelMaps.Instances.Count > 0)
            {
                MySandboxGame.AreClipmapsReady = false;
            }
            MyMultiplayer.Static.PendingReplicablesDone -= Static_PendingReplicablesDone;

        }

        private void LoadConnectedClients()
        {
            Seamless.TryShow($"BEFORE {MySession.Static.LocalHumanPlayer == null} - {MySession.Static.LocalCharacter == null}");
            MyPlayer.PlayerId? savingPlayerId = new MyPlayer.PlayerId(Sync.MyId);
            if (!savingPlayerId.HasValue)
            {
                Seamless.TryShow("SavingPlayerID is null! Creating Default!");
                savingPlayerId = new MyPlayer.PlayerId(Sync.MyId);
            }


            LoadMembersFromWorld.Invoke(MySession.Static, new object[] { TargetWorld, MyMultiplayer.Static });




            player.IsWildlifeAgent = true;
            CreateNewPlayerInternal.Invoke(MySession.Static.Players, new object[] { Sync.Clients.LocalClient, savingPlayerId.Value, player });
            typeof(MyPlayerCollection).GetMethod("LoadPlayerInternal", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(MySession.Static.Players, new object[] { savingPlayerId.Value, player, false  });

            Seamless.TryShow("Saving PlayerID: " + savingPlayerId.ToString());

            Sync.Players.LoadConnectedPlayers(TargetWorld.Checkpoint, savingPlayerId);
            Sync.Players.LoadControlledEntities(TargetWorld.Checkpoint.ControlledEntities, TargetWorld.Checkpoint.ControlledObject, savingPlayerId);

            Seamless.TryShow($"AFTER {MySession.Static.LocalHumanPlayer == null} - {MySession.Static.LocalCharacter == null}");
        }
    }
}
