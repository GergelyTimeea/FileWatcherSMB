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

## Code Architecture Overview

This repository contains two main projects:

- **FileWatcherSMB** (code project): Implements the logic for monitoring file system changes, filtering temporary files, batching events, and sending notifications through RabbitMQ.
- **FileWatcherSMB.Tests** (test project): Contains automated unit and integration tests for the main codebase.

---
## Project Structure

### 1. **FileWatcherSMB (Main Code Project)**

- **Project File:** `FileWatcherSMB.csproj`
  - This is the main .NET project file.
  - It defines the application's dependencies and entry point.
- **Project File:** `appsettings.json`
  - Holds user-editable configuration values.
  - Defines the watched folder path (WatchPath), ignored file patterns (IgnorePatterns), and RabbitMQ connection details.
  - Lets you update settings without recompiling the application.


- **src/**  
  Contains all application source code, organized by feature for clarity and maintainability.

    - **Helpers/**
        - `ConcurrentHashSet.cs` and `IConcurrentHashSet.cs`: Implements a thread-safe set for tracking unique file events across threads.
        - `ItempFileFilter.cs` and `TempFileFilter.cs`: Defines and implements logic to filter out temporary/irrelevant files, so only meaningful changes are processed.
    
    - **Models/**
        - `RabbitMqOptions.cs`: Defines configuration options (host, credentials, queue) used for connecting to RabbitMQ.

    - **Processors/**
        - `FileEventProcessor.cs`: Handles the core logic of batching and processing file system events.

    - **Services/**
        - `IRabbitMqProducer.cs` and `RabbitMqProducer.cs`: Interface and implementation for sending messages to RabbitMQ.

    - **Watchers/**
        - `FileWatcherWrapper.cs` and `IFileWatcher.cs`: Provides an abstraction over .NET's file system watcher, making it easier to swap implementations and test.
    
    - **Program.cs**
        - Application entry point. Configures services, loads configuration, and starts the watcher and event processor.

---
## File System Watching


`FileSystemWatcher` is a .NET class that allows applications to monitor file system changes in real-time.  
It can watch a specific directory (and optionally its subdirectories) for events such as file or folder creation, modification, deletion, and renaming.

#### How is it used in our project?

- In our codebase, the logic for file system monitoring is abstracted in the files:
  - `Watchers/IFileWatcher.cs` (interface)
  - `Watchers/FileWatcherWrapper.cs` (implementation)
- We use `FileSystemWatcher` under the hood to receive notifications when files change, but expose a cleaner and more flexible interface to the rest of our application.


#### Limitations of FileSystemWatcher

- **Duplicate events:**  
  Multiple notifications may be triggered for a single file operation, especially on network shares. This can lead to processing the same file more than once unless handled.
- **Temporary files:**  
  Many applications create temporary files during save/copy operations. These files can trigger events that are not relevant to users.
- **Inaccurate notifications:**  
  Sometimes, the events received do not perfectly match the user's real actions (for example, when a file is edited, multiple create/change/rename events may occur).
- **SMB/Linux-specific issues:**  
  - Monitoring folders on a Linux server from Windows via SMB can result in duplicate or grouped events.
  - Some events may be missing, delayed, or not emitted at all.
  - File operations on Linux do not always generate the same notifications as on Windows.

---

## Detailed Breakdown of Program.cs


`Program.cs` is the **entry point** of the application. It defines how the application starts, sets up configuration, logging, and dependency injection, and orchestrates the initialization of all core components.



### Structures Used

- **IHost / Host**:  
  The .NET generic host provides a standard way to bootstrap, configure, and run background services. Here, it is used to manage the application’s lifetime and dependency injection container.
- **Dependency Injection (DI) Container**:  
  Provided via `IServiceCollection`, it allows services and dependencies to be registered and resolved throughout the app.
- **Configuration**:  
  Uses `IConfiguration` to read settings from `appsettings.json` and environment variables.



### Logic and Role

1. **Logging Configuration**  
   - Sets up console logging.
   - Filters out noisy logs from Microsoft internals for clarity.

2. **Configuration Loading**  
   - Reads `appsettings.json` for customizable settings (e.g., watched path, ignore patterns, RabbitMQ credentials).
   - Enables live reload and environment variable overrides.

3. **Dependency Injection Setup**  
   Registers all core services as **singletons** (one instance for the whole app), including:
   - `ITempFileFilter`: Filters out temporary files using patterns from config.
   - `IConcurrentHashSet`: Thread-safe set for holding unique file paths/events.
   - `IFileWatcher`: Watches the target directory for changes. Configuration is validated here to fail fast on bad input.
   - `IRabbitMqProducer`: Responsible for sending messages to RabbitMQ. Reads all connection details from configuration.
   - `FileEventProcessor`: Registered as a hosted background service, this orchestrates batching and processing of file events.

4. **Service Initialization and Start**
   - Resolves and starts the file watcher.
   - Logs when monitoring begins.
   - Starts the host (runs background services, including event processing).
   - On shutdown, stops the watcher and logs completion.

### Design Choices

- **Singletons**:  
  All core services are registered as singletons because they represent long-lived infrastructure (watchers, filters, event stores) that should only exist once in the app’s lifetime. This also avoids resource contention and ensures consistent behavior across threads.
- **Interfaces for Testability**:  
  By using interfaces for all major services, the project is easy to test and mock. This enables the separate test project to inject fakes or mocks as needed.

  ---
## RabbitMQ Integration

#### 1. Configuration Model: `Models/RabbitMqOptions.cs`

This class defines how RabbitMQ connection settings are represented in code.  
It includes properties for `HostName`, `UserName`, `Password`, and `QueueName`, which are mapped from `appsettings.json` or environment variables at startup.

This design keeps configuration clean and separate from logic, making it easy to manage and modify connection settings.



#### 2. Producer Abstraction: `Services/IRabbitMqProducer.cs` & `Services/RabbitMqProducer.cs`

- **`IRabbitMqProducer.cs`** defines the interface for publishing messages to RabbitMQ. This abstraction decouples the rest of the application from the messaging details and allows for easy mocking during testing.
- **`RabbitMqProducer.cs`** is the implementation. It takes a `RabbitMqOptions` instance (via dependency injection), establishes the connection, and exposes methods for sending messages to the configured queue.

By separating configuration, interface, and implementation:
- The application remains modular and testable.
- It is easy to change RabbitMQ settings or swap out the messaging technology without significant code changes.



##### Particularities of `RabbitMqProducer.cs`

- **Async Message Sending:**  
  Messages are sent to RabbitMQ using an async method. This means the operation does not block the application while waiting for RabbitMQ to confirm the message, making the system more responsive and scalable, especially when many messages are sent.

- **QueueDeclareAsync:**  
  Before sending a message, the code calls `QueueDeclareAsync`. This ensures the target queue exists on RabbitMQ. If the queue does not exist, it will be created automatically. This step prevents errors if the queue is missing and guarantees messages have a destination.

- **How They’re Connected:**  
  The async sending and the `QueueDeclareAsync` call happen together in the sending method. First, the queue is declared (or ensured it exists), then the message is published.

- **Message Format:**  
  The message is sent as a UTF-8 encoded string, typically in JSON format. This makes it easy for other applications or services to read and process the message from the queue.
---
## Temporary File Filtering: `Helpers/ITempFileFilter.cs` and `Helpers/TempFileFilter.cs`

#### Importnace of temp file filtering in our app:

When files are edited or copied (especially on network shares like SMB/NFS), many programs and operating systems create temporary files.  
These might have extensions like `.tmp`, `.~`, `.swp`, or start with `~$`.  
These files are used internally and do not represent real user changes, so we do not want to process or send events about them.

Filtering them out:
- **Prevents noise:** Makes sure only important, real file events are processed and sent.
- **Improves performance:** Avoids unnecessary processing and messaging.
- **Reduces errors:** Prevents acting on incomplete or irrelevant files.


#### How does the file filtering work?

- **`ITempFileFilter`** is a simple interface.  
  It defines a method that takes a file path and returns whether the file should be ignored (because it is temporary).  
  This makes the filter easy to swap or mock for testing.

- **`TempFileFilter`** is the class that actually does the filtering.  
  - When the application starts, it loads a list of patterns to ignore from the configuration file (`appsettings.json`).  
  - These patterns describe which files to skip, for example by file extension (`*.tmp`, `*.swp`) or by file name (`~$*`).
  - Every time a file event happens, `TempFileFilter` checks the file’s name against these patterns.
  - If the file name matches one of the patterns, the event is ignored and no further action is taken for that file.


#### How are patterns extracted and used?

- Patterns are listed in the configuration (for example, `NfsWatcher:IgnorePatterns` in `appsettings.json`).
- Example patterns: `*.tmp`, `*.swp`, `~$*`, etc.
- The filter reads this list at startup, so you can add or remove patterns without changing the code.



#### How does this connect to the rest of the app?

- The application always works with the `ITempFileFilter` interface, not the concrete `TempFileFilter` class.
- At runtime, the actual `TempFileFilter` is injected wherever needed.
- This means it’s easy to change how filtering works later (or replace it for tests), and keeps the code clean and maintainable.
---
## Event Deduplication Structures Used: `Helpers/IConcurrentHashSet.cs` and `Helpers/ConcurrentHashSet.cs`

####

This part of the code provides a **thread-safe set**—a data structure that safely stores unique items even when accessed by multiple threads at the same time.  
In this app, it is used to keep track of which file events need to be processed, making sure each file is only handled once per batch.

#### How does it work?

- **`IConcurrentHashSet`** is the interface.  
  It defines the basic operations needed for a set:
  - `Add(item)`: Adds an item if it's not already there.
  - `Remove(item)`: Removes an item from the set.
  - `Contains(item)`: Checks if the set has the item.
  - `Items`: Returns all items currently in the set.

- **`ConcurrentHashSet`** is the implementation.  
  - Internally, it uses a `ConcurrentDictionary<string, byte>` to store items in a thread-safe way.
  - Each file path is used as a key; the value (a byte) is not important.
  - This structure allows fast and safe adding, checking, and removing of items, even when multiple threads interact with it.

#### The exact role played in the app:

- It ensures that file events are **deduplicated**: if the same file changes multiple times quickly, only one event is kept until it is processed.
- It is safe to use from different threads, so the main file watcher and the background processor can both access it without problems.

---
## File Watcher Abstraction: `Watchers/IFileWatcher.cs` and `Watchers/FileWatcherWrapper.cs`



`FileSystemWatcher` is a powerful but sometimes complex .NET class. To make it easier to use, test, and extend, we created:
- **`IFileWatcher`** — a simple interface for starting and stopping file monitoring.
- **`FileWatcherWrapper`** — a class that wraps `FileSystemWatcher` and handles extra logic needed by our application.

This wrapper allows us to:
- Abstract away low-level details from the rest of the code.
- Easily swap the watcher for testing or future improvements.
- Centralize event handling, filtering, and error logging.

---

#### `FileWatcherWrapper` components:

- **Constructor**: Sets up the real `FileSystemWatcher` with all the options we need.
    - Watches a specified directory (and its subdirectories).
    - Uses a broad filter (`*.*`) to capture all file types.

- **NotifyFilters Used**:
    - `Attributes`, `CreationTime`, `DirectoryName`, `FileName`, `LastAccess`, `LastWrite`, `Security`, and `Size`                            
        These cover all meaningful changes: file content, renames, permission changes, creation, and more. This ensures no important event is missed.

- **Event Handlers**:
    - `Changed`, `Created`, `Renamed`:  
      Each of these triggers a method that checks if the file is temporary or ignored (using our filter). If not, the full file path is added to the deduplication set (`IConcurrentHashSet`). This ensures only real, unique, user-relevant events move forward in the pipeline.
    - `Error`:  
      Catches and logs any errors that happen during monitoring.

- **Start/Stop Methods**:
    - `Start()`: Turns on event monitoring.
    - `Stop()`: Turns off event monitoring.

- **Disposal**:
    - Implements `IDisposable` to clean up the underlying watcher when done.

---

#### Interface: `IFileWatcher` components:

- **Start() / Stop()**:  
  These two simple methods allow the rest of the application to control the watcher without knowing any details about how file monitoring is implemented.



---
### Event Processing and Batching: `Processors/FileEventProcessor.cs`



`FileEventProcessor` is the background service responsible for collecting, deduplicating, batching, and processing file events detected by the watcher. It connects the watcher, the deduplication set, and the RabbitMQ producer, making sure only unique and relevant events are sent out efficiently.



#### How does it work?

- **Structures Used:**
  - Uses `IConcurrentHashSet` to store unique file paths that need processing (deduplication).
  - Uses `IRabbitMqProducer` to send processed events to RabbitMQ.

- **Async and Background Execution:**
  - Inherits from `BackgroundService`, so it runs continuously in its own background thread.
  - The main logic is inside `ExecuteAsync`, which is an asynchronous method.
  - Async is chosen to avoid blocking the main application thread, allowing efficient, responsive, and scalable event processing.

- **Event Collection and Deduplication:**
  - The watcher (`FileWatcherWrapper`) adds paths of changed files to the `IConcurrentHashSet` as events occur.
  - The processor periodically reads all current paths from the set (`_eventMap.Items.ToList()`), ensuring each file is only processed once, even if multiple events occurred quickly.

- **Batch Processing:**
  - For each batch (every 500 milliseconds), it loops through all unique paths:
    - Tries to remove the path from the set (to prevent double-processing).
    - If removal is successful, sends a message to RabbitMQ using the producer (`SendMessageAsync`).
    - This ensures only files that are ready to be processed are sent out, and no duplicates are sent.

- **Task Delay of 500ms:**
  - The processor waits (`Task.Delay(500, stoppingToken)`) for 500 milliseconds between each batch.
  - This short delay balances responsiveness (near real-time processing) with efficiency (allows quick bursts to be batched together and avoids CPU overuse).
  - 500ms is a good compromise: fast enough for most scenarios, but not too frequent to cause performance issues.

- **Cancellation and Shutdown:**
  - The processor listens for cancellation requests (`stoppingToken`) so it can stop gracefully when the application is shutting down.



---
## How Everything Works Together —  Summary:

1. **App Initialization**
   - The app starts and sets up all required services.
   - Configuration settings are loaded (like watch path, RabbitMQ details, etc).

2. **File Watching**
   - A file watcher monitors a specific folder for file system changes (create, modify, rename).
   - Temporary or irrelevant files (e.g., `.tmp`, `~$`) are ignored using pattern filters.

3. **Event Filtering**
   - When a valid file change is detected, the file path is stored in a set.
   - Duplicate events for the same file are automatically skipped.

4. **Background Processing**
   - A background processor checks the set of changed files.
   - For each valid file, it sends a message to RabbitMQ.

5. **Message Delivery**
   - RabbitMQ receives the file change messages.
   - Other systems or services can consume these messages for further processing.

> All components work together to ensure only meaningful file changes are captured and sent — efficiently, reliably, and without noise.

---

### 2. **FileWatcherSMB.Tests (Test Project)**

- Contains unit and integration tests for all the main components.
- Ensures code correctness, reliability, and helps prevent regressions.

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
