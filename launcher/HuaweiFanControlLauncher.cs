using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

[assembly: AssemblyTitle("Huawei MateBook Fan Control")]
[assembly: AssemblyDescription("Launcher for the watchdog-backed Huawei MateBook fan controller")]
[assembly: AssemblyCompany("HuaweiFanControl community project")]
[assembly: AssemblyProduct("Huawei MateBook Fan Control")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]

namespace HuaweiFanControl
{
    internal static class Program
    {
        private const string PayloadVersion = "1.3.0";

        private sealed class Options
        {
            public bool ShowHelp;
            public bool MonitorOnly;
            public bool FullSpeed;
            public bool MinutesSpecified;
            public int Minutes;
            public int SampleSeconds = 3;
            public int HysteresisC = 3;
            public int EmergencyTemperatureC = 85;
            public string CurvePath;
        }

        private static int Main(string[] args)
        {
            Options options;
            try
            {
                options = ParseOptions(args);
            }
            catch (ArgumentException exception)
            {
                Console.Error.WriteLine("Argument error: " + exception.Message);
                PrintHelp();
                return 2;
            }

            if (options.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            bool ownsMutex = false;
            using (Mutex mutex = new Mutex(false, "Local\\HuaweiMateBookFanControlLauncher"))
            {
                try
                {
                    ownsMutex = mutex.WaitOne(0, false);
                }
                catch (AbandonedMutexException)
                {
                    ownsMutex = true;
                }

                if (!ownsMutex)
                {
                    Console.Error.WriteLine("Another HuaweiFanControl.exe instance is already running.");
                    return 3;
                }

                try
                {
                    return RunController(options);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("Launcher error: " + exception.Message);
                    return 1;
                }
                finally
                {
                    if (ownsMutex)
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
        }

        private static int RunController(Options options)
        {
            string payloadDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HuaweiFanControl",
                "payload-" + PayloadVersion);
            Directory.CreateDirectory(payloadDirectory);

            ExtractResource("HuaweiFanControl.Resources.Controller.ps1", Path.Combine(payloadDirectory, "HuaweiFan-AutoController.ps1"));
            ExtractResource("HuaweiFanControl.Resources.Watchdog.ps1", Path.Combine(payloadDirectory, "HuaweiFan-Watchdog.ps1"));
            ExtractResource("HuaweiFanControl.Resources.QuietCurve.json", Path.Combine(payloadDirectory, "quiet-balanced-curve.json"));
            ExtractResource("HuaweiFanControl.Resources.FullSpeedCurve.json", Path.Combine(payloadDirectory, "full-speed-curve.json"));

            string controllerPath = Path.Combine(payloadDirectory, "HuaweiFan-AutoController.ps1");
            string curvePath;
            if (!String.IsNullOrWhiteSpace(options.CurvePath))
            {
                curvePath = Path.GetFullPath(options.CurvePath);
                if (!File.Exists(curvePath))
                {
                    throw new FileNotFoundException("Curve file was not found.", curvePath);
                }
            }
            else
            {
                curvePath = Path.Combine(payloadDirectory, options.FullSpeed ? "full-speed-curve.json" : "quiet-balanced-curve.json");
            }

            int minutes = options.Minutes;
            if (options.FullSpeed && !options.MinutesSpecified)
            {
                minutes = 5;
            }

            string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string powershellPath = Path.Combine(windowsDirectory, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            if (!File.Exists(powershellPath))
            {
                throw new FileNotFoundException("Windows PowerShell 5.1 was not found.", powershellPath);
            }

            List<string> controllerArguments = new List<string>();
            controllerArguments.Add("-NoProfile");
            controllerArguments.Add("-ExecutionPolicy");
            controllerArguments.Add("Bypass");
            controllerArguments.Add("-File");
            controllerArguments.Add(Quote(controllerPath));
            controllerArguments.Add("-CurvePath");
            controllerArguments.Add(Quote(curvePath));
            controllerArguments.Add("-SampleSeconds");
            controllerArguments.Add(options.SampleSeconds.ToString());
            controllerArguments.Add("-HysteresisC");
            controllerArguments.Add(options.HysteresisC.ToString());
            controllerArguments.Add("-EmergencyTemperatureC");
            controllerArguments.Add(options.EmergencyTemperatureC.ToString());
            controllerArguments.Add("-MaxMinutes");
            controllerArguments.Add(minutes.ToString());
            if (!options.MonitorOnly)
            {
                controllerArguments.Add("-Apply");
            }

            Console.WriteLine("Huawei MateBook Fan Control " + PayloadVersion);
            Console.WriteLine(options.MonitorOnly ? "Mode: monitor only" : (options.FullSpeed ? "Mode: maximum EC request" : "Mode: quiet balanced automatic curve"));
            if (minutes > 0)
            {
                Console.WriteLine("Automatic stop: " + minutes + " minute(s)");
            }
            Console.WriteLine("Press Ctrl+C to stop; BIOS vendor control will be restored.");
            Console.WriteLine();

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = powershellPath;
            startInfo.Arguments = String.Join(" ", controllerArguments.ToArray());
            startInfo.WorkingDirectory = payloadDirectory;
            startInfo.UseShellExecute = false;

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("Windows PowerShell could not be started.");
                }
                process.WaitForExit();
                return process.ExitCode;
            }
        }

        private static Options ParseOptions(string[] args)
        {
            Options options = new Options();
            for (int index = 0; index < args.Length; index++)
            {
                string argument = args[index];
                switch (argument.ToLowerInvariant())
                {
                    case "--help":
                    case "-h":
                    case "/?":
                        options.ShowHelp = true;
                        break;
                    case "--monitor":
                        options.MonitorOnly = true;
                        break;
                    case "--full-speed":
                        options.FullSpeed = true;
                        break;
                    case "--minutes":
                        options.Minutes = ParseRange(NextValue(args, ref index, argument), argument, 0, 1440);
                        options.MinutesSpecified = true;
                        break;
                    case "--sample-seconds":
                        options.SampleSeconds = ParseRange(NextValue(args, ref index, argument), argument, 1, 10);
                        break;
                    case "--hysteresis":
                        options.HysteresisC = ParseRange(NextValue(args, ref index, argument), argument, 1, 8);
                        break;
                    case "--emergency-temp":
                        options.EmergencyTemperatureC = ParseRange(NextValue(args, ref index, argument), argument, 75, 95);
                        break;
                    case "--curve":
                        options.CurvePath = NextValue(args, ref index, argument);
                        break;
                    default:
                        throw new ArgumentException("Unknown option: " + argument);
                }
            }

            if (options.FullSpeed && !String.IsNullOrWhiteSpace(options.CurvePath))
            {
                throw new ArgumentException("--full-speed and --curve cannot be used together.");
            }
            return options;
        }

        private static string NextValue(string[] args, ref int index, string option)
        {
            index++;
            if (index >= args.Length)
            {
                throw new ArgumentException(option + " requires a value.");
            }
            return args[index];
        }

        private static int ParseRange(string value, string option, int minimum, int maximum)
        {
            int parsed;
            if (!Int32.TryParse(value, out parsed) || parsed < minimum || parsed > maximum)
            {
                throw new ArgumentException(option + " must be between " + minimum + " and " + maximum + ".");
            }
            return parsed;
        }

        private static void ExtractResource(string resourceName, string destinationPath)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream input = assembly.GetManifestResourceStream(resourceName))
            {
                if (input == null)
                {
                    throw new InvalidOperationException("Embedded resource is missing: " + resourceName);
                }
                string temporaryPath = destinationPath + ".tmp";
                using (FileStream output = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    input.CopyTo(output);
                }
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                File.Move(temporaryPath, destinationPath);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void PrintHelp()
        {
            Console.WriteLine("HuaweiFanControl.exe [options]");
            Console.WriteLine();
            Console.WriteLine("With no options, runs the quiet balanced automatic curve until Ctrl+C.");
            Console.WriteLine("  --monitor                 Read sensors without controlling fans");
            Console.WriteLine("  --full-speed              Use maximum EC request (defaults to 5 minutes)");
            Console.WriteLine("  --minutes N               Stop after N minutes; 0 means no time limit");
            Console.WriteLine("  --curve PATH              Use a custom JSON curve");
            Console.WriteLine("  --sample-seconds N        Sampling interval, 1-10 (default 3)");
            Console.WriteLine("  --hysteresis N            Downshift hysteresis, 1-8 C (default 3)");
            Console.WriteLine("  --emergency-temp N        Restore threshold, 75-95 C (default 85)");
            Console.WriteLine("  --help                    Show this help");
        }
    }
}
