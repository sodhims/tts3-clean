using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon;
using Amazon.Polly;
using Amazon.Polly.Model;
using Amazon.Runtime;
using TTS3.Models;

namespace TTS3.Services
{
    /// <summary>
    /// Amazon Polly Text-to-Speech service
    /// </summary>
    public class AWSPollyTTSService : ITTSService
    {
        private readonly CredentialService _credentialService;
        private AmazonPollyClient _pollyClient;

        public string Name => "AWS Polly";
        public bool RequiresApiKey => true;
        public bool IsConfigured => _pollyClient != null;

        private readonly Dictionary<string, (string VoiceId, string Engine)> _voiceMap = new()
        {
            // Standard US English voices
            { "Joanna (Female, US)", ("Joanna", "standard") },
            { "Matthew (Male, US)", ("Matthew", "standard") },
            { "Ivy (Female, Child, US)", ("Ivy", "standard") },
            { "Joey (Male, US)", ("Joey", "standard") },
            { "Justin (Male, Child, US)", ("Justin", "standard") },
            { "Kendra (Female, US)", ("Kendra", "standard") },
            { "Kimberly (Female, US)", ("Kimberly", "standard") },
            { "Salli (Female, US)", ("Salli", "standard") },
            
            // Neural US English voices
            { "Joanna Neural (Female, US)", ("Joanna", "neural") },
            { "Matthew Neural (Male, US)", ("Matthew", "neural") },
            { "Kevin Neural (Male, US)", ("Kevin", "neural") },
            { "Ruth Neural (Female, US)", ("Ruth", "neural") },
            { "Ivy Neural (Female, Child, US)", ("Ivy", "neural") },
            { "Joey Neural (Male, US)", ("Joey", "neural") },
            { "Justin Neural (Male, Child, US)", ("Justin", "neural") },
            { "Kendra Neural (Female, US)", ("Kendra", "neural") },
            { "Kimberly Neural (Female, US)", ("Kimberly", "neural") },
            { "Salli Neural (Female, US)", ("Salli", "neural") },
            
            // Other English variants
            { "Nicole (Female, AU)", ("Nicole", "standard") },
            { "Russell (Male, AU)", ("Russell", "standard") },
            { "Amy (Female, GB)", ("Amy", "standard") },
            { "Brian (Male, GB)", ("Brian", "standard") },
            { "Emma (Female, GB)", ("Emma", "standard") },
            { "Amy Neural (Female, GB)", ("Amy", "neural") },
            { "Brian Neural (Male, GB)", ("Brian", "neural") },
            { "Emma Neural (Female, GB)", ("Emma", "neural") },
            { "Aditi (Female, IN)", ("Aditi", "standard") },
            { "Raveena (Female, IN)", ("Raveena", "standard") }
        };

        public AWSPollyTTSService(CredentialService credentialService)
        {
            _credentialService = credentialService;
            InitializeClient();
        }

        public void InitializeClient()
        {
            if (!string.IsNullOrEmpty(_credentialService.AWSAccessKey) && 
                !string.IsNullOrEmpty(_credentialService.AWSSecretKey))
            {
                var credentials = new BasicAWSCredentials(
                    _credentialService.AWSAccessKey, 
                    _credentialService.AWSSecretKey);
                var region = RegionEndpoint.GetBySystemName(_credentialService.AWSRegion);
                _pollyClient = new AmazonPollyClient(credentials, region);
            }
        }

        public List<Models.VoiceInfo> GetAvailableVoices()
        {
            return _voiceMap.Select(kvp => new Models.VoiceInfo
            {
                DisplayName = kvp.Key,
                VoiceId = kvp.Value.VoiceId,
                Engine = kvp.Value.Engine,
                LanguageCode = "en-US"
            }).ToList();
        }

