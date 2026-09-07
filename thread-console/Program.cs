using System.Runtime.InteropServices;

[DllImport("libc.so.6", EntryPoint = "getpid")]
static extern int GetNativeProcessId();

[DllImport("libc.so.6", EntryPoint = "gettid")]
static extern int GetNativeThreadId();

//Lets print the current system info:
PrintSytemProcessAndThreadPoolInfo();

//save the current thread reference so we can check later.
var mainThread = Thread.CurrentThread; 
var mainThreadNativeId = GetNativeThreadId();
Console.WriteLine($"To see the status from command prompt, run: top -H -p {mainThreadNativeId}");

/*
    We will print the thread info.
    You can see that the main thread is a foreground thread (IsBackground: False) 
*/
PrintThreadInfo(mainThread); //put 1st breakpoint here, and check the top -H results in the terminal 

/* 
    Do some async task
    What is happening here: control is handed over to the async task and the current thread is freed up.
*/
using var httpClient = new HttpClient(); //put 2nd breakpoint here, and check the top -H results in the terminal 
string result = await httpClient.GetStringAsync("https://timeapi.io/api/v1/time/current/utc");
Console.WriteLine($"Result from API: {result}");

/*  previous awaited task finished, the rest of the code can resume.
    But here is the interesting part: Continuation from this point onwards is NOT executed on the main thread.
    Check the thread info again to see if code executes on the main thread. 
    You will see it is a different thread now, which is a thread-pool thread (look at the thread ID and the type).
    If you check Linux "top" command, you can see the native thread is named as ".NET TP Worker"
*/
PrintThreadInfo(Thread.CurrentThread); //This will print a different thread ID. 

/* 
    so what happened to the mainThread?  We will recheck.  
    You will see that it has changed its state from Running to WaitSleepJoin
*/
PrintThreadInfo(mainThread, mainThreadNativeId); //put the 3rd breakpoint here.

Console.ReadLine();

void PrintSytemProcessAndThreadPoolInfo()
{   
    Console.WriteLine($"System Info:");
    Console.WriteLine($"CPU Count: {Environment.ProcessorCount}"); 

    Console.WriteLine($"Process Info:");
    Console.WriteLine($"Native Process ID: {GetNativeProcessId()}"); 

    Console.WriteLine($"Thread Pool Info:");
    ThreadPool.GetMinThreads(out int workerThreadsCount, out int completionPortThreads);
    Console.WriteLine($"Min worker threads: {workerThreadsCount}, Min completion port threads: {completionPortThreads}");

    ThreadPool.GetMaxThreads(out workerThreadsCount, out completionPortThreads);
    Console.WriteLine($"Max worker threads: {workerThreadsCount}, Max completion port threads: {completionPortThreads}");

    ThreadPool.GetAvailableThreads(out workerThreadsCount, out completionPortThreads);
    Console.WriteLine($"Available worker threads: {workerThreadsCount}, Available completion port threads: {completionPortThreads}");
    Console.WriteLine(Environment.NewLine);
}

void PrintThreadInfo(Thread thread, int nativeThreadId = 0)
{
    int threadId = nativeThreadId > 0 ? nativeThreadId : GetNativeThreadId();
    string nativeThreadName = GetNativeThreadName(threadId);

    Console.WriteLine($"Current Thread info:" + Environment.NewLine +
                        $"Given Name: {thread.Name}" + Environment.NewLine +
                        $"Managed Thread ID: {thread.ManagedThreadId}" + Environment.NewLine +
                        $"IsBackground: {thread.IsBackground}" + Environment.NewLine +
                        $"IsThreadPoolThread: {thread.IsThreadPoolThread}" + Environment.NewLine +
                        $"Priority: {thread.Priority}" + Environment.NewLine +
                        $"ThreadState: {thread.ThreadState}" + Environment.NewLine +
                        $"Apartment state: {thread.GetApartmentState()}" + Environment.NewLine + 
                        $"Native Thread ID: {threadId}" + Environment.NewLine +
                        $"Native Thread Name: {nativeThreadName}" + Environment.NewLine +
                        $"ThreadPool thread count: {ThreadPool.ThreadCount}" + Environment.NewLine

                        );

}


string GetNativeThreadName(int nativeThreadId)
{
    //Build the virtual /proc path for this thread's name
    string commPath = $"/proc/self/task/{nativeThreadId}/comm";

    if(!File.Exists(commPath)) return String.Empty;

    try
    {
        //Read the name string directly out of the Linux kernel
        return File.ReadAllText(commPath).Trim();   
    }
    catch
    {
        return string.Empty;
    }
}

