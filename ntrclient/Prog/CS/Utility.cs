﻿using System;
using System.Diagnostics;
using System.Linq;

namespace ntrclient.Prog.CS
{
    public class Utility
    {
        public static int RunCommandAndGetOutput(string exeFile, string args, ref string output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exeFile,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true
                }
            };


            try
            {
                proc.Start();
                proc.WaitForExit();
                var processOutput = proc.StandardError.ReadToEnd();
                processOutput += proc.StandardOutput.ReadToEnd();
                var ret = proc.ExitCode;
                output = processOutput;
                proc.Close();
                return ret;
            }
            catch (Exception e)
            {
                output = e.Message;
                return -1;
            }
        }

        public static string ConvertByteArrayToHexString(byte[] arr)
        {
            return arr.Aggregate("", (current, t) => current + (t.ToString("X2") + " "));
        }
    }
}