using System;
using System.Threading.Tasks;

namespace RavenfallOfficial.Core
{
    public interface ITwitchCommandClient : IDisposable
    {
        void Start();
        void Stop();
        void SendChatMessage(string channel, string message);
    }
}