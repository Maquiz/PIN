using GameServer.Data.SDB.Records.customdata;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class ReduceCooldownsCommand : Command, ICommand
{
    private ReduceCooldownsCommandDef Params;

    public ReduceCooldownsCommand(ReduceCooldownsCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is CharacterEntity character)
        {
            uint reductionMs = (uint)context.Register;
            if (reductionMs > 0)
            {
                character.ReduceCooldowns(reductionMs);
            }
        }

        return true;
    }
}
