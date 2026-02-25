namespace my_gpx_activities.ApiService.Services;

public record FitDataPoint(DateTime Timestamp, int? HeartRate);

public interface IFitParserService
{
    Task<List<FitDataPoint>> ParseFitAsync(Stream fitStream);
}

public class FitParserService : IFitParserService
{
    // FIT protocol epoch: December 31, 1989, 00:00:00 UTC
    private static readonly DateTime FitEpoch = new DateTime(1989, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    private record FieldDef(int FieldDefNum, int Size);
    private record LocalMessageDef(int GlobalMessageNumber, bool BigEndian, List<FieldDef> Fields);

    public async Task<List<FitDataPoint>> ParseFitAsync(Stream fitStream)
    {
        using var ms = new MemoryStream();
        await fitStream.CopyToAsync(ms);
        var data = ms.ToArray();

        if (data.Length < 14)
            throw new InvalidDataException("FIT file is too small to be valid.");

        if (data[8] != '.' || data[9] != 'F' || data[10] != 'I' || data[11] != 'T')
            throw new InvalidDataException("Not a valid FIT file (missing .FIT magic bytes).");

        var headerSize = data[0];
        var dataSize = BitConverter.ToInt32(data, 4);
        var dataEnd = headerSize + dataSize;

        var definitions = new Dictionary<int, LocalMessageDef>();
        var results = new List<FitDataPoint>();
        uint lastTimestamp = 0;
        int pos = headerSize;

        while (pos < dataEnd && pos < data.Length)
        {
            var recordHeader = data[pos++];

            if ((recordHeader & 0x80) != 0)
            {
                // Compressed timestamp header: timestamp is NOT in the data payload
                var localType = (recordHeader >> 5) & 0x03;
                var timeOffset = (uint)(recordHeader & 0x1F);
                var timestamp = (lastTimestamp & 0xFFFFFFE0) | timeOffset;
                if (timestamp < lastTimestamp) timestamp += 32;
                lastTimestamp = timestamp;

                if (definitions.TryGetValue(localType, out var compDef) && compDef.GlobalMessageNumber == 20)
                {
                    int? heartRate = null;
                    foreach (var field in compDef.Fields)
                    {
                        if (field.FieldDefNum == 253)
                            continue; // timestamp not present in compressed payload
                        if (field.FieldDefNum == 3 && field.Size == 1)
                            heartRate = data[pos] != 0xFF ? data[pos] : null;
                        pos += field.Size;
                    }
                    var dt = FitEpoch.AddSeconds(timestamp);
                    results.Add(new FitDataPoint(dt, heartRate));
                }
                else if (definitions.TryGetValue(localType, out var skipDef))
                {
                    foreach (var field in skipDef.Fields)
                        if (field.FieldDefNum != 253) pos += field.Size;
                }
                continue;
            }

            var isDefinition = (recordHeader & 0x40) != 0;
            var hasDeveloperFields = (recordHeader & 0x20) != 0;
            var localMsgType = recordHeader & 0x0F;

            if (isDefinition)
            {
                pos++; // reserved byte
                var bigEndian = data[pos++] == 1;
                var globalMsgNum = bigEndian
                    ? (data[pos] << 8) | data[pos + 1]
                    : data[pos] | (data[pos + 1] << 8);
                pos += 2;
                var numFields = data[pos++];

                var fields = new List<FieldDef>();
                for (int i = 0; i < numFields; i++)
                {
                    var fieldDefNum = data[pos++];
                    var fieldSize = data[pos++];
                    pos++; // base type byte
                    fields.Add(new FieldDef(fieldDefNum, fieldSize));
                }

                if (hasDeveloperFields)
                {
                    var numDevFields = data[pos++];
                    for (int i = 0; i < numDevFields; i++)
                    {
                        pos++; // field number
                        var fieldSize = data[pos++];
                        pos++; // developer data index
                        fields.Add(new FieldDef(-1, fieldSize));
                    }
                }

                definitions[localMsgType] = new LocalMessageDef(globalMsgNum, bigEndian, fields);
            }
            else
            {
                if (!definitions.TryGetValue(localMsgType, out var def))
                    break; // cannot parse further without definition

                if (def.GlobalMessageNumber == 20) // FIT record message
                {
                    uint? timestamp = null;
                    int? heartRate = null;

                    foreach (var field in def.Fields)
                    {
                        if (field.FieldDefNum == 253 && field.Size == 4) // timestamp
                        {
                            timestamp = def.BigEndian
                                ? ((uint)data[pos] << 24) | ((uint)data[pos + 1] << 16) | ((uint)data[pos + 2] << 8) | data[pos + 3]
                                : BitConverter.ToUInt32(data, pos);
                        }
                        else if (field.FieldDefNum == 3 && field.Size == 1) // heart_rate
                        {
                            heartRate = data[pos] != 0xFF ? data[pos] : null;
                        }
                        pos += field.Size;
                    }

                    if (timestamp.HasValue)
                    {
                        lastTimestamp = timestamp.Value;
                        var dt = FitEpoch.AddSeconds(timestamp.Value);
                        results.Add(new FitDataPoint(dt, heartRate));
                    }
                }
                else
                {
                    pos += def.Fields.Sum(f => f.Size);
                }
            }
        }

        return results;
    }
}
