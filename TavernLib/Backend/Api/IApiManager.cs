using TavernLib.Backend.Server;
using TavernLib.Services;

namespace TavernLib.Backend.Api
{
    public interface IApiManager : IService
    {
        ServerListing ActiveListing { get; }
    }
}