using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TTS3.Models;

namespace TTS3.Services
{
    /// <summary>
    /// Service for managing files and folders
    /// </summary>
    public class FileManagementService
    {
        /// <summary>
        /// Get all audio files in a directory
        /// </summary>
        public List<AudioFileItem> GetAudioFilesInDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return new List<AudioFileItem>();
            }

            var audioFiles = new List<AudioFileItem>();

            var wavFiles = Directory.GetFiles(directoryPath, "*.wav");
            var mp3Files = Directory.GetFiles(directoryPath, "*.mp3");

            var allFiles = wavFiles.Concat(mp3Files).OrderBy(f => f).ToList();

            foreach (var file in allFiles)
            {
                var fileInfo = new FileInfo(file);
                audioFiles.Add(new AudioFileItem
                {
                    FullPath = file,
                    DisplayName = fileInfo.Name,
                    FileSize = fileInfo.Length
                });
            }

            return audioFiles;
        }

        /// <summary>
        /// Load text from file
        /// </summary>
        public string LoadTextFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            return File.ReadAllText(filePath);
        }

        /// <summary>
        /// Save text to file
        /// </summary>
        public void SaveTextFile(string filePath, string content)
        {
            File.WriteAllText(filePath, content);
        }

        /// <summary>
        /// Delete file safely
        /// </summary>
        public bool DeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting file: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Delete multiple files
        /// </summary>
        public int DeleteFiles(List<string> filePaths)
        {
            int deletedCount = 0;
            foreach (var file in filePaths)
            {
                if (DeleteFile(file))
                {
                    deletedCount++;
                }
            }
            return deletedCount;
        }

        /// <summary>
        /// Open folder in file explorer
        /// </summary>
        public void OpenFolderInExplorer(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", folderPath);
            }
        }

        /// <summary>
        /// Open file in file explorer (select in folder)
        /// </summary>
        public void SelectFileInExplorer(string filePath)
        {
            if (File.Exists(filePath))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
        }

        /// <summary>
        /// Get next available filename
        /// </summary>
        public string GetNextAvailableFilename(string directory, string baseFilename, string extension)
        {
            int counter = 1;
            string filename = Path.Combine(directory, $"{baseFilename}{extension}");

            while (File.Exists(filename))
            {
                filename = Path.Combine(directory, $"{baseFilename}_{counter}{extension}");
                counter++;
            }

            return filename;
        }

        /// <summary>
        /// Ensure directory exists
        /// </summary>
        public void EnsureDirectoryExists(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }
    }
}