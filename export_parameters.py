import  os

try:    from    base_parameters import BaseParameters
except: pass
try:    from    default_cfg import Cfg
except: pass

class ExportParameters(BaseParameters):
    """ Class holding the model export parameters """
    def __init__(self):
        """ Constructor """
        super().__init__()
        self._pipeline_config_path = os.path.join(self.model_dir, 'pipeline.config')
        self._trained_checkpoint_dir = self.model_dir
        self._output_directory = Cfg.exported_model_dir
        self._onnx = Cfg.exported_onnx
        self._frozen_graph = Cfg.exported_frozen_graph
        self._is_path.extend([
            'pipeline_config_path',
            'trained_checkpoint_dir',
            'output_directory',
            'onnx_path',
            'frozen_graph_path'])
    default = None
    @property
    def pipeline_config_path(self): return self._pipeline_config_path
    @pipeline_config_path.setter
    def pipeline_config_path(self, value): self._pipeline_config_path = value
    @property
    def trained_checkpoint_dir(self): return self._trained_checkpoint_dir
    @trained_checkpoint_dir.setter
    def trained_checkpoint_dir(self, value): self._trained_checkpoint_dir = value
    @property
    def output_directory(self): return self._output_directory
    @output_directory.setter
    def output_directory(self, value): self._output_directory = value
    @property
    def onnx(self): return self._onnx
    @onnx.setter
    def onnx(self, value): self._onnx = value
    @property
    def frozen_graph(self): return self._frozen_graph
    @frozen_graph.setter
    def frozen_graph(self, value): self._frozen_graph = value

ExportParameters.default = ExportParameters.default or ExportParameters()

if __name__ == '__main__':
    prm = ('prm' in locals() and isinstance(prm, ExportParameters) and prm) or ExportParameters.default
    print(prm)
    print('Export parameters configured')
