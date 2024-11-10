using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SeamlessClient.Models
{
    [ProtoContract]
    public class ModByte
    {
        [ProtoMember(1)]
        public ulong ModID { get; set; }

        [ProtoMember(2)]
        public byte[] AssemblyBytes { get; set; }



        public Assembly GetNewAssembly()
        {
            return Assembly.Load(AssemblyBytes);
        }

    }
}
