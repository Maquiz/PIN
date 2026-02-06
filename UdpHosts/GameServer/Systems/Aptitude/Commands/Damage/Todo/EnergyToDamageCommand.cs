using System;
using GameServer.Data.SDB.Records.aptfs;

namespace GameServer.Aptitude;

public class EnergyToDamageCommand : Command, ICommand
{
    private EnergyToDamageCommandDef Params;

    public EnergyToDamageCommand(EnergyToDamageCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Convert energy to damage: consume energy and set register to damage value
        if (Params.EnergyPerPoint <= 0)
        {
            return true;
        }

        float energyAvailable = Params.MaxEnergyAllowed;
        float damage = energyAvailable / Params.EnergyPerPoint;

        context.Register = damage;

        return true;
    }
}
