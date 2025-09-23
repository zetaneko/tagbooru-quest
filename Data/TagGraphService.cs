using Microsoft.Data.Sqlite;
using System.Data;

namespace TagbooruQuest.Data
{
    /// <summary>
    /// Thin models for returning data to UI / callers
    /// </summary>
    public record Node(int Id, string Slug, string Text, bool IsTag);
    public record Edge(int ParentId, int ChildId);
    public record SearchResult(int Id, string Slug, string Text, double Score, string Why, string? BestPath);
    public record PathRow(int NodeId, string PathText);

    /// <summary>
    /// Central service for navigating and searching the tag DAG.
    /// Create per-scope or register as a singleton — it is stateless (opens a new connection per call).
    /// </summary>
    public sealed class TagGraphService
    {
        private readonly string _cs; // connection string like "Data Source=/path/to/tags.db"

        public TagGraphService(string dbPath)
        {
            _cs = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        }

        #region ---- Connection helpers ----
        private SqliteConnection Open()
        {
            var conn = new SqliteConnection(_cs);
            conn.Open();
            return conn;
        }

        public IEnumerable<Node> GetRoots(int limit = 200)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT n.id, n.slug, n.text, n.is_tag
FROM node n
LEFT JOIN edge e ON e.child_id = n.id
WHERE e.child_id IS NULL
ORDER BY n.text
LIMIT $lim;";
            cmd.Parameters.AddWithValue("$lim", limit);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) yield return MapNode(rd);
        }


        private static Node MapNode(IDataRecord r) =>
            new Node(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetInt32(3) == 1);
        #endregion

        #region ---- Lookups & basic read operations ----

        public Node? GetNodeById(int id)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, slug, text, is_tag FROM node WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", id);
            using var rd = cmd.ExecuteReader();
            return rd.Read() ? MapNode(rd) : null;
        }

        public Node? GetNodeBySlug(string slug)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, slug, text, is_tag FROM node WHERE slug=$slug;";
            cmd.Parameters.AddWithValue("$slug", slug);
            using var rd = cmd.ExecuteReader();
            return rd.Read() ? MapNode(rd) : null;
        }

        public IEnumerable<Node> GetChildren(int parentId)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT child.id, child.slug, child.text, child.is_tag
FROM edge e
JOIN node child ON child.id=e.child_id
WHERE e.parent_id=$pid
ORDER BY child.text;";
            cmd.Parameters.AddWithValue("$pid", parentId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) yield return MapNode(rd);
        }

        public IEnumerable<Node> GetParents(int childId)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT parent.id, parent.slug, parent.text, parent.is_tag
FROM edge e
JOIN node parent ON parent.id=e.parent_id
WHERE e.child_id=$cid
ORDER BY parent.text;";
            cmd.Parameters.AddWithValue("$cid", childId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) yield return MapNode(rd);
        }

        public IEnumerable<Node> GetSiblings(int nodeId)
        {
            // siblings = all other children of any of this node's parents
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
WITH parents AS (
  SELECT parent_id FROM edge WHERE child_id=$id
)
SELECT DISTINCT n.id, n.slug, n.text, n.is_tag
FROM parents p
JOIN edge e ON e.parent_id=p.parent_id
JOIN node n ON n.id=e.child_id
WHERE n.id<>$id
ORDER BY n.text;";
            cmd.Parameters.AddWithValue("$id", nodeId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) yield return MapNode(rd);
        }

        /// <summary>
        /// Ancestors from root..node (first-found chain; DAG may have multiple).
        /// </summary>
        public IEnumerable<Node> GetBreadcrumb(int nodeId)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
WITH RECURSIVE up(id,depth,path) AS (
  SELECT $id,0,$id
  UNION
  SELECT e.parent_id, depth+1, path || ',' || e.parent_id
  FROM edge e JOIN up u ON e.child_id=u.id
  WHERE depth < 50 AND instr(u.path, ',' || e.parent_id || ',') = 0
)
SELECT n.id,n.slug,n.text,n.is_tag
FROM up JOIN node n ON n.id=up.id
ORDER BY depth DESC;";
            cmd.Parameters.AddWithValue("$id", nodeId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) yield return MapNode(rd);
        }

        /// <summary>
        /// All descendants (subtree). Use maxDepth for safety on very large subgraphs.
        /// </summary>
        public IEnumerable<Node> GetSubtree(int nodeId, int? maxDepth = null)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            var safeMaxDepth = maxDepth ?? 50; // Default safety limit
            cmd.CommandText = @"
