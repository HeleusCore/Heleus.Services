using System;
using Heleus.Chain;

namespace Heleus.MessageService
{
    public enum MessageRecordTypes
    {
        InboxName = 1,
        Message,
        Invalid
    }

    public static class MessageServiceInfo
    {
        public const long Version = 1;
        public const string Name = "Message Service";

        public const int ChainId = 7;
        public static readonly Uri EndPoint = new Uri("https://servicenode.heleuscore.com");

        public const int MaxInboxNameLength = 32;

        public static Index GetInboxIndex(short keyIndex)
        {
            return Index.New().Add((byte)MessageRecordTypes.InboxName).Add(keyIndex).Build();
        }

        public static readonly Index SubmitAccountIndex = Index.New().Add(1000).Build();

        public const uint FriendChainIndex = 0;
        public const uint MessageDataChainIndex = 1;

        public const short MessageDataIndex = 0;

        public static Index GetConversationIndex(long accountId1, short keyIndex1, long accountId2, short keyIndex2)
        {
            if (accountId1 > accountId2)
                return Index.New().Add((byte)MessageRecordTypes.Message).Add(accountId1).Add(keyIndex1).Add(accountId2).Add(keyIndex2).Build();

            return Index.New().Add((byte)MessageRecordTypes.Message).Add(accountId2).Add(keyIndex2).Add(accountId1).Add(keyIndex1).Build();
        }

        public static (long, short, long, short) GetAccountsAndKeyIndices(Chain.Index index)
        {
            try
            {
                var a1 = index.GetLong(1);
                var k1 = index.GetShort(2);
                var a2 = index.GetLong(3);
                var k2 = index.GetShort(4);

                return (a1, k1, a2, k2);
            }
            catch { }

            return (0, 0, 0, 0);
        }

        public static bool IsValidConversationIndex(Index index)
        {
            if (index == null)
                return false;

            try
            {
                if (index.SubIndexCount == 4)
                {
                    var type = (MessageRecordTypes)index.GetShort(0);
                    if (type == MessageRecordTypes.Message)
                    {
                        index.GetLong(1);
                        index.GetShort(2);
                        index.GetLong(3);
                        index.GetShort(4);

                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public static MessageRecordTypes GetIndexType(Index index)
        {
            if (index == null)
                return MessageRecordTypes.Invalid;

            var data = index.Get(0);
            if (data.Count != 2)
                return MessageRecordTypes.Invalid;

            var idx = Index.GetShort(data);
            if (idx <= 0 || idx >= (short)MessageRecordTypes.Invalid)
                return MessageRecordTypes.Invalid;

            return (MessageRecordTypes)idx;
        }
    }
}
