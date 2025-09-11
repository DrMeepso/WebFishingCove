using Cove.Server.Plugins;
using Cove.Server.Utils;
using System.Reflection;

namespace Cove.Server
{
    public partial class CoveServer
    {
        private bool arePluginsEnabled = false;

        public readonly List<PluginInstance> loadedPlugins = new List<PluginInstance>();
        public void loadAllPlugins(bool skipWarning = false)
        {
            if (!arePluginsEnabled) return; // plugins are disabled!

            if (!skipWarning)
            {
                Log("");
                Log("------------ WARNING ------------");
                Log("YOU HAVE ENABLED PLUGINS, PLUGINS RUN CODE THAT IS NOT APPROVED OR MADE BY COVE");
                Log("ANY AND ALL DAMMAGE TO YOUR COMPUTER IS YOU AND YOUR FAULT ALONE");
                Log("DO NOT RUN ANY UNTRUSTED PLUGINS!");
                Log("IF YOU ARE RUNNING UNTRUSTED PLUGINS EXIT COVE NOW");
                Log("------------ WARNING ------------");
                Log("");

                Thread.Sleep(5000);
            }

            Log("Loading Plugins...");

            string pluginsFolder = $"{AppDomain.CurrentDomain.BaseDirectory}plugins";

            List<Assembly> pluginAssemblys = new();

            // get all files in the plugins folder
            foreach (string fileName in Directory.GetFiles(pluginsFolder))
            {
                try
                {
                    AssemblyName thisFile = AssemblyName.GetAssemblyName(fileName); ;
                    pluginAssemblys.Add(Assembly.LoadFrom(fileName));
                }
                catch (BadImageFormatException)
                {
                    Log($"File: {fileName} is not a plugin!");
                }
            }

            Log($"Found {pluginAssemblys.Count} plugins!");

            foreach (Assembly assembly in pluginAssemblys)
            {
                // Get all types in the assembly
                Type[] types = assembly.GetTypes();

                // Iterate over each type and check if it inherits from CovePlugin
                foreach (Type type in types)
                {
                    if (type.IsClass && type.IsSubclassOf(typeof(CovePlugin)))
                    {
                        object instance = Activator.CreateInstance(type, this);
                        CovePlugin plugin = instance as CovePlugin;
                        if (plugin != null)
                        {
                            string pluginConfig = readConfigFromPlugin($"{assembly.GetName().Name}.plugin.cfg", assembly);
                            if (pluginConfig == string.Empty) continue; // no config file found, its probably not a plugin
                            Dictionary<string, string> config = ConfigReader.ReadFile(pluginConfig);

                            PluginInstance thisInstance = new(plugin, config["name"], config["id"], config["author"]);

                            loadedPlugins.Add(thisInstance);
                            Log($"Plugin Init: {config["name"]}");
                            plugin.onInit(); // start the plugin!
                        }
                        else
                            Log($"Unable to load {type.FullName}");
                    }
                }
            }
            
            loadInternalPlugin("EntityPlugin", "DrMeepso", "Handles entity management and syncing", new Cove.InternalPlugins.EntityPlugin(this));
            
        }

        string readConfigFromPlugin(string fileIdentifyer, Assembly asm)
        {
            using (Stream fileStream = asm.GetManifestResourceStream(fileIdentifyer))
            {
                if (fileStream != null)
                {
                    StreamReader reader = new StreamReader(fileStream);
                    return reader.ReadToEnd();
                }
                else
                {
                    return "";
                }
            }
        }
        
        private void loadInternalPlugin(string name, string id, string author, CovePlugin plugin)
        {
            PluginInstance thisInstance = new(plugin, name, id, author);

            loadedPlugins.Add(thisInstance);
            Log($"Internal Plugin Init: {name}");
            plugin.onInit(); // start the plugin!
        }
        
    }
}
