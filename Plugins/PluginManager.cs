using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using TTS3.Plugins;

namespace TTS3
{
    /// <summary>
    /// Manages plugin loading and lifecycle
    /// </summary>
    public class PluginManager
    {
        private readonly MainWindow _mainWindow;
        private readonly List<IPlugin> _loadedPlugins = new List<IPlugin>();
        private readonly string _pluginsFolder;

        public IReadOnlyList<IPlugin> LoadedPlugins => _loadedPlugins.AsReadOnly();

        public PluginManager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            _pluginsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            
            // Create plugins folder if it doesn't exist
            if (!Directory.Exists(_pluginsFolder))
            {
                Directory.CreateDirectory(_pluginsFolder);
            }
        }

        /// <summary>
        /// Load all plugins from the Plugins folder
        /// </summary>
        public void LoadPlugins()
        {
            if (!Directory.Exists(_pluginsFolder))
                return;

            var dllFiles = Directory.GetFiles(_pluginsFolder, "*.dll", SearchOption.TopDirectoryOnly);

            foreach (var dllFile in dllFiles)
            {
                try
                {
                    LoadPlugin(dllFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load plugin {dllFile}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load a specific plugin DLL
        /// </summary>
        private void LoadPlugin(string dllPath)
        {
            try
            {
                var assembly = Assembly.LoadFrom(dllPath);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var pluginType in pluginTypes)
                {
                    var plugin = (IPlugin)Activator.CreateInstance(pluginType);
                    var host = new PluginHost(_mainWindow);
                    
                    plugin.Initialize(host);
                    _loadedPlugins.Add(plugin);
                    
                    Console.WriteLine($"Loaded plugin: {plugin.Name} v{plugin.Version} by {plugin.Author}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading plugin from {Path.GetFileName(dllPath)}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Unload all plugins
        /// </summary>
        public void UnloadAll()
        {
            foreach (var plugin in _loadedPlugins)
            {
                try
                {
                    plugin.Cleanup();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error cleaning up plugin {plugin.Name}: {ex.Message}");
                }
            }
            
            _loadedPlugins.Clear();
        }
    }

    /// <summary>
    /// Implementation of IPluginHost that provides access to TTS3 functionality
    /// </summary>
    internal class PluginHost : IPluginHost
    {
        private readonly MainWindow _mainWindow;

        public PluginHost(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public Window MainWindow => _mainWindow;

        public string GetCurrentText()
        {
            return _mainWindow.Dispatcher.Invoke(() =>
            {
                var textRange = new TextRange(
                    _mainWindow.rtbTextContent.Document.ContentStart,
                    _mainWindow.rtbTextContent.Document.ContentEnd);
                return textRange.Text;
            });
        }

        public void SetCurrentText(string text)
        {
            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.rtbTextContent.Document.Blocks.Clear();
                _mainWindow.rtbTextContent.Document.Blocks.Add(new Paragraph(new Run(text)));
            });
        }

        public List<string> GetCreatedAudioFiles()
        {
            return _mainWindow.Dispatcher.Invoke(() => 
                new List<string>(_mainWindow._createdAudioFiles));
        }

        public string GetLastOutputFolder()
        {
            return _mainWindow.Dispatcher.Invoke(() => _mainWindow._lastOutputFolder);
        }

        public void LogMessage(string message)
        {
            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                _mainWindow.txtLog.ScrollToEnd();
            });
        }

        public void ShowMessage(string message, string title)
        {
            _mainWindow.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(_mainWindow, message, title, 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        public string OpenFileDialog(string filter, string title)
        {
            return _mainWindow.Dispatcher.Invoke(() =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = filter,
                    Title = title
                };
                
                return dialog.ShowDialog() == true ? dialog.FileName : null;
            });
        }

        public string OpenFolderDialog(string title)
        {
            return _mainWindow.Dispatcher.Invoke(() =>
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = title
                };
                
                return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK 
                    ? dialog.SelectedPath : null;
            });
        }
    }
}