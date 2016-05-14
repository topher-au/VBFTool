using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VBFTool;

namespace vbfextract
{
    class Program
    {
        static VirtuosBigFileReader vbfReader = new VirtuosBigFileReader();
        private static string fileList = "filelist.txt";
        private static string outputDir = "output";
        const string OutPath = "output";

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
                              " -o <directory>      Set output directory to <directory>\n" +
                              " -f <file>           Read list of file names from <file>\n");
            Console.WriteLine(message);
            Environment.Exit(0);
        }

        static void ExtractVBF(string vbfFile)
        {
            vbfReader.LoadBigFileFile(vbfFile);

            var fileDictionary = File.ReadAllLines(fileList);
            var fixDictionary = new List<string>();

            foreach(var file in fileDictionary)
                if(!fixDictionary.Contains(file)) fixDictionary.Add(file);

            foreach (string file in fixDictionary)
            {
                try
                {
                    var outPath = Path.Combine(outputDir, DenormalPath(file));
                    var outDir = Path.GetDirectoryName(outPath);
                    if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                    var extractedFile = vbfReader.ExtractFileContents(file, outPath);
                    if (!extractedFile) continue; // No file found
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
