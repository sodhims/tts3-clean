using System.Collections.Generic;
using System.Threading.Tasks;
using TTS3.Models;

namespace TTS3.Services
{
    /// <summary>
    /// Interface for Text-to-Speech service implementations
    /// </summary>
    public interface ITTSService
    {
        /// <summary>
        /// Service name for display
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Whether this service requires an API key
        /// </summary>
        bool RequiresApiKey { get; }

        /// <summary>
        /// Whether this service is properly configured
        /// </summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Get list of available voices
        /// </summary>
        List<VoiceInfo> GetAvailableVoices();

        /// <summary>
        /// Convert text segment to audio file
        /// </summary>
        Task<bool> ConvertToAudioAsync(TextSegment segment, string outputFile, ConversionSettings settings);

        /// <summary>
        /// Test a voice with sample text
        /// </summary>
        Task<bool> TestVoiceAsync(string text, string outputFile, int voiceIndex, ConversionSettings settings);
    }
}