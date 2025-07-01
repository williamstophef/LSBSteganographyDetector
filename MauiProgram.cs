using LSBSteganographyDetector;
using LSBSteganographyDetector.Services;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;

namespace LSBSteganographyDetector
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            // Remove Blazor WebView as it's not needed for this app

#if DEBUG
            builder.Services.AddLogging(configure => configure.AddDebug());
#endif

            // Register services
            builder.Services.AddSingleton<IStatisticalLSBDetector, StatisticalLSBDetectorRefactored>();

            return builder.Build();
        }
    }
}