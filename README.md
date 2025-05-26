# FileWatcherSMB

Un proiect .NET complet care monitorizează în timp real fișierele dintr-un folder partajat SMB și transmite evenimentele detectate către o coadă RabbitMQ, utilizând un sistem asincron eficient.

## Introducere

FileWatcherSMB este o aplicație care automatizează procesul de urmărire a fișierelor dintr-un director SMB partajat de o mașină virtuală Ubuntu, montat pe un sistem Windows. Acest proiect urmărește să creeze un sistem de logare și procesare în timp real a evenimentelor de fișier (creare, modificare, redenumire), folosind tehnologii moderne precum:

- SMB pentru partajarea fișierelor între mașini

- FileSystemWatcher din .NET pentru monitorizarea locală

- RabbitMQ pentru mesagerie și decuplarea fluxului de procesare

- Docker pentru rularea rapidă și izolată a RabbitMQ

 ## Conectarea SMB și Monitorizarea Fișierelor

### Context tehnic

SMB (Server Message Block) este un protocol de rețea pentru partajarea fișierelor între diferite dispozitive. În acest proiect, mașina virtuală cu Ubuntu acționează ca server SMB, iar Windows-ul gazdă montează acel folder partajat ca unitate locală.

### Server SMB – configurare pe Ubuntu


1. Pe serverul Ubuntu (VM), am instalat și configurat samba:
```
sudo apt update
sudo apt install samba
```
Explicație:

* ```apt update``` actualizează lista de pachete.

* ```apt install samba``` instalează Samba — software-ul care permite partajarea fișierelor între Linux și Windows.


2. Crearea folderului care va fi partajat

```
mkdir -p /home/user_name/smbshare
```
Explicație:

* Creezi folderul pe care vrei să-l partajezi.

* ```-p``` creează și directoarele părinte dacă nu există (```home/user_name``` în acest caz).
```
sudo chown timi:timi /home/user_name/smbshare
```
* Asta asigură că utilizatorul ```user_name``` are drepturi de scriere în folderul partajat.


 3. Adăugarea configurării în fișierul ```smb.conf```

```
sudo nano /etc/samba/smb.conf
```
Adaugi la finalul fișierului:
```
[smbshare]
   path = /home/user_name/smbshare
   browseable = yes
   read only = no
   guest ok = no
   valid users = user_name
```
Explicație:

* ```[smbshare]``` – numele cu care va apărea folderul în rețea.

* ```path``` – locația reală a folderului.

* ```browseable``` = yes – permite să fie vizibil în rețea.

* ```read only``` = no – oferă permisiune de scriere.

* ```guest ok``` = no – nu permite acces anonim.

* ```valid users``` = user_name – doar utilizatorul ```user_name``` poate accesa.


 4. Crearea unui utilizator Samba

```
sudo smbpasswd -a user_name
```
Explicație:

* Adaugă utilizatorul ```user_name``` în sistemul Samba și setează o parolă.

* Trebuie să existe deja ca utilizator în Ubuntu.


 5. Repornirea serviciului Samba

```
sudo systemctl restart smbd
```
Explicație:

* Aplică noile modificări din fișierul ```smb.conf.```


### Montarea partajării pe Windows

Pe Windows, folderul a fost montat cu următorii pași:
1. **Deschide File Explorer** → click pe **This PC**

2. **Click dreapta** în fereastră → **Map network drive**

3. Am ales **Z:**

4. La **Folder**, am scris:

```
\\192.168.1.10\smbshare
```
(înlocuiește IP-ul cu adresa IP a Ubuntu-ului tău, și ```smbshare``` cu numele partajării din smb.conf)

5. Am bifat :

* _ _Reconnect at sign-in_ _ – ca să se reconecteze automat după restart


6. Click **Finish**

7. Am introdus userul ```user_name``` și parola setată cu ```smbpasswd -a user_name```

---
## Structura Principală a Fișierelor Aplicației

Mai jos este prezentată structura principală a fișierelor proiectului și scopul fiecăruia.

---

### 1. `Program.cs`

**Scop:**  
Acesta este punctul de intrare și logica principală a aplicației.

**Utilizare:**
- Încarcă valorile de configurare din `appsettings.json` (precum directorul care trebuie monitorizat).
- Inițializează și configurează un `FileSystemWatcher` pentru a urmări modificările fișierelor (creare, modificare, redenumire).
- Filtrează fișierele temporare sau irelevante pentru a evita evenimentele zgomotoase sau redundante.
- Utilizează un cache de evenimente thread-safe și deduplicat (`ConcurrentHashSet`) pentru a stoca modificările detectate înainte de procesare.
- Rulează un thread de fundal care procesează periodic evenimentele în așteptare, le înregistrează în loguri și le trimite către RabbitMQ.
- Gestionează oprirea grațioasă și condițiile de eroare.

**Notă:**  
Orice modificare în `Program.cs` (precum schimbări de logică sau funcționalități noi) necesită recompilarea aplicației pentru ca modificările să aibă efect.

---

### 2. `appsettings.json`

**Scop:**  
Conține valorile de configurare care controlează comportamentul aplicației.