        public async Task<bool> ConvertToAudioAsync(TextSegment segment, string outputFile, ConversionSettings settings)
        {
            try
            {
                if (_pollyClient == null)
                {
                    throw new Exception("AWS Polly client not initialized");
                }

                var (voiceId, engine) = GetVoiceInfo(segment);
                bool useSSML = ContainsSSMLTags(segment.Text);

                var request = new SynthesizeSpeechRequest
                {
                    OutputFormat = OutputFormat.Pcm,
                    VoiceId = voiceId,
                    Engine = engine == "neural" ? Engine.Neural : Engine.Standard,
                    SampleRate = "16000",
                    TextType = useSSML ? TextType.Ssml : TextType.Text
                };

                if (useSSML)
                {
                    string ssmlText = ConvertToAWSSSML(segment.Text);
                    if (Math.Abs(settings.RateValue) > 0.1 || Math.Abs(settings.VolumeValue - 100) > 1)
                    {
                        ssmlText = AddAWSProsody(ssmlText, settings.RateValue, settings.VolumeValue);
                    }
                    request.Text = ssmlText;
                }
                else
                {
                    if (Math.Abs(settings.RateValue) > 0.1 || Math.Abs(settings.VolumeValue - 100) > 1)
                    {
                        string prosodyText = WrapAWSProsody(segment.Text, settings.RateValue, settings.VolumeValue);
                        request.Text = prosodyText;
                        request.TextType = TextType.Ssml;
                    }
                    else
                    {
                        request.Text = segment.Text;
                    }
                }

                var response = await _pollyClient.SynthesizeSpeechAsync(request);

                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    string wavFile = outputFile + ".wav";
                    await WritePCMToWav(response.AudioStream, wavFile);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AWS Polly Error: {ex.Message}");
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

        private (string VoiceId, string Engine) GetVoiceInfo(TextSegment segment)
        {
            var voices = _voiceMap.Values.ToList();

            if (!string.IsNullOrEmpty(segment.CustomVoiceId))
                return (segment.CustomVoiceId, "neural");

            if (segment.VoiceIndex >= 0 && segment.VoiceIndex < voices.Count)
                return voices[segment.VoiceIndex];

            return voices.FirstOrDefault();
        }

        private bool ContainsSSMLTags(string text)
        {
            string[] ssmlTags = { "<emphasis", "<break", "<prosody", "<say-as", "<phoneme", "<sub", "<audio", "<p>", "<s>" };
            return ssmlTags.Any(tag => text.Contains(tag, StringComparison.OrdinalIgnoreCase));
        }

        private string ConvertToAWSSSML(string text)
        {
            if (text.TrimStart().StartsWith("<speak", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(text, @"<speak[^>]*>(.*?)</speak>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (match.Success)
                    text = match.Groups[1].Value;
            }

            // Convert pitch values
            text = Regex.Replace(text, @"pitch=""high""", @"pitch=""+20%""", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"pitch=""x-high""", @"pitch=""+40%""", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"pitch=""low""", @"pitch=""-20%""", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"pitch=""x-low""", @"pitch=""-40%""", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"pitch=""medium""", @"pitch=""+0%""", RegexOptions.IgnoreCase);

            text = Regex.Replace(text, @"pitch=""([+-]?\d+)st""", match =>
            {
                int semitones = int.Parse(match.Groups[1].Value);
                int percentage = semitones * 8;
                return $"pitch=\"{(percentage >= 0 ? "+" : "")}{percentage}%\"";
            }, RegexOptions.IgnoreCase);

            return $"<speak>{text}</speak>";
        }

        private string WrapAWSProsody(string text, double rateValue, double volumeValue)
        {
            var prosodyAttrs = new List<string>();

            if (Math.Abs(rateValue) > 0.1)
            {
                int ratePercent = (int)(100 + (rateValue * 10));
                prosodyAttrs.Add($"rate=\"{ratePercent}%\"");
            }

            if (Math.Abs(volumeValue - 100) > 1)
            {
                string volumeStr = volumeValue >= 80 ? "x-loud" :
                                   volumeValue >= 60 ? "loud" :
                                   volumeValue >= 40 ? "medium" :
                                   volumeValue >= 20 ? "soft" : "x-soft";
                prosodyAttrs.Add($"volume=\"{volumeStr}\"");
            }

            if (prosodyAttrs.Count > 0)
            {
                string attrs = string.Join(" ", prosodyAttrs);
                return $"<speak><prosody {attrs}>{text}</prosody></speak>";
            }

            return $"<speak>{text}</speak>";
        }

        private string AddAWSProsody(string ssmlText, double rateValue, double volumeValue)
        {
            var match = Regex.Match(ssmlText, @"<speak>(.*?)</speak>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!match.Success)
                return ssmlText;

            string content = match.Groups[1].Value;
            var prosodyAttrs = new List<string>();

            if (Math.Abs(rateValue) > 0.1)
            {
                int ratePercent = (int)(100 + (rateValue * 10));
                prosodyAttrs.Add($"rate=\"{ratePercent}%\"");
            }

            if (Math.Abs(volumeValue - 100) > 1)
            {
                string volumeStr = volumeValue >= 80 ? "x-loud" :
                                   volumeValue >= 60 ? "loud" :
                                   volumeValue >= 40 ? "medium" :
                                   volumeValue >= 20 ? "soft" : "x-soft";
                prosodyAttrs.Add($"volume=\"{volumeStr}\"");
            }

            if (prosodyAttrs.Count > 0)
            {
                string attrs = string.Join(" ", prosodyAttrs);
                return $"<speak><prosody {attrs}>{content}</prosody></speak>";
            }

            return ssmlText;
        }

        private async Task WritePCMToWav(System.IO.Stream pcmStream, string outputFile)
        {
            using (var fs = new FileStream(outputFile, FileMode.Create))
            using (var writer = new BinaryWriter(fs))
            {
                byte[] buffer = new byte[8192];
                var pcmData = new List<byte>();
                int bytesRead;

                while ((bytesRead = await pcmStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    pcmData.AddRange(buffer.Take(bytesRead));
                }

                // Write WAV header for 16000 Hz, 16-bit mono PCM
                int sampleRate = 16000;
                short bitsPerSample = 16;
                short channels = 1;
                int byteRate = sampleRate * channels * (bitsPerSample / 8);
                short blockAlign = (short)(channels * (bitsPerSample / 8));
                int dataSize = pcmData.Count;

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
                writer.Write(pcmData.ToArray());
            }
        }

        public void Dispose()
        {
            _pollyClient?.Dispose();
        }
    }
}