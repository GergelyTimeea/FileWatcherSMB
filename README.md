# FileWatcherSMB

A complete .NET project that monitors files in a shared SMB folder in real-time and sends detected events to a RabbitMQ queue using an efficient asynchronous system.

## Introduction

FileWatcherSMB is an application that automates the process of tracking files in an SMB directory shared by an Ubuntu virtual machine, mounted on a Windows system. This project aims to create a real-time logging and processing system for file events (create, modify, rename), using modern technologies such as:

- SMB for file sharing between machines
- FileSystemWatcher from .NET for local monitoring
- RabbitMQ for messaging and decoupling the processing flow
- Docker for quick and isolated RabbitMQ deployment

## SMB Connection and File Monitoring

### Technical Context

SMB (Server Message Block) is a network protocol for file sharing between different devices. In this project, the Ubuntu virtual machine acts as an SMB server, and the host Windows system mounts that shared folder as a local drive.

### SMB Server – Configuration on Ubuntu

1. On the Ubuntu server (VM), we installed and configured Samba:
```
sudo apt update
sudo apt install samba
```
Explanation:

- ```apt update``` updates the package list.
- ```apt install samba``` installs Samba — the software that allows file sharing between Linux and Windows.

2. Creating the folder to be shared
```
mkdir -p /home/user_name/smbshare
```
Explanation:

- You create the folder you want to share.
- ```-p``` also creates parent directories if they don’t exist (```home/user_name``` in this case).
```
sudo chown timi:timi /home/user_name/smbshare
```
- This ensures that the ```user_name``` has write permissions to the shared folder.

3. Adding the configuration to the ```smb.conf``` file
```
sudo nano /etc/samba/smb.conf
```
Add at the end of the file:
```
[smbshare]
   path = /home/user_name/smbshare
   browseable = yes
   read only = no
   guest ok = no
   valid users = user_name
```
Explanation:

- ```[smbshare]``` – the name that will appear in the network.
- ```path``` – the actual folder location.
- ```browseable``` = yes – allows it to be visible on the network.
- ```read only``` = no – grants write permission.
- ```guest ok``` = no – disallows anonymous access.
- ```valid users``` = user_name – only the ```user_name``` can access it.

4. Creating a Samba user
```
sudo smbpasswd -a user_name
```
Explanation:

- Adds the ```user_name``` to the Samba system and sets a password.
- The user must already exist on the Ubuntu system.

5. Restarting the Samba service
```
sudo systemctl restart smbd
```
Explanation:

- Applies the new changes made to the ```smb.conf``` file.

### Mounting the Share on Windows

On Windows, the folder was mounted using the following steps:

