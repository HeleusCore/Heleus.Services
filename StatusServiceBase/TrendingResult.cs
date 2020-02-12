using System.Collections.Generic;
using Heleus.Base;

namespace Heleus.StatusService
{
    public class TrendingResult : IPackable
    {
        byte[] _data;

        public IReadOnlyList<long> NewAccounts;
        public IReadOnlyList<long> PopularAccounts;
        public IReadOnlyList<long> RecentAccounts;

        public TrendingResult(List<long> newAccounts, List<long> popularAccounts, List<long> recentAccounts)
        {
            NewAccounts = newAccounts;
            PopularAccounts = popularAccounts;
            RecentAccounts = recentAccounts;

            // caching
            using(var packer = new Packer())
            {
                Pack(packer);
                _data = packer.ToByteArray();
            }
        }

        public TrendingResult(Unpacker unpacker)
        {
            NewAccounts = unpacker.UnpackListLong();
            PopularAccounts = unpacker.UnpackListLong();
            RecentAccounts = unpacker.UnpackListLong();
        }

        public void Pack(Packer packer)
        {
            if(_data != null)
            {
                packer.Pack(_data, _data.Length);
                return;
            }

            packer.Pack(NewAccounts);
            packer.Pack(PopularAccounts);
            packer.Pack(RecentAccounts);
        }
    }
}
