using System;
using System.Collections.Generic;
using System.Linq;
using Alta.Chunks;
using Alta.Console.Commands;
using Alta.Networking;
using UnityEngine;

namespace TavernLib.Patches;

public static class SelectFixPatch
{
    public delegate IEnumerable<NetworkEntityInfo> FindObjects(Player player, float distance = 10f);
    public static IEnumerable<NetworkEntityInfo> SelectionFix(FindObjects orig, Player player, float distance = 10f)
    {
        try
        {
            if (player?.PlayerController == null)
            {
                TavernLogger.Warn($"PlayerController was null when using 'select find' command");
                return Array.Empty<NetworkEntityInfo>().AsEnumerable();
            }
        
            var entityCollection = new HashSet<NetworkEntity>();
            var playerPos = player.PlayerController.transform.position;
        
            foreach (var networkEntity in Chunk.ChunksByIndex.Values.SelectMany(chunk => chunk.Entities.Entities))
            {
                if (networkEntity == null)
                {
                    TavernLogger.Warn($"NetworkEntity {networkEntity.SafeName} was null and isn't?? {networkEntity.Chunk.ChunkIdentifier}");
                    continue;
                }
            
                if (Vector3.Distance(networkEntity.transform.position, playerPos) <= distance)
                {
                    entityCollection.Add(networkEntity.PrefabRoot);
                }
            }
        
            return entityCollection.Select<NetworkEntity, NetworkEntityInfo>(entity => entity);
        }
        catch (Exception e)
        {
            TavernLogger.Error($"Error when using select find! {e}");
            throw;
        }
    }
}