using GameServer.Data.SDB.Records.customdata;
using GameServer.Entities.Character;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class ModifyDamageForInflictCommand : Command, ICommand
{
    private ModifyDamageForInflictCommandDef Params;

    public ModifyDamageForInflictCommand(ModifyDamageForInflictCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Apply weapon damage dealt modifier from stat modifiers
        if (context.Initiator is CharacterEntity initiator)
        {
            float mod = initiator.GetCurrentStatModifierValue(StatModifierIdentifier.WeaponDamageDealtMod);
            context.Register *= mod;
        }

        return true;
    }
}
