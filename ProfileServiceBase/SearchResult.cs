using System.Collections.Generic;
using Heleus.Base;

namespace Heleus.ProfileService
{
    public class SearchResult : IPackable
    {
        public readonly List<string> Profiles;

        public int Count => Profiles.Count;

        public SearchResult(List<string> profiles)
        {
            Profiles = profiles;
        }

        public SearchResult(Unpacker unpacker)
        {
            Profiles = unpacker.UnpackListString();
        }

        public void Pack(Packer packer)
        {
            packer.Pack(Profiles);
        }
    }
}
