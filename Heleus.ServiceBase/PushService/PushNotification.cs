using System.Collections.Generic;
using Heleus.Base;

namespace Heleus.PushService
{
    public enum PushNotificationType
    {
        Account,
        Accounts,
        Channel,
    }

    public class PushNotification : IPackable
    {
        public const int IgnoreId = 0;

        public readonly PushNotificationType NotificationType;

        public readonly long AccountId;
        public readonly Chain.Index Channel;
        public readonly IReadOnlyList<long> Accounts;
        public readonly int NotificationId;

        public bool HasNotificationId => NotificationId != IgnoreId;


        public string NotificationTitle;
        public string NotificationMessage;
        public string NotificationImageUri;
        public string NotificationScheme;

        public PushNotification(Chain.Index channel, int notificationId)
        {
            NotificationType = PushNotificationType.Channel;
            Channel = channel;
            NotificationId = notificationId;
        }

        public PushNotification(long accountId, Chain.Index channel, int notificationId)
        {
            NotificationType = PushNotificationType.Account;
            AccountId = accountId;
            Channel = channel;
            NotificationId = notificationId;
        }

        public PushNotification(List<long> accounts, Chain.Index channel, int notificationId)
        {
            NotificationType = PushNotificationType.Accounts;
            Accounts = accounts;
            Channel = channel;
            NotificationId = notificationId;
        }

        public PushNotification(Unpacker unpacker)
        {
            NotificationType = (PushNotificationType)unpacker.UnpackUshort();
            unpacker.Unpack(out AccountId);
            Channel = new Chain.Index(unpacker);
            var accounts = unpacker.UnpackListLong();
            Accounts = accounts;

            NotificationId = unpacker.UnpackInt();

            NotificationTitle = unpacker.UnpackString();
            NotificationMessage = unpacker.UnpackString();
            NotificationImageUri = unpacker.UnpackString();
            NotificationScheme = unpacker.UnpackString();
        }

        public void Pack(Packer packer)
        {
            packer.Pack((ushort)NotificationType);
            packer.Pack(AccountId);
            packer.Pack(Channel);
            packer.Pack(Accounts);

            packer.Pack(NotificationId);

            packer.Pack(NotificationTitle);
            packer.Pack(NotificationMessage);
            packer.Pack(NotificationImageUri);

            packer.Pack(NotificationScheme);
        }
    }
}
