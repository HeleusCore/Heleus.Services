using System;
using Heleus.Chain;

namespace Heleus.StatusService
{
    public static class StatusServiceInfo
    {
        public const long Version = 1;
        public const string Name = "Status Service";

        public const int ChainId = 6;
        public static readonly Uri EndPoint = new Uri("https://servicenode.heleuscore.com");

        public const int MaxTrendingItems = 50;

        public const string StatusJsonFileName = "status.json";

        public const int ImageMaxFileSize = 1024 * 192;
        public const int ImageDimension = 1024;
        public const string ImageFileName = "image.png";

        public static Index StatusIndex = Index.New().Add((short)0).Build();
        public static Index MessageIndex = Index.New().Add((short)1000).Build();

        public const uint FanChainIndex = 0;
        public const uint StatusDataChainIndex = 1;
    }
}
