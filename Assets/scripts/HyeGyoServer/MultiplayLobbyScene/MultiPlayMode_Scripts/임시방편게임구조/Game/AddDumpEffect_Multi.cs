using System;
using System.Reflection;
using UnityEngine;

[Serializable]
public class AddDumpEffect_Multi : Effect_Multi
{
    private CardInstance_Multi dump;

    public AddDumpEffect_Multi(int magnitude)
        : base(EffectType.AddDump, magnitude)
    {
    }

    public override EffectTargetPolarity TargetPolarity =>
        EffectTargetPolarity.Negative;

    public override void OnApply(CharacterManager_Multi subject)
    {
        if (subject == null)
            return;

        CardDefinition dumpDefinition =
            CreateDumpDefinitionCompat(_magnitude);

        if (dumpDefinition == null)
        {
            Debug.LogError(
                "[AddDumpEffect_Multi] CardDefinition.Create 호환 오버로드를 찾지 못했습니다.");
            return;
        }

        dump = new CardInstance_Multi(
            dumpDefinition,
            subject);

        subject.ReturnToDeck(dump);
        PlayApplySound();
    }

    // 프로젝트의 CardDefinition 버전에 따라 Create가 4/5 인자 등으로 다를 수 있어
    // Single 파일을 수정하지 않고 현재 프로젝트 API에 맞춰 호출한다.
    private static CardDefinition CreateDumpDefinitionCompat(int cooldown)
    {
        MethodInfo[] methods = typeof(CardDefinition).GetMethods(
            BindingFlags.Public | BindingFlags.Static);

        foreach (MethodInfo method in methods)
        {
            if (method.Name != "Create")
                continue;

            ParameterInfo[] parameters = method.GetParameters();

            if (parameters.Length < 3)
                continue;

            if (parameters[0].ParameterType != typeof(int) ||
                parameters[1].ParameterType != typeof(int) ||
                !parameters[2].ParameterType.IsAssignableFrom(typeof(CardEffect[])))
            {
                continue;
            }

            object[] args = new object[parameters.Length];
            args[0] = cooldown;
            args[1] = 1;
            args[2] = Array.Empty<CardEffect>();

            for (int i = 3; i < args.Length; i++)
                args[i] = null;

            try
            {
                return method.Invoke(null, args) as CardDefinition;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[AddDumpEffect_Multi] CardDefinition.Create 호출 실패: {ex.Message}");
            }
        }

        return null;
    }
}
