using SingleInstanceProgramNS;

//event handler
void s_MessageReceivedFromOtherInstance(object? sender, MessageReceivedEventArgs e)
{
    //process the string array(commands sent by the other instance)
    if (e.Message != null)
    {
        PrintStringArray(e.Message);
    }

    //Respond to the sender instance
    if (e.RespondToOtherSender != null)
    {
        e.RespondToOtherSender(["Test123"]);
    }
}

void s_MessageReceivedFromFirstInstance(object? sender, MessageReceivedEventArgs e)
{
    if (e.Message != null)
    {
        PrintStringArray(e.Message);
    }
}
//Process args dummy function
void PrintStringArray(string[] strings)
{
    foreach (string s in strings)
    {
        Console.WriteLine(s);
    }
}

//Unique ID will be used to establish named mutex and pipes. args is the string[] of arguments that additional instances of the program should send to the initial instance.
#if DEBUG
SingleInstanceProgram s = SingleInstanceProgram.GetInstance("UniqueId", "1 2 3".Split());
#else
SingleInstanceProgram s = SingleInstanceProgram.GetInstance("UniqueId", args);
#endif

//Subscribe to the events before starting the single instance thread and calling functions that use events
s.MessageReceivedFromFirstInstance += s_MessageReceivedFromFirstInstance;
s.MessageReceivedFromOtherInstance += s_MessageReceivedFromOtherInstance;

//Starts the single instance thread and can call functions that use events
s.Start();

//Since the initial instance of the program won't trigger MessageReceived event handler, run the function for processing args manually.
PrintStringArray(args);

Console.ReadLine();
s.MessageReceivedFromFirstInstance -= s_MessageReceivedFromFirstInstance;
s.MessageReceivedFromOtherInstance -= s_MessageReceivedFromOtherInstance;