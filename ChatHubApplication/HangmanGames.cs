using System.ComponentModel.DataAnnotations.Schema;

namespace ChatHubApplication
{
    [Table("hangman_games")]
    public class HangmanGames
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("game_state")]
        public int GameState { get; set; }

        [Column("gamers_count")]
        public int GamersCount { get; set; }

        [Column("ready_gamers_count")]
        public int ReadyGamersCount { get; set; }

        [Column("win_word")]
        public string? WinWord { get; set; }

        [Column("guessing_word")]
        public string? GuessingWord { get; set; }

        [Column("room_identifier")]
        public string? RoomIdentifier { get; set; }

        [Column("guessing_connection_id")]
        public string? GuessingConnectionId { get; set; }
    }
}
