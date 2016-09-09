﻿namespace SIMServer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    public class Main
    {
        public Main()
        {
            var configInterface = new SIMCommon.JsonFileInterface(SIMCommon.Constants.SIMServerConfigFilename);
            this.Config = configInterface.GetObject<Config>();

            this.Clients = new Dictionary<IPAddress, SIMServer.Client>();

            this.Listener = new SIMServer.Listener(SIMCommon.Constants.SIMServerPort, this.ClientRequestHandler, this.UnknownRequestHandler);

            this.Database = new SIMServer.AuthDB(this.Config.ConnectionInfo);

            if (!File.Exists(SIMCommon.Constants.SIMServerBacklogFilename) || File.ReadAllText(SIMCommon.Constants.SIMServerBacklogFilename).Trim().Length == 0)
            {
                this.Backlog = new Backlog();
            }
            else
            {
                var backlogInterface = new SIMCommon.JsonFileInterface(SIMCommon.Constants.SIMServerBacklogFilename);
                this.Backlog = backlogInterface.GetObject<Backlog>();
            }

            this.LeaseMonitor = new Thread(() => this.MonitorLeases());
            this.LeaseMonitor.Start();
        }

        public Dictionary<IPAddress, Client> Clients { get; private set; }

        public Listener Listener { get; private set; }

        public Config Config { get; private set; }

        public AuthDB Database { get; private set; }

        public Backlog Backlog { get; private set; }

        public Thread LeaseMonitor { get; private set; }

        private void ClientRequestHandler(EventArgs e, SIMCommon.Requests.Encrypted encryptedRequest, IPAddress address)
        {
            if (this.Clients.Keys.Contains(address))
            {
                var targetClient = this.Clients[address];
                if (!targetClient.CheckLeaseExpired(this.Config.LeaseDuration))
                {
                    string decryptedRequest = targetClient.PGPClient.Decrypt(encryptedRequest.EncryptedRequest, encryptedRequest.EncryptedSessionKey);
                    string response = this.ProcessRequest(decryptedRequest, address);
                    this.Listener.Respond(response);
                }
                else
                {
                    var response = new SIMCommon.Responses.NoLease();
                    this.Listener.Respond(JsonConvert.SerializeObject(response));
                }
            }
            else
            {
                this.UnknownRequestHandler(e, encryptedRequest, address);
            }
        }

        private void UnknownRequestHandler(EventArgs e, SIMCommon.Requests.Base request, IPAddress address)
        {
        }

        private string ProcessRequest(string decryptedRequest, IPAddress address)
        {
            string response;
            var baseRequest = JsonConvert.DeserializeObject<SIMCommon.Requests.Base>(decryptedRequest);
            if (baseRequest.RequestType == typeof(SIMCommon.Requests.Base))
            {
                response = SIMCommon.Constants.SIMServerInvalidRequestResponse;
            }
            else if (baseRequest.RequestType == typeof(SIMCommon.Requests.Authenticate))
            {
                var request = JsonConvert.DeserializeObject<SIMCommon.Requests.Authenticate>(decryptedRequest);
                SIMCommon.Responses.Authenticate result;
                if (this.Database.UserExists(request.Username))
                {
                    result = new SIMCommon.Responses.Authenticate(this.Database.GetUser(this.Database.GetUserID(request.Username)).Authenticate(request.Password));
                }
                else
                {
                    result = new SIMCommon.Responses.Authenticate(false);
                }

                response = JsonConvert.SerializeObject(result);
            }
            else if (baseRequest.RequestType == typeof(SIMCommon.Requests.EndCommunication))
            {
                this.Clients.Remove(address);
                var result = new SIMCommon.Responses.EndCommunication();
                response = JsonConvert.SerializeObject(result);
            }
            else if (baseRequest.RequestType == typeof(SIMCommon.Requests.Get))
            {
                if (this.Clients[address].User != null)
                {
                    var messages = this.Backlog.Messages[this.Clients[address].User.ID];
                    var result = new SIMCommon.Responses.Get(messages);
                    response = JsonConvert.SerializeObject(result);
                }
                else
                {
                    response = JsonConvert.SerializeObject(new SIMCommon.Responses.Get(null));
                }
            }
            else if (baseRequest.RequestType == typeof(SIMCommon.Requests.Send))
            {
                var request = JsonConvert.DeserializeObject<SIMCommon.Requests.Send>(decryptedRequest);
                SIMCommon.Responses.Send result;
                if (this.Database.UserExists(request.Message.RecipientID))
                {
                    this.Backlog.Messages[request.Message.RecipientID].Add(request.Message);
                    result = new SIMCommon.Responses.Send(true);
                }
                else
                {
                    result = new SIMCommon.Responses.Send(false);
                }

                response = JsonConvert.SerializeObject(result);
            }
            else if (baseRequest.RequestType == typeof(SIMCommon.Requests.SignIn))
            {
                var request = JsonConvert.DeserializeObject<SIMCommon.Requests.SignIn>(decryptedRequest);
                SIMCommon.Responses.SignIn result;
                if (this.Database.UserExists(request.Username))
                {
                    var user = this.Database.GetUser(this.Database.GetUserID(request.Username));
                    if (user.Authenticate(request.Password))
                    {
                        this.Clients[address].LoadUser(user);
                        result = new SIMCommon.Responses.SignIn(true);
                    }
                    else
                    {
                        result = new SIMCommon.Responses.SignIn(false);
                    }
                }
                else
                {
                    result = new SIMCommon.Responses.SignIn(false);
                }

                response = JsonConvert.SerializeObject(result);
            }
            else if (baseRequest.RequestType == typeof(SIMCommon.Requests.SignOut))
            {
                this.Clients[address].RemoveUser();
                var result = new SIMCommon.Responses.SignOut();
                response = JsonConvert.SerializeObject(result);
            }
            else if (baseRequest.RequestType == typeof(SIMCommon.Requests.UserRef))
            {
                var request = JsonConvert.DeserializeObject<SIMCommon.Requests.UserRef>(decryptedRequest);
                SIMCommon.Responses.UserRef result;
                if (this.Database.UserExists(request.ID))
                {
                    var user = this.Database.GetUser(request.ID);
                    result = new SIMCommon.Responses.UserRef(user.Nickname);
                }
                else
                {
                    result = new SIMCommon.Responses.UserRef(null);
                }

                response = JsonConvert.SerializeObject(result);
            }
            else
            {
                response = SIMCommon.Constants.SIMServerInvalidRequestResponse;
            }

            return response;
        }

        private bool CheckUserActive(int id)
        {
            if (this.Clients.Values.ToList().Count(client => client.User.ID == id) == 1)
            {
                return true;
            }

            return false;
        }

        private IPAddress GetUserClient(int id)
        {
            if (this.CheckUserActive(id))
            {
                return this.Clients.Keys.ToList().Find(client => this.Clients[client].User.ID == id);
            }

            return null;
        }

        private void MonitorLeases()
        {
            while (true)
            {
                foreach (var client in this.Clients.Keys)
                {
                    if (!this.Clients[client].CheckLeaseExpired(this.Config.LeaseDuration))
                    {
                        this.Clients.Remove(client);
                        this.Listener.UpdateClients(this.Clients.Keys.ToList());
                    }
                }

                Thread.Sleep(100);
            }
        }
    }
}