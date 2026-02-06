using System.Linq;
using System.Text;
using GameServer.Data;
using GameServer.Data.SDB;

namespace GameServer.Systems.Chat.Commands;

[ChatCommand("List monster type IDs. Use 'hostile' to filter enemies only.", "enemies [hostile] [page]", "enemies", "npclist", "mobs")]
public class EnemyListChatCommand : ChatCommand
{
    private const int PageSize = 15;

    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        bool hostileOnly = false;
        int page = 1;

        foreach (var param in parameters)
        {
            if (param.Equals("hostile", System.StringComparison.OrdinalIgnoreCase))
            {
                hostileOnly = true;
            }
            else if (int.TryParse(param, out int p) && p >= 1)
            {
                page = p;
            }
        }

        var monsters = SDBInterface.GetAllMonsters();
        if (monsters == null || monsters.Count == 0)
        {
            SourceFeedback("No monsters found in SDB.", context);
            return;
        }

        var hostileIds = HardcodedCharacterData.HostileFactionIds;
        var filtered = hostileOnly
            ? monsters.Where(m => hostileIds.Contains(m.Value.FactionId))
            : monsters.AsEnumerable();

        var sorted = filtered.OrderBy(m => m.Key).ToList();
        int totalPages = (sorted.Count + PageSize - 1) / PageSize;
        if (page > totalPages)
        {
            page = totalPages;
        }

        var pageItems = sorted.Skip((page - 1) * PageSize).Take(PageSize);

        var sb = new StringBuilder();
        string label = hostileOnly ? "Hostile Monsters" : "All Monsters";
        sb.AppendLine($"--- {label} ({sorted.Count} total) Page {page}/{totalPages} ---");
        foreach (var kvp in pageItems)
        {
            var m = kvp.Value;
            string hostile = hostileIds.Contains(m.FactionId) ? "*" : " ";
            sb.AppendLine($" {hostile} {kvp.Key} F:{m.FactionId} C:{m.ChassisId}");
        }

        sb.Append($"Next: /enemies {(hostileOnly ? "hostile " : "")}{page + 1}  (* = hostile)");
        SourceFeedback(sb.ToString(), context);
    }
}
