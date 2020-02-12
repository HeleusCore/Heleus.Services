using Heleus.Base;
using Heleus.Network.Client.Record;

namespace Heleus.TodoService
{
    public enum TodoTaskStatusTypes
    {
        Open,
        Closed,
        Deleted
    }

    public class TodoTaskStatusRecord : Record
    {
        public readonly TodoTaskStatusTypes Status;

        public TodoTaskStatusRecord(TodoTaskStatusTypes status) : base((ushort)TodoRecordTypes.TaskStatus)
        {
            Status = status;
        }

        public TodoTaskStatusRecord(Unpacker unpacker) : base((ushort)TodoRecordTypes.TaskStatus)
        {
            Status = (TodoTaskStatusTypes)unpacker.UnpackByte();
        }

        public override void Pack(Packer packer)
        {
            packer.Pack((byte)Status);
        }
    }
}
