using System.Collections.Generic;
using Heleus.Base;
using Heleus.Service.Push;

namespace Heleus.Messages
{
    public class PushServiceSubscriptionResponseMessage : PushServiceMessage
    {
        public PushServiceSubscriptionMessageSender Sender { get; private set; }
        public PushSubscriptionResponse Response { get; private set; }

        public PushServiceSubscriptionResponseMessage() : base(PushServiceMessageTypes.SubscriptionResponse)
        {
        }

        public PushServiceSubscriptionResponseMessage(PushServiceSubscriptionMessageSender sender, PushSubscriptionResponse response, long requestCode, int chainId) : base(PushServiceMessageTypes.SubscriptionResponse, chainId, 0)
        {
            SetRequestCode(requestCode);
            Response = response;
            Sender = sender;
        }

        protected override void Pack(Packer packer)
        {
            base.Pack(packer);
            packer.Pack((byte)Sender);
            packer.Pack(Response);
        }

        protected override void Unpack(Unpacker unpacker)
        {
            base.Unpack(unpacker);
            Sender = (PushServiceSubscriptionMessageSender)unpacker.UnpackByte();
            Response = new PushSubscriptionResponse(unpacker);
        }
    }
}
