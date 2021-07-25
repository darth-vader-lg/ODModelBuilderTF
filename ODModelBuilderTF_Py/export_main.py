from    absl import flags
import  os
import  sys

try:    from    utilities import *
except: pass

# Avoiding the absl error for duplicated flags if run again the cell from a notebook
allow_flags_override()

flags.DEFINE_string   ('onnx', None, 'Name of the optional onnx file to generate')
flags.DEFINE_string   ('frozen_graph', None, 'Name of the optional frozen graph file to generate')

def export_main(unused_argv, **kwargs):
    """Main function for the export.
    Args:
        unused_argv:                /
        kwargs:                     extra argments
            saved_model_callback:   a function called after saved_model export.
            frozen_graph_callback:  a function called after the frozen graph export.
            onnx_callback:          a function called after the onnx export.
    """
    # Init the export environment
    from export_environment import init_export_environment
    from export_parameters import ExportParameters
    export_parameters = ExportParameters()
    export_parameters.update_values()
    # Check if the export directory is specified
    if (not export_parameters.output_directory):
        print('Export parameters not set. Skipping.')
        return;
    # Check if at least an export operation is defined
    if (not export_parameters.trained_checkpoint_dir and
        not export_parameters.onnx and
        not export_parameters.frozen_graph):
        return
    init_export_environment(export_parameters)
    # Export callback
    def _export_callback(export_fn, export_path):
        """Call export callback function."""
        # Prepare the data  
        class ExportData(object):
            def __init__(self, *args, **kwargs):
                self.path = export_path
                self.cancel = False
                return super().__init__(*args, **kwargs)
        export_data = ExportData()
        if (export_fn in kwargs):
            kwargs[export_fn](export_data)
        return export_data
    # Export the saved_model if it's needed an update
    if (export_parameters.trained_checkpoint_dir):
        # Import the export main function
        from object_detection import exporter_main_v2
        pipeline_config_path = export_parameters.pipeline_config_path
        if (not pipeline_config_path):
            pipeline_config_path = export_parameters.pipeline_config_path = os.path.join(export_parameters.trained_checkpoint_dir, 'pipeline.config')
        export_parameters.update_flags()
        exporter_main_v2.main(unused_argv)
        if (_export_callback('saved_model_callback', os.path.join(export_parameters.output_directory, 'saved_model')).cancel):
            return;
    # Export the frozen model if defined
    frozen_inputs = frozen_outputs = None
    if (export_parameters.frozen_graph):
        from export_frozen_graph import export_frozen_graph
        frozen_inputs, frozen_outputs = export_frozen_graph(export_parameters)
        if (_export_callback('frozen_graph_callback', os.path.join(export_parameters.output_directory, export_parameters.frozen_graph)).cancel):
            return;
    # Export the ONNX if defined
    if (export_parameters.onnx):
        from export_onnx import export_onnx
        export_onnx(export_parameters)
        if (_export_callback('onnx_callback', os.path.join(export_parameters.output_directory, export_parameters.onnx)).cancel):
            return;
    # Export the configuration files
    from export_model_config import export_model_config
    export_model_config(export_parameters, frozen_inputs, frozen_outputs)
    # Callbacks
    if (export_parameters.trained_checkpoint_dir):
        if (_export_callback('saved_model_config_callback', os.path.join(export_parameters.output_directory, 'saved_model.config')).cancel):
            return;
    if (export_parameters.frozen_graph):
        if (_export_callback('frozen_graph_config_callback', os.path.join(export_parameters.output_directory, export_parameters.frozen_graph)).cancel):
            return;
    if (export_parameters.onnx):
        if (_export_callback('onnx_config_callback', os.path.join(export_parameters.output_directory, export_parameters.onnx)).cancel):
            return;

if __name__ == '__main__':
    if (not is_executable()):
        from install_virtual_environment import install_virtual_environment
        install_virtual_environment()
    try:
        # Import of the TensorFlow module
        import tensorflow as tf
        # Allow the ovverride and save the current values of the mandatory flags
        allow_flags_override()
        # Import the module for defining the flags
        from object_detection import exporter_main_v2
        # Validate the hypothetical empty mandatory flags values and call the export main
        for flag in ['pipeline_config_path', 'trained_checkpoint_dir', 'output_directory']:
            flags.FLAGS[flag].validators.clear()
        tf.compat.v1.app.run(export_main)
    except KeyboardInterrupt:
        if (not is_executable()):
            print('Export interrupted by user')
    except SystemExit:
        if (not is_executable()):
            print('Export complete')
    else:
        if (not is_executable()):
            print('Export complete')
