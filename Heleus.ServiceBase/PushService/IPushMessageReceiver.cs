using System.Threading.Tasks;
using Heleus.Messages;

namespace Heleus.PushService
{
    public interface IPushMessageReceiver
    {
        int PushServiceChainId { get; }
        void HandlePushServiceMessage(PushServiceMessage message);
    }
}
