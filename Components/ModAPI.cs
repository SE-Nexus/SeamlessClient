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
    /// <summary>
    /// ModAPI so that mods can register seamless events
    /// </summary>
    public class ModAPI : ComponentBase
    {
        private static FieldInfo SessionComponents;
        private static List<LoadedMod> LoadedMods = new List<LoadedMod>();

        public class LoadedMod
        {
            public MethodInfo SeamlessServerUnload;
            public MethodInfo SeamlessServerLoad;
            public MySessionComponentBase ModSession;
        }


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

            Seamless.TryShow($"Invoking SeamlessUnload API on {LoadedMods.Count} mods!");
            foreach (var mod in LoadedMods)
            {
                try
                {
                    mod.SeamlessServerUnload?.Invoke(mod.ModSession, null);
                }
                catch (Exception ex)
                {
                    Seamless.TryShow(ex, $"Error during modAPI unloading! {mod.SeamlessServerUnload.Name}");
                }
            }
        }

        public static void ServerSwitched()
        {
            Seamless.TryShow($"Invoking SeamlessServerLoad API on {LoadedMods.Count} mods!");
            foreach (var mod in LoadedMods)
            {
                try
                {
                    mod.SeamlessServerLoad?.Invoke(mod.ModSession, null);
                }
                catch (Exception ex)
                {
                    Seamless.TryShow(ex, $"Error during modAPI loading! {mod.SeamlessServerLoad.Name}");
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

            MethodInfo Load = AccessTools.Method(component.ComponentType, "SeamlessServerLoaded");
            MethodInfo Unload = AccessTools.Method(component.ComponentType, "SeamlessServerUnloaded");

            if(Load != null || Unload != null)
            {

                LoadedMod newMod = new LoadedMod();
                newMod.SeamlessServerLoad = Load;
                newMod.SeamlessServerUnload = Unload;
                newMod.ModSession = component;

                Seamless.TryShow($"Mod Assembly: {component.ComponentType.FullName} has SeamlessServerLoaded/SeamlessServerUnloaded methods!");

                LoadedMods.Add(newMod);
                return;
            }




          
        }


    }
}
