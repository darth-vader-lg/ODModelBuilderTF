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

def install_object_detection():
    """
    Install a well known environment.
    """
    # Install TensorFlow
    is_installed = False
    try:
        comparing_version = Cfg.tensorflow_version.replace('tensorflow==', '')
        comparing_version = comparing_version.replace('tf-nightly==', '')
        comparing_version = comparing_version.replace('.dev', '-dev')
        is_installed = get_package_info('tensorflow').version == comparing_version
    except: pass
    if (not is_installed):
        install(Cfg.tensorflow_version)
    else:
        print(f'TensorFlow {Cfg.tensorflow_version} is already installed')
    # Install pygit2
    if (not get_package_info('pygit2').name):
        install('pygit2==1.5.0')
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
            install('grpcio-tools>=1.32.0')
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
        install('.')
        # Uninstall this package installed by someother one because incompatible with python >=3.7
        try:
            import pkg_resources as pkg
            pkg.require('dataclasses')
            execute_script(['-m', 'pip', 'uninstall', '-y', 'dataclasses'])
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
    tf2onnx_dir = os.path.join(tempfile.gettempdir(), 'tfensorflow-onnx-' + tf2onnx_git_sha1)
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
        install('.')  # TODO: Install the package from GitHub and try with release 1.9.0
        # Return to the original directory
        os.chdir(currentDir)
    else:
        print(f'Onnx converter SHA-1 {tf2onnx_git_sha1} is already installed')

    print('Installation ok.')

if __name__ == '__main__':
    install_object_detection()
