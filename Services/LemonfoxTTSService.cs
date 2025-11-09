using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TTS3.Models;

namespace TTS3.Services
{
    /// <summary>
    /// Lemonfox Text-to-Speech service (powered by Kokoro-82M model)
    /// </summary>
    public class LemonfoxTTSService : ITTSService
    {
        private readonly HttpClient _httpClient;
        private readonly CredentialService _credentialService;

        public string Name => "Lemonfox AI";
        public bool RequiresApiKey => true;
        public bool IsConfigured => !string.IsNullOrEmpty(_credentialService.LemonfoxApiKey);

        // Kokoro voice mappings (use short names for API)
        private readonly Dictionary<string, (string VoiceId, string Language)> _voiceMap = new()
        {
            // US English Female voices
            { "Alloy (Female, US)", ("alloy", "en-us") },
            { "Sarah (Female, US)", ("sarah", "en-us") },
            { "Nova (Female, US)", ("nova", "en-us") },
            { "Heart (Female, US)", ("heart", "en-us") },
            { "Bella (Female, US)", ("bella", "en-us") },
            { "Jessica (Female, US)", ("jessica", "en-us") },
            { "Nicole (Female, US)", ("nicole", "en-us") },
            { "River (Female, US)", ("river", "en-us") },
            { "Sky (Female, US)", ("sky", "en-us") },
            { "Aoede (Female, US)", ("aoede", "en-us") },
            { "Kore (Female, US)", ("kore", "en-us") },
            
            // US English Male voices
            { "Adam (Male, US)", ("adam", "en-us") },
            { "Echo (Male, US)", ("echo", "en-us") },
            { "Onyx (Male, US)", ("onyx", "en-us") },
            { "Eric (Male, US)", ("eric", "en-us") },
            { "Liam (Male, US)", ("liam", "en-us") },
            { "Michael (Male, US)", ("michael", "en-us") },
            { "Fenrir (Male, US)", ("fenrir", "en-us") },
            { "Puck (Male, US)", ("puck", "en-us") },
            
            // British English Female voices
            { "Alice (Female, GB)", ("alice", "en-gb") },
            { "Emma (Female, GB)", ("emma", "en-gb") },
            { "Isabella (Female, GB)", ("isabella", "en-gb") },
            { "Lily (Female, GB)", ("lily", "en-gb") },
            
            // British English Male voices
            { "Daniel (Male, GB)", ("daniel", "en-gb") },
            { "Fable (Male, GB)", ("fable", "en-gb") },
            { "George (Male, GB)", ("george", "en-gb") },
            { "Lewis (Male, GB)", ("lewis", "en-gb") }
        };

