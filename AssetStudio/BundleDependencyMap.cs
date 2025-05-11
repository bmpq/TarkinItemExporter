using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TarkinItemExporter
{
    public class BundleDependencyMap
    {
        private readonly Dictionary<string, List<string>> _dependencyMap;
        private readonly bool _isInitialized;

        public BundleDependencyMap(string jsonFilePath)
        {
            _dependencyMap = new Dictionary<string, List<string>>();
            _isInitialized = false;

            try
            {
                if (!File.Exists(jsonFilePath))
                {
                    Plugin.Log.LogError($"Error: JSON file not found at '{jsonFilePath}'");
                    return;
                }

                string jsonContent = File.ReadAllText(jsonFilePath);
                JObject rootObject = JObject.Parse(jsonContent);

                foreach (var property in rootObject.Properties())
                {
                    string entryName = property.Name;
                    JToken entryValue = property.Value;

                    if (entryValue is JObject entryDetails)
                    {
                        JToken dependenciesToken = entryDetails["Dependencies"];
                        if (dependenciesToken is JArray dependenciesArray)
                        {
                            _dependencyMap[entryName] = dependenciesArray.ToObject<List<string>>() ?? new List<string>();
                        }
                        else
                        {
                            _dependencyMap[entryName] = new List<string>();
                        }
                    }
                    else
                    {
                        _dependencyMap[entryName] = new List<string>();
                        Console.WriteLine($"Warning: Entry '{entryName}' does not have the expected object structure. Dependencies set to empty.");
                    }
                }
                _isInitialized = true;
            }
            catch (JsonException ex)
            {
                Plugin.Log.LogError($"Error parsing JSON file '{jsonFilePath}': {ex.Message}. Cache not initialized.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"An unexpected error occurred while initializing cache from '{jsonFilePath}': {ex.Message}. Cache not initialized.");
            }
        }

        public List<string> GetDependencies(string entryName)
        {
            if (!_isInitialized)
            {
                Plugin.Log.LogError("Cache was not initialized successfully. Returning empty dependencies.");
                return new List<string>();
            }

            entryName = entryName.Replace('\\', '/');

            if (_dependencyMap.TryGetValue(entryName, out List<string> dependencies))
            {
                return dependencies;
            }

            Plugin.Log.LogWarning($"'{entryName}' has no dependencies, hopefully that's correct");
            return new List<string>();
        }

        public bool IsInitialized => _isInitialized;
    }
}