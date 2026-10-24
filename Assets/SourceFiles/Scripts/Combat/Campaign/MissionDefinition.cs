using System;
using UnityEngine;

namespace Combat
{
    public enum ObjectiveType
    {
        // use a device (MissionDevice with this id)
        Interact,
        // carry Count items with this id
        Collect,
        // kill Count enemies of a kind: any, zombie, soldier, special, Runner/Tank/Screamer/Crawler, boss
        Kill,
        // stay inside the zone for Duration seconds (time stops while away; too long away fails)
        Defend,
        // stay alive for Duration seconds
        Survive,
        // use the devices Target_1..Target_Count in that order
        Sequence,
        // the power sector at the anchor is running
        RestorePower,
        // the door at the anchor is open (Count 1) or closed (Count 0)
        Door,
        // reach the zone
        Investigate,
        // pick up Count documents/equipment with this id (same as Collect, read as "recover")
        Retrieve,
        // reach the extraction zone
        Extract,
        // a wave of enemies is launched and must be cleared
        SurviveWave,
        // build Count barricades on points whose id starts with Target
        Fortify,
        // be holding a gun
        Armed
    }

    [Serializable]
    public class ObjectiveDefinition
    {
        [TextArea] public string Text = "Objetivo";
        public ObjectiveType Type;
        [Tooltip("Anchor, device or item id; enemy kind for Kill")]
        public string Target;
        public int Count = 1;
        [Tooltip("Seconds for Defend and Survive")]
        public float Duration;
        [Tooltip("Zone radius for Defend, Investigate and Extract")]
        public float Radius = 5.0f;
        [Tooltip("Bonus objective: never blocks the mission, pays extra")]
        public bool Optional;
        public int BonusCredits;
        [Tooltip("Enemies sent at the objective when it starts (noise, defence)")]
        public int PressureEnemies;
        [Tooltip("Radio line shown when the objective starts")]
        public string Radio;
        public bool ShowMarker = true;
        [Tooltip("After this long without finishing, the objective is waived so the run can't get stuck (0 = default)")]
        public float SkipAfter;
    }

    public enum MissionEffectType
    {
        RestorePower,
        CutPower,
        UnlockDoor,
        LockDoor,
        // no enemies come through this gate for Value seconds (0 = rest of the run)
        BlockSpawns,
        // switch on the floodlights at the anchor
        Floodlights,
        StartAlarm,
        StopAlarm,
        SetFlag,
        // quick items: Target = Medkit/Grenade/Molotov/Flare/Barricade/Decoy, Value = amount
        GiveItem,
        // full reserves on both guns
        GiveAmmo,
        // a weapon pickup at the anchor, Target2 = weapon name
        SpawnWeapon,
        Radio,
        Document,
        // Value enemies come for the anchor
        SpawnEnemies,
        // the shop gets cheaper (Value = discount fraction)
        ShopDiscount,
        // ammo, medkits and throwables dropped at the anchor
        SpawnSupplies,
        // children named "Reveal" under the anchor appear (closed gate bars, lit panels...)
        Reveal,
        // no power failures for Value seconds
        StablePower
    }

    [Serializable]
    public class MissionEffect
    {
        public MissionEffectType Type;
        public string Target;
        public string Target2;
        public float Value;
    }

    /* One mission as data: what to do (objectives, in order, optional ones alongside),
     * what happens when it starts, succeeds or fails, and what it pays. Scene objects are
     * referred to by id (MissionAnchor, MissionDevice, MissionPickup), so the asset works with
     * whatever the level provides.
     */
    [CreateAssetMenu(menuName = "Combat/Mission", fileName = "Mission_")]
    public class MissionDefinition : ScriptableObject
    {
        public string Id = "mission";
        public string Title = "Missão";
        [TextArea(2, 6)] public string Briefing;
        [Tooltip("Optional missions can be abandoned and are offered between waves")]
        public bool Optional;
        [Tooltip("Missions sharing a group are mutually exclusive: starting one drops the others")]
        public string ChoiceGroup;
        [Tooltip("What choosing this one means, shown on the choice screen")]
        [TextArea] public string ChoiceSummary;
        [Tooltip("Fails when the time runs out (0 = no limit)")]
        public float TimeLimit;
        [Tooltip("Can be offered again after failing")]
        public bool Retryable = true;

        public ObjectiveDefinition[] Objectives;

        [Header("Effects")]
        public MissionEffect[] OnStart;
        public MissionEffect[] OnComplete;
        public MissionEffect[] OnFail;

        [Header("Rewards (paid once)")]
        public int Credits = 150;
        public int Experience = 100;
        public int Score = 200;

        [Header("Requirements")]
        [Tooltip("Anchors/devices that must exist in the level for this mission to be offered")]
        public string[] RequiredIds;
        [Tooltip("Run flag needed before it is offered")]
        public string RequiredFlag;
    }
}