        public LemonfoxTTSService(HttpClient httpClient, CredentialService credentialService)
        {
            _httpClient = httpClient;
            _credentialService = credentialService;
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        public List<VoiceInfo> GetAvailableVoices()
        {
            return _voiceMap.Select(kvp => new VoiceInfo
            {
                DisplayName = kvp.Key,
                VoiceId = kvp.Value.VoiceId,
                LanguageCode = kvp.Value.Language,
                Gender = kvp.Key.Contains("Male, ") ? "MALE" : "FEMALE",
                Engine = "kokoro"
            }).ToList();
        }

        public async Task<bool> ConvertToAudioAsync(TextSegment segment, string outputFile, ConversionSettings settings)
        {
            try
            {
                if (!IsConfigured)
                {
                    throw new Exception("Lemonfox API key not configured");
                }

                var (voiceId, language) = GetVoiceInfo(segment);

                // Build the API URL
                string url = "https://api.lemonfox.ai/v1/audio/speech";

                // Calculate speed from rate setting (rateValue is typically -10 to +10)
                // Convert to Lemonfox speed range (0.5 to 4.0)
                double speed = 1.0 + (settings.RateValue / 10.0); // Maps -10 to 0.0, +10 to 2.0
                speed = Math.Max(0.5, Math.Min(4.0, speed)); // Clamp to valid range

                // Lemonfox doesn't support volume control directly in the API
                // Volume adjustment would need to be done post-processing

                var requestBody = new
                {
                    input = segment.Text,
                    voice = voiceId,
                    language = language,
                    response_format = "wav",
                    speed = speed
                };

                // DEBUG: Show what we're sending to API
                Console.WriteLine($"LEMONFOX API REQUEST: voice='{voiceId}', language='{language}', speed={speed}, text='{segment.Text.Substring(0, Math.Min(30, segment.Text.Length))}'");

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Add authorization header
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_credentialService.LemonfoxApiKey}");

                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Lemonfox API error: {response.StatusCode} - {errorContent}");
                }

                // Read the audio bytes directly
                byte[] audioBytes = await response.Content.ReadAsByteArrayAsync();

                // Save as WAV file
                string wavFile = outputFile + ".wav";
                File.WriteAllBytes(wavFile, audioBytes);

                // Apply volume adjustment if needed (post-processing)
                if (Math.Abs(settings.VolumeValue - 100) > 1)
                {
                    ApplyVolumeAdjustment(wavFile, settings.VolumeValue);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lemonfox Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> TestVoiceAsync(string text, string outputFile, int voiceIndex, ConversionSettings settings)
        {
            var segment = new TextSegment
            {
                Text = text,
                VoiceIndex = voiceIndex
            };

            return await ConvertToAudioAsync(segment, outputFile, settings);
        }

        private (string VoiceId, string Language) GetVoiceInfo(TextSegment segment)
        {
            // CRITICAL: Use keys to maintain same order as GetAvailableVoices()
            var voiceList = _voiceMap.Select(kvp => kvp.Value).ToList();

            if (!string.IsNullOrEmpty(segment.CustomVoiceId))
            {
                // Try to find the language for this voice
                var matchingVoice = _voiceMap.Values.FirstOrDefault(v => v.VoiceId == segment.CustomVoiceId);
                if (matchingVoice != default)
                    return matchingVoice;

                return (segment.CustomVoiceId, "en-us");
            }

            if (segment.VoiceIndex >= 0 && segment.VoiceIndex < voiceList.Count)
            {
                var selectedVoice = voiceList[segment.VoiceIndex];
                Console.WriteLine($"DEBUG: VoiceIndex={segment.VoiceIndex} -> {selectedVoice.VoiceId} ({selectedVoice.Language})");
                return selectedVoice;
            }

            Console.WriteLine($"DEBUG: Using default voice (index out of range: {segment.VoiceIndex})");
            return ("alloy", "en-us"); // Default voice
        }

        private void ApplyVolumeAdjustment(string wavFile, double volumePercent)
        {
            try
            {
                // Read the WAV file
                byte[] audioData = File.ReadAllBytes(wavFile);

                // Skip WAV header (first 44 bytes for standard WAV)
                const int headerSize = 44;
                if (audioData.Length < headerSize)
                    return;

                // Calculate volume multiplier (100 = 1.0, 50 = 0.5, 200 = 2.0)
                double volumeMultiplier = volumePercent / 100.0;

                // Process audio samples (assuming 16-bit PCM)
                for (int i = headerSize; i < audioData.Length - 1; i += 2)
                {
                    // Read 16-bit sample (little-endian)
                    short sample = (short)(audioData[i] | (audioData[i + 1] << 8));

                    // Apply volume
                    int adjustedSample = (int)(sample * volumeMultiplier);

                    // Clamp to 16-bit range
                    adjustedSample = Math.Max(short.MinValue, Math.Min(short.MaxValue, adjustedSample));

                    // Write back (little-endian)
                    audioData[i] = (byte)(adjustedSample & 0xFF);
                    audioData[i + 1] = (byte)((adjustedSample >> 8) & 0xFF);
                }

                // Write modified audio back to file
                File.WriteAllBytes(wavFile, audioData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Volume adjustment error: {ex.Message}");
                // Continue anyway - better to have audio at wrong volume than no audio
            }
        }

        public void Dispose()
        {
            // HttpClient is typically managed by dependency injection, so we don't dispose it here
        }
    }
}
