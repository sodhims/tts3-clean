using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NAudio.Wave;
using TTS3.Models;

namespace TTS3.Services
{
    /// <summary>
    /// ElevenLabs Text-to-Speech service
    /// </summary>
    public class ElevenLabsTTSService : ITTSService
    {
        private readonly HttpClient _httpClient;
        private readonly CredentialService _credentialService;

        public string Name => "ElevenLabs";
        public bool RequiresApiKey => true;
        public bool IsConfigured => !string.IsNullOrEmpty(_credentialService.ElevenLabsApiKey);

        private readonly Dictionary<string, string> _voiceMap = new()
        {
            // Pre-made voices (free tier)
            { "Rachel (Female, US)", "21m00Tcm4TlvDq8ikWAM" },
            { "Domi (Female, US)", "AZnzlk1XvdvUeBnXmlld" },
            { "Bella (Female, US)", "EXAVITQu4vr4xnSDxMaL" },
            { "Antoni (Male, US)", "ErXwobaYiN019PkySvjV" },
            { "Elli (Female, US)", "MF3mGyEYCl7XYWbV9V6O" },
            { "Josh (Male, US)", "TxGEqnHWrfWFTfGW9XjX" },
            { "Arnold (Male, US)", "VR6AewLTigWG4xSOukaG" },
            { "Adam (Male, US)", "pNInz6obpgDQGcFmaJgB" },
            { "Sam (Male, US)", "yoZ06aMxZJJ28mfd3POQ" }
        };

        private readonly Dictionary<string, string> _customVoiceIds = new()
        {
            { "myself", "kYUEq80KUacVYxEHVvbX" }
        };

        public ElevenLabsTTSService(HttpClient httpClient, CredentialService credentialService)
        {
            _httpClient = httpClient;
            _credentialService = credentialService;
        }

        public List<Models.VoiceInfo> GetAvailableVoices()
        {
            return _voiceMap.Select(kvp => new Models.VoiceInfo
            {
                DisplayName = kvp.Key,
                VoiceId = kvp.Value,
                LanguageCode = "en-US",
                Engine = "neural"
            }).ToList();
        }

        public async Task<bool> ConvertToAudioAsync(TextSegment segment, string outputFile, ConversionSettings settings)
        {
            try
            {
                if (!IsConfigured)
                {
                    throw new Exception("ElevenLabs API key not configured");
                }

                string voiceId = GetVoiceId(segment);
                string url = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}";

                var requestBody = new
                {
                    text = segment.Text,
                    model_id = "eleven_monolingual_v1",
                    voice_settings = new
                    {
                        stability = 0.5 + (settings.RateValue / 20.0),
                        similarity_boost = 0.75,
                        style = 0.0,
                        use_speaker_boost = true
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.Add("xi-api-key", _credentialService.ElevenLabsApiKey);
                    request.Content = content;

                    var response = await _httpClient.SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        throw new Exception($"ElevenLabs API error: {response.StatusCode} - {errorContent}");
                    }

                    var audioBytes = await response.Content.ReadAsByteArrayAsync();

                    // ElevenLabs returns MP3, convert to WAV
                    string tempMp3 = Path.GetTempFileName() + ".mp3";
                    File.WriteAllBytes(tempMp3, audioBytes);

                    try
                    {
                        string wavFile = outputFile + ".wav";
                        using (var reader = new Mp3FileReader(tempMp3))
                        using (var writer = new WaveFileWriter(wavFile, reader.WaveFormat))
                        {
                            reader.CopyTo(writer);
                        }

                        File.Delete(tempMp3);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        File.Delete(tempMp3);
                        throw new Exception($"Error converting MP3 to WAV: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ElevenLabs Error: {ex.Message}");
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

        private string GetVoiceId(TextSegment segment)
        {
            // Check for custom voice ID
            if (!string.IsNullOrEmpty(segment.CustomVoiceId))
            {
                // Check if it's a named custom voice
                if (_customVoiceIds.ContainsKey(segment.CustomVoiceId.ToLower()))
                {
                    return _customVoiceIds[segment.CustomVoiceId.ToLower()];
                }
                // Use the voice ID directly
                return segment.CustomVoiceId;
            }

            var voices = _voiceMap.Values.ToList();

            if (segment.VoiceIndex >= 0 && segment.VoiceIndex < voices.Count)
                return voices[segment.VoiceIndex];

            return voices.FirstOrDefault() ?? "21m00Tcm4TlvDq8ikWAM";
        }
    }
}