﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ntrclient.Prog.CS;
using ntrclient.Prog.Window;

namespace ntrclient.Extra
{
    public class Gateshark
    {
        private readonly List<GatesharkAr> _lines = new List<GatesharkAr>();
        private uint _offset;
        private uint _dxData;
        private bool _loop;
        private int _loopIndex;
        private uint _loopCount;

        public Gateshark(string code)
        {
            string[] l = Regex.Split(code, "\r\n|\r|\n");
            foreach (string line in l)
            {
                _lines.Add(new GatesharkAr(line));

            }
        }

        public static void Addlog(string n)
        {
            Program.GCmdWindow.Addlog(n);
        }

        public void Execute()
        {
            int index = 0;
            int dummyCount = 0;
            bool gsIf = true;
            bool valid = true;
            int gsIfLayer = 0;
            int gsIfSLayer = 0;
            do
            {
                GatesharkAr gsAr = _lines[index];
                uint cmd = gsAr.GetCmd();

                if (cmd != 0xff)
                    Addlog(string.Format("GS | {0:X} {1:X} {2:X} -> [{3}, {4}, {5}, {6:X}]", cmd, gsAr.getBlock_A(), gsAr.getBlock_B(), valid, gsIfLayer, gsIfSLayer, _offset));

                if (gsIfLayer == 0 && valid)
                {

                    if ((cmd == 0) || (cmd == 1) || (cmd == 2))
                    {
                        Program.Sm.GsUsed += 1;
                        valid = gsAr.Execute(_offset);

                    }
                    // Conditional codes
                    else if (cmd == 0x3)
                    {
                        uint read = Convert.ToUInt32(CmdWindow.FromLe(Program.GCmdWindow.readValue(gsAr.getBlock_A() + _offset, 4), 0));
                        gsIf = read < gsAr.getBlock_B();
                    }
                    else if (cmd == 0x4)
                    {
                        uint r1 = Program.GCmdWindow.readValue(gsAr.getBlock_A() + _offset, 4);
                        uint r2 = CmdWindow.FromLe(r1, 0);
                        
                        uint read = Convert.ToUInt32(r2);
                        gsIf = read > gsAr.getBlock_B();
                    }
                    else if (cmd == 0x5)
                    {
                        uint read = Convert.ToUInt32(CmdWindow.FromLe(Program.GCmdWindow.readValue(gsAr.getBlock_A() + _offset, 4), 0));
                        gsIf = read == gsAr.getBlock_B();
                    }
                    else if (cmd == 0x6)
                    {
                        uint read = Convert.ToUInt32(CmdWindow.FromLe(Program.GCmdWindow.readValue(gsAr.getBlock_A() + _offset, 4), 0));
                        gsIf = read != gsAr.getBlock_B();
                    }
                    else if (cmd == 0x7)
                    {
                        uint read = Convert.ToUInt32(CmdWindow.FromLe(Program.GCmdWindow.readValue(gsAr.getBlock_A() + _offset, 2), 2));
                        gsIf = read < gsAr.getBlock_B();
                    }
                    else if (cmd == 0x8)
                    {
                        uint read = Convert.ToUInt32(CmdWindow.FromLe(Program.GCmdWindow.readValue(gsAr.getBlock_A() + _offset, 2), 2));
                        gsIf = read > gsAr.getBlock_B();
                    }
                    else if (cmd == 0x9)
                    {
                        uint read = Convert.ToUInt32(CmdWindow.FromLe(Program.GCmdWindow.readValue(gsAr.getBlock_A() + _offset, 2), 2));
                        gsIf = read == gsAr.getBlock_B();
                    }
                    else if (cmd == 0xA)
                    {
                        uint read = Convert.ToUInt32(CmdWindow.FromLe(Program.GCmdWindow.readValue(gsAr.getBlock_A() + _offset, 2), 2));
                        gsIf = read != gsAr.getBlock_B();
                    }
                    else if (cmd == 0xB)
                    {
                        _offset = CmdWindow.FromLe(Program.GCmdWindow.readValue(gsAr.getBlock_A() + _offset, 4), 4);
                    }
                    else if (cmd == 0xC)
                    {
                        _loop = true;
                        _loopIndex = index;
                        _loopCount = gsAr.getBlock_B() + 1;
                    }
                    else if (cmd == 0xD1)
                    {
                        if (_loop)
                        {
                            _loopCount--;
                            if (_loopCount == 0)
                            {
                                _loop = false;
                            }
                            else
                            {
                                index = _loopIndex;
                                _offset += Convert.ToUInt32(gsAr.getBlock_B());
                            }
                        }
                    }
                    else if (cmd == 0xD2) // RESET
                    {
                        _loop = false;
                        _loopCount = 0;
                        _loopIndex = 0;

                        _offset = 0;
                        gsIfLayer = 0;
                        gsIfSLayer = 0;
                    }
                    else if (cmd == 0xD3) // Read offset
                    {
                        // Fix for Issue #8
                        _offset = gsAr.getBlock_B();
                    }
                    else if (cmd == 0xD4)
                    {
                        // Fix for Issue #8
                        uint b = gsAr.getBlock_B();

                        _dxData += b;
                    }
                    else if (cmd == 0xD5) // DxData WRITE
                    {
                        _dxData = gsAr.getBlock_B();
                    }
                    else if (cmd == 0xD6) // DxData WORD
                    {
                        uint len = 4;
                        string cmdString = Program.GCmdWindow.GenerateWriteString(gsAr.getBlock_B() + _offset, _dxData, len);
                        Program.GCmdWindow.RunCmd(cmdString);
                        _offset += len;
                    }
                    else if (cmd == 0xD7) // DxData SHORT
                    {
                        uint len = 2;
                        string cmdString = Program.GCmdWindow.GenerateWriteString(gsAr.getBlock_B() + _offset, _dxData, len);
                        Program.GCmdWindow.RunCmd(cmdString);
                        _offset += len;
                    }
                    else if (cmd == 0xD8) // DxData Byte
                    {
                        uint len = 1;
                        string cmdString = Program.GCmdWindow.GenerateWriteString(gsAr.getBlock_B() + _offset, _dxData, len);
                        Program.GCmdWindow.RunCmd(cmdString);
                        _offset += len;
                    }
                    else if (cmd == 0xD9) // DxData READ WORD
                    {
                        uint addr = gsAr.getBlock_B() + _offset;
                        _dxData = CmdWindow.FromLe(Program.GCmdWindow.readValue(addr, 4), 4);
                    }
                    else if (cmd == 0xDA) // DxData READ SHORT
                    {
                        uint addr = gsAr.getBlock_B() + _offset;
                        _dxData = CmdWindow.FromLe(Program.GCmdWindow.readValue(addr, 2), 2);
                    }
                    else if (cmd == 0xDB) // DxData READ BYTE
                    {
                        uint addr = gsAr.getBlock_B() + _offset;
                        _dxData = Program.GCmdWindow.readValue(addr, 1);
                    }
                    else if (cmd == 0xDC)
                    {
                        // Fix for Issue #8
                        uint b = gsAr.getBlock_B();

                        _offset += b;
                    }
                    else if (cmd == 0xDF)
                    {
                        // This doesn't actually exist! It's for testing only!
                        dummyCount++;
                        MessageBox.Show(string.Format(
                            "I: {0} \r\n" +
                            "O: {1:X} \r\n" +
                            "LOOP: {2} {3} {4} \r\n" +
                            "DX: {5:X} \r\n" +
                            "DUMMY: {6}\r\n" +
                            "LAYERS: {7} {8}"
                            , index, _offset, _loop, _loopIndex, _loopCount, _dxData, dummyCount, gsIfSLayer, gsIfLayer));
                    }

                    if (!gsIf)
                    {
                        gsIf = true;
                        gsIfLayer += 1;
                    }
                }
                else if (cmd >= 0x3 && cmd <= 0xA)
                {
                    gsIfSLayer += 1;
                }
                else if (cmd == 0xD0)
                {
                    if (gsIfSLayer > 0)
                        gsIfSLayer -= 1;
                    else if (gsIfLayer > 0)
                        gsIfLayer -= 1;
                }
                else if (cmd == 0xD2)
                {
                    _loop = false;
                    _loopCount = 0;
                    _loopIndex = 0;

                    _offset = 0;
                    gsIfLayer = 0;
                    gsIfSLayer = 0;
                }
                index++;
            } while (index < _lines.Count);
        }

