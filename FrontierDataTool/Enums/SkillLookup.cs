using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace FrontierDataTool.Enums
{
    /// <summary>
    /// Provides lookup dictionaries for skill enums.
    /// </summary>
    public static class SkillLookup
    {
        /// <summary>
        /// Dictionary mapping SkillTree enum values to English display names.
        /// </summary>
        public static readonly FrozenDictionary<SkillTree, string> SkillTreeNames = new Dictionary<SkillTree, string>
        {
            [SkillTree.None] = "None",
            [SkillTree.Passive] = "Passive",
            [SkillTree.Fate] = "Fate",
            [SkillTree.Backpacking] = "Backpacking",
            [SkillTree.AutoGuard] = "Auto-Guard",
            [SkillTree.Guard] = "Guard",
            [SkillTree.Recover] = "Recover",
            [SkillTree.RecoverSpeed] = "Recover Speed",
            [SkillTree.ClustSAdd] = "Clust S.Add",
            [SkillTree.Protection] = "Protection",
            [SkillTree.ThunderRes] = "Thunder Res",
            [SkillTree.PierceSUp] = "Pierce S.Up",
            [SkillTree.PierceSAdd] = "Pierce S.Add",
            [SkillTree.Stun] = "Stun",
            [SkillTree.Whim] = "Whim",
            [SkillTree.Sharpness] = "Sharpness",
            [SkillTree.Gluttony] = "Gluttony",
            [SkillTree.Stealth] = "Stealth",
            [SkillTree.Expert] = "Expert",
            [SkillTree.WideAreaRecovery] = "Wide-Area Recovery",
            [SkillTree.WideAreaDetox] = "Wide-Area Antidote",
            [SkillTree.Attack] = "Attack",
            [SkillTree.Gather] = "Gather",
            [SkillTree.PelletSUp] = "Pellet S.Up",
            [SkillTree.PelletSAdd] = "Pellet S.Add",
            [SkillTree.Sleep] = "Sleep",
            [SkillTree.AllResUp] = "All Res Up",
            [SkillTree.Psychic] = "Psychic",
            [SkillTree.Reload] = "Reload",
            [SkillTree.ColdRes] = "Cold Res",
            [SkillTree.HeatRes] = "Heat Res",
            [SkillTree.Health] = "Health",
            [SkillTree.Artisan] = "Artisan",
            [SkillTree.SeedWideArea] = "Wide-Area Seeds",
            [SkillTree.AmmoCombiner] = "Ammo Combiner",
            [SkillTree.Map] = "Map",
            [SkillTree.Hearing] = "Hearing",
            [SkillTree.Combining] = "Combining",
            [SkillTree.NormalSUp] = "Normal S.Up",
            [SkillTree.NormalSAdd] = "Normal S.Add",
            [SkillTree.Fish] = "Fish",
            [SkillTree.Throwing] = "Throwing",
            [SkillTree.Sharpening] = "Sharpening",
            [SkillTree.Poison] = "Poison",
            [SkillTree.StatusAttack] = "Status Attack",
            [SkillTree.Meat] = "Meat",
            [SkillTree.AntiTheft] = "Anti-Theft",
            [SkillTree.BombBoost] = "Bomb Boost",
            [SkillTree.Hunger] = "Hunger",
            [SkillTree.Recoil] = "Recoil",
            [SkillTree.FireRes] = "Fire Res",
            [SkillTree.WindPressure] = "Wind Pressure",
            [SkillTree.Horn] = "Horn",
            [SkillTree.Defense] = "Defense",
            [SkillTree.Paralysis] = "Paralysis",
            [SkillTree.WaterRes] = "Water Res",
            [SkillTree.DragonRes] = "Dragon Res",
            [SkillTree.CragSAdd] = "Crag S.Add",
            [SkillTree.Alchemy] = "Alchemy",
            [SkillTree.AutoReload] = "Auto-Reload",
            [SkillTree.GatherSpeed] = "Gather Speed",
            [SkillTree.Evasion] = "Evasion",
            [SkillTree.Adrenaline] = "Adrenaline",
            [SkillTree.Everlasting] = "Everlasting",
            [SkillTree.Stamina] = "Stamina",
            [SkillTree.Loading] = "Loading",
            [SkillTree.Precision] = "Precision",
            [SkillTree.Monster] = "Monster",
            [SkillTree.Eating] = "Eating",
            [SkillTree.Carving] = "Carving",
            [SkillTree.Terrain] = "Terrain",
            [SkillTree.Deodorant] = "Deodorant",
            [SkillTree.SnowballRes] = "Snowball Res",
            [SkillTree.IceRes] = "Ice Res",
            [SkillTree.QuakeRes] = "Quake Res",
            [SkillTree.WideArea] = "Wide-Area",
            [SkillTree.VocalChords] = "Vocal Chords",
            [SkillTree.Cooking] = "Cooking",
            [SkillTree.Gunnery] = "Gunnery",
            [SkillTree.FluteExpert] = "Flute Expert",
            [SkillTree.Breakout] = "Breakout",
            [SkillTree.Taijutsu] = "Taijutsu",
            [SkillTree.StrongArm] = "Strong Arm",
            [SkillTree.Inspiration] = "Inspiration",
            [SkillTree.Passive2] = "Passive",
            [SkillTree.Bond] = "Bond",
            [SkillTree.Guts] = "Guts",
            [SkillTree.Pressure] = "Pressure",
            [SkillTree.CapturePro] = "Capture Pro.",
            [SkillTree.PoisonCAdd] = "Poison C.Add",
            [SkillTree.ParaCAdd] = "Para C.Add",
            [SkillTree.SleepCAdd] = "Sleep C.Add",
            [SkillTree.FireAttack] = "Fire Attack",
            [SkillTree.WaterAttack] = "Water Attack",
            [SkillTree.ThunderAttack] = "Thunder Attack",
            [SkillTree.IceAttack] = "Ice Attack",
            [SkillTree.DragonAttack] = "Dragon Attack",
            [SkillTree.Fasting] = "Fasting",
            [SkillTree.BombSword] = "Bomb Sword",
            [SkillTree.StrongSword] = "Strong Attack Sword",
            [SkillTree.PoisonSword] = "Poison Sword",
            [SkillTree.ParaSword] = "Para Sword",
            [SkillTree.SleepSword] = "Sleep Sword",
            [SkillTree.FireSword] = "Fire Sword",
            [SkillTree.WaterSword] = "Water Sword",
            [SkillTree.ThunderSword] = "Thunder Sword",
            [SkillTree.IceSword] = "Ice Sword",
            [SkillTree.DragonSword] = "Dragon Sword",
            [SkillTree.Focus] = "Focus",
            [SkillTree.SnSTech] = "SnS Tech",
            [SkillTree.DSTech] = "DS Tech",
            [SkillTree.GSTech] = "GS Tech",
            [SkillTree.LSTech] = "LS Tech",
            [SkillTree.HammerTech] = "Hammer Tech",
            [SkillTree.HHTech] = "HH Tech",
            [SkillTree.LanceTech] = "Lance Tech",
            [SkillTree.GLTech] = "GL Tech",
            [SkillTree.HBGTech] = "HBG Tech",
            [SkillTree.LBGTech] = "LBG Tech",
            [SkillTree.BowTech] = "Bow Tech",
            [SkillTree.SpeedSetup] = "Speed Setup",
            [SkillTree.WpnHandling] = "Wpn Handling",
            [SkillTree.ElementAttack] = "Element Attack",
            [SkillTree.StaminaRecov] = "Stamina Recov",
            [SkillTree.KnifeThrowing] = "Knife Throwing",
            [SkillTree.Caring] = "Caring",
            [SkillTree.DefLock] = "Def Lock",
            [SkillTree.Fencing] = "Fencing",
            [SkillTree.StatusRes] = "Status Res",
            [SkillTree.Sobriety] = "Sobriety",
            [SkillTree.CrystalRes] = "Crystal Res",
            [SkillTree.MagneticRes] = "Magnetic Res",
            [SkillTree.LightTread] = "Light Tread",
            [SkillTree.Relief] = "Relief",
            [SkillTree.Shiriagari] = "Shiriagari",
            [SkillTree.LoneWolf] = "Lone Wolf",
            [SkillTree.ThreeWorlds] = "Three Worlds",
            [SkillTree.Reflect] = "Reflect",
            [SkillTree.Compensation] = "Compensation",
            [SkillTree.Edgemaster] = "Edgemaster",
            [SkillTree.RapidFire] = "Rapid Fire",
            [SkillTree.StrongAttack] = "Strong Attack",
            [SkillTree.Encourage] = "Encourage",
            [SkillTree.Grace] = "Grace",
            [SkillTree.Vitality] = "Vitality",
            [SkillTree.Rage] = "Rage",
            [SkillTree.IronArm] = "Iron Arm",
            [SkillTree.Breeder] = "Breeder",
            [SkillTree.Aiuchi] = "Mutual Strike",
            [SkillTree.Issen] = "Issen",
            [SkillTree.Survivor] = "Survivor",
            [SkillTree.SteadyHand] = "Steady Hand",
            [SkillTree.Mounting] = "Mounting",
            [SkillTree.Tenderizer] = "Tenderizer",
            [SkillTree.ComboExpert] = "Combo Expert",
            [SkillTree.Hunter] = "Hunter",
            [SkillTree.CriticalShot] = "Critical Shot",
            [SkillTree.RapidAttackDeleted] = "Continuous Strike (Deleted)",
            [SkillTree.EvadeDistance] = "Evade Distance",
            [SkillTree.ChargeAtkUp] = "Charge Atk Up",
            [SkillTree.BulletSaver] = "Bullet Saver",
            [SkillTree.MovementSpeed] = "Movement Speed",
            [SkillTree.Reinforcement] = "Reinforcement",
            [SkillTree.Vampirism] = "Vampirism",
            [SkillTree.Adaptation] = "Adaptation",
            [SkillTree.DarkPulse] = "Dark Pulse",
            [SkillTree.HerbalScience] = "Herbal Science",
            [SkillTree.TonfaTech] = "Tonfa Tech",
            [SkillTree.Incitement] = "Incitement",
            [SkillTree.BlazingGrace] = "Blazing Grace",
            [SkillTree.DrugKnowledge] = "Drug Knowledge",
            [SkillTree.AbsoluteDef] = "Absolute Def.",
            [SkillTree.Mindfulness] = "Mindfulness",
            [SkillTree.GatheringMastery] = "Gathering Mastery",
            [SkillTree.Stylish] = "Stylish",
            [SkillTree.Assistance] = "Assistance",
            [SkillTree.GentleShot] = "Gentle Shot",
            [SkillTree.Dissolver] = "Dissolver",
            [SkillTree.CombatSupremacy] = "Combat Supremacy",
            [SkillTree.Vigorous] = "Vigorous",
            [SkillTree.SwordGod] = "Sword God",
            [SkillTree.ThunderClad] = "Thunder Clad",
            [SkillTree.StatusAssault] = "Status Assault",
            [SkillTree.DrawingArts] = "Drawing Arts",
            [SkillTree.BlastRes] = "Blast Res",
            [SkillTree.CritConversion] = "Crit Conversion",
            [SkillTree.Determination] = "Determination",
            [SkillTree.StylishAssault] = "Stylish Assault",
            [SkillTree.FreezeRes] = "Freeze Res",
            [SkillTree.IceAge] = "Ice Age",
            [SkillTree.LavishAttack] = "Lavish Attack",
            [SkillTree.SAFTech] = "AS F Tech",
            [SkillTree.Fortification] = "Fortification",
            [SkillTree.Sniper] = "Sniper",
            [SkillTree.Obscurity] = "Obscurity",
            [SkillTree.EvasionBoost] = "Evasion Boost",
            [SkillTree.Rush] = "Rush",
            [SkillTree.Skilled] = "Skilled",
            [SkillTree.Ceaseless] = "Ceaseless",
            [SkillTree.BreakingPoint] = "Breaking Point",
            [SkillTree.Abnormality] = "Abnormality",
            [SkillTree.Spacing] = "Spacing",
            [SkillTree.Trained] = "Trained",
            [SkillTree.Furious] = "Furious",
            [SkillTree.MSTech] = "MS Tech"
        }.ToFrozenDictionary();

        /// <summary>
        /// Reverse lookup: English name to SkillTree enum.
        /// </summary>
        public static readonly FrozenDictionary<string, SkillTree> SkillTreeByName =
            SkillTreeNames.ToFrozenDictionary(kvp => kvp.Value, kvp => kvp.Key);

        /// <summary>
        /// Get English name for a skill tree ID byte.
        /// </summary>
        public static string GetSkillTreeName(byte id)
        {
            if (Enum.IsDefined(typeof(SkillTree), id) && SkillTreeNames.TryGetValue((SkillTree)id, out var name))
                return name;
            return $"Unknown_{id:X2}";
        }

        /// <summary>
        /// Get skill tree ID from English name.
        /// </summary>
        public static byte GetSkillTreeId(string name)
        {
            if (string.IsNullOrEmpty(name))
                return 0;
            return SkillTreeByName.TryGetValue(name, out var skill) ? (byte)skill : (byte)0;
        }

        /// <summary>
        /// Dictionary mapping ZenithSkill enum values to English display names.
        /// </summary>
        public static readonly FrozenDictionary<ZenithSkill, string> ZenithSkillNames = new Dictionary<ZenithSkill, string>
        {
            [ZenithSkill.None] = "None",
            [ZenithSkill.SkillSlotUpPlus1] = "Skill Slot UP +1",
            [ZenithSkill.SkillSlotUpPlus2] = "Skill Slot UP +2",
            [ZenithSkill.SkillSlotUpPlus3] = "Skill Slot UP +3",
            [ZenithSkill.SkillSlotUpPlus4] = "Skill Slot UP +4",
            [ZenithSkill.SkillSlotUpPlus5] = "Skill Slot UP +5",
            [ZenithSkill.SkillSlotUpPlus6] = "Skill Slot UP +6",
            [ZenithSkill.SkillSlotUpPlus7] = "Skill Slot UP +7",
            [ZenithSkill.CritConvUpPlus1] = "Crit Conv UP +1",
            [ZenithSkill.CritConvUpPlus2] = "Crit Conv UP +2",
            [ZenithSkill.StylishAssaultUpPlus1] = "Stylish Assault UP +1",
            [ZenithSkill.StylishAssaultUpPlus2] = "Stylish Assault UP +2",
            [ZenithSkill.DissolverUp] = "Dissolver UP",
            [ZenithSkill.ThunderCladUpPlus1] = "ThunderClad UP +1",
            [ZenithSkill.ThunderCladUpPlus2] = "ThunderClad UP +2",
            [ZenithSkill.IceAgeUp] = "Ice Age UP",
            [ZenithSkill.EarplugUpPlus1] = "Earplug UP +1",
            [ZenithSkill.EarplugUpPlus2] = "Earplug UP +2",
            [ZenithSkill.EarplugUpPlus3] = "Earplug UP +3",
            [ZenithSkill.WindResUpPlus1] = "Wind Res UP +1",
            [ZenithSkill.WindResUpPlus2] = "Wind Res UP +2",
            [ZenithSkill.WindResUpPlus3] = "Wind Res UP +3",
            [ZenithSkill.WindResUpPlus4] = "Wind Res UP +4",
            [ZenithSkill.QuakeResUpPlus1] = "Quake Res UP +1",
            [ZenithSkill.QuakeResUpPlus2] = "Quake Res UP +2",
            [ZenithSkill.PoisonResUpPlus1] = "Poison Res UP +1",
            [ZenithSkill.PoisonResUpPlus2] = "Poison Res UP +2",
            [ZenithSkill.ParaResUpPlus1] = "Para Res UP +1",
            [ZenithSkill.ParaResUpPlus2] = "Para Res UP +2",
            [ZenithSkill.SleepResUpPlus1] = "Sleep Res UP +1",
            [ZenithSkill.SleepResUpPlus2] = "Sleep Res UP +2",
            [ZenithSkill.VampirismUpPlus1] = "Vampirism UP +1",
            [ZenithSkill.VampirismUpPlus2] = "Vampirism UP +2",
            [ZenithSkill.DrugKnowledgeUp] = "Drug Knowledge UP",
            [ZenithSkill.AssistanceUp] = "Assistance UP",
            [ZenithSkill.BulletSaverUpPlus1] = "Bullet Saver UP +1",
            [ZenithSkill.BulletSaverUpPlus2] = "Bullet Saver UP +2",
            [ZenithSkill.GuardUpPlus1] = "Guard UP +1",
            [ZenithSkill.GuardUpPlus2] = "Guard UP +2",
            [ZenithSkill.AdaptationUpPlus1] = "Adaptation UP +1",
            [ZenithSkill.AdaptationUpPlus2] = "Adaptation UP +2",
            [ZenithSkill.EncourageUpPlus1] = "Encourage UP +1",
            [ZenithSkill.EncourageUpPlus2] = "Encourage UP +2",
            [ZenithSkill.ReflectUpPlus1] = "Reflect UP +1",
            [ZenithSkill.ReflectUpPlus2] = "Reflect UP +2",
            [ZenithSkill.ReflectUpPlus3] = "Reflect UP +3",
            [ZenithSkill.StylishUp] = "Stylish UP",
            [ZenithSkill.VigorousUp] = "Vigorous UP",
            [ZenithSkill.ObscurityUp] = "Obscurity UP",
            [ZenithSkill.SoulUp] = "Soul UP",
            [ZenithSkill.CeaselessUp] = "Ceaseless UP",
            [ZenithSkill.RushUp] = "Rush UP"
        }.ToFrozenDictionary();

        /// <summary>
        /// Get English name for a zenith skill ID.
        /// </summary>
        public static string GetZenithSkillName(ushort id)
        {
            if (Enum.IsDefined(typeof(ZenithSkill), id) && ZenithSkillNames.TryGetValue((ZenithSkill)id, out var name))
                return name;
            return $"Unknown_{id:X4}";
        }
    }
}
