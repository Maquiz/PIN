using System;
using System.Configuration;
using Autofac;
using Serilog;
using Shared.Common;
using SDB = FauFau.Formats.StaticDB;

namespace GameServer;

public class GameServerModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        RegisterTypes(builder);
        RegisterInstances(builder);

        base.Load(builder);
    }

    private static void RegisterTypes(ContainerBuilder builder)
    {
        builder.RegisterType<GameServerSettings>().SingleInstance();
        builder.RegisterType<SDB>().SingleInstance();
        builder.RegisterType<GameServer>();
    }

    /// <summary>
    ///     Setting lookup: GAMESERVER_* environment variable wins over App.config.
    ///     Lets containerized/production deployments configure without editing files.
    /// </summary>
    private static string GetSetting(string key)
    {
        return Environment.GetEnvironmentVariable($"GAMESERVER_{key.ToUpperInvariant()}") ?? ConfigurationManager.AppSettings[key];
    }

    private static void RegisterInstances(ContainerBuilder builder)
    {
        builder.Register(ctx =>
        {
            var settings = new GameServerSettings();

            if (GetSetting("Port") != null)
            {
                settings.Port = ushort.Parse(GetSetting("Port"));
            }

            if (GetSetting("GrpcChannelAddress") != null)
            {
                settings.GrpcChannelAddress = GetSetting("GrpcChannelAddress");
            }

            if (GetSetting("StaticDBPath") != null)
            {
                settings.StaticDBPath = GetSetting("StaticDBPath");
            }

            if (GetSetting("ZoneId") != null)
            {
                settings.ZoneId = uint.Parse(GetSetting("ZoneId"));
            }

            if (GetSetting("MapsPath") != null)
            {
                settings.MapsPath = GetSetting("MapsPath");

                if (GetSetting("LoadMapsCollision") != null)
                {
                    if (bool.TryParse(GetSetting("LoadMapsCollision"), out bool value))
                    {
                        settings.LoadMapsCollision = value;
                    }
                    else
                    {
                        Console.WriteLine($"Cannot parse LoadMapsCollision setting value");
                    }
                }
            }

            if (GetSetting("LoadZoneEntities") != null)
            {
                if (bool.TryParse(GetSetting("LoadZoneEntities"), out bool value))
                {
                    settings.LoadZoneEntities = value;
                }
                else
                {
                    Console.WriteLine($"Cannot parse LoadZoneEntities setting value");
                }
            }

            if (GetSetting("AllowHardcodedFallback") != null)
            {
                if (bool.TryParse(GetSetting("AllowHardcodedFallback"), out bool value))
                {
                    settings.AllowHardcodedFallback = value;
                }
                else
                {
                    Console.WriteLine($"Cannot parse AllowHardcodedFallback setting value");
                }
            }

            return settings;
        })
        .As<GameServerSettings>().SingleInstance();

        builder.Register(ctx =>
        {
            var loggerConfig = new LoggerConfiguration()
            .ReadFrom.AppSettings()
            .WriteTo.Console(theme: SerilogTheme.Custom);

            var settings = ctx.Resolve<GameServerSettings>();

            if (settings.LogLevel.HasValue)
            {
                loggerConfig = loggerConfig.MinimumLevel.Is(settings.LogLevel.Value);
            }

            return loggerConfig.CreateLogger();
        })
        .As<ILogger>().SingleInstance();

        builder.Register(ctx =>
        {
            var settings = ctx.Resolve<GameServerSettings>();
            
            Console.WriteLine($"Opening SDB from {settings.StaticDBPath}");
            var sdb = new SDB();
            sdb.Read(settings.StaticDBPath);

            return sdb;
        })
        .As<SDB>().SingleInstance();
    }
}