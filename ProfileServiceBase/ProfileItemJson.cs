using System.Collections.Generic;

namespace Heleus.ProfileService
{
    public class ProfileItemJson
    {
        public bool IsProfileName() => p == ProfileNameItem;
        public bool IsRealName() => p == RealNameItem;
        public bool IsBio() => p == BioItem;

        public bool IsMail() => p == MailItem;
        public bool IsWebSite() => p == WebSiteItem;

        public const string ProfileNameItem = "pname";
        public const string RealNameItem = "rname";
        public const string BioItem = "bio";

        public const string MailItem = "mail";
        public const string WebSiteItem = "site";

        public string k;
        public string v;
        public string p;

        public ProfileItemJson()
        {

        }

        public ProfileItemJson(ProfileItemJson profileItem)
        {
            k = profileItem.k;
            v = profileItem.v;
            p = profileItem.p;
        }

        public bool Equals(ProfileItemJson profileItem)
        {
            if (profileItem == null)
                return false;

            return k == profileItem.k && v == profileItem.v && p == profileItem.p;
        }

        public static bool ListsEqual(List<ProfileItemJson> a, List<ProfileItemJson> b)
        {
            if (a == null && b == null)
                return true;
            if (a == null && b != null)
                return false;
            if (a != null && b == null)
                return false;

            if (a.Count != b.Count)
                return false;

            for (var i = 0; i < a.Count; i++)
            {
                if (!a[i].Equals(b[i]))
                    return false;
            }

            return true;
        }

        public static ProfileItemJson GetItem(List<ProfileItemJson> profileItems, string itemName)
        {
            if (profileItems == null)
                return null;

            foreach (var item in profileItems)
            {
                if (item.p == itemName)
                    return item;
            }

            return null;
        }

        public static string GetItemValue(List<ProfileItemJson> profileItems, string itemName)
        {
            if (profileItems == null)
                return string.Empty;

            foreach (var item in profileItems)
            {
                if (item.p == itemName)
                    return item.v;
            }

            return string.Empty;
        }
    }
}
