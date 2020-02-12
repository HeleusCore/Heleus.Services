using Heleus.Base;
using Heleus.Network.Client.Record;

namespace Heleus.MessageService
{
    public class InboxNameRecord : Record
    {
        public readonly bool Active;
        public readonly string Title;

        public InboxNameRecord(bool active, string title) : base((ushort)MessageRecordTypes.InboxName)
        {
            Title = title;
            Active = active;
        }

        public InboxNameRecord(Unpacker unpacker) : this(unpacker.UnpackBool(), unpacker.UnpackString())
        {

        }

        public override void Pack(Packer packer)
        {
            packer.Pack(Active);
            packer.Pack(Title);
        }
    }
}
