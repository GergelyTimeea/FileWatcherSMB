using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileWatcherSMB.src.Services
{
    public interface IConnectionFactoryWrapper
    {
        Task<IConnection> CreateConnectionAsync();
    }
}
