using BepInEx.Logging;
using TarkinItemExporter;
using UnityEngine;

namespace AssetStudio
{
    public class BepinexLoggerAdapter : ILogger
    {
        readonly ManualLogSource logger;

        public BepinexLoggerAdapter(ManualLogSource logger)
        {
            this.logger = logger;
        }

        public void Log(LoggerEvent loggerEvent, string message, bool ignoreLevel = false)
        {
            switch (loggerEvent)
            {
                case LoggerEvent.Verbose:
                case LoggerEvent.Debug:
                case LoggerEvent.Info:
                    logger.LogInfo(message);
                    break;
                case LoggerEvent.Warning:
                    logger.LogWarning(message);
                    break;
                case LoggerEvent.Error:
                    logger.LogError(message);
                    break;
            }
        }
    }
}