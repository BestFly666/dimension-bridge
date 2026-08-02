using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SimpleXmlEditor.Localization;

namespace SimpleXmlEditor.Services
{
    /// <summary>
    /// Discovers and manages file format and post-process plugins.
    /// Uses built-in scanning first; external DLL loading can be added later.
    /// </summary>
    public class PluginLoader
    {
        private readonly List<IFileFormatPlugin> _formatPlugins = new();
        private readonly List<IPostProcessPlugin> _postPlugins = new();

        public IReadOnlyList<IFileFormatPlugin> FormatPlugins => _formatPlugins;
        public IReadOnlyList<IPostProcessPlugin> PostPlugins => _postPlugins;

        public event Action<string> LogMessage;

        private void Log(string msg) => LogMessage?.Invoke(msg);

        /// <summary>
        /// Scan the assembly for plugin implementations.
        /// </summary>
        public void DiscoverBuiltInPlugins()
        {
            var assembly = Assembly.GetExecutingAssembly();
            DiscoverInAssembly(assembly);
        }

        /// <summary>
        /// Load external plugins from a DLL directory.
        /// </summary>
        public void DiscoverExternalPlugins(string pluginDir)
        {
            if (!Directory.Exists(pluginDir))
                return;

            foreach (var dllPath in Directory.GetFiles(pluginDir, "*.dll"))
            {
                try
                {
                    var asm = Assembly.LoadFrom(dllPath);
                    DiscoverInAssembly(asm);
                    Log(LocalizationManager.GetString("LogPluginLoaded", Path.GetFileName(dllPath)));
                }
                catch (Exception ex)
                {
                    Log(LocalizationManager.GetString("LogPluginLoadFailed", Path.GetFileName(dllPath), ex.Message));
                }
            }
        }

        private void DiscoverInAssembly(Assembly asm)
        {
            foreach (var type in asm.GetExportedTypes())
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                // Check for file format plugins
                if (typeof(IFileFormatPlugin).IsAssignableFrom(type))
                {
                    try
                    {
                        var plugin = (IFileFormatPlugin)Activator.CreateInstance(type)!;
                        _formatPlugins.Add(plugin);
                        Log(LocalizationManager.GetString("LogPluginFormatDiscovered", plugin.FormatName, string.Join(", ", plugin.FileExtensions)));
                    }
                    catch (Exception ex)
                    {
                        Log(LocalizationManager.GetString("LogPluginFormatFailed", type.Name, ex.Message));
                    }
                }

                // Check for post-process plugins
                if (typeof(IPostProcessPlugin).IsAssignableFrom(type))
                {
                    try
                    {
                        var plugin = (IPostProcessPlugin)Activator.CreateInstance(type)!;
                        _postPlugins.Add(plugin);
                        Log(LocalizationManager.GetString("LogPluginPostDiscovered", plugin.Name));
                    }
                    catch (Exception ex)
                    {
                        Log(LocalizationManager.GetString("LogPluginPostFailed", type.Name, ex.Message));
                    }
                }
            }
        }

        /// <summary>
        /// Find the first format plugin that can handle the given file extension.
        /// </summary>
        public IFileFormatPlugin FindFormatPlugin(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLower();
            return _formatPlugins.FirstOrDefault(p => 
                p.FileExtensions.Any(e => e.ToLower() == ext));
        }

        /// <summary>
        /// Get all supported file extensions across all format plugins.
        /// </summary>
        public string[] GetAllSupportedExtensions()
        {
            var all = new List<string>();
            foreach (var p in _formatPlugins)
                all.AddRange(p.FileExtensions);
            return all.Distinct().ToArray();
        }
    }
}
