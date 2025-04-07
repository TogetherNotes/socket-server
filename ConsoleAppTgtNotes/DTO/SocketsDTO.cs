namespace WebApplicationTgtNotes.DTO
{
    public class SocketsDTO
    {
        public string type { get; set; } // "chat", "read_ack", etc.
        public int sender_id { get; set; }
        public int receiver_id { get; set; }
        public string content { get; set; }
        public int message_id { get; set; } // solo usado en read_ack
    }

}