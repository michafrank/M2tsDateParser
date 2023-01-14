namespace M2tsMetadataDateParser;

/**
 * Links:
 * https://github.com/exiftool/exiftool/blob/master/lib/Image/ExifTool/H264.pm
 * http://hirntier.blogspot.com/2010/02/avchd-timecode-update.html
*/

/// <summary>
/// Read original date time from Blu-ray disc Audio-Video (BDAV) MPEG-2 Transport Stream (M2TS)
/// </summary>
public class AvchdMpegTsMetadataParser : Parser
{
    private const int packetLength = 192;
    private const int dateTimeLength = 10;
    private const int maxPacketRead = 1000;
    private const int bufferSize = packetLength * 100;

    public static ICollection<string> SupportedExtensions => new string[] { ".m2ts", ".mts" };

    public bool ReadDate(Stream inputStream, out DateTime date, out string error)
    {
        var buffer = new byte[bufferSize];
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
                if (packetData[4] != 0x47)
                {
                    error = "Sync byte 0x47 not found";
                    return false;
                }
                if (ReadDateFromPacket(packetData, out date))
                {
                    return true;
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
        int mdpmOffset = FindMdpmOffset(packetData);
        if (mdpmOffset > 0)
        {
            string decodedDate = decodeDateTime(packetData[mdpmOffset..]);
            return DateTime.TryParse(decodedDate, out date);            
        }
        date = DateTime.MinValue;
        return false;
    }

    private string decodeDateTime(Span<byte> d)
    {
        // Byte: description
        // 0: Year and month tag: 0x18

        // 1: timezone information:
        //   0x80 - unused
        //   0x40 - DST flag
        //   0x20 - TimeZoneSign
        //   0x1e - TimeZoneValue
        //   0x01 - half-hour flag
        byte timezoneInfo = d[1];
        // string dst = ((timezoneInfo & 0x40) == 0x40) ? " DST" : "";
        string timeZoneSign = (timezoneInfo & 0x20) == 0x20 ? "-" : "+";
        string halfHourTimeZone = (timezoneInfo & 0x01) == 0x01 ? "30" : "00";
        int timeZone = timezoneInfo >> 1 & 0x0f;

        // --- BCD coded:
        // 2+3: Year
        // 4: Month

        // 5: Day and time tag: 0x19

        // --- BCD coded:
        // 6: Day
        // 7: Hour
        // 8: Minute
        // 9: Second

        return $"{d[2]:X2}{d[3]:X2}-{d[4]:X2}-{d[6]:X2} {d[7]:X2}:{d[8]:X2}:{d[9]:X2}{timeZoneSign}{timeZone:X2}:{halfHourTimeZone}";
    }

    private bool MatchUuid(Span<byte> data)
    {
        for (int i = 0; i < uuidBytes.Length; i++)
        {
            if (data[i] != uuidBytes[i])
            {
                return false;
            }
        }
        return true;
    }

    static byte[] uuidBytes = new byte[] {
        0x17, 0xee, 0x8c, 0x60, 0xf8, 0x4d, 0x11, 0xd9, 0x8c, 0xd6, 0x08, 0x00, 0x20, 0x0c, 0x9a, 0x66, // UUID
        0x4D, 0x44, 0x50, 0x4D // "MDPM"
    };

    /// <summary>
    /// Find the Modified Digital Video Pack Metadata (MDPM) of the unregistered user data with UUID
    /// 17ee8c60f84d11d98cd60800200c9a66 in the H.264 Supplemental Enhancement Information (SEI)
    /// plus "MDPM" for "ModifiedDVPackMeta"   
    /// </summary>
    private int FindMdpmOffset(Span<byte> data)
    {
        // skip first 8 bytes of TS packet:
        // 4 bytes arrival timestamp
        // 4 bytes PID
        int offset = 8;

        while (offset < data.Length - uuidBytes.Length - dateTimeLength)
        {
            if (MatchUuid(data[offset..]))
            {
                return offset + uuidBytes.Length + 1; // return position one byte after "MDPM"
            }
            offset++;
        }
        return -1;
    }
}
