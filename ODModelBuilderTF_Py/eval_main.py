from    absl import flags
import  os
import  sys

try:    from    utilities import *
except: pass

# Avoiding the absl error for duplicated flags if run again the cell from a notebook
allow_flags_override();

def eval_main(unused_argv, **kwargs):
    """Main function for the evaluation.
    Args:
        unused_argv:                /
        kwargs:                     extra argments
            eval_callback:          a function called each new evaluation.
            eval_timeout_callback:  a function called after the timeout without new checkpoints.
    """
    # Init the evaluation environment
    from eval_environment import init_eval_environment
    from eval_parameters import EvalParameters
    eval_parameters = EvalParameters()
    eval_parameters.update_values()
    if (not eval_parameters.checkpoint_dir):
        print('Evaluation parameters not set. Skipping.')
        return
    init_eval_environment(eval_parameters)
    # Import the eval main function
    from object_detection import model_main_tf2
    eval_parameters.update_flags()
    # Execute the evaluation
    model_main_tf2.main(unused_argv, **kwargs)

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
        tf.compat.v1.app.run(eval_main)
    except KeyboardInterrupt:
        if (not is_executable()):
            print('Evaluation interrupted by user')
    except SystemExit:
        if (not is_executable()):
            print('Evaluation complete')
    else:
        if (not is_executable()):
            print('Evaluation complete')
