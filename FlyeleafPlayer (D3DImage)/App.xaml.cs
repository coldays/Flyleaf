using FlyleafLib;
using System.Windows;

namespace FlyeleafPlayer__D3DImage_;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Ensures that we have enough worker threads to avoid the UI from freezing or not updating on time
        ThreadPool.GetMinThreads(out int workers, out int ports);
        ThreadPool.SetMinThreads(workers + 6, ports + 6);

        EngineConfig engineConfig;

        // Engine's Config
#if RELEASE
            if (File.Exists(EnginePath))
                try { engineConfig = EngineConfig.Load(EnginePath); } catch { engineConfig = DefaultEngineConfig(); }
            else
                engineConfig = DefaultEngineConfig();
#else
        engineConfig = DefaultEngineConfig();
#endif

        Engine.StartAsync(engineConfig);
    }

    private EngineConfig DefaultEngineConfig()
    {
        EngineConfig engineConfig = new EngineConfig();

        engineConfig.PluginsPath = ":Plugins";
        engineConfig.FFmpegPath = ":FFmpeg";
        engineConfig.FFmpegHLSLiveSeek
                                    = true;
        engineConfig.UIRefresh = true;
        engineConfig.FFmpegDevices = true;

#if RELEASE
            engineConfig.LogOutput      = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Flyleaf.FirstRun.log");
            engineConfig.LogLevel       = LogLevel.Debug;
#else
        engineConfig.LogOutput = ":debug";
        engineConfig.LogLevel = LogLevel.Debug;
        engineConfig.FFmpegLogLevel = FFmpegLogLevel.Warning;
#endif

        return engineConfig;
    }
}

