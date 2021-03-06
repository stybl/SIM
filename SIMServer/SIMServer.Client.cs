﻿namespace SIMServer
{
    using System;
    using System.Net;
    using SCrypto;

    public class Client
    {
        public Client(IPAddress address, string publicKey)
        {
            this.Address = address;
            this.PGPClient = new SCrypto.PGP.SPGP();
            this.User = null;
            this.LeaseStart = DateTime.Now;
            this.PublicKey = publicKey;
        }

        public DateTime LeaseStart { get; private set; }

        public SCrypto.PGP.SPGP PGPClient { get; private set; }

        public IPAddress Address { get; private set; }

        public User User { get; private set; }

        public string PublicKey { get; private set; }

        public void LoadUser(User user)
        {
            this.User = user;
        }

        public void RemoveUser()
        {
            this.User = null;
        }

        public void RenewLease()
        {
            this.LeaseStart = DateTime.Now;
        }

        public bool CheckLeaseExpired(int duration)
        {
            return ((int)(DateTime.Now - this.LeaseStart).TotalMilliseconds) > duration;
        }

        public int RemainingLeaseTime(int duration)
        {
            return (int)(DateTime.Now - this.LeaseStart).TotalMilliseconds;
        }
    }
}
