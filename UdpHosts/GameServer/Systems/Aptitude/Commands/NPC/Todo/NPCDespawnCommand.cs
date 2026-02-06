using GameServer.Data.SDB.Records.customdata;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class NPCDespawnCommand : Command, ICommand
{
    private NPCDespawnCommandDef Params;

    public NPCDespawnCommand(NPCDespawnCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character || character.IsPlayerControlled)
        {
            return true;
        }

        // Unregister from AI and remove entity
        context.Shard.AI.UnregisterNpc(character.EntityId);
        context.Shard.EntityMan.Remove(character.EntityId);

        return true;
    }
}
