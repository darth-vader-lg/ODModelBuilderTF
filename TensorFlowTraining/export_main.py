# Module: export_main.py
#@title #Export { form-width: "20%" }
#@markdown The main train loop. It trains the model and put it in the output directory.
#@markdown 
#@markdown It can be stopped before the completion when
#@markdown a considerable result is reached and restart after for enhancing the tuning.

from    absl import flags
import  os
import  sys

try:    from    utilities import *
except: pass

# Avoiding the absl error for duplicated flags if run again the cell from a notebook
for f in flags.FLAGS.flag_values_dict():
    flags.FLAGS[f].allow_override = True

def export_main(unused_argv):
    # Part of code not executed on Colab notebook
    def run_py_mode():
        # Init the train environment
        from export_environment import init_export_environment
        from export_parameters import ExportParameters
        export_parameters = ExportParameters()
        export_parameters.update_values()
        init_export_environment(export_parameters)
        # Import the export main function
        from object_detection import exporter_main_v2
        export_parameters.update_flags()
        # Export the model
        exporter_main_v2.main(unused_argv)
    def run_notebook_mode():
        # Import the train main function
        from object_detection import exporter_main_v2
        prm.update_flags()
        # Execute the train
        exporter_main_v2.main(unused_argv)
    # Execution
    if (is_jupyter()):
        run_notebook_mode()
    else:
        run_py_mode()

if __name__ == '__main__':
    if (not is_jupyter()):
        from od_install import install_object_detection
        install_object_detection()
    import tensorflow as tf
    try:
        tf.compat.v1.app.run(export_main)
    except SystemExit:
        pass
    print('Export complete')

#@markdown ---