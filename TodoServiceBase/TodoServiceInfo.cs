using System;
using Heleus.Chain;

namespace Heleus.TodoService
{
    public static class TodoServiceInfo
    {
        public const long Version = 1;
        public const string Name = "Todo Service";

        public const int ChainId = 5;
        public static readonly Uri EndPoint = new Uri("https://servicenode.heleuscore.com");

        public static Index TodoSubmitIndex = Index.New().Add("Submit").Build();

        public static Index TodoListIndex = Index.New().Add((short)1000).Build();
        public static Index TodoListNameIndex = Index.New().Add((short)2000).Build();
        public static Index TodoTaskIndex = Index.New().Add((short)3000).Build();
        public static Index TodoTaskStatusIndex = Index.New().Add((short)3001).Build();

        public const byte DataVersion = 1;

        public const short TodoDataItemIndex = 0;

        public const uint GroupChainIndex = 0;
        public const uint TodoDataChainIndex = 1;
    }
}