1. **Open File Explorer** → click on **This PC**
2. **Right-click** in the window → **Map network drive**
3. Chose **Z:**
4. Under **Folder**, entered:
```
\\192.168.1.10\smbshare
```
(Replace the IP with your Ubuntu's IP address, and ```smbshare``` with the share name from smb.conf)

5. Checked:

- _ _Reconnect at sign-in_ _ – so it reconnects automatically after restart

6. Click **Finish**
7. Entered the user ```user_name``` and the password set with ```smbpasswd -a user_name```

---

## Main Application File Structure

Below is the main file structure of the project and the purpose of each.

### 1. `Program.cs`

**Purpose:**  
This is the entry point and the main logic of the application.

**Usage:**

- Loads configuration values from `appsettings.json` (such as the directory to monitor).
- Initializes and configures a `FileSystemWatcher` to monitor file changes (create, modify, rename).
- Filters out temporary or irrelevant files to avoid noisy or redundant events.
- Uses a thread-safe, deduplicated event cache (`ConcurrentHashSet`) to store detected changes before processing.
- Runs a background thread that periodically processes pending events, logs them, and sends them to RabbitMQ.
- Handles graceful shutdown and error conditions.

**Note:**  
Any modification to `Program.cs` (such as logic changes or new functionality) requires recompiling the application for changes to take effect.

### 2. `appsettings.json`

**Purpose:**  
Contains configuration values that control the application's behavior.

**Usage:**

- Specifies the path of the folder to monitor (`NfsWatcher:WatchPath` – Samba share) and can include additional settings for RabbitMQ or other parameters.
- Allows configuration values (like the monitored directory) to be changed **without recompiling the application**.

**Note:**  
This is the only file intended for user editing for configuration. All other changes require recompilation.

### 3. `ConcurrentHashSet.cs`

**Purpose:**  
Defines a thread-safe set collection used for deduplicating file paths.

**Usage:**

- Implements a custom set using a concurrent dictionary, ensuring unique storage of file paths.
- Supports concurrent add/remove operations from multiple threads (file watcher and event processor), avoiding race conditions.
- Used by `Program.cs` to cache events before processing.

**Note:**  
If the logic or implementation in `ConcurrentHashSet.cs` is changed, the application must be recompiled for changes to take effect.

---

## File Monitoring with FileSystemWatcher

`FileSystemWatcher` is a .NET class that allows applications to monitor file system changes in real-time. It can watch a specific directory (and optionally its subdirectories) for events such as creation, modification, deletion, and renaming.

### How is it used in this application?

In this project, `FileSystemWatcher` is used for:

- **Monitoring a directory** specified in `appsettings.json`, including subdirectories.
- **Detecting file system events**:
  - **Created:** when a new file or folder is created.
  - **Changed:** when a file is modified.
  - **Deleted:** when a file or folder is deleted.
  - **Renamed:** when a file or folder is renamed.

Upon detecting such an event, the application checks if the file is temporary or irrelevant. If not, the event is added to a queue for later processing (logging, notifications, etc.).

### Limitations

- **Duplicate events:** Multiple events may be triggered for a single change, especially on network shares (SMB).
- **Temporary files:** Many applications create temporary files that trigger irrelevant events.
- **Inaccurate notifications:** Events may not precisely reflect the user's action, especially for network files.
- **SMB/Linux:** When monitoring a Linux server from Windows via SMB:
  - Duplicates or grouped events may occur.
  - Some events may be missing, delayed, or not emitted at all.
  - File operations on Linux do not always map directly to Windows notifications.
- **Lack of atomicity:** Events may be reported before the operation is fully completed (e.g., large file copy).

---

## Event Management Architecture

The application uses a thread-safe system to monitor, cache, and process events. Key components are `ConcurrentHashSet`, `eventMap`, and the `ProcessEvents` thread.

### 1. `ConcurrentHashSet`

**What is it?**  
A custom thread-safe set built on top of `ConcurrentDictionary<string, byte>`.

**Purpose in code:**  

- Stores unique file paths, **deduplicating** rapid or repeated events.
- Allows concurrent access between the main and background threads.

**How it stores data:**

- File paths are stored as dictionary keys.
- The value (`byte`) is unused, always set to 0; only the key matters.
- Supports fast, thread-safe add, remove, and check operations.

### 2. `eventMap`

**What is it?**  
A static/global instance of `ConcurrentHashSet`.

**Purpose in code:**  

- Acts as the **cache** for all unique pending events.
- Receives paths from event handlers.
- Ensures the same file is not processed more than once per batch.

**How it stores data:**  

- Keeps file paths until processed.
- Once processed, the path is removed to allow future changes to reappear.

### 3. `ProcessEvents` (Background Thread)

**What is it?**  
A dedicated thread that processes all events in `eventMap` periodically (every 500ms).

**Purpose in code:**

- **Processes in batch** all unique events.
- **Removes** each path after processing to avoid duplication.
- Logs the event and sends it to an external system (RabbitMQ).

**How it works:**

- Takes a snapshot of all paths from `eventMap`.
- For each path:
  - Removes it.
  - Processes/logs the event.
- Waits a set interval, then repeats the cycle.

**Why 500ms?**

- The 500ms interval is chosen to handle the noisy behavior of `FileSystemWatcher`, especially on SMB networks between Linux and Windows.
- Allows combining multiple events into a single entry (deduplication).
- It's a trade-off between real-time response and efficiency.

### 4. Thread Usage

- **Main thread** detects events and adds paths to `eventMap`.
- **Background thread** (`ProcessEvents`) processes them.
- No manual locking needed; `ConcurrentHashSet` ensures thread safety.

### 6. Event Deduplication

- **Deduplication** is guaranteed by the nature of the `ConcurrentHashSet`.
- If a file is modified multiple times quickly, only one entry is processed.
- Once processed and removed, a new change will be accepted again.

| Structure           | Type                                | Purpose                                             | Key Feature               |
|---------------------|--------------------------------------|-----------------------------------------------------|---------------------------|
| ConcurrentHashSet   | Thread-safe set (dictionary-based)   | Stores unique paths for pending files               | Deduplication             |
| eventMap            | Static instance of ConcurrentHashSet | Global store for pending events                     | Thread-safe, uniqueness   |
| ProcessEvents()     | Background thread                    | Processes and removes events, sends to RabbitMQ     | Periodic batch processing |

---

## RabbitMQ & Docker Configuration

### About RabbitMQ

RabbitMQ is an open-source message broker that implements the AMQP protocol and decouples producers from consumers using queues. It ensures secure and ordered message delivery between independent parts of a system.

### About Docker

Docker is an open-source platform that enables applications to run in isolated, consistent, and portable environments:

- **Image**: the complete definition of the runtime environment, built from a `Dockerfile`
- **Container**: a running instance of an image
- **Docker Compose**: orchestrates multiple containers from a YAML file

### Implementation in .NET

#### The `RabbitMqProducer` Class

`RabbitMqProducer` handles **asynchronous** message sending to the configured RabbitMQ queue. When `SendMessageAsync(string message)` is called:

1. It asynchronously opens a connection and channel (`CreateConnectionAsync()` and `CreateChannelAsync()`).
2. Declares the queue with `QueueDeclareAsync()`.
3. Converts the message to UTF-8 and publishes it via `BasicPublishAsync()` to avoid blocking the main execution with network operations.
4. The method returns a `Task` that completes when the broker confirms message receipt, and a log line appears in the console.

#### Dockerfile

The `Dockerfile` uses a two-stage build process to optimize image size and speed:

1. **Build stage**
   - Starts from `mcr.microsoft.com/dotnet/sdk:9.0`, installs dependencies with `dotnet restore`, and publishes the app to `/app/publish`.
   - This stage includes the full SDK needed for compilation but is not included in the final image.

2. **Runtime stage**
   - Starts from `mcr.microsoft.com/dotnet/aspnet:9.0`, a smaller image that includes only the .NET runtime.
   - Copies the published output to `/app` and sets `ENTRYPOINT` to run `FileWatcherSMB.dll`.

#### docker-compose.yml

The `docker-compose.yml` file orchestrates the two main services and the necessary volume:

- **Services**
  - **rabbitmq**: runs the broker with management UI, exposes ports `5672` (AMQP) and `15672` (web UI), and stores data in a persistent volume (`rabbitmq_data`).
  - **watcher**: builds the .NET container from `Dockerfile`, waits for RabbitMQ (`depends_on`), receives watch and connection configuration through environment variables, and mounts the local folder read-only for monitoring.

- **Volumes** – defines `rabbitmq_data` to persist RabbitMQ messages across container restarts.

### Usage

1. Open Docker Desktop  
2. In a terminal, navigate to the project directory (where `docker-compose.yml` is located) and run:
```
docker-compose up --build
```
3. Access the RabbitMQ Management UI in your browser at `http://localhost:15672`  
4. Log in with username and password: `admin/admin`  
5. To stop, press `Ctrl+C` in the terminal where docker-compose was started, then to clean up containers and volume, run:
```
docker-compose down --volumes
```
