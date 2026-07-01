using System;
using System.Linq;
using Alta.Api.DataTransferModels.Models.Responses;
using HarmonyLib;

namespace TavernLib.ServerBrowser
{
    [HarmonyPatch]
    class ServerBrowserPatches
    {
        [HarmonyPatch(typeof(ServerBoard), nameof(ServerBoard.SortAndUpdateServersList)), HarmonyPrefix]
        static void LoadLocalServer(ServerBoard __instance)
        {
            if (__instance.type == ServerBoardType.MyServers)
            {
                var serverList = __instance.lastFilteredServers.ToList();

                Tavern.Services.ServerMounter.LoadAllReferences();
                foreach (var serverReferences in Tavern.Services.ServerMounter.ServerReferences) 
                    serverList.Add(serverReferences.ServerInfo);
                
                serverList.Sort((x, y) => String.CompareOrdinal(x.Name, y.Name));
                
                
                var localServerInfo = MenuSettings.Instance.LocalGameServerInfo(0);
                localServerInfo.Description = "This is your own local server, make sure it's running before you try to join!";
                localServerInfo.OnlinePlayers = Array.Empty<UserInfo>();

                serverList.Insert(0, localServerInfo);
                __instance.lastFilteredServers = serverList;
            }
        }
    }
}