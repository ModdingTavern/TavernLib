using Alta.Networking;
using Alta.Networking.Servers;
using Alta.QuickAccessActions;
using Alta.Serialization;
using Newtonsoft.Json;
using TavernLib.Backend.Server.Configs;
using TavernLib.Services;
using TavernLib.Utils;
using UnityEngine;

namespace TavernLib.Backend;

public class EntranceMessageHandler : IService
{
    public UserConfig.User LocalUser { get; private set; } = new();


    public EntranceMessageHandler()
    {
        TavernEvents.SocketCreated.Subscribe(OnSocketCreated);
    }

    private void OnSocketCreated(ISocket socket)
    {
        socket.ConnectionCreated += SetMessageHandlerForConnection;
    }
    
    private void SetMessageHandlerForConnection(Connection connection)
    {
        TavernLogger.Msg($"Socket created, setting up receive role handler");
        if (!NetworkSceneManager.IsServer)
        {
            connection.SetHandler((MessageType)TavernMessages.ReceiveRoles, OnRolesReceived);
        }
    }
    
    private void OnRolesReceived(Connection connection, Stream stream)
    {
        TavernLogger.Msg($"Role message received");
        
        var roleCount = 0;
        stream.SerializeInteger(ref roleCount);
        LocalUser.Roles.Clear();
        TavernLogger.Msg($"Role count is {roleCount}");
        
        
        for (var i = 0; i < roleCount; i++)
        {
            string roleAtIndex = "";
            stream.SerializeString(ref roleAtIndex);
            TavernLogger.Msg($"Deserialized role {roleAtIndex}");

            LocalUser.Roles.Add(roleAtIndex);
        }
        
        TavernLogger.Warn($"ReceiveRoles result: IsMod: {LocalUser.IsModerator}, Roles: {JsonConvert.SerializeObject(LocalUser.Roles)}");
    }
}