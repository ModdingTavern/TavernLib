using System.Threading.Tasks;

namespace TavernLib.Services.Server
{
    public interface IApiServer
    {
        public Task Ping();
        public Task PublishDataToApi();
        public void CloseListing();
    }
}