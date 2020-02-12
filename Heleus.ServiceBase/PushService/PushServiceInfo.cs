using System;
namespace Heleus.PushService
{
    public static class PushServiceInfo
    {
        public const string DefaultPushServerBindAddress = "tcp://localhost:49853";
        public const string DefaultPushServerBindAddressConfigProperty = "push_serverbindaddress";

        public const string DefaultPushClientBindAddress = "tcp://localhost:48944";
        public const string DefaultPushClientBindAddressConfigProperty = "push_clientbindaddress";

        public const int DefaultPushClientId = 1;
        public const string DefaultPushClientIdConfigProperty = "push_clientid";
    }
}
