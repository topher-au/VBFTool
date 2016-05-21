using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using VBFTool.Properties;

namespace VBFTool
{
    class Program
    {
        static VirtuosBigFileReader vbfReader = new VirtuosBigFileReader();

        private static string outputDir = null;

        private static bool buildVbf = false;
        private static string buildDir = string.Empty;

        private static bool logEnabled = false;
        private static string logFile = string.Empty;

        static void Main(string[] args)
        {
            var currentVersion = Assembly.GetEntryAssembly().GetName().Version;
            var versionString = $"{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}.{currentVersion.Revision}";

            Console.WriteLine($"VBFTool v{versionString} by Topher\n" +
                              $"---------------------------\n");

            var vbfFile = args[args.Length - 1];

            if (args.Length > 1)
            {
                ProcessArgs(args);
            }

            if (buildVbf)
            {
                BuildVBF(buildDir, vbfFile);
            }
            else
            {
                if(!File.Exists(vbfFile))
                    PrintHelp($"Unable to find file: {args[0]}");
                ExtractVBF(vbfFile);
            }
            Console.Read();
        }

        static void ProcessArgs(string[] args)
        {
            for(int i=0; i<args.Length; i++)
                switch (args[i].ToLower())
                {
                    case "-o":
                        if (i + 1 < args.Length)
                            outputDir = args[i + 1];
                        break;
                    case "-l":
                        if (i + 1 < args.Length)
                        {
                            logFile = args[i + 1];
                            logEnabled = true;
                        }
                        break;
                    case "-b":
                        if (i + 1 < args.Length)
                        {
                            buildDir = args[i + 1];
                            buildVbf = true;
                            if (!Directory.Exists(buildDir))
                                PrintHelp("Please specify a valid source directory!");
                        }
                        break;
                    default:
                        break;
                }
        }

        static void PrintHelp(string message)
        {
            Console.WriteLine("A tool for extracting and rebuilding VBF files for Final Fantasy X/X-2 HD Remaster\n");
            Console.WriteLine($"Extraction:\n" +
                              $"  vbfextract <-o OutputDirectory> <-l LogFile.txt> <InputFile.VBF>\n");
            Console.WriteLine($"Rebuilding:\n" +
                              $"  vbfextract -b [SourceDirectory] <OutputFile.VBF>\n");
            Console.WriteLine("Parameters:\n" +
                              "  -o     Extract files to [OutputDirectory]\n" +
                              "  -l     Write extracted files to [LogFile.txt]\n" +
                              "  -b     Build [OutputFile.VBF] from files contained in [SourceDirectory]");
            if(message != string.Empty)
                Console.WriteLine("\nAn error occured:\n    " + message);
            Environment.Exit(0);
        }

        static void BuildVBF(string inputDir, string vbfFile)
        {
            var vbfW = new VirtuosBigFileWriter();
            vbfW.BuildVBF(inputDir, vbfFile);
        }

        static void ExtractVBF(string vbfFile)
        {
            vbfReader.LoadBigFileFile(vbfFile);
            var internalFileList = vbfReader.ReadFileList();

            // Attempt to extract files
            if (outputDir == null)
                outputDir = Path.GetFileNameWithoutExtension(vbfFile) + "_VBF";

            var successCount = 0;
            foreach (string file in internalFileList)
            {
                try
                {
                    var outPath = Path.Combine(outputDir, DenormalPath(file));
                    var outDir = Path.GetDirectoryName(outPath);
                    if (outDir != null && !Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

                    var extractedFile = vbfReader.ExtractFileContents(file, outPath);
                    if (!extractedFile) {
                        // File extraction failed
                        try
                        {
                            Console.WriteLine($"Failed to extract file: {file}");
                            File.Delete(outPath);
                        } catch { }
                        continue;
                    }
                    Console.WriteLine($"Extracted {file}");
                    if(logEnabled) File.AppendAllText(logFile, $"{file}\r\n");
                    successCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading file: {file} - {ex.Message}");
                    continue;
                }
            }

            Console.WriteLine($"\nExtraction completed successfully!\nExtracted {successCount}/{vbfReader.NumFiles} files");
        }

        static string DenormalPath(string path)
        {
            return path.Replace('/', '\\').TrimStart('\\');
        }
    }
}
