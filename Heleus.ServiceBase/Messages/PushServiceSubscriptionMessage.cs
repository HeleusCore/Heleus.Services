using Heleus.Base;
using Heleus.Service.Push;

namespace Heleus.Messages
{
    public enum PushServiceSubscriptionMessageSender
    {
        ServiceClient,
        ServiceUri
    }

    public class PushServiceSubscriptionMessage : PushServiceMessage
    {
        public PushServiceSubscriptionMessageSender Sender { get; private set; }
        public PushSubscription PushSubscription { get; private set; }

        public PushServiceSubscriptionMessage() : base(PushServiceMessageTypes.Subscription)
        {
        }

        public PushServiceSubscriptionMessage(PushServiceSubscriptionMessageSender sender, PushSubscription pushSubscription, long requestCode, int chainId, int clientId) : base(PushServiceMessageTypes.Subscription, chainId, clientId)
        {
            Sender = sender;
            SetRequestCode(requestCode);
            PushSubscription = pushSubscription;
        }

        protected override void Pack(Packer packer)
        {
            base.Pack(packer);
            packer.Pack((byte)Sender);
            packer.Pack(PushSubscription);
        }

        protected override void Unpack(Unpacker unpacker)
        {
            base.Unpack(unpacker);
            Sender = (PushServiceSubscriptionMessageSender)unpacker.UnpackByte();
            PushSubscription = new PushSubscription(unpacker);
        }
    }
}
