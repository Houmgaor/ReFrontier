using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using CsvHelper;
using CsvHelper.Configuration;

using FrontierDataTool.Structs;
using LibReFrontier;

namespace FrontierDataTool
{
    public class Program
    {
        // Entry sizes for data structures (in bytes)
        private const int ARMOR_ENTRY_SIZE = 0x48;
        private const int MELEE_WEAPON_ENTRY_SIZE = 0x34;
        private const int RANGED_WEAPON_ENTRY_SIZE = 0x3C;
        private const int ITEM_ENTRY_SIZE = 0x24;
        private const int PEARL_ENTRY_SIZE = 0x30;
        private const int SHOP_ENTRY_SIZE = 8;

        // Define offset pointers

        // --- mhfdat.bin ---
        // Strings
        // Start offset armor strings
        private static readonly int _soStringHead = 0x64, _soStringBody = 0x68, _soStringArm = 0x6C, _soStringWaist = 0x70, _soStringLeg = 0x74;

        // End offsets armor strings
        private static readonly int _eoStringHead = 0x60, _eoStringBody = 0x64, _eoStringArm = 0x68, _eoStringWaist = 0x6C, _eoStringLeg = 0x70;

        // Start offsets weapons string
        private static readonly int _soStringRanged = 0x84, _soStringMelee = 0x88;

        // End offsets weapons strings
        // static readonly int eoStringRanged = 0x88, eoStringMelee = 0x174;

        // Start offsets items names, descriptions
        private static readonly int _soStringItem = 0x100, _soStringItemDesc = 0x12C;

        // End offsets items names, descriptions
        private static readonly int _eoStringItem = 0xFC, _eoStringItemDesc = 0x100;

        // Armor
        // Start offsets armors data
        private static readonly int _soHead = 0x50, _soBody = 0x54, _soArm = 0x58, _soWaist = 0x5C, _soLeg = 0x60;

        // End offsets armors data
        private static readonly int _eoHead = 0xE8, _eoBody = 0x50, _eoArm = 0x54, _eoWaist = 0x58, _eoLeg = 0x5C;

        // Weapons
        // Start offsets weapons data
        private static readonly int _soRanged = 0x80, _soMelee = 0x7C;

        // End offsets weapons data
        private static readonly int _eoRanged = 0x7C, _eoMelee = 0x90;


        // --- mhfpac.bin ---
        // Strings
        private static readonly int _soStringSkillPt = 0xA20, _soStringSkillActivate = 0xA1C, _soStringZSkill = 0xFBC, _soStringSkillDesc = 0xb8;
        private static readonly int _eoStringSkillPt = 0xA1C, _eoStringSkillActivate = 0xBC0, _eoStringZSkill = 0xFB0, _eoStringSkillDesc = 0xc0;

        /// <summary>
        /// Pointers for armors data
        /// </summary>
        public static List<KeyValuePair<int, int>> _dataPointersArmor =
        [
            new KeyValuePair<int, int>(_soHead, _eoHead),
            new KeyValuePair<int, int>(_soBody, _eoBody),
            new KeyValuePair<int, int>(_soArm, _eoArm),
            new KeyValuePair<int, int>(_soWaist, _eoWaist),
            new KeyValuePair<int, int>(_soLeg, _eoLeg)
        ];

        // Unused: public enum EqType { 通常 = 0, ＳＰ = 1, 剛種 = 2, 進化 = 4, ＨＣ = 8 };

        /// <summary>
        /// Get weapon and armor data from game files.
        /// </summary>
        /// <param name="args">Input argument from console.</param>
        /// <returns>Exit code (0 for success).</returns>
        private static int Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Root command
            var rootCommand = new RootCommand("FrontierDataTool - Extract and edit Monster Hunter Frontier game data");

            // Action options
            var dumpOption = new Option<bool>(
                name: "--dump",
                description: "Extract weapon/armor/skill/quest data (requires --suffix, --mhfpac, --mhfdat, --mhfinf)"
            );
            rootCommand.AddOption(dumpOption);

            var modshopOption = new Option<bool>(
                name: "--modshop",
                description: "Modify shop prices (requires --mhfdat)"
            );
            rootCommand.AddOption(modshopOption);

            // Parameter options
            var suffixOption = new Option<string>(
                name: "--suffix",
                description: "Output suffix for files"
            );
            rootCommand.AddOption(suffixOption);

            var mhfpacOption = new Option<string>(
                name: "--mhfpac",
                description: "Path to mhfpac.bin"
            );
            rootCommand.AddOption(mhfpacOption);

            var mhfdatOption = new Option<string>(
                name: "--mhfdat",
                description: "Path to mhfdat.bin"
            );
            rootCommand.AddOption(mhfdatOption);

            var mhfinfOption = new Option<string>(
                name: "--mhfinf",
                description: "Path to mhfinf.bin"
            );
            rootCommand.AddOption(mhfinfOption);

            var closeOption = new Option<bool>(
                name: "--close",
                description: "Close terminal after command"
            );
            rootCommand.AddOption(closeOption);

