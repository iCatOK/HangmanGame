using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;

namespace ChatHubApplication
{
    public class ChatHub: Hub
    {
        private const string LobbyName = "Хаб";
        private const int WordCount = 300;

        public Connection? GetContextConnection(string name)
        {
            using (var db = new DatabaseContext())
            {
                var connection = db.Connections
                         .Include(c => c.GameRoom)
                         .SingleOrDefault(c => c.Name == name);

                if (connection == null) return null;
                
                if (connection.ConnectionId != Context.ConnectionId) return null;

                return connection;
            } 
        }
        
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            using (var db = new DatabaseContext())
            {
                var connections = await db.Connections
                    .Where(c => c.ConnectionId == Context.ConnectionId)
                    .Include(c => c.GameRoom)
                    .ToListAsync();

                foreach (var connection in connections)
                {
                    if (connection != null)
                    {
                        connection.UserStatus = UserStatus.Disconnected;
                        await db.SaveChangesAsync();

                        if (connection.GameRoom != null)
                        {
                            var roomName = connection.GameRoom.RoomName;
                            await Clients.OthersInGroup(roomName).SendAsync("Send", $"{connection.Name} отключился от хаба.", roomName);

                            await UpdateReadyCounter(db, roomName);
                        }
                    }
                }   
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task Authorize(string name)
        {
            using (var db = new DatabaseContext())
            {
                var contextConnection = await db.Connections
                        .Include(c => c.GameRoom)
                        .SingleOrDefaultAsync(c => c.ConnectionId == Context.ConnectionId);

                var connectionToAuth = await db.Connections
                        .Include(c => c.GameRoom)
                        .SingleOrDefaultAsync(c => c.Name == name);

                if (contextConnection != null && contextConnection.Name == name)
                {
                    await Clients.Caller.SendAsync("Send", $"Вы уже авторизованы как {name}.", LobbyName);
                    return;
                }
                
                if (connectionToAuth != null && connectionToAuth.UserStatus != UserStatus.Disconnected)
                {
                    await Clients.Caller.SendAsync("Send", $"Ник {name} уже занят. Выбери другой.", LobbyName);
                    return;
                }

                if (contextConnection != null)
                {
                    if (connectionToAuth != null && connectionToAuth.UserStatus == UserStatus.Disconnected)
                    {
                        db.Connections.Remove(connectionToAuth);
                    }

                    contextConnection.UserStatus = UserStatus.NotReady;
                    var prevName = contextConnection.Name;
                    contextConnection.Name = name;
                    await Clients.Caller.SendAsync("Send", $"Добро пожаловать, {contextConnection.Name}.", LobbyName);
                    await EnterToDisconnectedRoom(contextConnection, prevName);
                    await db.SaveChangesAsync();
                    return;
                }

                if (connectionToAuth == null)
                {
                    connectionToAuth = new Connection()
                    {
                        Name = name,
                        ConnectionId = Context.ConnectionId,
                        UserStatus = UserStatus.NotReady,
                    };

                    await db.Connections.AddAsync(connectionToAuth);
                    await Clients.Caller.SendAsync("Send", $"Добро пожаловать, {connectionToAuth.Name}.", LobbyName);
                }
                else
                {
                    connectionToAuth.ConnectionId = Context.ConnectionId;
                    connectionToAuth.UserStatus = UserStatus.NotReady;
                    db.Connections.Update(connectionToAuth);
                    await EnterToDisconnectedRoom(connectionToAuth);
                }

                await db.SaveChangesAsync();
            }
        }

        private async Task EnterToDisconnectedRoom(Connection? connection, string? prevName = null)
        {
            if (connection != null && connection.GameRoom != null)
            {
                var roomName = connection.GameRoom.RoomName;
                
                if (prevName != null)
                {
                    await Clients.OthersInGroup(roomName).SendAsync("Send", $"{prevName} вышел из комнаты.", roomName);
                } else
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, roomName);
                }
                
                await Clients.Caller.SendAsync("Send", $"Добро пожаловать в комнату '{roomName}', {connection.Name}.", roomName);
                await Clients.OthersInGroup(roomName).SendAsync("Send", $"{connection.Name} вошёл в комнату.", roomName);

                if (connection.GameRoom.GameStatus == RoomStatus.InGame)
                {
                    connection.UserStatus = UserStatus.InGame;
                }

                using (var db = new DatabaseContext())
                {
                    await UpdateReadyCounter(db, roomName);
                }
            }
        }

