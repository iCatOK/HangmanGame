using Microsoft.EntityFrameworkCore;

namespace ChatHubApplication
{
    public enum UserStatus : byte
    {
        Disconnected = 0,
        NotReady = 1,
        Ready = 2,
        InGame = 3,
    }

    public enum RoomStatus : byte
    {
        Lobby = 0,
        InGame = 1,
    }

    public class Connection
    {
        public int Id { get; set; }

        public string? ConnectionId { get; set; }

        public string? Name { get; set; }

        public UserStatus UserStatus { get; set; }

        public int? GameRoomId { get; set; }

        public GameRoom? GameRoom { get; set; }

    }

    public class GameRoom
    {
        public int Id { get; set; }
        public string RoomName { get; set; }
        public RoomStatus GameStatus { get; set; }
        public virtual ICollection<Connection> Connections { get; set; }
    }

    public class GameSession
    {
        public int Id { get; set; }

        public int GameRoomId { get; set; }

        public GameRoom GameRoom { get; set; }

        public int GuessingConnectionId { get; set; }

        public Connection GuessingConnection { get; set; }

        public string GuessingWord { get; set; }

        public string WinWord { get; set; }

        public int AttemptCount { get; set; }
    }

    public class DatabaseContext : DbContext
    {
        public DbSet<Connection> Connections { get; set; }
        public DbSet<GameRoom> GameRooms { get; set; }
        public DbSet<GameSession> GameSessions { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=database.db");
        }

        public DatabaseContext() => Database.EnsureCreated();
    }
}
