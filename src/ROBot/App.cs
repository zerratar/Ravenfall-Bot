using RavenfallOfficial.Core;
namespace ROBot
{
    public class App : IApplication
    {
        private readonly ILogger logger;
        private readonly ITwitchCommandClient twitch;
        private bool disposed;

        public App(
            ILogger logger,
            ITwitchCommandClient twitch
            // IYouTubeCommandClient youtube
            )
        {
            this.logger = logger;
            this.twitch = twitch;
        }

        public void Run()
        {
            logger.WriteMessage("Application Started");
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
        }
    }
}
