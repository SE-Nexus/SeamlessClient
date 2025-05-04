using HarmonyLib;
using Sandbox.Game.World;
using Sandbox;
using SeamlessClient.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.GameServices;
using Sandbox.Game.Gui;
using Sandbox.Game.SessionComponents;
using SpaceEngineers.Game.GUI;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Engine.Networking;
using System.Reflection;
using VRage.Network;
using Sandbox.ModAPI;
using VRageRender.Messages;
using VRageRender;
using Sandbox.Game.GUI;
using Sandbox.Game.World.Generator;
using Sandbox.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using SeamlessClient.ServerSwitching;
using Sandbox.Game.Entities.Character;
using VRage.Game.Utils;
using VRage;
using Sandbox.Game.GameSystems.CoordinateSystem;

namespace SeamlessClient.Components
{
    public class ServerSwitcherComponentOLD : ComponentBase
    {
        private static bool isSeamlessSwitching { get; set; } = false;
        private static bool WaitingForClientCheck { get; set; } = false;


        private static ConstructorInfo TransportLayerConstructor;
        private static ConstructorInfo SyncLayerConstructor;
        private static ConstructorInfo ClientConstructor;

        private static MethodInfo UnloadProceduralWorldGenerator;
        private static MethodInfo GpsRegisterChat;
        private static MethodInfo LoadMembersFromWorld;
        private static MethodInfo InitVirtualClients;
        private static FieldInfo AdminSettings;
        private static FieldInfo RemoteAdminSettings;
        private static FieldInfo VirtualClients;
        private static PropertyInfo MySessionLayer;

        public static MyGameServerItem TargetServer { get; private set; }
        public static MyObjectBuilder_World TargetWorld { get; private set; }

        public static ServerSwitcherComponentOLD Instance { get; private set; }
        private string OldArmorSkin { get; set; } = string.Empty;

        public ServerSwitcherComponentOLD() { Instance = this; }
        public static string SwitchingText = string.Empty;


