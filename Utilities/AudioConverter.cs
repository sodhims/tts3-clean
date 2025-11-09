using System;
using System.IO;
using NAudio.Lame;
using NAudio.Wave;

namespace TTS3.Utilities
{
    /// <summary>
    /// Utility for converting audio file formats
    /// </summary>
    public static class AudioConverter
    {
        /// <summary>
        /// Convert WAV file to MP3
        /// </summary>
        public static void ConvertWavToMp3(string wavFile, string mp3File)
        {
            if (!File.Exists(wavFile))
            {
                throw new FileNotFoundException($"WAV file not found: {wavFile}");
            }

            using (var reader = new AudioFileReader(wavFile))
            using (var writer = new LameMP3FileWriter(mp3File, reader.WaveFormat, LAMEPreset.STANDARD))
            {
                reader.CopyTo(writer);
            }
        }

        /// <summary>
        /// Get audio file duration
        /// </summary>
        public static TimeSpan GetDuration(string audioFile)
        {
            if (!File.Exists(audioFile))
            {
                return TimeSpan.Zero;
            }

            try
            {
                using (var reader = new AudioFileReader(audioFile))
                {
                    return reader.TotalTime;
                }
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Check if file is a valid audio file
        /// </summary>
        public static bool IsValidAudioFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            try
            {
                using (var reader = new AudioFileReader(filePath))
                {
                    return reader.TotalTime > TimeSpan.Zero;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get audio file format info
        /// </summary>
        public static AudioFileInfo GetAudioInfo(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                using (var reader = new AudioFileReader(filePath))
                {
                    return new AudioFileInfo
                    {
                        Duration = reader.TotalTime,
                        SampleRate = reader.WaveFormat.SampleRate,
                        Channels = reader.WaveFormat.Channels,
                        BitsPerSample = reader.WaveFormat.BitsPerSample,
                        FileSize = new FileInfo(filePath).Length
                    };
                }
            }
            catch
            {
                return null;
            }
        }
    }

    public class AudioFileInfo
    {
        public TimeSpan Duration { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int BitsPerSample { get; set; }
        public long FileSize { get; set; }

        public string FormattedDuration => Duration.ToString(@"mm\:ss");
        public string FormattedFileSize => FormatBytes(FileSize);

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}