using System;
using UnityEngine;

[Serializable]
public class AddDumpEffect:Effect
{
    
    private const int Priority = 0;
    private CardInstance dump;
    public AddDumpEffect(int magnitude) : base(EffectType.AddDump, magnitude) { }

    // AddDump는 subject(적용 대상)의 덱에 쓸모없는 카드를 얹는 디버프이므로 양수 magnitude는
    // Opponent, 음수는 User(디버프 완화)를 향한다.
    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Negative;

    public override void OnApply(CharacterManager subject)
    {
        CardDefinition dumpDefinition = CardDefinition.Create(this._magnitude, 1, Array.Empty<CardEffect>(), null,null);
        dump = new CardInstance(dumpDefinition, subject);
        subject.ReturnToDeck(dump);
    
    }

}
