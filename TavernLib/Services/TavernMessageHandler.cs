using TavernLib.Backend;

namespace TavernLib.Services;

public class TavernMessageHandler
{
    public EntranceMessageHandler ServerEntranceHandler { get; private set; } = new();
}