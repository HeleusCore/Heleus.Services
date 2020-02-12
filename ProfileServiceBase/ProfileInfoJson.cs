namespace Heleus.ProfileService
{
    public class ProfileInfoJson
    {
        public long a;
        public string p;
        public string r;
        public long pi;
        public int pk;
        public long ii;
        public int ik;

        public ProfileInfoJson()
        {

        }

        public ProfileInfoJson(ProfileInfo info)
        {
            a = info.AccountId;
            p = info.ProfileName;
            r = info.RealName;
            pi = info.ProfileTransactionId;
            pk = info.ProfileAttachementKey;
            ii = info.ImageTransactionId;
            ik = info.ImageAttachementKey;
        }

        public ProfileInfoJson(long accountId, string profileName, string realName, long profileTransactionId, int profileAttachementKey, long imageTransactionId, int imageAttachementKey)
        {
            a = accountId;
            p = profileName;
            r = realName;
            pi = profileTransactionId;
            pk = profileAttachementKey;
            ii = imageTransactionId;
            ik = imageAttachementKey;
        }

        public ProfileInfo GetProfileInfo()
        {
            return new ProfileInfo(a, p, r, pi, pk, ii, ik);
        }
    }
}
