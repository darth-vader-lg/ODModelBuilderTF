# Script for installing the Python virtual environment
import  sys

# The name of the virtual environment
env_name = 'env'

def check_requirements(requirements='requirements.txt', exclude=()):
    # Check installed dependencies meet requirements (pass *.txt file or list of packages)
    import pkg_resources as pkg
    from pathlib import Path
    if isinstance(requirements, (str, Path)):  # requirements.txt file
        file = Path(requirements)
        if not file.exists():
            print(f"{file.resolve()} not found, check failed.")
            return
        requirements = [f'{x.name}{x.specifier}' for x in pkg.parse_requirements(file.open()) if x.name not in exclude]
    else:  # list or tuple of packages
        requirements = [x for x in requirements if x not in exclude]

    n = 0  # number of packages updates
    for r in requirements:
        try:
            pkg.require(r)
        except Exception as e:  # DistributionNotFound or VersionConflict if requirements not met
            n += 1
    return n

def install_virtual_environment(env_name: str = env_name):
    """
    Install the virtual environment.
    Keyword arguments:
    env_name    -- the name of the virtual environment
    """
    import  os
    from    pathlib import Path
    import  subprocess
    # Creation of the virtual environment
    env_name = str(Path(env_name).absolute().resolve())
    script_dir = 'Scripts' if ('win32' in sys.platform) else 'bin'
    force_install_requirements = False
    from utilities import is_colab
    if (not is_colab()):
        if (not os.path.isdir(env_name)):
            print('Creating the Python virtual environment')
            try:
                from utilities import execute_script
                execute_script(['-m', 'venv', env_name])
                force_install_requirements = True
            except subprocess.CalledProcessError as exc:
                return exc.returncode
        # Adjust the environment paths
        if (os.path.dirname(sys.executable).lower() != os.path.join(env_name, script_dir).lower()):
            sys.executable = os.path.join(env_name, script_dir, os.path.basename(sys.executable))
            paths = [
                os.path.abspath(os.path.join(env_name, 'Lib', 'site-packages')),
                os.path.abspath(os.path.join(env_name, 'lib', 'python3.7', 'site-packages')),
                os.path.abspath(os.path.join(env_name, 'Scripts')),
                os.path.abspath(os.path.join(env_name, 'bin')),
                ]
            for path in paths:
                if (not path in sys.path):
                    sys.path.insert(0, path)
    # Installation of the requirements
    from utilities import execute_script, install, get_package_info
    if (force_install_requirements or (os.path.isfile('requirements.txt') and check_requirements('requirements.txt') > 0)):
        print('Installing the requirements')
        try:
            execute_script(['-m', 'pip', 'install', '--no-cache-dir', '--upgrade', '-r', 'requirements.txt'])
        except subprocess.CalledProcessError as exc:
            return exc.returncode
    # Install the object detection environment
    if (not get_package_info('object-detection').version):
        print('Installing the object detection API')
        try:
            from od_install import install_object_detection
            install_object_detection()
        except subprocess.CalledProcessError as exc:
            return exc.returncode
    return 0

if __name__ == '__main__':
    sys.exit(install_virtual_environment(env_name))
