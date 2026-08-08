using System;
using Alta.Networking;
using Alta.Networking.Internal;
using Alta.Serialization;
using HarmonyLib;

namespace TavernLib.Patches;

[HarmonyPatch]
public class MessageExpanderPatch
{
    [HarmonyPatch(typeof(MessageProcessor), nameof(MessageProcessor.ProcessSingleMessageFromData)), HarmonyPrefix]
    public static bool LogMessageHandler(Connection connection, ArraySegment<byte> data, ConnectionChannel channel, out MessageType messageType, ref int __result)
    {
        Buffer.BlockCopy(data.Array, data.Offset, MessageProcessor.size, 0, 2);
        ushort num = MessageProcessor.size[0];
        int num2 = data.Offset + 2;
        Buffer.BlockCopy(data.Array, num2, MessageProcessor.receiveBuffer, 0, (int)num);
        MessageProcessor.messageReader.Initialize(MessageProcessor.receiveBuffer, (int)num);
        
        uint messageTypeSerialized = 0U;
        MessageProcessor.messageReader.SerializeBits(ref messageTypeSerialized, 8); // Expand bitCount to 8 for 256 possible messages
        messageType = (MessageType)messageTypeSerialized;
        
        MessageProcessor.HandleMessage(connection, MessageProcessor.messageReader, messageType, channel);
        __result = (num + 2);

        return false;
    }
    
    [HarmonyPatch(typeof(OutgoingPacketManager), nameof(OutgoingPacketManager.StartSerialize)), HarmonyPrefix]
    public static bool StartSerializeCustom(Connection connection, MessageType type, ref StreamWriter __result)
    {
        var num = connection?.Identifier ?? 0;
        
        if (!OutgoingPacketManager.writerMap.TryGetValue(num, out var streamWriter))
        {
            streamWriter = new StreamWriter(new uint[(OutgoingPacketManager.maximumPacketSize + 3) / 4]);
            OutgoingPacketManager.writerMap[num] = streamWriter;
        }
        
        var messageType = (uint)type;
        streamWriter.Clear();
        streamWriter.SerializeBits(ref messageType, 8); // Expand bitCount to 8 for 256 possible messages
        streamWriter.MessageType = messageType;
        __result = streamWriter;

        return false;
    }
}