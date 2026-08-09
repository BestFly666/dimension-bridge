using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.ExpertProfiles;
using SimpleXmlEditor.Services;
using SimpleXmlEditor.ViewModels;

namespace SimpleXmlEditor
{
    public partial class App : Application
    {
        public static ServiceProvider Services { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();

            // Domain services (Singleton — shared state)
            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<IGlossaryManager, GlossaryManager>();
            services.AddSingleton<IBlacklistManager, BlacklistManager>();
            services.AddSingleton<IExpertProfileManager, ExpertProfileManager>();

            // AI services
            services.AddSingleton<IAiTranslationService, AiTranslationService>();
            services.AddSingleton<ITranslationEvaluator, TranslationEvaluator>();

            // Infrastructure
            services.AddSingleton<IXmlRepository, XmlRepository>();

            // Orchestrator is created by MainViewModel with the real log action
            // (DI would inject a no-op Action<string>, silencing all orchestrator logs)

            // ViewModel
            services.AddSingleton<MainViewModel>();

            // UI (Transient — new instance each time)
            services.AddTransient<MainWindow>();

            Services = services.BuildServiceProvider();

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Services?.Dispose();
            base.OnExit(e);
        }
    }
}
