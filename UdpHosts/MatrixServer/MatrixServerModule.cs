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

    private static void RegisterTypes(ContainerBuilder builder)
    {
        builder.Register(_ =>
        {
            var settings = new MatrixServerSettings();

            if (ConfigurationManager.AppSettings["Port"] != null)
            {
                settings.Port = ushort.Parse(ConfigurationManager.AppSettings["Port"]);
            }

            if (ConfigurationManager.AppSettings["GameServerId"] != null)
            {
                settings.GameServerId = ushort.Parse(ConfigurationManager.AppSettings["GameServerId"]);
            }

            if (ConfigurationManager.AppSettings["GameServerPort"] != null)
            {
                settings.GameServerPort = ushort.Parse(ConfigurationManager.AppSettings["GameServerPort"]);
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