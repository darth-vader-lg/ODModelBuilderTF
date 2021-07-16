# Script for installing the Python virtual environment
import  sys

# The name of the virtual environment
env_name = 'env'

def check_requirements(requirements='requirements.txt', exclude=(), no_deps=True):
    # Check installed dependencies meet requirements (pass *.txt file or list of packages)
    import pkg_resources, importlib
    importlib.reload(pkg_resources)
    from pathlib import Path
    working_set = pkg_resources.WorkingSet() if no_deps else None
    if isinstance(requirements, (str, Path)):  # requirements.txt file
        file = Path(requirements)
        if not file.exists():
            print(f"{file.resolve()} not found, check failed.")
            return
        requirements = [f'{x.name}{x.specifier}' for x in pkg_resources.parse_requirements(file.open()) if x.name not in exclude]
    else:  # list or tuple of packages
        requirements = [x for x in requirements if x not in exclude]

    missing = [] # missing packages
    for r in requirements:
        try:
            if (working_set):
                rq = pkg_resources.Requirement(r)
                if (not working_set.find(rq)):
                    missing.append(rq)
            else:
                pkg_resources.require(r)
        except Exception as e:  # DistributionNotFound or VersionConflict if requirements not met
            missing.append(r)
    return missing

def install_virtual_environment(env_name: str=env_name, no_cache=True, no_deps=True, no_requirements=False, custom_tf_dir=None):
    """
    Install the virtual environment.
    Keyword arguments:
    env_name        -- the name of the virtual environment
    no_cache        -- disable caching of the packages
    no_deps         -- disable installation of packages dependencies
    custom_tf_dir   -- directory containing special tensorflow builds
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
                force_install_requirements = not no_requirements
            except subprocess.CalledProcessError as exc:
                return exc.returncode
        # Adjust the environment paths
        executable_dir = os.path.dirname(sys.executable).lower()
        executable_name = os.path.basename(sys.executable).lower()
        if (executable_dir != os.path.join(env_name, script_dir).lower() and executable_dir != env_name.lower()):
            if (os.path.isfile(os.path.join(env_name, executable_name))):
                sys.executable = os.path.join(env_name, executable_name)
            else:
                sys.executable = os.path.join(env_name, script_dir, executable_name)
        paths = [
            os.path.abspath(os.path.join(env_name, 'DLLs')),
            os.path.abspath(os.path.join(env_name, 'Lib', 'site-packages')),
            os.path.abspath(os.path.join(env_name, 'lib', 'python3.7', 'site-packages')),
            os.path.abspath(os.path.join(env_name, 'Scripts')),
            os.path.abspath(os.path.join(env_name, 'bin')),
            os.path.abspath(os.path.join(env_name)),
            ]
        for path in paths:
            if (not path.lower() in [lc.lower() for lc in sys.path]):
                sys.path.insert(0, path)
    # Base packages installation function
    from utilities import install, get_package_info
    base_packages_installed = False
    def _install_base_packages():
        nonlocal base_packages_installed
        if (base_packages_installed):
            return
        install('pip', ['--upgrade', '-c', 'constraints.txt'])
        base_packages_installed = True
    # Check of missing packages
    missing = check_requirements(requirements='requirements.txt', no_deps=no_deps) if (os.path.isfile('requirements.txt') and not no_requirements) else []
    # Installation of the requirements
    if (force_install_requirements or len(missing) > 0):
        if (len(missing) > 0):
            print('Missing requirements:')
            for r in missing:
                print(r)
        print('Installing the requirements.')
        try:
            _install_base_packages()
            install_args = []
            if (no_cache):
                install_args.append('--no-cache')
            if (no_deps):
                install_args.append('--no-deps')
            install_args.extend(['-r', 'requirements.txt', '-c', 'constraints.txt'])
            install(None, install_args)
        except subprocess.CalledProcessError as exc:
            print ("Error!!!")
            print(exc)
            return exc.returncode
    # Install the object detection environment
    if (len(check_requirements(requirements=['object-detection'], no_deps=True)) > 0):
        print('Installing the object detection API')
        try:
            _install_base_packages()
            from od_install import install_object_detection
            install_object_detection(no_cache=no_cache, custom_tf_dir=custom_tf_dir)
        except subprocess.CalledProcessError as exc:
            print("Error! Couldn't install object detection api.")
            print(exc)
            return exc.returncode
        if (len(check_requirements(requirements=['object-detection'], no_deps=True)) > 0):
            print("Error! Couldn't install object detection api.")
            return -1

    # Uninstall the dataclasses package installed erroneusly (incompatible) for python >=3.7 by tf-models-official
    try:
        import pkg_resources, importlib
        importlib.reload(pkg_resources)
        pkg_resources.require('dataclasses')
        execute_script(['-m', 'pip', 'uninstall', '-y', 'dataclasses'])
    except Exception as e: pass
    return 0

if __name__ == '__main__':
    import  argparse
    parser = argparse.ArgumentParser()
    parser.add_argument(
        '--custom-tf-dir',
        dest='custom_tf_dir',
        help='Directory containing wheels for special cuda customized tensorflow.'
    )
    parser.add_argument(
        '--no-requirements',
        dest='no_requirements',
        action='store_true',
        help='Bypass the requirements installation. To discover any dependency from scratch'
    )
    args = parser.parse_args()
    from pathlib import Path
    sys.exit(install_virtual_environment(env_name, no_cache=False, no_deps=not args.no_requirements, no_requirements=args.no_requirements, custom_tf_dir=args.custom_tf_dir))
