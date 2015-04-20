// PkgCmdID.cs
// MUST match PkgCmdID.h
using System;

namespace VisualRust
{
    static class PkgCmdIDList
    {
        public const int cmdidCargoRun =        0x100;
        public const int cmdidCargoBuild =      0x120;
        public const int cmdidCargoNew =        0x130;
        public const int cmdidCargoClean =      0x140;
        public const int cmdidCargoUpdate =     0x150;
        public const int cmdidCargoTest =       0x160;
        public const int cmdidCargoBench =      0x170;

        public const int cmdidToolbarCargoRun =     0x130;
        public const int cmdidToolbarCargoBuild =   0x131;
        //public const int cmdidToolbarCargoNew =   0x132;
        public const int cmdidToolbarCargoClean =   0x133;
        public const int cmdidToolbarCargoUpdate =  0x134;
        public const int cmdidToolbarCargoTest =    0x135;
        public const int cmdidToolbarCargoBench =   0x136;
        public const int cmdidToolbarCustomCommand = 0x137;
        public const int cmdidToolbarCmdCombo = 0x138;
        public const int cmdidToolbarCmdArgsCombo = 0x0139;

        public const int cmdidToolbarCargoCustomComboGetList = 0x108;
    };
}