using System;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class ConsumeEnergyCommand : Command, ICommand
{
    private ConsumeEnergyCommandDef Params;

    public ConsumeEnergyCommand(ConsumeEnergyCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Energy tracking is client-side (EnergyParams only has Max/Delay/Recharge/Time).
        // The server doesn't track current energy, so we allow the command to pass.
        // The client will enforce energy costs locally.
        float cost = Params.Amount;
        if (Params.AmountRegop != 0)
        {
            cost = AbilitySystem.RegistryOp(context.Register, Params.Amount, (Operand)Params.AmountRegop);
        }

        Console.WriteLine($"[Ability] ConsumeEnergy cost={cost} (server pass-through, client enforces)");
        return true;
    }
}