WITH RECURSIVE down(id,depth,path) AS (
  SELECT $id,0,$id
  UNION
  SELECT e.child_id, depth+1, path || ',' || e.child_id
  FROM edge e JOIN down d ON e.parent_id=d.id
  WHERE depth < $maxDepth AND instr(d.path, ',' || e.child_id || ',') = 0
)
SELECT DISTINCT n.id,n.slug,n.text,n.is_tag
FROM down d JOIN node n ON n.id=d.id
ORDER BY n.text;";
            cmd.Parameters.AddWithValue("$id", nodeId);
            cmd.Parameters.AddWithValue("$maxDepth", safeMaxDepth);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) yield return MapNode(rd);
        }

        public PathRow? GetBestPath(int nodeId)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT node_id, path_text
FROM path
WHERE node_id=$id
ORDER BY length(path_text) ASC
LIMIT 1;";
            cmd.Parameters.AddWithValue("$id", nodeId);
            using var rd = cmd.ExecuteReader();
            return rd.Read() ? new PathRow(rd.GetInt32(0), rd.GetString(1)) : null;
        }

        public IEnumerable<PathRow> GetAllPaths(int nodeId)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT node_id, path_text FROM path WHERE node_id=$id ORDER BY length(path_text);";
            cmd.Parameters.AddWithValue("$id", nodeId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) yield return new PathRow(rd.GetInt32(0), rd.GetString(1));
        }

        #endregion

        #region ---- Search operations ----

        /// <summary>
        /// Typeahead by prefix on slug (super fast).
        /// </summary>
        public IEnumerable<Node> Typeahead(string prefix, int limit = 20)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT id, slug, text, is_tag
FROM node
WHERE slug LIKE $p || '%'
ORDER BY text
LIMIT $lim;";
            cmd.Parameters.AddWithValue("$p", prefix.ToLowerInvariant());
            cmd.Parameters.AddWithValue("$lim", limit);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) yield return MapNode(rd);
        }

        /// <summary>
        /// Combined search: exact > prefix > FTS (BM25). Returns scored results, with best path for context.
        /// </summary>
        public IEnumerable<SearchResult> Search(string q, int limit = 50)
        {
            var qSlug = Slugify(q);
            var results = new List<SearchResult>();

            using var conn = Open();

            // 1) exact slug or text
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT id,slug,text,is_tag, 100.0 AS score, 'exact' AS why
FROM node
WHERE slug=$s OR text=$t
LIMIT $lim;";
                cmd.Parameters.AddWithValue("$s", qSlug);
                cmd.Parameters.AddWithValue("$t", q.ToLowerInvariant());
                cmd.Parameters.AddWithValue("$lim", limit);
                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                    results.Add(ToScored(rd, BestPath(conn, rd.GetInt32(0))));
            }

            // 2) prefix on slug
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT id,slug,text,is_tag, 80.0 AS score, 'prefix' AS why
FROM node
WHERE slug LIKE $p || '%'
ORDER BY text
LIMIT $lim;";
                cmd.Parameters.AddWithValue("$p", qSlug);
                cmd.Parameters.AddWithValue("$lim", limit);
                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                    results.Add(ToScored(rd, BestPath(conn, rd.GetInt32(0))));
            }

            // 3) FTS (if table exists) - escape dashes for FTS queries
            if (TableExists(conn, "node_search"))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
SELECT n.id, n.slug, n.text, n.is_tag,
       bm25(node_search) AS score, 'fts' AS why
