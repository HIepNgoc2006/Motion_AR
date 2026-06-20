using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using SQLite;
using UnityEngine;

namespace MotionAR.Database
{
    /// <summary>
    /// ORM record that maps to the <c>Users</c> SQLite table.
    /// </summary>
    [Table("Users")]
    public sealed class UserRecord
    {
        /// <summary>
        /// Unique user identifier stored as a GUID string.
        /// </summary>
        [PrimaryKey]
        public string UserId { get; set; }

        /// <summary>
        /// Login / display username. Must be unique across all users.
        /// </summary>
        [Unique]
        public string Username { get; set; }

        /// <summary>
        /// Full display name of the user.
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// UTC timestamp indicating when the user record was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// ORM record that maps to the <c>TrainingSessions</c> SQLite table.
    /// </summary>
    [Table("TrainingSessions")]
    public sealed class TrainingSessionRecord
    {
        /// <summary>
        /// Unique session identifier stored as a GUID string.
        /// </summary>
        [PrimaryKey]
        public string SessionId { get; set; }

        /// <summary>
        /// Foreign-key reference to <see cref="UserRecord.UserId"/>.
        /// </summary>
        [Indexed]
        public string UserId { get; set; }

        /// <summary>
        /// Identifier of the lesson that was trained in this session.
        /// </summary>
        public string LessonId { get; set; }

        /// <summary>
        /// Total wall-clock time the session lasted, in seconds.
        /// </summary>
        public float TotalTimeSeconds { get; set; }

        /// <summary>
        /// Number of mistakes the trainee made during the session.
        /// </summary>
        public int MistakeCount { get; set; }

        /// <summary>
        /// Computed score for the session (0-100 scale recommended).
        /// </summary>
        public float Score { get; set; }

        /// <summary>
        /// UTC timestamp indicating when the session was completed.
        /// </summary>
        public DateTime CompletedAt { get; set; }
    }

    /// <summary>
    /// Thread-safe, lazy singleton that manages a local SQLite database
    /// for the Monitor AR Training Simulator.
    /// <para>
    /// This is a pure C# singleton (not a <see cref="MonoBehaviour"/>).
    /// Call <see cref="InitializeAsync"/> once at startup before using any
    /// other method.
    /// </para>
    /// <para>
    /// Uses the synchronous <see cref="SQLiteConnection"/> API from
    /// sqlite-net-pcl, wrapped in <c>UniTask.RunOnThreadPool</c> to avoid
    /// blocking Unity's main thread during I/O operations.
    /// </para>
    /// </summary>
    public sealed class SQLiteConnector
    {
        private static readonly Lazy<SQLiteConnector> _lazy =
            new Lazy<SQLiteConnector>(() => new SQLiteConnector());

        /// <summary>
        /// Global singleton instance of <see cref="SQLiteConnector"/>.
        /// </summary>
        public static SQLiteConnector Instance => _lazy.Value;

        private SQLiteConnection _connection;
        private readonly object _lock = new object();
        private readonly string _dbPath;
        private bool _initialized;

        private SQLiteConnector()
        {
            _dbPath = Path.Combine(Application.persistentDataPath, "database.db");
        }

        /// <summary>
        /// Opens (or creates) the SQLite database file and ensures all
        /// required tables exist. Safe to call multiple times; subsequent
        /// calls are no-ops.
        /// </summary>
        /// <param name="ct">Cancellation token for cooperative cancellation.</param>
        public async UniTask InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                await UniTask.RunOnThreadPool(() =>
                {
                    lock (_lock)
                    {
                        if (_initialized) return;

                        _connection = new SQLiteConnection(_dbPath);
                        _connection.CreateTable<UserRecord>();
                        _connection.CreateTable<TrainingSessionRecord>();
                        _initialized = true;
                    }
                }, cancellationToken: ct);

