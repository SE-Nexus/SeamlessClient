using HarmonyLib;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Networking;
using Sandbox.Engine.Utils;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using SeamlessClient.Utilities;
using SpaceEngineers.Game.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.GameServices;
using VRage.Library.Utils;
using VRage.Network;
using VRageRender;
using EmptyKeys.UserInterface.Generated.StoreBlockView_Bindings;
using System.Security.Cryptography;
using VRage.Groups;
using VRage.Game.ModAPI;
using VRage;
using static Sandbox.ModAPI.MyModAPIHelper;
using NLog.Targets;
using MyMultiplayer = Sandbox.Engine.Multiplayer.MyMultiplayer;
using static Sandbox.Engine.Networking.MyNetworkWriter;
using System.Collections.Concurrent;
using VRage.Game.Entity;
using VRageRender.Effects;
using VRage.Scripting;
using VRage.Utils;

namespace SeamlessClient.Components
{
    public class SeamlessSwitcher : ComponentBase
    {
        private bool isSwitchingServer = false;

        private PropertyInfo _MyGameServerItemProperty;
        private PropertyInfo _MultiplayerServerID;
        private static MethodInfo PauseClient;
        private MethodInfo _SendPlayerData;
        private MethodInfo _SendFlush;
        private MethodInfo _ClearTransportLayer;
        private FieldInfo _NetworkWriteQueue;
        private FieldInfo _TransportLayer;

        private MethodInfo _OnConnectToServer;
        public static SeamlessSwitcher Instance;

        private static List<MyCubeGrid> allGrids = new List<MyCubeGrid>();

        private long _OriginalCharacterEntity = -1;
        private long _OriginalGridEntity = -1;
        private ulong TargetServerID;
        private static bool PreventRPC = false;
        private static bool StartPacketCheck = false;

        public SeamlessSwitcher() 
        {
            Instance = this;    
        }


        public override void Patch(Harmony patcher)
        {
            _MyGameServerItemProperty = PatchUtils.GetProperty(PatchUtils.ClientType, "Server");
            _MultiplayerServerID = PatchUtils.GetProperty(typeof(MyMultiplayerBase), "ServerId");

            _OnConnectToServer = PatchUtils.GetMethod(PatchUtils.ClientType, "OnConnectToServer");
            PauseClient = PatchUtils.GetMethod(PatchUtils.MyMultiplayerClientBase, "PauseClient");

            _SendPlayerData = PatchUtils.GetMethod(PatchUtils.ClientType, "SendPlayerData");
            _ClearTransportLayer = PatchUtils.GetMethod(PatchUtils.MyTransportLayerType, "Clear");


            List<string> st = AccessTools.GetMethodNames(PatchUtils.MyMultiplayerClientBase);

            var _SendRPC = PatchUtils.GetMethod(PatchUtils.MyMultiplayerClientBase, "VRage.Replication.IReplicationClientCallback.SendEvent");

            var preSendRPC = PatchUtils.GetMethod(this.GetType(), "PreventEvents");
            _SendFlush = PatchUtils.GetMethod(PatchUtils.MyTransportLayerType, "SendFlush");
            _NetworkWriteQueue = PatchUtils.GetField(typeof(MyNetworkWriter), "m_packetsToSend");
            _TransportLayer = PatchUtils.GetField(typeof(MySyncLayer), "TransportLayer");


            var method = AccessTools.Method(PatchUtils.MyTransportLayerType, "SendMessage", new Type[] { typeof(MyMessageId), typeof(IPacketData), typeof(bool), typeof(EndpointId), typeof(byte) });
            var patchSend = PatchUtils.GetMethod(this.GetType(), "SendMessage_Patch");





            patcher.Patch(method, prefix: patchSend);
            patcher.Patch(_SendRPC, prefix: preSendRPC);



            base.Patch(patcher);
        }

