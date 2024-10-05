using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Utils;

namespace SeamlessClient.Components
{
    internal class SeamlessScriptManager
    {
        /*  Sandbox.Game.World.MyScriptManager 
         *  
         *  ScriptManager handles all mod script compiling and loading. Stores all compilations as various dictionaries like below:
         *  Looks like TryAddEntityScripts(,) can be called whenever to pass in the mod context and the compiled assembly on the fly
         *  
         *  Possibly to dynamically load/unload mods?
         */


        /* Dictionary<MyModContext, HashSet<MyStringId>> ScriptsPerMod ->   
         * 
         * Use in Sandbox.Game.World.MySession.RegisterComponentsFromAssembly
         * Primarily MySessionComponentDescriptor/Session Component
         */

        /* Dictionary<MyStringId, Assembly> Scripts ->   
         * 
         * Use in Sandbox.Game.World.MySession.RegisterComponentsFromAssembly
         * Stores actual assemblies for all modded scripts
         */


        /* Dictionary<Type, HashSet<Type>> EntityScripts ->  
         * 
         * Use in Sandbox.Game.Entities.MyEntityFactory.AddScriptGameLogic
         * List of Type entity Scripts. Activator.CreatInstance called on entity creation
         */

        /* Dictionary<Tuple<Type, string>, HashSet<Type>> SubEntityScripts -> 
         * 
         * Use in Sandbox.Game.Entities.MyEntityFactory.AddScriptGameLogic
         * List of Type && SubType entity Scripts. Activator.CreatInstance called on entity creation
         */

        /* Dictionary<string, Type> StatScripts ->
         * 
         * Use in Sandbox.Game.Components.MyEntityStatComponent
         * No idea what this is but prob need to deal with it
         */


        /* Dictionary<MyStringId, Type> InGameScripts -> */

        /* Dictionary<MyStringId, StringBuilder> InGameScriptsCode -> */

        /* Dictionary<Type, MyModContext> TypeToModMap -> 
         * 
         * Use in Sandbox.Game.Entities.MyEntityFactory.AddScriptGameLogic
         * Some sort of type to mod logic component?
         */

    }
}
