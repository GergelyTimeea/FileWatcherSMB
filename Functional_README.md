# 📁 Internship Project: FileWatcher for SMB Storage

🕒 **Duration:** 2–3 Months  
🧑‍💻 **Tech Stack:** C#, .NET, RabbitMQ, Docker (optional)

---

## 🎯 Objective

Create a C# application that **monitors an SMB (network) folder** and detects file events in **real time** — such as when a file is created, modified, or renamed.

The application should then **send these events to a RabbitMQ queue**, allowing another system to scan them for viruses or malware.

---

## 🔧 Core Features

### 🗂️ File Monitoring
- ✅ Watch a **valid SMB path**
- ✅ **Recursively** monitor all subfolders
- ✅ Detect file events:
  - 📥 **Created**
  - 📝 **Changed/Updated**
  - 🔁 **Renamed**
  - ❌ **Deleted** *(log only — not forwarded)*

### 🐛 Error Handling & Stability
- ✅ Handle invalid paths or network issues gracefully
- ✅ Catch runtime errors (e.g. file locks, timeouts)
- ✅ Log meaningful error messages
- ✅ Exit cleanly (e.g. Ctrl+C, app window closed)
- ✅ Recover automatically from temporary failures

### 📡 Async Event Reporting
- ✅ Send file events to a **RabbitMQ queue**
- ✅ Events must be processed and sent **non-blocking**
- ✅ RabbitMQ settings must be **configurable**

### ⚙️ Configurability
- ✅ Use `appsettings.json` (no hardcoding!)
- ✅ Configurable values include:
  - SMB path
  - RabbitMQ server, credentials, and queue
  - Logging settings
  - Event filtering rules (e.g. ignore temp files)

---

## ⚠️ Known Issues & Design Considerations

### 🧠 FileSystemWatcher Limitations
- `FileSystemWatcher` has a **limited internal buffer**
- Events must be **handled quickly** or they’ll be dropped

### 🔁 Duplicate / Noisy Events
- Rename/Move may trigger multiple events: *(delete + create)*
- Temporary files (e.g. `~$file.docx`) should be **filtered out**
- Filtering logic must be:
  - ✅ **Smart**
  - ✅ **Configurable**

---

## 🌟 Optional Features (Bonus Points!)
- ✅ Unit tests for logic and filtering
- ✅ Docker support (Linux environment)
- ✅ Docker Compose setup:
  - Your app
  - RabbitMQ server
  - RabbitMQ **Web UI**

---

## 🧪 Test Criteria

### 📦 Bulk File Drop Test
A folder with **1,000 files** will be copied into the monitored SMB share.

The application must:
- 🔍 Detect all **create** events
- 📬 Send messages to RabbitMQ in **real time**
- 📘 Log all events and errors clearly
- 🧘 Stay stable — no crashes, no event loss

### ✅ What We'll Check
- RabbitMQ Web UI shows incoming messages
- Events are accurate (no duplicates or noise)
- Temporary files are ignored (if configured)
- Logs are helpful and readable
- App handles burst load smoothly

---

## 📚 Learning Outcomes

By completing this project, you’ll gain experience in:

- 🔧 Real-time file monitoring using `System.IO.FileSystemWatcher`
- 🧑‍💻 Developing stable .NET services
- ⚙️ Writing configurable applications (`appsettings.json`)
- 📡 Messaging and async processing with RabbitMQ
- 🐳 Dockerizing and deploying .NET apps (optional)
- 🧪 Writing clean logs and handling unexpected errors



---
This part of the document summarizes how the project covers the requirements and how we implemented our solution:
# 🗂️ File Monitoring Use Cases 
## 1. ✅ Watch a **valid SMB path**

> SMB (Server Message Block) is a network protocol for file sharing between different devices. In this project, the Ubuntu virtual machine acts as an SMB server, and the host Windows system mounts that shared folder as a local drive.

> The watched folder path (WatchPath), ignored file patterns (IgnorePatterns), and RabbitMQ connection details are all defined in `appsettings.json` and can be updated without recompiling.

**Program setup (`Program.cs`):**
- At startup, the application reads the path to watch from configuration, which can be a mounted SMB share.

## 2. ✅ **Recursively** monitor all subfolders

**FileWatcherWrapper.cs:**
```csharp
_watcher = new FileSystemWatcher(pathToWatch)
{
    ...
    IncludeSubdirectories = true, // monitors all subfolders
    ...
};
```

