using System.Configuration;
using Autofac;
using Serilog;
using Shared.Common;

namespace MatrixServer;

public class MatrixServerModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        RegisterTypes(builder);
        RegisterInstances(builder);
        base.Load(builder);
    }

    /// <summary>
    ///     Setting lookup: MATRIXSERVER_* environment variable wins over App.config.
    /// </summary>
    private static string GetSetting(string key)
    {
        return System.Environment.GetEnvironmentVariable($"MATRIXSERVER_{key.ToUpperInvariant()}") ?? ConfigurationManager.AppSettings[key];
    }

    private static void RegisterTypes(ContainerBuilder builder)
    {
        builder.Register(_ =>
        {
            var settings = new MatrixServerSettings();

            if (GetSetting("Port") != null)
            {
                settings.Port = ushort.Parse(GetSetting("Port"));
            }

            if (GetSetting("GameServerId") != null)
            {
                settings.GameServerId = ushort.Parse(GetSetting("GameServerId"));
            }

            if (GetSetting("GameServerPort") != null)
            {
                settings.GameServerPort = ushort.Parse(GetSetting("GameServerPort"));
            }

            return settings;
        }).SingleInstance();
        builder.RegisterType<MatrixServer>();
    }

    private static void RegisterInstances(ContainerBuilder builder)
    {
        builder.Register(ctx =>
                         {
                             var loggerConfig = new LoggerConfiguration()
                                                .ReadFrom.AppSettings()
                                                .WriteTo.Console(theme: SerilogTheme.Custom);

                             var settings = ctx.Resolve<MatrixServerSettings>();

                             if (settings.LogLevel.HasValue)
                             {
                                 loggerConfig = loggerConfig.MinimumLevel.Is(settings.LogLevel.Value);
                             }

                             return loggerConfig.CreateLogger();
                         }).As<ILogger>().SingleInstance();
    }
}