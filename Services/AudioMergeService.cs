using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;

namespace TTS3.Services
{
    /// <summary>
    /// Service for merging multiple audio files
    /// </summary>
    public class AudioMergeService
    {
        /// <summary>
        /// Merge multiple audio files into one
        /// </summary>
        public async Task MergeAudioFilesAsync(List<string> inputFiles, string outputFile)
        {
            if (inputFiles == null || inputFiles.Count == 0)
            {
                throw new ArgumentException("No input files provided");
            }

            if (inputFiles.Count == 1)
            {
                // Just copy the file if only one input
                File.Copy(inputFiles[0], outputFile, true);
                return;
            }

            await Task.Run(() =>
            {
                WaveFormat outputFormat = null;

                // Get format from first file
                using (var reader = new AudioFileReader(inputFiles[0]))
                {
                    outputFormat = reader.WaveFormat;
                }

                using (var writer = new WaveFileWriter(outputFile, outputFormat))
                {
                    foreach (var file in inputFiles)
                    {
                        if (!File.Exists(file))
                        {
                            Console.WriteLine($"Warning: File not found: {file}");
                            continue;
                        }

                        using (var reader = new AudioFileReader(file))
                        {
                            // Resample if formats don't match
                            if (reader.WaveFormat.SampleRate != outputFormat.SampleRate ||
                                reader.WaveFormat.Channels != outputFormat.Channels)
                            {
                                using (var resampler = new MediaFoundationResampler(reader, outputFormat))
                                {
                                    resampler.ResamplerQuality = 60;
                                    byte[] buffer = new byte[outputFormat.AverageBytesPerSecond];
                                    int bytesRead;
                                    while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        writer.Write(buffer, 0, bytesRead);
                                    }
                                }
                            }
                            else
                            {
                                // Same format, just copy
                                reader.CopyTo(writer);
                            }
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Get total duration of merged audio
        /// </summary>
        public TimeSpan GetTotalDuration(List<string> inputFiles)
        {
            TimeSpan total = TimeSpan.Zero;

            foreach (var file in inputFiles)
            {
                if (File.Exists(file))
                {
                    try
                    {
                        using (var reader = new AudioFileReader(file))
                        {
                            total += reader.TotalTime;
                        }
                    }
                    catch
                    {
                        // Skip files that can't be read
                    }
                }
            }

            return total;
        }
    }
}