        public GatesharkAr[] GetAllCodes()
        {
            return _lines.ToArray();
        }
    }

    public class GatesharkAr
    {
        public string Line { get; }
        private readonly uint _cmd;
        private readonly uint _blockA;
        private readonly uint _blockB;
        public string Replace { get; }

        public GatesharkAr(string ar)
        {
            Line = ar;
            string[] blocks = ar.Split(' ');

            if (ar.Length != 17)
            {
                _cmd = 0xff;
                _blockA = 0x0fffffff;
                _blockB = 0x7fffffff;
                return;
            }
            Replace = ar.Replace(" ", string.Empty);
            _cmd = Convert.ToUInt32(ar[0].ToString(), 16);
            /*

            0   Write   Word
            1   Write   Short
            2   Write   Byte

            3   X > Y
            4   X < Y
            5   X = Y
            6   X ~ Y

            B   OFFSET = READ(X)
            D3  OFFSET = X

            */

            if (_cmd == 0xD)
            {
                _cmd = 0xD0;
                _cmd += Convert.ToUInt32(ar[1].ToString(), 16); // DX codes
                _blockA = 0x0;
            }
            else
            {
                _blockA = Convert.ToUInt32(blocks[0], 16);
                _blockA -= _cmd * 0x10000000;
            }


            _blockB = Convert.ToUInt32(blocks[1], 16);

        }

        public uint GetCmd()
        {
            return _cmd;
        }

        public uint getBlock_A()
        {
            return _blockA;
        }

        public uint getBlock_B()
        {
            return _blockB;
        }

        public bool Execute(uint offset)
        {
            if ((_cmd == 0) || (_cmd == 1) || (_cmd == 2))
            {
                uint len = 4;
                if (_cmd == 1) len = 2;
                else if (_cmd == 2) len = 1;
                if (Program.GCmdWindow.IsMemValid(_blockA + offset))
                {
                    var cmdString = Program.GCmdWindow.GenerateWriteString(_blockA + offset, _blockB, len);
                    Program.GCmdWindow.RunCmd(cmdString);
                    return true;
                }
            }
            return false;
        }
    }
}