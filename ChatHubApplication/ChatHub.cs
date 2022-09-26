using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatHubApplication
{
    public class ChatHub: Hub
    {
        public override async Task OnConnectedAsync()
        {
            using (var db = new DatabaseContext())
            {
                var connection = await db.Connections
                    .Include(c => c.GameRoom)
                    .SingleOrDefaultAsync(c => c.ConnectionId == Context.ConnectionId);

                if (connection == null)
                {
                    connection = new Connection()
                    {
                        Name = "anon",
                        ConnectionId = Context.ConnectionId,
                        UserStatus = UserStatus.NotReady,
                    };
                    db.Connections.Add(connection);
                    await db.SaveChangesAsync();
                } 
                else
                {
                    connection.ConnectionId = Context.ConnectionId;
                    db.Connections.Update(connection);
                }

            }
            await base.OnConnectedAsync();
        }

        public async Task AddToRoom(string roomName)
        {
            using (var db = new DatabaseContext())
            {
                
            }
            await Clients.Caller.SendAsync("Send", $"Вы вошли в комнату {roomName}.");
            await Clients.OthersInGroup(roomName).SendAsync("Send", $"Пользователь {Context.ConnectionId} вошел в комнату.");
        }

        public async Task RemoveFromRoom(string roomName)
        {
            using (var db = new DatabaseContext())
            {
               
            }
            await Clients.Caller.SendAsync("Send", $"Вы вышли из комнаты {roomName}.");
            await Clients.OthersInGroup(roomName).SendAsync("Send", $"Пользователь {Context.ConnectionId} вышел из комнаты.");
        }

        public async Task Send(string message, string name)
        {
            using (var db = new DatabaseContext())
            { 
                
            }
            await Clients.All.SendAsync("Send", message, name);
        }
    }
}
