using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace NLightning.Daemon.Utilities;

using Contracts.Constants;

public partial class DaemonUtils
{
    public static void ShowUsage()
    {
        Console.WriteLine("NLTG - NLightning Daemon");
        Console.WriteLine("Usage:");
        Console.WriteLine("  nltg [options]");
        Console.WriteLine("  nltg --stop         Stop a running daemon");
        Console.WriteLine("  nltg --status       Show daemon status");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --network, -n <network>    Network to use (mainnet, testnet, regtest) [default: mainnet]");
        Console.WriteLine("  --config, -c <path>        Path to custom configuration file");
        Console.WriteLine("  --daemon <true|false>      Run as a daemon [default: false]");
        Console.WriteLine("  --stop                     Stop a running daemon");
        Console.WriteLine("  --status                   Show daemon status information");
        Console.WriteLine("  --help, -h, -?             Show this help message");
        Console.WriteLine();
        Console.WriteLine("Environment Variables:");
        Console.WriteLine("  NLTG_NETWORK               Network to use");
        Console.WriteLine("  NLTG_CONFIG                Path to custom configuration file");
        Console.WriteLine("  NLTG_DAEMON                Run as a daemon");
        Console.WriteLine();
        Console.WriteLine("Configuration File:");
        Console.WriteLine("  Default path: ~/.nltg/{network}/appsettings.json");
        Console.WriteLine("  Settings:");
        Console.WriteLine("  {");
        Console.WriteLine("    \"Daemon\": true,         # Run as a background daemon");
        Console.WriteLine("    ... other settings ...");
        Console.WriteLine("  }");
        Console.WriteLine();
        Console.WriteLine("PID file location: ~/.nltg/{network}/nltg.pid");
    }

