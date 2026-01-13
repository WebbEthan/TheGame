using System;
using UnityEngine;


// This is the parameter and scripting store
[Serializable]
public struct EffectDesign
{
    public string EffectScript;
    public string[] ParameterValues;
}
// this is the base effect class
public abstract class Effect : ScriptableObject
{
    [NonSerialized]
    public string Name;
    [NonSerialized]
    public float Duration;
    // Applys effect return ticks per second
    public virtual int ApplyEffect(StatBlock stats) { return 1; }
    public virtual void TickEffect(StatBlock stats) { }
    public virtual void ClearEffect(StatBlock stats) { }
}


// The rest of the file contains the scripts for various attack types and effect they apply


public class Hurt : Effect
{
    public float Damage;
    public override int ApplyEffect(StatBlock stats)
    {
        stats.HP -= Damage;
        return 0;
    }
}
public class Poison : Effect
{
    public float DamagePerTick;
    public int TicksPerSecond;
    public override int ApplyEffect(StatBlock stats)
    {
        return TicksPerSecond;
    }
    public override void TickEffect(StatBlock stats)
    {
        stats.HP -= DamagePerTick;
    }
}
