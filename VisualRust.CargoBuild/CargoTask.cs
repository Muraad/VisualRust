using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Microsoft.Build.Framework;
using Microsoft.Build.Framework;
using VisualRust.Shared;

namespace VisualRust.CargoBuild
{
    public class CargoTask : Microsoft.Build.Utilities.Task
    {
        [Required]
        public string WorkingDirectory { get; set; }

        [Required]
        public string Configuration { get; set; }

        public override bool Execute()
        {
            try
            {
                return ExecuteInner();
            }
            catch (Exception ex)
            {
                this.Log.LogErrorFromException(ex, true);
                return false;
            }
        }

        private bool ExecuteInner()
        {
            bool result = true;
            string errorOutput = String.Empty;
            Log.LogMessage("Confing            = " + Configuration);
            Log.LogMessage("WorkingDirectory   = " + WorkingDirectory);
            switch(Configuration)
            {
                case "Build":
                    this.Log.LogCommandLine("Starting Cargo build at " + DateTime.Now.ToLongTimeString());
                    EchoStandardError(Shared.Cargo.Build(WorkingDirectory));
                    break;
                case "Run":
                    Log.LogCommandLine("Starting Cargo run at " + DateTime.Now.ToLongTimeString());
                    EchoStandardError(Shared.Cargo.Run(WorkingDirectory));
                    break;
                case "Update":
                    Log.LogCommandLine("Starting Cargo update at " + DateTime.Now.ToLongTimeString());
                    EchoStandardError(Shared.Cargo.Update(WorkingDirectory));
                    break;
                case "Test":
                    this.Log.LogCommandLine("Starting Cargo test at " + DateTime.Now.ToLongTimeString());
                    EchoStandardError(Shared.Cargo.Test(WorkingDirectory));
                    break;
                case "Bench":
                    this.Log.LogCommandLine("Starting Cargo bench at " + DateTime.Now.ToLongTimeString());
                    EchoStandardError(Shared.Cargo.Bench(WorkingDirectory));
                    break;
                case "Release":
                    this.Log.LogCommandLine("Starting Cargo build --release at " + DateTime.Now.ToLongTimeString());
                    EchoStandardError(Shared.Cargo.Release(WorkingDirectory));
                    break;
                case "Clean":
                    this.Log.LogCommandLine("Starting Cargo clean at " + DateTime.Now.ToLongTimeString());
                    EchoStandardError(Shared.Cargo.Clean(WorkingDirectory));
                    break;
                default: 
                    Log.LogCommandLine("Unknown configuration " + DateTime.Now.ToLongTimeString());
                    Log.LogError("Unknown configuration detected :(");
                    break;
                //case "CargoRelease":
                //    HandleProcess(Shared.Cargo.(WorkingDirectory));
                //    break;
            }
            return result;
        }


        private System.Threading.Tasks.Task EchoStandardError(Process process)
        {
            return System.Threading.Tasks.Task.Run(() =>
            {
                string errorOutput = process.StandardError.ReadToEnd();
                this.Log.LogCommandLine(errorOutput);

                var rustcErrors = RustcOutputProcessor.ParseOutput(errorOutput);
                foreach (var msg in rustcErrors)
                {
                    RustcOutputProcessor.LogRustcMessage(msg, this.Log);
                }

                process.WaitForExit();
                
            });
        }
    }
}
