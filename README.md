## Application Main File Structure

This application is a SMB FileSystemWatcher with event caching and temporary file filtering, designed to monitor file changes in a specified directory and send notifications to RabbitMQ. Below, we outline the main files of the project and their purposes.

---

### 1. `Program.cs`

**Purpose:**  
This is the entry point and core logic of the application.

**Responsibilities:**
- Loads configuration values from `appsettings.json` (such as the directory to watch).
- Initializes and configures a `FileSystemWatcher` to monitor for file changes (create, modify, rename).
- Filters out temporary or irrelevant files to avoid noisy or redundant events.
- Uses a thread-safe, deduplicating event cache (`ConcurrentHashSet`) to store detected file changes before processing.
- Spawns a background thread that periodically processes all pending events, logs them, and forwards them to RabbitMQ.
- Handles graceful shutdown and error conditions.

**Note:**  
Any modifications to `Program.cs` (such as logic changes or new features) require rebuilding the application for changes to take effect.

---

### 2. `appsettings.json`

**Purpose:**  
Holds configuration values that control application behavior.

**Responsibilities:**
- Specifies the folder path to be watched (`NfsWatcher:WatchPath` (Samba File share) ) and may hold additional settings for RabbitMQ or other parameters.
- Allows you to change configuration values (such as the directory being watched) **without recompiling or rebuilding** the application. 

**Note:**  
This is the only file designed to be user-edited for configuration. All other code changes require a rebuild.

---

### 3. `ConcurrentHashSet.cs`

**Purpose:**  
Defines a thread-safe set collection used for deduplicating file event paths.

**Responsibilities:**
- Implements a custom set using a concurrent dictionary, ensuring that only unique file paths are stored.
- Supports concurrent additions and removals from multiple threads (the file watcher and the event processor), avoiding race conditions.
- Used by `Program.cs` to cache file events until they are processed.

**Note:**  
If you change the logic or implementation in `ConcurrentHashSet.cs`, a rebuild of the application is required for changes to take effect.


---

## File Monitoring with FileSystemWatcher

### What is `FileSystemWatcher`?

`FileSystemWatcher` is a class provided by the .NET framework that allows applications to monitor changes in the file system in real time. It can watch a specific directory (and optionally its subdirectories) for changes such as file creation, modification, deletion, and renaming. When one of these events occurs, the `FileSystemWatcher` raises an event which your application can handle.

### What is it used for in this application?

In this project, `FileSystemWatcher` is used to:

- **Monitor a directory** (specified in `appsettings.json`) and all its subdirectories.
- **Catch file system events**:
  - **Created:** when a new file or directory appears.
  - **Changed:** when a file is modified.
  - **Deleted:** when a file or directory is removed.
  - **Renamed:** when a file or directory is renamed.

Whenever such an event is detected, the application checks if the file is a temporary or irrelevant file (to avoid noise), and if not, it adds the event to a processing queue for further handling (such as logging or sending notifications).

### Why is `FileSystemWatcher` important in .NET?

- **Native Integration:** It is built into .NET and works seamlessly with Windows file systems.
- **Real-Time Monitoring:** Provides immediate notification of file changes, making it ideal for synchronization, backup, auditing, and automated processing tasks.
- **Ease of Use:** Offers a declarative, event-driven model, so developers can react to file system changes with minimal code.

### Limitations and Caveats (especially with SMB and Linux)

- **Duplicate Events:** `FileSystemWatcher` can sometimes raise multiple events for a single change, especially when monitoring network shares or SMB mounts.
- **Temporary Files:** Many applications (such as editors or file managers) create temporary files during normal operation. These can trigger a flood of events that are not meaningful for most use cases.
- **Imprecise Notifications:** The events raised may not always map exactly to the actual user-initiated changes, especially on networked file systems. For example, a file save might trigger a sequence of create, rename, and change events, not just a single modification.
- **SMB/Network Shares with Linux:** When watching a Linux server from a Windows machine using SMB:
  - Events may be duplicated or batched.
  - Some events may be dropped, delayed, or not raised at all.
  - File operations from Linux may not have 1:1 mapping to Windows notifications, due to differences in filesystem semantics.
- **No Guarantee of Atomicity:** Changes to files or directories may be reported before the operation is fully complete (e.g. during a large file copy).

