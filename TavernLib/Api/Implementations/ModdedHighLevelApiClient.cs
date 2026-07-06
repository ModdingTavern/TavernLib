using System.Threading.Tasks;
using Alta.Api.Client;
using Alta.Api.Client.HighLevel;
using Alta.Api.Client.LowLevel;
using Alta.Api.DataTransferModels.Models.Requests;
using Alta.Api.DataTransferModels.Models.Responses;

namespace TavernLib.Api.Implementations
{
    public class ModdedHighLevelApiClient : IHighLevelApiClient
    {
        public IServerApiClient ServerClient { get; }
        public ISocialClient SocialClient { get; }
        public IUserApiClient UserClient { get; }
        public IServicesApiClient ServicesClient { get; }
        public ILauncherApiClient LauncherClient { get; }
        public IShopApiClient ShopClient { get; }
        public IVoiceApiClient VoiceClient { get; }
        public IAchievementClient AchievementClient { get; }
        public ISecurityApiClient SecurityClient { get; }
        public ISettingsClient SettingsClient { get; }
        public IAccountClient Account { get; }
        public IGroupsClient Groups { get; }
        public IMatchMakingClient MatchMaking { get; }
        public IInstancesClient Instances { get; }
        public IAnalyticsClient Analytics { get; }
        public IOculusClient Oculus { get; }
        public IPicoClient Pico { get; }
        public IFutureUserActions FutureUserActions { get; }
        public IPlayerReportsClient PlayerReports { get; }
        public IRecentPlayersClient RecentPlayers { get; }
        public LoginCredentials UserCredentials { get; }
        public LowLevelApiClient LowLevel { get; }
        public bool IsLoggedIn { get; }
        public event LoginCredentialsChangedHandler CredentialsChanged;
        
        
        public Task<IUserApiClient> LoginViaOauth(string clientId, string clientSecret, string scope)
        {
            throw new System.NotImplementedException();
        }

        public Task<IUserApiClient> LoginAsync(string username, string passwordHash)
        {
            throw new System.NotImplementedException();
        }

        public Task<IUserApiClient> LoginWithEmailAsync(string email, string passwordHash)
        {
            throw new System.NotImplementedException();
        }

        public IUserApiClient LoginWithTokens(string accessToken, string refreshToken, string identityToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<IUserApiClient> LoginWithOculus(string userNonce, ulong userIdentifier)
        {
            throw new System.NotImplementedException();
        }

        public Task<IUserApiClient> LoginWithPico(string userToken, ulong userIdentifier)
        {
            throw new System.NotImplementedException();
        }

        public Task<IUserApiClient> LoginWithRefreshTokenAsync(string refreshToken)
        {
            throw new System.NotImplementedException();
        }

        public void Logout()
        {
            throw new System.NotImplementedException();
        }

        public Task<UserInfo> RegisterUserAsync(string username, string passwordHash, string email, string referral)
        {
            throw new System.NotImplementedException();
        }

        public Task<UserInfo> RegisterOculusUserAsync(string username, string passwordHash, string email, string referral, AccountLinkData linkData)
        {
            throw new System.NotImplementedException();
        }

        public Task<UserInfo> RegisterPicoUserAsync(string username, string passwordHash, string email, string referral, AccountLinkData linkData)
        {
            throw new System.NotImplementedException();
        }
    }
}