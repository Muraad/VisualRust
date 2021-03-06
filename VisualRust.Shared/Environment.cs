﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualRust.Shared
{
    public class Environment
    {
        private const string InnoPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Rust_is1";
        private const string InnoKey = "InstallLocation";
        private const string MozillaPath = @"Software\Mozilla Foundation";
        private const string install = "InstallLocation";

        // I'm really torn between "default", "local", "native", "unspecified" and "any"
        public const string DefaultTarget = "default";


        public static string FindInstallPathOld(string target)
        {
            string result = null;
            foreach(string path in System.Environment.GetEnvironmentVariable("PATH").Split(System.IO.Path.PathSeparator))
            {
                if(File.Exists(Path.Combine(path, "rustc.exe")) 
                    && File.Exists(Path.Combine(path, "cargo.exe"))
                    && CanActuallyBuildTarget(path, target))
                result = path;
            }
            if (String.IsNullOrEmpty(result))
            {
                RegistryKey installpath = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default).OpenSubKey(InnoPath);

                if (installpath == null)
                    result = null;
                else
                {
                    object fullInstallKey = installpath.GetValue(InnoKey);
                    result = fullInstallKey != null ? fullInstallKey.ToString() : null;
                }
            }
            return result;
        }

        /* 
         * If the target is "default" just return first location
         * Otherwise check for bin\rustlib\<target>
         */
        public static string FindInstallPath(string target)
        {
            var installPaths = FindCurrentUserInstallPaths().ToList();
            return GetAllInstallPaths().Select(p => Path.Combine(p, "bin")).FirstOrDefault(p => CanActuallyBuildTarget(p, target));
        }

        public static IEnumerable<string> FindInstalledTargets()
        {
            return GetAllInstallPaths().SelectMany(SniffTargets);
        }

        public static IEnumerable<string> FindCurrentUserInstallPaths()
        {
            if(System.Environment.Is64BitOperatingSystem)
            {
                return GetInstallRoots(RegistryHive.CurrentUser, RegistryView.Registry64)
                    .Union(GetInstallRoots(RegistryHive.CurrentUser, RegistryView.Registry32));
            }
            else
            {
                return GetInstallRoots(RegistryHive.CurrentUser, RegistryView.Registry32);
            }
        }

        private static IEnumerable<string> GetAllInstallPaths()
        {
            if(System.Environment.Is64BitOperatingSystem)
            {
                return GetInstallRoots(RegistryHive.CurrentUser, RegistryView.Registry64)
                    .Union(GetInstallRoots(RegistryHive.CurrentUser, RegistryView.Registry32))
                    .Union(GetInstallRoots(RegistryHive.LocalMachine, RegistryView.Registry64))
                    .Union(GetInstallRoots(RegistryHive.LocalMachine, RegistryView.Registry32))
                    .Union(GetInnoInstallRoot());
            }
            else
            {
                return GetInstallRoots(RegistryHive.CurrentUser, RegistryView.Registry32)
                    .Union(GetInstallRoots(RegistryHive.LocalMachine, RegistryView.Registry32))
                    .Union(GetInnoInstallRoot());
            }
        }

        private static string[] GetInnoInstallRoot()
        {
            RegistryKey innoKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default).OpenSubKey(InnoPath);
            if(innoKey == null)
                return new string[0];
            string installPath = innoKey.GetValue("InstallLocation") as string;
            if(installPath == null)
                return new string[0];
            return new [] { installPath };
        }

        private static IEnumerable<string> GetInstallRoots(RegistryHive hive, RegistryView view)
        {
            RegistryKey mozillaKey = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(MozillaPath);
            if (mozillaKey == null)
                return new string[0];
            return mozillaKey
                .GetSubKeyNames()
                .Where(n => n.StartsWith("Rust", StringComparison.OrdinalIgnoreCase))
                .SelectMany(n => AllSubKeys(mozillaKey.OpenSubKey(n)))
                .Select(k => k.GetValue("InstallDir") as string)
                .Where(x => x != null);
        }

        private static IEnumerable<RegistryKey> AllSubKeys(RegistryKey key)
        {
            return key.GetSubKeyNames().Select(n => key.OpenSubKey(n));
        }

        private static bool CanActuallyBuildTarget(string binPath, string target)
        {
            if(String.Equals(DefaultTarget, target, StringComparison.OrdinalIgnoreCase))
                return true;
            return Directory.Exists(Path.Combine(binPath, "rustlib", target));
        }

        private static IEnumerable<string> SniffTargets(string installPath)
        {
            try
            {
                string root = Path.Combine(installPath, "bin", "rustlib");
                return Directory.GetDirectories(root, "*-*-*").Select(p => p.Substring(root.Length + 1).ToLowerInvariant());
            }
            catch(IOException)
            {
                return new string[0];
            }
        }
    }
}
