using GameServer.Data.SDB.Records.customdata;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class ResetCooldownsCommand : Command, ICommand
{
    private ResetCooldownsCommandDef Params;

    public ResetCooldownsCommand(ResetCooldownsCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is CharacterEntity character)
        {
            character.ResetAllCooldowns();
        }

        return true;
    }
}
