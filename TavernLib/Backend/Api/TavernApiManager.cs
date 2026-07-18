using System;
using System.IO;
using MelonLoader.Logging;
using TavernLib.Backend.Auth;
using TavernLib.Backend.Server;
using TavernLib.Services;
using YamlDotNet.Serialization;

namespace TavernLib.Backend.Api
{
    public class TavernApiManager : IService
    {
        internal AuthManager AuthManager { get; private set; }
        internal ServerListingController ListingController { get; private set; }
        
        
        public TavernApiManager()
        {
            ListingController = new ServerListingController();
            AuthManager = new AuthManager(this);
        }
    }
}