namespace TavernLib.Services.Api
{
    public class AuthManager : IAuthManager
    {
        private IApiManager _manager;
        
        public AuthManager(IApiManager manager)
        {
            _manager = manager;
        }
    }
}