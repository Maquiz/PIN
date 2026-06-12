using System.Collections.Generic;

namespace GameServer.Data;

/// <summary>
/// World-level faction relationship constants. These are world rules, not
/// per-character data — kept separate so retiring HardcodedCharacterData
/// doesn't drag them along. Eventually this should come from SDB faction data.
/// </summary>
public static class FactionRelations
{
    public static readonly HashSet<uint> HostileFactionIds = new() { 2, 3, 5, 6, 7, 8, 17, 22, 42, 43, 45, 46, 47, 48 };
}
