using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEngine;

namespace Multilingo.Localization.Editor
{
    /// <summary>
    /// Centralized file parser for CSV, XLSX, JSON, XML, YAML formats.
    /// Eliminates duplicated parsing logic across the codebase.
    /// </summary>
    public static class MultilingoFileParser
    {
        // =====================
        // PARSING (Import)
        // =====================

        /// <summary>
        /// Auto-detect format and parse any supported file into a 2D string table.
        /// Row 0 = headers. Row 1+ = data.
        /// </summary>
        public static List<List<string>> ParseFile(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            switch (ext)
            {
                case ".xlsx": return ParseXLSX(filePath);
                case ".json": return ParseJSON(File.ReadAllText(filePath, Encoding.UTF8));
                case ".xml": return ParseXML(File.ReadAllText(filePath, Encoding.UTF8));
                default: return ParseCSV(File.ReadAllText(filePath, Encoding.UTF8));
            }
        }

        /// <summary>
        /// RFC 4180 compliant CSV parser handling quoted fields, newlines within quotes, and escaped quotes.
        /// </summary>
        public static List<List<string>> ParseCSV(string content)
        {
            var result = new List<List<string>>();
            var currentLine = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (inQuotes)
                {
                    if (c == '"' && i + 1 < content.Length && content[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else if (c == '"')
                    {
                        inQuotes = false;
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        currentLine.Add(currentField.ToString());
                        currentField.Clear();
                    }
                    else if (c == '\n' || (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n'))
                    {
                        currentLine.Add(currentField.ToString());
                        currentField.Clear();
                        result.Add(currentLine);
                        currentLine = new List<string>();
                        if (c == '\r') i++;
                    }
                    else if (c != '\r')
                    {
                        currentField.Append(c);
                    }
                }
            }

            if (currentField.Length > 0 || currentLine.Count > 0)
            {
                currentLine.Add(currentField.ToString());
                result.Add(currentLine);
            }

            // Remove trailing empty row
            if (result.Count > 0 && result.Last().Count == 1 && string.IsNullOrWhiteSpace(result.Last()[0]))
                result.RemoveAt(result.Count - 1);

            return result;
        }

        /// <summary>
        /// Parse Excel .xlsx files using built-in System.IO.Compression and System.Xml.
        /// No third-party dependencies required.
        /// </summary>
        public static List<List<string>> ParseXLSX(string filePath)
        {
            var result = new List<List<string>>();
            var sharedStrings = new List<string>();

            using (var fileStream = File.OpenRead(filePath))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
            {
                // 1. Read shared strings
                var ssEntry = archive.GetEntry("xl/sharedStrings.xml");
                if (ssEntry != null)
                {
                    using (var stream = ssEntry.Open())
                    {
                        var doc = new XmlDocument();
                        doc.Load(stream);
                        var nsMgr = new XmlNamespaceManager(doc.NameTable);
                        nsMgr.AddNamespace("s", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                        var siNodes = doc.SelectNodes("//s:si", nsMgr);
                        if (siNodes != null)
                        {
                            foreach (XmlNode si in siNodes)
                            {
                                var tNodes = si.SelectNodes(".//s:t", nsMgr);
                                StringBuilder sb = new StringBuilder();
                                if (tNodes != null)
                                {
                                    foreach (XmlNode t in tNodes) sb.Append(t.InnerText);
                                }
                                sharedStrings.Add(sb.ToString());
                            }
                        }
                    }
                }

                // 2. Read first worksheet
                var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
                if (sheetEntry == null)
                {
                    Debug.LogError("Could not find sheet1.xml in the Excel file.");
                    return result;
                }

                using (var stream = sheetEntry.Open())
                {
                    var doc = new XmlDocument();
                    doc.Load(stream);
                    var nsMgr = new XmlNamespaceManager(doc.NameTable);
                    nsMgr.AddNamespace("s", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");

                    var rows = doc.SelectNodes("//s:sheetData/s:row", nsMgr);
                    if (rows == null) return result;

                    foreach (XmlNode row in rows)
                    {
                        var rowData = new List<string>();
                        var cells = row.SelectNodes("s:c", nsMgr);
                        if (cells == null) { result.Add(rowData); continue; }

                        foreach (XmlNode cell in cells)
                        {
                            string cellRef = cell.Attributes?["r"]?.Value ?? "";
                            int colIndex = CellRefToColumnIndex(cellRef);
                            while (rowData.Count < colIndex) rowData.Add("");

                            string cellType = cell.Attributes?["t"]?.Value ?? "";
                            var vNode = cell.SelectSingleNode("s:v", nsMgr);
                            string cellValue = vNode?.InnerText ?? "";

                            if (cellType == "s" && int.TryParse(cellValue, out int ssIndex) && ssIndex < sharedStrings.Count)
                                rowData.Add(sharedStrings[ssIndex]);
                            else if (cellType == "inlineStr")
                            {
                                var isNode = cell.SelectSingleNode("s:is/s:t", nsMgr);
                                rowData.Add(isNode?.InnerText ?? "");
                            }
                            else
                                rowData.Add(cellValue);
                        }
                        result.Add(rowData);
                    }
                }
            }

            // Normalize column count
            int maxCols = result.Count > 0 ? result.Max(r => r.Count) : 0;
            foreach (var row in result)
                while (row.Count < maxCols) row.Add("");

            // Remove trailing empty rows
            while (result.Count > 0 && result.Last().All(c => string.IsNullOrWhiteSpace(c)))
                result.RemoveAt(result.Count - 1);

            return result;
        }

        /// <summary>
        /// Parse a flat JSON array of objects into table data.
        /// Format: [{"Key":"val", "English":"Hello"}, ...]
        /// </summary>
        public static List<List<string>> ParseJSON(string content)
        {
            var result = new List<List<string>>();
            content = content.Trim();
            if (!content.StartsWith("[")) return ParseCSV(content); // Fallback

            // Simple JSON array parser without external dependencies
            var headers = new List<string>();
            var headerSet = new HashSet<string>();

            // First pass: collect all unique keys
            var keyMatches = System.Text.RegularExpressions.Regex.Matches(content, @"""([^""\\]*(?:\\.[^""\\]*)*)"":");
            foreach (System.Text.RegularExpressions.Match m in keyMatches)
            {
                string key = System.Text.RegularExpressions.Regex.Unescape(m.Groups[1].Value);
                if (headerSet.Add(key)) headers.Add(key);
            }

            if (headers.Count == 0) return result;
            result.Add(new List<string>(headers));

            // Second pass: extract objects
            var objMatches = System.Text.RegularExpressions.Regex.Matches(content, @"\{([^{}]*)\}");
            foreach (System.Text.RegularExpressions.Match objMatch in objMatches)
            {
                var row = new List<string>();
                string objContent = objMatch.Groups[1].Value;

                foreach (string header in headers)
                {
                    string escaped = System.Text.RegularExpressions.Regex.Escape(header);
                    var valMatch = System.Text.RegularExpressions.Regex.Match(objContent,
                        $@"""{escaped}""\s*:\s*""((?:[^""\\]|\\.)*)""");
                    row.Add(valMatch.Success ? System.Text.RegularExpressions.Regex.Unescape(valMatch.Groups[1].Value) : "");
                }
                result.Add(row);
            }

            return result;
        }

        /// <summary>
        /// Parse XML with <data><row>...</row></data> structure.
        /// </summary>
        public static List<List<string>> ParseXML(string content)
        {
            var result = new List<List<string>>();
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(content);

                var rows = doc.SelectNodes("//row");
                if (rows == null || rows.Count == 0) return ParseCSV(content); // Fallback

                var headers = new List<string>();
                // Get headers from first row's child elements
                if (rows.Count > 0)
                {
                    foreach (XmlNode child in rows[0].ChildNodes)
                    {
                        if (child.NodeType == XmlNodeType.Element)
                            headers.Add(child.Name);
                    }
                }
                result.Add(headers);

                foreach (XmlNode row in rows)
                {
                    var rowData = new List<string>();
                    foreach (string header in headers)
                    {
                        var node = row.SelectSingleNode(header);
                        rowData.Add(node?.InnerText ?? "");
                    }
                    result.Add(rowData);
                }
            }
            catch
            {
                return ParseCSV(content); // Fallback
            }

            return result;
        }

        // =====================
        // SAVING (Export)
        // =====================

        public enum OutputFormat { SameAsInput, CSV, XLSX, JSON, XML, YAML }

        public static void SaveFile(string path, OutputFormat format, List<List<string>> data)
        {
            OutputFormat targetFormat = format == OutputFormat.SameAsInput ? OutputFormat.CSV : format;

            switch (targetFormat)
            {
                case OutputFormat.CSV: SaveCSV(path, data); break;
                case OutputFormat.XLSX: SaveXLSX(path, data); break;
                case OutputFormat.JSON: SaveJSON(path, data); break;
                case OutputFormat.XML: SaveXML(path, data); break;
                case OutputFormat.YAML: SaveYAML(path, data); break;
            }
        }

        public static void SaveCSV(string path, List<List<string>> data)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var row in data)
            {
                for (int i = 0; i < row.Count; i++)
                {
                    string field = row[i] ?? "";
                    if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
                        field = "\"" + field.Replace("\"", "\"\"") + "\"";
                    sb.Append(field);
                    if (i < row.Count - 1) sb.Append(",");
                }
                sb.AppendLine();
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        public static void SaveJSON(string path, List<List<string>> data)
        {
            if (data.Count < 2) return;
            var headers = data[0];
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[");
            for (int r = 1; r < data.Count; r++)
            {
                sb.AppendLine("  {");
                for (int c = 0; c < headers.Count; c++)
                {
                    string key = headers[c];
                    string val = c < data[r].Count ? data[r][c] : "";
                    val = val.Replace("\"", "\\\"").Replace("\n", "\\n");
                    sb.Append($"    \"{key}\": \"{val}\"");
                    if (c < headers.Count - 1) sb.AppendLine(","); else sb.AppendLine();
                }
                sb.Append("  }");
                if (r < data.Count - 1) sb.AppendLine(","); else sb.AppendLine();
            }
            sb.AppendLine("]");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        public static void SaveXML(string path, List<List<string>> data)
        {
            if (data.Count < 2) return;
            var headers = data[0];
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<data>");
            for (int r = 1; r < data.Count; r++)
            {
                sb.AppendLine("  <row>");
                for (int c = 0; c < headers.Count; c++)
                {
                    string key = headers[c].Replace(" ", "_").Replace("(", "").Replace(")", "").Replace("-", "_");
                    if (string.IsNullOrEmpty(key)) key = "column_" + c;
                    string val = c < data[r].Count ? data[r][c] : "";
                    val = System.Security.SecurityElement.Escape(val);
                    sb.AppendLine($"    <{key}>{val}</{key}>");
                }
                sb.AppendLine("  </row>");
            }
            sb.AppendLine("</data>");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        public static void SaveYAML(string path, List<List<string>> data)
        {
            if (data.Count < 2) return;
            var headers = data[0];
            StringBuilder sb = new StringBuilder();

            for (int r = 1; r < data.Count; r++)
            {
                sb.AppendLine("-");
                for (int c = 0; c < headers.Count; c++)
                {
                    string key = headers[c];
                    string val = c < data[r].Count ? data[r][c] : "";

                    if (val.Contains(":") || val.Contains("#") || val.Contains("\"") || val.Contains("\n") || val.Contains("'"))
                        val = "\"" + val.Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
                    else if (string.IsNullOrWhiteSpace(val))
                        val = "\"\"";

                    sb.AppendLine($"  {key}: {val}");
                }
            }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        public static void SaveXLSX(string path, List<List<string>> data)
        {
            if (data.Count < 1) return;

            var allStrings = new List<string>();
            var stringIndex = new Dictionary<string, int>();
            foreach (var row in data)
            {
                foreach (var cell in row)
                {
                    string val = cell ?? "";
                    if (!stringIndex.ContainsKey(val))
                    {
                        stringIndex[val] = allStrings.Count;
                        allStrings.Add(val);
                    }
                }
            }

            string EscXml(string s) => System.Security.SecurityElement.Escape(s ?? "");

            // Shared Strings XML
            var ssSb = new StringBuilder();
            ssSb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            ssSb.AppendLine($"<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\"{allStrings.Count}\" uniqueCount=\"{allStrings.Count}\">");
            foreach (var s in allStrings)
                ssSb.AppendLine($"  <si><t>{EscXml(s)}</t></si>");
            ssSb.AppendLine("</sst>");

            // Sheet XML
            var shSb = new StringBuilder();
            shSb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            shSb.AppendLine("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            shSb.AppendLine("<sheetData>");
            for (int r = 0; r < data.Count; r++)
            {
                shSb.AppendLine($"  <row r=\"{r + 1}\">");
                for (int c = 0; c < data[r].Count; c++)
                {
                    string val = data[r][c] ?? "";
                    int idx = stringIndex[val];
                    shSb.AppendLine($"    <c r=\"{CellRef(c, r)}\" t=\"s\"><v>{idx}</v></c>");
                }
                shSb.AppendLine("  </row>");
            }
            shSb.AppendLine("</sheetData>");
            shSb.AppendLine("</worksheet>");

            string contentTypes = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>" +
                "</Types>";

            string rootRels = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "</Relationships>";

            string wbRels = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>" +
                "</Relationships>";

            string workbook = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                "<sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
                "</workbook>";

            if (File.Exists(path)) File.Delete(path);
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                WriteEntry(zip, "[Content_Types].xml", contentTypes);
                WriteEntry(zip, "_rels/.rels", rootRels);
                WriteEntry(zip, "xl/workbook.xml", workbook);
                WriteEntry(zip, "xl/_rels/workbook.xml.rels", wbRels);
                WriteEntry(zip, "xl/worksheets/sheet1.xml", shSb.ToString());
                WriteEntry(zip, "xl/sharedStrings.xml", ssSb.ToString());
            }
        }

        // =====================
        // Helpers
        // =====================

        static int CellRefToColumnIndex(string cellRef)
        {
            int col = 0;
            foreach (char c in cellRef)
            {
                if (char.IsLetter(c))
                    col = col * 26 + (char.ToUpper(c) - 'A' + 1);
                else break;
            }
            return col > 0 ? col - 1 : 0;
        }

        static string CellRef(int col, int row)
        {
            string colStr = "";
            int c = col;
            while (c >= 0) { colStr = (char)('A' + c % 26) + colStr; c = c / 26 - 1; }
            return colStr + (row + 1);
        }

        static void WriteEntry(ZipArchive zip, string entryName, string content)
        {
            var entry = zip.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Fastest);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.Write(content);
            }
        }

        /// <summary>
        /// Auto-detect the source language column index from headers.
        /// Checks for: English, en, Source, Original, Base, Default, Reference, Master.
        /// Falls back to content analysis (longest avg text, most filled cells).
        /// </summary>
        public static int DetectSourceColumn(List<List<string>> data, string[] headers)
        {
            if (headers == null || headers.Length == 0) return 0;

            // Priority-ordered indicators
            string[] sourceIndicators = {
                "english", "en", "source", "original", "key_text",
                "base", "default", "reference", "master"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                string headerLower = headers[i].ToLower().Trim();
                foreach (var indicator in sourceIndicators)
                {
                    if (headerLower == indicator || headerLower.Contains(indicator))
                        return i;
                }
            }

            // Content-based fallback: column with most filled cells and longest average text
            if (data.Count > 1)
            {
                int bestCol = 0;
                float bestScore = -1;

                for (int c = 0; c < headers.Length; c++)
                {
                    // Skip columns that look like lang codes
                    if (headers[c].Contains("(") && headers[c].Contains(")")) continue;

                    int filled = 0;
                    float totalLen = 0;
                    for (int r = 1; r < data.Count; r++)
                    {
                        if (c < data[r].Count && !string.IsNullOrWhiteSpace(data[r][c]))
                        {
                            filled++;
                            totalLen += data[r][c].Length;
                        }
                    }
                    float score = filled + (totalLen / System.Math.Max(1, data.Count - 1));
                    if (score > bestScore) { bestScore = score; bestCol = c; }
                }
                return bestCol;
            }

            return headers.Length > 2 ? 2 : 0;
        }

        /// <summary>
        /// Detect which columns contain a Context/Notes column for translation context.
        /// Returns -1 if not found.
        /// </summary>
        public static int DetectContextColumn(string[] headers)
        {
            if (headers == null) return -1;
            string[] contextIndicators = { "context", "notes", "description", "comment", "hint", "dev_notes" };
            for (int i = 0; i < headers.Length; i++)
            {
                string h = headers[i].ToLower().Trim();
                foreach (var indicator in contextIndicators)
                {
                    if (h == indicator || h.Contains(indicator)) return i;
                }
            }
            return -1;
        }
    }
}
