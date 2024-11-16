using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using LibReFrontier;

namespace FrontierDataTool
{
    class Program
    {
        // Define offset pointers
        // --- mhfdat.bin ---
        // Strings
        static readonly int soStringHead = 0x64, soStringBody = 0x68, soStringArm = 0x6C, soStringWaist = 0x70, soStringLeg = 0x74;

        static readonly int eoStringHead = 0x60; 
        static readonly int eoStringBody = 0x64; 
        static readonly int eoStringArm = 0x68; 
        static readonly int eoStringWaist = 0x6C; 
        static readonly int eoStringLeg = 0x70;

        static readonly int soStringRanged = 0x84, soStringMelee = 0x88;

        // static readonly int eoStringRanged = 0x88, eoStringMelee = 0x174;
        
        static readonly int soStringItem = 0x100; 
        static readonly int soStringItemDesc = 0x12C;
        static readonly int eoStringItem = 0xFC; 
        static readonly int eoStringItemDesc = 0x100;

        // Armor
        static readonly int soHead = 0x50; 
        static readonly int soBody = 0x54; 
        static readonly int soArm = 0x58; 
        static readonly int soWaist = 0x5C; 
        static readonly int soLeg = 0x60;

        static readonly int eoHead = 0xE8; 
        static readonly int eoBody = 0x50; 
        static readonly int eoArm = 0x54; 
        static readonly int eoWaist = 0x58; 
        static readonly int eoLeg = 0x5C;

        // Weapons
        static readonly int soRanged = 0x80, soMelee = 0x7C;
        static readonly int eoRanged = 0x7C, eoMelee = 0x90;


        // --- mhfpac.bin ---
        // Strings
        static readonly int soStringSkillPt = 0xA20, soStringSkillActivate = 0xA1C, soStringZSkill = 0xFBC, soStringSkillDesc = 0xb8;

        static readonly int eoStringSkillPt = 0xA1C, eoStringSkillActivate = 0xBC0, eoStringZSkill = 0xFB0, eoStringSkillDesc = 0xc0;

        // --- mhfinf.pac ---
        public static List<KeyValuePair<int, int>> offsetInfQuestData =
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

        public static List<KeyValuePair<int, int>> dataPointersArmor =
        [
            new KeyValuePair<int, int>(soHead, eoHead),
            new KeyValuePair<int, int>(soBody, eoBody),
            new KeyValuePair<int, int>(soArm, eoArm),
            new KeyValuePair<int, int>(soWaist, eoWaist),
            new KeyValuePair<int, int>(soLeg, eoLeg)
        ];

        public static List<KeyValuePair<int, int>> stringPointersArmor =
        [
            new KeyValuePair<int, int>(soStringHead, eoStringHead),
            new KeyValuePair<int, int>(soStringBody, eoStringBody),
            new KeyValuePair<int, int>(soStringArm, eoStringArm),
            new KeyValuePair<int, int>(soStringWaist, eoStringWaist),
            new KeyValuePair<int, int>(soStringLeg, eoStringLeg)
        ];

        public static string[] elementIds = ["なし", "火", "水", "雷", "龍", "氷", "炎", "光", "雷極", "天翔", "熾凍", "黒焔", "奏", "闇", "紅魔", "風", "響", "灼零", "皇鳴"];
        public static string[] ailmentIds = ["なし", "毒", "麻痺", "睡眠", "爆破"];
        public static string[] wClassIds = ["大剣", "ヘビィボウガン", "ハンマー", "ランス", "片手剣", "ライトボウガン", "双剣", "太刀", "狩猟笛", "ガンランス", "弓", "穿龍棍", "スラッシュアックスＦ", "マグネットスパイク"];
        public static string[] aClassIds = ["頭", "胴", "腕", "腰", "脚"];
        public enum EqType { 通常 = 0, ＳＰ = 1, 剛種 = 2, 進化 = 4, ＨＣ = 8 };

        static void Main(string[] args)
        {
            if (args.Length < 2) {
                Console.WriteLine("Too few arguments.");
                return;
            }
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            if (args[0] == "dump")
                DumpData(args[1], args[2], args[3], args[4]);  // suffix, mhfpac.bin, mhfdat.bin, mhfinf.bin
            if (args[0] == "modshop")
                ModShop(args[1]);                              // mhfdat.bin
            Console.WriteLine("Done");
            Console.Read();
        }

