using System;
using Heleus.Base;

namespace Heleus.Messages
{
    public enum PushServiceMessageTypes
    {
        PushTokenRegistration = 10000,
        PushTokenRemove,
        PushTokenResponse,

        Subscription,
        SubscriptionResponse,

        Notifiaction,

        Ping,
        Pong,
        Last
    }

    public abstract class PushServiceMessage : Messages.Message
    {
        public new PushServiceMessageTypes MessageType => (PushServiceMessageTypes)base.MessageType;

        public int ClientId { get; private set; }
        public long RequestCode { get; private set; }

        public void SetRequestCode()
        {
            do
            {
                RequestCode = Rand.NextLong();
            } while (RequestCode == 0);
        }

        public void SetRequestCode(long requestCode)
        {
            RequestCode = requestCode;
        }

        public int ChainId { get; private set; }

        public static void RegisterPushServiceMessages()
        {
            try
            {
                RegisterMessage<PushServiceTokenRegistrationMessage>();
                RegisterMessage<PushServiceTokenRemoveMessage>();
                RegisterMessage<PushServiceTokenResponseMessage>();
                RegisterMessage<PushServiceSubscriptionMessage>();
                RegisterMessage<PushServiceSubscriptionResponseMessage>();
                RegisterMessage<PushServiceNotificationMessage>();
                RegisterMessage<PushServicePingMessage>();
                RegisterMessage<PushServicePongMessage>();
            }
            catch (Exception) { }
        }

        protected PushServiceMessage(PushServiceMessageTypes messageType) : base((ushort)messageType)
        {
        }

        protected PushServiceMessage(PushServiceMessageTypes messageType, int chainId, int clientId) : base((ushort)messageType)
        {
            ChainId = chainId;
            ClientId = clientId;
        }

        protected override void Pack(Packer packer)
        {
            base.Pack(packer);
            packer.Pack(RequestCode);
            packer.Pack(ChainId);
            packer.Pack(ClientId);
        }

        protected override void Unpack(Unpacker unpacker)
        {
            base.Unpack(unpacker);
            RequestCode = unpacker.UnpackLong();
            ChainId = unpacker.UnpackInt();
            ClientId = unpacker.UnpackInt();
        }
    }
}
