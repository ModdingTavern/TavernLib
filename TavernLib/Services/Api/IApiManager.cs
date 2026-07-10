using TavernLib.Services.Server;

namespace TavernLib.Services.Api
{
    public interface IApiManager : IService
    {
        ServerListing ActiveListing { get; }
    }
}