        public async Task AddToRoom(string roomName, string name)
        {
            if (!string.IsNullOrEmpty(roomName))
            {
                using (var db = new DatabaseContext())
                {
                    var connection = GetContextConnection(name);

                    if (connection == null)
                    {
                        await Clients.Caller.SendAsync("Send", $"Для взаимодействия с комнатами нужно авторизоваться именем.", LobbyName);
                        return;
                    };

                    if (connection.GameRoom != null && connection.GameRoom.RoomName == roomName)
                    {
                        await Clients.Caller.SendAsync("Send", $"Вы уже состоите в комнате {roomName}", roomName);
                        return;
                    }

                    var gameRoom = await db.GameRooms
                        .SingleOrDefaultAsync(room => room.RoomName == roomName);

                    if (gameRoom == null)
                    {
                        gameRoom = new GameRoom
                        {
                            RoomName = roomName,
                            GameStatus = RoomStatus.Lobby
                        };
                        db.GameRooms.Add(gameRoom);
                    }

                    connection.GameRoom = gameRoom;
                    db.Connections.Update(connection);

                    await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

                    await Clients.Caller.SendAsync("Send", $"Вы вошли в комнату {roomName}.", roomName);
                    await Clients.Caller.SendAsync("RoomEnteredEvent");

                    await Clients.OthersInGroup(roomName).SendAsync("Send", $"{connection.Name} вошел в комнату.", roomName);

                    await db.SaveChangesAsync();
                    await UpdateReadyCounter(db, roomName);
                }
            }
        }

        public async Task RemoveFromRoom(string name)
        {
            using (var db = new DatabaseContext())
            {
                var connection = GetContextConnection(name);
                if (connection == null)
                {
                    await Clients.Caller.SendAsync("Send", $"Для взаимодействия с комнатами нужно авторизоваться именем.", LobbyName);
                    return;
                }

                if (connection.GameRoom == null)
                {
                    await Clients.Caller.SendAsync("Send", $"Вы уже не состоите в комнате", LobbyName);
                    return;
                }

                var roomName = connection.GameRoom.RoomName;
                
                await Clients.Caller.SendAsync("Send", $"Вы вышли из комнаты {roomName}.", "Лобби");
                await Clients.OthersInGroup(roomName).SendAsync("Send", $"{connection.Name} вышел из комнаты.", roomName);
                await Clients.Caller.SendAsync("RoomExitedEvent");

                connection.GameRoomId = null;
                connection.GameRoom = null;
                db.Connections.Update(connection);
                await db.SaveChangesAsync();

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
                await UpdateReadyCounter(db, roomName);
            }
            
        }

        public async Task Send(string message, string name)
        {
            using (var db = new DatabaseContext())
            {
                var connection = GetContextConnection(name);

                if (connection == null)
                {
                    await Clients.Caller.SendAsync("Send", message, LobbyName);
                    return;
                }

                if (connection.GameRoom == null)
                {
                    await Clients.Caller.SendAsync("Send", message, connection.Name);
                    return;
                }

                await Clients.Groups(connection.GameRoom.RoomName)
                        .SendAsync("Send", message, connection.Name);
            }
        }

        public async Task Ready(bool isReady, string name)
        {
            using (var db = new DatabaseContext())
            {
                var connection = GetContextConnection(name);

                if (connection != null)
                {
                    if (connection.UserStatus == UserStatus.Disconnected ||
                        connection.UserStatus == UserStatus.InGame ||
                        connection.GameRoom == null)
                    {
                        return;
                    }

                    connection.UserStatus = isReady ? UserStatus.Ready : UserStatus.NotReady;
                    var readyString = isReady ? "готов" : "не готов";
                    var roomName = connection.GameRoom.RoomName;

                    db.Connections.Update(connection);
                    await db.SaveChangesAsync();

                    await Clients.Caller.SendAsync("ReadyStateUpdate", isReady);
                    await UpdateReadyCounter(db, connection.GameRoom.RoomName);

                    await Clients.Groups(connection.GameRoom.RoomName)
                        .SendAsync("Send", $"{name} {readyString} к игре", connection.GameRoom.RoomName);

                    var userCount = await db.Connections.CountAsync(c => c.GameRoom != null && c.GameRoom.RoomName == roomName);
                    var readyCount = await db.Connections.CountAsync(
                        c => c.UserStatus == UserStatus.Ready && c.GameRoom != null && c.GameRoom.RoomName == roomName);

                    if (readyCount > userCount / 2 && readyCount > 1)
                    {
                        await CountDown(connection);
                    }
                }
            }
        }

        private async Task UpdateReadyCounter(DatabaseContext db, string roomName)
        {
            var gameRoom = db.GameRooms.Include(r => r.Connections).SingleOrDefault(r => r.RoomName == roomName);

            if (gameRoom != null)
            {
                var userCount = gameRoom.Connections.Where(c => c.GameRoom != null && c.GameRoom.RoomName == roomName).Count();
                var readyCount = gameRoom.Connections.Where(
                    c => c.UserStatus == UserStatus.Ready && c.GameRoom != null && c.GameRoom.RoomName == roomName).Count();
                
                if (gameRoom.GameStatus == RoomStatus.Lobby)
                {
                    await Clients.Groups(roomName).SendAsync("UpdateReadyCounter", userCount, readyCount);
                }    
                else
                {
                    var session = db.GameSessions.SingleOrDefault(s => s.GameRoomId == gameRoom.Id);

                    if (session != null)
                        await Clients.Groups(gameRoom.RoomName).SendAsync("UpdateGameHeader", session.GuessingWord);
                }
                    
            }
        }

