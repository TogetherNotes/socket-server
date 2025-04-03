using System;
using System.Net;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Threading;
using System.Net.Sockets;
using WebApplicationTgtNotes.DTO;
using System.Collections.Concurrent;
using ConsoleAppTgtNotes.Models;

namespace ConsoleAppTgtNotes
{
    public class ChatManager
    {
        private const int Port = 5000;
        private TcpListener _server;

        private static readonly ConcurrentDictionary<int, TcpClient> ConnectedClients = new ConcurrentDictionary<int, TcpClient>();

        public static void Main(string[] args)
        {
            var server = new ChatManager();
            server.StartServer();
        }

        public void StartServer()
        {
            _server = new TcpListener(IPAddress.Any, Port);
            _server.Start();
            Console.WriteLine($"[SERVER] Listening on port {Port}...");

            while (true)
            {
                TcpClient client = _server.AcceptTcpClient();
                Console.WriteLine("[SERVER] Client connected.");

                Thread clientThread = new Thread(HandleClient);
                clientThread.Start(client);
            }
        }

        private void HandleClient(object obj)
        {
            var client = (TcpClient)obj;
            var stream = client.GetStream();
            var buffer = new byte[1024];
            int byteCount;
            int currentUserId = -1;

            try
            {
                byteCount = stream.Read(buffer, 0, buffer.Length);
                var authJson = Encoding.UTF8.GetString(buffer, 0, byteCount);
                var authData = JsonConvert.DeserializeObject<AuthDTO>(authJson);

                if (authData == null || authData.type != "auth" || authData.userId <= 0)
                {
                    SendResponse(stream, "Invalid auth format");
                    return;
                }

                using (var db = new TgtNotesEntities())
                {
                    if (!db.app.Any(a => a.id == authData.userId))
                    {
                        SendResponse(stream, "User does not exist");
                        return;
                    }
                }

                currentUserId = authData.userId;
                ConnectedClients[currentUserId] = client;
                Console.WriteLine($"[INFO] User {currentUserId} connected");

                while ((byteCount = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, byteCount);
                    Console.WriteLine($"[RECEIVED] {message}");

                    var data = JsonConvert.DeserializeObject<SocketsDTO>(message);

                    if (data == null || data.sender_id != currentUserId || data.receiver_id <= 0 || string.IsNullOrWhiteSpace(data.content))
                    {
                        SendResponse(stream, "Invalid or spoofed message");
                        continue;
                    }

                    using (var db = new TgtNotesEntities())
                    {
                        var chat = db.chats.FirstOrDefault(c =>
                            (c.user1_id == data.sender_id && c.user2_id == data.receiver_id) ||
                            (c.user1_id == data.receiver_id && c.user2_id == data.sender_id));

                        if (chat == null)
                        {
                            chat = new chats
                            {
                                date = DateTime.Now,
                                user1_id = data.sender_id,
                                user2_id = data.receiver_id
                            };
                            db.chats.Add(chat);
                            db.SaveChanges();
                        }

                        var newMessage = new messages
                        {
                            sender_id = data.sender_id,
                            content = data.content,
                            send_at = DateTime.Now,
                            is_read = false,
                            chat_id = chat.id
                        };
                        db.messages.Add(newMessage);
                        db.SaveChanges();
                    }

                    if (ConnectedClients.TryGetValue(data.receiver_id, out var receiverClient))
                    {
                        try
                        {
                            var receiverStream = receiverClient.GetStream();
                            SendResponse(receiverStream, JsonConvert.SerializeObject(new
                            {
                                from = data.sender_id,
                                content = data.content
                            }));
                        }
                        catch (Exception)
                        {
                            Console.WriteLine($"[ERROR] Failed to send message to user {data.receiver_id}. Removing from active clients.");
                            ConnectedClients.TryRemove(data.receiver_id, out _);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
            }
            finally
            {
                if (currentUserId > 0)
                {
                    ConnectedClients.TryRemove(currentUserId, out _);
                    Console.WriteLine($"[INFO] User {currentUserId} disconnected");
                }

                client.Close();
            }
        }

        private void SendResponse(NetworkStream stream, string message)
        {
            var response = Encoding.UTF8.GetBytes(message);
            stream.Write(response, 0, response.Length);
        }
    }
}