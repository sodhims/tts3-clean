using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TTS3.Models;

namespace TTS3.Services
{
    /// <summary>
    /// Service for processing SSML and control tags
    /// </summary>
    public class SSMLProcessingService
    {
        /// <summary>
        /// Strip comment tags from text
        /// </summary>
        public string StripComments(string text)
        {
            // Remove comments with quotes: <comment="...">
            text = Regex.Replace(text, @"<comment\s*=\s*""[^""]*"">", "", RegexOptions.IgnoreCase);

            // Remove comments without quotes: <comment=...>
            text = Regex.Replace(text, @"<comment\s*=\s*[^>]+>", "", RegexOptions.IgnoreCase);

            return text;
        }

        /// <summary>
        /// Process text into segments based on split and voice tags
        /// </summary>
        public List<TextSegment> ProcessTextSegments(string text)
        {
            // Strip comments first
            text = StripComments(text);

            var segments = new List<TextSegment>();

            // Split by <split> tags
            var splitPattern = @"<split>";
            var majorParts = Regex.Split(text, splitPattern, RegexOptions.IgnoreCase);

            int currentOutputNumber = 1;

            for (int splitIndex = 0; splitIndex < majorParts.Length; splitIndex++)
            {
                var part = majorParts[splitIndex];
                if (string.IsNullOrWhiteSpace(part)) continue;

                // Check for <label=N> tag
                var labelMatch = Regex.Match(part, @"^\s*<label=(\d+)>", RegexOptions.IgnoreCase);
                int? labelNumber = null;

                if (labelMatch.Success)
                {
                    labelNumber = int.Parse(labelMatch.Groups[1].Value);
                    currentOutputNumber = labelNumber.Value;
                    part = part.Substring(labelMatch.Length);
                }

                // Process service/voice changes within section
                var subSegments = ProcessServiceAndVoiceChanges(part, currentOutputNumber, labelNumber);
                segments.AddRange(subSegments);

                currentOutputNumber++;
            }

            if (segments.Count == 0)
            {
                segments.Add(new TextSegment
                {
                    Text = text,
                    VoiceIndex = 0,
                    ServiceIndex = -1,
                    SplitIndex = 0,
                    SubIndex = 0,
                    LabelNumber = null
                });
            }

            return segments;
        }

        private List<TextSegment> ProcessServiceAndVoiceChanges(string text, int outputNumber, int? labelNumber)
        {
            var segments = new List<TextSegment>();
            int currentVoiceIndex = 0;
            int currentServiceIndex = -1;
            string currentCustomVoiceId = null;
            int subIndex = 0;

            // Find all service, voice, and vid tags
            var pattern = @"<(voice|service|vid)=([^>]+)>";
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);

            if (matches.Count == 0)
            {
                var cleanText = text.Trim();
                if (!string.IsNullOrEmpty(cleanText))
                {
                    segments.Add(new TextSegment
                    {
                        Text = cleanText,
                        VoiceIndex = currentVoiceIndex,
                        ServiceIndex = currentServiceIndex,
                        CustomVoiceId = currentCustomVoiceId,
                        SplitIndex = outputNumber,
                        SubIndex = subIndex,
                        LabelNumber = labelNumber
                    });
                }
                return segments;
            }

            int lastIndex = 0;

            foreach (Match match in matches)
            {
                // Add text before the tag
                if (match.Index > lastIndex)
                {
                    var beforeText = text.Substring(lastIndex, match.Index - lastIndex).Trim();
                    if (!string.IsNullOrEmpty(beforeText))
                    {
                        segments.Add(new TextSegment
                        {
                            Text = beforeText,
                            VoiceIndex = currentVoiceIndex,
                            ServiceIndex = currentServiceIndex,
                            CustomVoiceId = currentCustomVoiceId,
                            SplitIndex = outputNumber,
                            SubIndex = subIndex,
                            LabelNumber = labelNumber
                        });
                        subIndex++;
                    }
                }

                // Process the tag
                string tagType = match.Groups[1].Value.ToLower();
                string tagValue = match.Groups[2].Value.Trim('"', ' ');

                if (tagType == "voice")
                {
                    currentVoiceIndex = int.Parse(tagValue) - 1;
                    currentCustomVoiceId = null;
                }
                else if (tagType == "service")
                {
                    currentServiceIndex = int.Parse(tagValue) - 1;
                }
                else if (tagType == "vid")
                {
                    currentCustomVoiceId = tagValue;
                    if (currentServiceIndex < 0)
                    {
                        currentServiceIndex = 3; // Default to ElevenLabs
                    }
                }

                lastIndex = match.Index + match.Length;
            }

            // Add remaining text after last tag
            if (lastIndex < text.Length)
            {
                var remainingText = text.Substring(lastIndex).Trim();
                if (!string.IsNullOrEmpty(remainingText))
                {
                    segments.Add(new TextSegment
                    {
                        Text = remainingText,
                        VoiceIndex = currentVoiceIndex,
                        ServiceIndex = currentServiceIndex,
                        CustomVoiceId = currentCustomVoiceId,
                        SplitIndex = outputNumber,
                        SubIndex = subIndex,
                        LabelNumber = labelNumber
                    });
                }
            }

            return segments;
        }

        /// <summary>
        /// Check if text contains SSML tags
        /// </summary>
        public bool ContainsSSMLTags(string text)
        {
            string[] ssmlTags = { "<emphasis", "<break", "<prosody", "<say-as", "<phoneme", "<sub", "<audio", "<p>", "<s>" };
            return ssmlTags.Any(tag => text.Contains(tag, StringComparison.OrdinalIgnoreCase));
        }
    }
}