**Utilizare:**
- Specifică calea folderului care trebuie monitorizat (`NfsWatcher:WatchPath` – partajare Samba) și poate include setări suplimentare pentru RabbitMQ sau alți parametri.
- Permite schimbarea valorilor de configurare (cum ar fi directorul monitorizat) **fără a recompila aplicația**.

**Notă:**  
Acesta este singurul fișier destinat editării de către utilizator pentru configurare. Toate celelalte modificări necesită recompilare.

---

### 3. `ConcurrentHashSet.cs`

**Scop:**  
Definește o colecție de tip set thread-safe folosită pentru deduplicarea căilor către fișiere.

**Utilizare:**
- Implementează un set personalizat folosind un dicționar concurent, asigurând stocarea unică a căilor fișierelor.
- Suportă adăugarea și eliminarea concurentă din mai multe threaduri (watcher-ul de fișiere și procesatorul de evenimente), evitând condițiile de cursă.
- Este utilizat de `Program.cs` pentru a păstra în cache evenimentele până la procesare.

**Notă:**  
Dacă se modifică logica sau implementarea din `ConcurrentHashSet.cs`, aplicația trebuie recompilată pentru ca schimbările să aibă efect.

---

## Monitorizarea Fișierelor cu FileSystemWatcher


`FileSystemWatcher` este o clasă oferită de .NET care permite aplicațiilor să monitorizeze în timp real modificările din sistemul de fișiere. Poate urmări un director specific (și opțional subdirectoarele) pentru evenimente precum creare, modificare, ștergere și redenumire.

### Cum este folosit în această aplicație?

În acest proiect, `FileSystemWatcher` este folosit pentru:

- **Monitorizarea unui director** specificat în `appsettings.json`, inclusiv subdirectoarele.
- **Detectarea evenimentelor** din sistemul de fișiere:
  - **Created:** când este creat un fișier sau director nou.
  - **Changed:** când un fișier este modificat.
  - **Deleted:** când un fișier sau director este șters.
  - **Renamed:** când un fișier sau director este redenumit.

La detectarea unui astfel de eveniment, aplicația verifică dacă fișierul este temporar sau irelevant. Dacă nu, adaugă evenimentul într-o coadă pentru procesare ulterioară (logare, notificări etc.).


### Limitări 

- **Evenimente duplicate:** Pot apărea mai multe evenimente pentru o singură modificare, mai ales pe partajări de rețea (SMB).
- **Fișiere temporare:** Multe aplicații creează fișiere temporare care declanșează evenimente irelevante.
- **Notificări imprecise:** Evenimentele pot să nu reflecte exact acțiunea utilizatorului, mai ales pe fișiere de rețea.
- **SMB/Linux:** Când se monitorizează un server Linux de pe Windows prin SMB:
  - Pot apărea duplicate sau grupări de evenimente.
  - Unele evenimente pot lipsi, întârzia sau să nu fie emise.
  - Operațiile de fișiere din Linux nu au întotdeauna o corespondență exactă cu notificările Windows.
- **Lipsa de atomicitate:** Evenimentele pot fi raportate înainte ca operația să fie completă (ex: copiere fișier mare).

---

## Arhitectura de Gestionare a Evenimentelor

Aplicația folosește un sistem thread-safe pentru a monitoriza, cache-ui și procesa evenimentele. Componentele cheie sunt `ConcurrentHashSet`, `eventMap` și threadul `ProcessEvents`.

---

### 1. `ConcurrentHashSet`

**Ce este?**  
Un set thread-safe personalizat construit peste `ConcurrentDictionary<string, byte>`.

**Scop în cod:**  
- Stochează căi unice către fișiere, **deduplicând** evenimentele rapide sau repetate.
- Permite acces concurent între threadul principal și cel de fundal.

**Cum stochează informația:**  
- Căile fișierelor sunt stocate ca și chei în dicționar.
- Valoarea (`byte`) este nefolosită, setată mereu la 0; doar cheia contează.
- Suportă adăugare, eliminare și verificare rapidă într-un mod thread-safe.

---

### 2. `eventMap`

**Ce este?**  
O instanță statică/globală de `ConcurrentHashSet`.

**Scop în cod:**  
- Reprezintă **cache-ul** tuturor evenimentelor unice în așteptare.
- Primește căi din handler-ele de evenimente.
- Asigură că același fișier nu este procesat de mai multe ori în aceeași rundă.

**Cum stochează informația:**  
- Ține căile fișierelor până când sunt procesate.
- La procesare, calea este eliminată pentru a permite reapariția dacă apar modificări viitoare.

---

### 3. `ProcessEvents` (Thread de Fundal)

**Ce este?**  
Un thread dedicat care procesează periodic (la fiecare 500ms) toate evenimentele din `eventMap`.

**Scop în cod:**  
- **Procesează în batch** toate evenimentele unice.
- **Elimină** fiecare cale după procesare pentru a evita duplicarea.
- Loghează evenimentul și îl trimite către un sistem extern (RabbitMQ).

**Cum funcționează:**  
- Ia un snapshot cu toate căile din `eventMap`.
- Pentru fiecare cale:
  - O elimină.
  - Procesează/loghează evenimentul.
