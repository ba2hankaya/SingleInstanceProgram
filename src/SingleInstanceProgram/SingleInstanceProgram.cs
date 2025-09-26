using System.IO.Pipes;

namespace SingleInstanceProgramNS
{
    public class SingleInstanceProgram
    {
        private readonly string _instanceId; //string used for initailizing the named mutex and IPC pipes
        private Mutex? _mutex;
        private static SingleInstanceProgram? _singletonInstance = null;
        private string[] _argsToBeSentToFirstProgramInstance; //Stores the data passed to the constructor of an instance of this class. This will be sent by secondary instances of the program to the first instance of the program.
        Thread? _listenerThread = null;
        CancellationTokenSource? _cancellationTokenSource = null;

        /// <summary>
        /// Creates initial instance of the class. instanceId takes a unique id to be used in named mutex and pipe server. args takes the data to be sent from additional instances of the program to the initial one.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static SingleInstanceProgram Create(string instanceId, string[] args)  //Singleton pattern so that first instance can't create more instances which would act like secondary instances
        {
            if(_singletonInstance != null)
            {
                throw new InvalidOperationException("Singleton already created - use getinstance()");
            }
            _singletonInstance = new SingleInstanceProgram(instanceId, args);
            return _singletonInstance;
        }

        /// <summary>
        /// Returns the previously created instance of the class.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static SingleInstanceProgram GetInstance()  //Singleton pattern so that first instance can't create more instances which would act like secondary instances
        {
            if (_singletonInstance == null)
            {
                throw new InvalidOperationException("Singleton not created - use GetInstance(arg1, arg2)");
            }
            return _singletonInstance;
        }
        private SingleInstanceProgram(string instanceId, string[] args)
        {
            _instanceId = instanceId;
            _argsToBeSentToFirstProgramInstance = args;
        }

        /// <summary>
        /// Starts the single instance thread and calls functions that use events, event should be subscribed to before calling this method. Set isGlobal to true to make the system wide mutex global.(This will block other users on the same machine from creating first instances.)
        /// </summary>
        /// <param name="isGlobal"></param>
        /// 

        //This function defines a mutex using _instanceId and tries to own it. If it can't (if another instance of the program exists), sends myArgs to the first instance
        //If it can (if another instance of the program doesn't exist) it will start a new background thread for listening to secondary instances of the program (to listen for input).
        public void Start(bool isGlobal = false)
        {
            string mutexPre = isGlobal ? @"Global\" : @"Local\";
            _mutex = new Mutex(false, mutexPre + _instanceId);
            if (!_mutex.WaitOne(0, false))
            {
                SendToFirstInstance(_argsToBeSentToFirstProgramInstance);
                Environment.Exit(0);
            }
            Console.WriteLine("This is the first instance of the application.");
            _cancellationTokenSource = new CancellationTokenSource();
            _listenerThread = new Thread(() => ListenForClients(_cancellationTokenSource.Token));
            _listenerThread.IsBackground = true;
            _listenerThread.Start();
        }

        /// <summary>
        /// Stops the background thread for listening to incomming connections, releases and disposes of named mutex which is used to establish single instance behaviour.
        /// </summary>
        public void Stop()
        {
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }

            if( _listenerThread != null && _cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
            }
        }
        /* The method for the listener thread. Opens a two-way named pipe server stream with _instanceId, and checks for connections from other instances of the program forever.
         * Once a connection is received, it invokes the MessageReceivedFromOtherInstance event, exposing the received message and a function for responding back to the connecting instance as the arguments of the event.
        */
        private void ListenForClients(CancellationToken cancellationToken)
        {

            while (!cancellationToken.IsCancellationRequested)
            {
                using var server = new NamedPipeServerStream(_instanceId, PipeDirection.InOut);
                cancellationToken.Register(() => { server.Dispose(); });
                try
                {
                    server.WaitForConnection();
                    using (var reader = new StreamReader(server, leaveOpen: true)) //leave the stream open, let server handle disposing of the stream
                    {
                        string? message = reader.ReadLine();
                        if (message != null)
                        {
                            MessageReceivedEventArgs eventArgs = new MessageReceivedEventArgs();
                            eventArgs.Message = message.Split("~#$"); //use a special key for splitting and joining messages while communicating so strings with whitespaces can be sent as is.

                            //This function is for writing back to the connecting secondary instance. Done as a lambda action so that the writer stream is not exposed to the user of the library.
                            Action<string[]> s = (string[] args) =>
                            {
                                using (var writer = new StreamWriter(server, leaveOpen: true)) //leave the stream open, let server handle disposing of the stream
                                {
                                    writer.WriteLine(string.Join("~#$", args));  //use a special key for splitting and joining messages while communicating so strings with whitespaces can be sent as is.
                                    writer.Flush();
                                }
                            };
                            eventArgs.RespondToOtherSender = s;
                            OnMessageReceivedFromOtherInstance(eventArgs);
                        }
                    }
                }
                catch (ObjectDisposedException) //if the cancellation token is received and server operation is called, the disposed server will throw object disposed exception. If no server operation is called, the loop condition will return false.
                {
                    break;
                }
            }
        }

        /* This method is used by secondary instances of the program to send data to the first instance. It uses a two-way named pipe client stream so it can receive back data from the first instance as well.
         * Once connected, it will send the provided string array to the first instance. If the first instance responses, it will then invoke MessageReceivedFromFirstInstance event so that user can handle the data
         * from the second instance.
         */
        private void SendToFirstInstance(string[] args)
        {
            if (args.Length > 0)
            {
                using (var client = new NamedPipeClientStream(".",_instanceId,PipeDirection.InOut))
                {
                    client.Connect(200);
                    using (var writer = new StreamWriter(client, leaveOpen: true)) //leave the stream open, let client handle disposing of the stream
                    {
                        writer.WriteLine(string.Join("~#$", args));  //use a special key for splitting and joining messages while communicating so strings with whitespaces can be sent as is.
                        writer.Flush();
                    }
                    using (var reader = new StreamReader(client, leaveOpen: true)) //leave the stream open, let client handle disposing of the stream
                    {
                        string? message = reader.ReadLine();
                        if (message != null)
                        {
                            MessageReceivedEventArgs eventArgs = new MessageReceivedEventArgs();  
                            eventArgs.Message = message.Split("~#$");  //use a special key for splitting and joining messages while communicating so strings with whitespaces can be sent as is.
                            OnMessageReceivedFromFirstInstance(eventArgs);
                        }
                    }
                }
            }
        }

        protected virtual void OnMessageReceivedFromOtherInstance(MessageReceivedEventArgs e)
        {
            MessageReceivedFromOtherInstance?.Invoke(this, e);
        }

        protected virtual void OnMessageReceivedFromFirstInstance(MessageReceivedEventArgs e)
        {
            MessageReceivedFromFirstInstance?.Invoke(this, e);
        }

        //Exposed event so that user can handle how the first instance should handle arguments sent by a secondary instance
        public event EventHandler<MessageReceivedEventArgs>? MessageReceivedFromOtherInstance;

        //Exposed event so that user can handle how secondary instances should handle responses sent back by the first instance
        public event EventHandler<MessageReceivedEventArgs>? MessageReceivedFromFirstInstance;
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public string[]? Message;
        public Action<string[]>? RespondToOtherSender;
    }
}