> Watches a specified directory (and its subdirectories).

## 3. ✅ Detect file events

Only meaningful events are forwarded (created, changed, renamed). Deleted files are logged but not forwarded to RabbitMQ. 

### 📥 **Created**
- **FileWatcherWrapper.cs:**
  ```csharp
  _watcher.Created += OnFileCreated;
  private void OnFileCreated(object sender, FileSystemEventArgs e)
  {
      if (_filter.IsTemporaryOrIgnoredFile(e.FullPath)) return;
      _eventMap.Add(e.FullPath);
  }
  ```
- **README.md:**  
  > Each of these triggers a method that checks if the file is temporary or ignored (using our filter). If not, the full file path is added to the deduplication set (`IConcurrentHashSet`). This ensures only real, unique, user-relevant events move forward in the pipeline.

---

### 📝 **Changed/Updated**
- **FileWatcherWrapper.cs:**
  ```csharp
  _watcher.Changed += OnFileChanged;
  private void OnFileChanged(object sender, FileSystemEventArgs e)
  {
      if (_filter.IsTemporaryOrIgnoredFile(e.FullPath)) return;
      _eventMap.Add(e.FullPath);
  }
  ```

---

### 🔁 **Renamed**
- **FileWatcherWrapper.cs:**
  ```csharp
  _watcher.Renamed += OnFileRenamed;
  private void OnFileRenamed(object sender, RenamedEventArgs e)
  {
      if (_filter.IsTemporaryOrIgnoredFile(e.OldFullPath) || _filter.IsTemporaryOrIgnoredFile(e.FullPath)) return;
      _eventMap.Add(e.FullPath);
  }
  ```

---

### ❌ **Deleted** *(log only — not forwarded)*
- **FileWatcherWrapper.cs:**  
  There is no explicit forwarding/processing for deleted files — only logging or ignoring.
  
---
 # 🐛 Error Handling & Stability 

## 4. ✅ Handle invalid paths or network issues gracefully

> - The application validates the watch path at startup. If the path is invalid or not accessible (e.g. due to network/Samba issues), the watcher will not start and a clear error is logged.

**Program.cs:**
> - `IFileWatcher`: Watches the target directory for changes. **Configuration is validated here to fail fast on bad input.**

---

## 5. ✅ Catch runtime errors (e.g. file locks, timeouts)

**FileWatcherWrapper.cs:**
```csharp
private void OnFileError(object sender, ErrorEventArgs e)
{
    _logger.LogError(e.GetException(), "Eroare la monitorizarea fișierului.");
}
```
- All unexpected errors from the FileSystemWatcher (such as file locks, network glitches, buffer overflows, etc.) are caught and logged using this event handler.

---
## 6. ✅ Log meaningful error messages

**Logging Configuration:**
> - Sets up console logging.
> - Filters out noisy logs from Microsoft internals for clarity.

**FileWatcherWrapper.cs:**
```csharp
_logger.LogError(e.GetException(), "Eroare la monitorizarea fișierului.");
```
- All errors are logged with an explicit and meaningful message including the exception details.

**RabbitMqProducer.cs:**
> - Logs are written on connection or message failures to help diagnose messaging issues.

---
## 7. ✅ Exit cleanly (e.g. Ctrl+C, app window closed)

**Program.cs — Service Initialization and Start:**
> - On shutdown, stops the watcher and logs completion.
> - The .NET generic host (`IHost`) manages application lifetime, responding to Ctrl+C, SIGTERM, and window close events, ensuring all background services and watchers are stopped gracefully.

**FileWatcherWrapper.cs:**
```csharp
public void Dispose()
{
    _watcher.Dispose();
}
```
- Implements `IDisposable` to ensure the watcher is properly cleaned up on shutdown.

---
# 📡 Async Event Reporting 
## 8. ✅ Recover automatically from temporary failures

**FileEventProcessor — Background Processing:**
> - The processor listens for cancellation requests (`stoppingToken`) so it can stop gracefully when the application is shutting down.
> - If the watcher encounters a recoverable error (e.g. buffer overflow or network stall), the error is logged and the watcher continues running, ready to process future events.

**Resilience Design:**
- The app does not crash on transient errors — error handlers log the issue but allow the service to continue, enabling automatic recovery from temporary network or file system issues.

---
## 9. ✅ Send file events to a **RabbitMQ queue**

The app implements the logic for monitoring file system changes, filtering temporary files, batching events, and **sending notifications through RabbitMQ**.