        /// <summary>
        /// Dump data and strings
        /// </summary>
        /// <param name="suffix">Output suffix</param>
        /// <param name="mhfpac">Path to mhfpac</param>
        /// <param name="mhfdat">Path to mhfdat</param>
        /// <param name="mhfinf">Path to mhfinf</param>
        static void DumpData(string suffix, string mhfpac, string mhfdat, string mhfinf)
        {
            #region SkillSystem
            // Get and dump skill system dictionary
            Console.WriteLine("Dumping skill tree names.");
            MemoryStream msInput = new(File.ReadAllBytes(mhfpac));
            BinaryReader brInput = new(msInput);
            brInput.BaseStream.Seek(soStringSkillPt, SeekOrigin.Begin); 
            int sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(eoStringSkillPt, SeekOrigin.Begin);
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

            string textName = $"mhsx_SkillSys_{suffix}.txt";
            using (StreamWriter file = new(textName, false, Encoding.UTF8))
                foreach (KeyValuePair<int, string> entry in skillId)
                    file.WriteLine("{0}", entry.Value);
            #endregion

            #region ActiveSkill
            Console.WriteLine("Dumping active skill names.");
            brInput.BaseStream.Seek(soStringSkillActivate, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(eoStringSkillActivate, SeekOrigin.Begin);
            eOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            List<string> activeSkill = [];
            while (brInput.BaseStream.Position < eOffset)
            {
                string name = StringFromPointer(brInput);
                activeSkill.Add(name);
            }

            textName = $"mhsx_SkillActivate_{suffix}.txt";
            using (StreamWriter file = new(textName, false, Encoding.UTF8))
                foreach (string entry in activeSkill)
                    file.WriteLine("{0}", entry);
            #endregion

            #region SkillDescription
            Console.WriteLine("Dumping active skill descriptions.");
            brInput.BaseStream.Seek(soStringSkillDesc, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(eoStringSkillDesc, SeekOrigin.Begin);
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
            brInput.BaseStream.Seek(soStringZSkill, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(eoStringZSkill, SeekOrigin.Begin);
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

            #region Items
            Console.WriteLine("Dumping item names.");
            msInput = new MemoryStream(File.ReadAllBytes(mhfdat));
            brInput = new BinaryReader(msInput);            
            brInput.BaseStream.Seek(soStringItem, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(eoStringItem, SeekOrigin.Begin);
            eOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            List<string> items = [];
            while (brInput.BaseStream.Position < eOffset)
            {
                string name = StringFromPointer(brInput);
                items.Add(name);
            }

            textName = $"mhsx_Items_{suffix}.txt";
            using (StreamWriter file = new(textName, false, Encoding.UTF8))
                foreach (string entry in items)
                    file.WriteLine("{0}", entry);

            Console.WriteLine("Dumping item descriptions.");
            brInput.BaseStream.Seek(soStringItemDesc, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(eoStringItemDesc, SeekOrigin.Begin);
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

            #region EquipmentData
            // Dump armor data
            int totalCount = 0;
            for (int i = 0; i < 5; i++)
            {
                // Get raw data
                brInput.BaseStream.Seek(dataPointersArmor[i].Key, SeekOrigin.Begin);
                sOffset = brInput.ReadInt32();
                brInput.BaseStream.Seek(dataPointersArmor[i].Value, SeekOrigin.Begin);
                eOffset = brInput.ReadInt32();

                int entryCount = (eOffset - sOffset) / 0x48;
                totalCount += entryCount;
            }
            Console.WriteLine($"Total armor count: {totalCount}");

            Structs.ArmorDataEntry[] armorEntries = new Structs.ArmorDataEntry[totalCount];
            int currentCount = 0;
            for (int i = 0; i < 5; i++)
            {
                // Get raw data
                brInput.BaseStream.Seek(dataPointersArmor[i].Key, SeekOrigin.Begin);
                sOffset = brInput.ReadInt32();
                brInput.BaseStream.Seek(dataPointersArmor[i].Value, SeekOrigin.Begin);
                eOffset = brInput.ReadInt32();
                
                int entryCount = (eOffset - sOffset) / 0x48;
                brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
                Console.WriteLine($"{aClassIds[i]} count: {entryCount}");

                for (int j = 0; j < entryCount; j++)
                {
                    Structs.ArmorDataEntry entry = new()
                    {
                        equipClass = aClassIds[i],
                        modelIdMale = brInput.ReadInt16(),
                        modelIdFemale = brInput.ReadInt16()
                    };
                    byte bitfield = brInput.ReadByte();
                    entry.isMaleEquip = (bitfield & (1 << 1 - 1)) != 0;
                    entry.isFemaleEquip = (bitfield & (1 << 2 - 1)) != 0;
                    entry.isBladeEquip = (bitfield & (1 << 3 - 1)) != 0;
                    entry.isGunnerEquip = (bitfield & (1 << 4 - 1)) != 0;
                    entry.bool1 = (bitfield & (1 << 5 - 1)) != 0;
                    entry.isSPEquip = (bitfield & (1 << 6 - 1)) != 0;
                    entry.bool3 = (bitfield & (1 << 7 - 1)) != 0;
                    entry.bool4 = (bitfield & (1 << 8 - 1)) != 0;
                    entry.rarity = brInput.ReadByte();
                    entry.maxLevel = brInput.ReadByte();
                    entry.unk1_1 = brInput.ReadByte();
                    entry.unk1_2 = brInput.ReadByte();
                    entry.unk1_3 = brInput.ReadByte();
                    entry.unk1_4 = brInput.ReadByte();
                    entry.unk2 = brInput.ReadByte();
                    entry.zennyCost = brInput.ReadInt32();
                    entry.unk3 = brInput.ReadInt16();
                    entry.baseDefense = brInput.ReadInt16();
                    entry.fireRes = brInput.ReadSByte();
                    entry.waterRes = brInput.ReadSByte();
                    entry.thunderRes = brInput.ReadSByte();
                    entry.dragonRes = brInput.ReadSByte();
                    entry.iceRes = brInput.ReadSByte();
                    entry.unk3_1 = brInput.ReadInt16();
                    entry.baseSlots = brInput.ReadByte();
                    entry.maxSlots = brInput.ReadByte();
                    entry.sthEventCrown = brInput.ReadByte();
                    entry.unk5 = brInput.ReadByte();
                    entry.unk6 = brInput.ReadByte();
                    entry.unk7_1 = brInput.ReadByte();
                    entry.unk7_2 = brInput.ReadByte();
                    entry.unk7_3 = brInput.ReadByte();
                    entry.unk7_4 = brInput.ReadByte();
                    entry.unk8_1 = brInput.ReadByte();
                    entry.unk8_2 = brInput.ReadByte();
                    entry.unk8_3 = brInput.ReadByte();
                    entry.unk8_4 = brInput.ReadByte();
                    entry.unk10 = brInput.ReadInt16();
                    entry.skillId1 = skillId[brInput.ReadByte()].Value;
                    entry.skillPts1 = brInput.ReadSByte();
                    entry.skillId2 = skillId[brInput.ReadByte()].Value;
                    entry.skillPts2 = brInput.ReadSByte();
                    entry.skillId3 = skillId[brInput.ReadByte()].Value;
                    entry.skillPts3 = brInput.ReadSByte();
                    entry.skillId4 = skillId[brInput.ReadByte()].Value;
                    entry.skillPts4 = brInput.ReadSByte();
                    entry.skillId5 = skillId[brInput.ReadByte()].Value;
                    entry.skillPts5 = brInput.ReadSByte();
                    entry.sthHiden = brInput.ReadInt32();
                    entry.unk12 = brInput.ReadInt32();
                    entry.unk13 = brInput.ReadByte();
                    entry.unk14 = brInput.ReadByte();
                    entry.unk15 = brInput.ReadByte();
                    entry.unk16 = brInput.ReadByte();
                    entry.unk17 = brInput.ReadInt32();
                    entry.unk18 = brInput.ReadInt16();
                    entry.unk19 = brInput.ReadInt16();

                    armorEntries[j + currentCount] = entry;
                }

                // Get strings
                brInput.BaseStream.Seek(stringPointersArmor[i].Key, SeekOrigin.Begin);
                sOffset = brInput.ReadInt32();

                brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
                for (int j = 0; j < entryCount - 1; j++)
                {
                    string name = StringFromPointer(brInput);
                    armorEntries[j + currentCount].name = name;
                }
                currentCount += entryCount;
            }

            // Write armor csv
            using (var textWriter = new StreamWriter($"Armor.csv", false, Encoding.GetEncoding("shift-jis")))
            {
                var writer = new CsvWriter(textWriter);
                writer.Configuration.Delimiter = "\t";
                writer.WriteRecords(armorEntries);
            }

            // Write armor txt
            textName = $"mhsx_Armor_{suffix}.txt";
            using (StreamWriter file = new(textName, false, Encoding.UTF8))
                foreach (var entry in armorEntries)
                    file.WriteLine("{0}", entry.name);
            #endregion

            #region WeaponData
            // Dump melee weapon data
            brInput.BaseStream.Seek(soMelee, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(eoMelee, SeekOrigin.Begin);
            eOffset = brInput.ReadInt32();

            int entryCountMelee = (eOffset - sOffset) / 0x34;
            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            Console.WriteLine($"Melee count: {entryCountMelee}");

            Structs.MeleeWeaponEntry[] meleeEntries = new Structs.MeleeWeaponEntry[entryCountMelee];
            for (int i = 0; i < entryCountMelee; i++)
            {
                Structs.MeleeWeaponEntry entry = new()
                {
                    modelId = brInput.ReadInt16()
                };
                entry.modelIdData = GetModelIdData(entry.modelId);
                entry.rarity = brInput.ReadByte();
                entry.classId = wClassIds[brInput.ReadByte()];
                entry.zennyCost = brInput.ReadInt32();
                entry.sharpnessId = brInput.ReadInt16();
                entry.rawDamage = brInput.ReadInt16();
                entry.defense = brInput.ReadInt16();
                entry.affinity = brInput.ReadSByte();
                entry.elementId = elementIds[brInput.ReadByte()];
                entry.eleDamage = brInput.ReadByte() * 10;
                entry.ailmentId = ailmentIds[brInput.ReadByte()];
                entry.ailDamage = brInput.ReadByte() * 10;
                entry.slots = brInput.ReadByte();
                entry.unk3 = brInput.ReadByte();
                entry.unk4 = brInput.ReadByte();
                entry.unk5 = brInput.ReadInt16();
                entry.unk6 = brInput.ReadInt16();
                entry.unk7 = brInput.ReadInt16();
                entry.unk8 = brInput.ReadInt32();
                entry.unk9 = brInput.ReadInt32();
                entry.unk10 = brInput.ReadInt16();
                entry.unk11 = brInput.ReadInt16();
                entry.unk12 = brInput.ReadByte();
                entry.unk13 = brInput.ReadByte();
                entry.unk14 = brInput.ReadByte();
                entry.unk15 = brInput.ReadByte();
                entry.unk16 = brInput.ReadInt32();
                entry.unk17 = brInput.ReadInt32();

                meleeEntries[i] = entry;
            }

            // Get strings
            brInput.BaseStream.Seek(soStringMelee, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            for (int j = 0; j < entryCountMelee - 1; j++)
            {
                string name = StringFromPointer(brInput);
                meleeEntries[j].name = name;
            }

            // Write csv
            using (var textWriter = new StreamWriter("Melee.csv", false, Encoding.GetEncoding("shift-jis")))
            {
                var writer = new CsvWriter(textWriter);
                writer.Configuration.Delimiter = "\t";
                writer.WriteRecords(meleeEntries);
            }

            // Dump ranged weapon data
            brInput.BaseStream.Seek(soRanged, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(eoRanged, SeekOrigin.Begin);
            eOffset = brInput.ReadInt32();

            int entryCountRanged = (eOffset - sOffset) / 0x3C;
            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            Console.WriteLine($"Ranged count: {entryCountRanged}");

            Structs.RangedWeaponEntry[] rangedEntries = new Structs.RangedWeaponEntry[entryCountRanged];
            for (int i = 0; i < entryCountRanged; i++)
            {
                Structs.RangedWeaponEntry entry = new()
                {
                    modelId = brInput.ReadInt16()
                };
                entry.modelIdData = GetModelIdData(entry.modelId);
                entry.rarity = brInput.ReadByte();
                entry.maxSlotsMaybe = brInput.ReadByte();
                entry.classId = wClassIds[brInput.ReadByte()];
                entry.unk2_1 = brInput.ReadByte();
                entry.eqType = brInput.ReadByte().ToString(); //Enum.GetName(typeof(eqType), brInput.ReadByte());
                entry.unk2_3 = brInput.ReadByte();
                entry.unk3_1 = brInput.ReadByte();
                entry.unk3_2 = brInput.ReadByte();
                entry.unk3_3 = brInput.ReadByte();
                entry.unk3_4 = brInput.ReadByte();
                entry.unk4_1 = brInput.ReadByte();
                entry.unk4_2 = brInput.ReadByte();
                entry.unk4_3 = brInput.ReadByte();
                entry.unk4_4 = brInput.ReadByte();
                entry.unk5_1 = brInput.ReadByte();
                entry.unk5_2 = brInput.ReadByte();
                entry.unk5_3 = brInput.ReadByte();
                entry.unk5_4 = brInput.ReadByte();
                entry.zennyCost = brInput.ReadInt32();
                entry.rawDamage = brInput.ReadInt16();
                entry.defense = brInput.ReadInt16();
                entry.recoilMaybe = brInput.ReadByte();
                entry.slots = brInput.ReadByte();
                entry.affinity = brInput.ReadSByte();
                entry.sortOrderMaybe = brInput.ReadByte();
                entry.unk6_1 = brInput.ReadByte();
                entry.elementId = elementIds[brInput.ReadByte()];
                entry.eleDamage = brInput.ReadByte() * 10;
                entry.unk6_4 = brInput.ReadByte();
                entry.unk7_1 = brInput.ReadByte();
                entry.unk7_2 = brInput.ReadByte();
                entry.unk7_3 = brInput.ReadByte();
                entry.unk7_4 = brInput.ReadByte();
                entry.unk8_1 = brInput.ReadByte();
                entry.unk8_2 = brInput.ReadByte();
                entry.unk8_3 = brInput.ReadByte();
                entry.unk8_4 = brInput.ReadByte();
                entry.unk9_1 = brInput.ReadByte();
                entry.unk9_2 = brInput.ReadByte();
                entry.unk9_3 = brInput.ReadByte();
                entry.unk9_4 = brInput.ReadByte();
                entry.unk10_1 = brInput.ReadByte();
                entry.unk10_2 = brInput.ReadByte();
                entry.unk10_3 = brInput.ReadByte();
                entry.unk10_4 = brInput.ReadByte();
                entry.unk11_1 = brInput.ReadByte();
                entry.unk11_2 = brInput.ReadByte();
                entry.unk11_3 = brInput.ReadByte();
                entry.unk11_4 = brInput.ReadByte();
                entry.unk12_1 = brInput.ReadByte();
                entry.unk12_2 = brInput.ReadByte();
                entry.unk12_3 = brInput.ReadByte();
                entry.unk12_4 = brInput.ReadByte();

                rangedEntries[i] = entry;
            }

            // Get strings
            brInput.BaseStream.Seek(soStringRanged, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            for (int j = 0; j < entryCountRanged - 1; j++)
            {
                string name = StringFromPointer(brInput);
                rangedEntries[j].name = name;
            }

            // Write csv
            using (var textWriter = new StreamWriter("Ranged.csv", false, Encoding.GetEncoding("shift-jis")))
            {
                var writer = new CsvWriter(textWriter);
                writer.Configuration.Delimiter = "\t";
                writer.WriteRecords(rangedEntries);
            }
            #endregion

            #region QuestData
            // Dump inf quest data
            msInput = new MemoryStream(File.ReadAllBytes(mhfinf));
            brInput = new BinaryReader(msInput);

            totalCount = 0;
            for (int j = 0; j < offsetInfQuestData.Count; j++)
                totalCount += offsetInfQuestData[j].Value;
            Structs.QuestData[] quests = new Structs.QuestData[totalCount];

            currentCount = 0;
            for (int j = 0; j < offsetInfQuestData.Count; j++)
            {
                brInput.BaseStream.Seek(offsetInfQuestData[j].Key, SeekOrigin.Begin);                
                for (int i = 0; i < offsetInfQuestData[j].Value; i++)
                {
                    Structs.QuestData entry = new()
                    {
                        unk1 = brInput.ReadByte(),
                        unk2 = brInput.ReadByte(),
                        unk3 = brInput.ReadByte(),
                        unk4 = brInput.ReadByte(),
                        level = brInput.ReadByte(),
                        unk5 = brInput.ReadByte(),
                        courseType = brInput.ReadByte(),
                        unk7 = brInput.ReadByte(),
                        unk8 = brInput.ReadByte(),
                        unk9 = brInput.ReadByte(),
                        unk10 = brInput.ReadByte(),
                        unk11 = brInput.ReadByte(),
                        fee = brInput.ReadInt32(),
                        zennyMain = brInput.ReadInt32(),
                        zennyKo = brInput.ReadInt32(),
                        zennySubA = brInput.ReadInt32(),
                        zennySubB = brInput.ReadInt32(),
                        time = brInput.ReadInt32(),
                        unk12 = brInput.ReadInt32(),
                        unk13 = brInput.ReadByte(),
                        unk14 = brInput.ReadByte(),
                        unk15 = brInput.ReadByte(),
                        unk16 = brInput.ReadByte(),
                        unk17 = brInput.ReadByte(),
                        unk18 = brInput.ReadByte(),
                        unk19 = brInput.ReadByte(),
                        unk20 = brInput.ReadByte()
                    };
                    int questType = brInput.ReadInt32();
                    entry.mainGoalType = Enum.GetName(typeof(Structs.QuestTypes), questType);
                    entry.mainGoalType ??= questType.ToString("X8");

                    entry.mainGoalTarget = brInput.ReadInt16();
                    entry.mainGoalCount = brInput.ReadInt16();
                    questType = brInput.ReadInt32();
                    entry.subAGoalType = Enum.GetName(typeof(Structs.QuestTypes), questType);
                    entry.subAGoalType ??= questType.ToString("X8");

                    entry.subAGoalTarget = brInput.ReadInt16();
                    entry.subAGoalCount = brInput.ReadInt16();
                    questType = brInput.ReadInt32();
                    entry.subBGoalType = Enum.GetName(typeof(Structs.QuestTypes), questType);
                    entry.subBGoalType ??= questType.ToString("X8");

                    entry.subBGoalTarget = brInput.ReadInt16();
                    entry.subBGoalCount = brInput.ReadInt16();

                    brInput.BaseStream.Seek(0x5C, SeekOrigin.Current);
                    entry.mainGRP = brInput.ReadInt32();
                    entry.subAGRP = brInput.ReadInt32();
                    entry.subBGRP = brInput.ReadInt32();

                    brInput.BaseStream.Seek(0x90, SeekOrigin.Current);
                    entry.title = StringFromPointer(brInput);
                    entry.textMain = StringFromPointer(brInput);
                    entry.textSubA = StringFromPointer(brInput);
                    entry.textSubB = StringFromPointer(brInput);
                    brInput.BaseStream.Seek(0x10, SeekOrigin.Current);
                    Console.WriteLine(brInput.BaseStream.Position.ToString("X8"));

                    quests[currentCount + i] = entry;
                }
                currentCount += offsetInfQuestData[j].Value;
            }

            // Write csv
            using (var textWriter = new StreamWriter("InfQuests.csv", false, Encoding.GetEncoding("shift-jis")))
            {
                var writer = new CsvWriter(textWriter);
                writer.Configuration.Delimiter = "\t";
                writer.WriteRecords(quests);
            }
            #endregion
        }

        
        /// <summary>
        /// Add all-items shop to file, change item prices, change armor prices
        /// </summary>
        /// <param name="file">Input file path.</param>
        static void ModShop(string file)
        {
            MemoryStream msInput = new(File.ReadAllBytes(file));
            BinaryReader brInput = new(msInput);
            BinaryWriter brOutput = new(File.Open(file, FileMode.Open));

            // Patch item prices
            brInput.BaseStream.Seek(0xFC, SeekOrigin.Begin);
            int sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(0xA70, SeekOrigin.Begin);
            int eOffset = brInput.ReadInt32();

            int count = (eOffset - sOffset) / 0x24;
            Console.WriteLine($"Patching prices for {count} items starting at 0x{sOffset:X8}");
            for (int i = 0; i < count; i++)
            {
                brOutput.BaseStream.Seek(sOffset + (i * 0x24) + 12, SeekOrigin.Begin);
                brInput.BaseStream.Seek(sOffset + (i * 0x24) + 12, SeekOrigin.Begin);
                int buyPrice = brInput.ReadInt32() / 50;
                brOutput.Write(buyPrice);

                brOutput.BaseStream.Seek(sOffset + (i * 0x24) + 16, SeekOrigin.Begin);
                brInput.BaseStream.Seek(sOffset + (i * 0x24) + 16, SeekOrigin.Begin);
                int sellPrice = brInput.ReadInt32() * 5;
                brOutput.Write(sellPrice);
            }

            // Patch equip prices
            for (int i = 0; i < 5; i++)
            {
                brInput.BaseStream.Seek(dataPointersArmor[i].Key, SeekOrigin.Begin);
                sOffset = brInput.ReadInt32();
                brInput.BaseStream.Seek(dataPointersArmor[i].Value, SeekOrigin.Begin);
                eOffset = brInput.ReadInt32();

                count = (eOffset - sOffset) / 0x48;
                Console.WriteLine($"Patching prices for {count} armor pieces starting at 0x{sOffset:X8}");
                for (int j = 0; j < count; j++)
                {
                    brOutput.BaseStream.Seek(sOffset + (j * 0x48) + 12, SeekOrigin.Begin);
                    brOutput.Write(50);
                }
            }

            brOutput.Close();
            brInput.Close();

            // Generate shop array
            count = 16700;
            byte[] shopArray = new byte[(count * 8) + 5 * 32];
            int blockSize = count / 5 * 8;

            for (int i = 0; i < count; i++)
            {
                byte[] id = BitConverter.GetBytes((short)(i + 1));
                byte[] item = new byte[8];
                Array.Copy(id, item, 2);
                Array.Copy(item, 0, shopArray, i * 8, 8);
            }

            // Append modshop data to file          
            byte[] inputArray = File.ReadAllBytes(file);
            byte[] outputArray = new byte[inputArray.Length + shopArray.Length];
            Array.Copy(inputArray, outputArray, inputArray.Length);
            Array.Copy(shopArray, 0, outputArray, inputArray.Length, shopArray.Length);

            // Find and modify item shop data pointer
            byte[] needle = [0x0F, 01, 01, 00, 00, 00, 00, 00, 03, 01, 01, 00, 00, 00, 00, 00];
            int offsetData = Helpers.GetOffsetOfArray(outputArray, needle);
            if (offsetData != -1)
            {
                Console.WriteLine($"Found shop inventory to modify at 0x{offsetData:X8}.");
                byte[] offsetArray = BitConverter.GetBytes(offsetData);
                offsetArray.Reverse();
                int offsetPointer = Helpers.GetOffsetOfArray(outputArray, offsetArray);
                if (offsetPointer != -1)
                {
                    Console.WriteLine($"Found shop pointer at 0x{offsetPointer:X8}.");
                    byte[] patchedPointer = BitConverter.GetBytes(inputArray.Length);
                    patchedPointer.Reverse();
                    Array.Copy(patchedPointer, 0, outputArray, offsetPointer, patchedPointer.Length);                    
                }
                else Console.WriteLine("Could not find shop pointer, please check manually and correct code.");
            }
            else Console.WriteLine("Could not find shop needle, please check manually and correct code.");

            // Find and modify Hunter Pearl Skill unlocks
            needle = [01, 00, 01, 00, 00, 00, 00, 00, 0x25, 00, 0x25, 00, 0x25, 00, 0x25, 00, 0x25, 00, 0x25, 00, 0x25, 00];
            offsetData = Helpers.GetOffsetOfArray(outputArray, needle);
            if (offsetData != -1)
            {
                Console.WriteLine($"Found hunter pearl skill data to modify at 0x{offsetData:X8}.");
                byte[] pearlPatch = [02, 00, 02, 00, 02, 00, 02, 00, 02, 00, 02, 00, 02, 00];
                for (int i = 0; i < 108; i++)
                    Array.Copy(pearlPatch, 0, outputArray, offsetData + (i * 0x30) + 8, pearlPatch.Length);                
            }
            else Console.WriteLine("Could not find pearl skill needle, please check manually and correct code.");

            // Write to file
            File.WriteAllBytes(file, outputArray);
        }

        static string StringFromPointer(BinaryReader brInput)
        {
            int off = brInput.ReadInt32();
            long pos = brInput.BaseStream.Position;
            brInput.BaseStream.Seek(off, SeekOrigin.Begin);
            string str = Helpers.ReadNullterminatedString(brInput, Encoding.GetEncoding("shift-jis")).Replace("\n", "<NL>");
            brInput.BaseStream.Seek(pos, SeekOrigin.Begin);
            return str;
        }

        static string GetModelIdData(int id)
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
