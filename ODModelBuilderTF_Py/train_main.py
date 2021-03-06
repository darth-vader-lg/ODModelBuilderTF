from    absl import flags
import  os
import  sys

try:    from    utilities import *
except: pass

# Avoiding the absl error for duplicated flags if run again the cell from a notebook
allow_flags_override();

# Flags for arguments parameters
flags.DEFINE_string ('model_type', None, 'Type of the base model.')
flags.DEFINE_string ('train_images_dir', None, 'Path to the directory '
                     'containing the images for train and their labeling xml.')
flags.DEFINE_string ('eval_images_dir', None, 'Path to the directory '
                     'containing the images for evaluate and their labeling xml.')
flags.DEFINE_string ('annotations_dir', None, 'Path to the directory '
                     'containing the computed tensorflow records.')
flags.DEFINE_string ('pre_trained_model_dir', None, 'Path to the directory '
                     'containing the pre-trained model.')
flags.DEFINE_integer('batch_size', 0, 'The size of batch. If < 1 it uses the '
                     'value contained in the pipeline configuration file.')
flags.DEFINE_integer('tensorboard_port', 8080, 'The port of the tensorboard server')

def train_main(unused_argv, **kwargs):
    """Main function for the train.
    Args:
        unused_argv:                /
        kwargs:                     extra argments
            step_callback:          a function called each train step.
            checkpoint_callback:    a function called after each checkpoint saving.
    """
    # Init the train environment
    from pretrained_model import download_pretrained_model
    from tf_records import create_tf_records
    from train_environment import init_train_environment
    from train_parameters import TrainParameters
    from train_pipeline import config_train_pipeline
    train_parameters = TrainParameters()
    train_parameters.update_values()
    # Check if the number of train steps is 0
    if (train_parameters.num_train_steps == 0 or not train_parameters.model_dir):
        print('Train parameters not set. Skipping.')
        return
    init_train_environment(train_parameters)
    if (not train_parameters.pre_trained_model_dir):
        download_pretrained_model(train_parameters)
    create_tf_records(train_parameters)
    config_train_pipeline(train_parameters)
    # Import the train main function
    from object_detection import model_main_tf2
    train_parameters.update_flags()
    if (train_parameters.num_train_steps < 0):
        setattr(flags.FLAGS, 'num_train_steps', None)
    # Start the tensorboard
    from train_tensorboard import start_tensorboard, stop_tensorboard
    tb_process = start_tensorboard(train_parameters)
    # Execute the train
    try:
        model_main_tf2.main(unused_argv, **kwargs)
    finally:
        # Stop the tensorboard
        stop_tensorboard(tb_process)

if __name__ == '__main__':
    if (not is_executable()):
        from install_virtual_environment import install_virtual_environment
        install_virtual_environment()
    try:
        # import the module here just for having the flags defined
        allow_flags_override()
        from object_detection import model_main_tf2
        # Run the train main
        import tensorflow as tf
        tf.compat.v1.app.run(train_main)
    except KeyboardInterrupt:
        if (not is_executable()):
            print('Train interrupted by user')
    except SystemExit:
        if (not is_executable()):
            print('Train complete')
    else:
        if (not is_executable()):
            print('Train complete')
