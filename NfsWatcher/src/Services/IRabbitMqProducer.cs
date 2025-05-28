namespace FileWatcherSMB.Services
{
    public interface IRabbitMqProducer
    {
        Task SendMessageAsync(string message);
    }
}