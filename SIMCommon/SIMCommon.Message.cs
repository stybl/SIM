﻿namespace SIMCommon
{
    using System;
    using Newtonsoft.Json;

    public class Message
    {
        public Message(int senderID, int recipientID, string text)
        {
            this.SenderID = senderID;
            this.RecipientID = recipientID;
            this.Text = text;
            this.Timestamp = DateTime.Now;
        }

        public Message(int senderID, int recipientID, string text, DateTime timestamp)
        {
            this.SenderID = senderID;
            this.RecipientID = recipientID;
            this.Text = text;
            this.Timestamp = timestamp;
        }

        [JsonConstructor]
        public Message(int senderID, int recipientID, string text, DateTime timestamp, int threadID)
        {
            this.SenderID = senderID;
            this.RecipientID = recipientID;
            this.Text = text;
            this.Timestamp = timestamp;
            this.ThreadID = threadID;
        }

        public int SenderID { get; private set; }

        public int RecipientID { get; private set; }

        public string Text { get; private set; }

        public DateTime Timestamp { get; private set; }

        public int ThreadID { get; private set; }
    }
}
