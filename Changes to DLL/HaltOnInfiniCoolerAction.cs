using System;
using MoonSharp.Interpreter;

[MoonSharpUserData]
public class HaltOnInfiniCoolerAction : ConditionAction
{
	public override bool Execute(Condition condition, BaseCharacter target, VariableDefinitions vars)
	{
		return !Globals.GetInstance().IsInfiniteCooler();
	}
}
