using System;
using System.Net;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Threading;
using System.Net.Sockets;
using ConsoleAppTgtNotes.Models;
using WebApplicationTgtNotes.DTO;
using System.Collections.Concurrent;
using System.IO;

namespace ConsoleAppTgtNotes
{
    public class ChatManager
    {
        private const int Port = 5000;
        private TcpListener _server;

        // Stores connected clients by user ID
        private static readonly ConcurrentDictionary<int, TcpClient> ConnectedClients = new ConcurrentDictionary<int, TcpClient>();

        /// <summary>
        /// Entry point of the chat server application.
        /// </summary>
        public static void Main(string[] args)
        {
            var server = new ChatManager();
            server.StartServer();
        }

        /// <summary>
        /// Starts the TCP server and listens for incoming client connections.
        /// </summary>
        public void StartServer()
        {
            _server = new TcpListener(IPAddress.Any, Port);
            _server.Start();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SERVER] Listening on port {Port}...");

            while (true)
            {
                TcpClient client = _server.AcceptTcpClient();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SERVER] Client connected.");

                Thread clientThread = new Thread(HandleClient);
                clientThread.Start(client);
            }
        }

        /// <summary>
        /// Handles a single client: authentication, message sync, message reception, and forwarding.
        /// </summary>
        /// <param name="obj">The connected TcpClient object.</param>
        private void HandleClient(object obj)
        {
            var client = (TcpClient)obj;
            var stream = client.GetStream();
            var buffer = new byte[1024];
            int byteCount;
            int currentUserId = -1;

            try
            {
                // Receive and validate initial auth message
                byteCount = stream.Read(buffer, 0, buffer.Length);
                var authJson = Encoding.UTF8.GetString(buffer, 0, byteCount);
                var authData = JsonConvert.DeserializeObject<AuthDTO>(authJson);

                if (authData == null || authData.type != "auth" || authData.userId <= 0)
                {
                    SendResponse(stream, "Invalid auth format");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [AUTH] Invalid auth format.");
                    return;
                }

                using (var db = new TgtNotesEntities())
                {
                    if (!db.app.Any(a => a.id == authData.userId))
                    {
                        SendResponse(stream, "User does not exist");
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [AUTH] User {authData.userId} not found.");
                        return;
                    }
                }

                currentUserId = authData.userId;
                ConnectedClients[currentUserId] = client;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [INFO] User {currentUserId} connected.");

                // Send historical messages to the user
                // Enviar mensajes históricos a la interfaz del usuario sin marcar como leídos
                using (var db = new TgtNotesEntities())
                {
                    var messages = db.messages
                        .Where(m => m.chats.user1_id == currentUserId || m.chats.user2_id == currentUserId)
                        .OrderBy(m => m.send_at)
                        .ToList();

                    foreach (var message in messages)
                    {
                        var responseMessage = JsonConvert.SerializeObject(new
                        {
                            type = "message",
                            message_id = message.id,
                            from = message.sender_id,
                            content = message.content,
                            is_read = message.is_read  // No marcar como leído aquí
                        });

                        SendResponse(stream, responseMessage);

                    }

                    // Guardamos solo si hubo cambios
                    db.SaveChanges();
                }


                // Listen for incoming messages
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string messageLine;
                    while ((messageLine = reader.ReadLine()) != null)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [RECEIVED] {messageLine}");

                        SocketsDTO data;
                        try
                        {
                            data = JsonConvert.DeserializeObject<SocketsDTO>(messageLine);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] JSON inválido: {ex.Message}");
                            continue;
                        }

                        if (data.type == "read_ack" && data.message_id > 0)
                        {
                            using (var db = new TgtNotesEntities())
                            {
                                var msgToUpdate = db.messages.FirstOrDefault(m => m.id == data.message_id);
                                if (msgToUpdate != null)
                                {
                                    // Solo marcamos como leído si no está ya marcado
                                    if (!msgToUpdate.is_read.GetValueOrDefault())
                                    {
                                        msgToUpdate.is_read = true;
                                        db.SaveChanges();
                                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [READ_ACK] Mensaje {data.message_id} marcado como leído.");
                                    }
                                }
                            }
                            continue;
                        }
    


                        if (data == null || data.sender_id != currentUserId || data.receiver_id <= 0 || string.IsNullOrWhiteSpace(data.content))
                        {
                            SendResponse(stream, "Invalid or spoofed message");
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SECURITY] Invalid or spoofed message from user {currentUserId}.");
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

                            // Forward message if receiver is connected
                            if (ConnectedClients.TryGetValue(data.receiver_id, out var receiverClient))
                            {
                                try
                                {
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [INFO] Sending message to user {data.receiver_id}");
                                    var receiverStream = receiverClient.GetStream();
                                    SendResponse(receiverStream, JsonConvert.SerializeObject(new
                                    {
                                        type = "message",
                                        message_id = newMessage.id,
                                        from = data.sender_id,
                                        content = data.content,
                                        is_read = newMessage.is_read
                                    }));

                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] Failed to send to user {data.receiver_id}: {ex.Message}");
                                    ConnectedClients.TryRemove(data.receiver_id, out _);
                                }
                            }
                            else
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [INFO] User {data.receiver_id} is offline.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] {ex.Message}");
            }
            finally
            {
                if (currentUserId > 0)
                {
                    ConnectedClients.TryRemove(currentUserId, out _);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [INFO] User {currentUserId} disconnected");
                }

                client.Close();
            }
        }

        /// <summary>
        /// Sends a string response to a client's stream.
        /// </summary>
        /// <param name="stream">The network stream to write to.</param>
        /// <param name="message">The message to send as a response.</param>
        private void SendResponse(NetworkStream stream, string message)
        {
            var response = Encoding.UTF8.GetBytes(message);
            stream.Write(response, 0, response.Length);
            stream.WriteByte((byte)'\n'); // Ensure newline termination for client-side reading
        }
    }
}