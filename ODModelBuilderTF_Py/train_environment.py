import  os
from    pathlib import Path
import  sys

try:    from    default_cfg import Cfg
except: pass
try:    from    train_parameters import TrainParameters
except: pass

def init_train_environment(prm: TrainParameters):
    """
    Initialize the training environment with the right directories structure.
    Keyword arguments:
    prm     -- the train parameters
    """
    # Set the configuration for Google Colab
    if (os.path.isdir('/content') and os.path.isdir('/mnt/MyDrive')):
        # Check the existence of the train images dir
        gdrive_dir = os.path.join('/mnt', 'MyDrive', prm.train_images_dir)
        if (not os.path.isdir(gdrive_dir)):
            raise Exception('Error!!! The train images dir doesn`t exist')
        if (not os.path.exists('/content/train-images')):
            os.symlink(gdrive_dir, '/content/train-images', True)
        print(f"Google drive's {prm.train_images_dir} is linked to /content/train-images")
        prm.train_images_dir = '/content/train-images'
        # Check the existence of the evaluation images dir
        gdrive_dir = os.path.join('/mnt', 'MyDrive', prm.eval_images_dir)
        if (not os.path.isdir(gdrive_dir)):
            raise Exception('Error!!! The evaluation images dir doesn`t exist')
        if (not os.path.exists('/content/eval-images')):
            os.symlink(gdrive_dir, '/content/eval-images', True)
        print(f"Google drive's {prm.eval_images_dir} is linked to /content/eval-images")
        prm.eval_images_dir = '/content/eval-images'
        # Check the existence of the output directory
        gdrive_dir = os.path.join('/mnt', 'MyDrive', prm.model_dir)
        if (not os.path.isdir(gdrive_dir)):
            print('Creating the output directory')
            os.mkdir(gdrive_dir)
        if (not os.path.exists('/content/trained-model')):
            os.symlink(gdrive_dir, '/content/trained-model', True)
        print(f"Google drive's {prm.model_dir} is linked to /content/trained-model")
        prm.model_dir = '/content/trained-model'
        if (not prm.annotations_dir):
            prm.annotations_dir = "annotations"
        if (not os.path.exists(prm.annotations_dir)):
            if (not prm.annotations_dir.startswith('/')):
                prm.annotations_dir = os.path.join('/content', prm.annotations_dir)
            if (not os.path.exists(prm.annotations_dir)):
                os.mkdir(prm.annotations_dir)
        print(f'The annotations files will be in {str(Path(prm.annotations_dir).resolve())}')
    else:
        if (not os.path.isdir(prm.train_images_dir)):
            raise Exception('Error!!! The train images dir doesn`t exist')
        print(f'Train images from {str(Path(prm.train_images_dir).resolve())}')
        if (not os.path.isdir(prm.eval_images_dir)):
            raise Exception('Error!!! The evaluation images dir doesn`t exist')
        print(f'Train images from {str(Path(prm.eval_images_dir).resolve())}')
        if (not os.path.exists(prm.model_dir)):
            print('Creating the output directory')
            os.mkdir(prm.model_dir)
        print(f'The trained model will be in {str(Path(prm.model_dir).resolve())}')
        if (not prm.annotations_dir):
            prm.annotations_dir = "annotations"
        if (not os.path.exists(prm.annotations_dir)):
            os.mkdir(prm.annotations_dir)
        print(f'The annotations files will be in {str(Path(prm.annotations_dir).resolve())}')

if __name__ == '__main__':
    prm = ('prm' in locals() and isinstance(prm, TrainParameters) and prm) or TrainParameters.default
    init_train_environment(prm)
