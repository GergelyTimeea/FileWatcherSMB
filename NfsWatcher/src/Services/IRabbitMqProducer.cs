namespace FileWatcherSMB.Services
{
    public interface IRabbitMqProducer
    {
        Task SendMessageAsync(string message); /*Orice implementare trebuie să aibă această metodă asincronă 
        care trimite un mesaj text către coadă.*/
    }
}