FROM node_search
JOIN node n ON n.id=node_search.rowid
WHERE node_search MATCH $q
ORDER BY score
LIMIT $lim;";
                // For FTS, quote the query to treat it as a phrase and escape special characters
                var ftsQuery = "\"" + q.ToLowerInvariant().Replace("\"", "\"\"") + "\"";
                cmd.Parameters.AddWithValue("$q", ftsQuery);
                cmd.Parameters.AddWithValue("$lim", limit * 4); // pull more, we'll merge
                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                    results.Add(ToScored(rd, BestPath(conn, rd.GetInt32(0))));
            }

            // 4) Merge by node id (keep max score), then sort by score descending
            var byId = new Dictionary<int, SearchResult>();
            foreach (var r in results)
            {
                if (byId.TryGetValue(r.Id, out var prev))
                {
                    byId[r.Id] = (r.Score > prev.Score) ? r : prev;
                }
                else byId[r.Id] = r;
            }

            var merged = new List<SearchResult>(byId.Values);
            merged.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (merged.Count > limit) merged = merged.GetRange(0, limit);
            return merged;
        }

        private SearchResult ToScored(IDataRecord r, string? bestPath) =>
            new SearchResult(
                Id: r.GetInt32(0),
                Slug: r.GetString(1),
                Text: r.GetString(2),
                Score: r.GetDouble(4),
                Why: r.GetString(5),
                BestPath: bestPath
            );

        private string? BestPath(SqliteConnection conn, int nodeId)
        {
            using var p = conn.CreateCommand();
            p.CommandText = @"
SELECT path_text FROM path WHERE node_id=$id
ORDER BY length(path_text) ASC LIMIT 1;";
            p.Parameters.AddWithValue("$id", nodeId);
            var o = p.ExecuteScalar();
            return o == null || o is DBNull ? null : (string)o;
        }

        #endregion

        #region ---- Related / discovery helpers ----

        /// <summary>
        /// Related tags by simple graph proximity: siblings + parents (and their siblings).
        /// Useful to show "You may also want…" suggestions.
        /// </summary>
        public IEnumerable<Node> GetRelatedSimple(int nodeId, int limit = 40)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
WITH parents AS (
  SELECT parent_id FROM edge WHERE child_id=$id
),
siblings AS (
  SELECT DISTINCT e.child_id AS id
  FROM parents p
  JOIN edge e ON e.parent_id=p.parent_id
  WHERE e.child_id<>$id
),
cousins AS (
  SELECT DISTINCT e2.child_id AS id
  FROM parents p
  JOIN edge p2 ON p2.child_id=p.parent_id       -- grandparents
  JOIN edge e2 ON e2.parent_id=p2.parent_id     -- children of grandparents (aunts/uncles)
  WHERE e2.child_id<>$id
)
SELECT n.id,n.slug,n.text,n.is_tag
FROM (
  SELECT id FROM siblings
  UNION
  SELECT id FROM cousins
) u
JOIN node n ON n.id=u.id
ORDER BY n.text
LIMIT $lim;";
            cmd.Parameters.AddWithValue("$id", nodeId);
            cmd.Parameters.AddWithValue("$lim", limit);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) yield return MapNode(rd);
        }

        /// <summary>
        /// Random sample of tag leaves. Handy for smoke testing and discovery.
        /// </summary>
        public IEnumerable<Node> GetRandomTags(int count = 10)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT id,slug,text,is_tag