**Producer Abstraction:**  
 > - `IRabbitMqProducer.cs` defines the interface for publishing messages to RabbitMQ.  
 > - `RabbitMqProducer.cs` is the implementation. It takes a `RabbitMqOptions` instance (via dependency injection), establishes the connection, and exposes methods for sending messages to the configured queue.

**FileEventProcessor.cs:**
>- Collects and batches file events.
>- For each unique file, calls `SendMessageAsync` on the RabbitMQ producer (`IRabbitMqProducer`), which sends the event to the RabbitMQ queue.

---
## 10. ✅ Events must be processed and sent **non-blocking**

 **Async Message Sending:**  

  Messages are sent to RabbitMQ using an async method. This means the operation does not block the application while waiting for RabbitMQ to confirm the message, making the system more responsive and scalable, especially when many messages are sent.

 **Event Processing and Batching:**  
  >- `FileEventProcessor` is the background service responsible for collecting, deduplicating, batching, and processing file events detected by the watcher.  
 > - Runs continuously in its own background thread using `BackgroundService` and async logic (`ExecuteAsync`) to avoid blocking the main thread.  
  >- For each batch (every 500 milliseconds), it loops through unique paths and sends messages asynchronously to RabbitMQ.

**RabbitMqProducer.cs (from code):**
```csharp
public async Task SendMessageAsync(string message)
{
    // ... (connection and channel setup)
    await channel.QueueDeclareAsync(...);
    await channel.BasicPublishAsync(...);
    // ...
}
```
- The use of `async`/`await` ensures non-blocking sending of events.

---
## 11. ✅ RabbitMQ settings must be **configurable**

 **Configuration Model: `Models/RabbitMqOptions.cs`:**  

  This class defines how RabbitMQ connection settings are represented in code.  
  It includes properties for `HostName`, `UserName`, `Password`, and `QueueName`, which are mapped from `appsettings.json` or environment variables at startup.

  **appsettings.json**  
 > - Holds user-editable configuration values.  
 > - Defines the watched folder path (WatchPath), ignored file patterns (IgnorePatterns), and **RabbitMQ connection details**.

> - **Design keeps configuration clean and separate from logic, making it easy to manage and modify connection settings.**

**Program.cs:**
>- At startup, RabbitMQ settings are loaded from configuration and injected into services using dependency injection.

---
# ⚙️ Configurability 

## 12. ✅ Use `appsettings.json` (no hardcoding used)

  **Project File:** `appsettings.json`
>   - Holds user-editable configuration values.
>   - Defines the watched folder path (WatchPath), ignored file patterns (IgnorePatterns), and RabbitMQ connection details.
>   - Lets you update settings without recompiling the application.



**Configurable values include:**

> - **SMB path**
>   - The watched folder path (WatchPath) is defined in `appsettings.json`.

> - **RabbitMQ server, credentials, and queue**
>   - **Configuration Model: `Models/RabbitMqOptions.cs`**
>     - This class defines how RabbitMQ connection settings are represented in code.
>      - Properties for `HostName`, `UserName`, `Password`, and `QueueName` are mapped from `appsettings.json` or environment variables at startup.
>   - **appsettings.json**
>      - Defines RabbitMQ connection details.

> - **Event filtering rules**
>   - **Temporary File Filtering**
>     - Patterns are listed in the configuration (for example, `NfsWatcher:IgnorePatterns` in `appsettings.json`).
>     - Example patterns: `*.tmp`, `*.swp`, `~$*`, etc.
>     - The filter reads this list at startup, so you can add or remove patterns without changing the code.

**Startup logic: `Program.cs`:**
> - **Configuration Loading**
>   - Reads `appsettings.json` for customizable settings (e.g., watched path, ignore patterns, RabbitMQ credentials).
>   - Enables live reload and environment variable overrides.
---
# ⚠️ Known Issues & Design Considerations

## 13. 🧠 FileSystemWatcher Limitations

- `FileSystemWatcher` has a **limited internal buffer**.  

 >    -  If too many file events occur in a short period and are not handled quickly, the buffer can overflow and events will be lost.

 - Sometimes, the events received do not perfectly match the user's real actions (for example, when a file is edited, multiple create/change/rename events may occur).
 - This is why *monitoring folders on a Linux server from Windows via SMB can result in duplicate or grouped events*. 

 **⚙️ Our Solution:**  

 All events from `FileSystemWatcher` are immediately added to a **thread-safe**, **deduplicating set** as soon as they arrive.
  >- For each batch (every 500 milliseconds), it loops through all unique paths: Tries to remove the path from the set (to prevent double-processing). If removal is successful, sends a message to RabbitMQ using the producer (`SendMessageAsync`).  
  > - This ensures only files that are ready to be processed are sent out, and no duplicates are sent.  
  >- The processor waits (`Task.Delay(500, stoppingToken)`) for 500 milliseconds between each batch, balancing responsiveness with efficiency.