- Așteaptă un interval, apoi repetă ciclul.

**De ce am 500ms?**  
- Intervalul de 500ms este ales pentru a gestiona comportamentul zgomotos al `FileSystemWatcher`, mai ales pe rețele SMB între Linux și Windows.
- Permite combinarea evenimentelor multiple într-o singură intrare (deduplicare).
- Este un compromis între timp real și eficiență.

---

### 4. Utilizarea Threadurilor

- **Threadul principal** detectează evenimentele și adaugă căi în `eventMap`.
- **Threadul de fundal** (`ProcessEvents`) le procesează.
- Nu este nevoie de locking manual; `ConcurrentHashSet` asigură siguranța concurentă.

---

### 6. Deduplicarea Evenimentelor

- **Deduplicarea** este garantată de natura setului `ConcurrentHashSet`.
- Dacă un fișier este modificat de mai multe ori rapid, doar o singură intrare este procesată.
- După ce este procesată și eliminată, o nouă modificare va fi din nou acceptată.

---

| Structură           | Tip                                 | Scop                                                | Caracteristică cheie     |
|---------------------|--------------------------------------|-----------------------------------------------------|---------------------------|
| ConcurrentHashSet   | Set thread-safe (bazat pe dicționar) | Păstrează căi unice pentru fișiere în așteptare     | Deduplicare               |
| eventMap            | Instanță statică de ConcurrentHashSet| Stocare globală pentru evenimente în așteptare      | Thread-safe, unicitate    |
| ProcessEvents()     | Thread de fundal                     | Procesează și elimină evenimente, trimite la RabbitMQ | Procesare periodică în batch |

---


 ## Configurare RabbitMQ & Docker
 ### Despre RabbitMQ
 RabbitMQ este un broker de mesaje open-source care implementează protocolul AMQP și izolează producătorii de consumatori prin intermediul cozilor. Acesta asigură trimiterea sigură și ordonată a mesajelor între părţi independente ale unui sistem.
 ### Despre Docker
 Docker este o platformă open-source care permite rularea aplicațiilor în medii izolate, consistente și portabile:
 - **Imagine**: definiția completă a mediului de execuție, creată dintr-un `Dockerfile`
 - **Container**: instanță rulantă a imaginii
 - **Docker Compose**: orchestrează mai multe containere dintr-un fișier YAML

### Implementare în .NET

#### Clasa `RabbitMqProducer`
`RabbitMqProducer` gestionează trimiterea **asincronă** a mesajelor către coada RabbitMQ configurată. Când se apelează `SendMessageAsync(string message)`:
1. Se deschide asincron o conexiune și un canal (`CreateConnectionAsync()` și `CreateChannelAsync()`).
2. Se declară coada cu `QueueDeclareAsync()`.
3. Mesajul, convertit în UTF-8, este publicat prin `BasicPublishAsync()`, astfel încât operaţiile de reţea să nu blocheze execuția principală.
4. Metoda returnează un `Task` care se completează când brokerul confirmă primirea mesajului, iar în consolă apare o linie cu textul trimis.

#### Dockerfile
Fișierul `Dockerfile` folosește un build în două etape pentru a optimiza dimensiunea şi viteza imaginii:
1. **Build stage**
   - Pornim de la `mcr.microsoft.com/dotnet/sdk:9.0`, instalăm dependențele cu `dotnet restore` şi publicăm aplicația în folderul `/app/publish`.
   - Această etapă conține tot SDK-ul necesar pentru compilare, dar nu intră în imaginea finală.

2. **Runtime stage**
   - Pornim de la `mcr.microsoft.com/dotnet/aspnet:9.0`, o imagine mai mică care include doar runtime-ul .NET.
   - Copiem în `/app` rezultatul publicării şi setăm `ENTRYPOINT` la rularea `FileWatcherSMB.dll`.

#### docker-compose.yml
Fişierul `docker-compose.yml` orchestrează cele două servicii principale şi volumul necesar:
- **Services**
  - **rabbitmq**: rulează brokerul cu management UI, expune porturile `5672` (AMQP) și `15672` (web UI), şi stochează datele într-un volum persistent (`rabbitmq_data`).
  - **watcher**: construiește containerul .NET din `Dockerfile`, aşteaptă RabbitMQ (`depends_on`), primeşte configuraţia de watch şi conexiune prin variabile de mediu și montează folderul local read-only pentru monitorizare.
- **Volumes** – defineşte `rabbitmq_data` pentru a păstra mesajele RabbitMQ peste restart-uri de containere.

### Utilizare
1. Deschide Docker Desktop
2. Într-un terminal, navighează în directorul proiectului (unde se află `docker-compose.yml`) și execută:
```
docker-compose up --build
```
3. Accesează RabbitMQ Management UI în browser `http://localhost:15672`
4. Autentifică-te cu user și parolă: `admin/admin`
5. Pentru oprire apasă `Ctrl+C` în terminalul unde ai pornit docker-compose și apoi, pentru a curăța complet containerele și volumul, rulează:
```
docker-compose down --volumes
```