            // Set handler
            rootCommand.SetHandler((InvocationContext context) =>
            {
                var dump = context.ParseResult.GetValueForOption(dumpOption);
                var modshop = context.ParseResult.GetValueForOption(modshopOption);
                var suffix = context.ParseResult.GetValueForOption(suffixOption);
                var mhfpac = context.ParseResult.GetValueForOption(mhfpacOption);
                var mhfdat = context.ParseResult.GetValueForOption(mhfdatOption);
                var mhfinf = context.ParseResult.GetValueForOption(mhfinfOption);
                var close = context.ParseResult.GetValueForOption(closeOption);

                // Count how many actions are specified
                int actionCount = (dump ? 1 : 0) + (modshop ? 1 : 0);

                if (actionCount == 0)
                {
                    Console.Error.WriteLine("Error: No action specified. Use --dump or --modshop.");
                    context.ExitCode = 1;
                    FinishCommand(close);
                    return;
                }

                if (actionCount > 1)
                {
                    Console.Error.WriteLine("Error: Only one action can be specified at a time.");
                    context.ExitCode = 1;
                    FinishCommand(close);
                    return;
                }

                try
                {
                    if (dump)
                    {
                        // Validate required parameters
                        if (string.IsNullOrEmpty(suffix))
                        {
                            Console.Error.WriteLine("Error: --dump requires --suffix.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }
                        if (string.IsNullOrEmpty(mhfpac))
                        {
                            Console.Error.WriteLine("Error: --dump requires --mhfpac.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }
                        if (string.IsNullOrEmpty(mhfdat))
                        {
                            Console.Error.WriteLine("Error: --dump requires --mhfdat.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }
                        if (string.IsNullOrEmpty(mhfinf))
                        {
                            Console.Error.WriteLine("Error: --dump requires --mhfinf.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }

                        // Validate files exist
                        if (!File.Exists(mhfpac))
                        {
                            Console.Error.WriteLine($"Error: File '{mhfpac}' does not exist.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }
                        if (!File.Exists(mhfdat))
                        {
                            Console.Error.WriteLine($"Error: File '{mhfdat}' does not exist.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }
                        if (!File.Exists(mhfinf))
                        {
                            Console.Error.WriteLine($"Error: File '{mhfinf}' does not exist.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }

                        DumpData(suffix, mhfpac, mhfdat, mhfinf);
                    }
                    else if (modshop)
                    {
                        if (string.IsNullOrEmpty(mhfdat))
                        {
                            Console.Error.WriteLine("Error: --modshop requires --mhfdat.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }

                        if (!File.Exists(mhfdat))
                        {
                            Console.Error.WriteLine($"Error: File '{mhfdat}' does not exist.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }

                        ModShop(mhfdat);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    context.ExitCode = 1;
                }

                FinishCommand(close);
            });

            return rootCommand.Invoke(args);
        }

        /// <summary>
        /// Finish command execution with optional wait and message.
        /// </summary>
        /// <param name="close">If true, don't wait for user input.</param>
        private static void FinishCommand(bool close)
        {
            if (!close)
            {
                Console.WriteLine("Done");
                Console.Read();
            }
        }

        /// <summary>
        /// Dump data and strings
        /// </summary>
        /// <param name="suffix">Output suffix</param>
        /// <param name="mhfpac">Path to mhfpac</param>
        /// <param name="mhfdat">Path to mhfdat</param>
        /// <param name="mhfinf">Path to mhfinf</param>
        private static void DumpData(string suffix, string mhfpac, string mhfdat, string mhfinf)
        {
            var skillId = DumpSkillSystem(mhfpac, suffix);

            DumpSkillData(mhfpac, suffix);

            DumpItemData(mhfdat, suffix);

            DumpEquipementData(mhfdat, suffix, skillId);

            DumpWeaponData(mhfdat);

            DumpQuestData(mhfinf);
        }

        private static List<KeyValuePair<int, string>> DumpSkillSystem(string mhfpac, string suffix)
        {
            #region SkillSystem
            // Get and dump skill system dictionary
            Console.WriteLine("Dumping skill tree names.");
            using MemoryStream msInput = new(File.ReadAllBytes(mhfpac));
            using BinaryReader brInput = new(msInput);
            brInput.BaseStream.Seek(_soStringSkillPt, SeekOrigin.Begin);
            int sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(_eoStringSkillPt, SeekOrigin.Begin);
            int eOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            List<KeyValuePair<int, string>> skillId = [];
            int id = 0;
            while (brInput.BaseStream.Position < eOffset)
            {
                string name = StringFromPointer(brInput);
                skillId.Add(new KeyValuePair<int, string>(id, name));
                id++;
            }

            // Dump skill system data
            string textName = $"mhsx_SkillSys_{suffix}.txt";
            using (StreamWriter file = new(textName, false, Encoding.UTF8))
                foreach (KeyValuePair<int, string> entry in skillId)
                    file.WriteLine("{0}", entry.Value);

            return skillId;
            #endregion
        }

        private static void DumpSkillData(string mhfpac, string suffix)
        {
            using MemoryStream msInput = new(File.ReadAllBytes(mhfpac));
            using BinaryReader brInput = new(msInput);


            #region ActiveSkill
            Console.WriteLine("Dumping active skill names.");
            brInput.BaseStream.Seek(_soStringSkillActivate, SeekOrigin.Begin);
            int sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(_eoStringSkillActivate, SeekOrigin.Begin);
            int eOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            List<string> activeSkill = [];
            while (brInput.BaseStream.Position < eOffset)
            {
                string name = StringFromPointer(brInput);
                activeSkill.Add(name);
            }

            string textName = $"mhsx_SkillActivate_{suffix}.txt";
            using (StreamWriter file = new(textName, false, Encoding.UTF8))
                foreach (string entry in activeSkill)
                    file.WriteLine("{0}", entry);
            #endregion

            #region SkillDescription
            Console.WriteLine("Dumping active skill descriptions.");
            brInput.BaseStream.Seek(_soStringSkillDesc, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(_eoStringSkillDesc, SeekOrigin.Begin);
            eOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            List<string> skillDesc = [];
            while (brInput.BaseStream.Position < eOffset)
            {
                string name = StringFromPointer(brInput);
                skillDesc.Add(name);
            }

            textName = $"mhsx_SkillDesc_{suffix}.txt";
            using (StreamWriter file = new(textName, false, Encoding.UTF8))
                foreach (string entry in skillDesc)
                    file.WriteLine("{0}", entry);
            #endregion

            #region ZSkill
            Console.WriteLine("Dumping Z skill names.");
            brInput.BaseStream.Seek(_soStringZSkill, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(_eoStringZSkill, SeekOrigin.Begin);
            eOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            List<string> zSkill = [];
            while (brInput.BaseStream.Position < eOffset)
            {
                string name = StringFromPointer(brInput);
                zSkill.Add(name);
            }

            textName = $"mhsx_SkillZ_{suffix}.txt";
            using (StreamWriter file = new(textName, false, Encoding.UTF8))
                foreach (string entry in zSkill)
                    file.WriteLine("{0}", entry);
            #endregion
        }

        private static void DumpItemData(string mhfdat, string suffix)
        {
            #region Items
            Console.WriteLine("Dumping item names.");
            using var msInput = new MemoryStream(File.ReadAllBytes(mhfdat));
            using var brInput = new BinaryReader(msInput);
            brInput.BaseStream.Seek(_soStringItem, SeekOrigin.Begin);
            int sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(_eoStringItem, SeekOrigin.Begin);
            int eOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            List<string> items = [];
            while (brInput.BaseStream.Position < eOffset)
            {
                string name = StringFromPointer(brInput);
                items.Add(name);
            }

            string textName = $"mhsx_Items_{suffix}.txt";
            using (StreamWriter file = new(textName, false, Encoding.UTF8))
                foreach (string entry in items)
                    file.WriteLine("{0}", entry);

            Console.WriteLine("Dumping item descriptions.");
            brInput.BaseStream.Seek(_soStringItemDesc, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(_eoStringItemDesc, SeekOrigin.Begin);
            eOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            List<string> itemsDesc = [];
            while (brInput.BaseStream.Position < eOffset)
            {
                string name = StringFromPointer(brInput);
                itemsDesc.Add(name);
            }

            textName = $"Items_Desc_{suffix}.txt";
            using (StreamWriter file = new(textName, false, Encoding.UTF8))
                foreach (string entry in itemsDesc)
                    file.WriteLine("{0}", entry);
            #endregion
        }

        private static void DumpEquipementData(
            string mhfdat, string suffix, List<KeyValuePair<int, string>> skillId
        )
        {
            string[] aClassIds = ["頭", "胴", "腕", "腰", "脚"];

            // Pointers for armor
            List<KeyValuePair<int, int>> stringPointersArmor =
            [
                new KeyValuePair<int, int>(_soStringHead, _eoStringHead),
                new KeyValuePair<int, int>(_soStringBody, _eoStringBody),
                new KeyValuePair<int, int>(_soStringArm, _eoStringArm),
                new KeyValuePair<int, int>(_soStringWaist, _eoStringWaist),
                new KeyValuePair<int, int>(_soStringLeg, _eoStringLeg)
            ];

            #region EquipmentData
            // Dump armor data
            int totalCount = 0;
            int sOffset, eOffset;
            using var msInput = new MemoryStream(File.ReadAllBytes(mhfdat));
            using var brInput = new BinaryReader(msInput);
            for (int i = 0; i < 5; i++)
            {
                // Get raw data
                brInput.BaseStream.Seek(_dataPointersArmor[i].Key, SeekOrigin.Begin);
                sOffset = brInput.ReadInt32();
                brInput.BaseStream.Seek(_dataPointersArmor[i].Value, SeekOrigin.Begin);
                eOffset = brInput.ReadInt32();

                int entryCount = (eOffset - sOffset) / ARMOR_ENTRY_SIZE;
                totalCount += entryCount;
            }
            Console.WriteLine($"Total armor count: {totalCount}");

            ArmorDataEntry[] armorEntries = new ArmorDataEntry[totalCount];
            int currentCount = 0;
            for (int i = 0; i < 5; i++)
            {
                // Get raw data
                brInput.BaseStream.Seek(_dataPointersArmor[i].Key, SeekOrigin.Begin);
                sOffset = brInput.ReadInt32();
                brInput.BaseStream.Seek(_dataPointersArmor[i].Value, SeekOrigin.Begin);
                eOffset = brInput.ReadInt32();

                int entryCount = (eOffset - sOffset) / ARMOR_ENTRY_SIZE;
                brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
                Console.WriteLine($"{aClassIds[i]} count: {entryCount}");

                for (int j = 0; j < entryCount; j++)
                {
                    ArmorDataEntry entry = new()
                    {
                        EquipClass = aClassIds[i],
                        ModelIdMale = brInput.ReadInt16(),
                        ModelIdFemale = brInput.ReadInt16()
                    };
                    byte bitfield = brInput.ReadByte();
                    entry.IsMaleEquip = (bitfield & (1 << 1 - 1)) != 0; // brInput.ReadBoolean();
                    entry.IsFemaleEquip = (bitfield & (1 << 2 - 1)) != 0;
                    entry.IsBladeEquip = (bitfield & (1 << 3 - 1)) != 0;
                    entry.IsGunnerEquip = (bitfield & (1 << 4 - 1)) != 0;
                    entry.Bool1 = (bitfield & (1 << 5 - 1)) != 0;
                    entry.IsSPEquip = (bitfield & (1 << 6 - 1)) != 0;
                    entry.Bool3 = (bitfield & (1 << 7 - 1)) != 0;
                    entry.Bool4 = (bitfield & (1 << 8 - 1)) != 0;
                    entry.Rarity = brInput.ReadByte();
                    entry.MaxLevel = brInput.ReadByte();
                    entry.Unk1_1 = brInput.ReadByte();
                    entry.Unk1_2 = brInput.ReadByte();
                    entry.Unk1_3 = brInput.ReadByte();
                    entry.Unk1_4 = brInput.ReadByte();
                    entry.Unk2 = brInput.ReadByte();
                    entry.ZennyCost = brInput.ReadInt32();
                    entry.Unk3 = brInput.ReadInt16();
                    entry.BaseDefense = brInput.ReadInt16();
                    entry.FireRes = brInput.ReadSByte();
                    entry.WaterRes = brInput.ReadSByte();
                    entry.ThunderRes = brInput.ReadSByte();
                    entry.DragonRes = brInput.ReadSByte();
                    entry.IceRes = brInput.ReadSByte();
                    entry.Unk3_1 = brInput.ReadInt16();
                    entry.BaseSlots = brInput.ReadByte();
                    entry.MaxSlots = brInput.ReadByte();
                    entry.SthEventCrown = brInput.ReadByte();
                    entry.Unk5 = brInput.ReadByte();
                    entry.Unk6 = brInput.ReadByte();
                    entry.Unk7_1 = brInput.ReadByte();
                    entry.Unk7_2 = brInput.ReadByte();
                    entry.Unk7_3 = brInput.ReadByte();
                    entry.Unk7_4 = brInput.ReadByte();
                    entry.Unk8_1 = brInput.ReadByte();
                    entry.Unk8_2 = brInput.ReadByte();
                    entry.Unk8_3 = brInput.ReadByte();
                    entry.Unk8_4 = brInput.ReadByte();
                    entry.Unk10 = brInput.ReadInt16();
                    entry.SkillId1 = skillId[brInput.ReadByte()].Value;
                    entry.SkillPts1 = brInput.ReadSByte();
                    entry.SkillId2 = skillId[brInput.ReadByte()].Value;
                    entry.SkillPts2 = brInput.ReadSByte();
                    entry.SkillId3 = skillId[brInput.ReadByte()].Value;
                    entry.SkillPts3 = brInput.ReadSByte();
                    entry.SkillId4 = skillId[brInput.ReadByte()].Value;
                    entry.SkillPts4 = brInput.ReadSByte();
                    entry.SkillId5 = skillId[brInput.ReadByte()].Value;
                    entry.SkillPts5 = brInput.ReadSByte();
                    entry.SthHiden = brInput.ReadInt32();
                    entry.Unk12 = brInput.ReadInt32();
                    entry.Unk13 = brInput.ReadByte();
                    entry.Unk14 = brInput.ReadByte();
                    entry.Unk15 = brInput.ReadByte();
                    entry.Unk16 = brInput.ReadByte();
                    entry.Unk17 = brInput.ReadInt32();
                    entry.Unk18 = brInput.ReadInt16();
                    entry.Unk19 = brInput.ReadInt16();

                    armorEntries[j + currentCount] = entry;
                }

                // Get strings
                brInput.BaseStream.Seek(stringPointersArmor[i].Key, SeekOrigin.Begin);
                sOffset = brInput.ReadInt32();

                brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
                for (int j = 0; j < entryCount - 1; j++)
                {
                    string name = StringFromPointer(brInput);
                    armorEntries[j + currentCount].Name = name;
                }
                currentCount += entryCount;
            }

            // Write armor csv
            using (var textWriter = new StreamWriter($"Armor.csv", false, Encoding.GetEncoding("shift-jis")))
            {
                var configuration = new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
                {
                    Delimiter = "\t",
                };
                var writer = new CsvWriter(textWriter, configuration);
                writer.WriteRecords(armorEntries);
            }

            // Write armor txt
            string textName = $"mhsx_Armor_{suffix}.txt";
            using StreamWriter file = new(textName, false, Encoding.UTF8);
            foreach (var entry in armorEntries)
                file.WriteLine("{0}", entry.Name);
            #endregion
        }

        private static void DumpWeaponData(string mhfdat)
        {

            string[] ailmentIds = ["なし", "毒", "麻痺", "睡眠", "爆破"];
            string[] wClassIds = ["大剣", "ヘビィボウガン", "ハンマー", "ランス", "片手剣", "ライトボウガン", "双剣", "太刀", "狩猟笛", "ガンランス", "弓", "穿龍棍", "スラッシュアックスＦ", "マグネットスパイク"];
            string[] elementIds = ["なし", "火", "水", "雷", "龍", "氷", "炎", "光", "雷極", "天翔", "熾凍", "黒焔", "奏", "闇", "紅魔", "風", "響", "灼零", "皇鳴"];

            #region WeaponData
            // Dump melee weapon data
            using var msInput = new MemoryStream(File.ReadAllBytes(mhfdat));
            using var brInput = new BinaryReader(msInput);
            brInput.BaseStream.Seek(_soMelee, SeekOrigin.Begin);
            int sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(_eoMelee, SeekOrigin.Begin);
            int eOffset = brInput.ReadInt32();

            int entryCountMelee = (eOffset - sOffset) / MELEE_WEAPON_ENTRY_SIZE;
            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            Console.WriteLine($"Melee count: {entryCountMelee}");

            MeleeWeaponEntry[] meleeEntries = new MeleeWeaponEntry[entryCountMelee];
            for (int i = 0; i < entryCountMelee; i++)
            {
                MeleeWeaponEntry entry = new()
                {
                    ModelId = brInput.ReadInt16()
                };
                entry.ModelIdData = GetModelIdData(entry.ModelId);
                entry.Rarity = brInput.ReadByte();
                entry.ClassId = wClassIds[brInput.ReadByte()];
                entry.ZennyCost = brInput.ReadInt32();
                entry.SharpnessId = brInput.ReadInt16();
                entry.RawDamage = brInput.ReadInt16();
                entry.Defense = brInput.ReadInt16();
                entry.Affinity = brInput.ReadSByte();
                entry.ElementId = elementIds[brInput.ReadByte()];
                entry.EleDamage = brInput.ReadByte() * 10;
                entry.AilmentId = ailmentIds[brInput.ReadByte()];
                entry.AilDamage = brInput.ReadByte() * 10;
                entry.Slots = brInput.ReadByte();
                entry.Unk3 = brInput.ReadByte();
                entry.Unk4 = brInput.ReadByte();
                entry.Unk5 = brInput.ReadInt16();
                entry.Unk6 = brInput.ReadInt16();
                entry.Unk7 = brInput.ReadInt16();
                entry.Unk8 = brInput.ReadInt32();
                entry.Unk9 = brInput.ReadInt32();
                entry.Unk10 = brInput.ReadInt16();
                entry.Unk11 = brInput.ReadInt16();
                entry.Unk12 = brInput.ReadByte();
                entry.Unk13 = brInput.ReadByte();
                entry.Unk14 = brInput.ReadByte();
                entry.Unk15 = brInput.ReadByte();
                entry.Unk16 = brInput.ReadInt32();
                entry.Unk17 = brInput.ReadInt32();

                meleeEntries[i] = entry;
            }

            // Get strings
            brInput.BaseStream.Seek(_soStringMelee, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            for (int j = 0; j < entryCountMelee - 1; j++)
            {
                string name = StringFromPointer(brInput);
                meleeEntries[j].Name = name;
            }

            // Write csv
            using (var textWriter = new StreamWriter("Melee.csv", false, Encoding.GetEncoding("shift-jis")))
            {
                var configuration = new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
                {
                    Delimiter = "\t",
                };
                var writer = new CsvWriter(textWriter, configuration);
                writer.WriteRecords(meleeEntries);
            }

            // Dump ranged weapon data
            brInput.BaseStream.Seek(_soRanged, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(_eoRanged, SeekOrigin.Begin);
            eOffset = brInput.ReadInt32();

            int entryCountRanged = (eOffset - sOffset) / RANGED_WEAPON_ENTRY_SIZE;
            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            Console.WriteLine($"Ranged count: {entryCountRanged}");

            RangedWeaponEntry[] rangedEntries = new RangedWeaponEntry[entryCountRanged];
            for (int i = 0; i < entryCountRanged; i++)
            {
                RangedWeaponEntry entry = new()
                {
                    ModelId = brInput.ReadInt16()
                };
                entry.ModelIdData = GetModelIdData(entry.ModelId);
                entry.Rarity = brInput.ReadByte();
                entry.MaxSlotsMaybe = brInput.ReadByte();
                entry.ClassId = wClassIds[brInput.ReadByte()];
                entry.Unk2_1 = brInput.ReadByte();
                entry.EqType = brInput.ReadByte().ToString(); //Enum.GetName(typeof(eqType), brInput.ReadByte());
                entry.Unk2_3 = brInput.ReadByte();
                entry.Unk3_1 = brInput.ReadByte();
                entry.Unk3_2 = brInput.ReadByte();
                entry.Unk3_3 = brInput.ReadByte();
                entry.Unk3_4 = brInput.ReadByte();
                entry.Unk4_1 = brInput.ReadByte();
                entry.Unk4_2 = brInput.ReadByte();
                entry.Unk4_3 = brInput.ReadByte();
                entry.Unk4_4 = brInput.ReadByte();
                entry.Unk5_1 = brInput.ReadByte();
                entry.Unk5_2 = brInput.ReadByte();
                entry.Unk5_3 = brInput.ReadByte();
                entry.Unk5_4 = brInput.ReadByte();
                entry.ZennyCost = brInput.ReadInt32();
                entry.RawDamage = brInput.ReadInt16();
                entry.Defense = brInput.ReadInt16();
                entry.RecoilMaybe = brInput.ReadByte();
                entry.Slots = brInput.ReadByte();
                entry.Affinity = brInput.ReadSByte();
                entry.SortOrderMaybe = brInput.ReadByte();
                entry.Unk6_1 = brInput.ReadByte();
                entry.ElementId = elementIds[brInput.ReadByte()];
                entry.EleDamage = brInput.ReadByte() * 10;
                entry.Unk6_4 = brInput.ReadByte();
                entry.Unk7_1 = brInput.ReadByte();
                entry.Unk7_2 = brInput.ReadByte();
                entry.Unk7_3 = brInput.ReadByte();
                entry.Unk7_4 = brInput.ReadByte();
                entry.Unk8_1 = brInput.ReadByte();
                entry.Unk8_2 = brInput.ReadByte();
                entry.Unk8_3 = brInput.ReadByte();
                entry.Unk8_4 = brInput.ReadByte();
                entry.Unk9_1 = brInput.ReadByte();
                entry.Unk9_2 = brInput.ReadByte();
                entry.Unk9_3 = brInput.ReadByte();
                entry.Unk9_4 = brInput.ReadByte();
                entry.Unk10_1 = brInput.ReadByte();
                entry.Unk10_2 = brInput.ReadByte();
                entry.Unk10_3 = brInput.ReadByte();
                entry.Unk10_4 = brInput.ReadByte();
                entry.Unk11_1 = brInput.ReadByte();
                entry.Unk11_2 = brInput.ReadByte();
                entry.Unk11_3 = brInput.ReadByte();
                entry.Unk11_4 = brInput.ReadByte();
                entry.Unk12_1 = brInput.ReadByte();
                entry.Unk12_2 = brInput.ReadByte();
                entry.Unk12_3 = brInput.ReadByte();
                entry.Unk12_4 = brInput.ReadByte();

                rangedEntries[i] = entry;
            }

            // Get strings
            brInput.BaseStream.Seek(_soStringRanged, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            for (int j = 0; j < entryCountRanged - 1; j++)
            {
                string name = StringFromPointer(brInput);
                rangedEntries[j].Name = name;
            }

            // Write csv
            using (var textWriter = new StreamWriter("Ranged.csv", false, Encoding.GetEncoding("shift-jis")))
            {
                var configuration = new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
                {
                    Delimiter = "\t",
                };
                var writer = new CsvWriter(textWriter, configuration);
                writer.WriteRecords(rangedEntries);
            }
            #endregion

        }

        private static void DumpQuestData(string mhfinf)
        {
            // Quest data info
            List<KeyValuePair<int, int>> offsetInfQuestData =
            [
                new KeyValuePair<int, int>(0x6bd60, 95),
                new KeyValuePair<int, int>(0x74100, 62),
                new KeyValuePair<int, int>(0x797e0, 99),
                new KeyValuePair<int, int>(0x821a0, 98),
                new KeyValuePair<int, int>(0x8aa00, 99),
                new KeyValuePair<int, int>(0x933c0, 99),
                new KeyValuePair<int, int>(0x9bd80, 99),
                new KeyValuePair<int, int>(0xa4740, 99),
                new KeyValuePair<int, int>(0xad100, 99),
                new KeyValuePair<int, int>(0xb5b40, 36),
                new KeyValuePair<int, int>(0xb8e60, 96),
                new KeyValuePair<int, int>(0xc1400, 91),

                new KeyValuePair<int, int>(0x161220, 20), // Incorrect
            ];

            #region QuestData
            // Dump inf quest data
            using var msInput = new MemoryStream(File.ReadAllBytes(mhfinf));
            using var brInput = new BinaryReader(msInput);

            int totalCount = 0;
            for (int j = 0; j < offsetInfQuestData.Count; j++)
                totalCount += offsetInfQuestData[j].Value;
            QuestData[] quests = new QuestData[totalCount];

            int currentCount = 0;
            for (int j = 0; j < offsetInfQuestData.Count; j++)
            {
                brInput.BaseStream.Seek(offsetInfQuestData[j].Key, SeekOrigin.Begin);
                for (int i = 0; i < offsetInfQuestData[j].Value; i++)
                {
                    QuestData entry = new()
                    {
                        Unk1 = brInput.ReadByte(),
                        Unk2 = brInput.ReadByte(),
                        Unk3 = brInput.ReadByte(),
                        Unk4 = brInput.ReadByte(),
                        Level = brInput.ReadByte(),
                        Unk5 = brInput.ReadByte(),
                        CourseType = brInput.ReadByte(),
                        Unk7 = brInput.ReadByte(),
                        Unk8 = brInput.ReadByte(),
                        Unk9 = brInput.ReadByte(),
                        Unk10 = brInput.ReadByte(),
                        Unk11 = brInput.ReadByte(),
                        Fee = brInput.ReadInt32(),
                        ZennyMain = brInput.ReadInt32(),
                        ZennyKo = brInput.ReadInt32(),
                        ZennySubA = brInput.ReadInt32(),
                        ZennySubB = brInput.ReadInt32(),
                        Time = brInput.ReadInt32(),
                        Unk12 = brInput.ReadInt32(),
                        Unk13 = brInput.ReadByte(),
                        Unk14 = brInput.ReadByte(),
                        Unk15 = brInput.ReadByte(),
                        Unk16 = brInput.ReadByte(),
                        Unk17 = brInput.ReadByte(),
                        Unk18 = brInput.ReadByte(),
                        Unk19 = brInput.ReadByte(),
                        Unk20 = brInput.ReadByte()
                    };
                    int questType = brInput.ReadInt32();
                    entry.MainGoalType = Enum.GetName(typeof(QuestTypes), questType);
                    entry.MainGoalType ??= questType.ToString("X8");

                    entry.MainGoalTarget = brInput.ReadInt16();
                    entry.MainGoalCount = brInput.ReadInt16();
                    questType = brInput.ReadInt32();
                    entry.SubAGoalType = Enum.GetName(typeof(QuestTypes), questType);
                    entry.SubAGoalType ??= questType.ToString("X8");

                    entry.SubAGoalTarget = brInput.ReadInt16();
                    entry.SubAGoalCount = brInput.ReadInt16();
                    questType = brInput.ReadInt32();
                    entry.SubBGoalType = Enum.GetName(typeof(QuestTypes), questType);
                    entry.SubBGoalType ??= questType.ToString("X8");

                    entry.SubBGoalTarget = brInput.ReadInt16();
                    entry.SubBGoalCount = brInput.ReadInt16();

                    brInput.BaseStream.Seek(0x5C, SeekOrigin.Current);
                    entry.MainGRP = brInput.ReadInt32();
                    entry.SubAGRP = brInput.ReadInt32();
                    entry.SubBGRP = brInput.ReadInt32();

                    brInput.BaseStream.Seek(0x90, SeekOrigin.Current);
                    entry.Title = StringFromPointer(brInput);
                    entry.TextMain = StringFromPointer(brInput);
                    entry.TextSubA = StringFromPointer(brInput);
                    entry.TextSubB = StringFromPointer(brInput);
                    brInput.BaseStream.Seek(0x10, SeekOrigin.Current);
                    Console.WriteLine(brInput.BaseStream.Position.ToString("X8"));

                    quests[currentCount + i] = entry;
                }
                currentCount += offsetInfQuestData[j].Value;
            }

            // Write csv
            using var textWriter = new StreamWriter("InfQuests.csv", false, Encoding.GetEncoding("shift-jis"));
            var configuration = new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
            {
                Delimiter = "\t",
            };
            var writer = new CsvWriter(textWriter, configuration);
            writer.WriteRecords(quests);
            #endregion
        }


        /// <summary>
        /// Add all-items shop to file, change item prices, change armor prices
        /// </summary>
        /// <param name="file">Input file path, usually mhfdat.bin.</param>
        private static void ModShop(string file)
        {
            int count;

            // Patch item and equip prices in file
            using (MemoryStream msInput = new(File.ReadAllBytes(file)))
            using (BinaryReader brInput = new(msInput))
            using (BinaryWriter brOutput = new(File.Open(file, FileMode.Open)))
            {
                // Patch item prices
                brInput.BaseStream.Seek(0xFC, SeekOrigin.Begin);
                int sOffset = brInput.ReadInt32();
                brInput.BaseStream.Seek(0xA70, SeekOrigin.Begin);
                int eOffset = brInput.ReadInt32();

                count = (eOffset - sOffset) / ITEM_ENTRY_SIZE;
                Console.WriteLine($"Patching prices for {count} items starting at 0x{sOffset:X8}");
                for (int i = 0; i < count; i++)
                {
                    brOutput.BaseStream.Seek(sOffset + (i * ITEM_ENTRY_SIZE) + 12, SeekOrigin.Begin);
                    brInput.BaseStream.Seek(sOffset + (i * ITEM_ENTRY_SIZE) + 12, SeekOrigin.Begin);
                    int buyPrice = brInput.ReadInt32() / 50;
                    brOutput.Write(buyPrice);

                    brOutput.BaseStream.Seek(sOffset + (i * ITEM_ENTRY_SIZE) + 16, SeekOrigin.Begin);
                    brInput.BaseStream.Seek(sOffset + (i * ITEM_ENTRY_SIZE) + 16, SeekOrigin.Begin);
                    int sellPrice = brInput.ReadInt32() * 5;
                    brOutput.Write(sellPrice);
                }

                // Patch equip prices
                for (int i = 0; i < 5; i++)
                {
                    brInput.BaseStream.Seek(_dataPointersArmor[i].Key, SeekOrigin.Begin);
                    sOffset = brInput.ReadInt32();
                    brInput.BaseStream.Seek(_dataPointersArmor[i].Value, SeekOrigin.Begin);
                    eOffset = brInput.ReadInt32();

                    count = (eOffset - sOffset) / ARMOR_ENTRY_SIZE;
                    Console.WriteLine($"Patching prices for {count} armor pieces starting at 0x{sOffset:X8}");
                    for (int j = 0; j < count; j++)
                    {
                        brOutput.BaseStream.Seek(sOffset + (j * ARMOR_ENTRY_SIZE) + 12, SeekOrigin.Begin);
                        brOutput.Write(50);
                    }
                }
            }

            // Generate shop array
            count = 16700;
            byte[] shopArray = new byte[(count * SHOP_ENTRY_SIZE) + 5 * 32];

            for (int i = 0; i < count; i++)
            {
                byte[] id = BitConverter.GetBytes((short)(i + 1));
                byte[] item = new byte[8];
                Array.Copy(id, item, 2);
                Array.Copy(item, 0, shopArray, i * SHOP_ENTRY_SIZE, 8);
            }

            // Append modshop data to file
            byte[] inputArray = File.ReadAllBytes(file);
            byte[] outputArray = new byte[inputArray.Length + shopArray.Length];
            Array.Copy(inputArray, outputArray, inputArray.Length);
            Array.Copy(shopArray, 0, outputArray, inputArray.Length, shopArray.Length);

            // Find and modify item shop data pointer
            byte[] needle = [0x0F, 01, 01, 00, 00, 00, 00, 00, 03, 01, 01, 00, 00, 00, 00, 00];
            int offsetData = ByteOperations.GetOffsetOfArray(outputArray, needle);
            if (offsetData != -1)
            {
                Console.WriteLine($"Found shop inventory to modify at 0x{offsetData:X8}.");
                byte[] offsetArray = BitConverter.GetBytes(offsetData);
                offsetArray.Reverse();
                int offsetPointer = ByteOperations.GetOffsetOfArray(outputArray, offsetArray);
                if (offsetPointer != -1)
                {
                    Console.WriteLine($"Found shop pointer at 0x{offsetPointer:X8}.");
                    byte[] patchedPointer = BitConverter.GetBytes(inputArray.Length);
                    patchedPointer.Reverse();
                    Array.Copy(patchedPointer, 0, outputArray, offsetPointer, patchedPointer.Length);
                }
                else
                    Console.WriteLine("Could not find shop pointer, please check manually and correct code.");
            }
            else
                Console.WriteLine("Could not find shop needle, please check manually and correct code.");

            // Find and modify Hunter Pearl Skill unlocks
            needle = [01, 00, 01, 00, 00, 00, 00, 00, 0x25, 00, 0x25, 00, 0x25, 00, 0x25, 00, 0x25, 00, 0x25, 00, 0x25, 00];
            offsetData = ByteOperations.GetOffsetOfArray(outputArray, needle);
            if (offsetData != -1)
            {
                Console.WriteLine($"Found hunter pearl skill data to modify at 0x{offsetData:X8}.");
                byte[] pearlPatch = [02, 00, 02, 00, 02, 00, 02, 00, 02, 00, 02, 00, 02, 00];
                for (int i = 0; i < 108; i++)
                    Array.Copy(pearlPatch, 0, outputArray, offsetData + (i * PEARL_ENTRY_SIZE) + 8, pearlPatch.Length);
            }
            else
                Console.WriteLine("Could not find pearl skill needle, please check manually and correct code.");

            // Write to file
            File.WriteAllBytes(file, outputArray);
        }

        /// <summary>
        /// Read a string following a pointer.
        /// </summary>
        /// <param name="brInput">Input binary reader.</param>
        /// <returns>String found.</returns>
        private static string StringFromPointer(BinaryReader brInput)
        {
            int off = brInput.ReadInt32();
            long pos = brInput.BaseStream.Position;
            brInput.BaseStream.Seek(off, SeekOrigin.Begin);
            string str = FileOperations.ReadNullterminatedString(brInput, Encoding.GetEncoding("shift-jis")).Replace("\n", "<NL>");
            brInput.BaseStream.Seek(pos, SeekOrigin.Begin);
            return str;
        }

        public static string GetModelIdData(int id)
        {
            string str;
            if (id >= 0 && id < 1000) str = $"we{id:D3}";
            else if (id < 2000) str = $"wf{id - 1000:D3}";
            else if (id < 3000) str = $"wg{id - 2000:D3}";
            else if (id < 4000) str = $"wh{id - 3000:D3}";
            else if (id < 5000) str = $"wi{id - 4000:D3}";
            else if (id < 7000) str = $"wk{id - 6000:D3}";
            else if (id < 8000) str = $"wl{id - 7000:D3}";
            else if (id < 9000) str = $"wm{id - 8000:D3}";
            else if (id < 10000) str = $"wg{id - 9000:D3}";
            else str = "Unmapped";
            return str;
        }
    }
}
