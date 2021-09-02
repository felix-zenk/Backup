using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

/// <summary>
/// Backup is a project for mirroring a source directory to a target directory and verifying the successful copying process
/// </summary>
namespace Backup
{
    /// <summary>
    /// The entry point of this program
    /// </summary>
    class Program
    {
        /// <summary>
        /// Entry point of this program
        /// </summary>
        /// <param name="args">Start parameters</param>
        static void Main(string[] args)
        {
            bool verifyOnly = false;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLower().Equals("--verify") || args[i].ToLower().Equals("-v"))
                {
                    verifyOnly = true;
                }
                else if (args[i].ToLower().Equals("--help") || args[i].ToLower().Equals("-h"))
                {
                    ShowHelp();
                    Environment.Exit(-1);
                }
            }

            Backup backup = new(args);
            if (verifyOnly)
            {
                ConsoleColor fg = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine("\nSkipping Backup...");
                Console.ForegroundColor = fg;
            }
            else
            {
                backup.StartBackup(false);
            }
            // Adding a custom verification
            backup.AddVerification((string source, string target, bool debug) => {
                    Report.Log(ReportLevel.Warning, "\nCustom verification:\n- - - - - - - - - - -");
                    Report.Log(ReportLevel.Info, "Source: "+source);
                    Report.Log(ReportLevel.Info, "Target: "+target);
                    Report.Log(ReportLevel.Info, "Debug: "+debug);
                });
            backup.Verify();

