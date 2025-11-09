using System;
using System.IO;
using System.Text.Json;

namespace TTS3.Services
{
    /// <summary>
    /// Manages TTS service credentials with automatic persistence
    /// </summary>
    public class CredentialService
    {
        private string _credentialsPath;

        // Properties
        public string GoogleApiKey { get; set; }
        public string AWSAccessKey { get; set; }
        public string AWSSecretKey { get; set; }
        public string AWSRegion { get; set; }
        public string ElevenLabsApiKey { get; set; }
        public string LemonfoxApiKey { get; set; }
        public string LemonfoxUsername { get; set; }

        public CredentialService()
        {
            // Initialize credentials path
            _credentialsPath = GetCredentialsPath();

            // CRITICAL: Load existing credentials at startup
            LoadCredentials();
        }

        /// <summary>
        /// Gets the path to the credentials file in user's AppData
        /// </summary>
        private string GetCredentialsPath()
        {
            // Store in user's AppData (has write permissions)
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appData, "TTS3");

            // Create folder if it doesn't exist
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            return Path.Combine(appFolder, "credentials.json");
        }

        /// <summary>
        /// Saves credentials to file
        /// </summary>
        public void SaveCredentials()
        {
            try
            {
                var settings = new CredentialSettings
                {
                    GoogleApiKey = this.GoogleApiKey ?? string.Empty,
                    AWSAccessKey = this.AWSAccessKey ?? string.Empty,
                    AWSSecretKey = this.AWSSecretKey ?? string.Empty,
                    AWSRegion = this.AWSRegion ?? "us-east-1",
                    ElevenLabsApiKey = this.ElevenLabsApiKey ?? string.Empty,
                    LemonfoxApiKey = this.LemonfoxApiKey ?? string.Empty,
                    LemonfoxUsername = this.LemonfoxUsername ?? string.Empty
                };

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_credentialsPath, json);

                System.Diagnostics.Debug.WriteLine($"Credentials saved to: {_credentialsPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving credentials: {ex.Message}");
                // Don't throw - failing to save credentials shouldn't crash the app
            }
        }

        /// <summary>
        /// Loads credentials from file
        /// </summary>
        public void LoadCredentials()
        {
            try
            {
                if (!File.Exists(_credentialsPath))
                {
                    System.Diagnostics.Debug.WriteLine("No credentials file found - using defaults");

                    // Set defaults
                    GoogleApiKey = string.Empty;
                    AWSAccessKey = string.Empty;
                    AWSSecretKey = string.Empty;
                    AWSRegion = "us-east-1";
                    ElevenLabsApiKey = string.Empty;
                    LemonfoxApiKey = string.Empty;
                    LemonfoxUsername = string.Empty;

                    return;
                }

                var json = File.ReadAllText(_credentialsPath);
                var settings = JsonSerializer.Deserialize<CredentialSettings>(json);

                if (settings != null)
                {
                    GoogleApiKey = settings.GoogleApiKey ?? string.Empty;
                    AWSAccessKey = settings.AWSAccessKey ?? string.Empty;
                    AWSSecretKey = settings.AWSSecretKey ?? string.Empty;
                    AWSRegion = settings.AWSRegion ?? "us-east-1";
                    ElevenLabsApiKey = settings.ElevenLabsApiKey ?? string.Empty;
                    LemonfoxApiKey = settings.LemonfoxApiKey ?? string.Empty;
                    LemonfoxUsername = settings.LemonfoxUsername ?? string.Empty;

                    System.Diagnostics.Debug.WriteLine($"Credentials loaded from: {_credentialsPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading credentials: {ex.Message}");
                // Don't throw - failing to load credentials shouldn't crash the app
            }
        }

        /// <summary>
        /// Settings class for JSON serialization
        /// </summary>
        private class CredentialSettings
        {
            public string GoogleApiKey { get; set; }
            public string AWSAccessKey { get; set; }
            public string AWSSecretKey { get; set; }
            public string AWSRegion { get; set; }
            public string ElevenLabsApiKey { get; set; }
            public string LemonfoxApiKey { get; set; }
            public string LemonfoxUsername { get; set; }
        }
    }
}
