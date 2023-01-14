namespace M2tsMetadataDateParser;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage:  <path> ");
            return;
        }
        var path = args[0];
        ReadDateFromFile(path);
    }

    private static void ReadDateFromFile(string path)
    {
        using FileStream inStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var fileExtension = Path.GetExtension(path);

        Parser? parser = null;

        if (AvchdMpegTsMetadataParser.SupportedExtensions.Any(ext => fileExtension.Equals(ext, StringComparison.OrdinalIgnoreCase)))
        {
            parser = new AvchdMpegTsMetadataParser();
        }
        else if (HdvMpegTsMetadataParser.SupportedExtensions.Any(ext => fileExtension.Equals(ext, StringComparison.OrdinalIgnoreCase)))
        {
            parser = new HdvMpegTsMetadataParser();
        }

        if (parser != null)
        {
            if (parser.ReadDate(inStream, out DateTime date, out string error))
            {
                Console.WriteLine($"Found date time: {date}");
            }
            else if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine($"Error: {error}");
            }
            else
            {
                Console.WriteLine("No date could be found");
            }

        }
        else
        {
            Console.WriteLine("No parser available for " + fileExtension);
        }
    }
}
