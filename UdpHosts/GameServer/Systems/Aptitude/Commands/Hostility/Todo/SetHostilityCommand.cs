using AeroMessages.GSS.V66;
using GameServer.Data.SDB.Records.customdata;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class SetHostilityCommand : Command, ICommand
{
    private SetHostilityCommandDef Params;

    public SetHostilityCommand(SetHostilityCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        foreach (IAptitudeTarget target in context.Targets)
        {
            if (target is CharacterEntity character)
            {
                character.HostilityInfo = new HostilityInfoData
                {
                    Flags = HostilityInfoData.HostilityFlags.Faction,
                    FactionId = (byte)character.FactionId
                };
            }
        }

        return true;
    }
}
