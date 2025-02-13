using UnityEngine;

namespace AssetStudio
{
    public class BepinexLogger : ILogger
    {
        public void Log(LoggerEvent loggerEvent, string message, bool ignoreLevel = false)
        {
            switch (loggerEvent)
            {
                case LoggerEvent.Verbose:
                case LoggerEvent.Debug:
                case LoggerEvent.Info:
                    Plugin.Log.LogInfo(message);
                    break;
                case LoggerEvent.Warning:
                    Plugin.Log.LogWarning(message);
                    break;
                case LoggerEvent.Error:
                    Plugin.Log.LogError(message);
                    break;
            }
        }
    }
}