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
      private static IntPtr threadState;
      #endregion
      #region Properties
      /// <summary>
      /// Initialized status
      /// </summary>
      internal static bool Initialized { get; private set; }
      /// <summary>
      /// Initialized status
      /// </summary>
      internal static PyScope MainScope { get; private set; }
      #endregion
      #region Methods
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
      public static void Init(bool redirectStdout = true, bool redirectStderr = true, string virtualEnvPath = default)
      {
         // Check if already initialized
         if (Initialized)
            return;
         // Virtual environment's full path
         var assemblyName = Assembly.GetExecutingAssembly().GetName();
         virtualEnvPath = string.IsNullOrEmpty(virtualEnvPath) ? Path.Combine(Path.GetTempPath(), $"{assemblyName.Name}-{assemblyName.Version}") : Path.GetFullPath(virtualEnvPath);
         // Create a mutex to avoid overlapped initializations
         using var mutex = new Mutex(true, virtualEnvPath.Replace("\\", "_").Replace(":", "-"));
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
               // Package for setup
               var pythonNupkg = Path.Combine(virtualEnvPath, "python.zip");
               try {
                  // Create the virtual environment directory
                  Directory.CreateDirectory(virtualEnvPath);
                  // Download the package
                  using (var client = new WebClient())
                     client.DownloadFile("https://globalcdn.nuget.org/packages/python.3.7.8.nupkg", pythonNupkg);
                  // Extract the python
                  using var zip = ZipFile.Open(pythonNupkg, ZipArchiveMode.Read);
                  foreach (var entry in zip.Entries) {
                     if (!entry.FullName.StartsWith("tools"))
                        continue;
                     var dest = Path.Combine(virtualEnvPath, entry.FullName[6..]);
                     var destDir = Path.GetDirectoryName(dest);
                     if (!Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);
                     using var writer = File.Create(dest);
                     entry.Open().CopyTo(writer);
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
         // Initialize the python engine and enable thread execution
         void InitPythonEngine()
         {
            PythonEngine.Initialize();
            // Create the python scope with right paths and set the python executable
            using (Py.GIL()) {
               MainScope = Py.CreateScope();
               var sys = MainScope.Import("sys");
               sys.path = new PyList(new[]
               {
                  virtualEnvPath,
                  Path.Combine(virtualEnvPath, "Scripts"),
                  Path.Combine(virtualEnvPath, "DLLs"),
                  Path.Combine(virtualEnvPath, "Lib"),
                  Path.Combine(virtualEnvPath, "Lib", "site-packages")
               }.Select(p => p.ToPython()).ToArray());
               sys.executable = Path.Combine(virtualEnvPath, "python.exe").ToPython();
            }
            // Redirect the python's outputs
            RedirectOutputs(MainScope, redirectStdout, redirectStderr);
            // Token to stop the tracing
            var traceCancel = new CancellationTokenSource();
            PythonEngine.AddShutdownHandler(() => traceCancel.Cancel());
            // Enable the trace of the output
            TracePythonOutputs(traceCancel.Token);
            threadState = PythonEngine.BeginAllowThreads();
         }
         // Initialize the Python engine
         InitPythonEngine();
         // Check for missing requirements
         var reinit = false;
         if (zipEnv == null && Path.GetFileName(Path.GetDirectoryName(virtualEnvPath)).ToLower() != "odmodelbuildertf_py") {
            // Read the requirements file
            var requirementsRes = Assembly.GetExecutingAssembly().GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("requirements.txt"));
            using var requirementsContent = Assembly.GetExecutingAssembly().GetManifestResourceStream(requirementsRes);
            var tempRequirements = Path.GetTempFileName();
            try {
               using var writer = File.Create(tempRequirements);
               requirementsContent.CopyTo(writer);
               writer.Close();
               using (Py.GIL()) {
                  MainScope.Import(PythonEngine.ModuleFromString("default_cfg", GetPythonScript("default_cfg.py")));
                  MainScope.Import(PythonEngine.ModuleFromString("utilities", GetPythonScript("utilities.py")));
                  MainScope.Import(PythonEngine.ModuleFromString("od_install", GetPythonScript("od_install.py")));
                  MainScope.Import(PythonEngine.ModuleFromString("install_virtual_environment", GetPythonScript("install_virtual_environment.py")));
                  var missing = ((dynamic)MainScope).install_virtual_environment.check_requirements(requirements: tempRequirements, no_deps: true);
                  if (missing.Length() > 0) {
                     reinit = true;
                     ((dynamic)MainScope).install_virtual_environment.install_virtual_environment(
                        env_name: virtualEnvPath,
                        requirements: tempRequirements,
                        no_cache: false, //@@@true,
                        custom_tf_dir: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Packages"));
                  }
               }
            }
            finally {
               try { File.Delete(tempRequirements); } catch { }
            }
         }
         if (reinit) {
            PythonEngine.EndAllowThreads(threadState);
            using (Py.GIL()) {
               if (MainScope != null)
                  MainScope.Dispose();
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
            MainScope.Import(PythonEngine.ModuleFromString(module, GetPythonScript($"{module}.py")));
         }
         // Initialization terminated
         Initialized = true;
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
            var py = (dynamic)MainScope;
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
                           stdout = stdout[lenOut..];
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
                           stderr = stderr[lenErr..];
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
