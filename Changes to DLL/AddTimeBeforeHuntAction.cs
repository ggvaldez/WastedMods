using System;
using MoonSharp.Interpreter;

[MoonSharpUserData]
public class AddTimeBeforeHuntAction : ConditionAction
{
	public override bool Execute(Condition condition, BaseCharacter target, VariableDefinitions vars)
	{
		Globals.GetInstance().GetDungeon().timeUntilHunted += this.time_change;
		if (Globals.GetInstance().GetDungeon().timeUntilHunted < 0.1f)
		{
			Globals.GetInstance().GetDungeon().timeUntilHunted = 0.1f;
		}
		return true;
	}

	public float time_change;
}
