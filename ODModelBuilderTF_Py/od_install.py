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

def install_object_detection(no_cache=True, no_deps=True, custom_tf_dir=None):
    """
    Install a well known environment.
    """
    install_extra_args = []
    if (no_cache):
        install_extra_args.append('--no-cache')
    if (no_deps):
        install_extra_args.append('--no-deps')
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
    else:
        print(f'TensorFlow {Cfg.tensorflow_version} is already installed')
    # Install pygit2
    if (not get_package_info('pygit2').name):
        install('pygit2==1.5.0', install_extra_args)
    else:
        print('pygit2 is already installed')
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
    od_api_dir = os.path.join(tempfile.gettempdir(), 'tf-od-api-' + Cfg.od_api_git_sha1)
    # Install the object detection api
    is_installed = False
    try:
        if (get_package_info('object-detection').version):
            repo = pygit2.Repository(od_api_dir)
            if (repo.head.target.hex == Cfg.od_api_git_sha1):
                is_installed = True
    except: pass
    # Install the TensorFlow models
    if (not is_installed):
        try:
            repo = pygit2.Repository(od_api_dir)
        except:
            # Create the callback for the progress
            callbacks = GitCallbacks();
            # Clone the TensorFlow models repository
            print('Cloning the TensorFlow object detection api repository')
            pygit2.clone_repository('https://github.com/tensorflow/models.git', od_api_dir, callbacks = callbacks)
            print('TensorFlow object detection api repository cloned')
            repo = pygit2.Repository(od_api_dir)
        # Checkout the well known commit
        print(f'Checkout of the object detection api repository at the commit {Cfg.od_api_git_sha1}')
        (commit, reference) = repo.resolve_refish(Cfg.od_api_git_sha1)
        try:
            repo.checkout_tree(commit)
        except: pass
        repo.reset(pygit2.Oid(hex=Cfg.od_api_git_sha1), pygit2.GIT_RESET_HARD)
        # Move to the research dir
        currentDir = os.getcwd()
        os.chdir(os.path.join(od_api_dir, 'research'))
        # Install the protobuf tools
        if (not get_package_info('grpcio-tools').name):
            tf_package_info = get_package_info('tensorflow')
            for rq in tf_package_info.requires:
                if (rq.key == "grpcio"):
                    install(str(rq).replace('grpcio', 'grpcio-tools'), install_extra_args)
                    break
            if (not get_package_info('grpcio-tools').name):
                raise Exception('Error installing grpcio-tools')
        else:
            print('grpcio-tools is already installed')
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
        with open('./setup.py', 'r') as f:
            lines = f.readlines()
            for i in range(len(lines)):
                if ('tf-models-official' in lines[i]):
                    if (tf_comparing_version.lstrip().startswith('2.4')):
                        lines[i] = str(lines[i]).replace('tf-models-official', 'tf-models-official==2.4.0')
        with open('./setup.py', 'w') as f:
            f.writelines(lines)
        install('.', install_extra_args)
        # Uninstall the dataclasses package installed erroneusly (incompatible) for python >=3.7 by tf-models-official
        try:
            import pkg_resources, importlib
            importlib.reload(pkg_resources)
            pkg_resources.require('dataclasses')
            uninstall('dataclasses')
        except Exception as e: pass
        # Return to the original directory
        os.chdir(currentDir)
    else:
        print(f'TensorFlow object detection api SHA-1 {Cfg.od_api_git_sha1} is already installed')
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
    tf2onnx_git_sha1 = '596f23741b1b5476e720089ed0dfd5dbcc5a44d0'
    tf2onnx_dir = os.path.join(tempfile.gettempdir(), 'tensorflow-onnx-' + tf2onnx_git_sha1)
    # Install the onnx converter
    is_installed = False
    try:
        if (get_package_info('tensorflow-onnx').version):
            repo = pygit2.Repository(tf2onnx_dir)
            if (repo.head.target.hex == tf2onnx_git_sha1):
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
        print(f'Checkout of the onnx converter repository at the commit {tf2onnx_git_sha1}')
        (commit, reference) = repo.resolve_refish(tf2onnx_git_sha1)
        try:
            repo.checkout_tree(commit)
        except: pass
        repo.reset(pygit2.Oid(hex=tf2onnx_git_sha1), pygit2.GIT_RESET_HARD)
        # Move to the onnx converter dir
        currentDir = os.getcwd()
        os.chdir(tf2onnx_dir)
        # Install the converter
        install('.', install_extra_args)  # TODO: Install the package from GitHub and try with release 1.9.0
        # Return to the original directory
        os.chdir(currentDir)
    else:
        print(f'Onnx converter SHA-1 {tf2onnx_git_sha1} is already installed')

    print('Installation ok.')

if __name__ == '__main__':
    install_object_detection()
