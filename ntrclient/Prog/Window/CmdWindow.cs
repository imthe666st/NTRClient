﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ntrclient.Extra;
using ntrclient.Prog.CS;
using ntrclient.Prog.CS.GitHub;
using Octokit;
using System.Threading;

namespace ntrclient.Prog.Window
{
    public partial class CmdWindow : Form
    {

        private String version = @"V1.7-1";
        private String release = @"Public Version";
        private bool enableUpdater = true;

        //________________________________________________________________
        // System

        private void UpdateProgress()
        {
            if (Program.NtrClient.Progress != -1)
            {
                progressBar1.Value = Program.NtrClient.Progress;
            } else
            {
                progressBar1.Value = 0;
            }
        }

        private bool _lookedForUpdate;

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                UpdateProgress();
                Program.NtrClient.SendHeartbeatPacket();

                if (_hbc != null)
                {
                    label_heart_status.Text = _hbc.Status()
                        ? @"Heartbeat status: Running"
                        : @"Heartbeat status: OFFLINE";
                }
                else
                    label_heart_status.Text = @"Heartbeat status: OFFLINE";

                // Update check
                if (_lookedForUpdate == false)
                {
                    LookForUpdate();
                }
            }
            catch (Exception e_)
            {
                BugReporter br = new BugReporter(e_, "Timer exception");
                
            }
        }

        private void disconnectTimer_Tick(object sender, EventArgs e)
        {
            RunCmd("Disconnect()");
            disconnectTimer.Enabled = false;
        }

        public bool UpdateAvailable { get; private set; }

        private async void LookForUpdate()
        {
            _lookedForUpdate = true;
            if (enableUpdater)
            {
                Release upd = await Octo.GetLastUpdate();
                if (upd.TagName != version && upd.TagName != "ERROR" && !upd.Prerelease && !upd.Draft)
                {
                    string nVersion = Octo.GetLastVersionName();
                    string nBody = Octo.GetLastVersionBody();
                    MessageBox.Show(@"A new Update has been released! " + nVersion + Environment.NewLine + nBody);
                    checkingUpdateToolStripMenuItem.Text = @"Update available!";
                    UpdateAvailable = true;
                    Program.Dc.Addlog("Found a new Update - " + nVersion);
                }
                else
                {
                    //MessageBox.Show("No new release found!");
                    UpdateAvailable = false;
                    checkingUpdateToolStripMenuItem.Text = @"No new Update!";
                    Program.Dc.Addlog("No Update found");
                }
            } else
            {
                checkingUpdateToolStripMenuItem.Text = @"Autoupdater disabled";
            }
        }

        private void CmdWindow_Load(object sender, EventArgs e)
        {
            ResetLog();
            
            this.VersionNumberToolStripMenuItem.Text = version;
            this.VersionExtraToolStripMenuItem.Text = release;

            // Remove all errorreports. 
            DirectoryInfo di = new DirectoryInfo("bugreports");

            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }

            textBox_Ip.Text = Program.Sm.IpAddress;
            if (Program.Sm.IpAddress != "Nintendo 3DS IP")
            {
                Addlog("Loaded your last IP address!");
            }

            // Start Heartbeat
            _hbc = new HeartbeatController();
            _hbc.Start();

            // Start Octo
            Octo.Init();
        }

        private void CmdWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            Program.SaveConfig();
            Program.NtrClient.Disconnect();
            _hbc?.Stop();
        }


        // END of System

        // Logging
        public delegate void LogDelegate(string l);

        public LogDelegate DelAddLog;

        public CmdWindow()
        {
            DelAddLog = Addlog;

            InitializeComponent();
        }

        public delegate void DelegateLogAppend(string l);

        public void Addlog(string l)
        {
            if (!l.Contains("\r\n"))
            {
                l = l.Replace("\n", "\r\n");
            }
            if (!l.EndsWith("\n"))
            {
                l += "\r\n";
            }

            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new DelegateLogAppend(txtLog.AppendText), l);
            }
            else
            {
                txtLog.AppendText(l);
            }
        }

        public String GetLog()
        {
            return txtLog.Text;
        }

        // END of Logging

        //________________________________________________________________

        // Memregions

        public List<Memregion> Memregions = new List<Memregion>();

        public bool IsMemValid(uint v)
        {
            Memregion[] mems = Memregions.ToArray();
            return mems.Any(m => m.Contains(v));
        }

        public void SetMemregions(string memlayout)
        {
            if (!memlayout.Contains("\r\n"))
            {
                memlayout = memlayout.Replace("\n", "\r\n");
            }
            textBox_memlayout.Text = memlayout;
        }

        public delegate void SetMemregionsCallback(string memlayout);

        public void GenerateMemregions()
        {
            string layout = textBox_memlayout.Text;
            Regex regex = new Regex("\r\n");
            string[] lines = regex.Split(layout);

            Memregions.Clear();
            comboBox_memregions.Items.Clear();

            foreach (string mem in lines)
            {
                string[] memParts = mem.Split(' ');
                if (memParts.Length == 6)
                {
                    if (
                        memParts[1] == "-" &&
                        memParts[3] == "," &&
                        memParts[4] == "size:"
                        )
                    {
                        Memregion memregion = new Memregion(mem);
                        Memregions.Add(memregion);
                        uint start = memregion.Start;
                        uint end = memregion.End;
                        uint length = memregion.Length;
                        comboBox_memregions.Items.Add(string.Format("{0:X} -> {1:X} [{2:X}]", start, end, length));
                    }
                }
            }

            comboBox_memregions.SelectedIndex = 0;
        }

        private void textBox_memlayout_TextChanged(object sender, EventArgs e)
        {
            GenerateMemregions();
        }

        // END of Memregions

        //________________________________________________________________

        // Processes

        public List<NtrProcess> Processes = new List<NtrProcess>();

        public void SetProcesses(string p)
        {
            if (!p.Contains("\r\n"))
            {
                p = p.Replace("\n", "\r\n");
            }
            textBox_processes.Text = p;
        }

        public delegate void SetProcessesCallback(string p);

        private void textBox_processes_TextChanged(object sender, EventArgs e)
        {
            GenerateProcesses();
        }

        private void GenerateProcesses()
        {
            string layout = textBox_processes.Text;
            Regex regex = new Regex("\r\n");
            string[] lines = regex.Split(layout);


            Processes.Clear();
            comboBox_processes.Items.Clear();

            foreach (string p in lines)
            {
                string[] pParts = p.Split(' ');
                if (pParts.Length >= 8)
                {
                    int len = pParts.Length;
                    if (
                        pParts[0] == "pid:" &&
                        pParts[2] == "pname:" &&
                        pParts[len - 4] == "tid:" &&
                        pParts[len - 2] == "kpobj:"
                        )
                    {
                        NtrProcess np = new NtrProcess(p);
                        Processes.Add(np);
                        int pid = np.Pid;
                        string name = np.Name;
                        comboBox_processes.Items.Add(string.Format("{0} | {1:X} : {2}", FillString(CheckSystem(name), 6),
                            pid, name));
                    }
                }
                comboBox_processes.SelectedIndex = 0;
            }
        }

        public string FillString(string n, int len)
        {
            int ls = len - n.Length;
            if (ls <= 0) return n;
            for (int i = 0; i < ls; i++)
            {
                n += " ";
            }
            return n;
        }

        public string CheckSystem(string n)
        {
            string[] sys =
            {
                "fs",
                "loader",
                "pm",
                "sm",
                "pxi",
                "ns",
                "ptm",
                "cfg",
                "gpio",
                "i2c",
                "mcu",
                "pdn",
                "spi",
                "ps",
                "ps",
                "ErrDisp",
                "menu",
                "hid",
                "codec",
                "dsp",
                "am",
                "gsp",
                "qtm",
                "camera",
                "csnd",
                "mic",
                "ir",
                "nwm",
                "socket",
                "http",
                "ssl",
                "cecd",
                "friends",
                "ac",
                "boss",
                "act",
                "news",
                "ndm",
                "nim",
                "dlp",
                "ro",
                "nfc",
                "swkbd"
            };

            if (sys.Any(sysTitle => sysTitle == n))
            {
                return "System";
            }

            return "Game";
        }


        public delegate object DelComboBoxItem(ComboBox c);

        public object GetComboItem(ComboBox c)
        {
            return c.SelectedItem;
        }

        public delegate object DelComboBoxIndex(ComboBox c);

        public object GetComboIndex(ComboBox c)
        {
            return c.SelectedIndex;
        }

        public int GetPid()
        {
            if (comboBox_processes.InvokeRequired)
            {
                var o = comboBox_processes.Invoke(new DelComboBoxItem(GetComboItem), comboBox_processes);
                if (o != null)
                {
                    string s = o.ToString();
                    string[] ss = s.Split(' ');
                    int len = ss.Length;

                    int pid = Convert.ToInt32(ss[len - 3], 16);

                    return pid;
                }
                return 0x7FFFFFFF;
            }
            if (GetComboItem(comboBox_processes) != null)
            {
                string s = comboBox_processes.SelectedItem.ToString();
                string[] ss = s.Split(' ');
                int len = ss.Length;

                int pid = Convert.ToInt32(ss[len - 3], 16);

                return pid;
            }
            return 0x7FFFFFFF;
        }

        // END of Processes

        //________________________________________________________________

        // Handle Commands

        public uint ReadValue = 0xdeadbeef;


        public void SetReadValue(uint r)
        {
            ReadValue = r;
        }

        // ReSharper disable once InconsistentNaming
        public uint readValue(uint addr, uint size)
        {
            Addlog("Started readValue(int, int)");
            if (size < 1)
                size = 1;

            RunCmd(string.Format("Read(0x{0:X}, 0x{1:X}, pid=0x{2:X})", addr, size, GetPid()));
            int retry = 0;
            while (ReadValue == 0xdeadbeef && retry < 300000)
            {
                Task.Delay(25);
                retry++;
            }
            //if (retry >= 300000)
            //    Addlog("[READ ERROR] COULDN'T READ FAST ENOUGH!");
            //if (ReadValue == 0xdeadbeef)
            //    ReadValue = 0;
            uint v = ReadValue;
            ReadValue = 0xdeadbeef;
            return v;
        }

        public string RunCmd(string cmd)
        {
            try
            {
                Addlog("> " + cmd);
                object ret = Program.PyEngine.CreateScriptSourceFromString(cmd).Execute(Program.GlobalScope);
                if (ret != null)
                {
                    Addlog(ret.ToString());
                    return ret.ToString();
                }
                    Addlog("null");
                    return "";
                }
            catch (Exception ex)
            {
                Addlog(ex.Message);
                BugReporter br = new BugReporter(ex, "Command execution exception", false);
                return "";
            }
        }

        private void txtCmd_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == System.Windows.Forms.Keys.Enter)
            {
                string cmd = txtCmd.Text;
                txtCmd.Clear();
                RunCmd(cmd);
            }
        }

        // END of Handle Commands

        //________________________________________________________________

        // Utilities

        public void RunMemlayoutCmd()
        {
            RunCmd(string.Format("Memlayout(pid=0x{0:X})", GetPid()));
        }

        public void RunProcessesCmd()
        {
            RunCmd("Listprocess()");
        }

        private static int GetInt(string l)
        {
            return Convert.ToInt32(l, 10);
        }

        // ReSharper disable once UnusedMember.Local
        private string ToHex(int v)
        {
            return string.Format("{0:X}", v);
        }

        public static uint FromLe(string hexLe)
        {
            uint temp;
            if (hexLe.Length == 4)
            {
                temp = Convert.ToUInt16(hexLe, 16);
                return FromLe(temp, 2);
            }
            if (hexLe.Length == 2)
            {
                temp = Convert.ToUInt32(hexLe, 16);
                return temp;
            }
            temp = Convert.ToUInt32(hexLe, 16);
            return FromLe(temp, 4);
        }

        public static uint FromLe(int temp, int len)
        {
            byte[] bytes = BitConverter.GetBytes(temp);

            Array.Reverse(bytes);
            if (len == 2)
            {
                uint le = BitConverter.ToUInt16(bytes, 2);
                if (le >= 0) return le;
                uint ret = le + 0x10000;
                return ret;
            }
            if (len == 1)
                return bytes[bytes.Length - 1];

            return BitConverter.ToUInt32(bytes, 0);
        }

        public static uint FromLe(uint temp, int len)
        {
            byte[] bytes = BitConverter.GetBytes(temp);

            Array.Reverse(bytes);
            if (len == 2)
            {
                uint le = BitConverter.ToUInt16(bytes, 2);
                if (le >= 0) return le;
                uint ret = le + 0x10000;
                return ret;
            }
            if (len == 1)
                return bytes[bytes.Length - 1];

            return BitConverter.ToUInt32(bytes, 0);
        }

        public void ResetLog()
        {
            txtLog.Text = "";

            Addlog("NTR debugger by cell9 - Mod by imthe666st - project cleanup by Shadowtrance");
            RunCmd("import sys;sys.path.append('.\\python\\Lib')");
            RunCmd("for n in [n for n in dir(nc) if not n.startswith('_')]: globals()[n] = getattr(nc,n)    ");
            Addlog("Commands available: ");
            RunCmd("repr([n for n in dir(nc) if not n.startswith('_')])");
        }

        // Generating Hex chunks


        public string GenerateHexChunk(uint value, uint length)
        {
            string data = "(";
            byte[] bytes = BitConverter.GetBytes(value);
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (i < length - 1)
                {
                    data += string.Format("0x{0:X}, ", b);
                }
                else
                {
                    data += string.Format("0x{0:X}", b);
                    break;
                }
            }
            return data + ")";
        }

        public static string GenerateHexChunk(int value, int length)
        {
            string data = "(";
            byte[] bytes = BitConverter.GetBytes(value);
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (i < length - 1)
                {
                    data += string.Format("0x{0:X}, ", b);
                }
                else
                {
                    data += string.Format("0x{0:X}", b);
                    break;
                }
            }
            return data + ")";
        }

        public string GenerateHexChunk(uint value, int length)
        {
            string data = "(";
            byte[] bytes = BitConverter.GetBytes(value);
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (i < length - 1)
                {
                    data += string.Format("0x{0:X}, ", b);
                }
                else
                {
                    data += string.Format("0x{0:X}", b);
                    break;
                }
            }
            return data + ")";
        }

        public string GenerateHexChunk(int value, uint length)
        {
            string data = "(";
            byte[] bytes = BitConverter.GetBytes(value);
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (i < length - 1)
                {
                    data += string.Format("0x{0:X}, ", b);
                }
                else
                {
                    data += string.Format("0x{0:X}", b);
                    break;
                }
            }
            return data + ")";
        }

        // gen Write Strings

        public string GenerateWriteString(int addr, int value, uint length)
        {
            string data = GenerateHexChunk(value, length);

            return string.Format("Write(0x{0:X}, {1}, pid=0x{2:X})", addr, data, GetPid());
        }

        public string GenerateWriteString(uint addr, uint value, uint length)
        {
            string data = GenerateHexChunk(value, length);
            return string.Format("Write(0x{0:X}, {1}, pid=0x{2:X})", addr, data, GetPid());
        }

        public string GenerateWriteString(int addr, uint value, uint length)
        {
            string data = GenerateHexChunk(value, length);
            return string.Format("Write(0x{0:X}, {1}, pid=0x{2:X})", addr, data, GetPid());
        }

        public string GenerateWriteString(uint addr, int value, uint length)
        {
            string data = GenerateHexChunk(value, length);
            return string.Format("Write(0x{0:X}, {1}, pid=0x{2:X})", addr, data, GetPid());
        }

        public void startAutoDisconnect()
        {
            disconnectTimer.Enabled = true;
        }

        // END of Utilities

        //________________________________________________________________

        // Tool Strip

        private void ToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void CommandToolStripMenuItem_Click(object sender, EventArgs e)
        {
            (new QuickCmdWindow()).Show();
        }

        private void CmdWindow_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
        }


        public readonly int[] Keys = {38, 38, 40, 40, 37, 39, 37, 39, 66, 65, 13};
        private int _kPos;

        private void CmdWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                int t = e.KeyValue;
                if (t >= 48 && t <= 57)
                {
                    RunCmd(Program.Sm.QuickCmds[t - 48]);
                    e.SuppressKeyPress = true;
                }
                else if (t == Keys[_kPos])
                {
                    _kPos++;
                    label_kpos.Text = string.Format("KPOS: {0}", _kPos);
                    if (_kPos == Keys.Length)
                    {
                        MessageBox.Show(@"This is an easteregg");
                        _kPos = 0;
                    }
                }
                else
                {
                    _kPos = 0;
                }
            }
        }

        private void asmScratchPadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            (new AsmEditWindow()).Show();
        }

        private void moreCreditsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Credits c = new Credits();
            c.Show();
            c.Focus();
        }

        private void githubRepositoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Browser.OpenUrl("https://github.com/imthe666st/NTRClient");
        }

        private void toggleDebugToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tabPage_main_debug.Enabled = !tabPage_main_debug.Enabled;
        }

        private void clearLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ResetLog();
        }

        private void clearHeartbeatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_hbc != null)
            {
                _hbc.SetCode("");
                Addlog("Cleared Heartbeat command");
            }
            else
            {
                Addlog("Unable to clear Heartbear command");
            }
        }

        private void redditThreadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Browser.OpenUrl(
                "https://www.reddit.com/r/3dshacks/comments/45iz4o/ntr_improved_ntr_debugger_client_with_gateshark/");
        }

        private void gbaTempThreadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Browser.OpenUrl("http://gbatemp.net/threads/modified-ntr-client-with-gateshark-support.418294/");
        }

        private void checkingUpdateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Browser.OpenUrl("https://github.com/imthe666st/NTRClient/releases");
        }

        private void openConsoleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.Dc.Show();
        }

        // END of ToolStrip

        //________________________________________________________________

        // BASIC Tabpage

        private void button_Connect_Click(object sender, EventArgs e)
        {
            RunCmd("Connect('" + textBox_Ip.Text + "', 8000)");
            Program.Sm.IpAddress = textBox_Ip.Text;
        }

        private void button_processes_Click(object sender, EventArgs e)
        {
            RunCmd("Listprocess()");
        }

        private void comboBox_processes_SelectedIndexChanged(object sender, EventArgs e)
        {
            RunMemlayoutCmd();
        }

        private void button_hello_Click(object sender, EventArgs e)
        {
            RunCmd("Sayhello()");
        }

        private void button_dump_Click(object sender, EventArgs e)
        {
            string filename = textBox_dump_file.Text;
            Memregion mem = Memregions[comboBox_memregions.SelectedIndex];
            RunCmd(string.Format("Data(0x{0:X}, 0x{1:X}, filename='{2}', pid=0x{3:X})", mem.Start, mem.Length, filename,
                GetPid()));
        }

        private void button_disconnect_Click(object sender, EventArgs e)
        {
            RunCmd("Disconnect()");
        }

        private void button_dummy_read_Click(object sender, EventArgs e)
        {
            uint addr = Convert.ToUInt32(textBox_dummy_addr.Text, 16);
            uint v = readValue(addr, (uint) numericUpDown_dummy_length.Value);
            textBox_dummy_value_hex.Text = string.Format("{0:X}", v);
            textBox_dummy_value_hex_le.Text = string.Format("{0:X}", FromLe(v, (int) numericUpDown_dummy_length.Value));
            textBox_dummy_value_dec.Text = string.Format("{0}", FromLe(v, (int) numericUpDown_dummy_length.Value));
        }

        private void button_dummy_write_Click(object sender, EventArgs e)
        {
            try
            {
                int addr = Convert.ToInt32(textBox_dummy_addr.Text, 16);
                uint v = FromLe(Convert.ToUInt32(textBox_dummy_value_hex.Text, 16), (int)numericUpDown_dummy_length.Value);
                RunCmd(GenerateWriteString(addr, v, (uint)numericUpDown_dummy_length.Value));
            } catch ( Exception ex)
            {
                BugReporter br = new BugReporter(ex, @"Dummy HEX write Exception [" + textBox_dummy_addr.Text + @" -> " + textBox_dummy_value_hex + @"]");
            }
        }

        private void button_dummy_write_hex_le_Click(object sender, EventArgs e)
        {
            try
            {
                int addr = Convert.ToInt32(textBox_dummy_addr.Text, 16);
                uint v = Convert.ToUInt32(textBox_dummy_value_hex_le.Text, 16);
                RunCmd(GenerateWriteString(addr, v, (uint) numericUpDown_dummy_length.Value));
            } catch ( Exception ex)
            {
                BugReporter br = new BugReporter(ex, @"Dummy HEX write Exception [" + textBox_dummy_addr.Text + @" -> " + textBox_dummy_value_hex + @"]");
            }
        }

        private void button_dummy_write_dec_Click(object sender, EventArgs e)
        {
            try
            {
                int addr = Convert.ToInt32(textBox_dummy_addr.Text, 16);
                uint v = Convert.ToUInt32(textBox_dummy_value_dec.Text, 10);
                RunCmd(GenerateWriteString(addr, v, (uint) numericUpDown_dummy_length.Value));
            } catch ( Exception ex)
            {
                BugReporter br = new BugReporter(ex, @"Dummy HEX write Exception [" + textBox_dummy_addr.Text + @" -> " + textBox_dummy_value_hex + @"]");
            }
        }

        // 

        //private void button_dump_all_Click2(object sender, EventArgs e)
        //{
        //    String filename = textBox_dump_file.Text;
        //    Memregion mem = Memregions[Memregions.Count - 1];
        //    RunCmd(String.Format("Data(0x{0:X}, 0x{1:X}, filename='{2}', pid=0x{3:X})", 0, mem.Start + mem.Length, filename, GetPid()));
        //}

        private void button_dump_all_Click(object sender, EventArgs e)
        {
            String filename = textBox_dump_file.Text;

            uint memPos = 0;
            int fileSuffix = 0;

            foreach (Memregion mem in Memregions)
            {
                if (mem.Start > memPos)
                {
                    File.WriteAllBytes(filename + fileSuffix.ToString(), new byte[mem.Start - memPos]);
                    fileSuffix++;

                }
                RunCmd(String.Format("Data(0x{0:X}, 0x{1:X}, filename='{2}', pid=0x{3:X})", mem.Start, mem.Length, filename + fileSuffix.ToString(), GetPid()));
                while (!File.Exists(filename + fileSuffix.ToString()))
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
                memPos = mem.Start + mem.Length;
                fileSuffix++;
            }

            string destFileName = filename;
            using (Stream destStream = File.OpenWrite(destFileName))
            {
                for (int i = 0; i < fileSuffix; i++)
                {

                    using (Stream srcStream = File.OpenRead(filename + i.ToString()))
                    {
                        srcStream.CopyTo(destStream);
                    }

                }
            }

            for (int i = 0; i < fileSuffix; i++)
            {
                File.Delete(filename + i.ToString());
            }


        }

        // END of Basic

        //________________________________________________________________

        // Gateshark development

        private void button_gateshark_parse_Click(object sender, EventArgs e)
        {
            textBox_gateshark_parsed.Text = string.Empty;
            Gateshark gs = new Gateshark(textBox_gateshark.Text);
            foreach (GatesharkAr gsAr in gs.GetAllCodes())
            {
                uint cmd = gsAr.GetCmd();
                uint blockA = gsAr.getBlock_A();
                uint blockB = gsAr.getBlock_B();
                string parsed = string.Format("{0:X} {1:X} {2:X}\r\n", cmd, blockA, blockB);
                textBox_gateshark_parsed.AppendText(parsed);
            }
        }

        private void button_gateshark_execute_Click(object sender, EventArgs e)
        {
            string code = textBox_gateshark.Text;
            Gateshark gs = new Gateshark(code);
            Addlog("Executing code...");
            gs.Execute();
        }

        private void button_gateshark_heartbeat_Click(object sender, EventArgs e)
        {
            if (_hbc != null)
            {
                Addlog("Set code as HB-Code");
                _hbc.SetCode(textBox_gateshark.Text);
            }
            else
                Addlog("Unable to set code as HB-Code");
        }

        private void button_browser_fort42_Click(object sender, EventArgs e)
        {
            Browser.OpenUrl("http://www.fort42.com/gateshark");
        }

        //________________________________________________________________

        // Debug Tab

        private HeartbeatController _hbc;

        private void button_heart_test_start_Click(object sender, EventArgs e)
        {
            _hbc = new HeartbeatController();
            _hbc.Start();
        }

        private void button_heart_test_stop_Click(object sender, EventArgs e)
        {
            _hbc?.Stop();
        }

        private void button_heart_test_inject_Click(object sender, EventArgs e)
        {
            _hbc?.SetCode(
                "D3000000 00000470\r\n" +
                "481C7CD0 08800000\r\n" +
                "381C7CD0 08D00000\r\n" +
                "B81C7CD0 00000000\r\n" +
                "00001184 32003200\r\n" +
                "10001188 00003200\r\n" +
                "0000118C 44610000\r\n" +
                "00001190 44610000\r\n" +
                "10001194 00003200\r\n" +
                "D2000000 00000000\r\n" +
                "D3000000 081C8170\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 00000E28\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 000003E8\r\n" +
                "10000000 00000001\r\n" +
                "D2000000 00000000\r\n" +
                "D3000000 081C8174\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 00000E28\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 000003E8\r\n" +
                "10000000 00000001\r\n" +
                "D2000000 00000000"
                );
        }

        private void button_heart_test_toggle_Click(object sender, EventArgs e)
        {
            _hbc?.Toggle();
        }

        private void button_cfg_set_dummy_Click(object sender, EventArgs e)
        {
            Program.Sm.IpAddress = textBox_cfg_set_dummy.Text;
        }

        private void button_cfg_read_dummy_Click(object sender, EventArgs e)
        {
            textBox_cfg_read_dummy.Text = Program.Sm.IpAddress;
        }

        // Little Endian test | Issue #11

        private void button_debug_conv_hex_Click(object sender, EventArgs e)
        {
            try
            {
                int v = Convert.ToInt32(textBox_debug_conv_hex.Text, 16);

                textBox_debug_conv_hex_le.Text = string.Format("{0:X}", FromLe(v, (int) numericUpDown_debug_hextest.Value));
                textBox_debug_conv_dec.Text = string.Format("{0}", v);
            } catch ( Exception ex )
            {
                BugReporter br = new BugReporter(ex, "Debug Convert HEX Exception");
            }
        }

        private void button_debug_conv_hex_le_Click(object sender, EventArgs e)
        {
            try
            {
                uint v = FromLe(Convert.ToInt32(textBox_debug_conv_hex_le.Text, 16), (int) numericUpDown_debug_hextest.Value);

                textBox_debug_conv_hex.Text = string.Format("{0:X}", v);
                textBox_debug_conv_dec.Text = string.Format("{0}", v);
            } catch ( Exception ex )
            {
                BugReporter br = new BugReporter(ex, "Debug Convert HEXLE Exception");
    }
}

        private void button_debug_conv_dec_Click(object sender, EventArgs e)
        {
            try
            {
                int v = Convert.ToInt32(textBox_debug_conv_dec.Text, 10);

                textBox_debug_conv_hex.Text = string.Format("{0:X}", v);
                textBox_debug_conv_hex_le.Text = string.Format("{0:X}", FromLe(v, (int) numericUpDown_debug_hextest.Value));
            } catch ( Exception ex )
            {
                BugReporter br = new BugReporter(ex, "Debug Convert DEC Exception");
            }
        }

        // Update tests

        private void button_update_Click(object sender, EventArgs e)
        {
            LookForUpdate();
        }

        private void button_pTest_Click(object sender, EventArgs e)
        {
            const string p = "pid: 0x00000008, pname:     gpio, tid: 0004013000001b02, kpobj: fff76fb0";
            // ReSharper disable once UnusedVariable
            NtrProcess np = new NtrProcess(p);
        }

        private void button_debug_rTime_Click(object sender, EventArgs e)
        {
            textBox_rTime.Text = Octo.GetLastVersionName();
        }

        private void button_toolstrip_debug_Click(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = @"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvxyz1234567890";
        }

        private void button_btn_input_Click(object sender, EventArgs e)
        {
            // b5 for lstick left/right
            uint w = readValue(0x0010C0B5, 1);
            uint h = readValue(0x0010C0B7, 1);
            label_btn_input.Text = string.Format("{0} - {1}", w, h);
        }

        //________________________________________________________________

        // Starting with the Cheats section! 

        // Mario Kart 7 (US) [1.1] Codes
        private void button_mk7_coins_read_Click(object sender, EventArgs e)
        {
            const int addr = 0x1413C540;
            uint v = FromLe(readValue(addr, 4), 4);
            textBox_mk7_coins.Text = string.Format("{0}", v);
        }

        private void button_mk7_coins_write_Click(object sender, EventArgs e)
        {
            const int addr = 0x1413C540;
            int v = GetInt(textBox_mk7_coins.Text);
            RunCmd(GenerateWriteString(addr, v, 4));
        }

        //________________________________________________________________

        // Animal Crossing (EU)

        private void button_aceu_openIds_Click(object sender, EventArgs e)
        {
            Browser.OpenUrl(
                "https://docs.google.com/spreadsheets/d/1NlfzvYM-dxsL3c6uP_089t8g5YNcT-AuUJvTE4COjmo/edit#gid=0");
        }

        private void button_aceu_setSlot1_Click(object sender, EventArgs e)
        {
            const int addr = 0x15FBEDD0;
            uint id = FromLe(textBox_aceu_itemid.Text);
            if (id > 0xffff) 
                id /= 0x10000;
            RunCmd(GenerateWriteString(addr, id, 4));
        }

        private void button_aceu_clear_slot1_Click(object sender, EventArgs e)
        {
            const int addr = 0x15FBEDD0;
            uint id = FromLe("FE7F");
            RunCmd(GenerateWriteString(addr, id, 2));
        }

        private void button_aceu_clear_all_Click(object sender, EventArgs e)
        {
            ClearInv();
        }

        // Gen items

        private void GenItems(uint value, int invSize)
        {
            const uint addr = 0x15FBEDD0;

            for (uint i = 0; i < invSize; i++)
            {
                uint addrr = addr + i*4;
                RunCmd(GenerateWriteString(addrr, value + i, 4));
            }
        }

        private void ClearInv()
        {
            const int addr = 0x15FBEDD0;
            uint clearItem = FromLe("FE7F");
            const int invSize = 16;

            for (int i = 0; i < invSize; i++)
            {
                int addrr = addr + i*4;
                RunCmd(GenerateWriteString(addrr, clearItem, 4));
            }
        }

        // Item packs

        private void button_aceu_fossil1_Click(object sender, EventArgs e)
        {
            uint value = FromLe("902E");
            GenItems(value, 16);
        }

        private void button_aceu_fossil2_Click(object sender, EventArgs e)
        {
            uint value = FromLe("A02E");
            GenItems(value, 16);
        }

        private void button_aceu_fossil3_Click(object sender, EventArgs e)
        {
            uint value = FromLe("B02E");
            GenItems(value, 16);
        }

        private void button_aceu_fossil4_Click(object sender, EventArgs e)
        {
            uint value = FromLe("C02E");
            GenItems(value, 16);
        }

        private void button_aceu_fossil5_Click(object sender, EventArgs e)
        {
            uint value = FromLe("D02E");
            GenItems(value, 16);
        }

        private void button_aceu_fossil6_Click(object sender, EventArgs e)
        {
            uint value = FromLe("E02E");
            GenItems(value, 7);
        }

        //________________________________________________________________

        // Monster Hunter 4 Ultimate (EU) 

        private void button_mh4u_eu_name_Click(object sender, EventArgs e)
        {
            string wCmd = "";
            char[] name = textBox_mh4u_eu_name.Text.ToCharArray();
            for (int i = 0; i < 11; i++)
            {
                if (i < name.Length)
                {
                    wCmd += string.Format("ord('{0}'), 0", name[i]);
                }
                else
                {
                    wCmd += "0, 0";
                }

                if (i < 10)
                {
                    wCmd += ", ";
                }
            }

            string[] addr =
            {
                //"0x082839D0", "0x08284AA6"
                "0x0836984C", "0x083EF258",
                "0x083FF258", "0x089417E8",
                "0x08941915", "0x08948A50"
            };
            foreach (string a in addr)
            {
                RunCmd(string.Format("Write({0}, ({1}), pid=0x{2:X})", a, wCmd, GetPid()));
            }
        }

        // Using my Gateshark system.. It's already in here, so why not use it?
        private void button_mh4u_eu_mon1_kill_Click(object sender, EventArgs e)
        {
            string gsCode =
                "D3000000 081C8170\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 00000E28\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 000003E8\r\n" +
                "10000000 00000001\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000";

            Gateshark code = new Gateshark(gsCode);
            code.Execute();
        }

        private void button_mh4u_eu_mon2_kill_Click(object sender, EventArgs e)
        {
            string gsCode =
                "D3000000 081C8174\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 00000E28\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 000003E8\r\n" +
                "10000000 00000001\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000";

            Gateshark code = new Gateshark(gsCode);
            code.Execute();
        }

        private void button_mh4u_eu_monb_kill_Click(object sender, EventArgs e)
        {
            string gsCode =
                "D3000000 081C8170\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 00000E28\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 000003E8\r\n" +
                "10000000 00000001\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D3000000 081C8174\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 00000E28\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 000003E8\r\n" +
                "10000000 00000001\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000";

            Gateshark code = new Gateshark(gsCode);
            code.Execute();
        }

        private void button_mh4u_eu_hb_godmode_Click(object sender, EventArgs e)
        {
            _hbc?.SetCode(
                "D3000000 00000470\r\n" +
                "481C7CD0 08800000\r\n" +
                "381C7CD0 08D00000\r\n" +
                "B81C7CD0 00000000\r\n" +
                "00001184 32003200\r\n" +
                "10001188 00003200\r\n" +
                "0000118C 44610000\r\n" +
                "00001190 44610000\r\n" +
                "10001194 00003200\r\n" +
                "D2000000 00000000"
                );
        }

        // MH4U US
        // -0x410

        private void button_mh4u_us_name_Click(object sender, EventArgs e)
        {
            string wCmd = "";
            char[] name = textBox_mh4u_us_name.Text.ToCharArray();
            for (int i = 0; i < 11; i++)
            {
                if (i < name.Length)
                {
                    wCmd += string.Format("ord('{0}') % 0x100, int(ord('{0}') / 0x100)", name[i]);
                }
                else
                {
                    wCmd += "0, 0";
                }

                if (i < 10)
                {
                    wCmd += ", ";
                }
            }

            string[] addr =
            {
                "0109E348", "083693DC", "083EED38", "08948F40"
            };
            foreach (string a in addr)
            {
                RunCmd(string.Format("Write(0x{0}, ({1}), pid=0x{2:X})", a, wCmd, GetPid()));
            }
        }

        private void button_mh4u_us_mon1_kill_Click(object sender, EventArgs e)
        {
            string gsCode =
                "D3000000 081C7D00\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 00000E28\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 000003E8\r\n" +
                "10000000 00000001\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000";

            Gateshark code = new Gateshark(gsCode);
            code.Execute();
        }

        private void button_mh4u_us_mon2_kill_Click(object sender, EventArgs e)
        {
            string gsCode =
                "D3000000 081C7D04\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 00000E28\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 000003E8\r\n" +
                "10000000 00000001\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000";

            Gateshark code = new Gateshark(gsCode);
            code.Execute();
        }

        private void button_mh4u_us_hb_godmode_Click(object sender, EventArgs e)
        {
            _hbc?.SetCode(
                "481C7CD0 08800000\r\n" +
                "381C7CD0 08D00000\r\n" +
                "B81C7CD0 00000000\r\n" +
                "00001184 32003200\r\n" +
                "10001188 00003200\r\n" +
                "0000118C 44610000\r\n" +
                "00001190 44610000\r\n" +
                "10001194 00003200\r\n" +
                "D2000000 00000000"
                );
        }

        private void button_mh4u_us_monb_kill_Click(object sender, EventArgs e)
        {
            string gsCode =
                "D3000000 081C7D00\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 00000E28\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 000003E8\r\n" +
                "10000000 00000001\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D3000000 081C7D04\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 00000E28\r\n" +
                "40000000 08000000\r\n" +
                "30000000 0B13EFFF\r\n" +
                "B0000000 00000000\r\n" +
                "DC000000 000003E8\r\n" +
                "10000000 00000001\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000\r\n" +
                "D0000000 00000000";

            Gateshark code = new Gateshark(gsCode);
            code.Execute();
        }

        private void button_remoteplay_Click(object sender, EventArgs e)
        {
            RunCmd("Remoteplay()");
        }

        private void button_wifi_fix_Click(object sender, EventArgs e)
        {
            RunCmd("Write(0x0105AE4, (0x70, 0x47), pid=0x1a)");
        }

        private void button_ntr_viewer_Click(object sender, EventArgs e)
        {
            ntrViewerWorker.RunWorkerAsync();
        }

        private void ntrViewerWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (File.Exists(Directory.GetCurrentDirectory() + "/" + "NTRViewer" + "/" + "NTRViewer.exe"))
            {
                using (Process ntrViewer = new Process
                {
                    StartInfo =
                    {
                        FileName = Directory.GetCurrentDirectory() + "/" + "NTRViewer" + "/" + "NTRViewer.exe",
                        Arguments = textBox_ntr_viewer_args.Text
                    }
                })
                {
                    ntrViewer.Start();
                }
            }
            else
            {
                MessageBox.Show(
                    @"NTRViewer not found." + Environment.NewLine +
                    @"Place the NTRViewer folder in this folder and try again...",
                    @"NTRViewer Missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button_mh4u_us_exefs_Click(object sender, EventArgs e)
        {
            Gateshark gs = new Gateshark(
                "D3000000 00000000" + Environment.NewLine +
                "00DEC800 E92D4006" + Environment.NewLine +
                "00DEC804 EBD431C2" + Environment.NewLine +
                "00DEC808 E3A01001" + Environment.NewLine + // Defense
                "00DEC80C E0201091" + Environment.NewLine +
                "00DEC810 E2400001" + Environment.NewLine +
                "00DEC814 E3A02C7D" + Environment.NewLine +
                "00DEC818 E2422001" + Environment.NewLine +
                "00DEC81C E1500002" + Environment.NewLine +
                "00DEC820 A1A00002" + Environment.NewLine +
                "00DEC824 E8BD8006" + Environment.NewLine +
                "00AF8044 EB0BD1ED" + Environment.NewLine +
                "00B1EEA0 EB0B3656" + Environment.NewLine +
                "D3000000 00000000" + Environment.NewLine +
                "00DEC7C0 E92D4006" + Environment.NewLine +
                "00DEC7C4 EBD431D2" + Environment.NewLine +
                "00DEC7C8 E3A01001" + Environment.NewLine + // Attack
                "00DEC7CC E0000190" + Environment.NewLine +
                "00DEC7D0 E3A02C7D" + Environment.NewLine +
                "00DEC7D4 E1500002" + Environment.NewLine +
                "00DEC7D8 A1A00002" + Environment.NewLine +
                "00DEC7DC E8BD8006" + Environment.NewLine +
                "00AF8B60 EB0BCF16" + Environment.NewLine +
                "00B1EA78 EB0B3750" + Environment.NewLine +
                "00B6B328 E1D109B6" + Environment.NewLine + // Sharpness
                "00B964E8 E3A00001"                         // Autotracker
            );

            gs.Execute();
        }

        private void button_mh4u_us_attack_Click(object sender, EventArgs e)
        {
            RunCmd(GenerateWriteString(0x00DEC7C8, (uint) (0xE3A01000 + GetInt(textbox_mh4u_us_attack.Text)), 4));
        }

        private void button_mh4u_us_defense_Click(object sender, EventArgs e)
        {
            RunCmd(GenerateWriteString(0x00DEC808, (uint)(0xE3A01000 + GetInt(textBox_mh4u_us_defense.Text)), 4));
        }

        private int mh4uNameIndex = 0;
        private void button_mh4u_us_nameforce_Click(object sender, EventArgs e)
        {
            string wCmd = "";
            for (int i = 0; i < 11; i++)
            {
                wCmd += string.Format("{0} % 0x100, int({0} / 0x100)", mh4uNameIndex);
                mh4uNameIndex++;

                if (i < 10)
                {
                    wCmd += ", ";
                }
            }

            string[] addr =
            {
                "083693DC", "083EED38"
            };
            foreach (string a in addr)
            {
                RunCmd(string.Format("Write(0x{0}, ({1}), pid=0x{2:X})", a, wCmd, GetPid()));
            }
            label1.Text = "INDEX: " + mh4uNameIndex;
        }

        private void button_mh4u_us_nameindexset_Click(object sender, EventArgs e)
        {
            mh4uNameIndex = GetInt(textBox_mh4u_us_nameindex.Text);
            label1.Text = @"INDEX: " + mh4uNameIndex;
        }

        private void button_mk7_us_item_Click(object sender, EventArgs e)
        {
            uint v1 = Convert.ToUInt32(CmdWindow.FromLe(Program.GCmdWindow.readValue(0x147909D4, 2), 2));
            if (v1 == 0x0001)
            {
                uint v2 = Convert.ToUInt32(CmdWindow.FromLe(Program.GCmdWindow.readValue(0x1778244C, 4), 4));
                if (v2 > 0)
                {
                    v2 = Convert.ToUInt32(CmdWindow.FromLe(Program.GCmdWindow.readValue(v2 + 0x1BEC, 4), 4));
                    RunCmd(GenerateWriteString(v2 + 0x3C, 0xFFFFFFFF, 4));
                    RunCmd(GenerateWriteString(v2 + 0xA8, 0x203, 2));
                    RunCmd(GenerateWriteString(v2 + 0xC8, comboBox_mk7_us_item.SelectedIndex, 2));

                }
            }
        }

        // New stuff.. Need to add this to a category.
    }
}