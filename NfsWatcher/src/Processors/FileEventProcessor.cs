using FileWatcherSMB.Helpers;
using FileWatcherSMB.Services;
using FileWatcherSMB.src.Watchers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileWatcherSMB.src.Processors
{
    //Este un serviciu care rulează în fundal (background) și procesează evenimentele colectate de watcher.
    public class FileEventProcessor : BackgroundService //Moștenește BackgroundService
    // adică pornește automat când pornește aplicația și rulează independent de restul codului.
    {
        private readonly IConcurrentHashSet _eventMap;
        private readonly IRabbitMqProducer _producer; //Un obiect responsabil să trimită mesajele (evenimentele) către RabbitMQ.
        private readonly ILogger<FileEventProcessor> _logger;

        public FileEventProcessor(IConcurrentHashSet eventMap, IRabbitMqProducer producer, ILogger<FileEventProcessor> logger)
        {
            //Primește toate dependințele de care are nevoie (setul de evenimente, producătorul RabbitMQ și logger-ul)
            //  și le salvează în membri privați.
            _eventMap = eventMap;
            _producer = producer;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        { //Este metoda care rulează în buclă cât timp aplicația este pornită.
            try
            {
                while (!stoppingToken.IsCancellationRequested) //Cât timp aplicația nu e oprită
                                                               //.NET Host interceptează semnalul de întrerupere (SIGINT) generat de Ctrl+C.
                                                              //Trimite un CancellationToken către toate serviciile de fundal (inclusiv FileEventProcessor).
                {
                    foreach (var path in _eventMap.Items) //Ia toate căile de fișiere din setul de evenimente (_eventMap.Items)
                    {
                        try
                        {
                            await _producer.SendMessageAsync($"Eveniment: {path}"); //Încearcă să trimită un mesaj cu acea cale la RabbitMQ.
                            _eventMap.Remove(path); // Dacă trimiterea reușește, șterge acea cale din set (ca să nu fie procesată de două ori).
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Eroare la trimiterea mesajului pentru path-ul: {Path}", path); //Dacă apare vreo eroare, o loghează (nu oprește procesarea celorlalte).
                        }
                    }


                    await Task.Delay(500, stoppingToken);//După ce a terminat de procesat toate evenimentele, așteaptă 500ms și apoi reia procesarea
                }
            }
            catch (OperationCanceledException) { } //Prinde excepția care apare când aplicația este oprită, pentru a opri frumos serviciul fără erori suplimentare.

        }

    }
}
