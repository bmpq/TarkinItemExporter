using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetStudio
{
    internal class ProgressLogger : IProgress<int>
    {
        public static event Action<int> OnProgress;

        public void Report(int value)
        {
            OnProgress?.Invoke(value);
            Plugin.Log.LogMessage(value);
        }
    }
}
