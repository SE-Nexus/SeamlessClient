using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game;
using static Sandbox.Game.World.MySession;
using System.IO;
using VRage.Scripting;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using HarmonyLib;
using SeamlessClient.Models;
using VRage;

namespace SeamlessClient.Components
{
    /*  Keen compiles mods into assemblies. Unfortunetly, we cannot trust these assemblies after calling unload due to changed variables as the ASSEMBLY level. aka default types
     *  To fix this we need to go further and actually attempt to call the assembly to be unloaded, then reload the assembly and get the update type to be initiated.
     *  
     *  This might be a time consuming process? We will just have to see. Ideally would like to keep the whole mod script reloading down to like less than a second or two for N amount of mods. Might
     *  be able to speed up loading compared to keens way too
     * 
     * 
     */
    public class ModReloader
    {
        Dictionary<Type, MySessionComponentBase> sessionsBeingRemoved = new Dictionary<Type, MySessionComponentBase>();
        Dictionary<Type, MySessionComponentBase> sessionsToUnload = new Dictionary<Type, MySessionComponentBase>();
        private static ModCache modCache = new ModCache();
        private bool Unloading = false;
        private long Ticks = 0;


        public void Patch(Harmony patcher)
        {
            MethodInfo assemblyLoad = AccessTools.Method(typeof(Assembly), "Load", new Type[] { typeof(byte[]) });

            var patchAsmLoad = PatchUtils.GetMethod(this.GetType(), "AssemblyLoad");
            patcher.Patch(assemblyLoad, postfix: patchAsmLoad);

        }

        private static void AssemblyLoad(Assembly __result, byte[] rawAssembly)
        {
            //This will get all of the mods being loading into the game. Worry about saving/loading later
            modCache.AddModToCache(__result, rawAssembly);
        }

        public void Update()
        {
            if (!Unloading)
                return;

            if (Ticks == 1)
            {
                foreach (var item in sessionsToUnload)
                {
                    //Calls the component to be unloaded
                    //item.Value.UnloadDataConditional();
                    Seamless.TryShow($"Unloaded {item.Value.ModContext.ModName}");
                }

                sessionsToUnload.Clear();
                Unloading = false;
                Ticks = 0;
            }
            else
            {
                Ticks++;
            }
        }


        public void UnloadModSessionComponents()
        {
            CachingDictionary<Type, MySessionComponentBase> sessionComponents = (CachingDictionary<Type, MySessionComponentBase>)typeof(MySession).GetField("m_sessionComponents", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(MySession.Static);
            List<MySessionComponentBase> m_loadOrder = (List<MySessionComponentBase>)typeof(MySession).GetField("m_loadOrder", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(MySession.Static);
            List<MySessionComponentBase> m_sessionComponentForDraw = (List<MySessionComponentBase>)typeof(MySession).GetField("m_sessionComponentForDraw", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(MySession.Static);
            List<MySessionComponentBase> m_sessionComponentForDrawAsync = (List<MySessionComponentBase>)typeof(MySession).GetField("m_sessionComponentForDrawAsync", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(MySession.Static);
            Dictionary<int, SortedSet<MySessionComponentBase>> m_sessionComponentsForUpdate = (Dictionary<int, SortedSet<MySessionComponentBase>>)typeof(MySession).GetField("m_sessionComponentsForUpdate", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(MySession.Static);

            foreach (var component in sessionComponents)
            {
                if (component.Value.ModContext != null && !component.Value.ModContext.IsBaseGame)
                {
                    Seamless.TryShow($"{component.Key.FullName}");
                    sessionsBeingRemoved.Add(component.Key, component.Value);
                    sessionsToUnload.Add(component.Key, component.Value);
                }
            }


            /* Remove all */


            //Remove from session components
            sessionComponents.Clear();

            //Remove from draw
            m_sessionComponentForDraw.Clear();
            m_sessionComponentForDrawAsync.Clear();

            //Remove from update
            foreach (var item in m_sessionComponentsForUpdate.Values)
            {
                item.Clear();
            }

            //Remove from load order
            m_loadOrder.Clear();

            Unloading = true;
        }

        public void AddModComponents()
        {
            List<MySessionComponentBase> m_sessionComponentForDraw = (List<MySessionComponentBase>)typeof(MySession).GetField("m_sessionComponentForDraw", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(MySession.Static);


            List<MySessionComponentBase> newSessions = new List<MySessionComponentBase>();
            int newLoadedmods = 0;
            foreach (var item in sessionsBeingRemoved)
            {

                //If it fails, skip the mod? Or try to use old type? (May Fail to load)
                if (!modCache.TryGetModAssembly(item.Value.ModContext.ModItem.PublishedFileId, out Assembly newModAssembly))
                    continue;


                Type newType = newModAssembly.GetType(item.Key.FullName);
                MySessionComponentBase mySessionComponentBase = (MySessionComponentBase)Activator.CreateInstance(newType);
                mySessionComponentBase.ModContext = item.Value.ModContext;

                
                MySession.Static.RegisterComponent(mySessionComponentBase, mySessionComponentBase.UpdateOrder, mySessionComponentBase.Priority);
                newSessions.Add(mySessionComponentBase);

                m_sessionComponentForDraw.Add(mySessionComponentBase);
                newLoadedmods++;

            }

            //Will check toi see if session is actually loaded before calling load
            MySession.Static.LoadDataComponents();
            sessionsBeingRemoved.Clear();
            typeof(MySession).GetMethod("InitDataComponents", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(MySession.Static, null);

            //Call before start
            foreach (var session in newSessions)
                session.BeforeStart();


            Seamless.TryShow($"Loaded {newLoadedmods} mods");
        }
    }
}
