﻿using System;
using System.IO;
using System.Threading;
using System.Windows;

using FlyleafLib;

namespace FlyleafPlayer
{
    public partial class App : Application
    {
        public static string CmdUrl { get; set; } = null;
        public static string EnginePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Flyleaf.Engine.json");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Length == 1)
                CmdUrl = e.Args[0];

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

            Engine.Start(engineConfig);
        }

        private EngineConfig DefaultEngineConfig()
        {
            EngineConfig engineConfig = new EngineConfig();

            engineConfig.PluginsPath    = ":Plugins";
            engineConfig.FFmpegPath     = ":FFmpeg";
            engineConfig.FFmpegHLSLiveSeek
                                        = true;
            engineConfig.UIRefresh      = true;
            engineConfig.FFmpegDevices  = true;

            #if RELEASE
            engineConfig.LogOutput      = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Flyleaf.FirstRun.log");
            engineConfig.LogLevel       = LogLevel.Debug;
            #else
            engineConfig.LogOutput      = ":debug";
            engineConfig.LogLevel       = LogLevel.Debug;
            engineConfig.FFmpegLogLevel = FFmpegLogLevel.Warning;
            #endif

            return engineConfig;
        }
    }
}
