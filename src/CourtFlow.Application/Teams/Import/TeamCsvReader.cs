using System.Text;

namespace CourtFlow.Application.Teams.Import;

public sealed record TeamCsvRow(int Line, string Club, string Team, string Division, string Gender, string? Error);

public sealed record TeamCsvFile(string? Error, IReadOnlyList<TeamCsvRow> Rows);

/// <summary>Reads the teams CSV: a header naming Club, Team, Division, and Gender, then one team per row.</summary>
public static class TeamCsvReader
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public const string TooLarge = "The file is larger than 2 MB.";
    public const string NotUtf8 = "Save the file as CSV UTF-8 and try again.";
    public const string Empty = "The file is empty.";
    public const string NoTeams = "The file has no teams.";
    public const string ExtraValues = "This row has more values than the header.";

    public static readonly IReadOnlyList<string> Columns = ["Club", "Team", "Division", "Gender"];

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static string MissingColumns(IReadOnlyList<string> missing) =>
        $"The file is missing the {JoinWithAnd(missing)} column{(missing.Count == 1 ? "" : "s")}.";

    public static string DuplicateColumn(string column) => $"The file has more than one {column} column.";

    public static TeamCsvFile Read(byte[] bytes)
    {
        if (bytes.Length > MaxBytes)
            return Failed(TooLarge);

        string text;
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Failed(NotUtf8);
        }

        if (text.StartsWith('\uFEFF'))
            text = text[1..];

        var records = Parse(text)
            .Where(record => record.Fields.Any(field => field.Trim().Length > 0))
            .ToList();
        if (records.Count == 0)
            return Failed(Empty);

        var header = records[0].Fields.Select(field => field.Trim()).ToList();
        var indexes = new Dictionary<string, int>();
        var missing = new List<string>();
        foreach (var column in Columns)
        {
            var matches = header
                .Select((name, index) => (name, index))
                .Where(item => string.Equals(item.name, column, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count > 1)
                return Failed(DuplicateColumn(column));
            if (matches.Count == 0)
                missing.Add(column);
            else
                indexes[column] = matches[0].index;
        }

        if (missing.Count > 0)
            return Failed(MissingColumns(missing));
        if (records.Count == 1)
            return Failed(NoTeams);

        var rows = records
            .Skip(1)
            .Select(record =>
            {
                string Value(string column) =>
                    indexes[column] < record.Fields.Count ? record.Fields[indexes[column]].Trim() : "";

                return new TeamCsvRow(
                    record.Line,
                    Value("Club"),
                    Value("Team"),
                    Value("Division"),
                    Value("Gender"),
                    record.Fields.Count > header.Count ? ExtraValues : null);
            })
            .ToList();
        return new TeamCsvFile(null, rows);
    }

    internal static string JoinWithAnd(IReadOnlyList<string> items) =>
        items.Count switch
        {
            1 => items[0],
            2 => $"{items[0]} and {items[1]}",
            _ => $"{string.Join(", ", items.Take(items.Count - 1))}, and {items[^1]}",
        };

    private static TeamCsvFile Failed(string error) => new(error, []);

    private sealed record Record(int Line, List<string> Fields);

    /// <summary>RFC 4180 records. A record's line is the physical line it starts on; quoted line breaks advance the count.</summary>
    private static IEnumerable<Record> Parse(string text)
    {
        var line = 1;
        var position = 0;
        while (position < text.Length)
        {
            var record = new Record(line, []);
            var field = new StringBuilder();
            var inQuotes = false;
            var atFieldStart = true;
            var ended = false;

            while (position < text.Length && !ended)
            {
                var current = text[position];
                if (inQuotes)
                {
                    if (current == '"')
                    {
                        if (position + 1 < text.Length && text[position + 1] == '"')
                        {
                            field.Append('"');
                            position += 2;
                            continue;
                        }

                        inQuotes = false;
                    }
                    else
                    {
                        if (IsLineBreak(text, ref position, out var lineBreak))
                        {
                            field.Append(lineBreak);
                            line++;
                            continue;
                        }

                        field.Append(current);
                    }

                    position++;
                    continue;
                }

                if (current == '"' && atFieldStart)
                {
                    inQuotes = true;
                    atFieldStart = false;
                    position++;
                }
                else if (current == ',')
                {
                    record.Fields.Add(field.ToString());
                    field.Clear();
                    atFieldStart = true;
                    position++;
                }
                else if (IsLineBreak(text, ref position, out _))
                {
                    line++;
                    ended = true;
                }
                else
                {
                    field.Append(current);
                    atFieldStart = false;
                    position++;
                }
            }

            record.Fields.Add(field.ToString());
            yield return record;
        }
    }

    /// <summary>Consumes CRLF, LF, or CR at the position and reports the text consumed.</summary>
    private static bool IsLineBreak(string text, ref int position, out string lineBreak)
    {
        if (text[position] == '\r')
        {
            var crlf = position + 1 < text.Length && text[position + 1] == '\n';
            lineBreak = crlf ? "\r\n" : "\r";
            position += lineBreak.Length;
            return true;
        }

        if (text[position] == '\n')
        {
            lineBreak = "\n";
            position++;
            return true;
        }

        lineBreak = "";
        return false;
    }
}
