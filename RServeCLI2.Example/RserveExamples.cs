//-----------------------------------------------------------------------
// Copyright (c) 2011, Oliver M. Haynold
// All rights reserved.
//-----------------------------------------------------------------------

namespace RserveCLI2.Example
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class RProcess
    {
        private const int Port = 6311;
        private readonly Process _rtermProcess;

        public RConnection RConnection { get; private set; }

        public int ProcessId
        {
            get
            {
                return _rtermProcess.Id;
            }
        }

        public bool IsRunning
        {
            get
            {
                try { Process.GetProcessById(_rtermProcess.Id); }
                catch (InvalidOperationException) { return false; }
                catch (ArgumentException) { return false; }
                return true;
            }
        }

        /// <summary>
        /// Creates a self-hosted Rserve.
        /// </summary>
        /// <param name="showWindow">If true then the Rserve window will be visible.  Useful for debugging.  Default is false.</param>
        /// <param name="maxInputBufferSizeInKb">The maximal allowable size of the input buffer in kilobytes.  That is, the maximal size of data transported from the client to the server.</param>
        public RProcess(bool showWindow = false, int maxInputBufferSizeInKb = 0, int portNum = 6311)
        {
            // ReSharper restore AssignNullToNotNullAttribute
            string rExeFilePath = Path.Combine(@"C:\Program Files\R\R-3.2.2\bin", Environment.Is64BitOperatingSystem ? "x64" : "i386", "Rterm.exe");

            // the only way to set maxinbuf is via configuration file
            // generate a config file and reference it as part of the args parameter to Rserve() below
            string args = "";
            if (maxInputBufferSizeInKb > 0)
            {
                string configFile = Path.GetTempFileName();
                // plaintext warning only shows when using a config file.  setting plaintext enable to eliminate the warning
                File.WriteAllText(configFile, "maxinbuf " + maxInputBufferSizeInKb + "\r\n" + "plaintext enable");
                args = string.Format(", args = '--RS-conf {0}' ", configFile.Replace(@"\", "/"));
            }

            // launch RTerm and tell it load Rserve. 
            // Keep RTerm open, otherwise the child process will be killed.
            // We will use CmdShutdown to stop the server
            // ReSharper disable UseObjectOrCollectionInitializer
            _rtermProcess = new Process();
            _rtermProcess.StartInfo.FileName = rExeFilePath;
            _rtermProcess.StartInfo.Arguments = string.Format("--no-site-file --no-init-file --no-save -e \"library( Rserve ); Rserve( port = {0} , wait = TRUE {1});\"", portNum, args);
            _rtermProcess.StartInfo.UseShellExecute = false;
            _rtermProcess.StartInfo.CreateNoWindow = !showWindow;
            _rtermProcess.Start();
            Thread.Sleep(3000);
            // ReSharper restore UseObjectOrCollectionInitializer

            // create a connection to the server
            // ReSharper disable RedundantArgumentDefaultValue
            RConnection = RConnection.Connect(port: portNum);
            // ReSharper restore RedundantArgumentDefaultValue
        }
    }

    public class REngine
    {
        private static ConcurrentDictionary<string, RProcess> _userProcesses = new ConcurrentDictionary<string, RProcess>();
        private static int _lastPort = 6311;
        private static object portLock = new object();

        public static RConnection GetConnection(string userId)
        {
            if (!_userProcesses.ContainsKey(userId))
            {
                RProcess process;

                lock (portLock)
                {
                    process = new RProcess(portNum: _lastPort++);
                }

                RunScript(process.RConnection, @"C:\OnlineDataLab\Scripts\global.R");

                _userProcesses.TryAdd(userId, process);
            }

            return _userProcesses[userId].RConnection;
        }

        public static void RunScript(RConnection c, string scriptPath)
        {
            try
            {
                string script = File.ReadAllText(scriptPath);
                script = script.Replace("\r\n", "\r");
                c.TryVoidEval(script);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }

    /// <summary>
    /// A set of simple examples for RserveCLI.
    /// </summary>
    public class MainClass
    {
        /// <summary>
        /// Executes the program
        /// </summary>
        /// <param name="args">The arguments.</param>
        public static void Main(string[] args)
        {
            Task.Factory.StartNew(() =>
            {
                Run(REngine.GetConnection("buddalasunil999@gmail.com"));
                Console.WriteLine("buddalasunil999 Done");
            });
            Task.Factory.StartNew(() =>
            {
                Run(REngine.GetConnection("test1@gmail.com"));
                Console.WriteLine("test1 first Done");
            }).ContinueWith((t) =>
            {
                Run(REngine.GetConnection("test1@gmail.com"));
                Console.WriteLine("test1 second Done");
            });

            Console.ReadKey();
        }

        private static void Run(RConnection c)
        {
            string filePath = "C:/Users/DarkBlue/Google Drive/onlinedatalab/source/dummy data.csv";
            string scriptPath = @"C:\OnlineDataLab\Scripts\input.R";
            REngine.RunScript(c, scriptPath);
            Sexp r = c.TryEval(string.Format("processInput(\"{0}\")", filePath));
            Console.WriteLine(r.AsDictionary);
            //var x = c.Eval("R.version.string");
            //Console.WriteLine(x.AsString);

            //Task.Factory.StartNew(() =>
            //{
            //    //using (var s1 = RConnection.Connect(new System.Net.IPAddress(new byte[] { 127, 0, 0, 1 })))
            //    //{
            //    string filePath = "C:/Users/DarkBlue/Google Drive/onlinedatalab/source/dummy data.csv";
            //    string scriptPath = @"C:\OnlineDataLab\Scripts\input.R";
            //    c.TryVoidEval(File.ReadAllText(scriptPath));
            //    c.TryVoidEval("processInput <- function(inputFilePath){ uploaddata < -read.csv(inputFilePath);outputdata < -toJSON(uploaddata[1:20,]);allchoices < -names(uploaddata);return (list(\"uploaddata\" = uploaddata, \"outputdata\" = outputdata, \"allchoices\" = allchoices));}");
            //    //c.VoidEval(string.Format("uploaddata <- read.csv(\"{0}\")", filePath));
            //    var data = c.Eval("test()");
            //    Console.WriteLine(data.AsList);
            //    //s1.Shutdown();
            //    //}
            //}).Wait();

            //using (var s = RConnection.Connect(new System.Net.IPAddress(new byte[] { 127, 0, 0, 1 })))
            //{
            //    // Generate some example data
            //    var x = Enumerable.Range(1, 20).ToArray();
            //    var y = (from a in x select (0.5 * a * a) + 2).ToArray();

            //    // Build an R data frame
            //    var d = Sexp.MakeDataFrame();
            //    d["x"] = Sexp.Make(x);
            //    d["y"] = Sexp.Make(y);
            //    s["d"] = d;

            //    // Run a linear regression, obtain the summary, and print the result
            //    s.VoidEval("linearModelSummary = summary(lm(y ~ x, d))");
            //    var coefs = s["linearModelSummary$coefficients"];
            //    var rSquared = s["linearModelSummary$r.squared"].AsDouble;
            //    Console.WriteLine("y = {0} x + {1}. R^2 = {2,4:F}%", coefs[1, 0], coefs[0, 0], rSquared * 100);

            //    // Now let's do some linear algebra
            //    var matA = new double[,] { { 14, 9, 3 }, { 2, 11, 15 }, { 0, 12, 17 }, { 5, 2, 3 } };
            //    var matB = new double[,] { { 12, 25 }, { 9, 10 }, { 8, 5 } };
            //    s["a"] = Sexp.Make(matA);
            //    s["b"] = Sexp.Make(matB);
            //    Console.WriteLine(s["a %*% b"].ToString());
            //}
        }

        // need to work this into a unit test - create file on server, read from server, then delete from server
        //// Make a chart and transfer it to the local machine
        //        s.VoidEval( "library(ggplot2)" );
        //        s.VoidEval( "pdf(\"outfile.pdf\")" );
        //        s.VoidEval( "print(qplot(x,y, data=d))" );
        //        s.VoidEval( "dev.off()" );

        //        using ( var f = File.Create( "Data Plot.pdf" ) )
        //        {
        //            s.ReadFile( "outfile.pdf" ).CopyTo( f );
        //        }

        //        s.RemoveFile( "outfile.pdf" );

    }

    /// Extension methods for Rserve components
    public static class RserveExtensions
    {
        /// Eval method that catches errors and returns the actual R error.
        /// See http://www.rforge.net/Rserve/faq.html#errors
        ///  According to the Rserve
        /// docs the try() method is more reliable; however this method
        /// sometimes gives us a stack trace which is very helpful.
        public static Sexp TryEval(this RConnection conn, String expr)
        {
            try
            {
                return conn.Eval(expr);
            }
            catch (RserveException ex)
            {
                GetAndThrowRealError(conn, ex);
                return null;   // We won't get here but the compiler doesn't know that.
            }
        }

        /// VoidEval method with error reporting.
        public static void TryVoidEval(this RConnection conn, String expr)
        {
            try
            {
                conn.VoidEval(expr);
            }
            catch (RserveException ex)
            {
                GetAndThrowRealError(conn, ex);
            }
        }

        /// Try to get a real error message and stack trace; if that fails rethrow the original exception.
        private static void GetAndThrowRealError(RConnection conn, RserveException ex)
        {
            // Try to get the error message
            String msg;
            try
            {
                msg = conn.Eval("geterrmessage()").AsString;
            }
            catch
            {
                throw ex;
            }

            if (String.IsNullOrWhiteSpace(msg))
                throw ex;

            // Try to get the stack trace
            // It's possible that geterrmessage() succeeds and traceback() fails.
            // If so just use the error message
            try
            {
                var tracebacks = conn.Eval("traceback()").AsStrings;
                var traceback = String.Join("\r\n", tracebacks);
#if DEBUG
                msg = msg + traceback;
#endif
            }
            catch
            {
            }

            // Throw with a helpful message
            throw new RserveException(msg);
        }
    }

    /// <summary>
    /// Starts RServe and opens a connections to the server.
    /// </summary>
    /// <remarks>
    /// We are launching RServ using Rterm because its a reliable way to do that.
    /// R CMD Rserve requires RHOME to be in the registry.
    /// </remarks>
    public class Rservice : IDisposable
    {

        #region Constants and Fields

        /// <summary>
        /// The Sexp attributes, if any
        /// </summary>
        private const int Port = 6311;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a self-hosted Rserve.
        /// </summary>
        /// <param name="showWindow">If true then the Rserve window will be visible.  Useful for debugging.  Default is false.</param>
        /// <param name="maxInputBufferSizeInKb">The maximal allowable size of the input buffer in kilobytes.  That is, the maximal size of data transported from the client to the server.</param>
        public Rservice(bool showWindow = false, int maxInputBufferSizeInKb = 0, int portNum = 6311)
        {
            // ReSharper restore AssignNullToNotNullAttribute
            string rExeFilePath = Path.Combine(@"C:\Program Files\R\R-3.2.2\bin", Environment.Is64BitOperatingSystem ? "x64" : "i386", "Rterm.exe");

            // the only way to set maxinbuf is via configuration file
            // generate a config file and reference it as part of the args parameter to Rserve() below
            string args = "";
            if (maxInputBufferSizeInKb > 0)
            {
                string configFile = Path.GetTempFileName();
                // plaintext warning only shows when using a config file.  setting plaintext enable to eliminate the warning
                File.WriteAllText(configFile, "maxinbuf " + maxInputBufferSizeInKb + "\r\n" + "plaintext enable");
                args = string.Format(", args = '--RS-conf {0}' ", configFile.Replace(@"\", "/"));
            }

            // launch RTerm and tell it load Rserve. 
            // Keep RTerm open, otherwise the child process will be killed.
            // We will use CmdShutdown to stop the server
            // ReSharper disable UseObjectOrCollectionInitializer
            _rtermProcess = new Process();
            _rtermProcess.StartInfo.FileName = rExeFilePath;
            _rtermProcess.StartInfo.Arguments = string.Format("--no-site-file --no-init-file --no-save -e \"library( Rserve ); Rserve( port = {0} , wait = TRUE {1});\"", portNum, args);
            _rtermProcess.StartInfo.UseShellExecute = false;
            _rtermProcess.StartInfo.CreateNoWindow = !showWindow;
            _rtermProcess.Start();
            Thread.Sleep(3000);
            // ReSharper restore UseObjectOrCollectionInitializer

            // create a connection to the server
            // ReSharper disable RedundantArgumentDefaultValue
            RConnection = RConnection.Connect(port: portNum);
            // ReSharper restore RedundantArgumentDefaultValue

        }

        #endregion

        #region Properties

        /// <summary>
        /// Get the wrapped RConnection
        /// </summary>
        public RConnection RConnection { get; private set; }

        #endregion

        #region Public Members

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        #region Interface Implimentations

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                //if (disposing)
                //{
                //    // dispose the connection to server
                //    if (RConnection != null)
                //    {
                //        // Kill the server
                //        RConnection.Shutdown();
                //        RConnection.Dispose();
                //    }
                //}
                _disposed = true;
            }
        }

        #endregion

        #region Private Members

        private bool _disposed; // to detect redundant calls
        private readonly Process _rtermProcess;

        #endregion
    }
}
