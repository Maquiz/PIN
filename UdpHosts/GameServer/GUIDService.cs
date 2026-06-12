using System;
using System.Threading;

namespace GameServer.Data;

public static class GuidService
{
    private const byte MainServerId = 31;

    // Seeded randomly each start: item guids persist to the DB and are upserted
    // ON CONFLICT (item_guid), so restarts must not re-issue the same
    // (timestamp, counter) pairs. A fixed zero seed plus the uptime-based
    // timestamp made cross-restart collisions near-certain.
    private static int MainCounter = Random.Shared.Next();

    // https://github.com/themeldingwars/Documentation/wiki/Firefall-Guid-System#type-typecode
    public enum AdditionalTypes : byte
    {
        Instance = 0xFB,
        Army = 0xFC,
        Item = 0xFD,
        Character = 0xFE,
    }

    public static ulong GetNext(Enums.GSS.Controllers type = Enums.GSS.Controllers.Generic)
    {
        return GetNext((byte)type);
    }

    public static ulong GetNext(byte type)
    {
        // Wall clock, not shard uptime: uptime restarts at zero on every boot.
        // The packed field keeps 24 bits of (ms >> 8), wrapping every ~49.7
        // days; the random counter seed covers re-aligned windows.
        var timestamp = unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var counter = unchecked((uint)Interlocked.Increment(ref MainCounter));
        return new Core.Data.EntityGuid(MainServerId, timestamp, counter, type).Full;
    }
}
