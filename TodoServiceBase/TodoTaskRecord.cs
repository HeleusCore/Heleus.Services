using Heleus.Base;
using Heleus.Network.Client.Record;

namespace Heleus.TodoService
{
    public class TodoTaskRecord : Record
    {
        public readonly string Text;

        public TodoTaskRecord(string name) : base((ushort)TodoRecordTypes.Task)
        {
            Text = name;
        }

        public TodoTaskRecord(Unpacker unpacker) : base((ushort)TodoRecordTypes.Task)
        {
            unpacker.Unpack(out Text);
        }

        public override void Pack(Packer packer)
        {
            packer.Pack(Text);
        }
    }
}
