using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using SeamlessClient.GUI;
using SeamlessClient.Messages;
using SeamlessClient.OnlinePlayersWindow;
using SeamlessClient.ServerSwitching;
using SeamlessClient.Utilities;
using Shared.Config;
using Shared.Logging;
using Shared.Plugin;
using VRage.FileSystem;
using VRage.GameServices;
using VRage.Plugins;
using VRage.Utils;

namespace SeamlessClient
{
    public class Seamless : IPlugin, ICommonPlugin
    {
        public static Version NexusVersion = new Version(1, 0, 0);
        private static Harmony SeamlessPatcher;
        public static ushort SeamlessClientNetId = 2936;
        public static bool isSeamlessServer = false;

        public static bool isDebug = false;
        private static readonly IPluginLogger Logger = new PluginLogger(Name);
        public const string Name = "SeamlessClient";

        private readonly List<ComponentBase> allComps = new List<ComponentBase>();
        private bool Initialized;
        public static Version SeamlessVersion => typeof(Seamless).Assembly.GetName().Version;
        private static Assembly thisAssembly => typeof(Seamless).Assembly;
        public IPluginLogger Log => Logger;
        public IPluginConfig Config => config?.Data;
        private PersistentConfig<PluginConfig> config;
        private static readonly string ConfigFileName = $"{Name}.cfg";
        public long Tick { get; }

        public void Init(object gameInstance)
        {
            TryShow($"Running Seamless Client Plugin v[{SeamlessVersion}]");
            SeamlessPatcher = new Harmony("SeamlessClientPatcher");
            GetComponents();
            
            var configPath = Path.Combine(MyFileSystem.UserDataPath, ConfigFileName);
            config = PersistentConfig<PluginConfig>.Load(Log, configPath);

            
            Common.SetPlugin(this);

            PatchComponents(SeamlessPatcher);
        }
        
        public void Dispose()
        {

        }

        public void Update()
        {
            if (MyAPIGateway.Multiplayer == null)
                return;

            if (!Initialized)
            {
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(SeamlessClientNetId, MessageHandler);
                InitilizeComponents();

                Initialized = true;
            }
        }

        private void GetComponents()
        {
            var failedCount = 0;
            foreach (var type in thisAssembly.GetTypes())
            {

                if (type.BaseType != typeof(ComponentBase))
                    continue;

                try
                {
                    var s = (ComponentBase)Activator.CreateInstance(type);
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
            foreach (var component in allComps)
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
            foreach (var component in allComps)
            {
                try
                {
                    component.Initilized();
                    TryShow($"Initilized {component.GetType()}");

                }
                catch (Exception ex)
                {
                    TryShow(ex, $"Failed to initialize {component.GetType()}");
                }
            }
        }

        private static void MessageHandler(ushort packetID, byte[] data, ulong sender, bool fromServer)
        {
            //Ignore anything except dedicated server
            if (!fromServer || sender == 0)
                return;

            var msg = MessageUtils.Deserialize<ClientMessage>(data);
            if (msg == null)
                return;

            //Get Nexus Version
            if (!string.IsNullOrEmpty(msg.NexusVersion))
                NexusVersion = Version.Parse(msg.NexusVersion);


            switch (msg.MessageType)
            {
                case ClientMessageType.FirstJoin:
                    TryShow("Sending First Join!");
                    var response = new ClientMessage(SeamlessVersion.ToString());
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

        public static void StartSwitch(TransferData targetServer)
        {
            if (targetServer.TargetServerId == 0)
            {
                TryShow("This is not a valid server!");
                return;
            }

            var server = new MyGameServerItem
            {
                ConnectionString = targetServer.IpAddress,
                SteamID = targetServer.TargetServerId,
                Name = targetServer.ServerName
            };


            TryShow($"Beginning Redirect to server: {targetServer.TargetServerId}");
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
                MyAPIGateway.Utilities?.ShowMessage("Seamless", message + $"\n {ex}");

            MyLog.Default?.WriteLineAndConsole($"SeamlessClient: {message} \n {ex}");
        }
        
        // ReSharper disable once UnusedMember.Global
        // This allows the plugin to open a settings modal
        public void OpenConfigDialog()
        {
            MyGuiSandbox.AddScreen(new PluginConfigDialog());
        }
    }
}