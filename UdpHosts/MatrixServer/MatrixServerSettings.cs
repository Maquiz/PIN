using Serilog.Events;

namespace MatrixServer;

/// <summary>
///     Holds the settings for the server
/// </summary>
public class MatrixServerSettings
{
    /// <summary>
    ///     The log level to use for the logger. Any messages below this level won't be printed to console.
    /// </summary>
    public LogEventLevel? LogLevel { get; set; }

    /// <summary>
    ///     UDP port the matrix server should be listening on
    /// </summary>
    public ushort Port { get; set; } = 25000;

    /// <summary>
    ///     Server id handed to the client in the KISS/HUGG handshake
    /// </summary>
    public ushort GameServerId { get; set; } = 1;

    /// <summary>
    ///     UDP port of the GameServer the client is handed in the KISS/HUGG
    ///     handshake. The client connects to this port on the same host it
    ///     reached the matrix server on.
    /// </summary>
    public ushort GameServerPort { get; set; } = 25001;
}