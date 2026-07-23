using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 玩家能力管理器。
/// 持有已习得的类银河城能力标记位，供其他系统查询。
/// 每次击败区域Boss后调用 UnlockAbility。
/// </summary>
public class PlayerAbilities : MonoBehaviour
{
    private HashSet<AbilityType> unlockedAbilities = new HashSet<AbilityType>();

    [Header("Ability Icons")]
    [SerializeField] private Sprite airDashIcon;
    [SerializeField] private Sprite inscriptionReadIcon;
    [SerializeField] private Sprite shockwaveIcon;
    [SerializeField] private Sprite slideIcon;
    [SerializeField] private Sprite illusionSummonIcon;

    public event Action<AbilityType> OnAbilityUnlocked;

    public bool HasAbility(AbilityType type) => unlockedAbilities.Contains(type);

    public void UnlockAbility(AbilityType type)
    {
        if (unlockedAbilities.Add(type))
        {
            OnAbilityUnlocked?.Invoke(type);
            Debug.Log($"[Abilities] 习得能力: {type}");
        }
    }

    public Sprite GetAbilityIcon(AbilityType type)
    {
        return type switch
        {
            AbilityType.AirDash => airDashIcon,
            AbilityType.InscriptionRead => inscriptionReadIcon,
            AbilityType.Shockwave => shockwaveIcon,
            AbilityType.Slide => slideIcon,
            AbilityType.IllusionSummon => illusionSummonIcon,
            _ => null,
        };
    }

    public List<AbilityType> GetAllUnlocked()
    {
        return new List<AbilityType>(unlockedAbilities);
    }

    public void LoadFromSaveData(SaveData data)
    {
        if (data.hasAirDash) UnlockAbility(AbilityType.AirDash);
        if (data.hasInscriptionRead) UnlockAbility(AbilityType.InscriptionRead);
        if (data.hasShockwave) UnlockAbility(AbilityType.Shockwave);
        if (data.hasSlide) UnlockAbility(AbilityType.Slide);
        if (data.hasIllusionSummon) UnlockAbility(AbilityType.IllusionSummon);
        if (data.hasUnityState) UnlockAbility(AbilityType.UnityState);
        if (data.hasHeartOfEmperor) UnlockAbility(AbilityType.HeartOfEmperor);
    }
}

public enum AbilityType
{
    AirDash,
    InscriptionRead,
    Shockwave,
    Slide,
    IllusionSummon,
    UnityState,
    HeartOfEmperor
}
