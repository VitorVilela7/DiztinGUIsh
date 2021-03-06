﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiztinGUIsh
{
    public class Manager
    {
        public static int Step(int offset, bool branch, bool force, int prevOffset)
        {
            Project.unsavedChanges = true;
            switch (Data.GetArchitechture(offset))
            {
                case Data.Architechture.CPU65C816: return CPU65C816.Step(offset, branch, force, prevOffset);
                case Data.Architechture.APUSPC700: return offset;
                case Data.Architechture.GPUSuperFX: return offset;
            }
            return offset;
        }

        public static int AutoStep(int offset, bool harsh, int amount)
        {
            Project.unsavedChanges = true;
            int newOffset = offset, prevOffset = offset - 1, nextOffset = offset;
            if (harsh)
            {
                while (newOffset < offset + amount)
                {
                    nextOffset = Step(newOffset, false, true, prevOffset);
                    prevOffset = newOffset;
                    newOffset = nextOffset;
                }
            } else
            {
                Stack<int> stack = new Stack<int>();
                List<int> seenBranches = new List<int>();
                bool keepGoing = true;

                while (keepGoing)
                {
                    switch (Data.GetArchitechture(newOffset))
                    {
                        case Data.Architechture.CPU65C816:
                            if (seenBranches.Contains(newOffset))
                            {
                                keepGoing = false;
                                break;
                            }

                            int opcode = Data.GetROMByte(newOffset);

                            nextOffset = Step(newOffset, false, false, prevOffset);
                            int jumpOffset = Step(newOffset, true, false, prevOffset);

                            if (opcode == 0x40 || opcode == 0xCB || opcode == 0xDB || opcode == 0xF8 // RTI WAI STP SED
                                || opcode == 0xFB || opcode == 0x00 || opcode == 0x02 || opcode == 0x42 // XCE BRK COP WDM
                                || opcode == 0x6C || opcode == 0x7C || opcode == 0xDC || opcode == 0xFC // JMP JMP JML JSR
                            ) keepGoing = false;

                            if (opcode == 0x4C || opcode == 0x5C || opcode == 0x80 || opcode == 0x82 // JMP JML BRA BRL
                                || opcode == 0x10 || opcode == 0x30 || opcode == 0x50 || opcode == 0x70 // BPL BMI BVC BVS
                                || opcode == 0x90 || opcode == 0xB0 || opcode == 0xD0 || opcode == 0xF0 // BCC BCS BNE BEQ
                            ) seenBranches.Add(newOffset);

                            if (opcode == 0x08) // PHP
                            {
                                stack.Push(Data.GetMXFlags(newOffset));
                            } else if (opcode == 0x28) // PLP
                            {
                                if (stack.Count == 0)
                                {
                                    keepGoing = false; break;
                                } else
                                {
                                    Data.SetMXFlags(newOffset, stack.Pop());
                                }
                            }

                            if (opcode == 0x60 || opcode == 0x6B) // RTS RTL
                            {
                                if (stack.Count == 0)
                                {
                                    keepGoing = false;
                                    break;
                                }
                                else
                                {
                                    prevOffset = newOffset;
                                    newOffset = stack.Pop();
                                }
                            } else if (opcode == 0x20 || opcode == 0x22) // JSR JSL
                            {
                                stack.Push(nextOffset);
                                prevOffset = newOffset;
                                newOffset = jumpOffset;
                            } else
                            {
                                prevOffset = newOffset;
                                newOffset = nextOffset;
                            }
                            break;
                        case Data.Architechture.APUSPC700:
                        case Data.Architechture.GPUSuperFX:
                            nextOffset = Step(newOffset, false, true, prevOffset);
                            prevOffset = newOffset;
                            newOffset = nextOffset;
                            break;
                    }

                    Data.FlagType flag = Data.GetFlag(newOffset);
                    if (!(flag == Data.FlagType.Unreached || flag == Data.FlagType.Opcode || flag == Data.FlagType.Operand)) keepGoing = false;
                }
            }
            return newOffset;
        }

        public static int Mark(int offset, Data.FlagType type, int count)
        {
            Project.unsavedChanges = true;
            int i, size = Data.GetROMSize();
            for (i = 0; i < count && offset + i < size; i++) Data.SetFlag(offset + i, type);
            return offset + i < size ? offset + i : size - 1;
        }

        public static int MarkDataBank(int offset, int db, int count)
        {
            Project.unsavedChanges = true;
            int i, size = Data.GetROMSize();
            for (i = 0; i < count && offset + i < size; i++) Data.SetDataBank(offset + i, db);
            return offset + i < size ? offset + i : size - 1;
        }

        public static int MarkDirectPage(int offset, int dp, int count)
        {
            Project.unsavedChanges = true;
            int i, size = Data.GetROMSize();
            for (i = 0; i < count && offset + i < size; i++) Data.SetDirectPage(offset + i, dp);
            return offset + i < size ? offset + i : size - 1;
        }

        public static int MarkXFlag(int offset, bool x, int count)
        {
            Project.unsavedChanges = true;
            int i, size = Data.GetROMSize();
            for (i = 0; i < count && offset + i < size; i++) Data.SetXFlag(offset + i, x);
            return offset + i < size ? offset + i : size - 1;
        }

        public static int MarkMFlag(int offset, bool m, int count)
        {
            Project.unsavedChanges = true;
            int i, size = Data.GetROMSize();
            for (i = 0; i < count && offset + i < size; i++) Data.SetMFlag(offset + i, m);
            return offset + i < size ? offset + i : size - 1;
        }

        public static int MarkArchitechture(int offset, Data.Architechture arch, int count)
        {
            Project.unsavedChanges = true;
            int i, size = Data.GetROMSize();
            for (i = 0; i < count && offset + i < size; i++) Data.SetArchitechture(offset + i, arch);
            return offset + i < size ? offset + i : size - 1;
        }

        public static int GetInstructionLength(int offset)
        {
            switch (Data.GetArchitechture(offset))
            {
                case Data.Architechture.CPU65C816: return CPU65C816.GetInstructionLength(offset);
                case Data.Architechture.APUSPC700: return 1;
                case Data.Architechture.GPUSuperFX: return 1;
            }
            return 1;
        }

        public static int FixMisalignedFlags()
        {
            int count = 0, size = Data.GetROMSize();

            for (int i = 0; i < size; i++)
            {
                Data.FlagType flag = Data.GetFlag(i);

                if (flag == Data.FlagType.Opcode)
                {
                    int len = GetInstructionLength(i);
                    for (int j = 1; j < len && i + j < size; j++)
                    {
                        if (Data.GetFlag(i + j) != Data.FlagType.Operand)
                        {
                            Data.SetFlag(i + j, Data.FlagType.Operand);
                            count++;
                        }
                    }
                    i += len - 1;
                } else if (flag == Data.FlagType.Operand)
                {
                    Data.SetFlag(i, Data.FlagType.Opcode);
                    count++;
                    i--;
                } else if (Util.TypeStepSize(flag) > 1)
                {
                    int step = Util.TypeStepSize(flag);
                    for (int j = 1; j < step; j++)
                    {
                        if (Data.GetFlag(i + j) != flag)
                        {
                            Data.SetFlag(i + j, flag);
                            count++;
                        }
                    }
                    i += step - 1;
                }
            }

            if (count > 0) Project.unsavedChanges = true;

            return count;
        }

        public static void RescanInOutPoints()
        {
            for (int i = 0; i < Data.GetROMSize(); i++) Data.ClearInOutPoint(i);

            for (int i = 0; i < Data.GetROMSize(); i++)
            {
                if (Data.GetFlag(i) == Data.FlagType.Opcode)
                {
                    switch (Data.GetArchitechture(i))
                    {
                        case Data.Architechture.CPU65C816: CPU65C816.MarkInOutPoints(i); break;
                        case Data.Architechture.APUSPC700: break;
                        case Data.Architechture.GPUSuperFX: break;
                    }
                }
            }

            Project.unsavedChanges = true;
        }
        
        public static int ImportUsageMap(byte[] usageMap)
        {
            int size = Data.GetROMSize();
            bool unsaved = false;
            int modified = 0;
            int prevFlags = 0;

            for (int map = 0; map <= 0xFFFFFF; map++)
            {
                var i = Util.ConvertSNEStoPC(map);

                if (i == -1 || i >= size)
                {
                    // branch predictor may optimize this
                    continue;
                }

                var flags = (Data.BsnesPlusUsage)usageMap[map];

                if (flags == 0)
                {
                    // no information available
                    continue;
                }

                if (Data.GetFlag(i) != Data.FlagType.Unreached)
                {
                    // skip if there is something already set..
                    continue;
                }

                // opcode: 0x30, operand: 0x20
                if (flags.HasFlag(Data.BsnesPlusUsage.UsageExec))
                {
                    Data.SetFlag(i, Data.FlagType.Operand);

                    if (flags.HasFlag(Data.BsnesPlusUsage.UsageOpcode))
                    {
                        prevFlags = ((int)flags & 3) << 4;
                        Data.SetFlag(i, Data.FlagType.Opcode);
                    }

                    Data.SetMXFlags(i, prevFlags);
                    unsaved = true;
                    modified++;
                }
                else if (flags.HasFlag(Data.BsnesPlusUsage.UsageRead))
                {
                    Data.SetFlag(i, Data.FlagType.Data8Bit);
                    unsaved = true;
                    modified++;
                }
            }

            Project.unsavedChanges |= unsaved;
            return modified;
        }

        public static int ImportTraceLog(string[] lines)
        {
            // Must follow this format.
            // 028cde rep #$30               A:0004 X:0000 Y:0004 S:1fdd D:0000 DB:02 nvmxdiZC V:133 H: 654 F:36
            bool unsaved = false;
            int modified = 0;
            int size = Data.GetROMSize();

            foreach (var line in lines)
            {
                if (line.Length < 80)
                {
                    continue;
                }

                // TODO: error treatment
                // TODO: parse MX flags
                int directPageIndex = line.IndexOf("D:") + 2;
                int dataBankIndex = line.IndexOf("DB:") + 3;

                int snesAddress = Convert.ToInt32(line.Substring(0, 6), 16);
                int directPage = Convert.ToInt32(line.Substring(directPageIndex, 4), 16);
                int dataBank = Convert.ToInt32(line.Substring(dataBankIndex, 2), 16);

                int pc = Util.ConvertSNEStoPC(snesAddress);

                if (pc == -1)
                {
                    continue;
                }

                Data.SetFlag(pc, Data.FlagType.Opcode);
                
                do
                {
                    Data.SetDataBank(pc, dataBank);
                    Data.SetDirectPage(pc, directPage);
                    pc++;
                    unsaved = true;
                    modified++;
                } while (pc < size && Data.GetFlag(pc) == Data.FlagType.Operand);
            }

            Project.unsavedChanges |= unsaved;
            return modified;
        }
    }
}
