import  os
from    pathlib import Path
import  subprocess
import  sys

def allow_flags_override():
    """
    Allow the argv flags override.
    """
    # Avoiding the absl error for duplicated flags if defined more than one time
    from absl import flags
    for f in flags.FLAGS.flag_values_dict():
        flags.FLAGS[f].allow_override = True

def execute_subprocess(cmd: []):
    """
    Execute a subprocess returning each line of the standard output.
    Keyword arguments:
    cmd     -- the process to execute with its parameters
    """
    env = {
        **os.environ, 'PATH':
        os.path.dirname(sys.executable) + os.pathsep +
        ((os.path.dirname(__file__) + os.pathsep) if '__file__' in globals() else '') +
        os.environ['PATH']}
    shell = 'CREATE_NO_WINDOW' in dir(subprocess)
    creationflags = subprocess.CREATE_NO_WINDOW if shell else 0
    popen = subprocess.Popen(cmd, env=env, stdout=subprocess.PIPE, universal_newlines=True,shell=shell,creationflags=creationflags)
    for stdout_line in iter(popen.stdout.readline, ""):
        yield stdout_line 
    popen.stdout.close()
    return_code = popen.wait()
    if return_code:
        raise subprocess.CalledProcessError(return_code, cmd)

def execute(cmd: []):
    """
    Execute a subprocess printing its standard output.
    Keyword arguments:
    cmd     -- the process to execute with its parameters
    """
    for output in execute_subprocess(cmd):
        print(output, end="")

def execute_script(cmd: []):
    """
    Execute a script as a subprocess printing its standard output.
    Keyword arguments:
    cmd     -- the parameters of the script
    """
    script_cmd = [sys.executable]
    script_cmd.extend(cmd)
    for output in execute_subprocess(script_cmd):
        print(output, end="")

def get_package_info(package_name: str):
    """
    Return a package and its version if installed. Otherwise None
    package_name    -- package name to test for existence
    """
    class Result(object):
        def __init__(self, *args, **kwargs):
            try:
                import pkg_resources
                dist = pkg_resources.get_distribution(package_name)
                self.name = dist.key
                self.version = dist.version
                self.requires = dist.requires()
            except:
                self.name = None
                self.version = None
                self.requires = None
            return super().__init__(*args, **kwargs)
    return Result()

def get_type_of_script():
    """
    Return of the type of the script is being executed
    """
    try:
        try:
            import google.colab
            return 'colab'
        except: pass
        if ('ipykernel_launcher.py' in sys.argv[0]):
            return 'jupyter'
        return 'ipython'
    except:
        if ('python' in Path(sys.executable).name.lower()):
            return 'terminal'
        else:
            return "executable"

def install(package: str, extra_args: []):
    """
    Launch the installer process
    """
    script_args = ['-m', 'pip', 'install']
    if (extra_args):
        script_args.extend(extra_args)
    script_args.append(package)
    execute_script(script_args)

def is_colab():
    """
    True if running as an executable
    """
    return get_type_of_script() == 'colab'

def is_executable():
    """
    True if running as an executable
    """
    return get_type_of_script() == 'executable'

def is_ipython():
    """
    True if running in an ipython environment
    """
    return get_type_of_script() == 'ipython'

def is_jupyter():
    """
    True if running in a jupyter notebook
    """
    return get_type_of_script() == 'jupyter'

def is_terminal():
    """
    True if running a terminal environment
    """
    return get_type_of_script() == 'terminal'

def uninstall(package: str):
    """
    Launch the uninstaller process
    """
    execute_script(['-m', 'pip', 'uninstall', '-y', package])

if __name__ == '__main__':
    print('Utilities functions initialized')
