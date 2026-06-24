using LibBundle3;
using LibBundle3.Records;
using System.Text;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("--list", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 3)
            {
                PrintUsage();
                return 1;
            }

            string bundlePrefix = args.Length >= 4 ? args[3] : "";
            return ListFiles(args[1], args[2], bundlePrefix);
        }

        if (args.Length < 3)
        {
            PrintUsage();
            return 1;
        }

        return ExtractFile(args[0], args[1], args[2]);
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  BundleExtractor <index.bin> <file_path> <output_path>");
        Console.WriteLine("  BundleExtractor --list <index.bin> <output_tsv> [bundle_prefix]");
        Console.WriteLine("  index.bin    : Path to _.index.bin");
        Console.WriteLine("  file_path    : File path in bundle (e.g. data/balance/baseitemtypes.datc64)");
        Console.WriteLine("  output_path  : Output file path");
        Console.WriteLine("  output_tsv   : TSV list containing path, bundle, size, and offset");
        Console.WriteLine("  bundle_prefix: Optional bundle path prefix filter, e.g. LibGGPK3/");
    }

    static int ListFiles(string indexPath, string outputPath, string bundlePrefix)
    {
        try
        {
            using var loaded = LoadIndex(indexPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

            using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));
            writer.WriteLine("path\tbundle\tsize\toffset");
            int written = 0;
            foreach (var file in loaded.Index.Files.Values.OrderBy(file => file.Path ?? ""))
            {
                if (string.IsNullOrWhiteSpace(file.Path))
                {
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(bundlePrefix) &&
                    !file.BundleRecord.Path.StartsWith(bundlePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                writer.WriteLine(
                    $"{CleanTsv(file.Path)}\t{CleanTsv(file.BundleRecord.Path)}\t{file.Size}\t{file.Offset}"
                );
                written++;
            }

            Console.WriteLine($"Listed {written} files to: {outputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            PrintError(ex);
            return 1;
        }
    }

    static int ExtractFile(string indexPath, string filePath, string outputPath)
    {
        try
        {
            using var loaded = LoadIndex(indexPath);

            FileRecord? targetFile = null;
            foreach (var file in loaded.Index.Files.Values)
            {
                if (file.Path?.Equals(filePath, StringComparison.OrdinalIgnoreCase) == true)
                {
                    targetFile = file;
                    break;
                }
            }

            if (targetFile == null)
            {
                foreach (var file in loaded.Index.Files.Values)
                {
                    if (file.Path?.Contains(filePath, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        Console.WriteLine($"Found similar file: {file.Path}");
                        targetFile = file;
                        break;
                    }
                }
            }

            if (targetFile == null)
            {
                Console.WriteLine($"Error: File not found in bundle: {filePath}");
                return 1;
            }

            Console.WriteLine($"Found file: {targetFile.Path}");
            Console.WriteLine($"  Size: {targetFile.Size} bytes");
            Console.WriteLine($"  Offset: {targetFile.Offset}");
            Console.WriteLine($"  Bundle: {targetFile.BundleRecord.Path}");

            Console.WriteLine("Reading file content...");
            byte[] data;
            using (var bundle = loaded.Factory.GetBundle(targetFile.BundleRecord))
            {
                data = targetFile.Read(bundle).ToArray();
            }

            string outputDir = Path.GetDirectoryName(outputPath) ?? "";
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            File.WriteAllBytes(outputPath, data);
            Console.WriteLine($"Successfully extracted to: {outputPath} ({data.Length} bytes)");

            return 0;
        }
        catch (Exception ex)
        {
            PrintError(ex);
            return 1;
        }
    }

    static LoadedIndex LoadIndex(string indexPath)
    {
        if (!File.Exists(indexPath))
        {
            throw new FileNotFoundException($"Index file not found: {indexPath}", indexPath);
        }

        string bundles2Dir = Path.GetDirectoryName(indexPath) ?? "";
        if (string.IsNullOrEmpty(bundles2Dir))
        {
            bundles2Dir = Environment.CurrentDirectory;
        }

        Console.WriteLine($"Loading index: {indexPath}");
        Console.WriteLine($"Bundles2 dir: {bundles2Dir}");

        var factory = new DriveBundleFactory(bundles2Dir);
        var index = new LibBundle3.Index(indexPath, false, factory);

        int failedPaths = index.ParsePaths();
        if (failedPaths > 0)
        {
            Console.WriteLine($"Warning: {failedPaths} files failed to parse paths (ignored)");
        }

        Console.WriteLine($"Index loaded. Files count: {index.Files.Count}");
        return new LoadedIndex(factory, index);
    }

    static string CleanTsv(string value)
    {
        return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
    }

    static void PrintError(Exception ex)
    {
        Console.WriteLine($"Error: {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
    }

    sealed class LoadedIndex : IDisposable
    {
        public LoadedIndex(DriveBundleFactory factory, LibBundle3.Index index)
        {
            Factory = factory;
            Index = index;
        }

        public DriveBundleFactory Factory { get; }

        public LibBundle3.Index Index { get; }

        public void Dispose()
        {
            Index.Dispose();
        }
    }
}
