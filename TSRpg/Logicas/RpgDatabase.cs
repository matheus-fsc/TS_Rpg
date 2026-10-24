using Microsoft.Data.Sqlite;
using TShockAPI;

namespace TSRpg.Logicas
{
    public class RpgPlayer
    {
        public string Name { get; set; } = "";
        public string? ClassId { get; set; }
        public int Level { get; set; } = 1;
        /// <summary>XP accumulated inside the current level.</summary>
        public long Xp { get; set; }
        public DateTime? ClassChangedAt { get; set; }
        public bool Dirty { get; set; }
    }

    public static class RpgDatabase
    {
        private static readonly string DirectoryPath = Path.Combine(TShock.SavePath, "TSRpg");
        private static readonly string DbPath = Path.Combine(DirectoryPath, "rpg.sqlite");

        public static void InitializeDatabase()
        {
            try
            {
                if (!Directory.Exists(DirectoryPath))
                    Directory.CreateDirectory(DirectoryPath);

                using var conn = Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Players (
                        Name           TEXT PRIMARY KEY,
                        ClassId        TEXT,
                        Level          INT     NOT NULL DEFAULT 1,
                        Xp             INTEGER NOT NULL DEFAULT 0,
                        ClassChangedAt INTEGER
                    );
                ";
                cmd.ExecuteNonQuery();

                TShock.Log.ConsoleInfo("TSRpg: database initialized.");
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"TSRpg: error initializing database: {ex.Message}");
            }
        }

        public static RpgPlayer Load(string name)
        {
            var player = new RpgPlayer { Name = name };
            try
            {
                using var conn = Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT ClassId, Level, Xp, ClassChangedAt FROM Players WHERE Name=@n;";
                cmd.Parameters.AddWithValue("@n", name);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    player.ClassId = reader.IsDBNull(0) ? null : reader.GetString(0);
                    player.Level = reader.GetInt32(1);
                    player.Xp = reader.GetInt64(2);
                    player.ClassChangedAt = reader.IsDBNull(3)
                        ? null
                        : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3)).UtcDateTime;
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"TSRpg: error loading player {name}: {ex.Message}");
            }
            return player;
        }

        public static void Save(RpgPlayer player)
        {
            try
            {
                using var conn = Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Players (Name, ClassId, Level, Xp, ClassChangedAt)
                    VALUES (@n, @c, @l, @x, @t)
                    ON CONFLICT(Name) DO UPDATE SET
                        ClassId=excluded.ClassId, Level=excluded.Level,
                        Xp=excluded.Xp, ClassChangedAt=excluded.ClassChangedAt;
                ";
                cmd.Parameters.AddWithValue("@n", player.Name);
                cmd.Parameters.AddWithValue("@c", (object?)player.ClassId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@l", player.Level);
                cmd.Parameters.AddWithValue("@x", player.Xp);
                cmd.Parameters.AddWithValue("@t", player.ClassChangedAt.HasValue
                    ? new DateTimeOffset(player.ClassChangedAt.Value).ToUnixTimeSeconds()
                    : DBNull.Value);
                cmd.ExecuteNonQuery();
                player.Dirty = false;
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"TSRpg: error saving player {player.Name}: {ex.Message}");
            }
        }

        public static List<RpgPlayer> Top(int count)
        {
            var result = new List<RpgPlayer>();
            try
            {
                using var conn = Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Name, ClassId, Level, Xp FROM Players ORDER BY Level DESC, Xp DESC LIMIT @c;";
                cmd.Parameters.AddWithValue("@c", count);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(new RpgPlayer
                    {
                        Name = reader.GetString(0),
                        ClassId = reader.IsDBNull(1) ? null : reader.GetString(1),
                        Level = reader.GetInt32(2),
                        Xp = reader.GetInt64(3)
                    });
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"TSRpg: error reading ranking: {ex.Message}");
            }
            return result;
        }

        private static SqliteConnection Open()
        {
            var conn = new SqliteConnection($"Data Source={DbPath}");
            conn.Open();
            return conn;
        }
    }
}
