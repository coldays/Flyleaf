using FlyleafLib;
using System.Windows;
using Application = System.Windows.Application;

namespace SimplePlayer;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
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

        Engine.Start(engineConfig);
    }

    private EngineConfig DefaultEngineConfig()
    {
        EngineConfig engineConfig = new EngineConfig();

        engineConfig.PluginsPath = ":Plugins";
        engineConfig.FFmpegPath = ":FFmpeg";
        engineConfig.FFmpegHLSLiveSeek
                                    = true;
        engineConfig.UIRefresh = true;

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