FROM node
WHERE is_tag=1
ORDER BY random()
LIMIT $lim;";
            cmd.Parameters.AddWithValue("$lim", count);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) yield return MapNode(rd);
        }

        #endregion

        #region ---- CRUD (optional; safe, idempotent) ----

        public Node UpsertNode(string text, bool isTag)
        {
            var slug = Slugify(text);
            using var conn = Open();
            using var tx = conn.BeginTransaction();

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO node(slug,text,is_tag)
VALUES($slug,$text,$isTag)
ON CONFLICT(slug) DO UPDATE SET
  text=excluded.text,
  is_tag=CASE WHEN excluded.is_tag=1 THEN 1 ELSE node.is_tag END
RETURNING id,slug,text,is_tag;";
                cmd.Parameters.AddWithValue("$slug", slug);
                cmd.Parameters.AddWithValue("$text", text.ToLowerInvariant());
                cmd.Parameters.AddWithValue("$isTag", isTag ? 1 : 0);
                using var rd = cmd.ExecuteReader();
                rd.Read();
                var node = MapNode(rd);
                tx.Commit();
                return node;
            }
        }

        public void AddEdge(int parentId, int childId)
        {
            if (parentId == childId) return;

            // Check if this would create a cycle by seeing if childId is an ancestor of parentId
            if (WouldCreateCycle(parentId, childId))
            {
                System.Diagnostics.Debug.WriteLine($"WARNING: Skipping edge {parentId}->{childId} - would create cycle");
                return;
            }

            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO edge(parent_id,child_id) VALUES($p,$c);";
            cmd.Parameters.AddWithValue("$p", parentId);
            cmd.Parameters.AddWithValue("$c", childId);
            cmd.ExecuteNonQuery();
        }

        private bool WouldCreateCycle(int parentId, int childId)
        {
            try
            {
                // Check if childId is already an ancestor of parentId
                using var conn = Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
WITH RECURSIVE up(id,depth) AS (
  SELECT $parentId,0
  UNION
  SELECT e.parent_id, depth+1
  FROM edge e JOIN up u ON e.child_id=u.id
  WHERE depth < 20
)
SELECT 1 FROM up WHERE id = $childId LIMIT 1;";
                cmd.Parameters.AddWithValue("$parentId", parentId);
                cmd.Parameters.AddWithValue("$childId", childId);
                return cmd.ExecuteScalar() != null;
            }
            catch
            {
                // If cycle detection fails, err on the side of caution
                return true;
            }
        }

        public void RemoveEdge(int parentId, int childId)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM edge WHERE parent_id=$p AND child_id=$c;";
            cmd.Parameters.AddWithValue("$p", parentId);
            cmd.Parameters.AddWithValue("$c", childId);
            cmd.ExecuteNonQuery();
        }

        public void AddAlias(int nodeId, string aliasText)
        {
            var aliasSlug = Slugify(aliasText);
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
INSERT OR IGNORE INTO alias(node_id, alias_slug, alias_text)
VALUES($id,$slug,$text);";
            cmd.Parameters.AddWithValue("$id", nodeId);
            cmd.Parameters.AddWithValue("$slug", aliasSlug);
            cmd.Parameters.AddWithValue("$text", aliasText.ToLowerInvariant());
            cmd.ExecuteNonQuery();
        }

        #endregion

        #region ---- Stats & health ----

        public (long Nodes, long Edges, long Tags) GetStats()
        {
            using var conn = Open();
            long n = Scalar<long>(conn, "SELECT COUNT(*) FROM node;");
            long e = Scalar<long>(conn, "SELECT COUNT(*) FROM edge;");
            long t = Scalar<long>(conn, "SELECT COUNT(*) FROM node WHERE is_tag=1;");
            return (n, e, t);
        }

        public bool TableExists(string tableName)
        {
            using var conn = Open();
            return TableExists(conn, tableName);
        }

        #endregion

        #region ---- Private helpers ----

        private static T Scalar<T>(SqliteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var o = cmd.ExecuteScalar();
            if (o == null || o is DBNull) return default!;
            return (T)Convert.ChangeType(o, typeof(T));
        }

        private static bool TableExists(SqliteConnection conn, string table)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$n LIMIT 1;";
            cmd.Parameters.AddWithValue("$n", table);
            var o = cmd.ExecuteScalar();
            return o != null && o != DBNull.Value;
        }

        /// <summary>
        /// Lowercase, trim, and convert non [a-z0-9] to underscore, collapse repeats.
        /// </summary>
        private static string Slugify(string text)
        {
            var s = text.ToLowerInvariant().Trim();
            var chars = new char[s.Length];
            int j = 0;
            char prev = '\0';
            foreach (var ch in s)
            {
                char c = char.IsLetterOrDigit(ch) ? ch : '_';
                if (c == '_' && prev == '_') continue;
                chars[j++] = c;
                prev = c;
            }
            // trim underscores
            int start = 0; while (start < j && chars[start] == '_') start++;
            int end = j - 1; while (end >= start && chars[end] == '_') end--;
            return start <= end ? new string(chars, start, end - start + 1) : string.Empty;
        }

        #endregion
    }
}
