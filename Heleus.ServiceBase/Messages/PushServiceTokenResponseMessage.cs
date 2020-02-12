using Heleus.Base;
using Heleus.Service.Push;

namespace Heleus.Messages
{
    public class PushServiceTokenResponseMessage : PushServiceMessage
    {
        public PushTokenResult Result { get; private set; }

        public PushServiceTokenResponseMessage() : base(PushServiceMessageTypes.PushTokenResponse)
        {
        }

        public PushServiceTokenResponseMessage(PushTokenResult result, long requestCode, int chainId, int clientId) : base(PushServiceMessageTypes.PushTokenResponse, chainId, clientId)
        {
            SetRequestCode(requestCode);
            Result = result;
        }

        protected override void Pack(Packer packer)
        {
            base.Pack(packer);
            packer.Pack((ushort)Result);
        }

        protected override void Unpack(Unpacker unpacker)
        {
            base.Unpack(unpacker);
            Result = (PushTokenResult)unpacker.UnpackUshort();
        }
    }
}
