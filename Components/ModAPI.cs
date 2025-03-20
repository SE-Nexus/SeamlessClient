using HarmonyLib;
using Sandbox.Game.World;
using SeamlessClient.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;

namespace SeamlessClient.Components
{
    public class ModAPI : ComponentBase
    {
        private static FieldInfo SessionComponents;
        private static List<MySessionComponentBase> LoadedMods = new List<MySessionComponentBase>();



        public override void Patch(Harmony patcher)
        {

            var AddModAssembly = PatchUtils.GetMethod(typeof(MySession), "TryRegisterSessionComponent");
            patcher.Patch(AddModAssembly, postfix: new HarmonyMethod(Get(typeof(ModAPI), nameof(AddModAssembly))));

            SessionComponents = PatchUtils.GetField(typeof(MySession), "m_sessionComponents");


            base.Patch(patcher);
        }

        public static void ClearCache()
        {
            LoadedMods.Clear();
        }

        public static void StartModSwitching()
        {
            foreach(var mod in LoadedMods)
            {
                if (!mod.Loaded)
                    continue;

                MethodInfo Unload = PatchUtils.GetMethod(mod.GetType(), "SeamlessServerUnloaded");
                
                if (Unload == null)
                    continue;

                try
                {
                    Unload.Invoke(mod, null);
                }
                catch (Exception ex)
                {
                    Seamless.TryShow(ex, "Error during modAPI unloading!");
                }
            }
        }

        public static void ServerSwitched()
        {
            foreach (var mod in LoadedMods)
            {
                if (!mod.Loaded)
                    continue;
                MethodInfo Load = PatchUtils.GetMethod(mod.GetType(), "SeamlessServerLoaded");
                if (Load == null)
                    continue;
                try
                {
                    Load.Invoke(mod, null);
                }
                catch (Exception ex)
                {
                    Seamless.TryShow(ex, "Error during modAPI loading!");
                }
            }
        }


        public static void AddModAssembly(Type type, bool modAssembly, MyModContext context)
        {
            if (!modAssembly || context == null)
                return;

            CachingDictionary<Type, MySessionComponentBase> dict = (CachingDictionary<Type, MySessionComponentBase>)SessionComponents.GetValue(MySession.Static);
            dict.TryGetValue(type, out MySessionComponentBase component);
            Seamless.TryShow($"Loading Mod Assembly: {component.ComponentType.FullName}");
            LoadedMods.Add(component);
        }


    }
}
