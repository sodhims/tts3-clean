using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TTS3.Models;

namespace TTS3.Services
{
    /// <summary>
    /// Google Cloud Text-to-Speech service
    /// </summary>
    public class GoogleTTSService : ITTSService
    {
        private readonly HttpClient _httpClient;
        private readonly CredentialService _credentialService;

        public string Name => "Google Cloud TTS";
        public bool RequiresApiKey => true;
        public bool IsConfigured => !string.IsNullOrEmpty(_credentialService.GoogleApiKey);

        private readonly Dictionary<string, string> _voiceMap = new()
        {
            { "en-US-Wavenet-A (Female)", "en-US-Wavenet-A" },
            { "en-US-Wavenet-B (Male)", "en-US-Wavenet-B" },
            { "en-US-Wavenet-C (Female)", "en-US-Wavenet-C" },
            { "en-US-Wavenet-D (Male)", "en-US-Wavenet-D" },
            { "en-US-Wavenet-E (Female)", "en-US-Wavenet-E" },
            { "en-US-Wavenet-F (Female)", "en-US-Wavenet-F" },
            { "en-US-Neural2-A (Male)", "en-US-Neural2-A" },
            { "en-US-Neural2-C (Female)", "en-US-Neural2-C" },
            { "en-US-Neural2-D (Male)", "en-US-Neural2-D" },
            { "en-US-Neural2-E (Female)", "en-US-Neural2-E" },
            { "en-US-Standard-A (Male)", "en-US-Standard-A" },
            { "en-US-Standard-B (Male)", "en-US-Standard-B" },
            { "en-US-Standard-C (Female)", "en-US-Standard-C" },
            { "en-US-Standard-D (Male)", "en-US-Standard-D" },
            { "en-US-Standard-E (Female)", "en-US-Standard-E" }
        };

        public GoogleTTSService(HttpClient httpClient, CredentialService credentialService)
        {
            _httpClient = httpClient;
            _credentialService = credentialService;
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public List<VoiceInfo> GetAvailableVoices()
        {
            return _voiceMap.Select(kvp => new VoiceInfo
            {
                DisplayName = kvp.Key,
                VoiceId = kvp.Value,
                LanguageCode = "en-US",
                Gender = kvp.Key.Contains("Male") ? "MALE" : "FEMALE",
                Engine = kvp.Value.Contains("Neural") ? "neural" : 
                         kvp.Value.Contains("Wavenet") ? "wavenet" : "standard"
            }).ToList();
        }

        public async Task<bool> ConvertToAudioAsync(TextSegment segment, string outputFile, ConversionSettings settings)
        {
            try
            {
                if (!IsConfigured)
                {
                    throw new Exception("Google API key not configured");
                }

                string voiceToUse = GetVoiceId(segment);
                string url = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={_credentialService.GoogleApiKey}";

                string gender = DetermineGender(voiceToUse);
                bool useSSML = ContainsSSMLTags(segment.Text);

                object inputObject = useSSML 
                    ? new { ssml = ConvertToGoogleSSML(segment.Text) }
                    : new { text = segment.Text };

                var requestBody = new
                {
                    input = inputObject,
                    voice = new
                    {
                        languageCode = "en-US",
                        name = voiceToUse,
                        ssmlGender = gender
                    },
                    audioConfig = new
                    {
                        audioEncoding = "LINEAR16",
                        speakingRate = 1.0 + (settings.RateValue / 10.0),
                        pitch = 0.0,
                        volumeGainDb = (settings.VolumeValue - 100) / 5.0
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Google TTS API error: {response.StatusCode} - {errorContent}");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var responseData = JsonSerializer.Deserialize<Dictionary<string, object>>(responseJson);

                if (responseData.ContainsKey("audioContent"))
                {
                    string audioContent = responseData["audioContent"].ToString();
                    byte[] audioBytes = Convert.FromBase64String(audioContent);

                    string wavFile = outputFile + ".wav";
                    WriteWavFile(wavFile, audioBytes, 24000);

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google TTS Error: {ex.Message}");
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
            var voices = _voiceMap.Values.ToList();

            if (!string.IsNullOrEmpty(segment.CustomVoiceId))
                return segment.CustomVoiceId;

            if (segment.VoiceIndex >= 0 && segment.VoiceIndex < voices.Count)
                return voices[segment.VoiceIndex];

            return voices.FirstOrDefault() ?? "en-US-Wavenet-D";
        }

        private string DetermineGender(string voiceId)
        {
            if (voiceId.Contains("-C") || voiceId.Contains("-E") || 
                voiceId.Contains("-F") || voiceId.Contains("-H"))
                return "FEMALE";
            
            if (voiceId.Contains("-A") || voiceId.Contains("-B") || 
                voiceId.Contains("-D") || voiceId.Contains("-I") || 
                voiceId.Contains("-J"))
                return "MALE";

            return "NEUTRAL";
        }

        private bool ContainsSSMLTags(string text)
        {
            string[] ssmlTags = { "<emphasis", "<break", "<prosody", "<say-as", "<phoneme", "<sub", "<audio", "<p>", "<s>" };
            return ssmlTags.Any(tag => text.Contains(tag, StringComparison.OrdinalIgnoreCase));
        }

        private string ConvertToGoogleSSML(string text)
        {
            if (text.TrimStart().StartsWith("<speak", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(text, @"<speak[^>]*>(.*?)</speak>", 
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (match.Success)
                    text = match.Groups[1].Value;
            }

            // Convert pitch values
            text = Regex.Replace(text, @"pitch=""high""", @"pitch=""+5st""", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"pitch=""x-high""", @"pitch=""+10st""", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"pitch=""low""", @"pitch=""-5st""", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"pitch=""x-low""", @"pitch=""-10st""", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"pitch=""medium""", @"pitch=""+0st""", RegexOptions.IgnoreCase);

            return $"<speak>{text}</speak>";
        }

        private void WriteWavFile(string filename, byte[] audioData, int sampleRate)
        {
            using (var fs = new FileStream(filename, FileMode.Create))
            using (var writer = new BinaryWriter(fs))
            {
                short bitsPerSample = 16;
                short channels = 1;
                int byteRate = sampleRate * channels * (bitsPerSample / 8);
                short blockAlign = (short)(channels * (bitsPerSample / 8));
                int dataSize = audioData.Length;

                writer.Write(Encoding.UTF8.GetBytes("RIFF"));
                writer.Write(dataSize + 36);
                writer.Write(Encoding.UTF8.GetBytes("WAVE"));
                writer.Write(Encoding.UTF8.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write(bitsPerSample);
                writer.Write(Encoding.UTF8.GetBytes("data"));
                writer.Write(dataSize);
                writer.Write(audioData);
            }
        }
    }
}