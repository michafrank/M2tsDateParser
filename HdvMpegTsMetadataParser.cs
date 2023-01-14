namespace M2tsMetadataDateParser;

/**
 * Links:
 * https://github.com/MediaArea/MediaInfoLib/blob/master/Source/MediaInfo/Multiple/File_MpegPs.cpp
 */

public class HdvMpegTsMetadataParser : Parser
{
    private const int packetLength = 188;
    private const int maxPacketRead = 50000;
    private const int bufferSize = packetLength * 5000;
    
    // Sometimes the first found date seems to be from a previous scene
    // Select how many dates are skipped before a result is returned
    private const int skipDates = 1; 

    public static ICollection<string> SupportedExtensions => new string[] { ".m2t" };

    public bool ReadDate(Stream inputStream, out DateTime date, out string error)
    {
        var buffer = new byte[bufferSize];
        int datesFound = 0;
        int packetNumber = 1;
        date = DateTime.MinValue;
        error = "";

        int bytesRead = inputStream.Read(buffer, 0, buffer.Length);
        Span<byte> bytes = buffer;

        while (bytesRead > 0 && packetNumber < maxPacketRead)
        {
            int readOffset = 0;

            while (bytesRead - readOffset >= packetLength)
            {
                Span<byte> packetData = bytes.Slice(readOffset, packetLength);
                if (packetData[0] != 0x47)
                {
                    error = "TS packet sync byte 0x47 not found";
                    return false;
                }                

                int pid = (packetData[1] & 0x1F) << 8 | packetData[2];
                
                if (pid == 2065 && ReadDateFromPacket(packetData, out date))
                {                   
                    if (++datesFound > skipDates)
                    {
                        return true;
                    }
                }
                readOffset += packetLength;
                packetNumber++;
            }
            bytesRead = inputStream.Read(buffer, 0, buffer.Length);
        }

        return false;
    }

    private bool ReadDateFromPacket(Span<byte> packetData, out DateTime date)
    {
        // skip first 4 bytes of TS packet
        int offset = 4;
        while (offset < packetData.Length - 50)
        {
            var data = packetData[offset..];

            // search for PES packet header with id 0xBF
            if (data[0] == 0x0 && data[1] == 0x0 && data[2] == 0x1 && data[3] == 0xBF)
            {
                // skip PES packet header (4 bytes) and 2 bytes packet size and the following 36 bytes
                data = data[42..];

                byte day = (byte)(data[0] & 0x3F);
                byte month = (byte)(data[1] & 0x1F);
                byte year = data[2];

                byte second = (byte)(data[4] & 0x7F);
                byte minute = (byte)(data[5] & 0x7F);
                byte hour = (byte)(data[6] & 0x3F);

                return DateTime.TryParse($"20{year:x2}-{month:x2}-{day:x2} {hour:x2}:{minute:x2}:{second:x2}", out date);
            }
            offset++;
        }
        date = DateTime.MinValue;
        return false;
    }
}