        public override void Update()
        {
            //Toggle waiting for client check
            if (WaitingForClientCheck == false && isSeamlessSwitching)
                WaitingForClientCheck = true;

            if(WaitingForClientCheck && MySession.Static.LocalHumanPlayer != null)
                WaitingForClientCheck = false;

            if (isSeamlessSwitching || WaitingForClientCheck)
            {
                //SeamlessClient.TryShow("Switching Servers!");
                MyRenderProxy.DebugDrawText2D(new VRageMath.Vector2(MySandboxGame.ScreenViewport.Width/2, MySandboxGame.ScreenViewport.Height - 150), SwitchingText, VRageMath.Color.AliceBlue, 1f, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
                MyRenderProxy.DebugDrawText2D(new VRageMath.Vector2(MySandboxGame.ScreenViewport.Width / 2, MySandboxGame.ScreenViewport.Height - 200), $"Transferring to {TargetServer.Name}", VRageMath.Color.Yellow, 1.5f, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);


                MyRenderProxy.DebugDrawLine2D(new VRageMath.Vector2((MySandboxGame.ScreenViewport.Width / 2) - 250, MySandboxGame.ScreenViewport.Height - 170), new VRageMath.Vector2((MySandboxGame.ScreenViewport.Width / 2)+250, MySandboxGame.ScreenViewport.Height - 170), VRageMath.Color.Blue, VRageMath.Color.Green);
            }
        }


        public override void Patch(Harmony patcher)
        {
            TransportLayerConstructor = PatchUtils.GetConstructor(PatchUtils.MyTransportLayerType, new[] { typeof(int) });
            SyncLayerConstructor = PatchUtils.GetConstructor(PatchUtils.SyncLayerType,  new[] { PatchUtils.MyTransportLayerType });
            ClientConstructor = PatchUtils.GetConstructor(PatchUtils.ClientType, new[] { typeof(MyGameServerItem), PatchUtils.SyncLayerType });
            MySessionLayer = PatchUtils.GetProperty(typeof(MySession), "SyncLayer");

            var onJoin = PatchUtils.GetMethod(PatchUtils.ClientType, "OnUserJoined");
            UnloadProceduralWorldGenerator = PatchUtils.GetMethod(typeof(MyProceduralWorldGenerator), "UnloadData");
            GpsRegisterChat = PatchUtils.GetMethod(typeof(MyGpsCollection), "RegisterChat");
            AdminSettings = PatchUtils.GetField(typeof(MySession), "m_adminSettings");
            RemoteAdminSettings = PatchUtils.GetField(typeof(MySession), "m_remoteAdminSettings");
            LoadMembersFromWorld = PatchUtils.GetMethod(typeof(MySession), "LoadMembersFromWorld");
            InitVirtualClients = PatchUtils.GetMethod(PatchUtils.VirtualClientsType, "Init");
            VirtualClients = PatchUtils.GetField(typeof(MySession), "VirtualClients");

            patcher.Patch(onJoin, postfix: new HarmonyMethod(Get(typeof(ServerSwitcherComponentOLD), nameof(OnUserJoined))));
           
            
        }

        public override void Initilized()
        {
            MyAPIGateway.Utilities.MessageEntered += Utilities_MessageEntered;
        }

        private void Utilities_MessageEntered(string messageText, ref bool sendToOthers)
        {
            if (!messageText.StartsWith("/nexus"))
                return;

            string[] cmd = messageText.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (cmd[1] == "refreshcharacter")
            {
                if(MySession.Static.LocalHumanPlayer == null)
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

        private static void OnUserJoined(ref JoinResultMsg msg)
        {
            if (msg.JoinResult == JoinResult.OK && isSeamlessSwitching)
            {
                //SeamlessClient.TryShow("User Joined! Result: " + msg.JoinResult.ToString());

                //Invoke the switch event

                SwitchingText = "Server Responded! Removing Old Entities and forcing client connection!";
                RemoveOldEntities();
                ForceClientConnection();
                ModAPI.ServerSwitched();

            


                MySession.Static.LocalHumanPlayer?.Character?.Stand();
                isSeamlessSwitching = false;
            }
        }

        public void StartBackendSwitch(MyGameServerItem _TargetServer, MyObjectBuilder_World _TargetWorld)
        {

            if(MySession.Static.LocalCharacter != null)
            {
                var viewMatrix = MySession.Static.LocalCharacter.GetViewMatrix();
                MySpectator.Static.SetViewMatrix(viewMatrix);
            }
           
            SwitchingText = "Starting Seamless Switch... Please wait!";
            isSeamlessSwitching = true;
            OldArmorSkin = MySession.Static.LocalHumanPlayer.BuildArmorSkin;
            TargetServer = _TargetServer;
            TargetWorld = _TargetWorld;

            MySandboxGame.Static.Invoke(delegate
            {
                //Pause the game/update thread while we load
                MySandboxGame.IsPaused = true;

                //Set camera controller to fixed spectator
                MySession.Static.SetCameraController(MyCameraControllerEnum.SpectatorFixed);
                UnloadCurrentServer();
                SetNewMultiplayerClient();
                ModAPI.StartModSwitching();

                //SeamlessClient.IsSwitching = false;

                SwitchingText = "Waiting for server response...";
            }, "SeamlessClient");

        }

        private void SetNewMultiplayerClient()
        {
            // Following is called when the multiplayer is set successfully to target server
            MySandboxGame.Static.SessionCompatHelper.FixSessionComponentObjectBuilders(TargetWorld.Checkpoint, TargetWorld.Sector);

            // Create constructors
            var LayerInstance = TransportLayerConstructor.Invoke(new object[] { 2 });
            var SyncInstance = SyncLayerConstructor.Invoke(new object[] { LayerInstance });
            var instance = ClientConstructor.Invoke(new object[] { TargetServer, SyncInstance });


            MyMultiplayer.Static = UtilExtensions.CastToReflected(instance, PatchUtils.ClientType);
            MyMultiplayer.Static.ExperimentalMode = true;

         

            // Set the new SyncLayer to the MySession.Static.SyncLayer
            MySessionLayer.SetValue(MySession.Static, MyMultiplayer.Static.SyncLayer);

            SwitchingText = "New Multiplayer Session Set";
            Seamless.TryShow("Successfully set MyMultiplayer.Static");
            MySandboxGame.IsPaused = true;

            Sync.Clients.SetLocalSteamId(Sync.MyId, false, MyGameService.UserName);
            Sync.Players.RegisterEvents();
            SwitchingText = "Registering Player Events";
        }



        private static void ForceClientConnection()
        {
         


            //Set World Settings
            SetWorldSettings();

            //Load force load any connected players
            LoadConnectedClients();
            SwitchingText = "Loaded Connected Clients";


            MySector.InitEnvironmentSettings(TargetWorld.Sector.Environment);



            string text = ((!string.IsNullOrEmpty(TargetWorld.Checkpoint.CustomSkybox)) ? TargetWorld.Checkpoint.CustomSkybox : MySector.EnvironmentDefinition.EnvironmentTexture);
            MyRenderProxy.PreloadTextures(new string[1] { text }, TextureType.CubeMap);

            MyModAPIHelper.Initialize();
            MySession.Static.LoadDataComponents();
            MyModAPIHelper.Initialize();



            //MethodInfo A = typeof(MySession).GetMethod("LoadGameDefinition", BindingFlags.Instance | BindingFlags.NonPublic);
            // A.Invoke(MySession.Static, new object[] { TargetWorld.Checkpoint });



            MyMultiplayer.Static.OnSessionReady();
            SwitchingText = "Session Ready";

            UpdateWorldGenerator();

            StartEntitySync();

            //Resume the game/update thread
            MySandboxGame.IsPaused = false;

            MyHud.Chat.RegisterChat(MyMultiplayer.Static);
            GpsRegisterChat.Invoke(MySession.Static.Gpss, new object[] { MyMultiplayer.Static });
            SwitchingText = "Registered Chat";

            // Allow the game to start proccessing incoming messages in the buffer
            MyMultiplayer.Static.StartProcessingClientMessages();

            //Recreate all controls... Will fix weird gui/paint/crap
            MyGuiScreenHudSpace.Static.RecreateControls(true);
            SwitchingText = "Client Registered. Waiting for entities from server...";
           

            Seamless.TryShow($"LocalHumanPlayer = {MySession.Static.LocalHumanPlayer == null}");

            
          
            //MySession.Static.LocalHumanPlayer.BuildArmorSkin = OldArmorSkin;
        }

        private static void LoadOnlinePlayers()
        {
            //Get This players ID

            MyPlayer.PlayerId? savingPlayerId = new MyPlayer.PlayerId(Sync.MyId);
            if (!savingPlayerId.HasValue)
            {
                Seamless.TryShow("SavingPlayerID is null! Creating Default!");
                savingPlayerId = new MyPlayer.PlayerId(Sync.MyId);
            }
            Seamless.TryShow("Saving PlayerID: " + savingPlayerId.ToString());

            Sync.Players.LoadConnectedPlayers(TargetWorld.Checkpoint, savingPlayerId);
            Sync.Players.LoadControlledEntities(TargetWorld.Checkpoint.ControlledEntities, TargetWorld.Checkpoint.ControlledObject, savingPlayerId);
            /*
          

            SeamlessClient.TryShow("Saving PlayerID: " + savingPlayerId.ToString());



            foreach (KeyValuePair<MyObjectBuilder_Checkpoint.PlayerId, MyObjectBuilder_Player> item3 in TargetWorld.Checkpoint.AllPlayersData.Dictionary)
            {
                MyPlayer.PlayerId playerId5 = new MyPlayer.PlayerId(item3.Key.GetClientId(), item3.Key.SerialId);

                SeamlessClient.TryShow($"ConnectedPlayer: {playerId5.ToString()}");
                if (savingPlayerId.HasValue && playerId5.SteamId == savingPlayerId.Value.SteamId)
                {
                    playerId5 = new MyPlayer.PlayerId(Sync.MyId, playerId5.SerialId);
                }

                Patches.LoadPlayerInternal.Invoke(MySession.Static.Players, new object[] { playerId5, item3.Value, false });
                ConcurrentDictionary<MyPlayer.PlayerId, MyPlayer> Players = (ConcurrentDictionary<MyPlayer.PlayerId, MyPlayer>)Patches.MPlayerGPSCollection.GetValue(MySession.Static.Players);
                //LoadPlayerInternal(ref playerId5, item3.Value);
                if (Players.TryGetValue(playerId5, out MyPlayer myPlayer))
                {
                    List<Vector3> value2 = null;
                    if (TargetWorld.Checkpoint.AllPlayersColors != null && TargetWorld.Checkpoint.AllPlayersColors.Dictionary.TryGetValue(item3.Key, out value2))
                    {
                        myPlayer.SetBuildColorSlots(value2);
                    }
                    else if (TargetWorld.Checkpoint.CharacterToolbar != null && TargetWorld.Checkpoint.CharacterToolbar.ColorMaskHSVList != null && TargetWorld.Checkpoint.CharacterToolbar.ColorMaskHSVList.Count > 0)
                    {
                        myPlayer.SetBuildColorSlots(TargetWorld.Checkpoint.CharacterToolbar.ColorMaskHSVList);
                    }
                }
            }

            */

        }

        private static void SetWorldSettings()
        {
            //MyEntities.MemoryLimitAddFailureReset();

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
                MySession.Static.Gpss.LoadGpss(TargetWorld.Checkpoint);

            }
            catch (Exception ex)
            {
                Seamless.TryShow($"An error occured while loading GPS points! You will have an empty gps list! \n {ex.ToString()}");
            }


            MyRenderProxy.RebuildCullingStructure();
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

                    //m_adminSettings = adminSettingsEnum;
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




        }

        private static void LoadConnectedClients()
        {

            //TargetWorld.Checkpoint.AllPlayers.Count
            Seamless.TryShow($"Loading members from world... {TargetWorld.Checkpoint.AllPlayers.Count}");
            LoadMembersFromWorld.Invoke(MySession.Static, new object[] { TargetWorld, MyMultiplayer.Static });


            //Re-Initilize Virtual clients
            object VirtualClientsValue = VirtualClients.GetValue(MySession.Static);
            InitVirtualClients.Invoke(VirtualClientsValue, null);


            //load online players
            LoadOnlinePlayers();

        }

        private static void StartEntitySync()
        {
            Seamless.TryShow("Requesting Player From Server");
            Sync.Players.RequestNewPlayer(Sync.MyId, 0, MyGameService.UserName, null, realPlayer: true, initialPlayer: true);
            if (MySession.Static.ControlledEntity == null && Sync.IsServer && !Sandbox.Engine.Platform.Game.IsDedicated)
            {
                MyLog.Default.WriteLine("ControlledObject was null, respawning character");
                //m_cameraAwaitingEntity = true;
                MyPlayerCollection.RequestLocalRespawn();
            }

            //Request client state batch
            (MyMultiplayer.Static as MyMultiplayerClientBase).RequestBatchConfirmation();
            MyMultiplayer.Static.PendingReplicablesDone += MyMultiplayer_PendingReplicablesDone;
            //typeof(MyGuiScreenTerminal).GetMethod("CreateTabs")

            MySession.Static.LoadDataComponents();
            //MyGuiSandbox.LoadData(false);
            //MyGuiSandbox.AddScreen(MyGuiSandbox.CreateScreen(MyPerGameSettings.GUI.HUDScreen));
            MyRenderProxy.RebuildCullingStructure();
            MyRenderProxy.CollectGarbage();

            Seamless.TryShow("OnlinePlayers: " + MySession.Static.Players.GetOnlinePlayers().Count);
            Seamless.TryShow("Loading Complete!");
        }

        private static void MyMultiplayer_PendingReplicablesDone()
        {
            if (MySession.Static.VoxelMaps.Instances.Count > 0)
            {
                MySandboxGame.AreClipmapsReady = false;
            }
            MyMultiplayer.Static.PendingReplicablesDone -= MyMultiplayer_PendingReplicablesDone;
        }


        private static void UpdateWorldGenerator()
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

        private void UnloadCurrentServer()
        {
            //Unload current session on game thread
            if (MyMultiplayer.Static == null)
                throw new Exception("MyMultiplayer.Static is null on unloading? dafuq?");


            SwitchingText = "Unloading Local Server Components";
            //Try and close the quest log
            MySessionComponentIngameHelp component = MySession.Static.GetComponent<MySessionComponentIngameHelp>();
            component?.TryCancelObjective();

            //Clear all old players and clients.
            Sync.Clients.Clear();
            Sync.Players.ClearPlayers();


            MyHud.Chat.UnregisterChat(MyMultiplayer.Static);




            MySession.Static.Gpss.RemovePlayerGpss(MySession.Static.LocalPlayerId);
            MyHud.GpsMarkers.Clear();
            MyMultiplayer.Static.ReplicationLayer.Disconnect();
            MyMultiplayer.Static.ReplicationLayer.Dispose();
            MyMultiplayer.Static.Dispose();
            MyMultiplayer.Static = null;
            SwitchingText = "Local Multiplayer Disposed";

            //Clear grid coord systems
            ResetCoordinateSystems();

            //Close any respawn screens that are open
            MyGuiScreenMedicals.Close();

            //Unload any lingering updates queued
            MyEntities.Orchestrator.Unload();
            
        }

        private static void RemoveOldEntities()
        {
            foreach (var ent in MyEntities.GetEntities())
            {
                if (ent is MyPlanet)
                {
                    //Re-Add planet updates 
                    MyEntities.RegisterForUpdate(ent);
                    continue;
                }

                ent.Close();
            }
        }

        private static void ResetCoordinateSystems()
        {
            AccessTools.Field(typeof(MyCoordinateSystem), "m_lastCoordSysId").SetValue(MyCoordinateSystem.Static, 1L);
            AccessTools.Field(typeof(MyCoordinateSystem), "m_localCoordSystems").SetValue(MyCoordinateSystem.Static, new Dictionary<long, MyLocalCoordSys>());
        }   







    }
}
