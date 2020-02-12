using Heleus.Base;
using Heleus.PushService;

namespace Heleus.Messages
{
    public class PushServiceNotificationMessage : PushServiceMessage
    {
        public PushNotification Notification { get; private set; }

        public PushServiceNotificationMessage() : base(PushServiceMessageTypes.Notifiaction)
        {
        }

        public PushServiceNotificationMessage(PushNotification notification, int chainId, int clientId) : base( PushServiceMessageTypes.Notifiaction, chainId, clientId)
        {
            Notification = notification;
        }

        protected override void Pack(Packer packer)
        {
            base.Pack(packer);

            packer.Pack(Notification);
        }

        protected override void Unpack(Unpacker unpacker)
        {
            base.Unpack(unpacker);

            Notification = new PushNotification(unpacker);
        }
    }
}
