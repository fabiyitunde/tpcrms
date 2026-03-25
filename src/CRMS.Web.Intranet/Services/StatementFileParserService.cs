using ClosedXML.Excel;
using CRMS.Web.Intranet.Models;
using System.Text;

namespace CRMS.Web.Intranet.Services;

/// <summary>
/// Parses CSV and Excel bank statement files into a list of transaction rows.
/// Supports auto-detection of column layout by header name matching.
/// </summary>
public class StatementFileParserService
{
    public StatementParseResult Parse(Stream stream, string fileName, DateTime periodStart, DateTime periodEnd, decimal openingBalance)
    {
        try
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".xlsx" or ".xls" => ParseExcel(stream, periodStart, periodEnd, openingBalance),
                _ => ParseCsv(stream, periodStart, periodEnd, openingBalance)
            };
        }
        catch (Exception ex)
        {
            return new StatementParseResult { Error = $"Failed to parse file: {ex.Message}" };
        }
    }

    // ────────────────────────────────────────────────────────── Excel ──

    private static StatementParseResult ParseExcel(Stream stream, DateTime periodStart, DateTime periodEnd, decimal openingBalance)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.FirstOrDefault();
        if (ws == null)
            return new StatementParseResult { Error = "No worksheets found in Excel file." };

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow == 0)
            return new StatementParseResult { Error = "Excel file appears to be empty." };

        // Scan up to row 20 for a header row
        int headerRow = -1;
        int[] colMap = Array.Empty<int>();

        for (int r = 1; r <= Math.Min(lastRow, 20); r++)
        {
            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
            var pairs = new List<(int colNum, string text)>();
            for (int c = 1; c <= lastCol; c++)
            {
                var txt = ws.Cell(r, c).GetString().Trim();
                if (!string.IsNullOrWhiteSpace(txt))
                    pairs.Add((c, txt.ToLowerInvariant()));
            }

            if (pairs.Count < 2) continue;

            var mapping = DetectColumns(pairs);
            if (mapping[0] > 0 && mapping[1] > 0 && (mapping[2] > 0 || mapping[3] > 0 || mapping[4] > 0))
            {
                headerRow = r;
                colMap = mapping;
                break;
            }
        }

        if (headerRow < 0)
            return new StatementParseResult { Error = "Could not find a header row. Expected columns: Date, Description, and Debit/Credit or Amount." };

        var rows = new List<StatementTransactionRow>();
        var skipped = 0;
        var runBal = openingBalance;

        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            if (ws.Row(r).IsEmpty()) continue;

            // Read a cell as a trimmed string, using cached value for formula cells
            string? Cell(int oneBasedCol)
            {
                if (oneBasedCol <= 0) return null;
                var cell = ws.Cell(r, oneBasedCol);
                // For formula cells, use the cached (computed) numeric/text value
                if (cell.DataType == XLDataType.Number)
                    return cell.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
                return cell.GetString().Trim();
            }

            // Read date cell: handles OA serial numbers (Excel date-as-number) and DateTime cells
            DateTime? ReadCellDate(int oneBasedCol)
            {
                if (oneBasedCol <= 0) return null;
                var cell = ws.Cell(r, oneBasedCol);
                try
                {
                    if (cell.DataType == XLDataType.DateTime)
                        return cell.GetDateTime();
                    if (cell.DataType == XLDataType.Number)
                    {
                        var num = cell.GetDouble();
                        if (num > 1 && num < 2958466) // valid OA date range (Jan 1900 – Dec 9999)
                            return DateTime.FromOADate(num);
                    }
                }
                catch { }
                return TryParseDate(cell.GetString().Trim(), out var d) ? d : null;
            }

            // Read a cell as a decimal, preferring direct numeric access over string parsing
            decimal ReadCellDecimal(int oneBasedCol)
            {
                if (oneBasedCol <= 0) return 0;
                var cell = ws.Cell(r, oneBasedCol);
                if (cell.DataType == XLDataType.Number)
                    return (decimal)cell.GetDouble();
                return decimal.TryParse(CleanAmount(cell.GetString().Trim()), out var d) ? d : 0;
            }

            var date = ReadCellDate(colMap[0]);
            if (date == null) { skipped++; continue; }
            // Allow ±5 days tolerance for statement boundary dates
            if (date.Value < periodStart.AddDays(-5) || date.Value > periodEnd.AddDays(5)) { skipped++; continue; }

            var desc = Cell(colMap[1]);
            if (string.IsNullOrWhiteSpace(desc)) { skipped++; continue; }

            decimal debit = ReadCellDecimal(colMap[2]);
            decimal credit = ReadCellDecimal(colMap[3]);

            if (debit == 0 && credit == 0 && colMap[4] > 0)
            {
                var amt = ReadCellDecimal(colMap[4]);
                if (amt >= 0) credit = amt;
                else debit = Math.Abs(amt);
            }

            if (debit == 0 && credit == 0) { skipped++; continue; }

            decimal balance;
            if (colMap[5] > 0)
            {
                var fileBal = ReadCellDecimal(colMap[5]);
                balance = fileBal != 0 ? fileBal : runBal + credit - debit;
            }
            else
            {
                runBal += credit - debit;
                balance = runBal;
            }
            runBal = balance;

            var refVal = Cell(colMap[6]);

            rows.Add(new StatementTransactionRow
            {
                Date = date.Value,
                Description = desc,
                Reference = string.IsNullOrWhiteSpace(refVal) ? null : refVal,
                DebitAmount = debit > 0 ? debit : null,
                CreditAmount = credit > 0 ? credit : null,
                RunningBalance = balance
            });
        }

        return new StatementParseResult
        {
            Transactions = rows,
            SkippedRows = skipped,
            DetectedFormat = "Excel"
        };
    }

    // ─────────────────────────────────────────────────────────── CSV ──

    private static StatementParseResult ParseCsv(Stream stream, DateTime periodStart, DateTime periodEnd, decimal openingBalance)
    {
        string[] lines;
        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
        {
            var text = reader.ReadToEnd();
            lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        }

        if (lines.Length == 0)
            return new StatementParseResult { Error = "File is empty." };

        var delimiter = DetectDelimiter(lines.Take(5));

        // Find header row (scan up to row 20)
        int headerRow = -1;
        int[] colMap = Array.Empty<int>();

        for (int i = 0; i < Math.Min(lines.Length, 20); i++)
        {
            var parts = SplitLine(lines[i], delimiter);
            if (parts.Length < 2) continue;
            var pairs = parts.Select((h, idx) => (colNum: idx + 1, text: h.Trim().ToLowerInvariant())).ToList();
            var mapping = DetectColumns(pairs);
            if (mapping[0] > 0 && mapping[1] > 0 && (mapping[2] > 0 || mapping[3] > 0 || mapping[4] > 0))
            {
                headerRow = i;
                colMap = mapping;
                break;
            }
        }

        if (headerRow < 0)
            return new StatementParseResult { Error = "Could not find a header row. Expected columns: Date, Description, and Debit/Credit or Amount." };

        var rows = new List<StatementTransactionRow>();
        var skipped = 0;
        var runBal = openingBalance;

        for (int i = headerRow + 1; i < lines.Length; i++)
        {
            var parts = SplitLine(lines[i], delimiter);
            if (parts.Length < 2) { skipped++; continue; }

            string? Get(int oneBasedCol) =>
                oneBasedCol > 0 && oneBasedCol <= parts.Length ? parts[oneBasedCol - 1].Trim() : null;

            if (!TryParseDate(Get(colMap[0]), out var date)) { skipped++; continue; }
            if (date < periodStart.AddDays(-5) || date > periodEnd.AddDays(5)) { skipped++; continue; }

            var desc = Get(colMap[1]);
            if (string.IsNullOrWhiteSpace(desc)) { skipped++; continue; }

            decimal debit = 0, credit = 0;
            decimal.TryParse(CleanAmount(Get(colMap[2])), out debit);
            decimal.TryParse(CleanAmount(Get(colMap[3])), out credit);

            if (debit == 0 && credit == 0 && colMap[4] > 0)
            {
                if (decimal.TryParse(CleanAmount(Get(colMap[4])), out var amt))
                {
                    if (amt >= 0) credit = amt;
                    else debit = Math.Abs(amt);
                }
            }

            if (debit == 0 && credit == 0) { skipped++; continue; }

            decimal balance;
            if (colMap[5] > 0 && decimal.TryParse(CleanAmount(Get(colMap[5])), out var fileBal))
                balance = fileBal;
            else
            {
                runBal += credit - debit;
                balance = runBal;
            }

            var refVal = Get(colMap[6]);

            rows.Add(new StatementTransactionRow
            {
                Date = date,
                Description = desc,
                Reference = string.IsNullOrWhiteSpace(refVal) ? null : refVal,
                DebitAmount = debit > 0 ? debit : null,
                CreditAmount = credit > 0 ? credit : null,
                RunningBalance = balance
            });
        }

        return new StatementParseResult
        {
            Transactions = rows,
            SkippedRows = skipped,
            DetectedFormat = $"CSV"
        };
    }

    // ──────────────────────────────────────────────── Column detection ──

    /// <summary>
    /// Returns int[7]: [dateCol, descCol, debitCol, creditCol, amountCol, balanceCol, refCol]
    /// Values are 1-based column numbers; 0 means not found.
    /// </summary>
    private static int[] DetectColumns(List<(int colNum, string text)> headers)
    {
        var result = new int[7];
        foreach (var (col, text) in headers)
        {
            if (result[0] == 0 && IsDateCol(text)) result[0] = col;
            else if (result[1] == 0 && IsDescCol(text)) result[1] = col;
            else if (result[2] == 0 && IsDebitCol(text)) result[2] = col;
            else if (result[3] == 0 && IsCreditCol(text)) result[3] = col;
            else if (result[4] == 0 && IsAmountCol(text)) result[4] = col;
            else if (result[5] == 0 && IsBalanceCol(text)) result[5] = col;
            else if (result[6] == 0 && IsRefCol(text)) result[6] = col;
        }
        return result;
    }

    private static bool IsDateCol(string h) =>
        h is "date" || h.Contains("trans date") || h.Contains("value date") ||
        h.Contains("posting date") || h.Contains("txn date") || h.Contains("transaction date");

    private static bool IsDescCol(string h) =>
        h.Contains("desc") || h.Contains("narration") || h.Contains("narr") ||
        h.Contains("detail") || h.Contains("particular") || h.Contains("remark") ||
        h.Contains("memo") || h.Contains("payee") || h is "transaction";

    private static bool IsDebitCol(string h) =>
        h is "debit" or "dr" or "debit amount" or "withdrawal" or "withdrawals" ||
        h.Contains("amount out") || h.Contains("money out") || h.Contains("debit amt");

    private static bool IsCreditCol(string h) =>
        h is "credit" or "cr" or "credit amount" or "deposit" or "deposits" ||
        h.Contains("amount in") || h.Contains("money in") || h.Contains("credit amt");

    private static bool IsAmountCol(string h) =>
        h is "amount" || h.Contains("trans amount") || h.Contains("transaction amount") ||
        h.Contains("tran amount");

    private static bool IsBalanceCol(string h) =>
        h is "balance" or "bal" || h.Contains("running bal") || h.Contains("closing bal") ||
        h.Contains("ledger bal") || (h.Contains("balance") && !h.Contains("opening"));

    private static bool IsRefCol(string h) =>
        h.Contains("reference") || h.Contains("ref no") || h.Contains("cheque") ||
        h.Contains("tran id") || h.Contains("trans id") || h.Contains("trxn") ||
        h.Contains("transaction id");

    // ──────────────────────────────────────────────────────── Helpers ──

    private static char DetectDelimiter(IEnumerable<string> sampleLines)
    {
        var candidates = new Dictionary<char, int>
        {
            [','] = 0, ['|'] = 0, ['\t'] = 0, [';'] = 0
        };
        foreach (var line in sampleLines)
            foreach (var ch in candidates.Keys.ToList())
                candidates[ch] += line.Count(c => c == ch);

        return candidates.OrderByDescending(x => x.Value).First().Key;
    }

    private static string[] SplitLine(string line, char delimiter)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else inQuotes = !inQuotes;
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else current.Append(c);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    private static bool TryParseDate(string? val, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(val)) return false;

        // Common Nigerian bank statement date formats
        string[] formats =
        [
            "dd/MM/yyyy", "dd-MM-yyyy", "d/M/yyyy", "d-M-yyyy",
            "dd MMM yyyy", "d MMM yyyy", "dd-MMM-yyyy", "d-MMM-yyyy",
            "dd MMM yy", "d MMM yy",
            "yyyy-MM-dd", "yyyy/MM/dd",
            "MM/dd/yyyy", "M/d/yyyy",
            "dd/MM/yy", "d/M/yy",
            "dd/MMM/yyyy", "dd/MMM/yy"
        ];

        return DateTime.TryParseExact(val, formats,
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.None, out date)
               || DateTime.TryParse(val, out date);
    }

    private static string? CleanAmount(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        // Remove currency symbols (₦, #) and thousand separators
        var cleaned = val.Trim()
            .Replace("₦", "")
            .Replace("#", "")
            .Replace(",", "")
            .Trim();
        // Strip leading currency letter only if followed immediately by a digit
        if (cleaned.Length > 1 && char.IsLetter(cleaned[0]) && char.IsDigit(cleaned[1]))
            cleaned = cleaned[1..];
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }
}