            Console.WriteLine("\nPress any key to exit");
            Console.ReadKey();
        }

        /// <summary>
        /// Prints the help dialog to the console
        /// </summary>
        private static void ShowHelp()
        {
            Console.WriteLine("Copies all files from <source> to <target> and verifies that the copying was successful\n");
            Console.WriteLine("Backup [-s] [-t] [-v] [-d] [-h]\n");
            Console.WriteLine(" source       Defines the source directory");
            Console.WriteLine(" target       Defines the target directory");
            Console.WriteLine("\n");
            Console.WriteLine(" --source -s  Sets the source directory");
            Console.WriteLine(" --target -t  Sets the target directory");
            Console.WriteLine(" --verify -v  Only run the verification");
            Console.WriteLine(" --debug  -d  Show additional output");
            Console.WriteLine(" --help   -h  Displays this help");
        }
    }

    /// <summary>
    /// Class for reporting output to the user and logging actions
    /// </summary>
    class Report
    {
        /// <summary>
        /// Logs messages in different colors to the console
        /// </summary>
        /// <param name="level">The report level (Determines the color)</param>
        /// <param name="message">The message to log</param>
        public static void Log(ReportLevel level = ReportLevel.Unknown, string message = "")
        {
            ConsoleColor fg = Console.ForegroundColor;
            // ConsoleColor bg = Console.BackgroundColor;
            switch (level)
            {
                case ReportLevel.Info:
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    break;
                case ReportLevel.Debug:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case ReportLevel.Warning:
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    break;
                case ReportLevel.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                default:
                    break;
            }
            Console.WriteLine(message);
            Console.ForegroundColor = fg;
            // Console.BackgroundColor = bg;
        }
    }

    /// <summary>
    /// Enum that symbolizes different report levels
    /// </summary>
    enum ReportLevel
    {
        /// <summary>
        /// Symbolizes the standard report level
        /// </summary>
        Info,
        /// <summary>
        /// Used for additional information
        /// </summary>
        Debug,
        /// <summary>
        /// Indicates a warning
        /// </summary>
        Warning,
        /// <summary>
        /// Symbolizes an error
        /// </summary>
        Error,
        /// <summary>
        /// This value can be referenced when the report level is unknown
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Base class for IO operations
    /// </summary>
    class IOBase
    {
        /// <summary>
        /// The source directory
        /// </summary>
        protected string Source { get; set; }
        /// <summary>
        /// The target directory
        /// </summary>
        protected string Target { get; set; }
        /// <summary>
        /// Whether or not debug output is enabled
        /// </summary>
        protected bool debug = false;

        /// <summary>
        /// Copies a file from source to the mirrored directory at target
        /// </summary>
        /// <param name="file">The file to copy</param>
        /// <param name="overwrite">Whether existing files should be overwritten</param>
        /// <returns>Whether the file was copied</returns>
        protected bool CopyFile(string file, bool overwrite=false)
        {
            FileInfo fi;
            try
            {
                fi = new FileInfo(file);
            }
            catch (FileNotFoundException)
            {
                Report.Log(ReportLevel.Error, $"File \"{Path.GetFullPath(file)}\" does not exist anymore!");
                return false;
            }
            if (File.Exists(Target + fi.FullName[Source.Length..]))
            {
                if (overwrite)
                {
                    File.Delete(Target + fi.FullName[Source.Length..]);
                }
                else
                {
                    return false;
                }
            }
            
            try
            {
                File.Copy(fi.FullName, Target + fi.FullName[Source.Length..]);
                return true;
            }
            catch (DirectoryNotFoundException)
            {
                Directory.CreateDirectory(new FileInfo(Target + fi.FullName[Source.Length..]).DirectoryName);
                File.Copy(fi.FullName, Target + fi.FullName[Source.Length..]);
            }
            return true;
        }

        /// <summary>
        /// Throws UnauthorizedAccessException if the access fails
        /// </summary>
        /// <param name="rootDirectory">The directory to test</param>
        /// <param name="andTestWrite">Whether or not additional write privileges should be tested</param>
        public static void TestRead(string rootDirectory, bool andTestWrite = false)
        {
            // Read
            Directory.GetDirectories(rootDirectory);
            
            // Write
            if (andTestWrite)
            {
                byte[] hashbytes = new SHA1Managed().ComputeHash(Encoding.UTF8.GetBytes("testReadWrite"));
                StringBuilder builder = new StringBuilder(hashbytes.Length * 2);
                foreach (byte _byte in hashbytes)
                {
                    builder.Append(_byte.ToString("x2"));
                }
                string directoryName = builder.ToString();
                Directory.CreateDirectory(Path.Combine(rootDirectory, directoryName));
                Directory.Delete(Path.Combine(rootDirectory, directoryName));
                File.OpenWrite(Path.Combine(rootDirectory, directoryName + ".txt")).Close();
                File.Delete(Path.Combine(rootDirectory, directoryName + ".txt"));
            }
        }
    }

    /// <summary>
    /// Representation of a user configuration
    /// </summary>
    class Config
    {
        /// <summary>
        /// The source directory
        /// </summary>
        public string Source { get; set; }
        /// <summary>
        /// The target directory
        /// </summary>
        public string Target { get; set; }
        /// <summary>
        /// The options for exporting json strings
        /// </summary>
        private readonly JsonSerializerOptions options = new JsonSerializerOptions
        {
            WriteIndented = true,
        };
        /// <summary>
        /// The filename for the config file
        /// </summary>
        private readonly string filename = Path.GetFullPath("backup.config");
        
        /// <summary>
        /// Loads the source and targets from the config file
        /// </summary>
        public void Load()
        {
            if (File.Exists(filename) && new FileInfo(filename).Length > 0)
            {
                string jsonString = File.ReadAllText(filename);
                Dictionary<string, string> data = null;
                try
                {
                    data = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);
                    // Source
                    try
                    {
                        if (data["Source"] != null)
                        {
                            string sourcePath = Path.GetFullPath(data["Source"]);
                            if (Directory.Exists(sourcePath))
                            {
                                Source = sourcePath;
                            }
                            else
                            {
                                Report.Log(ReportLevel.Error, $"Could not load source from config file...\nPlease take a look at '{filename}'");
                                Environment.Exit(-1);
                            }
                        }
                        else
                        {
                            Source = null;
                        }
                    }
                    catch (ArgumentNullException)
                    {
                        Report.Log(ReportLevel.Error, $"Could not load source from config file...\nPlease take a look at '{filename}'");
                        Environment.Exit(-1);
                    }
                    // Target
                    try
                    {
                        if (data["Target"] != null)
                        {
                            string targetPath = Path.GetFullPath(data["Target"]);
                            if (Directory.Exists(targetPath))
                            {
                                Target = targetPath;
                            }
                            else
                            {
                                Report.Log(ReportLevel.Error, $"Could not load target from config file...\nPlease take a look at '{filename}'");
                                Environment.Exit(-1);
                            }
                        }
                        else
                        {
                            Target = null;
                        }
                    }
                    catch (ArgumentNullException)
                    {
                        Report.Log(ReportLevel.Error, $"Could not load target from config file...\nPlease take a look at '{filename}'");
                        Environment.Exit(-1);
                    }
                }
                catch (JsonException)
                {
                    Report.Log(ReportLevel.Error, $"Could not deserialize config file!\nPlease take a look at '{filename}'");
                    Environment.Exit(-1);
                }
            }
            else
            {
                Source = null;
                Target = null;
            }
        }

        /// <summary>
        /// Saves the source and target to the config file
        /// </summary>
        public void Save()
        {
            Dictionary<string, string> data = new() { { "Source", Source }, { "Target", Target} };
            string jsonString = JsonSerializer.Serialize(data, options);
            File.WriteAllText(filename, jsonString);
        }
    }

    /// <summary>
    /// Main backup class. Used to control the backup process.
    /// </summary>
    class Backup : IOBase
    {
        /// <summary>
        /// The cloner for copying the files
        /// </summary>
        private Cloner Cloner { get; set; }
        /// <summary>
        /// The verifier for determining the successful copying process
        /// </summary>
        private Verifier Verifier { get; set; }
        /// <summary>
        /// The user configuration
        /// </summary>
        private Config Config { get; set; } = new Config();
        /// <summary>
        /// The source directory
        /// </summary>
        private new string Source
        {
            get
            {
                return Config.Source;
            }
            set
            {
                Config.Source = value;
                Config.Save();
            }
        }
        /// <summary>
        /// The target directory
        /// </summary>
        private new string Target
        {
            get
            {
                return Config.Target;
            }
            set
            {
                Config.Target = value;
                Config.Save();
            }
        }
        /// <summary>
        /// A delegate for implementing custom verifications
        /// </summary>
        /// <param name="source">The source directory</param>
        /// <param name="target">The target directory</param>
        /// <param name="debug">Whether the program was started with debug enabled</param>
        public delegate void Verification(string source, string target, bool debug);
        /// <summary>
        /// The list of verifications
        /// </summary>
        private List<Verification> verifications = new List<Verification>();

        /// <summary>
        /// Constructor for a new backup instance
        /// </summary>
        /// <param name="args">Start parameters</param>
        public Backup(string[] args)
        {
            Config.Load();
            if (Source is not null)
            {
                Report.Log(ReportLevel.Info, "Loaded source from config");
            }
            if (Target is not null)
            {
                Report.Log(ReportLevel.Info, "Loaded target from config");
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLower().Equals("--source") || args[i].ToLower().Equals("-s"))
                {
                    try
                    {
                        if (args[i + 1].EndsWith("\""))
                        {
                            args[i + 1] = args[i + 1][0..^1];
                        }
                        if (Directory.Exists(Path.GetFullPath(args[i + 1])))
                        {
                            TestRead(Path.GetFullPath(args[i + 1]));
                            if (Source is not null && debug)
                            {
                                Report.Log(ReportLevel.Warning, "Overloaded source");
                            }
                            Source = Path.GetFullPath(args[i + 1]) + "\\";
                        }
                        else
                        {
                            Report.Log(ReportLevel.Error, $"The path \"{args[i + 1]}\" could not be found or is not a directory");
                        }
                    }
                    catch (Exception e) when (e is ArgumentException || e is IndexOutOfRangeException)
                    {
                        Report.Log(ReportLevel.Error, $"Please specify a path when using {args[i]}");
                        Environment.Exit(-1);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Report.Log(ReportLevel.Error, "Unauthorized path! Please choose another source or run this program as an authorized user.");
                        Environment.Exit(-1);
                    }
                }
                else if (args[i].ToLower().Equals("--target") || args[i].ToLower().Equals("-t"))
                {
                    try
                    {
                        if (args[i + 1].EndsWith("\""))
                        {
                            args[i + 1] = args[i + 1][0..^1];
                        }
                        if (Directory.Exists(Path.GetFullPath(args[i + 1])))
                        {
                            TestRead(Path.GetFullPath(args[i + 1]), true);
                            if (Target is not null && debug)
                            {
                                Report.Log(ReportLevel.Warning, "Overloaded target");
                            }
                            Target = Path.GetFullPath(args[i + 1]) + "\\";
                        }
                        else
                        {
                            Report.Log(ReportLevel.Error, $"The path \"{args[i + 1]}\" could not be found or is not a directory");
                        }
                    }
                    catch (Exception e) when (e is ArgumentException || e is IndexOutOfRangeException)
                    {
                        Report.Log(ReportLevel.Error, $"Please specify a path when using {args[i]}");
                        Environment.Exit(-1);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Report.Log(ReportLevel.Error, "Unauthorized path! Please choose another target or run this program as an authorized user.");
                        Environment.Exit(-1);
                    }
                }
                else if (args[i].ToLower().Equals("--debug") || args[i].ToLower().Equals("-d"))
                {
                    debug = true;
                }
            }

            if (Source == null || Target == null)
            {
                Report.Log(ReportLevel.Error, "Please specify both a valid source directory and a valid target directory!");
                Environment.Exit(-1);
            }
            if (Target.StartsWith(Source) || Target.Equals(Source))
            {
                if (Target.Equals(Source))
                {
                    Report.Log(ReportLevel.Error, "The target directory must not be equal to the source directory!");
                }
                else
                {
                    Report.Log(ReportLevel.Error, "The target directory must not be inside the source directory!");
                }
                Source = null;
                Target = null;
                Config.Save();
                Environment.Exit(-1);
            }

            Report.Log(ReportLevel.Info, $"Used source: \"{Source}\"");
            Report.Log(ReportLevel.Info, $"Used target: \"{Target}\"");
            Cloner = new Cloner(Source, Target, debug);
            Verifier = new Verifier(Source, Target, debug);
        }

        /// <summary>
        /// Method to add an additional verification to the verification process
        /// </summary>
        /// <param name="verification">The verification delegate</param>
        public void AddVerification(Verification verification)
        {
            verifications.Add(verification);
        }

        /// <summary>
        /// Resets additional verifications so that only the standard verification is run
        /// </summary>
        public void ResetVerifications()
        {
            verifications = new List<Verification>();
        }

        /// <summary>
        /// Starts the backup process
        /// </summary>
        /// <param name="overwrite">Whether to overwrite existing files in the target directory</param>
        public void StartBackup(bool overwrite = false)
        {
            Cloner.Backup(overwrite);
        }

        /// <summary>
        /// Starts the verification process
        /// </summary>
        public void Verify()
        {
            Verifier.Verify();
            foreach (Verification verification in verifications)
            {
                verification(Source, Target, debug);
            }
        }
    }

    /// <summary>
    /// Class that handles copying of files and directories
    /// </summary>
    class Cloner : IOBase
    {
        /// <summary>
        /// A cloner is used to traverse a path and copy each file from <paramref name="source"/> to <paramref name="target"/>.
        /// </summary>
        /// <param name="source">The source directory</param>
        /// <param name="target">The target directory</param>
        /// <param name="debug">Whether or not debug output is enabled</param>
        public Cloner(string source, string target, bool debug=false)
        {
            Source = source;
            Target = target;
            this.debug = debug;
        }

        /// <summary>
        /// Starts the backup progress
        /// traverses each directory starting from source and copies files and folders to target
        /// </summary>
        /// <param name="overwrite">Whether existing files should be overwritten</param>
        public void Backup(bool overwrite = false)
        {
            Report.Log(ReportLevel.Info, "\nStarting Backup...\n");
            Stack<string> dirs = new();
            dirs.Push(Source);
            if (debug)
            {
                Report.Log(ReportLevel.Debug, "Indexing files...");
                int fileCount = Directory.GetFiles(Source, "*", SearchOption.AllDirectories).Length;
                Report.Log(ReportLevel.Debug, $"Processing {fileCount} files.");
            }

            while (dirs.Count > 0)
            {
                string currentDir = dirs.Pop();
                string[] subDirs;
                try
                {
                    subDirs = Directory.GetDirectories(currentDir);
                    foreach (string str in subDirs)
                    {
                        dirs.Push(str);
                        if (debug)
                        {
                            Report.Log(ReportLevel.Debug, $"Added directory \"{str}\" to Stack.");
                        }
                    }
                }
                catch (Exception e) when (e is UnauthorizedAccessException || e is DirectoryNotFoundException)
                {
                    Report.Log(ReportLevel.Error, e.Message);
                    continue;
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(currentDir);
                }

                catch (Exception e) when (e is UnauthorizedAccessException || e is DirectoryNotFoundException)
                {
                    Report.Log(ReportLevel.Error, e.Message);
                    continue;
                }

                foreach (string file in files)
                {
                    CopyFile(file, overwrite);
                    if (debug)
                    {
                        Report.Log(ReportLevel.Debug, $"Copied file \"{file}\".");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Class for verifying the succesfull copying process
    /// </summary>
    class Verifier : IOBase
    {
        /// <summary>
        /// The amount of indexed files
        /// </summary>
        private int fileCount;

        /// <summary>
        /// Initializes a new Verifier instance
        /// </summary>
        /// <param name="source">The source directory</param>
        /// <param name="target">The target directory</param>
        /// <param name="debug">Whether or not debug output is enabled</param>
        public Verifier(string source, string target, bool debug)
        {
            Source = source;
            Target = target;
            this.debug = debug;
        }

        /// <summary>
        /// Verifies that each file was copied correctly
        /// </summary>
        /// <param name="pass">The iteration</param>
        public void Verify(int pass = 1)
        {
            Report.Log(ReportLevel.Info, $"\nStarting Verification (Pass: {pass})...\n");
            Stack<string> dirs = new();
            dirs.Push(Source);
            if (debug)
            {
                if (pass == 1)
                {
                    Report.Log(ReportLevel.Debug, "Indexing files...");
                    fileCount = Directory.GetFiles(Source, "*", SearchOption.AllDirectories).Length;
                }
                Report.Log(ReportLevel.Debug, $"Processing {fileCount} files.");
            }
            List<string> lostFiles = new();

            while (dirs.Count > 0)
            {
                string currentDir = dirs.Pop();
                string[] subDirs;
                try
                {
                    subDirs = Directory.GetDirectories(currentDir);
                    foreach (string str in subDirs)
                    {
                        dirs.Push(str);
                        if (debug)
                        {
                            Report.Log(ReportLevel.Debug, $"Verifying directory \"{str}\"...");
                        }
                    }
                }
                catch (Exception e) when (e is UnauthorizedAccessException || e is DirectoryNotFoundException)
                {
                    Report.Log(ReportLevel.Error, e.Message);
                    continue;
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(currentDir);
                }

                catch (Exception e) when (e is UnauthorizedAccessException || e is DirectoryNotFoundException)
                {
                    Report.Log(ReportLevel.Error, e.Message);
                    continue;
                }

                foreach (string file in files)
                {
                    if (debug)
                    {
                        Report.Log(ReportLevel.Debug, $"Verifying file \"{file}\".");
                    }
                    FileInfo fi;
                    try
                    {
                        fi = new FileInfo(file);
                        try
                        {
                            FileInfo targetFile = new(Target + fi.FullName[Source.Length..]);
                            if (fi.Length != targetFile.Length) // Partial copy?
                            {
                                lostFiles.Add(fi.FullName);
                                Report.Log(ReportLevel.Warning, $"Detected issue: \"{fi.FullName}\" differs in length from its clone.");
                            }
                        }
                        catch (FileNotFoundException)
                        {
                            lostFiles.Add(fi.FullName);
                            Report.Log(ReportLevel.Warning, $"Detected issue: \"{fi.FullName}\" was not copied.");
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        Report.Log(ReportLevel.Error, $"File \"{Path.GetFullPath(file)}\" does not exist anymore!");
                        continue;
                    }
                }
            }

            Console.WriteLine();
            if (lostFiles.Count > 0)
            {
                Report.Log(ReportLevel.Info, $"Found {lostFiles.Count} issues!");
                foreach (string file in lostFiles)
                {
                    Report.Log(ReportLevel.Error, $"File \"{file}\" is missing!");
                    Report.Log(ReportLevel.Warning, "Attempting to fix issue");
                    if (CopyFile(file, false))
                    {
                        Report.Log(ReportLevel.Info, "Success!");
                    }
                    else
                    {
                        Report.Log(ReportLevel.Error, "Failed to resolve issue");
                    }
                }
                Verify(pass+1);
            }
            else
            {
                Report.Log(ReportLevel.Info, "Everything okay!");
            }
        }
    }
}