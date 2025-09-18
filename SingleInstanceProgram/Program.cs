using SingleInstanceProgramNS;

//event handler
void s_MessageReceived(object? sender, MessageReceivedEventArgs e)
{
    if (e.Message != null)
    {
        PrintStringArray(e.Message);
    }
}

//Process args dummy function
void PrintStringArray(string[] strings)
{
    foreach(string s in strings)
    {
        Console.WriteLine(s);
    }
}

//Unique ID will be used to establish named mutex and pipes. args is the string[] of arguments that additional instances of the program should send to the initial instance.
SingleInstanceProgram s = SingleInstanceProgram.GetInstance("UniqueId", args);

//Since the initial instance of the program won't trigger MessageReceived event handler, run the function for processing args manually.
PrintStringArray(args);

//Subscribe to the event
s.MessageReceived += s_MessageReceived;
Console.ReadLine();
s.MessageReceived -= s_MessageReceived;