using System.Collections.Frozen;
using System.Text;

internal static class CsvReader
{
	public static IEnumerable<FrozenDictionary<string, string>> ReadCsvFile(string path)
	{
		using StreamReader sr = File.OpenText(path);

		string[] header =
			ParseCsvLine(sr)
			?? throw new FormatException("CSV file is empty (missing header row).");

		while (TryParseCsvLine(sr) is string[] row)
			yield return row.Length == header.Length
				? header.Zip(row, KeyValuePair.Create).ToFrozenDictionary()
				: throw new FormatException(
					$"CSV row has {row.Length} fields; expected {header.Length}."
				);
	}

	private static string[]? TryParseCsvLine(TextReader reader)
	{
		while (reader.Peek() is int c and not -1)
			if (c is '\r' or '\n')
				ConsumeNewline(reader);
			else if (char.IsWhiteSpace((char)c))
				reader.Read();
			else
				return ParseCsvLine(reader);
		return null;
	}

	private static void ConsumeNewline(TextReader reader)
	{
		if (reader.Read() is '\r' && reader.Peek() is '\n')
			reader.Read();
	}

	private static string[] ParseCsvLine(TextReader reader)
	{
		List<string> values = [];
		StringBuilder sb = new();

		bool inQuotes = false;

		while (reader.Peek() is int c and not -1)
		{
			// Stop at newline only when not in quotes
			if (!inQuotes && (c is '\r' or '\n'))
			{
				ConsumeNewline(reader);
				break;
			}

			_ = reader.Read();

			switch ((char)c)
			{
				case '"' when inQuotes:
					if (reader.Peek() == '"')
					{ // Escaped quote: "" => "
						_ = reader.Read();
						goto default;
					}
					inQuotes = false;
					break;

				case '"' when !inQuotes:
					if (sb.Length != 0)
						throw new FormatException(
							"Invalid CSV: quote encountered in middle of an unquoted value."
						);
					inQuotes = true;
					break;

				case ',' when !inQuotes:
					values.Add(sb.ToString());
					sb.Clear();
					break;

				default:
					sb.Append((char)c);
					break;
			}
		}

		if (inQuotes)
			throw new FormatException("CSV line has an unterminated quoted value.");

		values.Add(sb.ToString());
		return [.. values];
	}
}
