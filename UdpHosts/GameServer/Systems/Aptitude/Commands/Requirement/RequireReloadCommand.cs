using System;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class RequireReloadCommand : Command, ICommand
{
    private RequireReloadCommandDef Params;

    public RequireReloadCommand(RequireReloadCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        bool result = false;

        // NOTE: Investigate target handling
        var target = context.Self;

        if (target is CharacterEntity character)
        {
            if (Params.Inittime == 1)
            {
                result = character.Character_CombatView.WeaponReloadedProp > context.InitTime;
                Console.WriteLine($"[RequireReload] ReloadedProp={character.Character_CombatView.WeaponReloadedProp} vs InitTime={context.InitTime} → raw={result}, Negate={Params.Negate}");
            }
            else
            {
                // Inittime=0: treat as "weapon has been reloaded" (always true — no time comparison)
                result = true;
                Console.WriteLine($"[RequireReload] Inittime=0, defaulting result=true, Negate={Params.Negate}");
            }
        }
        else
        {
            Console.WriteLine("RequireReloadCommand fails because target is not a Character. If this is happening, we should investigate why.");
        }

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }
}