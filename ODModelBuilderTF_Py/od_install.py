import  os
import  datetime
from    pathlib import Path
import  shutil
import  sys
import  tempfile

try:    from    default_cfg import Cfg
except: pass
try:    from    utilities import *
except: pass

def install_object_detection(requirements:str=None, no_cache=True, custom_tf_dir=None):
    """
    Install a well known environment.
    """
    install_extra_args = []
    if (no_cache):
        install_extra_args.append('--no-cache')
    if (requirements):
        install_extra_args.append('-c')
        install_extra_args.append(os.path.abspath(requirements))

    # List of packages to check at the end of installation
    packages_to_check = []
        
    # Install pycocotools. It must be installed as first for some problems building on Windows.
    if (not get_package_info('pycocotools').name):
        # For some reasons the package doesn't function if installed by wheel on Windows.
        # So temporary uninstall wheel
        reinstall_wheel = False
        if (get_package_info('wheel').name):
            uninstall('wheel')
            reinstall_wheel = True
        install('pycocotools', install_extra_args)
        packages_to_check.append('pycocotools')
        # Reinstall wheel
        if (reinstall_wheel):
            install('wheel', install_extra_args)
            packages_to_check.append('wheel')

    # Install TensorFlow
    is_installed = False
    try:
        tf_comparing_version = Cfg.tensorflow_version.replace('tensorflow==', '')
        tf_comparing_version = tf_comparing_version.replace('tf-nightly==', '')
        tf_comparing_version = tf_comparing_version.replace('.dev', '-dev')
        is_installed = get_package_info('tensorflow').version == tf_comparing_version
    except: pass
    if (not is_installed):
        tensorflow_package = Cfg.tensorflow_version
        if (custom_tf_dir and Path(custom_tf_dir).is_dir()):
            try:
                # Read CUDA version
                import subprocess
                output = subprocess.check_output(['nvcc', '--version'], shell=True).decode()
                if ('V10.1' in output):
                    found = [f for f in Path(custom_tf_dir).glob('*.whl') if tf_comparing_version in Path(f).stem]
                    if (len(found) == 1):
                        tensorflow_package = str(found[0])
                        print(f'Info: installing a custom tensorflow located at {tensorflow_package}')
                    else:
                        print(f'Warning: couldn\'t find the special version of tensorflow-{tf_comparing_version}')
                        print('Installing the standard.')
            except:
                print(f'Warning: couldn\'t find cuda')
                print('Installing the standard tensorflow.')
        install(tensorflow_package, install_extra_args)
        packages_to_check.append('tensorflow')
    
    # Install pygit2
    if (not get_package_info('pygit2').name):
        install('pygit2', install_extra_args)
    import pygit2
    # Progress class for the git output
    class GitCallbacks(pygit2.RemoteCallbacks):
        def __init__(self, credentials=None, certificate=None):
            self.dateTime = datetime.datetime.now()
            return super().__init__(credentials=credentials, certificate=certificate)
        def transfer_progress(self, stats):
            now = datetime.datetime.now()
            if ((now - self.dateTime).total_seconds() > 1):
                print('\rReceiving... Deltas [%d / %d], Objects [%d / %d]'%(stats.indexed_deltas, stats.total_deltas, stats.indexed_objects, stats.total_objects), end='', flush=True)
                self.dateTime = now
            if (stats.received_objects >= stats.total_objects and stats.indexed_objects >= stats.total_objects and stats.indexed_deltas >= stats.total_deltas):
                print('\r\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\rDone Deltas %d, Objects %d.'%(stats.total_objects, stats.total_objects))
            return super().transfer_progress(stats)
    
    # Directory of the TensorFlow object detection api and commit id
    if (os.path.isdir(Cfg.od_api_git_repo)):
        od_api_dir = Cfg.od_api_git_repo
    else:
        od_api_dir = os.path.join(tempfile.gettempdir(), 'tf-od-api-' + Cfg.od_api_git_ref.replace('/', '_'))
    # Install the object detection api
    is_installed = False
    try:
        if (get_package_info('object-detection').version):
            repo = pygit2.Repository(od_api_dir)
            if ((od_api_dir == Cfg.od_api_git_repo) or (repo.head.target.hex == repo.resolve_refish(Cfg.od_api_git_ref).oid.hex)):
                is_installed = True
    except: pass
    # Install the TensorFlow models
    if (not is_installed):
        # Install from git
        try:
            repo = pygit2.Repository(od_api_dir)
        except:
            # Create the callback for the progress
            callbacks = GitCallbacks();
            # Clone the TensorFlow models repository
            print('Cloning the TensorFlow object detection api repository')
            pygit2.clone_repository(Cfg.od_api_git_repo, od_api_dir, callbacks = callbacks)
            print('TensorFlow object detection api repository cloned')
            repo = pygit2.Repository(od_api_dir)
        # Checkout the well known commit
        print(f'Checkout of the object detection api repository at {Cfg.od_api_git_ref}')
        (commit, reference) = repo.resolve_refish(Cfg.od_api_git_ref)
        try:
            repo.checkout_tree(commit)
        except: pass
        repo.reset(commit.oid, pygit2.GIT_RESET_HARD if od_api_dir != Cfg.od_api_git_repo else pygit2.GIT_RESET_SOFT)
        # Move to the research dir
        currentDir = os.getcwd()
        os.chdir(os.path.join(od_api_dir, 'research'))
        # Install the protobuf tools
        if (not get_package_info('grpcio-tools').name):
            install('grpcio-tools', install_extra_args)
        # Compile the protobufs
        print(f'Compiling the protobufs')
        import grpc_tools.protoc as protoc
        protoFiles = Path('object_detection/protos').rglob('*.proto')
        for protoFile in protoFiles:
            protoFilePath = str(protoFile)
            print('Compiling', protoFilePath)
            protoc.main(['grpc_tools.protoc', '--python_out=.', protoFilePath])
        # Install the object detection packages
        print(f'Installing the object detection api.')
        shutil.copy2('object_detection/packages/tf2/setup.py', '.')
        install('.', install_extra_args)
        packages_to_check.append('object-detection')
        # Uninstall the dataclasses package installed erroneusly (incompatible) for python >=3.7 by tf-models-official
        try:
            import pkg_resources, importlib
            importlib.reload(pkg_resources)
            pkg_resources.require('dataclasses')
            uninstall('dataclasses')
        except Exception as e: pass
        # Return to the original directory
        os.chdir(currentDir)
    
    # Append of the paths
    paths = [
        os.path.join(od_api_dir, 'research'),
        os.path.join(od_api_dir, 'research', 'slim'),
        os.path.join(od_api_dir, 'research', 'object_detection'),
        ]
    for path in paths:
        if (not path in sys.path):
            sys.path.append(path)

    # Directory of the onnx converter and commit id
    if (not Cfg.tf2onnx_git_repo or not Cfg.tf2onnx_git_ref):
        if (not get_package_info('tf2onnx').name):
            install('tf2onnx', install_extra_args)
            packages_to_check.append('tf2onnx')
    else:
        if (os.path.isdir(Cfg.tf2onnx_git_repo)):
            tf2onnx_dir = Cfg.tf2onnx_git_repo
        else:
            tf2onnx_dir = os.path.join(tempfile.gettempdir(), 'tensorflow-onnx-' + Cfg.tf2onnx_git_ref.replace('/', '_'))
        # Install the onnx converter
        is_installed = False
        try:
            if (get_package_info('tf2onnx').version):
                repo = pygit2.Repository(tf2onnx_dir)
                if ((od_api_dir == Cfg.od_api_git_repo) or (repo.head.target.hex == Cfg.tf2onnx_git_ref)):
                    is_installed = True
        except: pass
        # Install the onnx converter
        if (not is_installed):
            try:
                repo = pygit2.Repository(tf2onnx_dir)
            except:
                # Create the callback for the progress
                callbacks = GitCallbacks();
                # Clone the TensorFlow models repository
                print('Cloning the onnx converter repository')
                pygit2.clone_repository('https://github.com/onnx/tensorflow-onnx.git', tf2onnx_dir, callbacks = callbacks)
                print('Onnx converter repository cloned')
                repo = pygit2.Repository(tf2onnx_dir)
            # Checkout the well known commit
            print(f'Checkout of the onnx converter repository at {Cfg.tf2onnx_git_ref}')
            (commit, reference) = repo.resolve_refish(Cfg.tf2onnx_git_ref)
            try:
                repo.checkout_tree(commit)
            except: pass
            repo.reset(commit.oid, pygit2.GIT_RESET_HARD if tf2onnx_dir != Cfg.tf2onnx_git_repo else pygit2.GIT_RESET_SOFT)
            # Move to the onnx converter dir
            currentDir = os.getcwd()
            os.chdir(tf2onnx_dir)
            # Install the converter
            install('.', install_extra_args)  # TODO: Install the package from GitHub and try with release 1.9.0
            packages_to_check.append('tf2onnx')
            # Return to the original directory
            os.chdir(currentDir)

    # Check for correct installation
    wrong_installations = []
    for pkg in packages_to_check:
        try:
            if (not get_package_info(pkg, no_deps=is_colab()).name):
                wrong_installations.append(pkg)
        except:
            wrong_installations.append(pkg)
    for pkg in wrong_installations:
        print(f'Installation error for package {pkg}')
    if (len(wrong_installations) > 0):
        raise Exception('Object detection installation error.')
    print('Object detection environment installed successfully.')

if __name__ == '__main__':
    install_object_detection(os.path.join(os.path.dirname(__file__), 'requirements.txt'))