        public static void SendMessage_Patch(MyMessageId id, IPacketData data, bool reliable, EndpointId endpoint, byte index = 0)
        {
            if (!StartPacketCheck)
                return;

            MyLog.Default?.WriteLineAndConsole($"{System.Environment.StackTrace}");
            Seamless.TryShow($"Id:{id} -> {endpoint}");
        }


        public void StartSwitch(MyGameServerItem TargetServer, MyObjectBuilder_World TargetWorld)
        {
            isSwitchingServer = true;
            TargetServerID = TargetServer.GameID;
            MyReplicationClient localMPClient = (MyReplicationClient)MyMultiplayer.Static.ReplicationLayer;
            _OriginalCharacterEntity = MySession.Static.LocalCharacter?.EntityId ?? 0;

            if (MySession.Static.LocalCharacter?.Parent != null && MySession.Static.LocalCharacter.Parent is MyCockpit cockpit)
            {
                _OriginalGridEntity = cockpit.CubeGrid.EntityId;
            }
               


            UnloadCurrentServer(localMPClient);


            /* Fix any weird compatibilities */
            MySandboxGame.Static.SessionCompatHelper.FixSessionComponentObjectBuilders(TargetWorld.Checkpoint, TargetWorld.Sector);

            /* Set New Multiplayer Stuff */
            _MyGameServerItemProperty.SetValue(MyMultiplayer.Static, TargetServer);
            _MultiplayerServerID.SetValue(MyMultiplayer.Static, TargetServer.SteamID);

            /* Connect To Server */
            MyGameService.ConnectToServer(TargetServer, delegate (JoinResult joinResult)
            {
                MySandboxGame.Static.Invoke(delegate
                {
                    Seamless.TryShow("Connected to server!");
                    _OnConnectToServer.Invoke(MyMultiplayer.Static, new object[] { joinResult });
                    OnUserJoined(joinResult);

                }, "OnConnectToServer");
            });

        }

