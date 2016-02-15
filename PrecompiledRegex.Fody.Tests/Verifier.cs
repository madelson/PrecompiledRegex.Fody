﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PrecompiledRegex.Fody.Tests
{
    // from https://github.com/Fody/BasicFodyAddin/blob/master/Tests/Verifier.cs
    internal static class Verifier
    {
        public static void Verify(string beforeAssemblyPath, string afterAssemblyPath)
        {
            var before = Validate(beforeAssemblyPath);
            var after = Validate(afterAssemblyPath);
            var message = string.Format("Failed processing {0}\r\n{1}", Path.GetFileName(afterAssemblyPath), after);
            Assert.AreEqual(TrimLineNumbers(before), TrimLineNumbers(after), message);
        }

        static string Validate(string assemblyPath2)
        {
            var exePath = GetPathToPEVerify();
            if (!File.Exists(exePath))
            {
                return string.Empty;
            }
            var process = Process.Start(new ProcessStartInfo(exePath, "\"" + assemblyPath2 + "\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            Assert.AreEqual(true, process.WaitForExit(10000));
            return process.StandardOutput.ReadToEnd().Trim().Replace(assemblyPath2, "");
        }

        static string GetPathToPEVerify()
        {
            var exePath = Environment.ExpandEnvironmentVariables(@"%programfiles(x86)%\Microsoft SDKs\Windows\v7.0A\Bin\NETFX 4.0 Tools\PEVerify.exe");

            if (!File.Exists(exePath))
            {
                exePath = Environment.ExpandEnvironmentVariables(@"%programfiles(x86)%\Microsoft SDKs\Windows\v8.0A\Bin\NETFX 4.0 Tools\PEVerify.exe");
            }
            return exePath;
        }

        static string TrimLineNumbers(string foo)
        {
            return Regex.Replace(foo, @"0x.*]", "");
        }
    }
}