    public static bool IsStopRequested(string[] args)
    {
        return args.Any(arg =>
                            arg.Equals("--stop", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsStatusRequested(string[] args)
    {
        return args.Any(arg =>
                            arg.Equals("--status", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Starts the application as a daemon process if requested
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <param name="configuration">Configuration</param>
    /// <param name="pidFilePath">Path where to store the PID file</param>
    /// <param name="logger">Logger for startup messages</param>
    /// <returns>True if the parent process should exit, false to continue execution</returns>
    public static bool StartDaemonIfRequested(string[] args, IConfiguration configuration, string pidFilePath,
                                              ILogger logger)
    {
        // Check if we're already running as a daemon child process
        if (IsRunningAsDaemon())
        {
            return false; // Continue execution as a daemon child
        }

        // Check command line args (the highest priority)
        var isDaemonRequested = Array.Exists(args, arg =>
                                                 arg.Equals("--daemon", StringComparison.OrdinalIgnoreCase) ||
                                                 arg.Equals("--daemon=true", StringComparison.OrdinalIgnoreCase));

        // Check environment variable (middle priority)
        if (!isDaemonRequested)
        {
            var envDaemon = Environment.GetEnvironmentVariable("NLTG_DAEMON");
            isDaemonRequested = !string.IsNullOrEmpty(envDaemon) &&
                                (envDaemon.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                 envDaemon.Equals("1", StringComparison.OrdinalIgnoreCase));
        }

        // Check configuration file (lowest priority)
        if (!isDaemonRequested)
        {
            isDaemonRequested = configuration.GetValue<bool>("Node:Daemon");
        }

        if (!isDaemonRequested)
        {
            return false; // Continue normal execution
        }

        logger.Information("Daemon mode requested, starting background process");

        // Platform-specific daemon implementation
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                   ? StartWindowsDaemon(args, pidFilePath, logger)
                   : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                       ? StartMacOsDaemon(args, pidFilePath,
                                          logger) // Special implementation for macOS to avoid fork() issues
                       : StartUnixDaemon(pidFilePath, logger); // Linux and other Unix systems
    }

    private static bool StartWindowsDaemon(string[] args, string pidFilePath, ILogger logger)
    {
        try
        {
            // Create a new process info
            var startInfo = new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule?.FileName,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Environment.CurrentDirectory
            };

            // Copy all args except --daemon
            foreach (var arg in args)
            {
                if (!arg.StartsWith("--daemon", StringComparison.OrdinalIgnoreCase))
                {
                    startInfo.ArgumentList.Add(arg);
                }
            }

            // Add a special flag to indicate we're already in daemon mode
            startInfo.ArgumentList.Add("--daemon-child");

            // Start the new process
            var process = Process.Start(startInfo);
            if (process == null)
            {
                logger.Error("Failed to start daemon process");
                return false;
            }

            // Write PID to file
            File.WriteAllText(pidFilePath, process.Id.ToString());

            logger.Information("Daemon started with PID {PID}", process.Id);
            return true; // Parent should exit
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error starting daemon process");
            return false;
        }
    }

    /// <summary>
    /// Start daemon on macOS - uses a different approach than Linux to avoid fork() issues
    /// </summary>
    private static bool StartMacOsDaemon(string[] args, string pidFilePath, ILogger logger)
    {
        try
        {
            logger.Information("Using macOS-specific daemon startup");

            // Build the command line
            var processPath = Process.GetCurrentProcess().MainModule?.FileName;
            var arguments = new StringBuilder();

            // Add all the original arguments except --daemon
            foreach (var arg in args)
            {
                if (arg.StartsWith("--daemon", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Quote the argument if it contains spaces
                if (arg.Contains(' '))
                {
                    arguments.Append($"\"{arg}\" ");
                }
                else
                {
                    arguments.Append($"{arg} ");
                }
            }

            // Add daemon-child argument
            arguments.Append("--daemon-child");

            // Create a shell script to launch the process and disown it
            var scriptPath = Path.Combine(Path.GetTempPath(), $"nltg_daemon_{Guid.NewGuid()}.sh");

            // Write the shell script
            var scriptContent = $"""
                                 #!/bin/bash
                                 # Auto-generated daemon launcher for NLTG
                                 nohup "{processPath}" {arguments} > /dev/null 2>&1 &
                                 echo $! > "{pidFilePath}"

                                 """;
            File.WriteAllText(scriptPath, scriptContent);

            // Make the script executable
            var chmodProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            chmodProcess?.WaitForExit();

            // Run the script
            var scriptProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"\"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            scriptProcess?.WaitForExit();

            // Clean up the script file
            try
            {
                File.Delete(scriptPath);
            }
            catch
            {
                // Ignore cleanup errors
            }

            // Verify the PID file was created
            if (File.Exists(pidFilePath))
            {
                var pidContent = File.ReadAllText(pidFilePath).Trim();
                logger.Information("macOS daemon started with PID {PID}", pidContent);
                return true;
            }

            logger.Warning("PID file not created, daemon may not have started correctly");
            return true; // Parent still exits even if there might be an issue
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error starting macOS daemon process");
            return false;
        }
    }

    private static bool StartUnixDaemon(string pidFilePath, ILogger logger)
    {
        try
        {
            // First fork
            var pid = Fork();
            switch (pid)
            {
                case < 0:
                    logger.Error("First fork failed");
                    return false;
                case > 0:
                    // Parent process exits
                    logger.Information("Forked first process with PID {PID}", pid);
                    return true;
            }

            // Detach from terminal
            _ = Setsid();

            // Second fork
            pid = Fork();
            switch (pid)
            {
                case < 0:
                    logger.Error("Second fork failed");
                    return false;
                case > 0:
                    // Exit the intermediate process
                    Environment.Exit(0);
                    break;
            }

            // Child process continues
            // Change working directory
            Directory.SetCurrentDirectory("/");

            // Close standard file descriptors
            Console.SetIn(StreamReader.Null);
            Console.SetOut(StreamWriter.Null);
            Console.SetError(StreamWriter.Null);

            // Write the PID file
            var currentPid = Environment.ProcessId;
            File.WriteAllText(pidFilePath, currentPid.ToString());

            return false; // Continue execution in the child
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error starting Unix daemon process");
            return false;
        }
    }

    /// <summary>
    /// Checks if this process is already running as daemon
    /// </summary>
    public static bool IsRunningAsDaemon()
    {
        return Array.Exists(Environment.GetCommandLineArgs(),
                            arg => arg.Equals("--daemon-child", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the path for the PID file
    /// </summary>
    public static string GetPidFilePath(string configPath)
    {
        return Path.Combine(configPath, NodeConstants.PidFile);
    }

    /// <summary>
    /// Stops a running daemon if it exists
    /// </summary>
    public static bool StopDaemon(string pidFilePath, ILogger logger)
    {
        try
        {
            if (!File.Exists(pidFilePath))
            {
                logger.Warning("PID file not found, daemon may not be running");
                return false;
            }

            var pidText = File.ReadAllText(pidFilePath).Trim();
            if (!int.TryParse(pidText, out var pid))
            {
                logger.Error("Invalid PID in file: {PidText}", pidText);
                return false;
            }

            try
            {
                var process = Process.GetProcessById(pid);
                logger.Information("Stopping daemon process with PID {PID}", pid);

                // Send SIGTERM instead of Kill for graceful shutdown
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Windows - send Ctrl+C or use taskkill /PID {pid} /F
                    SendCtrlEvent(process);
                }
                else
                {
                    // Unix/macOS - send SIGTERM
                    SendSignal(pid, 15); // SIGTERM is 15
                }

                // Wait for exit
                var exited = process.WaitForExit(TimeSpan.FromSeconds(10));
                if (exited)
                {
                    logger.Information("Daemon process stopped successfully");
                    File.Delete(pidFilePath);
                    return true;
                }

                // If a graceful shutdown fails, force kill as last resort
                logger.Warning("Daemon process did not exit gracefully, forcing termination");
                process.Kill();
                exited = process.WaitForExit(5000);
                if (exited)
                {
                    File.Delete(pidFilePath);
                    return true;
                }

                return false;
            }
            catch (ArgumentException)
            {
                logger.Warning("No process found with PID {PID}, removing stale PID file", pid);
                File.Delete(pidFilePath);
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error stopping daemon");
            return false;
        }
    }

    private static void SendSignal(int pid, int signal)
    {
        Process.Start("kill", $"-{signal} {pid}").WaitForExit();
    }

    private static void SendCtrlEvent(Process process)
    {
        Process.Start("taskkill", $"/PID {process.Id}").WaitForExit();
    }

    #region Native Methods

    [LibraryImport("libc")]
    private static partial int fork();

    [LibraryImport("libc")]
    private static partial int setsid();

    private static int Fork()
    {
        // If not on Unix, simulate the fork by returning -1
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return -1;
        }

        return fork();
    }

    private static int Setsid()
    {
        // If not on Unix, simulate setsid by returning -1
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return -1;
        }

        return setsid();
    }

    #endregion
}