import  os
from    pathlib import Path
import  shutil
import  sys

try:    from    default_cfg import Cfg
except: pass
try:    from    export_parameters import ExportParameters
except: pass

def init_export_environment(prm: ExportParameters):
    """
    Initialize the model export environment with the right directories structure.
    Keyword arguments:
    prm     -- the export parameters
    """
    # Check if the export directory is specified
    if (not prm.output_directory):
        print('Warning: export output directory is not specified so nothing will be exported as saved_model')
        return;
    # Set the configuration for Google Colab
    if (os.path.isdir('/content') and os.path.isdir('/mnt/MyDrive')):
        # Check the existence of the checkpoints directory
        if (prm.trained_checkpoint_dir):
            gdrive_dir = os.path.join('/mnt', 'MyDrive', prm.trained_checkpoint_dir)
            if (not os.path.isdir(gdrive_dir)):
                raise Exception('Error!!! The trained checkpoint directory doesn`t exist')
            if (not os.path.exists('/content/trained-model')):
                os.symlink(gdrive_dir, '/content/trained-model', True)
            print(f"Google drive's {prm.trained_checkpoint_dir} is linked to /content/trained-model")
            prm.trained_checkpoint_dir = '/content/trained-model'
        # Check the existence of the output directory
        gdrive_dir = os.path.join('/mnt', 'MyDrive', prm.output_directory)
        if (not os.path.isdir(gdrive_dir)):
            print('Creating the output directory')
            os.mkdir(gdrive_dir)
        if (prm.model_dir and str(Path(prm.output_directory).resolve()) == str(Path(prm.model_dir).resolve())):
            raise Exception("Error: export directory cannot be the train directory")
        if (not os.path.exists('/content/exported-model')):
            os.symlink(gdrive_dir, '/content/exported-model', True)
        print(f"Google drive's {prm.output_directory} is linked to /content/exported-model")
        prm.output_directory = '/content/exported-model'
    else:
        if (prm.trained_checkpoint_dir):
            if (not os.path.isdir(prm.trained_checkpoint_dir)):
                raise Exception('Error!!! The trained checkpoint dir doesn`t exist')
            print(f'Trained checkpoint directory from {str(Path(prm.trained_checkpoint_dir).resolve())}')
        if (not os.path.exists(prm.output_directory)):
            print('Creating the output directory')
            os.mkdir(prm.output_directory)
        if (prm.model_dir and str(Path(prm.output_directory).resolve()) == str(Path(prm.model_dir).resolve())):
            raise Exception("Error: export directory cannot be the train directory")
        print(f'The exported model will be in {str(Path(prm.output_directory).resolve())}')
    # Copy the label file in the export directory
    try:
        if (prm.trained_checkpoint_dir):
            shutil.copy2(os.path.join(prm.trained_checkpoint_dir, 'label_map.pbtxt'), prm.output_directory)
    except:
        print(f"Warning: the file {os.path.join(prm.trained_checkpoint_dir, 'label_map.pbtxt')} doesn't exist and will not be copied to the output")

if __name__ == '__main__':
    prm = ('prm' in locals() and isinstance(prm, ExportParameters) and prm) or ExportParameters.default
    init_export_environment(prm)