        private void UnloadCurrentServer(MyReplicationClient localMPClient)
        {
            
            //Close and Cancel any screens (Medical or QuestLog)
            MySessionComponentIngameHelp component = MySession.Static.GetComponent<MySessionComponentIngameHelp>();
            component?.TryCancelObjective();
            MyGuiScreenMedicals.Close();

            MethodInfo removeClient = PatchUtils.GetMethod(PatchUtils.ClientType, "MyMultiplayerClient_ClientLeft");
            foreach (var connectedClient in Sync.Clients.GetClients())
            {
                if (connectedClient.SteamUserId == Sync.MyId || connectedClient.SteamUserId == Sync.ServerId)
                    continue;

                removeClient.Invoke(MyMultiplayer.Static, new object[] { connectedClient.SteamUserId, MyChatMemberStateChangeEnum.Left });
            }




            /* Sends disconnect message to outbound server */
            MyControlDisconnectedMsg myControlDisconnectedMsg = default(MyControlDisconnectedMsg);
            myControlDisconnectedMsg.Client = Sync.MyId;
            typeof(MyMultiplayerBase).GetMethod("SendControlMessage", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(typeof(MyControlDisconnectedMsg)).Invoke(MyMultiplayer.Static, new object[] { Sync.ServerId, myControlDisconnectedMsg, true });
            MyGameService.Peer2Peer.CloseSession(Sync.ServerId);
            MyGameService.DisconnectFromServer();


           

            //Remove old signals
            MyHud.GpsMarkers.Clear();
            MyHud.LocationMarkers.Clear();
            MyHud.HackingMarkers.Clear();


            Seamless.TryShow($"2 Streaming: {localMPClient.HasPendingStreamingReplicables} - LastMessage: {localMPClient.LastMessageFromServer}");
            Seamless.TryShow($"2 NexusMajor: {Seamless.NexusVersion.Major} - ConrolledEntity {MySession.Static.ControlledEntity == null} - HumanPlayer {MySession.Static.LocalHumanPlayer == null} - Character {MySession.Static.LocalCharacter == null}");

            UnloadOldEntities();
            ResetReplicationTime(false);

            //MyMultiplayer.Static.ReplicationLayer.Disconnect();
            //MyMultiplayer.Static.ReplicationLayer.Dispose();
            //MyMultiplayer.Static.Dispose();
        }

        private static bool PreventEvents(IPacketData data, bool reliable)
        {
            return !PreventRPC;
        }

        private void UnloadOldEntities()
        {

            
            foreach (var ent in MyEntities.GetEntities().ToList())
            {
                if (ent is MyPlanet || ent is MyCubeGrid)
                    continue;

                if(ent.EntityId == _OriginalCharacterEntity && ent is MyCharacter character)
                {
                    continue;
                }

                ent.Close();
            }

            List<IMyGridGroupData> grids = new List<IMyGridGroupData>();
            
            foreach (var gridgroup in MyCubeGridGroups.GetGridGroups(VRage.Game.ModAPI.GridLinkTypeEnum.Physical, grids))
            {

                List<IMyCubeGrid> localGrids = new List<IMyCubeGrid>();
                localGrids = gridgroup.GetGrids(localGrids);

                if (localGrids.Count == 0)
                    continue;

                if (localGrids.Any(x => x.EntityId == _OriginalGridEntity))
                {

                    foreach(var grid in localGrids)
                    {
                        MyEntity ent = grid as MyEntity;
                        allGrids.Add((MyCubeGrid)grid);
                        grid.Synchronized = false;

                        ent.SyncFlag = false;
                        ent.Save = false;

                        

                    }


                    continue;
                }
                   

                //Delete
                foreach (var grid in localGrids)
                {
                    grid.Close();
                }

            }

            //Following event doesnt clear networked replicables
            MyMultiplayer.Static.ReplicationLayer.Dispose();
            ClearClientReplicables();



            PreventRPC = true;
            StartPacketCheck = true;

            //_ClearTransportLayer.Invoke(_TransportLayer.GetValue(MyMultiplayer.Static.SyncLayer), null);


        }

        private void ClearClientReplicables()
        {
            MyReplicationClient replicationClient = (MyReplicationClient)MyMultiplayer.Static.ReplicationLayer;

            Dictionary<NetworkId,IMyNetObject> networkedobjs = (Dictionary<NetworkId, IMyNetObject>)typeof(MyReplicationLayer).GetField("m_networkIDToObject", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(replicationClient);

            MethodInfo destroyreplicable = typeof(MyReplicationClient).GetMethod("ReplicableDestroy", BindingFlags.Instance | BindingFlags.NonPublic);

            int i = 0;
            foreach(var obj in networkedobjs)
            {
                destroyreplicable.Invoke(replicationClient, new object[] { obj.Value, true });
                i++;
            }

            Seamless.TryShow($"Cleared {i} replicables");
        }

        private void OnUserJoined(JoinResult joinResult)
        {
            isSwitchingServer = false;

            if (joinResult != JoinResult.OK)
                return;

            Seamless.TryShow($"OnUserJoin! Result: {joinResult}");
            LoadDestinationServer();



            _SendPlayerData.Invoke(MyMultiplayer.Static, new object[] { MyGameService.OnlineName });
        }

        private void LoadDestinationServer()
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
            foreach (var client in Sync.Clients.GetClients())
            {
                clients.Add(client.SteamUserId);
                Seamless.TryShow($"ADDING {client.SteamUserId} - {Sync.MyId}");
            }

            foreach (var client in clients)
            {
                if (client == TargetServerID || client == Sync.MyId)
                    continue;

                Seamless.TryShow($"REMOVING {client}");
                Sync.Clients.RemoveClient(client);
            }


            typeof(MySandboxGame).GetField("m_pauseStackCount", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, 0);

            Seamless.TryShow($"6 NexusMajor: {Seamless.NexusVersion.Major} - ConrolledEntity {MySession.Static.ControlledEntity == null} - HumanPlayer {MySession.Static.LocalHumanPlayer == null} - Character {MySession.Static.LocalCharacter == null}");
            Seamless.TryShow($"6 Streaming: {clienta.HasPendingStreamingReplicables} - LastMessage: {clienta.LastMessageFromServer}");


          
           


            
            ResetReplicationTime(true);
            //MyPlayerCollection.ChangePlayerCharacter(MySession.Static.LocalHumanPlayer, MySession.Static.LocalCharacter, MySession.Static.LocalCharacter);
            // Allow the game to start proccessing incoming messages in the buffer
            //MyMultiplayer.Static.StartProcessingClientMessages();
            //Send Client Ready
            ClientReadyDataMsg clientReadyDataMsg = default(ClientReadyDataMsg);
            clientReadyDataMsg.ForcePlayoutDelayBuffer = MyFakes.ForcePlayoutDelayBuffer;
            clientReadyDataMsg.UsePlayoutDelayBufferForCharacter = true;
            clientReadyDataMsg.UsePlayoutDelayBufferForJetpack = true;
            clientReadyDataMsg.UsePlayoutDelayBufferForGrids = true;
            ClientReadyDataMsg msg = clientReadyDataMsg;
            clienta.SendClientReady(ref msg);

            PreventRPC = false;
            //_ClearTransportLayer.Invoke(_TransportLayer.GetValue(MyMultiplayer.Static.SyncLayer), null);

            StartEntitySync();
            Seamless.SendSeamlessVersion();


          
           


            PauseClient.Invoke(MyMultiplayer.Static, new object[] { false });
            MySandboxGame.PausePop();

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
            MyGuiScreenHudSpace.Static?.RecreateControls(true);



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


        public void ResetReplicationTime(bool ClientReady)
        {



       
            if (!ClientReady)
            {
                PatchUtils.GetField(typeof(MyReplicationClient), "m_lastServerTimestamp").SetValue(MyMultiplayer.Static.ReplicationLayer, MyTimeSpan.Zero);
                PatchUtils.GetField(typeof(MyReplicationClient), "m_lastServerTimeStampReceivedTime").SetValue(MyMultiplayer.Static.ReplicationLayer, MyTimeSpan.Zero);
                PatchUtils.GetField(typeof(MyReplicationClient), "m_clientStartTimeStamp").SetValue(MyMultiplayer.Static.ReplicationLayer, MyTimeSpan.Zero);
                PatchUtils.GetField(typeof(MyReplicationClient), "m_lastTime").SetValue(MyMultiplayer.Static.ReplicationLayer, MyTimeSpan.Zero);
                PatchUtils.GetField(typeof(MyReplicationClient), "m_lastClientTime").SetValue(MyMultiplayer.Static.ReplicationLayer, MyTimeSpan.Zero);
                PatchUtils.GetField(typeof(MyReplicationClient), "m_lastServerTime").SetValue(MyMultiplayer.Static.ReplicationLayer, MyTimeSpan.Zero);
                PatchUtils.GetField(typeof(MyReplicationClient), "m_lastClientTimestamp").SetValue(MyMultiplayer.Static.ReplicationLayer, MyTimeSpan.Zero);
                PatchUtils.GetField(typeof(MyReplicationClient), "m_lastStateSyncPacketId").SetValue(MyMultiplayer.Static.ReplicationLayer,(byte)0);
                PatchUtils.GetField(typeof(MyReplicationClient), "m_lastStreamingPacketId").SetValue(MyMultiplayer.Static.ReplicationLayer, (byte)0);
                PatchUtils.GetField(typeof(MyReplicationClient), "m_lastStreamingPacketId").SetValue(MyMultiplayer.Static.ReplicationLayer, (byte)0);
                //PatchUtils.GetField(typeof(MyReplicationClient), "m_acks").SetValue(MyMultiplayer.Static.ReplicationLayer, new List<byte>());
            }




            PatchUtils.GetField(typeof(MyReplicationClient), "m_clientReady").SetValue(MyMultiplayer.Static.ReplicationLayer, ClientReady);
        }



    }
}
