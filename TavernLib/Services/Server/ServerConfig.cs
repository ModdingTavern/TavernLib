using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace TavernLib.Services.Server
{
    public class ServerConfig
    {
        [YamlMember(Alias = "name")] public string Name { get; set; }
        [YamlMember(Alias = "description")] public string Description { get; set; }
        
        [YamlMember(Alias = "ports")] public Dictionary<string, int> Ports { get; set; }
        public int Game => Ports["game"];
        public int Forest => Ports["forest"];
        public int Rcon => Ports["rcon"];
        
        [YamlMember(Alias = "max-players")] public int MaxPlayers { get; set; }
        [YamlMember(Alias = "pc-world")] public bool PcWorld { get; set; }
        [YamlMember(Alias = "rcon-password")] public string RconPassword { get; set; }
        [YamlMember(Alias = "listing-token")] public string ListingToken { get; set; }
    }
}