### FileSystemWatcher: Event Detection

| Event Type  | Handled in App? | Description                                  |
|-------------|-----------------|----------------------------------------------|
| Created     | Yes             | A file or directory is created.              |
| Changed     | Yes             | A file’s content or attributes are modified. |
| Deleted     | Yes             | A file or directory is deleted.              |
| Renamed     | Yes             | A file or directory is renamed.              |
---

## Event Handling Architecture

This application uses a thread-safe system to monitor, cache, and process file system events. The key components of this system are `ConcurrentHashSet`, `eventMap`, and the `ProcessEvents` background thread.

---

### 1. `ConcurrentHashSet`

**What is it?**  
A custom thread-safe set built on top of `ConcurrentDictionary<string, byte>` from .NET.  
It is designed specifically to hold **unique file paths** that represent file system events.

**Purpose in the Code:**  
- Ensures that each file path is only stored once, **deduplicating** rapid or repeated file events.
- Allows safe concurrent access from multiple threads (the main thread and the background processor).

**How it holds information:**  
- Stores file paths (the full path of changed files) as dictionary keys.  
- The value (`byte`) is meaningless, always set to 0; only the key (file path) matters.
- Supports fast add, remove, and existence checks in a thread-safe way.

---

### 2. `eventMap`

**What is it?**  
A static/global instance of `ConcurrentHashSet`.

**Purpose in the Code:**  
- Acts as a **cache** for all unique file events that are waiting to be processed.
- Receives file paths from event handlers (when a file is created, changed, or renamed).
- Guarantees that the same file does not get processed more than once per batch.

**How it holds information:**  
- Holds file paths as long as they are pending processing.
- When the background thread processes an event, it removes the path from `eventMap` so it can be re-added if another change happens in the future.

---

### 3. `ProcessEvents` (Background Thread)

**What is it?**  
A dedicated background thread that periodically (every 500ms) processes and dispatches all pending events from `eventMap`.

**Purpose in the Code:**  
- **Batch processes** all unique file events collected since the last cycle.
- **Removes** each processed path from the set to avoid duplicate handling.
- Logs the event and sends it to an external system (RabbitMQ in our case).

**How it holds and processes information:**  
- Takes a snapshot of all file paths currently in `eventMap`.
- For each path:
  - Removes it (ensuring future events for the same file are not ignored).
  - Processes/logs the event.
- Sleeps for a set interval, then repeats.

**Why 500ms?**  
- The 500ms interval is specifically chosen to handle the bursty and sometimes unreliable behavior of `.NET`'s `FileSystemWatcher`, especially when monitoring SMB network shares between Linux servers and Windows clients.
- Over SMB, FileSystemWatcher can emit redundant or duplicate events for a single file operation. By waiting 500ms before processing, the app allows multiple rapid-fire events for the same file to be merged into a single entry (deduplication), minimizing unnecessary work and external notifications.
- This interval is a balance: it is short enough for nearly real-time responsiveness, but long enough to effectively batch and deduplicate noisy event streams caused by the limitations of network file systems.

---

### 4. Thread Usage

- The **main thread** handles file system events and adds file paths to `eventMap`.
- The **background thread** (`ProcessEvents`) removes and processes events from `eventMap`.
- No manual locking needed; `ConcurrentHashSet` ensures thread safety.

---

### 5. Cache Memory Usage

- `eventMap` acts as an **in-memory cache** of pending file system events.
- Only paths that have not yet been processed are stored, minimizing memory use.
- As soon as an event is processed, its path is removed from the cache.

---

### 6. Event Deduplication

- **Deduplication** is guaranteed by the set semantics of `ConcurrentHashSet`.
- If a file changes multiple times before being processed, only one entry appears in the cache.
- Once processed and removed, new events for the same file are accepted.

---

| Structure           | Type                               | Purpose                                             | Key Feature            |
|---------------------|------------------------------------|-----------------------------------------------------|------------------------|
| ConcurrentHashSet   | Thread-safe set (dictionary-based) | Holds unique file paths waiting to be processed      | Deduplication          |
| eventMap            | Static instance of ConcurrentHashSet| Global store for pending events                      | Thread-safe, unique    |
| ProcessEvents()     | Background thread                  | Processes and removes events, sends to RabbitMQ      | Periodic batch process |

---


