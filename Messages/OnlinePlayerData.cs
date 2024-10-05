using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeamlessClient.Messages
{
    [ProtoContract]
    public class OnlinePlayerData
    {
        [ProtoMember(10)] public List<OnlinePlayer> OnlinePlayers = new List<OnlinePlayer>();

        [ProtoMember(12)] public int currentServerID;
    }


    [ProtoContract]
    public class OnlinePlayer
    {
        [ProtoMember(1)] public string PlayerName;

        [ProtoMember(2)] public ulong SteamID;

        [ProtoMember(3)] public long IdentityID;

        [ProtoMember(4)] public int OnServer;

        public OnlinePlayer(string PlayerName, ulong SteamID, long IdentityID, int OnServer)
        {
            this.PlayerName = PlayerName;
            this.SteamID = SteamID;
            this.IdentityID = IdentityID;
            this.OnServer = OnServer;
        }

        public OnlinePlayer()
        {
        }
    }
}
