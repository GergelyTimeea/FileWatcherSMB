namespace FileWatcherSMB.Models
{ //Clasă simplă folosită pentru a mapa setările din configurație (appsettings.json) către proprietăți C#.
//E folosită la inițializarea lui RabbitMqProducer pentru a ști unde și cum să trimită mesajele.
    public class RabbitMqOptions
    {
        public string HostName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string QueueName { get; set; } = string.Empty;
    }
}