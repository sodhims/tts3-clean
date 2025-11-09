using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using TTS3.Services;

namespace TTS3
{
    public partial class App : Application
    {
        private ServiceProvider _serviceProvider;

        public App()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(ServiceCollection services)
        {
            // Register HttpClient
            services.AddSingleton<HttpClient>();

            // Register credential service
            services.AddSingleton<CredentialService>(provider =>
            {
                var credService = new CredentialService();
                credService.LoadCredentials();
                return credService;
            });

            // Register TTS services
            services.AddSingleton<SAPITTSService>();
            services.AddSingleton<GoogleTTSService>();
            services.AddSingleton<AWSPollyTTSService>();
            services.AddSingleton<ElevenLabsTTSService>();
            services.AddSingleton<LemonfoxTTSService>();

            // Register utility services
            services.AddSingleton<AudioPlaybackService>();
            services.AddSingleton<AudioMergeService>();
            services.AddSingleton<FileManagementService>();
            services.AddSingleton<SSMLProcessingService>();
            services.AddSingleton<SSMLValidationService>();

            // Register main window
            services.AddTransient<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}