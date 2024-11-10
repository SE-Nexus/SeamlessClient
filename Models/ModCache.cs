using ProtoBuf;
using SeamlessClient.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VRage.Serialization;

namespace SeamlessClient.Models
{
    [ProtoContract]
    public class ModCache
    {

        [ProtoMember(1)]
        public List<ModByte> CachedMods { get; set; } = new List<ModByte>();



        public void AddModToCache(Assembly asm, byte[] raw)
        {
            //Get the modID from the loaded assembly name
            ulong? modid = GetLeadingNumber(asm.FullName);
            if (!modid.HasValue || modid.Value == 0)
                return;

            //Check to see if the loading mod is already in our cache
            if(CachedMods.Any(x => x.ModID == modid.Value))
                return;


            ModByte mod = new ModByte();
            mod.ModID = modid.Value;
            mod.AssemblyBytes = raw;

            CachedMods.Add(mod);
        }

        public static void SaveToFile(ModCache cache)
        {
            byte[] data = NetUtils.Serialize(cache);
            File.WriteAllBytes("", data);
        }
        public ModCache LoadFromFile() 
        {
            byte[] data = File.ReadAllBytes("");
            return NetUtils.Deserialize<ModCache>(data);
        }

        public bool TryGetModAssembly(ulong modid, out Assembly asm)
        {
            asm = null;
            if (modid == 0)
                return false;

            ModByte mod = CachedMods.FirstOrDefault(x => x.ModID == modid);
            if(mod == null)
                return false;

            //Compiles new assembly
            try
            {
                asm = mod.GetNewAssembly();
                return true;
            }
            catch (Exception ex)
            {

                return false;
            }
        }
        static ulong? GetLeadingNumber(string assemblyName)
        {
            Match match = Regex.Match(assemblyName, @"^\d+");
            return match.Success ? ulong.Parse(match.Value) : (ulong?)null;
        }

    }
}
