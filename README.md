# SingleInstanceProgram

A helper class to ensure your program runs as a **single instance**.  
Additional instances send their arguments to the first instance via IPC.  
The first instance can optionally respond back to the secondary instances.
Note: A thread is spawned for IPC.

---
## Usage

### Installation Instructions

#### Using .NET CLI

```sh
dotnet add package singleinstanceprogram
```

#### Using the Package Manager Console (Visual Studio)

```powershell
Install-Package SingleInstanceProgram
```

#### Using csproj

Add the following to your project file:

```xml
<ItemGroup>
  <PackageReference Include="SingleInstanceProgram" />
</ItemGroup>
```

### Quick Start

```csharp
using SingleInstanceProgramNS;

// Create instance (uniqueId = app identifier, args = arguments secondary launches should send to the first instance)
SingleInstanceProgram s = SingleInstanceProgram.Create("UniqueId", args);

// Subscribe to events

//This event will be ran by secondary instances when the first instance responds
s.MessageReceivedFromFirstInstance += (sender, e) =>
{
    if (e.Message != null)
        Console.WriteLine("From first instance: " + string.Join(" ", e.Message));
};

//This event will be ran by the first instance when it receives data from secondary instances
s.MessageReceivedFromOtherInstance += (sender, e) =>
{
    if (e.Message != null)
        Console.WriteLine("From other instance: " + string.Join(" ", e.Message));

    // Respond back to sender
    e.RespondToOtherSender?.Invoke(["Hello from first instance!"]);
};

// Start background IPC thread
s.Start(); //This line is where secondary instances send their data to first then quit, following code will only be executed by the first instance.(your main program)

// Process this instance’s args manually if you wish to (first instance only)
foreach (var arg in args)
    Console.WriteLine(arg);
```

After completing the steps above, your program should be running as a single instance.
Additional launches will pass their `args` to the first instance.

---

### In detail explanation

#### Events

| Event | Raised By | When | Response Available |
|-------|-----------|------|--------------------|
| **`MessageReceivedFromOtherInstance`** | **First instance** | A new (secondary) instance starts | `RespondToOtherSender` (send reply to that secondary instance) |
| **`MessageReceivedFromFirstInstance`** | **Secondary instance** | First instance replies | `RespondToOtherInstance` is always `null` |

---

#### Event Args: `MessageReceivedEventArgs`
- **`string[]? Message`** → Arguments from the other instance.  
- **`Action<string[]>? RespondToOtherSender`** → Only non-null in the first instance; use to reply.  

---

#### Lifecycle
1. Call `Create("UniqueId", args)` → creates named mutex + IPC channels.  
2. Subscribe to events **before** calling `Start()`.  
3. Call `Start()` → begins background thread for listening.  
4. Manually process first-instance args (since no event fires for them).  
5. (Optional) On shutdown, unsubscribe from events.
6. (Optional) Call `Stop()` to release named mutex + IPC channels

---

### Notes
- Event handlers are invoked from a **background thread** — marshal back to the UI thread if needed.  
- The `UniqueId` must be stable and unique per app; otherwise, instances may conflict.
- The listening thread runs until either the process exits or `Stop()` is called. 

## How it's made

I implemented a singleton constructor to ensure that only one instance of the class is alive so that the first instance can't act like a second instance.

When the Start() method is called, a named mutex is created with the unique ID provided. This mutex acts as a system-wide lock:

- If the mutex can be acquired, this process is the first instance. A background thread is then started to listen for connections from other instances using a named pipe server.

- If the mutex cannot be acquired, this process is a secondary instance. Its arguments are sent to the first instance over a named pipe client, and the process then exits immediately.

The communication between instances is handled with named pipes:

- The first instance runs a loop (ListenForClients) that waits for other processes to connect. When a connection is made, it reads the message, wraps it into a MessageReceivedEventArgs, and triggers the MessageReceivedFromOtherInstance event.

- The event args also expose an Action<string[]> RespondToOtherSender which lets the first instance send a response back through the same pipe.

- On the other side, when a secondary instance sends its arguments, it waits for a possible reply from the first instance. If a response is received, the MessageReceivedFromFirstInstance event is triggered.

Events (MessageReceivedFromOtherInstance and MessageReceivedFromFirstInstance) are exposed so that user code can hook into this IPC flow without worrying about mutexes, pipes, or threading details.

## Lessons learned
- How to use named mutexes to manage single-instance enforcement.
- How to use named pipes with StreamReader/StreamWriter for IPC.
- How to expose a callback function in event args without exposing internal state (`RespondToOtherSender`).
- How to implement a singleton pattern that takes parameters safely.
- How to safely exit/abort a thread using cancellation tokens, including blocking threads
- How to write integration unit tests.
