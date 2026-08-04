using System;
using UnityEngine;

[Serializable]
public class AddDumpEffect:Effect
{
    
    private const int Priority = 0;
    private CardInstance dump;
    public AddDumpEffect(int magnitude) : base(EffectType.AddDump, magnitude) { }

    public override void OnApply(CharacterManager subject, CardEffect cardEffect)
    {
        CardDefinition dumpDefinition = CardDefinition.Create(this._magnitude, 1, Array.Empty<CardEffect>(), null,null);
        dump = new CardInstance(dumpDefinition, subject);
        subject.ReturnToDeck(dump);
    
    }

}
