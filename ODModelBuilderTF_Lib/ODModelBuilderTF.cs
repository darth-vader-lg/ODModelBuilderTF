using Python.Runtime;
using System;
using System.Collections.Generic;
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
   /// <summary>
   /// Class for the Python object detection model builder interfacing
   /// </summary>
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
      /// <summary>
      /// Thread state
      /// </summary>
      private static IntPtr ts;
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
      /// Model evaluation
      /// </summary>
      /// <param name="checkpointDir">The checkpoint directory</param>
      public static void Evaluate(string checkpointDir)
      {
         // Check arguments
         if (string.IsNullOrWhiteSpace(checkpointDir))
            throw new ArgumentNullException(nameof(checkpointDir), "Unspecified checkpoint directory");
         if (!Directory.Exists(checkpointDir))
            throw new ArgumentNullException(nameof(checkpointDir), "The checkpoint directory doesn't exist");
         try {
            // Acquire the GIL
            using var gil = Py.GIL();
            // Create a new scope
            var pyEval = ((PyScope)py).NewScope();
            // Prepare the arguments
            dynamic sys = pyEval.Import("sys");
            sys.argv = new PyList(
               new PyObject[]
               {
                  Assembly.GetEntryAssembly().Location.ToPython(),
                  "--checkpoint_dir".ToPython(), checkpointDir.ToPython()
               });
            // Import the main of the training
            dynamic eval_main = pyEval.Import("eval_main");
            // Import the module here just for having the flags defined
            eval_main.allow_flags_override();
            try {
               pyEval.Import("object_detection.model_main_tf2");
            }
            catch (Exception) {
            }
            // Import the TensorFlow
            dynamic tf = pyEval.Import("tensorflow");
            try {
               tf.compat.v1.app.run(eval_main.eval_main);
            }
            catch (PythonException exc) {
               // Response to the exceptions
               var action = exc.PyType switch
               {
                  var pexc when pexc == Exceptions.SystemExit => new Action(() => { }),
                  var pexc when pexc == Exceptions.KeyboardInterrupt => new Action(() =>
                  {
                     Trace.WriteLine("Interrupted by user");
                  }),
                  _ => new Action(() => { throw exc; })
               };
               action();
            }
         }
         catch (Exception exc) {
            Trace.WriteLine(exc.ToString().Replace("\\n", Environment.NewLine));
            throw;
         }
      }
      /// <summary>
      /// Return a python embedded resource
      /// </summary>
      /// <param name="name">Name of the python script</param>
      /// <returns>The script or null if it doesn't exist</returns>
      public static Stream GetPythonResource(string name)
      {
         var assembly = Assembly.GetExecutingAssembly();
         var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(r => r.EndsWith($".{name}"));
         return resourceName == null ? null : assembly.GetManifestResourceStream(resourceName);
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
      /// <param name="fullPython">Enable the installation of the full python instead of the embeddable one</param>
      public static void Init(bool redirectStdout, bool redirectStderr, string virtualEnvPath = null, bool fullPython = true)
      {
         // Create a mutex to avoid overlapped initializations
         virtualEnvPath = string.IsNullOrEmpty(virtualEnvPath) ? null : Path.GetFullPath(virtualEnvPath);
         using var mutex = new Mutex(true, !string.IsNullOrEmpty(virtualEnvPath) ? virtualEnvPath.Replace("\\", "_").Replace(":", "-") : Assembly.GetEntryAssembly().GetName().Name);
         // Check if it's required a virtual environment
         if (!string.IsNullOrEmpty(virtualEnvPath)) {
            // Name of the downloaded Python zip file and the get pip script
            var pythonZip = Path.Combine(virtualEnvPath, "python.zip");
            var getPipScript = Path.Combine(virtualEnvPath, "get-pip.py");
            // Check if the Assembly contains the environment
            var zipEnv = GetPythonResource("env.zip");
            if (zipEnv != null) {
               var archive = new ZipArchive(zipEnv);
               var extract = !Directory.Exists(virtualEnvPath);
               if (!extract) {
                  var buildTimeFile = Path.Combine(virtualEnvPath, "build-time.txt");
                  extract |= !File.Exists(buildTimeFile);
                  if (!extract) {
                     var arcBuildTimeFile = archive.GetEntry("build-time.txt");
                     extract |= arcBuildTimeFile.LastWriteTime > File.GetLastWriteTime(buildTimeFile);
                  }
               }
               if (extract) {
                  Trace.WriteLine("Preparing the environment");
                  archive.ExtractToDirectory(virtualEnvPath, true);
               }
            }
            else {
               // Check for the existence of the environment directory
               if (!Directory.Exists(virtualEnvPath)) {
                  Trace.WriteLine("Preparing the environment");
                  if (fullPython) {
                     // Package for setup
                     var pythonNupkg = Path.Combine(virtualEnvPath, "python.zip");
                     try {
                        // Create the virtual environment directory
                        Directory.CreateDirectory(virtualEnvPath);
                        // Download the package
                        using (var client = new WebClient())
                           client.DownloadFile("https://globalcdn.nuget.org/packages/python.3.7.8.nupkg", pythonNupkg);
                        // Extract the python
                        using (var zip = ZipFile.Open(pythonNupkg, ZipArchiveMode.Read)) {
                           foreach (var entry in zip.Entries) {
                              if (!entry.FullName.StartsWith("tools"))
                                 continue;
                              var dest = Path.Combine(virtualEnvPath, entry.FullName.Substring(6));
                              var destDir = Path.GetDirectoryName(dest);
                              if (!Directory.Exists(destDir))
                                 Directory.CreateDirectory(destDir);
                              using (var writer = File.Create(dest))
                                 entry.Open().CopyTo(writer);
                           }
                        }
                     }
                     catch {
                        // Delete the virtual environment directory
                        try { Directory.Delete(virtualEnvPath, true); } catch { }
                        throw;
                     }
                     finally {
                        // Delete the package used for setup
                        try { File.Delete(pythonNupkg); } catch { }
                     }
                  }
                  else {
                     // Create the directories
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
                     File.Move(Path.Combine(virtualEnvPath, "python37._pth"), Path.Combine(virtualEnvPath, "python37._pth.disabled"));
                  }
               }
            }
            // Set the environment variables pointing on the python virtual environment
            var path = Environment.GetEnvironmentVariable("PATH").TrimEnd(';');
            var pythonPath =
               $"{virtualEnvPath};" +
               $"{Path.Combine(virtualEnvPath, "Scripts")};" +
               $"{Path.Combine(virtualEnvPath, "DLLs")};" +
               $"{Path.Combine(virtualEnvPath, "Lib")};" +
               $"{Path.Combine(virtualEnvPath, "Lib", "site-packages")}";
            path = string.IsNullOrEmpty(path) ? pythonPath : pythonPath + ";" + path;
            Environment.SetEnvironmentVariable("PATH", path, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PYTHONHOME", virtualEnvPath, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PYTHONPATH", pythonPath, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PY_PIP", Path.Combine(virtualEnvPath, "Scripts"), EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PY_LIBS", Path.Combine(virtualEnvPath, "Lib", "site-packages"), EnvironmentVariableTarget.Process);
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
            // Create the environment activation script
            var activatePs1 = fullPython && zipEnv == null ? Path.Combine(virtualEnvPath, "Activate.ps1") : Path.Combine(virtualEnvPath, "Scripts", "Activate.ps1");
            if (!File.Exists(activatePs1)) {
               var assembly = Assembly.GetExecutingAssembly();
               var activateScriptName = assembly.GetManifestResourceNames().FirstOrDefault(r => r.EndsWith($".{"Activate.ps1"}"));
               var activateScript = activateScriptName == null ? null : assembly.GetManifestResourceStream(activateScriptName);
               if (activateScript != null) {
                  var content = new StreamReader(activateScript).ReadToEnd().Replace("<VirtualEnvironmentPath>", virtualEnvPath);
                  File.WriteAllText(activatePs1, content);
               }
            }
         }
         // Initialize the python engine and enable thread execution
         void InitPythonEngine()
         {
            PythonEngine.Initialize();
            // Create the python scope with right paths and set the python executable
            using (Py.GIL()) {
               py = Py.CreateScope();
               py.Import("sys");
               py.sys.path = new PyList(new[]
               {
                  virtualEnvPath,
                  Path.Combine(virtualEnvPath, "Scripts"),
                  Path.Combine(virtualEnvPath, "DLLs"),
                  Path.Combine(virtualEnvPath, "Lib"),
                  Path.Combine(virtualEnvPath, "Lib", "site-packages")
               }.Select(p => p.ToPython()).ToArray());
               py.sys.executable = Path.Combine(virtualEnvPath, "python.exe").ToPython();
            }
            // Redirect the python's outputs
            RedirectOutputs(py, redirectStdout, redirectStderr);
            // Token to stop the tracing
            var traceCancel = new CancellationTokenSource();
            PythonEngine.AddShutdownHandler(() => traceCancel.Cancel());
            // Enable the trace of the output
            TracePythonOutputs(traceCancel.Token);
            ts = PythonEngine.BeginAllowThreads();
         }
         InitPythonEngine();
         var reinit = false;
         if (Path.GetFileName(Path.GetDirectoryName(virtualEnvPath)).ToLower() != "odmodelbuildertf_py") {
            // Define the package check function
            static List<string> GetMissingRequirements(IEnumerable<string> requirements)
            {
               // List of missing packages
               var result = new List<string>();
               using (Py.GIL()) {
                  // Package resources module and workingset
                  var pkg = py.Import("pkg_resources");
                  py.Import("importlib");
                  py.importlib.reload(pkg);
                  var ws = pkg.WorkingSet();
                  // Check all requirements
                  foreach (var req in requirements) {
                     // Skip comments and blank lines
                     if (string.IsNullOrWhiteSpace(req) || req.TrimStart().StartsWith("#"))
                        continue;
                     // Check if the package exists
                     if (ws.find(pkg.Requirement(req)) == null)
                        result.Add(req);
                  }
               }
               // Return the list of missing packages
               return result;
            }
            // Install the required packages
            try {
               // Read the requirements file
               var requirementsRes = Assembly.GetExecutingAssembly().GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("requirements.txt"));
               using var requirementsContent = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(requirementsRes));
               // Read all requirements from the requirements file
               var requirements = requirementsContent.ReadToEnd().Split(Environment.NewLine);
               // Get the list of missing packages
               var missing = GetMissingRequirements(requirements);
               if (missing.Count > 0) {
                  reinit = true;
                  // Trace the missing packages
                  Trace.WriteLine("The following requirements are missing:");
                  missing.ForEach(req => Trace.WriteLine(req));
                  Trace.WriteLine("Installing.");
                  // Create a temporary requirements file
                  var tempRequirements = Path.GetTempFileName();
                  try {
                     using var writer = new StreamWriter(tempRequirements);
                     for (var i = 0; i < missing.Count; i++) {
                        var package = missing[i];
                        // Check if is just a comment or a blank line
                        if (string.IsNullOrWhiteSpace(package) || package.TrimStart().StartsWith("#"))
                           continue;
                        // Choose the right tensorflow for the installed CUDA
                        if (package.StartsWith("tensorflow==")) {
                           var sbCudaVer = new StringBuilder();
                           try {
                              var nvccProcess = new Process();
                              nvccProcess.StartInfo.FileName = "nvcc.exe";
                              nvccProcess.StartInfo.Arguments = "--version";
                              nvccProcess.StartInfo.UseShellExecute = false;
                              nvccProcess.StartInfo.RedirectStandardOutput = true;
                              nvccProcess.StartInfo.CreateNoWindow = true;
                              nvccProcess.OutputDataReceived += (sender, e) =>
                              {
                                 if (e.Data != null)
                                    sbCudaVer.AppendLine(e.Data);
                              };
                              nvccProcess.Start();
                              nvccProcess.BeginOutputReadLine();
                              nvccProcess.WaitForExit();
                              nvccProcess.CancelOutputRead();
                           }
                           catch (Exception) {
                           }
                           if (sbCudaVer.ToString().Contains("V10.1")) {
                              var version = package.Substring(package.IndexOf("==") + 2).Trim();
                              var whl =
                                 Directory.GetFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Packages"), "*.whl")
                                 .Where(file => { var name = Path.GetFileName(file).ToLower(); return name.Contains("tensorflow") && name.Contains("cp37") && name.Contains(version); })
                                 .FirstOrDefault();
                              if (whl != default)
                                 package = whl;
                           }
                        }
                        writer.WriteLine(package);
                     }
                     writer.Close();
                     // Upgrade pip and install the requirements
                     using (Py.GIL()) {
                        py.Import(PythonEngine.ModuleFromString("utilities", GetPythonScript("utilities.py")));
                        py.utilities.execute_script(new[] { "-m", "pip", "install", "--upgrade", "pip" });
                        py.utilities.execute_script(new[] { "-m", "pip", "install", "--upgrade", "setuptools" });
                        py.utilities.execute_script(new[] { "-m", "pip", "install", "--no-cache", "--no-deps", "-r", tempRequirements });
                     }
                     // Check for successfully installation
                     missing = GetMissingRequirements(requirements);
                     if (missing.Count > 0) {
                        Trace.WriteLine("Error! Couldn't install some requirements:");
                        missing.ForEach(req => Trace.WriteLine(req));
                        throw new Exception("Installation failed");
                     }
                  }
                  finally {
                     // Delete the temporary requirements file
                     try {
                        File.Delete(tempRequirements);
                     }
                     catch { }
                  }
               }
            }
            catch (Exception exc) {
               Trace.WriteLine(exc);
               throw;
            }
            // Install the object detection system
            try {
               if (GetMissingRequirements(new[] { "object-detection" }).Count > 0) {
                  reinit = true;
                  using (Py.GIL()) {
                     py.Import(PythonEngine.ModuleFromString("default_cfg", GetPythonScript("default_cfg.py")));
                     py.Import(PythonEngine.ModuleFromString("utilities", GetPythonScript("utilities.py")));
                     py.Import(PythonEngine.ModuleFromString("od_install", GetPythonScript("od_install.py")));
                     py.od_install.install_object_detection(no_cache: true, no_deps: true);
                  }
               }
               if (GetMissingRequirements(new[] { "object-detection" }).Count > 0) {
                  Trace.WriteLine("Error! Couldn't install object detection api");
                  throw new Exception("Installation failed");
               }
            }
            catch (Exception exc) {
               Trace.WriteLine(exc);
               throw;
            }
            reinit = true;
         }
         if (reinit) {
            PythonEngine.EndAllowThreads(ts);
            using (Py.GIL()) {
               if (py != null)
                  py.Dispose();
            }
            PythonEngine.Shutdown();
            InitPythonEngine();
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
            "eval_environment",
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
         // Initialization terminated
         initialized = true;
      }
      /// <summary>
      /// Redirect the outputs of Python to string buffers
      /// </summary>
      /// <param name="scope">Python scope</param>
      /// <param name="stdout">Redirect the standard output</param>
      /// <param name="stderr">Redirect the standard error</param>
      private static void RedirectOutputs(PyScope scope, bool stdout, bool stderr)
      {
         using var gil = Py.GIL();
         var sb = new StringBuilder();
         dynamic py = scope;
         py.Import("io");
         py.Import("sys");
         py.stdout = py.io.StringIO();
         py.stderr = py.io.StringIO();
         if (stdout) {
            py.sys.stdout = py.stdout;
            py.sys.stdout.flush();
         }
         if (stderr) {
            py.sys.stderr = py.stderr;
            py.sys.stderr.flush();
         }
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
      /// <summary>
      /// Model train
      /// </summary>
      /// <param name="modelType">Type of the model</param>
      /// <param name="modelDir">The train data directory</param>
      /// <param name="trainImagesDir">The train images and annotations dir</param>
      /// <param name="evalImagesDir">The evaluation images and annotations dir</param>
      /// <param name="batchSize">The batch size of the train</param>
      /// <param name="numTrainSteps">Maximum number of train steps. Read from the pipeline config file if < 0</param>
      /// <param name="tensorboardPort">The tensorboard listening port.</param>
      /// <param name="annotationsDir">The tensorflow records directory. A temporary directory will be created if null.</param>
      public static void Train(ModelTypes modelType, string modelDir, string trainImagesDir, string evalImagesDir, int batchSize, int numTrainSteps = -1, int tensorboardPort = 6006, string annotationsDir = null)
      {
         // Check arguments
         if (string.IsNullOrWhiteSpace(trainImagesDir))
            throw new ArgumentNullException(nameof(trainImagesDir), "Unspecified train images directory");
         if (!Directory.Exists(trainImagesDir))
            throw new ArgumentNullException(nameof(trainImagesDir), "The train images directory doesn't exist");
         if (string.IsNullOrWhiteSpace(evalImagesDir))
            throw new ArgumentNullException(nameof(trainImagesDir), "Unspecified evaluation images directory");
         if (!Directory.Exists(evalImagesDir))
            throw new ArgumentNullException(nameof(trainImagesDir), "The evaluation directory doesn't exist");
         if (string.IsNullOrWhiteSpace(modelDir))
            throw new ArgumentNullException(nameof(modelDir), "Unspecified model train directory");
         // Directory for the tf records
         var delAnnotationDir = string.IsNullOrWhiteSpace(annotationsDir);
         annotationsDir = !string.IsNullOrWhiteSpace(annotationsDir) ? annotationsDir : Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
         try {
            // Acquire the GIL
            using var gil = Py.GIL();
            // Create a new scope
            var pyTrain = ((PyScope)py).NewScope();
            // Prepare the arguments
            dynamic sys = pyTrain.Import("sys");
            sys.argv = new PyList(
               new PyObject[]
               {
                  Assembly.GetEntryAssembly().Location.ToPython(),
                  "--model_type".ToPython(), modelType.ToText().ToPython(),
                  "--train_images_dir".ToPython(), trainImagesDir.ToPython(),
                  "--eval_images_dir".ToPython(), evalImagesDir.ToPython(),
                  "--model_dir".ToPython(), modelDir.ToPython(),
                  "--annotations_dir".ToPython(), annotationsDir.ToPython(),
                  "--tensorboard_port".ToPython(), tensorboardPort.ToString().ToPython(),
                  "--num_train_steps".ToPython(), numTrainSteps.ToString().ToPython(),
                  "--batch_size".ToPython(), batchSize.ToString().ToPython()
               });
            // Import the main of the training
            dynamic train_main = pyTrain.Import("train_main");
            // Import the module here just for having the flags defined
            train_main.allow_flags_override();
            pyTrain.Import("object_detection.model_main_tf2");
            // Import the TensorFlow
            dynamic tf = pyTrain.Import("tensorflow");
            try {
               // Create annotation dir and start the train
               if (!Directory.Exists(annotationsDir))
                  Directory.CreateDirectory(annotationsDir);
               var train = new Action<dynamic>(unused_argv =>
               {
                  var stepCallback = new Action<dynamic>(args =>
                  {
                     using (Py.GIL())
                        Console.WriteLine($"Step {args.global_step}, Per-step time {args.per_step_time} secs, Loss {args.loss}");
                  });
                  var checkpointCallback = new Action<dynamic>(args =>
                  {
                     using (Py.GIL())
                        Console.WriteLine($"Checkpoint saved at {args.latest_checkpoint}");
                  });
                  using (Py.GIL())
                     train_main.train_main(unused_argv, step_callback:stepCallback, checkpoint_callback:checkpointCallback);
               });
               tf.compat.v1.app.run(train);
            }
            catch (PythonException exc) {
               // Response to the exceptions
               var action = exc.PyType switch
               {
                  var pexc when pexc == Exceptions.SystemExit => new Action(() => { }),
                  var pexc when pexc == Exceptions.KeyboardInterrupt => new Action(() =>
                  {
                     Trace.WriteLine("Interrupted by user");
                  }),
                  _ => new Action(() => { throw exc; })
               };
               action();
            }
         }
         catch (Exception exc) {
            Trace.WriteLine(exc.ToString().Replace("\\n", Environment.NewLine));
            throw;
         }
         finally {
            // Delete the annotations directory
            try {
               if (delAnnotationDir && Directory.Exists(annotationsDir))
                  Directory.Delete(annotationsDir, true);
            }
            catch (Exception exc) {
               Trace.WriteLine(exc);
            }
         }
      }
      #endregion
   }
}
