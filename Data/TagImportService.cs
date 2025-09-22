using System.Data;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text;

namespace TagbooruQuest.Data;

public class TagImportService
{
    private readonly string _dbPath;
    private readonly string _csvPath;

    public TagImportService(string dbPath, string csvPath)
    {
        _dbPath = dbPath;
        _csvPath = csvPath;
    }

    public void ImportIfNeeded()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        if (IsImported(conn))
            return;

        ImportCsv(conn, _csvPath);
        MarkImported(conn);
    }

    /// <summary>
    /// Force re-import of CSV data, clearing existing data first.
    /// Useful when CSV parsing has been improved or data is corrupted.
    /// </summary>
    public void ForceReimport()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        // Clear existing data
        ClearData(conn);

        // Reset import flag
        ResetImportFlag(conn);

        // Import fresh data
        ImportCsv(conn, _csvPath);
        MarkImported(conn);
    }

    private bool IsImported(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS meta (
  key TEXT PRIMARY KEY,
  value TEXT
);
INSERT OR IGNORE INTO meta (key,value) VALUES ('csv_imported','false');
SELECT value FROM meta WHERE key='csv_imported';";
        var result = cmd.ExecuteScalar()?.ToString();
        return result == "true";
    }

    private void MarkImported(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE meta SET value='true' WHERE key='csv_imported';";
        cmd.ExecuteNonQuery();
    }

    private void ImportCsv(SqliteConnection conn, string csvPath)
    {
        // First pass: collect all paths and identify conflicts
        var allPaths = new List<List<string>>();
        var tagSlugs = new HashSet<string>();
        var categoryPathSlugs = new Dictionary<string, List<string>>(); // slug -> list of full paths

        foreach (var line in File.ReadLines(csvPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = ParseCsvLine(line)
                           .Select(c => c.Trim().ToLowerInvariant())
                           .Where(c => !string.IsNullOrWhiteSpace(c))
                           .ToList();

            if (cols.Count > 0)
            {
                allPaths.Add(cols);

                // Last column is always a tag
                var tagText = cols[cols.Count - 1];
                var tagSlug = Slugify(tagText);
                tagSlugs.Add(tagSlug);

                // Track category paths for conflict detection
                for (int i = 0; i < cols.Count - 1; i++)
                {
                    var categorySlug = Slugify(cols[i]);
                    var fullPath = string.Join("/", cols.Take(i + 1));

                    if (!categoryPathSlugs.ContainsKey(categorySlug))
                        categoryPathSlugs[categorySlug] = new List<string>();

                    if (!categoryPathSlugs[categorySlug].Contains(fullPath))
                        categoryPathSlugs[categorySlug].Add(fullPath);
                }
            }
        }

        // Second pass: import with conflict resolution
        using var tx = conn.BeginTransaction();

        foreach (var cols in allPaths)
        {
            int? prevId = null;

            for (int i = 0; i < cols.Count; i++)
            {
                var text = cols[i];
                var isTag = i == cols.Count - 1;
                var baseSlug = Slugify(text);
                var slug = baseSlug;

                if (!isTag)
                {
                    // For categories, check for conflicts
                    var hasTagConflict = tagSlugs.Contains(baseSlug);
                    var hasCategoryConflict = categoryPathSlugs[baseSlug].Count > 1;

                    if (hasTagConflict || hasCategoryConflict)
                    {
                        // Create context-aware slug using parent path
                        var parentPath = string.Join("_", cols.Take(i).Select(Slugify));
                        slug = string.IsNullOrEmpty(parentPath) ? baseSlug : $"{parentPath}_{baseSlug}";
                    }
                }

                int nodeId = GetOrCreateNode(conn, slug, text, isTag, tx);

                if (prevId != null && prevId.Value != nodeId)
                    AddEdge(conn, prevId.Value, nodeId, tx);

                prevId = nodeId;
            }
        }

        tx.Commit();

        // Rebuild FTS index
        TagDbInitializer.RebuildFts(_dbPath);
    }

    private bool DoesTagExist(SqliteConnection conn, string slug, SqliteTransaction tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT 1 FROM node WHERE slug = $slug AND is_tag = 1 LIMIT 1;";
        cmd.Parameters.AddWithValue("$slug", slug);
        return cmd.ExecuteScalar() != null;
    }

    private int GetOrCreateNode(SqliteConnection conn, string slug, string text, bool isTag, SqliteTransaction tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
INSERT INTO node(slug,text,is_tag)
VALUES($slug,$text,$isTag)
ON CONFLICT(slug) DO UPDATE SET is_tag=CASE WHEN $isTag=1 THEN 1 ELSE is_tag END
RETURNING id;";
        cmd.Parameters.AddWithValue("$slug", slug);
        cmd.Parameters.AddWithValue("$text", text);
        cmd.Parameters.AddWithValue("$isTag", isTag ? 1 : 0);
        return Convert.ToInt32(cmd.ExecuteScalar()!);
    }

    private void AddEdge(SqliteConnection conn, int parentId, int childId, SqliteTransaction tx)
    {
        // Prevent self-loops
        if (parentId == childId)
        {
            System.Diagnostics.Debug.WriteLine($"WARNING: Prevented self-loop for node {parentId}");
            return;
        }

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT OR IGNORE INTO edge(parent_id,child_id) VALUES($p,$c);";
        cmd.Parameters.AddWithValue("$p", parentId);
        cmd.Parameters.AddWithValue("$c", childId);
        cmd.ExecuteNonQuery();
    }

    private string Slugify(string text)
    {
        var slug = text.ToLowerInvariant();
        slug = new string(slug.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        while (slug.Contains("__")) slug = slug.Replace("__", "_");
        return slug.Trim('_');
    }

    /// <summary>
    /// Properly parse a CSV line handling quoted fields that may contain commas.
    /// Supports RFC 4180 CSV format from Google Drive exports.
    /// </summary>
    private List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;
        bool nextIsEscaped = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (nextIsEscaped)
            {
                currentField.Append(c);
                nextIsEscaped = false;
                continue;
            }

            if (c == '"')
            {
                if (inQuotes)
                {
                    // Check if this is an escaped quote (double quote)
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++; // Skip the next quote
                    }
                    else
                    {
                        // End of quoted field
                        inQuotes = false;
                    }
                }
                else
                {
                    // Start of quoted field
                    inQuotes = true;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                // Field separator - add current field and start new one
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                // Regular character - add to current field
                currentField.Append(c);
            }
        }

        // Add the last field
        fields.Add(currentField.ToString());

        return fields;
    }

    private void ClearData(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
DELETE FROM path;
DELETE FROM alias;
DELETE FROM edge;
DELETE FROM node;
DELETE FROM node_search;";
        cmd.ExecuteNonQuery();
    }

    private void ResetImportFlag(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE meta SET value='false' WHERE key='csv_imported';";
        cmd.ExecuteNonQuery();
    }
}
