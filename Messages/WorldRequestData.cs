using ProtoBuf;
using Sandbox.Engine.Multiplayer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.ObjectBuilders.Private;

namespace SeamlessClient.Messages
{
    [ProtoContract]
    public class WorldRequest
    {
        //private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        [ProtoMember(1)] public ulong PlayerID;
        [ProtoMember(2)] public long IdentityID;
        [ProtoMember(3)] public string PlayerName;
        [ProtoMember(4)] public byte[] WorldData;

        [ProtoMember(5)] public MyObjectBuilder_Gps GpsCollection { get; set; }

        public WorldRequest()
        {
        }

        public WorldRequest(ulong playerId, long playerIdentity, string name)
        {
            this.PlayerID = playerId;
            this.PlayerName = name;
            this.IdentityID = playerIdentity;
        }

       

        public void SerializeWorldData(MyObjectBuilder_World WorldData)
        {
            var cleanupData = typeof(MyMultiplayerServerBase).GetMethod("CleanUpData",
                BindingFlags.Static | BindingFlags.NonPublic, null, new[]
                {
                    typeof(MyObjectBuilder_World),
                    typeof(ulong),
                    typeof(long),
                }, null);
            object[] data = { WorldData, PlayerID, IdentityID };
            cleanupData?.Invoke(null, data);
            WorldData = (MyObjectBuilder_World)data[0];
            using (var memoryStream = new MemoryStream())
            {
                MyObjectBuilderSerializerKeen.SerializeXML(memoryStream, WorldData,
                    MyObjectBuilderSerializerKeen.XmlCompression.Gzip);
                this.WorldData = memoryStream.ToArray();
                //Log.Warn("Successfully Converted World");
            }
        }

        public MyObjectBuilder_World DeserializeWorldData()
        {
            MyObjectBuilderSerializerKeen.DeserializeGZippedXML<MyObjectBuilder_World>(new MemoryStream(WorldData),
                out var objectBuilder);
            objectBuilder.Checkpoint.Gps.Dictionary.Add(IdentityID, GpsCollection);

            return objectBuilder;
        }
    }
}
