// PkgCmdID.cs
// MUST match PkgCmdID.h
using System;

namespace VisualRust
{
    static class PkgCmdIDList
    {
        public const uint cmdidCargoRun =       0x100;
        public const uint cmdidCargoBuild =     0x120;
        public const uint cmdidCargoNew =       0x130;
        public const uint cmdidCargoUpdate =    0x140;
        public const uint cmdidCargoTest =      0x150;
        public const uint cmdidCargoBench =     0x160;
    };
}