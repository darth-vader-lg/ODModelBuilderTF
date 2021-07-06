import  os
from    pathlib import Path
import  sys

try:    from    default_cfg import Cfg
except: pass
try:    from    eval_parameters import EvalParameters
except: pass

def init_eval_environment(prm: EvalParameters):
    """
    Initialize the eval environment with the right directories structure.
    Keyword arguments:
    prm     -- the eval parameters
    """
    # Set the configuration for Google Colab
    if (os.path.isdir('/content') and os.path.isdir('/mnt/MyDrive')):
        # Check the existence of the output directory
        gdrive_dir = os.path.join('/mnt', 'MyDrive', prm.checkpoint_dir)
        if (not os.path.isdir(gdrive_dir)):
            raise Exception('Error!!! The checkpoint dir doesn`t exist')
        if (not os.path.exists('/content/trained-model')):
            os.symlink(gdrive_dir, '/content/trained-model', True)
        print(f"Google drive's {prm.checkpoint_dir} is linked to /content/trained-model")
        prm.checkpoint_dir = '/content/trained-model'
    else:
        if (not os.path.exists(prm.checkpoint_dir)):
            raise Exception('Error!!! The checkpoint dir doesn`t exist')
        print(f'The evaluated model is in {str(Path(prm.checkpoint_dir).resolve())}')
    if (not prm.model_dir):
        prm.model_dir = prm.checkpoint_dir
    if (not prm.pipeline_config_path and os.path.exists(prm.model_dir) and os.path.exists(os.path.join(prm.model_dir, "pipeline.config"))):
        prm.pipeline_config_path = os.path.join(prm.model_dir, "pipeline.config")
    if (not prm.pipeline_config_path):
        raise Exception("Error!!! Undefined pipeline configuration file")
    if (not os.path.exists(prm.pipeline_config_path)):
        raise Exception("Error!!! The pipeline configuration file doesn`t exist")

if __name__ == '__main__':
    prm = ('prm' in locals() and isinstance(prm, EvalParameters) and prm) or EvalParameters.default
    init_eval_environment(prm)
