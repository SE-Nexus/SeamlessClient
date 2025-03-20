using HarmonyLib;
using Sandbox.Game.Gui;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World.Generator;
using Sandbox.Game.World;
using SeamlessClient.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VRage.GameServices;
using VRage.Network;

namespace SeamlessClient.Components
{
    public class PatchUtils : ComponentBase
    {
        /* All internal classes Types */
        public static readonly Type ClientType =
            Type.GetType("Sandbox.Engine.Multiplayer.MyMultiplayerClient, Sandbox.Game");

        public static readonly Type SyncLayerType = Type.GetType("Sandbox.Game.Multiplayer.MySyncLayer, Sandbox.Game");

        public static readonly Type MyTransportLayerType =
            Type.GetType("Sandbox.Engine.Multiplayer.MyTransportLayer, Sandbox.Game");

        public static readonly Type MySessionType = Type.GetType("Sandbox.Game.World.MySession, Sandbox.Game");

        public static readonly Type VirtualClientsType =
            Type.GetType("Sandbox.Engine.Multiplayer.MyVirtualClients, Sandbox.Game");

        public static readonly Type GUIScreenChat = Type.GetType("Sandbox.Game.Gui.MyGuiScreenChat, Sandbox.Game");

        public static readonly Type MyMultiplayerClientBase =
            Type.GetType("Sandbox.Engine.Multiplayer.MyMultiplayerClientBase, Sandbox.Game");

        public static readonly Type MySteamServerDiscovery =
            Type.GetType("VRage.Steam.MySteamServerDiscovery, Vrage.Steam");

        public static readonly Type MyEntitiesType =
            Type.GetType("Sandbox.Game.Entities.MyEntities, Sandbox.Game");

        public static readonly Type MySlimBlockType =
            Type.GetType("Sandbox.Game.Entities.Cube.MySlimBlock, Sandbox.Game");

        public static readonly Type MyLoadingScreenQuote =
            Type.GetType("Sandbox.Game.Screens.Helpers.MyLoadingScreenQuote, Sandbox.Game");

        public static readonly Type MyLoadingScreenHint =
    Type.GetType("Sandbox.Game.Screens.Helpers.MyLoadingScreenHint, Sandbox.Game");

        public static readonly Type MyLoadingPerformance =
Type.GetType("Sandbox.Engine.Utils.MyLoadingPerformance, Sandbox.Game");

        public static readonly Type MyLoadingScreenText =
Type.GetType("Sandbox.Game.Screens.Helpers.MyLoadingScreenText, Sandbox.Game");


        /* Harmony Patcher */
        private static readonly Harmony Patcher = new Harmony("SeamlessClientPatcher");


        /* Static Contructors */
        public static ConstructorInfo MySessionConstructor { get; private set; }
        public static ConstructorInfo MyMultiplayerClientBaseConstructor { get; private set; }



        /* Static MethodInfos */
        public static MethodInfo LoadPlayerInternal { get; private set; }
       




        public static event EventHandler<JoinResultMsg> OnJoinEvent;


        /* WorldGenerator */
        public static MethodInfo UnloadProceduralWorldGenerator;


        public override void Patch(Harmony patcher)
        {
         
            MySessionConstructor = GetConstructor(MySessionType, new[] { typeof(MySyncLayer), typeof(bool) });
            MyMultiplayerClientBaseConstructor = GetConstructor(MyMultiplayerClientBase, new[] { typeof(MySyncLayer) });



            /* Get Methods */
            LoadPlayerInternal = GetMethod(typeof(MyPlayerCollection), "LoadPlayerInternal");
            

            


            
            base.Patch(patcher);
        }



        #region PatchMethods

        public static MethodInfo GetMethod(Type type, string methodName)
        {

            var foundMethod = AccessTools.Method(type, methodName);
            if (foundMethod == null)
                throw new NullReferenceException($"Method for {methodName} is null!");
            return foundMethod;
        }

        public static FieldInfo GetField(Type type, string fieldName)
        {
            var foundField = AccessTools.Field(type, fieldName);
            if (foundField == null)
                throw new NullReferenceException($"Field for {fieldName} is null!");
            return foundField;
        }

        public static PropertyInfo GetProperty(Type type, string propertyName)
        {
            var foundProperty = AccessTools.Property(type, propertyName);
            if (foundProperty == null)
                throw new NullReferenceException($"Property for {propertyName} is null!");
            return foundProperty;
        }

        public static ConstructorInfo GetConstructor(Type type, Type[] types)
        {
            var foundConstructor = AccessTools.Constructor(type, types);
            if (foundConstructor == null)
                throw new NullReferenceException($"Contructor for {type.Name} is null!");
            return foundConstructor;
        }

        #endregion

    }
}
