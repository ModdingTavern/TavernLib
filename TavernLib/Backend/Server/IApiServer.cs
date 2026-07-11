using System.Threading.Tasks;

namespace TavernLib.Backend.Server
{
    public interface IApiServer
    {
        public Task Ping();
        public Task OpenListing();
        public void CloseListing();
    }
}