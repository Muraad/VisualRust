using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualRust.Shared
{
    public enum BuildOutputType
    {
        Application,
        Library,
        CargoApplication,
        CargoLibrary
    }

    public static class BuildOutputTypeExtension
    {
        public static BuildOutputType Parse(string val)
        {
            if(val == null)
                return BuildOutputType.Application;
            if(val.Equals("exe", StringComparison.OrdinalIgnoreCase))
                return BuildOutputType.Application;
            if(val.Equals("library", StringComparison.OrdinalIgnoreCase))
                return BuildOutputType.Library;
            if (val.Equals("cargo_exe", StringComparison.OrdinalIgnoreCase))
                return BuildOutputType.CargoApplication;
            if (val.Equals("cargo_library", StringComparison.OrdinalIgnoreCase))
                return BuildOutputType.CargoLibrary;
            return BuildOutputType.Application;
        }

        public static string ToBuildString(this BuildOutputType val)
        {
            switch(val)
            {
                case BuildOutputType.Application:
                    return "exe";
                case BuildOutputType.Library:
                    return "library";
                case BuildOutputType.CargoApplication:
                    return "cargo_exe";
                case BuildOutputType.CargoLibrary:
                    return "cargo_library";
                default:
                    throw new ArgumentException(null, "val");
            }
        }

        public static string ToDisplayString(this BuildOutputType val)
        {
            switch(val)
            {
                case BuildOutputType.Application:
                    return "Application";
                case BuildOutputType.Library:
                    return "Library";
                case BuildOutputType.CargoApplication:
                    return "Cargo application";
                case BuildOutputType.CargoLibrary:
                    return "Cargo library";
                default:
                    throw new ArgumentException(null, "val");
            }
        }

        public static string ToRustcString(this BuildOutputType val)
        {
            switch(val)
            {
                case BuildOutputType.Application:
                case BuildOutputType.CargoApplication:
                    return "bin";
                case BuildOutputType.Library:
                case BuildOutputType.CargoLibrary:
                    return "lib";
                default:
                    throw new ArgumentException(null, "val");
            }
        }

        public static string ToCrateFile(this BuildOutputType val)
        {
            switch(val)
            {
                case BuildOutputType.Application:
                    return "main.rs";
                case BuildOutputType.Library:
                    return "lib.rs";
                default:
                    throw new ArgumentException(null, "val");
            }
        }
    }
}
