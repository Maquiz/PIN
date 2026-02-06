using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class InflictHitFeedbackCommand : Command, ICommand
{
    private InflictHitFeedbackCommandDef Params;

    public InflictHitFeedbackCommand(InflictHitFeedbackCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Hit feedback is handled inline by ApplyDamage via TookHit/DealtHit events
        return true;
    }
}
