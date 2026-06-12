using Serilog.Events;

namespace GameServer;

/// <summary>
///     Holds the settings for the server`
/// </summary>
public class GameServerSettings
{
    /// <summary>
    ///     The log level to use for the logger. Any messages below this level won't be printed to console.
    /// </summary>
    public LogEventLevel? LogLevel { get; set; }

    /// <summary>
    ///     UDP port the game server should be listening on
    /// </summary>
    public ushort Port { get; set; } = 25001;

    /// <summary>
    ///    Address to use to connect to RIN.InternalAPI for GRPC. If the connection fails, GRPC will not be used.
    /// </summary>
    public string GrpcChannelAddress { get; set; } = "http://localhost:5201";

    /// <summary>
    ///    File path to the clientdb.sd2 located in system\db\ of the Firefall installation
    /// </summary>
    public string StaticDBPath { get; set; } = @"C:\Program Files\Steam\steamapps\common\Firefall\system\db\clientdb.sd2";

    /// <summary>
    ///    ZoneId to load
    /// </summary>
    public uint ZoneId { get; set; } = 448;

    /// <summary>
    ///    File path to PIN Maps Data
    /// </summary>
    public string MapsPath { get; set; } = string.Empty;

    /// <summary>
    ///    Enable loading world collision data from PIN Maps Data
    /// </summary>
    public bool LoadMapsCollision { get; set; } = false;

    /// <summary>
    ///    Enable loading entities on zone startup
    /// </summary>
    public bool LoadZoneEntities { get; set; } = true;

    /// <summary>
    ///    DEV ONLY: when RIN is unreachable, log in with the hardcoded fallback
    ///    character/inventory instead of refusing the login. Off by default so a
    ///    broken gRPC link can't silently diverge players from their DB state.
    /// </summary>
    public bool AllowHardcodedFallback { get; set; } = false;
}