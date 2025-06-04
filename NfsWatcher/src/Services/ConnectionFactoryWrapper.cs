using FileWatcherSMB.Models;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileWatcherSMB.src.Services
{
    public class ConnectionFactoryWrapper : IConnectionFactoryWrapper
    {
        private readonly ConnectionFactory _factory;

        public ConnectionFactoryWrapper(RabbitMqOptions options)
        {
            _factory = new ConnectionFactory
            {
                HostName = options.HostName,
                UserName = options.UserName,
                Password = options.Password
            };
        }

        public Task<IConnection> CreateConnectionAsync() => _factory.CreateConnectionAsync();
    }
}
