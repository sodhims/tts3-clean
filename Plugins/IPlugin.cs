using System;
using System.Collections.Generic;
using System.Windows;

namespace TTS3.Plugins
{
    /// <summary>
    /// Interface for TTS3 plugins
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Plugin name displayed in menu
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Plugin description
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Plugin version
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Plugin author
        /// </summary>
        string Author { get; }

        /// <summary>
        /// Icon/emoji for the plugin (optional)
        /// </summary>
        string Icon { get; }

        /// <summary>
        /// Initialize the plugin with host context
        /// </summary>
        void Initialize(IPluginHost host);

        /// <summary>
        /// Execute the plugin
        /// </summary>
        void Execute();

        /// <summary>
        /// Cleanup when plugin is unloaded
        /// </summary>
        void Cleanup();
    }

    /// <summary>
    /// Host interface providing access to TTS3 functionality
    /// </summary>
    public interface IPluginHost
    {
        /// <summary>
        /// Main window reference
        /// </summary>
        Window MainWindow { get; }

        /// <summary>
        /// Get current text content
        /// </summary>
        string GetCurrentText();

        /// <summary>
        /// Set text content
        /// </summary>
        void SetCurrentText(string text);

        /// <summary>
        /// Get list of created audio files
        /// </summary>
        List<string> GetCreatedAudioFiles();

        /// <summary>
        /// Get last output folder
        /// </summary>
        string GetLastOutputFolder();

        /// <summary>
        /// Log message to output
        /// </summary>
        void LogMessage(string message);

        /// <summary>
        /// Show message box
        /// </summary>
        void ShowMessage(string message, string title);

        /// <summary>
        /// Open file dialog
        /// </summary>
        string OpenFileDialog(string filter, string title);

        /// <summary>
        /// Open folder dialog
        /// </summary>
        string OpenFolderDialog(string title);
    }
}