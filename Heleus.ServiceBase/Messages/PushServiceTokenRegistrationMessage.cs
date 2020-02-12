using Heleus.Base;
using Heleus.Service.Push;

namespace Heleus.Messages
{
    public class PushServiceTokenRegistrationMessage : PushServiceMessage
    {
        public PushTokenInfo TokenInfo { get; private set; }

        public PushServiceTokenRegistrationMessage() : base(PushServiceMessageTypes.PushTokenRegistration)
        {
        }

        public PushServiceTokenRegistrationMessage(PushTokenInfo pushToken, long requestCode, int chainId, int clientId) : base(PushServiceMessageTypes.PushTokenRegistration, chainId, clientId)
        {
            TokenInfo = pushToken;
            SetRequestCode(requestCode);
        }

        protected override void Pack(Packer packer)
        {
            base.Pack(packer);
            packer.Pack(TokenInfo);
        }

        protected override void Unpack(Unpacker unpacker)
        {
            base.Unpack(unpacker);
            TokenInfo = new PushTokenInfo(unpacker);
        }
    }
}
