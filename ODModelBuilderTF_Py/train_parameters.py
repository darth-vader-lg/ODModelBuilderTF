import  os

try:    from    base_parameters import BaseParameters
except: pass
try:    from    default_cfg import Cfg
except: pass

class TrainParameters(BaseParameters):
    """ Class holding the train execution parameters """
    def __init__(self):
        """ Constructor """
        super().__init__()
        self._pre_trained_model_dir = None
        self._pipeline_config_path = os.path.join(self.annotations_dir, 'pipeline.config')
        self._num_train_steps = Cfg.num_train_steps if Cfg.num_train_steps > -1 else None
        self._eval_on_train_data = False
        self._use_tpu = False
        self._tpu_name = None
        self._num_workers = 1
        self._checkpoint_every_n = 1000
        self._record_summaries = True
        self._batch_size = Cfg.batch_size if Cfg.batch_size > 1 else None
        self._tensorboard_port = 8080
        self._is_path.extend([
            'pre_trained_model_dir', 'pipeline_config_path'])
    default = None
    @property
    def pre_trained_model_dir(self): return self._pre_trained_model_dir
    @pre_trained_model_dir.setter
    def pre_trained_model_dir(self, value): self._pre_trained_model_dir = value
    @property
    def pipeline_config_path(self): return self._pipeline_config_path
    @pipeline_config_path.setter
    def pipeline_config_path(self, value): self._pipeline_config_path = value
    @property
    def num_train_steps(self): return self._num_train_steps
    @num_train_steps.setter
    def num_train_steps(self, value): self._num_train_steps = value
    @property
    def eval_on_train_data(self): return self._eval_on_train_data
    @eval_on_train_data.setter
    def eval_on_train_data(self, value): self._eval_on_train_data = value
    @property
    def use_tpu(self): return self._use_tpu
    @use_tpu.setter
    def use_tpu(self, value): self._use_tpu = value
    @property
    def tpu_name(self): return self._tpu_name
    @tpu_name.setter
    def tpu_name(self, value): self._tpu_name = value
    @property
    def num_workers(self): return self._num_workers
    @num_workers.setter
    def num_workers(self, value): self._num_workers = value
    @property
    def checkpoint_every_n(self): return self._checkpoint_every_n
    @checkpoint_every_n.setter
    def checkpoint_every_n(self, value): self._checkpoint_every_n = value
    @property
    def record_summaries(self): return self._record_summaries
    @record_summaries.setter
    def record_summaries(self, value): self._record_summaries = value
    @property
    def batch_size(self): return self._batch_size
    @batch_size.setter
    def batch_size(self, value): self._batch_size = value
    @property
    def tensorboard_port(self): return self._tensorboard_port
    @tensorboard_port.setter
    def tensorboard_port(self, value): self._tensorboard_port = value

TrainParameters.default = TrainParameters.default or TrainParameters()

if __name__ == '__main__':
    prm = ('prm' in locals() and isinstance(prm, TrainParameters) and prm) or TrainParameters.default
    print(prm)
    print('Train parameters configured')
