using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using vbfextract.Properties;
using VBFTool;

namespace vbfextract
{
    class Program
    {
        static VirtuosBigFileReader vbfReader = new VirtuosBigFileReader();

        private static bool useAltFileList = false;
        private static string fileList = "filelist.txt";

        private static string outputDir = null;

        private static string dict = null;

        private static Dictionary<string, string> dictInternal = new Dictionary<string, string>()
        {
            { "ffx_data", "FFX_Data_1" },
            { "ffx2_data", "FFX2_Data_1" },
            { "metamenu", "metamenu_1" },
        }; 

        static void Main(string[] args)
        {
            var currentVersion = Assembly.GetEntryAssembly().GetName().Version;
            var versionString = $"{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}";

            Console.WriteLine($"VBF File Extractor v{versionString} by Topher\n------------------------------------\n");

            if (args.Length == 1)
            {
                if (File.Exists(args[0]))
                    ExtractVBF(args[0]);
            }
            else if (args.Length > 1)
            {
                ProcessArgs(args);
                ExtractVBF(args[args.Length - 1]);
            }
            else
            {
                PrintHelp();
            }
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
                    case "-f":
                        if (i + 1 < args.Length)
                        {
                            fileList = args[i + 1];
                            if (!File.Exists(fileList)) PrintHelp("The dictionary file specified does not exist");
                            useAltFileList = true;
                        }
                        break;
                    case "-d":
                        if (i + 1 < args.Length)
                        {
                            try
                            {
                                dict = dictInternal[args[i + 1]];
                            }
                            catch
                            {
                                PrintHelp("The internal dictionary file specified does not exist");
                            }
                        }
                        break;
                    default:
                        break;
                }
        }

        static void PrintHelp(string message = "")
        {
            Console.WriteLine("Attempt to extract files from a VBF container using filename hashes generated\n" +
                              "from a list of possible file names.\n");
            Console.WriteLine($"Usage: vbfextract <parameters> <filename>\n");
            Console.WriteLine("Valid parameters:\n" +
                              " -o <directory>                          Set output directory to <directory>\n" +
                              " -f <file>                               Read list of file names from <file>\n" +
                              " -d <ffx_data/ffx2_data/metamenu>        Load a specific internal dictionary\n");
            Console.WriteLine(message);
            Environment.Exit(0);
        }

        static void ExtractVBF(string vbfFile)
        {
            vbfReader.LoadBigFileFile(vbfFile);

            List<string> dictionary = new List<string>();

            if (useAltFileList)
            {
                // Load dictionary from file
                Console.WriteLine($"Loading filename dictionary: {fileList}");
                var fileDictionary = File.ReadAllLines(fileList);

                foreach (var file in fileDictionary)
                    if (!dictionary.Contains(file)) dictionary.Add(file);
            }
            else
            {
                // Load internal dictionaries
                Console.WriteLine("Loading internal dictionary...\n");

                if (dict == null)
                {
                    var vbfName = Path.GetFileNameWithoutExtension(vbfFile)?.ToLower();
                    if(vbfName == null) PrintHelp("Unable to automatically detect dictionary.\n Start with -d parameter to force dictionary type.");

                    try
                    {
                        dict = dictInternal[vbfName];
                    }
                    catch
                    {
                        PrintHelp($"Unable to load dictionary for {vbfFile}.\nStart with -f or -d to manually specify the dictionary type.");
                    }
                }

                string dictObj = (string)Resources.ResourceManager.GetObject(dict);
                var dictLines = dictObj.Split('\n');
                foreach (var line in dictLines)
                {
                    dictionary.Add(line.Trim('\0'));
                }
            }

            // Attempt to extract files
            if (outputDir == null)
                outputDir = Path.GetFileNameWithoutExtension(vbfFile) + "_VBF";

            foreach (string file in dictionary)
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
                            File.Delete(outPath);
                        } catch { }
                        continue;
                    }
                    Console.WriteLine($"Extracted {file}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading file: {file} - {ex.Message}");
                    continue;
                }
            }
        }

        static string DenormalPath(string path)
        {
            return path.Replace('/', '\\').TrimStart('\\');
        }
    }
}
