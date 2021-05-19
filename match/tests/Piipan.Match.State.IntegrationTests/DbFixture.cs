using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Piipan.Match.State.IntegrationTests
{
    /// <summary>
    /// Test fixture for per-state match API database integration testing.
    /// Creates a fresh set of participants and uploads tables, dropping them
    /// when testing is complete.
    /// </summary>
    public class DbFixture : IDisposable
    {
        public readonly string ConnectionString;
        public readonly NpgsqlFactory Factory;

        public DbFixture()
        {
            ConnectionString = Environment.GetEnvironmentVariable("DatabaseConnectionString");
            Factory = NpgsqlFactory.Instance;

            Initialize();
            ApplySchema();
        }

        /// <summary>
        /// Ensure the database is able to receive connections before proceeding.
        /// </summary>
        public void Initialize()
        {
            var retries = 10;
            var wait = 2000; // ms

            while (retries >= 0)
            {
                try
                {
                    using (var conn = Factory.CreateConnection())
                    {
                        conn.ConnectionString = ConnectionString;
                        conn.Open();
                        conn.Close();

                        return;
                    }
                }
                catch (Npgsql.NpgsqlException ex)
                {
                    retries--;
                    Console.WriteLine(ex.Message);
                    Thread.Sleep(wait);
                }
            }
        }

        public void Dispose()
        {
            using (var conn = Factory.CreateConnection())
            {
                conn.ConnectionString = ConnectionString;
                conn.Open();

                using (var cmd = Factory.CreateCommand())
                {
                    cmd.Connection = conn;

                    cmd.CommandText = "DROP TABLE IF EXISTS participants;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "DROP TABLE IF EXISTS uploads;";
                    cmd.ExecuteNonQuery();
                }

                conn.Close();
            }

        }

        // xxx Apply schema directly from ddl/per-state.sql
        private void ApplySchema()
        {
            using (var conn = Factory.CreateConnection())
            {
                conn.ConnectionString = ConnectionString;
                conn.Open();

                using (var cmd = Factory.CreateCommand())
                {
                    cmd.Connection = conn;

                    cmd.CommandText = "DROP TABLE IF EXISTS participants;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "DROP TABLE IF EXISTS uploads;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"CREATE TABLE uploads(
                        id serial PRIMARY KEY,
                        created_at timestamp NOT NULL,
                        publisher text NOT NULL);";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "INSERT INTO uploads (created_at, publisher) VALUES(now(), current_user)";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"CREATE TABLE IF NOT EXISTS participants(
                        id serial PRIMARY KEY,
                        last text NOT NULL,
                        first text,
                        middle text,
                        dob date NOT NULL,
                        ssn text NOT NULL,
                        exception text,
                        upload_id integer REFERENCES uploads(id)),
                        case_id text NOT NULL,
                        participant_id text;";
                    cmd.ExecuteNonQuery();
                }

                conn.Close();
            }
        }

        public void ClearParticipants()
        {
            using (var conn = Factory.CreateConnection())
            {
                conn.ConnectionString = ConnectionString;
                conn.Open();

                using (var cmd = Factory.CreateCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = "DELETE FROM participants";
                    cmd.ExecuteNonQuery();
                }

                conn.Close();
            }
        }

        public void Insert(PiiRecord record)
        {
            var factory = NpgsqlFactory.Instance;

            using (var conn = factory.CreateConnection())
            {
                conn.ConnectionString = ConnectionString;
                conn.Open();

                Int64 lastval = 0;
                using (var cmd = factory.CreateCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = "SELECT MAX(id) from uploads";
                    lastval = (Int32)cmd.ExecuteScalar();
                }

                using (var cmd = factory.CreateCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = "INSERT INTO participants (last, first, middle, dob, ssn, exception, upload_id, case_id, participant_id) " +
                           "VALUES (@last, @first, @middle, @dob, @ssn, @exception, @upload_id, @case_id, @participant_id)";

                    AddWithValue(cmd, DbType.String, "last", record.Last);
                    AddWithValue(cmd, DbType.String, "first", (object)record.First ?? DBNull.Value);
                    AddWithValue(cmd, DbType.String, "middle", (object)record.Middle ?? DBNull.Value);
                    AddWithValue(cmd, DbType.DateTime, "dob", record.Dob);
                    AddWithValue(cmd, DbType.String, "ssn", record.Ssn);
                    AddWithValue(cmd, DbType.String, "exception", (object)record.Exception ?? DBNull.Value);
                    AddWithValue(cmd, DbType.Int64, "upload_id", lastval);
                    AddWithValue(cmd, DbType.String, "case_id", record.CaseId);
                    AddWithValue(cmd, DbType.String, "participant_id", (object)record.ParticipantId ?? DBNull.Value);

                    cmd.ExecuteNonQuery();
                }

                conn.Close();
            }
        }
        static void AddWithValue(DbCommand cmd, DbType type, String name, object value)
        {
            var p = cmd.CreateParameter();
            p.DbType = type;
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }
    }
}
