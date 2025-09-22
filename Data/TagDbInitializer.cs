using Microsoft.Data.Sqlite;

namespace TagbooruQuest.Data
{
    public static class TagDbInitializer
    {
        public static void Initialize(string dbPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Default
            }.ToString();

            using var conn = new SqliteConnection(cs);
            conn.Open();

            // PRAGMAs must be outside a transaction
            Exec(conn, "PRAGMA journal_mode=WAL;");
            Exec(conn, "PRAGMA synchronous=NORMAL;");
            Exec(conn, "PRAGMA foreign_keys=ON;");
            Exec(conn, "PRAGMA temp_store=MEMORY;");

            using var tx = conn.BeginTransaction();

            // Tables
            Exec(conn, @"
CREATE TABLE IF NOT EXISTS node (
  id         INTEGER PRIMARY KEY,
  slug       TEXT NOT NULL UNIQUE,
  text       TEXT NOT NULL,
  is_tag     INTEGER NOT NULL DEFAULT 0,
  extra_json TEXT
);", tx);

            Exec(conn, @"
CREATE TABLE IF NOT EXISTS alias (
  node_id    INTEGER NOT NULL REFERENCES node(id) ON DELETE CASCADE,
  alias_slug TEXT NOT NULL UNIQUE,
  alias_text TEXT NOT NULL
);", tx);
            Exec(conn, "CREATE INDEX IF NOT EXISTS idx_alias_node ON alias(node_id);", tx);

            Exec(conn, @"
CREATE TABLE IF NOT EXISTS edge (
  parent_id INTEGER NOT NULL REFERENCES node(id) ON DELETE CASCADE,
  child_id  INTEGER NOT NULL REFERENCES node(id) ON DELETE CASCADE,
  UNIQUE(parent_id, child_id)
);", tx);
            Exec(conn, "CREATE INDEX IF NOT EXISTS idx_edge_parent ON edge(parent_id);", tx);
            Exec(conn, "CREATE INDEX IF NOT EXISTS idx_edge_child  ON edge(child_id);", tx);

            Exec(conn, @"
CREATE TABLE IF NOT EXISTS path (
  node_id   INTEGER NOT NULL REFERENCES node(id) ON DELETE CASCADE,
  path_text TEXT NOT NULL,
  UNIQUE(node_id, path_text)
);", tx);

            tx.Commit();

            // FTS virtual table (must be checked explicitly)
            if (!TableExists(conn, "node_search"))
            {
                Exec(conn, @"
CREATE VIRTUAL TABLE node_search USING fts5(
  text,
  aliases,
  path_tokens,
  content=''
);");
            }
        }

        public static void RebuildFts(string dbPath)
        {
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();

            EnsureFtsTable(conn); // create if missing (idempotent)

            using var tx = conn.BeginTransaction();

            // Clear and repopulate the contentless FTS5 table
            Exec(conn, "DELETE FROM node_search;", tx);

            Exec(conn, @"
INSERT INTO node_search(rowid, text, aliases, path_tokens)
SELECT n.id,
       n.text,
       IFNULL((
           SELECT GROUP_CONCAT(a.alias_text, ' ')
           FROM alias a
           WHERE a.node_id = n.id
       ), ''),
       IFNULL((
           SELECT GROUP_CONCAT(p.path_text, ' ')
           FROM path p
           WHERE p.node_id = n.id
       ), '')
FROM node n;", tx);

            tx.Commit();
        }

        private static void EnsureFtsTable(SqliteConnection conn)
        {
            // For virtual tables, sqlite_master.type is 'table'
            using (var check = conn.CreateCommand())
            {
                check.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name='node_search' LIMIT 1;";
                var exists = check.ExecuteScalar() != null;
                if (exists) return;
            }

            // Keep it minimal to avoid tokenizer parsing issues on some builds.
            // You can add a tokenize directive later once confirmed working.
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE VIRTUAL TABLE node_search USING fts5(
  text,
  aliases,
  path_tokens,
  content=''
);";
            cmd.ExecuteNonQuery();
        }

        // If you prefer async calls elsewhere:
        public static Task RebuildFtsAsync(string dbPath) => Task.Run(() => RebuildFts(dbPath));

        private static void Exec(SqliteConnection conn, string sql, SqliteTransaction? tx = null)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (tx != null) cmd.Transaction = tx;
            cmd.ExecuteNonQuery();
        }

        private static bool TableExists(SqliteConnection conn, string name)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$name;";
            cmd.Parameters.AddWithValue("$name", name);
            var result = cmd.ExecuteScalar();
            return result != null && result != DBNull.Value;
        }
    }
}
