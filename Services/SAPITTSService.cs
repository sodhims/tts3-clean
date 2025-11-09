using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TTS3.Models;
using SpeechVoiceInfo = System.Speech.Synthesis.VoiceInfo;

namespace TTS3.Services
{
    /// <summary>
    /// Windows SAPI Text-to-Speech service
    /// </summary>
    public class SAPITTSService : ITTSService
    {
        private readonly SpeechSynthesizer _synthesizer;

        public string Name => "Windows SAPI";
        public bool RequiresApiKey => false;
        public bool IsConfigured => true; // Always available on Windows

        public SAPITTSService()
        {
            _synthesizer = new SpeechSynthesizer();
            _synthesizer.SetOutputToDefaultAudioDevice();
        }

        public List<Models.VoiceInfo> GetAvailableVoices()
        {
            var voices = new List<Models.VoiceInfo>();
            var installedVoices = _synthesizer.GetInstalledVoices();

            foreach (var voice in installedVoices)
            {
                voices.Add(new Models.VoiceInfo
                {
                    VoiceId = voice.VoiceInfo.Name,
                    DisplayName = voice.VoiceInfo.Name,
                    LanguageCode = voice.VoiceInfo.Culture.Name,
                    Gender = voice.VoiceInfo.Gender.ToString(),
                    Engine = "standard"
                });
            }

            return voices;
        }

        public async Task<bool> ConvertToAudioAsync(TextSegment segment, string outputFile, ConversionSettings settings)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var synth = new SpeechSynthesizer())
                    {
                        var voices = synth.GetInstalledVoices();

                        // Select voice
                        if (segment.VoiceIndex >= 0 && segment.VoiceIndex < voices.Count)
                        {
                            synth.SelectVoice(voices[segment.VoiceIndex].VoiceInfo.Name);
                        }

                        synth.Rate = (int)settings.RateValue;
                        synth.Volume = (int)settings.VolumeValue;

                        string wavFile = outputFile + ".wav";
                        synth.SetOutputToWaveFile(wavFile);

                        // Check if text contains SSML
                        if (ContainsSSMLTags(segment.Text))
                        {
                            string ssmlText = WrapInSSML(segment.Text);
                            synth.SpeakSsml(ssmlText);
                        }
                        else
                        {
                            synth.Speak(segment.Text);
                        }

                        synth.SetOutputToDefaultAudioDevice();

                        // Verify file was created
                        if (!File.Exists(wavFile))
                        {
                            throw new Exception("SAPI did not create output file");
                        }

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SAPI Error: {ex.Message}");
                    return false;
                }
            });
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

        private bool ContainsSSMLTags(string text)
        {
            string[] ssmlTags = { "<emphasis", "<break", "<prosody", "<say-as", "<phoneme", "<sub", "<audio", "<p>", "<s>" };
            return ssmlTags.Any(tag => text.Contains(tag, StringComparison.OrdinalIgnoreCase));
        }

        private string WrapInSSML(string text)
        {
            if (text.TrimStart().StartsWith("<speak", StringComparison.OrdinalIgnoreCase))
                return text;

            // Fix pitch values for SAPI
            text = Regex.Replace(text, @"pitch=""\+(\d+)st""", @"pitch=""high""", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"pitch=""-(\d+)st""", @"pitch=""low""", RegexOptions.IgnoreCase);

            return $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">{text}</speak>";
        }

        public void Dispose()
        {
            _synthesizer?.Dispose();
        }
    }
}