using System;

namespace VisualRust
{
    static class GuidList
    {
        public const string guidVisualRustPkgString = "40c1d2b5-528b-4966-a7b1-1974e3568abe";

        public const string guidVSPackage1CmdSetString = "f26706f3-1d40-41c0-bda3-e78f8be21fe0";

        public const string guidVSPackage1CmdRunString = "4FBEC97C-CEF7-484D-AAF2-7FE69CD426FC";
        public static readonly Guid guidVSPackage1CmdSet = new Guid(guidVSPackage1CmdSetString);
        public static readonly Guid guidVSPackage1CmdRun = new Guid(guidVSPackage1CmdRunString);
    };
}