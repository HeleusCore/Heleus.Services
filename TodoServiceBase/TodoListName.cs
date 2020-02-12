using Heleus.Base;
using Heleus.Network.Client.Record;

namespace Heleus.TodoService
{
    public class TodoListNameRecord : Record
    {
        public readonly string Name;

        public TodoListNameRecord(string name) : base((ushort)TodoRecordTypes.ListName)
        {
            Name = name;
        }

        public TodoListNameRecord(Unpacker unpacker) : base((ushort)TodoRecordTypes.ListName)
        {
            unpacker.Unpack(out Name);
        }

        public override void Pack(Packer packer)
        {
            packer.Pack(Name);
        }
    }
}