                Debug.Log($"[SQLiteConnector] Database initialized at {_dbPath}");
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[SQLiteConnector] InitializeAsync was cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SQLiteConnector] Failed to initialize database: {ex}");
                throw; // Re-throw so callers can detect and handle the failure.
            }
        }

        /// <summary>
        /// Retrieves an existing <see cref="UserRecord"/> by
        /// <paramref name="username"/>, or creates a new one if no match is
        /// found.
        /// </summary>
        /// <param name="username">Unique username to look up or create.</param>
        /// <param name="fullName">
        /// Full display name used when a new record is created.
        /// </param>
        /// <param name="ct">Cancellation token for cooperative cancellation.</param>
        /// <returns>
        /// The existing or newly created <see cref="UserRecord"/>, or
        /// <c>null</c> if a database error occurs.
        /// </returns>
        public async UniTask<UserRecord> GetOrCreateUserAsync(
            string username,
            string fullName,
            CancellationToken ct = default)
        {
            try
            {
                EnsureInitialized();

                UserRecord result = await UniTask.RunOnThreadPool(() =>
                {
                    lock (_lock)
                    {
                        UserRecord existing = _connection
                            .Table<UserRecord>()
                            .Where(u => u.Username == username)
                            .FirstOrDefault();

                        if (existing != null)
                        {
                            return existing;
                        }

                        var newUser = new UserRecord
                        {
                            UserId = Guid.NewGuid().ToString(),
                            Username = username,
                            FullName = fullName,
                            CreatedAt = DateTime.UtcNow
                        };

                        _connection.Insert(newUser);
                        Debug.Log($"[SQLiteConnector] Created user '{username}' ({newUser.UserId})");
                        return newUser;
                    }
                }, cancellationToken: ct);

                return result;
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[SQLiteConnector] GetOrCreateUserAsync was cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SQLiteConnector] GetOrCreateUserAsync failed: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Inserts a new <see cref="TrainingSessionRecord"/> into the database.
        /// </summary>
        /// <param name="userId">
        /// The <see cref="UserRecord.UserId"/> that owns this session.
        /// </param>
        /// <param name="lessonId">Identifier of the completed lesson.</param>
        /// <param name="totalTime">Duration of the session in seconds.</param>
        /// <param name="mistakes">Number of mistakes made.</param>
        /// <param name="score">Computed score for the session.</param>
        /// <param name="ct">Cancellation token for cooperative cancellation.</param>
        public async UniTask InsertSessionAsync(
            string userId,
            string lessonId,
            float totalTime,
            int mistakes,
            float score,
            CancellationToken ct = default)
        {
            try
            {
                EnsureInitialized();

                await UniTask.RunOnThreadPool(() =>
                {
                    lock (_lock)
                    {
                        var record = new TrainingSessionRecord
                        {
                            SessionId = Guid.NewGuid().ToString(),
                            UserId = userId,
                            LessonId = lessonId,
                            TotalTimeSeconds = totalTime,
                            MistakeCount = mistakes,
                            Score = score,
                            CompletedAt = DateTime.UtcNow
                        };

                        _connection.Insert(record);
                    }
                }, cancellationToken: ct);

                Debug.Log(
                    $"[SQLiteConnector] Session recorded – User={userId}, " +
                    $"Lesson={lessonId}, Score={score:F1}");
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[SQLiteConnector] InsertSessionAsync was cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SQLiteConnector] InsertSessionAsync failed: {ex}");
            }
        }

        /// <summary>
        /// Retrieves every <see cref="TrainingSessionRecord"/> for the
        /// specified user, ordered by <see cref="TrainingSessionRecord.CompletedAt"/>
        /// descending (most recent first).
        /// </summary>
        /// <param name="userId">
        /// The <see cref="UserRecord.UserId"/> whose history to fetch.
        /// </param>
        /// <param name="ct">Cancellation token for cooperative cancellation.</param>
        /// <returns>
        /// A list of sessions ordered newest-first, or an empty list if a
        /// database error occurs.
        /// </returns>
        public async UniTask<List<TrainingSessionRecord>> GetSessionHistoryAsync(
            string userId,
            CancellationToken ct = default)
        {
            try
            {
                EnsureInitialized();

                List<TrainingSessionRecord> sessions = await UniTask.RunOnThreadPool(() =>
                {
                    lock (_lock)
                    {
                        return _connection
                            .Table<TrainingSessionRecord>()
                            .Where(s => s.UserId == userId)
                            .OrderByDescending(s => s.CompletedAt)
                            .ToList();
                    }
                }, cancellationToken: ct);

                return sessions;
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[SQLiteConnector] GetSessionHistoryAsync was cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SQLiteConnector] GetSessionHistoryAsync failed: {ex}");
                return new List<TrainingSessionRecord>();
            }
        }

        /// <summary>
        /// Guards against usage before <see cref="InitializeAsync"/> has
        /// completed successfully.
        /// </summary>
        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException(
                    "[SQLiteConnector] Database has not been initialized. " +
                    "Call InitializeAsync() first.");
            }
        }
    }
}