---
## 14. 🔁 Duplicate / Noisy Events

- **Rename/Move may trigger multiple events:**  
  For example, a rename or move can trigger both a delete and a create event, or even multiple creates/changes, especially on network shares or SMB/NFS mounts.

- **Temporary files (e.g. `~$file.docx`) should be filtered out:**  
  Many applications create temporary files during save/copy operations; these can generate noise and unnecessary events.


  ### ⚙️ Our Solution:
    > - Patterns are listed in the configuration (for example, `NfsWatcher:IgnorePatterns` in `appsettings.json`).  
    > - Example patterns: `*.tmp`, `*.swp`, `~$*`, etc.  
    > - The filter reads this list at startup, so you can add or remove patterns without changing the code.

  ### 🛠️ Architectural Code Design: ConcurrentHashSet
  A custom, thread-safe set implemented on top of `ConcurrentDictionary`. This data structure is critical to both performance and correctness:
    - **Deduplication:** Ensures each file path is only processed once per batch, even if multiple events occur quickly for the same file.
    - **Thread-safety:** Safely supports concurrent access from both the file watcher (main thread) and the event processor (background thread).
    - **Scalability:** Handles high event throughput without race conditions or data loss.

    >  - **`IConcurrentHashSet`** is the interface.  
    >   It defines the basic operations needed for a set:  
    >   `Add(item)`, `Remove(item)`, `Contains(item)`.
    >
    > - **`ConcurrentHashSet`** is the implementation.  
    >   Internally, it uses a `ConcurrentDictionary<string, byte>` to store items in a thread-safe way, where the key is the path of a file.

  ### 🔗 How It All Fits Together

   **1. FileSystemWatcher** detects file events (including in subdirectories), immediately adding full file paths to the `ConcurrentHashSet`.

   **2. TempFileFilter** (configurable via `appsettings.json`) filters out irrelevant/temporary file events before they are enqueued.

   **3. FileEventProcessor** periodically (every 500ms) processes all unique paths in the set, ensuring no duplicates, and sends valid events to RabbitMQ for downstream processing.

---
# 🌟 Optional Features (Implemented)

 Below are the extra features implemented in this project that provide added value, flexibility, and ease of deployment
 ## 15. ✅ Unit Tests for Logic and Filtering

 The project includes a dedicated test project using **xUnit** and **Moq** for automated testing.

- Test files and their focus:
  - **TempFileFilterTests.cs**  
   > - Checks that files matching ignore patterns (like `.tmp`, `~$file`) are correctly filtered out.
    >- Verifies that normal, valid files pass through and are not excluded.
  - **FileEventProcessorTests.cs**  
    >- Ensures the event processor batches, deduplicates, and processes file events as expected.
    >- Verifies that only unique, non-temporary file events are sent for further processing (e.g., to RabbitMQ).
  - **ConcurrentHashSetTests.cs**  
    >- Tests the thread-safe behaviour of `ConcurrentHashSet`, guaranteeing correct add/remove/contains operations even under concurrent access.
  - **RabbitMqProducerTests.cs**  
    >- Verifies the producer creates a connection and channel, declares the queue, and publishes a message to RabbitMQ.
    >- Ensures an informational log entry is created when a message is sent. 

  Together, these tests demonstrate the solution’s ability to filter out noise, batch and deduplicate events, and safely manage concurrency.
---
## 16. ✅ Docker Support (Linux Environment)

- The project is ready to run in a **Dockerized Linux environment**.
- A `Dockerfile` is included to build the application image for seamless deployment.

---

## 17. ✅ Docker Compose Setup

- The repository provides a `docker-compose.yml` configuration to spin up the entire system with one command.
- Services included:
  - **The app** (FileWatcherSMB)
  - **RabbitMQ server** (message broker)
  - **RabbitMQ Web UI** (for easy monitoring and management)
    ![RabbitMQ_WebUI](https://github.com/user-attachments/assets/b630fa94-c07d-4580-ad1c-57dee07884e6)


---
