using ROBot.Core;
using ROBot.Ravenfall.GameServer;

namespace ROBot
{
    public class App : IApplication
    {
        private readonly ILogger logger;
        private readonly IRavenfallServerConnection ravenfall;
        private readonly ITwitchCommandClient twitch;
        private bool disposed;

        public App(
            ILogger logger,
            IRavenfallServerConnection ravenfall,
            ITwitchCommandClient twitch
            // IYouTubeCommandClient youtube
            )
        {
            this.logger = logger;
            this.ravenfall = ravenfall;
            this.twitch = twitch;
        }

        public void Run()
        {
            logger.WriteMessage("Application Started");

            logger.WriteMessage("Establishing Ravenfall Communication..");
            ravenfall.Start();

            logger.WriteMessage("Initializing Twitch Integration..");
            twitch.Start();
        }

        public void Shutdown()
        {
            logger.WriteMessage("Application Shutdown initialized.");
            Dispose();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            twitch.Dispose();
            ravenfall.Dispose();
        }
    }
}
