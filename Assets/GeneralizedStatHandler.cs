using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface StatBlock
{ 
    public float HP { get; set; }
    public float MaxHP { get; set; }
    public PhysicsAttributeSet PhysicsAttributeSet { get; set; }
    public void Hurt();
    public void Heal();
    public void DIE();
}

public abstract class GeneralizedStateHandler : MonoBehaviour, StatBlock
{
    // Interface Handling
    private float _hp;
    public float HP 
    {
        get 
        {
            return _hp;
        }
        set 
        { 
            if (value < _hp)
            {
                // when damaged
                _hp = value;
                if (_hp <= 0)
                {
                    DIE();
                }
                else
                {
                    Hurt();
                }
            }
            else
            {
                // when healed
                _hp = Mathf.Clamp(value, 0, MaxHP);
                Heal();
            }
        }
    }
    public float MaxHP { get; set; }
    public PhysicsAttributeSet Attributes;
    public PhysicsAttributeSet PhysicsAttributeSet { get { return Attributes; } set { Attributes = value; } }
    public abstract void Hurt();
    public abstract void Heal();
    public abstract void DIE();

    #region Effects
    // Scripable Attribute Effect System
    private Dictionary<string, Effect> AppliedEffects = new Dictionary<string, Effect>();
    public virtual void ApplyEffect(Effect effect, string cause)
    {
        if (AppliedEffects.ContainsKey(effect.Name))
        {
            // Join effects
            AppliedEffects[effect.Name].Duration += effect.Duration/4;
        }
        else
        {
            // Start Effect
            AppliedEffects.Add(effect.Name, effect);
            StartCoroutine(EffectRunner(effect.Name));
        }
    }
    public virtual void RemoveEffect(string effectName)
    {
        if (!AppliedEffects.ContainsKey(effectName)) return;
        AppliedEffects[effectName].ClearEffect(this);
        AppliedEffects.Remove(effectName);
    }

    // Handles Effect Timing
    private IEnumerator EffectRunner(string effect)
    {
        // Get Tick Time and call ApplyEffect
        int ticking = AppliedEffects[effect].ApplyEffect(this);
        float delayTime = ticking > 0 ? 1f / (float)ticking : 1f;
        // Tick effect
        while (AppliedEffects[effect].Duration > 0 || AppliedEffects[effect].Duration == -1)
        {
            AppliedEffects[effect].TickEffect(this);
            yield return new WaitForSeconds(delayTime);
            AppliedEffects[effect].Duration-=delayTime;
        }
        // remove effect
        RemoveEffect(effect);
    }
    #endregion
}
