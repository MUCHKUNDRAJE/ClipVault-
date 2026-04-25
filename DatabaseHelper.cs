using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace ClipVault
{
    public static class DatabaseHelper
    {
        private static string DbFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "clips.db");
        private static string ConnectionString => $"Data Source={DbFileName}";
        private static readonly object _dbLock = new object();

        public static void InitializeDatabase()
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                using var pragmaCmd = connection.CreateCommand();
                pragmaCmd.CommandText = "PRAGMA journal_mode=WAL;";
                pragmaCmd.ExecuteNonQuery();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS clips (
                        id          INTEGER PRIMARY KEY AUTOINCREMENT,
                        content     TEXT NOT NULL,
                        type        TEXT,
                        pinned      INTEGER DEFAULT 0,
                        encrypted   INTEGER DEFAULT 0,
                        preview     TEXT,
                        copied_at   TEXT,
                        copy_count  INTEGER DEFAULT 1
                    );
                ";
                command.ExecuteNonQuery();
            }
        }

        public static void InsertOrUpdateClip(string content, string type, string preview)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                using var pragmaCmd = connection.CreateCommand();
                pragmaCmd.CommandText = "PRAGMA journal_mode=WAL;";
                pragmaCmd.ExecuteNonQuery();

                // Check if content already exists
                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT id, copy_count FROM clips WHERE content = $content LIMIT 1";
                checkCmd.Parameters.AddWithValue("$content", content);
                
                using var reader = checkCmd.ExecuteReader();
                if (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    int currentCount = reader.GetInt32(1);

                    using var updateCmd = connection.CreateCommand();
                    updateCmd.CommandText = "UPDATE clips SET copy_count = $count, copied_at = $at WHERE id = $id";
                    updateCmd.Parameters.AddWithValue("$count", currentCount + 1);
                    updateCmd.Parameters.AddWithValue("$at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    updateCmd.Parameters.AddWithValue("$id", id);
                    updateCmd.ExecuteNonQuery();
                }
                else
                {
                    using var insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = @"
                        INSERT INTO clips (content, type, preview, copied_at, copy_count)
                        VALUES ($content, $type, $preview, $at, 1);
                    ";
                    insertCmd.Parameters.AddWithValue("$content", content);
                    insertCmd.Parameters.AddWithValue("$type", type);
                    insertCmd.Parameters.AddWithValue("$preview", preview);
                    insertCmd.Parameters.AddWithValue("$at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    insertCmd.ExecuteNonQuery();
                }
            }
        }

        public static List<ClipModel> GetClips(string typeFilter = "ALL")
        {
            var list = new List<ClipModel>();
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                string query = "SELECT id, content, type, preview, copied_at, copy_count, pinned FROM clips";
                if (typeFilter != "ALL")
                {
                    query += " WHERE type = $type";
                    command.Parameters.AddWithValue("$type", typeFilter);
                }
                query += " ORDER BY pinned DESC, copied_at DESC LIMIT 50";
                
                command.CommandText = query;

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new ClipModel
                    {
                        Id = reader.GetInt32(0),
                        Content = reader.GetString(1),
                        Type = reader.GetString(2),
                        Preview = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        CopiedAt = reader.GetString(4),
                        CopyCount = reader.GetInt32(5),
                        IsPinned = reader.GetInt32(6) == 1
                    });
                }
            }
            return list;
        }

        public static void DeleteClip(int id)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM clips WHERE id = $id";
                command.Parameters.AddWithValue("$id", id);
                command.ExecuteNonQuery();
            }
        }
    }
}
