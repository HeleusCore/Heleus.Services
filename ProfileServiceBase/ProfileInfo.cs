using Heleus.Base;

namespace Heleus.ProfileService
{
    public class ProfileInfo : IPackable
    {
        public readonly long AccountId;
        public readonly string ProfileName;
        public readonly string RealName;
        public readonly long ProfileTransactionId;
        public readonly int ProfileAttachementKey;
        public readonly long ImageTransactionId;
        public readonly int ImageAttachementKey;

        public ProfileInfo(long accountId, string profileName, string realName, long profileTransactionId, int profileAttachementKey, long imageTransactionId, int imageAttachementKey)
        {
            AccountId = accountId;
            ProfileName = profileName;
            RealName = realName;
            ProfileTransactionId = profileTransactionId;
            ProfileAttachementKey = profileAttachementKey;
            ImageTransactionId = imageTransactionId;
            ImageAttachementKey = imageAttachementKey;
        }

        public ProfileInfo(Unpacker unpacker)
        {
            unpacker.Unpack(out AccountId);
            unpacker.Unpack(out ProfileName);
            unpacker.Unpack(out RealName);
            unpacker.Unpack(out ProfileTransactionId);
            unpacker.Unpack(out ProfileAttachementKey);
            unpacker.Unpack(out ImageTransactionId);
            unpacker.Unpack(out ImageAttachementKey);
        }

        public void Pack(Packer packer)
        {
            packer.Pack(AccountId);
            packer.Pack(ProfileName);
            packer.Pack(RealName);
            packer.Pack(ProfileTransactionId);
            packer.Pack(ProfileAttachementKey);
            packer.Pack(ImageTransactionId);
            packer.Pack(ImageAttachementKey);
        }

        public ProfileInfoJson ToProfileInfoJson()
        {
            return new ProfileInfoJson(this);
        }
    }
}
