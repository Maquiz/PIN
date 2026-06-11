using GameServer.Data.SDB;

namespace GameServer.Admin;

[ServerCommand("Remove an item from your inventory by guid or typeId", "deleteitem <itemGuid|typeId>", "deleteitem", "delete_item", "removeitem", "remove_item", "salvage")]
public class DeleteItemServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        var inventory = context.SourcePlayer?.Inventory;
        if (inventory == null)
        {
            SourceFeedback("Need a player inventory", context);
            return;
        }

        if (parameters.Length == 0)
        {
            SourceFeedback("Usage: deleteitem <itemGuid|typeId>", context);
            return;
        }

        ulong id = ParseULongParameter(parameters[0]);
        if (id == 0)
        {
            SourceFeedback("Could not parse item guid or typeId", context);
            return;
        }

        // Small values are sdb type ids; resolve to the first matching item.
        // Item guids are full 64-bit entity ids and far exceed uint range.
        ulong guid = id <= uint.MaxValue ? inventory.FindItemGuidBySdbId((uint)id) : id;
        if (guid == 0)
        {
            SourceFeedback($"No item with typeId {id} in inventory", context);
            return;
        }

        if (inventory.RemoveItem(guid))
        {
            SourceFeedback($"Removed item {guid}", context);
        }
        else
        {
            SourceFeedback($"No item with guid {guid} in inventory", context);
        }
    }
}
