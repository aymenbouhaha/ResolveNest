using System.Text;

namespace ResolveNest.Api.Features.Ingestion;

public static class CsvTableParser
{
    public static IReadOnlyList<CsvSourceRow> Parse(string content, string displayPath)
    {
        var lines = SplitRecords(content, displayPath);
        if (lines.Count == 0)
        {
            throw new SeedSourceException($"{displayPath} has no header row.");
        }

        var header = lines[0];
        var rows = new List<CsvSourceRow>(lines.Count - 1);
        for (var index = 1; index < lines.Count; index++)
        {
            var rowNumber = index + 1;
            var fields = lines[index];
            if (fields.Count != header.Count)
            {
                throw new SeedSourceException($"{displayPath} row {rowNumber} has {fields.Count} fields, expected {header.Count}.");
            }

            var values = new Dictionary<string, string>(header.Count, StringComparer.Ordinal);
            for (var column = 0; column < header.Count; column++)
            {
                values[header[column]] = fields[column];
            }

            rows.Add(new CsvSourceRow(new SourceLocation(displayPath, $"row={rowNumber}"), values));
        }

        return rows;
    }

    private static List<List<string>> SplitRecords(string content, string displayPath)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var insideQuotes = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (insideQuotes)
            {
                if (character == '"' && index + 1 < content.Length && content[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else if (character == '"')
                {
                    insideQuotes = false;
                }
                else
                {
                    field.Append(character);
                }
            }
            else if (character == '"')
            {
                insideQuotes = true;
            }
            else if (character == ',')
            {
                record.Add(field.ToString());
                field.Clear();
            }
            else if (character is '\n' or '\r')
            {
                if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                {
                    index++;
                }

                record.Add(field.ToString());
                records.Add(record);
                record = [];
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        if (insideQuotes)
        {
            throw new SeedSourceException($"{displayPath} ends inside a quoted field.");
        }

        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }

        return records;
    }
}
