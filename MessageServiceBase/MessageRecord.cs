using Heleus.Base;
using Heleus.Network.Client.Record;

namespace Heleus.MessageService
{
    public class MessageRecord : Record
    {
        public readonly string Text;

        public MessageRecord(string text) : base((ushort)MessageRecordTypes.Message)
        {
            Text = text;
        }

        public MessageRecord(Unpacker unpacker) : this(unpacker.UnpackString())
        {
        }

        public override void Pack(Packer packer)
        {
            packer.Pack(Text);
        }
    }
}
