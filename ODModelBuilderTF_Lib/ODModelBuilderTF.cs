using Python.Runtime;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ODModelBuilderTF
{
   public static class ODModelBuilderTF
   {
      #region Fields
      /// <summary>
      /// Initialization complete
      /// </summary>
      private static bool initialized;
      /// <summary>
      /// Primary python scope
      /// </summary>
      private static dynamic py;
      /// <summary>
      /// Stderr redirection
      /// </summary>
      private static bool redirectStderr;
      /// <summary>
      /// Stdout redirection
      /// </summary>
      private static bool redirectStdout;
      #endregion
      #region Methods
      /// <summary>
      /// Initialization check
      /// </summary>
      private static void CheckInitialization()
      {
         if (!initialized)
            throw new InvalidOperationException($"The Python engine is not initialized. Initialization must be done calling {nameof(ODModelBuilderTF)}.{nameof(Init)}");
      }
      /// <summary>
      /// Execute a script
      /// </summary>
      /// <param name="script">Script to be executed</param>
      /// <param name="workingDir">Working directory</param>
      public static int Exec(string script, string workingDir = null)
      {
         // Check the initialization state
         CheckInitialization();
         // Store the current directory
         var cd = Environment.CurrentDirectory;
         // Token to stop the output trace
         try {
            // Set the current directory in the root of the python environment
            if (!string.IsNullOrEmpty(workingDir))
               Environment.CurrentDirectory = workingDir;
            // Execute the script
            using var gil = Py.GIL();
            ((PyScope)py).Exec(script);
         }
         catch (PythonException exc) {
            // Check if the type of the exception was simply a system exit
            if (exc.PyType != Exceptions.SystemExit)
               throw;
            // Return the exit result
            var result = new PyObject(exc.PyValue).AsManagedObject(typeof(int));
            if (result != null)
               return (int)result;
         }
         catch (Exception exc) {
            Trace.WriteLine(exc);
            throw;
         }
         finally {
            // Restore the previous directory
            Environment.CurrentDirectory = cd;
         }
         return 0;
      }
      /// <summary>
      /// Return a python script embedded as resource
      /// </summary>
      /// <param name="name">Name of the python script</param>
      /// <returns>The script or null if it doesn't exist</returns>
      public static string GetPythonScript(string name)
      {
         var assembly = Assembly.GetExecutingAssembly();
         var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(r => r.EndsWith($".{name}"));
         var resource = resourceName == null ? null : assembly.GetManifestResourceStream(resourceName);
         return resource == null ? null : new StreamReader(resource).ReadToEnd();
      }
      /// <summary>
      /// Set the python's virtual environment
      /// </summary>
      /// <param name="redirectStdout">Redirect the standard output</param>
      /// <param name="redirectStderr">Redirect the standard error</param>
      /// <param name="virtualEnvPath">Path of the virtual environment</param>
      public static void Init(bool redirectStdout, bool redirectStderr, string virtualEnvPath = null)
      {
         // Create a mutex to avoid overlapped initializations
         using var mutex = new Mutex(true, !string.IsNullOrEmpty(virtualEnvPath) ? virtualEnvPath.Replace("\\", "_").Replace(":", "-") : Assembly.GetEntryAssembly().GetName().Name);
         // Check if it's required a virtual environment
         if (!string.IsNullOrEmpty(virtualEnvPath)) {
            // Name of the downloaded Python zip file and the get pip script
            var pythonZip = Path.Combine(virtualEnvPath, "python.zip");
            var getPipScript = Path.Combine(virtualEnvPath, "get-pip.py");
            // Check for the existence of the environment directory
            if (!Directory.Exists(virtualEnvPath)) {
               // Create the directories
               Directory.CreateDirectory(virtualEnvPath);
               Directory.CreateDirectory(Path.Combine(virtualEnvPath, "Lib"));
               // Download of python and get-pip script
               using (var client = new WebClient()) {
                  client.DownloadFile("https://www.python.org/ftp/python/3.7.8/python-3.7.8-embed-amd64.zip", pythonZip);
                  client.DownloadFile("https://bootstrap.pypa.io/get-pip.py", getPipScript);
               }
               // Extract the embeddable python to the virtual environment directory
               ZipFile.ExtractToDirectory(pythonZip, virtualEnvPath);
               // Delete the downloaded embeddable python zip
               File.Delete(pythonZip);
               // Enable the installation of packages
               var _pth = File.ReadAllLines(Path.Combine(virtualEnvPath, "python37._pth"));
               for (var i = 0; i < _pth.Length; i++) {
                  if (_pth[i].Contains("import site"))
                     _pth[i] = "import site";
               }
               File.WriteAllLines(Path.Combine(virtualEnvPath, "python37._pth"), _pth);
            }
            // Set the environment variables pointing on the python virtual environment
            var path = Environment.GetEnvironmentVariable("PATH").TrimEnd(';');
            var pythonPath = $"{virtualEnvPath};{Path.Combine(virtualEnvPath, "Scripts")};{Path.Combine(virtualEnvPath, "Lib", "site-packages")};{Path.Combine(virtualEnvPath, "Lib")}";
            path = string.IsNullOrEmpty(path) ? pythonPath : pythonPath + ";" + path;
            Environment.SetEnvironmentVariable("PATH", path, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PYTHONHOME", virtualEnvPath, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PYTHONPATH", pythonPath, EnvironmentVariableTarget.Process);
            // Check if the installation of pip is needed
            if (File.Exists(getPipScript)) {
               // Store the current working dir
               var cd = Environment.CurrentDirectory;
               try {
                  // Set the current directory in the root of the python environment
                  Environment.CurrentDirectory = virtualEnvPath;
                  // Install pip
                  var getPipProcess = new Process();
                  getPipProcess.StartInfo.FileName = "python.exe";
                  getPipProcess.StartInfo.Arguments = "get-pip.py";
                  getPipProcess.StartInfo.UseShellExecute = false;
                  getPipProcess.StartInfo.RedirectStandardOutput = true;
                  getPipProcess.StartInfo.RedirectStandardError = true;
                  getPipProcess.StartInfo.CreateNoWindow = true;
                  getPipProcess.OutputDataReceived += (sender, e) =>
                  {
                     if (e.Data != null)
                        Trace.WriteLine(e.Data.Replace(Environment.NewLine, ""));
                  };
                  getPipProcess.ErrorDataReceived += (sender, e) =>
                  {
                     if (e.Data != null)
                        Trace.WriteLine(e.Data.Replace(Environment.NewLine, ""));
                  };
                  getPipProcess.Start();
                  getPipProcess.BeginOutputReadLine();
                  getPipProcess.BeginErrorReadLine();
                  getPipProcess.WaitForExit();
                  getPipProcess.CancelOutputRead();
                  getPipProcess.CancelErrorRead();
                  // Delete the pip installation script
                  File.Delete(getPipScript);
               }
               finally {
                  // Restore the working directory
                  Environment.CurrentDirectory = cd;
               }
            }
         }
         // Initialize the python engine and enable thread execution
         PythonEngine.Initialize();
         PythonEngine.BeginAllowThreads();
         // Create the python scope and set the python executable
         using (Py.GIL()) {
            py ??= Py.CreateScope();
            py.Import("sys");
            py.sys.executable = Path.Combine(virtualEnvPath, "python.exe");
         }
         // Redirect the python's outputs
         RedirectOutputs(redirectStdout, redirectStderr);
         // Token to stop the tracing
         var traceCancel = new CancellationTokenSource();
         PythonEngine.AddShutdownHandler(() => traceCancel.Cancel());
         // Enable the trace of the output
         TracePythonOutputs(traceCancel.Token);
         // Read the requirements file
         var requirementsRes = Assembly.GetExecutingAssembly().GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("requirements.txt"));
         using var requirements = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(requirementsRes));
         // Define the packages installation functions
         using (Py.GIL()) {
            var sb = new StringBuilder();
            sb.AppendLine("def check_package(package_name, print_exc=False):");
            sb.AppendLine("  import pkg_resources as pkg");
            sb.AppendLine("  try:");
            sb.AppendLine("    pkg.get_distribution(package_name)");
            sb.AppendLine("    return True");
            sb.AppendLine("  except Exception as e:");
            sb.AppendLine("    if (print_exc):");
            sb.AppendLine("       print(e)");
            sb.AppendLine("    return False");
            sb.AppendLine("");
            sb.AppendLine("def install_package(package_name):");
            sb.AppendLine("  import subprocess");
            sb.AppendLine("  print(subprocess.check_output(['python.exe', '-m', 'pip', 'install', '--no-deps', package_name], shell=True).decode())");
            py.Exec(sb.ToString());
         }
         // Install the required packages
         for (var line = requirements.ReadLine(); line != null; line = requirements.ReadLine()) {
            using var gil = Py.GIL();
            if (!py.check_package(line, true))
               py.install_package(line);
         }
         // Import the object detection modules
         var modules = new[]
         {
            "default_cfg",
            "model_types",
            "utilities",
            "base_parameters",
            "eval_parameters",
            "export_parameters",
            "train_parameters",
            "eval_main",
            "export_environment",
            "export_frozen_graph",
            "export_main",
            "export_model_config",
            "export_onnx",
            "main",
            "od_install",
            "pretrained_model",
            "tf_records",
            "train_environment",
            "train_main",
            "train_pipeline",
            "train_tensorboard",
         };
         foreach (var module in modules) {
            using var gil = Py.GIL();
            py.Import(PythonEngine.ModuleFromString(module, GetPythonScript($"{module}.py")));
         }
         // Install the object detection system
         using (Py.GIL()) {
            if (!py.check_package("object-detection"))
               py.od_install.install_object_detection();
         }
         // Initialization terminated
         initialized = true;
      }
      /// <summary>
      /// Redirect the outputs of Python to string buffers
      /// </summary>
      /// <param name="stdout">Redirect the standard output</param>
      /// <param name="stderr">Redirect the standard error</param>
      private static void RedirectOutputs(bool stdout, bool stderr)
      {
         using var gil = Py.GIL();
         var sb = new StringBuilder();
         sb.Append("import sys\n");
         sb.Append("from io import StringIO\n");
         sb.Append("stdout = StringIO()\n");
         sb.Append("stderr = StringIO()\n");
         if (stdout) {
            sb.Append("sys.stdout = stdout\n");
            sb.Append("sys.stdout.flush()\n");
         }
         if (stderr) {
            sb.Append("sys.stderr = stderr\n");
            sb.Append("sys.stderr.flush()\n");
         }
         py.Exec(sb.ToString());
         redirectStdout = stdout;
         redirectStderr = stderr;
      }
      /// <summary>
      /// Trace the python's outputs
      /// </summary>
      /// <param name="cancel">Cancellation token</param>
      /// <returns>The trace task</returns>
      private static Task TracePythonOutputs(CancellationToken cancel)
      {
         // Run the trace task
         return Task.Run(async () =>
         {
            // Check if cancellation is required
            if (cancel.IsCancellationRequested)
               return;
            // Read the current output buffers
            string stdout;
            string stderr;
            using (Py.GIL()) {
               stdout = redirectStdout ? py.stdout.getvalue().ToString() : "";
               stderr = redirectStderr ? py.stderr.getvalue().ToString() : "";
            }
            var lenOut = stdout.Length;
            var lenErr = stderr.Length;
            // Loop until cancellation
            while (!cancel.IsCancellationRequested) {
               // Delay
               await Task.Delay(500, cancel).ContinueWith(t => { });
               if (cancel.IsCancellationRequested)
                  return;
               // Read from buffers and output the trace
               using (Py.GIL()) {
                  if (redirectStdout) {
                     try {
                        if (cancel.IsCancellationRequested)
                           return;
                        stdout = py.stdout.getvalue().ToString();
                        var newLen = stdout.Length;
                        if (newLen > lenOut) {
                           stdout = stdout.Substring(lenOut);
                           Trace.Write(stdout);
                           lenOut = newLen;
                        }
                     }
                     catch (Exception) {
                     }
                  }
                  if (redirectStderr) {
                     try {
                        if (cancel.IsCancellationRequested)
                           return;
                        stderr = py.stderr.getvalue().ToString();
                        var newLen = stderr.Length;
                        if (newLen > lenErr) {
                           stderr = stderr.Substring(lenErr);
                           Trace.Write(stderr);
                           lenErr = newLen;
                        }
                     }
                     catch (Exception) {
                     }
                  }
               }
            }
         }, cancel);
      }
      #endregion
   }
}
