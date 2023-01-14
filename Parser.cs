namespace M2tsMetadataDateParser
{
    public interface Parser
    {
        bool ReadDate(Stream inputStream, out DateTime date, out string error);
    }
}
