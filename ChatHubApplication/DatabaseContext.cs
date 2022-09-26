using Microsoft.EntityFrameworkCore;

namespace ChatHubApplication
{
    public enum UserStatus : byte
    {
        NotReady = 0,
        Ready = 1,
        InGame = 2,
    }

    public enum GameStatus : byte
    {
        Lobby = 0,
        InGame = 1,
    }

    [Index(nameof(ConnectionId), IsUnique = true)]
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
        public virtual ICollection<Connection> Connections { get; set; }
    }

    public class GameSession
    {
        public int Id { get; set; }

        public int GameRoomId { get; set; }

        public GameRoom GameRoom { get; set; }

        public GameStatus GameStatus { get; set; }

        public int GuessingConnectionId { get; set; }

        public Connection GuessingConnection { get; set; }

        public string GuessingWord { get; set; }

        public string WinWord { get; set; }
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
