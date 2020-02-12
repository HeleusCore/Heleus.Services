using System;
using Heleus.Chain;

namespace Heleus.VerifyService
{
    public static class VerifyServiceInfo
    {
        public const long Version = 1;
        public const string Name = "Verify Service";

        public const int ChainId = 3;
        public const uint ChainIndex = 0;

        public static readonly Uri EndPoint = new Uri("https://servicesnode.heleuscore.com");

        public const string JsonFileName = "verify.json";
        public const int JsonMaxFileSize = 1024 * 10;

        public static Index VerifyIndex = Index.New().Add((short)10).Build();
    }
}