        public async Task CountDown(Connection connection)
        {
            using (var db = new DatabaseContext())
            {
                if (connection == null) return;
                if (connection != null && connection.GameRoom == null) return;

                var roomName = connection.GameRoom.RoomName;

                for (int i = 10; i > 0; i--)
                {
                    await Clients.Groups(roomName).SendAsync("Send", $"Игра начнётся через {i} сек...", roomName);
                    await Task.Delay(1 * 1000);
                }

                await GameStart(connection.GameRoom, connection);
            }
        }

        public async Task GameStart(GameRoom room, Connection connection)
        {
            using (var db = new DatabaseContext())
            {
                var gamers = room.Connections
                    .Where(c => c.GameRoom == room && c.UserStatus == UserStatus.Ready)
                    .OrderBy(c => c.Id)
                    .ToList();

                if (gamers.Count == 0)
                {
                    await Clients.Groups(room.RoomName)
                       .SendAsync("Send", $"Недостаточно игроков для начала игры.", room.RoomName);
                    return;
                }
                
                gamers.ForEach(g => g.UserStatus = UserStatus.InGame);
                room.GameStatus = RoomStatus.InGame;

                Random random = new Random(DateTime.Now.GetHashCode());
                var word = File.ReadAllLines("words.txt")[random.Next(WordCount)];
                var guessingWord = new string('*', word.Length);

                var firstGamer = gamers[0];

                var session = new GameSession
                {
                    GameRoom = room,
                    GuessingConnection = firstGamer,
                    WinWord = word,
                    GuessingWord = guessingWord,
                };

                db.UpdateRange(gamers);
                db.Update(room);
                await db.AddAsync(session);
                await db.SaveChangesAsync();

                await Clients.Groups(room.RoomName)
                    .SendAsync("Send", $"Игра началась. Загаданное слово: {guessingWord}. Отгадывать начинает {firstGamer.Name}." +
                    $" Букв: {guessingWord.Length}", connection.GameRoom.RoomName);
                
                await Clients.Groups(room.RoomName).SendAsync("GameStart");
                await Clients.Groups(room.RoomName).SendAsync("UpdateGameHeader", guessingWord);
                await Clients.Client(firstGamer.ConnectionId).SendAsync("TurnEvent");
            }
        }

        public async Task TurnAttempt(string guess, string name)
        {
            using(var db = new DatabaseContext())
            {
                
                var connection = GetContextConnection(name);
                if (connection == null) return;

                var gameRoom = connection.GameRoom;
                if (gameRoom == null || gameRoom.GameStatus == RoomStatus.Lobby) return;

                var gamers = db.Connections
                    .Where(c => c.GameRoomId == gameRoom.Id && c.UserStatus == UserStatus.InGame)
                    .ToList();
                if (gamers.Count < 2) return;

                var roomName = gameRoom.RoomName;

                var gameSession = db.GameSessions.SingleOrDefault(s => s.GameRoomId == gameRoom.Id);
                if (gameSession == null) return;

                gameSession.AttemptCount++;
                var nextIndex = gameSession.AttemptCount % gamers.Count();
                var nextGamer = gamers[nextIndex];

                var attemptString = guess.Length > 1 ? "Слово" : "Буква";

                await Clients.Groups(roomName).SendAsync(
                            "Send",
                            $"{attemptString} {guess}!", name);

                if (HangmanGameHelper.CheckGuess(guess, gameSession))
                {
                    if (gameSession.GuessingWord == gameSession.WinWord)
                    {
                        var winWord = gameSession.WinWord.ToUpper();
                        
                        gamers.ForEach(g => g.UserStatus = UserStatus.NotReady);
                        gameRoom.GameStatus = RoomStatus.Lobby;
                        db.GameSessions.Remove(gameSession);
                        await db.SaveChangesAsync();

                        await Clients.Groups(roomName).SendAsync(
                            "Send",
                            $"Игра окончена, загаданное слово - {winWord}! Победил {name}!", roomName);
                        await Clients.Groups(roomName).SendAsync("GameOver", winWord);
                        await UpdateReadyCounter(db, roomName);
                        return;
                    }
                    else
                    {
                        await Clients.Groups(roomName).SendAsync(
                            "Send",
                            $"{name} угадал букву! Слово: {gameSession.GuessingWord}. Следующим отгадывает {nextGamer.Name}.", roomName);

                        await Clients.Groups(roomName).SendAsync("WordUpdate", gameSession.GuessingWord);
                        await Clients.Groups(roomName).SendAsync("UpdateGameHeader", gameSession.GuessingWord);
                    }
                }
                else
                {
                    await Clients.Groups(roomName).SendAsync(
                                "Send",
                                $"Неверно! Следующим отгадывает {nextGamer.Name}.", roomName);
                }

                await db.SaveChangesAsync();
                await Clients.Caller.SendAsync("TurnOverEvent");
                await Clients.Client(nextGamer.ConnectionId).SendAsync("TurnEvent");
            }
        }
    }
}
